using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Access;
using Pegasus.Core.ActionHistory;
using Pegasus.Core.Cases;
using Pegasus.Core.Triage;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Infrastructure.Workflow;

internal static class WorkflowStoreHelpers
{
    public static string NormalizeRegistration(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    public static BusinessActionEntity ToEntity(BusinessAction action) => new()
    {
        Id = action.Id,
        CaseId = action.CaseId,
        TriageId = action.TriageId,
        ActorKind = action.ActorKind,
        ActorId = action.ActorId,
        Caller = action.Caller,
        Action = action.Action,
        OccurredAtUtc = action.OccurredAtUtc,
        CorrelationId = action.CorrelationId,
        BeforeJson = action.BeforeJson,
        AfterJson = action.AfterJson,
        Outcome = action.Outcome,
        Reason = action.Reason
    };

    public static TriageHistoryEntry ToHistory(BusinessActionEntity entity, string actorName) =>
        new(entity.Id, entity.Action, entity.Outcome, entity.OccurredAtUtc, actorName, entity.Reason);

    public static CaseHistoryEntry ToCaseHistory(BusinessActionEntity entity, string actorName) =>
        new(entity.Id, entity.Action, entity.Outcome, entity.OccurredAtUtc, actorName, entity.Reason);
}

public sealed class EfTriageStore(IDbContextFactory<PegasusDbContext> contextFactory, TimeProvider clock) : ITriageStore
{
    public async Task<IReadOnlyList<TriageSummary>> ListAsync(TriageQuery query, StaffActor actor, CancellationToken cancellationToken)
    {
        ValidateQuery(query);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var items = db.Triages.AsNoTracking().AsQueryable();
        if (query.State is not null) items = items.Where(item => item.State == query.State.Value.ToString());
        if (query.AssigneeId is not null) items = items.Where(item => item.AssigneeId == query.AssigneeId);
        if (query.NormalizedRegistration is not null) items = items.Where(item => item.Registration == query.NormalizedRegistration);
        var values = (await items.ToListAsync(cancellationToken))
            .OrderByDescending(item => item.LastChangedAtUtc)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return values.Select(item => new TriageSummary(item.Id, item.SourceId, item.Registration, item.AssigneeName,
            Enum.Parse<TriageState>(item.State), item.LastChangedAtUtc, item.Version)).ToList();
    }

    public async Task<TriageDetail?> GetAsync(Guid id, StaffActor actor, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Triages.AsNoTracking().Include(x => x.Findings).Include(x => x.ReplyEvidence)
            .Include(x => x.CaseLinks).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return item is null ? null : await MapDetailAsync(db, item, cancellationToken);
    }

    public async Task<int> GetOpenCountAsync(StaffActor actor, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Triages.CountAsync(item => item.State == "Open" || item.State == "AwaitingInformation" || item.State == "FindingRecorded", cancellationToken);
    }

    public Task<TriageCommandResult> AssignAsync(Guid id, long expectedVersion, StaffActor actor, Guid assigneeId, string assigneeName, CancellationToken cancellationToken) =>
        MutateAsync(id, expectedVersion, actor, "Assign", null, item =>
        {
            item.AssigneeId = assigneeId;
            item.AssigneeName = assigneeName;
        }, cancellationToken);

    public Task<TriageCommandResult> MarkAwaitingInformationAsync(Guid id, long expectedVersion, StaffActor actor, string reason, CancellationToken cancellationToken) =>
        MutateAsync(id, expectedVersion, actor, "MarkAwaitingInformation", reason, item =>
        {
            if (item.State != nameof(TriageState.Open)) throw new WorkflowFailureException(TriageCommandFailure.InvalidState);
            item.State = nameof(TriageState.AwaitingInformation);
        }, cancellationToken);

    public Task<TriageCommandResult> RecordFindingAsync(Guid id, long expectedVersion, StaffActor actor, RoadworthinessFinding? roadworthiness, AssessmentFinding? assessment, string? reason, CancellationToken cancellationToken) =>
        MutateAsync(id, expectedVersion, actor, "RecordFinding", reason, item =>
        {
            if (roadworthiness is null && assessment is null) throw new WorkflowFailureException(TriageCommandFailure.FindingRequired);
            if (item.State == nameof(TriageState.Cancelled)) throw new WorkflowFailureException(TriageCommandFailure.InvalidState);
            if (item.Findings.Count > 0 && string.IsNullOrWhiteSpace(reason)) throw new WorkflowFailureException(TriageCommandFailure.ReasonRequired);
            var finding = new TriageFindingEntity
            {
                Id = Guid.NewGuid(), TriageId = item.Id, Roadworthiness = roadworthiness?.ToString(),
                Assessment = assessment?.ToString(), Reason = reason, RecordedAtUtc = clock.GetUtcNow(), ActorId = actor.Id
            };
            item.Findings.Add(finding);
            item.State = nameof(TriageState.FindingRecorded);
            item.ReplyEvidence = null;
        }, cancellationToken);

