using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfCaseDueChaserStore(
    IDbContextFactory<PegasusDbContext> contextFactory)
    : ICaseDueChaserQueries, ICaseDueChaserStore
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<DueCaseChaser>> GetDueAsync(
        DateTimeOffset asOfUtc,
        int maximumResults,
        CancellationToken cancellationToken)
    {
        if (maximumResults is < 1 or > RunDueChasers.MaximumBatchSize)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumResults));
        }

        asOfUtc = asOfUtc.ToUniversalTime();
        var asOfUtcTicks = asOfUtc.UtcDateTime.Ticks;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.CaseDueWork
            .AsNoTracking()
            .Where(item =>
                item.State == nameof(CaseDueWorkState.Scheduled)
                && item.NextChaseAtUtc != null
                && item.NextChaseAtUtcTicks != null
                && item.NextChaseAtUtcTicks <= asOfUtcTicks
                && item.Workflow.State == nameof(CaseLifecycleState.NotReady)
                && item.Workflow.ArchivedAtUtc == null)
            .OrderBy(item => item.NextChaseAtUtcTicks)
            .ThenBy(item => item.CaseId)
            .Select(item => new DueCaseChaser(
                item.CaseId,
                item.Version,
                item.Workflow.Case.Reference,
                item.MissingMaterialReason,
                item.NextChaseAtUtc!.Value,
                context.Set<RequestUploadLinkEntity>()
                    .Where(link =>
                        link.CaseId == item.CaseId
                        && link.Status == RequestUploadStatus.Active
                        && link.RevokedAtUtc == null
                        && link.CreatedAtUtc <= asOfUtc
                        && link.ExpiresAtUtc > asOfUtc)
                    .OrderByDescending(link => link.CreatedAtUtc)
                    .ThenByDescending(link => link.Id)
                    .Select(link => (Guid?)link.Id)
                    .FirstOrDefault()))
            .Take(maximumResults)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<GeneratedCaseChaser?> GetLatestAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<CaseDueChaserEntity>()
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .OrderByDescending(item => item.ScheduledAtUtc)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<DueChaserClaimResult> TryClaimAndRecordAsync(
        DueChaserTransition transition,
        CancellationToken cancellationToken)
    {
        Validate(transition);
        var requestHash = RequestHash(transition);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken);

        var replay = await context.Set<CaseDueChaserEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.OperationKey == transition.OperationKey,
                cancellationToken);
        if (replay is not null)
        {
            return Replay(replay, transition, requestHash);
        }

        var dueWork = await context.CaseDueWork
            .Include(item => item.Workflow)
            .SingleOrDefaultAsync(item => item.CaseId == transition.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{transition.CaseId}' has no due work.");
        if (dueWork.Version != transition.ExpectedDueWorkVersion
            || dueWork.State != nameof(CaseDueWorkState.Scheduled)
            || dueWork.NextChaseAtUtc != transition.ScheduledAtUtc
            || dueWork.Workflow.State != nameof(CaseLifecycleState.NotReady)
            || dueWork.Workflow.ArchivedAtUtc is not null)
        {
            return new(DueChaserClaimOutcome.Superseded, null);
        }

        RequestUploadLinkEntity? requestLink = null;
        if (transition.RequestLinkReference is { } requestLinkReference)
        {
            requestLink = await context.Set<RequestUploadLinkEntity>()
                .SingleOrDefaultAsync(
                    item => item.Id == requestLinkReference
                        && item.CaseId == transition.CaseId
                        && item.Status == RequestUploadStatus.Active
                        && item.RevokedAtUtc == null
                        && item.CreatedAtUtc <= transition.GeneratedAtUtc
                        && item.ExpiresAtUtc > transition.GeneratedAtUtc,
                    cancellationToken);
            if (requestLink is null)
            {
                return new(DueChaserClaimOutcome.Superseded, null);
            }
        }

        var beforeVersion = dueWork.Version;
        var beforeJson = JsonSerializer.Serialize(
            DueWorkHistoryValue.Before(dueWork),
            SerializerOptions);
        dueWork.NextChaseAtUtc = transition.NextChaseAtUtc;
        dueWork.Version = checked(dueWork.Version + 1);

        var entity = new CaseDueChaserEntity
        {
            Id = transition.Id,
            CaseId = transition.CaseId,
            DueWork = dueWork,
            ScheduledAtUtc = transition.ScheduledAtUtc,
            GeneratedAtUtc = transition.GeneratedAtUtc,
            NextChaseAtUtc = transition.NextChaseAtUtc,
            CopyableText = transition.CopyableText,
            RequestLinkReference = transition.RequestLinkReference,
            RequestLink = requestLink,
            RequestLinkPurpose = transition.RequestLinkPurpose,
            OperationKey = transition.OperationKey,
            RequestHash = requestHash,
            BeforeDueWorkVersion = beforeVersion,
            AfterDueWorkVersion = dueWork.Version
        };
        context.Set<CaseDueChaserEntity>().Add(entity);
        context.ActionHistory.Add(new ActionHistoryEntity
        {
            Id = Guid.NewGuid(),
            AggregateType = "case_due_work",
            AggregateId = transition.CaseId.ToString("D"),
            EventKind = "due_chaser_generated",
            ActorKind = transition.Actor.Kind.ToString(),
            ActorSubjectId = transition.Actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(
                transition.Actor.Roles.OrderBy(role => role),
                SerializerOptions),
            OccurredAtUtc = transition.GeneratedAtUtc,
            Outcome = "Succeeded",
            CorrelationId = transition.OperationKey,
            Reason = "Scheduled missing-material chaser draft generated for staff copy.",
            BeforeJson = beforeJson,
            AfterJson = JsonSerializer.Serialize(
                DueWorkHistoryValue.After(dueWork, entity),
                SerializerOptions),
            PolicyVersion = CaseChaseSchedule.PolicyIdentity
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(DueChaserClaimOutcome.Recorded, Map(entity));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return await FindReplayAsync(transition, requestHash)
                ?? new(DueChaserClaimOutcome.Superseded, null);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            var concurrentReplay = await FindReplayAsync(transition, requestHash);
            if (concurrentReplay is not null)
            {
                return concurrentReplay;
            }

            throw;
        }
    }

    private async Task<DueChaserClaimResult?> FindReplayAsync(
        DueChaserTransition transition,
        string requestHash)
    {
        await using var verification = await contextFactory.CreateDbContextAsync();
        var replay = await verification.Set<CaseDueChaserEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationKey == transition.OperationKey);
        return replay is null ? null : Replay(replay, transition, requestHash);
    }

    private static DueChaserClaimResult Replay(
        CaseDueChaserEntity replay,
        DueChaserTransition transition,
        string requestHash)
    {
        if (replay.CaseId != transition.CaseId
            || replay.ScheduledAtUtc != transition.ScheduledAtUtc
            || !FixedTimeEquals(replay.RequestHash, requestHash))
        {
            throw new CaseOperationConflictException(
                transition.CaseId,
                transition.OperationKey);
        }

        return new(DueChaserClaimOutcome.Replay, Map(replay));
    }

    private static void Validate(DueChaserTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        if (transition.Id == Guid.Empty || transition.CaseId == Guid.Empty)
        {
            throw new ArgumentException(
                "The due-chaser transition and case identifiers are required.",
                nameof(transition));
        }
        if (transition.ExpectedDueWorkVersion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(transition));
        }
        if (transition.GeneratedAtUtc < transition.ScheduledAtUtc)
        {
            throw new ArgumentException(
                "A chaser cannot be generated before its scheduled occurrence.",
                nameof(transition));
        }
        if (transition.NextChaseAtUtc != CaseChaseSchedule.NextChaseAt(transition.ScheduledAtUtc))
        {
            throw new ArgumentException(
                "The next chaser must follow the Europe/London seven-calendar-day schedule.",
                nameof(transition));
        }
        if (string.IsNullOrWhiteSpace(transition.CopyableText)
            || transition.CopyableText.Length > 2000)
        {
            throw new ArgumentException(
                "Copyable chaser text must contain between 1 and 2000 characters.",
                nameof(transition));
        }
        var expectedOperationKey =
            $"due-chaser:{transition.CaseId:N}:{transition.ScheduledAtUtc.UtcDateTime.Ticks}";
        if (!string.Equals(
                transition.OperationKey,
                expectedOperationKey,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The due-chaser operation key must identify the exact scheduled occurrence.",
                nameof(transition));
        }
        if (transition.RequestLinkReference == Guid.Empty
            || (transition.RequestLinkReference is null) != (transition.RequestLinkPurpose is null)
            || (transition.RequestLinkPurpose is not null
                && transition.RequestLinkPurpose != RunDueChasers.MissingMaterialRequestLinkPurpose))
        {
            throw new ArgumentException(
                "A request-link reference must use the missing-material purpose.",
                nameof(transition));
        }
        if (transition.Actor.Kind != ActorKind.SystemWorker
            || transition.Actor.SubjectId != RunDueChasers.WorkerSubjectId)
        {
            throw new StaffAuthorizationException(StaffAccessRight.ExecuteSystemWork);
        }
    }

    private static GeneratedCaseChaser Map(CaseDueChaserEntity entity) => new(
        entity.Id,
        entity.CaseId,
        entity.ScheduledAtUtc,
        entity.GeneratedAtUtc,
        entity.NextChaseAtUtc,
        entity.CopyableText,
        entity.RequestLinkReference,
        entity.RequestLinkPurpose,
        entity.AfterDueWorkVersion);

    private static string RequestHash(DueChaserTransition transition) => Hash(
        JsonSerializer.Serialize(
            new DueChaserRequestValue(
                transition.CaseId,
                transition.ExpectedDueWorkVersion,
                transition.ScheduledAtUtc,
                transition.NextChaseAtUtc,
                transition.CopyableText,
                transition.RequestLinkReference,
                transition.RequestLinkPurpose,
                transition.OperationKey,
                transition.Actor.Kind.ToString(),
                transition.Actor.SubjectId),
            SerializerOptions));

    private static bool FixedTimeEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(left),
            Convert.FromHexString(right));

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    private sealed record DueChaserRequestValue(
        Guid CaseId,
        long ExpectedDueWorkVersion,
        DateTimeOffset ScheduledAtUtc,
        DateTimeOffset NextChaseAtUtc,
        string CopyableText,
        Guid? RequestLinkReference,
        string? RequestLinkPurpose,
        string OperationKey,
        string ActorKind,
        string ActorSubjectId);

    private sealed record DueWorkHistoryValue(
        string State,
        DateTimeOffset? NextChaseAtUtc,
        long DueWorkVersion,
        Guid? ChaserId,
        DateTimeOffset? ScheduledAtUtc,
        Guid? RequestLinkReference,
        string? RequestLinkPurpose)
    {
        public static DueWorkHistoryValue Before(CaseDueWorkEntity dueWork) => new(
            dueWork.State,
            dueWork.NextChaseAtUtc,
            dueWork.Version,
            null,
            null,
            null,
            null);

        public static DueWorkHistoryValue After(
            CaseDueWorkEntity dueWork,
            CaseDueChaserEntity chaser) => new(
                dueWork.State,
                dueWork.NextChaseAtUtc,
                dueWork.Version,
                chaser.Id,
                chaser.ScheduledAtUtc,
                chaser.RequestLinkReference,
                chaser.RequestLinkPurpose);
    }
}
