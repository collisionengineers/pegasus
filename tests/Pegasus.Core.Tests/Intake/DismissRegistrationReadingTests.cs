using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// Dismiss is offered only on a pending, suggested reading of an open item with
/// a file of its own, and the record, its handler and the Automation Actor all
/// follow that one rule (R1092). A reading already decided goes to the store,
/// which answers the key that decided it.
/// </summary>
public sealed class DismissRegistrationReadingTests
{
    private static readonly DateTimeOffset Now = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private static readonly ActionActor Staff = ActionActor.Staff(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        [StaffRole.Engineer]);

    [Fact]
    public async Task APendingSuggestedReadingOnAnOpenSingleFileItemIsDismissed()
    {
        var reading = Reading(VrmRecognitionOutcomeKind.Suggested);
        var context = Context(UnidentifiedState.Open, withReceipt: true, reading);
        var store = new RecordingSuggestions();

        Assert.True(context.OffersDismiss(reading));
        var dismissed = await new DismissRegistrationReading(store).ExecuteAsync(
            context, reading.Id, Staff, "Not this vehicle.", "dismiss-1", CancellationToken.None);

        var request = Assert.Single(store.Requests);
        Assert.Equal(reading.Id, request.SuggestionId);
        Assert.Equal(ImageVrmSuggestionDisposition.Dismissed, request.Disposition);
        Assert.Equal("Not this vehicle.", request.Reason);
        Assert.Equal("dismiss-1", request.OperationKey);
        Assert.Equal(ImageVrmSuggestionDisposition.Dismissed, dismissed.Disposition);
    }

    [Fact]
    public async Task APendingReadingThatIsNotASuggestionIsRefused()
    {
        var reading = Reading(VrmRecognitionOutcomeKind.NoReadableResult);
        var context = Context(UnidentifiedState.Open, withReceipt: true, reading);

        Assert.False(context.OffersDismiss(reading));
        await AssertRefusedAsync(context, reading);
    }

    [Fact]
    public async Task AReadingOnAClosedItemIsRefused()
    {
        var reading = Reading(VrmRecognitionOutcomeKind.Suggested);
        var context = Context(UnidentifiedState.Resolved, withReceipt: true, reading);

        Assert.False(context.OffersDismiss(reading));
        await AssertRefusedAsync(context, reading);
    }

    [Fact]
    public async Task AReadingOnASubmissionGroupItemIsRefused()
    {
        var reading = Reading(VrmRecognitionOutcomeKind.Suggested);
        var context = Context(UnidentifiedState.Open, withReceipt: false, reading);

        Assert.False(context.OffersDismiss(reading));
        await AssertRefusedAsync(context, reading);
    }

    [Fact]
    public async Task AReadingAlreadyDecidedGoesToTheStoreToAnswer()
    {
        var reading = Reading(VrmRecognitionOutcomeKind.Suggested) with
        {
            Disposition = ImageVrmSuggestionDisposition.Dismissed,
        };
        var context = Context(UnidentifiedState.Resolved, withReceipt: true, reading);
        var store = new RecordingSuggestions();

        Assert.False(context.OffersDismiss(reading));
        await new DismissRegistrationReading(store).ExecuteAsync(
            context, reading.Id, Staff, "Not this vehicle.", "dismiss-1", CancellationToken.None);

        Assert.Equal(reading.Id, Assert.Single(store.Requests).SuggestionId);
    }

    [Fact]
    public async Task AReadingNotOnTheItemIsRefused()
    {
        var context = Context(UnidentifiedState.Open, withReceipt: true, Reading(VrmRecognitionOutcomeKind.Suggested));
        var store = new RecordingSuggestions();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new DismissRegistrationReading(store).ExecuteAsync(
                context, Guid.NewGuid(), Staff, "Not this vehicle.", "dismiss-1", CancellationToken.None));
        Assert.Empty(store.Requests);
    }

    private static async Task AssertRefusedAsync(UnidentifiedItemContext context, ImageVrmSuggestion reading)
    {
        var store = new RecordingSuggestions();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new DismissRegistrationReading(store).ExecuteAsync(
                context, reading.Id, Staff, "Not this vehicle.", "dismiss-1", CancellationToken.None));
        Assert.Empty(store.Requests);
    }

    private static UnidentifiedItemContext Context(
        UnidentifiedState state,
        bool withReceipt,
        ImageVrmSuggestion reading)
    {
        var receiptId = reading.IntakeReceiptId;
        var item = new UnidentifiedItem(
            Guid.NewGuid(),
            1,
            UnidentifiedReferenceFormat.Create(1),
            withReceipt ? UnidentifiedOrigin.Receipt(receiptId) : UnidentifiedOrigin.SubmissionGroup(Guid.NewGuid()),
            UnidentifiedReasonCode.NoUsableIdentification,
            "Recorded safe detail.",
            state,
            Now,
            state == UnidentifiedState.Open ? null : Now,
            ActionActor.SystemWorker("intake-processing"),
            null,
            null,
            null,
            null,
            null,
            0);
        return new(
            item,
            withReceipt ? Receipt(receiptId) : null,
            [reading],
            null,
            null,
            CanRegisterImages: false,
            CanOpenTriage: false);
    }

    private static ImageVrmSuggestion Reading(VrmRecognitionOutcomeKind outcome) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        "storage/0",
        new string('a', 64),
        "engine",
        "1",
        "models",
        outcome,
        outcome == VrmRecognitionOutcomeKind.Suggested ? "AB12CDE" : null,
        outcome == VrmRecognitionOutcomeKind.Suggested ? 0.9 : null,
        null,
        null,
        Now,
        ImageVrmSuggestionDisposition.Pending,
        null,
        null,
        null);

    private static IntakeReceipt Receipt(Guid id) => new(
        id,
        "vehicle.jpg",
        "image/jpeg",
        10,
        new string('b', 64),
        new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, id.ToString("N")),
        Now,
        Now,
        IntakeDecision.NeedsSorting,
        "Recorded by the pipeline.",
        [],
        [],
        null,
        [],
        null,
        null,
        false,
        "intake_source_reader",
        "1",
        null,
        null);

    private sealed class RecordingSuggestions : IVrmSuggestionStore
    {
        public List<ImageVrmSuggestionDispositionRequest> Requests { get; } = [];

        public Task<ImageVrmSuggestion> SetDispositionAsync(
            ImageVrmSuggestionDispositionRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(Reading(VrmRecognitionOutcomeKind.Suggested) with
            {
                Id = request.SuggestionId,
                Disposition = request.Disposition,
                DispositionReason = request.Reason,
            });
        }

        public Task<ImageVrmSuggestion> RecordAsync(
            ImageVrmSuggestionDraft draft,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ImageVrmSuggestion>> ListForReceiptAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<ImageVrmSuggestion>>> ListForReceiptsAsync(
            IReadOnlyCollection<Guid> intakeReceiptIds,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
