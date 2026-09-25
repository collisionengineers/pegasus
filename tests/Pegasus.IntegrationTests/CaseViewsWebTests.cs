using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
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
/// view by default, the read-only Inspection view with the approved label on
/// every editable head, the Report of each view, the Audit folder chip in
/// Files, and Create audit's dialog and in-place post.
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
        Assert.DoesNotContain(Frame.ReadOnlyAuditCreated, html, StringComparison.Ordinal);
        Assert.Contains("data-section-edit=", html, StringComparison.Ordinal);
        Assert.Contains("data-case-edit-form", RecordBar(html), StringComparison.Ordinal);
        Assert.DoesNotContain("data-inspection-report", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStandaloneAuditHasOneViewAndKeepsItsOriginalReport()
    {
        var store = new RecordingCaseDetailsStore { SummaryCaseType = CaseType.Audit };
        using var host = new ReadingHost(store);

        var html = await host.ReadAsync($"/Cases/{store.CaseId:D}?view=inspection");

        Assert.DoesNotContain("data-case-views", html, StringComparison.Ordinal);
        Assert.Contains("id=\"section-original-report\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain(Frame.ReadOnlyAuditCreated, html, StringComparison.Ordinal);
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
        // Razor keeps a data- attribute whose value is null, empty: no view is named.
        Assert.DoesNotMatch("data-case-view=\"[^\"]", html);
        Assert.DoesNotContain(Frame.ReadOnlyAuditCreated, html, StringComparison.Ordinal);
        Assert.Contains("data-section-edit=", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Inspection view reads the primary work and never edits: no Edit in
    /// the ribbon or any head, the approved label on every editable head but
    /// Files and Notes, and its section links, Refresh and mounted bodies
    /// stay in the view.
    /// </summary>
    [Fact]
    public async Task TheInspectionViewReadsThePrimaryWorkAndOffersNoEdit()
    {
        var store = AuditedCase();
        using var host = new ReadingHost(store);

        var html = await host.ReadAsync($"/Cases/{store.CaseId:D}?view=inspection");

        Assert.Equal(CaseWorkSelector.Primary, Assert.Single(store.PageFrameQueries).Work);
        Assert.Contains("data-case-view=\"inspection\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-section-edit=", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-case-edit-form", html, StringComparison.Ordinal);
        foreach (var key in new[] { "overview", "claim", "inspection", "estimate", "settlement", "report" })
        {
            Assert.Matches(
                $"data-section-availability=\"{key}\">\\s*<svg[^>]*>.*?</svg>\\s*<span>{Regex.Escape(Frame.ReadOnlyAuditCreated)}</span>",
                html);
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
        Assert.Contains(Frame.ReadOnlyAuditCreated, vehicle, StringComparison.Ordinal);
        var audited = await host.ReadAsync($"/Cases/{store.CaseId:D}/Section?section=vehicle");
        Assert.Equal(CaseWorkSelector.Current, store.VehicleSectionQueries[^1].Work);
        Assert.DoesNotContain(Frame.ReadOnlyAuditCreated, audited, StringComparison.Ordinal);
    }

    /// <summary>
    /// Decisions answer 3: the lease is Case-wide, so its holder at
    /// <c>?view=inspection</c> keeps the ribbon's editing controls while every
    /// section reads; the Audit view is where the sections edit.
    /// </summary>
    [Fact]
    public async Task TheLeaseHolderKeepsTheRibbonControlsWhileTheInspectionViewReads()
    {
        var store = AuditedCase();
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var html = WebUtility.HtmlDecode(await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?view=inspection"));

        var bar = RecordBar(html);
        Assert.Contains($">{Frame.Editing}</span>", bar, StringComparison.Ordinal);
        Assert.Contains("data-case-cancel", bar, StringComparison.Ordinal);
        Assert.Contains("data-case-actions", bar, StringComparison.Ordinal);
        Assert.DoesNotContain("data-case-edit-form", bar, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"case-edit-form\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("form=\"case-edit-form\"", MainColumn(html), StringComparison.Ordinal);
        Assert.Contains($"<span>{Frame.ReadOnlyAuditCreated}</span>", html, StringComparison.Ordinal);

        var audit = await workspace.GetWorkspaceAsync();
        Assert.Contains("id=\"case-edit-form\"", audit, StringComparison.Ordinal);
    }

    /// <summary>
    /// One report per work (v29 P4): the Audit view's card carries a.{Case/PO}
    /// under the Inspection's sent report line and its link to the Inspection
    /// view; the Inspection view shows only its own sent report, with nothing
    /// to generate or deliver.
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
        var line = InspectionReportLine(auditReport);
        Assert.Contains($"<span>{sentLine}</span>", line, StringComparison.Ordinal);
        Assert.Contains(
            $"href=\"/Cases/{store.CaseId:D}?section=report&view=inspection#section-report\">{Frame.InspectionViewLink}</a>",
            line,
            StringComparison.Ordinal);
        Assert.Contains($"{AuditReference} · </span>", ReportStatus(auditReport), StringComparison.Ordinal);
        Assert.Contains("data-prepare-delivery", auditReport, StringComparison.Ordinal);
        Assert.Contains(reports.Audit.Id.ToString("D"), auditReport, StringComparison.Ordinal);

        var inspectionHtml = WebUtility.HtmlDecode(await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?view=inspection"));
        var inspectionReport = Section(inspectionHtml, "section-report-title");
        Assert.Contains($"<span>{sentLine}</span>", ReportStatus(inspectionReport), StringComparison.Ordinal);
        Assert.Contains(reports.Inspection.Id.ToString("D"), inspectionReport, StringComparison.Ordinal);
        Assert.DoesNotContain(reports.Audit.Id.ToString("D"), inspectionReport, StringComparison.Ordinal);
        foreach (var hidden in new[]
        {
            "data-inspection-report", "data-report-reference", "data-report-not-ready", "data-report-gate",
            "data-generate-report", "data-prepare-delivery", "data-send-prepared", "data-report-menu"
        })
        {
            Assert.DoesNotContain(hidden, inspectionReport, StringComparison.Ordinal);
        }
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

        createAudit.Refusal = AuditRefusal.ReportNotSent;
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
        Assert.Contains("Create audit is available once the report is sent.", after, StringComparison.Ordinal);
        Assert.Single(createAudit.Requests);
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

    /// <summary>The Audit view's sent Inspection report line, which reads just before the Audit's card.</summary>
    private static string InspectionReportLine(string report)
    {
        var start = report.IndexOf("data-inspection-report", StringComparison.Ordinal);
        Assert.True(start >= 0, "The sent Inspection report line is not rendered.");
        var end = report.IndexOf("data-report-preview-card", start, StringComparison.Ordinal);
        Assert.True(end > start, "The sent Inspection report line does not precede the Audit's card.");
        return report[start..end];
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
                    Substitute<IGetCase>(services, store);
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
        public CaseReportGenerationRecord Audit { get; } = Generation(caseId, AuditReference, CaseWorkKind.Audit);

        public CaseReportGenerationRecord Inspection { get; } = Generation(caseId, Reference, CaseWorkKind.Primary);

        public List<CaseWorkSelector> CurrentReads { get; } = [];

        public Task<CaseReportGenerationRecord?> GetCurrentAsync(
            ActionActor actor, Guid id, CaseWorkSelector work, CancellationToken cancellationToken)
        {
            CurrentReads.Add(work);
            return Task.FromResult<CaseReportGenerationRecord?>(work == CaseWorkSelector.Primary ? Inspection : Audit);
        }

        public Task<CaseReportGenerationRecord?> GetAsync(
            ActionActor actor, Guid id, Guid generationId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseReportGenerationRecord?>(
                generationId == Inspection.Id ? Inspection : generationId == Audit.Id ? Audit : null);

        public Task<IReadOnlyList<CaseReportGenerationRecord>> ListAsync(
            ActionActor actor, Guid id, CaseWorkSelector work, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CaseReportGenerationRecord>>(
                [work == CaseWorkSelector.Primary ? Inspection : Audit]);

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

        /// <summary>One confirmed generation of <paramref name="kind"/>'s work, referenced <paramref name="reference"/>.</summary>
        private static CaseReportGenerationRecord Generation(Guid caseId, string reference, CaseWorkKind kind)
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
}
