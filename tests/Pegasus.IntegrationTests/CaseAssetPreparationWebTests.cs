using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Workflow;
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
    /// Read-only view: the Files section's Images tab names every image of the
    /// case and offers nothing that could change one while this browser holds
    /// no edit lease. Report composition is not here at all — it is stated
    /// once, on the Report section (issue 6).
    /// </summary>
    [Fact]
    public async Task TheImagesTabNamesEveryImageAndOffersNoControlWithoutTheLease()
    {
        var fixture = new PreparedImages();
        var store = fixture.Store();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<ICaseAssetPreparationQueries>(services, store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=files");
        var grid = ImageGrid(html);

        foreach (var fileName in new[]
        {
            CloseUpFileName, OverviewFileName, FirstSupportingFileName, SecondSupportingFileName, UnusedFileName
        })
        {
            Assert.Contains(fileName, grid, StringComparison.Ordinal);
        }
        // The tile asks for the derived rendering, and the viewer link keeps
        // the full image.
        Assert.Contains("size=thumb", grid, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-evidence-item", grid, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", grid, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<button", grid, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-preparation-", grid, StringComparison.Ordinal);
        Assert.DoesNotContain("report-images-title", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// U5a: Crop follows the Case edit lease. A Review-state case — where
    /// CanEditEngineering is false and Crop used to be nowhere — offers it on
    /// every image tile. The reader without the lease is the test above, which
    /// finds no preparation hook at all.
    /// </summary>
    [Fact]
    public async Task CropIsOfferedOnTheImagesTabWheneverTheCaseEditLeaseIsHeld()
    {
        var fixture = new PreparedImages();
        var store = fixture.Store(CaseLifecycleState.Review);
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            Substitute<ICaseAssetPreparationQueries>(services, store);
        });

        var leased = await workspace.GetWorkspaceAsync();
        var grid = ImageGrid(await GetFilesFragmentAsync(workspace, leased));

        Assert.Contains("data-preparation-crop", grid, StringComparison.Ordinal);
        Assert.Contains(
            $"data-preparation-occurrence=\"{fixture.OverviewOccurrenceId:D}\"",
            grid,
            StringComparison.Ordinal);
        // The viewer's own Crop button needs the occurrence on the item it is
        // paging over.
        Assert.Contains(
            $"data-evidence-preparation-occurrence=\"{fixture.OverviewOccurrenceId:D}\"",
            grid,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// U5: preparation is staged with the record and the one global Case Save
    /// carries the resulting edit under the Case lease and version.
    /// </summary>
    [Fact]
    public async Task SavingTheCaseCarriesTheStagedImagePreparationEdit()
    {
        var fixture = new PreparedImages();
        var store = fixture.Store(CaseLifecycleState.ReportPreparation);
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            Substitute<ICaseAssetPreparationQueries>(services, store);
            Substitute<ISaveCaseWorkspace>(services, store);
        });
        const string operationKey = "0a0b0c0d0e0f01020304050607080900";

        var leased = await workspace.GetWorkspaceAsync();
        Assert.Equal(1, Occurrences(leased, "id=\"case-edit-form\""));
        Assert.DoesNotContain("handler=SaveAssetPreparation", leased, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=ResetAssetPreparation", leased, StringComparison.Ordinal);
        // A missing closing quote used to swallow the preview URL into the
        // crop-height attribute, leaving the browser crop editor without a source.
        Assert.Matches("data-preparation-crop-height=\"[0-9.]+\"\\s+data-preparation-preview=\"/Cases/", leased);

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(
                operationKey,
                "Prepared report image.",
                ("preparationEdits[0].occurrenceId", fixture.OverviewOccurrenceId.ToString("D")),
                ("preparationEdits[0].expectedPreparationVersion", "4"),
                ("preparationEdits[0].role", nameof(CaseAssetReportRole.Supporting)),
                ("preparationEdits[0].order", "3"),
                ("preparationEdits[0].rotation", "180"),
                ("preparationEdits[0].cropLeft", "0.05"),
                ("preparationEdits[0].cropTop", "0.1"),
                ("preparationEdits[0].cropWidth", "0.5"),
                ("preparationEdits[0].cropHeight", "0.6")));

        AssertPrg(response, store.CaseId);
        var preparation = Assert.Single(store.Saves).ImagePreparation;
        Assert.NotNull(preparation);
        Assert.NotNull(preparation.Edits);
        var edit = Assert.Single(preparation.Edits);
        Assert.Equal(
            new CaseAssetPreparationEdit(
                fixture.OverviewOccurrenceId,
                4,
                CaseAssetReportRole.Supporting,
                3,
                CaseAssetRotation.Half,
                new(0.05m, 0.1m, 0.5m, 0.6m)),
            edit);
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
    /// U5: each editable card supplies the state the workspace script stages
    /// into the one Case Save form. The cards deliberately contain no local
    /// post shape or command because crop, reset, rotation and role changes
    /// are all saved with the Case.
    /// </summary>
    [Fact]
    public async Task TheEditableCardsStagePreparationForTheSingleCaseSave()
    {
        var fixture = new PreparedImages();
        var store = fixture.Store();
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            Substitute<ICaseAssetPreparationQueries>(services, store);
        });

        var leased = await workspace.GetWorkspaceAsync();
        var files = await GetFilesFragmentAsync(workspace, leased);
        // MapStaticAssets fingerprints the served file name, so the assertion
        // names the asset rather than the exact path the tag helper writes.
        Assert.Contains("js/case-workspace", leased, StringComparison.Ordinal);

        // The Files section stages through the image tiles themselves: the
        // same occurrence, version and crop the Report card carries, so a crop
        // taken on either lands in the one Case form.
        var tiles = ImageGrid(files);
        foreach (var hook in new[]
        {
            "data-preparation-card",
            "data-preparation-occurrence=",
            "data-preparation-version=",
            "data-preparation-crop-left=",
            "data-preparation-crop-top=",
            "data-preparation-crop-width=",
            "data-preparation-crop-height=",
            "data-preparation-preview=",
            "data-preparation-crop"
        })
        {
            Assert.Contains(hook, tiles, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("handler=SaveAssetPreparation", tiles, StringComparison.Ordinal);
        Assert.DoesNotContain("data-preparation-role-select", tiles, StringComparison.Ordinal);

        foreach (var panel in new[] { Section(leased, "section-report-title") })
        {
            Assert.Contains("data-report-images=", panel, StringComparison.Ordinal);
            foreach (var occurrenceId in new[]
            {
                fixture.CloseUpOccurrenceId,
                fixture.OverviewOccurrenceId,
                fixture.FirstSupportingOccurrenceId, fixture.SecondSupportingOccurrenceId
            })
            {
                var card = Card(panel, occurrenceId);
                foreach (var hook in new[]
                {
                    "data-preparation-card",
                    "data-preparation-occurrence=",
                    "data-preparation-version=",
                    "data-preparation-role=",
                    "data-preparation-rotation=",
                    "data-preparation-crop-left=",
                    "data-preparation-crop-top=",
                    "data-preparation-crop-width=",
                    "data-preparation-crop-height=",
                    "data-preparation-role-select",
                    "data-preparation-rotate",
                    "data-preparation-reset",
                    "data-preparation-crop"
                })
                {
                    Assert.Contains(hook, card, StringComparison.Ordinal);
                }
                Assert.DoesNotContain("handler=SaveAssetPreparation", card, StringComparison.Ordinal);
                Assert.DoesNotContain("handler=ResetAssetPreparation", card, StringComparison.Ordinal);
                Assert.DoesNotContain("name=\"edits[", card, StringComparison.Ordinal);
            }

            foreach (var occurrenceId in new[]
            {
                fixture.FirstSupportingOccurrenceId,
                fixture.SecondSupportingOccurrenceId
            })
            {
                Assert.Contains("data-preparation-order=", Card(panel, occurrenceId), StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// The Files body mounts after the page's first response. Match the
    /// browser request: send the rendered lease token only as fragment
    /// rendering data, so the server can render the existing edit controls.
    /// </summary>
    private static async Task<string> GetFilesFragmentAsync(
        LeasedWorkspace workspace,
        string renderedWorkspace)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/Cases/{workspace.Store.CaseId:D}/Section?section=files");
        request.Headers.Add(
            "X-Pegasus-Edit-Lease",
            InputValue(renderedWorkspace, "editLeaseToken"));

        using var response = await workspace.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
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

    /// <summary>
    /// The Files section's image grid and nothing around it, so an assertion
    /// about the tiles is never answered by the document rows or the upload
    /// requests beside them.
    /// </summary>
    private static string ImageGrid(string html)
    {
        var start = html.IndexOf("<ul class=\"gallery image-grid\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The Images tab grid is not rendered.");
        var end = html.IndexOf("</ul>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The Images tab grid is not closed.");
        return html[start..end];
    }

    /// <summary>One occurrence's card within a section.</summary>
    private static string Card(string panel, Guid occurrenceId)
    {
        var marker = panel.IndexOf($"data-preparation-occurrence=\"{occurrenceId:D}\"", StringComparison.Ordinal);
        Assert.True(marker >= 0, $"The card for '{occurrenceId:D}' is not rendered.");
        var start = panel.LastIndexOf("<article", marker, StringComparison.Ordinal);
        var end = panel.IndexOf("</article>", marker, StringComparison.Ordinal);
        Assert.True(end > start, $"The card for '{occurrenceId:D}' is not closed.");
        return panel[start..(end + "</article>".Length)];
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

        public RecordingCaseDetailsStore Store(
            CaseLifecycleState state = CaseLifecycleState.NotReady)
        {
            var store = new RecordingCaseDetailsStore
            {
                State = state,
                CaseState = state,
                CaseDocuments =
                [
                    Document(CloseUpOccurrenceId, VersionOf(CloseUpOccurrenceId), CloseUpFileName, "image/jpeg", DocumentSemanticRole.Image),
                    Document(OverviewOccurrenceId, VersionOf(OverviewOccurrenceId), OverviewFileName, "image/jpeg", DocumentSemanticRole.Image),
                    Document(FirstSupportingOccurrenceId, VersionOf(FirstSupportingOccurrenceId), FirstSupportingFileName, "image/jpeg", DocumentSemanticRole.Image),
                    Document(SecondSupportingOccurrenceId, VersionOf(SecondSupportingOccurrenceId), SecondSupportingFileName, "image/jpeg", DocumentSemanticRole.Image),
                    Document(UnusedOccurrenceId, VersionOf(UnusedOccurrenceId), UnusedFileName, "image/jpeg", DocumentSemanticRole.Image)
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
    }

    /// <summary>
    /// The report-preparation queries the workspace reads. The query answers
    /// in the order the persisted store answers — role, then supporting order
    /// — so the page is exercised against the shape it really receives.
    /// </summary>
    private sealed partial class RecordingCaseDetailsStore :
        ICaseAssetPreparationQueries
    {
        /// <summary>The case's image preparations, when a test supplies them.</summary>
        public IReadOnlyList<CaseAssetPreparation> Preparations { get; set; } = [];

        Task<IReadOnlyList<CaseAssetPreparation>> ICaseAssetPreparationQueries.ListForCaseAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Current());

        private IReadOnlyList<CaseAssetPreparation> Current() =>
        [
            .. Preparations
                .OrderBy(item => item.Role)
                .ThenBy(item => item.Order ?? int.MaxValue)
        ];
    }
}
