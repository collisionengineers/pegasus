using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.PhotoCapture;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.Web.PhotoCapture;
using Pegasus.Web.Presentation;

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

    private const string FirstScene =
        "kind=scene&position=1&takenAt=2031-05-06T10%3A20%3A00.000Z&live=false&original=false";

    private const string FirstOtherVehicle =
        "kind=other-vehicle&position=1&takenAt=2031-05-06T10%3A20%3A00.000Z&live=false&original=false";

    /// <summary>A damage photo the app sends with no area and no stage ("Add more", the phone's own camera).</summary>
    private const string LooseDamage =
        "kind=damage&position=1&takenAt=2031-05-06T10%3A20%3A00.000Z&live=false&original=false";

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

        using (var marked = await MarkFrontDriverCornerAsync(client, link))
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

    /// <summary>
    /// A Case link's photos arrive in their baskets (R775): an overview stays
    /// In report; the accident scene arrives out of it with no tag; the other
    /// vehicle arrives out of it tagged Third party, as the routing's system
    /// work.
    /// </summary>
    [Fact]
    public async Task ACaseLinksPhotosArriveInTheirBaskets()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);
        var overview = await SendAndDrainAsync(factory, client, link, FirstOverview, seed: 1);
        var scene = await SendAndDrainAsync(factory, client, link, FirstScene, seed: 2);
        var otherVehicle = await SendAndDrainAsync(factory, client, link, FirstOtherVehicle, seed: 3);

        var images = await CaseImagesAsync(factory, caseId);

        Assert.True(Assert.Single(images, image => image.FileName == overview.FileName).InReport);
        var sceneImage = Assert.Single(images, image => image.FileName == scene.FileName);
        Assert.False(sceneImage.InReport);
        Assert.Empty(await TagsAsync(factory, sceneImage.OccurrenceId));
        var otherVehicleImage = Assert.Single(images, image => image.FileName == otherVehicle.FileName);
        Assert.False(otherVehicleImage.InReport);
        var tag = Assert.Single(await TagsAsync(factory, otherVehicleImage.OccurrenceId));
        Assert.Equal(ImageTagVocabulary.ThirdPartyId, tag.TagId);
        Assert.Equal(nameof(ActorKind.SystemWorker), tag.AppliedByKind);
        Assert.Equal(PhotoCaptureIntakeRules.RoutingActorId, tag.AppliedBySubjectId);
        Assert.Equal(
            $"photo-capture-arrival:{otherVehicleImage.OccurrenceId:N}:{ImageTagVocabulary.ThirdPartyId:N}",
            tag.OperationKey);
    }

    /// <summary>
    /// A link sent from a Case is the decision of the member of staff who sent
    /// it (R790): its photo's receipt is linked to the Case in the issuer's
    /// name, with the Intake log's reason for a Case link (R805).
    /// </summary>
    [Fact]
    public async Task ACaseLinksPhotoIsLinkedToTheCaseAsItsIssuersDecision()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);

        var item = await SendAndDrainAsync(factory, client, link, FirstOverview, seed: 1);

        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        var receiptId = item.IntakeReceiptId
            ?? throw new InvalidOperationException("The item did not learn its processed receipt.");
        var association = await db.IntakeManualAssociations.AsNoTracking()
            .SingleAsync(value => value.IntakeReceiptId == receiptId);
        Assert.Equal(caseId, association.CaseId);
        Assert.True(association.IsActive);
        Assert.Equal(nameof(ActorKind.Staff), association.ActorKind);
        Assert.Equal(DevelopmentOfflineIdentity.AdministratorId, Guid.Parse(association.ActorSubjectId));
        Assert.Equal(PhotoCaptureIntakeRules.CaseLinkDestinationReason, association.Reason);
    }

    /// <summary>
    /// The sender's decision files on any Case that is not archived (R790):
    /// on a Case whose report has been sent, a Case link's photo is filed as
    /// one of its images and the receipt's filing completes, not left a link.
    /// </summary>
    [Fact]
    public async Task ACaseLinksPhotoFilesOnACaseWhoseReportWasSent()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        await MarkReportSentAsync(factory, caseId);
        var link = await IssueLinkAsync(factory, caseId);

        var item = await SendAndDrainAsync(factory, client, link, FirstOverview, seed: 1);

        var receiptId = item.IntakeReceiptId
            ?? throw new InvalidOperationException("The item did not learn its processed receipt.");
        Assert.True(await PromotionCompletedAsync(factory, receiptId));
        Assert.Single(await CaseImagesAsync(factory, caseId), image => image.FileName == item.FileName);
    }

    /// <summary>
    /// The first photo of a Case link files and its filing completes, but the
    /// Case's images are not completed by it (R781): that waits for the
    /// capture to settle.
    /// </summary>
    [Fact]
    public async Task ACaseLinksFirstFiledPhotoDoesNotCompleteTheCasesImages()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);

        var item = await SendAndDrainAsync(factory, client, link, FirstOverview, seed: 1);

        var receiptId = item.IntakeReceiptId
            ?? throw new InvalidOperationException("The item did not learn its processed receipt.");
        Assert.True(await PromotionCompletedAsync(factory, receiptId));
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        Assert.False(await db.Cases.Where(value => value.Id == caseId).Select(value => value.ImagesComplete).SingleAsync());
    }

    /// <summary>
    /// A damage photo with no area and no stage is a loose damage photo
    /// (R802): accepted, named in the Damage group after every area the app
    /// can mark, with no area of its own, so the marked area still asks for
    /// its three stages when the sender presses Send.
    /// </summary>
    [Fact]
    public async Task ALooseDamagePhotoIsKeptInTheDamageGroupAndLeavesTheMarkedAreaMissing()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);
        var clientItemId = Guid.NewGuid();
        using (var marked = await MarkFrontDriverCornerAsync(client, link))
        {
            Assert.Equal(HttpStatusCode.OK, marked.StatusCode);
        }

        using (var sent = await PutItemAsync(client, link.Token, clientItemId, LooseDamage, MultiFormatFixture.Photograph(1)))
        {
            Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        }

        var item = await ItemAsync(factory, link.Capture.Id, clientItemId);
        Assert.Equal("04-01 Damage.jpg", item.FileName);
        Assert.Null(item.Area);
        Assert.False(item.Stage.HasValue, "A loose damage photo sent with no stage kept one.");
        using var completed = await SendAsync(client, HttpMethod.Post, $"{Api}/complete", link.Token);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var missing = (await JsonAsync(completed)).GetProperty("missing").EnumerateArray()
            .Select(entry => entry.GetProperty("code").GetString())
            .ToArray();
        Assert.Contains("damage:fd:overview", missing);
        Assert.Contains("damage:fd:closer", missing);
        Assert.Contains("damage:fd:close-up", missing);
    }

    /// <summary>
    /// An item whose evaluation is recorded before the item has recorded its
    /// staged receipt is found by the member token its receipt carries, so it
    /// still arrives in its basket. The item's staged receipt is cleared
    /// before the drain to stand for that ordering.
    /// </summary>
    [Fact]
    public async Task AnItemEvaluatedBeforeItRecordsItsReceiptStillArrivesInItsBasket()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);
        var clientItemId = Guid.NewGuid();
        using (var sent = await PutItemAsync(client, link.Token, clientItemId, FirstScene, MultiFormatFixture.Photograph(1)))
        {
            Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        }

        var item = await ItemAsync(factory, link.Capture.Id, clientItemId);
        var stagedReceiptId = item.StagedReceiptId
            ?? throw new InvalidOperationException("The item was taken without a staged intake receipt.");
        await using (var db = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            await db.PhotoCaptureItems.Where(value => value.Id == item.Id).ExecuteUpdateAsync(update => update
                .SetProperty(value => value.StagedReceiptId, (Guid?)null));
        }

        await DrainAsync(factory, stagedReceiptId);

        Assert.True(
            (await ItemAsync(factory, link.Capture.Id, clientItemId)).IntakeAssetId.HasValue,
            "The item found by its member token did not learn its intake asset.");
        Assert.False(Assert.Single(await CaseImagesAsync(factory, caseId), image => image.FileName == item.FileName).InReport);
    }

    /// <summary>
    /// Links we sent names a Case link's From as the Case's Principal, read
    /// when the page is shown (R805), on the list and on the link's own page.
    /// </summary>
    [Fact]
    public async Task LinksWeSentNamesTheCasesPrincipalAsFrom()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);
        string principalName;
        await using (var db = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            principalName = await db.Cases.Where(value => value.Id == caseId)
                .Select(value => value.Principal.Organization.Name)
                .SingleAsync();
        }

        var encoded = HtmlEncoder.Default.Encode(principalName);

        var list = await IntakeWebDriver.GetHtmlAsync(client, $"/PhotoCapture?tab={OperatorLabels.PhotoCapture.LinksWeSentKey}");
        Assert.Contains($"<td>{encoded}</td>", ListRow(list, link.Capture.Id), StringComparison.Ordinal);
        var details = await IntakeWebDriver.GetHtmlAsync(client, $"/PhotoCapture/{link.Capture.Id:D}");
        Assert.Contains($"<dt>{OperatorLabels.PhotoCapture.From}</dt><dd>{encoded}", details, StringComparison.Ordinal);
    }

    /// <summary>
    /// Remind is offered only on a live link that is due one: a live
    /// link not opened after two days offers it; an expired link that was
    /// sent with something missing does not, though a reminder is due.
    /// </summary>
    [Fact]
    public async Task LinksWeSentOffersRemindOnlyOnALiveLink()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var live = await IssueLinkAsync(factory, caseId);
        var expired = await IssueLinkAsync(factory, caseId);
        var now = IntakeWebApplicationFactory.FixedUtcNow;
        await using (var db = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            await db.PhotoCaptures.Where(value => value.Id == live.Capture.Id).ExecuteUpdateAsync(update => update
                .SetProperty(value => value.CreatedAtUtc, now.AddDays(-2)));
            await db.PhotoCaptures.Where(value => value.Id == expired.Capture.Id).ExecuteUpdateAsync(update => update
                .SetProperty(value => value.CreatedAtUtc, now.AddDays(-8))
                .SetProperty(value => value.ExpiresAtUtc, now.AddDays(-1))
                .SetProperty(value => value.SentAtUtc, (DateTimeOffset?)now.AddDays(-2)));
        }

        var list = await IntakeWebDriver.GetHtmlAsync(client, $"/PhotoCapture?tab={OperatorLabels.PhotoCapture.LinksWeSentKey}");

        Assert.Contains("handler=Remind", ListRow(list, live.Capture.Id), StringComparison.Ordinal);
        Assert.DoesNotContain("handler=Remind", ListRow(list, expired.Capture.Id), StringComparison.Ordinal);
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

    /// <summary>Processes the item's staged receipt as the Worker would; answers the processed receipt.</summary>
    private static Task<Guid> DrainAsync(IntakeWebApplicationFactory factory, PhotoCaptureItem item) =>
        DrainAsync(
            factory,
            item.StagedReceiptId
                ?? throw new InvalidOperationException("The item was taken without a staged intake receipt."));

    private static async Task<Guid> DrainAsync(IntakeWebApplicationFactory factory, Guid stagedReceiptId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return (await IntakeWebDriver.DrainStagedAsync(scope.ServiceProvider, stagedReceiptId)).ProcessedReceiptId;
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

    /// <summary>The sender marks the front driver corner on the damage diagram, as the app's details call does.</summary>
    private static Task<HttpResponseMessage> MarkFrontDriverCornerAsync(HttpClient client, IssuedPhotoLink link) =>
        SendAsync(
            client,
            HttpMethod.Post,
            $"{Api}/details",
            link.Token,
            new StringContent("{\"details\":{\"damageAreas\":[\"fd\"]}}", Encoding.UTF8, "application/json"));

    /// <summary>Sends one photo on the link, processes its receipt as the Worker would, and answers the item as it then stands.</summary>
    private static async Task<PhotoCaptureItem> SendAndDrainAsync(
        IntakeWebApplicationFactory factory,
        HttpClient client,
        IssuedPhotoLink link,
        string query,
        int seed)
    {
        var clientItemId = Guid.NewGuid();
        using (var sent = await PutItemAsync(client, link.Token, clientItemId, query, MultiFormatFixture.Photograph(seed)))
        {
            Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        }

        await DrainAsync(factory, await ItemAsync(factory, link.Capture.Id, clientItemId));
        return await ItemAsync(factory, link.Capture.Id, clientItemId);
    }

    /// <summary>The Case's images: each occurrence, the name its version was filed under, and whether it is In report.</summary>
    private static async Task<CaseImage[]> CaseImagesAsync(IntakeWebApplicationFactory factory, Guid caseId)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        var images = await (from occurrence in db.Set<DocumentOccurrenceEntity>().AsNoTracking()
                            join version in db.Set<DocumentVersionEntity>().AsNoTracking() on occurrence.VersionId equals version.Id
                            where occurrence.CaseId == caseId && occurrence.SemanticRole == DocumentSemanticRole.Image
                            select new { occurrence.Id, version.FileName, occurrence.InReport }).ToArrayAsync();
        return images.Select(image => new CaseImage(image.Id, image.FileName, image.InReport)).ToArray();
    }

    private static async Task<DocumentOccurrenceTagEntity[]> TagsAsync(IntakeWebApplicationFactory factory, Guid occurrenceId)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        return await db.Set<DocumentOccurrenceTagEntity>().AsNoTracking()
            .Where(tag => tag.OccurrenceId == occurrenceId)
            .ToArrayAsync();
    }

    /// <summary>Whether the receipt's automatic Case filing recorded its completion.</summary>
    private static async Task<bool> PromotionCompletedAsync(IntakeWebApplicationFactory factory, Guid receiptId)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        var completionKey = $"case-intake-promotion-complete:{receiptId:N}";
        return await db.IntakeMutationHistory.AsNoTracking().AnyAsync(item => item.OperationKey == completionKey);
    }

    /// <summary>
    /// The Case's report has been sent: its state, and the sent message's
    /// evidence, as <c>AssociatedMailEvidenceIntegrationTests</c> seeds them.
    /// </summary>
    private static async Task MarkReportSentAsync(IntakeWebApplicationFactory factory, Guid caseId)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        var workflow = await db.CaseWorkflows.SingleAsync(value => value.CaseId == caseId);
        workflow.State = nameof(CaseLifecycleState.ReportSent);
        workflow.ReportSentEvidence = new CaseReportSentEvidenceEntity
        {
            Id = Guid.NewGuid(), CaseId = caseId, MailboxIdentity = "test-mailbox",
            SentFolderIdentity = "sent", ImmutableItemIdentity = "sent-item",
            InternetMessageIdentity = "sent@test.invalid", ConversationIdentity = "conversation",
            ReplyChainIdentity = "reply-chain", SourceOccurrenceIdentity = "sent-source",
            SourceSha256 = new string('A', 64), MimeSha256 = new string('B', 64),
            SentAtUtc = DateTimeOffset.UtcNow, DiscoveredAtUtc = DateTimeOffset.UtcNow,
            DiscoveredByKind = nameof(ActorKind.SystemWorker), DiscoveredBySubjectId = "test",
            RetentionOperationKey = "retained-sent-test", RetentionRequestHash = new string('C', 64)
        };
        db.Add(workflow.ReportSentEvidence);
        await db.SaveChangesAsync();
    }

    /// <summary>One capture's row of a Photo capture list, from its row marker to the row's end.</summary>
    private static string ListRow(string html, Guid captureId)
    {
        var start = html.IndexOf($"data-photo-capture-row=\"{captureId:D}\"", StringComparison.OrdinalIgnoreCase);
        Assert.True(start >= 0, $"The list shows no row for capture {captureId:D}.");
        var end = html.IndexOf("</tr>", start, StringComparison.Ordinal);
        Assert.True(end > start, $"The row for capture {captureId:D} does not end.");
        return html[start..end];
    }

    private sealed record CaseFile(DocumentSemanticRole Role, string FileName);

    private sealed record CaseImage(Guid OccurrenceId, string FileName, bool InReport);
}
