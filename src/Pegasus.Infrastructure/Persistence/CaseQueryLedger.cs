using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The one writer of a Case's queries and of the workflow's open-query mark,
/// applying <see cref="CaseQueryRules"/> for every store: a post-report
/// receipt joining or leaving a Case, a staff-logged query, Resolve query, and
/// a flagged reply's observed Sent item. Each caller owns its transaction and
/// holds the Case's workflow row lock; the mail paths return whether anything
/// changed so the caller advances the version and records the event through
/// <see cref="AddQueryHistory"/>.
/// </summary>
internal static class CaseQueryLedger
{
    internal const string QueryReceivedEvent = "case_query_received";
    internal const string QueryWithdrawnEvent = "case_query_withdrawn";
    internal const string QueryLoggedEvent = "case_query_logged";
    internal const string QueriesResolvedEvent = "case_queries_resolved";

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

    /// <summary>
    /// A received message has joined the Case (automatic match, staff link or
    /// correction): raises a Mail query when the Core rule says so. True when
    /// a row was added and the open-query mark set.
    /// </summary>
    internal static async Task<bool> RaiseForMailAsync(
        PegasusDbContext context,
        IntakeReceiptEntity receipt,
        CaseWorkflowEntity workflow,
        ActorKind actorKind,
        string actorSubjectId,
        string operationKey,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var state = Enum.Parse<CaseLifecycleState>(workflow.State);
        var isPostReport = IsPostReport(receipt);
        if (!CaseQueryRules.RaisesOnMailJoined(state, isPostReport, alreadyOpenForMessage: false))
        {
            return false;
        }
        var alreadyOpen = await context.CaseQueries
            .AnyAsync(item => item.CaseId == workflow.CaseId
                && item.IntakeReceiptId == receipt.Id
                && item.ClosedAtUtc == null, cancellationToken);
        if (!CaseQueryRules.RaisesOnMailJoined(state, isPostReport, alreadyOpen))
        {
            return false;
        }
        context.CaseQueries.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Workflow = workflow,
            Source = nameof(CaseQuerySource.Mail),
            IntakeReceiptId = receipt.Id,
            RaisedAtUtc = occurredAtUtc,
            RaisedByKind = actorKind.ToString(),
            RaisedBySubjectId = actorSubjectId,
            RaisedOperationKey = operationKey
        });
        workflow.HasOpenQuery = true;
        return true;
    }

    /// <summary>
    /// A received message has left the Case (unlink) or stopped being
    /// post-report (correction): withdraws the open query it raised, if any.
    /// True when a row closed; the mark is recomputed from the remaining rows.
    /// </summary>
    internal static async Task<bool> WithdrawForMailAsync(
        PegasusDbContext context,
        IntakeReceiptEntity receipt,
        CaseWorkflowEntity workflow,
        ActorKind actorKind,
        string actorSubjectId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var open = await context.CaseQueries
            .SingleOrDefaultAsync(item => item.CaseId == workflow.CaseId
                && item.IntakeReceiptId == receipt.Id
                && item.ClosedAtUtc == null, cancellationToken);
        if (!CaseQueryRules.WithdrawsOnMailLeft(open is not null))
        {
            return false;
        }
        Close(open!, nameof(CaseQueryClosure.Withdrawn), actorKind, actorSubjectId, occurredAtUtc, null);
        workflow.HasOpenQuery = await context.CaseQueries
            .AnyAsync(item => item.CaseId == workflow.CaseId
                && item.ClosedAtUtc == null
                && item.Id != open!.Id, cancellationToken);
        return true;
    }

    /// <summary>A staff member or Automation logs a query (Channel and Details); runs inside the workflow mutation.</summary>
    internal static void RaiseForStaff(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        RaiseCaseQueryRequest request,
        DateTimeOffset occurredAtUtc)
    {
        context.CaseQueries.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Workflow = workflow,
            Source = nameof(CaseQuerySource.Staff),
            Channel = request.Channel.ToString(),
            Details = string.IsNullOrWhiteSpace(request.Details) ? null : request.Details.Trim(),
            RaisedAtUtc = occurredAtUtc,
            RaisedByKind = request.Actor.Kind.ToString(),
            RaisedBySubjectId = request.Actor.SubjectId,
            RaisedOperationKey = request.OperationKey.Trim()
        });
        workflow.HasOpenQuery = true;
    }

    /// <summary>
    /// Resolves every open query on the Case (operator, 11 October 2026) and
    /// clears the mark. Returns how many closed; the caller decides whether
    /// zero is a refusal (an action) or a no-op (an observed Sent item).
    /// </summary>
    internal static async Task<int> ResolveAllAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        ActorKind actorKind,
        string actorSubjectId,
        DateTimeOffset occurredAtUtc,
        Guid? resolvedByMailOperationId,
        CancellationToken cancellationToken)
    {
        var open = await context.CaseQueries
            .Where(item => item.CaseId == workflow.CaseId && item.ClosedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var item in open)
        {
            Close(item, nameof(CaseQueryClosure.Resolved), actorKind, actorSubjectId, occurredAtUtc, resolvedByMailOperationId);
        }
        workflow.HasOpenQuery = false;
        return open.Count;
    }

    /// <summary>
    /// A reply flagged "Also resolve query" reached Sent: resolves the Case's
    /// open queries under the workflow row lock, attributed to the reply's
    /// sender, advancing the version as any system write does. Returns rather
    /// than throws, so the mailbox's Sent poll never stalls on a Case that has
    /// nothing open.
    /// </summary>
    internal static async Task ResolveForObservedReplyAsync(
        PegasusDbContext context,
        StaffMailSendOperationEntity mail,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        if (!mail.ResolvesCaseQueries)
        {
            return;
        }
        await EfCaseWorkflowStore.AcquireWorkflowMutationLockAsync(context, mail.ContextId, cancellationToken);
        var workflow = await context.CaseWorkflows
            .SingleOrDefaultAsync(item => item.CaseId == mail.ContextId, cancellationToken);
        if (workflow is null || !CaseQueryRules.ResolvesOnObservedReply(mail.ResolvesCaseQueries, workflow.HasOpenQuery))
        {
            return;
        }
        var beforeVersion = workflow.Version;
        var beforeJson = SnapshotState(workflow);
        await ResolveAllAsync(
            context, workflow, mail.ActorKind, mail.ActorSubjectId, occurredAtUtc, mail.Id, cancellationToken);
        CaseMutationGuard.Advance(workflow);
        AddQueryHistory(
            context, workflow, beforeVersion, QueriesResolvedEvent,
            mail.ActorKind, mail.ActorSubjectId, "[]",
            $"query-reply:{mail.Id:N}", mail.PayloadHash, null, occurredAtUtc, beforeJson);
    }

    /// <summary>One workflow event and its action-history row for a query raised, withdrawn or resolved outside a lease-carrying mutation.</summary>
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
        string beforeJson) =>
        AddQueryHistory(
            context, workflow, beforeVersion, eventType,
            actor.Kind, actor.SubjectId, JsonSerializer.Serialize(actor.Roles.OrderBy(role => role)),
            operationKey, requestHash, reason, occurredAtUtc, beforeJson);

    internal static void AddQueryHistory(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        long beforeVersion,
        string eventType,
        ActorKind actorKind,
        string actorSubjectId,
        string rolesJson,
        string operationKey,
        string requestHash,
        string? reason,
        DateTimeOffset occurredAtUtc,
        string beforeJson)
    {
        var resultJson = SnapshotState(workflow);
        context.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Workflow = workflow,
            EventType = eventType,
            OperationKey = operationKey,
            RequestHash = requestHash,
            ActorKind = actorKind.ToString(),
            ActorSubjectId = actorSubjectId,
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
            ActorKind = actorKind.ToString(),
            ActorSubjectId = actorSubjectId,
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

    internal static string SnapshotState(CaseWorkflowEntity workflow) =>
        JsonSerializer.Serialize(new
        {
            workflow.State,
            workflow.HasOpenQuery,
            workflow.Version
        });

    private static void Close(
        CaseQueryEntity query,
        string closedAs,
        ActorKind actorKind,
        string actorSubjectId,
        DateTimeOffset occurredAtUtc,
        Guid? resolvedByMailOperationId)
    {
        query.ClosedAs = closedAs;
        query.ClosedAtUtc = occurredAtUtc;
        query.ClosedByKind = actorKind.ToString();
        query.ClosedBySubjectId = actorSubjectId;
        query.ResolvedByMailOperationId = resolvedByMailOperationId;
    }
}
