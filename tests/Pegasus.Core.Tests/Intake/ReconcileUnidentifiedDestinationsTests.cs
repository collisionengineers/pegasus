using System.Security.Cryptography;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Core.Tests.Intake;

public sealed class ReconcileUnidentifiedDestinationsTests
{
    private static readonly byte[] ImageBytes = [1, 2, 3, 4, 5];
    private static readonly string ImageHash = Convert.ToHexString(SHA256.HashData(ImageBytes));
    private static readonly DateTimeOffset Now = new(2031, 8, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PromotedImageReceiptResolvesItsOpenItemToTheImageIntake()
    {
        var harness = new Harness();
        var receipt = Receipt(Guid.NewGuid(), IntakeDecision.ImageIntakeRegistered);
        harness.Receipts.Receipts[receipt.Id] = receipt;
        var item = harness.AddOpenItem(1, UnidentifiedOrigin.Receipt(receipt.Id));
        var intakeId = Guid.NewGuid();
        harness.ImageIntakes.DetailsByOriginReceipt[receipt.Id] = Detail(intakeId, receipt, "AB12CDE-01");

        var result = await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(1, 1, 0), result);
        var resolve = Assert.Single(harness.Resolve.Requests);
        Assert.Equal(item.Id, resolve.UnidentifiedItemId);
        Assert.Equal(UnidentifiedResolutionTargetKind.ImageIntake, resolve.TargetKind);
        Assert.Equal(intakeId.ToString("N"), resolve.TargetId);
        Assert.Equal("AB12CDE-01", resolve.TargetReference);
        Assert.Equal(ActorKind.Automation, resolve.Actor.Kind);
        Assert.Equal(
            $"intake-unidentified-reconcile:{receipt.Id:N}:{receipt.Version}",
            resolve.OperationKey);
    }

    [Fact]
    public async Task CaseCreatedReceiptResolvesToTheInstructionCase()
    {
        var harness = new Harness();
        var caseId = Guid.NewGuid();
        var receipt = Receipt(
            Guid.NewGuid(),
            IntakeDecision.CaseCreated,
            acceptedCaseId: caseId,
            acceptedCaseReference: "QDOS26009");
        harness.Receipts.Receipts[receipt.Id] = receipt;
        harness.AddOpenItem(2, UnidentifiedOrigin.Receipt(receipt.Id));

        var result = await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(1, result.Resolved);
        var resolve = Assert.Single(harness.Resolve.Requests);
        Assert.Equal(UnidentifiedResolutionTargetKind.InstructionCase, resolve.TargetKind);
        Assert.Equal(caseId.ToString("N"), resolve.TargetId);
        Assert.Equal("QDOS26009", resolve.TargetReference);
    }

