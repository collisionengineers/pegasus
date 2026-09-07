using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Lifecycle;

public sealed class AcquireCaseEditLease(ILeaseCaseForEdit leases) : IAcquireCaseEditLease
{
    private readonly ILeaseCaseForEdit _leases =
        leases ?? throw new ArgumentNullException(nameof(leases));

    public Task<CaseEditLease> ExecuteAsync(
        ClaimCaseEditLeaseRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedRequest = CaseCommandSeamRules.ValidateAcquire(request);
        return _leases.ClaimAsync(normalizedRequest, cancellationToken);
    }
}

public sealed class RenewCaseEditLease(ILeaseCaseForEdit leases) : IRenewCaseEditLease
{
    private readonly ILeaseCaseForEdit _leases =
        leases ?? throw new ArgumentNullException(nameof(leases));

    public Task<CaseEditLease> ExecuteAsync(
        RenewCaseEditLeaseRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedRequest = CaseCommandSeamRules.ValidateRenew(request);
        return _leases.RenewAsync(normalizedRequest, cancellationToken);
    }
}

public sealed class HeartbeatCaseEditLease(ILeaseCaseForEdit leases) : IHeartbeatCaseEditLease
{
    private readonly ILeaseCaseForEdit _leases =
        leases ?? throw new ArgumentNullException(nameof(leases));

    public Task<CaseEditLease> ExecuteAsync(
        HeartbeatCaseEditLeaseRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedRequest = CaseCommandSeamRules.ValidateHeartbeat(request);
        return _leases.HeartbeatAsync(normalizedRequest, cancellationToken);
    }
}

public sealed class ReleaseCaseEditLease(ILeaseCaseForEdit leases) : IReleaseCaseEditLease
{
    private readonly ILeaseCaseForEdit _leases =
        leases ?? throw new ArgumentNullException(nameof(leases));

    public Task ExecuteAsync(
        ReleaseCaseEditLeaseRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedRequest = CaseCommandSeamRules.ValidateRelease(request);
        return _leases.ReleaseAsync(normalizedRequest, cancellationToken);
    }
}

public sealed class ClearCaseEditLease(IAdministrativeCaseEditLeaseStore leases) : IClearCaseEditLease
{
    private readonly IAdministrativeCaseEditLeaseStore _leases =
        leases ?? throw new ArgumentNullException(nameof(leases));

    public Task<ClearCaseEditLeaseResult> ExecuteAsync(
        ClearCaseEditLeaseRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedRequest = CaseCommandSeamRules.ValidateAdministrativeClear(request);
        return _leases.ClearAsync(normalizedRequest, cancellationToken);
    }
}

public sealed class HoldCase(IPutCaseOnHold hold) : IHoldCase
{
    private readonly IPutCaseOnHold _hold = hold ?? throw new ArgumentNullException(nameof(hold));

    public Task<CaseWorkflowRecord> ExecuteAsync(
        PutCaseOnHoldRequest request,
        CancellationToken cancellationToken) =>
        _hold.ExecuteAsync(request, cancellationToken);
}

public sealed class ReleaseCase(IReleaseCaseHold release) : IReleaseCase
{
    private readonly IReleaseCaseHold _release =
        release ?? throw new ArgumentNullException(nameof(release));

    public Task<CaseWorkflowRecord> ExecuteAsync(
        CaseMutationRequest request,
        CancellationToken cancellationToken) =>
        _release.ExecuteAsync(request, cancellationToken);
}

public sealed class TransitionCase(
    IReturnCaseToReview returnToReview,
    IStartCaseWork startCaseWork) : ITransitionCase
{
    private readonly IReturnCaseToReview _returnToReview =
        returnToReview ?? throw new ArgumentNullException(nameof(returnToReview));
    private readonly IStartCaseWork _startCaseWork =
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

    private Task<CaseWorkflowRecord> ReturnToReviewAsync(
        TransitionCaseRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Readiness is null)
        {
            throw new ArgumentException(
                "A transition to Review requires readiness evidence.",
                nameof(request));
        }

        return _returnToReview.ExecuteAsync(
            new(
                request.CaseId,
                request.ExpectedVersion,
                request.Actor,
                request.OperationKey,
                request.Reason,
                request.EditLeaseToken,
                request.Readiness),
            cancellationToken);
    }

    private Task<CaseWorkflowRecord> StartCaseWorkAsync(
        TransitionCaseRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Readiness is not null)
        {
            throw new ArgumentException(
                "Readiness evidence is accepted only for a transition to Review.",
                nameof(request));
        }

        return _startCaseWork.ExecuteAsync(
            new ChangeCaseStateRequest(
                request.CaseId,
                request.ExpectedVersion,
                request.Actor,
                request.OperationKey,
                request.Reason,
                request.EditLeaseToken),
            cancellationToken);
    }
}

