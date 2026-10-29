using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The upload review, through the real caller (operator, 28 October 2026,
/// R463): an uploaded image shows its own preview at once, before and after
/// processing, and an image the upload left Unidentified is registered on the
/// review itself through the act its Unidentified record runs.
/// </summary>
public sealed class UploadReviewImageWebTests
{
    [Fact]
    public async Task AnUploadedImageShowsAtOnceAndStillOnceProcessed()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var bytes = Convert.FromBase64String(MultiFormatFixture.TinyPngBase64);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var upload = await IntakeWebDriver.UploadAsync(
            client, "vehicle.png", "image/png", bytes, Guid.NewGuid().ToString("N"));
        var stagedReceiptId = Assert.IsType<Guid>(IntakeWebDriver.Landing(upload).StagedReceiptId);
        var imageUrl = $"/Upload/Image/{stagedReceiptId:D}?v={hash}";

        // Nothing has been processed yet, and the tile already names the image.
        var pending = await IntakeWebDriver.GetHtmlAsync(client, $"/Upload/Status/{stagedReceiptId:D}");
        Assert.Contains($"src=\"{imageUrl}\"", pending, StringComparison.Ordinal);
        await AssertServesAsync(client, imageUrl, bytes, hash);

        await IntakeWebDriver.ProcessQueuedAsync(factory, upload);

        var processed = await IntakeWebDriver.GetHtmlAsync(client, $"/Upload/Status/{stagedReceiptId:D}");
        Assert.Contains($"src=\"{imageUrl}\"", processed, StringComparison.Ordinal);
        await AssertServesAsync(client, imageUrl, bytes, hash);

        using var unknown = await client.GetAsync($"/Upload/Image/{Guid.NewGuid():D}");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task AnImageTheUploadLeftUnidentifiedIsRegisteredOnTheReview()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var upload = await IntakeWebDriver.UploadAsync(
            client,
            "vehicle.png",
            "image/png",
            Convert.FromBase64String(MultiFormatFixture.TinyPngBase64),
            Guid.NewGuid().ToString("N"));
        var stagedReceiptId = Assert.IsType<Guid>(IntakeWebDriver.Landing(upload).StagedReceiptId);
        var receiptId = IntakeWebDriver.ReceiptId(await IntakeWebDriver.ProcessQueuedAsync(factory, upload));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var item = await scope.ServiceProvider.GetRequiredService<IUnidentifiedStore>()
                .GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId), CancellationToken.None);
            Assert.Equal(UnidentifiedState.Open, Assert.IsType<UnidentifiedItem>(item).State);
        }

        var review = await IntakeWebDriver.GetHtmlAsync(client, $"/Upload/Status/{stagedReceiptId:D}");
        Assert.Contains("handler=RegisterImages", review, StringComparison.Ordinal);
        Assert.Contains("data-upload-register-open", review, StringComparison.Ordinal);

        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);
        using var response = await client.PostAsync(
            $"/Upload/Status/{stagedReceiptId:D}?handler=RegisterImages",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["operationKey"] = Guid.NewGuid().ToString("N"),
                ["vehicleRegistration"] = "ab12 cde",
                ["reason"] = "Staff read the registration from the uploaded image."
            }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            $"/Upload/Status/{stagedReceiptId:D}",
            response.Headers.Location!.OriginalString,
            ignoreCase: true);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var receipt = await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
                .GetAsync(receiptId, CancellationToken.None);
            Assert.Equal(IntakeDecision.ImageIntakeRegistered, Assert.IsType<IntakeReceipt>(receipt).Decision);
        }

        // The review now reports the registration, and offers it no longer.
        var after = await IntakeWebDriver.GetHtmlAsync(client, $"/Upload/Status/{stagedReceiptId:D}");
        Assert.Contains("AB12CDE-01", after, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=RegisterImages", after, StringComparison.Ordinal);
    }

    private static async Task AssertServesAsync(HttpClient client, string url, byte[] bytes, string hash)
    {
        using var image = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType!.MediaType);
        Assert.Equal(TimeSpan.FromDays(7), image.Headers.CacheControl!.MaxAge);
        Assert.Equal($"\"{hash.ToLowerInvariant()}\"", image.Headers.ETag!.Tag);
        Assert.Equal(bytes, await image.Content.ReadAsByteArrayAsync());
    }
}
