using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Reports;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Pegasus.IntegrationTests.Reports;

/// <summary>
/// The integrated renderer (ADR-0050) against the ready fixture: the three
/// artifact shapes render to real pages whose text carries the accepted
/// wording, the provenance the port promises holds, and an input the
/// snapshot or the renderer cannot print fails closed.
/// </summary>
public sealed partial class AssessmentReportRendererTests
{
    /// <summary>
    /// The damage diagram (23 September 2026): a drawn disc prints as drawn,
    /// however wide, and every disc is clipped to the body so none paints off
    /// the vehicle.
    /// </summary>
    [Fact]
    public void TheDamageDiagramClipsEveryDiscToTheBody()
    {
        var wide = DamageAreaGeometry.RenderDisc(["left_side", "right_side"], 124, 364, new DamageDisc(0.5, 0.5, DamageAreaGeometry.MaxRadius))!;
        var svg = AssessmentReportLayout.DiagramSvg([wide]);

        Assert.Contains("<clipPath id=\"plan-body\"><path d=\"", svg, StringComparison.Ordinal);
        var clipped = Regex.Match(svg, "<g clip-path=\"url\\(#plan-body\\)\">(.*?)</g>", RegexOptions.Singleline);
        Assert.True(clipped.Success, "The discs are not clipped to the body.");
        Assert.Contains("r=\"62\"", clipped.Groups[1].Value, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(clipped.Groups[1].Value, "<circle "));
    }

