using Pegasus.Core.Identity;

namespace Pegasus.Core.Workflow;

/// <summary>
/// The Case edit lease use case (FRD-14, ADR-0075): it validates each request and hands it to the
/// <see cref="ILeaseCaseForEdit"/> port, which owns the lease rules. There is no renewal; a holder
/// keeps the lease with a heartbeat (R524).
/// </summary>
public sealed class CaseEditLeases(ILeaseCaseForEdit leases)
{
    private readonly ILeaseCaseForEdit _leases =
        leases ?? throw new ArgumentNullException(nameof(leases));

    public Task<CaseEditLease> ClaimAsync(
        ClaimCaseEditLeaseRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedRequest = ValidateClaim(request);
        return _leases.ClaimAsync(normalizedRequest, cancellationToken);
    }

    /// <summary>
    /// Keeps the lease its holder already has from lapsing under an open editor. It carries no
    /// operation key and leaves no replay record: FRD-01 counts a heartbeat as telemetry, and an
    /// editor produces one every minute for as long as the page stays open.
    /// </summary>
    public Task<CaseEditLease> HeartbeatAsync(
        HeartbeatCaseEditLeaseRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedRequest = ValidateHeartbeat(request);
        return _leases.HeartbeatAsync(normalizedRequest, cancellationToken);
    }

    /// <summary>
    /// Resumes the live lease the calling staff member already holds, so returning to a case they
    /// are editing puts them straight back into edit mode. Only the Case page resumes; a one-off
    /// change made elsewhere claims, and is refused while the lease is live, so it never ends the
    /// holder's edit session. A caller who holds no live lease gets null.
    /// </summary>
    public Task<CaseEditLease?> ResumeAsync(
        ResumeCaseEditLeaseRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedRequest = ValidateResume(request);
        return _leases.ResumeAsync(normalizedRequest, cancellationToken);
    }

    public Task ReleaseAsync(
        ReleaseCaseEditLeaseRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedRequest = ValidateRelease(request);
        return _leases.ReleaseAsync(normalizedRequest, cancellationToken);
    }

    private static ClaimCaseEditLeaseRequest ValidateClaim(ClaimCaseEditLeaseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCaseAndVersion(request.CaseId, request.ExpectedVersion);
        ValidateActor(request.Actor);
        RequireText(request.OperationKey, "An operation key is required.", 100, nameof(request));
        return request with { OperationKey = request.OperationKey.Trim() };
    }

    private static HeartbeatCaseEditLeaseRequest ValidateHeartbeat(
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

    private static ResumeCaseEditLeaseRequest ValidateResume(ResumeCaseEditLeaseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(request));
        }
        ValidateActor(request.Actor);
        return request;
    }

    private static ReleaseCaseEditLeaseRequest ValidateRelease(ReleaseCaseEditLeaseRequest request)
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
