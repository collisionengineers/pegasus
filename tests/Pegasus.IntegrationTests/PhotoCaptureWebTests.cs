using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.PhotoCapture;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.Web.PhotoCapture;

namespace Pegasus.IntegrationTests;

/// <summary>
/// A photo link sent from a Case, through the composed Web host and the
/// Worker's own processing (FRD-28 "Routing and filing", ADR-0090): the phone
/// calls the capture API on the link's token exactly as the capture app does,
/// each item is its own intake receipt declared for the Case, and the
/// in-process drain stands in for the Worker.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class PhotoCaptureWebTests
{
    private const string Api = PhotoCaptureSurface.ApiBasePath;

    /// <summary>The query the app builds for its first "closer" shot of the front driver corner (capture.js itemQuery).</summary>
    private const string FrontDriverCornerCloser =
        "kind=damage&area=fd&stage=closer&position=1&takenAt=2031-05-06T10%3A20%3A00.000Z&live=true&original=false";

    private const string FirstOverview =
        "kind=overview&position=1&takenAt=2031-05-06T10%3A20%3A00.000Z&live=true&original=false";

    private static readonly ActionActor Issuer =
        ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);

    /// <summary>
    /// The sender marks the front driver corner, then sends its first closer
    /// shot: the item takes its story name, and once its receipt is processed
    /// the photograph is one of the Case's images under that name.
    /// </summary>
    [Fact]
    public async Task ADamagePhotoSentOnACaseLinkIsFiledOnTheCaseUnderItsStoryName()
    {
        const string expectedName = "02-01 Front driver corner closer.jpg";
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);
        var clientItemId = Guid.NewGuid();

        using (var marked = await SendAsync(
            client,
            HttpMethod.Post,
            $"{Api}/details",
            link.Token,
            new StringContent("{\"details\":{\"damageAreas\":[\"fd\"]}}", Encoding.UTF8, "application/json")))
        {
            Assert.Equal(HttpStatusCode.OK, marked.StatusCode);
        }

        using (var sent = await PutItemAsync(client, link.Token, clientItemId, FrontDriverCornerCloser, MultiFormatFixture.Photograph(1)))
        {
            Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
            var body = await JsonAsync(sent);
            Assert.Equal("created", body.GetProperty("write").GetString());
            Assert.Equal(1, body.GetProperty("count").GetInt32());
        }

        var item = await ItemAsync(factory, link.Capture.Id, clientItemId);
        Assert.Equal(expectedName, item.FileName);
        await DrainAsync(factory, item);

        var files = await CaseFilesAsync(factory, caseId);
        Assert.Single(files, file => file.Role == DocumentSemanticRole.Image && file.FileName == expectedName);
    }

    /// <summary>
    /// A capture's token reaches only its own capture: the first link's
    /// context counts none of the second's photos; the second's item id sent
    /// on the first link is a new item of the first capture and leaves the
    /// second's item as it was; and parts the second link staged can neither
    /// be added to nor committed on the first link's token.
    /// </summary>
    [Fact]
    public async Task OneLinksTokenNeitherReadsNorWritesAnotherCapturesItems()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var first = await IssueLinkAsync(factory, caseId);
        var second = await IssueLinkAsync(factory, caseId);
        var itemId = Guid.NewGuid();
        using (var sent = await PutItemAsync(client, second.Token, itemId, FirstOverview, MultiFormatFixture.Photograph(1)))
        {
            Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        }

        var secondsItem = await ItemAsync(factory, second.Capture.Id, itemId);

        using (var context = await SendAsync(client, HttpMethod.Get, $"{Api}/context", first.Token))
        {
            Assert.Equal(HttpStatusCode.OK, context.StatusCode);
            var view = await JsonAsync(context);
            Assert.Equal(CaptureContextFactory.LinkKey(first.Capture.Id), view.GetProperty("linkKey").GetString());
            Assert.Equal(0, view.GetProperty("counts").GetProperty("total").GetInt32());
        }

        using (var written = await PutItemAsync(client, first.Token, itemId, FirstOverview, MultiFormatFixture.Photograph(2)))
        {
            Assert.Equal(HttpStatusCode.Created, written.StatusCode);
            Assert.Equal("created", (await JsonAsync(written)).GetProperty("write").GetString());
        }

        var firstsItem = await ItemAsync(factory, first.Capture.Id, itemId);
        Assert.NotEqual(secondsItem.Id, firstsItem.Id);
        Assert.NotEqual(secondsItem.ContentHash, firstsItem.ContentHash);
        var secondsAfter = await ItemAsync(factory, second.Capture.Id, itemId);
        Assert.Equal(secondsItem.Id, secondsAfter.Id);
        Assert.Equal(secondsItem.ContentHash, secondsAfter.ContentHash);
        Assert.Equal(secondsItem.StagedReceiptId, secondsAfter.StagedReceiptId);

        // The parts route, which the app takes for a file larger than one
        // request: here one part of the second link's upload, staged under it.
        var photo = MultiFormatFixture.Photograph(3);
        var partItemId = Guid.NewGuid();
        var partQuery = $"{FirstOverview}&totalBytes={photo.Length}";
        using (var staged = await SendAsync(
            client, HttpMethod.Put, $"{Api}/items/{partItemId:D}/parts/1?{partQuery}", second.Token, Jpeg(photo)))
        {
            Assert.Equal(HttpStatusCode.Created, staged.StatusCode);
        }

        using (var added = await SendAsync(
            client, HttpMethod.Put, $"{Api}/items/{partItemId:D}/parts/1?{partQuery}", first.Token, Jpeg(photo)))
        {
            Assert.Equal(HttpStatusCode.Conflict, added.StatusCode);
            Assert.Equal(PhotoCaptureRefusals.ItemConflict, (await JsonAsync(added)).GetProperty("code").GetString());
        }

        var commitBody = JsonSerializer.Serialize(new
        {
            totalBytes = photo.Length,
            sha256 = Convert.ToHexString(SHA256.HashData(photo))
        });
        using (var committed = await SendAsync(
            client,
            HttpMethod.Post,
            $"{Api}/items/{partItemId:D}/commit?{partQuery}",
            first.Token,
            new StringContent(commitBody, Encoding.UTF8, "application/json")))
        {
            Assert.Equal(HttpStatusCode.Conflict, committed.StatusCode);
            Assert.Equal(PhotoCaptureRefusals.NotReady, (await JsonAsync(committed)).GetProperty("code").GetString());
        }

        Assert.Single(await ItemsAsync(factory, second.Capture.Id));
        Assert.Single(await ItemsAsync(factory, first.Capture.Id));
    }

    /// <summary>Issues a link for the Case as a member of staff does from it (FRD-28 "Photo links").</summary>
    private static async Task<IssuedPhotoLink> IssueLinkAsync(IntakeWebApplicationFactory factory, Guid caseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IssuePhotoLink>().ExecuteAsync(
            new IssuePhotoLinkRequest(
                caseId,
                Issuer,
                CustomerName: null,
                Mobile: null,
                Email: null,
                IncidentDate: null,
                PhotoCaptureMode.Guided,
                LiveCameraOnly: false,
                OriginalsInBackground: false,
                CaptureLimits.DefaultLifetimeDays,
                PhotoLinkTokenPurpose.Copy),
            CancellationToken.None);
    }

    /// <summary>One capture API call on a link's token, carried as the app carries it: <c>X-Capture-Link: c.{token}</c>.</summary>
    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        CaptureLinkSecret token,
        HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Add(PhotoCaptureSurface.CredentialHeader, PhotoCaptureAccess.CapturePrefix + token.Value);
        return await client.SendAsync(request);
    }

    /// <summary>One photo in a single request, as the app sends one no larger than a part.</summary>
    private static Task<HttpResponseMessage> PutItemAsync(
        HttpClient client,
        CaptureLinkSecret token,
        Guid clientItemId,
        string query,
        byte[] photo) =>
        SendAsync(client, HttpMethod.Put, $"{Api}/items/{clientItemId:D}?{query}", token, Jpeg(photo));

    private static ByteArrayContent Jpeg(byte[] photo)
    {
        var content = new ByteArrayContent(photo);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return content;
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static async Task<PhotoCaptureItem> ItemAsync(IntakeWebApplicationFactory factory, Guid captureId, Guid clientItemId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var item = await scope.ServiceProvider.GetRequiredService<IPhotoCaptureStore>()
            .FindItemAsync(captureId, clientItemId, CancellationToken.None);
        Assert.NotNull(item);
        return item!;
    }

    private static async Task<IReadOnlyList<PhotoCaptureItem>> ItemsAsync(IntakeWebApplicationFactory factory, Guid captureId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPhotoCaptureStore>()
            .ListItemsAsync(captureId, CancellationToken.None);
    }

    /// <summary>Processes the item's staged receipt as the Worker would.</summary>
    private static async Task DrainAsync(IntakeWebApplicationFactory factory, PhotoCaptureItem item)
    {
        var stagedReceiptId = item.StagedReceiptId
            ?? throw new InvalidOperationException("The item was taken without a staged intake receipt.");
        await using var scope = factory.Services.CreateAsyncScope();
        await IntakeWebDriver.DrainStagedAsync(scope.ServiceProvider, stagedReceiptId);
    }

    /// <summary>Every document on the Case, by its role and the name its version was filed under.</summary>
    private static async Task<CaseFile[]> CaseFilesAsync(IntakeWebApplicationFactory factory, Guid caseId)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        var files = await (from occurrence in db.Set<DocumentOccurrenceEntity>().AsNoTracking()
                           join version in db.Set<DocumentVersionEntity>().AsNoTracking() on occurrence.VersionId equals version.Id
                           where occurrence.CaseId == caseId
                           select new { occurrence.SemanticRole, version.FileName }).ToArrayAsync();
        return files.Select(file => new CaseFile(file.SemanticRole, file.FileName)).ToArray();
    }

    private sealed record CaseFile(DocumentSemanticRole Role, string FileName);
}
