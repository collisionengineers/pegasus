using System.Security.Cryptography;
using Pegasus.Core.Documents;

namespace Pegasus.Core.Tests.Documents;

/// <summary>
/// R203: a document staged in parts, each refused by its own SHA-256 before
/// it is stored, opened whole and checked against its declaration. Nothing
/// here writes custody; the open marks nothing, so a replayed using call reads
/// the same bytes until the upload expires.
/// </summary>
public sealed class DocumentUploadTests
{
    private const string Owner = "pegasus-automation";
    private static readonly DateTimeOffset Now = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private static readonly Guid UploadId = new("6a1f3e0c-8b5d-4f7e-9a2b-1c3d5e7f9a0b");

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(DocumentUploadLimits.MaximumContentBytes + 1L)]
    public async Task BeginRefusesBadTotals(long totalBytes)
    {
        var harness = new Harness();

        var refused = await Assert.ThrowsAsync<ArgumentException>(() => harness.Uploads.ExecuteAsync(
            new BeginDocumentUploadRequest(UploadId, Owner, totalBytes, Sha256Of([1])),
            default));

        Assert.Equal(DocumentUploadRefusals.TotalBytes, refused.Message);
        Assert.Empty(harness.Staging.Manifests);
    }

    [Theory]
    [InlineData("ABC")]
    [InlineData("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    [InlineData("")]
    public async Task BeginRefusesBadHashes(string sha256)
    {
        var harness = new Harness();

        var refused = await Assert.ThrowsAsync<ArgumentException>(() => harness.Uploads.ExecuteAsync(
            new BeginDocumentUploadRequest(UploadId, Owner, 10, sha256),
            default));

        Assert.Equal(DocumentUploadRefusals.Sha256, refused.Message);
        Assert.Empty(harness.Staging.Manifests);
    }

    [Fact]
    public async Task BeginReplaysAndRefusesChangedDetails()
    {
        var harness = new Harness();
        var document = Document(DocumentUploadLimits.PartBytes * 2 + 10);

        var first = await harness.BeginAsync(document);
        await harness.AppendAsync(document, 2);
        var replay = await harness.BeginAsync(document);
        var changed = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Uploads.ExecuteAsync(
                new BeginDocumentUploadRequest(UploadId, Owner, document.Length, Sha256Of([9])),
                default));

        Assert.False(first.IsReplay);
        Assert.Equal(3, first.PartCount);
        Assert.Equal(DocumentUploadLimits.PartBytes, first.PartBytes);
        Assert.Equal(Now + DocumentUploadLimits.Lifetime, first.ExpiresAtUtc);
        Assert.Empty(first.ReceivedParts);
        Assert.True(replay.IsReplay);
        Assert.Equal([2], replay.ReceivedParts);
        Assert.Equal(DocumentUploadRefusals.OperationKeyConflict, changed.Message);
        Assert.Single(harness.Staging.Manifests);
    }

    [Fact]
    public async Task PartsAreCheckedAndAReplayedPartIsANoOp()
    {
        var harness = new Harness();
        var document = Document(DocumentUploadLimits.PartBytes + 7);
        await harness.BeginAsync(document);

        var outside = await Assert.ThrowsAsync<ArgumentException>(() => harness.AppendAsync(document, 3));
        var wrongLength = await Assert.ThrowsAsync<ArgumentException>(() => harness.Uploads.ExecuteAsync(
            new AppendDocumentUploadPartRequest(UploadId, Owner, 2, new byte[8], Sha256Of(new byte[8])),
            default));
        var damaged = PartOf(document, 1);
        damaged[100] ^= 0xFF;
        var mismatch = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Uploads.ExecuteAsync(
            new AppendDocumentUploadPartRequest(UploadId, Owner, 1, damaged, Sha256Of(PartOf(document, 1))),
            default));
        Assert.Empty(harness.Staging.Parts);

        var stored = await harness.AppendAsync(document, 1);
        var replayed = await harness.AppendAsync(document, 1);
        var conflict = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Uploads.ExecuteAsync(
            new AppendDocumentUploadPartRequest(UploadId, Owner, 1, damaged, Sha256Of(damaged)),
            default));

        Assert.Equal(DocumentUploadRefusals.PartNumber(2), outside.Message);
        Assert.Equal(DocumentUploadRefusals.PartLength(2, 7), wrongLength.Message);
        Assert.Equal(DocumentUploadRefusals.PartSha256Mismatch, mismatch.Message);
        Assert.False(stored.IsReplay);
        Assert.Equal(Sha256Of(PartOf(document, 1)), stored.Sha256);
        Assert.True(replayed.IsReplay);
        Assert.Equal(DocumentUploadRefusals.PartConflict, conflict.Message);
        Assert.Equal(PartOf(document, 1), Assert.Single(harness.Staging.Parts).Value);
    }

    [Fact]
    public async Task OpenRefusesMissingPartsLengthAndHashMismatch()
    {
        var harness = new Harness();
        var document = Document(DocumentUploadLimits.PartBytes * 3 + 1);
        await harness.BeginAsync(document);
        await harness.AppendAsync(document, 1);
        await harness.AppendAsync(document, 3);

        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.OpenAsync());
        Assert.Equal(DocumentUploadRefusals.MissingParts([2, 4]), missing.Message);

        await harness.AppendAsync(document, 2);
        await harness.AppendAsync(document, 4);
        harness.Staging.Parts[(UploadId, 4)] = [1, 2];
        var misfit = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.OpenAsync());
        Assert.Equal(DocumentUploadRefusals.LengthMismatch, misfit.Message);

        harness.Staging.Parts[(UploadId, 4)] = [(byte)(document[^1] ^ 0xFF)];
        var mismatch = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.OpenAsync());
        Assert.Equal(DocumentUploadRefusals.Sha256Mismatch, mismatch.Message);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("expired")]
    [InlineData("unknown")]
    public async Task ForeignOrExpiredUploadIsRefusedNonDisclosingly(string kind)
    {
        var harness = new Harness();
        var document = Document(20);
        await harness.BeginAsync(document);
        var owner = kind == "foreign" ? "another-client" : Owner;
        var uploadId = kind == "unknown" ? Guid.NewGuid() : UploadId;
        if (kind == "expired")
        {
            harness.Clock.Now = Now + DocumentUploadLimits.Lifetime;
        }

        var part = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Uploads.ExecuteAsync(
            new AppendDocumentUploadPartRequest(uploadId, owner, 1, document, Sha256Of(document)),
            default));
        var open = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Uploads.ExecuteAsync(new OpenDocumentUploadRequest(uploadId, owner), default));

        Assert.Equal(DocumentUploadRefusals.Unavailable, part.Message);
        Assert.Equal(DocumentUploadRefusals.Unavailable, open.Message);
        Assert.Empty(harness.Staging.Parts);
    }

    [Fact]
    public async Task OpenReturnsBytesAndKeepsTheSession()
    {
        var harness = new Harness();
        var document = Document(DocumentUploadLimits.PartBytes * 2 + 333);
        await harness.BeginAsync(document);
        foreach (var partNumber in new[] { 3, 1, 2 })
        {
            await harness.AppendAsync(document, partNumber);
        }

        var first = await harness.OpenAsync();
        var second = await harness.OpenAsync();

        Assert.Equal(document, first.Content.ToArray());
        Assert.Equal(Sha256Of(document), first.Sha256);
        Assert.Equal(document, second.Content.ToArray());
        Assert.Single(harness.Staging.Manifests);
        Assert.Equal(3, harness.Staging.Parts.Count);
        Assert.Equal(0, harness.Staging.Deletes);
    }

    private static byte[] Document(int length)
    {
        var bytes = new byte[length];
        new Random(length).NextBytes(bytes);
        return bytes;
    }

    private static byte[] PartOf(byte[] document, int partNumber)
    {
        var offset = (partNumber - 1) * DocumentUploadLimits.PartBytes;
        return document[offset..Math.Min(document.Length, offset + DocumentUploadLimits.PartBytes)];
    }

    private static string Sha256Of(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private sealed class Harness
    {
        public Harness()
        {
            Uploads = new DocumentUploads(Staging, Clock);
        }

        public InMemoryDocumentUploadStaging Staging { get; } = new();
        public SettableClock Clock { get; } = new(Now);
        public DocumentUploads Uploads { get; }

        public Task<DocumentUploadTicket> BeginAsync(byte[] document) =>
            Uploads.ExecuteAsync(
                new BeginDocumentUploadRequest(UploadId, Owner, document.Length, Sha256Of(document).ToLowerInvariant()),
                default);

        public Task<DocumentUploadPartReceipt> AppendAsync(byte[] document, int partNumber)
        {
            var part = PartOf(document, Math.Min(partNumber, DocumentUploadLimits.PartCount(document.Length)));
            return Uploads.ExecuteAsync(
                new AppendDocumentUploadPartRequest(UploadId, Owner, partNumber, part, Sha256Of(part)),
                default);
        }

        public Task<DocumentUploadContent> OpenAsync() =>
            Uploads.ExecuteAsync(new OpenDocumentUploadRequest(UploadId, Owner), default);
    }
}

