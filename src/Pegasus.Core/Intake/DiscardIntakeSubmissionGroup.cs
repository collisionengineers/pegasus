using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

/// <summary>The reviewed, submission-level terminal decision to retain but discard a manual upload.</summary>
public sealed record DiscardIntakeSubmissionGroupRequest(
    Guid GroupId,
    long ExpectedGroupVersion,
    IReadOnlyDictionary<Guid, long> ExpectedReceiptVersions,
    ActionActor Actor,
    Guid OperationId);

public sealed record DiscardIntakeSubmissionGroupResult(
    bool Succeeded,
    bool IsReplay,
    string Message)
{
    public static DiscardIntakeSubmissionGroupResult Conflict(string message) =>
        new(false, false, message);
}

public interface IDiscardIntakeSubmissionGroup
{
    Task<DiscardIntakeSubmissionGroupResult> ExecuteAsync(
        DiscardIntakeSubmissionGroupRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Validates the staff command before the durable group store atomically
/// rechecks and records the terminal decision. It deliberately owns no source
/// deletion, queue cancellation, or alternate disposition workflow.
/// </summary>
public sealed class DiscardIntakeSubmissionGroup(
    IIntakeSubmissionGroupStore groups,
    TimeProvider timeProvider) : IDiscardIntakeSubmissionGroup
{
    public Task<DiscardIntakeSubmissionGroupResult> ExecuteAsync(
        DiscardIntakeSubmissionGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.GroupId == Guid.Empty)
        {
            throw new ArgumentException("A submission group identifier is required.", nameof(request));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(request.ExpectedGroupVersion);
        if (request.ExpectedReceiptVersions is null
            || request.ExpectedReceiptVersions.Count == 0
            || request.ExpectedReceiptVersions.Any(item => item.Key == Guid.Empty || item.Value < 0))
        {
            throw new ArgumentException("Every reviewed receipt version is required.", nameof(request));
        }
        if (request.OperationId == Guid.Empty)
        {
            throw new ArgumentException("A discard operation identifier is required.", nameof(request));
        }
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);

        return groups.DiscardAsync(request, timeProvider.GetUtcNow(), cancellationToken);
    }
}
