using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Lifecycle;

/// <summary>
/// The one owner of when a Case gains or loses an open query (FRD-13 "Report
/// sent and queries"; operator, 27 October 2026). A query is a record on the
/// Case, never a state: an e-mail that matches query criteria joining a Case
/// whose report is sent opens one, staff or Automation open one, and each open
/// query is marked done on its own. "Report is sent" is
/// <see cref="CaseLifecycleRules.ReportIsSent(CaseWorkflowRecord)"/>: Report
/// sent, or With Engineer holding the Inspection report's Sent item while the
/// Audit is still to come. The stores apply these answers inside the
/// transaction that links, corrects, unlinks or sends. Query acts take no edit
/// lease, expected version or reason.
/// </summary>
public static class CaseQueryRules
{
    public const int MaximumOperationKeyLength = 100;

    /// <summary>A query may be opened only once the report is sent, on a Case that is not archived.</summary>
    public static bool CanOpen(CaseWorkflowRecord workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        return CaseLifecycleRules.ReportIsSent(workflow) && workflow.Archive is null;
    }

    /// <summary><see cref="CanOpen"/> asked of the facts the transition store reads under its lock.</summary>
    public static void RequireCanOpen(bool reportIsSent, bool archived)
    {
        if (!reportIsSent || archived)
        {
            throw new InvalidOperationException("A query can be opened only once the report is sent.");
        }
    }

    /// <summary>Mark done is live on an open query of a Case that is not archived.</summary>
    public static bool CanMarkDone(bool isOpen, bool archived) => isOpen && !archived;

    /// <summary><see cref="CanMarkDone"/> asked of the facts the store reads under its lock.</summary>
    public static void RequireCanMarkDone(bool isOpen, bool archived)
    {
        if (!CanMarkDone(isOpen, archived))
        {
            throw new InvalidOperationException("The query is not open.");
        }
    }

    /// <summary>
    /// Reopen is live on a Done query (resolved, not withdrawn) while the Case
    /// is neither archived nor closed. Any staff role and Automation may call
    /// it; the store does not add a further role gate.
    /// </summary>
    public static bool CanReopen(bool isResolvedNotWithdrawn, bool archived, bool closed) =>
        isResolvedNotWithdrawn && !archived && !closed;

    /// <summary><see cref="CanReopen"/> asked of the facts the store reads under its lock.</summary>
    public static void RequireCanReopen(bool isResolvedNotWithdrawn, bool archived, bool closed)
    {
        if (!CanReopen(isResolvedNotWithdrawn, archived, closed))
        {
            throw new InvalidOperationException("The query cannot be reopened.");
        }
    }

    /// <summary>
    /// A received message has joined the Case, by automatic match, staff link
    /// or correction: it opens a query when the Case's report is sent
    /// (<paramref name="reportIsSent"/>, the Case's
    /// <see cref="CaseLifecycleRules.ReportIsSent(CaseLifecycleState, bool, CaseType, bool)"/>),
    /// the message is currently classified as a query, and that message does
    /// not already hold an open query on the Case.
    /// </summary>
    public static bool RaisesOnMailJoined(bool reportIsSent, bool isQuery, bool alreadyOpenForMessage) =>
        reportIsSent && isQuery && !alreadyOpenForMessage;

    /// <summary>
    /// A message has left the Case or stopped being a query: the open
    /// query it raised, if any, is withdrawn. A resolved query stays resolved.
    /// </summary>
    public static bool WithdrawsOnMailLeft(bool openForMessage) => openForMessage;

    /// <summary>
    /// A reply flagged Mark query done marks that replied message's own open
    /// query done when its Sent item is observed. Another open query on the
    /// Case is left open.
    /// </summary>
    public static bool ResolvesOnObservedReply(bool flagged, bool messageHasOpenQuery) =>
        flagged && messageHasOpenQuery;