internal sealed class SettableClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>The staging port in memory, with what each call stored.</summary>
internal sealed class InMemoryDocumentUploadStaging : IDocumentUploadStaging
{
    public Dictionary<Guid, DocumentUploadManifest> Manifests { get; } = [];
    public Dictionary<(Guid UploadId, int PartNumber), byte[]> Parts { get; } = [];
    public List<DocumentUploadStagedObject> Objects { get; } = [];
    public HashSet<string> FailingDeletes { get; } = new(StringComparer.Ordinal);
    public List<string?> Continuations { get; } = [];
    public int Deletes { get; private set; }

    public Task<bool> CreateManifestAsync(DocumentUploadManifest manifest, CancellationToken cancellationToken) =>
        Task.FromResult(Manifests.TryAdd(manifest.UploadId, manifest));

    public Task<DocumentUploadManifest?> ReadManifestAsync(Guid uploadId, CancellationToken cancellationToken) =>
        Task.FromResult(Manifests.GetValueOrDefault(uploadId));

    public Task<DocumentUploadPartWrite> CreatePartAsync(
        Guid uploadId,
        int partNumber,
        ReadOnlyMemory<byte> content,
        string sha256,
        CancellationToken cancellationToken)
    {
        if (Parts.TryAdd((uploadId, partNumber), content.ToArray()))
        {
            return Task.FromResult(DocumentUploadPartWrite.Created);
        }

        return Task.FromResult(
            Convert.ToHexString(SHA256.HashData(Parts[(uploadId, partNumber)])) == sha256
                ? DocumentUploadPartWrite.AlreadyStored
                : DocumentUploadPartWrite.Conflict);
    }

