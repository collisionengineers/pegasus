using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

internal static class AssessmentWorkspaceTestData
{
    public static AssessmentWorkspace Create(CaseAssessmentProjection assessment)
    {
        var identity = new CaseIdentity(
            assessment.CaseId, "QDOS", 2026, 42, assessment.Reference);
        var emptyString = new CaseField<string>(null, null, null);
        var emptyDate = new CaseField<DateOnly>(null, null, null);

        var data = new CaseDataProjection(
            identity,
            new CaseOriginIdentity(
                Guid.NewGuid(), IntakeSourceChannel.Mailbox, "test", new string('a', 64),
                DateTimeOffset.UtcNow, "test", "1", null, null),
            DateTimeOffset.UtcNow,
            assessment.CaseVersion,
            assessment.State,
            new CaseCompletenessProjection(
                new CaseCompleteness(false, false, false, false),
                new CaseCompletenessEvaluation(false, "test", 1)),
            new CaseProviderData(emptyString),
            new CaseClaimantData(emptyString),
            new CaseClaimData(emptyString),
            new CaseVehicleData(
                emptyString, emptyString, emptyString,
                new CaseField<long>(null, null, null), emptyString),
            new CaseAccidentData(emptyDate, emptyString),
            new CaseContactData(emptyString, emptyString, emptyString),
            new CaseInstructionData(emptyDate, emptyString),
            new CaseInspectionData(
                emptyDate, emptyDate, emptyString,
                new CaseField<CaseInspectionMode>(null, null, null)));

        return new AssessmentWorkspace(
            new AssessmentWorkspaceHeader(
                assessment.CaseId,
                assessment.Reference,
                "Approved Principal",
                assessment.CaseOwned.Registration,
                CaseType.Inspection,
                assessment.State,
                assessment.CaseVersion,
                null,
                "case-root-id"),
            data,
            null,
            assessment,
            null,
            null,
            null);
    }

    public static AssessmentWorkspace Create(
        CaseDetails details,
        CaseAssessmentProjection assessment,
        RepairSpecificationVersion? draftSpecification = null,
        RepairSpecificationVersion? acceptedSpecification = null)
    {
        var fallback = Create(assessment);
        return fallback with
        {
            Header = new AssessmentWorkspaceHeader(
                details.Summary.CaseId,
                details.Summary.Reference,
                details.Summary.Principal,
                details.Summary.Registration,
                details.Summary.CaseType,
                details.Summary.State,
                details.Workflow.Version,
                details.Workflow.DueWork?.DueBy,
                details.CustodyFolderRemoteId),
            Data = details.Data ?? fallback.Data,
            LatestVehicleObservation = details.VehicleEvidence?.LatestObservation,
            DraftSpecification = draftSpecification,
            AcceptedSpecification = acceptedSpecification
        };
    }
}

internal sealed class FakeGetAssessmentWorkspace(AssessmentWorkspace workspace)
    : IGetAssessmentWorkspace
{
    public Task<AssessmentWorkspace?> ExecuteAsync(
        GetAssessmentWorkspaceQuery query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<AssessmentWorkspace?>(workspace);
}

internal sealed class FakeGetAssessmentAccess(bool canOpen = true) : IGetAssessmentAccess
{
    public Task<AssessmentAccessState?> ExecuteAsync(
        GetAssessmentAccessQuery query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<AssessmentAccessState?>(canOpen
            // D11: the open states are With Engineer onwards — Review no
            // longer opens the workspace, so the open fake must sit inside
            // the new state set or the policy itself refuses it.
            ? new(CaseLifecycleState.ReportPreparation, 0, 0)
            : new(CaseLifecycleState.NotReady, 0, null));
}
