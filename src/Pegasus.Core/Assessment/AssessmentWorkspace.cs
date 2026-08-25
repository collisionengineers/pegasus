using Pegasus.Core.AiWork;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Assessment;

/// <summary>
/// The compact case identity and lifecycle state rendered by the Assessment
/// screen. It deliberately excludes the documents, history, tasks, upload
/// links and custody preparation carried by the general Case-details query.
/// </summary>
public sealed record AssessmentWorkspaceHeader(
    Guid CaseId,
    string Reference,
    string Principal,
    string? Registration,
    CaseType CaseType,
    CaseLifecycleState State,
    long Version,
    DateOnly? DueBy,
    string? CaseRootRemoteId);

public sealed record AssessmentAccessState(
    CaseLifecycleState State,
    long LatestReviewVersion,
    long? LatestExportVersion)
{
    public bool CanOpen => AssessmentAccessPolicy.CanOpen(this);
}

public static class AssessmentAccessPolicy
{
    public static bool CanOpen(AssessmentAccessState access)
    {
        ArgumentNullException.ThrowIfNull(access);
        return access.State is CaseLifecycleState.Review or CaseLifecycleState.ReportPreparation
            && access.LatestExportVersion is { } exportedVersion
            && exportedVersion >= access.LatestReviewVersion;
    }
}

public sealed record GetAssessmentAccessQuery(Guid CaseId, ActionActor Actor);

public interface IAssessmentAccessSource
{
    Task<AssessmentAccessState?> GetAsync(
        Guid caseId,
        CancellationToken cancellationToken = default);
}

public interface IGetAssessmentAccess
{
    Task<AssessmentAccessState?> ExecuteAsync(
        GetAssessmentAccessQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class GetAssessmentAccess(IAssessmentAccessSource source) : IGetAssessmentAccess
{
    public Task<AssessmentAccessState?> ExecuteAsync(
        GetAssessmentAccessQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(query));
        }

        return source.GetAsync(query.CaseId, cancellationToken);
    }
}

/// <summary>
/// Everything the Assessment GET renders, loaded as one bounded relational
/// projection. Document metadata and content are generation-time concerns and
/// are intentionally absent.
/// </summary>
public sealed record AssessmentWorkspace(
    AssessmentWorkspaceHeader Header,
    CaseDataProjection Data,
    VehicleLookupObservation? LatestVehicleObservation,
    CaseAssessmentProjection Assessment,
    RepairSpecificationVersion? DraftSpecification,
    RepairSpecificationVersion? AcceptedSpecification,
    AiWorkRequestRecord? LatestRequest);

public sealed record GetAssessmentWorkspaceQuery(Guid CaseId, ActionActor Actor);

public interface IAssessmentWorkspaceSource
{
    Task<AssessmentWorkspace?> GetAsync(
        Guid caseId,
        CancellationToken cancellationToken = default);
}

public interface IGetAssessmentWorkspace
{
    Task<AssessmentWorkspace?> ExecuteAsync(
        GetAssessmentWorkspaceQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class GetAssessmentWorkspace(IAssessmentWorkspaceSource source)
    : IGetAssessmentWorkspace
{
    public Task<AssessmentWorkspace?> ExecuteAsync(
        GetAssessmentWorkspaceQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(query));
        }

        return source.GetAsync(query.CaseId, cancellationToken);
    }
}