    [Fact]
    public void NoSignatoryResourceIsEmbedded()
    {
        var assembly = typeof(QuestPdfAssessmentReportRenderer).Assembly;
        Assert.DoesNotContain(
            assembly.GetManifestResourceNames(),
            name => name.Contains("brand.signatures", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheRendererPublishesItsEngineVersionWithoutRendering()
    {
        await using var provider = RendererProvider();
        await using var scope = provider.CreateAsyncScope();

        var engineVersion = scope.ServiceProvider
            .GetRequiredService<IAssessmentReportRenderer>().EngineVersion;

        Assert.StartsWith("QuestPDF/", engineVersion, StringComparison.Ordinal);
        Assert.DoesNotContain(";", engineVersion, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheAssessmentReportRendersItsAcceptedWordingWithProvenance()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var snapshot = ReadySnapshot();

        var artifact = await new GenerateAssessmentReportDraft(renderer)
            .ExecuteAsync(snapshot, CaseReportArtifactKind.AssessmentReport);

        Assert.Equal("CE_100_assessment.pdf", artifact.SuggestedFileName);
        Assert.Equal(AssessmentReportContract.TemplateVersion, artifact.TemplateVersion);
        Assert.Equal(renderer.EngineVersion, artifact.EngineVersion);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(artifact.Pdf)), artifact.Sha256);
        // Page 1, the vehicle data, the work lists, the images and the
        // statement of truth each start a page.
        Assert.True(artifact.PageCount >= 5, $"Expected at least five pages, got {artifact.PageCount}.");

        var pages = PageTexts(artifact.Pdf);
        Assert.Equal(artifact.PageCount, pages.Length);
        var text = string.Join(" ", pages);
        var presentation = snapshot.Presentation();
        Assert.Contains(snapshot.OurReference, text, StringComparison.Ordinal);
        Assert.Contains(presentation.Badge, text, StringComparison.Ordinal);
        Assert.Contains(presentation.SettlementText, text, StringComparison.Ordinal);
        Assert.Contains(AssessmentReportContract.StatementOfTruth1, text, StringComparison.Ordinal);
        Assert.Contains(AssessmentReportContract.StatementOfTruth4, text, StringComparison.Ordinal);
        Assert.DoesNotContain("VIN Checked", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Fault Codes", text, StringComparison.Ordinal);
        Assert.Contains($"{snapshot.Signatory.PrintedName} — {snapshot.Signatory.Qualifications}", text, StringComparison.Ordinal);
        Assert.DoesNotContain("TOTAL DUE", text, StringComparison.Ordinal);
        for (var page = 1; page <= pages.Length; page++)
        {
            Assert.Contains($"Page {page} of {pages.Length}", pages[page - 1], StringComparison.Ordinal);
            Assert.Contains($"{snapshot.Vehicle.Registration} · {snapshot.OurReference}", pages[page - 1], StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ReportChromeExtractionKeepsCurrentAssessmentTextAndPageCountStable()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var draft = new GenerateAssessmentReportDraft(renderer);
        var snapshot = ReadySnapshot();

        // The pre-extraction renderer is no longer present. The checked-in
        // baseline freezes reviewed current text and records that limitation.
        var current = await draft.ExecuteAsync(snapshot, CaseReportArtifactKind.AssessmentReport);
        var comparison = await draft.ExecuteAsync(snapshot, CaseReportArtifactKind.AssessmentReport);
        var pages = PageTexts(current.Pdf);
        var reviewedBaseline = File.ReadAllLines(Path.Combine(
            RepositoryRoot(), "tests", "Pegasus.IntegrationTests", "Reports", "Baselines",
            "AssessmentReportRenderer.current-text.txt"));
        var reviewedPageCount = int.Parse(
            reviewedBaseline.Single(line => line.StartsWith("pages=", StringComparison.Ordinal))["pages=".Length..],
            System.Globalization.CultureInfo.InvariantCulture);
        var reviewedText = reviewedBaseline
            .Where(line => line.Length > 0 && !line.StartsWith('#') && !line.StartsWith("pages=", StringComparison.Ordinal))
            .ToArray();
        var actualText = string.Join(" ", pages);

        Assert.Equal(7, current.PageCount);
        Assert.Equal(current.PageCount, comparison.PageCount);
        Assert.Equal(PageTexts(current.Pdf), PageTexts(comparison.Pdf));
        Assert.Equal(reviewedPageCount, pages.Length);
        Assert.All(reviewedText, expected => Assert.Contains(expected, actualText, StringComparison.Ordinal));
    }

    /// <summary>
    /// v28 P22: the image pack is the included images alone, under the
    /// report's letterhead, with none of the report's narrative or its
    /// statement of truth.
    /// </summary>
    [Fact]
    public async Task TheImagePackRendersTheIncludedImagesAlone()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var snapshot = ReadySnapshot();

        var artifact = await new GenerateAssessmentReportDraft(renderer)
            .ExecuteAsync(snapshot, CaseReportArtifactKind.ImagePack);

        Assert.Equal("CE_100_images.pdf", artifact.SuggestedFileName);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(artifact.Pdf)), artifact.Sha256);
        var text = string.Join(" ", PageTexts(artifact.Pdf));
        Assert.Contains("Vehicle Images", text, StringComparison.Ordinal);
        Assert.Contains($"{snapshot.Vehicle.Registration} · {snapshot.OurReference}", text, StringComparison.Ordinal);
        Assert.DoesNotContain(AssessmentReportContract.StatementOfTruth1, text, StringComparison.Ordinal);
        Assert.DoesNotContain("TOTAL DUE", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Repair Cost Calculation", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheImagePackPaginatesOrdinaryImagesInPairs()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var snapshot = ReadySnapshot();
        var photo = snapshot.Photos.Single();
        var photos = Enumerable.Range(0, 5)
            .Select(index => photo with { CustodyReference = $"site-{index}.jpg", Order = index })
            .ToArray();

        var artifact = await new GenerateAssessmentReportDraft(renderer)
            .ExecuteAsync(snapshot with { Photos = photos }, CaseReportArtifactKind.ImagePack);

        Assert.Equal(3, artifact.PageCount);
    }

    [Fact]
    public async Task TheImagePackDoesNotLeaveAHeaderOnlyPageBeforeAFullPageImage()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var snapshot = ReadySnapshot();
        var photo = snapshot.Photos.Single();
        var fullPage = photo with { CustodyReference = "full.jpg", Order = 0, FullPage = true };
        var ordinary = photo with { CustodyReference = "ordinary.jpg", Order = 1, FullPage = false };

        var artifact = await new GenerateAssessmentReportDraft(renderer)
            .ExecuteAsync(snapshot with { Photos = [fullPage, ordinary] }, CaseReportArtifactKind.ImagePack);

        Assert.Equal(2, artifact.PageCount);
    }

    [Fact]
    public async Task AnImagePackNeverAppendsTheFeeNoteEvenWhenTheSnapshotIncludesIt()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();

        var artifact = await new GenerateAssessmentReportDraft(renderer)
            .ExecuteAsync(ReadySnapshot() with { IncludeFeeNote = true }, CaseReportArtifactKind.ImagePack);

        var text = string.Join(" ", PageTexts(artifact.Pdf));
        Assert.Contains("Vehicle Images", text, StringComparison.Ordinal);
        Assert.DoesNotContain("FEE NOTE", text, StringComparison.Ordinal);
        Assert.DoesNotContain("TOTAL DUE", text, StringComparison.Ordinal);
        Assert.DoesNotContain($"VAT No: {AssessmentReportContract.VatNumber}", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnImagePackOfACaseWhoseReportUsesNoImageIsRefused()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var snapshot = ReadySnapshot() with { Photos = [] };

        await Assert.ThrowsAsync<ReportRenderRejectedException>(
            () => new GenerateAssessmentReportDraft(renderer)
                .ExecuteAsync(snapshot, CaseReportArtifactKind.ImagePack));
    }

    [Fact]
    public async Task TheFeeNoteRendersItsOwnDocument()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var snapshot = ReadySnapshot();

        var artifact = await new GenerateAssessmentReportDraft(renderer)
            .ExecuteAsync(snapshot, CaseReportArtifactKind.FeeNote);

        Assert.Equal("CE_100_fee_note.pdf", artifact.SuggestedFileName);
        Assert.True(artifact.PageCount >= 1);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(artifact.Pdf)), artifact.Sha256);
        var text = string.Join(" ", PageTexts(artifact.Pdf));
        Assert.Contains("FEE NOTE", text, StringComparison.Ordinal);
        Assert.Contains("TOTAL DUE", text, StringComparison.Ordinal);
        Assert.Contains(snapshot.OurReference, text, StringComparison.Ordinal);
        Assert.Contains($"VAT No: {AssessmentReportContract.VatNumber}", text, StringComparison.Ordinal);
        Assert.Contains("Page 1 of", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Statement of Truth", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// R34B: with the choice made, the report's own document ends with the
    /// fee note's pages — one document, the fee note last, after a page
    /// break — and the separate fee-note document is unchanged.
    /// </summary>
    [Fact]
    public async Task TheCombinedReportEndsWithTheFeeNotePagesInOneDocument()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var draft = new GenerateAssessmentReportDraft(renderer);
        var snapshot = ReadySnapshot();

        var plain = await draft.ExecuteAsync(snapshot, CaseReportArtifactKind.AssessmentReport);
        var combined = await draft.ExecuteAsync(
            snapshot with { IncludeFeeNote = true }, CaseReportArtifactKind.AssessmentReport);
        var separate = await draft.ExecuteAsync(
            snapshot with { IncludeFeeNote = true }, CaseReportArtifactKind.FeeNote);

        Assert.Equal("CE_100_assessment.pdf", combined.SuggestedFileName);
        Assert.True(combined.PageCount > plain.PageCount);
        var pages = PageTexts(combined.Pdf);
        var last = pages[^1];
        Assert.Contains("TOTAL DUE", last, StringComparison.Ordinal);
        Assert.Contains("BILL TO:", last, StringComparison.Ordinal);
        Assert.Contains($"Page {pages.Length} of {pages.Length}", last, StringComparison.Ordinal);
        // The report is first and complete: its final page still closes with
        // the signatory, and no report page carries the fee note's totals.
        var reportPages = pages.Take(plain.PageCount).ToArray();
        Assert.Contains(snapshot.Signatory.PrintedName, reportPages[^1], StringComparison.Ordinal);
        Assert.All(reportPages, page => Assert.DoesNotContain("TOTAL DUE", page, StringComparison.Ordinal));
        Assert.All(reportPages, page => Assert.DoesNotContain(
            $"VAT No: {AssessmentReportContract.VatNumber}", page, StringComparison.Ordinal));
        Assert.All(pages.Skip(plain.PageCount), page => Assert.Contains(
            $"VAT No: {AssessmentReportContract.VatNumber}", page, StringComparison.Ordinal));
        // The separate fee-note document is exactly the fee note.
        Assert.Equal(combined.PageCount - plain.PageCount, separate.PageCount);
        Assert.Contains("TOTAL DUE", string.Join(" ", PageTexts(separate.Pdf)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EveryPageOfAMultiPageFeeNoteUsesTheFeeFooter()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var snapshot = ReadySnapshot() with
        {
            FeeDescriptionLines = Enumerable.Range(1, 80)
                .Select(index => $"Engineering service line {index:00} with retained billing detail")
                .ToArray(),
        };

        var artifact = await new GenerateAssessmentReportDraft(renderer)
            .ExecuteAsync(snapshot, CaseReportArtifactKind.FeeNote);
        var pages = PageTexts(artifact.Pdf);

        Assert.True(artifact.PageCount > 1);
        Assert.All(pages, page => Assert.Contains(
            $"VAT No: {AssessmentReportContract.VatNumber}", page, StringComparison.Ordinal));
        Assert.Contains("TOTAL DUE", string.Join(" ", pages), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARotatedAndCroppedPhotoStillPrints()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var snapshot = ReadySnapshot();
        var rotated = snapshot.Photos[0] with
        {
            Rotation = CaseAssetRotation.Clockwise90,
            Crop = new CaseAssetCrop(0.1m, 0.2m, 0.5m, 0.6m),
            Role = CaseAssetReportRole.CloseUp,
        };

        var artifact = await new GenerateAssessmentReportDraft(renderer).ExecuteAsync(
            snapshot with { Photos = [rotated, snapshot.Photos[0]] },
            CaseReportArtifactKind.AssessmentReport);

        Assert.True(artifact.PageCount >= 5);
    }

    [Fact]
    public async Task NormalImagesArePagedInPairsAndFullPageImageIsIsolated()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var draft = new GenerateAssessmentReportDraft(renderer);
        var bytes = Bitmap(160, 120, SKEncodedImageFormat.Jpeg);
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));

        ReportImageEvidence Photo(int index, bool fullPage = false) => new(
            $"site-{index}.jpg",
            "image/jpeg",
            bytes,
            hash,
            CaseAssetReportRole.Supporting,
            index,
            CaseAssetRotation.None,
            CaseAssetCrop.Full,
            FullPage: fullPage);

        var fourNormal = ReadySnapshot() with
        {
            Photos = [Photo(1), Photo(2), Photo(3), Photo(4)],
        };
        var normalArtifact = await draft.ExecuteAsync(
            fourNormal, CaseReportArtifactKind.AssessmentReport);
        Assert.Equal(2, VehicleImagePageCount(normalArtifact.Pdf));

        var isolated = fourNormal with
        {
            Photos = [Photo(1), Photo(2), Photo(3, fullPage: true), Photo(4)],
        };
        var isolatedArtifact = await draft.ExecuteAsync(
            isolated, CaseReportArtifactKind.AssessmentReport);
        Assert.Equal(3, VehicleImagePageCount(isolatedArtifact.Pdf));
    }

