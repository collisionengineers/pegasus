using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Documents;
using Pegasus.Core.PhotoCapture;

namespace Pegasus.IntegrationTests;

/// <summary>
/// A file larger than one request, sent in parts and committed (FRD-28,
/// ADR-0090): the commit checks the staged parts against the phone's digest,
/// keeps the item and clears its parts; a retried commit is answered from the
/// item; and a part the commit would refuse is refused before it is kept.
/// </summary>
public sealed partial class PhotoCaptureWebTests
{
    /// <summary>
    /// A two-part document is kept under the phone's digest and its parts are
    /// cleared; the commit sent again is a replay with nothing staged, and a
    /// commit naming other bytes is not.
    /// </summary>
    [Fact]
    public async Task AFileSentInPartsIsKeptItsPartsClearedAndARetriedCommitReplays()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);
        var itemId = Guid.NewGuid();
        var file = TwoPartPdf();
        var query = $"{PaperworkQuery}&totalBytes={file.Length}";

        await PutPartsAsync(client, link.Token, itemId, query, file);
        using (var committed = await CommitPartsAsync(client, link.Token, itemId, query, file.Length, Sha256Of(file)))
        {
            Assert.Equal(HttpStatusCode.Created, committed.StatusCode);
            Assert.Equal("created", (await JsonAsync(committed)).GetProperty("write").GetString());
        }

        var item = await ItemAsync(factory, link.Capture.Id, itemId);
        Assert.Equal(Sha256Of(file), item.ContentHash, ignoreCase: true);
        Assert.Equal(file.Length, item.ContentLength);
        Assert.NotNull(item.StagedReceiptId);
        var staging = factory.Services.GetRequiredService<IDocumentUploadStaging>();
        Assert.Null(await staging.ReadManifestAsync(itemId, CancellationToken.None));
        Assert.Empty(await staging.ListPartNumbersAsync(itemId, CancellationToken.None));

        using (var retried = await CommitPartsAsync(client, link.Token, itemId, query, file.Length, Sha256Of(file)))
        {
            Assert.Equal(HttpStatusCode.Created, retried.StatusCode);
            Assert.Equal("replayed", (await JsonAsync(retried)).GetProperty("write").GetString());
        }

        using (var other = await CommitPartsAsync(client, link.Token, itemId, query, file.Length, new string('0', 64)))
        {
            Assert.Equal(HttpStatusCode.Conflict, other.StatusCode);
            Assert.Equal(PhotoCaptureRefusals.NotReady, (await JsonAsync(other)).GetProperty("code").GetString());
        }

        var after = Assert.Single(await ItemsAsync(factory, link.Capture.Id));
        Assert.Equal(item.StagedReceiptId, after.StagedReceiptId);
    }

    /// <summary>A commit whose digest is not the staged parts' is an item conflict and keeps nothing.</summary>
    [Fact]
    public async Task ACommitWhoseDigestIsNotThePartsKeepsNothing()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);
        var itemId = Guid.NewGuid();
        var file = TwoPartPdf();
        var query = $"{PaperworkQuery}&totalBytes={file.Length}";
        await PutPartsAsync(client, link.Token, itemId, query, file);
        var changed = file.ToArray();
        changed[^1] ^= 0xFF;

        using (var committed = await CommitPartsAsync(client, link.Token, itemId, query, file.Length, Sha256Of(changed)))
        {
            Assert.Equal(HttpStatusCode.Conflict, committed.StatusCode);
            Assert.Equal(PhotoCaptureRefusals.ItemConflict, (await JsonAsync(committed)).GetProperty("code").GetString());
        }

        Assert.Empty(await ItemsAsync(factory, link.Capture.Id));
        var staging = factory.Services.GetRequiredService<IDocumentUploadStaging>();
        Assert.Equal([1, 2], (await staging.ListPartNumbersAsync(itemId, CancellationToken.None)).Order());
    }

    /// <summary>
    /// A part the commit would refuse is refused before it is read or kept,
    /// with the commit's own answer: an original on a link that takes none
    /// (R801), and a file that would carry the link past its bytes.
    /// </summary>
    [Fact]
    public async Task APartTheCommitWouldRefuseIsRefusedBeforeItIsKept()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);
        var photo = MultiFormatFixture.Photograph(1);

        using (var original = await SendAsync(
            client,
            HttpMethod.Put,
            $"{Api}/items/{Guid.NewGuid():D}/parts/1?{FirstOverviewOriginal}&totalBytes={photo.Length}",
            link.Token,
            Jpeg(photo)))
        {
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, original.StatusCode);
            Assert.Equal(PhotoCaptureRefusals.UnsupportedType, (await JsonAsync(original)).GetProperty("code").GetString());
        }

        var file = TwoPartPdf();
        await using (var db = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            await db.PhotoCaptures.Where(value => value.Id == link.Capture.Id).ExecuteUpdateAsync(update => update
                .SetProperty(value => value.AcceptedBytes, CaptureLimits.MaximumBytesPerCapture - file.Length + 1));
        }

        var itemId = Guid.NewGuid();
        using (var part = await SendAsync(
            client,
            HttpMethod.Put,
            $"{Api}/items/{itemId:D}/parts/1?{PaperworkQuery}&totalBytes={file.Length}",
            link.Token,
            Pdf(file.AsSpan(0, CaptureLimits.PartBytes).ToArray())))
        {
            Assert.Equal(HttpStatusCode.Conflict, part.StatusCode);
            Assert.Equal(PhotoCaptureRefusals.CapReached, (await JsonAsync(part)).GetProperty("code").GetString());
        }

        var staging = factory.Services.GetRequiredService<IDocumentUploadStaging>();
        Assert.Null(await staging.ReadManifestAsync(itemId, CancellationToken.None));
        Assert.Empty(await staging.ListPartNumbersAsync(itemId, CancellationToken.None));
    }

    /// <summary>A document one part and a little larger, so it goes in two parts: a PDF signature, then a fixed pattern.</summary>
    private static byte[] TwoPartPdf()
    {
        var file = new byte[CaptureLimits.PartBytes + 1000];
        for (var index = 0; index < file.Length; index++)
        {
            file[index] = (byte)(index % 251);
        }

        "%PDF-1.4\n"u8.CopyTo(file);
        return file;
    }

    /// <summary>Every part of the file in order, each accepted, as the app sends them.</summary>
    private static async Task PutPartsAsync(HttpClient client, CaptureLinkSecret token, Guid itemId, string query, byte[] file)
    {
        for (var part = 1; part <= CaptureLimits.PartCount(file.Length); part++)
        {
            var offset = (part - 1) * CaptureLimits.PartBytes;
            var length = CaptureLimits.PartLength(file.Length, part);
            using var sent = await SendAsync(
                client,
                HttpMethod.Put,
                $"{Api}/items/{itemId:D}/parts/{part}?{query}",
                token,
                Pdf(file.AsSpan(offset, length).ToArray()));
            Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        }
    }

    /// <summary>The commit the app sends once every part is accepted: the size and the phone's digest, no type.</summary>
    private static Task<HttpResponseMessage> CommitPartsAsync(
        HttpClient client,
        CaptureLinkSecret token,
        Guid itemId,
        string query,
        long totalBytes,
        string sha256) =>
        SendAsync(
            client,
            HttpMethod.Post,
            $"{Api}/items/{itemId:D}/commit?{query}",
            token,
            new StringContent(JsonSerializer.Serialize(new { totalBytes, sha256 }), Encoding.UTF8, "application/json"));

    private static ByteArrayContent Pdf(byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        return content;
    }

    private static string Sha256Of(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
