using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.PhotoCapture;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.Web.PhotoCapture;
using Pegasus.Web.Presentation;
using CaptureRecord = Pegasus.Core.PhotoCapture.PhotoCapture;

namespace Pegasus.IntegrationTests;

/// <summary>
/// A contact's standing link through the composed Web host, up to the settle
/// (FRD-28 "Standing photo links" and "Routing and filing"): the phone starts
/// or hands on a capture with <c>X-Capture-Link: s.{slug}</c> and sends its
/// photos on the capture's token, and the in-process drain stands in for the
/// Worker. Nothing here sends a capture or settles one.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class StandingCaptureWebTests
{
    private const string Api = StandingCaptureDriver.Api;
    private const string UnheldRegistration = "ZZ99 QQQ";

    private const string FirstOverview =
        "kind=overview&position=1&takenAt=2031-05-06T10%3A20%3A00.000Z&live=true&original=false";

    private const string SecondOverview =
        "kind=overview&position=2&takenAt=2031-05-06T10%3A20%3A00.000Z&live=true&original=false";

    private const string FourthOverview =
        "kind=overview&position=4&takenAt=2031-05-06T10%3A20%3A00.000Z&live=true&original=false";

    private const string FirstOverviewOriginal =
        "kind=overview&position=1&takenAt=2031-05-06T10%3A20%3A00.000Z&live=true&original=true";

    private const string Paperwork = "kind=paperwork&position=1&live=false&original=false";

    /// <summary>
    /// A contact's link starts a capture for a registration no Case holds:
    /// the answer's token works on the capture, which is Opened as it is
    /// made (it is the phone taking the photos) and routed to a Vehicle
    /// images record.
    /// </summary>
    [Fact]
    public async Task AContactsLinkStartsAnOpenedCaptureBoundForAVehicleImagesRecord()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);

        using var started = await StandingCaptureDriver.SendAsync(
            client, HttpMethod.Post, $"{Api}/start", StandingCaptureDriver.Standing(link), StandingCaptureDriver.Json("{\"registration\":\"ZZ99 QQQ\"}"));

        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        var answer = await StandingCaptureDriver.JsonAsync(started);
        var token = answer.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("The start answered no token.");
        var capture = await StandingCaptureDriver.CaptureByTokenAsync(factory, token);
        Assert.Equal(CaptureContextFactory.LinkKey(capture.Id), answer.GetProperty("linkKey").GetString());
        Assert.Equal(link.Id, capture.StandingLinkId);
        Assert.Equal(PhotoCaptureOrigin.StandingStaff, capture.Origin);
        Assert.Equal("ZZ99QQQ", capture.NormalizedRegistration);
        Assert.True(capture.OpenedAtUtc.HasValue, "A capture started to take the photos now is Opened as it is made.");
        Assert.Equal(PhotoCaptureDestination.VehicleImages, capture.Destination);
        Assert.False(capture.CaseId.HasValue, "A capture whose registration no Case holds has no Case.");
        using var context = await StandingCaptureDriver.SendAsync(
            client, HttpMethod.Get, $"{Api}/context", StandingCaptureDriver.Capture(token));
        Assert.Equal(HttpStatusCode.OK, context.StatusCode);
        Assert.Equal(
            CaptureContextFactory.LinkKey(capture.Id),
            (await StandingCaptureDriver.JsonAsync(context)).GetProperty("linkKey").GetString());
    }

    /// <summary>
    /// A photo on a capture with no Case waits for its capture to be sent:
    /// its receipt needs sorting with the waiting reason (R805), is linked to
    /// no Case, is in no submission group yet, and is never Unidentified.
    /// </summary>
    [Fact]
    public async Task APhotoOnACaptureWithNoCaseWaitsAndIsNeverUnidentified()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var started = await StandingCaptureDriver.StartAsync(factory, client, link, UnheldRegistration);

        var item = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, FirstOverview, MultiFormatFixture.Photograph(1), "image/jpeg");
        var receiptId = await StandingCaptureDriver.DrainAsync(factory, item);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receipt = await services.GetRequiredService<IIntakeReceiptQueries>().GetAsync(receiptId, CancellationToken.None)
            ?? throw new InvalidOperationException("The photo's receipt was not recorded.");
        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Equal(PhotoCaptureIntakeRules.WaitingReason, receipt.DecisionReason);
        Assert.False(receipt.CurrentCaseId.HasValue, "A waiting photo is linked to no Case.");
        Assert.Null(await services.GetRequiredService<IIntakeSubmissionGroupStore>()
            .FindForMemberSourceAsync(receipt.SourceIdentity, CancellationToken.None));
        Assert.Null(await services.GetRequiredService<IUnidentifiedStore>()
            .GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId), CancellationToken.None));
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        Assert.False(await db.IntakeManualAssociations.AnyAsync(value => value.IntakeReceiptId == receiptId));
    }

    /// <summary>
    /// Paperwork on a capture with no Case is taken, not refused: its staged
    /// receipt declares no Case and so no document type (FRD-28).
    /// </summary>
    [Fact]
    public async Task PaperworkOnACaptureWithNoCaseIsTakenAndDeclaresNothing()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var started = await StandingCaptureDriver.StartAsync(factory, client, link, UnheldRegistration);

        var item = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, Paperwork, IntakeTestEvidence.CreatePdf("Repair estimate"), "application/pdf");

        var stagedReceiptId = item.StagedReceiptId
            ?? throw new InvalidOperationException("The paperwork was taken without a staged intake receipt.");
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        var staged = await db.IntakeStagedReceipts.AsNoTracking().SingleAsync(value => value.Id == stagedReceiptId);
        Assert.False(staged.DeclaredCaseId.HasValue, "Paperwork on a capture with no Case declared a Case.");
        Assert.Null(staged.DeclaredDocumentType);
    }

    /// <summary>
    /// Until its first photo a capture is routed again on each details call
    /// (FRD-28 "Registration"): a changed registration a Case holds sends it
    /// to that Case. Once a photo is in, a different registration is refused
    /// and nothing is written, and the same details sent again are taken.
    /// </summary>
    [Fact]
    public async Task ARegistrationRoutesUntilTheFirstPhotoAndIsThenLocked()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var started = await StandingCaptureDriver.StartAsync(factory, client, link, UnheldRegistration);
        Assert.Equal(PhotoCaptureDestination.VehicleImages, started.Capture.Destination);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        await StandingCaptureDriver.SetCaseRegistrationAsync(factory, caseId, "XY99ZZZ");

        using (var routed = await StandingCaptureDriver.SendAsync(
            client, HttpMethod.Post, $"{Api}/details", StandingCaptureDriver.Capture(started.Token), StandingCaptureDriver.Json("{\"registration\":\"XY99 ZZZ\"}")))
        {
            Assert.Equal(HttpStatusCode.OK, routed.StatusCode);
        }

        var onCase = await StandingCaptureDriver.CaptureAsync(factory, started.Capture.Id);
        Assert.Equal(PhotoCaptureDestination.Case, onCase.Destination);
        Assert.Equal(caseId, onCase.CaseId);
        Assert.Equal("XY99ZZZ", onCase.NormalizedRegistration);
        await StandingCaptureDriver.SendItemAsync(
            factory, client, started, FirstOverview, MultiFormatFixture.Photograph(1), "image/jpeg");
        var beforeRefusal = await StandingCaptureDriver.CaptureAsync(factory, started.Capture.Id);

        using (var refused = await StandingCaptureDriver.SendAsync(
            client, HttpMethod.Post, $"{Api}/details", StandingCaptureDriver.Capture(started.Token), StandingCaptureDriver.Json("{\"registration\":\"AB12 CDE\"}")))
        {
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Equal(
                PhotoCaptureRefusals.RegistrationLocked,
                (await StandingCaptureDriver.JsonAsync(refused)).GetProperty("code").GetString());
        }

        var afterRefusal = await StandingCaptureDriver.CaptureAsync(factory, started.Capture.Id);
        Assert.Equal(beforeRefusal.Version, afterRefusal.Version);
        Assert.Equal("XY99ZZZ", afterRefusal.NormalizedRegistration);
        Assert.Equal(caseId, afterRefusal.CaseId);
        using (var again = await StandingCaptureDriver.SendAsync(
            client, HttpMethod.Post, $"{Api}/details", StandingCaptureDriver.Capture(started.Token), StandingCaptureDriver.Json("{\"registration\":\"XY99 ZZZ\"}")))
        {
            Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        }
    }

    /// <summary>
    /// A capture with no Case takes no originals (R801): its context says so,
    /// and an original sent all the same is refused as a type it does not take.
    /// </summary>
    [Fact]
    public async Task ACaptureWithNoCaseTakesNoOriginals()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var started = await StandingCaptureDriver.StartAsync(factory, client, link, UnheldRegistration);

        using (var context = await StandingCaptureDriver.SendAsync(
            client, HttpMethod.Get, $"{Api}/context", StandingCaptureDriver.Capture(started.Token)))
        {
            Assert.Equal(HttpStatusCode.OK, context.StatusCode);
            Assert.False((await StandingCaptureDriver.JsonAsync(context)).GetProperty("originals").GetBoolean());
        }

        using var original = await StandingCaptureDriver.SendAsync(
            client,
            HttpMethod.Put,
            $"{Api}/items/{Guid.NewGuid():D}?{FirstOverviewOriginal}",
            StandingCaptureDriver.Capture(started.Token),
            StandingCaptureDriver.Content(MultiFormatFixture.Photograph(1), "image/jpeg"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, original.StatusCode);
        Assert.Equal(
            PhotoCaptureRefusals.UnsupportedType,
            (await StandingCaptureDriver.JsonAsync(original)).GetProperty("code").GetString());
    }

    /// <summary>
    /// With one eligible Case under the link's Principal holding the
    /// registration, the capture is routed to that Case as it starts, and a
    /// photo files on it.
    /// </summary>
    [Fact]
    public async Task ACaptureForARegistrationOneCaseHoldsFilesOnThatCase()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        await StandingCaptureDriver.SetCaseRegistrationAsync(factory, caseId, "XY99ZZZ");
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);

        var started = await StandingCaptureDriver.StartAsync(factory, client, link, "XY99 ZZZ");

        Assert.Equal(PhotoCaptureDestination.Case, started.Capture.Destination);
        Assert.Equal(caseId, started.Capture.CaseId);
        var item = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, FirstOverview, MultiFormatFixture.Photograph(1), "image/jpeg");
        await StandingCaptureDriver.DrainAsync(factory, item);
        Assert.Contains(item.FileName, await StandingCaptureDriver.CaseImageNamesAsync(factory, caseId));
    }

    /// <summary>
    /// A hand-off mints the customer's link and a Session token for the staff
    /// phone: paperwork the phone sends under the Session token never opens
    /// the customer's link, which Coming in reads "Sent, not opened", and the
    /// customer's first call on their own token opens it.
    /// </summary>
    [Fact]
    public async Task AHandOffsSessionTokenNeverOpensTheCustomersLink()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);

        using var handedOn = await StandingCaptureDriver.SendAsync(
            client, HttpMethod.Post, $"{Api}/handoff", StandingCaptureDriver.Standing(link), StandingCaptureDriver.Json("{\"registration\":\"ZZ88 HHH\"}"));

        Assert.Equal(HttpStatusCode.Created, handedOn.StatusCode);
        var answer = await StandingCaptureDriver.JsonAsync(handedOn);
        var url = answer.GetProperty("url").GetString()
            ?? throw new InvalidOperationException("The hand-off answered no link.");
        var sessionToken = answer.GetProperty("sessionToken").GetString()
            ?? throw new InvalidOperationException("The hand-off answered no Session token.");
        var customerToken = url[(url.LastIndexOf('/') + 1)..];
        Assert.NotEqual(customerToken, sessionToken);
        var capture = await StandingCaptureDriver.CaptureByTokenAsync(factory, customerToken);
        Assert.Equal(PhotoCaptureOrigin.StandingForwarded, capture.Origin);
        var session = new StartedCapture(sessionToken, capture);
        await StandingCaptureDriver.SendItemAsync(
            factory, client, session, Paperwork, IntakeTestEvidence.CreatePdf("Repair estimate"), "application/pdf");

        Assert.False(
            (await StandingCaptureDriver.CaptureAsync(factory, capture.Id)).OpenedAtUtc.HasValue,
            "Paperwork sent under the Session token opened the customer's link.");
        var row = StandingCaptureDriver.ListRow(await IntakeWebDriver.GetHtmlAsync(client, "/PhotoCapture"), capture.Id);
        Assert.Contains(
            $">{OperatorLabels.PhotoCapture.State(PhotoCaptureState.SentNotOpened)}</span>",
            row,
            StringComparison.Ordinal);
        using (var opened = await StandingCaptureDriver.SendAsync(
            client, HttpMethod.Get, $"{Api}/context", StandingCaptureDriver.Capture(customerToken)))
        {
            Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        }

        Assert.True(
            (await StandingCaptureDriver.CaptureAsync(factory, capture.Id)).OpenedAtUtc.HasValue,
            "The customer's first call did not open the link.");
    }

    /// <summary>
    /// The store gives an item its batch under the capture's lock (F-R6a):
    /// once batch 1 is settled a new item is batch 2's first; a batch-1 item
    /// that never reached intake joins batch 2 when it is sent again; one
    /// whose receipt did stage keeps its batch, so its retry finds that
    /// receipt. The settle is stood in for by marking batch 1 settled.
    /// </summary>
    [Fact]
    public async Task AnItemJoinsTheOpenBatchUnlessItsReceiptAlreadyStaged()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var started = await StandingCaptureDriver.StartAsync(factory, client, link, UnheldRegistration);
        var first = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, FirstOverview, MultiFormatFixture.Photograph(1), "image/jpeg");
        var stagedPhoto = MultiFormatFixture.Photograph(2);
        var staged = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, SecondOverview, stagedPhoto, "image/jpeg");
        Assert.Equal((1, 0), (first.Batch, first.Ordinal));
        Assert.Equal((1, 1), (staged.Batch, staged.Ordinal));
        var unstagedPhoto = MultiFormatFixture.Photograph(3);
        var unstagedClientId = Guid.NewGuid();
        await using (var db = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            // A batch-1 row whose upload never reached intake, as a request
            // that failed after writing it leaves one.
            db.PhotoCaptureItems.Add(new PhotoCaptureItemEntity
            {
                Id = Guid.NewGuid(),
                CaptureId = started.Capture.Id,
                ClientItemId = unstagedClientId,
                Kind = PhotoCaptureCodes.ToCode(PhotoCaptureItemKind.Overview),
                Position = 3,
                Batch = 1,
                Ordinal = 2,
                StoryGroup = PhotoCaptureStory.OverviewGroup,
                StorySequence = 3,
                FileName = PhotoCaptureStory.FileName(
                    PhotoCaptureItemKind.Overview, null, null, 3, PhotoCaptureStory.OverviewGroup, 3, "image/jpeg"),
                MediaType = "image/jpeg",
                InReportOnArrival = true,
                ContentHash = Convert.ToHexStringLower(SHA256.HashData(unstagedPhoto)),
                ContentLength = unstagedPhoto.Length,
                ReceivedAtUtc = IntakeWebApplicationFactory.FixedUtcNow
            });
            await db.SaveChangesAsync();
            // The staged item's row as one whose receipt staged but was not yet recorded on it.
            await db.PhotoCaptureItems.Where(value => value.Id == staged.Id).ExecuteUpdateAsync(update => update
                .SetProperty(value => value.StagedReceiptId, (Guid?)null));
            await db.PhotoCaptures.Where(value => value.Id == started.Capture.Id).ExecuteUpdateAsync(update => update
                .SetProperty(value => value.SettledBatches, 1));
        }

        var next = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, FourthOverview, MultiFormatFixture.Photograph(4), "image/jpeg");
        Assert.Equal((2, 0), (next.Batch, next.Ordinal));

        var moved = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, "kind=overview&position=3&live=true&original=false", unstagedPhoto, "image/jpeg", unstagedClientId);
        Assert.Equal((2, 1), (moved.Batch, moved.Ordinal));
        Assert.True(moved.StagedReceiptId.HasValue, "The moved item did not reach intake.");

        var kept = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, SecondOverview, stagedPhoto, "image/jpeg", staged.ClientItemId);
        Assert.Equal((1, 1), (kept.Batch, kept.Ordinal));
    }
}

