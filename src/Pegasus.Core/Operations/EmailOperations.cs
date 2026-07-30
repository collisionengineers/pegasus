using System.Collections.Immutable;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Operations;

public enum EmailOperationDirection
{
    Received,
    Sent
}

public enum EmailOperationState
{
    Pending,
    Succeeded,
    Failed,
    Unknown
}

public sealed record EmailOperationProjection(
    string OperationId,
    EmailOperationDirection Direction,
    EmailOperationState State,
    string? MailboxIdentity,
    DateTimeOffset LastActivityAtUtc,
    Guid? IntakeId,
    Guid? TriageId,
    Guid? CaseId,
    string? CaseReference,
    string? PrincipalCode,
    string? FailureCode,
    string? RetryMailboxId,
    DateTimeOffset? RetryExpectedDueAtUtc)
{
    public bool CanRetry => RetryMailboxId is not null && RetryExpectedDueAtUtc is not null;
}

public sealed record EmailOperationsProjection(
    ImmutableArray<EmailOperationProjection> Received,
    ImmutableArray<EmailOperationProjection> Sent,
    bool ReceivedLimitReached,
    bool SentLimitReached);

public interface IEmailOperationsProjectionStore
{
    Task<EmailOperationsProjection> GetAsync(
        int maximumItemsPerDirection,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
}

public sealed class GetEmailOperations(
    IEmailOperationsProjectionStore store,
    TimeProvider timeProvider)
{
    public const int MaximumItemsPerDirection = 50;

    private readonly IEmailOperationsProjectionStore store =
        store ?? throw new ArgumentNullException(nameof(store));
    private readonly TimeProvider timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public async Task<EmailOperationsProjection> ExecuteAsync(
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        var projection = await store.GetAsync(
            MaximumItemsPerDirection,
            timeProvider.GetUtcNow(),
            cancellationToken);
        Validate(projection);
        return projection;
    }

    private static void Validate(EmailOperationsProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (projection.Received.IsDefault || projection.Sent.IsDefault)
        {
            throw new InvalidDataException("Email operations projections must contain initialized immutable collections.");
        }
        if (projection.Received.Length > MaximumItemsPerDirection
            || projection.Sent.Length > MaximumItemsPerDirection)
        {
            throw new InvalidDataException("The email operations projection exceeded its Core result bound.");
        }
        if (projection.Received.Any(item => item.Direction != EmailOperationDirection.Received)
            || projection.Sent.Any(item => item.Direction != EmailOperationDirection.Sent))
        {
            throw new InvalidDataException("The email operations projection contains an item in the wrong direction.");
        }
    }
}

public sealed record RetryMailboxProcessingCommand(
    string MailboxId,
    EmailOperationDirection Direction,
    string ExpectedFailureCode,
    DateTimeOffset ExpectedDueAtUtc,
    ActionActor Actor,
    string OperationKey);

public sealed record OperationsRetryResult(bool IsReplay);

public interface IMailboxProcessingRetryStore
{
    Task<OperationsRetryResult> RetryAsync(
        RetryMailboxProcessingCommand command,
        DateTimeOffset retryAtUtc,
        CancellationToken cancellationToken);
}

public sealed class RetryMailboxProcessing(
    IMailboxProcessingRetryStore store,
    TimeProvider timeProvider)
{
    private readonly IMailboxProcessingRetryStore store =
        store ?? throw new ArgumentNullException(nameof(store));
    private readonly TimeProvider timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public Task<OperationsRetryResult> ExecuteAsync(
        RetryMailboxProcessingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        StaffAuthorization.Require(command.Actor, StaffAccessRight.PerformCasework);
        if (!Enum.IsDefined(command.Direction))
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                "The mailbox processing direction is not recognized.");
        }
        RequireText(command.MailboxId, 100, nameof(command.MailboxId));
        RequireText(command.ExpectedFailureCode, 100, nameof(command.ExpectedFailureCode));
        RequireText(command.OperationKey, 100, nameof(command.OperationKey));
        if (command.ExpectedDueAtUtc == default)
        {
            throw new ArgumentException(
                "The expected mailbox failure version is required.",
                nameof(command));
        }

        return store.RetryAsync(command, timeProvider.GetUtcNow(), cancellationToken);
    }

    private static void RequireText(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Trim().Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"The value cannot exceed {maximumLength} characters.");
        }
    }
}
