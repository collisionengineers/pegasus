using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Intake;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class MultiFormatIntakeWebTests
{
    [Fact]
    public async Task DirectDocxTextCannotEstablishQdosThroughWebCaller()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var docx = CreateDocx(
            "QDOS instruction",
            "Claim Number: SYN-DOCX-001",
            "Vehicle Registration: AB12 CDE",
            "Claimant Name: Synthetic Person");

        var result = await UploadAsync(factory, client, "synthetic-instruction.docx",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        docx);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Null(receipt.InstructionDraft);
        Assert.Empty(receipt.Fields);
        Assert.Contains("synthetic-instruction.docx", reviewHtml, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Browser")]
    public async Task PinnedChromiumRendersActualIntakeReviewWithoutAutomatedAxeViolations()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var docx = CreateDocx(
            "QDOS instruction",
            "Claim Number: SYN-BROWSER-001",
            "Vehicle Registration: AB12 CDE",
            "Claimant Name: Synthetic Person");

        var result = await UploadAsync(factory, client, "synthetic-browser-instruction.docx",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        docx);
        var reviewHtml = await GetReviewHtmlAsync(client, result);
        var stylesheet = await client.GetStringAsync("/css/site.css");
        var styledReviewHtml = reviewHtml.Replace(
            "</head>",
            $"<style>{stylesheet}</style></head>",
            StringComparison.Ordinal);

        var violationIds = await OfflineBrowserAxe.FindViolationIdsAsync(styledReviewHtml);

        Assert.Empty(violationIds);
    }

    [Theory]
    [InlineData("legacy-instruction.doc", "application/msword")]
    [InlineData("outlook-message.msg", "application/vnd.ms-outlook")]
    public async Task DeferredLegacyContainersAreAcceptedIntoNeedsSortingWithoutReference(
        string fileName,
        string mediaType)
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);

        var result = await UploadAsync(factory, client, fileName, mediaType, CreateOleHeader());
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Null(receipt.FailureCode);
        Assert.Contains(receipt.AssetRecords, asset => asset.FileName == fileName && asset.Kind == IntakeAssetKind.Source);
        Assert.Contains(fileName, reviewHtml, StringComparison.Ordinal);
        Assert.Contains("Needs sorting", reviewHtml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("vehicle-front.jpg", "image/jpeg", TinyJpegBase64)]
    [InlineData("vehicle-side.png", "image/png", TinyPngBase64)]
    public async Task DirectImagesAreAcceptedIntoNeedsSortingWithoutOcrOrReference(
        string fileName,
        string mediaType,
        string base64)
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);

        var result = await UploadAsync(factory, client, fileName, mediaType, Convert.FromBase64String(base64));
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Null(receipt.FailureCode);
        Assert.Empty(receipt.ScannedPdfPages);
        Assert.Contains(receipt.AssetRecords, asset => asset.FileName == fileName && asset.Kind == IntakeAssetKind.Source);
        Assert.DoesNotContain(
            receipt.Evidence,
            item => item.Signal.Contains("ocr", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("Document text required", reviewHtml, StringComparison.Ordinal);
        Assert.Contains(fileName, reviewHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmailAttachmentsAndNestedMessageRetainVisibleProvenance()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var docx = CreateDocx(
            "QDOS instruction",
            "Claim Number: SYN-EML-001",
            "Vehicle Registration: AB12 CDE");
        var pdf = CreatePdf("Synthetic PDF attachment");
        var image = Convert.FromBase64String(TinyJpegBase64);
        var nested = CreateMessage(
            "Nested evidence",
            "Nested message body",
            ("nested-photo.jpg", "image/jpeg", image));
        var message = CreateQdosMessage(
            "Synthetic mixed intake",
            "Please review the attached instruction and evidence.",
            ("instruction.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", docx),
            ("supporting.pdf", "application/pdf", pdf),
            ("vehicle.jpg", "image/jpeg", image));
        AttachNestedMessage(message, "forwarded-message.eml", nested);

        var result = await UploadAsync(factory, client, "mixed-intake.eml", "message/rfc822", Serialize(message));
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);
        Assert.Contains(receipt.AssetRecords, asset => asset.FileName == "instruction.docx");
        Assert.Contains(receipt.AssetRecords, asset => asset.FileName == "supporting.pdf");
        Assert.Contains(receipt.AssetRecords, asset => asset.FileName == "vehicle.jpg");
        Assert.Contains(receipt.AssetRecords, asset => asset.FileName == "forwarded-message.eml");
        Assert.Contains(receipt.AssetRecords, asset => asset.FileName == "nested-photo.jpg");
        Assert.Contains("instruction.docx", reviewHtml, StringComparison.Ordinal);
        Assert.Contains("supporting.pdf", reviewHtml, StringComparison.Ordinal);
        Assert.Contains("vehicle.jpg", reviewHtml, StringComparison.Ordinal);
        Assert.Contains("forwarded-message.eml", reviewHtml, StringComparison.Ordinal);
        Assert.Contains("nested-photo.jpg", reviewHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExactDuplicateImageOccurrencesAreBothRetainedAndVisible()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var image = Convert.FromBase64String(TinyJpegBase64);
        var message = CreateQdosMessage(
            "Synthetic duplicate evidence",
            "QDOS instruction\r\nClaim Number: SYN-DUP-001\r\nVehicle Registration: AB12 CDE",
            ("vehicle-front-original.jpg", "image/jpeg", image),
            ("vehicle-front-copy.jpg", "image/jpeg", image));

        var result = await UploadAsync(factory, client, "duplicate-images.eml", "message/rfc822", Serialize(message));
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);
        var original = Assert.Single(
            receipt.AssetRecords,
            asset => asset.FileName == "vehicle-front-original.jpg");
        var copy = Assert.Single(
            receipt.AssetRecords,
            asset => asset.FileName == "vehicle-front-copy.jpg");
        Assert.NotEqual(original.Id, copy.Id);
        Assert.Equal(original.ContentHash, copy.ContentHash);
        Assert.Contains("vehicle-front-original.jpg", reviewHtml, StringComparison.Ordinal);
        Assert.Contains("vehicle-front-copy.jpg", reviewHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MalformedDocxProducesExplicitVisibleTerminalFailure()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);

        var result = await UploadAsync(factory, client, "malformed.docx",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "PK not a valid Open XML package"u8.ToArray());
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(IntakeDecision.Unsupported, receipt.Decision);
        // The persisted code is the contract; the screen shows the same
        // distinction in words, because the operator has to know whether the
        // document was unreadable or merely too large.
        Assert.Equal("unreadable_docx", receipt.FailureCode);
        Assert.Contains(
            "The Word document could not be read", reviewHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("unreadable_docx", reviewHtml, StringComparison.Ordinal);
        Assert.Contains("malformed.docx", reviewHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NestedMimeBeyondEightLevelsStopsThatBranchAndSurfacesLimitGuard()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var message = CreateNestedMessageChain(10);

        var result = await UploadAsync(factory, client, "too-deep.eml", "message/rfc822", Serialize(message));
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Contains(receipt.Evidence, item => item.Signal == "intake_limit_exceeded");
        Assert.Contains("nesting depth exceeds 8", reviewHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FullPageRasterWithLowTextProducesExactlyOneScannedPageOcrCandidate()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var pdf = CreateImagePdf(new PdfImagePlacement(0, 0, 612, 792, 0xff, 0xff, 0xff));

        var result = await UploadAsync(factory, client, "full-page-scan.pdf", "application/pdf", pdf);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(IntakeDecision.OcrRequired, receipt.Decision);
        var candidate = Assert.Single(receipt.ScannedPdfPages);
        Assert.Equal(1, candidate.PageNumber);
        Assert.Equal("uploaded full-page-scan.pdf", candidate.SourceLabel);
        var image = Assert.Single(
            receipt.AssetRecords,
            asset => asset.Kind == IntakeAssetKind.EmbeddedImage);
        Assert.Equal(1, image.PageNumber);
        Assert.Contains("Scanned PDF pages", reviewHtml, StringComparison.Ordinal);
        Assert.Contains("uploaded full-page-scan.pdf, page 1", reviewHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LowTextPdfWithoutDominantRasterDoesNotSelectOcr()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var pdf = CreateImagePdf(new PdfImagePlacement(20, 20, 100, 100, 0xff, 0xff, 0xff));

        var result = await UploadAsync(factory, client, "small-raster.pdf", "application/pdf", pdf);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Empty(receipt.ScannedPdfPages);
        Assert.DoesNotContain(receipt.Evidence, evidence => evidence.Signal == "scanned-pdf-page");
        Assert.Single(receipt.AssetRecords, asset => asset.Kind == IntakeAssetKind.EmbeddedImage);
        Assert.DoesNotContain("Scanned PDF pages", reviewHtml, StringComparison.Ordinal);
        Assert.Contains("not an image-led scanned page", reviewHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TwoDiscretePdfImageObjectsRemainSeparateInReceipt()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var pdf = CreateImagePdf(
            new PdfImagePlacement(20, 20, 100, 100, 0xff, 0x00, 0x00),
            new PdfImagePlacement(180, 20, 100, 100, 0x00, 0x00, 0xff));

        var result = await UploadAsync(factory, client, "two-images.pdf", "application/pdf", pdf);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var images = receipt.AssetRecords
            .Where(asset => asset.Kind == IntakeAssetKind.EmbeddedImage)
            .OrderBy(asset => asset.FileName, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Empty(receipt.ScannedPdfPages);
        Assert.Equal(2, images.Length);
        Assert.NotEqual(images[0].Id, images[1].Id);
        Assert.NotEqual(images[0].ContentHash, images[1].ContentHash);

    }

    [Fact]
    public async Task RetainedHtmlSourceDownloadsOnlyAsANoSniffAttachmentAndUnknownIdDoesNotDisclose()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var htmlSource = Encoding.UTF8.GetBytes(
            "<!doctype html><script>document.body.dataset.executed='true'</script>");
        var result = await UploadAsync(factory, client, "inbound.html", "text/html", htmlSource);
        var receiptId = ReceiptId(result);

        using var response = await client.GetAsync($"/Received/{receiptId}/Source");
        response.EnsureSuccessStatusCode();
        var downloaded = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var contentTypeOptions));
        Assert.Contains("nosniff", contentTypeOptions);
        Assert.Equal(htmlSource, downloaded);

        using var unknownResponse = await client.GetAsync($"/Received/{Guid.NewGuid()}/Source");
        var unknownBody = await unknownResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);
        Assert.DoesNotContain(receiptId.ToString(), unknownBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("inbound.html", unknownBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("document.body.dataset", unknownBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThirtyPagePdfWithConfirmingContentOnFinalPageIsClassified()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var pageTexts = Enumerable.Range(1, 30)
            .Select(pageNumber => pageNumber == 30 ? ConfirmingQdosBody : string.Empty)
            .ToArray();
        var pdf = CreateTextPdf(pageTexts);

        var result = await UploadAsync(factory, client, "thirty-page-instruction.pdf", "application/pdf", pdf);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));

        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Null(receipt.InstructionDraft);
        Assert.Empty(receipt.Fields);
        Assert.DoesNotContain(receipt.Evidence, evidence =>
            evidence.Finding == IntakeEvidenceFinding.SupportsPrincipal);
        Assert.DoesNotContain(receipt.Evidence, evidence => evidence.Signal == "intake_limit_exceeded");
    }

    [Fact]
    public async Task PdfAggregateProcessingDeadlineFailsClosedWithoutWaiting()
    {
        using var factory = new IntakeWebApplicationFactory(new SteppingTimeProvider(TimeSpan.FromSeconds(1)));
        using var client = CreateClient(factory);
        var pdf = CreateTextPdf(Enumerable.Repeat(string.Empty, 40).ToArray());
        Assert.InRange(pdf.LongLength, 1, 10L * 1024 * 1024);

        var result = await UploadAsync(factory, client, "many-blank-pages.pdf", "application/pdf", pdf);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));

        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Null(receipt.InstructionDraft);
        Assert.Contains(
            receipt.Evidence,
            evidence => evidence.Signal == "intake_limit_exceeded"
                && evidence.Detail.Contains(
                    "exceeds the safe PDF processing time limit.",
                    StringComparison.Ordinal));
    }

    [Fact]
    public async Task MoreThan512DiscretePdfImagesStopsBeforeAssetExtraction()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var images = Enumerable.Range(0, 513)
            .Select(index => new PdfImagePlacement(
                index % 600,
                index % 780,
                1,
                1,
                (byte)(index % 256),
                (byte)((index + 1) % 256),
                (byte)((index + 2) % 256)))
            .ToArray();
        var pdf = CreateImagePdf(images);
        Assert.InRange(pdf.LongLength, 1, 10L * 1024 * 1024);

        var result = await UploadAsync(factory, client, "too-many-pdf-images.pdf", "application/pdf", pdf);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));

        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Contains(receipt.Evidence, evidence => evidence.Signal == "intake_limit_exceeded");
        Assert.DoesNotContain(receipt.AssetRecords, asset => asset.Kind == IntakeAssetKind.EmbeddedImage);
    }

    [Fact]
    public async Task PdfImageSampleDimensionsOverAggregatePixelLimitStopBeforeRasterDecoding()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var pdf = CreateImagePdf(new PdfImagePlacement(
            20,
            20,
            100,
            100,
            0xff,
            0xff,
            0xff,
            SampleWidth: 10_001,
            SampleHeight: 10_000));

        var result = await UploadAsync(factory, client, "oversized-pdf-raster.pdf", "application/pdf", pdf);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));

        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Contains(receipt.Evidence, evidence => evidence.Signal == "intake_limit_exceeded");
        Assert.DoesNotContain(receipt.AssetRecords, asset => asset.Kind == IntakeAssetKind.EmbeddedImage);
    }

    [Fact]
    public async Task PdfExtractedTextOverFiveMillionCharactersStopsIntake()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var text = new string('A', (5 * 1024 * 1024) + 1);
        var pdf = CreatePdf(text);
        Assert.InRange(pdf.LongLength, 1, 10L * 1024 * 1024);

        var result = await UploadAsync(factory, client, "excessive-pdf-text.pdf", "application/pdf", pdf);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));

        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Contains(receipt.Evidence, evidence => evidence.Signal == "intake_limit_exceeded");
        Assert.Null(receipt.InstructionDraft);
    }

    [Fact]
    public async Task PdfImageObjectBudgetIsSharedAcrossEmailAttachmentsAndConfirmingBodyFailsClosed()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var images = Enumerable.Range(0, 300)
            .Select(index => new PdfImagePlacement(
                index % 600,
                index % 780,
                1,
                1,
                (byte)(index % 256),
                (byte)((index + 1) % 256),
                (byte)((index + 2) % 256)))
            .ToArray();
        var firstPdf = CreateImagePdf(images);
        var secondPdf = CreateImagePdf(images);
        var message = CreateMessage(
            "Synthetic shared PDF image budget",
            ConfirmingQdosBody,
            ("first-300-images.pdf", "application/pdf", firstPdf),
            ("second-300-images.pdf", "application/pdf", secondPdf));
        var source = Serialize(message);
        Assert.InRange(source.LongLength, 1, 10L * 1024 * 1024);

        var result = await UploadAsync(factory, client, "shared-pdf-budget.eml", "message/rfc822", source);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));

        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Contains(
            receipt.Evidence,
            evidence => evidence.Signal == "intake_limit_exceeded"
                && evidence.Detail.Contains("second-300-images.pdf", StringComparison.Ordinal));
        Assert.Null(receipt.InstructionDraft);
        Assert.Equal(
            300,
            receipt.AssetRecords.Count(asset => asset.Kind == IntakeAssetKind.EmbeddedImage));
    }

    [Fact]
    public async Task ReusedJpegWhoseRetainedBytesExceedTwentyFiveMegabytesFailsClosed()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        const int placementCount = 13;
        var jpeg = CreatePaddedJpeg((2 * 1024 * 1024) + 1);
        var pdf = CreateRepeatedJpegImagePdf(jpeg, placementCount);
        Assert.InRange(pdf.LongLength, jpeg.LongLength, 10L * 1024 * 1024);

        var result = await UploadAsync(factory, client, "reused-large-jpeg.pdf", "application/pdf", pdf);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));

        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Contains(
            receipt.Evidence,
            evidence => evidence.Signal == "intake_limit_exceeded"
                && evidence.Detail.Contains("extracted-image processing limit", StringComparison.Ordinal));
        Assert.Null(receipt.InstructionDraft);
        Assert.Equal(
            placementCount - 1,
            receipt.AssetRecords.Count(asset => asset.Kind == IntakeAssetKind.EmbeddedImage));
    }

    [Fact]
    public async Task MoreThan128MimeEntitiesStopsRemainingPartsAndSurfacesLimitGuard()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var message = CreateMessage("Synthetic large MIME tree", "Initial body");
        var multipart = Assert.IsType<Multipart>(message.Body);
        for (var index = 0; index < 130; index++)
        {
            multipart.Add(new TextPart("plain") { Text = $"Synthetic part {index}" });
        }

        var result = await UploadAsync(factory, client, "many-parts.eml", "message/rfc822", Serialize(message));
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Contains(receipt.Evidence, evidence => evidence.Signal == "intake_limit_exceeded");
        Assert.Contains("more than 128 MIME entities", reviewHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmingBodyWithMoreThan128MimeEntitiesFailsClosedWithoutReference()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var message = CreateMessage("Synthetic confirming large MIME tree", ConfirmingQdosBody);
        var multipart = Assert.IsType<Multipart>(message.Body);
        for (var index = 0; index < 130; index++)
        {
            multipart.Add(new TextPart("plain") { Text = $"Synthetic part {index}" });
        }

        var result = await UploadAsync(factory, client, "confirming-many-parts.eml", "message/rfc822", Serialize(message));
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));

        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Contains(receipt.Evidence, evidence => evidence.Signal == "intake_limit_exceeded");
    }

    [Fact]
    public async Task ConfirmingOuterBodyWithMoreThanEightNestedMessagesFailsClosedWithoutReference()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var message = CreateNestedMessageChain(10);
        SetFirstTextBody(message, ConfirmingQdosBody);

        var result = await UploadAsync(factory, client, "confirming-too-deep.eml", "message/rfc822", Serialize(message));
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));

        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Contains(receipt.Evidence, evidence => evidence.Signal == "intake_limit_exceeded");
    }

    [Fact]
    public async Task ConfirmingNestedEmailWhoseRepeatedDecodedPayloadExceeds25MbFailsClosed()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var largeAttachment = new byte[3 * 1024 * 1024];
        var message = CreateNestedMessageChainWithAttachment(7, largeAttachment);
        SetFirstTextBody(message, ConfirmingQdosBody);
        var source = Serialize(message);
        Assert.InRange(source.LongLength, 1, 10L * 1024 * 1024);

        var result = await UploadAsync(factory, client, "confirming-decoded-limit.eml", "message/rfc822", source);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));

        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Contains(receipt.Evidence, evidence => evidence.Signal == "intake_limit_exceeded");
    }

    [Fact]
    public async Task DocxWithMoreThan512ZipEntriesIsVisiblyResourceLimited()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var docx = CreateResourceHeavyDocx(additionalEntryCount: 513, additionalUncompressedBytes: 0);
        Assert.InRange(docx.LongLength, 1, 10L * 1024 * 1024);

        var result = await UploadAsync(factory, client, "too-many-entries.docx",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        docx);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(IntakeDecision.Unsupported, receipt.Decision);
        Assert.Equal("docx_limit_exceeded", receipt.FailureCode);
        Assert.Contains(
            "The Word document is larger than the processing limit allows",
            reviewHtml,
            StringComparison.Ordinal);
        Assert.DoesNotContain("docx_limit_exceeded", reviewHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DocxWithMoreThan50MbUncompressedIsVisiblyResourceLimited()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var docx = CreateResourceHeavyDocx(
            additionalEntryCount: 0,
            additionalUncompressedBytes: (51L * 1024 * 1024));
        Assert.InRange(docx.LongLength, 1, 10L * 1024 * 1024);

        var result = await UploadAsync(factory, client, "too-large-expanded.docx",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        docx);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(IntakeDecision.Unsupported, receipt.Decision);
        Assert.Equal("docx_limit_exceeded", receipt.FailureCode);
        Assert.Contains(
            "The Word document is larger than the processing limit allows",
            reviewHtml,
            StringComparison.Ordinal);
        Assert.DoesNotContain("docx_limit_exceeded", reviewHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DocxWithXmlPartOverTenMbIsVisiblyResourceLimited()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var docx = CreateResourceHeavyDocx(
            additionalEntryCount: 0,
            additionalUncompressedBytes: (11L * 1024 * 1024));
        Assert.InRange(docx.LongLength, 1, 10L * 1024 * 1024);

        var result = await UploadAsync(factory, client, "oversized-xml-part.docx",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        docx);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));

        Assert.Equal(IntakeDecision.Unsupported, receipt.Decision);
        Assert.Equal("docx_limit_exceeded", receipt.FailureCode);
        Assert.Null(receipt.InstructionDraft);
        Assert.Equal(
            "The DOCX exceeds the safe package, part, or extracted-image processing limits.",
            receipt.FailureReason);
    }

    [Fact]
    public async Task ConfirmingEmailWithDocxExtractedImagesOverTwentyFiveMbFailsClosedAndRetainsAttachment()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        const string attachmentName = "oversized-extracted-image.docx";
        var docx = CreateDocxWithImagePlacements(
            [13L * 1024 * 1024, 13L * 1024 * 1024],
            [0, 1]);
        var message = CreateMessage(
            "Synthetic oversized DOCX image attachment",
            ConfirmingQdosBody,
            (attachmentName, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", docx));
        var source = Serialize(message);
        Assert.InRange(source.LongLength, 1, 10L * 1024 * 1024);

        var result = await UploadAsync(factory, client, "oversized-docx-image-attachment.eml",
        "message/rfc822",
        source);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));

        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Null(receipt.InstructionDraft);
        Assert.Contains(
            receipt.Evidence,
            evidence => evidence.Signal == "intake_limit_exceeded"
                && evidence.Detail.Contains(attachmentName, StringComparison.Ordinal));
        Assert.Contains(
            receipt.AssetRecords,
            asset => asset.FileName == attachmentName
                && asset.Kind == IntakeAssetKind.Attachment);
    }

    [Fact]
    public async Task ConfirmingEmailWithRepeatedDocxImagePlacementCountsSharedContentOnce()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var docx = CreateDocxWithImagePlacements(
            [13L * 1024 * 1024],
            [0, 0]);
        var message = CreateQdosMessage(
            "Synthetic repeated DOCX image placement",
            ConfirmingQdosBody,
            ("repeated-image.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", docx));

        var result = await UploadAsync(factory, client, "repeated-docx-image-attachment.eml",
        "message/rfc822",
        Serialize(message));
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var images = receipt.AssetRecords
            .Where(asset => asset.Kind == IntakeAssetKind.EmbeddedImage)
            .OrderBy(asset => asset.SourceLabel, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);
        Assert.DoesNotContain(receipt.Evidence, evidence => evidence.Signal == "intake_limit_exceeded");
        Assert.Equal(2, images.Length);
        Assert.Equal(
            "uploaded repeated-docx-image-attachment.eml, attachment 1: repeated-image.docx, embedded image 1",
            images[0].SourceLabel);
        Assert.Equal(
            "uploaded repeated-docx-image-attachment.eml, attachment 1: repeated-image.docx, embedded image 2",
            images[1].SourceLabel);
        Assert.NotEqual(images[0].Id, images[1].Id);
        Assert.All(images, image => Assert.Equal(13L * 1024 * 1024, image.ContentLength));
        Assert.Equal(images[0].ContentHash, images[1].ContentHash);
    }

    [Fact]
    public async Task ConfirmingEmailKeepsBodyDecisionAndSurfacesCorruptDocumentAttachments()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var message = CreateQdosMessage(
            "Synthetic corrupt attachments",
            ConfirmingQdosBody,
            ("corrupt.pdf", "application/pdf", "not a PDF"u8.ToArray()),
            ("corrupt.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "PK invalid"u8.ToArray()));

        var result = await UploadAsync(factory, client, "corrupt-attachments.eml", "message/rfc822", Serialize(message));
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);
        Assert.Contains(receipt.Evidence, evidence => evidence.Signal == "unreadable-pdf-attachment");
        Assert.Contains(receipt.Evidence, evidence => evidence.Signal == "unreadable-docx-attachment");
        Assert.Contains("corrupt.pdf", reviewHtml, StringComparison.Ordinal);
        Assert.Contains("corrupt.docx", reviewHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MostlyOffPageRasterUsesVisibleIntersectionAndDoesNotSelectOcr()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var pdf = CreateImagePdf(new PdfImagePlacement(500, 0, 612, 792, 0xff, 0xff, 0xff));

        var result = await UploadAsync(factory, client, "mostly-off-page.pdf", "application/pdf", pdf);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));

        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Empty(receipt.ScannedPdfPages);
        Assert.Single(receipt.AssetRecords, asset => asset.Kind == IntakeAssetKind.EmbeddedImage);
    }

    [Fact]
    public async Task RasterCoveringExactly80PercentOfVisiblePageSelectsOcr()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var pdf = CreateImagePdf(
            600,
            800,
            new PdfImagePlacement(0, 0, 480, 800, 0xff, 0xff, 0xff));

        var result = await UploadAsync(factory, client, "exact-boundary.pdf", "application/pdf", pdf);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));

        Assert.Equal(IntakeDecision.OcrRequired, receipt.Decision);
        Assert.Single(receipt.ScannedPdfPages);
    }

    [Fact]
    public async Task TamperedRetainedSourceReturnsConflictWithoutIntegrityDetailLeakage()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory);
        var original = Convert.FromBase64String(TinyPngBase64)
            .Concat(Guid.NewGuid().ToByteArray())
            .ToArray();
        var result = await UploadAsync(factory, client, "unique-integrity.png", "image/png", original);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var asset = Assert.Single(
            receipt.AssetRecords,
            candidate => candidate.Kind == IntakeAssetKind.Source);
        var artifactRoot = Path.GetFullPath(factory.ArtifactDirectory);
        var storageSegments = asset.StorageKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, storageSegments.Length);
        var artifactPath = Path.GetFullPath(Path.Combine(artifactRoot, Path.Combine(storageSegments)));
        var requiredPrefix = artifactRoot.EndsWith(Path.DirectorySeparatorChar)
            ? artifactRoot
            : artifactRoot + Path.DirectorySeparatorChar;
        Assert.True(
            artifactPath.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase),
            "The generated artifact did not resolve inside the ignored intake artifact root.");

        const string tamperedText = "synthetic tampered artifact bytes";
        try
        {
            Assert.True(File.Exists(artifactPath), "The real intake caller did not retain its source artifact.");
            await File.WriteAllBytesAsync(artifactPath, Encoding.UTF8.GetBytes(tamperedText));

            using var response = await client.GetAsync($"/Received/{receipt.Id}/Source");
            var responseBody = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("The retained source could not be downloaded safely.", responseBody);
            Assert.False(
                responseBody.Contains(asset.StorageKey, StringComparison.Ordinal),
                "The integrity response exposed its storage key.");
            Assert.False(
                responseBody.Contains(asset.ContentHash, StringComparison.OrdinalIgnoreCase),
                "The integrity response exposed its expected content hash.");
            Assert.False(
                responseBody.Contains(artifactPath, StringComparison.OrdinalIgnoreCase),
                "The integrity response exposed a local artifact path.");
            Assert.False(
                responseBody.Contains(tamperedText, StringComparison.Ordinal),
                "The integrity response echoed tampered artifact content.");
        }
        finally
        {
            if (File.Exists(artifactPath))
            {
                File.Delete(artifactPath);
            }
        }
    }

    private const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

    private const string TinyJpegBase64 =
        "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////2wBDAf//////////////////////////////////////////////////////////////////////////////////////wAARCAABAAEDASIAAhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAf/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/9oADAMBAAIQAxAAAAF//8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABBQJ//8QAFBEBAAAAAAAAAAAAAAAAAAAAAP/aAAgBAwEBPwF//8QAFBEBAAAAAAAAAAAAAAAAAAAAAP/aAAgBAgEBPwF//8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQAGPwJ//8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABPyF//9oADAMBAAIAAwAAABD/xAAUEQEAAAAAAAAAAAAAAAAAAAAA/9oACAEDAQE/EH//xAAUEQEAAAAAAAAAAAAAAAAAAAAA/9oACAECAQE/EH//xAAUEAEAAAAAAAAAAAAAAAAAAAAA/9oACAEBAAE/EH//2Q==";

    private const string ConfirmingQdosBody =
        "QDOS instruction\r\nClaim Number: SYN-GUARD-001\r\nVehicle Registration: AB12 CDE";

    private static HttpClient CreateClient(IntakeWebApplicationFactory factory) => factory.CreateClient(
        new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private static Task<UploadResult> UploadAsync(
        IntakeWebApplicationFactory factory,
        HttpClient client,
        string fileName,
        string mediaType,
        byte[] bytes) =>
        IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            fileName,
            mediaType,
            bytes);

    private static Guid ReceiptId(UploadResult result) =>
        IntakeWebDriver.ReceiptId(result);

    private static async Task<IntakeReceipt> GetReceiptAsync(
        IntakeWebApplicationFactory factory,
        Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
        return Assert.IsType<IntakeReceipt>(await queries.GetAsync(id, CancellationToken.None));
    }

    private static async Task<string> GetReviewHtmlAsync(
        HttpClient client,
        UploadResult result)
    {
        using var response = await client.GetAsync(result.Location);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static byte[] CreateDocx(params string[] paragraphs)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteZipEntry(
                archive,
                "[Content_Types].xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
                  <Default Extension="xml" ContentType="application/xml" />
                  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml" />
                </Types>
                """);
            WriteZipEntry(
                archive,
                "_rels/.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml" />
                </Relationships>
                """);
            var body = string.Join(
                string.Empty,
                paragraphs.Select(paragraph =>
                    $"<w:p><w:r><w:t>{WebUtility.HtmlEncode(paragraph)}</w:t></w:r></w:p>"));
            WriteZipEntry(
                archive,
                "word/document.xml",
                $"""
                 <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                 <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                   <w:body>{body}<w:sectPr /></w:body>
                 </w:document>
                 """);
        }

        return output.ToArray();
    }

    private static void WriteZipEntry(ZipArchive archive, string path, string value)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.SmallestSize);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(value);
    }

    private static byte[] CreateResourceHeavyDocx(
        int additionalEntryCount,
        long additionalUncompressedBytes)
    {
        var baseDocx = CreateDocx(
            "QDOS instruction",
            "Claim Number: SYN-DOCX-LIMIT",
            "Vehicle Registration: AB12 CDE");
        using var output = new MemoryStream();
        output.Write(baseDocx);
        output.Position = 0;
        using (var archive = new ZipArchive(output, ZipArchiveMode.Update, leaveOpen: true))
        {
            for (var index = 0; index < additionalEntryCount; index++)
            {
                WriteZipEntry(archive, $"unused/entry-{index}.xml", "<unused />");
            }

            if (additionalUncompressedBytes > 0)
            {
                var entry = archive.CreateEntry("unused/highly-compressible.xml", CompressionLevel.SmallestSize);
                using var entryStream = entry.Open();
                var buffer = new byte[64 * 1024];
                long written = 0;
                while (written < additionalUncompressedBytes)
                {
                    var count = (int)Math.Min(buffer.Length, additionalUncompressedBytes - written);
                    entryStream.Write(buffer, 0, count);
                    written += count;
                }
            }
        }

        return output.ToArray();
    }

    private static byte[] CreateDocxWithImagePlacements(
        IReadOnlyList<long> imageByteCounts,
        IReadOnlyList<int> placementImageIndexes)
    {
        var baseDocx = CreateDocx(
            "QDOS instruction",
            "Claim Number: SYN-DOCX-IMAGE-LIMIT",
            "Vehicle Registration: AB12 CDE");
        using var output = new MemoryStream();
        output.Write(baseDocx);
        output.Position = 0;
        using (var archive = new ZipArchive(output, ZipArchiveMode.Update, leaveOpen: true))
        {
            var contentTypesEntry = Assert.IsType<ZipArchiveEntry>(archive.GetEntry("[Content_Types].xml"));
            string contentTypes;
            using (var reader = new StreamReader(contentTypesEntry.Open(), Encoding.UTF8))
            {
                contentTypes = reader.ReadToEnd();
            }

            contentTypesEntry.Delete();
            WriteZipEntry(
                archive,
                "[Content_Types].xml",
                contentTypes.Replace(
                    "</Types>",
                    "<Default Extension=\"png\" ContentType=\"image/png\" /></Types>",
                    StringComparison.Ordinal));

            var relationships = string.Join(
                string.Empty,
                imageByteCounts.Select((_, index) =>
                    $"<Relationship Id=\"rIdImage{index}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"media/image-{index}.png\" />"));
            WriteZipEntry(
                archive,
                "word/_rels/document.xml.rels",
                $"""
                 <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                 <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                   {relationships}
                 </Relationships>
                 """);

            var documentEntry = Assert.IsType<ZipArchiveEntry>(archive.GetEntry("word/document.xml"));
            string document;
            using (var reader = new StreamReader(documentEntry.Open(), Encoding.UTF8))
            {
                document = reader.ReadToEnd();
            }

            var placements = string.Join(
                string.Empty,
                placementImageIndexes.Select(index =>
                    $"""
                     <w:p><w:r><w:drawing>
                       <wp:inline xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing">
                         <a:graphic xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
                           <a:graphicData>
                             <pic:pic xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture">
                               <pic:blipFill><a:blip xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" r:embed="rIdImage{index}" /></pic:blipFill>
                             </pic:pic>
                           </a:graphicData>
                         </a:graphic>
                       </wp:inline>
                     </w:drawing></w:r></w:p>
                     """));
            documentEntry.Delete();
            WriteZipEntry(
                archive,
                "word/document.xml",
                document.Replace("<w:sectPr />", $"{placements}<w:sectPr />", StringComparison.Ordinal));

            for (var imageIndex = 0; imageIndex < imageByteCounts.Count; imageIndex++)
            {
                var imageEntry = archive.CreateEntry(
                    $"word/media/image-{imageIndex}.png",
                    CompressionLevel.SmallestSize);
                using var imageStream = imageEntry.Open();
                var buffer = new byte[64 * 1024];
                Array.Fill(buffer, (byte)(imageIndex + 1));
                long written = 0;
                while (written < imageByteCounts[imageIndex])
                {
                    var count = (int)Math.Min(buffer.Length, imageByteCounts[imageIndex] - written);
                    imageStream.Write(buffer, 0, count);
                    written += count;
                }
            }
        }

        return output.ToArray();
    }

    private static MimeMessage CreateMessage(
        string subject,
        string body,
        params (string FileName, string MediaType, byte[] Bytes)[] attachments)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Synthetic Sender", "synthetic@example.test"));
        message.To.Add(new MailboxAddress("Synthetic Intake", "intake@example.test"));
        message.Subject = subject;

        var multipart = new Multipart("mixed") { new TextPart("plain") { Text = body } };
        foreach (var attachment in attachments)
        {
            var typeParts = attachment.MediaType.Split('/', 2);
            multipart.Add(new MimePart(typeParts[0], typeParts[1])
            {
                FileName = attachment.FileName,
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                Content = new MimeContent(new MemoryStream(attachment.Bytes, writable: false))
            });
        }

        message.Body = multipart;
        return message;
    }

    private static MimeMessage CreateQdosMessage(
        string subject,
        string body,
        params (string FileName, string MediaType, byte[] Bytes)[] attachments)
    {
        var message = CreateMessage(subject, body, attachments);
        message.From.Clear();
        message.From.Add(new MailboxAddress("Synthetic QDOS sender", "instructions@qdosassist.co.uk"));
        return message;
    }

    private static void AttachNestedMessage(MimeMessage parent, string fileName, MimeMessage nested)
    {
        var multipart = Assert.IsType<Multipart>(parent.Body);
        var part = new MessagePart("rfc822")
        {
            Message = nested,
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment)
        };
        part.ContentDisposition.FileName = fileName;
        part.ContentType.Name = fileName;
        multipart.Add(part);
    }

    private static MimeMessage CreateNestedMessageChain(int depth)
    {
        var current = CreateMessage("Synthetic nested leaf", "Leaf body");
        for (var level = depth; level >= 1; level--)
        {
            var wrapper = CreateMessage($"Synthetic wrapper {level}", $"Wrapper body {level}");
            AttachNestedMessage(wrapper, $"nested-{level}.eml", current);
            current = wrapper;
        }

        return current;
    }

    private static MimeMessage CreateNestedMessageChainWithAttachment(int depth, byte[] attachment)
    {
        var current = CreateMessage(
            "Synthetic nested payload leaf",
            "Leaf body",
            ("large-retained.doc", "application/msword", attachment));
        for (var level = depth; level >= 1; level--)
        {
            var wrapper = CreateMessage($"Synthetic payload wrapper {level}", $"Wrapper body {level}");
            AttachNestedMessage(wrapper, $"payload-nested-{level}.eml", current);
            current = wrapper;
        }

        return current;
    }

    private static void SetFirstTextBody(MimeMessage message, string text)
    {
        var multipart = Assert.IsType<Multipart>(message.Body);
        Assert.IsType<TextPart>(multipart[0]).Text = text;
    }

    private static byte[] Serialize(MimeMessage message)
    {
        using var output = new MemoryStream();
        message.WriteTo(output);
        return output.ToArray();
    }

    private static byte[] CreateOleHeader()
    {
        var bytes = new byte[512];
        byte[] signature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
        signature.CopyTo(bytes, 0);
        return bytes;
    }

    private static byte[] CreatePdf(string text) => CreateTextPdf([text]);

    private static byte[] CreateTextPdf(string[] pageTexts)
    {
        ArgumentOutOfRangeException.ThrowIfZero(pageTexts.Length);
        var firstPageObject = 3;
        var fontObject = firstPageObject + pageTexts.Length;
        var firstContentObject = fontObject + 1;
        var kids = string.Join(
            " ",
            Enumerable.Range(firstPageObject, pageTexts.Length).Select(objectNumber => $"{objectNumber} 0 R"));
        var objectBodies = new List<byte[]>
        {
            Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
            Encoding.ASCII.GetBytes($"<< /Type /Pages /Kids [{kids}] /Count {pageTexts.Length} >>")
        };

        for (var index = 0; index < pageTexts.Length; index++)
        {
            objectBodies.Add(Encoding.ASCII.GetBytes(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] "
                + $"/Resources << /Font << /F1 {fontObject} 0 R >> >> "
                + $"/Contents {firstContentObject + index} 0 R >>"));
        }

        objectBodies.Add(Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));
        foreach (var text in pageTexts)
        {
            var operators = CreatePdfTextOperators(text);
            objectBodies.Add(Encoding.ASCII.GetBytes($"<< /Length {operators.Length} >>\nstream\n")
                .Concat(operators)
                .Concat(Encoding.ASCII.GetBytes("\nendstream"))
                .ToArray());
        }

        return WritePdfObjects(objectBodies);
    }

    private static byte[] CreatePdfTextOperators(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        var operators = new StringBuilder("BT /F1 12 Tf 72 720 Td\n");
        foreach (var line in lines)
        {
            operators.Append('(')
                .Append(EscapePdfText(line))
                .Append(") Tj\n0 -14 Td\n");
        }

        operators.Append("ET");
        return Encoding.ASCII.GetBytes(operators.ToString());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static byte[] CreateImagePdf(params PdfImagePlacement[] images)
        => CreateImagePdf(612, 792, images);

    private static byte[] CreateImagePdf(
        int pageWidth,
        int pageHeight,
        params PdfImagePlacement[] images)
    {
        var objectBodies = new List<byte[]>();
        objectBodies.Add(Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"));
        objectBodies.Add(Encoding.ASCII.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"));

        var firstImageObject = 4;
        var contentObject = firstImageObject + images.Length;
        var resources = string.Join(
            " ",
            images.Select((_, index) => $"/Im{index + 1} {firstImageObject + index} 0 R"));
        objectBodies.Add(Encoding.ASCII.GetBytes(
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pageWidth} {pageHeight}] "
            + $"/Resources << /XObject << {resources} >> >> /Contents {contentObject} 0 R >>"));

        foreach (var image in images)
        {
            using var imageObject = new MemoryStream();
            WriteAscii(
                imageObject,
                $"<< /Type /XObject /Subtype /Image /Width {image.SampleWidth} /Height {image.SampleHeight} "
                + "/ColorSpace /DeviceRGB /BitsPerComponent 8 /Length 3 >>\nstream\n");
            imageObject.WriteByte(image.Red);
            imageObject.WriteByte(image.Green);
            imageObject.WriteByte(image.Blue);
            WriteAscii(imageObject, "\nendstream");
            objectBodies.Add(imageObject.ToArray());
        }

        var operators = string.Join(
            string.Empty,
            images.Select((image, index) =>
                $"q\n{image.Width} 0 0 {image.Height} {image.X} {image.Y} cm\n/Im{index + 1} Do\nQ\n"));
        var operatorBytes = Encoding.ASCII.GetBytes(operators);
        objectBodies.Add(Encoding.ASCII.GetBytes(
                $"<< /Length {operatorBytes.Length} >>\nstream\n")
            .Concat(operatorBytes)
            .Concat(Encoding.ASCII.GetBytes("endstream"))
            .ToArray());

        return WritePdfObjects(objectBodies);
    }

    private static byte[] CreateRepeatedJpegImagePdf(byte[] jpeg, int placementCount)
    {
        ArgumentOutOfRangeException.ThrowIfZero(placementCount);
        var objectBodies = new List<byte[]>
        {
            Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
            Encoding.ASCII.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Encoding.ASCII.GetBytes(
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] "
                + "/Resources << /XObject << /Im1 4 0 R >> >> /Contents 5 0 R >>")
        };

        using (var imageObject = new MemoryStream())
        {
            WriteAscii(
                imageObject,
                $"<< /Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceRGB "
                + $"/BitsPerComponent 8 /Filter /DCTDecode /Length {jpeg.Length} >>\nstream\n");
            imageObject.Write(jpeg);
            WriteAscii(imageObject, "\nendstream");
            objectBodies.Add(imageObject.ToArray());
        }

        var operators = string.Concat(
            Enumerable.Range(0, placementCount).Select(
                index => $"q\n100 0 0 100 {20 + index} {20 + index} cm\n/Im1 Do\nQ\n"));
        var operatorBytes = Encoding.ASCII.GetBytes(operators);
        objectBodies.Add(Encoding.ASCII.GetBytes($"<< /Length {operatorBytes.Length} >>\nstream\n")
            .Concat(operatorBytes)
            .Concat(Encoding.ASCII.GetBytes("endstream"))
            .ToArray());

        return WritePdfObjects(objectBodies);
    }

    private static byte[] CreatePaddedJpeg(int minimumLength)
    {
        var original = Convert.FromBase64String(TinyJpegBase64);
        Assert.True(original is [0xff, 0xd8, ..], "The compact JPEG fixture must begin with an SOI marker.");
        if (original.Length >= minimumLength)
        {
            return original;
        }

        using var output = new MemoryStream(minimumLength + 256);
        output.Write(original.AsSpan(0, 2));
        var remainingPayload = minimumLength - original.Length;
        while (remainingPayload > 0)
        {
            var payloadLength = Math.Min(remainingPayload, ushort.MaxValue - 2);
            var segmentLength = payloadLength + 2;
            output.WriteByte(0xff);
            output.WriteByte(0xef);
            output.WriteByte((byte)(segmentLength >> 8));
            output.WriteByte((byte)segmentLength);
            output.Write(new byte[payloadLength]);
            remainingPayload -= payloadLength;
        }

        output.Write(original.AsSpan(2));
        return output.ToArray();
    }

    private static byte[] WritePdfObjects(List<byte[]> objectBodies)
    {
        using var output = new MemoryStream();
        WriteAscii(output, "%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objectBodies.Count; index++)
        {
            offsets.Add(output.Position);
            WriteAscii(output, $"{index + 1} 0 obj\n");
            output.Write(objectBodies[index]);
            WriteAscii(output, "\nendobj\n");
        }

        var xref = output.Position;
        WriteAscii(output, $"xref\n0 {objectBodies.Count + 1}\n");
        WriteAscii(output, "0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            WriteAscii(output, $"{offset:0000000000} 00000 n \n");
        }

        WriteAscii(
            output,
            $"trailer\n<< /Size {objectBodies.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    private static string EscapePdfText(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("(", "\\(", StringComparison.Ordinal)
        .Replace(")", "\\)", StringComparison.Ordinal);

    private static void WriteAscii(Stream stream, string value) =>
        stream.Write(Encoding.ASCII.GetBytes(value));


    private sealed record PdfImagePlacement(
        int X,
        int Y,
        int Width,
        int Height,
        byte Red,
        byte Green,
        byte Blue,
        int SampleWidth = 1,
        int SampleHeight = 1);

    private sealed class SteppingTimeProvider(TimeSpan step) : TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow() => new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

        public override long GetTimestamp() =>
            Interlocked.Add(ref timestamp, step.Ticks);
    }
}
