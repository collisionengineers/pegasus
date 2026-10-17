using Pegasus.Core.Intake;
using Pegasus.Core.Tests.ImageIntake;

namespace Pegasus.Core.Tests.Intake;

public sealed class ReconcileStagedArtifactsTests
{
    private const string Hash = "ABABABABABABABABABABABABABABABABABABABABABABABABABABABABABABABAB";
    private const long Length = 17;
    private static readonly DateTimeOffset Now = new(2031, 8, 9, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Aged = Now - ReconcileStagedArtifacts.ExpiryGracePeriod;
    private static readonly DateTimeOffset Young = Aged.AddSeconds(1);
    private static readonly ReconcileStagedArtifactsResult None = new(0, 0, 0, 0, 0, 0, 0, 0);

    [Fact]
    public async Task AnAgedOrphanIsSetToOrphanThenDeletedUnderThatTag()
    {
        var harness = new Harness();
        var orphan = harness.Store.Add(1, Aged, StagedArtifactDisposition.Pending);

        var result = await harness.RunAsync();

        Assert.Equal(None with { Expired = 1 }, result);
        Assert.Equal(new[] { $"set {orphan} Orphan", $"delete {orphan} Orphan" }, harness.Store.Calls);
        Assert.False(harness.Store.IsStaged(orphan));
    }

    [Fact]
    public async Task AYoungOrphanIsMarkedAndCountedButKept()
    {
        var harness = new Harness();
        var orphan = harness.Store.Add(1, Young, StagedArtifactDisposition.Pending);

        var result = await harness.RunAsync();

        Assert.Equal(None with { Orphans = 1 }, result);
        Assert.Equal(new[] { $"set {orphan} Orphan" }, harness.Store.Calls);
        Assert.True(harness.Store.IsStaged(orphan));
    }

    [Fact]
    public async Task AgedUnmatchedBlobsAreDeletedAndAYoungOneIsKept()
    {
        var harness = new Harness();
        var otherHash = harness.Store.Add(1, Aged, StagedArtifactDisposition.Pending);
        harness.Authority.Add(otherHash, StagedArtifactAuthorityState.Pending, hash: new string('C', 64));
        var noWorkItem = harness.Store.Add(2, Aged, StagedArtifactDisposition.Pending);
        harness.Authority.Add(noWorkItem, StagedArtifactAuthorityState.Unmatched);
        var young = harness.Store.Add(3, Young, StagedArtifactDisposition.Pending);
        harness.Authority.Add(young, StagedArtifactAuthorityState.Unmatched);

        var result = await harness.RunAsync();

        Assert.Equal(None with { Expired = 2, Unmatched = 1 }, result);
        Assert.False(harness.Store.IsStaged(otherHash));
        Assert.False(harness.Store.IsStaged(noWorkItem));
        Assert.True(harness.Store.IsStaged(young));
    }

    /// <summary>
    /// A blob whose metadata and tag disagree lists as Unmatched whatever its
    /// tag, so the delete's tag condition holds only after the set.
    /// </summary>
    [Fact]
    public async Task AnAgedBlobListedWithItsTargetIsStillSetBeforeTheDelete()
    {
        var harness = new Harness();
        var unmatched = harness.Store.Add(
            1,
            Aged,
            StagedArtifactDisposition.Unmatched,
            tag: StagedArtifactDisposition.Pending);
        harness.Authority.Add(unmatched, StagedArtifactAuthorityState.Pending, hash: new string('C', 64));

        var result = await harness.RunAsync();

        Assert.Equal(None with { Expired = 1 }, result);
        Assert.Equal(
            new[] { $"set {unmatched} Unmatched", $"delete {unmatched} Unmatched" },
            harness.Store.Calls);
    }

    /// <summary>
    /// A completed item with no source retained under another key may have
    /// only its staging blob as the source: it is kept at any age and counted
    /// in Unmatched (operator, 16 October 2026).
    /// </summary>
    [Fact]
    public async Task ACompletedItemWhoseSourceIsNotRetainedKeepsItsBlobAtAnyAge()
    {
        var harness = new Harness();
        var kept = harness.Store.Add(1, Now.AddDays(-30), StagedArtifactDisposition.Pending);
        harness.Authority.Add(kept, StagedArtifactAuthorityState.CompletedSourceNotRetained);

        var first = await harness.RunAsync();
        var second = await harness.RunAsync();

        Assert.Equal(None with { Unmatched = 1, UnmatchedKept = 1 }, first);
        Assert.Equal(None with { Unmatched = 1, UnmatchedKept = 1 }, second);
        Assert.Equal(new[] { $"set {kept} Unmatched" }, harness.Store.Calls);
        Assert.True(harness.Store.IsStaged(kept));
    }

    [Fact]
    public async Task PendingAndFailedAreKeptAtAnyAgeAndCompletedIsDeleted()
    {
        var harness = new Harness();
        var pending = harness.Store.Add(1, Aged, StagedArtifactDisposition.Pending);
        harness.Authority.Add(pending, StagedArtifactAuthorityState.Pending);
        var failed = harness.Store.Add(2, Aged, StagedArtifactDisposition.Pending);
        harness.Authority.Add(failed, StagedArtifactAuthorityState.Failed);
        var completed = harness.Store.Add(3, Young, StagedArtifactDisposition.Pending);
        harness.Authority.Add(completed, StagedArtifactAuthorityState.Completed);

        var result = await harness.RunAsync();

        Assert.Equal(None with { Completed = 1, Retained = 2 }, result);
        Assert.Equal(
            new[] { $"set {failed} Failed", $"set {completed} Completed", $"delete {completed} Completed" },
            harness.Store.Calls);
        Assert.True(harness.Store.IsStaged(pending));
        Assert.True(harness.Store.IsStaged(failed));
        Assert.False(harness.Store.IsStaged(completed));
    }

    [Fact]
    public async Task ARefusedSetOrDeleteOfAnAgedBlobIsAFailure()
    {
        var harness = new Harness();
        var setRefused = harness.Store.Add(1, Aged, StagedArtifactDisposition.Pending);
        harness.Store.RefuseSet.Add(setRefused);
        var deleteRefused = harness.Store.Add(2, Aged, StagedArtifactDisposition.Pending);
        harness.Store.RefuseDelete.Add(deleteRefused);

        var result = await harness.RunAsync();

        Assert.Equal(None with { Failures = 2 }, result);
        Assert.True(harness.Store.IsStaged(setRefused));
        Assert.True(harness.Store.IsStaged(deleteRefused));
    }

    [Fact]
    public async Task TheSweepPagesPastBlobsItNeverDeletesAndStartsOverAtTheEnd()
    {
        var harness = new Harness();
        foreach (var index in Enumerable.Range(1, 120))
        {
            var key = harness.Store.Add(index, Aged, StagedArtifactDisposition.Pending);
            harness.Authority.Add(key, StagedArtifactAuthorityState.Pending);
        }

        var orphan = harness.Store.Add(121, Aged, StagedArtifactDisposition.Pending);

        var first = await harness.RunAsync();
        var second = await harness.RunAsync();
        Assert.True(harness.Store.IsStaged(orphan));
        var third = await harness.RunAsync();
        var fourth = await harness.RunAsync();

        Assert.Equal(None with { Retained = 50 }, first);
        Assert.Equal(None with { Retained = 50 }, second);
        Assert.Equal(None with { Retained = 20, Expired = 1 }, third);
        Assert.False(harness.Store.IsStaged(orphan));
        Assert.Equal(None with { Retained = 50 }, fourth);
        Assert.Equal(
            new string?[] { null, FakeStagedStore.Key(50), FakeStagedStore.Key(100), null },
            harness.Store.Continuations);
        Assert.All(harness.Store.PageSizes, size => Assert.Equal(50, size));
    }

    private sealed class Harness
    {
        private readonly StagedArtifactSweepCursor cursor = new();

        public FakeStagedStore Store { get; } = new();

        public FakeAuthority Authority { get; } = new();

        /// <summary>
        /// A fresh reconciler over the one cursor, as the Worker builds one per tick.
        /// </summary>
        public Task<ReconcileStagedArtifactsResult> RunAsync()
        {
            var work = AutomaticImageIntakeTests.Forwarder.For<IIntakeWorkStore>(null);
            work.Overrides[nameof(IIntakeWorkStore.RecoverInterruptedWorkAsync)] = _ => Task.FromResult(0);
            return new ReconcileStagedArtifacts(
                (IIntakeWorkStore)work.Proxy,
                Authority,
                Store,
                cursor,
                new FixedClock(Now)).ExecuteAsync(50);
        }
    }

    private sealed class FakeAuthority : IStagedArtifactAuthority
    {
        private readonly Dictionary<string, StagedArtifactAuthority> records = new(StringComparer.Ordinal);

        public void Add(string storageKey, StagedArtifactAuthorityState state, string hash = Hash) =>
            records[storageKey] = new(storageKey, hash, Length, state);

        public Task<StagedArtifactAuthority?> FindAsync(
            string storageKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(records.GetValueOrDefault(storageKey));
    }

    /// <summary>
    /// Staged blobs in key order. A set writes the disposition to both the
    /// listing and the tag; a delete holds only under the current token and tag.
    /// </summary>
    private sealed class FakeStagedStore : IIntakeArtifactStore
    {
        private readonly SortedDictionary<string, Entry> entries = new(StringComparer.Ordinal);

        public List<string> Calls { get; } = [];

        public List<string?> Continuations { get; } = [];

        public List<int> PageSizes { get; } = [];

        public HashSet<string> RefuseSet { get; } = new(StringComparer.Ordinal);

        public HashSet<string> RefuseDelete { get; } = new(StringComparer.Ordinal);

        public static string Key(int index) =>
            $"staging/00000000-0000-0000-0000-{index:D12}/{Hash}";

        public string Add(
            int index,
            DateTimeOffset firstSeenAtUtc,
            StagedArtifactDisposition listed,
            StagedArtifactDisposition? tag = null)
        {
            var key = Key(index);
            entries[key] = new(new(key, Hash, Length, firstSeenAtUtc, listed, $"token-{index}"), tag ?? listed);
            return key;
        }

        public bool IsStaged(string storageKey) => entries.ContainsKey(storageKey);

        public Task<StagedArtifactInventoryPage> ListStagedAsync(
            int maximumItems,
            string? continuation,
            CancellationToken cancellationToken)
        {
            Continuations.Add(continuation);
            PageSizes.Add(maximumItems);
            var remaining = entries.Keys
                .Where(key => continuation is null || string.CompareOrdinal(key, continuation) > 0)
                .ToList();
            var items = remaining.Take(maximumItems).Select(key => entries[key].Item).ToList();
            return Task.FromResult(new StagedArtifactInventoryPage(
                items,
                remaining.Count > maximumItems ? items[^1].StorageKey : null));
        }

        public Task<StagedArtifactInventoryItem?> TrySetStagedDispositionAsync(
            string storageKey,
            string expectedConcurrencyToken,
            StagedArtifactDisposition disposition,
            CancellationToken cancellationToken)
        {
            Calls.Add($"set {storageKey} {disposition}");
            if (RefuseSet.Contains(storageKey)
                || !entries.TryGetValue(storageKey, out var entry)
                || !string.Equals(entry.Item.ConcurrencyToken, expectedConcurrencyToken, StringComparison.Ordinal))
            {
                return Task.FromResult<StagedArtifactInventoryItem?>(null);
            }

            var updated = entry.Item with
            {
                Disposition = disposition,
                ConcurrencyToken = entry.Item.ConcurrencyToken + "+"
            };
            entries[storageKey] = new(updated, disposition);
            return Task.FromResult<StagedArtifactInventoryItem?>(updated);
        }

        public Task<bool> DeleteStagedAsync(
            string storageKey,
            string expectedConcurrencyToken,
            StagedArtifactDisposition expectedDisposition,
            CancellationToken cancellationToken)
        {
            Calls.Add($"delete {storageKey} {expectedDisposition}");
            return Task.FromResult(
                !RefuseDelete.Contains(storageKey)
                && entries.TryGetValue(storageKey, out var entry)
                && string.Equals(entry.Item.ConcurrencyToken, expectedConcurrencyToken, StringComparison.Ordinal)
                && entry.Tag == expectedDisposition
                && entries.Remove(storageKey));
        }

        public Task<string> StoreAsync(
            string contentHash,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The sweep never stores.");

        public Task<StagedArtifactInventoryItem> StageAsync(
            Guid stagedReceiptId,
            string contentHash,
            Stream content,
            long contentLength,
            DateTimeOffset firstSeenAtUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The sweep never stages.");

        public Task<ReadOnlyMemory<byte>?> ReadAsync(
            string storageKey,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The sweep never reads content.");

        private sealed record Entry(StagedArtifactInventoryItem Item, StagedArtifactDisposition Tag);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
