using Pegasus.Core.Actors;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

public enum RetainedMailFolderMoveOutcome
{
    Succeeded,
    Failed,
    Uncertain
}

public sealed record MoveRetainedMailFolderRequest(
    Guid MessageId,
    int ExpectedClassificationVersion,
    string ExpectedRecommendationPolicyKey,
    int ExpectedRecommendationPolicyVersion,
    int ExpectedMailboxVersion,
    string OperationKey,
    string Reason);

public sealed record RetainedMailFolderMoveResult(
    RetainedMailFolderMoveOutcome Outcome,
    MailLogicalFolderType FolderType,
    string Reason,
    DateTimeOffset RecordedAtUtc,
    bool IsReplay = false);

public sealed record RetainedMailFolderMoveCoordinates(
    string MailboxId,
    string SourceFolderId,
    string ImmutableMessageId,
    string DestinationFolderId);

public interface IRetainedMailFolderMover
{
    bool IsAvailable { get; }

    Task MoveAsync(
        RetainedMailFolderMoveCoordinates coordinates,
        CancellationToken cancellationToken);

    Task<string?> GetParentFolderIdAsync(
        string mailboxId,
        string immutableMessageId,
        CancellationToken cancellationToken);
}

public interface IRetainedMailFolderMoveStore
{
    Task<RetainedMailFolderMoveResult?> MoveAsync(
        ActionActor actor,
        MoveRetainedMailFolderRequest request,
        CancellationToken cancellationToken);

    Task<RetainedMailFolderMoveResult?> GetLatestAsync(
        Guid messageId,
        CancellationToken cancellationToken);
}

internal sealed class EmptyRetainedMailFolderMoveStore : IRetainedMailFolderMoveStore
{
    public static EmptyRetainedMailFolderMoveStore Instance { get; } = new();

    public Task<RetainedMailFolderMoveResult?> MoveAsync(ActionActor actor, MoveRetainedMailFolderRequest request, CancellationToken cancellationToken) =>
        throw new RetainedMailFolderMoveException("Outlook folder moves are unavailable in this runtime.");

    public Task<RetainedMailFolderMoveResult?> GetLatestAsync(Guid messageId, CancellationToken cancellationToken) =>
        Task.FromResult<RetainedMailFolderMoveResult?>(null);
}

public sealed class RetainedMailFolderMoveException(string message) : InvalidOperationException(message);

public sealed class MoveRetainedMailFolder(IRetainedMailFolderMoveStore store)
{
    public Task<RetainedMailFolderMoveResult?> ExecuteAsync(
        ActionActor actor,
        MoveRetainedMailFolderRequest request,
        CancellationToken cancellationToken = default)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (actor.Kind != ActorKind.Staff)
        {
            throw new StaffAuthorizationException(StaffAccessRight.PerformCasework);
        }
        ArgumentNullException.ThrowIfNull(request);
        if (request.MessageId == Guid.Empty)
        {
            throw new ArgumentException("A retained message identifier is required.", nameof(request));
        }
        if (request.ExpectedClassificationVersion < 1
            || request.ExpectedRecommendationPolicyVersion < 1
            || request.ExpectedMailboxVersion < 1)
        {
            throw new ArgumentException("Current classification, recommendation and mailbox versions are required.", nameof(request));
        }
        if (string.IsNullOrWhiteSpace(request.ExpectedRecommendationPolicyKey))
        {
            throw new ArgumentException("The recommendation policy is required.", nameof(request));
        }
        if (!Guid.TryParse(request.OperationKey, out var operationKey) || operationKey == Guid.Empty)
        {
            throw new ArgumentException("A valid operation key is required.", nameof(request));
        }
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
        {
            throw new ArgumentException("A move reason of 1 to 500 characters is required.", nameof(request));
        }

        return store.MoveAsync(actor, request with
        {
            ExpectedRecommendationPolicyKey = request.ExpectedRecommendationPolicyKey.Trim(),
            OperationKey = operationKey.ToString("D"),
            Reason = reason
        }, cancellationToken);
    }
}

public sealed class UnavailableRetainedMailFolderMover : IRetainedMailFolderMover
{
    public bool IsAvailable => false;

    public Task MoveAsync(RetainedMailFolderMoveCoordinates coordinates, CancellationToken cancellationToken) =>
        throw new RetainedMailFolderMoveException("Outlook folder moves are unavailable in this runtime.");

    public Task<string?> GetParentFolderIdAsync(string mailboxId, string immutableMessageId, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);
}
