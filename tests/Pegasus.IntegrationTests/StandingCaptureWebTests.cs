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
/// Worker. The settle tests run the Worker's settle in process as well.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class StandingCaptureWebTests
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

    /// <summary>The accident date the settle tests type on the start form.</summary>
    private static readonly DateOnly TypedAccidentDate = new(2031, 4, 30);

    private static readonly ActionActor Administrator =
        ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);

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
    /// receipt declares no Case, and declares its kind's type, Other, so it
    /// files as Other when the capture reaches a Case (FRD-28; R803).
    /// </summary>
    [Fact]
    public async Task PaperworkOnACaptureWithNoCaseIsTakenAndDeclaresOnlyItsType()
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
        Assert.Equal(nameof(DocumentSemanticRole.Other), staged.DeclaredDocumentType);
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

    /// <summary>
    /// A capture with no Case settles as one submission group of its
    /// photographs on the Photo capture channel, carrying the typed
    /// registration and date (R791); the paperwork waits outside it (R803).
    /// The group registers a Vehicle images record awaiting its instruction,
    /// nothing is Unidentified, and the record's page shows the Photo link.
    /// </summary>
    [Fact]
    public async Task ACaptureWithNoCaseSettlesAsOneGroupThatRegistersItsRecord()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var started = await StandingCaptureDriver.StartWithFormAsync(
            factory, client, link, new { registration = UnheldRegistration, incidentDate = "2031-04-30" });
        var (photographs, _, paperwork) = await SendPhotographsAndPaperworkAsync(factory, client, started);
        await StandingCaptureDriver.CompleteAsync(client, started.Token, "Please call before 5pm.");

        await SettleAndReconcileAsync(factory);

        var capture = await StandingCaptureDriver.CaptureAsync(factory, started.Capture.Id);
        Assert.Equal(1, capture.SettledBatches);
        Assert.False(capture.CaseId.HasValue, "A capture whose registration no Case holds reached a Case.");
        var groupId = capture.SubmissionGroupId
            ?? throw new InvalidOperationException("The settle formed no submission group.");
        Assert.Equal(PhotoCaptureIntakeRules.BatchGroupId(capture.Id, 1), groupId);
        var group = await GroupAsync(factory, groupId);
        Assert.Equal(IntakeSourceChannel.PhotoCapture, group.Channel);
        Assert.Equal("ZZ99QQQ", group.DeclaredRegistration);
        Assert.Equal(TypedAccidentDate, group.DeclaredIncidentDate);
        Assert.Equal(photographs.Length, group.ExpectedMemberCount);
        Assert.Equal(
            photographs.Select(item => item.StagedReceiptId!.Value).Order(),
            group.Members.Select(member => member.StagedReceiptId).Order());
        Assert.DoesNotContain(group.Members, member => member.StagedReceiptId == paperwork.StagedReceiptId);

        var record = await StandingCaptureDriver.RecordForGroupAsync(factory, groupId);
        Assert.Equal("awaiting_instruction", record.LifecycleState);
        Assert.False(await AnythingUnidentifiedAsync(factory), "A photo link's capture was registered Unidentified.");
        await AssertRecordShowsThePhotoLinkAsync(client, record.Id, capture.Id);

        // The close recorded the batch with its group, no Case and no bell.
        var closed = Assert.Single(await StandingCaptureDriver.BatchesAsync(factory, capture.Id));
        Assert.Equal(1, closed.Batch);
        Assert.Equal(groupId, closed.SubmissionGroupId);
        Assert.False(closed.CaseId.HasValue, "A Vehicle images batch recorded a Case at its close.");
        Assert.False(closed.BellOwedAtUtc.HasValue, "A Vehicle images batch owed a bell.");
    }

    /// <summary>
    /// A Case that appears after the start, under the link's Principal with
    /// the registration and no contradicting date, takes the batch at the
    /// settle (R791): the photos file straight on it, with no group and no
    /// Vehicle images record, and the capture records the Case.
    /// </summary>
    [Fact]
    public async Task ACaseThatAppearsBeforeTheSettleTakesThePhotosStraight()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var started = await StandingCaptureDriver.StartAsync(factory, client, link, "XY99 ZZZ");
        Assert.Equal(PhotoCaptureDestination.VehicleImages, started.Capture.Destination);
        var photo = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, FirstOverview, MultiFormatFixture.Photograph(1), "image/jpeg");
        await StandingCaptureDriver.DrainAsync(factory, photo);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        await StandingCaptureDriver.SetCaseRegistrationAsync(factory, caseId, "XY99ZZZ");
        await StandingCaptureDriver.AssignEngineerAsync(factory, caseId);
        await StandingCaptureDriver.CompleteAsync(client, started.Token);

        // A second tick is what the Worker's timer would run should the first find the filing still in flight.
        await StandingCaptureDriver.SettleAsync(factory);
        await StandingCaptureDriver.SettleAsync(factory);

        var capture = await StandingCaptureDriver.CaptureAsync(factory, started.Capture.Id);
        Assert.Equal(caseId, capture.CaseId);
        Assert.Equal(PhotoCaptureDestination.Case, capture.Destination);
        Assert.False(capture.SubmissionGroupId.HasValue, "A capture that found its Case at the settle formed a group.");
        Assert.Equal(1, capture.SettledBatches);
        Assert.Contains(photo.FileName, await StandingCaptureDriver.CaseImageNamesAsync(factory, caseId));
        Assert.Null(await FindGroupAsync(factory, PhotoCaptureIntakeRules.BatchGroupId(capture.Id, 1)));
        await using (var db = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            Assert.False(await db.ImageIntakes.AnyAsync(), "A capture that found its Case at the settle registered a record.");
        }

        Assert.True(await StandingCaptureDriver.ImagesCompleteAsync(factory, caseId));
        Assert.Equal(1, await StandingCaptureDriver.PhotosReceivedCountAsync(factory, caseId));
    }

    /// <summary>Two eligible Cases are no match (R791): the settle makes the record and files on neither.</summary>
    [Fact]
    public async Task TwoEligibleCasesMakeTheRecordAndNeitherTakesThePhotos()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var started = await StandingCaptureDriver.StartAsync(factory, client, link, "XY99 ZZZ");
        var photo = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, FirstOverview, MultiFormatFixture.Photograph(1), "image/jpeg");
        await StandingCaptureDriver.DrainAsync(factory, photo);
        var first = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var second = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory, sequence: 10);
        await StandingCaptureDriver.SetCaseRegistrationAsync(factory, first, "XY99ZZZ");
        await StandingCaptureDriver.SetCaseRegistrationAsync(factory, second, "XY99ZZZ");
        await StandingCaptureDriver.CompleteAsync(client, started.Token);

        await SettleAndReconcileAsync(factory);

        var capture = await StandingCaptureDriver.CaptureAsync(factory, started.Capture.Id);
        Assert.False(capture.CaseId.HasValue, "A capture with two eligible Cases was filed on one.");
        var groupId = capture.SubmissionGroupId
            ?? throw new InvalidOperationException("The settle formed no submission group.");
        Assert.Equal("awaiting_instruction", (await StandingCaptureDriver.RecordForGroupAsync(factory, groupId)).LifecycleState);
        Assert.Empty(await StandingCaptureDriver.CaseImageNamesAsync(factory, first));
        Assert.Empty(await StandingCaptureDriver.CaseImageNamesAsync(factory, second));
    }

    /// <summary>
    /// A Case whose incident date contradicts the typed one is no match at the
    /// settle (R791): the batch makes the record, which carries the typed date
    /// and does not pair with that Case.
    /// </summary>
    [Fact]
    public async Task ACaseWhoseDateContradictsTheTypedDateIsNoMatch()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var started = await StandingCaptureDriver.StartWithFormAsync(
            factory, client, link, new { registration = "XY99 ZZZ", incidentDate = "2031-04-30" });
        var photo = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, FirstOverview, MultiFormatFixture.Photograph(1), "image/jpeg");
        await StandingCaptureDriver.DrainAsync(factory, photo);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        await StandingCaptureDriver.SetCaseRegistrationAsync(factory, caseId, "XY99ZZZ");
        await using (var db = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            await db.CaseMatchIndex.Where(value => value.CaseId == caseId).ExecuteUpdateAsync(update => update
                .SetProperty(value => value.IncidentDate, (DateOnly?)new DateOnly(2031, 3, 1)));
        }

        await StandingCaptureDriver.CompleteAsync(client, started.Token);
        await SettleAndReconcileAsync(factory);

        var capture = await StandingCaptureDriver.CaptureAsync(factory, started.Capture.Id);
        Assert.False(capture.CaseId.HasValue, "A capture was filed on a Case whose incident date contradicts the typed one.");
        var groupId = capture.SubmissionGroupId
            ?? throw new InvalidOperationException("The settle formed no submission group.");
        Assert.Equal(TypedAccidentDate, (await GroupAsync(factory, groupId)).DeclaredIncidentDate);
        var record = await StandingCaptureDriver.RecordForGroupAsync(factory, groupId);
        Assert.Equal("awaiting_instruction", record.LifecycleState);
        Assert.False(record.MergedIntoCaseId.HasValue, "The record paired with a Case whose date contradicts the typed one.");
        Assert.Empty(await StandingCaptureDriver.CaseImageNamesAsync(factory, caseId));
    }

    /// <summary>
    /// A settled capture whose record later reaches a Case finishes there
    /// (R803, R804): the note is written, the kept paperwork files as Other,
    /// the capture records the Case and the bell rings once. The record is
    /// merged by staff as the merge tests do, and its fold run as the Worker
    /// runs it.
    /// </summary>
    [Fact]
    public async Task ARecordThatReachesACaseFinishesItsCaptureThere()
    {
        const string note = "Please call before 5pm.";
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var started = await StandingCaptureDriver.StartAsync(factory, client, link, UnheldRegistration);
        var (_, photographReceipts, paperwork) = await SendPhotographsAndPaperworkAsync(factory, client, started);
        await StandingCaptureDriver.CompleteAsync(client, started.Token, note);
        await SettleAndReconcileAsync(factory);
        var settled = await StandingCaptureDriver.CaptureAsync(factory, started.Capture.Id);
        var record = await StandingCaptureDriver.RecordForGroupAsync(
            factory, settled.SubmissionGroupId ?? throw new InvalidOperationException("The settle formed no submission group."));
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        await CreateCaseCustodyRootAsync(factory, caseId);
        await StandingCaptureDriver.AssignEngineerAsync(factory, caseId);
        await ImageIntakeTestData.ProcessImageCaseCustodyAsync(factory);
        await MergeRecordIntoCaseAsync(factory, record.Id, photographReceipts, caseId);
        await ImageIntakeTestData.ProcessCustodyAsync(factory, Pegasus.Core.Custody.ExternalWorkKinds.MergeImageCaseCustody);

        await StandingCaptureDriver.SettleAsync(factory);

        var reached = await StandingCaptureDriver.CaptureAsync(factory, started.Capture.Id);
        Assert.Equal(caseId, reached.CaseId);
        Assert.Equal(
            "Customer's note (photo link): " + note,
            Assert.Single(await StandingCaptureDriver.CaseEventReasonsAsync(factory, caseId, Pegasus.Core.Cases.AddCaseNote.EventType)));
        await using (var db = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            var kept = await (from occurrence in db.Set<DocumentOccurrenceEntity>().AsNoTracking()
                              join version in db.Set<DocumentVersionEntity>().AsNoTracking() on occurrence.VersionId equals version.Id
                              where occurrence.CaseId == caseId && version.FileName == paperwork.FileName
                              select occurrence.SemanticRole).ToListAsync();
            Assert.Equal(DocumentSemanticRole.Other, Assert.Single(kept));
        }

        Assert.NotEmpty(await StandingCaptureDriver.CaseImageNamesAsync(factory, caseId));
        Assert.Equal(1, await StandingCaptureDriver.PhotosReceivedCountAsync(factory, caseId));

        await StandingCaptureDriver.SettleAsync(factory);

        Assert.Equal(1, await StandingCaptureDriver.PhotosReceivedCountAsync(factory, caseId));
        Assert.Single(await StandingCaptureDriver.CaseEventReasonsAsync(factory, caseId, Pegasus.Core.Cases.AddCaseNote.EventType));
    }

    /// <summary>
    /// A further photo after the first record forms the next batch: its own
    /// group and record for the same registration, and both records' pages
    /// show the capture's Photo link.
    /// </summary>
    [Fact]
    public async Task ASecondBatchMakesASecondRecordAndBothShowTheCapture()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var started = await StandingCaptureDriver.StartAsync(factory, client, link, UnheldRegistration);
        var first = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, FirstOverview, MultiFormatFixture.Photograph(1), "image/jpeg");
        await StandingCaptureDriver.DrainAsync(factory, first);
        await StandingCaptureDriver.CompleteAsync(client, started.Token);
        await SettleAndReconcileAsync(factory);
        await StandingCaptureDriver.MoveLastSettleBackAsync(factory, started.Capture.Id);

        var further = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, SecondOverview, MultiFormatFixture.Photograph(2), "image/jpeg");
        Assert.Equal(2, further.Batch);
        await StandingCaptureDriver.DrainAsync(factory, further);
        await StandingCaptureDriver.CompleteAsync(client, started.Token);
        await SettleAndReconcileAsync(factory);

        var capture = await StandingCaptureDriver.CaptureAsync(factory, started.Capture.Id);
        Assert.Equal(2, capture.SettledBatches);
        var firstGroupId = PhotoCaptureIntakeRules.BatchGroupId(capture.Id, 1);
        var secondGroupId = PhotoCaptureIntakeRules.BatchGroupId(capture.Id, 2);
        Assert.Equal(secondGroupId, capture.SubmissionGroupId);
        Assert.Equal("ZZ99QQQ", (await GroupAsync(factory, secondGroupId)).DeclaredRegistration);
        var firstRecord = await StandingCaptureDriver.RecordForGroupAsync(factory, firstGroupId);
        var secondRecord = await StandingCaptureDriver.RecordForGroupAsync(factory, secondGroupId);
        Assert.NotEqual(firstRecord.Id, secondRecord.Id);
        await AssertRecordShowsThePhotoLinkAsync(client, firstRecord.Id, capture.Id);
        await AssertRecordShowsThePhotoLinkAsync(client, secondRecord.Id, capture.Id);
    }

    /// <summary>
    /// A photo link's group is never Unidentified (FRD-28): a settled
    /// capture's group past its window, and a waiting capture's receipt past
    /// it, are left as they are by the grouped-image sweep.
    /// </summary>
    [Fact]
    public async Task ACaptureIsNeverUnidentifiedOnceItsWindowHasPassed()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var settled = await StandingCaptureDriver.StartAsync(factory, client, link, UnheldRegistration);
        var settledPhoto = await StandingCaptureDriver.SendItemAsync(
            factory, client, settled, FirstOverview, MultiFormatFixture.Photograph(1), "image/jpeg");
        await StandingCaptureDriver.DrainAsync(factory, settledPhoto);
        await StandingCaptureDriver.CompleteAsync(client, settled.Token);
        await SettleAndReconcileAsync(factory);
        var groupId = (await StandingCaptureDriver.CaptureAsync(factory, settled.Capture.Id)).SubmissionGroupId
            ?? throw new InvalidOperationException("The settle formed no submission group.");
        var waiting = await StandingCaptureDriver.StartAsync(factory, client, link, "ZZ88 HHH");
        var waitingPhoto = await StandingCaptureDriver.SendItemAsync(
            factory, client, waiting, FirstOverview, MultiFormatFixture.Photograph(2), "image/jpeg");
        var waitingReceiptId = await StandingCaptureDriver.DrainAsync(factory, waitingPhoto);
        // The window is judged on the SQL server's clock, not the test's fixed one.
        var longAgo = DateTimeOffset.UtcNow - IntakeEscapeWindow.Length - TimeSpan.FromDays(1);
        await using (var db = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            Assert.Equal(1, await db.IntakeSubmissionGroups.Where(value => value.Id == groupId).ExecuteUpdateAsync(update => update
                .SetProperty(value => value.ReceivedAtUtc, longAgo)));
            await db.IntakeStagedReceipts.Where(value => value.Id == waitingPhoto.StagedReceiptId).ExecuteUpdateAsync(update => update
                .SetProperty(value => value.ReceivedAtUtc, longAgo));
            await db.IntakeReceipts.Where(value => value.Id == waitingReceiptId).ExecuteUpdateAsync(update => update
                .SetProperty(value => value.ReceivedAtUtc, longAgo));
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await IntakeWebDriver.ReconcileGroupedImageIntakeAsync(scope.ServiceProvider);
        }

        Assert.False(await AnythingUnidentifiedAsync(factory), "A photo link's capture was registered Unidentified.");
        Assert.Equal("awaiting_instruction", (await StandingCaptureDriver.RecordForGroupAsync(factory, groupId)).LifecycleState);
        await using var verify = factory.Services.CreateAsyncScope();
        var receipt = await verify.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(waitingReceiptId, CancellationToken.None)
            ?? throw new InvalidOperationException("The waiting photo's receipt was not found.");
        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
    }

    /// <summary>
    /// The settle lists a cancelled capture that holds a photo, whose photos
    /// still settle (R794), and not a cancelled one with none (R792).
    /// </summary>
    [Fact]
    public async Task ACancelledCaptureWithPhotosIsListedForSettleAndOneWithoutIsNot()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var withPhoto = await StandingCaptureDriver.StartAsync(factory, client, link, UnheldRegistration);
        var withoutPhoto = await StandingCaptureDriver.StartAsync(factory, client, link, "ZZ88 HHH");
        var photo = await StandingCaptureDriver.SendItemAsync(
            factory, client, withPhoto, FirstOverview, MultiFormatFixture.Photograph(1), "image/jpeg");
        await StandingCaptureDriver.DrainAsync(factory, photo);
        await using var scope = factory.Services.CreateAsyncScope();
        var actions = scope.ServiceProvider.GetRequiredService<PhotoLinkActions>();
        await actions.RevokeAsync(Administrator, withPhoto.Capture.Id, Guid.NewGuid().ToString("N"), CancellationToken.None);
        await actions.RevokeAsync(Administrator, withoutPhoto.Capture.Id, Guid.NewGuid().ToString("N"), CancellationToken.None);

        var listed = await scope.ServiceProvider.GetRequiredService<IPhotoCaptureStore>()
            .ListAwaitingSettleAsync(IntakeWebApplicationFactory.FixedUtcNow, after: null, 50, CancellationToken.None);

        Assert.Contains(listed, candidate => candidate.Capture.Id == withPhoto.Capture.Id);
        Assert.DoesNotContain(listed, candidate => candidate.Capture.Id == withoutPhoto.Capture.Id);
    }

    /// <summary>
    /// An earlier batch's record that reaches a Case after a later batch went
    /// straight to another Case finishes on its own Case (R803, R804): the
    /// note is written there, its own kept paperwork files there, and the
    /// bell rings for it, while the later batch's paperwork stays where its
    /// own settle filed it and the capture keeps its Case.
    /// </summary>
    [Fact]
    public async Task AnEarlierBatchsRecordThatReachesAnotherCaseFinishesThereAlone()
    {
        const string note = "Please call before 5pm.";
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var started = await StandingCaptureDriver.StartAsync(factory, client, link, "XY99 ZZZ");
        var (_, firstPhotographReceipts, firstPaperwork) = await SendPhotographsAndPaperworkAsync(factory, client, started);
        var firstPaperworkReceipt = await ReceiptOfAsync(factory, firstPaperwork);
        await StandingCaptureDriver.CompleteAsync(client, started.Token, note);
        await SettleAndReconcileAsync(factory);
        var firstRecord = await StandingCaptureDriver.RecordForGroupAsync(
            factory, PhotoCaptureIntakeRules.BatchGroupId(started.Capture.Id, 1));
        Assert.Equal("awaiting_instruction", firstRecord.LifecycleState);
        await StandingCaptureDriver.MoveLastSettleBackAsync(factory, started.Capture.Id);

        // The second batch: a photo and its own paperwork, then a Case with the registration appears.
        var secondPhoto = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, FourthOverview, MultiFormatFixture.Photograph(3), "image/jpeg");
        var secondPaperwork = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, Paperwork, IntakeTestEvidence.CreatePdf("Engineer's invoice"), "application/pdf");
        Assert.Equal((2, 2), (secondPhoto.Batch, secondPaperwork.Batch));
        await StandingCaptureDriver.DrainAsync(factory, secondPhoto);
        var secondPaperworkReceipt = await StandingCaptureDriver.DrainAsync(factory, secondPaperwork);
        var straightCase = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        await StandingCaptureDriver.SetCaseRegistrationAsync(factory, straightCase, "XY99ZZZ");
        await StandingCaptureDriver.CompleteAsync(client, started.Token, note);
        // A second tick is what the Worker's timer would run should the first find the filing still in flight.
        await StandingCaptureDriver.SettleAsync(factory);
        await StandingCaptureDriver.SettleAsync(factory);
        var afterSecond = await StandingCaptureDriver.CaptureAsync(factory, started.Capture.Id);
        Assert.Equal(2, afterSecond.SettledBatches);
        Assert.Equal(straightCase, afterSecond.CaseId);
        Assert.True(await StandingCaptureDriver.FiledOnAsync(factory, secondPaperworkReceipt, straightCase));

        // Staff merge the first batch's record into a different Case.
        var reachedCase = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory, sequence: 10);
        await CreateCaseCustodyRootAsync(factory, reachedCase);
        await StandingCaptureDriver.AssignEngineerAsync(factory, reachedCase);
        await ImageIntakeTestData.ProcessImageCaseCustodyAsync(factory);
        await MergeRecordIntoCaseAsync(factory, firstRecord.Id, firstPhotographReceipts, reachedCase);
        await ImageIntakeTestData.ProcessCustodyAsync(factory, Pegasus.Core.Custody.ExternalWorkKinds.MergeImageCaseCustody);

        await StandingCaptureDriver.SettleAsync(factory);

        Assert.Equal(
            "Customer's note (photo link): " + note,
            Assert.Single(await StandingCaptureDriver.CaseEventReasonsAsync(factory, reachedCase, Pegasus.Core.Cases.AddCaseNote.EventType)));
        Assert.True(await StandingCaptureDriver.FiledOnAsync(factory, firstPaperworkReceipt, reachedCase));
        Assert.Equal(DocumentSemanticRole.Other, await KeptFileRoleAsync(factory, reachedCase, firstPaperwork.FileName));
        Assert.Equal(1, await StandingCaptureDriver.PhotosReceivedCountAsync(factory, reachedCase));
        Assert.False(await StandingCaptureDriver.FiledOnAsync(factory, secondPaperworkReceipt, reachedCase));
        Assert.Equal(straightCase, (await StandingCaptureDriver.AssociationAsync(factory, secondPaperworkReceipt)).CaseId);
        Assert.Equal(straightCase, (await StandingCaptureDriver.CaptureAsync(factory, started.Capture.Id)).CaseId);
        var batches = await StandingCaptureDriver.BatchesAsync(factory, started.Capture.Id);
        Assert.Equal(2, batches.Length);
        Assert.Equal(reachedCase, batches[0].CaseId);
        Assert.True(batches[0].ReachedAtUtc.HasValue, "The first batch's reach was not recorded.");
        Assert.False(batches[0].BellOwedAtUtc.HasValue, "The reach's bell was left owed.");
        Assert.Equal(straightCase, batches[1].CaseId);
        Assert.False(batches[1].ReachedAtUtc.HasValue, "The later batch was recorded as reaching a Case through a record.");
    }

    /// <summary>
    /// A batch with no photograph filed on its Case writes nothing there: its
    /// only photo was linked by staff to another Case before the settle
    /// matched the capture to this one, so the batch closes with no images
    /// completed, no note and no bell.
    /// </summary>
    [Fact]
    public async Task ABatchWithNoPhotographFiledOnItsCaseWritesNothingThere()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var started = await StandingCaptureDriver.StartAsync(factory, client, link, "XY99 ZZZ");
        var photo = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, FirstOverview, MultiFormatFixture.Photograph(1), "image/jpeg");
        var receiptId = await StandingCaptureDriver.DrainAsync(factory, photo);
        var elsewhere = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory, sequence: 10);
        await StandingCaptureDriver.LinkByStaffAsync(factory, receiptId, elsewhere);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        await StandingCaptureDriver.SetCaseRegistrationAsync(factory, caseId, "XY99ZZZ");
        await StandingCaptureDriver.AssignEngineerAsync(factory, caseId);
        await StandingCaptureDriver.CompleteAsync(client, started.Token, "Please call before 5pm.");

        await StandingCaptureDriver.SettleAsync(factory);
        await StandingCaptureDriver.SettleAsync(factory);

        var capture = await StandingCaptureDriver.CaptureAsync(factory, started.Capture.Id);
        Assert.Equal(1, capture.SettledBatches);
        Assert.Equal(caseId, capture.CaseId);
        Assert.False(capture.ImagesCompletedAtUtc.HasValue, "A batch with no photo on the Case spent the images-complete.");
        var closed = Assert.Single(await StandingCaptureDriver.BatchesAsync(factory, started.Capture.Id));
        Assert.Equal(caseId, closed.CaseId);
        Assert.False(closed.BellOwedAtUtc.HasValue, "A batch with no photo on the Case owed a bell.");
        Assert.False(await StandingCaptureDriver.FiledOnAsync(factory, receiptId, caseId));
        Assert.Equal(elsewhere, (await StandingCaptureDriver.AssociationAsync(factory, receiptId)).CaseId);
        Assert.False(await StandingCaptureDriver.ImagesCompleteAsync(factory, caseId));
        Assert.Empty(await StandingCaptureDriver.CaseEventReasonsAsync(factory, caseId, EfPhotoCaptureCaseWriter.ImagesCompletedEventType));
        Assert.Empty(await StandingCaptureDriver.CaseEventReasonsAsync(factory, caseId, Pegasus.Core.Cases.AddCaseNote.EventType));
        Assert.Equal(0, await StandingCaptureDriver.PhotosReceivedCountAsync(factory, caseId));
    }

    /// <summary>
    /// A record a member of staff merged takes its batch's kept paperwork as
    /// that member of staff's decision, which files on a Case in any state
    /// that is not archived (R790): here the Case's report was sent between
    /// the merge and the settle.
    /// </summary>
    [Fact]
    public async Task AStaffMergeFilesTheKeptPaperworkOnAReportSentCase()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var started = await StandingCaptureDriver.StartAsync(factory, client, link, UnheldRegistration);
        var (_, photographReceipts, paperwork) = await SendPhotographsAndPaperworkAsync(factory, client, started);
        var paperworkReceipt = await ReceiptOfAsync(factory, paperwork);
        await StandingCaptureDriver.CompleteAsync(client, started.Token);
        await SettleAndReconcileAsync(factory);
        var record = await StandingCaptureDriver.RecordForGroupAsync(
            factory, PhotoCaptureIntakeRules.BatchGroupId(started.Capture.Id, 1));
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        await CreateCaseCustodyRootAsync(factory, caseId);
        await ImageIntakeTestData.ProcessImageCaseCustodyAsync(factory);
        await MergeRecordIntoCaseAsync(factory, record.Id, photographReceipts, caseId);
        await ImageIntakeTestData.ProcessCustodyAsync(factory, Pegasus.Core.Custody.ExternalWorkKinds.MergeImageCaseCustody);
        await PhotoCaptureWebTests.MarkReportSentAsync(factory, caseId);

        await StandingCaptureDriver.SettleAsync(factory);

        var association = await StandingCaptureDriver.AssociationAsync(factory, paperworkReceipt);
        Assert.Equal(caseId, association.CaseId);
        Assert.Equal(nameof(Pegasus.Core.Identity.ActorKind.Staff), association.ActorKind);
        Assert.Equal(DevelopmentOfflineIdentity.AdministratorId, Guid.Parse(association.ActorSubjectId));
        Assert.True(await StandingCaptureDriver.FiledOnAsync(factory, paperworkReceipt, caseId));
        Assert.Equal(DocumentSemanticRole.Other, await KeptFileRoleAsync(factory, caseId, paperwork.FileName));
    }

    /// <summary>
    /// A record Pegasus paired with a Case takes its batch's kept paperwork as
    /// the system work that follows the record's merge, not as any member of
    /// staff's decision, so it files wherever the photos went (operator,
    /// 10 November 2026; R844); it files as Other and the bell rings once.
    /// </summary>
    [Fact]
    public async Task APegasusPairingFilesTheKeptPaperworkAsTheWorkThatFollowsTheMerge()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var started = await StandingCaptureDriver.StartAsync(factory, client, link, UnheldRegistration);
        var (_, _, paperwork) = await SendPhotographsAndPaperworkAsync(factory, client, started);
        var paperworkReceipt = await ReceiptOfAsync(factory, paperwork);
        await StandingCaptureDriver.CompleteAsync(client, started.Token);
        await SettleAndReconcileAsync(factory);
        var groupId = PhotoCaptureIntakeRules.BatchGroupId(started.Capture.Id, 1);
        Assert.Equal("awaiting_instruction", (await StandingCaptureDriver.RecordForGroupAsync(factory, groupId)).LifecycleState);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        await StandingCaptureDriver.SetCaseRegistrationAsync(factory, caseId, "ZZ99QQQ");
        await CreateCaseCustodyRootAsync(factory, caseId);
        await StandingCaptureDriver.AssignEngineerAsync(factory, caseId);
        await ImageIntakeTestData.ProcessImageCaseCustodyAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            // The pairing a newly accepted Case runs.
            await scope.ServiceProvider.GetRequiredService<Pegasus.Core.ImageIntake.IImageIntakeCasePairing>()
                .PairAcceptedCaseAsync(caseId, CancellationToken.None);
        }

        Assert.Equal(caseId, (await StandingCaptureDriver.RecordForGroupAsync(factory, groupId)).MergedIntoCaseId);
        await ImageIntakeTestData.ProcessCustodyAsync(factory, Pegasus.Core.Custody.ExternalWorkKinds.MergeImageCaseCustody);

        await StandingCaptureDriver.SettleAsync(factory);

        var association = await StandingCaptureDriver.AssociationAsync(factory, paperworkReceipt);
        Assert.Equal(caseId, association.CaseId);
        Assert.Equal(nameof(Pegasus.Core.Identity.ActorKind.SystemWorker), association.ActorKind);
        Assert.Equal(PhotoCaptureIntakeRules.ReachActorId, association.ActorSubjectId);
        Assert.True(await StandingCaptureDriver.FiledOnAsync(factory, paperworkReceipt, caseId));
        Assert.Equal(DocumentSemanticRole.Other, await KeptFileRoleAsync(factory, caseId, paperwork.FileName));
        Assert.Equal(1, await StandingCaptureDriver.PhotosReceivedCountAsync(factory, caseId));
    }

    /// <summary>
    /// Recording a Case on a batch that has closed records nothing: the store
    /// answers that it did not, and the capture keeps no Case.
    /// </summary>
    [Fact]
    public async Task RecordingACaseOnAClosedBatchRecordsNothing()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var started = await StandingCaptureDriver.StartAsync(factory, client, link, UnheldRegistration);
        var photo = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, FirstOverview, MultiFormatFixture.Photograph(1), "image/jpeg");
        await StandingCaptureDriver.DrainAsync(factory, photo);
        await StandingCaptureDriver.CompleteAsync(client, started.Token);
        await SettleAndReconcileAsync(factory);
        Assert.Equal(1, (await StandingCaptureDriver.CaptureAsync(factory, started.Capture.Id)).SettledBatches);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);

        PhotoCaptureCaseRecorded recorded;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            recorded = await scope.ServiceProvider.GetRequiredService<IPhotoCaptureStore>().RecordCaseAsync(
                started.Capture.Id, batch: 1, caseId, RoutePhotoCapture.SingleEligibleCase, CancellationToken.None);
        }

        Assert.False(recorded.Changed, "A Case was recorded on a batch that had closed.");
        var capture = await StandingCaptureDriver.CaptureAsync(factory, started.Capture.Id);
        Assert.False(capture.CaseId.HasValue, "The capture took a Case for a closed batch.");
        Assert.Equal(PhotoCaptureDestination.VehicleImages, capture.Destination);
    }

    /// <summary>The processed receipt an item learned when its staged receipt was drained.</summary>
    private static async Task<Guid> ReceiptOfAsync(IntakeWebApplicationFactory factory, PhotoCaptureItem item)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var current = await scope.ServiceProvider.GetRequiredService<IPhotoCaptureStore>()
            .FindItemAsync(item.CaptureId, item.ClientItemId, CancellationToken.None)
            ?? throw new InvalidOperationException("The item was not found.");
        return current.IntakeReceiptId
            ?? throw new InvalidOperationException("The item did not learn its processed receipt.");
    }

    /// <summary>The role of the one document filed on the Case under the kept file's name.</summary>
    private static async Task<DocumentSemanticRole> KeptFileRoleAsync(IntakeWebApplicationFactory factory, Guid caseId, string fileName)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        return Assert.Single(await (from occurrence in db.Set<DocumentOccurrenceEntity>().AsNoTracking()
                                    join version in db.Set<DocumentVersionEntity>().AsNoTracking() on occurrence.VersionId equals version.Id
                                    where occurrence.CaseId == caseId && version.FileName == fileName
                                    select occurrence.SemanticRole).ToListAsync());
    }

    /// <summary>
    /// Two photographs and a paperwork PDF on the capture, each drained as the
    /// Worker would; answers the photographs' processed receipts beside them.
    /// </summary>
    private static async Task<(PhotoCaptureItem[] Photographs, Guid[] PhotographReceipts, PhotoCaptureItem Paperwork)> SendPhotographsAndPaperworkAsync(
        IntakeWebApplicationFactory factory,
        HttpClient client,
        StartedCapture started)
    {
        var first = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, FirstOverview, MultiFormatFixture.Photograph(1), "image/jpeg");
        var second = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, SecondOverview, MultiFormatFixture.Photograph(2), "image/jpeg");
        var paperwork = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, Paperwork, IntakeTestEvidence.CreatePdf("Repair estimate"), "application/pdf");
        var firstReceipt = await StandingCaptureDriver.DrainAsync(factory, first);
        var secondReceipt = await StandingCaptureDriver.DrainAsync(factory, second);
        await StandingCaptureDriver.DrainAsync(factory, paperwork);
        return ([first, second], [firstReceipt, secondReceipt], paperwork);
    }

    /// <summary>The settle, then the grouped-image sweep that re-drives a group whose first member's replay did not register it.</summary>
    private static async Task SettleAndReconcileAsync(IntakeWebApplicationFactory factory)
    {
        await StandingCaptureDriver.SettleAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        await IntakeWebDriver.ReconcileGroupedImageIntakeAsync(scope.ServiceProvider);
    }

    private static async Task<IntakeSubmissionGroup?> FindGroupAsync(IntakeWebApplicationFactory factory, Guid groupId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIntakeSubmissionGroupStore>()
            .GetAsync(groupId, CancellationToken.None);
    }

    private static async Task<IntakeSubmissionGroup> GroupAsync(IntakeWebApplicationFactory factory, Guid groupId) =>
        await FindGroupAsync(factory, groupId)
        ?? throw new InvalidOperationException("The submission group was not found.");

    /// <summary>Whether the estate holds any Unidentified item; the tests' databases start with none.</summary>
    private static async Task<bool> AnythingUnidentifiedAsync(IntakeWebApplicationFactory factory)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        return await db.UnidentifiedItems.AnyAsync();
    }

    private static async Task AssertRecordShowsThePhotoLinkAsync(HttpClient client, Guid recordId, Guid captureId)
    {
        var page = await IntakeWebDriver.GetHtmlAsync(client, $"/VehicleImages/{recordId:D}");
        Assert.Contains("id=\"image-intake-photo-link-title\"", page, StringComparison.Ordinal);
        Assert.Contains($"/PhotoCapture/{captureId:D}", page, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates the seeded Case's evidence folder in the test custody and
    /// records it on the Case, as <c>ImageCaseCustodyIntegrationTests</c>'
    /// <c>SeedCaseWithFolderAsync</c> does: the merge's fold moves the
    /// record's photographs into a folder that must already exist.
    /// </summary>
    private static async Task CreateCaseCustodyRootAsync(IntakeWebApplicationFactory factory, Guid caseId)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        var reference = await db.Cases.Where(value => value.Id == caseId).Select(value => value.Reference).SingleAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var root = await scope.ServiceProvider.GetRequiredService<Pegasus.Core.Custody.ICaseCustody>().CreateCaseRootAsync(
            caseId, reference, $"photo-capture-case-root:{caseId:N}", CancellationToken.None);
        Assert.Equal(1, await db.Cases.Where(value => value.Id == caseId).ExecuteUpdateAsync(update => update
            .SetProperty(value => value.CustodyRootRemoteId, root.RemoteId)
            .SetProperty(value => value.CustodyState, "confirmed")));
    }

    /// <summary>
    /// Staff link the record's photograph receipts to the Case and merge the
    /// record into it, as <c>ImageCaseCustodyIntegrationTests</c> does; the
    /// merge queues the fold.
    /// </summary>
    private static async Task MergeRecordIntoCaseAsync(
        IntakeWebApplicationFactory factory,
        Guid recordId,
        IReadOnlyList<Guid> receiptIds,
        Guid caseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var store = services.GetRequiredService<Pegasus.Core.ImageIntake.IImageIntakeStore>();
        var mutations = services.GetRequiredService<IIntakeMutationStore>();
        var receipts = services.GetRequiredService<IIntakeReceiptQueries>();
        var workflows = services.GetRequiredService<Pegasus.Core.Workflow.ICaseWorkflowQueries>();
        foreach (var receiptId in receiptIds)
        {
            var workflow = await workflows.GetAsync(caseId, CancellationToken.None);
            var lease = await services.GetRequiredService<Pegasus.Core.Workflow.ILeaseCaseForEdit>().ClaimAsync(
                new(caseId, workflow!.Version, Administrator, $"photo-capture-link-lease:{receiptId:N}"),
                CancellationToken.None);
            var receipt = await receipts.GetAsync(receiptId, CancellationToken.None);
            await mutations.LinkAsync(
                new(receiptId, caseId, receipt!.Version, workflow.Version, lease.Token, Administrator,
                    $"photo-capture-link:{receiptId:N}", "Staff confirmed these photos belong to the instruction Case."),
                DateTimeOffset.UtcNow,
                CancellationToken.None);
        }

        var detail = await store.GetAsync(recordId, CancellationToken.None);
        var recordLease = await services.GetRequiredService<Pegasus.Core.Workflow.IEditScopeLeases>().ClaimAsync(
            new(
                Pegasus.Core.Workflow.EditScopeKind.ImageIntake,
                recordId,
                detail!.LifecycleVersion,
                Administrator,
                $"photo-capture-merge-lease:{recordId:N}"),
            CancellationToken.None);
        await store.MergeAsync(
            new(
                recordId,
                caseId,
                Administrator,
                $"photo-capture-merge:{recordId:N}",
                "The Image-initiated case was merged into the linked formal Case.",
                detail.LifecycleVersion,
                ExpectedStaffOriginAssociationVersion: 0)
            {
                EditLeaseToken = recordLease.Token
            },
            CancellationToken.None);
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
            .CreateForContactAsync(Administrator, Guid.NewGuid().ToString("N"), organizationId, CancellationToken.None);
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

    /// <summary>Starts a capture on the link with the whole short form the phone sends; answers its token and the capture.</summary>
    internal static async Task<StartedCapture> StartWithFormAsync(
        IntakeWebApplicationFactory factory,
        HttpClient client,
        StandingPhotoLink link,
        object form)
    {
        using var started = await SendAsync(
            client, HttpMethod.Post, $"{Api}/start", Standing(link), Json(JsonSerializer.Serialize(form)));
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        var token = (await JsonAsync(started)).GetProperty("token").GetString()
            ?? throw new InvalidOperationException("The start answered no token.");
        return new(token, await CaptureByTokenAsync(factory, token));
    }

    /// <summary>The sender presses Send on the capture's token, with a note when one is given.</summary>
    internal static async Task CompleteAsync(HttpClient client, string token, string? note = null)
    {
        using var completed = await SendAsync(
            client,
            HttpMethod.Post,
            $"{Api}/complete",
            Capture(token),
            note is null ? null : Json(JsonSerializer.Serialize(new { note })));
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
    }

    /// <summary>Runs the Worker's settle once, standing in for its timer.</summary>
    internal static async Task<SettlePhotoCapturesResult> SettleAsync(IntakeWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await IntakeWebDriver.SettlePhotoCapturesAsync(scope.ServiceProvider);
    }

    /// <summary>Makes the development Administrator the Case's Engineer, so the Case's bell has someone to ring for.</summary>
    internal static async Task AssignEngineerAsync(IntakeWebApplicationFactory factory, Guid caseId)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        await db.CaseWorkflows.Where(value => value.CaseId == caseId).ExecuteUpdateAsync(update => update
            .SetProperty(value => value.AssignedEngineerId, (Guid?)DevelopmentOfflineIdentity.AdministratorId));
    }

    /// <summary>How many "Photos received" notifications the Case raised.</summary>
    internal static async Task<int> PhotosReceivedCountAsync(IntakeWebApplicationFactory factory, Guid caseId)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        var photosReceived = nameof(Pegasus.Core.Notifications.StaffNotificationCause.PhotosReceived);
        return await db.Set<StaffNotificationEntity>().AsNoTracking()
            .CountAsync(value => value.CaseId == caseId && value.Cause == photosReceived);
    }

    /// <summary>The reasons of the Case's workflow events of one type, in the order they were written.</summary>
    internal static async Task<string?[]> CaseEventReasonsAsync(IntakeWebApplicationFactory factory, Guid caseId, string eventType)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        return await db.CaseWorkflowEvents.AsNoTracking()
            .Where(value => value.CaseId == caseId && value.EventType == eventType)
            .OrderBy(value => value.OccurredAtUtc)
            .Select(value => value.Reason)
            .ToArrayAsync();
    }

    internal static async Task<bool> ImagesCompleteAsync(IntakeWebApplicationFactory factory, Guid caseId)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        return await db.Cases.Where(value => value.Id == caseId).Select(value => value.ImagesComplete).SingleAsync();
    }

    /// <summary>
    /// Moves the capture's last settle an hour back. The test clock is fixed,
    /// so a Send after a settle would otherwise carry the settle's own time
    /// and read as already settled.
    /// </summary>
    internal static async Task MoveLastSettleBackAsync(IntakeWebApplicationFactory factory, Guid captureId)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        await db.PhotoCaptures.Where(value => value.Id == captureId).ExecuteUpdateAsync(update => update
            .SetProperty(value => value.SettledAtUtc, (DateTimeOffset?)IntakeWebApplicationFactory.FixedUtcNow.AddHours(-1)));
    }

    /// <summary>The Vehicle images record a submission group registered.</summary>
    internal static async Task<ImageIntakeEntity> RecordForGroupAsync(IntakeWebApplicationFactory factory, Guid groupId)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        return await db.ImageIntakes.AsNoTracking().SingleAsync(value => value.SubmissionGroupId == groupId);
    }

    /// <summary>The capture's settled batches, as the close and the reach record them, in batch order.</summary>
    internal static async Task<PhotoCaptureBatchEntity[]> BatchesAsync(IntakeWebApplicationFactory factory, Guid captureId)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        return await db.PhotoCaptureBatches.AsNoTracking()
            .Where(value => value.CaptureId == captureId)
            .OrderBy(value => value.Batch)
            .ToArrayAsync();
    }

    /// <summary>
    /// Marks a closed batch's bell as owed, as a Worker that stopped between
    /// the close and the ring leaves it.
    /// </summary>
    internal static async Task OweBellAsync(IntakeWebApplicationFactory factory, Guid captureId, int batch)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        Assert.Equal(1, await db.PhotoCaptureBatches
            .Where(value => value.CaptureId == captureId && value.Batch == batch)
            .ExecuteUpdateAsync(update => update
                .SetProperty(value => value.BellOwedAtUtc, (DateTimeOffset?)IntakeWebApplicationFactory.FixedUtcNow)));
    }

    /// <summary>Whether the receipt's evidence finished filing on the Case, as the filer records it.</summary>
    internal static async Task<bool> FiledOnAsync(IntakeWebApplicationFactory factory, Guid receiptId, Guid caseId)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        return await db.IntakeMutationHistory.AsNoTracking().AnyAsync(value =>
            value.IntakeReceiptId == receiptId
            && value.CaseId == caseId
            && value.EventType == EfCaseFilingReplayStore.PromotionCompletedEventType);
    }

    /// <summary>The receipt's association with a Case, and who made it.</summary>
    internal static async Task<IntakeManualAssociationEntity> AssociationAsync(IntakeWebApplicationFactory factory, Guid receiptId)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        return await db.IntakeManualAssociations.AsNoTracking().SingleAsync(value => value.IntakeReceiptId == receiptId);
    }

    /// <summary>A member of staff links the receipt to the Case from the Intake log, holding the Case's edit lease.</summary>
    internal static async Task LinkByStaffAsync(IntakeWebApplicationFactory factory, Guid receiptId, Guid caseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var workflow = await services.GetRequiredService<Pegasus.Core.Workflow.ICaseWorkflowQueries>()
            .GetAsync(caseId, CancellationToken.None);
        var lease = await services.GetRequiredService<Pegasus.Core.Workflow.ILeaseCaseForEdit>().ClaimAsync(
            new(caseId, workflow!.Version, Administrator, $"photo-capture-staff-link-lease:{receiptId:N}"),
            CancellationToken.None);
        var receipt = await services.GetRequiredService<IIntakeReceiptQueries>().GetAsync(receiptId, CancellationToken.None);
        await services.GetRequiredService<ILinkIntake>().ExecuteAsync(
            new(
                receiptId,
                caseId,
                receipt!.Version,
                lease.Version,
                lease.Token,
                Administrator,
                $"photo-capture-staff-link:{receiptId:N}",
                "Staff confirmed this photo belongs to another Case."),
            CancellationToken.None);
    }
}
