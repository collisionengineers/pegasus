using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

/// <summary>
/// CASE-006: retained vehicle images are viewable in Pegasus — an authorised
/// staff-only endpoint serves true image media inline, and the
/// Image-initiated Case page and the case Evidence tab render thumbnail
/// galleries whose thumbnails click-expand to that endpoint. Anything that is
/// not an image stays on the forced-download route.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class ImageViewingWebTests
{
    [Fact]
    public async Task InlineImageEndpointServesOnlyImagesToAuthorisedStaff()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            useIntegrationTestAuthentication: true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var pngBytes = Convert.FromBase64String(MultiFormatFixture.TinyPngBase64);
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "vehicle.png",
            "image/png",
            pngBytes,
            Guid.NewGuid().ToString("N"));
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        using var image = await client.GetAsync($"/Received/{receiptId:D}/Image");
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        Assert.Equal("inline", image.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("nosniff", Assert.Single(image.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal(pngBytes, await image.Content.ReadAsByteArrayAsync());

        // Non-image material never renders inline; it stays on the
        // forced-download route.
        var email = IntakeTestEvidence.CreateEmail(
            "instruction.eml",
            "QDOS instruction\r\nClaim Number: IMG-VIEW-001\r\nVehicle Registration: AB12 CDE");
        var emailUpload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            email.FileName,
            email.MediaType,
            email.Content);
        var emailReceiptId = IntakeWebDriver.ReceiptId(emailUpload);
        using var refused = await client.GetAsync($"/Received/{emailReceiptId:D}/Image");
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);

        // Authorisation: anonymous is challenged to sign in; an authenticated
        // account with no staff role is refused.
        using var anonymousRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/Received/{receiptId:D}/Image");
        anonymousRequest.Headers.Add("X-Test-Anonymous", "1");
        using var anonymous = await client.SendAsync(anonymousRequest);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.Contains(
            "/Account/SignIn",
            anonymous.Headers.Location!.OriginalString,
            StringComparison.Ordinal);

        using var rolelessRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/Received/{receiptId:D}/Image");
        rolelessRequest.Headers.Add("X-Test-Roleless", "1");
        using var roleless = await client.SendAsync(rolelessRequest);
        Assert.Equal(HttpStatusCode.Forbidden, roleless.StatusCode);

        // An unknown receipt does not disclose anything.
        using var unknown = await client.GetAsync($"/Received/{Guid.NewGuid():D}/Image");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task ImageCasePageAndCaseEvidenceTabRenderTheGallery()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine("AB12CDE"));
        using var client = IntakeWebDriver.CreateClient(factory);

        var caseEmail = IntakeTestEvidence.CreateEmail(
            "gallery-case.eml",
            "QDOS instruction\r\nClaim Number: GALLERY-01\r\nVehicle Registration: AB12 CDE");
        var caseUpload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            caseEmail.FileName,
            caseEmail.MediaType,
            caseEmail.Content);
        var caseOriginReceiptId = IntakeWebDriver.ReceiptId(caseUpload);
        var caseId = await ImageIntakeTestData.PromoteAllocatedCaseAsync(
            factory.Services,
            caseOriginReceiptId,
            nameof(CaseLifecycleState.Review));

        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "vehicle.png",
            "image/png",
            Convert.FromBase64String(MultiFormatFixture.TinyPngBase64),
            Guid.NewGuid().ToString("N"));
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IImageIntakeQueries>();
        var detail = await queries.GetByOriginReceiptAsync(receiptId, CancellationToken.None);
        Assert.Equal(caseId, detail!.AssociatedCaseId);

        var images = await queries.ListImagesAsync(detail.Record.Id, CancellationToken.None);
        var galleryImage = Assert.Single(images);
        Assert.Equal(receiptId, galleryImage.ReceiptId);
        Assert.Equal("vehicle.png", galleryImage.FileName);

        var expectedSource = $"/Received/{receiptId:D}/Image";
        var imageCasePage = await GetAsync(client, $"/VehicleImages/{detail.Record.Id:D}");
        Assert.Contains(expectedSource, imageCasePage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("alt=\"vehicle.png\"", imageCasePage, StringComparison.Ordinal);
        Assert.Contains("loading=\"lazy\"", imageCasePage, StringComparison.Ordinal);

        var casePage = await GetAsync(client, $"/Cases/{caseId:D}?tab=evidence");
        Assert.Contains(expectedSource, casePage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AB12CDE-01", casePage, StringComparison.Ordinal);

        // The overview tab does not pay the gallery query cost.
        var overview = await GetAsync(client, $"/Cases/{caseId:D}");
        Assert.DoesNotContain(expectedSource, overview, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// DOCS-006: an instruction's retained image assets serve inline through
    /// the receipt-scoped asset route; non-image assets and foreign
    /// receipt/asset pairings stay off it.
    /// </summary>
    [Fact]
    public async Task AssetEndpointServesOnlyTheReceiptsOwnImagesInline()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);

        var pngBytes = Convert.FromBase64String(MultiFormatFixture.TinyPngBase64);
        var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress("Synthetic sender", "instructions@qdosassist.co.uk"));
        message.To.Add(new MimeKit.MailboxAddress("Pegasus Intake", "intake@example.test"));
        message.Subject = "QDOS asset endpoint";
        var builder = new MimeKit.BodyBuilder
        {
            TextBody = "QDOS instruction\r\nClaimant Name: Asset Endpoint\r\nClaim Number: AST-001"
        };
        builder.Attachments.Add("damage.png", pngBytes, MimeKit.ContentType.Parse("image/png"));
        builder.Attachments.Add(
            "estimate.pdf", "%PDF-1.4 synthetic"u8.ToArray(), MimeKit.ContentType.Parse("application/pdf"));
        message.Body = builder.ToMessageBody();
        using var output = new MemoryStream();
        message.WriteTo(output);

        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "asset-endpoint.eml",
            "message/rfc822",
            output.ToArray());
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        await using var scope = factory.Services.CreateAsyncScope();
        var receipt = await scope.ServiceProvider
            .GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        var imageAsset = Assert.Single(
            receipt!.AssetRecords,
            asset => asset.Kind == IntakeAssetKind.Attachment
                && asset.MediaType == "image/png");
        var pdfAsset = Assert.Single(
            receipt.AssetRecords,
            asset => asset.Kind == IntakeAssetKind.Attachment
                && asset.MediaType == "application/pdf");

        using var image = await client.GetAsync($"/Received/{receiptId:D}/Asset/{imageAsset.Id:D}");
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        Assert.Equal("inline", image.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("nosniff", Assert.Single(image.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal(pngBytes, await image.Content.ReadAsByteArrayAsync());

        // Non-image assets never render inline.
        using var refusedPdf = await client.GetAsync($"/Received/{receiptId:D}/Asset/{pdfAsset.Id:D}");
        Assert.Equal(HttpStatusCode.NotFound, refusedPdf.StatusCode);

        // An asset cannot be fetched under another receipt's identity.
        using var foreign = await client.GetAsync($"/Received/{Guid.NewGuid():D}/Asset/{imageAsset.Id:D}");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);

        // Anonymous is challenged; roleless is refused.
        using var anonymousRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/Received/{receiptId:D}/Asset/{imageAsset.Id:D}");
        anonymousRequest.Headers.Add("X-Test-Anonymous", "1");
        using var anonymous = await client.SendAsync(anonymousRequest);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
    }

    private static async Task<string> GetAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }
}
