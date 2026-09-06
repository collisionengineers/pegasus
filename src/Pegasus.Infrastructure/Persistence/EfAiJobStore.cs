using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Persists the AI job ledger (ADR-0035) with the AI-09 mechanics: creation
/// is idempotent per operation key (a replay with different inputs is a
/// conflict), transitions are optimistic on Version and validated against
/// the Core state graph, and every change writes permanent attributable
/// action history correlated by the job identifier. A lapsed lease is
/// applied when the row is next touched — recorded as <c>ai_job_expired</c>
/// before the new claim — and read as Queued until then.
/// </summary>
public sealed class EfAiJobStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : IAiJobStore, IAiJobQueries
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    internal const string AggregateType = "ai_job";

    public async Task<AiJobRecord> CreateAsync(NewAiJob job, CancellationToken cancellationToken)
    {
        AiJobPolicy.ValidateNew(job);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var requestHash = CreateHash(job);
        var existing = await context.AiJobs.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationKey == job.OperationKey, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Operation '{job.OperationKey}' already created a different AI job.");
            }

            return Map(existing, UtcNow());
        }

        var now = UtcNow();
        var entity = new AiJobEntity
        {
            JobId = Guid.NewGuid(),
            Kind = job.Kind.ToString(),
            SubjectKind = job.SubjectKind.ToString(),
            SubjectId = job.SubjectId,
            SubjectReference = job.SubjectReference.Trim(),
            Instruction = job.Instruction.Trim(),
            TargetPercentOfEngineerValue = job.TargetPercentOfEngineerValue,
            EngineerValueAtSend = job.EngineerValueAtSend,
            State = nameof(AiJobState.Queued),
            OperationKey = job.OperationKey,
            RequestHash = requestHash,
            CreatedByKind = job.Actor.Kind.ToString(),
            CreatedBy = job.Actor.SubjectId,
            CreatedAtUtc = now,
            ExpiresAtUtc = now + job.Expiry,
            Version = 0
        };
        context.AiJobs.Add(entity);
        AddHistory(context, entity, "ai_job_created", job.Actor, job.OperationKey, reason: null, now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity, now);
    }

    public async Task<AiJobRecord?> GetAsync(Guid jobId, CancellationToken cancellationToken)
    {
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException("A job identifier is required.", nameof(jobId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.AiJobs.AsNoTracking()
            .SingleOrDefaultAsync(item => item.JobId == jobId, cancellationToken);
        return entity is null ? null : Map(entity, UtcNow());
    }

    public async Task<AiJobRecord> TransitionAsync(
        AiJobTransition transition,
        CancellationToken cancellationToken)
    {
        AiJobPolicy.ValidateTransition(transition);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var entity = await context.AiJobs
            .SingleOrDefaultAsync(item => item.JobId == transition.JobId, cancellationToken)
            ?? throw new KeyNotFoundException("The AI job was not found.");
        var now = UtcNow();
        if (string.Equals(entity.LastOperationKey, transition.OperationKey, StringComparison.Ordinal))
        {
            // Idempotent replay of a transition already applied.
            return Map(entity, now);
        }

        var persisted = Parse<AiJobState>(entity.State);
        var current = AiJobPolicy.EffectiveState(
            persisted,
            entity.ExpiresAtUtc,
            entity.LeaseExpiresAtUtc,
            now);
        if (current == transition.TargetState && transition.TargetState != AiJobState.Taken)
        {
            return Map(entity, now);
        }
        if (current == AiJobState.Taken
            && transition.TargetState == AiJobState.Taken
            && transition.ProgressNote is null)
        {
            // A take is a claim on a queued job; a held job is renewed
            // through progress, never taken again.
            throw new InvalidOperationException("The AI job is already taken.");
        }
        if (!AiJobPolicy.IsLegalTransition(current, transition.TargetState))
        {
            throw new InvalidOperationException(
                $"An AI job cannot move from {current} to {transition.TargetState}.");
        }
        if (entity.Version != transition.ExpectedVersion)
        {
            throw new InvalidOperationException(
                "The AI job changed concurrently; reload and retry.");
        }
        if (current == AiJobState.Taken
            && transition.Actor.Kind == ActorKind.Automation
            && !string.Equals(entity.TakenBy, transition.Actor.SubjectId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The AI job is taken by another client.");
        }

        if (persisted == AiJobState.Taken && current == AiJobState.Queued)
        {
            // The lapsed claim is recorded as its own event before the new
            // transition, so nothing is erased.
            AddHistory(
                context,
                entity,
                "ai_job_expired",
                transition.Actor,
                transition.OperationKey,
                $"The lease held by {entity.TakenBy} expired.",
                now);
            entity.State = current.ToString();
            entity.Version++;
            entity.TakenBy = null;
            entity.TakenAtUtc = null;
            entity.LeaseExpiresAtUtc = null;
        }

        entity.State = transition.TargetState.ToString();
        entity.Version++;
        entity.LastOperationKey = transition.OperationKey;
        string eventKind;
        switch (transition.TargetState)
        {
            case AiJobState.Taken when current == AiJobState.Queued:
                entity.TakenBy = transition.Actor.SubjectId;
                entity.TakenAtUtc = now;
                entity.LeaseExpiresAtUtc = transition.LeaseExpiresAtUtc;
                entity.ProgressNote = null;
                eventKind = "ai_job_taken";
                break;
            case AiJobState.Taken:
                entity.LeaseExpiresAtUtc = transition.LeaseExpiresAtUtc;
                entity.ProgressNote = transition.ProgressNote?.Trim();
                eventKind = "ai_job_progress";
                break;
            case AiJobState.Queued:
                entity.TakenBy = null;
                entity.TakenAtUtc = null;
                entity.LeaseExpiresAtUtc = null;
                eventKind = "ai_job_released";
                break;
            case AiJobState.DraftReady:
                var expectedResultKind = AiJobPolicy.ResultKindFor(Parse<AiJobKind>(entity.Kind));
                if (transition.Result!.Kind != expectedResultKind)
                {
                    throw new InvalidOperationException(
                        $"A {entity.Kind} job completes with a {expectedResultKind} result.");
                }
                entity.ResultKind = transition.Result.Kind.ToString();
                entity.ResultReference = transition.Result.Reference?.Trim();
                entity.ResultText = transition.Result.Text?.Trim();
                entity.LeaseExpiresAtUtc = null;
                eventKind = "ai_job_draft_ready";
                break;
            default:
                entity.ClosedAtUtc = now;
                entity.ClosureReason = transition.Reason?.Trim();
                entity.LeaseExpiresAtUtc = null;
                eventKind = "ai_job_" + transition.TargetState.ToString().ToLowerInvariant();
                break;
        }

        AddHistory(
            context,
            entity,
            eventKind,
            transition.Actor,
            transition.OperationKey,
            transition.Reason?.Trim() ?? transition.ProgressNote?.Trim(),
            now);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(
                "The AI job changed concurrently; reload and retry.");
        }

        return Map(entity, now);
    }

    public async Task<IReadOnlyList<AiJobRecord>> ListOpenAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = UtcNow();
        var rows = await OpenRows(context)
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.JobId)
            .ToListAsync(cancellationToken);
        return rows.Select(row => Map(row, now)).ToArray();
    }

    public async Task<AiJobQueryPage> ListOpenPageAsync(
        AiJobKind? kind,
        string grantId,
        DateTimeOffset? afterCreatedAtUtc,
        Guid? afterJobId,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(grantId);
        if ((afterCreatedAtUtc is null) != (afterJobId is null))
        {
            throw new ArgumentException("Both AI job cursor values are required together.");
        }
        if (limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = UtcNow();
        var query = OpenRows(context)
            .Where(item => item.ExpiresAtUtc > now)
            .Where(item => item.State == nameof(AiJobState.Queued)
                || item.LeaseExpiresAtUtc <= now
                || item.TakenBy == grantId);
        if (kind is not null)
        {
            var kindName = kind.Value.ToString();
            query = query.Where(item => item.Kind == kindName);
        }
        if (afterCreatedAtUtc is not null)
        {
            var created = afterCreatedAtUtc.Value;
            var id = afterJobId!.Value;
            query = query.Where(item => item.CreatedAtUtc > created
                || (item.CreatedAtUtc == created && item.JobId.CompareTo(id) > 0));
        }
        var rows = await query.OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.JobId)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        return new(rows.Take(limit).Select(row => Map(row, now)).ToArray(), rows.Count > limit);
    }

    public async Task<IReadOnlyList<AiJobRecord>> ListForSubjectAsync(
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        if (subjectId == Guid.Empty)
        {
            throw new ArgumentException("A subject identifier is required.", nameof(subjectId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = UtcNow();
        var rows = await context.AiJobs.AsNoTracking()
            .Where(item => item.SubjectId == subjectId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.JobId)
            .ToListAsync(cancellationToken);
        return rows.Select(row => Map(row, now)).ToArray();
    }

    public async Task<IReadOnlyList<AiJobRecord>> ListRecentAsync(
        int max,
        CancellationToken cancellationToken)
    {
        if (max is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(max), "Between 1 and 500 jobs may be listed.");
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = UtcNow();
        var rows = await context.AiJobs.AsNoTracking()
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.JobId)
            .Take(max)
            .ToListAsync(cancellationToken);
        return rows.Select(row => Map(row, now)).ToArray();
    }

    public async Task<AiJobCounts> GetCountsAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = UtcNow();
        var open = await OpenRows(context)
            .Select(item => new { item.State, item.ExpiresAtUtc, item.LeaseExpiresAtUtc })
            .ToListAsync(cancellationToken);
        var active = open.Count(item =>
            !AiJobStates.IsTerminal(AiJobPolicy.EffectiveState(
                Parse<AiJobState>(item.State),
                item.ExpiresAtUtc,
                item.LeaseExpiresAtUtc,
                now)));
        var failed = await context.AiJobs.AsNoTracking()
            .CountAsync(item => item.State == nameof(AiJobState.Failed), cancellationToken);
        return new(active, failed);
    }

    private static IQueryable<AiJobEntity> OpenRows(PegasusDbContext context) =>
        context.AiJobs.AsNoTracking()
            .Where(item =>
                item.State == nameof(AiJobState.Queued)
                || item.State == nameof(AiJobState.Taken)
                || item.State == nameof(AiJobState.DraftReady));

    internal static void AddHistory(
        PegasusDbContext context,
        AiJobEntity entity,
        string eventKind,
        ActionActor actor,
        string operationKey,
        string? reason,
        DateTimeOffset occurredAtUtc) =>
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = AggregateType,
            AggregateId = entity.JobId.ToString("D"),
            EventKind = eventKind,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(
                actor.Roles.OrderBy(role => role),
                JsonOptions),
            OccurredAtUtc = occurredAtUtc,
            Outcome = "Succeeded",
            CorrelationId = operationKey.Trim(),
            Reason = reason,
            AfterJson = JsonSerializer.Serialize(
                new
                {
                    entity.JobId,
                    entity.Kind,
                    entity.SubjectKind,
                    CaseId = entity.SubjectKind == nameof(AiJobSubjectKind.Case) ? entity.SubjectId : null,
                    entity.SubjectReference,
                    entity.State,
                    entity.TakenBy,
                    entity.ResultKind,
                    entity.ResultReference
                },
                JsonOptions)
        });

    internal static AiJobRecord Map(AiJobEntity entity, DateTimeOffset now) => new(
        entity.JobId,
        Parse<AiJobKind>(entity.Kind),
        Parse<AiJobSubjectKind>(entity.SubjectKind),
        entity.SubjectId,
        entity.SubjectReference,
        entity.Instruction,
        entity.TargetPercentOfEngineerValue,
        entity.EngineerValueAtSend,
        AiJobPolicy.EffectiveState(
            Parse<AiJobState>(entity.State),
            entity.ExpiresAtUtc,
            entity.LeaseExpiresAtUtc,
            now),
        Parse<ActorKind>(entity.CreatedByKind),
        entity.CreatedBy,
        entity.CreatedAtUtc,
        entity.ExpiresAtUtc,
        entity.TakenBy,
        entity.TakenAtUtc,
        entity.LeaseExpiresAtUtc,
        entity.ProgressNote,
        entity.ResultKind is null ? null : Parse<AiJobResultKind>(entity.ResultKind),
        entity.ResultReference,
        entity.ResultText,
        entity.ClosedAtUtc,
        entity.ClosureReason,
        entity.Version);

    private static TEnum Parse<TEnum>(string value) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new InvalidDataException(
                $"Unknown persisted AI job {typeof(TEnum).Name} '{value}'.");

    private DateTimeOffset UtcNow()
    {
        var now = timeProvider.GetUtcNow();
        return now.Offset == TimeSpan.Zero ? now : now.ToUniversalTime();
    }

    private static string CreateHash(NewAiJob job)
    {
        var material = JsonSerializer.Serialize(new
        {
            Command = "create_ai_job",
            Kind = job.Kind.ToString(),
            SubjectKind = job.SubjectKind.ToString(),
            job.SubjectId,
            SubjectReference = job.SubjectReference.Trim(),
            Instruction = job.Instruction.Trim(),
            job.TargetPercentOfEngineerValue,
            job.EngineerValueAtSend,
            ActorKind = job.Actor.Kind.ToString(),
            ActorSubjectId = job.Actor.SubjectId,
            job.OperationKey,
            Expiry = job.Expiry.Ticks
        }, JsonOptions);
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }
}
