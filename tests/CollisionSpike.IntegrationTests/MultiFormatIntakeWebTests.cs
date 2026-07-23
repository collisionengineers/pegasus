using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CollisionSpike.Core.Intake.Qdos;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;

namespace CollisionSpike.IntegrationTests;

public sealed partial class MultiFormatIntakeWebTests
{
    [Fact]
    public async Task DirectDocxTextProducesReadableQdosDecisionThroughWebCaller()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = CreateClient(factory);
        var docx = CreateDocx(
            "QDOS instruction",
            "Claim Number: SYN-DOCX-001",
            "Vehicle Registration: AB12 CDE",
            "Claimant Name: Synthetic Person");

        var result = await UploadAsync(
            client,
            "synthetic-instruction.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            docx);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(QdosIntakeDecision.ConfirmedQdos, receipt.Decision);
        Assert.Equal(
            "SYN-DOCX-001",
            Assert.Single(receipt.Fields, field => field.Name == "Claim number").SuggestedValue);
        Assert.Equal(
            "AB12 CDE",
            Assert.Single(receipt.Fields, field => field.Name == "Vehicle registration").SuggestedValue);
        Assert.Equal("AB12CDE", Assert.IsType<QdosTypedDraft>(receipt.TypedDraft).VehicleRegistration);
        Assert.Contains("synthetic-instruction.docx", reviewHtml, StringComparison.Ordinal);
        Assert.Contains("Confirmed QDOS", reviewHtml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("legacy-instruction.doc", "application/msword")]
    [InlineData("outlook-message.msg", "application/vnd.ms-outlook")]
    public async Task DeferredLegacyContainersAreAcceptedIntoNeedsSortingWithoutReference(
        string fileName,
        string mediaType)
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = CreateClient(factory);

        var result = await UploadAsync(client, fileName, mediaType, CreateOleHeader());
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(QdosIntakeDecision.NeedsSorting, receipt.Decision);
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
        using var factory = new QdosWebApplicationFactory();
        using var client = CreateClient(factory);

        var result = await UploadAsync(client, fileName, mediaType, Convert.FromBase64String(base64));
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(QdosIntakeDecision.NeedsSorting, receipt.Decision);
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
        using var factory = new QdosWebApplicationFactory();
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
        var message = CreateMessage(
            "Synthetic mixed intake",
            "Please review the attached instruction and evidence.",
            ("instruction.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", docx),
            ("supporting.pdf", "application/pdf", pdf),
            ("vehicle.jpg", "image/jpeg", image));
        AttachNestedMessage(message, "forwarded-message.eml", nested);

        var result = await UploadAsync(client, "mixed-intake.eml", "message/rfc822", Serialize(message));
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(QdosIntakeDecision.ConfirmedQdos, receipt.Decision);
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
        using var factory = new QdosWebApplicationFactory();
        using var client = CreateClient(factory);
        var image = Convert.FromBase64String(TinyJpegBase64);
        var message = CreateMessage(
            "Synthetic duplicate evidence",
            "QDOS instruction\r\nClaim Number: SYN-DUP-001\r\nVehicle Registration: AB12 CDE",
            ("vehicle-front-original.jpg", "image/jpeg", image),
            ("vehicle-front-copy.jpg", "image/jpeg", image));

        var result = await UploadAsync(client, "duplicate-images.eml", "message/rfc822", Serialize(message));
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(QdosIntakeDecision.ConfirmedQdos, receipt.Decision);
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
        using var factory = new QdosWebApplicationFactory();
        using var client = CreateClient(factory);

        var result = await UploadAsync(
            client,
            "malformed.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "PK not a valid Open XML package"u8.ToArray());
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(QdosIntakeDecision.Unsupported, receipt.Decision);
        Assert.Equal("unreadable_docx", receipt.FailureCode);
        Assert.Contains("unreadable_docx", reviewHtml, StringComparison.Ordinal);
        Assert.Contains("malformed.docx", reviewHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NestedMimeBeyondEightLevelsStopsThatBranchAndSurfacesLimitGuard()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = CreateClient(factory);
        var message = CreateNestedMessageChain(10);

        var result = await UploadAsync(client, "too-deep.eml", "message/rfc822", Serialize(message));
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(QdosIntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Contains(receipt.Evidence, item => item.Signal == "intake_limit_exceeded");
        Assert.Contains("nesting depth exceeds 8", reviewHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FullPageRasterWithLowTextProducesExactlyOneScannedPageOcrCandidate()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = CreateClient(factory);
        var pdf = CreateImagePdf(new PdfImagePlacement(0, 0, 612, 792, 0xff, 0xff, 0xff));

        var result = await UploadAsync(client, "full-page-scan.pdf", "application/pdf", pdf);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(QdosIntakeDecision.OcrRequired, receipt.Decision);
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
        using var factory = new QdosWebApplicationFactory();
        using var client = CreateClient(factory);
        var pdf = CreateImagePdf(new PdfImagePlacement(20, 20, 100, 100, 0xff, 0xff, 0xff));

        var result = await UploadAsync(client, "small-raster.pdf", "application/pdf", pdf);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(QdosIntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Empty(receipt.ScannedPdfPages);
        Assert.DoesNotContain(receipt.Evidence, evidence => evidence.Signal == "scanned-pdf-page");
        Assert.Single(receipt.AssetRecords, asset => asset.Kind == IntakeAssetKind.EmbeddedImage);
        Assert.DoesNotContain("Scanned PDF pages", reviewHtml, StringComparison.Ordinal);
        Assert.Contains("not an image-led scanned page", reviewHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TwoDiscretePdfImageObjectsRemainSeparateAndDownloadAsImages()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = CreateClient(factory);
        var pdf = CreateImagePdf(
            new PdfImagePlacement(20, 20, 100, 100, 0xff, 0x00, 0x00),
            new PdfImagePlacement(180, 20, 100, 100, 0x00, 0x00, 0xff));

        var result = await UploadAsync(client, "two-images.pdf", "application/pdf", pdf);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var images = receipt.AssetRecords
            .Where(asset => asset.Kind == IntakeAssetKind.EmbeddedImage)
            .OrderBy(asset => asset.FileName, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(QdosIntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Empty(receipt.ScannedPdfPages);
        Assert.Equal(2, images.Length);
        Assert.NotEqual(images[0].Id, images[1].Id);
        Assert.NotEqual(images[0].ContentHash, images[1].ContentHash);

        foreach (var image in images)
        {
            using var response = await client.GetAsync(
                $"/Intake/Review/{receipt.Id}?handler=Asset&assetId={image.Id}");
            response.EnsureSuccessStatusCode();
            var downloaded = await response.Content.ReadAsByteArrayAsync();

            Assert.Equal(image.MediaType, response.Content.Headers.ContentType?.MediaType);
            Assert.StartsWith("image/", image.MediaType, StringComparison.Ordinal);
            Assert.Equal(image.ContentLength, downloaded.LongLength);
            Assert.Equal(image.ContentHash, Convert.ToHexString(SHA256.HashData(downloaded)));
        }
    }

    [Fact]
    public async Task MoreThan128MimeEntitiesStopsRemainingPartsAndSurfacesLimitGuard()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = CreateClient(factory);
        var message = CreateMessage("Synthetic large MIME tree", "Initial body");
        var multipart = Assert.IsType<Multipart>(message.Body);
        for (var index = 0; index < 130; index++)
        {
            multipart.Add(new TextPart("plain") { Text = $"Synthetic part {index}" });
        }

        var result = await UploadAsync(client, "many-parts.eml", "message/rfc822", Serialize(message));
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(QdosIntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Contains(receipt.Evidence, evidence => evidence.Signal == "intake_limit_exceeded");
        Assert.Contains("more than 128 MIME entities", reviewHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmingBodyWithMoreThan128MimeEntitiesFailsClosedWithoutReference()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = CreateClient(factory);
        var message = CreateMessage("Synthetic confirming large MIME tree", ConfirmingQdosBody);
        var multipart = Assert.IsType<Multipart>(message.Body);
        for (var index = 0; index < 130; index++)
        {
            multipart.Add(new TextPart("plain") { Text = $"Synthetic part {index}" });
        }

        var result = await UploadAsync(client, "confirming-many-parts.eml", "message/rfc822", Serialize(message));
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));

        Assert.Equal(QdosIntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Contains(receipt.Evidence, evidence => evidence.Signal == "intake_limit_exceeded");
    }

    [Fact]
    public async Task ConfirmingOuterBodyWithMoreThanEightNestedMessagesFailsClosedWithoutReference()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = CreateClient(factory);
        var message = CreateNestedMessageChain(10);
        SetFirstTextBody(message, ConfirmingQdosBody);

        var result = await UploadAsync(client, "confirming-too-deep.eml", "message/rfc822", Serialize(message));
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));

        Assert.Equal(QdosIntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Contains(receipt.Evidence, evidence => evidence.Signal == "intake_limit_exceeded");
    }

    [Fact]
    public async Task ConfirmingNestedEmailWhoseRepeatedDecodedPayloadExceeds25MbFailsClosed()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = CreateClient(factory);
        var largeAttachment = new byte[3 * 1024 * 1024];
        var message = CreateNestedMessageChainWithAttachment(7, largeAttachment);
        SetFirstTextBody(message, ConfirmingQdosBody);
        var source = Serialize(message);
        Assert.InRange(source.LongLength, 1, 10L * 1024 * 1024);

        var result = await UploadAsync(client, "confirming-decoded-limit.eml", "message/rfc822", source);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));

        Assert.Equal(QdosIntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Contains(receipt.Evidence, evidence => evidence.Signal == "intake_limit_exceeded");
    }

    [Fact]
    public async Task DocxWithMoreThan512ZipEntriesIsVisiblyResourceLimited()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = CreateClient(factory);
        var docx = CreateResourceHeavyDocx(additionalEntryCount: 513, additionalUncompressedBytes: 0);
        Assert.InRange(docx.LongLength, 1, 10L * 1024 * 1024);

        var result = await UploadAsync(
            client,
            "too-many-entries.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            docx);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(QdosIntakeDecision.Unsupported, receipt.Decision);
        Assert.Equal("docx_limit_exceeded", receipt.FailureCode);
        Assert.Contains("docx_limit_exceeded", reviewHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DocxWithMoreThan50MbUncompressedIsVisiblyResourceLimited()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = CreateClient(factory);
        var docx = CreateResourceHeavyDocx(
            additionalEntryCount: 0,
            additionalUncompressedBytes: (51L * 1024 * 1024));
        Assert.InRange(docx.LongLength, 1, 10L * 1024 * 1024);

        var result = await UploadAsync(
            client,
            "too-large-expanded.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            docx);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(QdosIntakeDecision.Unsupported, receipt.Decision);
        Assert.Equal("docx_limit_exceeded", receipt.FailureCode);
        Assert.Contains("docx_limit_exceeded", reviewHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmingEmailKeepsBodyDecisionAndSurfacesCorruptDocumentAttachments()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = CreateClient(factory);
        var message = CreateMessage(
            "Synthetic corrupt attachments",
            ConfirmingQdosBody,
            ("corrupt.pdf", "application/pdf", "not a PDF"u8.ToArray()),
            ("corrupt.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "PK invalid"u8.ToArray()));

        var result = await UploadAsync(client, "corrupt-attachments.eml", "message/rfc822", Serialize(message));
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var reviewHtml = await GetReviewHtmlAsync(client, result);

        Assert.Equal(QdosIntakeDecision.ConfirmedQdos, receipt.Decision);
        Assert.Contains(receipt.Evidence, evidence => evidence.Signal == "unreadable-pdf-attachment");
        Assert.Contains(receipt.Evidence, evidence => evidence.Signal == "unreadable-docx-attachment");
        Assert.Contains("corrupt.pdf", reviewHtml, StringComparison.Ordinal);
        Assert.Contains("corrupt.docx", reviewHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MostlyOffPageRasterUsesVisibleIntersectionAndDoesNotSelectOcr()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = CreateClient(factory);
        var pdf = CreateImagePdf(new PdfImagePlacement(500, 0, 612, 792, 0xff, 0xff, 0xff));

        var result = await UploadAsync(client, "mostly-off-page.pdf", "application/pdf", pdf);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));

        Assert.Equal(QdosIntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Empty(receipt.ScannedPdfPages);
        Assert.Single(receipt.AssetRecords, asset => asset.Kind == IntakeAssetKind.EmbeddedImage);
    }

    [Fact]
    public async Task RasterCoveringExactly80PercentOfVisiblePageSelectsOcr()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = CreateClient(factory);
        var pdf = CreateImagePdf(
            600,
            800,
            new PdfImagePlacement(0, 0, 480, 800, 0xff, 0xff, 0xff));

        var result = await UploadAsync(client, "exact-boundary.pdf", "application/pdf", pdf);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));

        Assert.Equal(QdosIntakeDecision.OcrRequired, receipt.Decision);
        Assert.Single(receipt.ScannedPdfPages);
    }

    [Fact]
    public async Task TamperedStoredImageReturnsConflictWithoutIntegrityDetailLeakage()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = CreateClient(factory);
        var original = Convert.FromBase64String(TinyPngBase64)
            .Concat(Guid.NewGuid().ToByteArray())
            .ToArray();
        var result = await UploadAsync(client, "unique-integrity.png", "image/png", original);
        var receipt = await GetReceiptAsync(factory, ReceiptId(result));
        var asset = Assert.Single(
            receipt.AssetRecords,
            candidate => candidate.Kind == IntakeAssetKind.Source);
        var artifactRoot = Path.GetFullPath(Path.Combine(FindRepositoryRoot(), "artifacts", "intake"));
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

            using var response = await client.GetAsync(
                $"/Intake/Review/{receipt.Id}?handler=Asset&assetId={asset.Id}");
            var responseBody = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Contains("integrity", responseBody, StringComparison.OrdinalIgnoreCase);
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

    private static HttpClient CreateClient(QdosWebApplicationFactory factory) => factory.CreateClient(
        new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private static async Task<MultiFormatUploadResult> UploadAsync(
        HttpClient client,
        string fileName,
        string mediaType,
        byte[] bytes)
    {
        using var formPage = await client.GetAsync("/Intake/Qdos");
        formPage.EnsureSuccessStatusCode();
        var formHtml = await formPage.Content.ReadAsStringAsync();
        var tokenTag = AntiforgeryTagRegex().Match(formHtml);
        Assert.True(tokenTag.Success, "The real upload page must render an antiforgery token.");
        var tokenValue = AntiforgeryValueRegex().Match(tokenTag.Value);
        Assert.True(tokenValue.Success, "The antiforgery token must have a value.");
        var receiptTokenTag = ExternalReceiptTokenTagRegex().Match(formHtml);
        Assert.True(receiptTokenTag.Success, "The real upload page must render an external receipt token.");
        var receiptTokenValue = AntiforgeryValueRegex().Match(receiptTokenTag.Value);
        Assert.True(receiptTokenValue.Success, "The external receipt token must have a value.");

        using var multipart = new MultipartFormDataContent();
        multipart.Add(
            new StringContent(WebUtility.HtmlDecode(tokenValue.Groups["value"].Value)),
            "__RequestVerificationToken");
        multipart.Add(
            new StringContent(WebUtility.HtmlDecode(receiptTokenValue.Groups["value"].Value)),
            "ExternalReceiptToken");
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
        multipart.Add(file, "Upload", fileName);

        using var response = await client.PostAsync("/Intake/Qdos", multipart);
        return new(
            response.StatusCode,
            response.Headers.Location,
            await response.Content.ReadAsStringAsync());
    }

    private static Guid ReceiptId(MultiFormatUploadResult result)
    {
        Assert.True(
            result.StatusCode == HttpStatusCode.Redirect,
            $"Expected the real caller to accept the source, but received {(int)result.StatusCode}: {result.ResponseBody}");
        Assert.NotNull(result.Location);
        var path = result.Location!.OriginalString.Split('?', 2)[0];
        Assert.True(Guid.TryParse(path.Split('/', StringSplitOptions.RemoveEmptyEntries).Last(), out var id));
        return id;
    }

    private static async Task<QdosIntakeRecord> GetReceiptAsync(
        QdosWebApplicationFactory factory,
        Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IQdosIntakeQueries>();
        return Assert.IsType<QdosIntakeRecord>(await queries.GetAsync(id, CancellationToken.None));
    }

    private static async Task<string> GetReviewHtmlAsync(
        HttpClient client,
        MultiFormatUploadResult result)
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

    private static byte[] CreatePdf(string text)
    {
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {text.Length + 34} >>\nstream\nBT /F1 12 Tf 72 720 Td ({EscapePdfText(text)}) Tj ET\nendstream"
        };

        using var output = new MemoryStream();
        WriteAscii(output, "%PDF-1.4\n");
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(output.Position);
            WriteAscii(output, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var xref = output.Position;
        WriteAscii(output, $"xref\n0 {objects.Length + 1}\n");
        WriteAscii(output, "0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            WriteAscii(output, $"{offset:0000000000} 00000 n \n");
        }

        WriteAscii(
            output,
            $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
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
                "<< /Type /XObject /Subtype /Image /Width 1 /Height 1 "
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

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryTagRegex();

    [GeneratedRegex("<input[^>]*name=\"ExternalReceiptToken\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExternalReceiptTokenTagRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryValueRegex();

    private sealed record MultiFormatUploadResult(
        HttpStatusCode StatusCode,
        Uri? Location,
        string ResponseBody);

    private sealed record PdfImagePlacement(
        int X,
        int Y,
        int Width,
        int Height,
        byte Red,
        byte Green,
        byte Blue);
}
