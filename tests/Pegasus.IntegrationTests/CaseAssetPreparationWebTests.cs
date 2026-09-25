using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.IntegrationTests.Reports;

using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// B06 phase 2: report-image preparation on the one Case workspace. The Files
/// section states each image's role, order, rotation and crop and — in edit
/// mode — offers the controls that change them; the Report section carries no
/// image surface, so the tests assert that Files is the single presentation home.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseAssetPreparationWebTests
{
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
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<IGetAssessmentWorkspace>(services, store);
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

    [Fact]
    public async Task FilesImagesWithoutPreparationUseVersionZeroThumbnailAddresses()
    {
        var fixture = new PreparedImages();
        var store = fixture.Store();
        store.Preparations = [];
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<IGetAssessmentWorkspace>(services, store);
                Substitute<ICaseAssetPreparationQueries>(services, store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var files = ImageGrid(await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=files"));

        foreach (var occurrenceId in new[]
        {
            fixture.CloseUpOccurrenceId,
            fixture.OverviewOccurrenceId,
            fixture.FirstSupportingOccurrenceId,
            fixture.SecondSupportingOccurrenceId,
            fixture.UnusedOccurrenceId
        })
        {
            var tile = Assert.Single(
                Tiles(files),
                candidate => candidate.Contains(
                    $"data-image-tile=\"{occurrenceId:D}\"",
                    StringComparison.Ordinal));
            Assert.Contains(
                $"&amp;prep=0&amp;renderer={CaseDocumentThumbnails.RendererIdentity}\"",
                tile,
                StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Custody does not decide whether the operator can see their own file. An
    /// image whose bytes have not reached durable custody is on the Case, so
    /// the tab shows it as the placeholder the other galleries draw — its name
    /// and its custody state, with nothing that would read content that cannot
    /// be read. The tab built from confirmed files only showed no sign of it.
    /// </summary>
    [Fact]
    public async Task TheImagesTabShowsAnImageStillReachingCustodyAsAPlaceholder()
    {
        var storedOccurrenceId = Guid.NewGuid();
        var storingOccurrenceId = Guid.NewGuid();
        var store = new RecordingCaseDetailsStore
        {
            CaseDocuments =
            [
                Document(
                    storedOccurrenceId,
                    Guid.NewGuid(),
                    OverviewFileName,
                    "image/jpeg",
                    DocumentSemanticRole.Image),
                Document(
                    storingOccurrenceId,
                    Guid.NewGuid(),
                    UnusedFileName,
                    "image/jpeg",
                    DocumentSemanticRole.Image,
                    custody: DocumentCustodyStatus.Pending)
            ]
        };
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<IGetAssessmentWorkspace>(services, store);
                Substitute<ICaseAssetPreparationQueries>(services, store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var grid = ImageGrid(await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=files"));
        var tiles = Tiles(grid);

        Assert.Equal(2, tiles.Length);
        var stored = Assert.Single(tiles, tile => tile.Contains(OverviewFileName, StringComparison.Ordinal));
        var storing = Assert.Single(tiles, tile => tile.Contains(UnusedFileName, StringComparison.Ordinal));

        Assert.Contains("size=thumb", stored, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-gallery-placeholder", stored, StringComparison.Ordinal);

        // The placeholder names the file and states where its storage has
        // reached, and offers no thumbnail, no viewer link and no download.
        Assert.Contains("data-gallery-placeholder", storing, StringComparison.Ordinal);
        Assert.Contains(
            Pegasus.Web.Presentation.OperatorLabels.CustodyState(DocumentCustodyStatus.Pending),
            WebUtility.HtmlDecode(storing),
            StringComparison.Ordinal);
        Assert.DoesNotContain("<img", storing, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<a", storing, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-evidence-item", storing, StringComparison.Ordinal);
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
        Assert.Contains("<noscript>", grid, StringComparison.Ordinal);
        Assert.Contains("name=\"preparationEdits[0].Role\"", grid, StringComparison.Ordinal);
        Assert.Contains("name=\"preparationEdits[0].Order\"", grid, StringComparison.Ordinal);
        Assert.Contains("name=\"preparationEdits[0].Rotation\"", grid, StringComparison.Ordinal);
        Assert.Contains("name=\"preparationEdits[0].FullPage\"", grid, StringComparison.Ordinal);
        Assert.Contains("Remove from report", grid, StringComparison.Ordinal);
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
        // crop-height attribute, leaving the browser crop editor without a
        // source. The metadata is the tile's own since v28 P50, so the guard
        // reads the Files fragment the tile is rendered in.
        Assert.Matches(
            "data-preparation-crop-height=\"[0-9.]+\"\\s+data-preparation-full-page=\"(true|false)\"\\s+data-preparation-preview=\"/Cases/",
            await GetFilesFragmentAsync(workspace, leased));

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
                ("preparationEdits[0].cropHeight", "0.6"),
                ("preparationEdits[0].fullPage", "true")));

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
                new(0.05m, 0.1m, 0.5m, 0.6m),
                true),
            edit);
    }

    /// <summary>
    /// D4/FRD-12: an image preparation is not an engineering field. Crop is
    /// offered wherever the Case edit lease is held, so the Save that carries
    /// one is accepted in Review along with the editable Engineer sections.
    /// </summary>
    [Fact]
    public async Task NativeRemoveFromReportPostsTheSameCasePreparationCommand()
    {
        var fixture = new PreparedImages();
        var store = fixture.Store(CaseLifecycleState.ReportPreparation);
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            Substitute<ICaseAssetPreparationQueries>(services, store);
            Substitute<ISaveCaseWorkspace>(services, store);
        });
        var leased = await workspace.GetWorkspaceAsync();
        var grid = ImageGrid(await GetFilesFragmentAsync(workspace, leased));
        Assert.Contains("Remove from report", grid, StringComparison.Ordinal);

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(
                "0a0b0c0d0e0f01020304050607080903",
                "Report images prepared.",
                ("preparationEdits[0].OccurrenceId", fixture.OverviewOccurrenceId.ToString("D")),
                ("preparationEdits[0].ExpectedPreparationVersion", "4"),
                ("preparationEdits[0].Role", nameof(CaseAssetReportRole.NotUsed)),
                ("preparationEdits[0].Rotation", "0"),
                ("preparationEdits[0].CropLeft", "0.05"),
                ("preparationEdits[0].CropTop", "0.1"),
                ("preparationEdits[0].CropWidth", "0.5"),
                ("preparationEdits[0].CropHeight", "0.6"),
                ("preparationEdits[0].FullPage", "false")));

        AssertPrg(response, store.CaseId);
        var edit = Assert.Single(Assert.Single(store.Saves).ImagePreparation!.Edits!);
        Assert.Equal(CaseAssetReportRole.NotUsed, edit.Role);
        Assert.False(edit.FullPage);
    }

    [Fact]
    public async Task ACropIsSavedOnAReviewStateCase()
    {
        var fixture = new PreparedImages();
        var store = fixture.Store(CaseLifecycleState.Review);
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            Substitute<ICaseAssetPreparationQueries>(services, store);
            Substitute<ISaveCaseWorkspace>(services, store);
        });

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(
                "0a0b0c0d0e0f01020304050607080901",
                "Cropped a report image.",
                CropFields(fixture.OverviewOccurrenceId)));

        AssertPrg(response, store.CaseId);
        var preparation = Assert.Single(store.Saves).ImagePreparation;
        Assert.NotNull(preparation);
        Assert.NotNull(preparation.Edits);
        var edit = Assert.Single(preparation.Edits);
        Assert.Equal(fixture.OverviewOccurrenceId, edit.OccurrenceId);
        Assert.Equal(new CaseAssetCrop(0.05m, 0.1m, 0.5m, 0.6m), edit.Crop);
    }

    /// <summary>
    /// The read-only end of the Case is still read-only. Once the case is
    /// Complete nothing about the record is edited, preparation included, and
    /// the refusal is the page's — the store never sees the save.
    /// </summary>
    [Fact]
    public async Task ACropIsRefusedOnceTheCaseIsComplete()
    {
        var fixture = new PreparedImages();
        var store = fixture.Store();
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            Substitute<ICaseAssetPreparationQueries>(services, store);
            Substitute<ISaveCaseWorkspace>(services, store);
        });
        store.State = CaseLifecycleState.PostReportComplete;

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(
                "0a0b0c0d0e0f01020304050607080902",
                "Cropped a report image.",
                CropFields(fixture.OverviewOccurrenceId)));

        AssertPrg(response, store.CaseId);
        Assert.Empty(store.Saves);
        Assert.Contains(
            "role=\"alert\"",
            await workspace.GetWorkspaceAsync(),
            StringComparison.Ordinal);
    }

    /// <summary>One staged crop, as the workspace script posts it.</summary>
    private static (string Name, string Value)[] CropFields(Guid occurrenceId) =>
    [
        ("preparationEdits[0].occurrenceId", occurrenceId.ToString("D")),
        ("preparationEdits[0].expectedPreparationVersion", "4"),
        ("preparationEdits[0].role", nameof(CaseAssetReportRole.Overview)),
        ("preparationEdits[0].rotation", "0"),
        ("preparationEdits[0].cropLeft", "0.05"),
        ("preparationEdits[0].cropTop", "0.1"),
        ("preparationEdits[0].cropWidth", "0.5"),
        ("preparationEdits[0].cropHeight", "0.6")
    ];
    /// <summary>
    /// v28 P50: an image has one place. Its report role, its order and the
    /// tools that change them are on its tile under Files, with the count of
    /// what the report uses beneath the grid; the Report section carries no
    /// image surface at all.
    /// </summary>
    [Fact]
    public async Task TheImageTileCarriesItsReportRoleAndTheReportSectionCarriesNoImages()
    {
        var fixture = new PreparedImages();
        var store = fixture.Store();
        store.CaseDocuments =
        [
            store.CaseDocuments[0],
            store.CaseDocuments[1],
            store.CaseDocuments[3],
            store.CaseDocuments[2],
            store.CaseDocuments[4]
        ];
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            Substitute<ICaseAssetPreparationQueries>(services, store);
        });

        var html = await workspace.GetWorkspaceAsync();
        var panel = Section(html, "section-report-title");
        Assert.DoesNotContain("data-report-images", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("data-report-preparation", panel, StringComparison.Ordinal);
        Assert.DoesNotContain(CloseUpFileName, panel, StringComparison.Ordinal);

        var files = await GetFilesFragmentAsync(workspace, html);
        var tiles = ImageGrid(files);
        // Every readable image is a tile, including the unused one, with the
        // role select and the appropriate report controls.
        foreach (var fileName in new[]
        {
            CloseUpFileName, OverviewFileName, FirstSupportingFileName, SecondSupportingFileName, UnusedFileName
        })
        {
            Assert.Contains(fileName, tiles, StringComparison.Ordinal);
        }
        var tileOrder = Tiles(tiles)
            .Select(tile => new[]
            {
                CloseUpFileName, OverviewFileName, FirstSupportingFileName,
                SecondSupportingFileName, UnusedFileName
            }.Single(fileName => tile.Contains(fileName, StringComparison.Ordinal)))
            .ToArray();
        Assert.Equal(
            new[] { CloseUpFileName, OverviewFileName, FirstSupportingFileName, SecondSupportingFileName, UnusedFileName },
            tileOrder);
        Assert.Equal(5, Regex.Count(tiles, "data-preparation-role-select"));
        Assert.Equal(5, Regex.Count(tiles, "data-image-full-page"));
        Assert.Equal(5, Regex.Count(tiles, "data-image-remove"));
        Assert.Equal(5, Regex.Count(tiles, "data-preparation-full-page="));
        foreach (var occurrenceId in new[]
        {
            fixture.CloseUpOccurrenceId,
            fixture.OverviewOccurrenceId,
            fixture.FirstSupportingOccurrenceId,
            fixture.SecondSupportingOccurrenceId
        })
        {
            AssertImageActionVisibility(Card(tiles, occurrenceId), visible: true);
        }
        AssertImageActionVisibility(Card(tiles, fixture.UnusedOccurrenceId), visible: false);
        Assert.Contains("data-image-report-count>4 of 5 in report<", files, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheImageCountIncludesCaseImagesWithoutPreparationCards()
    {
        var fixture = new PreparedImages();
        var store = fixture.Store();
        store.CaseDocuments =
        [
            .. store.CaseDocuments,
            Document(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "pending.jpg",
                "image/jpeg",
                DocumentSemanticRole.Image,
                custody: DocumentCustodyStatus.Pending)
        ];
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            Substitute<ICaseAssetPreparationQueries>(services, store);
        });

        var html = await workspace.GetWorkspaceAsync();
        var files = await GetFilesFragmentAsync(workspace, html);

        Assert.Contains("data-image-tile", files, StringComparison.Ordinal);
        Assert.Contains("data-image-report-count>4 of 6 in report<", files, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheInitialCaseResponseLoadsReportImageOfferWhileFilesIsDeferred()
    {
        var store = new PreparedImages().Store();
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<IGetAssessmentAccess>(services, store);
            Substitute<ICaseAssetPreparationQueries>(services, store);
            Substitute<ICaseReportSnapshotSource>(services,
                new AssessmentReportDraftWebTests.FakeProjectionSource(
                    AssessmentReportDraftWebTests.ReadyInput(store.CaseId)));
        });

        var html = await workspace.GetWorkspaceAsync();

        Assert.Contains("data-lazy=\"files\"", html, StringComparison.Ordinal);
        Assert.Contains("data-report-preview-images", html, StringComparison.Ordinal);
    }
    /// <summary>
    /// Without the Case's edit lease a tile states the report role it holds
    /// and offers nothing that would change it: no select, no tool, no form.
    /// </summary>
    [Fact]
    public async Task TheImageTilesCarryNoReportControlWithoutTheLease()
    {
        var fixture = new PreparedImages();
        var store = fixture.Store();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<IGetAssessmentWorkspace>(services, store);
                Substitute<ICaseAssetPreparationQueries>(services, store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=files");
        var tiles = ImageGrid(html);

        Assert.Contains("data-image-report-read", tiles, StringComparison.Ordinal);
        Assert.DoesNotContain("data-preparation-role-select", tiles, StringComparison.Ordinal);
        Assert.DoesNotContain("data-image-full-page", tiles, StringComparison.Ordinal);
        Assert.DoesNotContain("data-image-remove", tiles, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=SaveAssetPreparation", tiles, StringComparison.Ordinal);

        var panel = Section(html, "section-report-title");
        Assert.DoesNotContain("data-report-images", panel, StringComparison.Ordinal);
    }

    /// <summary>
    /// U5: each editable card supplies the state the workspace script stages
    /// into the one Case Save form. The cards deliberately contain no local
    /// post shape or command because crop, reset, rotation and role changes
    /// are all saved with the Case.
    /// </summary>
    [Fact]
    public async Task TheEditableTilesStagePreparationForTheSingleCaseSave()
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

        foreach (var panel in new[] { tiles })
        {
            Assert.Contains("data-preparation-card", panel, StringComparison.Ordinal);
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
                    "data-image-full-page",
                    "data-image-remove",
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
    /// The Files section's image grid and nothing around it, so an assertion
    /// about the tiles is never answered by the document rows or the upload
    /// requests beside them.
    /// </summary>
    private static string ImageGrid(string html)
    {
        var marker = html.IndexOf("data-image-grid>", StringComparison.Ordinal);
        Assert.True(marker >= 0, "The Images tab grid is not rendered.");
        var start = html.LastIndexOf("<ul", marker, StringComparison.Ordinal);
        var end = html.IndexOf("</ul>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The Images tab grid is not closed.");
        return html[start..end];
    }

    /// <summary>
    /// The grid's tiles, one string each, so an assertion about one tile is
    /// never answered by its neighbour. Tiles do not nest.
    /// </summary>
    private static string[] Tiles(string grid) =>
    [
        .. grid.Split("<li", StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
    ];

    /// <summary>
    /// One occurrence's tile within a section. v28 P50 moved the report role
    /// and its tools onto the image tile itself, so a card is the tile's own
    /// list item.
    /// </summary>
    private static string Card(string panel, Guid occurrenceId)
    {
        var marker = panel.IndexOf($"data-preparation-occurrence=\"{occurrenceId:D}\"", StringComparison.Ordinal);
        Assert.True(marker >= 0, $"The card for '{occurrenceId:D}' is not rendered.");
        var start = panel.LastIndexOf("<li", marker, StringComparison.Ordinal);
        var end = panel.IndexOf("</li>", marker, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"The card for '{occurrenceId:D}' is not closed.");
        return panel[start..(end + "</li>".Length)];
    }

    private static void AssertImageActionVisibility(string card, bool visible)
    {
        var fullPage = Regex.Match(card, "<button[^>]*data-image-full-page[^>]*>", RegexOptions.CultureInvariant);
        var remove = Regex.Match(card, "<button[^>]*data-image-remove[^>]*>", RegexOptions.CultureInvariant);
        Assert.True(fullPage.Success, "The Full page action is not rendered.");
        Assert.True(remove.Success, "The Remove action is not rendered.");
        if (visible)
        {
            Assert.DoesNotContain("hidden", fullPage.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("hidden", remove.Value, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("hidden", fullPage.Value, StringComparison.Ordinal);
            Assert.Contains("hidden", remove.Value, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The report-preparation queries the workspace reads. The query answers
    /// in the order the persisted store answers — role, then supporting order
    /// — so the page is exercised against the shape it really receives.
    /// </summary>


    [Fact]
    public async Task TheLazyFilesFragmentOffersImagePreparationFromTheHeldLeaseNotAssessmentAccess()
    {
        var store = new PreparedImages().Store();
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<ICaseAssetPreparationQueries>(services, store);
            services.RemoveAll<IGetAssessmentAccess>();
            services.AddSingleton<IGetAssessmentAccess>(new FakeGetAssessmentAccess(canOpen: false));
        });
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/Cases/{store.CaseId:D}/Section?section=files");
        request.Headers.Add("X-Pegasus-Edit-Lease", store.LeaseToken);

        using var response = await workspace.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var fragment = await response.Content.ReadAsStringAsync();

        Assert.Contains("image-tile", fragment, StringComparison.Ordinal);
        Assert.Contains("data-preparation-crop", fragment, StringComparison.Ordinal);
        Assert.DoesNotContain("report-images", fragment, StringComparison.Ordinal);
    }

    /// <summary>
    /// The case history table shows the resolved actor name, never the
    /// raw actor subject id (docs/design/README.md:168) — a Staff row shows its
    /// username and an Automation row shows the client label, not either GUID.
    /// </summary>
}
