using System.Security.Cryptography;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// The upload review shows an uploaded image at once, from the bytes intake
/// holds (operator, 28 October 2026, R463): the staged copy before processing,
/// the retained source after it, each re-verified against its recorded hash.
/// </summary>
public sealed class ReadUploadedImageTests
{
    private static readonly ActionActor Staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
    private static readonly byte[] Bytes = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4];
    private static readonly string Hash = Convert.ToHexString(SHA256.HashData(Bytes));

    [Fact]
    public async Task BeforeProcessingTheStagedCopyIsRead()
    {
        var harness = new Harness(Staged("image/png"));
        harness.Artifacts.Content["staged-key"] = Bytes;

        var image = Assert.IsType<IntakeSourceDownload>(
            await harness.Sut.ExecuteAsync(harness.StagedId, Staff));

        Assert.Equal(Bytes, image.Content.ToArray());
        Assert.Equal("image/png", image.ContentType);
        Assert.Equal("vehicle.png", image.FileName);
        Assert.Equal(Hash, image.Sha256);
        Assert.Equal("staged-key", Assert.Single(harness.Artifacts.Reads));
    }

    [Fact]
    public async Task OnceProcessedTheRetainedSourceIsRead()
    {
        var harness = new Harness(Staged("image/png"));
        var receipt = Receipt("retained-key");
        harness.Statuses.ProcessedReceiptId = receipt.Id;
        harness.Receipts.Receipt = receipt;
        harness.Artifacts.Content["retained-key"] = Bytes;

        var image = await harness.Sut.ExecuteAsync(harness.StagedId, Staff);

        Assert.NotNull(image);
        Assert.Equal("retained-key", Assert.Single(harness.Artifacts.Reads));
    }

    [Fact]
    public async Task BytesThatDoNotMatchTheRecordedHashAreRefused()
    {
        var harness = new Harness(Staged("image/png"));
        harness.Artifacts.Content["staged-key"] = [9, 9, 9];

        await Assert.ThrowsAsync<IntakeArtifactIntegrityException>(
            () => harness.Sut.ExecuteAsync(harness.StagedId, Staff));
    }

    [Fact]
    public async Task OnlyAnImageIsAnswered()
    {
        var harness = new Harness(Staged("application/pdf"));
        harness.Artifacts.Content["staged-key"] = Bytes;

        Assert.Null(await harness.Sut.ExecuteAsync(harness.StagedId, Staff));
        Assert.Empty(harness.Artifacts.Reads);
    }

    [Fact]
    public async Task AnUnknownUploadOrMissingBytesAnswerNothing()
    {
        var harness = new Harness(Staged("image/jpeg"));

        Assert.Null(await harness.Sut.ExecuteAsync(Guid.NewGuid(), Staff));
        Assert.Null(await harness.Sut.ExecuteAsync(harness.StagedId, Staff));
    }

    private static IntakeStagedReceipt Staged(string mediaType) =>
        new(
            Guid.NewGuid(),
            "vehicle.png",
            mediaType,
            Bytes.Length,
            Hash,
            new(IntakeSourceChannel.ManualUpload, "token"),
            DateTimeOffset.UtcNow,
            "staff",
            "staged-key",
            DateTimeOffset.UtcNow);

    private static IntakeReceipt Receipt(string storageKey)
    {
        var id = Guid.NewGuid();
        IntakeAssetRecord source = new(
            Guid.NewGuid(), "source", "vehicle.png", "image/png",
            IntakeAssetKind.Source, IntakeAssetDisposition.Source,
            Bytes.Length, Hash, storageKey, null, null, null, null,
            IncomingArtifactCustodyState.Pending);
        return new(
            id,
            "vehicle.png",
            "image/png",
            Bytes.Length,
            Hash,
            new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, "token"),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            IntakeDecision.NeedsSorting,
            "reason",
            [],
            [],
            null,
            [],
            null,
            null,
            false,
            "reader",
            "1",
            null,
            null,
            [source]);
    }

    private sealed class Harness
    {
        public Harness(IntakeStagedReceipt staged)
        {
            StagedId = staged.Id;
            Statuses = new(staged);
            Sut = new ReadUploadedImage(Statuses, Receipts, Artifacts);
        }

        public Guid StagedId { get; }

        public Statuses Statuses { get; }

        public Receipts Receipts { get; } = new();

        public Artifacts Artifacts { get; } = new();

        public ReadUploadedImage Sut { get; }
    }

    private sealed class Statuses(IntakeStagedReceipt staged) : IQueuedIntakeStatusQueries
    {
        public Guid? ProcessedReceiptId { get; set; }

        public Task<QueuedIntakeStatus?> GetAsync(Guid stagedReceiptId, CancellationToken cancellationToken = default) =>
            Task.FromResult<QueuedIntakeStatus?>(stagedReceiptId == staged.Id
                ? new(
                    staged.Id,
                    staged.SourceFileName,
                    staged.ReceivedAtUtc,
                    ProcessedReceiptId is null ? QueuedIntakeStatusKind.Received : QueuedIntakeStatusKind.Complete,
                    ProcessedReceiptId,
                    FailureCode: null)
                : null);

        public Task<QueuedIntakeStatus?> FindBySourceIdentityAsync(
            IntakeSourceIdentity sourceIdentity,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IntakeStagedReceipt?> GetStagedReceiptAsync(
            Guid stagedReceiptId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(stagedReceiptId == staged.Id ? staged : null);
    }

    private sealed class Receipts : IIntakeReceiptQueries
    {
        public IntakeReceipt? Receipt { get; set; }

        public Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Receipt?.Id == id ? Receipt : null);
    }

    private sealed class Artifacts : IIntakeArtifactStore
    {
        public Dictionary<string, byte[]> Content { get; } = [];

        public List<string> Reads { get; } = [];

        public Task<string> StoreAsync(string contentHash, ReadOnlyMemory<byte> content, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StagedArtifactInventoryItem> StageAsync(
            Guid stagedReceiptId, string contentHash, Stream content, long contentLength,
            DateTimeOffset firstSeenAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ReadOnlyMemory<byte>?> ReadAsync(string storageKey, CancellationToken cancellationToken)
        {
            Reads.Add(storageKey);
            return Task.FromResult<ReadOnlyMemory<byte>?>(
                Content.TryGetValue(storageKey, out var bytes) ? bytes : null);
        }
    }
}
