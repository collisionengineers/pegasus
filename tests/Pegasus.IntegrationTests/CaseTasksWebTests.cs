using System.Net;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Tasks page: the manual chase is covered beside the workspace tests; these cover the case
/// task lifecycle and the report-Sent evidence links.
/// </summary>
public sealed partial class CaseDetailsWebTests
{
    [Fact]
    public async Task TasksPageBindsTaskLifecycleAndReportEvidenceLinks()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<ICreateCaseTask>(services, store);
            Substitute<IAssignCaseTask>(services, store);
            Substitute<ICompleteCaseTask>(services, store);
            Substitute<ICancelCaseTask>(services, store);
            Substitute<ILinkReportEvidence>(services, store);
            Substitute<IUnlinkReportEvidence>(services, store);
        });
        var taskId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        var evidenceId = Guid.NewGuid();
        (string Name, string Value)[] existingTask =
        [
            ("taskId", taskId.ToString("D")),
            ("expectedTaskVersion", "3")
        ];

        using var created = await workspace.PostAsync(
            "Tasks?handler=CreateTask",
            workspace.MutationForm(
                "create-task",
                "Chase the provider",
                ("taskId", taskId.ToString("D")),
                ("description", "Request the missing images")));
        using var assigned = await workspace.PostAsync(
            "Tasks?handler=AssignTask",
            workspace.MutationForm("assign-task", "Hand over", [("assigneeId", assigneeId.ToString("D")), .. existingTask]));
        using var completed = await workspace.PostAsync(
            "Tasks?handler=CompleteTask",
            workspace.MutationForm("complete-task", "Images received", existingTask));
        using var cancelled = await workspace.PostAsync(
            "Tasks?handler=CancelTask",
            workspace.MutationForm("cancel-task", "No longer needed", existingTask));
        using var linked = await workspace.PostAsync(
            "Tasks?handler=LinkReportEvidence",
            workspace.MutationForm("link-evidence", "Report sent", ("evidenceId", evidenceId.ToString("D"))));
        using var unlinked = await workspace.PostAsync(
            "Tasks?handler=UnlinkReportEvidence",
            workspace.MutationForm("unlink-evidence", "Wrong message", ("evidenceId", evidenceId.ToString("D"))));

        foreach (var response in new[] { created, assigned, completed, cancelled, linked, unlinked })
        {
            AssertPrg(response, store.CaseId);
        }

        var creation = Assert.Single(store.TaskCreations);
        AssertClaimant(workspace, creation.Actor);
        Assert.Equal(taskId, creation.TaskId);
        Assert.Equal(store.CaseVersion, creation.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, creation.EditLeaseToken);
        Assert.Equal("create-task", creation.OperationKey);
        Assert.Equal("Chase the provider", creation.Reason);
        Assert.Equal("Request the missing images", creation.Description);
        Assert.Null(creation.AssigneeId);

        var assignment = Assert.Single(store.TaskAssignments);
        AssertClaimant(workspace, assignment.Actor);
        Assert.Equal(taskId, assignment.TaskId);
        Assert.Equal(3, assignment.ExpectedTaskVersion);
        Assert.Equal(store.CaseVersion, assignment.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, assignment.EditLeaseToken);
        Assert.Equal("assign-task", assignment.OperationKey);
        Assert.Equal(assigneeId, assignment.AssigneeId);

        var completion = Assert.Single(store.TaskCompletions);
        AssertClaimant(workspace, completion.Actor);
        Assert.Equal(taskId, completion.TaskId);
        Assert.Equal(3, completion.ExpectedTaskVersion);
        Assert.Equal("complete-task", completion.OperationKey);
        Assert.Equal("Images received", completion.Reason);

        var cancellation = Assert.Single(store.TaskCancellations);
        AssertClaimant(workspace, cancellation.Actor);
        Assert.Equal(taskId, cancellation.TaskId);
        Assert.Equal(3, cancellation.ExpectedTaskVersion);
        Assert.Equal("cancel-task", cancellation.OperationKey);
        Assert.Equal("No longer needed", cancellation.Reason);

        var link = Assert.Single(store.EvidenceLinks);
        AssertLeasedMutation(workspace, link, "link-evidence", "Report sent");
        Assert.Equal(evidenceId, link.EvidenceId);

        var unlink = Assert.Single(store.EvidenceUnlinks);
        AssertLeasedMutation(workspace, unlink, "unlink-evidence", "Wrong message");
        Assert.Equal(evidenceId, unlink.EvidenceId);

        await AssertRefusalKeepsEditModeAsync(
            workspace,
            "Tasks?handler=CompleteTask",
            workspace.MutationForm("complete-task-2", "Already closed", existingTask));
    }

    /// <summary>
    /// EPIC-011 §1.8 Inspection address: the recorded value and, in edit
    /// context, an editor for it. CASE-038: the record renders every section
    /// at once, so the Inspection section no longer carries a whole-record
    /// form of its own — its control is associated with the one record form,
    /// which is the only entry for `inspectionAddress`, and that one form
    /// still carries every editable value SaveCase writes.
    /// </summary>
    [Fact]
    public async Task InspectionAddressEditorContributesTheOnlyAddressEntryToTheRecordForm()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<Pegasus.Core.Address.IInspectionAddressChoicesQueries>(services, store));

        var page = await GetHtmlAsync(
            workspace.Client,
            $"/Cases/{store.CaseId:D}?section=inspection");

        Assert.Contains("1 Depot Road", page, StringComparison.Ordinal);
        Assert.DoesNotContain("case-inspection-address-form", page, StringComparison.Ordinal);
        Assert.Contains(
            "id=\"inspection-address\" name=\"inspectionAddress\" form=\"case-edit-form\"",
            page,
            StringComparison.Ordinal);
        Assert.Equal(
            1,
            page.Split("name=\"inspectionAddress\"", StringSplitOptions.None).Length - 1);

        var formStart = page.IndexOf("id=\"case-edit-form\"", StringComparison.Ordinal);
        Assert.True(formStart >= 0, "The record edit form is not rendered.");
        var html = page[formStart..];
        Assert.Contains($"/Cases/{store.CaseId:D}?handler=Save", page, StringComparison.Ordinal);
        foreach (var field in new[]
        {
            "claimantName",
            "claimantContactNumber",
            "claimantAddress",
            "claimNumber",
            "vehicleRegistration",
            "vehicleMake",
            "vehicleModel",
            "vehicleMileage",
            "vehicleMileageUnit",
            "accidentCircumstances",
            "incidentDate",
            "contactName",
            "contactEmailAddress",
            "contactPhoneNumber",
            "instructionDate",
            "vatStatus",
            "inspectionDate",
            "inspectionDeadline",
            "inspectionMode",
            "storageLocation"
        })
        {
            Assert.Contains($"name=\"{field}\"", html, StringComparison.Ordinal);
        }

        Assert.Contains("name=\"storageLocation\" form=\"case-edit-form\"", page, StringComparison.Ordinal);
        var imageBased = page.IndexOf("value=\"ImageBasedAssessment\"", StringComparison.Ordinal);
        var claimant = page.IndexOf("value=\"ClaimantAddress\"", StringComparison.Ordinal);
        var repairer = page.IndexOf("value=\"RepairerLocation\"", StringComparison.Ordinal);
        var storage = page.IndexOf("value=\"StorageLocation\"", StringComparison.Ordinal);
        var previous = page.IndexOf("value=\"PreviousAddress\"", StringComparison.Ordinal);
        var manual = page.IndexOf("value=\"ManualEntry\"", StringComparison.Ordinal);
        Assert.True(imageBased >= 0 && imageBased < claimant && claimant < repairer && repairer < storage
            && storage < previous && previous < manual);
        var repairerOption = WebUtility.HtmlDecode(
            page[repairer..page.IndexOf("</option>", repairer, StringComparison.Ordinal)]);
        Assert.Contains("disabled", repairerOption, StringComparison.Ordinal);
        Assert.Contains("Repairer location · not recorded", repairerOption, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Overview editor writes the same editable values, so it must carry the
    /// claimant's own contact number and address too: SaveCase writes a null for
    /// anything the form omits, which cleared them on every save (CASE-027).
    /// </summary>
    [Fact]
    public async Task OverviewEditorAlsoPostsTheClaimantContactNumberAndAddress()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var html = await workspace.GetWorkspaceAsync();

        Assert.Contains("name=\"claimantContactNumber\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"claimantAddress\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"storageLocation\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// EPIC-011 §1.8 Notes: entries carry the date, the clock time and the
    /// actor, and both writing actions post the handlers the case already has.
    /// </summary>
    [Fact]
    public async Task NotesSectionOffersAddNoteAndRecordChaseAgainstTheExistingHandlers()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=notes");

        Assert.Contains($"/Cases/{store.CaseId:D}/Tasks?handler=AddNote", html, StringComparison.Ordinal);
        Assert.Contains(
            $"/Cases/{store.CaseId:D}/Tasks?handler=RecordManualChase",
            html,
            StringComparison.Ordinal);
    }

    private sealed partial class RecordingCaseDetailsStore :
        ICreateCaseTask,
        IAssignCaseTask,
        ICompleteCaseTask,
        ICancelCaseTask,
        ILinkReportEvidence,
        IUnlinkReportEvidence
    {
        public List<CreateCaseTaskRequest> TaskCreations { get; } = [];
        public List<AssignCaseTaskRequest> TaskAssignments { get; } = [];
        public List<CompleteCaseTaskRequest> TaskCompletions { get; } = [];
        public List<CancelCaseTaskRequest> TaskCancellations { get; } = [];
        public List<LinkReportEvidenceRequest> EvidenceLinks { get; } = [];
        public List<UnlinkReportEvidenceRequest> EvidenceUnlinks { get; } = [];

        Task<CaseTaskRecord> ICreateCaseTask.ExecuteAsync(
            CreateCaseTaskRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            TaskCreations.Add(request);
            return Task.FromResult(TaskRecord(request.TaskId, request.Description, request.AssigneeId, CaseTaskState.Open, 1));
        }

        Task<CaseTaskRecord> IAssignCaseTask.ExecuteAsync(
            AssignCaseTaskRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            TaskAssignments.Add(request);
            return Task.FromResult(TaskRecord(request.TaskId, "task", request.AssigneeId, CaseTaskState.Open, request.ExpectedTaskVersion + 1));
        }

        Task<CaseTaskRecord> ICompleteCaseTask.ExecuteAsync(
            CompleteCaseTaskRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            TaskCompletions.Add(request);
            return Task.FromResult(TaskRecord(request.TaskId, "task", null, CaseTaskState.Completed, request.ExpectedTaskVersion + 1));
        }

        Task<CaseTaskRecord> ICancelCaseTask.ExecuteAsync(
            CancelCaseTaskRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            TaskCancellations.Add(request);
            return Task.FromResult(TaskRecord(request.TaskId, "task", null, CaseTaskState.Cancelled, request.ExpectedTaskVersion + 1));
        }

        Task<CaseWorkflowRecord> ILinkReportEvidence.ExecuteAsync(
            LinkReportEvidenceRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            EvidenceLinks.Add(request);
            return Task.FromResult(CreateWorkflow());
        }

        Task<CaseWorkflowRecord> IUnlinkReportEvidence.ExecuteAsync(
            UnlinkReportEvidenceRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            EvidenceUnlinks.Add(request);
            return Task.FromResult(CreateWorkflow());
        }

        private CaseTaskRecord TaskRecord(Guid taskId, string description, Guid? assigneeId, CaseTaskState state, long version) =>
            new(taskId, CaseId, description, assigneeId, state, version, CaseVersion + 1);
    }
}
