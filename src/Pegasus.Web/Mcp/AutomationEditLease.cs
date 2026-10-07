using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Mcp;

/// <summary>
/// The lease a write tool runs under. A caller that holds a lease from
/// <c>pegasus_edit_begin</c> presents its token and the command runs under it
/// unchanged. A caller that presents none gets the lease for that one command:
/// the same Core claim staff make, held while the command runs and released
/// after it, so Claude Desktop makes one call per edit while the explicit
/// lease tools remain for multi-step work (operator, 1 October 2026). The
/// claim is refused exactly as a staff claim is when another editor holds the
/// record or the version is stale, and nothing is written.
/// </summary>
internal sealed class AutomationEditLease(
    IAcquireCaseEditLease acquireCase,
    IReleaseCaseEditLease releaseCase,
    IEditScopeLeases editScopes)
{
    private const int MaximumOperationKeyLength = 100;

    public async Task<TResult> RunCaseAsync<TResult>(
        Guid caseId,
        long expectedVersion,
        string? suppliedToken,
        ActionActor actor,
        string operationKey,
        Func<string, Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(suppliedToken))
        {
            return await action(suppliedToken);
        }

        var lease = await acquireCase.ExecuteAsync(
            new(caseId, expectedVersion, actor, Derive(operationKey, ":lease")),
            cancellationToken);
        try
        {
            return await action(lease.Token);
        }
        finally
        {
            await ReleaseQuietlyAsync(() => releaseCase.ExecuteAsync(
                new(caseId, actor, Derive(operationKey, ":release"), lease.Token),
                CancellationToken.None));
        }
    }

    public async Task<TResult> RunTriageAsync<TResult>(
        Guid triageId,
        long expectedVersion,
        string? suppliedToken,
        ActionActor actor,
        string operationKey,
        Func<string, Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(suppliedToken))
        {
            return await action(suppliedToken);
        }

        var lease = await editScopes.ClaimAsync(
            new(EditScopeKind.Triage, triageId, expectedVersion, actor, Derive(operationKey, ":lease")),
            cancellationToken);
        try
        {
            return await action(lease.Token);
        }
        finally
        {
            await ReleaseQuietlyAsync(() => editScopes.ReleaseAsync(
                new(EditScopeKind.Triage, triageId, actor, Derive(operationKey, ":release"), lease.Token),
                CancellationToken.None));
        }
    }

    /// <summary>
    /// The lease's own operation keys, derived from the command's so a replay
    /// of the command replays its claim, within Core's key length.
    /// </summary>
    internal static string Derive(string operationKey, string suffix)
    {
        var room = MaximumOperationKeyLength - suffix.Length;
        return (operationKey.Length > room ? operationKey[..room] : operationKey) + suffix;
    }

    /// <summary>
    /// A release after the command is housekeeping: a Case mutation consumes
    /// the lease it ran under, and a lease that lapsed meanwhile is already
    /// free, so a refused release changes nothing about the command's result.
    /// </summary>
    private static async Task ReleaseQuietlyAsync(Func<Task> release)
    {
        try
        {
            await release();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
        }
    }
}
