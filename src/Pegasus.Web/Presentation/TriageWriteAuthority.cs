using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Web.Pages;

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
/// <see cref="EditScopeConflictException"/>, and the caller says who holds it
/// (<see cref="DescribeHeldAsync"/>). A claim never takes a live scope over.
/// </remarks>
public static partial class TriageWriteAuthority
{
    /// <summary>The record as the operator reading an ownership sentence names it.</summary>
    public const string RecordName = "Triage record";

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
    /// Claims the scope, runs <paramref name="action"/> with its token and
    /// releases the scope if the action is refused, before the refusal
    /// propagates. An action that answers with a result instead of an
    /// exception names in <paramref name="consumesScope"/> which results
    /// consumed the scope; any other result releases it here too.
    /// </summary>
    public static async Task<T> ExecuteAsync<T>(
        IEditScopeLeases editScopes,
        Guid triageCaseId,
        long expectedVersion,
        ActionActor actor,
        string operationKey,
        ILogger logger,
        Func<string, Task<T>> action,
        CancellationToken cancellationToken,
        Func<T, bool>? consumesScope = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(action);
        var lease = await ClaimAsync(editScopes, triageCaseId, expectedVersion, actor, operationKey, cancellationToken);
        T result;
        try
        {
            result = await action(lease.Token);
        }
        catch
        {
            await ReleaseAsync(editScopes, triageCaseId, actor, operationKey, lease.Token, logger);
            throw;
        }

        if (consumesScope is not null && !consumesScope(result))
        {
            await ReleaseAsync(editScopes, triageCaseId, actor, operationKey, lease.Token, logger);
        }

        return result;
    }

    public static Task ExecuteAsync(
        IEditScopeLeases editScopes,
        Guid triageCaseId,
        long expectedVersion,
        ActionActor actor,
        string operationKey,
        ILogger logger,
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
            logger,
            async token =>
            {
                await action(token);
                return true;
            },
            cancellationToken);
    }

    /// <summary>
    /// Who holds the Triage record while a save could not claim it: an
    /// Automation session, or a colleague's save in flight. One wording for
    /// every surface that posts a Triage write.
    /// </summary>
    public static async Task<string> DescribeHeldAsync(
        IEditScopeLeases editScopes,
        IDescribeCaseEditAuthorityHolder describeHolder,
        Guid triageCaseId,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(editScopes);
        ArgumentNullException.ThrowIfNull(describeHolder);
        var active = await editScopes.GetActiveAsync(
            EditScopeKind.Triage, triageCaseId, actor, cancellationToken);
        if (active is null)
        {
            return $"Another member of staff is editing this {RecordName}. Reload to try again.";
        }

        var isSelf = EditScopeAuthority.IsHolder(active.HolderKind, active.Holder, actor);
        var holder = isSelf
            ? CaseEditAuthorityHolder.Unnamed
            : await describeHolder.ExecuteAsync(
                active.HolderKind,
                active.Holder,
                actor,
                cancellationToken);
        return EditModeDisplay.HeldBy(RecordName, holder, isSelf);
    }

    /// <summary>
    /// Releases a scope a refused save left behind. A scope that has already
    /// gone, or that a newer claim replaced, protects nothing and is not an
    /// error. Any other failure to release is logged and swallowed: the
    /// refusal the operator needs is the original one, and the scope expires
    /// by itself.
    /// </summary>
    private static async Task ReleaseAsync(
        IEditScopeLeases editScopes,
        Guid triageCaseId,
        ActionActor actor,
        string operationKey,
        string token,
        ILogger logger)
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
        catch (Exception exception)
        {
            LogReleaseFailed(logger, triageCaseId, exception);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The Triage edit scope for {TriageCaseId} could not be released after a refused save; it expires on its own.")]
    private static partial void LogReleaseFailed(ILogger logger, Guid triageCaseId, Exception exception);
}
