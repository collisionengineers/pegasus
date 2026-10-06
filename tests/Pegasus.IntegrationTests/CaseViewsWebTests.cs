using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.IntegrationTests.Reports;
using Pegasus.Web.Presentation;

using static Pegasus.IntegrationTests.CaseWebTestSupport;
using Frame = Pegasus.Web.Presentation.CaseWorkspaceLabels.Frame;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The two views of an Inspection + Audit Case once it has its Audit (v29
/// option 4, Stage 2 slice 7): the Views card heading the aside, the Audit
/// view by default, the Inspection view that reads and edits the primary work
/// (operator, 2 October 2026), the Report of each view, the Audit folder chip
/// in Files, and Create audit's dialog and in-place post.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseViewsWebTests
{
    /// <summary>The recording store's Case/PO and the Audit report's reference.</summary>
    private const string Reference = "QDOS3100042";
    private const string AuditReference = "a.QDOS3100042";

    private static readonly DateTimeOffset InspectionSentAtUtc = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    /// <summary>An Inspection + Audit Case after Create audit: With Engineer on its Audit.</summary>
    private static RecordingCaseDetailsStore AuditedCase()
    {
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            SummaryCaseType = CaseType.InspectionAndAudit
        };
        store.GiveAudit(InspectionSentAtUtc);
        return store;
    }

    /// <summary>An Inspection + Audit Case whose Inspection report is sent, with its Engineer.</summary>
    private static RecordingCaseDetailsStore SentCase(Guid engineerId) => new()
    {
        State = CaseLifecycleState.PostReport,
        SummaryCaseType = CaseType.InspectionAndAudit,
        AssignedEngineerId = engineerId,
        ReportSentEvidence = SentEvidence(InspectionSentAtUtc)
    };

    [Fact]
    public async Task ACaseWithoutAnAuditHasNoViewsCardAndIgnoresTheView()
    {
        var store = new RecordingCaseDetailsStore { SummaryCaseType = CaseType.InspectionAndAudit };
        using var host = new ReadingHost(store);

        var html = await host.ReadAsync($"/Cases/{store.CaseId:D}?view=inspection");

        Assert.DoesNotContain("data-case-views", html, StringComparison.Ordinal);
        // Razor keeps a data- attribute whose value is null, empty: no view is named.
        Assert.DoesNotMatch("data-case-view=\"[^\"]", html);
        Assert.Contains("data-section-edit=", html, StringComparison.Ordinal);
        Assert.Contains("data-case-edit-form", RecordBar(html), StringComparison.Ordinal);
        // No form names a view the Case does not have.
        Assert.DoesNotContain("name=\"view\" value=\"inspection\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStandaloneAuditHasOneViewAndKeepsItsOriginalReport()
    {
        var store = new RecordingCaseDetailsStore { SummaryCaseType = CaseType.Audit };
        using var host = new ReadingHost(store);

        var html = await host.ReadAsync($"/Cases/{store.CaseId:D}?view=inspection");

        Assert.DoesNotContain("data-case-views", html, StringComparison.Ordinal);
        Assert.Contains("id=\"section-original-report\"", html, StringComparison.Ordinal);
        Assert.Contains("data-section-edit=", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Once the Case has its Audit, the Views card is the aside's first card
    /// and the page opens on the Audit: its row reads plain with the Case's
    /// state chip exactly as the ribbon renders it, and the Inspection row is
    /// the link to <c>?view=inspection</c> with its Sent chip.
    /// </summary>
    [Fact]
    public async Task TheViewsCardHeadsTheAsideAndTheCaseOpensOnItsAudit()
    {
        var store = AuditedCase();
        using var host = new ReadingHost(store);

        var html = await host.ReadAsync($"/Cases/{store.CaseId:D}");

        var asideStart = html.IndexOf("data-case-aside>", StringComparison.Ordinal);
        Assert.True(asideStart >= 0, "The aside is not rendered.");
        Assert.Equal(
            html.IndexOf("<section class=\"panel context-card\" data-case-views>", asideStart, StringComparison.Ordinal),
            html.IndexOf("<section", asideStart, StringComparison.Ordinal));
        var card = ViewsCard(html);
        Assert.Contains($"<h2>{Frame.Views}</h2>", card, StringComparison.Ordinal);

        var inspection = ViewRow(card, "inspection");
        Assert.Contains(
            $"<a href=\"/Cases/{store.CaseId:D}?view=inspection\">{Frame.InspectionView} · {Reference}</a>",
            inspection,
            StringComparison.Ordinal);
        Assert.Contains($"<span class=\"status status--plain status--green\">{Frame.Sent}</span>", inspection, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-current", inspection, StringComparison.Ordinal);

        var audit = ViewRow(card, "audit");
        Assert.Contains($"<span aria-current=\"page\">{Frame.AuditView} · {AuditReference}</span>", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("href=", audit, StringComparison.Ordinal);
        Assert.Contains(RibbonStateChip(html), audit, StringComparison.Ordinal);

        Assert.Equal(CaseWorkSelector.Current, Assert.Single(store.PageFrameQueries).Work);
    }

    /// <summary>
    /// Create audit no longer waits for the Inspection report to be sent
    /// (operator, 1 October 2026): an Audit created before that leaves the
    /// Inspection row without a Sent chip, never with one it did not earn.
    /// </summary>
    [Fact]
    public async Task TheInspectionRowHasNoSentChipWhenItsReportWasNeverSent()
    {
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            SummaryCaseType = CaseType.InspectionAndAudit
        };
        store.GiveAudit(InspectionSentAtUtc, inspectionSent: false);
        using var host = new ReadingHost(store);

        var html = await host.ReadAsync($"/Cases/{store.CaseId:D}");

        var inspection = ViewRow(ViewsCard(html), "inspection");
        Assert.Contains($"{Frame.InspectionView} · {Reference}</a>", inspection, StringComparison.Ordinal);
        Assert.DoesNotContain(Frame.Sent, inspection, StringComparison.Ordinal);
        Assert.DoesNotContain("status--green", inspection, StringComparison.Ordinal);
        // Razor keeps a data- attribute whose value is null, empty: no view is named.
        Assert.DoesNotMatch("data-case-view=\"[^\"]", html);
        Assert.Contains("data-section-edit=", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Inspection view reads the primary work and edits it (operator,
    /// 2 October 2026): the ribbon's Edit and every section head's Edit, each
    /// claim naming the view, no read-only label anywhere, and its section
    /// links, Refresh and mounted bodies stay in the view.
    /// </summary>
    [Fact]
    public async Task TheInspectionViewReadsThePrimaryWorkAndOffersEdit()
    {
        var store = AuditedCase();
        using var host = new ReadingHost(store);

        var html = await host.ReadAsync($"/Cases/{store.CaseId:D}?view=inspection");

        Assert.Equal(CaseWorkSelector.Primary, Assert.Single(store.PageFrameQueries).Work);
        Assert.Contains("data-case-view=\"inspection\"", html, StringComparison.Ordinal);
        Assert.Contains("data-case-edit-form", RecordBar(html), StringComparison.Ordinal);
        Assert.DoesNotContain("Read-only · Audit created", html, StringComparison.Ordinal);
        foreach (var key in new[] { "overview", "claim", "inspection", "estimate", "settlement", "report" })
        {
            Assert.Contains($"data-section-edit=\"{key}\"", html, StringComparison.Ordinal);
            Assert.Matches(
                $"name=\"section\" value=\"{key}\" />\\s*<input type=\"hidden\" name=\"view\" value=\"inspection\" />",
                html);
            Assert.DoesNotContain($"data-section-availability=\"{key}\"", html, StringComparison.Ordinal);
        }

        var card = ViewsCard(html);
        Assert.Contains($"<span aria-current=\"page\">{Frame.InspectionView} · {Reference}</span>", ViewRow(card, "inspection"), StringComparison.Ordinal);
        Assert.Contains(
            $"<a href=\"/Cases/{store.CaseId:D}\">{Frame.AuditView} · {AuditReference}</a>",
            ViewRow(card, "audit"),
            StringComparison.Ordinal);

        Assert.Contains(
            $"href=\"/Cases/{store.CaseId:D}?section=claim&view=inspection#section-claim\"",
            html,
            StringComparison.Ordinal);
        // Refresh replays the view through the shared refresh button's field.
        Assert.Contains("<input type=\"hidden\" name=\"view\" value=\"inspection\" data-refresh-field=\"view\" />", html, StringComparison.Ordinal);

        var files = await host.ReadAsync($"/Cases/{store.CaseId:D}?view=inspection&section=files");
        Assert.DoesNotContain("data-section-availability=\"files\"", files, StringComparison.Ordinal);
        Assert.DoesNotContain("data-section-availability=\"notes\"", files, StringComparison.Ordinal);

        var vehicle = await host.ReadAsync($"/Cases/{store.CaseId:D}/Section?section=vehicle&view=inspection");
        Assert.Equal(CaseWorkSelector.Primary, store.VehicleSectionQueries[^1].Work);
        Assert.Contains("data-section-edit=\"vehicle\"", vehicle, StringComparison.Ordinal);
        Assert.Contains("<input type=\"hidden\" name=\"view\" value=\"inspection\" />", vehicle, StringComparison.Ordinal);
        var audited = await host.ReadAsync($"/Cases/{store.CaseId:D}/Section?section=vehicle");
        Assert.Equal(CaseWorkSelector.Current, store.VehicleSectionQueries[^1].Work);
        Assert.Contains("data-section-edit=\"vehicle\"", audited, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"view\" value=\"inspection\"", audited, StringComparison.Ordinal);
    }

    /// <summary>
    /// The lease is Case-wide (Decisions answer 3), and its holder at
    /// <c>?view=inspection</c> edits the Inspection there (operator, 2 October
    /// 2026): the Case form names the view, and a save posted from it writes
    /// the primary work and returns to the Inspection view.
    /// </summary>
    [Fact]
    public async Task TheLeaseHolderSavesThePrimaryWorkFromTheInspectionView()
    {
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            SummaryCaseType = CaseType.InspectionAndAudit,
            AcceptWorkspaceSaves = true
        };
        store.GiveAudit(InspectionSentAtUtc);
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<ISaveCaseWorkspace>(services, store));

        var html = WebUtility.HtmlDecode(await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?view=inspection"));

        var bar = RecordBar(html);
        Assert.Contains($">{Frame.Editing}</span>", bar, StringComparison.Ordinal);
        Assert.Contains("data-case-done", bar, StringComparison.Ordinal);
        Assert.Contains("data-case-save-now", bar, StringComparison.Ordinal);
        Assert.Contains("data-case-actions", bar, StringComparison.Ordinal);
        Assert.Contains("id=\"case-edit-form\"", html, StringComparison.Ordinal);
        Assert.Contains("form=\"case-edit-form\"", MainColumn(html), StringComparison.Ordinal);
        Assert.Matches(
            "(?s)id=\"case-edit-form\"[^>]*>(?:(?!</form>).)*<input type=\"hidden\" name=\"view\" value=\"inspection\" />",
            html);
        Assert.DoesNotContain("Read-only · Audit created", html, StringComparison.Ordinal);

        using var saved = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(
                Guid.NewGuid().ToString("N"),
                "Inspection corrected",
                ("claimantName", "Inspection claimant"),
                ("section", "claim"),
                ("view", "inspection")));

        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal(
            $"/Cases/{store.CaseId:D}?section=claim&view=inspection",
            saved.Headers.Location?.OriginalString);
        Assert.Equal(CaseWorkSelector.Primary, Assert.Single(store.Saves).Work);
    }

    /// <summary>
    /// One report per work (v29 P4): the Audit view's card carries a.{Case/PO}
    /// and shows the Audit report alone (operator, 2 October 2026); the
    /// Inspection view shows its own report, sent or not, with the same
    /// delivery and More menu the Audit view offers (operator, 2 October 2026).
    /// </summary>
    [Fact]
    public async Task EachViewShowsItsOwnReport()
    {
        var store = AuditedCase();
        var reports = new ReportsPerWork(store.CaseId);
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
            Substitute<ICaseReportGenerationStore>(services, reports));
        var sentLine = $"{Reference} · {Frame.Sent} {OperatorLabels.OfficeTime(InspectionSentAtUtc)}";

        var auditHtml = WebUtility.HtmlDecode(await workspace.GetWorkspaceAsync());
        var auditReport = Section(auditHtml, "section-report-title");
        Assert.Contains($"<span class=\"mono\" data-report-reference>{AuditReference} · </span>", auditReport, StringComparison.Ordinal);
        Assert.DoesNotContain(sentLine, auditReport, StringComparison.Ordinal);
        Assert.DoesNotContain(reports.Inspection!.Id.ToString("D"), auditReport, StringComparison.Ordinal);
        Assert.Contains($"{AuditReference} · </span>", ReportStatus(auditReport), StringComparison.Ordinal);
        Assert.Contains("data-send-report", auditReport, StringComparison.Ordinal);
        // The delivery message is the Case report delivery template, rendered
        // for staff to edit before Send report.
        Assert.Matches(
            "<textarea[^>]*name=\"coveringMessage\"[^>]*data-report-message>[^<]*Kind regards\\s+Collision Engineers</textarea>",
            auditReport);
        Assert.Matches("<textarea[^>]*data-report-message>[^<]*Our reference: \\S+", auditReport);
        Assert.Contains(reports.Audit!.Id.ToString("D"), auditReport, StringComparison.Ordinal);

        var inspectionHtml = WebUtility.HtmlDecode(await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?view=inspection"));
        var inspectionReport = Section(inspectionHtml, "section-report-title");
        // The sent report reads as its generation, as the Audit's does.
        Assert.DoesNotContain(sentLine, ReportStatus(inspectionReport), StringComparison.Ordinal);
        Assert.Contains("data-report-filing", ReportStatus(inspectionReport), StringComparison.Ordinal);
        Assert.Contains(reports.Inspection!.Id.ToString("D"), inspectionReport, StringComparison.Ordinal);
        Assert.DoesNotContain(reports.Audit!.Id.ToString("D"), inspectionReport, StringComparison.Ordinal);
        Assert.DoesNotContain("data-report-reference", inspectionReport, StringComparison.Ordinal);
        Assert.Contains("data-send-report", inspectionReport, StringComparison.Ordinal);
        Assert.Equal(
            auditReport.Contains("data-report-menu", StringComparison.Ordinal),
            inspectionReport.Contains("data-report-menu", StringComparison.Ordinal));
        Assert.Contains("<input type=\"hidden\" name=\"view\" value=\"inspection\" />", inspectionReport, StringComparison.Ordinal);
        Assert.Contains(CaseWorkSelector.Primary, reports.CurrentReads);
    }

    /// <summary>
    /// Files names the Audit's a. folder beside the Case folder, mirroring the
    /// Case chip's states and tones (operator, 24 September 2026).
    /// </summary>
    [Theory]
    [InlineData(CaseCustodyState.Confirmed, "audit-folder", "status--green", "Box audit · confirmed")]
    [InlineData(CaseCustodyState.Confirmed, null, "status--amber", "Box audit folder: unavailable")]
    [InlineData(CaseCustodyState.Pending, null, "status--amber", "Box audit folder: preparing")]
    [InlineData(CaseCustodyState.Failed, null, "status--red", "Box audit folder: unavailable")]
    public async Task FilesNamesTheAuditFolderAsTheCaseFolderIsNamed(
        CaseCustodyState state, string? remoteId, string tone, string text)
    {
        var store = AuditedCase();
        store.AuditCustodyState = state;
        store.AuditCustodyFolderRemoteId = remoteId;
        using var host = new ReadingHost(store);

        var files = Section(await host.ReadAsync($"/Cases/{store.CaseId:D}?section=files"), "section-files-title");

        Assert.Contains(
            $"<span class=\"status {tone} status--plain\" data-audit-custody-chip>{text}</span>",
            files,
            StringComparison.Ordinal);
        Assert.True(
            files.IndexOf("data-custody-chip", StringComparison.Ordinal)
                < files.IndexOf("data-audit-custody-chip", StringComparison.Ordinal),
            "The Audit folder's chip follows the Case folder's.");
    }

    [Fact]
    public async Task FilesHasNoAuditFolderChipWithoutAnAudit()
    {
        var store = new RecordingCaseDetailsStore { SummaryCaseType = CaseType.InspectionAndAudit };
        using var host = new ReadingHost(store);

        var files = Section(await host.ReadAsync($"/Cases/{store.CaseId:D}?section=files"), "section-files-title");

        Assert.Contains("data-custody-chip", files, StringComparison.Ordinal);
        Assert.DoesNotContain("data-audit-custody-chip", files, StringComparison.Ordinal);
    }

    /// <summary>
    /// Decision D: the dialog states the Case, the Audit reference it gains
    /// and the Engineer; there is no Outcome row and no reason.
    /// </summary>
    [Fact]
    public async Task CreateAuditStatesTheCaseTheAuditReferenceAndTheEngineer()
    {
        var engineerId = Guid.NewGuid();
        var store = SentCase(engineerId);
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<IStaffAccountQueries>(services, new StubStaffAccounts(engineerId, "Ed Mawdsley", StaffRole.Engineer)));

        var html = WebUtility.HtmlDecode(await workspace.GetWorkspaceAsync());

        Assert.Contains("data-create-audit", RecordBar(html), StringComparison.Ordinal);
        var dialog = Section(html, "case-create-audit-dialog-title");
        Assert.Contains("handler=CreateAudit", dialog, StringComparison.Ordinal);
        Assert.Contains($"<dt>{Frame.AuditDialogCase}</dt><dd class=\"mono\">{Reference}</dd>", dialog, StringComparison.Ordinal);
        Assert.Contains($"<dt>{Frame.AuditReference}</dt><dd class=\"mono\" data-audit-reference>{AuditReference}</dd>", dialog, StringComparison.Ordinal);
        Assert.Contains(
            $"<dt>{OperatorLabels.CaseWorkspace.RibbonEngineer}</dt><dd data-audit-engineer>Ed Mawdsley</dd>",
            dialog,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Outcome", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"reason\"", dialog, StringComparison.Ordinal);
    }

    /// <summary>
    /// The dialog's one primary posts the session's envelope to the use case
    /// and lands back on this Case's default view with no notice; a refusal
    /// stays on the Case with Core's approved reason.
    /// </summary>
    [Fact]
    public async Task CreateAuditPostsInPlaceWithoutANoticeOrStatesTheRefusal()
    {
        var store = SentCase(Guid.NewGuid());
        var createAudit = new RecordingCreateAudit();
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<ICreateAudit>(services, createAudit));

        const string operationKey = "6a6b6c6d6e6f60616263646566676869";
        using var created = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=CreateAudit",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", operationKey),
                ("editLeaseToken", store.LeaseToken)));

        AssertPrg(created, store.CaseId, expectedQuery: string.Empty);
        var request = Assert.Single(createAudit.Requests);
        AssertClaimant(workspace, request.Actor);
        Assert.Equal(store.CaseId, request.CaseId);
        Assert.Equal(store.CaseVersion, request.ExpectedVersion);
        Assert.Equal(store.LeaseToken, request.EditLeaseToken);
        Assert.Equal(operationKey, request.OperationKey);
        Assert.DoesNotContain("data-confirmation", await workspace.GetWorkspaceAsync(), StringComparison.Ordinal);

        createAudit.Refusal = AuditRefusal.Held;
        using var refused = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=CreateAudit",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", "7a7b7c7d7e7f70717273747576777879"),
                ("editLeaseToken", store.LeaseToken)));

        AssertPrg(refused, store.CaseId, expectedQuery: string.Empty);
        var after = WebUtility.HtmlDecode(await workspace.GetWorkspaceAsync());
        Assert.Contains("A held case cannot have an audit created from it.", after, StringComparison.Ordinal);
        Assert.Single(createAudit.Requests);
    }

    /// <summary>
    /// The Inspection report still to be sent after Create audit (operator,
    /// 1 October 2026): the Inspection view sends it on its own
    /// work, its forms naming the view.
    /// </summary>
    [Fact]
    public async Task TheInspectionViewSendsItsReportWhileItAwaitsSending()
    {
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            SummaryCaseType = CaseType.InspectionAndAudit
        };
        store.GiveAudit(InspectionSentAtUtc, inspectionSent: false);
        var reports = new ReportsPerWork(store.CaseId);
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
            Substitute<ICaseReportGenerationStore>(services, reports));

        var inspectionHtml = WebUtility.HtmlDecode(await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?view=inspection"));
        var inspectionReport = Section(inspectionHtml, "section-report-title");
        Assert.DoesNotContain(Frame.Sent, ReportStatus(inspectionReport), StringComparison.Ordinal);
        Assert.Contains("data-send-report", inspectionReport, StringComparison.Ordinal);
        Assert.Contains("<input type=\"hidden\" name=\"view\" value=\"inspection\" />", inspectionReport, StringComparison.Ordinal);
        Assert.Contains(reports.Inspection!.Id.ToString("D"), inspectionReport, StringComparison.Ordinal);
        Assert.DoesNotContain(reports.Audit!.Id.ToString("D"), inspectionReport, StringComparison.Ordinal);
        Assert.DoesNotContain("data-report-reference", inspectionReport, StringComparison.Ordinal);
        Assert.Contains(CaseWorkSelector.Primary, reports.CurrentReads);
    }

    /// <summary>
    /// The Next action is the viewed work's (operator, 2 October 2026): the
    /// Inspection view states the Inspection report's own step, its link
    /// staying in that view, and nothing once that report is sent; the Audit
    /// view states the Audit's step as before.
    /// </summary>
    [Fact]
    public async Task TheInspectionViewsNextActionIsTheInspectionReports()
    {
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            SummaryCaseType = CaseType.InspectionAndAudit,
            AssignedEngineerId = Guid.NewGuid()
        };
        store.GiveAudit(InspectionSentAtUtc, inspectionSent: false);
        var reports = new ReportsPerWork(store.CaseId) { Inspection = null };
        using var host = new ReadingHost(store, services =>
        {
            Substitute<ICaseReportGenerationStore>(services, reports);
            Substitute<ICaseReportSnapshotSource>(
                services,
                new AssessmentReportDraftWebTests.FakeProjectionSource(AssessmentReportDraftWebTests.ReadyInput(store.CaseId)));
        });
        var inspectionPath = $"/Cases/{store.CaseId:D}?view=inspection";

        // No Inspection generation yet: Generate report, in the Inspection view.
        var next = NextAction(await host.ReadAsync(inspectionPath));
        Assert.Contains($"<span data-next-label>{CaseWorkspaceLabels.ReportDelivery.GenerateReport}</span>", next, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Cases/{store.CaseId:D}?section=report&view=inspection#section-report\"", next, StringComparison.Ordinal);
        Assert.Contains("data-section-jump=\"report\"", next, StringComparison.Ordinal);
        Assert.DoesNotContain("data-report-not-ready", next, StringComparison.Ordinal);

        // The Audit's own step is untouched by the Inspection's: its stored
        // report awaits delivery, and its link lands on the Audit view.
        var auditNext = NextAction(await host.ReadAsync($"/Cases/{store.CaseId:D}"));
        Assert.Contains($"<span data-next-label>{CaseWorkspaceLabels.ReportDelivery.SendReport}</span>", auditNext, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Cases/{store.CaseId:D}?section=report#section-report\"", auditNext, StringComparison.Ordinal);

        // The Inspection report stored: its delivery is the step.
        reports.Inspection = ReportsPerWork.Generation(store.CaseId, Reference, CaseWorkKind.Primary);
        next = NextAction(await host.ReadAsync(inspectionPath));
        Assert.Contains($"<span data-next-label>{CaseWorkspaceLabels.ReportDelivery.SendReport}</span>", next, StringComparison.Ordinal);
        Assert.Contains("view=inspection#section-report", next, StringComparison.Ordinal);

        // The Inspection report sent: nothing more for the Inspection.
        store.GiveAudit(InspectionSentAtUtc, inspectionSent: true);
        next = NextAction(await host.ReadAsync(inspectionPath));
        Assert.DoesNotContain("data-next-label", next, StringComparison.Ordinal);
        Assert.DoesNotContain("data-report-not-ready", next, StringComparison.Ordinal);
    }

    /// <summary>
    /// Once the Inspection report is sent, Create audit is the Next action of
    /// an Inspection + Audit Case that has no Audit yet, with the Actions
    /// menu's own control: live, or greyed with Core's reason (operator,
    /// 2 October 2026). Any other Case is marked completed next.
    /// </summary>
    [Theory]
    [InlineData(CaseType.InspectionAndAudit, true, null)]
    [InlineData(CaseType.InspectionAndAudit, false, "Report preparation requires an assigned Engineer.")]
    [InlineData(CaseType.Inspection, true, null)]
    public async Task CreateAuditIsTheNextActionOnceTheInspectionReportIsSent(
        CaseType caseType, bool engineerAssigned, string? condition)
    {
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.PostReport,
            SummaryCaseType = caseType,
            AssignedEngineerId = engineerAssigned ? Guid.NewGuid() : null,
            ReportSentEvidence = SentEvidence(InspectionSentAtUtc)
        };
        using var host = new ReadingHost(store, services =>
        {
            Substitute<ICaseReportGenerationStore>(services, new ReportsPerWork(store.CaseId));
            Substitute<ICaseReportSnapshotSource>(
                services,
                new AssessmentReportDraftWebTests.FakeProjectionSource(AssessmentReportDraftWebTests.ReadyInput(store.CaseId)));
        });

        var next = NextAction(await host.ReadAsync($"/Cases/{store.CaseId:D}"));

        if (caseType != CaseType.InspectionAndAudit)
        {
            Assert.Contains($"<span data-next-label>{Frame.MarkCompleted}</span>", next, StringComparison.Ordinal);
            Assert.DoesNotContain("data-next-create-audit", next, StringComparison.Ordinal);
            return;
        }
        Assert.Contains($"<span data-next-label>{Frame.CreateAudit}</span>", next, StringComparison.Ordinal);
        if (condition is null)
        {
            Assert.Contains(
                $"<button type=\"button\" class=\"btn btn--small\" data-dialog-open=\"case-create-audit-dialog\" data-next-create-audit>{Frame.CreateAudit}</button>",
                next,
                StringComparison.Ordinal);
            Assert.DoesNotContain("menu-gated", next, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains($"<span class=\"menu-gated\" title=\"{condition}\" data-create-audit-condition=\"{condition}\">", next, StringComparison.Ordinal);
            Assert.Contains("disabled aria-disabled=\"true\" data-next-create-audit>", next, StringComparison.Ordinal);
            Assert.DoesNotContain("data-dialog-open", next, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Generate report posted from the Inspection view makes the Inspection
    /// report, lands back on the Inspection view with that report marked to
    /// open once, and the Next action there moves on to its delivery
    /// (operator, 2 October 2026).
    /// </summary>
    [Fact]
    public async Task GenerateReportFromTheInspectionViewReturnsToItWithTheReportToOpen()
    {
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            SummaryCaseType = CaseType.InspectionAndAudit,
            AssignedEngineerId = Guid.NewGuid()
        };
        store.GiveAudit(InspectionSentAtUtc, inspectionSent: false);
        var reports = new ReportsPerWork(store.CaseId) { Inspection = null };
        var generator = new InspectionReportGenerator(reports, store.CaseId);
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            Substitute<IGetCaseHeader>(services, store);
            Substitute<ICaseReportGenerationStore>(services, reports);
            Substitute<IGenerateCaseReport>(services, generator);
            Substitute<ICaseReportSnapshotSource>(
                services,
                new AssessmentReportDraftWebTests.FakeProjectionSource(AssessmentReportDraftWebTests.ReadyInput(store.CaseId)));
        });
        var caseId = store.CaseId.ToString("D");

        using var generated = await workspace.Client.PostAsync(
            $"/Cases/{caseId}?handler=GenerateReport&section=report",
            Form(
                workspace.AntiforgeryToken,
                ("id", caseId),
                ("operationKey", Guid.NewGuid().ToString("N")),
                ("editLeaseToken", store.LeaseToken),
                ("expectedCaseVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("view", "inspection")));

        Assert.Equal(HttpStatusCode.Redirect, generated.StatusCode);
        Assert.Equal($"/Cases/{caseId}?section=report&view=inspection", generated.Headers.Location?.OriginalString);
        Assert.Equal(CaseWorkSelector.Primary, Assert.Single(generator.Requests).Work);

        var arrived = WebUtility.HtmlDecode(await GetHtmlAsync(workspace.Client, generated.Headers.Location!.OriginalString));
        var report = Section(arrived, "section-report-title");
        Assert.Matches(
            "<a[^>]*data-report-artifact=\"AssessmentReport\"[^>]*data-open-on-arrival=\"true\"",
            report);
        Assert.Contains($"generationId={reports.Inspection!.Id:D}", report, StringComparison.Ordinal);
        Assert.Contains("view=inspection", report, StringComparison.Ordinal);
        var next = NextAction(arrived);
        Assert.Contains($"<span data-next-label>{CaseWorkspaceLabels.ReportDelivery.SendReport}</span>", next, StringComparison.Ordinal);

        var later = WebUtility.HtmlDecode(await GetHtmlAsync(workspace.Client, $"/Cases/{caseId}?section=report&view=inspection"));
        Assert.DoesNotContain("data-open-on-arrival=\"true\"", later, StringComparison.Ordinal);
    }

    /// <summary>
    /// While the Inspection report is not ready, the Inspection view's Next
    /// action lists that report's own blockers, each linking to its section
    /// in the Inspection view (operator, 2 October 2026).
    /// </summary>
    [Fact]
    public async Task TheInspectionViewListsTheInspectionReportsBlockers()
    {
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            SummaryCaseType = CaseType.InspectionAndAudit,
            AssignedEngineerId = Guid.NewGuid()
        };
        store.GiveAudit(InspectionSentAtUtc, inspectionSent: false);
        using var host = new ReadingHost(store, services =>
        {
            Substitute<ICaseReportGenerationStore>(services, new ReportsPerWork(store.CaseId) { Inspection = null });
            Substitute<ICaseReportSnapshotSource>(services, store);
        });

        var next = NextAction(await host.ReadAsync($"/Cases/{store.CaseId:D}?view=inspection"));

        Assert.Contains("data-report-not-ready", next, StringComparison.Ordinal);
        Assert.DoesNotContain("data-next-label", next, StringComparison.Ordinal);
        var blockerLinks = Regex.Matches(next, "data-report-blocker=\"[a-z-]+\".*?href=\"([^\"]+)\"", RegexOptions.Singleline);
        Assert.NotEmpty(blockerLinks);
        Assert.All(blockerLinks, link => Assert.Contains("view=inspection#section-", link.Groups[1].Value, StringComparison.Ordinal));
    }

    /// <summary>
    /// The Inspection report already sent is generated and sent
    /// again from the Inspection view when needed (operator, 2 October 2026):
    /// each command posted from it reaches the report use cases on the
    /// Inspection's own work and lands back on the Inspection view's Report.
    /// </summary>
    [Fact]
    public async Task TheInspectionReportIsGeneratedAndSentAgainFromTheInspectionView()
    {
        var store = AuditedCase();
        var commands = new RecordingReportCommands();
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            Substitute<ICaseReportGenerationStore>(services, new ReportsPerWork(store.CaseId));
            Substitute<IGenerateCaseReport>(services, commands);
            Substitute<ISendCaseReport>(services, commands);
        });
        var caseId = store.CaseId.ToString("D");
        var version = store.CaseVersion.ToString(CultureInfo.InvariantCulture);

        async Task PostFromTheInspectionViewAsync(string handler, params (string Name, string Value)[] fields)
        {
            using var posted = await workspace.Client.PostAsync(
                $"/Cases/{caseId}?handler={handler}&section=report",
                Form(workspace.AntiforgeryToken, [("id", caseId), .. fields, ("view", "inspection")]));

            Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode);
            Assert.Equal($"/Cases/{caseId}?section=report&view=inspection", posted.Headers.Location?.OriginalString);
        }

        await PostFromTheInspectionViewAsync(
            "GenerateReport",
            ("operationKey", Guid.NewGuid().ToString("N")),
            ("editLeaseToken", store.LeaseToken),
            ("expectedCaseVersion", version));
        await PostFromTheInspectionViewAsync(
            "SendReport",
            ("operationKey", Guid.NewGuid().ToString("N")),
            ("editLeaseToken", store.LeaseToken),
            ("expectedCaseVersion", version),
            ("generationId", Guid.NewGuid().ToString("D")),
            ("expectedGenerationVersion", "1"),
            ("coveringMessage", "Please find attached our report."),
            ("toRecipients", "handler@principal.example"));

        Assert.Equal(2, commands.Calls.Count);
        Assert.Equal(CaseWorkSelector.Primary, Assert.IsType<GenerateCaseReportRequest>(commands.Calls[0]).Work);
        Assert.Equal(CaseWorkSelector.Primary, Assert.IsType<SendCaseReportRequest>(commands.Calls[1]).Work);
    }

    /// <summary>
    /// Mark report sent from the Inspection view marks the Inspection's own
    /// work and lands back on that view's Report section.
    /// </summary>
    [Fact]
    public async Task MarkReportSentFromTheInspectionViewReturnsToItsReport()
    {
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            SummaryCaseType = CaseType.InspectionAndAudit
        };
        store.GiveAudit(InspectionSentAtUtc, inspectionSent: false);
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<ILinkReportEvidence>(services, store));

        using var linked = await workspace.PostAsync(
            "Tasks?handler=LinkReportEvidence",
            workspace.MutationForm(
                "link-evidence",
                "Report sent",
                ("evidenceId", Guid.NewGuid().ToString("D")),
                ("view", "inspection")));

        Assert.Equal(HttpStatusCode.Redirect, linked.StatusCode);
        Assert.Equal(
            $"/Cases/{store.CaseId:D}?section=report&view=inspection",
            linked.Headers.Location?.OriginalString);
        Assert.Equal(CaseWorkSelector.Primary, Assert.Single(store.EvidenceLinks).Work);
    }

    private static string ViewsCard(string html)
    {
        var start = html.IndexOf("data-case-views>", StringComparison.Ordinal);
        Assert.True(start >= 0, "The Views card is not rendered.");
        var end = html.IndexOf("</section>", start, StringComparison.Ordinal);
        return html[start..end];
    }

    private static string ViewRow(string card, string key)
    {
        var start = card.IndexOf($"data-case-view-row=\"{key}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"The {key} view row is not rendered.");
        var end = card.IndexOf("</div>", start, StringComparison.Ordinal);
        return card[start..end];
    }

    /// <summary>The aside's Next action card.</summary>
    private static string NextAction(string html)
    {
        var card = NextActionRegex().Match(html);
        Assert.True(card.Success, "The Next action card is not rendered.");
        return card.Value;
    }

    /// <summary>The report card's status line.</summary>
    private static string ReportStatus(string report)
    {
        var start = report.IndexOf("data-report-status", StringComparison.Ordinal);
        Assert.True(start >= 0, "The report card's status is not rendered.");
        var end = report.IndexOf("</div>", start, StringComparison.Ordinal);
        return report[start..end];
    }

    /// <summary>The state chip the ribbon renders, exactly.</summary>
    private static string RibbonStateChip(string html)
    {
        var chip = Regex.Match(
            html,
            "data-case-ribbon-chips>\\s*(?<chip><span class=\"status status--[a-z]+\">[^<]+</span>)",
            RegexOptions.CultureInvariant);
        Assert.True(chip.Success, "The ribbon's state chip is not rendered.");
        return chip.Groups["chip"].Value;
    }

    /// <summary>A reading client with the recording store and any further substitutions.</summary>
    private sealed class ReadingHost : IDisposable
    {
        private readonly IntakeWebApplicationFactory baseFactory = new();
        private readonly WebApplicationFactory<Program> factory;

        public ReadingHost(RecordingCaseDetailsStore store, Action<IServiceCollection>? substitute = null)
        {
            factory = baseFactory.WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    Substitute<IGetCaseEditBasis>(services, store);
                    SubstituteDetailsPageReaders(services, store);
                    substitute?.Invoke(services);
                }));
            Client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });
        }

        public HttpClient Client { get; }

        public async Task<string> ReadAsync(string path) =>
            WebUtility.HtmlDecode(await GetHtmlAsync(Client, path));

        public void Dispose()
        {
            Client.Dispose();
            factory.Dispose();
            baseFactory.Dispose();
        }
    }

    /// <summary>
    /// Each work's current generation: the Audit's for the current work, the
    /// Inspection's for the primary one. Only the reads the Case page makes
    /// are answered.
    /// </summary>
    private sealed class ReportsPerWork(Guid caseId) : ICaseReportGenerationStore
    {
        /// <summary>The Audit's current generation; null while it has none.</summary>
        public CaseReportGenerationRecord? Audit { get; set; } = Generation(caseId, AuditReference, CaseWorkKind.Audit);

        /// <summary>The Inspection's current generation; null while it has none.</summary>
        public CaseReportGenerationRecord? Inspection { get; set; } = Generation(caseId, Reference, CaseWorkKind.Primary);

        public List<CaseWorkSelector> CurrentReads { get; } = [];

        public Task<CaseReportGenerationRecord?> GetCurrentAsync(
            ActionActor actor, Guid id, CaseWorkSelector work, CancellationToken cancellationToken)
        {
            CurrentReads.Add(work);
            return Task.FromResult(work == CaseWorkSelector.Primary ? Inspection : Audit);
        }

        public Task<CaseReportGenerationRecord> GetForDeliveryAsync(
            SendCaseReportRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseReportGenerationRecord?> GetAsync(
            ActionActor actor, Guid id, Guid generationId, CancellationToken cancellationToken) =>
            Task.FromResult(
                generationId == Inspection?.Id ? Inspection : generationId == Audit?.Id ? Audit : null);

        public Task<IReadOnlyList<CaseReportGenerationRecord>> ListAsync(
            ActionActor actor, Guid id, CaseWorkSelector work, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CaseReportGenerationRecord>>(
                (work == CaseWorkSelector.Primary ? Inspection : Audit) is { } current ? [current] : []);

        public Task<CaseReportFreezeResult> FreezeAsync(
            FreezeCaseReportGenerationRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseReportGenerationRecord> ConfirmArtifactAsync(
            ConfirmCaseReportArtifactRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseReportGenerationRecord> RecordArtifactOutcomeAsync(
            RecordCaseReportArtifactOutcomeRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<int> MarkStaleAsync(Guid id, string reasonCode, CancellationToken cancellationToken) =>
            Task.FromResult(0);

        public Task RecordDraftPreviewedAsync(
            RecordCaseReportDraftPreviewedRequest request, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        /// <summary>One confirmed, stored generation of <paramref name="kind"/>'s work, referenced <paramref name="reference"/>.</summary>
        public static CaseReportGenerationRecord Generation(Guid caseId, string reference, CaseWorkKind kind)
        {
            var input = AssessmentReportDraftWebTests.ReadyInput(caseId);
            var report = AssessmentReportProjection.Project(input).Snapshot!;
            var generationId = Guid.NewGuid();
            var snapshot = new CaseReportGenerationSnapshot(
                caseId, 0, reference, "operation-1", CaseReportActor.None, InspectionSentAtUtc,
                Guid.NewGuid(), new string('a', 64), "image/png", input.CurrentEstimate!.SpecificationId, input.CurrentEstimate.Version,
                report.Costs, report.EngineerValue, Guid.NewGuid(),
                report.Content, report.Guides, report.ReportDate, false,
                report.AgreedFee, report.FeeDescriptionLines, [], [],
                AssessmentReportContract.TemplateVersion, "fake", report)
            {
                CurrentEstimate = input.CurrentEstimate
            };
            return new(
                generationId, caseId, 0, 1, new string('b', 64), snapshot,
                AssessmentReportContract.TemplateVersion, "fake",
                CaseReportGenerationState.Confirmed, InspectionSentAtUtc, null,
                [
                    new(
                        Guid.NewGuid(), generationId, CaseReportArtifactKind.AssessmentReport,
                        CaseReportArtifactStatus.Confirmed, "operation-1",
                        Guid.NewGuid(), Guid.NewGuid(), new string('c', 64), 3,
                        reference + "_assessment.pdf", "application/pdf",
                        null, null, null, null),
                ])
            {
                WorkId = kind == CaseWorkKind.Primary ? caseId : Guid.NewGuid(),
                WorkKind = kind
            };
        }
    }

    /// <summary>The Create audit use case, recorded rather than run.</summary>
    private sealed class RecordingCreateAudit : ICreateAudit
    {
        public List<CreateAuditRequest> Requests { get; } = [];

        public AuditRefusal? Refusal { get; set; }

        public Task<CreateAuditResult> ExecuteAsync(CreateAuditRequest request, CancellationToken cancellationToken)
        {
            if (Refusal is { } refusal)
            {
                throw new AuditCreationException(request.CaseId, refusal);
            }
            Requests.Add(request);
            return Task.FromResult(new CreateAuditResult(
                new CaseIdentity(request.CaseId, "QDOS", 2031, 42, Reference),
                Guid.NewGuid(),
                AuditReference,
                false));
        }
    }

    /// <summary>Generate report on the Inspection work: stores one confirmed generation as the Inspection's current one.</summary>
    private sealed class InspectionReportGenerator(ReportsPerWork reports, Guid caseId) : IGenerateCaseReport
    {
        public List<GenerateCaseReportRequest> Requests { get; } = [];

        public Task<CaseReportGenerationResult> ExecuteAsync(
            GenerateCaseReportRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            reports.Inspection = ReportsPerWork.Generation(caseId, Reference, CaseWorkKind.Primary);
            return Task.FromResult(new CaseReportGenerationResult(
                CaseReportGenerationOutcome.Generated, reports.Inspection, []));
        }
    }

    /// <summary>
    /// The report use cases, recorded rather than run: each refuses after
    /// recording, the way a use case states its own refusal, so the page
    /// answers with its redirect.
    /// </summary>
    private sealed class RecordingReportCommands
        : IGenerateCaseReport, ISendCaseReport
    {
        public List<object> Calls { get; } = [];

        public Task<CaseReportGenerationResult> ExecuteAsync(
            GenerateCaseReportRequest request, CancellationToken cancellationToken)
        {
            Calls.Add(request);
            throw new InvalidOperationException("Recorded.");
        }

        public Task<StaffMailOperation> ExecuteAsync(
            SendCaseReportRequest request, CancellationToken cancellationToken)
        {
            Calls.Add(request);
            throw new InvalidOperationException("Recorded.");
        }
    }
}
