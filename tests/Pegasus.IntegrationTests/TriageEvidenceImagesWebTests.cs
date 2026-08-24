using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Triage;

namespace Pegasus.IntegrationTests;

/// <summary>
/// A Triage request's photographs are the whole subject of the assessment the
/// engineer is being asked to make. Until INTK-034 they were viewable nowhere:
/// the Triage page's "View e-mail" link lands on a receipt page that lists
/// attachments by name and renders none of them.
/// </summary>
public sealed partial class QdosTriageIntegrationTests
{
    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task ATriagePageShowsTheVehiclePhotographsItsRequestCarried()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            extractionPolicy: new AcceptedTriageMatchPolicy());
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "triage-with-images.eml",
            "QDOS instruction\r\nClaimant Name: Triage Claimant\r\nClaim Number: TRIAGE-IMG"
            + "\r\nVehicle Registration: AB12 CDE",
            attachments:
            [
                ("Client vehicle damage 1.jpg", "image/jpeg", TinyPngBytes),
                ("Client vehicle damage 2.jpg", "image/jpeg", TinyPngBytes2)
            ]);

        await IntakeWebDriver.UploadAndProcessAsync(
            factory, client, email.FileName, email.MediaType, email.Content);

        Guid triageId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            triageId = Assert.Single(
                await scope.ServiceProvider.GetRequiredService<ITriageQueries>()
                    .ListAsync(null, CancellationToken.None)).Id;
        }

        using var response = await client.GetAsync($"/Triage/{triageId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Vehicle images", html, StringComparison.Ordinal);
        Assert.Contains("Client vehicle damage 1.jpg", html, StringComparison.Ordinal);
        Assert.Contains("Client vehicle damage 2.jpg", html, StringComparison.Ordinal);
        // Served by the one authorised, hash-verified asset route — not copied
        // anywhere, and not a second custody of the same bytes.
        Assert.Contains("/Asset/", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task ATriageWhoseRequestCarriedNoPhotographsRendersNoImagesSection()
    {
        // The design authority is explicit that a read-only section with
        // nothing to show is absent, not an empty-state panel.
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            extractionPolicy: new AcceptedTriageMatchPolicy());
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "triage-without-images.eml",
            "QDOS instruction\r\nClaimant Name: Triage Claimant\r\nClaim Number: TRIAGE-NOIMG"
            + "\r\nVehicle Registration: AB12 CDE");

        await IntakeWebDriver.UploadAndProcessAsync(
            factory, client, email.FileName, email.MediaType, email.Content);

        Guid triageId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            triageId = Assert.Single(
                await scope.ServiceProvider.GetRequiredService<ITriageQueries>()
                    .ListAsync(null, CancellationToken.None)).Id;
        }

        using var response = await client.GetAsync($"/Triage/{triageId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Vehicle images", html, StringComparison.Ordinal);
    }

    private static readonly byte[] TinyPngBytes =
        Convert.FromBase64String(MultiFormatFixture.TinyPngBase64);

    // A second, distinct image: InstructionEvidenceImages de-duplicates by
    // content hash, so two identical attachments would collapse to one and the
    // test would pass while proving less than it claims.
    private static readonly byte[] TinyPngBytes2 =
        [.. Convert.FromBase64String(MultiFormatFixture.TinyPngBase64), 0x00];
}
