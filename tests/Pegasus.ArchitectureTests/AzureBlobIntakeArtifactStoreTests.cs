using System.Globalization;
using System.Security.Cryptography;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Intake;
using Pegasus.Worker;

namespace Pegasus.ArchitectureTests;

public sealed class AzureBlobIntakeArtifactStoreTests
{
    private const string Hash =
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string StorageKey =
        "staging/10213243-5465-7687-98a9-bacbdcedfe0f/" + Hash;
    private static readonly DateTimeOffset FirstSeenAtUtc =
        new(2026, 7, 30, 8, 15, 0, TimeSpan.Zero);
    private static readonly byte[] PromotedContent = "promoted bytes"u8.ToArray();
    private static readonly string PromotedHash = Convert.ToHexString(SHA256.HashData(PromotedContent));
    private static readonly string PromotedStagingKey =
        $"staging/10213243-5465-7687-98a9-bacbdcedfe0f/{PromotedHash}";
    private static readonly string PromotedKey = $"sha256/{PromotedHash[..2]}/{PromotedHash}";

    [Fact]
    public async Task InventoryIsBoundedAndMalformedMetadataMapsToUnmatchedWithoutBodyDownload()
    {
        var malformed = new StubBlobClient(
            StorageKey,
            Hash,
            17,
            FirstSeenAtUtc,
            StagedArtifactDisposition.Pending,
            new ETag("\"etag-1\""));
        malformed.Metadata.Remove("contentlength");
        var second = new StubBlobClient(
            StorageKey.Replace("10213243", "20213243", StringComparison.Ordinal),
            Hash,
            23,
            FirstSeenAtUtc.AddMinutes(1),
            StagedArtifactDisposition.Pending,
            new ETag("\"etag-2\""));
        var container = new StubBlobContainerClient(malformed, second);
        var store = CreateStore(container);

        var listed = await store.ListStagedAsync(1, null, CancellationToken.None);
        var fetched = await store.GetStagedAsync(StorageKey, CancellationToken.None);

        var item = Assert.Single(listed.Items);
        Assert.Equal(StorageKey, item.StorageKey);
        Assert.Equal(Hash, item.ContentHash);
        Assert.Equal(17, item.ContentLength);
        Assert.Equal(StagedArtifactDisposition.Unmatched, item.Disposition);
        Assert.NotNull(fetched);
        Assert.Equal(StagedArtifactDisposition.Unmatched, fetched.Disposition);
        Assert.Equal(BlobTraits.Metadata | BlobTraits.Tags, container.LastListTraits);
        Assert.Equal("staging/", container.LastListPrefix);
        Assert.Equal(1, container.LastPageSizeHint);
        Assert.Equal(0, malformed.DownloadCount);
        Assert.Equal(0, second.DownloadCount);
    }

    [Fact]
    public async Task InventoryResumesFromTheContinuationAndEndsWithNone()
    {
        var blobs = Enumerable.Range(1, 3)
            .Select(index => StagedStub(
                $"{index}0213243",
                StagedArtifactDisposition.Pending,
                new ETag($"\"etag-{index}\"")))
            .ToArray();
        var container = new StubBlobContainerClient(blobs);
        var store = CreateStore(container);

        var first = await store.ListStagedAsync(2, null, CancellationToken.None);
        var second = await store.ListStagedAsync(2, first.NextContinuation, CancellationToken.None);

        Assert.Equal(new[] { blobs[0].Name, blobs[1].Name }, first.Items.Select(item => item.StorageKey));
        Assert.NotNull(first.NextContinuation);
        Assert.Equal(first.NextContinuation, container.LastContinuation);
        Assert.Equal(2, container.LastPageSizeHint);
        Assert.Equal(blobs[2].Name, Assert.Single(second.Items).StorageKey);
        Assert.Null(second.NextContinuation);
    }

