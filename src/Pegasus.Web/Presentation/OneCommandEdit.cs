using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The one Web helper for a command that holds a Case's edit authority for itself: it claims
/// the authority Core names for the record (<see cref="OneCommandEditAuthority"/>), runs the
/// command under its token and always gives it back afterwards, whatever the command answered.
/// A failed release never changes the command's outcome: it is logged as a Warning with its
/// type and message and no exception object, and an unreleased authority lapses by itself
/// (FRD-14; R523, 29 October 2026). A staff write ends the authority it was made under, so the
/// release after it finding nothing to give back is not a failure and logs nothing.
/// </summary>
public sealed partial class OneCommandEdit(
    OneCommandEditAuthority authority,
    ILogger<OneCommandEdit> logger)
{
    /// <summary>Claims the authority alone, for a caller that words a refused claim itself.</summary>
    public Task<OneCommandEditClaim> ClaimAsync(
        Guid caseId,
        bool isTriageCase,
        long expectedVersion,
        ActionActor actor,
        string operationKey,
        CancellationToken cancellationToken) =>
        authority.ClaimAsync(caseId, isTriageCase, expectedVersion, actor, operationKey, cancellationToken);

    /// <summary>Claims the authority, runs <paramref name="action"/> under it and releases it.</summary>
    public async Task<T> RunAsync<T>(
        Guid caseId,
        bool isTriageCase,
        long expectedVersion,
        ActionActor actor,
        string operationKey,
        Func<OneCommandEditClaim, Task<T>> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        var claim = await ClaimAsync(caseId, isTriageCase, expectedVersion, actor, operationKey, cancellationToken);
        return await RunClaimedAsync(claim, actor, () => action(claim));
    }

    public Task RunAsync(
        Guid caseId,
        bool isTriageCase,
        long expectedVersion,
        ActionActor actor,
        string operationKey,
        Func<OneCommandEditClaim, Task> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        return RunAsync(
            caseId,
            isTriageCase,
            expectedVersion,
            actor,
            operationKey,
            async claim =>
            {
                await action(claim);
                return true;
            },
            cancellationToken);
    }

    /// <summary>
    /// One staff Triage save: claims the Triage edit scope, refuses a record that moved past the
    /// posted version as changed before the save runs
    /// (<see cref="OneCommandEditAuthority.RequireTriageSaveAtPostedVersion"/>), runs the save
    /// with the scope's token and releases the scope.
    /// </summary>
    public async Task<T> RunTriageSaveAsync<T>(
        Guid triageCaseId,
        long postedVersion,
        ActionActor actor,
        string operationKey,
        Func<string, Task<T>> save,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(save);
        var claim = await ClaimAsync(triageCaseId, true, postedVersion, actor, operationKey, cancellationToken);
        return await RunClaimedAsync(claim, actor, () =>
        {
            OneCommandEditAuthority.RequireTriageSaveAtPostedVersion(claim, postedVersion);
            return save(claim.Token);
        });
    }

    public Task RunTriageSaveAsync(
        Guid triageCaseId,
        long postedVersion,
        ActionActor actor,
        string operationKey,
        Func<string, Task> save,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(save);
        return RunTriageSaveAsync(
            triageCaseId,
            postedVersion,
            actor,
            operationKey,
            async token =>
            {
                await save(token);
                return true;
            },
            cancellationToken);
    }

    /// <summary>Runs <paramref name="action"/> under an authority already claimed, then releases it.</summary>
    public async Task<T> RunClaimedAsync<T>(
        OneCommandEditClaim claim,
        ActionActor actor,
        Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(action);
        var completed = false;
        try
        {
            var result = await action();
            completed = true;
            return result;
        }
        finally
        {
            await ReleaseAsync(claim, actor, completed);
        }
    }

    /// <summary>
    /// An Automation write tool's Case lease. A caller that holds a lease from
    /// <c>pegasus_edit_begin</c> presents its token and the command runs under it unchanged; the
    /// lease stands until the caller ends it (operator, 8 October 2026). A caller that presents
    /// none gets the lease for that one command, claimed with a key derived from the command's so
    /// a replay replays the claim (operator, 1 October 2026).
    /// </summary>
    public Task<T> RunCaseAsync<T>(
        Guid caseId,
        long expectedVersion,
        string? heldToken,
        ActionActor actor,
        string operationKey,
        Func<string, Task<T>> action,
        CancellationToken cancellationToken) =>
        RunUnlessHeldAsync(caseId, false, expectedVersion, heldToken, actor, operationKey, action, cancellationToken);

    /// <summary>The same, for an Automation write to a Triage Case under its Triage edit scope.</summary>
    public Task<T> RunTriageAsync<T>(
        Guid triageCaseId,
        long expectedVersion,
        string? heldToken,
        ActionActor actor,
        string operationKey,
        Func<string, Task<T>> action,
        CancellationToken cancellationToken) =>
        RunUnlessHeldAsync(triageCaseId, true, expectedVersion, heldToken, actor, operationKey, action, cancellationToken);

    private Task<T> RunUnlessHeldAsync<T>(
        Guid caseId,
        bool isTriageCase,
        long expectedVersion,
        string? heldToken,
        ActionActor actor,
        string operationKey,
        Func<string, Task<T>> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        return !string.IsNullOrWhiteSpace(heldToken)
            ? action(heldToken)
            : RunAsync(
                caseId,
                isTriageCase,
                expectedVersion,
                actor,
                OneCommandEditAuthority.DeriveOperationKey(operationKey, ":lease"),
                claim => action(claim.Token),
                cancellationToken);
    }

    /// <summary>
    /// Releases the claimed authority. After a command that completed, a release the port
    /// refuses because the write already ended the authority is the expected outcome and logs
    /// nothing (<see cref="OneCommandEditAuthority.WriteEndedIt"/>); every other refusal or
    /// fault is a release that was due and did not happen, and is logged.
    /// </summary>
    private async Task ReleaseAsync(OneCommandEditClaim claim, ActionActor actor, bool commandCompleted)
    {
        try
        {
            await authority.ReleaseAsync(
                claim,
                actor,
                OneCommandEditAuthority.DeriveOperationKey(claim.OperationKey, ":release"),
                CancellationToken.None);
        }
        catch (Exception exception) when (commandCompleted && OneCommandEditAuthority.WriteEndedIt(actor, exception))
        {
            // The write ended the authority itself; there was nothing left to release.
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            LogReleaseFailed(logger, claim.CaseId, exception.GetType().Name, exception.Message);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The edit authority claimed for one command on case {CaseId} was not released: {RefusalType}: {RefusalMessage}")]
    private static partial void LogReleaseFailed(
        ILogger logger,
        Guid caseId,
        string refusalType,
        string refusalMessage);
}
