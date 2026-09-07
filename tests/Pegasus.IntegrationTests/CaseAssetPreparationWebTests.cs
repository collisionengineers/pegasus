using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Intake;
using ReportImageLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportImages;

namespace Pegasus.IntegrationTests;

/// <summary>
/// B06 phase 2: report-image preparation on the one Case workspace. The Files
/// section states each image's role, order, rotation and crop and — in edit
/// mode — offers the script-off controls that change them; the Report section
/// states the same prepared set in the report's own order. Both read one
/// loaded set, so the tests assert the same values in both places.
/// </summary>
public sealed partial class CaseDetailsWebTests
{
    private const string CloseUpFileName = "front-nearside.jpg";
    private const string OverviewFileName = "vehicle-overview.jpg";
    private const string FirstSupportingFileName = "rear-offside.jpg";
    private const string SecondSupportingFileName = "interior.jpg";
    private const string UnusedFileName = "plate.jpg";

    /// <summary>
    /// Read-only view: every prepared value is stated, and nothing that could
    /// change one is rendered while this browser holds no edit lease.
    /// </summary>
    [Fact]
    public async Task TheFilesSectionStatesEachImagesPreparationAndOffersNoControlWithoutTheLease()
    {
        var fixture = new PreparedImages();
        var store = fixture.Store();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<ICaseAssetPreparationQueries>(services, store);
                Substitute<ICaseEvidenceImageQueries>(services, store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=files");
        var panel = Section(html, "report-images-title");
        var visible = WebUtility.HtmlDecode(VisibleText(panel));

        Assert.Contains(ReportImageLabels.SectionTitle, visible, StringComparison.Ordinal);
        foreach (var fileName in new[]
        {
            CloseUpFileName, OverviewFileName, FirstSupportingFileName, SecondSupportingFileName, UnusedFileName
        })
        {
            Assert.Contains(fileName, visible, StringComparison.Ordinal);
        }
        foreach (var role in Enum.GetValues<CaseAssetReportRole>())
        {
            Assert.Contains(ReportImageLabels.RoleLabel(role), visible, StringComparison.Ordinal);
        }
        Assert.Contains(ReportImageLabels.RotationLabel(CaseAssetRotation.Clockwise90), visible, StringComparison.Ordinal);
        Assert.Contains(ReportImageLabels.CropLabel(fixture.OverviewCrop), visible, StringComparison.Ordinal);
        Assert.Contains(ReportImageLabels.CropLabel(CaseAssetCrop.Full), visible, StringComparison.Ordinal);

        Assert.DoesNotContain("<form", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<button", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<select", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<input", panel, StringComparison.OrdinalIgnoreCase);
        // B08: the drag enhancement reorders through the same command the
        // controls post, so a card that offers no control is not draggable
        // and carries none of the shape the drag would submit.
        Assert.DoesNotContain("draggable", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-preparation-version", panel, StringComparison.Ordinal);
    }

    /// <summary>
    /// Edit mode renders the controls, and a save posts exactly what the
    /// editor chose — the claimant, the case and its version, the lease
    /// token, the form's operation key, and the one edit its card carries.
    /// </summary>
    [Fact]
    public async Task SavingAnImagesPreparationPostsTheBoundEditAndItsEnvelope()
    {
        var fixture = new PreparedImages();
        var store = fixture.Store();
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            Substitute<ICaseAssetPreparationQueries>(services, store);
            Substitute<ICaseAssetPreparationStore>(services, store);
            Substitute<ICaseEvidenceImageQueries>(services, store);
        });
        const string operationKey = "0a0b0c0d0e0f01020304050607080900";

        var leased = await workspace.GetWorkspaceAsync();
        var panel = Section(leased, "report-images-title");
        foreach (var field in new[]
        {
            "edits[0].occurrenceId",
            "edits[0].expectedPreparationVersion",
            "edits[0].role",
            "edits[0].order",
            "edits[0].rotation",
            "edits[0].cropLeft",
            "edits[0].cropTop",
            "edits[0].cropWidth",
            "edits[0].cropHeight",
            "edits[1].occurrenceId",
            "occurrenceIds"
        })
        {
            Assert.Contains($"name=\"{field}\"", panel, StringComparison.Ordinal);
        }
        foreach (var control in new[]
        {
            ReportImageLabels.Save,
            ReportImageLabels.Reset,
            ReportImageLabels.RotateLeft,
            ReportImageLabels.RotateRight,
            ReportImageLabels.MoveUp,
            ReportImageLabels.MoveDown
        })
        {
            Assert.Contains(control, panel, StringComparison.Ordinal);
        }

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=SaveAssetPreparation",
            workspace.MutationForm(
                operationKey,
                "ignored: the handler names its own reason",
                ("edits[0].occurrenceId", fixture.OverviewOccurrenceId.ToString("D")),
                ("edits[0].expectedPreparationVersion", "4"),
                ("edits[0].role", nameof(CaseAssetReportRole.Supporting)),
                ("edits[0].order", "3"),
                ("edits[0].rotation", "180"),
                ("edits[0].cropLeft", "0.05"),
                ("edits[0].cropTop", "0.1"),
                ("edits[0].cropWidth", "0.5"),
                ("edits[0].cropHeight", "0.6")));

        AssertFilesPrg(response, store.CaseId);
        var save = Assert.Single(store.PreparationSaves);
        AssertLeasedMutation(workspace, save, operationKey, ReportImageLabels.SaveReason);
        Assert.Equal(
            new CaseAssetPreparationEdit(
                fixture.OverviewOccurrenceId,
                4,
                CaseAssetReportRole.Supporting,
                3,
                CaseAssetRotation.Half,
                new(0.05m, 0.1m, 0.5m, 0.6m)),
            Assert.Single(save.Edits));
    }

