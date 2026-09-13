using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        Assert.Contains($"{snapshot.Signatory.PrintedName} — {snapshot.Signatory.Qualifications}", text, StringComparison.Ordinal);
        Assert.DoesNotContain("TOTAL DUE", text, StringComparison.Ordinal);
        for (var page = 1; page <= pages.Length; page++)
        {
            Assert.Contains($"Page {page} of {pages.Length}", pages[page - 1], StringComparison.Ordinal);
            Assert.Contains($"{snapshot.Vehicle.Registration} · {snapshot.OurReference}", pages[page - 1], StringComparison.Ordinal);
        }
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
        // The separate fee-note document is exactly the fee note.
        Assert.Equal(combined.PageCount - plain.PageCount, separate.PageCount);
        Assert.Contains("TOTAL DUE", string.Join(" ", PageTexts(separate.Pdf)), StringComparison.Ordinal);
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
                    new ReportImpact("Right rear door", "Moderate", "Creased below the swage line", "right_rear_door"),
                    new ReportImpact("Wheel right rear", "Light", "Kerbed rim", "wheel_right_rear"),
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

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private static ServiceProvider RendererProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPegasusInfrastructure((_, options) =>
            options.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=renderer;Trusted_Connection=True"));
        services.AddPegasusReportRendering();
        return services.BuildServiceProvider();
    }
}
