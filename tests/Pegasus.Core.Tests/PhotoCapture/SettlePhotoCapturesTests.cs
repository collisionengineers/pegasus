using Pegasus.Core.AiWork;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Notifications;
using Pegasus.Core.PhotoCapture;
using Pegasus.Core.Tests.ImageIntake;
using Pegasus.Core.Triage;
using static Pegasus.Core.Tests.PhotoCapture.PhotoCaptureTestData;

namespace Pegasus.Core.Tests.PhotoCapture;

/// <summary>
/// The settle (FRD-28; operator, 9 November 2026, R791 to R794, R803,
/// R804): a Case batch waits until its photos have filed or ended for good,
/// writes on the Case only when a photo filed, completes the images once per
/// capture, writes the note once per wording, closes with its bell owed and
/// rings it after; a Vehicle images batch is matched again with the link's
/// Principal and the typed date; each batch whose record reaches a Case
/// finishes there with its own kept files.
/// </summary>
public sealed class SettlePhotoCapturesTests
{
    private static readonly Guid CaseId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid PrincipalId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid StaffId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly DateOnly TypedDate = new(2026, 10, 28);
    private static readonly string[] FilledFields = ["claimant_contact_number", "claimant_name", "incident_date", "vehicle_mileage"];
    private static readonly string[] CaseBatchOrder = ["images", "note", "fill", "close", "bell"];
    private static readonly string[] IdleRunReads =
    [
        nameof(IPhotoCaptureStore.ListBellsOwedAsync),
        nameof(IPhotoCaptureStore.ListBatchesReachingCaseAsync),
        nameof(IPhotoCaptureStore.ListAwaitingSettleAsync)
    ];
    private static readonly string[] NoteThenChangedNote =
    [
        "Customer's note (photo link): Rattle from the back.",
        "Customer's note (photo link): Rattle from the back, and the boot leaks."
    ];

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
        harness.Store.AddFiled(capture, CaseId, OnCase(PhotoCaptureItemKind.Overview, 0), OnCase(PhotoCaptureItemKind.NumberPlate, 1));

