using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The authority one staff Triage write claims and consumes: the Triage edit
/// scope, held for that one save. A Triage Case has no Edit step; each action
/// claims the scope here, runs the Core command with its token, and the store
/// ends the scope when the save commits. A refused save releases it here, so
/// the record is free for the next action at once.
/// </summary>
/// <remarks>
/// A scope another session holds — an Automation session, or a colleague's
/// save in flight — refuses the claim with
/// <see cref="EditScopeConflictException"/>, and the caller says who holds it.
/// A claim never takes a live scope over.
/// </remarks>
public static class TriageWriteAuthority
{
    /// <summary>Claims the Triage edit scope for one save.</summary>
    public static Task<EditScopeLease> ClaimAsync(
        IEditScopeLeases editScopes,
        Guid triageCaseId,
        long expectedVersion,
        ActionActor actor,
        string operationKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(editScopes);
        return editScopes.ClaimAsync(
            new ClaimEditScopeRequest(
                EditScopeKind.Triage,
                triageCaseId,
                expectedVersion,
                actor,
                $"triage-write-claim:{operationKey}"),
            cancellationToken);
    }

    /// <summary>
    /// Claims the scope, runs <paramref name="action"/> with its token and, if
    /// the action is refused, releases the scope before the refusal propagates.
    /// </summary>
    public static async Task<T> ExecuteAsync<T>(
        IEditScopeLeases editScopes,
        Guid triageCaseId,
        long expectedVersion,
        ActionActor actor,
        string operationKey,
        Func<string, Task<T>> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        var lease = await ClaimAsync(editScopes, triageCaseId, expectedVersion, actor, operationKey, cancellationToken);
        try
        {
            return await action(lease.Token);
        }
        catch
        {
            await ReleaseAsync(editScopes, triageCaseId, actor, operationKey, lease.Token);
            throw;
        }
    }

    public static Task ExecuteAsync(
        IEditScopeLeases editScopes,
        Guid triageCaseId,
        long expectedVersion,
        ActionActor actor,
        string operationKey,
        Func<string, Task> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        return ExecuteAsync(
            editScopes,
            triageCaseId,
            expectedVersion,
            actor,
            operationKey,
            async token =>
            {
                await action(token);
                return true;
            },
            cancellationToken);
    }

    /// <summary>
    /// Releases a scope a refused save left behind. A scope that has already
    /// gone, or that a newer claim replaced, protects nothing and is not an error.
    /// </summary>
    private static async Task ReleaseAsync(
        IEditScopeLeases editScopes,
        Guid triageCaseId,
        ActionActor actor,
        string operationKey,
        string token)
    {
        ArgumentNullException.ThrowIfNull(editScopes);
        try
        {
            await editScopes.ReleaseAsync(
                new ReleaseEditScopeRequest(
                    EditScopeKind.Triage,
                    triageCaseId,
                    actor,
                    $"triage-write-release:{operationKey}",
                    token),
                CancellationToken.None);
        }
        catch (Exception exception)
            when (exception is EditScopeExpiredException or EditScopeConflictException)
        {
        }
    }
}
