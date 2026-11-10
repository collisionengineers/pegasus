using Pegasus.Core.AiWork;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Notifications;
using Pegasus.Core.PhotoCapture;
using Pegasus.Core.Tests.ImageIntake;
using static Pegasus.Core.Tests.PhotoCapture.PhotoCaptureTestData;

namespace Pegasus.Core.Tests.PhotoCapture;

/// <summary>
/// The settle (FRD-28; operator, 9 November 2026, R791 to R794, R803,
/// R804): a Case batch waits until its photos have filed, completes the
/// images once per capture, writes the note once per wording, closes and
/// rings once; a Vehicle images batch is matched again with the link's
/// Principal and the typed date; a record that reaches a Case finishes there.
/// </summary>
public sealed class SettlePhotoCapturesTests
{
    private static readonly Guid CaseId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid PrincipalId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateOnly TypedDate = new(2026, 10, 28);
    private static readonly string[] FilledFields = ["claimant_contact_number", "claimant_name", "incident_date", "vehicle_mileage"];
    private static readonly string[] CaseBatchOrder = ["images", "note", "fill", "close", "bell"];

    [Fact]
    public async Task ASentCaseCaptureCompletesTheImagesWritesTheNoteFillsTheFieldsAndTellsTheEngineer()
    {
        var harness = new Harness();
        var capture = Capture(
            openedAtUtc: Now.AddMinutes(-30),
            acceptedItemCount: 2,
            sentAtUtc: Now.AddMinutes(-1),
            note: "The boot doesn't close.",
            incidentDate: TypedDate,
            mileage: 48_210);
        harness.Store.Add(capture, OnCase(PhotoCaptureItemKind.Overview, 0), OnCase(PhotoCaptureItemKind.NumberPlate, 1));

        var result = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, result.Settled);
        Assert.Equal(0, result.Failures);
        var close = Assert.Single(harness.Store.Closes);
        Assert.Null(close.Group);
        var images = Assert.Single(harness.CaseWriter.ImagesCompleted);
        Assert.Equal((CaseId, capture.Id), (images.CaseId, images.CaptureId));
        var note = Assert.Single(harness.Notes.Written);
        Assert.Equal("Customer's note (photo link): The boot doesn't close.", note.Note);
        Assert.Equal(ActorKind.SystemWorker, note.Actor.Kind);
        Assert.Equal(FilledFields, harness.CaseWriter.Filled.Single().Keys.Order());
        Assert.Equal((StaffNotificationCause.PhotosReceived, CaseId), Assert.Single(harness.Notifier.Raised));
        Assert.Empty(harness.Mutations.Links);
        Assert.Empty(harness.Processor.Redriven);
        // The Case-side work runs before the batch closes, and the bell after.
        Assert.Equal(CaseBatchOrder, harness.Events);
    }

    [Fact]
    public async Task ACaseBatchWaitsUntilEveryPhotoHasFiledNotOnlyBeenEvaluated()
    {
        var harness = new Harness();
        harness.Store.Add(Capture(sentAtUtc: Now), OnCase(PhotoCaptureItemKind.Overview, 0, intakeReceiptId: Guid.NewGuid()));
        // Every photo is evaluated, none has filed on the Case yet.
        harness.Store.Settled = (_, _, caseId) => caseId is null;

        var waiting = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(0, waiting.Settled);
        Assert.Equal(CaseId, Assert.Single(harness.Store.SettledAsked).CaseId);
        Assert.Empty(harness.Store.Closes);
        Assert.Empty(harness.CaseWriter.ImagesCompleted);
        Assert.Empty(harness.Notifier.Raised);

        harness.Store.Settled = (_, _, _) => true;
        var filed = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, filed.Settled);
        Assert.Single(harness.CaseWriter.ImagesCompleted);
        Assert.Single(harness.Notifier.Raised);
    }

    [Fact]
    public async Task ASendWithNoPhotosSettlesNothing()
    {
        var harness = new Harness();
        var paperwork = OnCase(PhotoCaptureItemKind.Paperwork, 0) with { MediaType = "application/pdf" };
        // A photo whose upload never reached intake is not a photo the batch holds.
        var lost = OnCase(PhotoCaptureItemKind.Overview, 1) with { StagedReceiptId = null, ReceivedAtUtc = Now.AddHours(-1) };
        harness.Store.Add(Capture(sentAtUtc: Now, note: "Only the paperwork."), paperwork, lost);

        var result = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(0, result.Settled);
        Assert.Empty(harness.Store.Closes);
        Assert.Empty(harness.CaseWriter.ImagesCompleted);
        Assert.Empty(harness.Notes.Written);
        Assert.Empty(harness.CaseWriter.Filled);
        Assert.Empty(harness.Notifier.Raised);
    }

    /// <summary>
    /// The images complete once per capture (R792): a second batch never
    /// completes them again, so a later staff change stands.
    /// </summary>
    [Fact]
    public async Task ASecondBatchMakesNoImagesCallAndRingsAgain()
    {
        var harness = new Harness();
        var capture = Capture(sentAtUtc: Now.AddMinutes(-10));
        harness.Store.Add(capture, OnCase(PhotoCaptureItemKind.Overview, 0));
        await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        harness.Store.SendAgain(capture.Id, Now.AddMinutes(1), note: null, OnCase(PhotoCaptureItemKind.Dashboard, 0, batch: 2));
        var second = await harness.Settle(Now.AddMinutes(2)).ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, second.Settled);
        Assert.Single(harness.CaseWriter.ImagesCompleted);
        Assert.Equal(2, harness.Store.Closes.Count);
        Assert.Equal(2, harness.Notifier.Raised.Count);
    }

    [Fact]
    public async Task TheSameNoteReplaysAndAChangedNoteIsAppended()
    {
        var harness = new Harness();
        var capture = Capture(sentAtUtc: Now.AddMinutes(-10), note: "Rattle from the back.");
        harness.Store.Add(capture, OnCase(PhotoCaptureItemKind.Overview, 0));
        await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        harness.Store.SendAgain(capture.Id, Now.AddMinutes(1), "Rattle from the back.", OnCase(PhotoCaptureItemKind.Dashboard, 0, batch: 2));
        await harness.Settle(Now.AddMinutes(2)).ExecuteAsync(50, CancellationToken.None);

        Assert.Single(harness.Notes.Written);

        harness.Store.SendAgain(capture.Id, Now.AddMinutes(3), "Rattle from the back, and the boot leaks.", OnCase(PhotoCaptureItemKind.Vin, 0, batch: 3));
        await harness.Settle(Now.AddMinutes(4)).ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(2, harness.Notes.Written.Count);
        Assert.Equal("Customer's note (photo link): Rattle from the back, and the boot leaks.", harness.Notes.Written[^1].Note);
        Assert.NotEqual(harness.Notes.Written[0].OperationKey, harness.Notes.Written[1].OperationKey);
    }

    [Fact]
    public async Task TheBellRingsOncePerClosedBatch()
    {
        var harness = new Harness();
        harness.Store.Add(Capture(sentAtUtc: Now), OnCase(PhotoCaptureItemKind.Overview, 0), OnCase(PhotoCaptureItemKind.Damage, 1));

        await harness.Settle().ExecuteAsync(50, CancellationToken.None);
        var again = await harness.Settle(Now.AddMinutes(5)).ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(0, again.Settled);
        Assert.Single(harness.Store.Closes);
        Assert.Single(harness.Notifier.Raised);
    }

    [Fact]
    public async Task AFailedCaseSideStepRetriesOnTheNextTickWithOneBell()
    {
        var harness = new Harness();
        harness.Store.Add(Capture(sentAtUtc: Now), OnCase(PhotoCaptureItemKind.Overview, 0));
        harness.CaseWriter.FailImages = 1;

        var failed = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, failed.Failures);
        Assert.Empty(harness.Store.Closes);
        Assert.Empty(harness.Notifier.Raised);

        var retried = await harness.Settle(Now.AddMinutes(5)).ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, retried.Settled);
        Assert.Single(harness.CaseWriter.ImagesCompleted);
        Assert.Single(harness.Store.Closes);
        Assert.Single(harness.Notifier.Raised);
    }

    /// <summary>
    /// The close is decided under the capture's lock, not on the version the
    /// listing read, so a Send recorded in between neither fails the settle
    /// nor rings twice (F-R6b).
    /// </summary>
    [Fact]
    public async Task ASendBetweenTheListingAndTheCloseDoesNotBreakTheSettle()
    {
        var harness = new Harness();
        var capture = Capture(sentAtUtc: Now.AddMinutes(-2));
        harness.Store.Add(capture, OnCase(PhotoCaptureItemKind.Overview, 0));
        harness.Store.BeforeClose = id => harness.Store.Captures[id] = harness.Store.Captures[id] with
        {
            SentAtUtc = Now.AddSeconds(-1),
            Version = harness.Store.Captures[id].Version + 1
        };

        var result = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, result.Settled);
        Assert.Equal(0, result.Failures);
        Assert.Single(harness.Notifier.Raised);
    }

    [Fact]
    public async Task SixtyWaitingCapturesDoNotKeepAReadyOneFromItsSettle()
    {
        var harness = new Harness();
        var waiting = new HashSet<Guid>();
        for (var index = 0; index < 60; index++)
        {
            var queued = Capture(sentAtUtc: Now, createdAtUtc: Now.AddHours(-3).AddMinutes(index)) with { Id = Guid.NewGuid() };
            waiting.Add(queued.Id);
            harness.Store.Add(queued, OnCase(PhotoCaptureItemKind.Overview, 0));
        }

        var ready = Capture(sentAtUtc: Now, createdAtUtc: Now.AddHours(-1)) with { Id = Guid.NewGuid() };
        harness.Store.Add(ready, OnCase(PhotoCaptureItemKind.Overview, 0));
        harness.Store.Settled = (captureId, _, _) => !waiting.Contains(captureId);

        var result = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, result.Settled);
        Assert.Equal(61, result.Examined);
        Assert.Equal(ready.Id, Assert.Single(harness.Store.Closes).CaptureId);
        Assert.Equal(2, harness.Store.Listings.Count);
        Assert.Null(harness.Store.Listings[0]);
        Assert.NotNull(harness.Store.Listings[1]);
    }

    /// <summary>
    /// No Case for the typed registration: the batch closes as one group of
    /// its photographs, carrying the registration and the date, before its
    /// first photograph is re-driven to register the record; the paperwork
    /// waits outside the group (R791, R803).
    /// </summary>
    [Fact]
    public async Task AVehicleImagesBatchClosesWithItsGroupBeforeTheFirstPhotographIsRedriven()
    {
        var harness = new Harness();
        var capture = Standing(sentAtUtc: Now, incidentDate: TypedDate);
        var paperwork = Item(PhotoCaptureItemKind.Paperwork, ordinal: 0, intakeReceiptId: Guid.NewGuid()) with { MediaType = "application/pdf" };
        var first = Item(PhotoCaptureItemKind.Overview, ordinal: 1, intakeReceiptId: Guid.NewGuid());
        var second = Item(PhotoCaptureItemKind.Damage, 1, "fd", PhotoCaptureStage.Overview, ordinal: 2, intakeReceiptId: Guid.NewGuid());
        harness.Store.Add(capture, paperwork, second, first);

        var result = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, result.Settled);
        var close = Assert.Single(harness.Store.Closes);
        Assert.Equal(new PhotoCaptureDeclaredGroup("RY65OFG", TypedDate), close.Group);
        Assert.Equal(first.StagedReceiptId, Assert.Single(harness.Processor.Redriven));
        Assert.Equal(["close", $"redrive:{first.StagedReceiptId}"], harness.Events);
        Assert.Null(harness.Store.SettledAsked.Single().CaseId);
        Assert.Empty(harness.Store.CaseRecords);
        Assert.Empty(harness.Mutations.Links);
        Assert.Empty(harness.CaseWriter.ImagesCompleted);
        Assert.Empty(harness.Notifier.Raised);
    }

    /// <summary>
    /// None, several, or one whose date contradicts the typed one, or one of
    /// another Principal's Cases: the batch becomes a group with the date
    /// (R791).
    /// </summary>
    [Theory]
    [InlineData("none")]
    [InlineData("several")]
    [InlineData("date contradicts")]
    [InlineData("another Principal")]
    public async Task NoneOrSeveralEligibleCasesFormTheGroupWithTheTypedDate(string cases)
    {
        var harness = new Harness();
        ImageIntakeCaseCandidate[] eligible = cases switch
        {
            "several" => [Candidate(PrincipalId), Candidate(PrincipalId)],
            "date contradicts" => [Candidate(PrincipalId, TypedDate.AddDays(-3))],
            "another Principal" => [Candidate(Guid.NewGuid())],
            _ => []
        };
        harness.Candidates.Cases.AddRange(eligible);
        harness.Store.Add(Standing(sentAtUtc: Now, incidentDate: TypedDate), Item(PhotoCaptureItemKind.Overview, intakeReceiptId: Guid.NewGuid()));

        await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(new PhotoCaptureDeclaredGroup("RY65OFG", TypedDate), Assert.Single(harness.Store.Closes).Group);
        Assert.Empty(harness.Store.CaseRecords);
        Assert.Single(harness.Processor.Redriven);
    }

    /// <summary>
    /// One eligible Case of the link's Principal whose date does not
    /// contradict: the batch goes straight onto it (R791), every receipt,
    /// paperwork included, linked as the routing's system work and re-driven
    /// to file; no group and no record. It closes once they have filed.
    /// </summary>
    [Fact]
    public async Task OneMatchAtTheSettleLinksAndRedrivesEveryReceiptWithNoGroup()
    {
        var harness = new Harness();
        var matched = Candidate(PrincipalId, TypedDate);
        harness.Candidates.Cases.Add(matched);
        var photo = Item(PhotoCaptureItemKind.Overview, ordinal: 0, intakeReceiptId: Guid.NewGuid());
        var paperwork = Item(PhotoCaptureItemKind.Paperwork, ordinal: 1, intakeReceiptId: Guid.NewGuid()) with { MediaType = "application/pdf" };
        var capture = Standing(sentAtUtc: Now, incidentDate: TypedDate);
        harness.Store.Add(capture, photo, paperwork);
        harness.Store.Settled = (_, _, caseId) => caseId is null;

        var result = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(0, result.Settled);
        var recorded = Assert.Single(harness.Store.CaseRecords);
        Assert.Equal((matched.CaseId, RoutePhotoCapture.SingleEligibleCase, (DateTimeOffset?)null), (recorded.CaseId, recorded.Reason, recorded.ImagesCompletedAtUtc));
        Assert.Equal(
            [photo.IntakeReceiptId!.Value, paperwork.IntakeReceiptId!.Value],
            harness.Mutations.Links.Select(link => link.ReceiptId));
        Assert.All(harness.Mutations.Links, link =>
        {
            Assert.Equal(matched.CaseId, link.CaseId);
            Assert.Equal(ActorKind.SystemWorker, link.Decider.Kind);
            Assert.Equal(PhotoCaptureIntakeRules.RoutedDestinationReason, link.Reason);
            Assert.Equal($"photo-capture-route:{link.ReceiptId:N}:{matched.CaseId:N}", link.OperationKey);
        });
        Assert.Equal([photo.StagedReceiptId!.Value, paperwork.StagedReceiptId!.Value], harness.Processor.Redriven);
        Assert.Empty(harness.Store.Closes);

        // Filed: the next tick settles it as a Case batch, once.
        harness.Store.Settled = (_, _, _) => true;
        var filed = await harness.Settle(Now.AddMinutes(1)).ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, filed.Settled);
        Assert.Null(Assert.Single(harness.Store.Closes).Group);
        Assert.Equal(2, harness.Mutations.Links.Count);
        Assert.Equal(2, harness.Processor.Redriven.Count);
        Assert.Single(harness.CaseWriter.ImagesCompleted);
        Assert.Equal((StaffNotificationCause.PhotosReceived, matched.CaseId), Assert.Single(harness.Notifier.Raised));
    }

    /// <summary>
    /// A record that reaches a Case: its note and fill, its kept paperwork
    /// filed (R803), the Case recorded with the images marker, the fold
    /// having completed the images; the bell rings once (R804).
    /// </summary>
    [Fact]
    public async Task TheReachedPathMakesNoImagesCallFilesTheKeptPaperworkAndRingsOnce()
    {
        var harness = new Harness();
        var capture = Standing(sentAtUtc: Now.AddHours(-2), settledAtUtc: Now.AddHours(-1), settledBatches: 1, note: "Rattle from the back.");
        var photo = Item(PhotoCaptureItemKind.Overview, ordinal: 0, intakeReceiptId: Guid.NewGuid());
        var paperwork = Item(PhotoCaptureItemKind.Paperwork, ordinal: 1, intakeReceiptId: Guid.NewGuid()) with { MediaType = "application/pdf" };
        harness.Store.Add(capture, photo, paperwork);
        var reachedCaseId = Guid.NewGuid();
        harness.Store.Reaching.Add((capture.Id, reachedCaseId));

        var result = await harness.Settle().ExecuteAsync(50, CancellationToken.None);
        var again = await harness.Settle(Now.AddMinutes(5)).ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, result.ReachedCase);
        Assert.Equal(0, again.ReachedCase);
        Assert.Empty(harness.CaseWriter.ImagesCompleted);
        var recorded = Assert.Single(harness.Store.CaseRecords, record => record.Changed);
        Assert.Equal((reachedCaseId, (string?)null, (DateTimeOffset?)Now), (recorded.CaseId, recorded.Reason, recorded.ImagesCompletedAtUtc));
        Assert.Contains("Rattle from the back.", Assert.Single(harness.Notes.Written).Note);
        Assert.Equal(paperwork.IntakeReceiptId, Assert.Single(harness.Mutations.Links).ReceiptId);
        Assert.Equal(paperwork.StagedReceiptId, Assert.Single(harness.Processor.Redriven));
        Assert.Equal((StaffNotificationCause.PhotosReceived, reachedCaseId), Assert.Single(harness.Notifier.Raised));
    }

    /// <summary>A cancelled link's photos still settle as normal (R794).</summary>
    [Fact]
    public async Task ACancelledCaptureWithPhotosSettles()
    {
        var harness = new Harness();
        harness.Store.Add(
            Capture(revokedAtUtc: Now.AddMinutes(-1), note: "Sent before it was cancelled."),
            OnCase(PhotoCaptureItemKind.Overview, 0));

        var result = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, result.Settled);
        Assert.Single(harness.CaseWriter.ImagesCompleted);
        Assert.Single(harness.Notes.Written);
        Assert.Single(harness.Notifier.Raised);
    }

    /// <summary>An archived Case: the batch closes and nothing is written to the Case (R793).</summary>
    [Fact]
    public async Task AnArchivedCaseClosesTheBatchWithNoWriterCalls()
    {
        var harness = new Harness();
        harness.Store.CaseArchived = true;
        harness.Store.Add(
            Capture(sentAtUtc: Now, note: "Too late.", mileage: 48_210),
            OnCase(PhotoCaptureItemKind.Overview, 0),
            Item(PhotoCaptureItemKind.Damage, 1, "fd", PhotoCaptureStage.Overview, ordinal: 1, intakeReceiptId: Guid.NewGuid()));

        var result = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, result.Settled);
        Assert.Single(harness.Store.Closes);
        Assert.Empty(harness.CaseWriter.ImagesCompleted);
        Assert.Empty(harness.CaseWriter.Filled);
        Assert.Empty(harness.Notes.Written);
        Assert.Empty(harness.Mutations.Links);
        Assert.Empty(harness.Notifier.Raised);
    }

    private static PhotoCaptureItem OnCase(PhotoCaptureItemKind kind, int ordinal, int batch = 1, Guid? intakeReceiptId = null) =>
        Item(kind, ordinal: ordinal, batch: batch, intakeReceiptId: intakeReceiptId ?? Guid.NewGuid()) with { DeclaredCaseId = CaseId };

    private static Core.PhotoCapture.PhotoCapture Standing(
        DateTimeOffset? sentAtUtc = null,
        DateTimeOffset? settledAtUtc = null,
        int settledBatches = 0,
        string? note = null,
        DateOnly? incidentDate = null) =>
        Capture(
            origin: PhotoCaptureOrigin.StandingStaff,
            destination: PhotoCaptureDestination.VehicleImages,
            sentAtUtc: sentAtUtc,
            settledAtUtc: settledAtUtc,
            settledBatches: settledBatches,
            note: note,
            incidentDate: incidentDate);

    private static ImageIntakeCaseCandidate Candidate(Guid principalId, DateOnly? incidentDate = null) =>
        new(Guid.NewGuid(), "QDOS26044", CaseVersion: 2, "RY65OFG", Now.AddDays(-2), principalId, IncidentDate: incidentDate);

    private sealed class Harness
    {
        public List<string> Events { get; } = [];
        public CaptureStore Store { get; }
        public FakeCandidates Candidates { get; } = new();
        public FakeMutations Mutations { get; }
        public FakeProcessor Processor { get; }
        public FakeCaseWriter CaseWriter { get; }
        public RecordingNotes Notes { get; }
        public FakeNotifier Notifier { get; }

        public Harness()
        {
            Store = new(Events);
            Mutations = new(Events);
            Processor = new(Events);
            CaseWriter = new(Events, Store);
            Notes = new(Events);
            Notifier = new(Events);
        }

        public SettlePhotoCaptures Settle(DateTimeOffset? now = null)
        {
            var clock = new FixedClock(now ?? Now);
            var standingLinks = AutomaticImageIntakeTests.Forwarder.For<IStandingPhotoLinkStore>(null);
            standingLinks.Overrides[nameof(IStandingPhotoLinkStore.GetAsync)] = args =>
                Task.FromResult<StandingPhotoLink?>(new StandingPhotoLink(
                    (Guid)args[0]!, StandingPhotoLinkKind.Contact, Guid.NewGuid(), "Northside Bodyshop", PrincipalId,
                    IsRepairer: true, ContactActive: true, "northside", PhotoCaptureMode.Quick, LiveCameraOnly: false,
                    LinkLifetimeDays: 7, OriginalsInBackground: true, PausedAtUtc: null, RetiredAtUtc: null, Now.AddDays(-30), Version: 1));
            return new(
                Store.Proxy,
                (IStandingPhotoLinkStore)standingLinks.Proxy,
                new RoutePhotoCapture(Candidates),
                Mutations,
                Processor,
                CaseWriter,
                new AddCaseNote(Notes, clock),
                Notifier,
                clock);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>
    /// The capture store's settle reads and writes, over captures held in
    /// memory; any other call is not expected. Its listing pages by
    /// (created, id) and lists every capture, so the settle's own checks are
    /// what keep a capture from settling.
    /// </summary>
    private sealed class CaptureStore
    {
        private readonly AutomaticImageIntakeTests.Forwarder forwarder =
            AutomaticImageIntakeTests.Forwarder.For<IPhotoCaptureStore>(null);

        public CaptureStore(List<string> events)
        {
            forwarder.Overrides[nameof(IPhotoCaptureStore.ListAwaitingSettleAsync)] = args =>
            {
                var after = (PhotoCaptureSettleCursor?)args[1];
                Listings.Add(after);
                return Task.FromResult<IReadOnlyList<PhotoCaptureSettleCandidate>>(Captures.Values
                    .OrderBy(capture => capture.CreatedAtUtc)
                    .ThenBy(capture => capture.Id)
                    .Where(capture => after is null
                        || capture.CreatedAtUtc > after.CreatedAtUtc
                        || (capture.CreatedAtUtc == after.CreatedAtUtc && capture.Id.CompareTo(after.Id) > 0))
                    .Take((int)args[2]!)
                    .Select(capture => new PhotoCaptureSettleCandidate(
                        capture, Items[capture.Id], capture.CaseId, CaseArchived, CaseIsTriage: false))
                    .ToArray());
            };
            forwarder.Overrides[nameof(IPhotoCaptureStore.ListSettledReachingCaseAsync)] = _ =>
                Task.FromResult<IReadOnlyList<PhotoCaptureSettleCandidate>>(Reaching
                    .Where(reached => Captures[reached.CaptureId].CaseId is null)
                    .Select(reached => new PhotoCaptureSettleCandidate(
                        Captures[reached.CaptureId], Items[reached.CaptureId], reached.CaseId, CaseArchived, CaseIsTriage: false))
                    .ToArray());
            forwarder.Overrides[nameof(IPhotoCaptureStore.AllItemsSettledAsync)] = args =>
            {
                var captureId = (Guid)args[0]!;
                var batch = (int)args[1]!;
                var caseId = (Guid?)args[2];
                SettledAsked.Add((captureId, batch, caseId));
                return Task.FromResult(Settled(captureId, batch, caseId));
            };
            forwarder.Overrides[nameof(IPhotoCaptureStore.CloseBatchAsync)] = args =>
            {
                var captureId = (Guid)args[0]!;
                var batch = (int)args[1]!;
                var group = (PhotoCaptureDeclaredGroup?)args[3];
                BeforeClose?.Invoke(captureId);
                Closes.Add((captureId, batch, group));
                events.Add("close");
                var capture = Captures[captureId];
                if (capture.SettledBatches >= batch)
                {
                    return Task.FromResult(new PhotoCaptureBatchClosed(PhotoCaptureBatchClose.AlreadyClosed, capture, []));
                }

                capture = capture with
                {
                    SettledBatches = batch,
                    SettledAtUtc = (DateTimeOffset)args[2]!,
                    SubmissionGroupId = group is null ? capture.SubmissionGroupId : PhotoCaptureIntakeRules.BatchGroupId(captureId, batch),
                    Version = capture.Version + 1
                };
                Captures[captureId] = capture;
                var received = Items[captureId].Where(item => item.Batch == batch && item.StagedReceiptId is not null).ToArray();
                return Task.FromResult(new PhotoCaptureBatchClosed(PhotoCaptureBatchClose.Closed, capture, received));
            };
            forwarder.Overrides[nameof(IPhotoCaptureStore.RecordCaseAsync)] = args =>
            {
                var captureId = (Guid)args[0]!;
                var caseId = (Guid)args[1]!;
                var reason = (string?)args[2];
                var imagesCompletedAtUtc = (DateTimeOffset?)args[3];
                var capture = Captures[captureId];
                var changed = capture.CaseId is null;
                CaseRecords.Add((captureId, caseId, reason, imagesCompletedAtUtc, changed));
                if (changed)
                {
                    capture = capture with
                    {
                        CaseId = caseId,
                        CaseReference = "QDOS26044",
                        Destination = PhotoCaptureDestination.Case,
                        RoutingReason = reason ?? capture.RoutingReason,
                        ImagesCompletedAtUtc = capture.ImagesCompletedAtUtc ?? imagesCompletedAtUtc,
                        Version = capture.Version + 1
                    };
                    Captures[captureId] = capture;
                }

                return Task.FromResult(new PhotoCaptureCaseRecorded(capture, changed));
            };
        }

        public IPhotoCaptureStore Proxy => (IPhotoCaptureStore)forwarder.Proxy;

        public Dictionary<Guid, Core.PhotoCapture.PhotoCapture> Captures { get; } = [];

        public Dictionary<Guid, List<PhotoCaptureItem>> Items { get; } = [];

        public bool CaseArchived { get; set; }

        public List<(Guid CaptureId, Guid CaseId)> Reaching { get; } = [];

        /// <summary>What <see cref="IPhotoCaptureStore.AllItemsSettledAsync"/> answers, by capture, batch and Case.</summary>
        public Func<Guid, int, Guid?, bool> Settled { get; set; } = (_, _, _) => true;

        public Action<Guid>? BeforeClose { get; set; }

        public List<PhotoCaptureSettleCursor?> Listings { get; } = [];

        public List<(Guid CaptureId, int Batch, Guid? CaseId)> SettledAsked { get; } = [];

        public List<(Guid CaptureId, int Batch, PhotoCaptureDeclaredGroup? Group)> Closes { get; } = [];

        public List<(Guid CaptureId, Guid CaseId, string? Reason, DateTimeOffset? ImagesCompletedAtUtc, bool Changed)> CaseRecords { get; } = [];

        public void Add(Core.PhotoCapture.PhotoCapture capture, params PhotoCaptureItem[] items)
        {
            Captures[capture.Id] = capture;
            Items[capture.Id] = items.Select(item => item with { CaptureId = capture.Id }).ToList();
        }

        /// <summary>The sender adds an item to the open batch and presses Send again.</summary>
        public void SendAgain(Guid captureId, DateTimeOffset sentAtUtc, string? note, PhotoCaptureItem item)
        {
            Captures[captureId] = Captures[captureId] with { SentAtUtc = sentAtUtc, Note = note };
            Items[captureId].Add(item with { CaptureId = captureId });
        }
    }

    private sealed class FakeCandidates : IImageIntakeCaseCandidates
    {
        public List<ImageIntakeCaseCandidate> Cases { get; } = [];

        public Task<IReadOnlyList<ImageIntakeCaseCandidate>> FindEligibleByRegistrationAsync(
            string normalizedVehicleRegistration,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageIntakeCaseCandidate>>(Cases.ToArray());
    }

    /// <summary>Records each declared-destination link; a repeated operation key replays.</summary>
    private sealed class FakeMutations(List<string> events) : IIntakeMutationStore
    {
        private readonly HashSet<string> keys = [];

        public List<DeclaredDestinationLinkRequest> Links { get; } = [];

        public Task<DeclaredDestinationLinkOutcome> LinkDeclaredDestinationAsync(
            DeclaredDestinationLinkRequest request,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken)
        {
            if (!keys.Add(request.OperationKey))
            {
                return Task.FromResult(DeclaredDestinationLinkOutcome.AlreadyLinked);
            }

            Links.Add(request);
            events.Add($"link:{request.ReceiptId}");
            return Task.FromResult(DeclaredDestinationLinkOutcome.Linked);
        }

        public Task<IntakeReceipt> ResolveAsync(ResolveIntakeRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IntakeReceipt> ScheduleReevaluationAsync(ReevaluateIntakeRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IntakeReceipt> ScheduleOcrRetryAsync(RetryIntakeOcrRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task LinkAsync(LinkIntakeRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ReverseLinkAsync(ReverseIntakeLinkRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<long> AutoLinkAsync(AutomaticIntakeLinkRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeProcessor(List<string> events) : IProcessQueuedIntake
    {
        public List<Guid> Redriven { get; } = [];

        public Task<QueuedIntakeProcessingResult> ProcessAsync(Guid stagedReceiptId, CancellationToken cancellationToken = default)
        {
            Redriven.Add(stagedReceiptId);
            events.Add($"redrive:{stagedReceiptId}");
            return Task.FromResult(new QueuedIntakeProcessingResult(QueuedIntakeProcessingOutcome.Completed));
        }
    }

    /// <summary>The Case-side writer; completing the images records the capture's marker, as the store's transaction does.</summary>
    private sealed class FakeCaseWriter(List<string> events, CaptureStore store) : IPhotoCaptureCaseWriter
    {
        public List<(Guid CaseId, Guid CaptureId)> ImagesCompleted { get; } = [];
        public List<IReadOnlyDictionary<string, string>> Filled { get; } = [];
        public int FailImages { get; set; }

        public Task CompleteImagesAsync(Guid caseId, Guid captureId, string operationKey, string reason, CancellationToken cancellationToken)
        {
            if (FailImages > 0)
            {
                FailImages--;
                throw new InvalidOperationException("The Case could not be written just now.");
            }

            ImagesCompleted.Add((caseId, captureId));
            events.Add("images");
            store.Captures[captureId] = store.Captures[captureId] with { ImagesCompletedAtUtc = Now };
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> FillEmptyFieldsAsync(Guid caseId, IReadOnlyDictionary<string, string> values, Guid captureId, string sourceLabel, CancellationToken cancellationToken)
        {
            Filled.Add(values);
            events.Add("fill");
            return Task.FromResult<IReadOnlyList<string>>(values.Keys.ToArray());
        }

        public Task<Guid?> AssignedEngineerAsync(Guid caseId, CancellationToken cancellationToken) => Task.FromResult<Guid?>(Guid.NewGuid());
    }

    /// <summary>The Case's Notes: a repeated operation key replays, as the store's does.</summary>
    private sealed class RecordingNotes(List<string> events) : ICaseNoteStore
    {
        public List<AddCaseNoteRequest> Written { get; } = [];

        public Task AddAsync(AddCaseNoteRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken)
        {
            if (Written.TrueForAll(note => note.OperationKey != request.OperationKey))
            {
                Written.Add(request);
                events.Add("note");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeNotifier(List<string> events) : ICaseStaffNotifier
    {
        public List<(StaffNotificationCause Cause, Guid CaseId)> Raised { get; } = [];

        public Task<StaffNotification?> NotifyAsync(StaffNotificationCause cause, Guid caseId, ActionActor? actor, string? section, string? registration, CancellationToken cancellationToken)
        {
            Raised.Add((cause, caseId));
            events.Add("bell");
            return Task.FromResult<StaffNotification?>(null);
        }

        public Task<StaffNotification?> NotifyAsync(StaffNotificationCause cause, Guid caseId, string reference, Guid? assignedEngineerId, ActionActor? actor, string? section, string? registration, CancellationToken cancellationToken)
        {
            Raised.Add((cause, caseId));
            events.Add("bell");
            return Task.FromResult<StaffNotification?>(null);
        }

        public Task<StaffNotification?> NotifyMailArrivalAsync(Guid caseId, Guid intakeReceiptId, ActionActor? actor, CancellationToken cancellationToken) => Task.FromResult<StaffNotification?>(null);
        public Task<StaffNotification?> NotifyCancellationReceivedAsync(Guid caseId, ActionActor? actor, CancellationToken cancellationToken) => Task.FromResult<StaffNotification?>(null);
        public Task<StaffNotification?> NotifyAiDraftReadyAsync(AiJobRecord job, CancellationToken cancellationToken) => Task.FromResult<StaffNotification?>(null);
    }
}