    [Fact]
    public async Task DispositionTransitionUsesTheExpectedETagForMetadataAndTags()
    {
        var initialETag = new ETag("\"etag-1\"");
        var updatedETag = new ETag("\"etag-2\"");
        var blob = new StubBlobClient(
            StorageKey,
            Hash,
            17,
            FirstSeenAtUtc,
            StagedArtifactDisposition.Pending,
            initialETag)
        {
            NextMetadataETag = updatedETag
        };
        var store = CreateStore(new StubBlobContainerClient(blob));

        var updated = await store.TrySetStagedDispositionAsync(
            StorageKey,
            initialETag.ToString(),
            StagedArtifactDisposition.Failed,
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(StagedArtifactDisposition.Failed, updated.Disposition);
        Assert.Equal(updatedETag.ToString(), updated.ConcurrencyToken);
        Assert.Equal(initialETag, blob.LastConditionalGetPropertiesConditions?.IfMatch);
        Assert.Equal(initialETag, blob.LastSetMetadataConditions?.IfMatch);
        Assert.Equal(updatedETag, blob.LastSetTagsConditions?.IfMatch);
        Assert.Equal(
            "\"PegasusDisposition\" = 'Pending'",
            blob.LastSetTagsConditions?.TagConditions);
        Assert.Equal("Failed", blob.Metadata["disposition"]);
        Assert.Equal("Failed", blob.Tags["PegasusDisposition"]);
        Assert.Equal(0, blob.DownloadCount);
    }

    [Fact]
    public async Task DeleteRequiresTheExpectedTagUnderTheExpectedETagWithoutRereadingTheBlob()
    {
        var etag = new ETag("\"etag-1\"");
        var pending = StagedStub("10213243", StagedArtifactDisposition.Pending, etag);
        var completed = StagedStub("20213243", StagedArtifactDisposition.Completed, etag);
        var orphan = StagedStub("30213243", StagedArtifactDisposition.Orphan, etag);
        var unmatched = StagedStub("40213243", StagedArtifactDisposition.Unmatched, etag);
        var stale = StagedStub("50213243", StagedArtifactDisposition.Orphan, new ETag("\"etag-2\""));
        var store = CreateStore(new StubBlobContainerClient(pending, completed, orphan, unmatched, stale));

        var pendingDeleted = await store.DeleteStagedAsync(
            pending.Name, etag.ToString(), StagedArtifactDisposition.Completed, CancellationToken.None);
        var completedDeleted = await store.DeleteStagedAsync(
            completed.Name, etag.ToString(), StagedArtifactDisposition.Completed, CancellationToken.None);
        var orphanDeleted = await store.DeleteStagedAsync(
            orphan.Name, etag.ToString(), StagedArtifactDisposition.Orphan, CancellationToken.None);
        var unmatchedDeleted = await store.DeleteStagedAsync(
            unmatched.Name, etag.ToString(), StagedArtifactDisposition.Unmatched, CancellationToken.None);
        var staleDeleted = await store.DeleteStagedAsync(
            stale.Name, etag.ToString(), StagedArtifactDisposition.Orphan, CancellationToken.None);

        Assert.False(pendingDeleted);
        Assert.Equal(0, pending.DeleteCount);
        Assert.True(completedDeleted);
        Assert.Equal(1, completed.DeleteCount);
        Assert.Equal(etag, completed.LastDeleteConditions?.IfMatch);
        Assert.Equal(
            "\"PegasusDisposition\" = 'Completed'",
            completed.LastDeleteConditions?.TagConditions);
        Assert.True(orphanDeleted);
        Assert.Equal(1, orphan.DeleteCount);
        Assert.Equal(etag, orphan.LastDeleteConditions?.IfMatch);
        Assert.Equal(
            "\"PegasusDisposition\" = 'Orphan'",
            orphan.LastDeleteConditions?.TagConditions);
        Assert.True(unmatchedDeleted);
        Assert.Equal(1, unmatched.DeleteCount);
        Assert.Equal(
            "\"PegasusDisposition\" = 'Unmatched'",
            unmatched.LastDeleteConditions?.TagConditions);
        Assert.False(staleDeleted);
        Assert.Equal(0, stale.DeleteCount);
        Assert.All(
            new[] { pending, completed, orphan, unmatched, stale },
            blob =>
            {
                Assert.Equal(0, blob.DownloadCount);
                Assert.Equal(0, blob.ReadCount);
            });
    }

    [Fact]
    public async Task CompletingAStagedBlobTakesThreeOperationsUnderTheETagItRead()
    {
        var etag = new ETag("\"etag-1\"");
        var blob = StagedStub("10213243", StagedArtifactDisposition.Pending, etag);
        var store = CreateStore(new StubBlobContainerClient(blob));

        var completed = await store.TryCompleteStagedAsync(blob.Name, CancellationToken.None);

        Assert.True(completed);
        Assert.Equal(3, blob.Operations);
        Assert.Equal(etag, blob.LastSetTagsConditions?.IfMatch);
        Assert.Equal("Completed", blob.Tags["PegasusDisposition"]);
        Assert.Equal(1, blob.DeleteCount);
        Assert.Equal(etag, blob.LastDeleteConditions?.IfMatch);
        Assert.Equal(
            "\"PegasusDisposition\" = 'Completed'",
            blob.LastDeleteConditions?.TagConditions);
    }

    [Fact]
    public async Task CompletingAMissingStagedBlobStopsAfterOneOperation()
    {
        var blob = StagedStub("10213243", StagedArtifactDisposition.Pending, new ETag("\"etag-1\""));
        blob.Missing = true;
        var store = CreateStore(new StubBlobContainerClient(blob));

        Assert.False(await store.TryCompleteStagedAsync(blob.Name, CancellationToken.None));
        Assert.Equal(1, blob.Operations);
        Assert.Equal(0, blob.DeleteCount);
    }

    [Fact]
    public async Task CompletingAStagedBlobThatChangedBeforeTheDeleteLeavesIt()
    {
        var blob = StagedStub("10213243", StagedArtifactDisposition.Pending, new ETag("\"etag-1\""));
        blob.ETagBeforeDelete = new ETag("\"etag-2\"");
        var store = CreateStore(new StubBlobContainerClient(blob));

        Assert.False(await store.TryCompleteStagedAsync(blob.Name, CancellationToken.None));
        Assert.Equal(3, blob.Operations);
        Assert.Equal(0, blob.DeleteCount);
        // The Completed tag over Pending metadata lists as Unmatched for the sweep.
        Assert.Equal("Completed", blob.Tags["PegasusDisposition"]);
        Assert.Equal("Pending", blob.Metadata["disposition"]);
    }

    [Fact]
    public async Task ReadTranslatesAzureDependencyFailureToTheCorePortException()
    {
        var blob = new StubBlobClient(
            StorageKey,
            Hash,
            17,
            FirstSeenAtUtc,
            StagedArtifactDisposition.Pending,
            new ETag("\"etag-1\""))
        {
            DownloadFailure = new RequestFailedException(503, "Controlled unavailable dependency.")
        };
        var store = CreateStore(new StubBlobContainerClient(blob));

        var exception = await Assert.ThrowsAsync<IntakeDependencyUnavailableException>(
            () => store.ReadAsync(StorageKey, CancellationToken.None));

        Assert.IsType<RequestFailedException>(exception.InnerException);
        Assert.Equal(1, blob.DownloadCount);
    }

    [Fact]
    public async Task QuarantineStoreOfANewBlobUploadsOnceAndDoesNotReadItBack()
    {
        var content = "quarantined bytes"u8.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(content));
        var blob = StubFor($"sha256/{hash[..2]}/{hash}", hash, content);
        var store = CreateStore(new StubBlobContainerClient(blob));

        var artifact = await store.StoreStreamAsync(
            new MemoryStream(content), content.Length, CancellationToken.None);

        Assert.Equal(hash, artifact.ContentHash);
        Assert.Equal(1, blob.UploadCount);
        Assert.Equal(0, blob.DownloadCount);
    }

