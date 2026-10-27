using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Notifications;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The one writer of a Case's queries and of the workflow's open-query mark,
/// applying <see cref="CaseQueryRules"/> for every store: a message whose
/// classification opens a query joining a Case, a query opened from a message
/// or a channel,
/// Mark done, Reopen, Assign, and a flagged reply's observed Sent item. Each
/// caller owns its transaction and holds the Case's workflow row lock; the
/// mail paths return whether anything changed so the caller advances the
/// version and records the event through <see cref="AddQueryHistory"/>.
/// </summary>
internal static class CaseQueryLedger
{
    internal const string QueryReceivedEvent = "case_query_received";
    internal const string QueryWithdrawnEvent = "case_query_withdrawn";
    internal const string QueryLoggedEvent = "case_query_logged";
    internal const string QueryResolvedEvent = "case_query_resolved";
    internal const string QueryReopenedEvent = "case_query_reopened";
    internal const string QueryAssignedEvent = "case_query_assigned";

    /// <summary>The actor a matched query criterion records. Not a staff member.</summary>
    internal const string QueryCriteriaActor = "system-worker:query-criteria";

    /// <summary>The history reason a matched query criterion records.</summary>
    internal const string QueryCriteriaReason = "Matched query criteria";

    /// <summary>
    /// A received message has left the Case (unlink): withdraws the open query
    /// it raised, if any. A category correction does not.
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

    /// <summary>
    /// Opens a query on a linked received message. Refuses when that message
    /// already holds a non-withdrawn query. Returns the new query's id. The
    /// caller notifies the Case's Engineer.
    /// </summary>
    internal static async Task<Guid> OpenForMessageAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        Guid retainedMessageId,
        ActionActor actor,
        string operationKey,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var receiptId = await ReceiptOfMessageAsync(context, retainedMessageId, cancellationToken)
            ?? throw new InvalidOperationException("The message is not a retained receipt.");
        if (!await IsLinkedToCaseAsync(context, receiptId, workflow.CaseId, cancellationToken))
        {
            throw new InvalidOperationException("The message is not linked to this case.");
        }

        var withdrawn = nameof(CaseQueryClosure.Withdrawn);
        if (await context.CaseQueries.AnyAsync(
                item => item.CaseId == workflow.CaseId
                    && item.IntakeReceiptId == receiptId
                    && item.ClosedAs != withdrawn,
                cancellationToken))
        {
            throw new InvalidOperationException("This message already has a query.");
        }

