using Pegasus.Core.Workflow;

namespace Pegasus.Core.Lifecycle;

public sealed class HoldCase(IPutCaseOnHold hold) : IHoldCase
{
    private readonly IPutCaseOnHold _hold = hold ?? throw new ArgumentNullException(nameof(hold));

    public Task<CaseWorkflowRecord> ExecuteAsync(
        PutCaseOnHoldRequest request,
        CancellationToken cancellationToken) =>
        _hold.ExecuteAsync(request, cancellationToken);
}

public sealed class ReleaseCase(ReleaseCaseHold release) : IReleaseCase
{
    private readonly ReleaseCaseHold _release =
        release ?? throw new ArgumentNullException(nameof(release));

    public Task<CaseWorkflowRecord> ExecuteAsync(
        CaseMutationRequest request,
        CancellationToken cancellationToken) =>
        _release.ExecuteAsync(request, cancellationToken);
}

public sealed class TransitionCase(
    ReturnCaseToReview returnToReview,
    StartCaseWork startCaseWork) : ITransitionCase
{
    private readonly ReturnCaseToReview _returnToReview =
        returnToReview ?? throw new ArgumentNullException(nameof(returnToReview));
    private readonly StartCaseWork _startCaseWork =
        startCaseWork ?? throw new ArgumentNullException(nameof(startCaseWork));

    public Task<CaseWorkflowRecord> ExecuteAsync(
        TransitionCaseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.Destination))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "The case transition destination is invalid.");
        }

        return request.Destination switch
        {
            CaseTransitionDestination.Review => ReturnToReviewAsync(request, cancellationToken),
            CaseTransitionDestination.ReportPreparation => StartCaseWorkAsync(request, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };
    }

    /// <summary>
    /// The store re-reads the case's persisted completeness facts inside its
    /// own transaction and decides there.
    /// </summary>
    private Task<CaseWorkflowRecord> ReturnToReviewAsync(
        TransitionCaseRequest request,
        CancellationToken cancellationToken) =>
        _returnToReview.ExecuteAsync(
            new(
                request.CaseId,
                request.ExpectedVersion,
                request.Actor,
                request.OperationKey,
                request.Reason,
                request.EditLeaseToken),
            cancellationToken);

    private Task<CaseWorkflowRecord> StartCaseWorkAsync(
        TransitionCaseRequest request,
        CancellationToken cancellationToken) =>
        _startCaseWork.ExecuteAsync(
            new ChangeCaseStateRequest(
                request.CaseId,
                request.ExpectedVersion,
                request.Actor,
                request.OperationKey,
                request.Reason,
                request.EditLeaseToken),
            cancellationToken);
}

public sealed class ArchiveCase(ICaseArchiveStore store) : IArchiveCase
{
    private readonly ICaseArchiveStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseWorkflowRecord> ExecuteAsync(
        ArchiveCaseRequest request,
        CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateMutation(request);
        return await _store.ArchiveAsync(request, cancellationToken);
    }
}
