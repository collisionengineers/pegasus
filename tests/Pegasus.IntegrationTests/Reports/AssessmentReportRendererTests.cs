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
using static Pegasus.IntegrationTests.Reports.PrintedPages;

namespace Pegasus.IntegrationTests.Reports;

/// <summary>
/// The integrated renderer (ADR-0050) against the ready fixture: the three
/// artifact shapes render to real pages whose text carries the accepted
/// wording, the images stand in their slots, the provenance the port
/// promises holds, and an input the snapshot or the renderer cannot print
/// fails closed. The layout against the template is
/// <see cref="AssessmentReportTemplateConformanceTests"/>'s.
/// </summary>
public sealed partial class AssessmentReportRendererTests
{
    /// <summary>
    /// The damage diagram (28 September 2026): each damage prints as the one
    /// yellow comic burst over its disc, as drawn however wide, unclipped and
    /// unnumbered, whatever its severity.
    /// </summary>
    [Fact]
    public void EachDamagePrintsAsTheOneUnclippedBurst()
    {
        var wide = new ReportImpact(
            ["left_side", "right_side"], "moderate", new DamageDisc(0.5, 0.5, DamageAreaGeometry.MaxRadius));
        var light = new ReportImpact(["left_front"], "light");
        var heavy = new ReportImpact(["right_rear"], "heavy");

        var svg = DamagePlanDrawing.Svg(DamagePlanGeometry.Car, [wide, light, heavy]);

        foreach (var impact in new[] { wide, light, heavy })
        {
            var disc = DamagePlanGeometry.Disc(impact.Codes, impact.Disc)!;
            Assert.Contains(
                $"<path fill=\"#ffeb69\" fill-opacity=\"0.96\" stroke=\"#1d1d1d\" stroke-width=\"3.2\" stroke-linejoin=\"round\" d=\"{DamagePlanGeometry.BurstPath(disc)}\"/>",
                svg,
                StringComparison.Ordinal);
        }
        foreach (var banned in new[] { "clip-path", "clipPath", "<circle", "<text" })
        {
            Assert.DoesNotContain(banned, svg, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The drawing is the recorded vehicle's (28 September 2026): the report
    /// draws the Case page's plan of the car, the van or the motorbike in its
    /// flat colours, turned on its side with the front at the left, with no
    /// words.
    /// </summary>
    [Theory]
    [InlineData(DamagePlanGeometry.Car)]
    [InlineData(DamagePlanGeometry.Van)]
    [InlineData(DamagePlanGeometry.Motorbike)]
    public void TheDamageDiagramIsTheVehiclesPlanTurnedOnItsSide(string profile)
    {
        var svg = DamagePlanDrawing.Svg(profile, [new ReportImpact(["right_rear"], "moderate")]);

        // The plan is 240 wide and 434 tall; turned, it is 434 by 240.
        Assert.Equal("0 0 240 434", DamagePlanGeometry.ViewBox);
        Assert.Contains("viewBox=\"0 0 434 240\"", svg, StringComparison.Ordinal);
        // A quarter turn and nothing else, so left stays left.
        Assert.StartsWith(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 434 240\"><g transform=\"translate(0 240) rotate(-90)\">",
            svg,
            StringComparison.Ordinal);
        Assert.Contains(DamagePlanGeometry.FlatArtwork(profile), svg, StringComparison.Ordinal);
        foreach (var banned in new[] { "<text", "Gradient", "<filter", "filter=", "url(#", "FRONT", "REAR" })
        {
            Assert.DoesNotContain(banned, svg, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// When no damage names a plan area the plan prints with no marks
    /// (operator, 27 September 2026).
    /// </summary>
    [Fact]
    public void ADamageThatNamesNoPlanAreaLeavesThePlanUnmarked()
    {
        var svg = DamagePlanDrawing.Svg(DamagePlanGeometry.Van, [new ReportImpact(["underside"], "light")]);

        Assert.DoesNotContain(DamageBurst.Fill, svg, StringComparison.Ordinal);
        Assert.Contains(DamagePlanGeometry.FlatArtwork(DamagePlanGeometry.Van), svg, StringComparison.Ordinal);
        Assert.Equal(svg, DamagePlanDrawing.Svg(DamagePlanGeometry.Van, []));
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
        // Page 1, the narrative, the vehicle data, the work lists, the images
        // and the statement of truth each start a page.
        Assert.Equal(6, artifact.PageCount);

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
            // The company block runs on every page, and the VAT number on none of the report's.
            Assert.Contains(AssessmentReportWording.CompanyName, pages[page - 1], StringComparison.Ordinal);
            Assert.Contains(AssessmentReportWording.CompanyWebsite, pages[page - 1], StringComparison.Ordinal);
            Assert.DoesNotContain(AssessmentReportWording.VatNumberLine, pages[page - 1], StringComparison.Ordinal);
        }
        Assert.Contains(AssessmentReportWording.VehicleDetailsHeading, pages[0], StringComparison.Ordinal);
        Assert.Contains(presentation.SettlementText, pages[1], StringComparison.Ordinal);
        Assert.Contains(AssessmentReportWording.RepairCostHeading, pages[2], StringComparison.Ordinal);
        Assert.Contains(AssessmentReportWording.StatementOfTruthHeading, pages[^1], StringComparison.Ordinal);
    }

    /// <summary>
    /// The rows the template lacks are not printed (operator, 27 September
    /// 2026): the damage tables, tyres and seat belts, the settlement facts
    /// and the further vehicle details.
    /// </summary>
    [Fact]
    public async Task TheReportPrintsNoRowTheTemplateLacks()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();

        var artifact = await new GenerateAssessmentReportDraft(renderer)
            .ExecuteAsync(ReadySnapshot(), CaseReportArtifactKind.AssessmentReport);

        var text = string.Join(" ", PageTexts(artifact.Pdf));
        foreach (var gone in new[]
        {
            "Tyres and Seat Belts", "Tyre / Belt", "Spare Tyre", "Centre Belt", "Severity", "Paint / Material Transfer",
            "Unrelated Damage Deduction", "Transmission", "Colour / Body", "Tax Expiry", "MOT Expiry",
            "Airbags Deployed", "Temporary Repair", "Excess", "Betterment", "Claimant VAT Registered", "Reserve",
            "Equity", "Agreed Contract Sum", "Repair Delays", "Storage Per Day", "Hire Start", "Diminution",
            "Salvage Agent", "Owner Retains Salvage", "Paint Hours", "Panel Labour", "FRONT", "REAR",
        })
        {
            Assert.DoesNotContain(gone, text, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A work list flows as a table with its title row repeated
    /// (DESIGN_SPEC.md, fixed slots): a list that runs onto another page
    /// begins that page at the top of the body, under its title again. A
    /// list with no items is not printed.
    /// </summary>
    [Fact]
    public async Task ALongWorkListRunsOnUnderItsTitleAndAnEmptyOneIsNotPrinted()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var snapshot = ReadySnapshot() with
        {
            NewParts = [],
            Operations = [.. Enumerable.Range(1, 90).Select(index => $"Operation {index:00}")],
        };

        var artifact = await new GenerateAssessmentReportDraft(renderer)
            .ExecuteAsync(snapshot, CaseReportArtifactKind.AssessmentReport);

        var texts = PageTexts(artifact.Pdf);
        Assert.DoesNotContain(
            texts, page => page.Contains(AssessmentReportWording.NewPartsHeading, StringComparison.Ordinal));
        var listed = Enumerable.Range(0, texts.Length)
            .Where(page => texts[page].Contains(AssessmentReportWording.OperationsHeading, StringComparison.Ordinal))
            .ToArray();
        // Forty-five rows of two: more than two pages hold.
        Assert.Equal(3, listed.Length);
        for (var index = 1; index <= 90; index++)
        {
            Assert.Equal(1, Regex.Count(string.Join(" ", texts), $"Operation {index:00}"));
        }
        // Each page it runs onto opens with the red title row, at the top of the body.
        var printed = Read(artifact.Pdf);
        Assert.All(listed.Skip(1), page =>
        {
            var title = printed[page].Shapes
                .Where(shape => shape.Fill == "#c80a32" && shape.Height > 1)
                .MinBy(shape => shape.Top)!;
            Assert.InRange(title.Top, 38.8, 39.2);
            Assert.InRange(title.Left, 22.0, 22.3);
            Assert.InRange(title.Right, 187.7, 188.0);
        });
    }

    /// <summary>
    /// v28 P22: the image pack is the included images alone, on the report's
    /// own page, with none of the report's narrative or its statement of
    /// truth.
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
        Assert.Contains(Squeeze(AssessmentReportWording.ImagePackTitle), Squeeze(text), StringComparison.Ordinal);
        Assert.Contains($"{snapshot.Vehicle.Registration} · {snapshot.OurReference}", text, StringComparison.Ordinal);
        Assert.Contains(AssessmentReportWording.CompanyName, text, StringComparison.Ordinal);
        Assert.DoesNotContain(AssessmentReportContract.StatementOfTruth1, text, StringComparison.Ordinal);
        Assert.DoesNotContain("TOTAL DUE", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Repair Cost Calculation", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The image pack uses the report's own image pages (operator, 27
    /// September 2026): six images to a page, the Close-up among them.
    /// </summary>
    [Theory]
    [InlineData(5, 1)]
    [InlineData(6, 1)]
    [InlineData(7, 2)]
    [InlineData(13, 3)]
    public async Task TheImagePackPrintsSixOrdinaryImagesToAPage(int images, int pages)
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var snapshot = ReadySnapshot();
        var photo = snapshot.Photos.Single();
        var photos = Enumerable.Range(0, images)
            .Select(index => photo with
            {
                CustodyReference = $"site-{index}.jpg",
                Order = index,
                Role = index == 0 ? CaseAssetReportRole.CloseUp : CaseAssetReportRole.Supporting,
            })
            .ToArray();

        var artifact = await new GenerateAssessmentReportDraft(renderer)
            .ExecuteAsync(snapshot with { Photos = photos }, CaseReportArtifactKind.ImagePack);

        Assert.Equal(pages, artifact.PageCount);
        Assert.Equal(images, BodyImages(artifact.Pdf).Sum(page => page.Count));
        Assert.All(BodyImages(artifact.Pdf).SelectMany(page => page), image =>
        {
            Assert.InRange(image.Width, 80.2, 80.6);
            Assert.InRange(image.Height, 47.8, 48.2);
        });
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
        Assert.Contains(Squeeze(AssessmentReportWording.ImagePackTitle), Squeeze(text), StringComparison.Ordinal);
        Assert.DoesNotContain(Squeeze(AssessmentReportWording.FeeNoteTitle), Squeeze(text), StringComparison.Ordinal);
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
        Assert.Contains(Squeeze(AssessmentReportWording.FeeNoteTitle), Squeeze(text), StringComparison.Ordinal);
        Assert.Contains(AssessmentReportWording.BillToLabel, text, StringComparison.Ordinal);
        Assert.Contains(AssessmentReportWording.PaymentDetailsHeading, text, StringComparison.Ordinal);
        Assert.Contains(AssessmentReportWording.ThankYou, text, StringComparison.Ordinal);
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
        Assert.Contains(AssessmentReportWording.BillToLabel, last, StringComparison.Ordinal);
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

        Assert.Equal(6, artifact.PageCount);
        // The Close-up leads page 1 in its slot, turned and cropped as the
        // Engineer left it and then trimmed to the slot's shape.
        var lead = Assert.Single(BodyImages(artifact.Pdf)[0]);
        Assert.InRange(lead.Width, 80.2, 80.6);
        Assert.InRange(lead.Height, 35.8, 36.2);
    }

    /// <summary>
    /// Six images to a page, two across and three down, and an image flagged
    /// Full page alone on a page of its own (operator, 27 September 2026).
    /// </summary>
    [Fact]
    public async Task OrdinaryImagesPrintSixToAPageAndAFullPageImageAlone()
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

        var seven = await draft.ExecuteAsync(
            ReadySnapshot() with { Photos = [.. Enumerable.Range(1, 7).Select(index => Photo(index))] },
            CaseReportArtifactKind.AssessmentReport);
        Assert.Equal(2, VehicleImagePageCount(seven.Pdf));
        var pages = ImagePages(seven.Pdf);
        Assert.Equal([6, 1], pages.Select(page => page.Count));
        // Two across and three down: the frames stand at the body's left
        // edge and at 107.5 mm, 54.8 mm apart down the page.
        (double Left, double Top)[] slots =
        [
            (22.1, 52.2), (107.5, 52.2), (22.1, 107.0), (107.5, 107.0), (22.1, 161.9), (107.5, 161.9),
        ];
        Assert.All(pages[0].Zip(slots), pair =>
        {
            Assert.InRange(pair.First.Left, pair.Second.Left - 0.2, pair.Second.Left + 0.2);
            Assert.InRange(pair.First.Top, pair.Second.Top - 0.2, pair.Second.Top + 0.2);
        });
        // The page after it begins at the top of the body, with no heading.
        Assert.InRange(Assert.Single(pages[1]).Top, 38.8, 39.2);
        Assert.All(pages.SelectMany(page => page), image =>
        {
            Assert.InRange(image.Width, 80.2, 80.6);
            Assert.InRange(image.Height, 47.8, 48.2);
        });

        var isolated = await draft.ExecuteAsync(
            ReadySnapshot() with { Photos = [Photo(1), Photo(2), Photo(3, fullPage: true), Photo(4)] },
            CaseReportArtifactKind.AssessmentReport);
        Assert.Equal(3, VehicleImagePageCount(isolated.Pdf));
        Assert.Equal([2, 1, 1], ImagePages(isolated.Pdf).Select(page => page.Count));
    }

    /// <summary>
    /// An image flagged Full page is fitted whole into the body of its
    /// page: nothing is trimmed from it and it is not enlarged.
    /// </summary>
    [Fact]
    public async Task AFullPageImageKeepsItsWholeCropWithinTheBody()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var tall = Bitmap(2400, 3200, SKEncodedImageFormat.Jpeg);
        var photo = new ReportImageEvidence(
            "tall.jpg", "image/jpeg", tall, Convert.ToHexStringLower(SHA256.HashData(tall)), FullPage: true);

        var artifact = await new GenerateAssessmentReportDraft(renderer).ExecuteAsync(
            ReadySnapshot() with { Photos = [photo] }, CaseReportArtifactKind.AssessmentReport);

        var image = Assert.Single(Assert.Single(ImagePages(artifact.Pdf)));
        // Its longest edge is 2000 px and its shape is its own, three to four.
        Assert.Equal((1500, 2000), (image.PixelWidth, image.PixelHeight));
        Assert.InRange(image.Width / image.Height, 0.749, 0.751);
        Assert.InRange(image.Left, 22.0, 187.9);
        Assert.InRange(image.Left + image.Width, 22.0, 188.0);
        Assert.InRange(image.Top, 39, 275);
        Assert.InRange(image.Top + image.Height, 39, 275);
    }

    /// <summary>
    /// Page 1 carries the Close-up beside the damage diagram and the image
    /// pages begin with the Overview (operator, 27 September 2026). The
    /// Close-up prints on page 1 only, and Full page has no effect on it.
    /// </summary>
    [Fact]
    public async Task TheCloseUpLeadsPageOneAndTheOverviewLeadsTheImagePages()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();

        static ReportImageEvidence Photo(
            string name, int width, int height, CaseAssetReportRole role, int? order = null)
        {
            var bytes = Bitmap(width, height, SKEncodedImageFormat.Jpeg);
            return new(
                name, "image/jpeg", bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)), role, order);
        }

        // Out of order, and each of a size of its own, so each can be told apart in print.
        var snapshot = ReadySnapshot() with
        {
            Photos =
            [
                Photo("second.jpg", 640, 480, CaseAssetReportRole.Supporting, 2),
                Photo("overview.jpg", 800, 600, CaseAssetReportRole.Overview),
                Photo("close-up.jpg", 1600, 1200, CaseAssetReportRole.CloseUp) with { FullPage = true },
                Photo("first.jpg", 400, 300, CaseAssetReportRole.Supporting, 1),
            ],
        };

        var artifact = await new GenerateAssessmentReportDraft(renderer)
            .ExecuteAsync(snapshot, CaseReportArtifactKind.AssessmentReport);

        var printed = BodyImages(artifact.Pdf);
        var closeUp = Assert.Single(printed[0]);
        // 80.4 by 36 mm at the body's left edge, 1000 px across.
        Assert.Equal((1000, 448), (closeUp.PixelWidth, closeUp.PixelHeight));
        Assert.Equal(22.1, Math.Round(closeUp.Left, 1));
        Assert.InRange(closeUp.Width, 80.2, 80.6);
        Assert.InRange(closeUp.Height, 35.8, 36.2);
        var images = Assert.Single(ImagePages(artifact.Pdf));
        // The Overview, then the supporting images in the Engineer's order;
        // none is enlarged, so each prints at its own width in pixels.
        Assert.Equal([800, 400, 640], images.Select(image => image.PixelWidth));
        // The narrative, the vehicle data and the work lists carry no image,
        // and the last page carries the signature alone.
        Assert.Equal([1, 0, 0, 0, 3, 1], printed.Select(page => page.Count));
    }

    /// <summary>
    /// An image is trimmed about its centre to the shape of its slot after
    /// the Engineer's own crop, and is never enlarged.
    /// </summary>
    [Theory]
    [InlineData(1600, 1200, 1000, 597)]
    [InlineData(3000, 1000, 1000, 597)]
    [InlineData(1200, 1600, 1000, 597)]
    [InlineData(160, 120, 160, 96)]
    public async Task AnImageIsTrimmedToItsSlotAndNeverEnlarged(int width, int height, int printedWidth, int printedHeight)
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var bytes = Bitmap(width, height, SKEncodedImageFormat.Png);
        var photo = new ReportImageEvidence(
            "site.png", "image/png", bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)));

        var artifact = await new GenerateAssessmentReportDraft(renderer).ExecuteAsync(
            ReadySnapshot() with { Photos = [photo] }, CaseReportArtifactKind.AssessmentReport);

        var image = Assert.Single(Assert.Single(ImagePages(artifact.Pdf)));
        Assert.Equal((printedWidth, printedHeight), (image.PixelWidth, image.PixelHeight));
        Assert.InRange(image.Width, 80.2, 80.6);
        Assert.InRange(image.Height, 47.8, 48.2);
    }

    /// <summary>
    /// Every image the Engineer includes prints, whatever their number
    /// (operator, 24 September 2026): thirty ordinary images fill five pages
    /// of the image pack and five image pages of the report.
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

        Assert.Equal(5, imagePack.PageCount);
        Assert.Equal(5, VehicleImagePageCount(report.Pdf));
        Assert.Equal(30, ImagePages(report.Pdf).Sum(page => page.Count));
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
                    new ReportImpact(["right_side", "right_rear"], "moderate", new DamageDisc(0.8, 0.72, 0.12)),
                    new ReportImpact(["underside"], "light"),
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
        var (imagePage, statementPage) = ImagePageRange(pdf);
        return statementPage - imagePage;
    }

    /// <summary>The report's image pages: from its images heading to its statement of truth.</summary>
    private static (int First, int Statement) ImagePageRange(byte[] pdf)
    {
        var pages = PageTexts(pdf);
        var imagePage = Array.FindIndex(
            pages, page => page.Contains(AssessmentReportWording.VehicleImagesHeading, StringComparison.Ordinal));
        var statementPage = Array.FindIndex(
            pages, page => page.Contains(AssessmentReportWording.StatementOfTruthHeading, StringComparison.Ordinal));
        Assert.True(imagePage >= 0, "The report did not render the Vehicle Images section.");
        Assert.True(statementPage > imagePage, "The Statement of Truth did not follow the image pages.");
        return (imagePage, statementPage);
    }

    /// <summary>The images of the report's image pages, page by page, in print order.</summary>
    private static List<PrintedImage>[] ImagePages(byte[] pdf)
    {
        var (first, statement) = ImagePageRange(pdf);
        return BodyImages(pdf)[first..statement];
    }

    /// <summary>
    /// Each page's images beneath the running header, in print order, in
    /// millimetres from the top left of the page and in the pixels they hold.
    /// </summary>
    private static List<PrintedImage>[] BodyImages(byte[] pdf)
    {
        const double millimetres = 25.4 / 72.0;
        using var document = PdfDocument.Open(pdf);
        return document.GetPages()
            .Select(page => page.GetImages()
                .Select(image => new PrintedImage(
                    image.BoundingBox.Left * millimetres,
                    (page.Height - image.BoundingBox.Top) * millimetres,
                    image.BoundingBox.Width * millimetres,
                    image.BoundingBox.Height * millimetres,
                    image.WidthInSamples,
                    image.HeightInSamples))
                .Where(image => image.Top > 36)
                .OrderBy(image => Math.Round(image.Top))
                .ThenBy(image => image.Left)
                .ToList())
            .ToArray();
    }

    private sealed record PrintedImage(
        double Left, double Top, double Width, double Height, int PixelWidth, int PixelHeight);

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private static ServiceProvider RendererProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPegasusReportRendering();
        return services.BuildServiceProvider();
    }
}