    public Task<TriageCommandResult> CompleteAsync(Guid id, long expectedVersion, StaffActor actor, TriageReplyEvidence evidence, CancellationToken cancellationToken) =>
        Task.FromResult(TriageCommandResult.Failed(TriageCommandFailure.ReplyEvidenceUnavailable,
            "Exact approved reply-chain evidence is unavailable."));

    public Task<TriageCommandResult> CancelAsync(Guid id, long expectedVersion, StaffActor actor, string reason, CancellationToken cancellationToken) =>
        MutateAsync(id, expectedVersion, actor, "Cancel", reason, item =>
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new WorkflowFailureException(TriageCommandFailure.ReasonRequired);
            if (item.State == nameof(TriageState.Completed)) throw new WorkflowFailureException(TriageCommandFailure.InvalidState);
            item.State = nameof(TriageState.Cancelled);
        }, cancellationToken);

    public Task<TriageCommandResult> ReopenAsync(Guid id, long expectedVersion, StaffActor actor, string reason, CancellationToken cancellationToken) =>
        MutateAsync(id, expectedVersion, actor, "Reopen", reason, item =>
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new WorkflowFailureException(TriageCommandFailure.ReasonRequired);
            if (item.State is not (nameof(TriageState.Completed) or nameof(TriageState.Cancelled))) throw new WorkflowFailureException(TriageCommandFailure.InvalidState);
            item.State = nameof(TriageState.Open);
        }, cancellationToken);

    public Task<TriageCommandResult> LinkCaseAsync(Guid id, long expectedVersion, StaffActor actor, Guid caseId, CancellationToken cancellationToken) =>
        MutateAsync(id, expectedVersion, actor, "LinkCase", null, item =>
        {
            if (item.CaseLinks.Any(link => link.UnlinkedAtUtc is null)) throw new WorkflowFailureException(TriageCommandFailure.InvalidState);
            item.CaseLinks.Add(new TriageCaseLinkEntity { Id = Guid.NewGuid(), TriageId = item.Id, CaseId = caseId, LinkedAtUtc = clock.GetUtcNow() });
        }, cancellationToken);

    public Task<TriageCommandResult> UnlinkCaseAsync(Guid id, long expectedVersion, StaffActor actor, string reason, CancellationToken cancellationToken) =>
        MutateAsync(id, expectedVersion, actor, "UnlinkCase", reason, item =>
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new WorkflowFailureException(TriageCommandFailure.ReasonRequired);
            var link = item.CaseLinks.LastOrDefault(x => x.UnlinkedAtUtc is null) ?? throw new WorkflowFailureException(TriageCommandFailure.CaseNotFound);
            link.UnlinkedAtUtc = clock.GetUtcNow();
            link.Reason = reason;
        }, cancellationToken);

    public async Task<IReadOnlyList<BusinessAction>> ListForCaseAsync(Guid caseId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.BusinessActions.AsNoTracking().Where(x => x.CaseId == caseId).OrderBy(x => x.OccurredAtUtc)
            .Select(x => new BusinessAction(x.Id, x.CaseId, x.TriageId, x.ActorKind, x.ActorId, x.Caller, x.Action, x.OccurredAtUtc, x.CorrelationId, x.BeforeJson, x.AfterJson, x.Outcome, x.Reason)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BusinessAction>> ListForTriageAsync(Guid triageId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.BusinessActions.AsNoTracking().Where(x => x.TriageId == triageId).OrderBy(x => x.OccurredAtUtc)
            .Select(x => new BusinessAction(x.Id, x.CaseId, x.TriageId, x.ActorKind, x.ActorId, x.Caller, x.Action, x.OccurredAtUtc, x.CorrelationId, x.BeforeJson, x.AfterJson, x.Outcome, x.Reason)).ToListAsync(cancellationToken);
    }

    private async Task<TriageCommandResult> MutateAsync(Guid id, long expectedVersion, StaffActor actor, string action, string? reason, Action<TriageEntity> mutation, CancellationToken cancellationToken)
    {
        if (actor is null) return TriageCommandResult.Failed(TriageCommandFailure.Denied);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Triages.Include(x => x.Findings).Include(x => x.ReplyEvidence).Include(x => x.CaseLinks)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null) return TriageCommandResult.Failed(TriageCommandFailure.NotFound);
        if (item.Version != expectedVersion) return TriageCommandResult.Failed(TriageCommandFailure.StaleVersion);
        try { mutation(item); }
        catch (WorkflowFailureException failure) { return TriageCommandResult.Failed(failure.Failure); }
        item.LastChangedAtUtc = clock.GetUtcNow();
        item.Version++;
        db.BusinessActions.Add(new BusinessActionEntity
        {
            Id = Guid.NewGuid(), TriageId = id, ActorKind = "Staff", ActorId = actor.Id, Caller = "Web",
            Action = action, OccurredAtUtc = item.LastChangedAtUtc, CorrelationId = Guid.NewGuid(),
            Outcome = "Succeeded", Reason = reason
        });
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return TriageCommandResult.Failed(TriageCommandFailure.StaleVersion); }
        return new TriageCommandResult(await MapDetailAsync(db, item, cancellationToken), null);
    }

    private static void ValidateQuery(TriageQuery query)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(query));
    }

    private static async Task<TriageDetail> MapDetailAsync(PegasusDbContext db, TriageEntity item, CancellationToken cancellationToken)
    {
        var history = await db.BusinessActions.AsNoTracking().Where(x => x.TriageId == item.Id).OrderBy(x => x.OccurredAtUtc)
            .Select(x => new TriageHistoryEntry(x.Id, x.Action, x.Outcome, x.OccurredAtUtc, x.ActorId.ToString(), x.Reason)).ToListAsync(cancellationToken);
        var revisions = item.Findings.OrderBy(x => x.RecordedAtUtc).Select(x => new TriageFindingRevision(x.Id,
            Enum.TryParse<RoadworthinessFinding>(x.Roadworthiness, out var roadworthiness) ? roadworthiness : null,
            Enum.TryParse<AssessmentFinding>(x.Assessment, out var assessment) ? assessment : null,
            x.Reason, x.RecordedAtUtc, x.ActorId)).ToList();
        var links = item.CaseLinks.OrderBy(x => x.LinkedAtUtc).Select(x => new TriageCaseLink(x.CaseId, x.LinkedAtUtc, x.UnlinkedAtUtc, x.Reason)).ToList();
        var evidence = item.ReplyEvidence is null ? null : new TriageReplyEvidence(item.ReplyEvidence.ExternalMessageId, item.ReplyEvidence.ConversationId, item.ReplyEvidence.ApprovedMailbox, item.ReplyEvidence.SentAtUtc, item.ReplyEvidence.ReplyHash);
        return new TriageDetail(item.Id, item.SourceId, item.Registration, item.AssigneeId, item.AssigneeName, Enum.Parse<TriageState>(item.State),
            revisions.LastOrDefault(), revisions, evidence, links.LastOrDefault(x => x.UnlinkedAtUtc is null), links, item.Version, history);
    }

    private sealed class WorkflowFailureException(TriageCommandFailure failure) : Exception
    {
        public TriageCommandFailure Failure { get; } = failure;
    }
}