    public Task<IReadOnlyList<int>> ListPartNumbersAsync(Guid uploadId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<int>>(
            [.. Parts.Keys.Where(key => key.UploadId == uploadId).Select(key => key.PartNumber).Order()]);

    public Task<ReadOnlyMemory<byte>?> ReadPartAsync(Guid uploadId, int partNumber, CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte>? part = null;
        if (Parts.TryGetValue((uploadId, partNumber), out var bytes))
        {
            part = bytes;
        }

        return Task.FromResult(part);
    }

    public Task<DocumentUploadStagedPage> ListAsync(int maximumItems, string? continuation, CancellationToken cancellationToken)
    {
        Continuations.Add(continuation);
        var remaining = Objects
            .Where(item => continuation is null || string.CompareOrdinal(item.Name, continuation) > 0)
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .ToArray();
        var page = remaining.Take(maximumItems).ToArray();
        return Task.FromResult(new DocumentUploadStagedPage(
            page,
            page.Length < remaining.Length ? page[^1].Name : null));
    }

    public Task<bool> DeleteAsync(string name, DateTimeOffset createdAtOrBeforeUtc, CancellationToken cancellationToken)
    {
        Deletes++;
        if (FailingDeletes.Contains(name))
        {
            throw new IOException("The store refused the delete.");
        }

        var removed = Objects.RemoveAll(item => item.Name == name && item.CreatedAtUtc <= createdAtOrBeforeUtc);
        return Task.FromResult(removed > 0);
    }
}
