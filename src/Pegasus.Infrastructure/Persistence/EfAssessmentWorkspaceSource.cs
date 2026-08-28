using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The Assessment screen's bounded relational projection. Six commands load
/// only what that screen and report generation share; general Case documents,
/// history, tasks, upload links and custody preparation stay on the Case screen.
/// </summary>
internal sealed class EfAssessmentWorkspaceSource(
    IDbContextFactory<PegasusDbContext> contextFactory) : IAssessmentWorkspaceSource
{
    public async Task<AssessmentWorkspace?> GetAsync(
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var workflows = context.CaseWorkflows.AsNoTracking()
            .Include(item => item.Case)
            .ThenInclude(item => item.Principal)
            .Include(item => item.DueWork);
        var workspaceState = await workflows
            .Where(item => item.CaseId == caseId)
            .Select(item => new
            {
                Workflow = item,
                LatestReviewVersion = context.CaseWorkflowEvents
                    .Where(history => history.CaseId == item.CaseId
                        && (history.EventType == "state_Review"
                            || history.EventType == "case_returned_to_review"
                            || history.EventType == "case_reopened_Review"))
                    .Select(history => (long?)history.AfterVersion)
                    .Max() ?? 0,
                LatestExportVersion = context.EvaFirstHandoffProxies
                    .Where(export => export.CaseId == item.CaseId)
                    .Select(export => export.LatestExportedWorkflowVersion)
                    .SingleOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (workspaceState is null)
        {
            return null;
        }
        var workflow = workspaceState.Workflow;
        var access = new AssessmentAccessState(
            Enum.Parse<CaseLifecycleState>(workflow.State),
            workspaceState.LatestReviewVersion,
            workspaceState.LatestExportVersion);
        if (!access.CanOpen)
        {
            return null;
        }

        var snapshot = await EfCaseDataStore.SnapshotQuery(context, tracking: false)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken)
            ?? throw new InvalidDataException(
                "The accepted case is missing its typed data projection.");
        var assessmentFields = await context.CaseAssessmentFields.AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .OrderBy(item => item.FieldPath)
            .ToArrayAsync(cancellationToken);
        var specificationEntities = await context.CaseRepairSpecifications.AsNoTracking()
            .Where(item => item.CaseId == caseId
                && (item.State == RepairSpecificationState.Draft.ToString()
                    || item.State == RepairSpecificationState.Accepted.ToString()))
            .Include(item => item.Lines)
            .ToArrayAsync(cancellationToken);
        var latestObservationEntity = await context.Set<VehicleLookupObservationEntity>()
            .AsNoTracking()
            .Include(item => item.Request)
            .Where(item => item.Request.CaseId == caseId)
            .OrderByDescending(item => item.RecordedAtUtc)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var latestRequestEntity = await context.AiWorkRequests.AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.RequestId)
            .FirstOrDefaultAsync(cancellationToken);

        // Named estimates (ENG-026): a case may hold several drafts and
        // several accepted estimates; the workspace shows the latest draft
        // and the Current one, the same choice EfRepairSpecificationStore's
        // DraftQuery / AcceptedQuery make.
        var draftEntity = specificationEntities
            .Where(item => item.State == RepairSpecificationState.Draft.ToString())
            .OrderByDescending(item => item.Version)
            .FirstOrDefault();
        var acceptedEntity = specificationEntities.SingleOrDefault(item => item.IsCurrent);
        var currentSpecification = acceptedEntity ?? draftEntity;
        var data = EfCaseDataStore.Map(snapshot, workflow);
        var assessment = EfCaseAssessmentStore.Map(
            workflow,
            assessmentFields,
            currentSpecification?.Lines ?? [],
            snapshot.Fields);
        return new(
            new(
                caseId,
                workflow.Case.Reference,
                workflow.Case.Principal.Code,
                data.Vehicle.Registration.Current?.Value,
                EfCaseQueryStore.ParseCaseType(workflow.Case.Type),
                Enum.Parse<CaseLifecycleState>(workflow.State),
                workflow.Version,
                workflow.DueWork?.DueBy,
                workflow.Case.CustodyRootRemoteId),
            data,
            latestObservationEntity is null
                ? null
                : EfVehicleLookupWorkStore.MapObservation(latestObservationEntity),
            assessment,
            draftEntity is null ? null : EfRepairSpecificationStore.Map(draftEntity),
            acceptedEntity is null ? null : EfRepairSpecificationStore.Map(acceptedEntity),
            latestRequestEntity is null ? null : EfAiWorkRequestStore.Map(latestRequestEntity));
    }
}