    [Fact]
    public async Task StillUnidentifiedReceiptsAreNeverForceClosed()
    {
        var harness = new Harness();
        // Image-only material still awaiting sorting, and a terminal
        // unsupported receipt: both remain legitimately Unidentified.
        var pendingImage = Receipt(Guid.NewGuid(), IntakeDecision.NeedsSorting);
        var unsupported = Receipt(Guid.NewGuid(), IntakeDecision.Unsupported, mediaType: "application/zip");
        harness.Receipts.Receipts[pendingImage.Id] = pendingImage;
        harness.Receipts.Receipts[unsupported.Id] = unsupported;
        harness.AddOpenItem(3, UnidentifiedOrigin.Receipt(pendingImage.Id));
        harness.AddOpenItem(4, UnidentifiedOrigin.Receipt(unsupported.Id));

        var result = await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(2, 0, 0), result);
        Assert.Empty(harness.Resolve.Requests);
    }

    [Fact]
    public async Task GroupOriginItemsAreSkipped()
    {
        var harness = new Harness();
        harness.AddOpenItem(5, UnidentifiedOrigin.SubmissionGroup(Guid.NewGuid()));

        var result = await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(0, 0, 0), result);
        Assert.Empty(harness.Resolve.Requests);
    }

    [Fact]
    public async Task AResolveFailureIsCountedAndNeverStopsTheSweep()
    {
        var harness = new Harness();
        var failing = Receipt(Guid.NewGuid(), IntakeDecision.ImageIntakeRegistered);
        var succeeding = Receipt(Guid.NewGuid(), IntakeDecision.ImageIntakeRegistered);
        harness.Receipts.Receipts[failing.Id] = failing;
        harness.Receipts.Receipts[succeeding.Id] = succeeding;
        harness.AddOpenItem(6, UnidentifiedOrigin.Receipt(failing.Id));
        harness.AddOpenItem(7, UnidentifiedOrigin.Receipt(succeeding.Id));
        harness.ImageIntakes.DetailsByOriginReceipt[failing.Id] = Detail(Guid.NewGuid(), failing, "AB12CDE-01");
        harness.ImageIntakes.DetailsByOriginReceipt[succeeding.Id] = Detail(Guid.NewGuid(), succeeding, "AB12CDE-02");
        harness.Resolve.FailForReceiptOperationKeys.Add(
            $"intake-unidentified-reconcile:{failing.Id:N}:{failing.Version}");

        var result = await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(2, 1, 1), result);
    }

    [Fact]
    public async Task AnAlreadyResolvedItemIsANoOp()
    {
        var harness = new Harness();
        var receipt = Receipt(Guid.NewGuid(), IntakeDecision.ImageIntakeRegistered);
        harness.ImageIntakes.DetailsByOriginReceipt[receipt.Id] = Detail(Guid.NewGuid(), receipt, "AB12CDE-01");
        harness.AddResolvedItem(8, UnidentifiedOrigin.Receipt(receipt.Id));

        var resolved = await harness.Reconciler.ResolveForReceiptAsync(receipt, CancellationToken.None);

        Assert.False(resolved);
        Assert.Empty(harness.Resolve.Requests);
    }

    private static IntakeReceipt Receipt(
        Guid id,
        IntakeDecision decision,
        string mediaType = "image/jpeg",
        Guid? acceptedCaseId = null,
        string? acceptedCaseReference = null) =>
        new(
            id,
            "vehicle.jpg",
            mediaType,
            ImageBytes.Length,
            ImageHash,
            new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, id.ToString("N")),
            Now,
            Now,
            decision,
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
            null,
            [
                new IntakeAssetRecord(
                    Guid.NewGuid(),
                    "uploaded source",
                    "vehicle.jpg",
                    mediaType,
                    IntakeAssetKind.Source,
                    IntakeAssetDisposition.Source,
                    ImageBytes.Length,
                    ImageHash,
                    "storage/0",
                    null,
                    null,
                    null,
                    null)
            ],
            Version: 3,
            AcceptedCaseId: acceptedCaseId,
            AcceptedCaseReference: acceptedCaseReference);

    private static ImageIntakeDetail Detail(Guid intakeId, IntakeReceipt receipt, string reference) =>
        new(
            new ImageIntakeRecord(
                intakeId,
                new ImageIntakeOrigin(
                    receipt.Id,
                    receipt.SourceIdentity,
                    receipt.SourceHash.ToLowerInvariant(),
                    Guid.NewGuid()),
                "AB12CDE",
                reference),
            Now,
            null,
            null);

    private sealed class Harness
    {
        public Harness() => Reconciler = new ReconcileUnidentifiedDestinations(
            Store,
            Resolve,
            Receipts,
            ImageIntakes,
            TimeProvider.System);

        public FakeUnidentifiedStore Store { get; } = new();

        public FakeResolveUnidentified Resolve { get; } = new();

        public FakeReceiptQueries Receipts { get; } = new();

        public FakeImageIntakeQueries ImageIntakes { get; } = new();

        public ReconcileUnidentifiedDestinations Reconciler { get; }

        public UnidentifiedItem AddOpenItem(long sequence, UnidentifiedOrigin origin)
        {
            var item = new UnidentifiedItem(
                Guid.NewGuid(),
                sequence,
                UnidentifiedReferenceFormat.Create(sequence),
                origin,
                UnidentifiedReasonCode.NoUsableIdentification,
                "Recorded safe detail.",
                UnidentifiedState.Open,
                Now,
                null,
                ActionActor.SystemWorker("intake-processing"),
                null,
                null,
                null,
                null,
                null,
                0);
            Store.Items.Add(item);
            return item;
        }

        public UnidentifiedItem AddResolvedItem(long sequence, UnidentifiedOrigin origin)
        {
            var item = new UnidentifiedItem(
                Guid.NewGuid(),
                sequence,
                UnidentifiedReferenceFormat.Create(sequence),
                origin,
                UnidentifiedReasonCode.NoUsableIdentification,
                "Recorded safe detail.",
                UnidentifiedState.Resolved,
                Now,
                Now,
                ActionActor.SystemWorker("intake-processing"),
                ActionActor.Automation("intake-processing"),
                "Previously resolved.",
                UnidentifiedResolutionTargetKind.ExternalReference,
                "earlier-target",
                null,
                1);
            Store.Items.Add(item);
            return item;
        }
    }

    private sealed class FakeUnidentifiedStore : IUnidentifiedStore
    {
        public List<UnidentifiedItem> Items { get; } = [];

        public Task<UnidentifiedRegisterResult> RegisterAsync(
            RegisterUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedRegisterResult?> ProbeRegisterReplayAsync(
            RegisterUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedResolveResult> ResolveAsync(
            ResolveUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedResolveResult?> ProbeResolveReplayAsync(
            ResolveUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedItem?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(item => item.Id == id));

        public Task<UnidentifiedItem?> GetByReferenceAsync(
            string reference,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(item => item.Reference == reference));

        public Task<UnidentifiedItem?> GetByOriginAsync(
            UnidentifiedOrigin origin,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(item => item.Origin == origin));

        public Task<IReadOnlyList<UnidentifiedItem>> ListAsync(
            UnidentifiedState? state = UnidentifiedState.Open,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UnidentifiedItem>>(
                Items.Where(item => state is null || item.State == state).ToArray());

        public Task<IReadOnlyList<UnidentifiedQueueRow>> ListQueueAsync(
            UnidentifiedMediaKind? mediaKind,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<UnidentifiedHistoryEntry>> HistoryAsync(
            Guid unidentifiedItemId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeResolveUnidentified : IResolveUnidentified
    {
        public List<ResolveUnidentifiedRequest> Requests { get; } = [];

        public HashSet<string> FailForReceiptOperationKeys { get; } = [];

        public Task<UnidentifiedResolveResult> ExecuteAsync(
            ResolveUnidentifiedRequest request,
            CancellationToken cancellationToken = default)
        {
            if (FailForReceiptOperationKeys.Contains(request.OperationKey))
            {
                throw new InvalidOperationException("Simulated transient resolution failure.");
            }

            Requests.Add(request);
            var resolved = new UnidentifiedItem(
                request.UnidentifiedItemId,
                1,
                UnidentifiedReferenceFormat.Create(1),
                UnidentifiedOrigin.Receipt(Guid.NewGuid()),
                UnidentifiedReasonCode.NoUsableIdentification,
                "Recorded safe detail.",
                UnidentifiedState.Resolved,
                Now,
                request.ResolvedAtUtc,
                ActionActor.SystemWorker("intake-processing"),
                request.Actor,
                request.Reason,
                request.TargetKind,
                request.TargetId,
                request.TargetReference,
                1);
            return Task.FromResult(new UnidentifiedResolveResult(
                resolved,
                new UnidentifiedHistoryEntry(
                    Guid.NewGuid(),
                    request.UnidentifiedItemId,
                    UnidentifiedState.Open,
                    UnidentifiedState.Resolved,
                    request.Actor,
                    request.ResolvedAtUtc,
                    request.Reason,
                    request.OperationKey,
                    request.TargetKind,
                    request.TargetId,
                    request.TargetReference),
                false));
        }
    }

    private sealed class FakeReceiptQueries : IIntakeReceiptQueries
    {
        public Dictionary<Guid, IntakeReceipt> Receipts { get; } = [];

        public Task<IntakeQueueCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new IntakeQueueCounts(0, 0));

        public Task<IntakeListPage> ListAsync(
            IntakeDecision? decision,
            int page,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new IntakeListPage([], page, pageSize, 0));

        public Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Receipts.TryGetValue(id, out var receipt) ? receipt : null);

        public Task<IntakeAssetRecord?> GetAssetAsync(
            Guid receiptId,
            Guid assetId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IntakeAssetRecord?>(null);
    }

    private sealed class FakeImageIntakeQueries : IImageIntakeQueries
    {
        public Dictionary<Guid, ImageIntakeDetail> DetailsByOriginReceipt { get; } = [];

        public Task<IReadOnlyList<ImageIntakeSummary>> ListAsync(
            bool? associated,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageIntakeSummary>>([]);

        public Task<ImageIntakeDetail?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<ImageIntakeDetail?>(null);

        public Task<ImageIntakeDetail?> GetByReferenceAsync(
            string imageIntakeReference,
            CancellationToken cancellationToken) =>
            Task.FromResult<ImageIntakeDetail?>(null);

        public Task<ImageIntakeDetail?> GetByOriginReceiptAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                DetailsByOriginReceipt.TryGetValue(intakeReceiptId, out var detail) ? detail : null);

        public Task<IReadOnlyList<ImageIntakeSummary>> ListByOriginReceiptsAsync(
            IReadOnlyCollection<Guid> intakeReceiptIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageIntakeSummary>>([]);

        public Task<IReadOnlyList<ImageIntakeSummary>> ListForCaseAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageIntakeSummary>>([]);

        public Task<IReadOnlyList<ImageIntakeSummary>> SearchByRegistrationAsync(
            string normalizedVehicleRegistration,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageIntakeSummary>>([]);
    }
}
