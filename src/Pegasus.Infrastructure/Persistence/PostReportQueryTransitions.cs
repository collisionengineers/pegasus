using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Operations;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Applies <see cref="PostReportQueryRules"/> for the stores: the one entity
/// predicate for a received post-report receipt, the observed-reply read, and
/// the workflow writes for entering Query, completing on an observed reply and
/// withdrawing from Query. Each caller owns its transaction, completes the
/// workflow (version and lease) and records its own link, unlink or correction
/// event; this class writes only the Query-specific history rows.
/// </summary>
internal static class PostReportQueryTransitions
{
    internal const string QueryReceivedEvent = "case_query_received";
    internal const string QueryWithdrawnEvent = "case_query_withdrawn";
    internal const string QueryRepliedEvent = "case_query_replied";

    /// <summary>A mailbox receipt whose current classification is a received post-report family.</summary>
    internal static readonly Expression<Func<IntakeReceiptEntity, bool>> IsPostReportReceipt =
        item => item.SourceChannel == "mailbox"
            && item.MailClassificationDecision != null
            && item.MailClassificationDecision.Outcome == "classified"
            && item.MailClassificationDecision.Direction == "received"
            && item.MailClassificationDecision.Family == "post-report-emails";

    internal static bool IsPostReport(IntakeReceiptEntity receipt) =>
        receipt.SourceChannel == "mailbox" && IsPostReport(receipt.MailClassificationDecision);

    internal static bool IsPostReport(IntakeMailClassificationDecisionEntity? decision) =>
        decision is { Outcome: "classified", Direction: "received", Family: "post-report-emails" };

    internal sealed record Entry(
        PostReportQueryEntry Kind,
        ObservedQueryReply? ObservedReply,
        string? BeforeReplyJson)
    {
        internal static readonly Entry None = new(PostReportQueryEntry.None, null, null);
    }

    internal sealed record ObservedQueryReply(
        Guid MailOperationId,
        Guid RetainedMessageId,
        string SentImmutableMessageId,
        DateTimeOffset ProviderSentAtUtc,
        DateTimeOffset ObservedAtUtc,
        ActorKind ActorKind,
        string ActorSubjectId,
        string PayloadHash);

    /// <summary>
    /// A post-report receipt has joined the Case: sets the workflow's state
    /// fields per the Core rule and says what happened. On
    /// <see cref="PostReportQueryEntry.EnterQuery"/> the caller records its own
    /// event; on <see cref="PostReportQueryEntry.CompleteWithObservedReply"/> it
    /// calls <see cref="AddObservedReplyHistory"/> once the workflow is completed.
    /// </summary>
    internal static async Task<Entry> EnterAsync(
        PegasusDbContext context,
        IntakeReceiptEntity receipt,
        CaseWorkflowEntity workflow,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        if (!IsPostReport(receipt))
        {
            return Entry.None;
        }
        var state = Enum.Parse<CaseLifecycleState>(workflow.State);
        if (PostReportQueryRules.OnPostReportLinked(state, replyObserved: false) == PostReportQueryEntry.None)
        {
            return Entry.None;
        }
        var observedReply = await FindObservedQueryReplyAsync(context, receipt, workflow.CaseId, cancellationToken);
        var kind = PostReportQueryRules.OnPostReportLinked(state, observedReply is not null);
        if (kind == PostReportQueryEntry.EnterQuery)
        {
            workflow.State = nameof(CaseLifecycleState.Query);
            workflow.StateEnteredAtUtc = occurredAtUtc;
            workflow.ClosureOutcome = null;
            return new(kind, null, null);
        }
        var beforeJson = SnapshotQueryReply(workflow, observedReply!);
        workflow.State = nameof(CaseLifecycleState.PostReportComplete);
        workflow.ClosureOutcome = nameof(CaseClosureOutcome.PostReportComplete);
        return new(kind, observedReply, beforeJson);
    }