        var result = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, result.Settled);
        Assert.Equal(0, result.Failures);
        var close = Assert.Single(harness.Store.Closes);
        Assert.Null(close.Request.Group);
        Assert.True(close.Request.BellOwed);
        var images = Assert.Single(harness.CaseWriter.ImagesCompleted);
        Assert.Equal((CaseId, capture.Id), (images.CaseId, images.CaptureId));
        var note = Assert.Single(harness.Notes.Written);
        Assert.Equal("Customer's note (photo link): The boot doesn't close.", note.Note);
        Assert.Equal(ActorKind.SystemWorker, note.Actor.Kind);
        Assert.Equal(FilledFields, harness.CaseWriter.Filled.Single().Keys.Order());
        Assert.Equal((StaffNotificationCause.PhotosReceived, CaseId), Assert.Single(harness.Notifier.Raised));
        Assert.Empty(harness.Store.BellsOwed);
        Assert.Empty(harness.Mutations.Links);
        Assert.Empty(harness.Processor.Redriven);
        // The Case-side work runs before the close commits the batch; the bell after it.
        Assert.Equal(CaseBatchOrder, harness.Events);
    }

    [Fact]
    public async Task ACaseBatchWaitsUntilEveryPhotoHasFiledNotOnlyBeenEvaluated()
    {
        var harness = new Harness();
        var photo = OnCase(PhotoCaptureItemKind.Overview, 0);
        harness.Store.Add(Capture(sentAtUtc: Now), photo);
        // Linked and its filing under way, not yet filed.
        harness.Store.Facts(photo).Link = new PhotoCaptureItemLink(CaseId, true, BySystem: false, Now.AddMinutes(-5));
        harness.Store.Facts(photo).StartedOn = CaseId;

        var waiting = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(0, waiting.Settled);
        Assert.Empty(harness.Store.Closes);
        Assert.Empty(harness.CaseWriter.ImagesCompleted);
        Assert.Empty(harness.Notifier.Raised);
        // A filing under way is not re-driven: its custody owns it.
        Assert.Empty(harness.Processor.Redriven);

        harness.Store.Facts(photo).FiledOn = CaseId;
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
        harness.Store.AddFiled(Capture(sentAtUtc: Now, note: "Only the paperwork."), CaseId, paperwork, lost);

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
        harness.Store.AddFiled(capture, CaseId, OnCase(PhotoCaptureItemKind.Overview, 0));
        await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        harness.Store.SendAgain(capture.Id, Now.AddMinutes(1), note: null, CaseId, OnCase(PhotoCaptureItemKind.Dashboard, 0, batch: 2));
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
        harness.Store.AddFiled(capture, CaseId, OnCase(PhotoCaptureItemKind.Overview, 0));
        await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        harness.Store.SendAgain(capture.Id, Now.AddMinutes(1), "Rattle from the back.", CaseId, OnCase(PhotoCaptureItemKind.Dashboard, 0, batch: 2));
        await harness.Settle(Now.AddMinutes(2)).ExecuteAsync(50, CancellationToken.None);

        Assert.Single(harness.Notes.Written);

        harness.Store.SendAgain(capture.Id, Now.AddMinutes(3), "Rattle from the back, and the boot leaks.", CaseId, OnCase(PhotoCaptureItemKind.Vin, 0, batch: 3));
        await harness.Settle(Now.AddMinutes(4)).ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(2, harness.Notes.Written.Count);
        Assert.Equal("Customer's note (photo link): Rattle from the back, and the boot leaks.", harness.Notes.Written[^1].Note);
        Assert.NotEqual(harness.Notes.Written[0].OperationKey, harness.Notes.Written[1].OperationKey);
    }

    /// <summary>
    /// A note changed after the settle read the capture, while the batch was
    /// settling, is not lost under the close: the close waits, and the next
    /// run writes the changed note and then closes (R792).
    /// </summary>
    [Fact]
    public async Task ANoteChangedBetweenTheListingAndTheCloseIsWritten()
    {
        var harness = new Harness();
        var capture = Capture(sentAtUtc: Now.AddMinutes(-2), note: "Rattle from the back.");
        harness.Store.AddFiled(capture, CaseId, OnCase(PhotoCaptureItemKind.Overview, 0));
        harness.Store.BeforeClose = id =>
        {
            harness.Store.Captures[id] = harness.Store.Captures[id] with
            {
                Note = "Rattle from the back, and the boot leaks.",
                SentAtUtc = Now.AddSeconds(-1)
            };
            harness.Store.BeforeClose = null;
        };

        var first = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(0, first.Settled);
        Assert.Equal(PhotoCaptureBatchClose.Waiting, Assert.Single(harness.Store.Closes).Outcome);
        Assert.Empty(harness.Notifier.Raised);

        var second = await harness.Settle(Now.AddMinutes(1)).ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, second.Settled);
        Assert.Equal(NoteThenChangedNote, harness.Notes.Written.Select(note => note.Note));
        Assert.Single(harness.CaseWriter.ImagesCompleted);
        Assert.Single(harness.Notifier.Raised);
    }

    [Fact]
    public async Task TheBellRingsOncePerClosedBatch()
    {
        var harness = new Harness();
        harness.Store.AddFiled(Capture(sentAtUtc: Now), CaseId, OnCase(PhotoCaptureItemKind.Overview, 0), OnCase(PhotoCaptureItemKind.Damage, 1));

        await harness.Settle().ExecuteAsync(50, CancellationToken.None);
        var again = await harness.Settle(Now.AddMinutes(5)).ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(0, again.Settled);
        Assert.Single(harness.Store.Closes);
        Assert.Single(harness.Notifier.Raised);
    }

    /// <summary>
    /// The bell is owed in the close's own write and cleared only once it
    /// has rung: a failure between the close and the ring, or a run stopped
    /// there, leaves it owed, and the next run rings it once.
    /// </summary>
    [Fact]
    public async Task ABellOwedIsRungAfterAFailureBetweenTheCloseAndTheRing()
    {
        var harness = new Harness();
        harness.Store.AddFiled(Capture(sentAtUtc: Now), CaseId, OnCase(PhotoCaptureItemKind.Overview, 0));
        harness.Notifier.FailNext = 1;

        var failed = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, failed.Settled);
        Assert.Equal(1, failed.Failures);
        Assert.Empty(harness.Notifier.Raised);
        Assert.Single(harness.Store.BellsOwed);

        var next = await harness.Settle(Now.AddMinutes(5)).ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(0, next.Failures);
        Assert.Equal((StaffNotificationCause.PhotosReceived, CaseId), Assert.Single(harness.Notifier.Raised));
        Assert.Empty(harness.Store.BellsOwed);
        Assert.Single(harness.Store.Closes);
    }

    [Fact]
    public async Task AFailedCaseSideStepRetriesOnTheNextRunWithOneBell()
    {
        var harness = new Harness();
        harness.Store.AddFiled(Capture(sentAtUtc: Now), CaseId, OnCase(PhotoCaptureItemKind.Overview, 0));
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
        harness.Store.AddFiled(capture, CaseId, OnCase(PhotoCaptureItemKind.Overview, 0));
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

    /// <summary>
    /// A batch with no photograph filed on its Case, its photos failed or
    /// moved to another Case, closes with nothing written and spends none of
    /// the capture's one images-complete (R792).
    /// </summary>
    [Fact]
    public async Task ABatchWithNoFiledPhotographWritesNothing()
    {
        var harness = new Harness();
        var moved = OnCase(PhotoCaptureItemKind.Overview, 0);
        var failed = OnCase(PhotoCaptureItemKind.Damage, 1) with { IntakeReceiptId = null };
        harness.Store.Add(Capture(sentAtUtc: Now, note: "Nothing reached you.", mileage: 48_210), moved, failed);
        harness.Store.Facts(moved).Link = new PhotoCaptureItemLink(Guid.NewGuid(), true, BySystem: false, Now.AddMinutes(-5));
        harness.Store.Facts(failed).WorkFailed = true;

        var result = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, result.Settled);
        var close = Assert.Single(harness.Store.Closes);
        Assert.Equal(PhotoCaptureBatchClose.Closed, close.Outcome);
        Assert.False(close.Request.BellOwed);
        Assert.Empty(harness.CaseWriter.ImagesCompleted);
        Assert.Empty(harness.CaseWriter.Filled);
        Assert.Empty(harness.Notes.Written);
        Assert.Empty(harness.Notifier.Raised);
        Assert.Null(harness.Store.Captures[Capture().Id].ImagesCompletedAtUtc);
    }

    /// <summary>
    /// A photo whose filing ended for good without filing (its replay
    /// exhausted after its filing started, or Pegasus's link three hours old
    /// with its filing never started) no longer holds the batch, which
    /// settles on the photos that did file: the operator's rule
    /// (operator, 10 November 2026; R840).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task APhotoWhoseFilingEndedDoesNotHoldTheBatch(bool exhausted)
    {
        var harness = new Harness();
        var filed = OnCase(PhotoCaptureItemKind.Overview, 0);
        var ended = OnCase(PhotoCaptureItemKind.Damage, 1);
        harness.Store.AddFiled(Capture(sentAtUtc: Now), CaseId, filed);
        harness.Store.AddItems(Capture().Id, ended);
        harness.Store.Facts(ended).Link = new PhotoCaptureItemLink(
            CaseId, true, BySystem: !exhausted, exhausted ? Now.AddMinutes(-30) : Now.AddHours(-3));
        harness.Store.Facts(ended).StartedOn = exhausted ? CaseId : null;
        harness.Store.Facts(ended).ExhaustedAtUtc = exhausted ? Now.AddMinutes(-1) : null;

        var result = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, result.Settled);
        Assert.Single(harness.CaseWriter.ImagesCompleted);
        Assert.Single(harness.Notifier.Raised);
        Assert.Empty(harness.Processor.Redriven);
    }

    /// <summary>
    /// A filing that has started can still be confirmed by the pending
    /// custody, which has no window: Pegasus's link three hours old does not
    /// end it, nor does its work failing after it started, so the batch waits
    /// with nothing written on the Case.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AStartedFilingStillHoldsTheBatchPastTheReplayWindow(bool workFailed)
    {
        var harness = new Harness();
        var filed = OnCase(PhotoCaptureItemKind.Overview, 0);
        var pending = OnCase(PhotoCaptureItemKind.Damage, 1);
        harness.Store.AddFiled(Capture(sentAtUtc: Now), CaseId, filed);
        harness.Store.AddItems(Capture().Id, pending);
        harness.Store.Facts(pending).Link = new PhotoCaptureItemLink(CaseId, true, BySystem: true, Now.AddHours(-3));
        harness.Store.Facts(pending).StartedOn = CaseId;
        harness.Store.Facts(pending).WorkFailed = workFailed;

        var result = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(0, result.Settled);
        Assert.Empty(harness.Store.Closes);
        Assert.Empty(harness.CaseWriter.ImagesCompleted);
        Assert.Empty(harness.Notes.Written);
        Assert.Empty(harness.Notifier.Raised);
        Assert.Empty(harness.Processor.Redriven);
    }

    /// <summary>
    /// A receipt linked to the Case whose filing never started is re-driven,
    /// whether the settle's own link answered Linked or, replayed, AlreadyLinked,
    /// so the pipeline finishes what it had not.
    /// </summary>
    [Theory]
    [InlineData(DeclaredDestinationLinkOutcome.Linked)]
    [InlineData(DeclaredDestinationLinkOutcome.AlreadyLinked)]
    public async Task ALinkedReceiptWhoseFilingNeverStartedIsRedriven(DeclaredDestinationLinkOutcome outcome)
    {
        var harness = new Harness();
        var unlinked = OnCase(PhotoCaptureItemKind.Overview, 0);
        var linked = OnCase(PhotoCaptureItemKind.Damage, 1);
        harness.Store.Add(Capture(sentAtUtc: Now), unlinked, linked);
        harness.Store.Facts(linked).Link = new PhotoCaptureItemLink(CaseId, true, BySystem: false, Now.AddMinutes(-5));
        harness.Mutations.Outcome = outcome;

        await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(unlinked.IntakeReceiptId, Assert.Single(harness.Mutations.Links).ReceiptId);
        Assert.Equal(
            [unlinked.StagedReceiptId!.Value, linked.StagedReceiptId!.Value],
            harness.Processor.Redriven);
        Assert.Empty(harness.Store.Closes);
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
            var photo = OnCase(PhotoCaptureItemKind.Overview, 0);
            harness.Store.Add(queued, photo);
            harness.Store.Facts(photo).Link = new PhotoCaptureItemLink(CaseId, true, BySystem: false, Now.AddMinutes(-5));
            harness.Store.Facts(photo).StartedOn = CaseId;
        }

        var ready = Capture(sentAtUtc: Now, createdAtUtc: Now.AddHours(-1)) with { Id = Guid.NewGuid() };
        harness.Store.AddFiled(ready, CaseId, OnCase(PhotoCaptureItemKind.Overview, 0));

        var result = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, result.Settled);
        Assert.Equal(61, result.Examined);
        Assert.Equal(ready.Id, Assert.Single(harness.Store.Closes).CaptureId);
        Assert.Equal(2, harness.Store.Listings.Count);
        Assert.Null(harness.Store.Listings[0]);
        Assert.NotNull(harness.Store.Listings[1]);
        Assert.DoesNotContain(harness.Store.Closes, close => waiting.Contains(close.CaptureId));
    }

    /// <summary>
    /// No Case for the typed registration: the batch closes as one group of
    /// its evaluated photographs, carrying the registration and the date,
    /// before its first member is re-driven to register the record; the
    /// paperwork waits outside the group (R791, R803).
    /// </summary>
    [Fact]
    public async Task AVehicleImagesBatchClosesWithItsGroupBeforeTheFirstMemberIsRedriven()
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
        Assert.Equal(new PhotoCaptureDeclaredGroup("RY65OFG", TypedDate), close.Request.Group);
        Assert.Equal([first.Id, second.Id], close.Members.Select(member => member.Id));
        Assert.Equal(first.StagedReceiptId, Assert.Single(harness.Processor.Redriven));
        Assert.Equal(["close", $"redrive:{first.StagedReceiptId}"], harness.Events);
        Assert.Empty(harness.Store.CaseRecords);
        Assert.Empty(harness.Mutations.Links);
        Assert.Empty(harness.CaseWriter.ImagesCompleted);
        Assert.Empty(harness.Notifier.Raised);
    }

    /// <summary>
    /// A photograph whose work failed and produced no receipt is not a group
    /// member: it could never complete the group, which would never register.
    /// The first evaluated photograph is the one re-driven.
    /// </summary>
    /// <summary>
    /// A photograph whose work failed after its receipt was evaluated has its
    /// receipt, and so is a group member like any other: left out, it would
    /// be filed nowhere.
    /// </summary>
    [Fact]
    public async Task APhotographWhoseWorkFailedAfterItsEvaluationIsAGroupMember()
    {
        var harness = new Harness();
        var failedLate = Item(PhotoCaptureItemKind.Overview, ordinal: 0, intakeReceiptId: Guid.NewGuid());
        var evaluated = Item(PhotoCaptureItemKind.Overview, 2, ordinal: 1, intakeReceiptId: Guid.NewGuid());
        harness.Store.Add(Standing(sentAtUtc: Now, incidentDate: TypedDate), failedLate, evaluated);
        harness.Store.Facts(failedLate).WorkFailed = true;

        await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal([failedLate.Id, evaluated.Id], Assert.Single(harness.Store.Closes).Members.Select(member => member.Id));
        Assert.Equal(failedLate.StagedReceiptId, Assert.Single(harness.Processor.Redriven));
    }

    [Fact]
    public async Task AFailedPhotographIsNotAGroupMember()
    {
        var harness = new Harness();
        var failed = Item(PhotoCaptureItemKind.Overview, ordinal: 0, intakeReceiptId: null);
        var evaluated = Item(PhotoCaptureItemKind.Overview, 2, ordinal: 1, intakeReceiptId: Guid.NewGuid());
        harness.Store.Add(Standing(sentAtUtc: Now, incidentDate: TypedDate), failed, evaluated);
        harness.Store.Facts(failed).WorkFailed = true;

        await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal([evaluated.Id], Assert.Single(harness.Store.Closes).Members.Select(member => member.Id));
        Assert.Equal(evaluated.StagedReceiptId, Assert.Single(harness.Processor.Redriven));
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

        Assert.Equal(new PhotoCaptureDeclaredGroup("RY65OFG", TypedDate), Assert.Single(harness.Store.Closes).Request.Group);
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

        var result = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(0, result.Settled);
        var recorded = Assert.Single(harness.Store.CaseRecords);
        Assert.Equal((matched.CaseId, 1, RoutePhotoCapture.SingleEligibleCase), (recorded.CaseId, recorded.Batch, recorded.Reason));
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

        // Filed: the next run settles it as a Case batch, once, and links nothing again.
        harness.Store.Facts(photo).FiledOn = matched.CaseId;
        harness.Store.Facts(paperwork).FiledOn = matched.CaseId;
        var filed = await harness.Settle(Now.AddMinutes(1)).ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, filed.Settled);
        Assert.Null(Assert.Single(harness.Store.Closes).Request.Group);
        Assert.Equal(2, harness.Mutations.Links.Count);
        Assert.Equal(2, harness.Processor.Redriven.Count);
        Assert.Single(harness.CaseWriter.ImagesCompleted);
        Assert.Equal((StaffNotificationCause.PhotosReceived, matched.CaseId), Assert.Single(harness.Notifier.Raised));
    }

    /// <summary>
    /// A record Pegasus paired that reaches a Case: its own batch's kept
    /// paperwork linked as the work that follows the record's merge, so it
    /// files wherever the photos went (R844), then its note and fill, and
    /// the reach recorded with its bell owed and then rung once (R803,
    /// R804); the merge completed the images, so no images call.
    /// </summary>
    [Fact]
    public async Task TheReachedPathMakesNoImagesCallFilesTheKeptPaperworkAndRingsOnce()
    {
        var harness = new Harness();
        harness.Processor.FilesOnRedrive = true;
        var capture = Standing(sentAtUtc: Now.AddHours(-2), settledAtUtc: Now.AddHours(-1), settledBatches: 1, note: "Rattle from the back.");
        var photo = Item(PhotoCaptureItemKind.Overview, ordinal: 0, intakeReceiptId: Guid.NewGuid());
        var paperwork = Item(PhotoCaptureItemKind.Paperwork, ordinal: 1, intakeReceiptId: Guid.NewGuid()) with { MediaType = "application/pdf" };
        harness.Store.Add(capture, photo, paperwork);
        var reachedCaseId = Guid.NewGuid();
        harness.Store.Reach(capture.Id, batch: 1, reachedCaseId);

        var result = await harness.Settle().ExecuteAsync(50, CancellationToken.None);
        var again = await harness.Settle(Now.AddMinutes(5)).ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, result.ReachedCase);
        Assert.Equal(0, again.ReachedCase);
        Assert.Empty(harness.CaseWriter.ImagesCompleted);
        Assert.Equal(reachedCaseId, Assert.Single(harness.Store.Reached).CaseId);
        Assert.Contains("Rattle from the back.", Assert.Single(harness.Notes.Written).Note);
        var link = Assert.Single(harness.Mutations.Links);
        Assert.Equal(paperwork.IntakeReceiptId, link.ReceiptId);
        Assert.Equal(ActorKind.SystemWorker, link.Decider.Kind);
        Assert.Equal(PhotoCaptureIntakeRules.ReachActorId, link.Decider.SubjectId);
        Assert.Equal(PhotoCaptureIntakeRules.RoutedDestinationReason, link.Reason);
        Assert.Equal(paperwork.StagedReceiptId, Assert.Single(harness.Processor.Redriven));
        Assert.Equal((StaffNotificationCause.PhotosReceived, reachedCaseId), Assert.Single(harness.Notifier.Raised));
    }

    /// <summary>
    /// A record a member of staff took to its Case on the record's page
    /// files its batch's kept files as that member of staff, with the reason
    /// their link carries, so they file on any Case that is not archived.
    /// </summary>
    [Fact]
    public async Task AStaffChosenRecordFilesItsKeptFilesAsThatMemberOfStaff()
    {
        var harness = new Harness();
        var capture = Standing(sentAtUtc: Now.AddHours(-2), settledAtUtc: Now.AddHours(-1), settledBatches: 1);
        var paperwork = Item(PhotoCaptureItemKind.Paperwork, ordinal: 1, intakeReceiptId: Guid.NewGuid()) with { MediaType = "application/pdf" };
        harness.Store.Add(capture, Item(PhotoCaptureItemKind.Overview, ordinal: 0, intakeReceiptId: Guid.NewGuid()), paperwork);
        harness.Store.Reach(capture.Id, batch: 1, CaseId, StaffId, "Staff confirmed these photos belong to the instruction Case.");

        await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        var link = Assert.Single(harness.Mutations.Links);
        Assert.Equal(ActorKind.Staff, link.Decider.Kind);
        Assert.Equal(StaffId.ToString("D"), link.Decider.SubjectId);
        Assert.Equal("Staff confirmed these photos belong to the instruction Case.", link.Reason);
    }

    /// <summary>
    /// Kept files follow their own batch's record: a record reaching a Case
    /// files the paperwork of its batch, never another batch's, and waits
    /// while its own paperwork has no receipt yet.
    /// </summary>
    [Fact]
    public async Task KeptFilesFollowTheirOwnBatchsRecord()
    {
        var harness = new Harness();
        harness.Processor.FilesOnRedrive = true;
        var capture = Standing(sentAtUtc: Now.AddHours(-2), settledAtUtc: Now.AddHours(-1), settledBatches: 2);
        var firstBatchPaperwork = Item(PhotoCaptureItemKind.Paperwork, ordinal: 1, intakeReceiptId: null) with { MediaType = "application/pdf" };
        var secondBatchPaperwork = Item(PhotoCaptureItemKind.Paperwork, ordinal: 1, batch: 2, intakeReceiptId: Guid.NewGuid()) with { MediaType = "application/pdf" };
        harness.Store.Add(
            capture,
            Item(PhotoCaptureItemKind.Overview, ordinal: 0, intakeReceiptId: Guid.NewGuid()),
            firstBatchPaperwork,
            Item(PhotoCaptureItemKind.Overview, 2, ordinal: 0, batch: 2, intakeReceiptId: Guid.NewGuid()),
            secondBatchPaperwork);
        harness.Store.Reach(capture.Id, batch: 1, CaseId);

        var waiting = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(0, waiting.ReachedCase);
        Assert.Empty(harness.Store.Reached);
        Assert.Empty(harness.Mutations.Links);

        harness.Store.Evaluate(firstBatchPaperwork, Guid.NewGuid());
        var reached = await harness.Settle(Now.AddMinutes(5)).ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, reached.ReachedCase);
        Assert.Equal(
            harness.Store.Item(firstBatchPaperwork.Id).IntakeReceiptId,
            Assert.Single(harness.Mutations.Links).ReceiptId);
        Assert.DoesNotContain(harness.Mutations.Links, link => link.ReceiptId == secondBatchPaperwork.IntakeReceiptId);
    }

    /// <summary>
    /// A kept file holds the reach as a photograph holds its batch (R844):
    /// linked and re-driven, it is waited for until it has filed, and only
    /// then are the note and the bell written.
    /// </summary>
    [Fact]
    public async Task AReachWaitsUntilItsKeptPaperworkHasFiled()
    {
        var harness = new Harness();
        var capture = Standing(sentAtUtc: Now.AddHours(-2), settledAtUtc: Now.AddHours(-1), settledBatches: 1, note: "Rattle from the back.");
        var paperwork = Item(PhotoCaptureItemKind.Paperwork, ordinal: 1, intakeReceiptId: Guid.NewGuid()) with { MediaType = "application/pdf" };
        harness.Store.Add(capture, Item(PhotoCaptureItemKind.Overview, ordinal: 0, intakeReceiptId: Guid.NewGuid()), paperwork);
        harness.Store.Reach(capture.Id, batch: 1, CaseId);

        var waiting = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(0, waiting.ReachedCase);
        Assert.Single(harness.Mutations.Links);
        Assert.Equal(paperwork.StagedReceiptId, Assert.Single(harness.Processor.Redriven));
        Assert.Empty(harness.Store.Reached);
        Assert.Empty(harness.Notes.Written);
        Assert.Empty(harness.Notifier.Raised);

        harness.Store.FileLinked(paperwork.StagedReceiptId!.Value);
        var reached = await harness.Settle(Now.AddMinutes(1)).ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, reached.ReachedCase);
        Assert.Single(harness.Notes.Written);
        Assert.Equal((StaffNotificationCause.PhotosReceived, CaseId), Assert.Single(harness.Notifier.Raised));
    }

    /// <summary>
    /// Reaching a Case is tracked per batch: an earlier batch's record that
    /// reaches a different Case after the capture already had one still gets
    /// its note, fill, kept files and bell there.
    /// </summary>
    [Fact]
    public async Task AnEarlierBatchsRecordReachingADifferentCaseLaterFinishesThere()
    {
        var harness = new Harness();
        harness.Processor.FilesOnRedrive = true;
        var capture = Standing(sentAtUtc: Now.AddHours(-2), settledAtUtc: Now.AddHours(-1), settledBatches: 2, note: "Rattle from the back.") with
        {
            CaseId = CaseId,
            Destination = PhotoCaptureDestination.Case
        };
        var paperwork = Item(PhotoCaptureItemKind.Paperwork, ordinal: 1, intakeReceiptId: Guid.NewGuid()) with { MediaType = "application/pdf" };
        harness.Store.Add(capture, Item(PhotoCaptureItemKind.Overview, ordinal: 0, intakeReceiptId: Guid.NewGuid()), paperwork);
        var otherCaseId = Guid.NewGuid();
        harness.Store.Reach(capture.Id, batch: 1, otherCaseId);

        var result = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, result.ReachedCase);
        Assert.Equal(otherCaseId, Assert.Single(harness.Notes.Written).CaseId);
        Assert.Equal(otherCaseId, Assert.Single(harness.CaseWriter.FilledOn));
        Assert.Equal(otherCaseId, Assert.Single(harness.Mutations.Links).CaseId);
        Assert.Equal((StaffNotificationCause.PhotosReceived, otherCaseId), Assert.Single(harness.Notifier.Raised));
        Assert.Equal(CaseId, harness.Store.Captures[capture.Id].CaseId);
    }

    /// <summary>
    /// A cancelled link's photos still settle as normal (R794), and the
    /// Case history's reason says so (operator, 10 November 2026; R871).
    /// </summary>
    [Fact]
    public async Task ACancelledCaptureWithPhotosSettles()
    {
        var harness = new Harness();
        harness.Store.AddFiled(
            Capture(revokedAtUtc: Now.AddMinutes(-1), note: "Sent before it was cancelled."),
            CaseId,
            OnCase(PhotoCaptureItemKind.Overview, 0));

        var result = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, result.Settled);
        Assert.Equal(
            "The photo link's photos are in: the sender pressed Send, or the link ran out or was cancelled with photos on it.",
            Assert.Single(harness.CaseWriter.ImagesCompleted).Reason);
        Assert.Single(harness.Notes.Written);
        Assert.Single(harness.Notifier.Raised);
    }

    /// <summary>
    /// A Triage Case keeps no Case Notes: the sender's note is written as its
    /// own Triage note, with the words a Case note gets, once; a changed note
    /// is appended (operator, 10 November 2026; R845, R792).
    /// </summary>
    [Fact]
    public async Task ATriageCaseTakesTheNoteAsATriageNoteOnceAndAChangedNoteIsAppended()
    {
        var harness = new Harness();
        harness.Store.CaseIsTriage = true;
        var capture = Capture(sentAtUtc: Now.AddMinutes(-10), note: "Rattle from the back.");
        harness.Store.AddFiled(capture, CaseId, OnCase(PhotoCaptureItemKind.Overview, 0));
        await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        harness.Store.SendAgain(capture.Id, Now.AddMinutes(1), "Rattle from the back.", CaseId, OnCase(PhotoCaptureItemKind.Dashboard, 0, batch: 2));
        await harness.Settle(Now.AddMinutes(2)).ExecuteAsync(50, CancellationToken.None);

        var first = Assert.Single(harness.TriageNotes.Written);
        Assert.Equal((CaseId, ActorKind.SystemWorker), (first.CaseId, first.ActorKind));
        Assert.Equal("Customer's note (photo link): Rattle from the back.", first.Note);
        Assert.Empty(harness.Notes.Written);

        harness.Store.SendAgain(capture.Id, Now.AddMinutes(3), "Rattle from the back, and the boot leaks.", CaseId, OnCase(PhotoCaptureItemKind.Vin, 0, batch: 3));
        await harness.Settle(Now.AddMinutes(4)).ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(NoteThenChangedNote, harness.TriageNotes.Written.Select(note => note.Note));
        Assert.NotEqual(harness.TriageNotes.Written[0].OperationKey, harness.TriageNotes.Written[1].OperationKey);
        Assert.Empty(harness.Notes.Written);
    }

    /// <summary>
    /// A Triage note holds 500 characters, so a longer note is written as
    /// up to four Triage notes, in order, the words starting the first (R845).
    /// </summary>
    [Fact]
    public async Task ATriageCaseTakesALongNoteAsSeveralTriageNotesInOrder()
    {
        var harness = new Harness();
        harness.Store.CaseIsTriage = true;
        var note = string.Concat(Enumerable.Range(0, 120).Select(index => (index % 10).ToString(System.Globalization.CultureInfo.InvariantCulture).PadRight(10, '.')));
        Assert.Equal(1_200, note.Length);
        harness.Store.AddFiled(Capture(sentAtUtc: Now.AddMinutes(-1), note: note), CaseId, OnCase(PhotoCaptureItemKind.Overview, 0));

        await harness.Settle().ExecuteAsync(50, CancellationToken.None);
        await harness.Settle(Now.AddMinutes(1)).ExecuteAsync(50, CancellationToken.None);

        var parts = harness.TriageNotes.Written.Select(written => written.Note).ToArray();
        Assert.Equal(3, parts.Length);
        Assert.All(parts, part => Assert.InRange(part.Length, 1, TriageNotes.MaximumLength));
        Assert.Equal("Customer's note (photo link): " + note, string.Concat(parts));
        Assert.StartsWith("Customer's note (photo link): ", parts[0], StringComparison.Ordinal);
        Assert.Equal(3, harness.TriageNotes.Written.Select(written => written.OperationKey).Distinct().Count());
        Assert.Empty(harness.Notes.Written);
    }

    /// <summary>A completed or cancelled Triage Case takes no note (R845).</summary>
    [Fact]
    public async Task AFinishedTriageCaseTakesNoNote()
    {
        var harness = new Harness();
        harness.Store.CaseIsTriage = true;
        harness.TriageNotes.Finished = true;
        harness.Store.AddFiled(Capture(sentAtUtc: Now.AddMinutes(-1), note: "Rattle from the back."), CaseId, OnCase(PhotoCaptureItemKind.Overview, 0));

        var result = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, result.Settled);
        Assert.Empty(harness.TriageNotes.Written);
        Assert.Empty(harness.Notes.Written);
    }

    /// <summary>
    /// The settle runs every minute (R838): a run that settles and reaches
    /// nothing reads the owed bells once, the reaches and the waiting
    /// captures, and nothing else.
    /// </summary>
    [Fact]
    public async Task AnIdleRunReadsTheOwedBellsOnce()
    {
        var harness = new Harness();
        // Not yet sent: the walk examines it and leaves it.
        harness.Store.Add(Capture(), OnCase(PhotoCaptureItemKind.Overview, 0));

        var result = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal((1, 0, 0, 0), (result.Examined, result.Settled, result.ReachedCase, result.Failures));
        Assert.Equal(IdleRunReads, harness.Store.Calls);
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
        var close = Assert.Single(harness.Store.Closes);
        Assert.Null(close.Request.CaseId);
        Assert.False(close.Request.BellOwed);
        Assert.Empty(harness.CaseWriter.ImagesCompleted);
        Assert.Empty(harness.CaseWriter.Filled);
        Assert.Empty(harness.Notes.Written);
        Assert.Empty(harness.Mutations.Links);
        Assert.Empty(harness.Notifier.Raised);
    }

    private static PhotoCaptureItem OnCase(PhotoCaptureItemKind kind, int ordinal, int batch = 1) =>
        Item(kind, ordinal: ordinal, batch: batch, intakeReceiptId: Guid.NewGuid()) with { DeclaredCaseId = CaseId };

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
        public RecordingTriageNotes TriageNotes { get; }
        public FakeNotifier Notifier { get; }

        public Harness()
        {
            Store = new(Events);
            Mutations = new(Events, Store);
            Processor = new(Events, Store);
            CaseWriter = new(Events, Store);
            Notes = new(Events);
            TriageNotes = new(Events);
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
                new AddTriageNote(TriageNotes.Proxy),
                Notifier,
                clock);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>What a test says of one item's receipt and its filing.</summary>
    private sealed class ItemFacts
    {
        public bool WorkFailed { get; set; }
        public PhotoCaptureItemLink? Link { get; set; }
        public Guid? StartedOn { get; set; }
        public Guid? FiledOn { get; set; }
        public DateTimeOffset? ExhaustedAtUtc { get; set; }
    }

    private sealed class BatchRow
    {
        public Guid? GroupId { get; set; }
        public Guid? CaseId { get; set; }
        public bool Reached { get; set; }
        public bool BellOwed { get; set; }
    }

    /// <summary>
    /// The capture store's settle reads and writes, over captures, items,
    /// filing facts and batch rows held in memory; any other call is not
    /// expected. Its listing pages by (created, id) and lists every capture,
    /// so the settle's own checks are what keep a capture from settling; its
    /// close checks what the store's does under its lock.
    /// </summary>
    private sealed class CaptureStore
    {
        private readonly AutomaticImageIntakeTests.Forwarder forwarder =
            AutomaticImageIntakeTests.Forwarder.For<IPhotoCaptureStore>(null);

        private readonly Dictionary<Guid, ItemFacts> facts = [];
        private readonly Dictionary<(Guid CaptureId, int Batch), BatchRow> batches = [];
        private readonly List<(Guid CaptureId, int Batch, Guid CaseId, Guid? StaffId, string? Reason)> reaches = [];

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
                        capture, Items[capture.Id], capture.CaseId, CaseArchived, CaseIsTriage))
                    .ToArray());
            };
            forwarder.Overrides[nameof(IPhotoCaptureStore.ListBatchFilingsAsync)] = args =>
                Task.FromResult<IReadOnlyList<PhotoCaptureItemFiling>>(Filings((Guid)args[0]!, (int)args[1]!, (Guid?)args[2]));
            forwarder.Overrides[nameof(IPhotoCaptureStore.CloseBatchAsync)] = args =>
            {
                var captureId = (Guid)args[0]!;
                var batch = (int)args[1]!;
                var request = (PhotoCaptureBatchCloseRequest)args[2]!;
                BeforeClose?.Invoke(captureId);
                var result = Close(captureId, batch, request);
                Closes.Add((captureId, batch, request, result.Outcome, result.Members));
                if (result.Outcome == PhotoCaptureBatchClose.Closed)
                {
                    events.Add("close");
                }

                return Task.FromResult(result);
            };
            forwarder.Overrides[nameof(IPhotoCaptureStore.RecordCaseAsync)] = args =>
            {
                var captureId = (Guid)args[0]!;
                var batch = (int)args[1]!;
                var caseId = (Guid)args[2]!;
                var reason = (string)args[3]!;
                var capture = Captures[captureId];
                var changed = capture.CaseId is null && capture.SettledBatches < batch;
                CaseRecords.Add((captureId, batch, caseId, reason, changed));
                if (changed)
                {
                    capture = capture with
                    {
                        CaseId = caseId,
                        CaseReference = "QDOS26044",
                        Destination = PhotoCaptureDestination.Case,
                        RoutingReason = reason,
                        Version = capture.Version + 1
                    };
                    Captures[captureId] = capture;
                }

                return Task.FromResult(new PhotoCaptureCaseRecorded(capture, changed));
            };
            forwarder.Overrides[nameof(IPhotoCaptureStore.ListBatchesReachingCaseAsync)] = _ =>
                Task.FromResult<IReadOnlyList<PhotoCaptureBatchReach>>(reaches
                    .Where(reach => !Row(reach.CaptureId, reach.Batch).Reached)
                    .Select(reach => new PhotoCaptureBatchReach(
                        Captures[reach.CaptureId], reach.Batch, reach.CaseId, CaseArchived, CaseIsTriage, reach.StaffId, reach.Reason))
                    .ToArray());
            forwarder.Overrides[nameof(IPhotoCaptureStore.RecordBatchReachedAsync)] = args =>
            {
                var captureId = (Guid)args[0]!;
                var row = Row(captureId, (int)args[1]!);
                if (row.Reached)
                {
                    return Task.FromResult(false);
                }

                var caseId = (Guid)args[2]!;
                row.Reached = true;
                row.CaseId = caseId;
                row.BellOwed |= (bool)args[3]!;
                Reached.Add((captureId, caseId));
                var capture = Captures[captureId];
                // The images marker is the record's merge's, not the reach's (R841).
                Captures[captureId] = capture with
                {
                    CaseId = capture.CaseId ?? caseId,
                    Destination = PhotoCaptureDestination.Case
                };
                return Task.FromResult(true);
            };
            forwarder.Overrides[nameof(IPhotoCaptureStore.ListBellsOwedAsync)] = _ =>
                Task.FromResult<IReadOnlyList<PhotoCaptureBellOwed>>(BellsOwed);
            forwarder.Overrides[nameof(IPhotoCaptureStore.ClearBellAsync)] = args =>
            {
                Row((Guid)args[0]!, (int)args[1]!).BellOwed = false;
                return Task.CompletedTask;
            };
        }

        public IPhotoCaptureStore Proxy => (IPhotoCaptureStore)forwarder.Proxy;

        /// <summary>The store's calls in order, by method name.</summary>
        public List<string> Calls => forwarder.Calls;

        public Dictionary<Guid, Core.PhotoCapture.PhotoCapture> Captures { get; } = [];

        public Dictionary<Guid, List<PhotoCaptureItem>> Items { get; } = [];

        public bool CaseArchived { get; set; }

        public bool CaseIsTriage { get; set; }

        public Action<Guid>? BeforeClose { get; set; }

        public List<PhotoCaptureSettleCursor?> Listings { get; } = [];

        public List<(Guid CaptureId, int Batch, PhotoCaptureBatchCloseRequest Request, PhotoCaptureBatchClose Outcome, IReadOnlyList<PhotoCaptureItem> Members)> Closes { get; } = [];

        public List<(Guid CaptureId, int Batch, Guid CaseId, string Reason, bool Changed)> CaseRecords { get; } = [];

        public List<(Guid CaptureId, Guid CaseId)> Reached { get; } = [];

        public IReadOnlyList<PhotoCaptureBellOwed> BellsOwed => batches
            .Where(pair => pair.Value.BellOwed && pair.Value.CaseId is not null)
            .Select(pair => new PhotoCaptureBellOwed(
                pair.Key.CaptureId, pair.Key.Batch, pair.Value.CaseId!.Value, Captures[pair.Key.CaptureId].NormalizedRegistration, CaseArchived))
            .ToArray();

        public void Add(Core.PhotoCapture.PhotoCapture capture, params PhotoCaptureItem[] items)
        {
            Captures[capture.Id] = capture;
            Items[capture.Id] = [];
            AddItems(capture.Id, items);
        }

        public void AddItems(Guid captureId, params PhotoCaptureItem[] items) =>
            Items[captureId].AddRange(items.Select(item => item with { CaptureId = captureId }));

        /// <summary>Adds the capture with its items already filed on the Case, as a Case link's are as they arrive.</summary>
        public void AddFiled(Core.PhotoCapture.PhotoCapture capture, Guid caseId, params PhotoCaptureItem[] items)
        {
            Add(capture, items);
            foreach (var item in items)
            {
                Facts(item).Link = new PhotoCaptureItemLink(caseId, true, BySystem: false, Now.AddMinutes(-5));
                Facts(item).StartedOn = caseId;
                Facts(item).FiledOn = caseId;
            }
        }

        public ItemFacts Facts(PhotoCaptureItem item) => FactsOf(item.Id);

        public PhotoCaptureItem Item(Guid itemId) => Items.Values.SelectMany(list => list).Single(item => item.Id == itemId);

        /// <summary>The sender adds an item, already filed on the Case, to the open batch and presses Send again.</summary>
        public void SendAgain(Guid captureId, DateTimeOffset sentAtUtc, string? note, Guid caseId, PhotoCaptureItem item)
        {
            Captures[captureId] = Captures[captureId] with { SentAtUtc = sentAtUtc, Note = note };
            AddItems(captureId, item);
            Facts(item).Link = new PhotoCaptureItemLink(caseId, true, BySystem: false, sentAtUtc);
            Facts(item).FiledOn = caseId;
        }

        /// <summary>The item's receipt is evaluated now.</summary>
        public void Evaluate(PhotoCaptureItem item, Guid intakeReceiptId)
        {
            var list = Items[Captures.Keys.Single(id => Items[id].Any(value => value.Id == item.Id))];
            var index = list.FindIndex(value => value.Id == item.Id);
            list[index] = list[index] with { IntakeReceiptId = intakeReceiptId };
        }

        /// <summary>The batch's record merged into the Case, as decided by a member of staff or, with none, by Pegasus.</summary>
        public void Reach(Guid captureId, int batch, Guid caseId, Guid? staffId = null, string? reason = null)
        {
            Row(captureId, batch).GroupId = PhotoCaptureIntakeRules.BatchGroupId(captureId, batch);
            reaches.Add((captureId, batch, caseId, staffId, reason));
        }

        /// <summary>The receipt files on the Case its active link names.</summary>
        public void FileLinked(Guid stagedReceiptId)
        {
            var item = Items.Values.SelectMany(list => list).SingleOrDefault(value => value.StagedReceiptId == stagedReceiptId);
            if (item is not null && Facts(item).Link is { IsActive: true } link)
            {
                Facts(item).StartedOn = link.CaseId;
                Facts(item).FiledOn = link.CaseId;
            }
        }

        /// <summary>Links the receipt to the Case as the store does; answers the outcome it would.</summary>
        public DeclaredDestinationLinkOutcome Link(Guid receiptId, Guid caseId, bool bySystem, DateTimeOffset now)
        {
            var item = Items.Values.SelectMany(list => list).Single(value => value.IntakeReceiptId == receiptId);
            var itemFacts = Facts(item);
            if (itemFacts.Link is { } existing)
            {
                return existing.IsActive && existing.CaseId == caseId
                    ? DeclaredDestinationLinkOutcome.AlreadyLinked
                    : DeclaredDestinationLinkOutcome.Superseded;
            }

            itemFacts.Link = new PhotoCaptureItemLink(caseId, true, bySystem, now);
            return DeclaredDestinationLinkOutcome.Linked;
        }

        private ItemFacts FactsOf(Guid itemId)
        {
            if (!facts.TryGetValue(itemId, out var found))
            {
                found = new ItemFacts();
                facts[itemId] = found;
            }

            return found;
        }

        private BatchRow Row(Guid captureId, int batch)
        {
            if (!batches.TryGetValue((captureId, batch), out var row))
            {
                row = new BatchRow();
                batches[(captureId, batch)] = row;
            }

            return row;
        }

        private PhotoCaptureItemFiling[] Filings(Guid captureId, int batch, Guid? caseId) =>
            Items[captureId]
                .Where(item => item.Batch == batch && item.StagedReceiptId is not null)
                .Select(item =>
                {
                    var itemFacts = FactsOf(item.Id);
                    return new PhotoCaptureItemFiling(
                        item,
                        itemFacts.WorkFailed,
                        itemFacts.Link,
                        caseId is not null && itemFacts.StartedOn == caseId,
                        caseId is not null && itemFacts.FiledOn == caseId,
                        caseId is null ? null : itemFacts.ExhaustedAtUtc);
                })
                .ToArray();

        private PhotoCaptureBatchClosed Close(Guid captureId, int batch, PhotoCaptureBatchCloseRequest request)
        {
            var capture = Captures[captureId];
            if (capture.SettledBatches >= batch)
            {
                return new(PhotoCaptureBatchClose.AlreadyClosed, capture, []);
            }

            var filings = Filings(captureId, batch, request.CaseId);
            if (!PhotoCaptureFiling.Settled(filings, request.CaseId, request.SystemFilingCutoff)
                || (request.Group is not null && capture.CaseId is not null)
                || (request.BellOwed && !string.Equals(capture.Note, request.CaseSideNote, StringComparison.Ordinal)))
            {
                return new(PhotoCaptureBatchClose.Waiting, capture, []);
            }

            PhotoCaptureItem[] members = request.Group is null
                ? []
                : filings
                    .Where(filing => PhotoCaptureStory.IsPhotographKind(filing.Item.Kind)
                        && PhotoCaptureFiling.StateOf(filing, caseId: null, request.SystemFilingCutoff) == PhotoCaptureFilingState.Evaluated)
                    .Select(filing => filing.Item)
                    .OrderBy(item => item.Ordinal)
                    .ToArray();
            capture = capture with
            {
                SettledBatches = batch,
                SettledAtUtc = request.NowUtc,
                SubmissionGroupId = members.Length > 0 ? PhotoCaptureIntakeRules.BatchGroupId(captureId, batch) : capture.SubmissionGroupId,
                Version = capture.Version + 1
            };
            Captures[captureId] = capture;
            var row = Row(captureId, batch);
            row.GroupId = members.Length > 0 ? PhotoCaptureIntakeRules.BatchGroupId(captureId, batch) : null;
            row.CaseId = request.CaseId;
            row.BellOwed = request.BellOwed;
            return new(PhotoCaptureBatchClose.Closed, capture, members);
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

    /// <summary>
    /// Records each declared-destination link and links it in the store; a
    /// repeated operation key replays. <see cref="Outcome"/>, when set,
    /// answers in place of the store, as a replay of an earlier link does.
    /// </summary>
    private sealed class FakeMutations(List<string> events, CaptureStore store) : IIntakeMutationStore
    {
        private readonly HashSet<string> keys = [];

        public List<DeclaredDestinationLinkRequest> Links { get; } = [];

        public DeclaredDestinationLinkOutcome? Outcome { get; set; }

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
            // A link that follows a record's merge files as a member of staff's does (R844).
            var bySystem = request.Decider.Kind == ActorKind.SystemWorker
                && !PhotoCaptureIntakeRules.FollowsRecordMerge(request.Decider.Kind.ToString(), request.Decider.SubjectId);
            var linked = store.Link(request.ReceiptId, request.CaseId, bySystem, occurredAtUtc);
            return Task.FromResult(Outcome ?? linked);
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

    /// <summary>
    /// Records each re-drive. With <see cref="FilesOnRedrive"/> the re-driven
    /// receipt files on the Case its active link names, as the pipeline's
    /// processing of a linked receipt does.
    /// </summary>
    private sealed class FakeProcessor(List<string> events, CaptureStore store) : IProcessQueuedIntake
    {
        public List<Guid> Redriven { get; } = [];

        public bool FilesOnRedrive { get; set; }

        public Task<QueuedIntakeProcessingResult> ProcessAsync(Guid stagedReceiptId, CancellationToken cancellationToken = default)
        {
            Redriven.Add(stagedReceiptId);
            events.Add($"redrive:{stagedReceiptId}");
            if (FilesOnRedrive)
            {
                store.FileLinked(stagedReceiptId);
            }

            return Task.FromResult(new QueuedIntakeProcessingResult(QueuedIntakeProcessingOutcome.Completed));
        }
    }

    /// <summary>The Case-side writer; completing the images records the capture's marker, as the store's transaction does.</summary>
    private sealed class FakeCaseWriter(List<string> events, CaptureStore store) : IPhotoCaptureCaseWriter
    {
        public List<(Guid CaseId, Guid CaptureId, string Reason)> ImagesCompleted { get; } = [];
        public List<IReadOnlyDictionary<string, string>> Filled { get; } = [];
        public List<Guid> FilledOn { get; } = [];
        public int FailImages { get; set; }

        public Task CompleteImagesAsync(Guid caseId, Guid captureId, string operationKey, string reason, CancellationToken cancellationToken)
        {
            if (FailImages > 0)
            {
                FailImages--;
                throw new InvalidOperationException("The Case could not be written just now.");
            }

            ImagesCompleted.Add((caseId, captureId, reason));
            events.Add("images");
            store.Captures[captureId] = store.Captures[captureId] with { ImagesCompletedAtUtc = Now };
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> FillEmptyFieldsAsync(Guid caseId, IReadOnlyDictionary<string, string> values, Guid captureId, string sourceLabel, CancellationToken cancellationToken)
        {
            Filled.Add(values);
            FilledOn.Add(caseId);
            events.Add("fill");
            return Task.FromResult<IReadOnlyList<string>>(values.Keys.ToArray());
        }

        public Task<Guid?> AssignedEngineerAsync(Guid caseId, CancellationToken cancellationToken) => Task.FromResult<Guid?>(Guid.NewGuid());
    }

    /// <summary>The Case's Notes: an operation key repeated on the same Case replays, as the store's does.</summary>
    private sealed class RecordingNotes(List<string> events) : ICaseNoteStore
    {
        public List<AddCaseNoteRequest> Written { get; } = [];

        public Task AddAsync(AddCaseNoteRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken)
        {
            if (Written.TrueForAll(note => note.CaseId != request.CaseId || note.OperationKey != request.OperationKey))
            {
                Written.Add(request);
                events.Add("note");
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// A Triage Case's own notes as system work writes them: an operation key
    /// already written replays, and a finished Triage takes nothing, as the
    /// store's do.
    /// </summary>
    private sealed class RecordingTriageNotes
    {
        private readonly AutomaticImageIntakeTests.Forwarder forwarder =
            AutomaticImageIntakeTests.Forwarder.For<ITriageStore>(null);

        public RecordingTriageNotes(List<string> events)
        {
            forwarder.Overrides[nameof(ITriageStore.AddSystemNoteAsync)] = args =>
            {
                var operationKey = (string)args[2]!;
                if (Finished || Written.Exists(note => note.OperationKey == operationKey))
                {
                    return Task.FromResult(false);
                }

                Written.Add(((Guid)args[0]!, ((ActionActor)args[1]!).Kind, operationKey, (string)args[3]!));
                events.Add("triage-note");
                return Task.FromResult(true);
            };
        }

        public ITriageStore Proxy => (ITriageStore)forwarder.Proxy;

        public bool Finished { get; set; }

        public List<(Guid CaseId, ActorKind ActorKind, string OperationKey, string Note)> Written { get; } = [];
    }

    private sealed class FakeNotifier(List<string> events) : ICaseStaffNotifier
    {
        public List<(StaffNotificationCause Cause, Guid CaseId)> Raised { get; } = [];

        public int FailNext { get; set; }

        public Task<StaffNotification?> NotifyAsync(StaffNotificationCause cause, Guid caseId, ActionActor? actor, string? section, string? registration, CancellationToken cancellationToken)
        {
            if (FailNext > 0)
            {
                FailNext--;
                throw new InvalidOperationException("The bell could not be raised just now.");
            }

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