        var id = Guid.NewGuid();
        context.CaseQueries.Add(new()
        {
            Id = id,
            CaseId = workflow.CaseId,
            Workflow = workflow,
            Source = nameof(CaseQuerySource.Mail),
            IntakeReceiptId = receiptId,
            RaisedAtUtc = occurredAtUtc,
            RaisedByKind = actor.Kind.ToString(),
            RaisedBySubjectId = actor.SubjectId,
            RaisedOperationKey = operationKey
        });
        workflow.HasOpenQuery = true;
        return id;
    }

    /// <summary>Opens a logged query (channel and details). A new query is Unassigned.</summary>
    internal static Guid OpenLogged(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        OpenCaseQueryRequest request,
        DateTimeOffset occurredAtUtc)
    {
        var id = Guid.NewGuid();
        context.CaseQueries.Add(new()
        {
            Id = id,
            CaseId = workflow.CaseId,
            Workflow = workflow,
            Source = nameof(CaseQuerySource.Staff),
            Channel = request.Channel!.Value.ToString(),
            Details = string.IsNullOrWhiteSpace(request.Details) ? null : request.Details.Trim(),
            RaisedAtUtc = occurredAtUtc,
            RaisedByKind = request.Actor.Kind.ToString(),
            RaisedBySubjectId = request.Actor.SubjectId,
            RaisedOperationKey = request.OperationKey.Trim()
        });
        workflow.HasOpenQuery = true;
        return id;
    }

    /// <summary>Marks one open query done and recomputes the open-query mark from the rows that remain.</summary>
    internal static async Task MarkDoneAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        CaseQueryEntity query,
        ActorKind actorKind,
        string actorSubjectId,
        DateTimeOffset occurredAtUtc,
        Guid? resolvedByMailOperationId,
        CancellationToken cancellationToken)
    {
        Close(query, nameof(CaseQueryClosure.Resolved), actorKind, actorSubjectId, occurredAtUtc, resolvedByMailOperationId);
        workflow.HasOpenQuery = await AnyOtherOpenAsync(context, workflow.CaseId, query.Id, cancellationToken);
    }

    /// <summary>Clears a Done query's closure and sets the open-query mark. The assignee is kept.</summary>
    internal static void Reopen(CaseWorkflowEntity workflow, CaseQueryEntity query)
    {
        query.ClosedAs = null;
        query.ClosedAtUtc = null;
        query.ClosedByKind = null;
        query.ClosedBySubjectId = null;
        query.ResolvedByMailOperationId = null;
        workflow.HasOpenQuery = true;
    }

    /// <summary>
    /// Assigns the query, or sets it Unassigned. Returns the history reason:
    /// "Assigned to {name}", "Taken by {name}" or "Set to Unassigned".
    /// </summary>
    internal static async Task<string> AssignAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        CaseQueryEntity query,
        ActionActor actor,
        Guid? assigneeStaffId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        string reason;
        if (assigneeStaffId is null)
        {
            query.AssigneeStaffId = null;
            query.AssignedAtUtc = null;
            reason = "Set to Unassigned";
        }
        else
        {
            var name = await context.Users.AsNoTracking()
                .Where(user => user.Id == assigneeStaffId && user.IsEnabled && user.UserName != null)
                .Select(user => user.UserName)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("The assignee is not an enabled member of staff.");
            query.AssigneeStaffId = assigneeStaffId;
            query.AssignedAtUtc = occurredAtUtc;
            var actorStaffId = StaffNotificationPolicy.StaffId(actor);
            reason = assigneeStaffId == actorStaffId ? $"Taken by {name}" : $"Assigned to {name}";
            Notify(
                context,
                workflow,
                StaffNotificationCause.QueryAssigned,
                assigneeStaffId,
                actor,
                StaffNotificationPolicy.CaseRoute(workflow.CaseId, "queries"),
                occurredAtUtc);
        }

        return reason;
    }

    /// <summary>The Case's Engineer is told when a query is opened from an e-mail, unless they opened it.</summary>
    internal static void NotifyQueryReceived(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        ActionActor actor,
        DateTimeOffset occurredAtUtc) =>
        Notify(
            context,
            workflow,
            StaffNotificationCause.QueryReceived,
            workflow.AssignedEngineerId,
            actor,
            StaffNotificationPolicy.CaseRoute(workflow.CaseId, "correspondence"),
            occurredAtUtc);

    /// <summary>
    /// A reply flagged Mark query done reached Sent: marks that replied
    /// message's own open query done, under the workflow row lock, attributed
    /// to the reply's sender. Returns rather than throws, so the mailbox's
    /// Sent poll never stalls on a message that has no open query.
    /// </summary>
    internal static async Task ResolveForObservedReplyAsync(
        PegasusDbContext context,
        StaffMailSendOperationEntity mail,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        if (!mail.ResolvesCaseQueries || mail.OriginalRetainedMessageId is not { } messageId)
        {
            return;
        }

        await EfCaseWorkflowStore.AcquireWorkflowMutationLockAsync(context, mail.ContextId, cancellationToken);
        var workflow = await context.CaseWorkflows
            .SingleOrDefaultAsync(item => item.CaseId == mail.ContextId, cancellationToken);
        if (workflow is null)
        {
            return;
        }

        var receiptId = await ReceiptOfMessageAsync(context, messageId, cancellationToken);
        var open = receiptId is null
            ? null
            : await context.CaseQueries.SingleOrDefaultAsync(
                item => item.CaseId == workflow.CaseId
                    && item.IntakeReceiptId == receiptId
                    && item.ClosedAtUtc == null,
                cancellationToken);
        if (!CaseQueryRules.ResolvesOnObservedReply(mail.ResolvesCaseQueries, open is not null))
        {
            return;
        }

        var beforeVersion = workflow.Version;
        var beforeJson = SnapshotState(workflow);
        await MarkDoneAsync(
            context, workflow, open!, mail.ActorKind, mail.ActorSubjectId, occurredAtUtc, mail.Id, cancellationToken);
        CaseMutationGuard.Advance(workflow);
        AddQueryHistory(
            context, workflow, beforeVersion, QueryResolvedEvent,
            mail.ActorKind, mail.ActorSubjectId, "[]",
            $"query-reply:{mail.Id:N}", mail.PayloadHash, null, occurredAtUtc, beforeJson);
    }

    /// <summary>
    /// The link the caller just wrote may still be only in the change tracker.
    /// A saved link is read the same way every other association is.
    /// </summary>
    private static async Task<bool> IsLinkedToCaseAsync(
        PegasusDbContext context,
        Guid receiptId,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var tracked = context.IntakeManualAssociations.Local
            .FirstOrDefault(item => item.IntakeReceiptId == receiptId && item.IsActive);
        if (tracked is not null)
        {
            return tracked.CaseId == caseId;
        }

        var associations = await CurrentIntakeAssociations.ReadAsync(context, [receiptId], cancellationToken);
        return associations.Current.TryGetValue(receiptId, out var link) && link.CaseId == caseId;
    }

    private static async Task<Guid?> ReceiptOfMessageAsync(
        PegasusDbContext context,
        Guid retainedMessageId,
        CancellationToken cancellationToken)
    {
        var token = await context.RetainedMailboxMessages.AsNoTracking()
            .Where(item => item.Id == retainedMessageId)
            .Select(item => item.ExternalReceiptToken)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        return await context.IntakeReceipts.AsNoTracking()
            .Where(item => item.ExternalReceiptToken == token)
            .Select(item => (Guid?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static Task<bool> AnyOtherOpenAsync(
        PegasusDbContext context,
        Guid caseId,
        Guid exceptId,
        CancellationToken cancellationToken) =>
        context.CaseQueries.AnyAsync(
            item => item.CaseId == caseId && item.ClosedAtUtc == null && item.Id != exceptId,
            cancellationToken);

    private static void Notify(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        StaffNotificationCause cause,
        Guid? recipientStaffId,
        ActionActor actor,
        string route,
        DateTimeOffset occurredAtUtc)
    {
        var recipient = StaffNotificationPolicy.Recipient(
            cause,
            recipientStaffId,
            jobStarterStaffId: null,
            StaffNotificationPolicy.StaffId(actor),
            assigneeStaffId: recipientStaffId);
        if (recipient is not { } staffId || workflow.Case is null)
        {
            return;
        }

        context.Set<StaffNotificationEntity>().Add(new()
        {
            Id = Guid.NewGuid(),
            StaffId = staffId,
            CaseId = workflow.CaseId,
            Reference = workflow.Case.Reference,
            Registration = null,
            Cause = cause.ToString(),
            Route = route,
            ActorSubjectId = actor.Kind == ActorKind.Staff ? actor.SubjectId : null,
            RaisedAtUtc = occurredAtUtc
        });
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