public sealed class ArchiveCase(
    ICaseWorkflowQueries queries,
    ICaseArchiveReadinessQueries readiness,
    ICaseArchiveStore store) : IArchiveCase
{
    private readonly ICaseWorkflowQueries _queries =
        queries ?? throw new ArgumentNullException(nameof(queries));
    private readonly ICaseArchiveReadinessQueries _readiness =
        readiness ?? throw new ArgumentNullException(nameof(readiness));
    private readonly ICaseArchiveStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseWorkflowRecord> ExecuteAsync(
        ArchiveCaseRequest request,
        CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateMutation(request);
        var isReplay = await _readiness.HasCaseMutationOperationAsync(
            request.CaseId,
            request.OperationKey,
            cancellationToken);
        if (isReplay)
        {
            return await _store.ArchiveAsync(request, cancellationToken);
        }

        var current = await CaseLifecycleRules.GetRequiredAsync(
            _queries,
            request.CaseId,
            cancellationToken);
        if (current.Archive is not null)
        {
            throw new CaseArchivedException(request.CaseId);
        }
        if (!CaseLifecycleRules.IsTerminal(current.State))
        {
            throw new InvalidOperationException("Only a terminal case can be archived.");
        }

        var readiness = await _readiness.GetArchiveReadinessAsync(
            request.CaseId,
            cancellationToken);
        if (!readiness.IsCustodyConfirmed)
        {
            throw new InvalidOperationException(
                "A case can be archived only after its required custody is confirmed.");
        }
        if (readiness.HasBlockingExternalWork)
        {
            throw new InvalidOperationException(
                "A case cannot be archived while required durable work is incomplete or unrecognized work exists.");
        }

        return await _store.ArchiveAsync(request, cancellationToken);
    }
}

internal static class CaseCommandSeamRules
{
    public static ClaimCaseEditLeaseRequest ValidateAcquire(ClaimCaseEditLeaseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCaseAndVersion(request.CaseId, request.ExpectedVersion);
        ValidateActor(request.Actor);
        RequireText(request.OperationKey, "An operation key is required.", 100, nameof(request));
        return request with { OperationKey = request.OperationKey.Trim() };
    }

    public static RenewCaseEditLeaseRequest ValidateRenew(RenewCaseEditLeaseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCaseAndVersion(request.CaseId, request.ExpectedVersion);
        ValidateActor(request.Actor);
        RequireText(request.OperationKey, "An operation key is required.", 100, nameof(request));
        RequireText(
            request.LeaseToken,
            "An active edit lease token is required.",
            CaseEditAuthority.LeaseTokenLength,
            nameof(request));
        return request with { OperationKey = request.OperationKey.Trim() };
    }

    public static HeartbeatCaseEditLeaseRequest ValidateHeartbeat(
        HeartbeatCaseEditLeaseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(request));
        }
        ValidateActor(request.Actor);
        RequireText(
            request.LeaseToken,
            "An active edit lease token is required.",
            CaseEditAuthority.LeaseTokenLength,
            nameof(request));
        return request;
    }

    public static ReleaseCaseEditLeaseRequest ValidateRelease(ReleaseCaseEditLeaseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(request));
        }
        ValidateActor(request.Actor);
        RequireText(request.OperationKey, "An operation key is required.", 100, nameof(request));
        RequireText(
            request.LeaseToken,
            "An active edit lease token is required.",
            CaseEditAuthority.LeaseTokenLength,
            nameof(request));
        return request with { OperationKey = request.OperationKey.Trim() };
    }

    public static ClearCaseEditLeaseRequest ValidateAdministrativeClear(
        ClearCaseEditLeaseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(request));
        }
        if (request.ExpectedHolderUserId == Guid.Empty)
        {
            throw new ArgumentException("An expected lease holder is required.", nameof(request));
        }
        if (request.ExpectedLeaseGeneration <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "The expected lease generation must be positive.");
        }
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.ManageStaffAccounts);
        RequireText(request.OperationKey, "An operation key is required.", 100, nameof(request));
        RequireText(request.Reason, "A reason is required.", 500, nameof(request));
        return request with
        {
            OperationKey = request.OperationKey.Trim(),
            Reason = request.Reason.Trim()
        };
    }

    private static void ValidateCaseAndVersion(Guid caseId, long expectedVersion)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }
        if (expectedVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedVersion),
                "The expected case version cannot be negative.");
        }
    }

    private static void ValidateActor(ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
    }

    private static void RequireText(
        string value,
        string message,
        int maximumLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, parameterName);
        }
        if (value.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"The value cannot exceed {maximumLength} characters.");
        }
    }
}
