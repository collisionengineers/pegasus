using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
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
    /// drawn disabled — whatever the shared access decision says. The page
    /// takes that decision from the Case's state; Held is one it cannot open.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheRecordOffersNoAssessmentAction(bool canOpen)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore
        {
            State = canOpen ? CaseLifecycleState.NotReady : CaseLifecycleState.Held
        };
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCaseEditBasis>(services, store);
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

        Assert.DoesNotContain("Open Assessment", html, StringComparison.Ordinal);
        Assert.DoesNotContain(
            $"/Cases/{store.CaseId:D}/Assessment",
            html,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// FRD-07: the EVA handoff is a Review act. Outside
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
                Substitute<IGetCaseEditBasis>(services, store);
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
        Assert.Equal(CaseSectionLinkKeys, JumpLinkOrder(html));

        // The four sections that have a body below the fold are served as
        // fragments; every other host, including the Engineer shells,
        // renders with the page.
        Assert.Equal(
            ["vehicle", "valuation", "files", "notes"],
            DeferredSections(html));
        Assert.Matches(
            "data-lazy=\"valuation\"\\s+data-section-parent=\"vehicle\"",
            html);
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
    [InlineData("?section=damage", "vehicle")]
    [InlineData("?section=valuation", "vehicle")]
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
                Substitute<IGetCaseEditBasis>(services, store);
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

    [Fact]
    public async Task HiddenOriginalReportSectionFallsBackToOverviewInTabs()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCaseEditBasis>(services, store);
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
        client.DefaultRequestHeaders.Add("Cookie", "pegasus-case-layout=tabs");

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=original-report");

        Assert.Contains("data-section-current=\"overview\"", html, StringComparison.Ordinal);
        Assert.Equal("overview", CurrentSectionKey(html));
        Assert.Contains("is-active", Section(html, "section-overview-title"), StringComparison.Ordinal);
        Assert.DoesNotContain("data-section-link=\"original-report\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"section-original-report\"", html, StringComparison.Ordinal);
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
                Substitute<IGetCaseEditBasis>(services, store);
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
        AssertBalancedMarkup(fragment);
    }

    /// <summary>
    /// Issue 816: a stray closing tag in one section let the browser close the
    /// main column early, so Decisions and every section after it drew in the
    /// aside. Read and edit mode each render the record with every section
    /// closed inside the main column.
    /// </summary>
    [Fact]
    public async Task EverySectionClosesInsideTheMainColumnInReadAndEditMode()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCaseEditBasis>(services, store);
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
        var reading = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");

        using var workspace = await EnterEditModeAsync(new RecordingCaseDetailsStore(), _ => { });
        var editing = await workspace.GetWorkspaceAsync();

        foreach (var html in new[] { reading, editing })
        {
            AssertBalancedMarkup(html);
            var main = MainColumn(html);
            AssertBalancedMarkup(main);
            Assert.Equal(CaseSectionKeys, HostOrder(main));
        }
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
                Substitute<IGetCaseEditBasis>(services, store);
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
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Script asks for a fragment and reads only its status, so a fragment's 404
    /// has an empty body, not the 18 KB status page and its shell reads. The
    /// same 404 on the full page still renders the designed page.
    /// </summary>
    [Theory]
    [InlineData("/Section?section=vehicle")]
    [InlineData("/Section?section=valuation")]
    [InlineData("/Section?section=notes")]
    [InlineData("/Section?section=files")]
    [InlineData("?handler=GlassSession")]
    public async Task AFragmentOfAnUnknownCaseAnswersWithAnEmptyBodyWhileTheFullPageKeepsTheStatusPage(
        string fragment)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCaseEditBasis>(services, store);
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
        var unknown = Guid.NewGuid();

        using var fragmentResponse = await client.GetAsync(
            new Uri($"/Cases/{unknown:D}{fragment}", UriKind.Relative));
        using var pageResponse = await client.GetAsync(
            new Uri($"/Cases/{unknown:D}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, fragmentResponse.StatusCode);
        Assert.Equal(string.Empty, await fragmentResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, pageResponse.StatusCode);
        Assert.Contains(
            "We could not find that page",
            await pageResponse.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
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
        // Each editable value the Case save writes appears once across the
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
            "inspectionAddress",
            "inspectionMode",
            "storageLocation"
        })
        {
            Assert.Equal(1, Occurrences(html, $"name=\"{field}\""));
        }
        Assert.Equal(0, Occurrences(html, "name=\"instructionDate\""));
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

    [Fact]
    public async Task TheClaimSourceContactShowsEachCaseOverrideAheadOfTheSnapshot()
    {
        var store = new RecordingCaseDetailsStore
        {
            ClaimSource = new(
                Guid.NewGuid(),
                4,
                "Acme Claims",
                "Directory Handler",
                "0113 000 0000",
                "directory@acme.example",
                "Case Handler",
                null,
                "case@acme.example")
        };

        var reading = WebUtility.HtmlDecode(OverviewPanel(await ReadCaseAsync(store)));

        // Three cells, the same in both modes: each the Case's override where
        // one is recorded, else the contact copied from the record.
        foreach (var (part, label, value) in new[]
        {
            ("name", "Claim source contact name", "Case Handler"),
            ("phone", "Claim source contact phone", "0113 000 0000"),
            ("email", "Claim source contact e-mail", "case@acme.example")
        })
        {
            Assert.Matches(
                $"<div class=\"fc ro\" data-claim-source-contact=\"{part}\">\\s*<span class=\"lbl\">{Regex.Escape(label)}</span>\\s*<div class=\"fv\">{Regex.Escape(value)}</div>",
                reading);
        }
        Assert.DoesNotContain("name=\"claimSourceContactName\"", reading, StringComparison.Ordinal);

        using var workspace = await EnterEditModeAsync(store, _ => { });
        var editing = WebUtility.HtmlDecode(OverviewPanel(await workspace.GetWorkspaceAsync()));
        Assert.Contains(
            "name=\"claimSourceContactTelephone\" form=\"case-edit-form\" maxlength=\"100\" value=\"0113 000 0000\"",
            editing,
            StringComparison.Ordinal);
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
        // The shared refresh button: the label the "Refreshing" rewrite
        // targets is the button's accessible name (issue 831).
        Assert.Contains("title=\"Refresh\"", refresh, StringComparison.Ordinal);
        Assert.Contains("data-refresh-label", refresh, StringComparison.Ordinal);

        // The ribbon's actions: the Case form's default Save now and Done
        // beside the Editing badge and the one Actions menu (save as you go,
        // 29 September 2026); no reason dialog stands between a save and the
        // record (v25 decision A).
        var actions = StickyActionRow(html);
        Assert.Contains("form=\"case-finish-editing-form\"", actions, StringComparison.Ordinal);
        Assert.Contains("data-case-done-form", actions, StringComparison.Ordinal);
        Assert.Contains("form=\"case-edit-form\"", actions, StringComparison.Ordinal);
        Assert.DoesNotContain("case-save-reason-dialog", html, StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(actions, ">Done</span>"));
        Assert.Equal(1, Occurrences(actions, ">Save now</span>"));
        Assert.DoesNotContain(">Cancel</span>", actions, StringComparison.Ordinal);
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
        // Held: a state the assessment cannot open.
        var store = new RecordingCaseDetailsStore { State = CaseLifecycleState.Held };
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
                new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero))],
            [],
            new("AB12CDE", null, null, null, null, null, "tbc", null, new DateOnly(2026, 8, 2), null, null, null, null, null));
        store.FocusedAssessment = assessment;
        var assessmentWorkspace = new CountingAssessmentWorkspace(
            AssessmentWorkspaceTestData.Create(assessment));
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCaseEditBasis>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseVehicleSection>(services, store);
                Substitute<IGetCaseValuationSection>(services, store);
                Substitute<IGetCaseNotesSection>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
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
                Substitute<IGetCaseEditBasis>(services, store);
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
                Substitute<IGetCaseEditBasis>(services, store);
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
        var versionId = Guid.NewGuid();
        var store = new RecordingCaseDetailsStore
        {
            SummaryCaseType = CaseType.Audit,
            CaseDocuments =
            [
                Document(
                    occurrenceId,
                    versionId,
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
                ("occurrenceId", occurrenceId.ToString("D")),
                ("versionId", versionId.ToString("D"))));

        AssertPrg(response, store.CaseId);
        var command = Assert.Single(store.OriginalReportMarks);
        Assert.Equal(occurrenceId, command.DocumentOccurrenceId);
        Assert.Equal(versionId, command.DocumentVersionId);
        AssertClaimant(workspace, command.Actor);
        var after = await workspace.GetWorkspaceAsync();
        Assert.Contains("The original report was recorded.", after, StringComparison.Ordinal);
        Assert.DoesNotContain("Original report missing", after, StringComparison.Ordinal);
        var filesAfter = await GetHtmlAsync(
            workspace.Client,
            $"/Cases/{store.CaseId:D}?section=files");
        Assert.DoesNotContain(OperatorLabels.MarkAsOriginalReport, filesAfter, StringComparison.Ordinal);
    }

    /// <summary>
    /// Roadmap Lane H (FRD-16): a save-as-you-go commit the page script posts is
    /// answered with the parts it swaps and not with the page. The answer holds the
    /// record wrapper that confirms the commit, the five swap roots, the Save form
    /// with the authority the next commit sends, and no section.
    /// </summary>
    [Fact]
    public async Task AFetchCommitAnswersWithTheCasePartsTheScriptSwapsAndNotThePage()
    {
        var store = new RecordingCaseDetailsStore
        {
            AcceptWorkspaceSaves = true,
            State = CaseLifecycleState.ReportPreparation
        };
        using var workspace = await EnterEngineerEditModeAsync(
            store, services => Substitute<ISaveCaseWorkspace>(services, store));
        var pageBefore = await workspace.GetWorkspaceAsync();
        var expectedVersion = store.CaseVersion;

        using var response = await PostFetchCommitAsync(workspace, ("claimantName", "Case claimant"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var answer = await response.Content.ReadAsStringAsync();
        AssertBalancedMarkup(answer);
        Assert.Single(store.Saves);
        Assert.Equal(expectedVersion + 1, store.CaseVersion);

        // The record wrapper: the version it moved to, the session it kept, the commit it confirms.
        Assert.Equal(1, Occurrences(answer, "data-case-record"));
        Assert.Contains($"data-case-version=\"{store.CaseVersion}\"", answer, StringComparison.Ordinal);
        Assert.Contains("data-case-editing=\"true\"", answer, StringComparison.Ordinal);
        AssertEditorCommit(answer, "case-edit-form", DetailsModelOperationKey, expectedVersion);

        // The five swap roots, each once, and the notice the commit itself made.
        foreach (var root in new[]
                 {
                     "data-case-notices", "data-case-ribbon-facts", "data-case-ribbon-actions",
                     "data-case-aside", "data-case-dialogs"
                 })
        {
            Assert.Equal(1, Occurrences(answer, root));
        }
        Assert.Contains("Case saved.", answer, StringComparison.Ordinal);

        // The authority the next commit sends: the Case's new version, the lease, a fresh key.
        Assert.Equal(1, Occurrences(answer, "id=\"case-edit-form\""));
        Assert.Equal(
            store.CaseVersion.ToString(CultureInfo.InvariantCulture),
            SaveFormValue(answer, "expectedVersion"));
        Assert.Equal(store.LeaseToken, SaveFormValue(answer, "editLeaseToken"));
        Assert.NotEqual(DetailsModelOperationKey, SaveFormValue(answer, "operationKey"));
        Assert.Equal(store.LeaseToken, InputValue(answer, "editLeaseToken"));

        // Every carry-forward input the page draws is drawn again, and no section is.
        var after = await workspace.GetWorkspaceAsync();
        Assert.Equal(CarryForwardNames(after).Order(), CarryForwardNames(answer).Order());
        Assert.Contains("name=\"selection.Opening\"", after, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"case-main\"", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("data-case-viewer-host", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("data-section-nav", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"section-", answer, StringComparison.Ordinal);
        Assert.True(answer.Length < pageBefore.Length, "The answer is smaller than the page it replaces.");
    }

    /// <summary>
    /// The notice the answer drew is not queued for the next page load, and the lease
    /// the browser holds is: the next load reads it and must carry the one the answer did.
    /// </summary>
    [Fact]
    public async Task AFetchCommitQueuesNoNoticeButKeepsTheLeaseForTheNextPage()
    {
        var store = new RecordingCaseDetailsStore
        {
            AcceptWorkspaceSaves = true,
            State = CaseLifecycleState.ReportPreparation
        };
        using var workspace = await EnterEngineerEditModeAsync(
            store, services => Substitute<ISaveCaseWorkspace>(services, store));

        using var response = await PostFetchCommitAsync(workspace, ("claimantName", "Case claimant"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var next = await workspace.GetWorkspaceAsync();
        Assert.DoesNotContain("Case saved.", next, StringComparison.Ordinal);
        Assert.DoesNotContain("data-confirmation", next, StringComparison.Ordinal);
        AssertNoEditorCommit(next);
        Assert.Contains("data-case-editing=\"true\"", next, StringComparison.Ordinal);
        Assert.Equal(store.LeaseToken, SaveFormValue(next, "editLeaseToken"));
        Assert.Equal(
            store.CaseVersion.ToString(CultureInfo.InvariantCulture),
            SaveFormValue(next, "expectedVersion"));
    }

    /// <summary>
    /// A commit without the script's header is a plain form post: it keeps its redirect,
    /// and the page it lands on carries the notice and the commit it confirms.
    /// </summary>
    [Fact]
    public async Task ACommitWithoutTheFetchHeaderStillRedirectsAndQueuesItsNotice()
    {
        var store = new RecordingCaseDetailsStore
        {
            AcceptWorkspaceSaves = true,
            State = CaseLifecycleState.ReportPreparation
        };
        using var workspace = await EnterEngineerEditModeAsync(
            store, services => Substitute<ISaveCaseWorkspace>(services, store));
        var expectedVersion = store.CaseVersion;

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(
                DetailsModelOperationKey, "Saved as it was made.", ("claimantName", "Case claimant")));

        AssertPrg(response, store.CaseId);
        var next = await workspace.GetWorkspaceAsync();
        Assert.Contains("Case saved.", next, StringComparison.Ordinal);
        AssertEditorCommit(next, "case-edit-form", DetailsModelOperationKey, expectedVersion);
    }

    /// <summary>
    /// A refused commit answers 200 in the shape the script recognises as refused: the
    /// record wrapper with no commit to confirm, and the refusal as the notice. Nothing
    /// is queued for the next page.
    /// </summary>
    [Fact]
    public async Task ARefusedFetchCommitAnswersTheRefusedShapeWithItsNotice()
    {
        var store = new RecordingCaseDetailsStore
        {
            AcceptWorkspaceSaves = true,
            State = CaseLifecycleState.ReportPreparation
        };
        using var workspace = await EnterEngineerEditModeAsync(
            store, services => Substitute<ISaveCaseWorkspace>(services, store));
        var version = store.CaseVersion;
        store.NextFailure = new InvalidOperationException("The case refused the command.");

        using var response = await PostFetchCommitAsync(workspace, ("claimantName", "Case claimant"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var answer = await response.Content.ReadAsStringAsync();
        AssertBalancedMarkup(answer);
        Assert.Empty(store.Saves);
        Assert.Equal(1, Occurrences(answer, "data-case-record"));
        AssertNoEditorCommit(answer);
        Assert.Contains($"data-case-version=\"{version}\"", answer, StringComparison.Ordinal);
        Assert.Contains("role=\"alert\"", answer, StringComparison.Ordinal);
        Assert.Contains("The case refused the command.", answer, StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(answer, "data-case-notices"));
        Assert.Equal(1, Occurrences(answer, "data-case-aside"));
        Assert.DoesNotContain("id=\"case-main\"", answer, StringComparison.Ordinal);

        var next = await workspace.GetWorkspaceAsync();
        Assert.DoesNotContain("The case refused the command.", next, StringComparison.Ordinal);
        Assert.Equal(store.LeaseToken, SaveFormValue(next, "editLeaseToken"));
    }

    /// <summary>
    /// A commit that recorded staged crops or rotations also answers with the Files
    /// section, which the script replaces so the tiles show what was saved. One that
    /// recorded nothing of the kind does not.
    /// </summary>
    [Fact]
    public async Task AFetchCommitWithPreparationEditsAlsoAnswersWithTheFilesSection()
    {
        var fixture = new PreparedImages();
        var store = fixture.Store(CaseLifecycleState.ReportPreparation, acceptWorkspaceSaves: true);
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            Substitute<ICaseAssetPreparationQueries>(services, store);
            Substitute<ISaveCaseWorkspace>(services, store);
        });

        using var plain = await PostFetchCommitAsync(workspace, ("claimantName", "Case claimant"));
        Assert.Equal(HttpStatusCode.OK, plain.StatusCode);
        Assert.DoesNotContain("id=\"section-files\"", await plain.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var response = await PostFetchCommitAsync(
            workspace,
            ("preparationEdits[0].occurrenceId", fixture.OverviewOccurrenceId.ToString("D")),
            ("preparationEdits[0].expectedPreparationVersion", "4"),
            ("preparationEdits[0].rotation", "180"),
            ("preparationEdits[0].cropLeft", "0.05"),
            ("preparationEdits[0].cropTop", "0.1"),
            ("preparationEdits[0].cropWidth", "0.5"),
            ("preparationEdits[0].cropHeight", "0.6"),
            ("preparationEdits[0].fullPage", "false"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var answer = await response.Content.ReadAsStringAsync();
        AssertBalancedMarkup(answer);
        Assert.Equal(2, store.Saves.Count);
        Assert.NotNull(store.Saves[1].ImagePreparation);
        AssertEditorCommit(answer, "case-edit-form", DetailsModelOperationKey, store.CaseVersion - 1);
        Assert.Equal(1, Occurrences(answer, "id=\"section-files\""));
        Assert.Contains(
            $"data-preparation-occurrence=\"{fixture.OverviewOccurrenceId:D}\"",
            answer,
            StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"case-main\"", answer, StringComparison.Ordinal);
    }

    /// <summary>
    /// A commit that landed but could not keep the session (its lease could not be claimed
    /// again) leaves nothing to update in place: it takes the redirect, and the page it
    /// lands on confirms the commit and says it ended editing, as the script expects.
    /// </summary>
    [Fact]
    public async Task AFetchCommitThatEndsTheSessionKeepsTheRedirect()
    {
        var store = new RecordingCaseDetailsStore
        {
            AcceptWorkspaceSaves = true,
            State = CaseLifecycleState.ReportPreparation
        };
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            Substitute<ISaveCaseWorkspace>(services, store);
            Substitute<IAcquireCaseEditLease>(services, new ClaimsOnce(store));
        });
        var expectedVersion = store.CaseVersion;

        using var response = await PostFetchCommitAsync(workspace, ("claimantName", "Case claimant"));

        AssertPrg(response, store.CaseId);
        var next = await workspace.GetWorkspaceAsync();
        Assert.Contains("data-case-editing=\"false\"", next, StringComparison.Ordinal);
        Assert.Contains("Case saved.", next, StringComparison.Ordinal);
        AssertEditorCommit(next, "case-edit-form", DetailsModelOperationKey, expectedVersion);
    }

    /// <summary>The store's first claim enters edit mode; a later one, the reclaim after a save, is refused.</summary>
    private sealed class ClaimsOnce(RecordingCaseDetailsStore store) : IAcquireCaseEditLease
    {
        private int claims;

        public Task<CaseEditLease> ExecuteAsync(
            ClaimCaseEditLeaseRequest request,
            CancellationToken cancellationToken) =>
            claims++ == 0
                ? ((IAcquireCaseEditLease)store).ExecuteAsync(request, cancellationToken)
                : throw new InvalidOperationException("The lease could not be claimed again.");
    }

    /// <summary>The Save posted the way case-workspace.js posts a commit: a fetch, expecting the page's HTML.</summary>
    private static async Task<HttpResponseMessage> PostFetchCommitAsync(
        LeasedWorkspace workspace,
        params (string Name, string Value)[] fields)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/Cases/{workspace.Store.CaseId:D}?handler=Save")
        {
            Content = workspace.MutationForm(DetailsModelOperationKey, "Saved as it was made.", fields)
        };
        request.Headers.Add("X-Requested-With", "fetch");
        request.Headers.Accept.ParseAdd("text/html");
        return await workspace.Client.SendAsync(request);
    }

    /// <summary>
    /// The record wrapper confirms no commit. Razor draws a data-* attribute whose value is null
    /// as <c>data-editor-commit=""</c>, and the script reads an empty one as no commit, so the
    /// attribute may be there; a value in it may not.
    /// </summary>
    private static void AssertNoEditorCommit(string html) =>
        Assert.DoesNotMatch("data-editor-commit=\"[^\"]+\"", html);

    /// <summary>The Save form's own input, as the script's carry-forward reads it.</summary>
    private static string SaveFormValue(string html, string name)
    {
        var start = html.IndexOf("id=\"case-edit-form\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The Save form must be drawn.");
        var end = html.IndexOf("</form>", start, StringComparison.Ordinal);
        return InputValue(html[start..end], name);
    }

    /// <summary>The names of every input the script carries forward from a response.</summary>
    private static string[] CarryForwardNames(string html) =>
        [.. Regex.Matches(html, "<input[^>]*name=\"(?<name>[^\"]+)\"[^>]*data-carry-forward[^>]*>")
            .Select(match => match.Groups["name"].Value)];
}
