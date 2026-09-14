using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

/// <summary>
/// Dismiss (Inbox planning, 13 September): a message row or record gains a Dismiss
/// action that takes the message out of the incoming scopes without classifying
/// or linking it. The message moves to the <see cref="MailLogicalFolderType.Dismissed"/>
/// logical folder, is reachable under the Dismissed scope, and can be restored from
/// there. Nothing is deleted, and the act is logged. Always allowed, including on a
/// message with an open Unidentified item, which stays open.
/// </summary>
public sealed record DismissRetainedMailRequest(
    Guid MessageId,
    ActionActor Actor,
    string OperationKey);

public sealed record RestoreRetainedMailRequest(
    Guid MessageId,
    ActionActor Actor,
    string OperationKey);

/// <summary>The message's dismissal state after the act, or as replayed.</summary>
public sealed record RetainedMailDismissal(
    Guid MessageId,
    bool IsDismissed,
    DateTimeOffset? DismissedAtUtc,
    string? DismissedBy,
    bool IsReplay);

public interface IRetainedMailDismissalStore
{
    /// <summary>Null when the message is not retained.</summary>
    Task<RetainedMailDismissal?> DismissAsync(
        DismissRetainedMailRequest request,
        CancellationToken cancellationToken);

    Task<RetainedMailDismissal?> RestoreAsync(
        RestoreRetainedMailRequest request,
        CancellationToken cancellationToken);
}

public interface IDismissRetainedMail
{
    Task<RetainedMailDismissal?> ExecuteAsync(
        DismissRetainedMailRequest request,
        CancellationToken cancellationToken);
}

public interface IRestoreRetainedMail
{
    Task<RetainedMailDismissal?> ExecuteAsync(
        RestoreRetainedMailRequest request,
        CancellationToken cancellationToken);
}

public static class RetainedMailDismissalPolicy
{
    public const int MaximumOperationKeyLength = 100;

    public static string RequireStaff(ActionActor actor, Guid messageId, string operationKey)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (actor.Kind != ActorKind.Staff)
        {
            throw new StaffAuthorizationException(StaffAccessRight.PerformCasework);
        }

        if (messageId == Guid.Empty)
        {
            throw new ArgumentException("A retained message identifier is required.", nameof(messageId));
        }

        var key = operationKey?.Trim();
        if (string.IsNullOrEmpty(key) || key.Length > MaximumOperationKeyLength)
        {
            throw new ArgumentException("An operation key is required.", nameof(operationKey));
        }

        return key;
    }
}

public sealed class DismissRetainedMail(IRetainedMailDismissalStore store) : IDismissRetainedMail
{
    private readonly IRetainedMailDismissalStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<RetainedMailDismissal?> ExecuteAsync(
        DismissRetainedMailRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var key = RetainedMailDismissalPolicy.RequireStaff(request.Actor, request.MessageId, request.OperationKey);
        return _store.DismissAsync(request with { OperationKey = key }, cancellationToken);
    }
}

public sealed class RestoreRetainedMail(IRetainedMailDismissalStore store) : IRestoreRetainedMail
{
    private readonly IRetainedMailDismissalStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<RetainedMailDismissal?> ExecuteAsync(
        RestoreRetainedMailRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var key = RetainedMailDismissalPolicy.RequireStaff(request.Actor, request.MessageId, request.OperationKey);
        return _store.RestoreAsync(request with { OperationKey = key }, cancellationToken);
    }
}