    /// <summary>
    /// Reordering exchanges the two neighbours' orders in one command, so the
    /// sequence the operator sees is the one they asked for rather than one a
    /// tie-break settled.
    /// </summary>
    [Fact]
    public async Task MovingASupportingImageUpPostsBothNeighboursWithExchangedOrders()
    {
        var fixture = new PreparedImages();
        var store = fixture.Store();
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            Substitute<ICaseAssetPreparationQueries>(services, store);
            Substitute<ICaseAssetPreparationStore>(services, store);
            Substitute<ICaseEvidenceImageQueries>(services, store);
        });
        const string operationKey = "1a1b1c1d1e1f11121314151617181910";

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=SaveAssetPreparation",
            workspace.MutationForm(
                operationKey,
                "ignored: the handler names its own reason",
                ("edits[0].occurrenceId", fixture.SecondSupportingOccurrenceId.ToString("D")),
                ("edits[0].expectedPreparationVersion", "1"),
                ("edits[0].role", nameof(CaseAssetReportRole.Supporting)),
                ("edits[0].order", "1"),
                ("edits[0].rotation", "0"),
                ("edits[0].cropLeft", "0"),
                ("edits[0].cropTop", "0"),
                ("edits[0].cropWidth", "1"),
                ("edits[0].cropHeight", "1"),
                ("edits[1].occurrenceId", fixture.FirstSupportingOccurrenceId.ToString("D")),
                ("edits[1].expectedPreparationVersion", "1"),
                ("edits[1].role", nameof(CaseAssetReportRole.Supporting)),
                ("edits[1].order", "2"),
                ("edits[1].rotation", "0"),
                ("edits[1].cropLeft", "0"),
                ("edits[1].cropTop", "0"),
                ("edits[1].cropWidth", "1"),
                ("edits[1].cropHeight", "1")));

        AssertFilesPrg(response, store.CaseId);
        var save = Assert.Single(store.PreparationSaves);
        Assert.Equal(2, save.Edits.Count);
        Assert.Equal(fixture.SecondSupportingOccurrenceId, save.Edits[0].OccurrenceId);
        Assert.Equal(1, save.Edits[0].Order);
        Assert.Equal(fixture.FirstSupportingOccurrenceId, save.Edits[1].OccurrenceId);
        Assert.Equal(2, save.Edits[1].Order);
    }

    /// <summary>A reset names the occurrences to restore and nothing else.</summary>
    [Fact]
    public async Task ResettingAnImagesPreparationPostsItsOccurrenceId()
    {
        var fixture = new PreparedImages();
        var store = fixture.Store();
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            Substitute<ICaseAssetPreparationQueries>(services, store);
            Substitute<ICaseAssetPreparationStore>(services, store);
            Substitute<ICaseEvidenceImageQueries>(services, store);
        });
        const string operationKey = "2a2b2c2d2e2f21222324252627282920";

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ResetAssetPreparation",
            workspace.MutationForm(
                operationKey,
                "ignored: the handler names its own reason",
                ("occurrenceIds", fixture.CloseUpOccurrenceId.ToString("D"))));

        AssertFilesPrg(response, store.CaseId);
        var reset = Assert.Single(store.PreparationResets);
        AssertLeasedMutation(workspace, reset, operationKey, ReportImageLabels.ResetReason);
        Assert.Equal(fixture.CloseUpOccurrenceId, Assert.Single(reset.OccurrenceIds));
    }

    /// <summary>
    /// A refused save reports the refusal on the section the editor acted on
    /// and keeps this browser in edit mode, so they can correct and resubmit.
    /// </summary>
    [Fact]
    public async Task ARefusedPreparationSaveReportsTheRefusalAndKeepsEditMode()
    {
        var fixture = new PreparedImages();
        var store = fixture.Store();
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            Substitute<ICaseAssetPreparationQueries>(services, store);
            Substitute<ICaseAssetPreparationStore>(services, store);
            Substitute<ICaseEvidenceImageQueries>(services, store);
        });
        store.NextFailure = new InvalidOperationException("At most one Close-up image is permitted.");

        using var refused = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=SaveAssetPreparation",
            workspace.MutationForm(
                "3a3b3c3d3e3f31323334353637383930",
                "ignored: the handler names its own reason",
                ("edits[0].occurrenceId", fixture.OverviewOccurrenceId.ToString("D")),
                ("edits[0].expectedPreparationVersion", "4"),
                ("edits[0].role", nameof(CaseAssetReportRole.CloseUp)),
                ("edits[0].rotation", "0"),
                ("edits[0].cropLeft", "0"),
                ("edits[0].cropTop", "0"),
                ("edits[0].cropWidth", "1"),
                ("edits[0].cropHeight", "1")));

        AssertFilesPrg(refused, store.CaseId);
        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=files");
        Assert.Contains("role=\"alert\"", html, StringComparison.Ordinal);
        Assert.Contains("At most one Close-up image is permitted.", html, StringComparison.Ordinal);
        Assert.Equal(store.LeaseToken, InputValue(html, "editLeaseToken"));
    }

    /// <summary>
    /// The Report section states the same prepared set in the report's own
    /// order — Close-up, Overview, then Supporting by order — and omits the
    /// images the report does not use.
    /// </summary>
    [Fact]
    public async Task TheReportSectionStatesThePreparedSetInReportOrderAndOmitsUnusedImages()
    {
        var fixture = new PreparedImages();
        var store = fixture.Store();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<ICaseAssetPreparationQueries>(services, store);
                Substitute<ICaseEvidenceImageQueries>(services, store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=report");
        var panel = Section(html, "section-report-title");

        var order = new[]
        {
            CloseUpFileName, OverviewFileName, FirstSupportingFileName, SecondSupportingFileName
        }
            .Select(fileName => panel.IndexOf(fileName, StringComparison.Ordinal))
            .ToArray();
        Assert.All(order, position => Assert.True(position >= 0, "Every prepared image is named on the report cards."));
        Assert.Equal(order.OrderBy(position => position), order);
        Assert.DoesNotContain(UnusedFileName, panel, StringComparison.Ordinal);
        Assert.DoesNotContain(ReportImageLabels.RoleLabel(CaseAssetReportRole.NotUsed), panel, StringComparison.Ordinal);

        var visible = WebUtility.HtmlDecode(VisibleText(panel));
        Assert.Contains(ReportImageLabels.RotationLabel(CaseAssetRotation.Clockwise90), visible, StringComparison.Ordinal);
        Assert.Contains(ReportImageLabels.CropLabel(fixture.OverviewCrop), visible, StringComparison.Ordinal);
        Assert.Contains(ReportImageLabels.FullFrame, visible, StringComparison.Ordinal);
    }

    /// <summary>
    /// B08: the Report cards offer the same preparation the Files cards do.
    /// One partial renders both from one loaded set, so a card in either
    /// section binds exactly the same fields and offers exactly the same
    /// controls; only the section its forms post back to differs.
    /// </summary>
    [Fact]
    public async Task TheReportCardsOfferTheSameControlsAndBoundFieldsAsTheFilesCards()
    {
        var fixture = new PreparedImages();
        var store = fixture.Store();
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            Substitute<ICaseAssetPreparationQueries>(services, store);
            Substitute<ICaseAssetPreparationStore>(services, store);
            Substitute<ICaseEvidenceImageQueries>(services, store);
        });

        var leased = await workspace.GetWorkspaceAsync();
        var files = Section(leased, "report-images-title");
        var report = Section(leased, "section-report-title");

        foreach (var occurrenceId in new[]
        {
            fixture.CloseUpOccurrenceId,
            fixture.OverviewOccurrenceId,
            fixture.FirstSupportingOccurrenceId,
            fixture.SecondSupportingOccurrenceId
        })
        {
            var filesCard = Card(files, occurrenceId);
            var reportCard = Card(report, occurrenceId);
            Assert.NotEmpty(BoundFieldNames(reportCard));
            Assert.Equal(BoundFieldNames(filesCard), BoundFieldNames(reportCard));
            Assert.Contains("section=files", filesCard, StringComparison.Ordinal);
            Assert.DoesNotContain("section=report", filesCard, StringComparison.Ordinal);
            Assert.Contains("section=report", reportCard, StringComparison.Ordinal);
            Assert.DoesNotContain("section=files", reportCard, StringComparison.Ordinal);
        }

        foreach (var control in new[]
        {
            ReportImageLabels.Save,
            ReportImageLabels.Reset,
            ReportImageLabels.RotateLeft,
            ReportImageLabels.RotateRight,
            ReportImageLabels.MoveUp,
            ReportImageLabels.MoveDown
        })
        {
            Assert.Contains(control, ReportImageCards(report), StringComparison.Ordinal);
        }

        // The move each end of the sequence does not offer is the same in
        // both sections, because both read one Supporting sequence.
        Assert.DoesNotContain(ReportImageLabels.MoveUp, Card(report, fixture.FirstSupportingOccurrenceId), StringComparison.Ordinal);
        Assert.DoesNotContain(ReportImageLabels.MoveUp, Card(files, fixture.FirstSupportingOccurrenceId), StringComparison.Ordinal);
        Assert.DoesNotContain(ReportImageLabels.MoveDown, Card(report, fixture.SecondSupportingOccurrenceId), StringComparison.Ordinal);
        Assert.DoesNotContain(ReportImageLabels.MoveDown, Card(files, fixture.SecondSupportingOccurrenceId), StringComparison.Ordinal);
    }

    /// <summary>
    /// B08: a preparation command posted from the Report section returns to
    /// the Report section, so the editor reads the outcome where they acted
    /// rather than being moved to Files.
    /// </summary>
    [Fact]
    public async Task APreparationCommandFromTheReportSectionReturnsToTheReportSection()
    {
        var fixture = new PreparedImages();
        var store = fixture.Store();
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            Substitute<ICaseAssetPreparationQueries>(services, store);
            Substitute<ICaseAssetPreparationStore>(services, store);
            Substitute<ICaseEvidenceImageQueries>(services, store);
        });

        using var saved = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=SaveAssetPreparation&section=report",
            workspace.MutationForm(
                "4a4b4c4d4e4f41424344454647484940",
                "ignored: the handler names its own reason",
                ("edits[0].occurrenceId", fixture.FirstSupportingOccurrenceId.ToString("D")),
                ("edits[0].expectedPreparationVersion", "1"),
                ("edits[0].role", nameof(CaseAssetReportRole.Supporting)),
                ("edits[0].order", "2"),
                ("edits[0].rotation", "0"),
                ("edits[0].cropLeft", "0"),
                ("edits[0].cropTop", "0"),
                ("edits[0].cropWidth", "1"),
                ("edits[0].cropHeight", "1")));

        AssertPreparationPrg(saved, store.CaseId, "report");
        Assert.Equal(
            fixture.FirstSupportingOccurrenceId,
            Assert.Single(Assert.Single(store.PreparationSaves).Edits).OccurrenceId);

        using var reset = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ResetAssetPreparation&section=report",
            workspace.MutationForm(
                "5a5b5c5d5e5f51525354555657585950",
                "ignored: the handler names its own reason",
                ("occurrenceIds", fixture.CloseUpOccurrenceId.ToString("D"))));

        AssertPreparationPrg(reset, store.CaseId, "report");
        Assert.Equal(
            fixture.CloseUpOccurrenceId,
            Assert.Single(Assert.Single(store.PreparationResets).OccurrenceIds));
    }

    /// <summary>
    /// B08: the read-only Report cards state the prepared values and nothing
    /// that could change one — no control and no drag hook — while this
    /// browser holds no edit lease.
    /// </summary>
    [Fact]
    public async Task TheReportCardsCarryNoControlWithoutTheLease()
    {
        var fixture = new PreparedImages();
        var store = fixture.Store();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<ICaseAssetPreparationQueries>(services, store);
                Substitute<ICaseEvidenceImageQueries>(services, store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=report");
        var cards = ReportImageCards(Section(html, "section-report-title"));

        Assert.Contains(CloseUpFileName, cards, StringComparison.Ordinal);
        Assert.Contains(FirstSupportingFileName, cards, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", cards, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<button", cards, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<select", cards, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<input", cards, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("draggable", cards, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-preparation-version", cards, StringComparison.Ordinal);
    }

    /// <summary>
    /// B08: in edit mode the record carries its own enhancement script and
    /// each Supporting card carries the shape of the edit a drop submits, so
    /// dragging posts the same SaveAssetPreparation command Move up and Move
    /// down post. A card the sequence does not order is not draggable. The
    /// drop itself is a browser gesture and there is no browser harness in
    /// this suite, so what is proved here is the markup the script binds to.
    /// </summary>
    [Fact]
    public async Task TheSupportingCardsCarryTheDragHooksInEditMode()
    {
        var fixture = new PreparedImages();
        var store = fixture.Store();
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            Substitute<ICaseAssetPreparationQueries>(services, store);
            Substitute<ICaseAssetPreparationStore>(services, store);
            Substitute<ICaseEvidenceImageQueries>(services, store);
        });

        var leased = await workspace.GetWorkspaceAsync();
        // MapStaticAssets fingerprints the served file name, so the assertion
        // names the asset rather than the exact path the tag helper writes.
        Assert.Contains("js/case-workspace", leased, StringComparison.Ordinal);

        foreach (var panel in new[]
        {
            Section(leased, "report-images-title"),
            Section(leased, "section-report-title")
        })
        {
            Assert.Contains("data-report-images=", panel, StringComparison.Ordinal);
            Assert.Contains("data-preparation-command=\"save\"", panel, StringComparison.Ordinal);
            foreach (var occurrenceId in new[]
            {
                fixture.FirstSupportingOccurrenceId, fixture.SecondSupportingOccurrenceId
            })
            {
                var card = Card(panel, occurrenceId);
                foreach (var hook in new[]
                {
                    "draggable=\"true\"",
                    "data-preparation-version=",
                    "data-report-role=",
                    "data-report-order=",
                    "data-report-rotation=",
                    "data-crop-left=",
                    "data-crop-top=",
                    "data-crop-width=",
                    "data-crop-height="
                })
                {
                    Assert.Contains(hook, card, StringComparison.Ordinal);
                }
            }

            // Only the Supporting sequence is ordered, so only it is dragged.
            Assert.DoesNotContain("draggable", Card(panel, fixture.CloseUpOccurrenceId), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("draggable", Card(panel, fixture.OverviewOccurrenceId), StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// The preparation redirect lands on the Files section, not the record's
    /// top, so the editor reads the outcome where they acted.
    /// </summary>
    private static void AssertFilesPrg(HttpResponseMessage response, Guid caseId) =>
        AssertPreparationPrg(response, caseId, "files");

    /// <summary>
    /// B08: the redirect lands on the section the command was posted from —
    /// the same controls are offered in Files and in Report, and each
    /// section's forms carry their own section.
    /// </summary>
    private static void AssertPreparationPrg(HttpResponseMessage response, Guid caseId, string section)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Cases/{caseId:D}?section={section}", response.Headers.Location?.OriginalString);
    }

    /// <summary>
    /// Every report-image card in a section and nothing around them, so an
    /// assertion about the cards is never answered by the panel's other
    /// controls. Cards do not nest, so the scan needs no depth count.
    /// </summary>
    private static string ReportImageCards(string panel)
    {
        var cards = new StringBuilder();
        var index = 0;
        while (true)
        {
            var start = panel.IndexOf("<article class=\"report-image\"", index, StringComparison.Ordinal);
            if (start < 0)
            {
                return cards.ToString();
            }
            var end = panel.IndexOf("</article>", start, StringComparison.Ordinal);
            Assert.True(end > start, "A report-image card is not closed.");
            end += "</article>".Length;
            cards.Append(panel[start..end]);
            index = end;
        }
    }

    /// <summary>One occurrence's card within a section.</summary>
    private static string Card(string panel, Guid occurrenceId)
    {
        var marker = panel.IndexOf($"data-report-image=\"{occurrenceId:D}\"", StringComparison.Ordinal);
        Assert.True(marker >= 0, $"The card for '{occurrenceId:D}' is not rendered.");
        var start = panel.LastIndexOf("<article", marker, StringComparison.Ordinal);
        var end = panel.IndexOf("</article>", marker, StringComparison.Ordinal);
        Assert.True(end > start, $"The card for '{occurrenceId:D}' is not closed.");
        return panel[start..(end + "</article>".Length)];
    }

    /// <summary>
    /// The names a card's controls bind, sorted and without duplicates, so
    /// two cards can be compared for the exact posted shape rather than for
    /// the operation keys and tokens that are fresh on every render.
    /// </summary>
    private static IReadOnlyList<string> BoundFieldNames(string card)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        var index = 0;
        while (true)
        {
            var start = card.IndexOf("name=\"", index, StringComparison.Ordinal);
            if (start < 0)
            {
                return [.. names];
            }
            start += "name=\"".Length;
            var end = card.IndexOf('"', start);
            Assert.True(end > start, "A bound control's name is not closed.");
            names.Add(card[start..end]);
            index = end;
        }
    }

    /// <summary>
    /// One case's image occurrences and the preparation each carries: a
    /// Close-up turned a quarter turn, a cropped Overview, two ordered
    /// Supporting images and one the report does not use.
    /// </summary>
    private sealed class PreparedImages
    {
        public Guid CloseUpOccurrenceId { get; } = Guid.NewGuid();

        public Guid OverviewOccurrenceId { get; } = Guid.NewGuid();

        public Guid FirstSupportingOccurrenceId { get; } = Guid.NewGuid();

        public Guid SecondSupportingOccurrenceId { get; } = Guid.NewGuid();

        public Guid UnusedOccurrenceId { get; } = Guid.NewGuid();

        public CaseAssetCrop OverviewCrop { get; } = new(0.1m, 0.1m, 0.8m, 0.8m);

        public RecordingCaseDetailsStore Store()
        {
            var store = new RecordingCaseDetailsStore
            {
                CaseDocuments =
                [
                    Document(CloseUpOccurrenceId, VersionOf(CloseUpOccurrenceId), CloseUpFileName, "image/jpeg"),
                    Document(OverviewOccurrenceId, VersionOf(OverviewOccurrenceId), OverviewFileName, "image/jpeg"),
                    Document(FirstSupportingOccurrenceId, VersionOf(FirstSupportingOccurrenceId), FirstSupportingFileName, "image/jpeg"),
                    Document(SecondSupportingOccurrenceId, VersionOf(SecondSupportingOccurrenceId), SecondSupportingFileName, "image/jpeg"),
                    Document(UnusedOccurrenceId, VersionOf(UnusedOccurrenceId), UnusedFileName, "image/jpeg")
                ]
            };
            store.Preparations =
            [
                Preparation(store.CaseId, CloseUpOccurrenceId, CaseAssetReportRole.CloseUp, null, CaseAssetRotation.Clockwise90, CaseAssetCrop.Full, 2),
                Preparation(store.CaseId, OverviewOccurrenceId, CaseAssetReportRole.Overview, null, CaseAssetRotation.None, OverviewCrop, 4),
                Preparation(store.CaseId, FirstSupportingOccurrenceId, CaseAssetReportRole.Supporting, 1, CaseAssetRotation.None, CaseAssetCrop.Full, 1),
                Preparation(store.CaseId, SecondSupportingOccurrenceId, CaseAssetReportRole.Supporting, 2, CaseAssetRotation.None, CaseAssetCrop.Full, 1),
                Preparation(store.CaseId, UnusedOccurrenceId, CaseAssetReportRole.NotUsed, null, CaseAssetRotation.None, CaseAssetCrop.Full, 0)
            ];
            store.CaseEvidenceImages =
            [
                EvidenceImage(CloseUpOccurrenceId, CloseUpFileName),
                EvidenceImage(OverviewOccurrenceId, OverviewFileName),
                EvidenceImage(FirstSupportingOccurrenceId, FirstSupportingFileName),
                EvidenceImage(SecondSupportingOccurrenceId, SecondSupportingFileName),
                EvidenceImage(UnusedOccurrenceId, UnusedFileName)
            ];
            return store;
        }

        /// <summary>
        /// The pinned version of an occurrence. It is derived from the
        /// occurrence identity so the document fixture and the preparation
        /// name the same version without a second table to keep in step.
        /// </summary>
        private static Guid VersionOf(Guid occurrenceId)
        {
            var bytes = occurrenceId.ToByteArray();
            bytes[0] ^= 0xFF;
            return new(bytes);
        }

        private static CaseAssetPreparation Preparation(
            Guid caseId,
            Guid occurrenceId,
            CaseAssetReportRole role,
            int? order,
            CaseAssetRotation rotation,
            CaseAssetCrop crop,
            long preparationVersion) =>
            new(
                caseId,
                occurrenceId,
                Guid.NewGuid(),
                VersionOf(occurrenceId),
                1,
                new string('a', 64),
                "image/jpeg",
                role,
                order,
                rotation,
                crop,
                preparationVersion,
                preparationVersion == 0 ? null : "staff",
                preparationVersion == 0 ? null : new DateTimeOffset(2031, 5, 6, 9, 0, 0, TimeSpan.Zero));

        private static CaseEvidenceImage EvidenceImage(Guid occurrenceId, string fileName) =>
            new(Guid.NewGuid(), Guid.NewGuid(), fileName, "image/jpeg", 24_576, occurrenceId, VersionOf(occurrenceId));
    }

    /// <summary>
    /// The report-preparation ports the workspace calls, recording what the
    /// page bound. The query answers in the order the persisted store answers
    /// in — role, then supporting order — so the page is exercised against the
    /// shape it really receives.
    /// </summary>
    private sealed partial class RecordingCaseDetailsStore :
        ICaseAssetPreparationQueries,
        ICaseAssetPreparationStore,
        ICaseEvidenceImageQueries
    {
        /// <summary>The case's image preparations, when a test supplies them.</summary>
        public IReadOnlyList<CaseAssetPreparation> Preparations { get; set; } = [];

        /// <summary>The instruction evidence photographs, when a test supplies them.</summary>
        public IReadOnlyList<CaseEvidenceImage> CaseEvidenceImages { get; set; } = [];

        public List<SaveCaseAssetPreparationRequest> PreparationSaves { get; } = [];

        public List<ResetCaseAssetPreparationRequest> PreparationResets { get; } = [];

        Task<IReadOnlyList<CaseAssetPreparation>> ICaseAssetPreparationQueries.ListForCaseAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Current());

        Task<IReadOnlyList<CaseAssetPreparation>> ICaseAssetPreparationStore.SaveAsync(
            SaveCaseAssetPreparationRequest request,
            CancellationToken cancellationToken)
        {
            PreparationSaves.Add(request);
            ThrowNextFailure();
            return Task.FromResult(Current());
        }

        Task<IReadOnlyList<CaseAssetPreparation>> ICaseAssetPreparationStore.ResetAsync(
            ResetCaseAssetPreparationRequest request,
            CancellationToken cancellationToken)
        {
            PreparationResets.Add(request);
            ThrowNextFailure();
            return Task.FromResult(Current());
        }

        Task<IReadOnlyList<CaseEvidenceImage>> ICaseEvidenceImageQueries.ListForCaseAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult(CaseEvidenceImages);

        private IReadOnlyList<CaseAssetPreparation> Current() =>
        [
            .. Preparations
                .OrderBy(item => item.Role)
                .ThenBy(item => item.Order ?? int.MaxValue)
        ];
    }
}
