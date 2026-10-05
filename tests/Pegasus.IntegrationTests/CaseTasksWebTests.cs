using System.Net;
using System.Globalization;
using System.Text.RegularExpressions;
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
using TaskLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.Tasks;

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
    public async Task TasksPageBindsReportEvidenceLinksAndRefusesRetiredTaskHandlers()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<ILinkReportEvidence>(services, store);
            Substitute<IUnlinkReportEvidence>(services, store);
        });
        var evidenceId = Guid.NewGuid();
        using var linked = await workspace.PostAsync(
            "Tasks?handler=LinkReportEvidence",
            workspace.MutationForm("link-evidence", "Report sent", ("evidenceId", evidenceId.ToString("D"))));
        using var unlinked = await workspace.PostAsync(
            "Tasks?handler=UnlinkReportEvidence",
            workspace.MutationForm("unlink-evidence", "Wrong message", ("evidenceId", evidenceId.ToString("D"))));

        AssertPrg(linked, store.CaseId);
        AssertPrg(unlinked, store.CaseId);
        var link = Assert.Single(store.EvidenceLinks);
        var unlink = Assert.Single(store.EvidenceUnlinks);
        AssertLeasedMutation(workspace, link, "link-evidence", "Report sent");
        AssertLeasedMutation(workspace, unlink, "unlink-evidence", "Wrong message");
        Assert.Equal(evidenceId, link.EvidenceId);
        Assert.Equal(evidenceId, unlink.EvidenceId);

        foreach (var handler in new[] { "CreateTask", "AssignTask", "CompleteTask", "CancelTask" })
        {
            using var retired = await workspace.PostAsync(
                $"Tasks?handler={handler}",
                workspace.MutationForm("retired-task", "No task route"));
            Assert.Equal(HttpStatusCode.NotFound, retired.StatusCode);
        }
    }
    /// <summary>
    /// Inspection address: the recorded value and, in edit
    /// context, an editor for it. The record renders every section
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
            "dueBy",
            "claimSourceContactName",
            "claimSourceContactTelephone",
            "claimSourceContactEmail",
            "contactName",
            "contactEmailAddress",
            "contactPhoneNumber",
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
    /// anything the form omits, which cleared them on every save.
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
    /// Notes: entries carry the date, the clock time and the
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
                services.RemoveAll<IAcquireCaseEditLease>();
                services.RemoveAll<IRecordManualCaseChase>();
                services.AddSingleton<IGetCaseEditBasis>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                Substitute<ICaseWorkflowQueries>(services, store);
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
            // A second window of the holder resumes the same lease: no takeover of themselves.
            Assert.Equal(store.LeaseToken, InputValue(recoveryHtml, "editLeaseToken"));
            Assert.DoesNotContain("Take over", RecordBar(recoveryHtml), StringComparison.Ordinal);
            Assert.DoesNotContain("name=\"takeOver\"", recoveryHtml, StringComparison.Ordinal);
        }
        Assert.Single(store.Claims);

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
        AssertCarriesNoLease(currentHtml);
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
    /// The Tasks section follows Notes, lists the Case's tasks, and offers Complete and Cancel
    /// on an open task and Add task only in the edit session: a section fetched without the
    /// render lease reads, and a closed task never offers an action.
    /// </summary>
    [Fact]
    public async Task TasksSectionListsTheTasksAndOffersItsActionsOnlyInTheEditSession()
    {
        var store = new RecordingCaseDetailsStore();
        var open = new CaseTaskRecord(
            Guid.NewGuid(), store.CaseId, "Send the figures to the garage", null, CaseTaskState.Open, 0, store.CaseVersion);
        var done = new CaseTaskRecord(
            Guid.NewGuid(), store.CaseId, "Phone the claimant", null, CaseTaskState.Completed, 1, store.CaseVersion);
        store.Tasks.AddRange([open, done]);
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<IGetCaseTasksSection>(services, store);
            Substitute<ICreateCaseTask>(services, store);
            Substitute<ICompleteCaseTask>(services, store);
            Substitute<ICancelCaseTask>(services, store);
        });

        Assert.Equal(
            "notes",
            OperatorLabelsSectionKeyBefore("tasks"));

        var html = WebUtility.HtmlDecode(
            await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=tasks"));

        var section = Section(html, "section-tasks-title");
        Assert.Contains("Send the figures to the garage", section, StringComparison.Ordinal);
        Assert.Contains("Phone the claimant", section, StringComparison.Ordinal);
        Assert.Contains("Open", section, StringComparison.Ordinal);
        Assert.Contains("Completed", section, StringComparison.Ordinal);
        Assert.Contains("handler=CreateCaseTask", section, StringComparison.Ordinal);
        Assert.Contains("handler=CompleteCaseTask", section, StringComparison.Ordinal);
        Assert.Contains("handler=CancelCaseTask", section, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"reason\"", section, StringComparison.Ordinal);
        var doneRow = Regex.Match(
            section,
            $"<tr data-case-task=\"{done.Id:D}\".*?</tr>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(doneRow.Success, "The completed task's row is not rendered.");
        Assert.DoesNotContain("handler=", doneRow.Value, StringComparison.Ordinal);

        using var read = await workspace.Client.GetAsync($"/Cases/{store.CaseId:D}/Section?section=tasks");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var fragment = WebUtility.HtmlDecode(await read.Content.ReadAsStringAsync());
        Assert.Contains("Send the figures to the garage", fragment, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=CompleteCaseTask", fragment, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=CreateCaseTask", fragment, StringComparison.Ordinal);
    }

    /// <summary>
    /// Complete, Cancel and Add task post the session's envelope to their use cases with the fixed
    /// reason each carries, and land back on the Tasks section; the session carries on.
    /// </summary>
    [Fact]
    public async Task CompleteCancelAndAddPostTheSessionsEnvelopeWithTheirFixedReasons()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<IGetCaseTasksSection>(services, store);
            Substitute<ICreateCaseTask>(services, store);
            Substitute<ICompleteCaseTask>(services, store);
            Substitute<ICancelCaseTask>(services, store);
        });
        var taskId = Guid.NewGuid();
        const string completeKey = "1a1b1c1d1e1f10111213141516171819";
        const string cancelKey = "2a2b2c2d2e2f20212223242526272829";
        const string addKey = "3a3b3c3d3e3f30313233343536373839";

        using var completed = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=CompleteCaseTask",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("taskId", taskId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("expectedTaskVersion", "4"),
                ("operationKey", completeKey),
                ("editLeaseToken", store.LeaseToken)));
        AssertPrg(completed, store.CaseId, expectedQuery: "section=tasks");
        var complete = Assert.Single(store.TaskCompletions);
        AssertTaskMutation(workspace, complete, completeKey, TaskLabels.CompleteReason);
        Assert.Equal(taskId, complete.TaskId);
        Assert.Equal(4, complete.ExpectedTaskVersion);

        using var cancelled = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=CancelCaseTask",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("taskId", taskId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("expectedTaskVersion", "5"),
                ("operationKey", cancelKey),
                ("editLeaseToken", store.LeaseToken)));
        AssertPrg(cancelled, store.CaseId, expectedQuery: "section=tasks");
        var cancel = Assert.Single(store.TaskCancellations);
        AssertTaskMutation(workspace, cancel, cancelKey, TaskLabels.CancelReason);
        Assert.Equal(5, cancel.ExpectedTaskVersion);

        using var added = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=CreateCaseTask",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("taskId", taskId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", addKey),
                ("editLeaseToken", store.LeaseToken),
                ("description", "Authorise the garage")));
        AssertPrg(added, store.CaseId, expectedQuery: "section=tasks");
        var create = Assert.Single(store.TaskCreations);
        AssertTaskMutation(workspace, create.Actor, create.CaseId, create.ExpectedCaseVersion, create.EditLeaseToken, create.OperationKey, create.Reason, addKey, TaskLabels.AddReason);
        Assert.Equal(taskId, create.TaskId);
        Assert.Equal("Authorise the garage", create.Description);
        Assert.Null(create.AssigneeId);
    }

    private static void AssertTaskMutation(
        LeasedWorkspace workspace,
        ExistingCaseTaskMutationRequest request,
        string operationKey,
        string reason) =>
        AssertTaskMutation(
            workspace,
            request.Actor,
            request.CaseId,
            request.ExpectedCaseVersion,
            request.EditLeaseToken,
            request.OperationKey,
            request.Reason,
            operationKey,
            reason);

    private static void AssertTaskMutation(
        LeasedWorkspace workspace,
        ActionActor actor,
        Guid caseId,
        long expectedCaseVersion,
        string editLeaseToken,
        string recordedOperationKey,
        string recordedReason,
        string operationKey,
        string reason)
    {
        AssertClaimant(workspace, actor);
        Assert.Equal(workspace.Store.CaseId, caseId);
        Assert.Equal(workspace.Store.CaseVersion, expectedCaseVersion);
        Assert.Equal(workspace.Store.LeaseToken, editLeaseToken);
        Assert.Equal(operationKey, recordedOperationKey);
        Assert.Equal(reason, recordedReason);
    }

    /// <summary>The key of the section the record lists immediately before <paramref name="key"/>.</summary>
    private static string OperatorLabelsSectionKeyBefore(string key)
    {
        var keys = Pegasus.Web.Presentation.OperatorLabels.CaseWorkspace.Sections
            .Select(section => section.Key)
            .ToList();
        return keys[keys.IndexOf(key) - 1];
    }

    /// <summary>
    /// What each reasoned lifecycle mutation posts from the leased workspace — the case id, its
    /// version, operation key, lease token, reason, and action-specific fields.
    /// </summary>
}
