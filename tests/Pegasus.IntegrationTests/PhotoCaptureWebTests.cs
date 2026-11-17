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
public sealed partial class PhotoCaptureWebTests
{
    private const string Api = PhotoCaptureSurface.ApiBasePath;

    /// <summary>Where the website link is managed (v50 F; R911).</summary>
    private const string WebsiteLinkPage = "/Administration/PhotoLinks";

    /// <summary>The query the app builds for its first "closer" shot of the front driver corner (capture.js itemQuery).</summary>
    private const string FrontDriverCornerCloser =
        "kind=damage&area=fd&stage=closer&position=1&takenAt=2031-05-06T10%3A20%3A00.000Z&live=true&original=false";

    private const string FirstOverview =
        "kind=overview&position=1&takenAt=2031-05-06T10%3A20%3A00.000Z&live=true&original=false";

    /// <summary>The full-size original of the first overview, sent under the same item id (capture.js itemQuery).</summary>
    private const string FirstOverviewOriginal =
        "kind=overview&position=1&takenAt=2031-05-06T10%3A20%3A00.000Z&live=true&original=true";

    private const string FirstScene =
        "kind=scene&position=1&takenAt=2031-05-06T10%3A20%3A00.000Z&live=false&original=false";

    private const string FirstOtherVehicle =
        "kind=other-vehicle&position=1&takenAt=2031-05-06T10%3A20%3A00.000Z&live=false&original=false";

    /// <summary>A damage photo the app sends with no area and no stage ("Add more", the phone's own camera).</summary>
    private const string LooseDamage =
        "kind=damage&position=1&takenAt=2031-05-06T10%3A20%3A00.000Z&live=false&original=false";

    private const string SecondOverview =
        "kind=overview&position=2&takenAt=2031-05-06T10%3A20%3A00.000Z&live=true&original=false";

    /// <summary>A paperwork page the app sends from the camera roll.</summary>
    private const string PaperworkQuery = "kind=paperwork&position=1&live=false&original=false";

    /// <summary>The workflow event a Case note is recorded as, the sender's note included.</summary>
    private const string AddCaseNoteEventType = Pegasus.Core.Cases.AddCaseNote.EventType;

    /// <summary>The Case fields a Case link's typed details fill on a Case that holds a mileage, in ordinal order.</summary>
    private static readonly string[] FilledFromThePhotoLink =
    [
        Pegasus.Core.Cases.CaseDataFieldNames.ClaimantContactNumber,
        Pegasus.Core.Cases.CaseDataFieldNames.ClaimantName,
        Pegasus.Core.Cases.CaseDataFieldNames.IncidentDate
    ];

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
        const string expectedName = "02-201 Front driver corner closer.jpg";
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
    /// Direct links names a Case link's Principal under its Case, read when
    /// the page is shown (R805; v51 N, R948), on the list and on the link's
    /// own page.
    /// </summary>
    [Fact]
    public async Task DirectLinksNamesTheCasesPrincipal()
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