    /// <summary>
    /// A post-report receipt has left the Case, by unlink or by correction. When
    /// the Core rule says the query is withdrawn, sets the workflow back to
    /// Completed and returns the before snapshot for the caller's
    /// <see cref="QueryWithdrawnEvent"/>; otherwise null and nothing changes.
    /// </summary>
    internal static async Task<string?> WithdrawAsync(
        PegasusDbContext context,
        IntakeReceiptEntity receipt,
        CaseWorkflowEntity workflow,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var state = Enum.Parse<CaseLifecycleState>(workflow.State);
        if (state != CaseLifecycleState.Query)
        {
            return null;
        }
        var observedReply = await FindObservedQueryReplyAsync(context, receipt, workflow.CaseId, cancellationToken);
        var otherLinked = await OtherPostReportLinkedAsync(context, receipt.Id, workflow.CaseId, cancellationToken);
        if (!PostReportQueryRules.ShouldWithdraw(state, observedReply is not null, otherLinked))
        {
            return null;
        }
        var beforeJson = SnapshotState(workflow);
        workflow.State = nameof(CaseLifecycleState.PostReportComplete);
        workflow.StateEnteredAtUtc = occurredAtUtc;
        workflow.ClosureOutcome = nameof(CaseClosureOutcome.PostReportComplete);
        return beforeJson;
    }

