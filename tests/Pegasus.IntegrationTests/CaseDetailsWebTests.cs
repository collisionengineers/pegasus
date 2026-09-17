using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Actors;
using Pegasus.Core.Address;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Tasks;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class CaseDetailsWebTests
{
    /// <summary>
    /// D30: the Engineer's work is Case sections, so the record carries no
    /// Open Assessment action and no assessment gate — neither enabled nor
    /// drawn disabled — whatever the shared access decision says.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheRecordOffersNoAssessmentAction(bool canOpen)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseVehicleSection>(services, store);
                Substitute<IGetCaseValuationSection>(services, store);
                Substitute<IGetCaseNotesSection>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<IGetAssessmentWorkspace>(services, store);
                services.RemoveAll<IGetAssessmentAccess>();
                services.AddSingleton<IGetAssessmentAccess>(new FakeGetAssessmentAccess(canOpen));
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");

        Assert.DoesNotContain("Open Assessment", html, StringComparison.Ordinal);
        Assert.DoesNotContain(
            $"/Cases/{store.CaseId:D}/Assessment",
            html,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// EPIC-011 §1.8 and FRD-07: the EVA handoff is a Review act. Outside
    /// Review the workspace offers no EVA control and draws no handoff, rather
    /// than drawing a disabled one.
    /// </summary>
    [Fact]
    public async Task TheRecordRendersTenOrderedSectionHostsAndJumpLinks()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseVehicleSection>(services, store);
                Substitute<IGetCaseValuationSection>(services, store);
                Substitute<IGetCaseNotesSection>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<IGetAssessmentWorkspace>(services, store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");

        Assert.Equal(CaseSectionKeys, HostOrder(html));
        Assert.Equal(CaseSectionKeys, JumpLinkOrder(html));

        // The four sections that have a body below the fold are served as
        // fragments; every other host, including the Engineer shells,
        // renders with the page.
        Assert.Equal(
            ["vehicle", "valuation", "files", "notes"],
            DeferredSections(html));
    }

    /// <summary>
    /// <c>?section=</c> is a jump target, not an alternative: the addressed
    /// section is rendered by the first response, so the link works with no
    /// script, and it is the entry the jump-nav marks current. A key the record
    /// does not own — including the deleted pre-redesign keys, which are not
    /// aliased — selects Overview rather than nothing.
    /// </summary>
    [Theory]
    [InlineData("", "overview")]
    [InlineData("?section=overview", "overview")]
    [InlineData("?section=engineer-notes", "overview")]
    [InlineData("?section=vehicle", "vehicle")]
    [InlineData("?section=estimate", "estimate")]
    [InlineData("?section=files", "files")]
    [InlineData("?section=notes", "notes")]
    [InlineData("?section=valuations", "overview")]
    [InlineData("?section=inspection-address", "overview")]
    [InlineData("?section=case-files", "overview")]
    [InlineData("?section=evidence", "overview")]
    [InlineData("?tab=files", "overview")]
    public async Task TheAddressedSectionIsRenderedAndMarkedCurrent(
        string query,
        string currentSection)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseVehicleSection>(services, store);
                Substitute<IGetCaseValuationSection>(services, store);
                Substitute<IGetCaseNotesSection>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<IGetAssessmentWorkspace>(services, store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}{query}");

        Assert.Equal(currentSection, CurrentSectionKey(html));
        Assert.DoesNotContain(
            $"data-lazy=\"{currentSection}\"",
            html,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("files")]
    [InlineData("notes")]
    [InlineData("vehicle")]
    [InlineData("valuation")]
    public async Task TheSectionFragmentReturnsOnlyThatSectionBody(string key)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseVehicleSection>(services, store);
                Substitute<IGetCaseValuationSection>(services, store);
                Substitute<IGetCaseNotesSection>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var fragment = await GetHtmlAsync(
            client,
            $"/Cases/{store.CaseId:D}/Section?section={key}");

        Assert.DoesNotContain("case-sticky", fragment, StringComparison.Ordinal);
        Assert.DoesNotContain("section-nav", fragment, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"case-main\"", fragment, StringComparison.Ordinal);
        Assert.DoesNotContain("<html", fragment, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A section the frame renders itself, and a key the record does not own,
    /// are not fragments at all — the deleted keys are refused rather than
    /// aliased.
    /// </summary>
    [Theory]
    [InlineData("overview")]
    [InlineData("inspection")]
    [InlineData("case-files")]
    [InlineData("engineer-notes")]
    [InlineData("nonsense")]
    public async Task TheSectionFragmentRefusesKeysItDoesNotServe(string key)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseVehicleSection>(services, store);
                Substitute<IGetCaseValuationSection>(services, store);
                Substitute<IGetCaseNotesSection>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<IGetAssessmentWorkspace>(services, store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.GetAsync(
            new Uri($"/Cases/{store.CaseId:D}/Section?section={key}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// The record has exactly one editor. Every section that contributes fields
    /// to its Save form renders at once while the lease is held; Files has no
    /// such fields and mounts separately. A second form posting the whole
    /// record would write stored values over another section's unsaved input,
    /// so Inspection contributes to the one record form instead. Editing one
    /// section and saving therefore cannot discard an unsaved edit in another.
    /// </summary>
    [Fact]
    public async Task TheRecordRendersOneEditorForEverySection()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var html = await workspace.GetWorkspaceAsync();

        Assert.Equal(1, Occurrences(html, $"/Cases/{store.CaseId:D}?handler=Save"));
        Assert.Equal(1, Occurrences(html, "id=\"case-edit-form\""));
        Assert.Equal(1, Occurrences(html, "data-edit-save"));
        // Each editable value SaveCase writes appears once across the
        // record, so no control is shadowed by a stale copy of itself.
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
            "inspectionAddress",
            "inspectionMode",
            "storageLocation"
        })
        {
            Assert.Equal(1, Occurrences(html, $"name=\"{field}\""));
        }
        Assert.Contains(
            "<select id=\"edit-mileage-unit\" class=\"fi\" name=\"vehicleMileageUnit\" form=\"case-edit-form\">",
            html,
            StringComparison.Ordinal);
        // The Inspection section's control is the record form's entry for the
        // address, wherever it renders on the page.
        Assert.Contains(
            "id=\"inspection-address\" class=\"fi\" name=\"inspectionAddress\" form=\"case-edit-form\"",
            html,
            StringComparison.Ordinal);
        // WP6: the vehicle's own identity is edited in the Vehicle section,
        // between that section's host and the next one, and still posts
        // through the record's one form.
        Assert.Contains(
            "id=\"edit-registration\" class=\"fi mono\" name=\"vehicleRegistration\" form=\"case-edit-form\"",
            html,
            StringComparison.Ordinal);
        foreach (var control in new[] { "edit-registration", "edit-make", "edit-model" })
        {
            Assert.InRange(
                html.IndexOf($"id=\"{control}\"", StringComparison.Ordinal),
                html.IndexOf("id=\"section-vehicle\"", StringComparison.Ordinal),
                html.IndexOf("id=\"section-damage\"", StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// WP6 (issue 1): the record's frame is three sticky rows — the identity
    /// ribbon carrying the record's own two controls, one action row, and the
    /// section nav. Nothing sits above them to scroll away, and the edit state
    /// is a badge beside Cancel and Save rather than a fourth row.
    /// </summary>
    [Fact]
    public async Task TheCaseFrameIsThreeStickyRowsWithNoPageHeaderOrEditBar()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var html = await workspace.GetWorkspaceAsync();

        Assert.DoesNotContain("page-header", html, StringComparison.Ordinal);
        Assert.DoesNotContain("edit-bar", html, StringComparison.Ordinal);
        // v26: one measured sticky block — the 56px ribbon and the 40px
        // section row — and nothing else travels with the record.
        Assert.Equal(1, Occurrences(html, "data-sticky-block"));
        Assert.Equal(1, Occurrences(html, "class=\"ribbon\""));
        Assert.Equal(1, Occurrences(html, "class=\"section-row\""));

        // The identity row: the eyebrow with the registration, the reference
        // as the page's one heading, Claimant, Principal, Engineer, the
        // state chip and the record's two controls. Back to Cases and the
        // presence strip are gone (v25 decisions 1 and C).
        Assert.Contains(
            "<div class=\"ribbon-label\">Case workspace · AB12CDE</div>",
            html,
            StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(html, "<h1 class=\"ribbon-value\">"));
        Assert.DoesNotContain("Back to Cases", html, StringComparison.Ordinal);
        Assert.DoesNotContain("presence-strip", html, StringComparison.Ordinal);
        var refresh = RefreshForm(html);
        Assert.Contains("name=\"section\"", refresh, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Refresh\"", refresh, StringComparison.Ordinal);

        // The ribbon's actions: Cancel and Save beside the Editing badge and
        // the one Actions menu; no reason dialog stands between Save and the
        // record (v25 decision A).
        var actions = StickyActionRow(html);
        Assert.Contains("form=\"case-finish-editing-form\"", actions, StringComparison.Ordinal);
        Assert.Contains("data-case-cancel-form", actions, StringComparison.Ordinal);
        Assert.Contains("form=\"case-edit-form\"", actions, StringComparison.Ordinal);
        Assert.DoesNotContain("data-case-save-reason", html, StringComparison.Ordinal);
        Assert.DoesNotContain("case-save-reason-dialog", html, StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(actions, ">Cancel</span>"));
        Assert.Equal(1, Occurrences(actions, ">Save</span>"));
        Assert.Contains("case-edit-badge", actions, StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(actions, "data-case-actions"));
        Assert.DoesNotContain("You are editing this case", html, StringComparison.Ordinal);

        // The second row is the section nav, with its Scroll/Tabs switch.
        Assert.Equal(1, Occurrences(html, "data-section-nav"));
        Assert.Equal(1, Occurrences(html, "data-case-layout-switch"));
    }

    /// <summary>
    /// WP6 (issue 2): the Inspection panel prints each fact once. The chosen
    /// source and the mode restated the address itself on an image-based
    /// Case, which read as the same value four times; the Principal's default
    /// is named only where the Case holds something else.
    /// </summary>
    private static string RefreshForm(string html)
    {
        var start = html.IndexOf("<form method=\"get\" data-refresh-form", StringComparison.Ordinal);
        Assert.True(start >= 0, "The record must offer Refresh.");
        var end = html.IndexOf("</form>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The Refresh form must close.");
        return html[start..end];
    }

    /// <summary>
    /// Files remains deferred while editing because it contributes no fields to
    /// the record's one Save form. The editable record sections still render
    /// together, so mounting Files cannot replace entered values.
    /// </summary>
    [Fact]
    public async Task FocusedVehicleAndValuationReadsReuseOneDirectWorkspaceAndMatchLazyAssessmentProvenance()
    {
        var store = new RecordingCaseDetailsStore { ThrowOnBroadCaseRead = true };
        var assessment = new CaseAssessmentProjection(
            store.CaseId,
            "QDOS3100042",
            store.CaseVersion,
            store.State,
            null,
            [new(
                AssessmentVocabulary.VehicleFuel,
                "diesel",
                ActorKind.Automation,
                "vehicle-lookup",
                new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero),
                "vehicle-lookup",
                new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero))],
            [],
            new("AB12CDE", null, null, null, null, null, "tbc", null, null, null, null));
        store.FocusedAssessment = assessment;
        var assessmentWorkspace = new CountingAssessmentWorkspace(
            AssessmentWorkspaceTestData.Create(assessment));
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseVehicleSection>(services, store);
                Substitute<IGetCaseValuationSection>(services, store);
                Substitute<IGetCaseNotesSection>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<IGetAssessmentAccess>(services, new FakeGetAssessmentAccess(canOpen: false));
                Substitute<IGetAssessmentWorkspace>(services, assessmentWorkspace);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var initial = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=vehicle");
        Assert.Equal(1, assessmentWorkspace.ReadCount);
        Assert.Single(store.VehicleSectionQueries);

        using (var claim = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(initial),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", InputValue(initial, "operationKey")))))
        {
            AssertPrg(claim, store.CaseId);
        }

        assessmentWorkspace.Reset();
        store.VehicleSectionQueries.Clear();
        store.VehicleSectionAssessments.Clear();
        var directVehicle = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=vehicle");
        var directVehicleQuery = Assert.Single(store.VehicleSectionQueries);
        Assert.Equal(1, assessmentWorkspace.ReadCount);
        Assert.True(directVehicleQuery.HasAssessmentWorkspace);
        Assert.Same(assessmentWorkspace.Workspace, directVehicleQuery.AssessmentWorkspace);
        Assert.NotNull(directVehicleQuery.Frame);
        Assert.Equal(store.CaseId, directVehicleQuery.Frame!.Summary.CaseId);
        Assert.Same(assessment, Assert.Single(store.VehicleSectionAssessments));
        Assert.Contains("diesel", directVehicle, StringComparison.Ordinal);
        Assert.Contains("src-tag--lookup", directVehicle, StringComparison.Ordinal);

        var lazyVehicle = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}/Section?section=vehicle");
        var lazyVehicleQuery = store.VehicleSectionQueries.Last();
        Assert.Equal(1, assessmentWorkspace.ReadCount);
        Assert.False(lazyVehicleQuery.HasAssessmentWorkspace);
        Assert.Null(lazyVehicleQuery.AssessmentWorkspace);
        Assert.Null(lazyVehicleQuery.Frame);
        Assert.Same(assessment, store.VehicleSectionAssessments.Last());
        Assert.Contains("diesel", lazyVehicle, StringComparison.Ordinal);
        Assert.Contains("src-tag--lookup", lazyVehicle, StringComparison.Ordinal);

        assessmentWorkspace.Reset();
        store.ValuationSectionQueries.Clear();
        store.ValuationSectionAssessments.Clear();
        await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=valuation");
        var directValuationQuery = Assert.Single(store.ValuationSectionQueries);
        Assert.Equal(1, assessmentWorkspace.ReadCount);
        Assert.True(directValuationQuery.HasAssessmentWorkspace);
        Assert.Same(assessmentWorkspace.Workspace, directValuationQuery.AssessmentWorkspace);
        Assert.NotNull(directValuationQuery.Frame);
        Assert.Equal(store.CaseId, directValuationQuery.Frame!.Workflow.CaseId);
        Assert.Same(assessment, Assert.Single(store.ValuationSectionAssessments));

        await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}/Section?section=valuation");
        var lazyValuationQuery = store.ValuationSectionQueries.Last();
        Assert.Equal(1, assessmentWorkspace.ReadCount);
        Assert.False(lazyValuationQuery.HasAssessmentWorkspace);
        Assert.Null(lazyValuationQuery.AssessmentWorkspace);
        Assert.Null(lazyValuationQuery.Frame);
        Assert.Same(assessment, store.ValuationSectionAssessments.Last());
    }

    /// <summary>
    /// WP7 moved report composition off the Files section entirely — the
    /// Report section is now the only place a report's image set is chosen —
    /// and moved image preparation's gate from assessment access to
    /// <c>CanEditCaseData</c> (lease held, state not PostReportComplete or
    /// Query, not archived). So a visit with assessment access denied but the
    /// Case lease held still sees the Images tab's tiles with their crop
    /// controls: assessment access no longer has a say in this surface.
    /// </summary>
    private static FormUrlEncodedContent RepeatableForm(
        string antiforgeryToken,
        params (string Name, string Value)[] values) =>
        new(values
            .Select(item => KeyValuePair.Create(item.Name, item.Value))
            .Append(KeyValuePair.Create("__RequestVerificationToken", antiforgeryToken)));

    private static string[] JumpLinkOrder(string html) =>
        [.. JumpLinkRegex().Matches(JumpNav(html)).Select(match => match.Groups[1].Value)];

    /// <summary>
    /// The key of the jump-nav entry marked current. Scoped to the jump-nav so
    /// the shell rail's own current link cannot answer for it.
    /// </summary>
    private static string CurrentSectionKey(string html)
    {
        var current = CurrentSectionRegex().Match(JumpNav(html));
        Assert.True(current.Success, "No section is marked current.");
        return current.Groups[1].Value;
    }

    private static string JumpNav(string html)
    {
        var marker = html.IndexOf("data-section-nav", StringComparison.Ordinal);
        Assert.True(marker >= 0, "The section jump-nav is not rendered.");
        var start = html.LastIndexOf("<nav", marker, StringComparison.Ordinal);
        Assert.True(start >= 0, "The section jump-nav is not a nav element.");
        var end = html.IndexOf("</nav>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The jump-nav is not closed.");
        return html[start..end];
    }

    private sealed class CountingAssessmentWorkspace(AssessmentWorkspace workspace) : IGetAssessmentWorkspace
    {
        public AssessmentWorkspace Workspace { get; } = workspace;
        public int ReadCount { get; private set; }

        public Task<AssessmentWorkspace?> ExecuteAsync(
            GetAssessmentWorkspaceQuery query,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult<AssessmentWorkspace?>(Workspace);
        }

        public void Reset() => ReadCount = 0;
    }



    [Fact]
    public async Task CaseHistoryShowsResolvedActorNamesAndNeverARawSubjectId()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var staffSubjectId = Guid.NewGuid().ToString("D");
        var automationSubjectId = Guid.NewGuid().ToString("D");
        var store = new RecordingCaseDetailsStore
        {
            HistoryEntries =
            [
                new(
                    "case_returned_to_review",
                    staffSubjectId,
                    nameof(ActorKind.Staff),
                    new(2031, 5, 6, 9, 0, 0, TimeSpan.Zero),
                    "Missing instructions.",
                    3,
                    4)
                {
                    ActorDisplayName = "alex"
                },
                new(
                    "case_created",
                    automationSubjectId,
                    nameof(ActorKind.Automation),
                    new(2031, 5, 5, 9, 0, 0, TimeSpan.Zero),
                    "Automated intake.",
                    0,
                    1)
                {
                    ActorDisplayName = "Automation"
                }
            ]
        };
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseVehicleSection>(services, store);
                Substitute<IGetCaseValuationSection>(services, store);
                Substitute<IGetCaseNotesSection>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<IGetAssessmentWorkspace>(services, store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=notes");

        Assert.Contains("alex", html, StringComparison.Ordinal);
        Assert.Contains("Automation", html, StringComparison.Ordinal);
        Assert.DoesNotContain(staffSubjectId, html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(automationSubjectId, html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(CaseType.Audit, false, true)]
    [InlineData(CaseType.Audit, true, false)]
    [InlineData(CaseType.Inspection, false, false)]
    public async Task OriginalReportRequirementOnlyRendersForAnAuditWithoutEvidence(
        CaseType caseType,
        bool hasIntakeEvidence,
        bool requirementExpected)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore
        {
            SummaryCaseType = caseType,
            StandaloneAuditEvidenceId = hasIntakeEvidence ? Guid.NewGuid() : null
        };
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseVehicleSection>(services, store);
                Substitute<IGetCaseValuationSection>(services, store);
                Substitute<IGetCaseNotesSection>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<IGetAssessmentWorkspace>(services, store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");

        Assert.Equal(
            requirementExpected,
            html.Contains("Original report missing", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LinkedAuditDoesNotRenderTheStandaloneOriginalReportRequirementOrAction()
    {
        var store = new RecordingCaseDetailsStore
        {
            SummaryCaseType = CaseType.Audit,
            AuditOfCaseId = Guid.NewGuid()
        };
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var fullPage = await workspace.GetWorkspaceAsync();
        var files = await GetHtmlAsync(
            workspace.Client,
            $"/Cases/{store.CaseId:D}?section=files");

        Assert.DoesNotContain("Original report missing", fullPage, StringComparison.Ordinal);
        Assert.DoesNotContain(OperatorLabels.MarkAsOriginalReport, files, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemovedOriginalReportRestoresTheRequirementAndReplacementAction()
    {
        var report = Document(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "removed-original-report.pdf",
            "application/pdf",
            DocumentSemanticRole.AuditReport);
        var replacement = Document(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "replacement-original-report.pdf",
            "application/pdf");
        var store = new RecordingCaseDetailsStore
        {
            SummaryCaseType = CaseType.Audit,
            CaseDocuments =
            [
                report with
                {
                    Versions = report.Versions
                        .Select(version => version with
                        {
                            IsCurrent = false,
                            IsLogicallyRemoved = true,
                            RemovalReason = "Removed"
                        })
                        .ToArray()
                },
                replacement
            ]
        };
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var fullPage = await workspace.GetWorkspaceAsync();
        var files = await GetHtmlAsync(
            workspace.Client,
            $"/Cases/{store.CaseId:D}?section=files");

        Assert.Contains("Original report missing", fullPage, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.MarkAsOriginalReport, files, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MarkingAFiledDocumentAsTheOriginalReportClearsTheRequirement()
    {
        var occurrenceId = Guid.NewGuid();
        var store = new RecordingCaseDetailsStore
        {
            SummaryCaseType = CaseType.Audit,
            CaseDocuments =
            [
                Document(
                    occurrenceId,
                    Guid.NewGuid(),
                    "original-report.pdf",
                    "application/pdf")
            ]
        };
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<IMarkAsOriginalReportStore>(services, store));
        var before = await workspace.GetWorkspaceAsync();
        var filesBefore = await GetHtmlAsync(
            workspace.Client,
            $"/Cases/{store.CaseId:D}?section=files");
        Assert.Contains("Original report missing", before, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.MarkAsOriginalReport, filesBefore, StringComparison.Ordinal);

        using var response = await workspace.PostAsync(
            "Custody?handler=MarkAsOriginalReport",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", "mark-original-report"),
                ("editLeaseToken", store.LeaseToken),
                ("occurrenceId", occurrenceId.ToString("D"))));

        AssertPrg(response, store.CaseId);
        var command = Assert.Single(store.OriginalReportMarks);
        Assert.Equal(occurrenceId, command.DocumentOccurrenceId);
        AssertClaimant(workspace, command.Actor);
        var after = await workspace.GetWorkspaceAsync();
        Assert.Contains("The original report was recorded.", after, StringComparison.Ordinal);
        Assert.DoesNotContain("Original report missing", after, StringComparison.Ordinal);
        var filesAfter = await GetHtmlAsync(
            workspace.Client,
            $"/Cases/{store.CaseId:D}?section=files");
        Assert.DoesNotContain(OperatorLabels.MarkAsOriginalReport, filesAfter, StringComparison.Ordinal);
    }
}
