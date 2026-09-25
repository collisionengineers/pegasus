using System.Collections.Immutable;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Operations;

public enum RequestOperationState
{
    Pending,
    Failed,
    Completed,
    UnknownExternal
}

public sealed record RequestOperationProjection(
    Guid Id,
    RequestOperationState State,
    Guid CaseId,
    string CaseReference,
    string PrincipalCode,
    DateTimeOffset LastActivityAtUtc,
    string? ExternalKind,
    int? AttemptCount,
    string? FailureCode,
    string? FailureReason,
    bool CanRetry);

public sealed record RequestOperationsProjection(
    ImmutableArray<RequestOperationProjection> Items,
    bool LimitReached);

public interface IRequestOperationsProjectionStore
{
    Task<RequestOperationsProjection> GetAsync(
        int maximumItems,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Counts every retryable external-work failure. This is deliberately
    /// separate from <see cref="GetAsync"/>'s bounded operator list: the rail
    /// badge is a total, not a count of the first page.
    /// </summary>
    Task<int> CountRetryableExternalFailuresAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
}

public sealed class GetRequestOperations(
    IRequestOperationsProjectionStore store,
    TimeProvider timeProvider)
{
    public const int MaximumItems = 100;

    private readonly IRequestOperationsProjectionStore store =
        store ?? throw new ArgumentNullException(nameof(store));
    private readonly TimeProvider timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public async Task<RequestOperationsProjection> ExecuteAsync(
        ActionActor actor,
        DateTimeOffset? asOfUtc = null,
        CancellationToken cancellationToken = default)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        var nowUtc = asOfUtc ?? timeProvider.GetUtcNow();
        var projection = await store.GetAsync(
            MaximumItems,
            nowUtc,
            cancellationToken);
        ArgumentNullException.ThrowIfNull(projection);
        if (projection.Items.IsDefault)
        {
            throw new InvalidDataException("Request operations projections must contain an initialized immutable collection.");
        }
        if (projection.Items.Length > MaximumItems)
        {
            throw new InvalidDataException("The request operations projection exceeded its Core result bound.");
        }
        foreach (var item in projection.Items)
        {
            if (item.Id == Guid.Empty ||
                item.CaseId == Guid.Empty ||
                string.IsNullOrWhiteSpace(item.CaseReference) ||
                string.IsNullOrWhiteSpace(item.PrincipalCode) ||
                !Enum.IsDefined(item.State) ||
                item.AttemptCount is < 0)
            {
                throw new InvalidDataException("The request operations projection contains invalid identity or state.");
            }
            if (item.CanRetry &&
                (item.State != RequestOperationState.Failed ||
                 item.AttemptCount is null))
            {
                throw new InvalidDataException("Only a retryable external-work failure may expose retry.");
            }
        }

        return projection;
    }
}

public sealed record RetryExternalWorkCommand(
    Guid WorkItemId,
    int ExpectedAttemptCount,
    ActionActor Actor,
    string OperationKey);

public sealed record OperationsRetryResult(bool IsReplay);

public interface IExternalWorkRetryStore
{
    Task<OperationsRetryResult> RetryAsync(
        RetryExternalWorkCommand command,
        DateTimeOffset retryAtUtc,
        CancellationToken cancellationToken);
}

public sealed class RetryExternalWork(
    IExternalWorkRetryStore store,
    TimeProvider timeProvider)
{
    private readonly IExternalWorkRetryStore store =
        store ?? throw new ArgumentNullException(nameof(store));
    private readonly TimeProvider timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public Task<OperationsRetryResult> ExecuteAsync(
        RetryExternalWorkCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        StaffAuthorization.Require(command.Actor, StaffAccessRight.PerformCasework);
        if (command.WorkItemId == Guid.Empty)
        {
            throw new ArgumentException("An external work identifier is required.", nameof(command));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(command.ExpectedAttemptCount);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.OperationKey);
        if (command.OperationKey.Trim().Length > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                "The operation key cannot exceed 100 characters.");
        }

        return store.RetryAsync(command, timeProvider.GetUtcNow(), cancellationToken);
    }
}