        var list = await IntakeWebDriver.GetHtmlAsync(client, "/PhotoCapture?scope=all");
        // A long name ends in an ellipsis with the full name on hover (v50 I),
        // and is shown at every width (v51 N; R1011).
        Assert.Contains(
            $"<small class=\"muted photo-capture-sub photo-capture-clip\" title=\"{encoded}\">{encoded}</small>",
            ListRow(list, link.Capture.Id),
            StringComparison.Ordinal);
        var details = await IntakeWebDriver.GetHtmlAsync(client, $"/PhotoCapture/{link.Capture.Id:D}");
        Assert.Contains($"<dt>{OperatorLabels.Principal}</dt><dd>{encoded}</dd>", details, StringComparison.Ordinal);
    }

    /// <summary>
    /// Resend, which took in Remind (R914), is offered only on a live link: a
    /// live link not opened after two days leads with it, opening the
    /// composer (R915) back to the list; an expired link that was sent with
    /// something missing does not, though a reminder is due. Neither row
    /// offers Remind.
    /// </summary>
    [Fact]
    public async Task DirectLinksOffersResendOnlyOnALiveLink()
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

        var list = await IntakeWebDriver.GetHtmlAsync(client, "/PhotoCapture?scope=all");

        var liveRow = ListRow(list, live.Capture.Id);
        var expiredRow = ListRow(list, expired.Capture.Id);
        Assert.Contains(
            $"href=\"/PhotoCapture/{live.Capture.Id:D}/Resend?returnUrl=%2FPhotoCapture%3Fscope%3Dall\" data-photo-link-composer>{OperatorLabels.PhotoCapture.Resend}</a>",
            liveRow,
            StringComparison.Ordinal);
        Assert.DoesNotContain("/Resend", expiredRow, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=Remind", list, StringComparison.Ordinal);
        Assert.DoesNotContain(">Remind<", list, StringComparison.Ordinal);
    }

    /// <summary>
    /// Once its Case is archived a link is dead (R793): its page is the
    /// unknown token's, an upload is refused as unavailable, the photos it
    /// already filed stay on the Case, Links we sent reads it Expired with no
    /// Resend, and it is neither sent again nor extended.
    /// </summary>
    [Fact]
    public async Task ALinkWhoseCaseIsArchivedIsDead()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);
        var filed = await SendAndDrainAsync(factory, client, link, FirstOverview, seed: 1);
        await ArchiveCaseAsync(factory, caseId);

        var unknown = await NotFoundBytesAsync(client, $"/c/{PhotoLinkToken.Create().Value}");
        Assert.Equal(unknown, await NotFoundBytesAsync(client, $"/c/{link.Token.Value}"));
        using (var sent = await PutItemAsync(client, link.Token, Guid.NewGuid(), FirstScene, MultiFormatFixture.Photograph(2)))
        {
            Assert.Equal(HttpStatusCode.NotFound, sent.StatusCode);
            Assert.Equal(PhotoCaptureRefusals.Unavailable, (await JsonAsync(sent)).GetProperty("code").GetString());
        }

        Assert.Single(await CaseImagesAsync(factory, caseId), image => image.FileName == filed.FileName);
        var row = ListRow(
            await IntakeWebDriver.GetHtmlAsync(client, "/PhotoCapture?scope=all"),
            link.Capture.Id);
        Assert.Contains(
            $">{OperatorLabels.PhotoCapture.State(PhotoCaptureState.Expired)}</span>",
            row,
            StringComparison.Ordinal);
        Assert.DoesNotContain("/Resend", row, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=Resend", row, StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var actions = scope.ServiceProvider.GetRequiredService<PhotoLinkActions>();
        var resend = await Assert.ThrowsAsync<PhotoCaptureRefusalException>(() => actions.ResendAsync(
            Issuer, link.Capture.Id, PhotoLinkTokenPurpose.Copy, NewOperationKey(), CancellationToken.None));
        Assert.Equal(PhotoCaptureRefusals.CaseUnavailable, resend.Code);
        var extend = await Assert.ThrowsAsync<PhotoCaptureRefusalException>(() => actions.ExtendAsync(
            Issuer, link.Capture.Id, CaptureLimits.DefaultLifetimeDays, NewOperationKey(), CancellationToken.None));
        Assert.Equal(PhotoCaptureRefusals.CaseUnavailable, extend.Code);
    }

    /// <summary>
    /// A Case link with originals on tells the app so; a filed photo's
    /// full-size original is taken, counted against the link's originals and
    /// bytes, and becomes the image's next version (FRD-28; R801).
    /// </summary>
    [Fact]
    public async Task AnOriginalOnACaseLinkIsCountedAndBecomesTheImagesNextVersion()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId, originalsInBackground: true);
        using (var context = await SendAsync(client, HttpMethod.Get, $"{Api}/context", link.Token))
        {
            Assert.Equal(HttpStatusCode.OK, context.StatusCode);
            Assert.True((await JsonAsync(context)).GetProperty("originals").GetBoolean());
        }

        var filed = await SendAndDrainAsync(factory, client, link, FirstOverview, seed: 1);
        var before = await CaptureCountsAsync(factory, link.Capture.Id);
        var original = MultiFormatFixture.Photograph(2);

        using (var sent = await PutItemAsync(client, link.Token, filed.ClientItemId, FirstOverviewOriginal, original))
        {
            Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        }

        var after = await CaptureCountsAsync(factory, link.Capture.Id);
        Assert.Equal(before.OriginalCount + 1, after.OriginalCount);
        Assert.Equal(before.AcceptedBytes + original.Length, after.AcceptedBytes);
        Assert.True((await ItemAsync(factory, link.Capture.Id, filed.ClientItemId)).OriginalReceived);
        var image = Assert.Single(await CaseImagesAsync(factory, caseId), value => value.FileName == filed.FileName);
        var versions = await ImageVersionsAsync(factory, image.OccurrenceId);
        Assert.Equal(2, versions.Versions);
        Assert.Equal(2, versions.Current);
    }

    /// <summary>
    /// An original that would carry the link past its bytes is refused as the
    /// cap reached, and nothing is counted or filed.
    /// </summary>
    [Fact]
    public async Task AnOriginalPastTheLinksBytesIsRefusedAndNothingIsCounted()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId, originalsInBackground: true);
        var filed = await SendAndDrainAsync(factory, client, link, FirstOverview, seed: 1);
        var original = MultiFormatFixture.Photograph(2);
        var nearlyFull = CaptureLimits.MaximumBytesPerCapture - original.Length + 1;
        await using (var db = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            await db.PhotoCaptures.Where(value => value.Id == link.Capture.Id).ExecuteUpdateAsync(update => update
                .SetProperty(value => value.AcceptedBytes, nearlyFull));
        }

        using (var sent = await PutItemAsync(client, link.Token, filed.ClientItemId, FirstOverviewOriginal, original))
        {
            Assert.Equal(HttpStatusCode.Conflict, sent.StatusCode);
            Assert.Equal(PhotoCaptureRefusals.CapReached, (await JsonAsync(sent)).GetProperty("code").GetString());
        }

        var counts = await CaptureCountsAsync(factory, link.Capture.Id);
        Assert.Equal(0, counts.OriginalCount);
        Assert.Equal(nearlyFull, counts.AcceptedBytes);
        Assert.False((await ItemAsync(factory, link.Capture.Id, filed.ClientItemId)).OriginalReceived);
        var image = Assert.Single(await CaseImagesAsync(factory, caseId), value => value.FileName == filed.FileName);
        Assert.Equal(1, (await ImageVersionsAsync(factory, image.OccurrenceId)).Versions);
    }

    /// <summary>A link sent from a Case keeps its Case's registration: one the sender types is ignored.</summary>
    [Fact]
    public async Task ACaseLinksTypedRegistrationIsIgnored()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        await SeedCaseRegistrationAsync(factory, caseId, "AB12 CDE");
        var link = await IssueLinkAsync(factory, caseId);
        Assert.Equal("AB12CDE", link.Capture.NormalizedRegistration);

        using (var typed = await SendAsync(
            client,
            HttpMethod.Post,
            $"{Api}/details",
            link.Token,
            new StringContent("{\"registration\":\"XY99 ZZZ\"}", Encoding.UTF8, "application/json")))
        {
            Assert.Equal(HttpStatusCode.OK, typed.StatusCode);
            Assert.Equal("AB12CDE", (await JsonAsync(typed)).GetProperty("registration").GetString());
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var capture = await scope.ServiceProvider.GetRequiredService<IPhotoCaptureStore>()
            .GetAsync(link.Capture.Id, CancellationToken.None);
        Assert.Equal("AB12CDE", capture?.NormalizedRegistration);
    }

    /// <summary>
    /// Extend, Log call and Cancel through the link's page each record one
    /// act in the member of staff's name, once per operation key: posting
    /// the same key again adds nothing; the link's history names who acted
    /// (R805).
    /// </summary>
    [Fact]
    public async Task CancelExtendAndLogCallEachRecordWhoDidItOnce()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);
        var page = $"/PhotoCapture/{link.Capture.Id:D}";
        var extendKey = NewOperationKey();
        var callKey = NewOperationKey();
        var cancelKey = NewOperationKey();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using (var extended = await PostStaffFormAsync(client, $"{page}?handler=Extend", extendKey, "Days", "7"))
            {
                Assert.Equal(HttpStatusCode.Redirect, extended.StatusCode);
            }

            using (var called = await PostStaffFormAsync(client, $"{page}?handler=LogCall", callKey))
            {
                Assert.Equal(HttpStatusCode.Redirect, called.StatusCode);
            }

            using (var cancelled = await PostStaffFormAsync(client, $"{page}?handler=Cancel", cancelKey))
            {
                Assert.Equal(HttpStatusCode.Redirect, cancelled.StatusCode);
            }
        }

        await using (var db = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            var aggregateId = link.Capture.Id.ToString("D");
            var acts = await db.ActionHistory.AsNoTracking()
                .Where(item => item.AggregateType == "photo_capture" && item.AggregateId == aggregateId
                    && item.EventKind != PhotoLinkActs.Issued)
                .ToListAsync();
            Assert.Equal(3, acts.Count);
            foreach (var (key, kind) in new[]
                     {
                         (extendKey, PhotoLinkActs.Extended),
                         (callKey, PhotoLinkActs.CallLogged),
                         (cancelKey, PhotoLinkActs.Cancelled)
                     })
            {
                var act = Assert.Single(acts, item => item.CorrelationId == key);
                Assert.Equal(kind, act.EventKind);
                Assert.Equal(nameof(ActorKind.Staff), act.ActorKind);
                Assert.Equal(DevelopmentOfflineIdentity.AdministratorId, Guid.Parse(act.ActorSubjectId));
            }
        }

        var name = await StaffNameAsync(factory, DevelopmentOfflineIdentity.AdministratorId);
        var details = await IntakeWebDriver.GetHtmlAsync(client, page);
        Assert.Contains(HtmlEncoder.Default.Encode($"{OperatorLabels.PhotoCapture.Cancelled} · {name}"), details, StringComparison.Ordinal);
        Assert.Contains(HtmlEncoder.Default.Encode($"{OperatorLabels.PhotoCapture.LinkExtended} · {name}"), details, StringComparison.Ordinal);
        Assert.Contains(HtmlEncoder.Default.Encode($"{OperatorLabels.PhotoCapture.CallLoggedAt} · {name}"), details, StringComparison.Ordinal);
    }

    /// <summary>
    /// The link's page takes only the acts the link offers (#2130; R1092):
    /// Extend posted under a new key to a cancelled link is not applied, the
    /// page saying the general sentence, and no act is recorded.
    /// </summary>
    [Fact]
    public async Task ExtendOnACancelledLinkIsNotAppliedAndRecordsNothing()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);
        var page = $"/PhotoCapture/{link.Capture.Id:D}";
        using (var cancelled = await PostStaffFormAsync(client, $"{page}?handler=Cancel", NewOperationKey()))
        {
            Assert.Equal(HttpStatusCode.Redirect, cancelled.StatusCode);
        }

        using (var extended = await PostStaffFormAsync(client, $"{page}?handler=Extend", NewOperationKey(), "Days", "7"))
        {
            Assert.Equal(HttpStatusCode.OK, extended.StatusCode);
            Assert.Contains(
                HtmlEncoder.Default.Encode(OperatorLabels.ActionNotApplied),
                await extended.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
        }

        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        var aggregateId = link.Capture.Id.ToString("D");
        Assert.Equal(
            0,
            await db.ActionHistory.AsNoTracking().CountAsync(item => item.AggregateType == "photo_capture"
                && item.AggregateId == aggregateId
                && item.EventKind == PhotoLinkActs.Extended));
    }

    /// <summary>
    /// Direct links rows carry no Cancel link (v51 N; R948): it stays on the
    /// link's page and the Case. Posted with a return address it goes back
    /// there, and a return address that is not this application's goes to the
    /// link's page instead.
    /// </summary>
    [Fact]
    public async Task DirectLinksRowsCarryNoCancelAndCancelReturnsWhereItWasAsked()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var live = await IssueLinkAsync(factory, caseId);
        var other = await IssueLinkAsync(factory, caseId);

        var listPath = "/PhotoCapture?scope=all";
        var list = await IntakeWebDriver.GetHtmlAsync(client, listPath);
        Assert.DoesNotContain("handler=Cancel", ListRow(list, live.Capture.Id), StringComparison.Ordinal);
        Assert.DoesNotContain(OperatorLabels.PhotoCapture.CancelLink, ListRow(list, live.Capture.Id), StringComparison.Ordinal);

        using (var cancelled = await PostStaffFormAsync(
            client, $"/PhotoCapture/{live.Capture.Id:D}?handler=Cancel", NewOperationKey(), "returnUrl", listPath))
        {
            Assert.Equal(HttpStatusCode.Redirect, cancelled.StatusCode);
            Assert.Equal(listPath, cancelled.Headers.Location?.OriginalString);
        }

        using (var elsewhere = await PostStaffFormAsync(
            client, $"/PhotoCapture/{other.Capture.Id:D}?handler=Cancel", NewOperationKey(), "returnUrl", "https://example.invalid/photos"))
        {
            Assert.Equal(HttpStatusCode.Redirect, elsewhere.StatusCode);
            Assert.Equal(
                $"/PhotoCapture/{other.Capture.Id:D}",
                elsewhere.Headers.Location?.OriginalString,
                ignoreCase: true);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IPhotoCaptureStore>();
        Assert.True((await store.GetAsync(live.Capture.Id, CancellationToken.None))?.IsRevoked);
        Assert.True((await store.GetAsync(other.Capture.Id, CancellationToken.None))?.IsRevoked);
    }

    /// <summary>
    /// Retire ends a contact's standing link for good (R796): its address
    /// answers like an unknown one, and the contact may be given a new link.
    /// </summary>
    [Fact]
    public async Task ARetiredContactLinkAnswersLikeAnUnknownOneAndTheContactMayHaveANewLink()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var organizationId = await QdosOrganizationIdAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var administration = scope.ServiceProvider.GetRequiredService<StandingPhotoLinkAdministration>();
        var first = await administration.CreateForContactAsync(Issuer, NewOperationKey(), organizationId, CancellationToken.None);

        await administration.RetireAsync(Issuer, NewOperationKey(), first.Id, first.Version, CancellationToken.None);

        var unknown = await NotFoundBytesAsync(client, $"/s/{PhotoLinkToken.CreateSlug()}");
        Assert.Equal(unknown, await NotFoundBytesAsync(client, $"/s/{first.Slug}"));
        var second = await administration.CreateForContactAsync(Issuer, NewOperationKey(), organizationId, CancellationToken.None);
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.Slug, second.Slug);
        var current = await scope.ServiceProvider.GetRequiredService<IStandingPhotoLinkStore>()
            .FindForOrganizationAsync(organizationId, CancellationToken.None);
        Assert.Equal(second.Id, current?.Id);
    }

    /// <summary>
    /// The website link is created at the address the Administrator typed
    /// (R797), on Administration › Photo links (v50 F; R911): an address of
    /// the wrong shape, and one a link already holds, are each refused and
    /// write nothing; its defaults save, and its mode stays guided whatever is
    /// posted.
    /// </summary>
    [Fact]
    public async Task TheWebsiteLinkIsCreatedAtTheTypedAddressAndItsDefaultsSave()
    {
        const string typedSlug = "ce-vehicle-photos";
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var organizationId = await QdosOrganizationIdAsync(factory);
        StandingPhotoLink contactLink;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            contactLink = await scope.ServiceProvider.GetRequiredService<StandingPhotoLinkAdministration>()
                .CreateForContactAsync(Issuer, NewOperationKey(), organizationId, CancellationToken.None);
        }

        var linksBefore = await StandingLinkCountAsync(factory);
        using (var wrongShape = await PostStaffFormAsync(
            client, WebsiteLinkPage + "?handler=CreateWebsiteLink", NewOperationKey(), "WebsiteSlug", "Not a slug!"))
        {
            Assert.Equal(HttpStatusCode.Redirect, wrongShape.StatusCode);
        }

        using (var inUse = await PostStaffFormAsync(
            client, WebsiteLinkPage + "?handler=CreateWebsiteLink", NewOperationKey(), "WebsiteSlug", contactLink.Slug))
        {
            Assert.Equal(HttpStatusCode.Redirect, inUse.StatusCode);
        }

        Assert.Equal(linksBefore, await StandingLinkCountAsync(factory));
        Assert.Null(await WebsiteLinkAsync(factory));

        using (var created = await PostStaffFormAsync(
            client, WebsiteLinkPage + "?handler=CreateWebsiteLink", NewOperationKey(), "WebsiteSlug", typedSlug))
        {
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        }

        var website = await WebsiteLinkAsync(factory);
        Assert.NotNull(website);
        Assert.Equal(typedSlug, website!.Slug);
        Assert.Equal(PhotoCaptureMode.Guided, website.ForwardedMode);

        var defaults = new Dictionary<string, string>
        {
            ["ExpectedVersion"] = website.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["LinkLiveCameraOnly"] = "false",
            ["LinkLifetimeDays"] = "14",
            ["LinkOriginalsInBackground"] = "false",
            ["LinkForwardedMode"] = nameof(PhotoCaptureMode.Quick)
        };
        using (var saved = await PostStaffFormAsync(client, WebsiteLinkPage + "?handler=UpdateWebsiteLinkDefaults", defaults))
        {
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        }

        var updated = await WebsiteLinkAsync(factory);
        Assert.NotNull(updated);
        Assert.False(updated!.LiveCameraOnly);
        Assert.Equal(14, updated.LinkLifetimeDays);
        Assert.False(updated.OriginalsInBackground);
        Assert.Equal(PhotoCaptureMode.Guided, updated.ForwardedMode);
        Assert.Equal(typedSlug, updated.Slug);
    }

    /// <summary>
    /// Direct links carries how many of its links are Awaiting, and Standing
    /// links how many standing links there are (v51 N, P; R948).
    /// </summary>
    [Fact]
    public async Task TheTabsCountAwaitingDirectLinksAndStandingLinks()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        await IssueLinkAsync(factory, caseId);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        await StandingCaptureDriver.StartAsync(factory, client, link, "ZZ99 QQQ");
        await StandingCaptureDriver.StartAsync(factory, client, link, "ZZ88 HHH");

        var list = await IntakeWebDriver.GetHtmlAsync(client, "/PhotoCapture");

        var counts = System.Text.RegularExpressions.Regex.Matches(list, "<span class=\"tab-count\">(\\d+)</span>")
            .Select(match => match.Groups[1].Value)
            .ToArray();
        Assert.Equal(2, counts.Length);
        Assert.Equal("1", counts[0]);
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        Assert.Equal(
            (await db.StandingPhotoLinks.CountAsync(value => value.RetiredAtUtc == null)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            counts[1]);
    }

    /// <summary>
    /// Standing links lists the captures its links started, all of them or
    /// one link's, each with its Destination: the Case, or Awaiting
    /// instruction. None offers Ask for it, Match to a case or Resend, which
    /// are retired for standing captures (v51 P, Q, R; R948).
    /// </summary>
    [Fact]
    public async Task StandingLinksListsTheirSubmissionsWithNoAskForItOrMatchToACase()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        await StandingCaptureDriver.SetCaseRegistrationAsync(factory, caseId, "XY99ZZZ");
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var onCase = await StandingCaptureDriver.StartAsync(factory, client, link, "XY99 ZZZ");
        var noCase = await StandingCaptureDriver.StartAsync(factory, client, link, "ZZ99 QQQ");
        Assert.Equal(caseId, onCase.Capture.CaseId);
        var now = IntakeWebApplicationFactory.FixedUtcNow;
        await using (var db = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            await db.PhotoCaptures.Where(value => value.Id == noCase.Capture.Id).ExecuteUpdateAsync(update => update
                .SetProperty(value => value.SentAtUtc, (DateTimeOffset?)now.AddHours(-1)));
        }

        var all = await IntakeWebDriver.GetHtmlAsync(client, $"/PhotoCapture?tab={OperatorLabels.PhotoCapture.StandingLinksKey}");
        var one = await IntakeWebDriver.GetHtmlAsync(
            client, $"/PhotoCapture?tab={OperatorLabels.PhotoCapture.StandingLinksKey}&link={link.Id:D}");

        foreach (var html in new[] { all, one })
        {
            var onCaseRow = StandingCaptureDriver.ListRow(html, onCase.Capture.Id);
            var noCaseRow = StandingCaptureDriver.ListRow(html, noCase.Capture.Id);
            Assert.Contains($"/Cases/{caseId:D}", onCaseRow, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(OperatorLabels.PhotoCapture.AwaitingInstruction, noCaseRow, StringComparison.Ordinal);
            Assert.DoesNotContain("Resend", noCaseRow, StringComparison.Ordinal);
            Assert.DoesNotContain(OperatorLabels.PhotoCapture.State(PhotoCaptureState.SomethingMissing), noCaseRow, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("Ask for it", all, StringComparison.Ordinal);
        Assert.DoesNotContain("Match to a case", all, StringComparison.Ordinal);
        Assert.Contains($"data-dialog=\"standing-qr-{link.Id:D}\"", all, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The Case's Files → Images shows one photo link bar (v51 I, J; R948):
    /// the link with a photo leads with its status and count and Resend,
    /// which opens the composer back to the Case's files (R915) and posts
    /// nothing itself; the empty link folds under "Other photo links on this
    /// case" with its status and "No photos".
    /// </summary>
    [Fact]
    public async Task TheCasePageLeadsWithTheLinkWithPhotosAndFoldsTheOthers()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(factory, client, "AB12 CDE", "LINK-STATUS-01");
        var withPhoto = await IssueLinkAsync(factory, caseId);
        var empty = await IssueLinkAsync(factory, caseId);
        using (var sent = await PutItemAsync(client, withPhoto.Token, Guid.NewGuid(), FirstOverview, MultiFormatFixture.Photograph(1)))
        {
            Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        }

        var files = await IntakeWebDriver.GetHtmlAsync(client, $"/Cases/{caseId:D}?section=files");

        var bar = Between(files, $"data-photo-link-bar=\"{withPhoto.Capture.Id:D}\"", "data-photo-link-others");
        Assert.Contains(OperatorLabels.PhotoCapture.PhotoCount(1, complete: false), bar, StringComparison.Ordinal);
        Assert.Contains(
            $"href=\"/PhotoCapture/{withPhoto.Capture.Id:D}/Resend?returnUrl=",
            bar,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("handler=Resend", bar, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.PhotoCapture.OtherPhotoLinksOnThisCase(1), files, StringComparison.Ordinal);
        var folded = Between(files, $"data-photo-link-other=\"{empty.Capture.Id:D}\"", "</li>");
        Assert.Contains(
            $">{OperatorLabels.PhotoCapture.State(PhotoCaptureState.SentNotOpened)}</span>",
            folded,
            StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.PhotoCapture.NoPhotos, folded, StringComparison.Ordinal);
    }

    /// <summary>The page from <paramref name="start"/> to the first <paramref name="end"/> after it.</summary>
    private static string Between(string html, string start, string end)
    {
        var from = html.IndexOf(start, StringComparison.OrdinalIgnoreCase);
        Assert.True(from >= 0, $"The page has no {start}.");
        var to = html.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        Assert.True(to > from, $"Nothing ends {start}.");
        return html[from..to];
    }

    /// <summary>
    /// A kept paperwork file of a capture with no Case opens from the link's
    /// page through the received-file page (R807), which serves it.
    /// </summary>
    [Fact]
    public async Task AKeptPaperworkFileOpensFromTheLinksPage()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var started = await StandingCaptureDriver.StartAsync(factory, client, link, "ZZ99 QQQ");
        var pdf = IntakeTestEvidence.CreatePdf("Repair estimate");
        var sent = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, "kind=paperwork&position=1&live=false&original=false", pdf, "application/pdf");
        await StandingCaptureDriver.DrainAsync(factory, sent);
        var item = await ItemAsync(factory, started.Capture.Id, sent.ClientItemId);
        var receiptId = item.IntakeReceiptId
            ?? throw new InvalidOperationException("The paperwork did not learn its receipt.");
        var assetId = item.IntakeAssetId
            ?? throw new InvalidOperationException("The paperwork did not learn its asset.");
        var keptHref = $"/Received/{receiptId:D}/Asset/{assetId:D}";

        var page = await IntakeWebDriver.GetHtmlAsync(client, $"/PhotoCapture/{started.Capture.Id:D}");

        Assert.Contains($"href=\"{keptHref}\"", page, StringComparison.OrdinalIgnoreCase);
        using var kept = await client.GetAsync(keptHref);
        Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
        Assert.Equal(pdf, await kept.Content.ReadAsByteArrayAsync());
    }

    /// <summary>
    /// A contact's own address is offered on its standing link's form with
    /// its postcode once: an address whose text already ends with the
    /// postcode is not given it again, and one without it is followed by it.
    /// </summary>
    [Fact]
    public async Task AContactsAddressIsOfferedWithItsPostcodeOnce()
    {
        using var factory = new IntakeWebApplicationFactory();
        _ = factory.Services;
        var endsWithPostcode = Guid.NewGuid();
        var withoutPostcode = Guid.NewGuid();
        await using (var db = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            db.Organizations.AddRange(
                new OrganizationEntity
                {
                    Id = endsWithPostcode, Name = "Dunnswood Repairs", Role = "repairer", Active = true, Version = 0,
                    Address = "1 Dunnswood Road, G67 3EN", Postcode = "G67 3EN"
                },
                new OrganizationEntity
                {
                    Id = withoutPostcode, Name = "Example Repairs", Role = "repairer", Active = true, Version = 0,
                    Address = "2 Example Street, Exampleton", Postcode = "EX1 2AB"
                });
            await db.SaveChangesAsync();
        }

        StandingPhotoLink first;
        StandingPhotoLink second;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var administration = scope.ServiceProvider.GetRequiredService<StandingPhotoLinkAdministration>();
            first = await administration.CreateForContactAsync(Issuer, NewOperationKey(), endsWithPostcode, CancellationToken.None);
            second = await administration.CreateForContactAsync(Issuer, NewOperationKey(), withoutPostcode, CancellationToken.None);
        }

        Assert.Equal(
            new StandingPhotoLinkAddress("Dunnswood Repairs", "1 Dunnswood Road, G67 3EN"),
            Assert.Single((await AddressBookAsync(factory, first.Id)).FromContact));
        Assert.Equal(
            new StandingPhotoLinkAddress("Example Repairs", "2 Example Street, Exampleton, EX1 2AB"),
            Assert.Single((await AddressBookAsync(factory, second.Id)).FromContact));
    }

    /// <summary>
    /// An address saved on a contact's Photo links tab is offered on the
    /// firm's form after the contact's own (R806, R815); changing it changes
    /// it there, removing it takes it away, and a change over a stale read is
    /// refused and saves nothing.
    /// </summary>
    [Fact]
    public async Task AContactsSavedAddressIsOfferedOnItsFormAfterTheContactsOwn()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var organizationId = link.OrganizationId
            ?? throw new InvalidOperationException("A contact's link names no contact.");
        var contactPage = $"/Administration/Contacts/Edit/{organizationId:D}";
        var fromContact = await StandingAddressesAsync(client, link);

        using (var added = await PostStaffFormAsync(client, $"{contactPage}?handler=AddPhotoLinkAddress", new Dictionary<string, string>
        {
            ["StandingLinkVersion"] = link.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["SavedAddressName"] = "Back yard",
            ["SavedAddressText"] = "1 Example Street, Exampleton"
        }))
        {
            Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);
        }

        var afterAdd = await StandingAddressesAsync(client, link);
        Assert.Equal(fromContact, afterAdd[..^1]);
        Assert.Equal(("Back yard", "1 Example Street, Exampleton"), afterAdd[^1]);

        var saved = Assert.Single((await AddressBookAsync(factory, link.Id)).Saved);
        var readBeforeChange = await StandingLinkAsync(factory, link.Id);
        using (var changed = await PostStaffFormAsync(client, $"{contactPage}?handler=ChangePhotoLinkAddress", new Dictionary<string, string>
        {
            ["StandingLinkVersion"] = readBeforeChange.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["SavedAddressId"] = saved.Id.ToString("D"),
            ["SavedAddressName"] = "Front yard",
            ["SavedAddressText"] = "2 Example Street, Exampleton"
        }))
        {
            Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        }

        Assert.Equal(("Front yard", "2 Example Street, Exampleton"), (await StandingAddressesAsync(client, link))[^1]);

        using (var stale = await PostStaffFormAsync(client, $"{contactPage}?handler=ChangePhotoLinkAddress", new Dictionary<string, string>
        {
            ["StandingLinkVersion"] = readBeforeChange.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["SavedAddressId"] = saved.Id.ToString("D"),
            ["SavedAddressName"] = "Side gate",
            ["SavedAddressText"] = "3 Example Street, Exampleton"
        }))
        {
            Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        }

        var afterStale = Assert.Single((await AddressBookAsync(factory, link.Id)).Saved);
        Assert.Equal("Front yard", afterStale.Name);
        Assert.Equal("2 Example Street, Exampleton", afterStale.Address);

        var readBeforeRemove = await StandingLinkAsync(factory, link.Id);
        using (var removed = await PostStaffFormAsync(client, $"{contactPage}?handler=RemovePhotoLinkAddress", new Dictionary<string, string>
        {
            ["StandingLinkVersion"] = readBeforeRemove.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["SavedAddressId"] = saved.Id.ToString("D")
        }))
        {
            Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
        }

        Assert.Equal(fromContact, await StandingAddressesAsync(client, link));
    }

    /// <summary>The website link has no contact and keeps no saved addresses (R806): adding one is refused and nothing is saved.</summary>
    [Fact]
    public async Task TheWebsiteLinkTakesNoSavedAddress()
    {
        using var factory = new IntakeWebApplicationFactory();
        _ = factory.Services;
        await using var scope = factory.Services.CreateAsyncScope();
        var administration = scope.ServiceProvider.GetRequiredService<StandingPhotoLinkAdministration>();
        var website = await administration.CreateWebsiteAsync(Issuer, NewOperationKey(), "ce-vehicle-photos", CancellationToken.None);

        var refused = await Assert.ThrowsAsync<StandingPhotoLinkException>(() => administration.AddAddressAsync(
            Issuer, NewOperationKey(), website.Id, website.Version, "Back yard", "1 Example Street, Exampleton", CancellationToken.None));

        Assert.Equal(StandingPhotoLinkError.NotFound, refused.Error);
        Assert.True((await AddressBookAsync(factory, website.Id)).IsEmpty);
        Assert.Equal(website.Version, (await StandingLinkAsync(factory, website.Id)).Version);
    }

    /// <summary>
    /// Send with Quick stores who the link is for (R808); with Guided it
    /// stores no one even when a Type is posted; Quick without a Type is
    /// refused beside the Type with the operator's sentence (R868) and issues
    /// nothing, and a Type that is not one of the page's own is not its post.
    /// </summary>
    [Fact]
    public async Task SendStoresAQuickLinksTypeAndOnlyAQuickLinks()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var send = $"/PhotoCapture/Send/{caseId:D}";

        var quick = await SendFormAsync(factory, client, send, caseId, nameof(PhotoCaptureMode.Quick), nameof(PhotoCaptureQuickLinkFor.AccidentManagement));
        Assert.Equal(PhotoCaptureQuickLinkFor.AccidentManagement, quick?.QuickLinkFor);
        var guided = await SendFormAsync(factory, client, send, caseId, nameof(PhotoCaptureMode.Guided), nameof(PhotoCaptureQuickLinkFor.Bodyshop));
        Assert.NotNull(guided);
        Assert.False(guided!.QuickLinkFor.HasValue, "A guided link stored who it is for.");

        var before = await CaptureIdsAsync(factory, caseId);
        using (var untyped = await PostStaffFormAsync(client, send, new Dictionary<string, string>
        {
            ["OperationKey"] = NewOperationKey(),
            ["Mode"] = nameof(PhotoCaptureMode.Quick),
            ["Days"] = "7",
            ["Submit"] = Pegasus.Web.Pages.PhotoCapture.SendModel.ToCopy
        }))
        {
            Assert.Equal(HttpStatusCode.OK, untyped.StatusCode);
            var html = await untyped.Content.ReadAsStringAsync();
            var typeField = html[html.IndexOf("data-photo-link-type", StringComparison.Ordinal)..];
            typeField = typeField[..typeField.IndexOf("</fieldset>", StringComparison.Ordinal)];
            Assert.Contains(
                HtmlEncoder.Default.Encode(OperatorLabels.PhotoCapture.ChooseAQuickLinkType),
                typeField,
                StringComparison.Ordinal);
        }

        using (var unknownType = await PostStaffFormAsync(client, send, new Dictionary<string, string>
        {
            ["OperationKey"] = NewOperationKey(),
            ["Mode"] = nameof(PhotoCaptureMode.Quick),
            ["QuickLinkFor"] = "99",
            ["Days"] = "7",
            ["Submit"] = Pegasus.Web.Pages.PhotoCapture.SendModel.ToCopy
        }))
        {
            Assert.Equal(HttpStatusCode.BadRequest, unknownType.StatusCode);
        }

        Assert.Equal(before, await CaptureIdsAsync(factory, caseId));
    }

    /// <summary>
    /// The firm's token on a quick Case link opens the firm's own start: quick,
    /// naming who it is for, and for Accident management the Case's Principal
    /// as the firm; a Bodyshop is named nowhere and offered no address (R798, R808).
    /// </summary>
    [Fact]
    public async Task AQuickCaseLinksContextIsTheFirmsOwn()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var principalName = await PrincipalNameAsync(factory, caseId);
        var accidentManagement = await IssueQuickLinkAsync(factory, caseId, PhotoCaptureQuickLinkFor.AccidentManagement);
        var bodyshop = await IssueQuickLinkAsync(factory, caseId, PhotoCaptureQuickLinkFor.Bodyshop);

        var forFirm = await ContextAsync(client, accidentManagement.Token);
        Assert.Equal("quick", forFirm.GetProperty("mode").GetString());
        Assert.Equal("accident-management", forFirm.GetProperty("quickLinkFor").GetString());
        Assert.Equal(principalName, forFirm.GetProperty("standing").GetProperty("name").GetString());
        Assert.Equal(CaptureCodes.CaseKind, forFirm.GetProperty("standing").GetProperty("kind").GetString());

        var forBodyshop = await ContextAsync(client, bodyshop.Token);
        Assert.Equal("quick", forBodyshop.GetProperty("mode").GetString());
        Assert.Equal("bodyshop", forBodyshop.GetProperty("quickLinkFor").GetString());
        var bodyshopView = forBodyshop.GetProperty("standing");
        Assert.Equal(JsonValueKind.Null, bodyshopView.GetProperty("name").ValueKind);
        Assert.Equal(0, bodyshopView.GetProperty("addresses").GetArrayLength());
    }

    /// <summary>
    /// The firm hands a quick Case link on to their customer (R798): the same
    /// capture gains a customer's link and a Session token, its Opened stands,
    /// Links we sent says it was sent on, and the customer's link opens the
    /// guided app with no firm's start. A guided Case link cannot be handed on.
    /// </summary>
    [Fact]
    public async Task AQuickCaseLinkIsHandedOnToTheFirmsCustomer()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        await SeedCaseRegistrationAsync(factory, caseId, "AB12 CDE");
        var quick = await IssueQuickLinkAsync(factory, caseId, PhotoCaptureQuickLinkFor.AccidentManagement);
        var guided = await IssueLinkAsync(factory, caseId);
        await ContextAsync(client, quick.Token);
        var openedBefore = (await StandingCaptureDriver.CaptureAsync(factory, quick.Capture.Id)).OpenedAtUtc;
        var capturesBefore = await CaptureIdsAsync(factory, caseId);

        using var handedOn = await SendAsync(
            client, HttpMethod.Post, $"{Api}/handoff", quick.Token, new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Created, handedOn.StatusCode);
        var answer = await JsonAsync(handedOn);
        Assert.Equal("AB12 CDE", answer.GetProperty("registration").GetString());
        var url = answer.GetProperty("url").GetString()
            ?? throw new InvalidOperationException("The hand-on answered no link.");
        var sessionToken = answer.GetProperty("sessionToken").GetString();
        var customerToken = url[(url.LastIndexOf('/') + 1)..];
        Assert.NotEqual(customerToken, sessionToken);
        Assert.NotEqual(quick.Token.Value, customerToken);
        Assert.Equal(capturesBefore, await CaptureIdsAsync(factory, caseId));
        Assert.Equal(openedBefore, (await StandingCaptureDriver.CaptureAsync(factory, quick.Capture.Id)).OpenedAtUtc);
        var row = ListRow(
            await IntakeWebDriver.GetHtmlAsync(client, "/PhotoCapture?scope=all"),
            quick.Capture.Id);
        Assert.Contains(OperatorLabels.PhotoCapture.ForwardedToCustomer, row, StringComparison.Ordinal);

        var forCustomer = await ContextAsync(client, new CaptureLinkSecret(customerToken));
        Assert.Equal("guided", forCustomer.GetProperty("mode").GetString());
        Assert.Equal(JsonValueKind.Null, forCustomer.GetProperty("quickLinkFor").ValueKind);
        Assert.Equal(JsonValueKind.Null, forCustomer.GetProperty("standing").ValueKind);

        using var refused = await SendAsync(
            client, HttpMethod.Post, $"{Api}/handoff", guided.Token, new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.Equal(PhotoCaptureRefusals.Unavailable, (await JsonAsync(refused)).GetProperty("code").GetString());
    }

    /// <summary>A Bodyshop's quick Case link owes the VIN (R808); an Accident management one does not.</summary>
    [Fact]
    public async Task ABodyshopsQuickCaseLinkOwesTheVin()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var bodyshop = await IssueQuickLinkAsync(factory, caseId, PhotoCaptureQuickLinkFor.Bodyshop);
        var accidentManagement = await IssueQuickLinkAsync(factory, caseId, PhotoCaptureQuickLinkFor.AccidentManagement);

        var bodyshopMissing = await MissingOnCompleteAsync(client, bodyshop.Token);
        var accidentManagementMissing = await MissingOnCompleteAsync(client, accidentManagement.Token);

        Assert.Contains(("vin", "No VIN photo"), bodyshopMissing);
        Assert.DoesNotContain(accidentManagementMissing, missing => missing.Code == "vin");
    }

    /// <summary>
    /// Send opens with the standard wording filled in, the link still a
    /// placeholder until it exists (R806), and "Put the standard wording
    /// back" returns the standard wording over what staff typed. The message
    /// is drawn only where a staff send is composed and the Case has a
    /// mailbox that can send: here a double that never sends, and a seeded
    /// Outgoing mailbox.
    /// </summary>
    [Fact]
    public async Task SendOpensWithTheStandardWordingAndPutsItBack()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(baseFactory);
        await SeedCaseRegistrationAsync(baseFactory, caseId, "AB12 CDE");
        await SeedSendableMailboxAsync(baseFactory);
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            CaseWebTestSupport.Substitute<Pegasus.Core.Operations.IStaffMailSend>(services, new NeverSendingStaffMailSend())));
        using var client = IntakeWebDriver.CreateClient(factory);
        var send = $"/PhotoCapture/Send/{caseId:D}";
        var expiry = OperatorLabels.OfficeDate(IntakeWebApplicationFactory.FixedUtcNow.AddDays(CaptureLimits.DefaultLifetimeDays));

        var opened = TextareaValue(await IntakeWebDriver.GetHtmlAsync(client, send), "Body");

        Assert.Contains("Collision Engineers need a few photos of AB12 CDE to assess the damage.", opened, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.PhotoCapture.PhotoLinkPlaceholder, opened, StringComparison.Ordinal);
        Assert.Contains($"The link works until {expiry}.", opened, StringComparison.Ordinal);

        using var reset = await PostStaffFormAsync(client, send, new Dictionary<string, string>
        {
            ["Mode"] = nameof(PhotoCaptureMode.Guided),
            ["Days"] = "7",
            ["LiveCameraOnly"] = "true",
            ["OriginalsInBackground"] = "true",
            ["Subject"] = "Photos",
            ["Body"] = "Words staff typed instead.",
            ["StandardBody"] = opened,
            ["Submit"] = Pegasus.Web.Pages.PhotoCapture.SendModel.StandardWording
        });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        var putBack = TextareaValue(await reset.Content.ReadAsStringAsync(), "Body");
        Assert.DoesNotContain("Words staff typed instead.", putBack, StringComparison.Ordinal);
        Assert.Contains("Collision Engineers need a few photos of AB12 CDE to assess the damage.", putBack, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.PhotoCapture.PhotoLinkPlaceholder, putBack, StringComparison.Ordinal);
    }

    /// <summary>
    /// A Case link's Send settles its batch once its photo has filed (R781,
    /// R792): the Case's images complete, the sender's note is written, the
    /// batch closes and the Engineer's bell rings. A second settle with
    /// nothing new writes nothing.
    /// </summary>
    [Fact]
    public async Task ACaseLinksSendSettlesItsBatchOnce()
    {
        const string note = "Please call before 5pm.";
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        await StandingCaptureDriver.AssignEngineerAsync(factory, caseId);
        var link = await IssueLinkAsync(factory, caseId);
        await SendAndDrainAsync(factory, client, link, FirstOverview, seed: 1);
        await StandingCaptureDriver.CompleteAsync(client, link.Token.Value, note);
        Assert.False(await StandingCaptureDriver.ImagesCompleteAsync(factory, caseId));

        await StandingCaptureDriver.SettleAsync(factory);

        Assert.True(await StandingCaptureDriver.ImagesCompleteAsync(factory, caseId));
        await AssertSettledOnceAsync(factory, caseId, link.Capture.Id, note);
        Assert.True((await StandingCaptureDriver.CaptureAsync(factory, link.Capture.Id)).ImagesCompletedAtUtc.HasValue);

        await StandingCaptureDriver.SettleAsync(factory);

        await AssertSettledOnceAsync(factory, caseId, link.Capture.Id, note);
    }

    /// <summary>
    /// A second batch after staff marked the Case's images incomplete settles
    /// without completing them again: images complete once per capture
    /// (R792). The note is not written twice, and the bell rings for the
    /// second batch's photos too.
    /// </summary>
    [Fact]
    public async Task ASecondBatchNeverCompletesTheImagesAgain()
    {
        const string note = "Please call before 5pm.";
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        await StandingCaptureDriver.AssignEngineerAsync(factory, caseId);
        var link = await IssueLinkAsync(factory, caseId);
        await SendAndDrainAsync(factory, client, link, FirstOverview, seed: 1);
        await StandingCaptureDriver.CompleteAsync(client, link.Token.Value, note);
        await StandingCaptureDriver.SettleAsync(factory);
        Assert.True(await StandingCaptureDriver.ImagesCompleteAsync(factory, caseId));
        await using (var db = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            // Staff mark the images incomplete; nothing on the capture changes.
            await db.Cases.Where(value => value.Id == caseId).ExecuteUpdateAsync(update => update
                .SetProperty(value => value.ImagesComplete, false));
        }

        await StandingCaptureDriver.MoveLastSettleBackAsync(factory, link.Capture.Id);
        await SendAndDrainAsync(factory, client, link, SecondOverview, seed: 2);
        await StandingCaptureDriver.CompleteAsync(client, link.Token.Value);
        await StandingCaptureDriver.SettleAsync(factory);

        Assert.False(await StandingCaptureDriver.ImagesCompleteAsync(factory, caseId));
        Assert.Single(await StandingCaptureDriver.CaseEventReasonsAsync(factory, caseId, EfPhotoCaptureCaseWriter.ImagesCompletedEventType));
        Assert.Single(await StandingCaptureDriver.CaseEventReasonsAsync(factory, caseId, AddCaseNoteEventType));
        Assert.Equal(2, (await StandingCaptureDriver.CaptureAsync(factory, link.Capture.Id)).SettledBatches);
        Assert.Equal(2, await StandingCaptureDriver.PhotosReceivedCountAsync(factory, caseId));
    }

    /// <summary>A Send with no photos settles nothing (R792): no batch closes and the Case is untouched.</summary>
    [Fact]
    public async Task ASendWithNoPhotosSettlesNothing()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);
        await StandingCaptureDriver.CompleteAsync(client, link.Token.Value, "Nothing to add.");

        await StandingCaptureDriver.SettleAsync(factory);

        var capture = await StandingCaptureDriver.CaptureAsync(factory, link.Capture.Id);
        Assert.Equal(0, capture.SettledBatches);
        Assert.False(capture.SettledAtUtc.HasValue, "A Send with no photos recorded a settle.");
        Assert.False(await StandingCaptureDriver.ImagesCompleteAsync(factory, caseId));
        Assert.Empty(await StandingCaptureDriver.CaseEventReasonsAsync(factory, caseId, AddCaseNoteEventType));
    }

    /// <summary>
    /// Cancelling a link stops uploads only (R794): photos it already
    /// received still settle and complete the Case's images; a cancelled link
    /// with no photos settles nothing.
    /// </summary>
    [Fact]
    public async Task ACancelledLinksPhotosStillSettle()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var withPhotos = await IssueLinkAsync(factory, caseId);
        var empty = await IssueLinkAsync(factory, caseId);
        await SendAndDrainAsync(factory, client, withPhotos, FirstOverview, seed: 1);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var actions = scope.ServiceProvider.GetRequiredService<PhotoLinkActions>();
            await actions.RevokeAsync(Issuer, withPhotos.Capture.Id, NewOperationKey(), CancellationToken.None);
            await actions.RevokeAsync(Issuer, empty.Capture.Id, NewOperationKey(), CancellationToken.None);
        }

        await StandingCaptureDriver.SettleAsync(factory);

        Assert.True(await StandingCaptureDriver.ImagesCompleteAsync(factory, caseId));
        Assert.Single(await StandingCaptureDriver.CaseEventReasonsAsync(factory, caseId, EfPhotoCaptureCaseWriter.ImagesCompletedEventType));
        Assert.Equal(1, (await StandingCaptureDriver.CaptureAsync(factory, withPhotos.Capture.Id)).SettledBatches);
        Assert.Equal(0, (await StandingCaptureDriver.CaptureAsync(factory, empty.Capture.Id)).SettledBatches);
    }

    /// <summary>
    /// The sender's typed details fill the Case's empty fields as Photo link
    /// facts (R783); the typed inspection location is not written to the
    /// Case and stays on the capture (R811); a field the Case already holds
    /// is not overwritten.
    /// </summary>
    [Fact]
    public async Task TheTypedDetailsFillOnlyTheCasesEmptyFieldsAndNeverTheLocation()
    {
        const string location = "12 Example Road, Exampleton";
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);
        using (var typed = await SendAsync(
            client,
            HttpMethod.Post,
            $"{Api}/details",
            link.Token,
            new StringContent(
                JsonSerializer.Serialize(new
                {
                    customerName = "Sam Example",
                    mobile = "07700 900123",
                    incidentDate = "2031-04-30",
                    details = new { inspectionLocation = location, mileage = 99999 }
                }),
                Encoding.UTF8,
                "application/json")))
        {
            Assert.Equal(HttpStatusCode.OK, typed.StatusCode);
        }

        await SendAndDrainAsync(factory, client, link, FirstOverview, seed: 1);
        await StandingCaptureDriver.CompleteAsync(client, link.Token.Value);
        await StandingCaptureDriver.SettleAsync(factory);

        await using (var db = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            var filled = await db.CaseDataFields.AsNoTracking()
                .Where(value => value.WorkId == caseId && value.SourceKind == CaseDataCodes.PhotoLink)
                .ToListAsync();
            Assert.Equal(FilledFromThePhotoLink, filled.Select(value => value.FieldName).Order(StringComparer.Ordinal));
            Assert.All(filled, value =>
            {
                Assert.Equal(CaseDataCodes.Fact, value.ValueKind);
                Assert.Equal(PhotoCaptureCaseFields.SourceLabel(link.Capture), value.SourceLabel);
            });
            Assert.Equal("Sam Example", Assert.Single(filled, value => value.FieldName == Pegasus.Core.Cases.CaseDataFieldNames.ClaimantName).Value);
            Assert.Equal("07700 900123", Assert.Single(filled, value => value.FieldName == Pegasus.Core.Cases.CaseDataFieldNames.ClaimantContactNumber).Value);
            Assert.Equal("2031-04-30", Assert.Single(filled, value => value.FieldName == Pegasus.Core.Cases.CaseDataFieldNames.IncidentDate).Value);
            Assert.False(await db.CaseDataFields.AnyAsync(value =>
                value.WorkId == caseId && value.FieldName == Pegasus.Core.Cases.CaseDataFieldNames.InspectionAddress));

            // A fill into empty fields replaced nothing, so its history has
            // no before value (R843).
            var aggregateId = caseId.ToString("D");
            var history = Assert.Single(await db.ActionHistory.AsNoTracking()
                .Where(item => item.AggregateId == aggregateId && item.EventKind == EfPhotoCaptureCaseWriter.FilledEventType)
                .ToListAsync());
            Assert.Null(history.BeforeJson);
        }

        var page = await IntakeWebDriver.GetHtmlAsync(client, $"/PhotoCapture/{link.Capture.Id:D}");
        Assert.Contains(HtmlEncoder.Default.Encode(location), page, StringComparison.Ordinal);
    }

    /// <summary>
    /// A link whose Case is archived closes its batch with nothing written to
    /// the Case (R793): no images completed, no note, no bell.
    /// </summary>
    [Fact]
    public async Task AnArchivedCasesBatchClosesWithNothingWritten()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        await StandingCaptureDriver.AssignEngineerAsync(factory, caseId);
        var link = await IssueLinkAsync(factory, caseId);
        await SendAndDrainAsync(factory, client, link, FirstOverview, seed: 1);
        await StandingCaptureDriver.CompleteAsync(client, link.Token.Value, "Please call before 5pm.");
        await ArchiveCaseAsync(factory, caseId);

        await StandingCaptureDriver.SettleAsync(factory);

        var capture = await StandingCaptureDriver.CaptureAsync(factory, link.Capture.Id);
        Assert.Equal(1, capture.SettledBatches);
        Assert.False(capture.ImagesCompletedAtUtc.HasValue, "An archived Case's images were completed.");
        Assert.False(await StandingCaptureDriver.ImagesCompleteAsync(factory, caseId));
        Assert.Empty(await StandingCaptureDriver.CaseEventReasonsAsync(factory, caseId, EfPhotoCaptureCaseWriter.ImagesCompletedEventType));
        Assert.Empty(await StandingCaptureDriver.CaseEventReasonsAsync(factory, caseId, AddCaseNoteEventType));
        Assert.Equal(0, await StandingCaptureDriver.PhotosReceivedCountAsync(factory, caseId));
    }

    /// <summary>
    /// The close records the batch with its Case and its bell owed, and the
    /// same run rings the bell and clears it: one notification for the
    /// Engineer, nothing owed. A bell still owed, as a Worker that stopped
    /// between the close and the ring leaves it, is rung by a later run, once.
    /// </summary>
    [Fact]
    public async Task ACaseBatchsBellIsOwedWithItsCloseAndRungOnceEvenOnALaterRun()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        await StandingCaptureDriver.AssignEngineerAsync(factory, caseId);
        var link = await IssueLinkAsync(factory, caseId);
        await SendAndDrainAsync(factory, client, link, FirstOverview, seed: 1);
        await StandingCaptureDriver.CompleteAsync(client, link.Token.Value);

        await StandingCaptureDriver.SettleAsync(factory);

        var closed = Assert.Single(await StandingCaptureDriver.BatchesAsync(factory, link.Capture.Id));
        Assert.Equal(1, closed.Batch);
        Assert.Equal(caseId, closed.CaseId);
        Assert.False(closed.SubmissionGroupId.HasValue, "A Case batch recorded a submission group.");
        Assert.False(closed.BellOwedAtUtc.HasValue, "The settle left its bell owed.");
        Assert.Equal(1, await StandingCaptureDriver.PhotosReceivedCountAsync(factory, caseId));

        await StandingCaptureDriver.OweBellAsync(factory, link.Capture.Id, batch: 1);
        await StandingCaptureDriver.SettleAsync(factory);

        Assert.Equal(2, await StandingCaptureDriver.PhotosReceivedCountAsync(factory, caseId));
        Assert.False(
            Assert.Single(await StandingCaptureDriver.BatchesAsync(factory, link.Capture.Id)).BellOwedAtUtc.HasValue,
            "The owed bell was not cleared once rung.");

        await StandingCaptureDriver.SettleAsync(factory);

        Assert.Equal(2, await StandingCaptureDriver.PhotosReceivedCountAsync(factory, caseId));
    }

    /// <summary>
    /// Paperwork sent as a JPEG files on the Case as a document of type
    /// Other, not as one of its images: its type is its kind's (R803).
    /// </summary>
    [Fact]
    public async Task PaperworkSentAsAJpegFilesAsOtherAndNotAsAnImage()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);

        var paperwork = await SendAndDrainAsync(factory, client, link, PaperworkQuery, seed: 1);

        var receiptId = paperwork.IntakeReceiptId
            ?? throw new InvalidOperationException("The paperwork did not learn its processed receipt.");
        Assert.True(await PromotionCompletedAsync(factory, receiptId));
        var files = await CaseFilesAsync(factory, caseId);
        Assert.Equal(DocumentSemanticRole.Other, Assert.Single(files, file => file.FileName == paperwork.FileName).Role);
        Assert.DoesNotContain(files, file => file.Role == DocumentSemanticRole.Image);
    }

    /// <summary>What one settle of a Case link's first batch leaves, however often the settle runs.</summary>
    private static async Task AssertSettledOnceAsync(IntakeWebApplicationFactory factory, Guid caseId, Guid captureId, string note)
    {
        Assert.Single(await StandingCaptureDriver.CaseEventReasonsAsync(factory, caseId, EfPhotoCaptureCaseWriter.ImagesCompletedEventType));
        Assert.Equal(
            "Customer's note (photo link): " + note,
            Assert.Single(await StandingCaptureDriver.CaseEventReasonsAsync(factory, caseId, AddCaseNoteEventType)));
        Assert.Equal(1, (await StandingCaptureDriver.CaptureAsync(factory, captureId)).SettledBatches);
        Assert.Equal(1, await StandingCaptureDriver.PhotosReceivedCountAsync(factory, caseId));
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
                QuickLinkFor: null,
                LiveCameraOnly: false,
                OriginalsInBackground: false,
                CaptureLimits.DefaultLifetimeDays,
                PhotoLinkTokenPurpose.Copy,
                NewOperationKey()),
            CancellationToken.None);
    }

    /// <summary>Issues a link for the Case with full-size originals on or off, as Send photo link's choice sets it.</summary>
    private static async Task<IssuedPhotoLink> IssueLinkAsync(
        IntakeWebApplicationFactory factory,
        Guid caseId,
        bool originalsInBackground)
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
                QuickLinkFor: null,
                LiveCameraOnly: false,
                originalsInBackground,
                CaptureLimits.DefaultLifetimeDays,
                PhotoLinkTokenPurpose.Copy,
                NewOperationKey()),
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
    internal static async Task MarkReportSentAsync(IntakeWebApplicationFactory factory, Guid caseId)
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
            RetentionOperationKey = "retained-sent-test", RetentionRequestHash = new string('C', 64),
            // Linked evidence carries who linked it, as the sent poll records
            // it; without it the Case's reads refuse the row.
            LinkedAtUtc = DateTimeOffset.UtcNow, LinkedByKind = nameof(ActorKind.SystemWorker),
            LinkedBySubjectId = "approved-mailbox-sent-poll", LinkedByRolesJson = "[]"
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

    private static string NewOperationKey() => Guid.NewGuid().ToString("N");

    /// <summary>A page answer that must be 404; answers its bytes.</summary>
    private static async Task<byte[]> NotFoundBytesAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        return await response.Content.ReadAsByteArrayAsync();
    }

    /// <summary>
    /// Archives the Case, as <c>AssociatedMailEvidenceIntegrationTests</c>
    /// archives one: the workflow's archive time and who archived it.
    /// </summary>
    private static async Task ArchiveCaseAsync(IntakeWebApplicationFactory factory, Guid caseId)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        var workflow = await db.CaseWorkflows.SingleAsync(value => value.CaseId == caseId);
        workflow.ArchivedAtUtc = DateTimeOffset.UtcNow;
        workflow.ArchivedByKind = nameof(ActorKind.Staff);
        workflow.ArchivedBySubjectId = Guid.NewGuid().ToString();
        workflow.ArchivedByRolesJson = "[\"Administrator\"]";
        workflow.ArchiveReason = "Archived while a photo link was live.";
        await db.SaveChangesAsync();
    }

    /// <summary>The Case's confirmed registration, as its record carries one.</summary>
    private static async Task SeedCaseRegistrationAsync(IntakeWebApplicationFactory factory, Guid caseId, string registration)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        db.Add(new CaseDataFieldEntity
        {
            WorkId = caseId, FieldName = Pegasus.Core.Cases.CaseDataFieldNames.VehicleRegistration,
            ValueKind = CaseDataCodes.Confirmed, ValueType = CaseDataCodes.Text, Value = registration,
            SourceKind = "intake_evidence", SourceIdentity = "registration", SourceLabel = "photo capture fixture",
            PolicyKey = "photo-capture-fixture", PolicyVersion = 1,
            ConfirmedByActor = "staff", ConfirmedAtUtc = IntakeWebApplicationFactory.FixedUtcNow
        });
        await db.SaveChangesAsync();
    }

    /// <summary>The capture's originals and its bytes, as its row counts them.</summary>
    private static async Task<CaptureCounts> CaptureCountsAsync(IntakeWebApplicationFactory factory, Guid captureId)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        return await db.PhotoCaptures.AsNoTracking()
            .Where(value => value.Id == captureId)
            .Select(value => new CaptureCounts(value.OriginalCount, value.AcceptedBytes))
            .SingleAsync();
    }

    /// <summary>How many versions the image's document has, and which one its occurrence names.</summary>
    private static async Task<ImageVersions> ImageVersionsAsync(IntakeWebApplicationFactory factory, Guid occurrenceId)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        var occurrence = await db.Set<DocumentOccurrenceEntity>().AsNoTracking()
            .SingleAsync(value => value.Id == occurrenceId);
        var versions = await db.Set<DocumentVersionEntity>().AsNoTracking()
            .Where(value => value.DocumentId == occurrence.DocumentId)
            .Select(value => new { value.Id, value.Version })
            .ToArrayAsync();
        return new(versions.Length, versions.Single(value => value.Id == occurrence.VersionId).Version);
    }

    /// <summary>
    /// Posts a staff page's form as the browser does: the antiforgery token
    /// the session's pages carry, the act's operation key, and at most one
    /// further field.
    /// </summary>
    private static Task<HttpResponseMessage> PostStaffFormAsync(
        HttpClient client,
        string url,
        string operationKey,
        string? name = null,
        string? value = null)
    {
        var fields = new Dictionary<string, string> { ["OperationKey"] = operationKey };
        if (name is not null)
        {
            fields[name] = value ?? string.Empty;
        }

        return PostStaffFormAsync(client, url, fields);
    }

    private static async Task<HttpResponseMessage> PostStaffFormAsync(
        HttpClient client,
        string url,
        Dictionary<string, string> fields)
    {
        var form = new Dictionary<string, string>(fields)
        {
            ["__RequestVerificationToken"] = await IntakeWebDriver.GetAntiforgeryTokenAsync(client)
        };
        using var content = new FormUrlEncodedContent(form);
        return await client.PostAsync(url, content);
    }

    private static async Task<string> StaffNameAsync(IntakeWebApplicationFactory factory, Guid staffId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var names = await scope.ServiceProvider.GetRequiredService<IStaffAccountQueries>()
            .GetNamesAsync([staffId], CancellationToken.None);
        return names[staffId];
    }

    private static async Task<Guid> QdosOrganizationIdAsync(IntakeWebApplicationFactory factory)
    {
        _ = factory.Services;
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        return await db.Principals.Where(value => value.Code == "QDOS").Select(value => value.OrganizationId).SingleAsync();
    }

    private static async Task<int> StandingLinkCountAsync(IntakeWebApplicationFactory factory)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        return await db.StandingPhotoLinks.CountAsync();
    }

    private static async Task<StandingPhotoLink?> WebsiteLinkAsync(IntakeWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IStandingPhotoLinkStore>()
            .FindWebsiteAsync(CancellationToken.None);
    }

    /// <summary>Issues a quick link from the Case for <paramref name="quickLinkFor"/>, as Send's Type sets it (R808).</summary>
    private static async Task<IssuedPhotoLink> IssueQuickLinkAsync(
        IntakeWebApplicationFactory factory,
        Guid caseId,
        PhotoCaptureQuickLinkFor quickLinkFor)
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
                PhotoCaptureMode.Quick,
                quickLinkFor,
                LiveCameraOnly: false,
                OriginalsInBackground: false,
                CaptureLimits.DefaultLifetimeDays,
                PhotoLinkTokenPurpose.Copy,
                NewOperationKey()),
            CancellationToken.None);
    }

    /// <summary>The capture app's context on a token.</summary>
    private static async Task<JsonElement> ContextAsync(HttpClient client, CaptureLinkSecret token)
    {
        using var context = await SendAsync(client, HttpMethod.Get, $"{Api}/context", token);
        Assert.Equal(HttpStatusCode.OK, context.StatusCode);
        return await JsonAsync(context);
    }

    /// <summary>The sender presses Send; answers what the capture is still missing, by code and label.</summary>
    private static async Task<(string? Code, string? Label)[]> MissingOnCompleteAsync(HttpClient client, CaptureLinkSecret token)
    {
        using var completed = await SendAsync(client, HttpMethod.Post, $"{Api}/complete", token);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        return [.. (await JsonAsync(completed)).GetProperty("missing").EnumerateArray()
            .Select(entry => (entry.GetProperty("code").GetString(), entry.GetProperty("label").GetString()))];
    }

    /// <summary>The addresses a standing link's form offers, in order, from its context.</summary>
    private static async Task<(string? Name, string? Address)[]> StandingAddressesAsync(HttpClient client, StandingPhotoLink link)
    {
        using var context = await StandingCaptureDriver.SendAsync(
            client, HttpMethod.Get, $"{Api}/context", StandingCaptureDriver.Standing(link));
        Assert.Equal(HttpStatusCode.OK, context.StatusCode);
        return [.. (await JsonAsync(context)).GetProperty("standing").GetProperty("addresses").EnumerateArray()
            .Select(address => (address.GetProperty("name").GetString(), address.GetProperty("address").GetString()))];
    }

    private static async Task<StandingPhotoLinkAddressBook> AddressBookAsync(IntakeWebApplicationFactory factory, Guid standingLinkId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IStandingPhotoLinkStore>()
            .GetAddressBookAsync(standingLinkId, CancellationToken.None);
    }

    private static async Task<StandingPhotoLink> StandingLinkAsync(IntakeWebApplicationFactory factory, Guid standingLinkId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IStandingPhotoLinkStore>()
            .GetAsync(standingLinkId, CancellationToken.None)
            ?? throw new InvalidOperationException("The standing link was not found.");
    }

    /// <summary>The ids of the captures on the Case, in id order.</summary>
    private static async Task<Guid[]> CaptureIdsAsync(IntakeWebApplicationFactory factory, Guid caseId)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        var ids = await db.PhotoCaptures.AsNoTracking()
            .Where(value => value.CaseId == caseId)
            .Select(value => value.Id)
            .ToArrayAsync();
        return [.. ids.Order()];
    }

    /// <summary>
    /// Posts Send's form to copy a link with the mode and Type given; answers
    /// the capture it issued, or null when it issued none.
    /// </summary>
    private static async Task<CaptureRecordView?> SendFormAsync(
        IntakeWebApplicationFactory factory,
        HttpClient client,
        string send,
        Guid caseId,
        string mode,
        string quickLinkFor)
    {
        var before = await CaptureIdsAsync(factory, caseId);
        using (var posted = await PostStaffFormAsync(client, send, new Dictionary<string, string>
        {
            ["OperationKey"] = NewOperationKey(),
            ["Mode"] = mode,
            ["QuickLinkFor"] = quickLinkFor,
            ["Days"] = "7",
            ["LiveCameraOnly"] = "true",
            ["OriginalsInBackground"] = "false",
            ["Submit"] = Pegasus.Web.Pages.PhotoCapture.SendModel.ToCopy
        }))
        {
            Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        }

        var issued = (await CaptureIdsAsync(factory, caseId)).Except(before).ToArray();
        if (issued.Length == 0)
        {
            return null;
        }

        var capture = await StandingCaptureDriver.CaptureAsync(factory, Assert.Single(issued));
        return new(capture.Id, capture.QuickLinkFor);
    }

    private static async Task<string> PrincipalNameAsync(IntakeWebApplicationFactory factory, Guid caseId)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        return await db.Cases.Where(value => value.Id == caseId)
            .Select(value => value.Principal.Organization.Name)
            .SingleAsync();
    }

    /// <summary>An Outgoing mailbox that can send, as <c>StaffCorrespondenceWebTests</c> seeds one.</summary>
    private static async Task SeedSendableMailboxAsync(IntakeWebApplicationFactory factory)
    {
        _ = factory.Services;
        await using var context = await (await factory.DatabaseAsync).CreateContextAsync();
        var mailboxId = await TestMailboxId.EnsureApprovedAsync(
            context, "photo-link-sender", "photo-link-sender@collisionengineers.co.uk", IntakeWebApplicationFactory.FixedUtcNow.AddDays(-1));
        await context.SaveChangesAsync();
        var mailbox = await context.ApprovedMailboxes.SingleAsync(item => item.Id == mailboxId);
        mailbox.AllowOutgoing = true;
        mailbox.SentFolderIdentity = "sent-items";
        mailbox.MailboxGeneration = 1;
        await context.SaveChangesAsync();
    }

    /// <summary>A textarea's text as the page drew it.</summary>
    private static string TextareaValue(string html, string name)
    {
        var textarea = System.Text.RegularExpressions.Regex.Match(
            html,
            "<textarea[^>]*name=\"" + System.Text.RegularExpressions.Regex.Escape(name) + "\"[^>]*>(?<text>[\\s\\S]*?)</textarea>");
        Assert.True(textarea.Success, $"The page draws no {name} message.");
        return WebUtility.HtmlDecode(textarea.Groups["text"].Value);
    }

    private sealed record CaptureCounts(int OriginalCount, long AcceptedBytes);

    /// <summary>A capture Send issued: its id and who a quick link is for.</summary>
    private sealed record CaptureRecordView(Guid Id, PhotoCaptureQuickLinkFor? QuickLinkFor);

    /// <summary>A staff send that is composed, so Send draws its message, and that is never asked to send.</summary>
    private sealed class NeverSendingStaffMailSend : Pegasus.Core.Operations.IStaffMailSend
    {
        public Task<Pegasus.Core.Operations.StaffMailOperation> SendAsync(
            Pegasus.Core.Operations.StaffMailSendCommand command,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Opening Send and putting its wording back send nothing.");

        public Task<Pegasus.Core.Operations.StaffMailOperation?> GetAsync(
            ActionActor actor,
            Guid operationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Pegasus.Core.Operations.StaffMailOperation?>(null);

        public Task<Pegasus.Core.Operations.StaffMailOperation?> GetLatestForOriginalAsync(
            ActionActor actor,
            Guid retainedMessageId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Pegasus.Core.Operations.StaffMailOperation?>(null);

        public Task<Pegasus.Core.Operations.StaffMailOperation> CancelAsync(
            ActionActor actor,
            Guid operationId,
            long expectedVersion,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Opening Send and putting its wording back cancel nothing.");
    }

    private sealed record ImageVersions(int Versions, int Current);

    private sealed record CaseFile(DocumentSemanticRole Role, string FileName);

    private sealed record CaseImage(Guid OccurrenceId, string FileName, bool InReport);
}
