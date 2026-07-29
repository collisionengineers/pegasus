using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfCaseWorkflowStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : ICaseWorkflowStore, ICaseDueWorkStore
{
    private static readonly TimeSpan EditLeaseDuration = TimeSpan.FromMinutes(15);

    public async Task<CaseWorkflowRecord?> GetAsync(Guid caseId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await WorkflowQuery(context, tracking: false)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<bool> HasOperationAsync(
        Guid caseId,
        string operationKey,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.CaseWorkflowEvents.AsNoTracking().AnyAsync(
            item => item.CaseId == caseId && item.OperationKey == operationKey,
            cancellationToken);
    }

    async Task<CaseDueWork?> ICaseDueWorkQueries.GetAsync(Guid caseId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.CaseDueWork.AsNoTracking()
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<CaseDueWork>> GetDueAsync(
        DateTimeOffset asOfUtc,
        int maximumResults,
        CancellationToken cancellationToken)
    {
        if (maximumResults is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumResults));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await context.CaseDueWork.AsNoTracking()
            .Where(item => item.State == nameof(CaseDueWorkState.Scheduled)
                && item.NextChaseAtUtc != null
                && item.NextChaseAtUtc <= asOfUtc)
            .OrderBy(item => item.NextChaseAtUtc)
            .ThenBy(item => item.CaseId)
            .Take(maximumResults)
            .ToArrayAsync(cancellationToken);
        return entities.Select(Map).ToArray();
    }

    public async Task<CaseEditLease> ClaimAsync(
        ClaimCaseEditLeaseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var workflow = await context.CaseWorkflows.SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        RequireVersion(workflow, request.ExpectedVersion);
        var now = timeProvider.GetUtcNow();
        if (workflow.EditLeaseExpiresAtUtc > now)
        {
            throw new CaseEditLeaseConflictException(request.CaseId);
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        workflow.EditLeaseTokenHash = Hash(token);
        workflow.EditLeaseHolder = request.Actor.SubjectId;
        workflow.EditLeaseOperationKey = request.OperationKey;
        workflow.EditLeaseExpiresAtUtc = now + EditLeaseDuration;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(request.CaseId, token, request.Actor.SubjectId, workflow.Version, workflow.EditLeaseExpiresAtUtc.Value);
    }

    public async Task<CaseEditLease> RenewAsync(
        RenewCaseEditLeaseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var workflow = await context.CaseWorkflows.SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        RequireVersion(workflow, request.ExpectedVersion);
        RequireLease(workflow, request.Actor, request.LeaseToken, timeProvider.GetUtcNow());
        workflow.EditLeaseExpiresAtUtc = timeProvider.GetUtcNow() + EditLeaseDuration;
        await context.SaveChangesAsync(cancellationToken);
        return new(request.CaseId, request.LeaseToken, request.Actor.SubjectId, workflow.Version, workflow.EditLeaseExpiresAtUtc.Value);
    }

    public async Task ReleaseAsync(ReleaseCaseEditLeaseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var workflow = await context.CaseWorkflows.SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        RequireLease(workflow, request.Actor, request.LeaseToken, timeProvider.GetUtcNow());
        ClearLease(workflow);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<CaseWorkflowRecord> ChangeStateAsync(
        CaseMutationRequest request,
        CaseLifecycleState targetState,
        CancellationToken cancellationToken) =>
        MutateAsync(request, $"state_{targetState}", (context, workflow, now) =>
        {
            workflow.State = targetState.ToString();
            return Task.CompletedTask;
        }, cancellationToken, targetState.ToString());

    public Task<CaseWorkflowRecord> HoldAsync(PutCaseOnHoldRequest request, CancellationToken cancellationToken) =>
        MutateAsync(request, "case_held", (context, workflow, now) =>
        {
            workflow.State = nameof(CaseLifecycleState.Held);
            var due = workflow.DueWork;
            if (due is not null && due.State == nameof(CaseDueWorkState.Scheduled))
            {
                due.State = nameof(CaseDueWorkState.Held);
                due.HeldAtUtc = request.HeldAtUtc;
                due.RemainingChaseIntervalTicks = due.NextChaseAtUtc is null
                    ? 0
                    : CaseChaseSchedule.RemainingInterval(due.NextChaseAtUtc.Value, request.HeldAtUtc).Ticks;
                due.NextChaseAtUtc = null;
                due.Version++;
            }
            return Task.CompletedTask;
        }, cancellationToken);

    public Task<CaseWorkflowRecord> ReleaseHoldAsync(CaseMutationRequest request, CancellationToken cancellationToken) =>
        MutateAsync(request, "case_hold_released", (context, workflow, now) =>
        {
            workflow.State = nameof(CaseLifecycleState.NotReady);
            var due = workflow.DueWork;
            if (due is null)
            {
                context.CaseDueWork.Add(new CaseDueWorkEntity
                {
                    CaseId = workflow.CaseId,
                    Workflow = workflow,
                    MissingMaterialReason = request.Reason,
                    State = nameof(CaseDueWorkState.Scheduled),
                    NextChaseAtUtc = CaseChaseSchedule.FirstChaseAt(now),
                    Version = 0
                });
            }
            else
            {
                due.State = nameof(CaseDueWorkState.Scheduled);
                due.NextChaseAtUtc = CaseChaseSchedule.ResumeAt(now, TimeSpan.FromTicks(due.RemainingChaseIntervalTicks ?? 0));
                due.HeldAtUtc = null;
                due.RemainingChaseIntervalTicks = null;
                due.Version++;
            }
            return Task.CompletedTask;
        }, cancellationToken);

    public Task<CaseWorkflowRecord> ReturnToReviewAsync(ReturnCaseToReviewRequest request, CancellationToken cancellationToken) =>
        MutateAsync(request, "case_returned_to_review", (context, workflow, now) =>
        {
            workflow.State = nameof(CaseLifecycleState.Review);
            StopDueWork(workflow);
            return Task.CompletedTask;
        }, cancellationToken);

    public Task<CaseWorkflowRecord> AssignEngineerAsync(AssignCaseEngineerRequest request, CancellationToken cancellationToken) =>
        MutateAsync(request, "case_engineer_assigned", (context, workflow, now) =>
        {
            workflow.AssignedEngineerId = request.EngineerId;
            return Task.CompletedTask;
        }, cancellationToken);

    public Task<CaseWorkflowRecord> RecordReportApprovalAsync(
        RecordCaseReportApprovalRequest request,
        CancellationToken cancellationToken) =>
        MutateAsync(request, "case_report_approved", (context, workflow, now) =>
        {
            var approval = request.Approval;
            context.CaseReportApprovals.Add(new()
            {
                Id = approval.ApprovalId,
                CaseId = workflow.CaseId,
                ArtifactIdentity = approval.ArtifactIdentity,
                ArtifactSha256 = approval.ArtifactSha256.ToLowerInvariant(),
                ApprovedByKind = approval.ApprovedBy.Kind.ToString(),
                ApprovedBySubjectId = approval.ApprovedBy.SubjectId,
                ApprovedByRolesJson = RolesJson(approval.ApprovedBy),
                ApprovedAtUtc = approval.ApprovedAtUtc
            });
            workflow.ReportApprovalId = approval.ApprovalId;
            return Task.CompletedTask;
        }, cancellationToken);

    public Task<CaseWorkflowRecord> RecordReportSentAsync(
        RecordCaseReportSentRequest request,
        CancellationToken cancellationToken) =>
        MutateAsync(request, "case_report_sent", (context, workflow, now) =>
        {
            var evidence = request.Evidence;
            context.CaseReportSentEvidence.Add(new()
            {
                Id = evidence.EvidenceId,
                CaseId = workflow.CaseId,
                MailboxIdentity = evidence.MailboxIdentity,
                SentFolderIdentity = evidence.SentFolderIdentity,
                ImmutableItemIdentity = evidence.ImmutableItemIdentity,
                ConversationIdentity = evidence.ConversationIdentity,
                ReplyChainIdentity = evidence.ReplyChainIdentity,
                SentAtUtc = evidence.SentAtUtc,
                LinkedAtUtc = evidence.LinkedAtUtc,
                LinkedByKind = evidence.LinkedBy.Kind.ToString(),
                LinkedBySubjectId = evidence.LinkedBy.SubjectId,
                LinkedByRolesJson = RolesJson(evidence.LinkedBy)
            });
            workflow.ReportSentEvidenceId = evidence.EvidenceId;
            workflow.State = nameof(CaseLifecycleState.PostReport);
            return Task.CompletedTask;
        }, cancellationToken);

    public Task<CaseWorkflowRecord> CloseAsync(CloseCaseRequest request, CancellationToken cancellationToken) =>
        MutateAsync(request, $"case_closed_{request.Outcome}", async (context, workflow, now) =>
        {
            if (request.ReplacementCaseId is { } replacementId)
            {
                var replacement = await context.CaseWorkflows.AsNoTracking()
                    .SingleOrDefaultAsync(item => item.CaseId == replacementId, cancellationToken)
                    ?? throw new InvalidOperationException("The linked replacement case does not exist.");
                if (replacement.State == nameof(CaseLifecycleState.CreatedInError))
                {
                    throw new InvalidOperationException("A Created in error case cannot be used as a replacement.");
                }
            }
            workflow.State = request.Outcome.ToString();
            workflow.ClosureOutcome = request.Outcome.ToString();
            workflow.ReplacementCaseId = request.ReplacementCaseId;
            StopDueWork(workflow);
        }, cancellationToken);

    public Task<CaseWorkflowRecord> ReopenAsync(ReopenCaseRequest request, CancellationToken cancellationToken) =>
        MutateAsync(request, $"case_reopened_{request.Destination}", (context, workflow, now) =>
        {
            workflow.State = request.Destination.ToString();
            workflow.ClosureOutcome = null;
            if (request.Destination != CaseReopenDestination.PostReport)
            {
                workflow.ReportApprovalId = null;
                workflow.ReportApproval = null;
                workflow.ReportSentEvidenceId = null;
                workflow.ReportSentEvidence = null;
            }
            if (request.Destination == CaseReopenDestination.NotReady)
            {
                var due = workflow.DueWork;
                if (due is null)
                {
                    context.CaseDueWork.Add(new()
                    {
                        CaseId = workflow.CaseId,
                        Workflow = workflow,
                        MissingMaterialReason = request.Reason,
                        State = nameof(CaseDueWorkState.Scheduled),
                        NextChaseAtUtc = CaseChaseSchedule.FirstChaseAt(now),
                        Version = 0
                    });
                }
                else
                {
                    due.State = nameof(CaseDueWorkState.Scheduled);
                    due.NextChaseAtUtc = CaseChaseSchedule.FirstChaseAt(now);
                    due.Version++;
                }
            }
            else
            {
                StopDueWork(workflow);
            }
            return Task.CompletedTask;
        }, cancellationToken);

    public async Task<CaseDueWork> RecordManualChaseAsync(
        ManualChaseRecord request,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var hash = RequestHash(request);
        var replay = await context.CaseManualChases.AsNoTracking()
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId && item.OperationKey == request.OperationKey, cancellationToken);
        if (replay is not null)
        {
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(replay.RequestHash), Convert.FromHexString(hash)))
            {
                throw new CaseOperationConflictException(request.CaseId, request.OperationKey);
            }
            var replayedDue = await context.CaseDueWork.AsNoTracking().SingleAsync(item => item.CaseId == request.CaseId, cancellationToken);
            return Map(replayedDue);
        }

        var workflow = await context.CaseWorkflows.SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        RequireVersion(workflow, request.ExpectedCaseVersion);
        RequireLease(workflow, request.Actor, request.EditLeaseToken, timeProvider.GetUtcNow());
        var due = await context.CaseDueWork.SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken)
            ?? throw new InvalidOperationException("The case has no due work to chase.");
        if (due.State != nameof(CaseDueWorkState.Scheduled))
        {
            throw new InvalidOperationException("Only scheduled due work can be chased.");
        }
        due.MostRecentChannel = request.Channel;
        due.MostRecentOutcome = request.Outcome;
        due.MostRecentNote = request.Note;
        due.NextChaseAtUtc = CaseChaseSchedule.NextChaseAt(request.AttemptedAtUtc);
        due.Version++;
        workflow.Version++;
        ClearLease(workflow);
        context.CaseManualChases.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = request.CaseId,
            OperationKey = request.OperationKey,
            RequestHash = hash,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = RolesJson(request.Actor),
            Reason = request.Reason,
            Channel = request.Channel,
            TargetPartyOrAddress = request.TargetPartyOrAddress,
            AttemptedAtUtc = request.AttemptedAtUtc,
            Outcome = request.Outcome,
            Note = request.Note,
            ResultingVersion = workflow.Version
        });
        AddEvent(context, workflow, request.Actor, request.OperationKey, request.Reason, hash, "manual_chase_recorded", workflow.Version - 1, workflow.Version);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(due);
    }

    private async Task<CaseWorkflowRecord> MutateAsync(
        CaseMutationRequest request,
        string eventType,
        Func<PegasusDbContext, CaseWorkflowEntity, DateTimeOffset, Task> apply,
        CancellationToken cancellationToken,
        string? discriminator = null)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var hash = Hash($"{request.GetType().FullName}|{JsonSerializer.Serialize(request, request.GetType())}|{discriminator}");
        var replay = await context.CaseWorkflowEvents.AsNoTracking()
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId && item.OperationKey == request.OperationKey, cancellationToken);
        if (replay is not null)
        {
            if (!string.Equals(replay.RequestHash, hash, StringComparison.Ordinal))
            {
                throw new CaseOperationConflictException(request.CaseId, request.OperationKey);
            }
            var replayWorkflow = await WorkflowQuery(context, tracking: false).SingleAsync(item => item.CaseId == request.CaseId, cancellationToken);
            return Map(replayWorkflow);
        }

        var workflow = await WorkflowQuery(context, tracking: true)
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        RequireVersion(workflow, request.ExpectedVersion);
        var now = timeProvider.GetUtcNow();
        RequireLease(workflow, request.Actor, request.EditLeaseToken, now);
        var beforeVersion = workflow.Version;
        await apply(context, workflow, now);
        workflow.Version++;
        ClearLease(workflow);
        AddEvent(context, workflow, request.Actor, request.OperationKey, request.Reason, hash, eventType, beforeVersion, workflow.Version);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(workflow);
    }

    private static IQueryable<CaseWorkflowEntity> WorkflowQuery(PegasusDbContext context, bool tracking)
    {
        var query = context.CaseWorkflows
            .Include(item => item.Case).ThenInclude(item => item.Principal)
            .Include(item => item.ReportApproval)
            .Include(item => item.ReportSentEvidence)
            .Include(item => item.DueWork);
        return tracking ? query : query.AsNoTracking();
    }


    private static void StopDueWork(CaseWorkflowEntity workflow)
    {
        var due = workflow.DueWork;
        if (due is null) return;
        due.State = nameof(CaseDueWorkState.Stopped);
        due.NextChaseAtUtc = null;
        due.HeldAtUtc = null;
        due.RemainingChaseIntervalTicks = null;
        due.Version++;
    }

    private static void RequireVersion(CaseWorkflowEntity workflow, long expectedVersion)
    {
        if (workflow.Version != expectedVersion)
        {
            throw new CaseVersionConflictException(workflow.CaseId, expectedVersion, workflow.Version);
        }
    }

    private static void RequireLease(CaseWorkflowEntity workflow, ActionActor actor, string token, DateTimeOffset now)
    {
        if (workflow.EditLeaseExpiresAtUtc is null || workflow.EditLeaseExpiresAtUtc <= now
            || workflow.EditLeaseTokenHash is null || workflow.EditLeaseHolder is null)
        {
            throw new CaseEditLeaseExpiredException(workflow.CaseId);
        }
        if (!string.Equals(workflow.EditLeaseHolder, actor.SubjectId, StringComparison.Ordinal)
            || !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(workflow.EditLeaseTokenHash),
                Convert.FromHexString(Hash(token))))
        {
            throw new CaseEditLeaseConflictException(workflow.CaseId);
        }
    }

    private static void ClearLease(CaseWorkflowEntity workflow)
    {
        workflow.EditLeaseTokenHash = null;
        workflow.EditLeaseHolder = null;
        workflow.EditLeaseOperationKey = null;
        workflow.EditLeaseExpiresAtUtc = null;
    }

    private static void AddEvent(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        ActionActor actor,
        string operationKey,
        string reason,
        string requestHash,
        string eventType,
        long beforeVersion,
        long afterVersion) => context.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Workflow = workflow,
            EventType = eventType,
            OperationKey = operationKey,
            RequestHash = requestHash,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = RolesJson(actor),
            Reason = reason,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            BeforeVersion = beforeVersion,
            AfterVersion = afterVersion
        });

    private static CaseWorkflowRecord Map(CaseWorkflowEntity entity) => new(
        entity.CaseId,
        new CaseIdentity(entity.CaseId, entity.Case.Principal.Code, entity.Case.Year, entity.Case.Sequence, entity.Case.Reference, entity.Case.AuditReference),
        Enum.Parse<CaseLifecycleState>(entity.State),
        entity.AssignedEngineerId,
        entity.ReportApproval is null ? null : new ReportApprovalEvidence(
            entity.ReportApproval.Id,
            entity.ReportApproval.ArtifactIdentity,
            entity.ReportApproval.ArtifactSha256,
            Actor(entity.ReportApproval.ApprovedByKind, entity.ReportApproval.ApprovedBySubjectId, entity.ReportApproval.ApprovedByRolesJson),
            entity.ReportApproval.ApprovedAtUtc),
        entity.ReportSentEvidence is null ? null : new ApprovedMailboxReportSentEvidence(
            entity.ReportSentEvidence.Id,
            entity.ReportSentEvidence.MailboxIdentity,
            entity.ReportSentEvidence.SentFolderIdentity,
            entity.ReportSentEvidence.ImmutableItemIdentity,
            entity.ReportSentEvidence.ConversationIdentity,
            entity.ReportSentEvidence.ReplyChainIdentity,
            entity.ReportSentEvidence.SentAtUtc,
            entity.ReportSentEvidence.LinkedAtUtc,
            Actor(entity.ReportSentEvidence.LinkedByKind, entity.ReportSentEvidence.LinkedBySubjectId, entity.ReportSentEvidence.LinkedByRolesJson)),
        entity.DueWork is null ? null : Map(entity.DueWork),
        entity.ClosureOutcome is null ? null : Enum.Parse<CaseClosureOutcome>(entity.ClosureOutcome),
        entity.Version);

    private static CaseDueWork Map(CaseDueWorkEntity entity) => new(
        entity.CaseId,
        entity.MissingMaterialReason,
        entity.DueBy,
        Enum.Parse<CaseDueWorkState>(entity.State),
        entity.NextChaseAtUtc,
        entity.HeldAtUtc,
        entity.RemainingChaseIntervalTicks is null ? null : TimeSpan.FromTicks(entity.RemainingChaseIntervalTicks.Value),
        entity.MostRecentChannel,
        entity.MostRecentOutcome,
        entity.MostRecentNote,
        entity.Version);

    private static ActionActor Actor(string kind, string subjectId, string rolesJson)
    {
        if (kind != nameof(ActorKind.Staff) || !Guid.TryParse(subjectId, out var staffId))
        {
            throw new InvalidOperationException("Workflow evidence contains an unsupported actor identity.");
        }
        return ActionActor.Staff(staffId, JsonSerializer.Deserialize<StaffRole[]>(rolesJson) ?? []);
    }

    private static string RolesJson(ActionActor actor) => JsonSerializer.Serialize(actor.Roles.OrderBy(role => role));
    private static string RequestHash<T>(T request) => Hash(JsonSerializer.Serialize(request));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