public sealed class EfCaseStore(IDbContextFactory<PegasusDbContext> contextFactory, TimeProvider clock) : ICaseWorkflow, ICaseEditing
{
    public async Task<IReadOnlyList<CaseSummary>> ListAsync(CaseQuery query, StaffActor actor, CancellationToken cancellationToken)
    {
        ValidateQuery(query);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var items = db.Cases.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.CaseReference)) items = items.Where(x => x.DisplayReference.Contains(query.CaseReference));
        if (!string.IsNullOrWhiteSpace(query.Registration)) items = items.Where(x => x.Registration == WorkflowStoreHelpers.NormalizeRegistration(query.Registration));
        if (!string.IsNullOrWhiteSpace(query.Claimant)) items = items.Where(x => x.Claimant != null && x.Claimant.Contains(query.Claimant));
        if (!string.IsNullOrWhiteSpace(query.ClaimNumber)) items = items.Where(x => x.ClaimNumber == query.ClaimNumber);
        if (!string.IsNullOrWhiteSpace(query.PrincipalCode)) items = items.Where(x => x.PrincipalCode == query.PrincipalCode);
        if (query.State is not null) items = items.Where(x => x.State == query.State.Value.ToString());
        if (query.EngineerId is not null) items = items.Where(x => x.EngineerId == query.EngineerId);
        if (query.Origin is not null) items = items.Where(x => x.Origin == query.Origin);
        if (query.ReceivedFrom is not null) items = items.Where(x => x.ReceivedAtUtc >= query.ReceivedFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (query.ReceivedTo is not null) items = items.Where(x => x.ReceivedAtUtc < query.ReceivedTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (query.InstructionFrom is not null) items = items.Where(x => x.InstructionDate >= query.InstructionFrom);
        if (query.InstructionTo is not null) items = items.Where(x => x.InstructionDate <= query.InstructionTo);
        var values = (await items.ToListAsync(cancellationToken))
            .OrderByDescending(x => x.NextDueAtUtc).ThenByDescending(x => x.ReceivedAtUtc).ThenBy(x => x.DisplayReference)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return values.Select(MapSummary).ToList();
    }

    public async Task<CaseDetail?> GetAsync(Guid id, StaffActor actor, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Cases.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return item is null ? null : await MapDetailAsync(db, item, actor, cancellationToken);
    }

    public async Task<CaseQueueCounts> GetQueueCountsAsync(StaffActor actor, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.GetUtcNow();
        return new CaseQueueCounts(
            await db.Cases.CountAsync(x => x.State == "NotReady" && !x.IsHeld, cancellationToken),
            await db.Cases.CountAsync(x => x.State == "Review" && !x.IsHeld, cancellationToken),
            await db.Cases.CountAsync(x => x.IsHeld, cancellationToken),
            await db.Cases.CountAsync(x => x.NextDueAtUtc != null && x.NextDueAtUtc <= now && !x.IsHeld, cancellationToken),
            await db.Cases.CountAsync(x => x.ReceivedAtUtc >= now.AddDays(-1), cancellationToken),
            await db.Cases.CountAsync(x => x.State == "ReportPreparation", cancellationToken),
            await db.Cases.CountAsync(x => x.State == "PostReport", cancellationToken), now);
    }

    public Task<CaseCommandResult> ConfirmCompletenessAsync(Guid id, string? leaseToken, long expectedVersion, StaffActor actor, bool instructionsComplete, bool imagesComplete, CancellationToken cancellationToken) =>
        MutateAsync(id, leaseToken, expectedVersion, actor, "ConfirmCompleteness", null, item =>
        {
            if (item.State is not (nameof(CaseWorkflowState.NotReady) or nameof(CaseWorkflowState.Review))) throw new CaseFailureException(CaseCommandFailure.InvalidState);
            if (!instructionsComplete || !imagesComplete) { item.State = nameof(CaseWorkflowState.NotReady); item.NextDueAtUtc ??= clock.GetUtcNow().AddDays(7); }
            else { item.State = nameof(CaseWorkflowState.Review); item.NextDueAtUtc = null; }
        }, cancellationToken);
    public Task<CaseCommandResult> HoldAsync(Guid id, string? leaseToken, long expectedVersion, StaffActor actor, string reason, CancellationToken cancellationToken) =>
        MutateAsync(id, leaseToken, expectedVersion, actor, "Hold", reason, item =>
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new CaseFailureException(CaseCommandFailure.ReasonRequired);
            if (item.IsHeld) throw new CaseFailureException(CaseCommandFailure.InvalidState);
            item.IsHeld = true; item.DuePaused = true;
        }, cancellationToken);

    public Task<CaseCommandResult> ReleaseAsync(Guid id, string? leaseToken, long expectedVersion, StaffActor actor, string reason, CancellationToken cancellationToken) =>
        MutateAsync(id, leaseToken, expectedVersion, actor, "Release", reason, item =>
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new CaseFailureException(CaseCommandFailure.ReasonRequired);
            if (!item.IsHeld) throw new CaseFailureException(CaseCommandFailure.InvalidState);
            item.IsHeld = false; item.DuePaused = false;
        }, cancellationToken);

    public Task<CaseCommandResult> RecordChaseAsync(Guid id, string? leaseToken, long expectedVersion, StaffActor actor, string channel, string target, string outcome, string? note, CancellationToken cancellationToken) =>
        MutateAsync(id, leaseToken, expectedVersion, actor, "RecordChase", note, item =>
        {
            item.ChaseCount++;
            if (!item.IsHeld) item.NextDueAtUtc = (item.NextDueAtUtc ?? clock.GetUtcNow()).AddDays(7);
        }, cancellationToken);
    public Task<CaseCommandResult> StartReportPreparationAsync(Guid id, string? leaseToken, long expectedVersion, StaffActor actor, CancellationToken cancellationToken) =>
        MutateAsync(id, leaseToken, expectedVersion, actor, "StartReportPreparation", null, item =>
        {
            if (item.State != nameof(CaseWorkflowState.Review) || item.IsHeld) throw new CaseFailureException(CaseCommandFailure.InvalidState);
            item.State = nameof(CaseWorkflowState.ReportPreparation);
        }, cancellationToken);

    public Task<CaseCommandResult> RecordReportSentAsync(Guid id, string? leaseToken, long expectedVersion, StaffActor actor, CancellationToken cancellationToken) =>
        MutateAsync(id, leaseToken, expectedVersion, actor, "RecordReportSent", null, item =>
        {
            if (item.State != nameof(CaseWorkflowState.ReportPreparation)) throw new CaseFailureException(CaseCommandFailure.InvalidState);
            item.State = nameof(CaseWorkflowState.PostReport);
        }, cancellationToken);
    public Task<CaseCommandResult> CloseAsync(Guid id, string? leaseToken, long expectedVersion, StaffActor actor, CaseTerminalOutcome outcome, string reason, CancellationToken cancellationToken) =>
        MutateAsync(id, leaseToken, expectedVersion, actor, "Close", reason, item =>
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new CaseFailureException(CaseCommandFailure.ReasonRequired);
            item.TerminalOutcome = outcome.ToString();
        }, cancellationToken);

    public Task<CaseCommandResult> ReopenAsync(Guid id, string? leaseToken, long expectedVersion, StaffActor actor, string reason, CancellationToken cancellationToken) =>
        MutateAsync(id, leaseToken, expectedVersion, actor, "Reopen", reason, item =>
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new CaseFailureException(CaseCommandFailure.ReasonRequired);
            if (item.TerminalOutcome is null) throw new CaseFailureException(CaseCommandFailure.InvalidState);
            if (item.TerminalOutcome == nameof(CaseTerminalOutcome.CreatedInError)) throw new CaseFailureException(CaseCommandFailure.CreatedInErrorCannotReopen);
            item.TerminalOutcome = null; item.State = nameof(CaseWorkflowState.Review); item.IsHeld = false; item.DuePaused = false;
        }, cancellationToken);
    public Task<CaseCommandResult> CreateCorrectPrincipalReplacementAsync(Guid id, string? leaseToken, long expectedVersion, StaffActor actor, string reason, CancellationToken cancellationToken) =>
        MutateAsync(id, leaseToken, expectedVersion, actor, "CreateCorrectPrincipalReplacement", reason, item =>
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new CaseFailureException(CaseCommandFailure.ReasonRequired);
            item.TerminalOutcome = nameof(CaseTerminalOutcome.CreatedInError);
        }, cancellationToken);
    public async Task<CaseLeaseResult> AcquireAsync(Guid caseId, StaffActor actor, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Cases.AnyAsync(x => x.Id == caseId, cancellationToken))
            return new CaseLeaseResult(null, new CaseLeaseState(null, null, null, false), CaseCommandFailure.NotFound);
        var now = clock.GetUtcNow();
        var lease = await db.CaseLeases.SingleOrDefaultAsync(x => x.CaseId == caseId, cancellationToken);
        if (lease is not null && lease.ExpiresAtUtc > now && lease.HolderId != actor.Id)
            return new CaseLeaseResult(null, new CaseLeaseState(lease.HolderId, lease.HolderName, lease.ExpiresAtUtc, false), CaseCommandFailure.LeaseRequired);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        if (lease is null) { lease = new CaseLeaseEntity { CaseId = caseId }; db.CaseLeases.Add(lease); }
        lease.HolderId = actor.Id; lease.HolderName = actor.DisplayName; lease.TokenHash = Hash(token); lease.AcquiredAtUtc = now; lease.RenewedAtUtc = now; lease.ExpiresAtUtc = now.AddMinutes(15); lease.Version++;
        await db.SaveChangesAsync(cancellationToken);
        return new CaseLeaseResult(token, new CaseLeaseState(actor.Id, actor.DisplayName, lease.ExpiresAtUtc, true));
    }

    public Task<CaseLeaseResult> RenewAsync(Guid caseId, string token, StaffActor actor, CancellationToken cancellationToken) => LeaseMutationAsync(caseId, token, actor, false, cancellationToken);
    public Task<CaseLeaseResult> ReleaseAsync(Guid caseId, string token, StaffActor actor, CancellationToken cancellationToken) => LeaseMutationAsync(caseId, token, actor, true, cancellationToken);

    public async Task<IReadOnlyList<BusinessAction>> ListForCaseAsync(Guid caseId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.BusinessActions.AsNoTracking().Where(x => x.CaseId == caseId).OrderBy(x => x.OccurredAtUtc)
            .Select(x => new BusinessAction(x.Id, x.CaseId, x.TriageId, x.ActorKind, x.ActorId, x.Caller, x.Action, x.OccurredAtUtc, x.CorrelationId, x.BeforeJson, x.AfterJson, x.Outcome, x.Reason)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BusinessAction>> ListForTriageAsync(Guid triageId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.BusinessActions.AsNoTracking().Where(x => x.TriageId == triageId).OrderBy(x => x.OccurredAtUtc)
            .Select(x => new BusinessAction(x.Id, x.CaseId, x.TriageId, x.ActorKind, x.ActorId, x.Caller, x.Action, x.OccurredAtUtc, x.CorrelationId, x.BeforeJson, x.AfterJson, x.Outcome, x.Reason)).ToListAsync(cancellationToken);
    }
    private async Task<CaseCommandResult> MutateAsync(Guid id, string? leaseToken, long expectedVersion, StaffActor actor, string action, string? reason, Action<CaseEntity> mutation, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Cases.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null) return CaseCommandResult.Failed(CaseCommandFailure.NotFound);
        if (item.TerminalOutcome is not null && action != "Reopen") return CaseCommandResult.Failed(CaseCommandFailure.InvalidState);
        var leaseFailure = await ValidateLeaseAsync(db, id, leaseToken, actor, cancellationToken);
        if (leaseFailure is not null) return CaseCommandResult.Failed(leaseFailure.Value);
        if (item.Version != expectedVersion) return CaseCommandResult.Failed(CaseCommandFailure.StaleVersion);
        try { mutation(item); } catch (CaseFailureException ex) { return CaseCommandResult.Failed(ex.Failure); }
        item.Version++;
        db.BusinessActions.Add(new BusinessActionEntity { Id = Guid.NewGuid(), CaseId = id, ActorKind = "Staff", ActorId = actor.Id, Caller = "Web", Action = action, OccurredAtUtc = clock.GetUtcNow(), CorrelationId = Guid.NewGuid(), Outcome = "Succeeded", Reason = reason });
        try { await db.SaveChangesAsync(cancellationToken); } catch (DbUpdateConcurrencyException) { return CaseCommandResult.Failed(CaseCommandFailure.StaleVersion); }
        return new CaseCommandResult(await MapDetailAsync(db, item, actor, cancellationToken), null);
    }

    private async Task<CaseLeaseResult> LeaseMutationAsync(Guid caseId, string token, StaffActor actor, bool release, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var lease = await db.CaseLeases.SingleOrDefaultAsync(x => x.CaseId == caseId, cancellationToken);
        var now = clock.GetUtcNow();
        if (lease is null || lease.HolderId != actor.Id || lease.TokenHash != Hash(token)) return new CaseLeaseResult(null, new CaseLeaseState(lease?.HolderId, lease?.HolderName, lease?.ExpiresAtUtc, false), CaseCommandFailure.LeaseWrongHolder);
        if (lease.ExpiresAtUtc <= now) return new CaseLeaseResult(null, new CaseLeaseState(lease.HolderId, lease.HolderName, lease.ExpiresAtUtc, false), CaseCommandFailure.LeaseExpired);
        if (release) { db.CaseLeases.Remove(lease); await db.SaveChangesAsync(cancellationToken); return new CaseLeaseResult(null, new CaseLeaseState(null, null, null, false)); }
        lease.RenewedAtUtc = now; lease.ExpiresAtUtc = now.AddMinutes(15); lease.Version++;
        await db.SaveChangesAsync(cancellationToken);
        return new CaseLeaseResult(token, new CaseLeaseState(actor.Id, actor.DisplayName, lease.ExpiresAtUtc, true));
    }

    private async Task<CaseCommandFailure?> ValidateLeaseAsync(PegasusDbContext db, Guid caseId, string? token, StaffActor actor, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token)) return CaseCommandFailure.LeaseRequired;
        var lease = await db.CaseLeases.SingleOrDefaultAsync(x => x.CaseId == caseId, cancellationToken);
        if (lease is null || lease.HolderId != actor.Id || lease.TokenHash != Hash(token)) return CaseCommandFailure.LeaseWrongHolder;
        return lease.ExpiresAtUtc <= clock.GetUtcNow() ? CaseCommandFailure.LeaseExpired : null;
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static void ValidateQuery(CaseQuery query)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(query));
        if (query.ReceivedFrom > query.ReceivedTo || query.InstructionFrom > query.InstructionTo) throw new ArgumentException("Date range is inverted.");
    }
    private static IQueryable<CaseEntity> ApplyQueue(IQueryable<CaseEntity> items, CaseQueue queue) => queue switch
    {
        CaseQueue.NotReady => items.Where(x => x.State == "NotReady" && !x.IsHeld),
        CaseQueue.Review => items.Where(x => x.State == "Review" && !x.IsHeld),
        CaseQueue.Held => items.Where(x => x.IsHeld),
        CaseQueue.DueToday => items.Where(x => x.NextDueAtUtc != null && x.NextDueAtUtc <= DateTimeOffset.UtcNow && !x.IsHeld),
        CaseQueue.InToday => items.Where(x => x.ReceivedAtUtc >= DateTimeOffset.UtcNow.Date),
        CaseQueue.SentToEngineer => items.Where(x => x.State == "ReportPreparation"),
        CaseQueue.ReportsSent => items.Where(x => x.State == "PostReport"),
        _ => items
    };
    private static CaseSummary MapSummary(CaseEntity x) => new(x.Id, x.DisplayReference, x.Registration, x.Claimant, x.ClaimNumber, x.PrincipalCode, Enum.Parse<CaseWorkflowState>(x.State), x.IsHeld, x.ReceivedAtUtc, x.InstructionDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), x.EngineerName, new CaseDueWork(x.NextDueAtUtc, x.NextDueAtUtc <= DateTimeOffset.UtcNow && !x.IsHeld, x.ChaseCount, x.DuePaused), x.Version);
    private static async Task<CaseDetail> MapDetailAsync(PegasusDbContext db, CaseEntity x, StaffActor actor, CancellationToken cancellationToken)
    {
        var lease = await db.CaseLeases.AsNoTracking().SingleOrDefaultAsync(l => l.CaseId == x.Id, cancellationToken);
        var history = await db.BusinessActions.AsNoTracking().Where(h => h.CaseId == x.Id).OrderBy(h => h.OccurredAtUtc).Select(h => new CaseHistoryEntry(h.Id, h.Action, h.Outcome, h.OccurredAtUtc, h.ActorId.ToString(), h.Reason)).ToListAsync(cancellationToken);
        var leaseState = lease is null ? new CaseLeaseState(null, null, null, false) : new CaseLeaseState(lease.HolderId, lease.HolderName, lease.ExpiresAtUtc, lease.HolderId == actor.Id && lease.ExpiresAtUtc > DateTimeOffset.UtcNow);
        var identity = new CaseIdentity(x.Id, x.PrincipalCode, x.BaseReference, x.DisplayReference, Enum.Parse<CaseType>(x.Type), x.Registration, x.SecondaryAuditReference);
        return new CaseDetail(identity, x.Claimant, x.ClaimNumber, x.ReceivedAtUtc, x.InstructionDate, x.Origin, [], Enum.Parse<CaseWorkflowState>(x.State), x.IsHeld, new CaseDueWork(x.NextDueAtUtc, x.NextDueAtUtc <= DateTimeOffset.UtcNow && !x.IsHeld, x.ChaseCount, x.DuePaused), x.EngineerId, x.EngineerName, x.TerminalOutcome is null ? null : Enum.Parse<CaseTerminalOutcome>(x.TerminalOutcome), null, leaseState, x.Version, history);
    }
    private sealed class CaseFailureException(CaseCommandFailure failure) : Exception { public CaseCommandFailure Failure { get; } = failure; }
}
