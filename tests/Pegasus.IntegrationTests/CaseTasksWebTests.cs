using System.Net;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Tasks page: the manual chase is covered beside the workspace tests; these cover the case
/// task lifecycle and the report-Sent evidence links.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseTasksWebTests
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
            "id=\"inspection-address\" class=\"fi\" name=\"inspectionAddress\" form=\"case-edit-form\"",
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
        Assert.Equal(
            1,
            html.Split("name=\"vehicleMileageUnit\"", StringSplitOptions.None).Length - 1);
        Assert.Contains(
            "<select id=\"edit-mileage-unit\" class=\"fi\" name=\"vehicleMileageUnit\" form=\"case-edit-form\">",
            html,
            StringComparison.Ordinal);

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



    [Fact]
    public async Task ManualChasePostUsesAntiforgeryServerActorLiveLeaseVersionAndReplayKey()
    {
        // The attempt time is the server's clock at the post, never a value
        // the form carried (PR 670 port), so the host's clock is pinned here.
        var attemptedAtUtc = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
        using var baseFactory = new IntakeWebApplicationFactory(
            new CaseWorkflowPersistenceTests.MutableTimeProvider(attemptedAtUtc));
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.RemoveAll<IRecordManualCaseChase>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<IRecordManualCaseChase>(store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var initialHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        var claimOperationKey = InputValue(initialHtml, "operationKey");
        using var claimResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(initialHtml),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", claimOperationKey)));
        AssertPrg(claimResponse, store.CaseId);
        Assert.Single(store.Claims);
        Assert.Equal(claimOperationKey, store.Claims[0].OperationKey);

        using (var recoveryClient = factory.CreateClient(new WebApplicationFactoryClientOptions
               {
                   AllowAutoRedirect = false,
                   BaseAddress = new Uri("https://localhost")
               }))
        {
            var recoveryHtml = await GetHtmlAsync(recoveryClient, $"/Cases/{store.CaseId:D}");
            // The holder returning to their own case in a second window gets
            // the one control, reading Take over (v26 § R): the claim replays
            // their retained lease, so nothing is "recovered".
            Assert.Contains("Take over", RecordBar(recoveryHtml), StringComparison.Ordinal);
            Assert.Contains("data-case-edit", RecordBar(recoveryHtml), StringComparison.Ordinal);
            Assert.Equal(claimOperationKey, InputValue(recoveryHtml, "operationKey"));
            using var recoveryResponse = await recoveryClient.PostAsync(
                $"/Cases/{store.CaseId:D}?handler=ClaimLease",
                Form(
                    AntiforgeryValue(recoveryHtml),
                    ("id", store.CaseId.ToString("D")),
                    ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                    ("operationKey", claimOperationKey)));
            AssertPrg(recoveryResponse, store.CaseId);
        }
        Assert.Equal(2, store.Claims.Count);
        Assert.Equal(store.Claims[0].OperationKey, store.Claims[1].OperationKey);

        var leasedHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=notes");
        var refreshedHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=notes");
        Assert.Equal(
            InputValue(leasedHtml, "editLeaseToken"),
            InputValue(refreshedHtml, "editLeaseToken"));
        leasedHtml = refreshedHtml;
        // The Record chase control lives on the Notes section and renders in
        // edit context while a chase is scheduled.
        Assert.Contains("Record chase", leasedHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"recipient\"", leasedHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"content\"", leasedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"attemptedAtUtc\"", leasedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"targetPartyOrAddress\"", leasedHtml, StringComparison.Ordinal);
        var chaseForm = ManualChaseMarkup(leasedHtml);
        Assert.DoesNotContain("name=\"reason\"", chaseForm, StringComparison.Ordinal);
        var operationKey = "manual-chase-replay";
        using var firstResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}/Tasks?handler=RecordManualChase",
            ManualChaseForm(AntiforgeryValue(leasedHtml), store, operationKey));
        AssertPrg(firstResponse, store.CaseId);

        var currentHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.DoesNotContain("name=\"editLeaseToken\"", currentHtml, StringComparison.Ordinal);
        using var replayResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}/Tasks?handler=RecordManualChase",
            ManualChaseForm(AntiforgeryValue(currentHtml), store, operationKey));
        AssertPrg(replayResponse, store.CaseId);

        Assert.Equal(2, store.ManualChases.Count);
        var command = store.ManualChases[0];
        var replay = store.ManualChases[1];
        Assert.Equal(command with { Actor = replay.Actor }, replay);
        Assert.Equal(command.Actor.Kind, replay.Actor.Kind);
        Assert.Equal(command.Actor.SubjectId, replay.Actor.SubjectId);
        Assert.Equal(command.Actor.Roles, replay.Actor.Roles);
        Assert.Equal(store.CaseId, command.CaseId);
        Assert.Equal(store.CaseVersion, command.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, command.EditLeaseToken);
        Assert.Equal(operationKey, command.OperationKey);
        Assert.Equal(ActorKind.Staff, command.Actor.Kind);
        Assert.Equal(attemptedAtUtc, command.AttemptedAtUtc);
        Assert.NotEmpty(command.Actor.Roles);
        Assert.Equal("Telephone", command.Channel);
        Assert.Equal("Provider claims team", command.TargetPartyOrAddress);
        Assert.Equal("Awaiting requested photographs", command.Outcome);
        Assert.Equal("Asked provider for missing images", command.Note);
    }


    private static FormUrlEncodedContent ManualChaseForm(
        string antiforgeryToken,
        RecordingCaseDetailsStore store,
        string operationKey) => Form(
            antiforgeryToken,
            ("id", store.CaseId.ToString("D")),
            ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
            ("operationKey", operationKey),
            ("editLeaseToken", store.LeaseToken),
            ("channel", "Telephone"),
            ("recipient", "Provider claims team"),
            ("outcome", "Awaiting requested photographs"),
            ("content", "Asked provider for missing images"));


    private static string ManualChaseMarkup(string html)
    {
        var start = html.IndexOf("handler=RecordManualChase", StringComparison.Ordinal);
        Assert.True(start >= 0, "The manual chase form is not rendered.");
        var end = html.IndexOf("</form>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The manual chase form is not closed.");
        return html[start..end];
    }

    /// <summary>
    /// What each reasoned lifecycle mutation posts from the leased workspace — the case id, its
    /// version, operation key, lease token, reason, and action-specific fields.
    /// </summary>
}
