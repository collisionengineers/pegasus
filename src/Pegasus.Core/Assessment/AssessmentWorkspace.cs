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
    /// <summary>
    /// D11 (FRD-11): the workspace opens once the case is With Engineer
    /// (Report preparation or later) and a current-cycle export exists.
    /// </summary>
    public bool CanOpen => AssessmentAccessPolicy.CanOpen(this);

    /// <summary>
    /// D11: the workspace is read-only once the case is Post-report
    /// complete; Report preparation and Post report stay editable.
    /// </summary>
    public bool IsReadOnly => AssessmentAccessPolicy.IsReadOnly(this);
}

public static class AssessmentAccessPolicy
{
    public static bool CanOpen(AssessmentAccessState access)
    {
        ArgumentNullException.ThrowIfNull(access);
        return access.State
                is CaseLifecycleState.ReportPreparation
                    or CaseLifecycleState.PostReport
                    or CaseLifecycleState.PostReportComplete
            && access.LatestExportVersion is { } exportedVersion
            && exportedVersion >= access.LatestReviewVersion;
    }

    /// <summary>
    /// H3 (CASE-047; D02 overriding how D47 was encoded): the report
    /// generation, preview and delivery journey never depends on an EVA
    /// export cycle. The workspace opening rule above keeps D11 unchanged;
    /// this is its state set without the export clause.
    /// </summary>
    public static bool CanOpenReports(AssessmentAccessState access)
    {
        ArgumentNullException.ThrowIfNull(access);
        return access.State
            is CaseLifecycleState.ReportPreparation
                or CaseLifecycleState.PostReport
                or CaseLifecycleState.PostReportComplete;
    }

    public static bool IsReadOnly(AssessmentAccessState access)
    {
        ArgumentNullException.ThrowIfNull(access);
        return access.State == CaseLifecycleState.PostReportComplete;
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