    /// <summary>
    /// Every image the Engineer includes prints, whatever their number
    /// (operator, 24 September 2026): thirty ordinary images fill fifteen
    /// pages of the image pack and fifteen image pages of the report.
    /// </summary>
    [Fact]
    public async Task ThirtyImagesPrintWithoutACountLimit()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var draft = new GenerateAssessmentReportDraft(renderer);
        var snapshot = ReadySnapshot();
        var photo = snapshot.Photos.Single();
        var photos = Enumerable.Range(0, 30)
            .Select(index => photo with { CustodyReference = $"site-{index}.jpg", Order = index })
            .ToArray();

        var imagePack = await draft.ExecuteAsync(
            snapshot with { Photos = photos }, CaseReportArtifactKind.ImagePack);
        var report = await draft.ExecuteAsync(
            snapshot with { Photos = photos }, CaseReportArtifactKind.AssessmentReport);

        Assert.Equal(15, imagePack.PageCount);
        Assert.Equal(15, VehicleImagePageCount(report.Pdf));
    }

    /// <summary>
    /// A source image's file size is no limit either (operator, 24 September
    /// 2026): an image of more than 8 MiB prints as its print-resolution copy,
    /// so the report is much smaller than the retained source it came from.
    /// </summary>
    [Fact]
    public async Task AnImageOverEightMebibytesPrintsAsAPrintResolutionCopy()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var source = NoiseJpeg(4000, 3000);
        Assert.True(source.Length > 8 * 1024 * 1024, $"The source image is only {source.Length} bytes.");
        var large = new ReportImageEvidence(
            "large.jpg", "image/jpeg", source, Convert.ToHexStringLower(SHA256.HashData(source)));

        var artifact = await new GenerateAssessmentReportDraft(renderer).ExecuteAsync(
            ReadySnapshot() with { Photos = [large] }, CaseReportArtifactKind.AssessmentReport);

        Assert.Equal(1, VehicleImagePageCount(artifact.Pdf));
        Assert.True(
            artifact.Pdf.Length < source.Length / 4,
            $"The report is {artifact.Pdf.Length} bytes for a {source.Length} byte source image.");
    }

    /// <summary>
    /// Phase 5b: the valuation commentary text frozen into the snapshot prints
    /// when the switch is on, and not when it is off.
    /// </summary>
    [Fact]
    public async Task SelectedValuationCommentaryPrintsTheRecordedText()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var draft = new GenerateAssessmentReportDraft(renderer);
        const string commentary = "Low mileage for its age; the retail guide is adjusted up.";
        var ready = ReadySnapshot();

        var selected = await draft.ExecuteAsync(
            ready with { Content = ready.Content with { IncludeValuationCommentary = true }, ValuationCommentary = commentary },
            CaseReportArtifactKind.AssessmentReport);
        var unselected = await draft.ExecuteAsync(
            ready with { Content = ready.Content with { IncludeValuationCommentary = false }, ValuationCommentary = commentary },
            CaseReportArtifactKind.AssessmentReport);

        Assert.Contains(commentary, string.Join(" ", PageTexts(selected.Pdf)), StringComparison.Ordinal);
        Assert.DoesNotContain(commentary, string.Join(" ", PageTexts(unselected.Pdf)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnInvalidSnapshotFailsClosedBeforeRendering()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var draft = new GenerateAssessmentReportDraft(renderer);

        await Assert.ThrowsAsync<ReportRenderRejectedException>(() => draft.ExecuteAsync(
            ReadySnapshot() with { ReportFor = [] }, CaseReportArtifactKind.AssessmentReport));
        await Assert.ThrowsAsync<ReportRenderRejectedException>(() => draft.ExecuteAsync(
            ReadySnapshot() with { Vehicle = ReadySnapshot().Vehicle with { MileageSource = "guess" } },
            CaseReportArtifactKind.AssessmentReport));
    }

    /// <summary>
    /// Bytes that pass custody but do not decode as an image never print as a
    /// placeholder frame: the render is refused, naming the image.
    /// </summary>
    [Fact]
    public async Task AnUndecodableImageFailsTheRenderClosed()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var snapshot = ReadySnapshot();
        var garbage = new byte[] { 137, 80, 78, 71, 1, 2, 3, 4 };
        var broken = new ReportImageEvidence(
            "broken.jpg", "image/jpeg", garbage, Convert.ToHexStringLower(SHA256.HashData(garbage)));

        var rejection = await Assert.ThrowsAsync<ReportRenderRejectedException>(() =>
            renderer.RenderAsync(snapshot with { Photos = [broken] }, CaseReportArtifactKind.AssessmentReport));

        Assert.Contains("broken.jpg", rejection.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The ready web fixture with images the page can actually print: the
    /// projection's snapshot, its photo and signature replaced by decodable
    /// bytes under their own custody hashes, and two coded impacts so the
    /// marked damage diagram is drawn.
    /// </summary>
    internal static AssessmentReportSnapshot ReadySnapshot()
    {
        var snapshot = AssessmentReportProjection
            .Project(AssessmentReportDraftWebTests.ReadyInput(Guid.NewGuid())).Snapshot!;
        var photo = Bitmap(160, 120, SKEncodedImageFormat.Jpeg);
        var signature = Bitmap(300, 80, SKEncodedImageFormat.Png);
        return snapshot with
        {
            Damage = snapshot.Damage with
            {
                Impacts =
                [
                    new ReportImpact("RH Side, RH Rear", "Moderate", "Creased below the swage line", ["right_side", "right_rear"], new DamageDisc(0.8, 0.72, 0.12)),
                    new ReportImpact("Underside", "Light", "Exhaust hanger bent", ["underside"]),
                ],
            },
            Photos =
            [
                new ReportImageEvidence(
                    "site.jpg", "image/jpeg", photo, Convert.ToHexStringLower(SHA256.HashData(photo))),
            ],
            Signatory = snapshot.Signatory with
            {
                SignatureContent = signature,
                SignatureContentType = "image/png",
            },
        };
    }

    private static byte[] Bitmap(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.DarkSlateBlue };
            canvas.DrawRect(new SKRect(0, 0, width / 2f, height / 2f), paint);
            paint.Color = SKColors.Firebrick;
            canvas.DrawRect(new SKRect(width / 2f, height / 2f, width, height), paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, 90);
        return encoded.ToArray();
    }

    /// <summary>
    /// A photograph-sized JPEG of seeded random noise at full quality. Noise
    /// barely compresses, so the file is as large as a camera original can be.
    /// </summary>
    private static byte[] NoiseJpeg(int width, int height)
    {
        var pixels = new byte[checked(width * height * 4)];
        new Random(834).NextBytes(pixels);
        for (var alpha = 3; alpha < pixels.Length; alpha += 4)
        {
            pixels[alpha] = 255;
        }
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 100);
        return encoded.ToArray();
    }

    /// <summary>
    /// Each page's text with whitespace normalised, so a phrase that wraps
    /// across lines is still one phrase.
    /// </summary>
    private static string[] PageTexts(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return document.GetPages()
            .Select(page => WhitespaceRegex().Replace(ContentOrderTextExtractor.GetText(page), " ").Trim())
            .ToArray();
    }

    private static int VehicleImagePageCount(byte[] pdf)
    {
        var pages = PageTexts(pdf);
        var imagePage = Array.FindIndex(
            pages, page => page.Contains("Vehicle Images", StringComparison.Ordinal));
        var statementPage = Array.FindIndex(
            pages, page => page.Contains("Statement of Truth", StringComparison.Ordinal));
        Assert.True(imagePage >= 0, "The report did not render the Vehicle Images section.");
        Assert.True(statementPage > imagePage, "The Statement of Truth did not follow the image pages.");
        return statementPage - imagePage;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Pegasus.slnx")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static ServiceProvider RendererProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPegasusReportRendering();
        return services.BuildServiceProvider();
    }
}