/// <summary>A capture a standing link started or handed on: a token it is reached by, and the capture as it then stood.</summary>
internal sealed record StartedCapture(string Token, CaptureRecord Capture);

/// <summary>
/// The phone's calls on a standing link and on the captures it starts, and
/// the reads the standing-capture evidence makes; shared by
/// <see cref="StandingCaptureWebTests"/> and <see cref="PhotoCaptureWebTests"/>.
/// </summary>
internal static class StandingCaptureDriver
{
    internal const string Api = PhotoCaptureSurface.ApiBasePath;

    private static readonly ActionActor Administrator =
        ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);

    /// <summary>The QDOS Principal's contact's standing link, made as an Administrator makes it.</summary>
    internal static async Task<StandingPhotoLink> CreateContactLinkAsync(IntakeWebApplicationFactory factory)
    {
        _ = factory.Services;
        Guid organizationId;
        await using (var db = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            organizationId = await db.Principals.Where(value => value.Code == "QDOS")
                .Select(value => value.OrganizationId)
                .SingleAsync();
        }

        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<StandingPhotoLinkAdministration>()
            .CreateForContactAsync(Administrator, organizationId, CancellationToken.None);
    }

    internal static string Standing(StandingPhotoLink link) => PhotoCaptureAccess.StandingPrefix + link.Slug;

    internal static string Capture(string token) => PhotoCaptureAccess.CapturePrefix + token;

    /// <summary>One capture API call with the credential the phone carries in <c>X-Capture-Link</c>.</summary>
    internal static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string credential,
        HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Add(PhotoCaptureSurface.CredentialHeader, credential);
        return await client.SendAsync(request);
    }

    internal static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    internal static ByteArrayContent Content(byte[] bytes, string mediaType)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        return content;
    }

    internal static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    /// <summary>Starts a capture on the link for the registration, as "Take photos now" does; answers its token and the capture.</summary>
    internal static async Task<StartedCapture> StartAsync(
        IntakeWebApplicationFactory factory,
        HttpClient client,
        StandingPhotoLink link,
        string registration)
    {
        using var started = await SendAsync(
            client,
            HttpMethod.Post,
            $"{Api}/start",
            Standing(link),
            Json(JsonSerializer.Serialize(new { registration })));
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        var token = (await JsonAsync(started)).GetProperty("token").GetString()
            ?? throw new InvalidOperationException("The start answered no token.");
        return new(token, await CaptureByTokenAsync(factory, token));
    }

    /// <summary>Sends one file on the capture's token in a single request; answers the item as the store then holds it.</summary>
    internal static async Task<PhotoCaptureItem> SendItemAsync(
        IntakeWebApplicationFactory factory,
        HttpClient client,
        StartedCapture capture,
        string query,
        byte[] bytes,
        string mediaType,
        Guid? clientItemId = null)
    {
        var itemId = clientItemId ?? Guid.NewGuid();
        using (var sent = await SendAsync(
            client, HttpMethod.Put, $"{Api}/items/{itemId:D}?{query}", Capture(capture.Token), Content(bytes, mediaType)))
        {
            Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPhotoCaptureStore>()
            .FindItemAsync(capture.Capture.Id, itemId, CancellationToken.None)
            ?? throw new InvalidOperationException("The item was not recorded.");
    }

    /// <summary>Processes the item's staged receipt as the Worker would; answers the processed receipt.</summary>
    internal static async Task<Guid> DrainAsync(IntakeWebApplicationFactory factory, PhotoCaptureItem item)
    {
        var stagedReceiptId = item.StagedReceiptId
            ?? throw new InvalidOperationException("The item was taken without a staged intake receipt.");
        await using var scope = factory.Services.CreateAsyncScope();
        return (await IntakeWebDriver.DrainStagedAsync(scope.ServiceProvider, stagedReceiptId)).ProcessedReceiptId;
    }

    internal static async Task<CaptureRecord> CaptureByTokenAsync(IntakeWebApplicationFactory factory, string token)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var found = await scope.ServiceProvider.GetRequiredService<IPhotoCaptureStore>()
            .FindByTokenDigestAsync(PhotoLinkToken.Digest(token), CancellationToken.None);
        return found?.Capture ?? throw new InvalidOperationException("No capture holds the token.");
    }

    internal static async Task<CaptureRecord> CaptureAsync(IntakeWebApplicationFactory factory, Guid captureId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPhotoCaptureStore>()
            .GetAsync(captureId, CancellationToken.None)
            ?? throw new InvalidOperationException("The capture was not found.");
    }

    /// <summary>The registration the Case's match index holds, the one routing reads.</summary>
    internal static async Task SetCaseRegistrationAsync(IntakeWebApplicationFactory factory, Guid caseId, string normalizedRegistration)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        await db.CaseMatchIndex.Where(value => value.CaseId == caseId).ExecuteUpdateAsync(update => update
            .SetProperty(value => value.NormalizedVrm, normalizedRegistration));
    }

    /// <summary>The names the Case's images were filed under.</summary>
    internal static async Task<string[]> CaseImageNamesAsync(IntakeWebApplicationFactory factory, Guid caseId)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        return await (from occurrence in db.Set<DocumentOccurrenceEntity>().AsNoTracking()
                      join version in db.Set<DocumentVersionEntity>().AsNoTracking() on occurrence.VersionId equals version.Id
                      where occurrence.CaseId == caseId && occurrence.SemanticRole == DocumentSemanticRole.Image
                      select version.FileName).ToArrayAsync();
    }

    /// <summary>One capture's row of a Photo capture list, from its row marker to the row's end.</summary>
    internal static string ListRow(string html, Guid captureId)
    {
        var start = html.IndexOf($"data-photo-capture-row=\"{captureId:D}\"", StringComparison.OrdinalIgnoreCase);
        Assert.True(start >= 0, $"The list shows no row for capture {captureId:D}.");
        var end = html.IndexOf("</tr>", start, StringComparison.Ordinal);
        Assert.True(end > start, $"The row for capture {captureId:D} does not end.");
        return html[start..end];
    }
}