    /// <summary>The Query entry or withdrawal of a correction or an unlink: one workflow event and its action-history row.</summary>
    internal static void AddQueryHistory(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        long beforeVersion,
        string eventType,
        ActionActor actor,
        string operationKey,
        string requestHash,
        string? reason,
        DateTimeOffset occurredAtUtc,
        string beforeJson)
    {
        var resultJson = SnapshotState(workflow);
        var rolesJson = JsonSerializer.Serialize(actor.Roles.OrderBy(role => role));
        context.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Workflow = workflow,
            EventType = eventType,
            OperationKey = operationKey,
            RequestHash = requestHash,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = rolesJson,
            Reason = reason,
            OccurredAtUtc = occurredAtUtc,
            BeforeVersion = beforeVersion,
            AfterVersion = workflow.Version,
            ResultJson = resultJson
        });
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = "case",
            AggregateId = workflow.CaseId.ToString("D"),
            EventKind = eventType,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = rolesJson,
            OccurredAtUtc = occurredAtUtc,
            Outcome = "Succeeded",
            CorrelationId = operationKey,
            Reason = reason,
            BeforeJson = beforeJson,
            AfterJson = resultJson,
            PolicyVersion = "case-lifecycle-v1"
        });
    }

    internal static void AddObservedReplyHistory(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        long beforeVersion,
        ObservedQueryReply observedReply,
        string beforeJson)
    {
        const string reason = "Confirmed reply to retained post-report query.";
        var operationKey = $"query-reply:{observedReply.MailOperationId:N}";
        var resultJson = SnapshotQueryReply(workflow, observedReply);
        context.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Workflow = workflow,
            EventType = QueryRepliedEvent,
            OperationKey = operationKey,
            RequestHash = observedReply.PayloadHash,
            ActorKind = observedReply.ActorKind.ToString(),
            ActorSubjectId = observedReply.ActorSubjectId,
            ActorRolesJson = "[]",
            Reason = reason,
            OccurredAtUtc = observedReply.ObservedAtUtc,
            BeforeVersion = beforeVersion,
            AfterVersion = workflow.Version,
            ResultJson = resultJson
        });
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = "case",
            AggregateId = workflow.CaseId.ToString("D"),
            EventKind = QueryRepliedEvent,
            ActorKind = observedReply.ActorKind.ToString(),
            ActorSubjectId = observedReply.ActorSubjectId,
            ActorRolesJson = "[]",
            OccurredAtUtc = observedReply.ObservedAtUtc,
            Outcome = "Succeeded",
            CorrelationId = operationKey,
            Reason = reason,
            BeforeJson = beforeJson,
            AfterJson = resultJson,
            PolicyVersion = "case-lifecycle-v1"
        });
    }

    internal static string SnapshotState(CaseWorkflowEntity workflow) =>
        JsonSerializer.Serialize(new
        {
            workflow.State,
            workflow.ClosureOutcome,
            workflow.Version
        });

    private static string SnapshotQueryReply(
        CaseWorkflowEntity workflow,
        ObservedQueryReply observedReply) =>
        JsonSerializer.Serialize(new
        {
            workflow.State,
            workflow.ClosureOutcome,
            workflow.Version,
            RetainedReply = new
            {
                observedReply.RetainedMessageId,
                observedReply.SentImmutableMessageId,
                observedReply.ProviderSentAtUtc,
                observedReply.ObservedAtUtc,
                observedReply.ActorSubjectId
            }
        });

    // Durable Sent evidence for a reply to this receipt's retained message,
    // sent from this Case; a prepared, submitted or unrelated send is not one.
    private static async Task<ObservedQueryReply?> FindObservedQueryReplyAsync(
        PegasusDbContext context,
        IntakeReceiptEntity receipt,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var retainedMessageIds = await context.RetainedMailboxMessages.AsNoTracking()
            .Where(item => item.ExternalReceiptToken == receipt.ExternalReceiptToken)
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        if (retainedMessageIds.Length == 0)
        {
            return null;
        }

        return await context.Set<StaffMailSendOperationEntity>().AsNoTracking()
            .Where(item => item.ContextId == caseId
                && item.Purpose == StaffMailPurpose.GeneralCorrespondence
                && (item.ComposeMode == StaffMailComposeMode.Reply || item.ComposeMode == StaffMailComposeMode.ReplyAll)
                && item.OriginalRetainedMessageId != null
                && retainedMessageIds.Contains(item.OriginalRetainedMessageId.Value)
                && item.State == StaffMailState.Sent
                && item.AttemptStage == StaffMailAttemptStage.ObserveSent
                && item.ObservedSentImmutableMessageId != null
                && item.ProviderSentAtUtc != null
                && item.ObservedSentAtUtc != null)
            .Select(item => new ObservedQueryReply(
                item.Id,
                item.OriginalRetainedMessageId!.Value,
                item.ObservedSentImmutableMessageId!,
                item.ProviderSentAtUtc!.Value,
                item.ObservedSentAtUtc!.Value,
                item.ActorKind,
                item.ActorSubjectId,
                item.PayloadHash))
            .SingleOrDefaultAsync(cancellationToken);
    }

    // Any other receipt currently linked to this Case, by staff or by its
    // accepted origin, whose current classification is post-report.
    private static async Task<bool> OtherPostReportLinkedAsync(
        PegasusDbContext context,
        Guid receiptId,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var manual = await context.IntakeManualAssociations.AsNoTracking()
            .Where(item => item.CaseId == caseId && item.IsActive && item.IntakeReceiptId != receiptId)
            .Select(item => item.IntakeReceiptId)
            .ToListAsync(cancellationToken);
        var accepted = await context.CaseIntakeLinks.AsNoTracking()
            .Where(item => item.CaseId == caseId && item.IntakeReceiptId != receiptId)
            .Select(item => item.IntakeReceiptId)
            .ToListAsync(cancellationToken);
        var candidates = manual.Concat(accepted).Distinct().ToArray();
        if (candidates.Length == 0)
        {
            return false;
        }
        var associations = await CurrentIntakeAssociations.ReadAsync(context, candidates, cancellationToken);
        var current = associations.Current
            .Where(pair => pair.Value.CaseId == caseId)
            .Select(pair => pair.Key)
            .ToArray();
        return current.Length > 0
            && await context.IntakeReceipts.AsNoTracking()
                .Where(IsPostReportReceipt)
                .AnyAsync(item => current.Contains(item.Id), cancellationToken);
    }
}
