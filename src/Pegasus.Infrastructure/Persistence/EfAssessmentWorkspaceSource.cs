using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The Assessment screen's bounded relational projection. Five commands load
/// only what that screen and report generation share; general Case documents,
/// history, tasks and custody preparation stay on the Case screen.
/// </summary>
internal sealed class EfAssessmentWorkspaceSource(
    IDbContextFactory<PegasusDbContext> contextFactory) : IAssessmentWorkspaceSource
{
    public async Task<AssessmentWorkspace?> GetAsync(
        Guid caseId,
        CaseWorkSelector work,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var workflows = context.CaseWorkflows.AsNoTracking()
            .Include(item => item.Case)
            .ThenInclude(item => item.Principal)
            .Include(item => item.DueWork);
        var workflow = await workflows
            .Where(item => item.CaseId == caseId)
            .SingleOrDefaultAsync(cancellationToken);
        if (workflow is null)
        {
            return null;
        }

        // The selected work is resolved inside the snapshot read, and the rest
        // of the workspace reads the work that snapshot belongs to.
        var selectedWorkIds = CaseWorkScope.SelectedIds(context, caseId, work);
        var snapshot = await EfCaseDataStore.SnapshotQuery(context, tracking: false)
            .SingleOrDefaultAsync(item => selectedWorkIds.Contains(item.WorkId), cancellationToken)
            ?? throw new InvalidDataException(
                "The accepted case is missing its typed data projection.");
        var workId = snapshot.WorkId;
        var assessmentFields = await context.CaseAssessmentFields.AsNoTracking()
            .Where(item => item.WorkId == workId)
            .OrderBy(item => item.FieldPath)
            .ToArrayAsync(cancellationToken);
        var currentEntity = await EfRepairSpecificationStore.CurrentQuery(context, workId)
            .AsNoTracking()
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(cancellationToken);
        var latestObservationEntity = await context.Set<VehicleLookupObservationEntity>()
            .AsNoTracking()
            .Include(item => item.Request)
            .Where(item => item.Request.CaseId == caseId)
            .OrderByDescending(item => item.RecordedAtUtc)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var data = EfCaseDataStore.Map(snapshot, workflow);
        var assessment = EfCaseAssessmentStore.Map(
            workflow,
            assessmentFields,
            currentEntity?.Lines ?? [],
            snapshot.Fields,
            snapshot.OriginReceivedAtUtc);
        return new(
            new(
                caseId,
                workflow.Case.Reference,
                workflow.Case.Principal.Code,
                data.Vehicle.Registration.Current?.Value,
                CaseTypeCodes.Parse(workflow.Case.Type),
                Enum.Parse<CaseLifecycleState>(workflow.State),
                workflow.Version,
                workflow.DueWork?.DueBy,
                workflow.Case.CustodyRootRemoteId),
            data,
            latestObservationEntity is null
                ? null
                : EfVehicleLookupWorkStore.MapObservation(latestObservationEntity),
            assessment,
            currentEntity is null ? null : EfRepairSpecificationStore.Map(currentEntity));
    }
}
