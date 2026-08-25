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

        var workflow = await context.CaseWorkflows.AsNoTracking()
            .Include(item => item.Case)
            .ThenInclude(item => item.Principal)
            .Include(item => item.DueWork)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
        if (workflow is null)
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

        var draftEntity = specificationEntities.SingleOrDefault(
            item => item.State == RepairSpecificationState.Draft.ToString());
        var acceptedEntity = specificationEntities.SingleOrDefault(
            item => item.State == RepairSpecificationState.Accepted.ToString());
        var currentSpecification = acceptedEntity ?? draftEntity;
        var data = EfCaseDataStore.Map(snapshot, workflow);
        var assessment = EfCaseAssessmentStore.Map(
            workflow,
            assessmentFields,
            currentSpecification?.Lines ?? [],
            snapshot.Fields);
        assessment = assessment with
        {
            Readiness = AssessmentPolicy.EvaluateReadiness(assessment)
        };

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
