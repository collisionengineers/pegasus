using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfTriageStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider? timeProvider = null) : ITriageStore
{
    private static readonly TimeSpan EditLeaseLifetime = TimeSpan.FromMinutes(5);

    public async Task<TriageRecord> CreateAsync(
        CreateTriageFromIntakeRequest request,
        CancellationToken cancellationToken)
    {
        ValidateCreate(request);
        var operationKey = request.OperationKey.Trim();
        var actor = request.Actor.Trim();
        var vrm = request.NormalizedVehicleRegistration.Trim().ToUpperInvariant();
        var sourceChannel = ToCode(request.Origin.SourceIdentity.Channel);
        var sourceToken = request.Origin.SourceIdentity.ExternalReceiptToken.Trim();
        var sourceHash = request.Origin.SourceHash.ToLowerInvariant();
        var requestHash = Hash($"create|{request.Origin.ReceiptId:N}|{sourceChannel}|{sourceToken}|{sourceHash}|{request.Origin.EvaluationRevisionId:N}|{vrm}|{actor}");

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var replay = await FindReplayAsync(context, operationKey, cancellationToken);
        if (replay is not null)
        {
            EnsureReplay(replay, "triage_created", requestHash);
            return await MapReplayAsync(context, replay, cancellationToken);
        }

        var existing = await context.Triage.AsNoTracking().SingleOrDefaultAsync(
            item => item.OriginReceiptId == request.Origin.ReceiptId
                || (item.SourceChannel == sourceChannel && item.ExternalReceiptToken == sourceToken),
            cancellationToken);
        if (existing is not null)
        {
            if (existing.OriginReceiptId != request.Origin.ReceiptId
                || existing.SourceChannel != sourceChannel
                || existing.ExternalReceiptToken != sourceToken
                || !string.Equals(existing.SourceHash, sourceHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new IntakeSourceIdentityConflictException();
            }

            return Map(existing);
        }

        var receipt = await context.IntakeReceipts.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == request.Origin.ReceiptId,
            cancellationToken) ?? throw new InvalidOperationException("The originating intake receipt does not exist.");
        if (receipt.SourceChannel != sourceChannel
            || receipt.ExternalReceiptToken != sourceToken
            || !string.Equals(receipt.SourceHash, sourceHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new IntakeSourceIdentityConflictException();
        }

        var evaluationExists = await context.IntakeEvaluations.AsNoTracking().AnyAsync(
            item => item.Id == request.Origin.EvaluationRevisionId
                && item.ProcessedReceiptId == request.Origin.ReceiptId,
            cancellationToken);
        if (!evaluationExists)
        {
            throw new InvalidOperationException("The creating intake evaluation revision does not exist for the receipt.");
        }

        var now = UtcNow();
        var entity = new TriageEntity
        {
            Id = Guid.NewGuid(),
            OriginReceiptId = request.Origin.ReceiptId,
            SourceChannel = sourceChannel,
            ExternalReceiptToken = sourceToken,
            SourceHash = sourceHash,
            EvaluationRevisionId = request.Origin.EvaluationRevisionId,
            NormalizedVehicleRegistration = vrm,
            State = ToCode(TriageState.Open),
            CreatedAtUtc = now,
            CreationOperationKey = operationKey,
            Version = 0,
            RowVersion = []
        };
        context.Triage.Add(entity);
        AppendHistory(context, entity, "triage_created", actor, operationKey, "Created from evaluated intake", requestHash, -1);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<TriageRecord> AssignAsync(AssignTriageRequest request, CancellationToken cancellationToken)
    {
        ValidateMutation(request.TriageId, request.ExpectedVersion, request.Actor, request.OperationKey, request.Reason, request.EditLeaseToken);
        if (request.AssigneeId == Guid.Empty)
        {
            throw new ArgumentException("A valid assignee is required.", nameof(request));
        }

        return await MutateAsync(
            request.TriageId,
            request.ExpectedVersion,
            request.Actor,
            request.OperationKey,
            request.Reason,
            request.EditLeaseToken,
            "triage_assigned",
            Hash($"assign|{request.TriageId:N}|{request.ExpectedVersion}|{request.AssigneeId:N}|{request.Actor.Trim()}|{request.Reason.Trim()}"),
            item =>
            {
                if (item.AssigneeId == request.AssigneeId)
                {
                    throw new InvalidOperationException("The Triage record is already assigned to that staff member.");
                }
                item.AssigneeId = request.AssigneeId;
            },
            cancellationToken);
    }

    public Task<TriageRecord> UnassignAsync(TriageMutationRequest request, CancellationToken cancellationToken) =>
        MutateStateNeutralAsync(request, "triage_unassigned", item => item.AssigneeId = null, cancellationToken);

    public Task<TriageRecord> RecordFindingAsync(
        RecordTriageFindingRequest request,
        CancellationToken cancellationToken) => RecordFindingAsync(request, false, cancellationToken);

    public Task<TriageRecord> SupersedeFindingAsync(
        RecordTriageFindingRequest request,
        CancellationToken cancellationToken) => RecordFindingAsync(request, true, cancellationToken);

    public async Task LinkResponseEvidenceAsync(
        TriageResponseEvidenceLinkRequest request,
        CancellationToken cancellationToken)
    {
        ValidateMutation(request.TriageId, request.ExpectedVersion, request.Actor, request.OperationKey, request.Reason, request.EditLeaseToken);
        if (request.SentEvidenceId == Guid.Empty)
        {
            throw new ArgumentException("Sent response evidence is required.", nameof(request));
        }

        var requestHash = Hash($"link_response|{request.TriageId:N}|{request.ExpectedVersion}|{request.SentEvidenceId:N}|{request.Actor.Trim()}|{request.Reason.Trim()}");
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await FindReplayAsync(context, request.OperationKey.Trim(), cancellationToken);
        if (replay is not null)
        {
            EnsureReplay(replay, "triage_response_linked", requestHash, request.TriageId);
            return;
        }

        var triage = await LoadForMutationAsync(context, request.TriageId, request.ExpectedVersion, request.Actor, request.EditLeaseToken, cancellationToken);
        var sent = await context.SentEmailEvidence.Include(item => item.Response).SingleOrDefaultAsync(
            item => item.Id == request.SentEvidenceId,
            cancellationToken) ?? throw new InvalidOperationException("The Sent evidence does not exist.");
        if (sent.TriageId != triage.Id || sent.Response is null)
        {
            throw new InvalidOperationException("Only exact replied Sent evidence belonging to this Triage can be linked.");
        }

        if (await context.TriageResponseEvidenceLinks.AnyAsync(
                item => item.TriageId == triage.Id && item.SentEvidenceId == sent.Id,
                cancellationToken))
        {
            throw new InvalidOperationException("The response evidence is already linked.");
        }

        context.TriageResponseEvidenceLinks.Add(new()
        {
            TriageId = triage.Id,
            Triage = triage,
            SentEvidenceId = sent.Id,
            SentEvidence = sent,
            Actor = request.Actor.Trim(),
            OperationKey = request.OperationKey.Trim(),
            Reason = request.Reason.Trim(),
            LinkedAtUtc = UtcNow()
        });
        AppendHistory(context, triage, "triage_response_linked", request.Actor.Trim(), request.OperationKey.Trim(), request.Reason.Trim(), requestHash);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UnlinkResponseEvidenceAsync(
        TriageResponseEvidenceLinkRequest request,
        CancellationToken cancellationToken)
    {
        ValidateMutation(request.TriageId, request.ExpectedVersion, request.Actor, request.OperationKey, request.Reason, request.EditLeaseToken);
        var requestHash = Hash($"unlink_response|{request.TriageId:N}|{request.ExpectedVersion}|{request.SentEvidenceId:N}|{request.Actor.Trim()}|{request.Reason.Trim()}");
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await FindReplayAsync(context, request.OperationKey.Trim(), cancellationToken);
        if (replay is not null)
        {
            EnsureReplay(replay, "triage_response_unlinked", requestHash, request.TriageId);
            return;
        }

        var triage = await LoadForMutationAsync(context, request.TriageId, request.ExpectedVersion, request.Actor, request.EditLeaseToken, cancellationToken);
        var link = await context.TriageResponseEvidenceLinks.SingleOrDefaultAsync(
            item => item.TriageId == triage.Id && item.SentEvidenceId == request.SentEvidenceId,
            cancellationToken) ?? throw new InvalidOperationException("The response evidence is not linked.");
        context.TriageResponseEvidenceLinks.Remove(link);
        AppendHistory(context, triage, "triage_response_unlinked", request.Actor.Trim(), request.OperationKey.Trim(), request.Reason.Trim(), requestHash);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task<TriageRecord> ChangeStateAsync(
        TriageMutationRequest request,
        TriageState targetState,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(targetState))
        {
            throw new ArgumentOutOfRangeException(nameof(targetState));
        }

        return MutateAsync(
            request.TriageId,
            request.ExpectedVersion,
            request.Actor,
            request.OperationKey,
            request.Reason,
            request.EditLeaseToken,
            $"triage_state_{ToCode(targetState)}",
            Hash($"state|{request.TriageId:N}|{request.ExpectedVersion}|{ToCode(targetState)}|{request.Actor.Trim()}|{request.Reason.Trim()}"),
            item => item.State = ToCode(targetState),
            cancellationToken);
    }

    public Task LinkCaseAsync(TriageCaseLinkRequest request, CancellationToken cancellationToken) =>
        ChangeCaseLinkAsync(request, true, cancellationToken);

    public Task UnlinkCaseAsync(TriageCaseLinkRequest request, CancellationToken cancellationToken) =>
        ChangeCaseLinkAsync(request, false, cancellationToken);

    public async Task<TriageEditLease> ClaimAsync(
        ClaimTriageEditLeaseRequest request,
        CancellationToken cancellationToken)
    {
        ValidateIdentityAndOperation(request.TriageId, request.Actor, request.OperationKey);
        if (request.ExpectedVersion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var triage = await context.Triage.SingleOrDefaultAsync(item => item.Id == request.TriageId, cancellationToken)
            ?? throw new KeyNotFoundException($"Triage '{request.TriageId}' does not exist.");
        EnsureVersion(triage, request.ExpectedVersion);
        var now = UtcNow();
        if (triage.EditLeaseExpiresAtUtc > now)
        {
            throw new TriageEditLeaseConflictException(triage.Id);
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        triage.EditLeaseTokenHash = Hash(token);
        triage.EditLeaseHolder = request.Actor.Trim();
        triage.EditLeaseOperationKey = request.OperationKey.Trim();
        triage.EditLeaseExpiresAtUtc = now.Add(EditLeaseLifetime);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(triage.Id, token, triage.EditLeaseHolder, triage.Version, triage.EditLeaseExpiresAtUtc.Value);
    }

    public async Task<TriageEditLease> RenewAsync(
        RenewTriageEditLeaseRequest request,
        CancellationToken cancellationToken)
    {
        ValidateLeaseRequest(request.TriageId, request.ExpectedVersion, request.Actor, request.LeaseToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var triage = await context.Triage.SingleOrDefaultAsync(item => item.Id == request.TriageId, cancellationToken)
            ?? throw new KeyNotFoundException($"Triage '{request.TriageId}' does not exist.");
        EnsureVersion(triage, request.ExpectedVersion);
        EnsureLiveLease(triage, request.Actor, request.LeaseToken);
        triage.EditLeaseExpiresAtUtc = UtcNow().Add(EditLeaseLifetime);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(triage.Id, request.LeaseToken, triage.EditLeaseHolder!, triage.Version, triage.EditLeaseExpiresAtUtc.Value);
    }

    public async Task ReleaseAsync(ReleaseTriageEditLeaseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateIdentity(request.TriageId, request.Actor);
        ValidateLeaseToken(request.LeaseToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var triage = await context.Triage.SingleOrDefaultAsync(item => item.Id == request.TriageId, cancellationToken)
            ?? throw new KeyNotFoundException($"Triage '{request.TriageId}' does not exist.");
        EnsureLiveLease(triage, request.Actor, request.LeaseToken);
        triage.EditLeaseTokenHash = null;
        triage.EditLeaseHolder = null;
        triage.EditLeaseOperationKey = null;
        triage.EditLeaseExpiresAtUtc = null;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TriageSummary>> ListAsync(TriageState? state, CancellationToken cancellationToken)
    {
        if (state is not null && !Enum.IsDefined(state.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.Triage.AsNoTracking();
        if (state is not null)
        {
            var stateCode = ToCode(state.Value);
            query = query.Where(item => item.State == stateCode);
        }

        var rows = await query.OrderByDescending(item => item.CreatedAtUtc).ThenBy(item => item.Id).ToListAsync(cancellationToken);
        return rows.Select(item => new TriageSummary(
            item.Id,
            item.NormalizedVehicleRegistration,
            ParseState(item.State),
            item.AssigneeId,
            item.LinkedCaseId,
            item.CreatedAtUtc,
            item.Version)).ToArray();
    }

    public async Task<TriageDetail?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A Triage identity is required.", nameof(id));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Triage.AsNoTracking()
            .Include(item => item.Findings)
            .Include(item => item.ResponseEvidenceLinks)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        return new(
            Map(entity),
            entity.CreatedAtUtc,
            entity.Findings.OrderBy(item => item.RecordedAtUtc).ThenBy(item => item.Id).Select(Map).ToArray(),
            entity.ResponseEvidenceLinks.OrderBy(item => item.LinkedAtUtc).ThenBy(item => item.SentEvidenceId).Select(Map).ToArray());
    }

    private async Task<TriageRecord> RecordFindingAsync(
        RecordTriageFindingRequest request,
        bool superseding,
        CancellationToken cancellationToken)
    {
        ValidateMutation(request.TriageId, request.ExpectedVersion, request.Actor, request.OperationKey, request.Reason, request.EditLeaseToken);
        if (request.Roadworthiness is null && request.Assessment is null)
        {
            throw new ArgumentException("A Triage finding must record Roadworthiness, Assessment, or both.", nameof(request));
        }

        if ((request.Roadworthiness is { } roadworthiness && !Enum.IsDefined(roadworthiness))
            || (request.Assessment is { } assessment && !Enum.IsDefined(assessment)))
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }

        if (superseding != (request.SupersedesFindingId is not null))
        {
            throw new ArgumentException("Finding supersession must identify exactly one prior finding.", nameof(request));
        }

        var eventType = superseding ? "triage_finding_superseded" : "triage_finding_recorded";
        var requestHash = Hash($"{eventType}|{request.TriageId:N}|{request.ExpectedVersion}|{request.Roadworthiness}|{request.Assessment}|{request.SupersedesFindingId:N}|{request.Actor.Trim()}|{request.Reason.Trim()}");
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await FindReplayAsync(context, request.OperationKey.Trim(), cancellationToken);
        if (replay is not null)
        {
            EnsureReplay(replay, eventType, requestHash, request.TriageId);
            return await MapReplayAsync(context, replay, cancellationToken);
        }

        var triage = await LoadForMutationAsync(context, request.TriageId, request.ExpectedVersion, request.Actor, request.EditLeaseToken, cancellationToken);
        if (superseding)
        {
            var priorExists = await context.TriageFindings.AnyAsync(
                item => item.Id == request.SupersedesFindingId && item.TriageId == triage.Id,
                cancellationToken);
            var alreadySuperseded = await context.TriageFindings.AnyAsync(
                item => item.SupersedesFindingId == request.SupersedesFindingId,
                cancellationToken);
            if (!priorExists || alreadySuperseded)
            {
                throw new InvalidOperationException("The prior finding does not exist or has already been superseded.");
            }
        }
        if (superseding)
        {
            var responseLinks = await context.TriageResponseEvidenceLinks
                .Where(item => item.TriageId == triage.Id)
                .ToListAsync(cancellationToken);
            context.TriageResponseEvidenceLinks.RemoveRange(responseLinks);
        }

        context.TriageFindings.Add(new()
        {
            Id = Guid.NewGuid(),
            TriageId = triage.Id,
            Triage = triage,
            Roadworthiness = request.Roadworthiness is null ? null : ToCode(request.Roadworthiness.Value),
            Assessment = request.Assessment is null ? null : ToCode(request.Assessment.Value),
            SupersedesFindingId = request.SupersedesFindingId,
            Actor = request.Actor.Trim(),
            OperationKey = request.OperationKey.Trim(),
            Reason = request.Reason.Trim(),
            RecordedAtUtc = UtcNow()
        });
        triage.State = ToCode(TriageState.FindingRecorded);
        AppendHistory(context, triage, eventType, request.Actor.Trim(), request.OperationKey.Trim(), request.Reason.Trim(), requestHash);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(triage);
    }

    private async Task<TriageRecord> MutateStateNeutralAsync(
        TriageMutationRequest request,
        string eventType,
        Action<TriageEntity> mutation,
        CancellationToken cancellationToken)
    {
        ValidateMutation(request.TriageId, request.ExpectedVersion, request.Actor, request.OperationKey, request.Reason, request.EditLeaseToken);
        return await MutateAsync(
            request.TriageId,
            request.ExpectedVersion,
            request.Actor,
            request.OperationKey,
            request.Reason,
            request.EditLeaseToken,
            eventType,
            Hash($"{eventType}|{request.TriageId:N}|{request.ExpectedVersion}|{request.Actor.Trim()}|{request.Reason.Trim()}"),
            mutation,
            cancellationToken);
    }

    private async Task<TriageRecord> MutateAsync(
        Guid triageId,
        long expectedVersion,
        string actor,
        string operationKey,
        string reason,
        string leaseToken,
        string eventType,
        string requestHash,
        Action<TriageEntity> mutation,
        CancellationToken cancellationToken)
    {
        ValidateMutation(triageId, expectedVersion, actor, operationKey, reason, leaseToken);
        operationKey = operationKey.Trim();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await FindReplayAsync(context, operationKey, cancellationToken);
        if (replay is not null)
        {
            EnsureReplay(replay, eventType, requestHash, triageId);
            return await MapReplayAsync(context, replay, cancellationToken);
        }

        var triage = await LoadForMutationAsync(context, triageId, expectedVersion, actor, leaseToken, cancellationToken);
        if (eventType == "triage_unassigned" && triage.AssigneeId is null)
        {
            throw new InvalidOperationException("The Triage record is not assigned.");
        }
        if (eventType == "triage_state_completed"
            && !await context.TriageResponseEvidenceLinks.AnyAsync(
                item => item.TriageId == triage.Id,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "Triage completion requires exact replied Sent email evidence.");
        }
        mutation(triage);
        AppendHistory(context, triage, eventType, actor.Trim(), operationKey, reason.Trim(), requestHash);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(triage);
    }

    private async Task ChangeCaseLinkAsync(TriageCaseLinkRequest request, bool linking, CancellationToken cancellationToken)
    {
        ValidateMutation(request.TriageId, request.ExpectedTriageVersion, request.Actor, request.OperationKey, request.Reason, request.EditLeaseToken);
        if (request.CaseId == Guid.Empty || request.ExpectedCaseVersion < 0)
        {
            throw new ArgumentException("A valid case and expected case version are required.", nameof(request));
        }

        var eventType = linking ? "triage_case_linked" : "triage_case_unlinked";
        var requestHash = Hash($"{eventType}|{request.TriageId:N}|{request.ExpectedTriageVersion}|{request.CaseId:N}|{request.ExpectedCaseVersion}|{request.Actor.Trim()}|{request.Reason.Trim()}");
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var replay = await FindReplayAsync(context, request.OperationKey.Trim(), cancellationToken);
        if (replay is not null)
        {
            EnsureReplay(replay, eventType, requestHash, request.TriageId);
            return;
        }
        if (await context.CaseHistory.AsNoTracking().AnyAsync(
                item => item.OperationKey == request.OperationKey.Trim(),
                cancellationToken))
        {
            throw new TriageOperationConflictException(request.TriageId, request.OperationKey.Trim());
        }


        var triage = await LoadForMutationAsync(context, request.TriageId, request.ExpectedTriageVersion, request.Actor, request.EditLeaseToken, cancellationToken);
        var caseEntity = await context.Cases.SingleOrDefaultAsync(item => item.Id == request.CaseId, cancellationToken)
            ?? throw new InvalidOperationException("The case does not exist.");
        if (caseEntity.Version != request.ExpectedCaseVersion)
        {
            throw new DbUpdateConcurrencyException("The case changed before its Triage link could be updated.");
        }

        if (linking)
        {
            if (triage.LinkedCaseId is not null)
            {
                throw new InvalidOperationException("The Triage record is already linked to a case.");
            }
            triage.LinkedCaseId = caseEntity.Id;
        }
        else
        {
            if (triage.LinkedCaseId != caseEntity.Id)
            {
                throw new InvalidOperationException("The Triage record is not linked to the specified case.");
            }
            triage.LinkedCaseId = null;
        }

        var beforeCaseVersion = caseEntity.Version++;
        context.CaseHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = caseEntity.Id,
            Case = caseEntity,
            EventType = eventType,
            Actor = request.Actor.Trim(),
            Reason = request.Reason.Trim(),
            OccurredAtUtc = UtcNow(),
            OperationKey = request.OperationKey.Trim(),
            BeforeVersion = beforeCaseVersion,
            AfterVersion = caseEntity.Version
        });
        AppendHistory(context, triage, eventType, request.Actor.Trim(), request.OperationKey.Trim(), request.Reason.Trim(), requestHash);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<TriageEntity> LoadForMutationAsync(
        PegasusDbContext context,
        Guid id,
        long expectedVersion,
        string actor,
        string leaseToken,
        CancellationToken cancellationToken)
    {
        var triage = await context.Triage.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Triage '{id}' does not exist.");
        EnsureVersion(triage, expectedVersion);
        EnsureLiveLease(triage, actor, leaseToken);
        return triage;
    }

    private static Task<TriageHistoryEntity?> FindReplayAsync(
        PegasusDbContext context,
        string operationKey,
        CancellationToken cancellationToken) => context.TriageHistory.AsNoTracking().SingleOrDefaultAsync(
            item => item.OperationKey == operationKey,
            cancellationToken);

    private static void EnsureReplay(
        TriageHistoryEntity replay,
        string eventType,
        string requestHash,
        Guid? requestedTriageId = null)
    {
        if (replay.EventType != eventType
            || replay.RequestHash != requestHash
            || (requestedTriageId is not null && replay.TriageId != requestedTriageId))
        {
            throw new TriageOperationConflictException(replay.TriageId, replay.OperationKey);
        }
    }

    private static async Task<TriageRecord> MapReplayAsync(
        PegasusDbContext context,
        TriageHistoryEntity replay,
        CancellationToken cancellationToken)
    {
        var entity = await context.Triage.AsNoTracking().SingleAsync(item => item.Id == replay.TriageId, cancellationToken);
        return Map(entity) with
        {
            State = ParseState(replay.AfterState),
            AssigneeId = replay.AfterAssigneeId,
            LinkedCaseId = replay.AfterLinkedCaseId,
            Version = replay.AfterVersion
        };
    }

    private void AppendHistory(
        PegasusDbContext context,
        TriageEntity triage,
        string eventType,
        string actor,
        string operationKey,
        string reason,
        string requestHash,
        long? beforeVersion = null)
    {
        var before = beforeVersion ?? triage.Version;
        if (beforeVersion is null)
        {
            triage.Version++;
        }
        context.TriageHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            TriageId = triage.Id,
            Triage = triage,
            EventType = eventType,
            Actor = actor,
            Reason = reason,
            OperationKey = operationKey,
            RequestHash = requestHash,
            OccurredAtUtc = UtcNow(),
            BeforeVersion = before,
            AfterVersion = triage.Version,
            AfterState = triage.State,
            AfterAssigneeId = triage.AssigneeId,
            AfterLinkedCaseId = triage.LinkedCaseId
        });
    }

    private void EnsureLiveLease(TriageEntity triage, string actor, string leaseToken)
    {
        if (triage.EditLeaseExpiresAtUtc is null
            || triage.EditLeaseExpiresAtUtc <= UtcNow()
            || string.IsNullOrEmpty(triage.EditLeaseTokenHash)
            || string.IsNullOrEmpty(triage.EditLeaseHolder))
        {
            throw new TriageEditLeaseExpiredException(triage.Id);
        }

        var expected = Convert.FromHexString(triage.EditLeaseTokenHash);
        var supplied = Convert.FromHexString(Hash(leaseToken));
        if (triage.EditLeaseHolder != actor.Trim()
            || !CryptographicOperations.FixedTimeEquals(expected, supplied))
        {
            throw new TriageEditLeaseConflictException(triage.Id);
        }
    }

    private static void EnsureVersion(TriageEntity triage, long expectedVersion)
    {
        if (triage.Version != expectedVersion)
        {
            throw new TriageVersionConflictException(triage.Id, expectedVersion, triage.Version);
        }
    }

    private static void ValidateCreate(CreateTriageFromIntakeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Origin);
        ArgumentNullException.ThrowIfNull(request.Origin.SourceIdentity);
        ValidateIdentityAndOperation(request.Origin.ReceiptId, request.Actor, request.OperationKey);
        if (request.Origin.EvaluationRevisionId == Guid.Empty)
        {
            throw new ArgumentException("A creating evaluation revision is required.", nameof(request));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Origin.SourceIdentity.ExternalReceiptToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.NormalizedVehicleRegistration);
        ValidateSha256(request.Origin.SourceHash, nameof(request));
        if (request.Origin.SourceIdentity.ExternalReceiptToken.Trim().Length > 200
            || request.NormalizedVehicleRegistration.Trim().Length > 20)
        {
            throw new ArgumentException("Triage origin or vehicle registration exceeds its storage limit.", nameof(request));
        }
    }

    private static void ValidateMutation(
        Guid triageId,
        long expectedVersion,
        string actor,
        string operationKey,
        string reason,
        string leaseToken)
    {
        ValidateIdentityAndOperation(triageId, actor, operationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ValidateLeaseToken(leaseToken);
        if (expectedVersion < 0 || reason.Trim().Length > 500)
        {
            throw new ArgumentException("A valid expected version and bounded reason are required.");
        }
    }

    private static void ValidateLeaseRequest(Guid id, long expectedVersion, string actor, string leaseToken)
    {
        ValidateIdentity(id, actor);
        ValidateLeaseToken(leaseToken);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedVersion, nameof(expectedVersion));
    }

    private static void ValidateIdentityAndOperation(Guid id, string actor, string operationKey)
    {
        ValidateIdentity(id, actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        if (operationKey.Trim().Length > 100)
        {
            throw new ArgumentException("The operation key cannot exceed 100 characters.", nameof(operationKey));
        }
    }

    private static void ValidateIdentity(Guid id, string actor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        if (id == Guid.Empty || actor.Trim().Length > 200)
        {
            throw new ArgumentException("A valid identity and bounded actor are required.");
        }
    }

    private static void ValidateLeaseToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        if (token.Length > 64)
        {
            throw new ArgumentException("The edit lease token cannot exceed 64 characters.", nameof(token));
        }
    }

    private static void ValidateSha256(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != 64 || value.Any(item => !char.IsAsciiHexDigit(item)))
        {
            throw new ArgumentException("A SHA-256 value must contain 64 hexadecimal characters.", parameterName);
        }
    }

    private DateTimeOffset UtcNow() => timeProvider?.GetUtcNow() ?? TimeProvider.System.GetUtcNow();

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static TriageRecord Map(TriageEntity entity) => new(
        entity.Id,
        new(
            entity.OriginReceiptId,
            new(ParseSourceChannel(entity.SourceChannel), entity.ExternalReceiptToken),
            entity.SourceHash,
            entity.EvaluationRevisionId),
        entity.NormalizedVehicleRegistration,
        ParseState(entity.State),
        entity.AssigneeId,
        entity.LinkedCaseId,
        entity.Version);

    private static TriageFinding Map(TriageFindingEntity entity) => new(
        entity.Id,
        entity.TriageId,
        entity.Roadworthiness is null ? null : ParseRoadworthiness(entity.Roadworthiness),
        entity.Assessment is null ? null : ParseAssessment(entity.Assessment),
        entity.SupersedesFindingId,
        entity.Actor,
        entity.OperationKey,
        entity.Reason,
        entity.RecordedAtUtc);

    private static TriageResponseEvidenceLink Map(TriageResponseEvidenceLinkEntity entity) => new(
        entity.TriageId,
        entity.SentEvidenceId,
        entity.Actor,
        entity.OperationKey,
        entity.Reason,
        entity.LinkedAtUtc);

    private static string ToCode(IntakeSourceChannel value) => value switch
    {
        IntakeSourceChannel.ManualUpload => "manual_upload",
        IntakeSourceChannel.Mailbox => "mailbox",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static IntakeSourceChannel ParseSourceChannel(string value) => value switch
    {
        "manual_upload" => IntakeSourceChannel.ManualUpload,
        "mailbox" => IntakeSourceChannel.Mailbox,
        _ => throw new InvalidDataException($"Unknown persisted intake source channel '{value}'.")
    };

    private static string ToCode(TriageState value) => value switch
    {
        TriageState.Open => "open",
        TriageState.AwaitingInformation => "awaiting_information",
        TriageState.FindingRecorded => "finding_recorded",
        TriageState.Completed => "completed",
        TriageState.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static TriageState ParseState(string value) => value switch
    {
        "open" => TriageState.Open,
        "awaiting_information" => TriageState.AwaitingInformation,
        "finding_recorded" => TriageState.FindingRecorded,
        "completed" => TriageState.Completed,
        "cancelled" => TriageState.Cancelled,
        _ => throw new InvalidDataException($"Unknown persisted Triage state '{value}'.")
    };

    private static string ToCode(RoadworthinessFinding value) => value switch
    {
        RoadworthinessFinding.Roadworthy => "roadworthy",
        RoadworthinessFinding.Unroadworthy => "unroadworthy",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static RoadworthinessFinding ParseRoadworthiness(string value) => value switch
    {
        "roadworthy" => RoadworthinessFinding.Roadworthy,
        "unroadworthy" => RoadworthinessFinding.Unroadworthy,
        _ => throw new InvalidDataException($"Unknown persisted Roadworthiness finding '{value}'.")
    };

    private static string ToCode(AssessmentFinding value) => value switch
    {
        AssessmentFinding.Repairable => "repairable",
        AssessmentFinding.TotalLoss => "total_loss",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static AssessmentFinding ParseAssessment(string value) => value switch
    {
        "repairable" => AssessmentFinding.Repairable,
        "total_loss" => AssessmentFinding.TotalLoss,
        _ => throw new InvalidDataException($"Unknown persisted Assessment finding '{value}'.")
    };
}
