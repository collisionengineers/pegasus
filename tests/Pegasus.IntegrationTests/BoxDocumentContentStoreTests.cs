using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using Pegasus.Core.Documents;
using Pegasus.Infrastructure.Custody;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Behavioral proof for the production managed-document content store against
/// an in-memory Box: the managed layout, hash/length verification, replay
/// semantics, delete idempotence, and durable case-root addressing. No
/// network call is made.
/// </summary>
public sealed class BoxDocumentContentStoreTests
{
    private const string BoxConfigJson = """
        {"boxAppSettings":{"clientID":"client-id","appAuth":{"publicKeyID":"key-id","privateKey":"private-key","passphrase":"passphrase"}},"enterpriseID":"enterprise-id"}
        """;
    private const string ApprovedRootId = "405543781910";
    private const string CaseRootId = "case-root-id";

    private static readonly Guid CaseId = Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f");
    private static readonly Guid VersionId = Guid.Parse("20314253-6475-8697-a8b9-cadbecfd0e1f");
    private static readonly Guid OccurrenceId = Guid.Parse("30415263-7485-96a7-b8c9-daebfc0d1e2f");
    private static readonly Guid DocumentId = Guid.Parse("40516273-8495-a6b7-c8d9-eafb0c1d2e3f");
    private const string CaseReference = "QDOS31001";

    [Fact]
    public async Task StoreThenOpenReadRoundTripsThroughTheManagedLayout()
    {
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var store = CreateStore(box);
        var content = Encoding.UTF8.GetBytes("managed document content");
        var hash = Sha256(content);

        var written = await store.StoreVersionAsync(Address(), content, hash, CancellationToken.None);

        var expectedPath = $"{CaseReference}/002 evidence.jpg";
        Assert.True(box.PathExists(expectedPath), $"Expected Box path {expectedPath} to exist.");

        await using var stream = await store.OpenReadVersionAsync(
            Persisted(Address(), written), hash, content.Length, CancellationToken.None);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        Assert.Equal(content, buffer.ToArray());
    }

    [Fact]
    public async Task OtherRoleAddressUsesThePersistedCaseRootAndManagedOrdinal()
    {
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var store = CreateStore(box);
        var content = Encoding.UTF8.GetBytes("other evidence");
        var address = Address() with
        {
            SemanticRole = DocumentSemanticRole.Other,
            FileName = "other evidence.txt",
            MediaType = "text/plain"
        };

        await store.StoreVersionAsync(address, content, Sha256(content), CancellationToken.None);

        Assert.True(box.PathExists($"{CaseReference}/002 other evidence.txt"));
    }

    [Fact]
    public async Task StreamStoreStagesNonSeekableContentAndPreservesTheCallerStream()
    {
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var store = CreateStore(box);
        var bytes = Encoding.UTF8.GetBytes("streamed managed document content");
        using var content = new ChunkedReadStream(bytes, 3);

        var written = await store.StoreVersionAsync(
            Address(),
            content,
            bytes.LongLength,
            Sha256(bytes),
            CancellationToken.None);

        Assert.Equal(DocumentContentWriteDisposition.Created, written.Disposition);
        Assert.True(content.CanRead);
        Assert.True(content.ReadCount > 1);
        Assert.True(box.PathExists($"{CaseReference}/002 evidence.jpg"));
    }

    [Fact]
    public async Task StreamStoreRejectsAnInvalidHashBeforeWritingToBox()
    {
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var store = CreateStore(box);
        var bytes = Encoding.UTF8.GetBytes("streamed content with the wrong declared hash");
        using var content = new ChunkedReadStream(bytes, 2);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.StoreVersionAsync(
            Address(),
            content,
            bytes.LongLength,
            Sha256(Encoding.UTF8.GetBytes("different content")),
            CancellationToken.None));

