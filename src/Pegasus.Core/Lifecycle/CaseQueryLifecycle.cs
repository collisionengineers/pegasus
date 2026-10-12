using Pegasus.Core.Workflow;

namespace Pegasus.Core.Lifecycle;

/// <summary>
/// The one owner of when a Case gains or loses an open query (FRD-13 "Report
/// sent and queries", operator 11 October 2026). A query is a record on the
/// Case, never a state: an e-mail classified as a query joining a Report sent Case raises
/// one, staff or Automation log one, and every open query resolves together.
/// The stores apply these answers inside the transaction that links, corrects,
/// unlinks or sends.
/// </summary>
public static class CaseQueryRules
{
    /// <summary>Staff may log a query only once the report is sent, on a Case that is not archived.</summary>
    public static bool CanLog(CaseWorkflowRecord workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        return workflow.State == CaseLifecycleState.ReportSent && workflow.Archive is null;
    }

    /// <summary>Resolve query is live while any query is open, on a Case that is not archived.</summary>
    public static bool CanResolve(CaseWorkflowRecord workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        return workflow.HasOpenQuery && workflow.Archive is null;
    }

    public static void RequireCanLog(CaseWorkflowRecord workflow)
    {
        if (!CanLog(workflow))
        {
            throw new InvalidOperationException("A query can be logged only once the report is sent.");
        }
    }

    public static void RequireCanResolve(CaseWorkflowRecord workflow)
    {
        if (!CanResolve(workflow))
        {
            throw new InvalidOperationException("The case has no open query to resolve.");
        }
    }

    /// <summary>
    /// A received message has joined the Case, by automatic match, staff link
    /// or correction: it raises a query when the Case's report is sent, the
    /// message is currently classified as a query, and that message does not
    /// already hold an open query on the Case.
    /// </summary>
    public static bool RaisesOnMailJoined(CaseLifecycleState state, bool isQuery, bool alreadyOpenForMessage) =>
        state == CaseLifecycleState.ReportSent && isQuery && !alreadyOpenForMessage;

    /// <summary>
    /// A message has left the Case or stopped being a query: the open
    /// query it raised, if any, is withdrawn. A resolved query stays resolved.
    /// </summary>
    public static bool WithdrawsOnMailLeft(bool openForMessage) => openForMessage;

    /// <summary>A reply flagged "Also resolve query" resolves the open queries when its Sent item is observed.</summary>
    public static bool ResolvesOnObservedReply(bool flagged, bool hasOpenQuery) => flagged && hasOpenQuery;

    /// <summary>
    /// The Query stage: an open Case with an open query reads as Query on every
    /// screen, queue and filter, whatever its state (operator, 11 October 2026).
    /// </summary>
    public static bool IsQueryStage(CaseLifecycleState state, bool hasOpenQuery) =>
        hasOpenQuery && !CaseLifecycleRules.IsTerminal(state);
}

/// <summary>Logs a staff- or Automation-sourced query on a Report sent Case.</summary>
public sealed class RaiseCaseQuery(ICaseWorkflowStore store) : IRaiseCaseQuery
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseWorkflowRecord> ExecuteAsync(RaiseCaseQueryRequest request, CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateMutation(request, requireReason: false);
        if (!Enum.IsDefined(request.Channel))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The query channel is invalid.");
        }

        var current = await CaseLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        if (!await _store.HasOperationAsync(request.CaseId, request.OperationKey, cancellationToken))
        {
            CaseQueryRules.RequireCanLog(current);
        }

        return await _store.RaiseQueryAsync(request, cancellationToken);
    }
}

/// <summary>Resolves every open query on the Case at once.</summary>
public sealed class ResolveCaseQueries(ICaseWorkflowStore store) : IResolveCaseQueries
{
    private readonly ICaseWorkflowStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseWorkflowRecord> ExecuteAsync(ResolveCaseQueriesRequest request, CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateMutation(request, requireReason: false);
        var current = await CaseLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        if (!await _store.HasOperationAsync(request.CaseId, request.OperationKey, cancellationToken))
        {
            CaseQueryRules.RequireCanResolve(current);
        }

        return await _store.ResolveQueriesAsync(request, cancellationToken);
    }
}