    [Fact]
    public async Task QuarantineStoreOfAnExistingBlobVerifiesTheStoredBytesOnce()
    {
        var content = "quarantined bytes"u8.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(content));
        var blob = StubFor($"sha256/{hash[..2]}/{hash}", hash, content);
        blob.UploadFailure = new RequestFailedException(409, "Blob exists.");
        var store = CreateStore(new StubBlobContainerClient(blob));

        await store.StoreStreamAsync(
            new MemoryStream(content), content.Length, CancellationToken.None);

        Assert.Equal(1, blob.UploadCount);
        Assert.Equal(1, blob.DownloadCount);
    }

    [Fact]
    public async Task PromoteStagedCopiesOnTheServiceWithoutUploadingOrDownloading()
    {
        var (staging, destination, store) = PromotionOf();
        destination.CopiedLength = PromotedContent.Length;

        var key = await store.PromoteStagedAsync(
            PromotedStagingKey, PromotedHash, PromotedContent, CancellationToken.None);

        Assert.Equal(PromotedKey, key);
        Assert.Equal(1, destination.CopyStartCount);
        Assert.Equal(staging.Uri, destination.LastCopySource);
        var options = Assert.IsType<BlobCopyFromUriOptions>(destination.LastCopyOptions);
        Assert.Equal(ETag.All, options.DestinationConditions?.IfNoneMatch);
        var metadata = Assert.Single(options.Metadata!);
        Assert.Equal("sha256", metadata.Key);
        Assert.Equal(PromotedHash, metadata.Value);
        Assert.Null(options.Tags);
        Assert.All(
            new[] { staging, destination },
            blob =>
            {
                Assert.Equal(0, blob.UploadCount);
                Assert.Equal(0, blob.DownloadCount);
            });
    }

    [Theory]
    [InlineData(409)]
    [InlineData(412)]
    public async Task PromoteStagedOfAnExistingDestinationVerifiesAndDoesNotOverwrite(int status)
    {
        var (staging, destination, store) = PromotionOf();
        destination.CopyStartFailure = new RequestFailedException(status, "Blob exists.");

        var key = await store.PromoteStagedAsync(
            PromotedStagingKey, PromotedHash, PromotedContent, CancellationToken.None);

        Assert.Equal(PromotedKey, key);
        Assert.Equal(1, destination.CopyStartCount);
        Assert.Equal(1, destination.DownloadCount);
        Assert.Equal(0, destination.UploadCount);
        Assert.Equal(0, destination.DeleteCount);
        Assert.Equal(0, staging.DownloadCount);
    }

    [Fact]
    public async Task PromoteStagedOfAnExistingDestinationWithDifferentBytesIsAnIntegrityFailure()
    {
        var staging = StubFor(PromotedStagingKey, PromotedHash, PromotedContent);
        var destination = StubFor(PromotedKey, PromotedHash, "different bytes"u8.ToArray());
        destination.CopyStartFailure = new RequestFailedException(412, "Blob exists.");
        var store = CreateStore(new StubBlobContainerClient(staging, destination));

        await Assert.ThrowsAsync<IntakeArtifactIntegrityException>(
            () => store.PromoteStagedAsync(
                PromotedStagingKey, PromotedHash, PromotedContent, CancellationToken.None));

        Assert.Equal(1, destination.DownloadCount);
        Assert.Equal(0, destination.DeleteCount);
    }

    [Fact]
    public async Task PromoteStagedWaitsForAPendingCopyOfTheSameHashBeforeVerifying()
    {
        var (_, destination, store) = PromotionOf();
        destination.CopyStartFailure = new RequestFailedException(409, "A copy is pending.");
        destination.CopyId = "copy-1";
        destination.CopyStatus = CopyStatus.Success;
        destination.PendingReads = 1;

        var key = await store.PromoteStagedAsync(
            PromotedStagingKey, PromotedHash, PromotedContent, CancellationToken.None);

        Assert.Equal(PromotedKey, key);
        Assert.Equal(0, destination.PendingReads);
        Assert.Equal(1, destination.DownloadCount);
        Assert.Equal(0, destination.DeleteCount);
    }

    [Theory]
    // A copy left Failed or Aborted by a crashed process, then our own copy that fails.
    [InlineData(409, CopyStatus.Failed)]
    [InlineData(412, CopyStatus.Aborted)]
    [InlineData(202, CopyStatus.Failed)]
    public async Task PromoteStagedDeletesTheEmptyDestinationOfAFailedCopyAndReportsDependencyUnavailable(
        int startStatus,
        CopyStatus status)
    {
        var (_, destination, store) = PromotionOf();
        destination.CopyStartFailure = startStatus == 202
            ? null
            : new RequestFailedException(startStatus, "Blob exists.");
        destination.CopyId = "copy-1";
        destination.CopyStatus = status;
        var etag = new ETag("\"etag-1\"");

        await Assert.ThrowsAsync<IntakeDependencyUnavailableException>(
            () => store.PromoteStagedAsync(
                PromotedStagingKey, PromotedHash, PromotedContent, CancellationToken.None));

        Assert.Equal(1, destination.DeleteCount);
        Assert.Equal(etag, destination.LastDeleteConditions?.IfMatch);
        Assert.Equal(0, destination.UploadCount);
        Assert.Equal(0, destination.DownloadCount);
    }

    [Fact]
    public async Task PromoteStagedRefusesAStagingKeyWhoseHashIsNotTheContentHash()
    {
        var (_, destination, store) = PromotionOf();

        await Assert.ThrowsAsync<IntakeArtifactIntegrityException>(
            () => store.PromoteStagedAsync(
                StorageKey, PromotedHash, PromotedContent, CancellationToken.None));

        Assert.Equal(0, destination.CopyStartCount);
    }

    [Fact]
    public async Task PromoteStagedOfAMissingStagedBlobIsAnIntegrityFailure()
    {
        var (_, destination, store) = PromotionOf();
        destination.CopyStartFailure = new RequestFailedException(404, "Blob not found.");

        await Assert.ThrowsAsync<IntakeArtifactIntegrityException>(
            () => store.PromoteStagedAsync(
                PromotedStagingKey, PromotedHash, PromotedContent, CancellationToken.None));

        Assert.Equal(0, destination.DownloadCount);
    }

    [Fact]
    public async Task PromoteStagedTranslatesAnyOtherServiceFaultToDependencyUnavailable()
    {
        var (_, destination, store) = PromotionOf();
        destination.CopyStartFailure = new RequestFailedException(503, "Controlled unavailable dependency.");

        var exception = await Assert.ThrowsAsync<IntakeDependencyUnavailableException>(
            () => store.PromoteStagedAsync(
                PromotedStagingKey, PromotedHash, PromotedContent, CancellationToken.None));

        Assert.IsType<RequestFailedException>(exception.InnerException);
    }

    private static (StubBlobClient Staging, StubBlobClient Destination, AzureBlobIntakeArtifactStore Store)
        PromotionOf()
    {
        var staging = StubFor(PromotedStagingKey, PromotedHash, PromotedContent);
        var destination = StubFor(PromotedKey, PromotedHash, PromotedContent);
        return (staging, destination, CreateStore(new StubBlobContainerClient(staging, destination)));
    }

    private static StubBlobClient StagedStub(
        string receiptPrefix,
        StagedArtifactDisposition disposition,
        ETag etag) =>
        new(
            StorageKey.Replace("10213243", receiptPrefix, StringComparison.Ordinal),
            Hash,
            17,
            FirstSeenAtUtc,
            disposition,
            etag);

    private static StubBlobClient StubFor(string key, string hash, byte[] content) =>
        new(
            key,
            hash,
            content.Length,
            FirstSeenAtUtc,
            StagedArtifactDisposition.Pending,
            new ETag("\"etag-1\""))
        {
            Body = content
        };

    private static AzureBlobIntakeArtifactStore CreateStore(
        BlobContainerClient container) =>
        new(
            container,
            allowCreateIfNotExists: false);

    private sealed class StubBlobContainerClient : BlobContainerClient
    {
        private readonly Dictionary<string, StubBlobClient> blobs;

        internal StubBlobContainerClient(params StubBlobClient[] blobs)
        {
            this.blobs = blobs.ToDictionary(blob => blob.Name, StringComparer.Ordinal);
        }

        internal BlobTraits? LastListTraits { get; private set; }
        internal string? LastListPrefix { get; private set; }
        internal string? LastContinuation { get; private set; }
        internal int? LastPageSizeHint { get; private set; }

        public override BlobClient GetBlobClient(string blobName) => blobs[blobName];

        public override AsyncPageable<BlobItem> GetBlobsAsync(
            BlobTraits traits = BlobTraits.None,
            BlobStates states = BlobStates.None,
            string? prefix = null,
            CancellationToken cancellationToken = default)
        {
            LastListTraits = traits;
            LastListPrefix = prefix;
            return new StubPageable(
                this,
                blobs.Values
                    .Select(blob => blob.ToBlobItem())
                    .OrderBy(blob => blob.Name, StringComparer.Ordinal)
                    .ToArray());
        }

        /// <summary>
        /// Lists in name order from the continuation (the last name returned),
        /// one page of the hinted size, ending with the service's empty marker.
        /// </summary>
        private sealed class StubPageable(
            StubBlobContainerClient owner,
            BlobItem[] items) : AsyncPageable<BlobItem>
        {
            public override IAsyncEnumerable<Page<BlobItem>> AsPages(
                string? continuationToken = null,
                int? pageSizeHint = null)
            {
                owner.LastContinuation = continuationToken;
                owner.LastPageSizeHint = pageSizeHint;
                var remaining = items
                    .Where(item => continuationToken is null
                        || string.CompareOrdinal(item.Name, continuationToken) > 0)
                    .ToArray();
                var values = remaining.Take(pageSizeHint ?? remaining.Length).ToArray();
                var next = values.Length < remaining.Length ? values[^1].Name : string.Empty;
                return AsyncPageable<BlobItem>.FromPages(
                    [Page<BlobItem>.FromValues(values, next, new StubResponse())]).AsPages();
            }
        }
    }

    private sealed class StubBlobClient : BlobClient
    {
        private ETag etag;

        internal StubBlobClient(
            string name,
            string hash,
            long contentLength,
            DateTimeOffset firstSeenAtUtc,
            StagedArtifactDisposition disposition,
            ETag etag)
        {
            Name = name;
            ContentLength = contentLength;
            CreatedOn = firstSeenAtUtc;
            this.etag = etag;
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sha256"] = hash,
                ["contentlength"] = contentLength.ToString(CultureInfo.InvariantCulture),
                ["firstseenatutc"] = firstSeenAtUtc.ToString("O", CultureInfo.InvariantCulture),
                ["disposition"] = disposition.ToString()
            };
            Tags = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PegasusDisposition"] = disposition.ToString()
            };
        }

        public override string Name { get; }
        public override Uri Uri => new($"https://account.blob.core.windows.net/container/{Name}");
        internal long ContentLength { get; }
        internal DateTimeOffset CreatedOn { get; }
        internal Dictionary<string, string> Metadata { get; private set; }
        internal Dictionary<string, string> Tags { get; private set; }
        internal ETag NextMetadataETag { get; init; } = new("\"etag-next\"");
        internal RequestFailedException? DownloadFailure { get; init; }
        internal int DownloadCount { get; private set; }
        internal int UploadCount { get; private set; }
        internal RequestFailedException? UploadFailure { get; set; }
        internal byte[] Body { get; init; } = [];
        internal int DeleteCount { get; private set; }
        internal int ReadCount { get; private set; }

        /// <summary>Every Blob operation sent to this blob.</summary>
        internal int Operations { get; private set; }

        /// <summary>Every operation answers 404, as for a blob already gone.</summary>
        internal bool Missing { get; set; }

        /// <summary>Another writer changes the blob just before a delete.</summary>
        internal ETag? ETagBeforeDelete { get; set; }

        /// <summary>
        /// What the service reports of the copy that wrote this blob. A blob
        /// that was never copied reads as Pending with no copy id.
        /// </summary>
        internal CopyStatus CopyStatus { get; set; }
        internal string? CopyId { get; set; }

        /// <summary>
        /// The reads that still find the copy running: they report Pending,
        /// and the blob is empty until they are used up.
        /// </summary>
        internal int PendingReads { get; set; }

        /// <summary>Refuses the start of a copy onto this blob.</summary>
        internal RequestFailedException? CopyStartFailure { get; set; }

        /// <summary>The length a copy onto this blob reports when it finishes.</summary>
        internal long CopiedLength { get; set; }

        internal int CopyStartCount { get; private set; }
        internal Uri? LastCopySource { get; private set; }
        internal BlobCopyFromUriOptions? LastCopyOptions { get; private set; }

        internal BlobRequestConditions? LastConditionalGetPropertiesConditions { get; private set; }
        internal BlobRequestConditions? LastSetMetadataConditions { get; private set; }
        internal BlobRequestConditions? LastSetTagsConditions { get; private set; }
        internal BlobRequestConditions? LastDeleteConditions { get; private set; }

        public override Task<Response<BlobProperties>> GetPropertiesAsync(
            BlobRequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            Operations++;
            ReadCount++;
            ThrowIfMissing();
            if (conditions is not null)
            {
                LastConditionalGetPropertiesConditions = conditions;
            }

            ThrowIfETagDiffers(conditions);
            var copyStatus = PendingReads > 0 ? CopyStatus.Pending : CopyStatus;
            if (PendingReads > 0)
            {
                PendingReads--;
            }

            return Task.FromResult(Response.FromValue(
                BlobsModelFactory.BlobProperties(
                    lastModified: CreatedOn,
                    contentLength: ContentLength,
                    eTag: etag,
                    metadata: Metadata,
                    createdOn: CreatedOn,
                    blobCopyStatus: copyStatus,
                    copyId: CopyId),
                new StubResponse()));
        }

        public override Task<Response<GetBlobTagResult>> GetTagsAsync(
            BlobRequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            Operations++;
            ReadCount++;
            ThrowIfMissing();
            return Task.FromResult(Response.FromValue(
                BlobsModelFactory.GetBlobTagResult(Tags),
                new StubResponse()));
        }

        public override Task<Response<BlobInfo>> SetMetadataAsync(
            IDictionary<string, string> metadata,
            BlobRequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            Operations++;
            ThrowIfMissing();
            LastSetMetadataConditions = conditions;
            ThrowIfETagDiffers(conditions);
            Metadata = new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
            etag = NextMetadataETag;
            return Task.FromResult(Response.FromValue(
                BlobsModelFactory.BlobInfo(etag, DateTimeOffset.UtcNow),
                new StubResponse()));
        }

        public override Task<Response> SetTagsAsync(
            IDictionary<string, string> tags,
            BlobRequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            Operations++;
            ThrowIfMissing();
            LastSetTagsConditions = conditions;
            ThrowIfETagDiffers(conditions);
            ThrowIfTagDiffers(conditions);
            Tags = new Dictionary<string, string>(tags, StringComparer.Ordinal);
            return Task.FromResult<Response>(new StubResponse());
        }

        public override Task<Response<bool>> DeleteIfExistsAsync(
            DeleteSnapshotsOption snapshotsOption = default,
            BlobRequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            Operations++;
            if (Missing)
            {
                return Task.FromResult(Response.FromValue(false, new StubResponse()));
            }

            if (ETagBeforeDelete is { } changed)
            {
                etag = changed;
            }

            LastDeleteConditions = conditions;
            ThrowIfETagDiffers(conditions);
            ThrowIfTagDiffers(conditions);
            DeleteCount++;
            return Task.FromResult(Response.FromValue(true, new StubResponse()));
        }

        private void ThrowIfMissing()
        {
            if (Missing)
            {
                throw new RequestFailedException(404, "Blob not found.");
            }
        }

        private void ThrowIfETagDiffers(BlobRequestConditions? conditions)
        {
            if (conditions?.IfMatch is { } expected && expected != etag)
            {
                throw new RequestFailedException(412, "ETag condition not met.");
            }
        }

        private void ThrowIfTagDiffers(BlobRequestConditions? conditions)
        {
            if (conditions?.TagConditions is { } tagCondition
                && !tagCondition.Contains($"'{Tags["PegasusDisposition"]}'", StringComparison.Ordinal))
            {
                throw new RequestFailedException(412, "Tag condition not met.");
            }
        }

        public override Task<Response<BlobDownloadResult>> DownloadContentAsync(
            CancellationToken cancellationToken)
        {
            Operations++;
            DownloadCount++;
            if (DownloadFailure is not null)
            {
                throw DownloadFailure;
            }
            throw new InvalidOperationException(
                "Inventory and disposition operations must not download blob bodies.");
        }

        public override Task<Response<BlobContentInfo>> UploadAsync(
            Stream content,
            BlobUploadOptions options,
            CancellationToken cancellationToken = default)
        {
            Operations++;
            UploadCount++;
            if (UploadFailure is not null)
            {
                throw UploadFailure;
            }

            return Task.FromResult(Response.FromValue(
                BlobsModelFactory.BlobContentInfo(etag, DateTimeOffset.UtcNow, null, null, 0),
                new StubResponse()));
        }

        public override Task<CopyFromUriOperation> StartCopyFromUriAsync(
            Uri source,
            BlobCopyFromUriOptions options,
            CancellationToken cancellationToken = default)
        {
            Operations++;
            CopyStartCount++;
            LastCopySource = source;
            LastCopyOptions = options;
            if (CopyStartFailure is not null)
            {
                throw CopyStartFailure;
            }

            return Task.FromResult<CopyFromUriOperation>(new StubCopyOperation(CopiedLength));
        }

        public override Task<Response<BlobDownloadStreamingResult>> DownloadStreamingAsync(
            BlobDownloadOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(StreamBody());

        public override Task<Response<BlobDownloadStreamingResult>> DownloadStreamingAsync(
            HttpRange range = default,
            BlobRequestConditions? conditions = null,
            bool rangeGetContentHash = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(StreamBody());

        private Response<BlobDownloadStreamingResult> StreamBody()
        {
            Operations++;
            DownloadCount++;
            var body = PendingReads > 0 ? Array.Empty<byte>() : Body;
            return Response.FromValue(
                BlobsModelFactory.BlobDownloadStreamingResult(
                    new MemoryStream(body),
                    BlobsModelFactory.BlobDownloadDetails(contentLength: body.Length)),
                new StubResponse());
        }

        internal BlobItem ToBlobItem() =>
            BlobsModelFactory.BlobItem(
                name: Name,
                properties: BlobsModelFactory.BlobItemProperties(
                    accessTierInferred: false,
                    contentLength: ContentLength,
                    eTag: etag,
                    createdOn: CreatedOn),
                metadata: Metadata,
                tags: Tags);
    }

    /// <summary>
    /// A finished copy; the service reports a failed or aborted one as
    /// finished too, with a length of zero.
    /// </summary>
    private sealed class StubCopyOperation(long length) : CopyFromUriOperation
    {
        public override ValueTask<Response<long>> WaitForCompletionAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Response.FromValue(length, new StubResponse()));
    }

    private sealed class StubResponse : Response
    {
        public override int Status => 200;
        public override string ReasonPhrase => "OK";
        public override Stream? ContentStream { get; set; }
        public override string ClientRequestId { get; set; } = Guid.NewGuid().ToString("D");

        public override void Dispose()
        {
        }

        protected override bool ContainsHeader(string name) => false;

        protected override IEnumerable<HttpHeader> EnumerateHeaders() => [];

        protected override bool TryGetHeader(string name, out string value)
        {
            value = string.Empty;
            return false;
        }

        protected override bool TryGetHeaderValues(
            string name,
            out IEnumerable<string> values)
        {
            values = [];
            return false;
        }
    }
}
