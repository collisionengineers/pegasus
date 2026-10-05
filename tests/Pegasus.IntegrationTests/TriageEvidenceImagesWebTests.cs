using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Documents;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;

namespace Pegasus.IntegrationTests;

/// <summary>
/// A Triage request's photographs are the whole subject of the assessment the
/// engineer is being asked to make. Until the Triage page showed them they were viewable nowhere:
/// the Triage page's "View e-mail" link lands on a receipt page that lists
/// attachments by name and renders none of them.
/// </summary>
public sealed partial class QdosTriageIntegrationTests
{
    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task ATriagePageShowsTheVehiclePhotographsItsRequestCarried()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEngineerTriageRequest(
            "triage-with-images.eml",
            attachments:
            [
                ("Client vehicle damage 1.png", "image/png", TinyPngBytes),
                ("Client vehicle damage 2.png", "image/png", TinyPngBytes2)
            ]);

        await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, email);

        Guid triageId;
        Guid receiptId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var triage = Assert.Single(
                await scope.ServiceProvider.GetRequiredService<ITriageQueries>()
                    .ListAsync(null, CancellationToken.None));
            triageId = triage.CaseId;
            receiptId = Assert.IsType<TriageDetail>(
                await scope.ServiceProvider.GetRequiredService<ITriageQueries>()
                    .GetAsync(triage.CaseId, CancellationToken.None)).Record.Origin!.ReceiptId;
        }

        using var response = await client.GetAsync($"/Cases/{triageId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Vehicle images", html, StringComparison.Ordinal);
        Assert.Contains("Client vehicle damage 1.png", html, StringComparison.Ordinal);
        Assert.Contains("Client vehicle damage 2.png", html, StringComparison.Ordinal);
        // Served by the one authorised, hash-verified asset route, against this
        // receipt — not copied anywhere, and not a second custody of the same
        // bytes.
        Assert.Contains($"/Received/{receiptId:D}/Asset/", html, StringComparison.Ordinal);
        // A Triage takes no crop or tag (operator, 5 October 2026).
        Assert.DoesNotContain("data-precase-asset", html, StringComparison.Ordinal);

        // Nothing is recorded on either photograph, yet each tile is a
        // rendering at tile size, not the 4032 by 3024 original; the viewer's
        // link and Open file are still the original.
        IReadOnlyList<IntakeAssetRecord> photographs;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var receipt = Assert.IsType<IntakeReceipt>(
                await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
                    .GetAsync(receiptId, CancellationToken.None));
            photographs =
            [
                .. receipt.AssetRecords.Where(asset =>
                    asset.Kind == IntakeAssetKind.Attachment && asset.MediaType == "image/png")
            ];
        }
        Assert.Equal(2, photographs.Count);
        foreach (var photograph in photographs)
        {
            var route = $"/Received/{receiptId:D}/Asset/{photograph.Id:D}";
            Assert.Contains(
                $"href=\"{route}?v={photograph.ContentHash}\"",
                html,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                $"src=\"{route}?size=thumb&amp;v={photograph.ContentHash}&amp;prep=0&amp;renderer={CaseDocumentThumbnails.RendererIdentity}\"",
                html,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain($"src=\"{route}?v=", html, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task ATriageWhoseRequestCarriedNoPhotographsRendersNoImagesSection()
    {
        // The design authority is explicit that a read-only section with
        // nothing to show is absent, not an empty-state panel.
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEngineerTriageRequest("triage-without-images.eml");

        await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, email);

        Guid triageId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            triageId = Assert.Single(
                await scope.ServiceProvider.GetRequiredService<ITriageQueries>()
                    .ListAsync(null, CancellationToken.None)).CaseId;
        }

        using var response = await client.GetAsync($"/Cases/{triageId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Vehicle images", html, StringComparison.Ordinal);
    }

    private static readonly byte[] TinyPngBytes =
        Convert.FromBase64String(MultiFormatFixture.TinyPngBase64);

    // A second, distinct image: InstructionEvidenceImages de-duplicates by
    // content hash, so identical bytes would render one entry, not two. A
    // trailing byte after IEND changes the hash and leaves the PNG readable.
    private static readonly byte[] TinyPngBytes2 =
        [.. Convert.FromBase64String(MultiFormatFixture.TinyPngBase64), 0x00];
}
