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