    /// <summary>
    /// The Query stage: an open Case with an open query reads as Query on every
    /// screen, queue and filter, whatever its state (operator, 11 October 2026).
    /// </summary>
    public static bool IsQueryStage(CaseLifecycleState state, bool hasOpenQuery) =>
        hasOpenQuery && !CaseLifecycleRules.IsTerminal(state);

    /// <summary>
    /// Exactly one of a linked message and a channel. A message query has no
    /// details; a logged query names a known channel.
    /// </summary>
    public static void ValidateOpen(OpenCaseQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(request));
        }

        ValidateActor(request.Actor, request.OperationKey);
        var hasMessage = request.RetainedMessageId is not null;
        var hasChannel = request.Channel is not null;
        if (hasMessage == hasChannel)
        {
            throw new ArgumentException(
                "A query is opened from a message or a channel, not both and not neither.",
                nameof(request));
        }

        if (request.RetainedMessageId == Guid.Empty)
        {
            throw new ArgumentException("A message identifier is required.", nameof(request));
        }

        if (hasChannel && !Enum.IsDefined(request.Channel!.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The query channel is invalid.");
        }

        if (hasMessage && !string.IsNullOrWhiteSpace(request.Details))
        {
            throw new ArgumentException("A message query has no details.", nameof(request));
        }
    }

    /// <summary>A query act names the query, the actor and an operation key. Nothing else.</summary>
    public static void ValidateAct(CaseQueryActRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.QueryId == Guid.Empty)
        {
            throw new ArgumentException("A query identifier is required.", nameof(request));
        }

        ValidateActor(request.Actor, request.OperationKey);
    }

    /// <summary>Assign names the query and, when not Unassigned, a staff id.</summary>
    public static void ValidateAssign(AssignCaseQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.QueryId == Guid.Empty)
        {
            throw new ArgumentException("A query identifier is required.", nameof(request));
        }

        if (request.AssigneeStaffId == Guid.Empty)
        {
            throw new ArgumentException("An assignee identifier is required.", nameof(request));
        }

        ValidateActor(request.Actor, request.OperationKey);
    }

    private static void ValidateActor(ActionActor actor, string operationKey)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (string.IsNullOrWhiteSpace(operationKey) || operationKey.Trim().Length > MaximumOperationKeyLength)
        {
            throw new ArgumentException("An operation key is required.", nameof(operationKey));
        }
    }
}

/// <summary>Opens one query on a Report sent Case, from a linked e-mail or a logged channel.</summary>
public sealed class OpenCaseQuery(ICaseWorkflowStore store) : IOpenCaseQuery
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<Guid> ExecuteAsync(OpenCaseQueryRequest request, CancellationToken cancellationToken)
    {
        CaseQueryRules.ValidateOpen(request);
        return _store.OpenQueryAsync(request, cancellationToken);
    }
}

/// <summary>Marks one open query done.</summary>
public sealed class MarkCaseQueryDone(ICaseWorkflowStore store) : IMarkCaseQueryDone
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<CaseWorkflowRecord> ExecuteAsync(CaseQueryActRequest request, CancellationToken cancellationToken)
    {
        CaseQueryRules.ValidateAct(request);
        return _store.MarkQueryDoneAsync(request, cancellationToken);
    }
}

/// <summary>Reopens one Done query. Any staff role and Automation may call it.</summary>
public sealed class ReopenCaseQuery(ICaseWorkflowStore store) : IReopenCaseQuery
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<CaseWorkflowRecord> ExecuteAsync(CaseQueryActRequest request, CancellationToken cancellationToken)
    {
        CaseQueryRules.ValidateAct(request);
        return _store.ReopenQueryAsync(request, cancellationToken);
    }
}

/// <summary>Assigns one open query, or sets it Unassigned. Taking it posts the actor.</summary>
public sealed class AssignCaseQuery(ICaseWorkflowStore store) : IAssignCaseQuery
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<CaseWorkflowRecord> ExecuteAsync(AssignCaseQueryRequest request, CancellationToken cancellationToken)
    {
        CaseQueryRules.ValidateAssign(request);
        return _store.AssignQueryAsync(request, cancellationToken);
    }
}