        Assert.Equal(1, box.UploadCount);
        Assert.False(box.PathExists($"{CaseReference}/002 evidence.jpg"));
    }

    [Fact]
    public async Task IdenticalRepeatStoreIsAReplayNotASecondUpload()
    {
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var store = CreateStore(box);
        var content = Encoding.UTF8.GetBytes("replayed content");
        var hash = Sha256(content);

        var first = await store.StoreVersionAsync(Address(), content, hash, CancellationToken.None);
        var uploadsAfterFirst = box.UploadCount;
        var replay = await store.StoreVersionAsync(Address(), content, hash, CancellationToken.None);

        Assert.Equal(DocumentContentWriteDisposition.Created, first.Disposition);
        Assert.Equal(DocumentContentWriteDisposition.Replay, replay.Disposition);
        Assert.Equal($"version-{first.RemoteId}", first.BoxVersionId);
        Assert.Equal(first.BoxVersionId, replay.BoxVersionId);
        // Flat layout: the case binding and the content. The occurrence and
        // version binding sidecars went with the folders that held them.
        Assert.Equal(2, uploadsAfterFirst);
        Assert.Equal(2, box.UploadCount);
    }

    [Fact]
    public async Task PersistedVersionReadAndReplayDoNotDriftToANewerIdenticalVersion()
    {
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var store = CreateStore(box);
        var content = Encoding.UTF8.GetBytes("version-pinned content");
        var hash = Sha256(content);
        var first = await store.StoreVersionAsync(Address(), content, hash, default);
        var persisted = Persisted(Address(), first);

        var newerVersion = box.AddVersion($"{CaseReference}/002 evidence.jpg", content);
        var replay = await store.StoreVersionAsync(persisted, content, hash, default);
        box.AddVersion(
            $"{CaseReference}/002 evidence.jpg",
            Encoding.UTF8.GetBytes("newer different current bytes"));
        await using var read = await store.OpenReadVersionAsync(
            persisted, hash, content.Length, default);

        Assert.Equal(content, await ReadAllAsync(read));
        Assert.Equal(first.BoxVersionId, replay.BoxVersionId);
        Assert.NotEqual(newerVersion, replay.BoxVersionId);
    }

    [Fact]
    public async Task RollbackRefusesToDeleteAFileThatAdvancedAfterThisWrite()
    {
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var store = CreateStore(box);
        var content = Encoding.UTF8.GetBytes("created version");
        await store.StoreVersionAsync(Address(), content, Sha256(content), default);
        box.AddVersion($"{CaseReference}/002 evidence.jpg", Encoding.UTF8.GetBytes("new owner version"));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            store.DeleteAsync(CaseId, CaseReference, VersionId, default));

        Assert.True(box.PathExists($"{CaseReference}/002 evidence.jpg"));
        Assert.Equal(0, box.DeleteCount);
    }

    [Fact]
    public async Task PersistedExactVersionCannotBeReadThroughAnotherCaseRoot()
    {
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var store = CreateStore(box);
        var content = Encoding.UTF8.GetBytes("case-owned content");
        var hash = Sha256(content);
        var written = await store.StoreVersionAsync(Address(), content, hash, default);
        var wrongCaseRoot = Persisted(Address(), written) with
        {
            CaseRootRemoteId = box.CreateFolderPath("QDOS39999")
        };

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            store.OpenReadVersionAsync(wrongCaseRoot, hash, content.Length, default));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            store.StoreVersionAsync(wrongCaseRoot, content, hash, default));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            store.ReadVersionsAsync([new(wrongCaseRoot, hash, content.Length)], default));
    }

    [Fact]
    public async Task SimultaneousIdenticalStoresConvergeOnOneBoxFileAndVersion()
    {
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var firstStore = CreateStore(box);
        var secondStore = CreateStore(box);
        var content = Encoding.UTF8.GetBytes("simultaneous custody content");
        var hash = Sha256(content);

        var results = await Task.WhenAll(
            firstStore.StoreVersionAsync(Address(), content, hash, CancellationToken.None),
            secondStore.StoreVersionAsync(Address(), content, hash, CancellationToken.None));

        Assert.Equal(2, box.UploadCount);
        Assert.Single(results.Select(value => value.RemoteId).Distinct());
        Assert.Single(results.Select(value => value.BoxVersionId).Distinct());
    }

    [Fact]
    public async Task StoreRejectsContentThatDoesNotMatchTheCustodyHash()
    {
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var store = CreateStore(box);
        var content = Encoding.UTF8.GetBytes("actual content");
        var wrongHash = Sha256(Encoding.UTF8.GetBytes("different content"));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            store.StoreVersionAsync(Address(), content, wrongHash, CancellationToken.None));
        Assert.Equal(1, box.UploadCount);
    }

    [Fact]
    public async Task BoxContentFailsClosedWithoutThePersistedCaseRoot()
    {
        var box = new InMemoryBox();
        var store = CreateStore(box);
        var content = Encoding.UTF8.GetBytes("content");

        await Assert.ThrowsAsync<ArgumentException>(() => store.StoreVersionAsync(
            Address() with { CaseRootRemoteId = null },
            content,
            Sha256(content),
            CancellationToken.None));
        Assert.Equal(0, box.RequestCount);
    }

    [Fact]
    public async Task OpenReadFailsClosedWhenStoredContentDoesNotVerify()
    {
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var store = CreateStore(box);
        var content = Encoding.UTF8.GetBytes("original content");
        var hash = Sha256(content);
        var written = await store.StoreVersionAsync(Address(), content, hash, CancellationToken.None);

        box.CorruptFile($"{CaseReference}/002 evidence.jpg");

        await Assert.ThrowsAsync<InvalidDataException>(() => store.OpenReadVersionAsync(
            Persisted(Address(), written), hash, content.Length, CancellationToken.None));
    }

    [Fact]
    public async Task UncommittedWriteRollbackIsTheOnlyDeleteAndNeverRemovesAcceptedCustody()
    {
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var store = CreateStore(box);
        var content = Encoding.UTF8.GetBytes("deletable content");
        var hash = Sha256(content);
        var created = await store.StoreVersionAsync(Address(), content, hash, CancellationToken.None);

        await store.DeleteAsync(CaseId, CaseReference, VersionId, CancellationToken.None);
        await store.DeleteAsync(CaseId, CaseReference, VersionId, CancellationToken.None);

        Assert.Equal(DocumentContentWriteDisposition.Created, created.Disposition);
        Assert.False(box.PathExists($"{CaseReference}/002 evidence.jpg"));
        Assert.True(box.PathExists($"{CaseReference}/pegasus-case-binding.json"));
        Assert.Equal(1, box.DeleteCount);
        await Assert.ThrowsAsync<FileNotFoundException>(() => store.OpenReadVersionAsync(
            Persisted(Address(), created), hash, content.Length, CancellationToken.None));

        var wrongMedia = new InMemoryBox();
        wrongMedia.BindCaseRoot();
        var wrongMediaStore = CreateStore(wrongMedia);
        await wrongMediaStore.StoreVersionAsync(Address(), content, hash, default);
        wrongMedia.SetMediaType(
            $"{CaseReference}/002 evidence.jpg",
            "application/octet-stream");
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            wrongMediaStore.StoreVersionAsync(Address(), content, hash, default));

        var lostResponse = new InMemoryBox();
        lostResponse.BindCaseRoot();
        lostResponse.LoseNextUploadResponseForName = "002 evidence.jpg";
        var lostResponseStore = CreateStore(lostResponse);
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            lostResponseStore.StoreVersionAsync(Address(), content, hash, default));
        var reconciled = await lostResponseStore.StoreVersionAsync(Address(), content, hash, default);
        Assert.Equal(DocumentContentWriteDisposition.Replay, reconciled.Disposition);
    }

    [Fact]
    public async Task ManagedReadUsesPersistedCaseRootWithoutListingApprovedRoot()
    {
        var box = new InMemoryBox();
        for (var i = 0; i < 1000; i++)
        {
            box.AddFolder(ApprovedRootId, $"aaaa-decoy-case-{i:D4}");
        }
        box.BindCaseRoot();
        var store = CreateStore(box);
        var content = Encoding.UTF8.GetBytes("second page content");
        var hash = Sha256(content);
        var written = await store.StoreVersionAsync(Address(), content, hash, CancellationToken.None);

        await using var stream = await store.OpenReadVersionAsync(
            Persisted(Address(), written), hash, content.Length, CancellationToken.None);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);

        Assert.Equal(content, buffer.ToArray());
        Assert.Equal(0, box.ApprovedRootListingCount);
    }

    [Fact]
    public async Task OneManagedReadCostsThreeBoxRoundTrips()
    {
        // It used to cost nine — the case folder resolved, the file
        // found, its metadata re-fetched, and its ancestry walked twice more
        // before a byte moved. The listing that finds the file already carries
        // its size and parent, and the SHA-256 below is the real guarantee.
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var store = CreateStore(box);
        var content = Encoding.UTF8.GetBytes("one read");
        var hash = Sha256(content);
        var written = await store.StoreVersionAsync(Address(), content, hash, CancellationToken.None);
        var before = box.RequestCount;

        await using var stream = await store.OpenReadVersionAsync(
            Persisted(Address(), written), hash, content.Length, CancellationToken.None);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);

        Assert.Equal(content, buffer.ToArray());
        Assert.Equal(3, box.RequestCount - before);
    }

    [Fact]
    public async Task ABatchReadResolvesTheCaseFolderOnceForEveryImage()
    {
        // Five photographs cost forty-five Box round trips, which is
        // the eighteen seconds the operator measured. Each read is now its
        // file and its content; the case folder's ancestry is walked by the
        // reads that start before the first proof is remembered.
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var store = CreateStore(box);
        var reads = new List<ManagedDocumentContentRead>();
        var expected = new List<byte[]>();
        for (var ordinal = 1; ordinal <= 5; ordinal++)
        {
            var content = Encoding.UTF8.GetBytes($"photograph {ordinal}");
            var address = Address(ordinal, $"photo-{ordinal}.jpg");
            var written = await store.StoreVersionAsync(address, content, Sha256(content), CancellationToken.None);
            reads.Add(new(Persisted(address, written), Sha256(content), content.Length));
            expected.Add(content);
        }
        var before = box.RequestCount;

        var contents = await store.ReadVersionsAsync(reads, CancellationToken.None);

        Assert.Equal(expected, contents.Select(item => item.ToArray()));
        Assert.InRange(box.RequestCount - before, 11, 14);

        // The archive is built from exactly these bytes in exactly this order,
        // so a batch that disagreed with the one-at-a-time reads would change
        // the export's contents. It must not: this is a latency fix.
        var oneAtATime = new List<byte[]>();
        foreach (var read in reads)
        {
            await using var single = await store.OpenReadVersionAsync(
                read.Address, read.ExpectedSha256, read.ExpectedLength, CancellationToken.None);
            using var buffer = new MemoryStream();
            await single.CopyToAsync(buffer);
            oneAtATime.Add(buffer.ToArray());
        }
        Assert.Equal(oneAtATime, contents.Select(item => item.ToArray()));
    }

    [Fact]
    public async Task ABatchReadStillFailsClosedOnContentThatDoesNotVerify()
    {
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var store = CreateStore(box);
        var content = Encoding.UTF8.GetBytes("batched content");
        var hash = Sha256(content);
        var written = await store.StoreVersionAsync(Address(), content, hash, CancellationToken.None);
        box.CorruptFile($"{CaseReference}/002 evidence.jpg");

        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadVersionsAsync(
            [new(Persisted(Address(), written), hash, content.Length)], CancellationToken.None));

        var missing = new InMemoryBox();
        missing.BindCaseRoot();
        var missingStore = CreateStore(missing);
        await Assert.ThrowsAsync<FileNotFoundException>(() => missingStore.ReadVersionsAsync(
            [new(Address() with { BoxFileId = "missing", BoxVersionId = "missing-version" }, hash, content.Length)], CancellationToken.None));
    }

    /// <summary>
    /// Box rate-limits per application, and a Case's gallery is the
    /// burst that finds the limit. A 429 on a read is Box saying "later", so
    /// the read waits and asks again instead of becoming a failed page element.
    /// </summary>
    [Fact]
    public async Task AThrottledSingleReadIsRetriedRatherThanFailed()
    {
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var store = CreateStore(box);
        var content = Encoding.UTF8.GetBytes("throttled content");
        var hash = Sha256(content);
        var written = await store.StoreVersionAsync(Address(), content, hash, CancellationToken.None);
        box.ThrottleNextDownloads = 1;

        await using var stream = await store.OpenReadVersionAsync(
            Persisted(Address(), written), hash, content.Length, CancellationToken.None);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);

        Assert.Equal(content, buffer.ToArray());
        Assert.Equal(1, box.ThrottledDownloadCount);
        Assert.Equal(0, box.ThrottleNextDownloads);
    }

    /// <summary>
    /// The retry is bounded: a limit that does not lift is the caller's answer,
    /// carrying the status the caller maps to "try again shortly".
    /// </summary>
    [Fact]
    public async Task AReadThatStaysThrottledFailsWithItsStatusAfterItsAttempts()
    {
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var store = CreateStore(box);
        var content = Encoding.UTF8.GetBytes("persistently throttled content");
        var hash = Sha256(content);
        var written = await store.StoreVersionAsync(Address(), content, hash, CancellationToken.None);
        box.ThrottleNextDownloads = 10;

        var failure = await Assert.ThrowsAsync<BoxThrottledException>(() =>
            store.OpenReadVersionAsync(
                Persisted(Address(), written), hash, content.Length, CancellationToken.None));

        Assert.Equal(HttpStatusCode.TooManyRequests, failure.StatusCode);
        Assert.Equal(3, box.ThrottledDownloadCount);
    }

    /// <summary>
    /// A read always asks for the file itself, but the Case folder's ancestry
    /// is remembered for ten minutes, so a repeat read is the file and its
    /// content only. After ten minutes the ancestry is walked again.
    /// </summary>
    [Fact]
    public async Task ARepeatReadRemembersTheCaseFolderForTenMinutes()
    {
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var clock = new MutableClock();
        var store = CreateStore(box, clock);
        var content = Encoding.UTF8.GetBytes("remembered folder");
        var hash = Sha256(content);
        var address = Persisted(Address(), await store.StoreVersionAsync(Address(), content, hash, default));

        Assert.Equal(3, await CountReadRequestsAsync(box, store, address, hash, content));
        Assert.Equal(2, await CountReadRequestsAsync(box, store, address, hash, content));
        clock.Advance(TimeSpan.FromMinutes(9));
        Assert.Equal(2, await CountReadRequestsAsync(box, store, address, hash, content));
        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(3, await CountReadRequestsAsync(box, store, address, hash, content));
    }

    /// <summary>
    /// The file's own trash state is checked on every read, remembered folder
    /// or not, and a refused read forgets the folder so the next read proves
    /// it again.
    /// </summary>
    [Fact]
    public async Task ATrashedFileIsRefusedAndTheRefusalForgetsTheFolder()
    {
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var store = CreateStore(box, new MutableClock());
        var content = Encoding.UTF8.GetBytes("trashed file");
        var hash = Sha256(content);
        var address = Persisted(Address(), await store.StoreVersionAsync(Address(), content, hash, default));
        Assert.Equal(3, await CountReadRequestsAsync(box, store, address, hash, content));

        box.SetTrashed(address.BoxFileId!, trashed: true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            store.OpenReadVersionAsync(address, hash, content.Length, default));

        box.SetTrashed(address.BoxFileId!, trashed: false);
        Assert.Equal(3, await CountReadRequestsAsync(box, store, address, hash, content));
    }

    /// <summary>
    /// The accepted delay: a Case folder moved out of the approved root outside
    /// Pegasus is still read while its proof is remembered, and refused once
    /// the memory lapses. A write is refused at once, because a write always
    /// proves the ancestry itself.
    /// </summary>
    [Fact]
    public async Task AFolderMovedOutOfTheRootIsReadUntilItsMemoryLapsesButNeverWritten()
    {
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var clock = new MutableClock();
        var store = CreateStore(box, clock);
        var content = Encoding.UTF8.GetBytes("moved folder");
        var hash = Sha256(content);
        var address = Persisted(Address(), await store.StoreVersionAsync(Address(), content, hash, default));
        Assert.Equal(3, await CountReadRequestsAsync(box, store, address, hash, content));

        box.MoveOutsideApprovedRoot(CaseRootId);

        await using (var remembered = await store.OpenReadVersionAsync(address, hash, content.Length, default))
        {
            Assert.Equal(content, await ReadAllAsync(remembered));
        }
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            store.StoreVersionAsync(address, content, hash, default));

        // The refused write forgot the folder, so the next read proves it too.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            store.OpenReadVersionAsync(address, hash, content.Length, default));
        clock.Advance(TimeSpan.FromMinutes(11));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            store.OpenReadVersionAsync(address, hash, content.Length, default));
    }

    /// <summary>
    /// Box's Retry-After reaches the retry: the read waits at least as long
    /// as Box asked, not just the shorter backoff.
    /// </summary>
    [Fact]
    public async Task AThrottledReadWaitsForBoxsRetryAfter()
    {
        var box = new InMemoryBox();
        box.BindCaseRoot();
        var store = CreateStore(box);
        var content = Encoding.UTF8.GetBytes("retry after");
        var hash = Sha256(content);
        var address = Persisted(Address(), await store.StoreVersionAsync(Address(), content, hash, default));
        box.ThrottleNextDownloads = 1;
        box.ThrottleRetryAfter = TimeSpan.FromSeconds(1);

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        await using var stream = await store.OpenReadVersionAsync(address, hash, content.Length, default);

        Assert.Equal(content, await ReadAllAsync(stream));
        Assert.Equal(1, box.ThrottledDownloadCount);
        Assert.True(
            System.Diagnostics.Stopwatch.GetElapsedTime(started) >= TimeSpan.FromMilliseconds(900),
            "The read retried before Box's Retry-After.");
    }

    [Fact]
    public void TheRetryWaitIsTheLongerOfRetryAfterAndBackoffAndNeverMoreThanTenSeconds()
    {
        var backoff = BoxDocumentContentStore.WaitBeforeRetry(new BoxThrottledException(null), 2);

        Assert.Equal(TimeSpan.FromMilliseconds(250), backoff);
        Assert.Equal(backoff, BoxDocumentContentStore.WaitBeforeRetry(
            new BoxThrottledException(TimeSpan.FromMilliseconds(10)), 2));
        Assert.Equal(TimeSpan.FromSeconds(3), BoxDocumentContentStore.WaitBeforeRetry(
            new BoxThrottledException(TimeSpan.FromSeconds(3)), 2));
        Assert.Equal(TimeSpan.FromSeconds(10), BoxDocumentContentStore.WaitBeforeRetry(
            new BoxThrottledException(TimeSpan.FromMinutes(5)), 2));
        Assert.Equal(backoff, BoxDocumentContentStore.WaitBeforeRetry(
            new HttpRequestException("unavailable", null, HttpStatusCode.ServiceUnavailable), 2));
    }

    /// <summary>
    /// A read waiting out a throttle holds no gate slot: four other reads can
    /// all be inside the gate together while it waits.
    /// </summary>
    [Fact]
    public async Task AReadWaitingOutAThrottleGivesItsGateSlotBack()
    {
        var attempts = 0;
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var throttled = BoxDocumentContentStore.ReadGatedWithRetryAsync(
            _ =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                {
                    waiting.TrySetResult();
                    throw new BoxThrottledException(TimeSpan.FromSeconds(3));
                }
                return Task.FromResult(0);
            },
            CancellationToken.None);
        await waiting.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var inside = 0;
        var allInside = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var others = Enumerable.Range(1, 4)
            .Select(value => BoxDocumentContentStore.ReadGatedWithRetryAsync(
                async token =>
                {
                    if (Interlocked.Increment(ref inside) == 4)
                    {
                        allInside.TrySetResult();
                    }
                    await allInside.Task.WaitAsync(token);
                    return value;
                },
                CancellationToken.None))
            .ToArray();

        // Well inside the three-second wait: had the throttled read kept its
        // slot, only three could be inside until it finished.
        await allInside.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var values = await Task.WhenAll(others);
        Assert.Equal([1, 2, 3, 4], values);
        Assert.Equal(0, await throttled);
        Assert.Equal(2, attempts);
    }

    private static async Task<int> CountReadRequestsAsync(
        InMemoryBox box,
        BoxDocumentContentStore store,
        ManagedDocumentContentAddress address,
        string hash,
        byte[] content)
    {
        var before = box.RequestCount;
        await using var stream = await store.OpenReadVersionAsync(address, hash, content.Length, default);
        Assert.Equal(content, await ReadAllAsync(stream));
        return box.RequestCount - before;
    }

    private static BoxDocumentContentStore CreateStore(InMemoryBox box, TimeProvider? clock = null) => new(
        new BoxContentClient(
            BoxCustodyOptions.Create(
                "https://api.box.com/2.0/",
                "https://upload.box.com/api/2.0/",
                ApprovedRootId,
                BoxConfigJson,
                "client-secret",
                "holding-folder"),
            new HttpClient(new InMemoryBoxHandler(box)),
            new StaticAuthorizationHeaderProvider(),
            clock ?? TimeProvider.System));

    private static ManagedDocumentContentAddress Address() =>
        Address(2, "evidence.jpg");

    private static ManagedDocumentContentAddress Address(int ordinal, string fileName) => new(
        CaseId,
        CaseReference,
        CaseRootId,
        OccurrenceId,
        ordinal,
        DocumentId,
        VersionId,
        1,
        DocumentSemanticRole.Image,
        fileName,
        "image/jpeg");

    private static ManagedDocumentContentAddress Persisted(
        ManagedDocumentContentAddress address,
        DocumentContentWriteResult write) =>
        address with { BoxFileId = write.RemoteId, BoxVersionId = write.BoxVersionId };

    private static string Sha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private sealed class MutableClock : TimeProvider
    {
        private DateTimeOffset now = new(2031, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan value) => now = now.Add(value);
    }

    private sealed class StaticAuthorizationHeaderProvider : IBoxAuthorizationHeaderProvider
    {
        public Task<string> GetAuthorizationHeaderAsync(CancellationToken cancellationToken) =>
            Task.FromResult("Bearer test-token");
    }

    /// <summary>Minimal stateful Box: folders, files, paged listing.</summary>
    private sealed class InMemoryBox
    {
        private sealed record Node(
            string Id,
            string Name,
            string Type,
            string? ParentId,
            string? MediaType = null);

        private readonly Dictionary<string, Node> nodes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, byte[]> fileBytes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> currentVersions = new(StringComparer.Ordinal);
        private readonly Dictionary<(string FileId, string VersionId), byte[]> versionBytes = [];
        private readonly Lock gate = new();
        private int nextId;

        public InMemoryBox() =>
            nodes[ApprovedRootId] = new Node(ApprovedRootId, "pegasus", "folder", null);

        public int UploadCount { get; private set; }
        public int DeleteCount { get; private set; }
        public int LargestItemsRequestOffset { get; private set; }
        public int ApprovedRootListingCount { get; private set; }
        /// <summary>
        /// Every Box round trip, whatever it is. The export's cost was
        /// never bytes — it was the number of requests made to move them.
        /// </summary>
        public int RequestCount { get; private set; }
        public string? LoseNextUploadResponseForName { get; set; }

        /// <summary>
        /// How many of the next content downloads Box answers with its rate
        /// limit.
        /// </summary>
        public int ThrottleNextDownloads { get; set; }

        /// <summary>How many downloads have been refused that way.</summary>
        public int ThrottledDownloadCount { get; private set; }

        /// <summary>The Retry-After a throttled download carries, if any.</summary>
        public TimeSpan? ThrottleRetryAfter { get; set; }

        private readonly HashSet<string> trashed = new(StringComparer.Ordinal);

        public void SetTrashed(string id, bool trashed)
        {
            lock (gate)
            {
                if (trashed)
                {
                    this.trashed.Add(id);
                }
                else
                {
                    this.trashed.Remove(id);
                }
            }
        }

        /// <summary>
        /// Moves a folder under a new folder that is not below the approved
        /// root, as someone could in Box itself.
        /// </summary>
        public void MoveOutsideApprovedRoot(string folderId)
        {
            lock (gate)
            {
                var outside = $"outside-{++nextId}";
                nodes[outside] = new Node(outside, "outside", "folder", null);
                nodes[folderId] = nodes[folderId] with { ParentId = outside };
            }
        }

        public string CreateFolderPath(string path)
        {
            var current = ApprovedRootId;
            foreach (var segment in path.Split('/'))
            {
                current = FindChild(current, segment, "folder")?.Id
                    ?? AddFolder(current, segment);
            }
            return current;
        }

        public string AddFolder(string parentId, string name)
        {
            var id = $"folder-{++nextId}";
            nodes[id] = new Node(id, name, "folder", parentId);
            return id;
        }

        public void BindCaseRoot()
        {
            if (FindChild(ApprovedRootId, CaseReference, "folder") is not null)
            {
                return;
            }
            const string root = CaseRootId;
            nodes[root] = new Node(root, CaseReference, "folder", ApprovedRootId);
            var bindingId = $"file-{++nextId}";
            nodes[bindingId] = new Node(
                bindingId, "pegasus-case-binding.json", "file", root, "application/json");
            fileBytes[bindingId] = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1,
                caseId = CaseId,
                caseReference = CaseReference
            });
            currentVersions[bindingId] = $"version-{bindingId}";
            versionBytes[(bindingId, currentVersions[bindingId])] = fileBytes[bindingId];
            UploadCount++;
        }

        public bool PathExists(string path)
        {
            var current = ApprovedRootId;
            foreach (var segment in path.Split('/'))
            {
                var child = FindChild(current, segment, "folder") ?? FindChild(current, segment, "file");
                if (child is null)
                {
                    return false;
                }
                current = child.Id;
            }
            return true;
        }

        public void CorruptFile(string path)
        {
            var fileId = RequireByPath(path);
            fileBytes[fileId] = Encoding.UTF8.GetBytes("corrupted");
            versionBytes[(fileId, currentVersions[fileId])] = fileBytes[fileId];
        }

        public string AddVersion(string path, byte[] content)
        {
            var fileId = RequireByPath(path);
            var versionId = $"version-{fileId}-{versionBytes.Keys.Count(key => key.FileId == fileId) + 1}";
            fileBytes[fileId] = content;
            currentVersions[fileId] = versionId;
            versionBytes[(fileId, versionId)] = content;
            return versionId;
        }

        public void SetMediaType(string path, string mediaType)
        {
            var fileId = RequireByPath(path);
            nodes[fileId] = nodes[fileId] with { MediaType = mediaType };
        }

        // A batch read now downloads concurrently, so the dictionaries below are
        // reached from several threads at once. The real Box is a server; this
        // one is a Dictionary, and needs saying so.
        public HttpResponseMessage Handle(HttpRequestMessage request)
        {
            lock (gate)
            {
                RequestCount++;
                return HandleCore(request);
            }
        }

        private HttpResponseMessage HandleCore(HttpRequestMessage request)
        {
            var path = request.RequestUri!.AbsolutePath;
            var query = HttpUtility.ParseQueryString(request.RequestUri.Query);

            if (request.Method == HttpMethod.Get && path.StartsWith("/2.0/folders/", StringComparison.Ordinal)
                && path.EndsWith("/items", StringComparison.Ordinal))
            {
                var folderId = path["/2.0/folders/".Length..^"/items".Length];
                if (folderId == ApprovedRootId)
                {
                    ApprovedRootListingCount++;
                }
                var limit = int.Parse(query["limit"] ?? "1000", CultureInfo.InvariantCulture);
                var offset = int.Parse(query["offset"] ?? "0", CultureInfo.InvariantCulture);
                LargestItemsRequestOffset = Math.Max(LargestItemsRequestOffset, offset);
                var children = nodes.Values
                    .Where(node => node.ParentId == folderId)
                    .OrderBy(node => node.Id, StringComparer.Ordinal)
                    .Skip(offset)
                    .Take(limit)
                    .Select(node => new
                    {
                        id = node.Id,
                        name = node.Name,
                        type = node.Type,
                        etag = "1",
                        file_version = node.Type == "file" ? new { id = currentVersions[node.Id] } : null,
                        size = node.Type == "file" ? fileBytes[node.Id].LongLength : (long?)null,
                        content_type = node.MediaType,
                        parent = node.ParentId is null ? null : new { id = node.ParentId }
                    });
                return Json(JsonSerializer.Serialize(new { entries = children }));
            }

            if (request.Method == HttpMethod.Get
                && (path.StartsWith("/2.0/folders/", StringComparison.Ordinal)
                    || path.StartsWith("/2.0/files/", StringComparison.Ordinal))
                && !path.EndsWith("/content", StringComparison.Ordinal))
            {
                var id = path[(path.LastIndexOf('/') + 1)..];
                if (!nodes.TryGetValue(id, out var node))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("missing") };
                }
                var parent = node.ParentId is null ? null : new { id = node.ParentId };
                return Json(JsonSerializer.Serialize(new
                {
                    id = node.Id,
                    name = node.Name,
                    type = node.Type,
                    etag = "1",
                    file_version = node.Type == "file" ? new { id = currentVersions[node.Id] } : null,
                    size = node.Type == "file" ? fileBytes[node.Id].LongLength : (long?)null,
                    content_type = node.MediaType,
                    parent,
                    trashed_at = trashed.Contains(node.Id) ? "2031-01-01T00:00:00Z" : null
                }));
            }

            if (request.Method == HttpMethod.Post && path == "/2.0/folders")
            {
                using var body = JsonDocument.Parse(ReadString(request.Content!));
                var name = body.RootElement.GetProperty("name").GetString()!;
                var parentId = body.RootElement.GetProperty("parent").GetProperty("id").GetString()!;
                var id = AddFolder(parentId, name);
                return Json(JsonSerializer.Serialize(new { id, name, type = "folder", etag = "1" }));
            }

            if (request.Method == HttpMethod.Post && path == "/api/2.0/files/content")
            {
                var multipart = Assert.IsType<MultipartFormDataContent>(request.Content);
                var attributes = ReadString(multipart.First(part =>
                    part.Headers.ContentDisposition?.Name?.Trim('"') == "attributes"));
                using var parsed = JsonDocument.Parse(attributes);
                var name = parsed.RootElement.GetProperty("name").GetString()!;
                var parentId = parsed.RootElement.GetProperty("parent").GetProperty("id").GetString()!;
                if (FindChild(parentId, name, "file") is not null)
                {
                    return new HttpResponseMessage(HttpStatusCode.Conflict)
                    {
                        Content = new StringContent(
                            """{"code":"item_name_in_use"}""",
                            Encoding.UTF8,
                            "application/json")
                    };
                }
                var bytes = multipart.First(part =>
                        part.Headers.ContentDisposition?.Name?.Trim('"') == "file")
                    .ReadAsByteArrayAsync().GetAwaiter().GetResult();
                var mediaType = multipart.First(part =>
                        part.Headers.ContentDisposition?.Name?.Trim('"') == "file")
                    .Headers.ContentType?.MediaType;
                var id = $"file-{++nextId}";
                nodes[id] = new Node(id, name, "file", parentId, mediaType);
                fileBytes[id] = bytes;
                currentVersions[id] = $"version-{id}";
                versionBytes[(id, currentVersions[id])] = bytes;
                UploadCount++;
                if (string.Equals(LoseNextUploadResponseForName, name, StringComparison.Ordinal))
                {
                    LoseNextUploadResponseForName = null;
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    {
                        Content = new StringContent("lost upload response")
                    };
                }
                return Json(JsonSerializer.Serialize(new
                {
                    entries = new[]
                    {
                        new
                        {
                            id,
                            name,
                            type = "file",
                            etag = "1",
                            file_version = new { id = $"version-{id}" }
                        }
                    }
                }));
            }

            if (request.Method == HttpMethod.Get && path.StartsWith("/2.0/files/", StringComparison.Ordinal)
                && path.EndsWith("/content", StringComparison.Ordinal))
            {
                if (ThrottleNextDownloads > 0)
                {
                    ThrottleNextDownloads--;
                    ThrottledDownloadCount++;
                    var throttled = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                    {
                        Content = new StringContent("""{"code":"rate_limit_exceeded"}""")
                    };
                    if (ThrottleRetryAfter is { } retryAfter)
                    {
                        throttled.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(retryAfter);
                    }
                    return throttled;
                }
                var id = path["/2.0/files/".Length..^"/content".Length];
                if (!currentVersions.TryGetValue(id, out var currentVersion))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }
                var versionId = query["version"] ?? currentVersion;
                if (!versionBytes.TryGetValue((id, versionId), out var bytes))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(bytes)
                };
            }

            if (request.Method == HttpMethod.Delete && path.StartsWith("/2.0/files/", StringComparison.Ordinal))
            {
                var id = path["/2.0/files/".Length..];
                nodes.Remove(id);
                fileBytes.Remove(id);
                currentVersions.Remove(id);
                foreach (var key in versionBytes.Keys.Where(key => key.FileId == id).ToArray())
                {
                    versionBytes.Remove(key);
                }
                DeleteCount++;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            throw new InvalidOperationException($"Unexpected Box request: {request.Method} {request.RequestUri}");
        }

        private Node? FindChild(string parentId, string name, string type) =>
            nodes.Values.SingleOrDefault(node =>
                node.ParentId == parentId && node.Name == name && node.Type == type);

        private string RequireByPath(string path)
        {
            var current = ApprovedRootId;
            foreach (var segment in path.Split('/'))
            {
                current = (FindChild(current, segment, "folder") ?? FindChild(current, segment, "file"))?.Id
                    ?? throw new InvalidOperationException($"Missing Box path segment '{segment}'.");
            }
            return current;
        }

        private static string ReadString(HttpContent content) =>
            content.ReadAsStringAsync().GetAwaiter().GetResult();

        private static HttpResponseMessage Json(string body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    private sealed class InMemoryBoxHandler(InMemoryBox box) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(box.Handle(request));
    }

    private sealed class ChunkedReadStream(byte[] bytes, int maximumReadLength) : Stream
    {
        private int position;
        private bool disposed;

        public int ReadCount { get; private set; }
        public override bool CanRead => !disposed;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => bytes.LongLength;
        public override long Position
        {
            get => position;
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            disposed = true;
            base.Dispose(disposing);
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (position == bytes.Length)
            {
                return ValueTask.FromResult(0);
            }

            var count = Math.Min(Math.Min(buffer.Length, maximumReadLength), bytes.Length - position);
            bytes.AsSpan(position, count).CopyTo(buffer.Span);
            position += count;
            ReadCount++;
            return ValueTask.FromResult(count);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
