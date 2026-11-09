using Pegasus.Core.AiWork;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Notifications;
using Pegasus.Core.PhotoCapture;
using static Pegasus.Core.Tests.PhotoCapture.PhotoCaptureTestData;

namespace Pegasus.Core.Tests.PhotoCapture;

public sealed class SettlePhotoCapturesTests
{
    private static readonly Guid Staged1 = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Staged2 = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly string[] FilledFields = ["claimant_contact_number", "claimant_name", "incident_date", "vehicle_mileage"];

    [Fact]
    public async Task ASentCaseCaptureCompletesTheImagesWritesTheNoteFillsTheFieldsAndTellsTheEngineer()
    {
        var harness = new Harness();
        var capture = Capture(
            openedAtUtc: Now.AddMinutes(-30),
            acceptedItemCount: 2,
            sentAtUtc: Now.AddMinutes(-1),
            note: "The boot doesn't close.",
            incidentDate: new DateOnly(2026, 10, 28),
            mileage: 48_210);
        harness.Captures.Pending.Add(new(capture, [
            Item(PhotoCaptureItemKind.Overview, 1, ordinal: 0, intakeReceiptId: Guid.NewGuid()),
            Item(PhotoCaptureItemKind.NumberPlate, 1, ordinal: 1, intakeReceiptId: Guid.NewGuid())
        ]));

        var result = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, result.Settled);
        Assert.Equal(0, result.Failures);
        Assert.Single(harness.Captures.Settled);
        Assert.Null(harness.Captures.Settled[0].GroupId);
        Assert.Equal(capture.CaseId, harness.CaseWriter.ImagesCompleted.Single());
        Assert.Equal("Customer's note (photo link): The boot doesn't close.", harness.Notes.Last?.Note);
        Assert.Equal(ActorKind.SystemWorker, harness.Notes.Last?.Actor.Kind);
        Assert.Equal(FilledFields, harness.CaseWriter.Filled.Single().Keys.Order());
        Assert.Equal(StaffNotificationCause.PhotosReceived, harness.Notifier.Raised.Single().Cause);
        Assert.Equal(capture.CaseId, harness.Notifier.Raised.Single().CaseId);
        Assert.Empty(harness.Groups.Created);
        Assert.Empty(harness.Processor.Redriven);
    }

    [Fact]
    public async Task ACaptureWaitsWhileAPhotoIsStillBeingProcessed()
    {
        var harness = new Harness();
        var capture = Capture(acceptedItemCount: 1, sentAtUtc: Now);
        harness.Captures.Pending.Add(new(capture, [Item(PhotoCaptureItemKind.Overview, 1, intakeReceiptId: null)]));
        harness.Captures.AllProcessed = false;

        var result = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, result.Examined);
        Assert.Equal(0, result.Settled);
        Assert.Empty(harness.Captures.Settled);
        Assert.Empty(harness.CaseWriter.ImagesCompleted);
    }

    [Fact]
    public async Task AVehicleImagesCaptureFormsItsBatchGroupWithTheTypedRegistrationAndRedrivesTheFirstMember()
    {
        var harness = new Harness();
        var capture = Capture(
            origin: PhotoCaptureOrigin.StandingStaff,
            destination: PhotoCaptureDestination.VehicleImages,
            acceptedItemCount: 2,
            sentAtUtc: Now);
        var first = Item(PhotoCaptureItemKind.Overview, 1, ordinal: 0, intakeReceiptId: Guid.NewGuid()) with { StagedReceiptId = Staged1 };
        var second = Item(PhotoCaptureItemKind.Video, 1, ordinal: 1, intakeReceiptId: Guid.NewGuid()) with { StagedReceiptId = Staged2 };
        // An upload that never completed leaves a row with no receipt: not a photo the capture holds.
        var dangling = Item(PhotoCaptureItemKind.Damage, 1, "fd", PhotoCaptureStage.Overview, ordinal: 2) with { StagedReceiptId = null };
        harness.Captures.Pending.Add(new(capture, [second, first, dangling]));

        var result = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, result.Settled);
        var group = Assert.Single(harness.Groups.Created);
        Assert.Equal(IntakeSourceChannel.PhotoCapture, group.Channel);
        Assert.Equal(PhotoCaptureIntakeRules.BatchToken(capture.Id, 1), group.SubmissionToken);
        Assert.Equal(2, group.ExpectedMemberCount);
        Assert.Equal("RY65OFG", group.DeclaredRegistration);
        Assert.Equal(PhotoCaptureIntakeRules.BatchGroupId(capture.Id, 1), group.Id);
        Assert.Equal([(0, Staged1), (1, Staged2)], harness.Groups.Members.Select(member => (member.Ordinal, member.StagedReceiptId)));
        Assert.Equal([Staged1], harness.Processor.Redriven);
        Assert.Equal(group.Id, harness.Captures.Settled.Single().GroupId);
        Assert.Empty(harness.CaseWriter.ImagesCompleted);
        Assert.Empty(harness.Notifier.Raised);
    }

    [Fact]
    public async Task ASettledCaptureThatReachesACaseFinishesTheCaseSideWork()
    {
        var harness = new Harness();
        var capture = Capture(
            origin: PhotoCaptureOrigin.Website,
            destination: PhotoCaptureDestination.VehicleImages,
            acceptedItemCount: 1,
            sentAtUtc: Now.AddHours(-2),
            settledAtUtc: Now.AddHours(-1),
            settledBatches: 1,
            note: "Rattle from the back.");
        var caseId = Guid.NewGuid();
        harness.Captures.Reaching.Add((new(capture, [Item(PhotoCaptureItemKind.Overview, 1, intakeReceiptId: Guid.NewGuid())]), caseId, "QDOS26044"));

        var result = await harness.Settle().ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, result.ReachedCase);
        Assert.Equal(caseId, harness.Captures.CaseRecorded.Single());
        Assert.Equal(caseId, harness.CaseWriter.ImagesCompleted.Single());
        Assert.Contains("Rattle from the back.", harness.Notes.Last?.Note);
        Assert.Equal(caseId, harness.Notifier.Raised.Single().CaseId);
    }

    private sealed class Harness
    {
        public FakeCaptureStore Captures { get; } = new();
        public FakeGroupStore Groups { get; } = new();
        public FakeProcessor Processor { get; } = new();
        public FakeCaseWriter CaseWriter { get; } = new();
        public RecordingNotes Notes { get; } = new();
        public FakeNotifier Notifier { get; } = new();

        public SettlePhotoCaptures Settle() => new(
            Captures,
            Groups,
            Processor,
            CaseWriter,
            new AddCaseNote(Notes, new FixedClock(Now)),
            Notifier,
            new FixedClock(Now));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeCaptureStore : IPhotoCaptureStore
    {
        public List<PhotoCaptureWithItems> Pending { get; } = [];
        public List<(PhotoCaptureWithItems Capture, Guid CaseId, string CaseReference)> Reaching { get; } = [];
        public List<(Guid CaptureId, Guid? GroupId)> Settled { get; } = [];
        public List<Guid> CaseRecorded { get; } = [];
        public bool AllProcessed { get; set; } = true;

        public Task<IReadOnlyList<PhotoCaptureWithItems>> ListAwaitingSettleAsync(DateTimeOffset nowUtc, int maximumItems, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PhotoCaptureWithItems>>(Pending.ToArray());

        public Task<IReadOnlyList<(PhotoCaptureWithItems Capture, Guid CaseId, string CaseReference)>> ListSettledReachingCaseAsync(int maximumItems, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<(PhotoCaptureWithItems, Guid, string)>>(Reaching.ToArray());

        public Task<bool> AllItemsProcessedAsync(Guid captureId, int batch, CancellationToken cancellationToken) => Task.FromResult(AllProcessed);

        public Task<Core.PhotoCapture.PhotoCapture> RecordSettledAsync(Guid captureId, long expectedVersion, Guid? submissionGroupId, DateTimeOffset settledAtUtc, CancellationToken cancellationToken)
        {
            Settled.Add((captureId, submissionGroupId));
            var capture = Pending.Single(pending => pending.Capture.Id == captureId).Capture;
            return Task.FromResult(capture with { SettledAtUtc = settledAtUtc, SubmissionGroupId = submissionGroupId, Version = capture.Version + 1 });
        }

        public Task<Core.PhotoCapture.PhotoCapture> RecordCaseAsync(Guid captureId, Guid caseId, string caseReference, CancellationToken cancellationToken)
        {
            CaseRecorded.Add(caseId);
            var capture = Reaching.Single(pending => pending.Capture.Capture.Id == captureId).Capture.Capture;
            return Task.FromResult(capture with { CaseId = caseId, CaseReference = caseReference, Destination = PhotoCaptureDestination.Case });
        }

        public Task<Core.PhotoCapture.PhotoCapture> CreateAsync(NewPhotoCapture capture, string tokenDigest, PhotoLinkTokenPurpose tokenPurpose, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Core.PhotoCapture.PhotoCapture?> GetAsync(Guid captureId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<(Core.PhotoCapture.PhotoCapture Capture, bool TokenRevoked, bool CaseArchived)?> FindByTokenDigestAsync(string tokenDigest, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddTokenAsync(Guid captureId, string tokenDigest, PhotoLinkTokenPurpose purpose, Guid? issuedByStaffId, DateTimeOffset issuedAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Core.PhotoCapture.PhotoCapture> RecordOpenedAsync(Guid captureId, DateTimeOffset openedAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Core.PhotoCapture.PhotoCapture> RecordDetailsAsync(Guid captureId, long expectedVersion, string? normalizedRegistration, DateOnly? incidentDate, string? customerName, string? mobile, string? email, PhotoCaptureDetails details, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Core.PhotoCapture.PhotoCapture> RecordRoutingAsync(Guid captureId, long expectedVersion, PhotoCaptureDestination destination, Guid? caseId, string reason, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PhotoCaptureItemWriteResult> AddItemAsync(NewPhotoCaptureItem item, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RecordItemReceiptAsync(Guid itemId, Guid stagedReceiptId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RecordItemProcessedAsync(Guid itemId, Guid intakeReceiptId, Guid? intakeAssetId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ReserveOriginalAsync(Guid itemId, long contentLength, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ReleaseOriginalAsync(Guid itemId, long contentLength, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RecordItemOriginalAsync(Guid itemId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Core.PhotoCapture.PhotoCapture> RecordSentAsync(Guid captureId, string? note, DateTimeOffset sentAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Core.PhotoCapture.PhotoCapture> RevokeAsync(Guid captureId, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Core.PhotoCapture.PhotoCapture> ExtendAsync(Guid captureId, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Core.PhotoCapture.PhotoCapture> RecordReminderAsync(Guid captureId, DateTimeOffset remindedAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Core.PhotoCapture.PhotoCapture> RecordCallAsync(Guid captureId, DateTimeOffset calledAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<PhotoCaptureItem>> ListItemsAsync(Guid captureId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PhotoCaptureItem?> FindItemAsync(Guid captureId, Guid clientItemId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeGroupStore : IIntakeSubmissionGroupStore
    {
        public List<IntakeSubmissionGroup> Created { get; } = [];
        public List<IntakeSubmissionGroupMember> Members { get; } = [];

        public Task<IntakeSubmissionGroup> GetOrCreateAsync(Guid groupId, IntakeSourceChannel channel, string submissionToken, int expectedMemberCount, string actor, DateTimeOffset receivedAtUtc, Guid? parentReceiptId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("A photo link's batch declares its registration.");

        public Task<IntakeSubmissionGroup> GetOrCreateDeclaredAsync(Guid groupId, IntakeSourceChannel channel, string submissionToken, int expectedMemberCount, string actor, DateTimeOffset receivedAtUtc, string declaredRegistration, CancellationToken cancellationToken = default)
        {
            var group = new IntakeSubmissionGroup(groupId, channel, submissionToken, expectedMemberCount, actor, receivedAtUtc, [], null)
            {
                DeclaredRegistration = declaredRegistration
            };
            Created.Add(group);
            return Task.FromResult(group);
        }

        public Task<IntakeSubmissionGroupMember> AddMemberAsync(Guid groupId, int ordinal, ReceivedIntake received, CancellationToken cancellationToken = default)
        {
            var member = new IntakeSubmissionGroupMember(groupId, ordinal, received.StagedReceiptId, "name.jpg", new string('a', 64), received.IsDuplicate);
            Members.Add(member);
            return Task.FromResult(member);
        }

        public Task<IReadOnlyList<Guid>> ListPendingImageGroupReceiptsAsync(int maximumItems, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<IntakeSubmissionGroup?> GetAsync(Guid groupId, CancellationToken cancellationToken = default) => Task.FromResult(Created.FirstOrDefault(group => group.Id == groupId));
        public Task<IntakeSubmissionGroup?> FindAsync(IntakeSourceChannel channel, string submissionToken, CancellationToken cancellationToken = default) => Task.FromResult<IntakeSubmissionGroup?>(null);
        public Task<IReadOnlyList<IntakeSubmissionGroupMember>> ListMembersAsync(Guid groupId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<IntakeSubmissionGroupMember>>(Members.Where(member => member.GroupId == groupId).ToArray());
    }

    private sealed class FakeProcessor : IProcessQueuedIntake
    {
        public List<Guid> Redriven { get; } = [];

        public Task<QueuedIntakeProcessingResult> ProcessAsync(Guid stagedReceiptId, CancellationToken cancellationToken = default)
        {
            Redriven.Add(stagedReceiptId);
            return Task.FromResult(new QueuedIntakeProcessingResult(QueuedIntakeProcessingOutcome.Completed));
        }
    }

    private sealed class FakeCaseWriter : IPhotoCaptureCaseWriter
    {
        public List<Guid> ImagesCompleted { get; } = [];
        public List<IReadOnlyDictionary<string, string>> Filled { get; } = [];

        public Task CompleteImagesAsync(Guid caseId, string operationKey, string reason, CancellationToken cancellationToken)
        {
            ImagesCompleted.Add(caseId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> FillEmptyFieldsAsync(Guid caseId, IReadOnlyDictionary<string, string> values, Guid captureId, string sourceLabel, CancellationToken cancellationToken)
        {
            Filled.Add(values);
            return Task.FromResult<IReadOnlyList<string>>(values.Keys.ToArray());
        }

        public Task<Guid?> AssignedEngineerAsync(Guid caseId, CancellationToken cancellationToken) => Task.FromResult<Guid?>(Guid.NewGuid());
    }

    private sealed class RecordingNotes : ICaseNoteStore
    {
        public AddCaseNoteRequest? Last { get; private set; }

        public Task AddAsync(AddCaseNoteRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken)
        {
            Last = request;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNotifier : ICaseStaffNotifier
    {
        public List<(StaffNotificationCause Cause, Guid CaseId)> Raised { get; } = [];

        public Task<StaffNotification?> NotifyAsync(StaffNotificationCause cause, Guid caseId, ActionActor? actor, string? section, string? registration, CancellationToken cancellationToken)
        {
            Raised.Add((cause, caseId));
            return Task.FromResult<StaffNotification?>(null);
        }

        public Task<StaffNotification?> NotifyAsync(StaffNotificationCause cause, Guid caseId, string reference, Guid? assignedEngineerId, ActionActor? actor, string? section, string? registration, CancellationToken cancellationToken)
        {
            Raised.Add((cause, caseId));
            return Task.FromResult<StaffNotification?>(null);
        }

        public Task<StaffNotification?> NotifyMailArrivalAsync(Guid caseId, Guid intakeReceiptId, ActionActor? actor, CancellationToken cancellationToken) => Task.FromResult<StaffNotification?>(null);
        public Task<StaffNotification?> NotifyCancellationReceivedAsync(Guid caseId, ActionActor? actor, CancellationToken cancellationToken) => Task.FromResult<StaffNotification?>(null);
        public Task<StaffNotification?> NotifyAiDraftReadyAsync(AiJobRecord job, CancellationToken cancellationToken) => Task.FromResult<StaffNotification?>(null);
    }
}
