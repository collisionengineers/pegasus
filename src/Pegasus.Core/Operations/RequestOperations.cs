using System.Collections.Immutable;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Operations;

public enum RequestOperationKind
{
    BoxFileRequest,
    PegasusUploadLink,
    ExternalWork
}

public enum RequestOperationState
{
    Pending,
    Active,
    Expired,
    Exhausted,
    Revoked,
    Failed,
    Completed,
    UnknownExternal
}

public enum RequestCaseEditLeaseState
{
    Available,
    Active,
    Unknown
}

public sealed record RequestOperationProjection(
    Guid Id,
    RequestOperationKind Kind,
    RequestOperationState State,
    Guid CaseId,
    string CaseReference,
    string PrincipalCode,
    DateTimeOffset LastActivityAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    long? Version,
    int? AcceptedFileCount,
    long? AcceptedByteCount,
    int? MaximumFileCount,
    long? MaximumByteCount,
    string? LimitsVersion,
    string? ExternalKind,
    int? AttemptCount,
    string? FailureCode,
    string? FailureReason,
    bool CanRetry,
    bool CanRevoke,
    long CaseVersion,
    RequestCaseEditLeaseState CaseEditLeaseState,
    DateTimeOffset? CaseEditLeaseExpiresAtUtc)
{
    public CaseEditLeaseSnapshot? ActiveEditLease { get; init; }
}

public sealed record RequestOperationsProjection(
    ImmutableArray<RequestOperationProjection> Items,
    bool LimitReached);

public interface IRequestOperationsProjectionStore
{
    Task<RequestOperationsProjection> GetAsync(
        int maximumItems,
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
        CancellationToken cancellationToken = default)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        var nowUtc = timeProvider.GetUtcNow();
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
                item.CaseVersion < 0 ||
                !Enum.IsDefined(item.Kind) ||
                !Enum.IsDefined(item.State) ||
                !Enum.IsDefined(item.CaseEditLeaseState))
            {
                throw new InvalidDataException("The request operations projection contains invalid identity or state.");
            }
            if (item.CaseEditLeaseState == RequestCaseEditLeaseState.Active &&
                (item.CaseEditLeaseExpiresAtUtc is not { } activeExpiry ||
                 activeExpiry <= nowUtc ||
                 item.ActiveEditLease is not { } activeLease ||
                 activeLease.ExpiresAtUtc != activeExpiry ||
                 string.IsNullOrWhiteSpace(activeLease.Holder) ||
                 string.IsNullOrWhiteSpace(activeLease.OperationKey)))
            {
                throw new InvalidDataException(
                    "An active request edit lease must have a complete future lease projection.");
            }
            if (item.Kind == RequestOperationKind.PegasusUploadLink &&
                item.State == RequestOperationState.Active &&
                (item.ExpiresAtUtc is not { } uploadExpiry || uploadExpiry <= nowUtc))
            {
                throw new InvalidDataException(
                    "An active upload link must have a future expiry in the request projection.");
            }
            if (item.CaseEditLeaseState != RequestCaseEditLeaseState.Active &&
                item.ActiveEditLease is not null)
            {
                throw new InvalidDataException(
                    "Only an active request edit lease may expose lease recovery metadata.");
            }
            if (item.CanRevoke &&
                (item.Kind == RequestOperationKind.ExternalWork || item.Version is null))
            {
                throw new InvalidDataException("Only versioned file requests may expose revocation.");
            }
            if (item.CanRetry &&
                (item.Kind != RequestOperationKind.ExternalWork ||
                 item.State != RequestOperationState.Failed ||
                 item.AttemptCount is null))
            {
                throw new InvalidDataException("Only a versioned durable external-work failure may expose retry.");
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
