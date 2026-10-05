using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Lifecycle;

public sealed class AutoLinkReportEvidenceTests
{
    private static readonly ActionActor WorkerActor =
        ActionActor.SystemWorker("sent-evidence-poll");

    [Fact]
    public async Task SystemWorkerReturnsCanonicalCommittedLink()
    {
        var caseId = Guid.NewGuid();
        var evidenceId = Guid.NewGuid();
        var store = new RecordingStore(new(
            AutoLinkReportEvidenceDisposition.Linked,
            new(caseId, evidenceId, CaseLifecycleState.PostReport, Version: 3),
            NotLinkedReasonCode: null));
        var useCase = new AutoLinkReportEvidence(store);
        var request = new AutoLinkReportEvidenceRequest(
            caseId,
            evidenceId,
            WorkerActor,
            "report-auto-link-operation",
            "Exact approved-mailbox Sent evidence and one authoritative Case identity");

        var result = await useCase.ExecuteAsync(request, default);

        Assert.Equal(AutoLinkReportEvidenceDisposition.Linked, result.Disposition);
        Assert.Equal(caseId, result.Link?.CaseId);
        Assert.Equal(CaseLifecycleState.PostReport, result.Link?.State);
        Assert.Equal(evidenceId, result.Link?.EvidenceId);
        Assert.Equal(3, result.Link?.Version);
        Assert.Equal(request, Assert.Single(store.Requests));
    }

    [Fact]
    public async Task StoreCannotSubstituteADifferentCommittedAssociation()
    {
        var requestedCaseId = Guid.NewGuid();
        var requestedEvidenceId = Guid.NewGuid();
        var store = new RecordingStore(new(
            AutoLinkReportEvidenceDisposition.Linked,
            new(
                requestedCaseId,
                Guid.NewGuid(),
                CaseLifecycleState.PostReport,
                Version: 3),
            NotLinkedReasonCode: null));
        var useCase = new AutoLinkReportEvidence(store);

        await Assert.ThrowsAsync<InvalidDataException>(() => useCase.ExecuteAsync(
            new(
                requestedCaseId,
                requestedEvidenceId,
                WorkerActor,
                "report-auto-link-substitution",
                "Only the exact retained evidence may be associated"),
            default));
    }

    [Fact]
    public async Task PolicyDenialRemainsAnExplicitNonLink()
    {
        var store = new RecordingStore(new(
            AutoLinkReportEvidenceDisposition.NotLinked,
            Link: null,
            "case_not_report_preparation"));
        var useCase = new AutoLinkReportEvidence(store);

        var result = await useCase.ExecuteAsync(
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                WorkerActor,
                "report-auto-link-denied",
                "Exact approved-mailbox Sent evidence and one authoritative Case identity"),
            default);

        Assert.Equal(AutoLinkReportEvidenceDisposition.NotLinked, result.Disposition);
        Assert.Null(result.Link);
        Assert.Equal("case_not_report_preparation", result.NotLinkedReasonCode);
    }

    [Fact]
    public async Task StaffActorCannotInvokeAutomaticLinkBoundary()
    {
        var store = new RecordingStore(new(
            AutoLinkReportEvidenceDisposition.NotLinked,
            Link: null,
            "must-not-run"));
        var useCase = new AutoLinkReportEvidence(store);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => useCase.ExecuteAsync(
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                "report-auto-link-staff",
                "Staff must use the lease-bound report-evidence action"),
            default));

        Assert.Empty(store.Requests);
    }


    [Fact]
    public async Task LinkedResultCarriesTheTasksTheLinkCreated()
    {
        var caseId = Guid.NewGuid();
        var evidenceId = Guid.NewGuid();
        var task = new CaseTaskRecord(
            Guid.NewGuid(), caseId, "Send the figures to the garage", null, CaseTaskState.Open, 0, 4);
        var store = new RecordingStore(new(
            AutoLinkReportEvidenceDisposition.Linked,
            new(caseId, evidenceId, CaseLifecycleState.PostReport, Version: 4),
            NotLinkedReasonCode: null)
        {
            TasksCreated = [task]
        });

        var result = await new AutoLinkReportEvidence(store).ExecuteAsync(
            new(caseId, evidenceId, WorkerActor, "report-auto-link-tasks", "Exact approved-mailbox Sent evidence"),
            default);

        Assert.Equal(task, Assert.Single(result.TasksCreated));
    }

    [Fact]
    public async Task ALinkThatCreatedNoTasksReportsNone()
    {
        var caseId = Guid.NewGuid();
        var evidenceId = Guid.NewGuid();
        var store = new RecordingStore(new(
            AutoLinkReportEvidenceDisposition.Linked,
            new(caseId, evidenceId, CaseLifecycleState.PostReport, Version: 3),
            NotLinkedReasonCode: null));

        var result = await new AutoLinkReportEvidence(store).ExecuteAsync(
            new(caseId, evidenceId, WorkerActor, "report-auto-link-no-tasks", "Exact approved-mailbox Sent evidence"),
            default);

        Assert.Empty(result.TasksCreated);
    }

    [Fact]
    public async Task ANonLinkCannotCarryTasks()
    {
        var caseId = Guid.NewGuid();
        var task = new CaseTaskRecord(Guid.NewGuid(), caseId, "Remind the garage", null, CaseTaskState.Open, 0, 4);
        var store = new RecordingStore(new(
            AutoLinkReportEvidenceDisposition.NotLinked,
            Link: null,
            "case_not_report_preparation")
        {
            TasksCreated = [task]
        });

        await Assert.ThrowsAsync<InvalidDataException>(() => new AutoLinkReportEvidence(store).ExecuteAsync(
            new(caseId, Guid.NewGuid(), WorkerActor, "report-auto-link-nonlink-tasks", "Exact approved-mailbox Sent evidence"),
            default));
    }

    [Fact]
    public async Task TasksOfAnotherCaseOrNotOpenAndUnassignedAreRefused()
    {
        var caseId = Guid.NewGuid();
        var evidenceId = Guid.NewGuid();
        CaseTaskRecord[] invalid =
        [
            new(Guid.NewGuid(), Guid.NewGuid(), "Another Case's task", null, CaseTaskState.Open, 0, 4),
            new(Guid.NewGuid(), caseId, "Already done", null, CaseTaskState.Completed, 1, 4),
            new(Guid.NewGuid(), caseId, "Assigned", Guid.NewGuid(), CaseTaskState.Open, 0, 4)
        ];
        foreach (var task in invalid)
        {
            var store = new RecordingStore(new(
                AutoLinkReportEvidenceDisposition.Linked,
                new(caseId, evidenceId, CaseLifecycleState.PostReport, Version: 4),
                NotLinkedReasonCode: null)
            {
                TasksCreated = [task]
            });

            await Assert.ThrowsAsync<InvalidDataException>(() => new AutoLinkReportEvidence(store).ExecuteAsync(
                new(caseId, evidenceId, WorkerActor, "report-auto-link-invalid-tasks", "Exact approved-mailbox Sent evidence"),
                default));
        }
    }

    private sealed class RecordingStore(AutoLinkReportEvidenceResult result)
        : IAutoLinkReportEvidenceStore
    {
        public List<AutoLinkReportEvidenceRequest> Requests { get; } = [];

        public Task<AutoLinkReportEvidenceResult> TryAutoLinkAsync(
            AutoLinkReportEvidenceRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(result);
        }
    }
}
