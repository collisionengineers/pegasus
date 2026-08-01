using System.Globalization;
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

        var listed = await store.ListStagedAsync(1, CancellationToken.None);
        var fetched = await store.GetStagedAsync(StorageKey, CancellationToken.None);

        var item = Assert.Single(listed);
        Assert.Equal(StorageKey, item.StorageKey);
        Assert.Equal(Hash, item.ContentHash);
        Assert.Equal(17, item.ContentLength);
        Assert.Equal(StagedArtifactDisposition.Unmatched, item.Disposition);
        Assert.NotNull(fetched);
        Assert.Equal(StagedArtifactDisposition.Unmatched, fetched.Disposition);
        Assert.Equal(BlobTraits.Metadata | BlobTraits.Tags, container.LastListTraits);
        Assert.Equal("staging/", container.LastListPrefix);
        Assert.Equal(0, malformed.DownloadCount);
        Assert.Equal(0, second.DownloadCount);
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
    public async Task DeleteRequiresCompletedMetadataAndTagUnderTheExpectedETag()
    {
        var etag = new ETag("\"etag-1\"");
        var pending = new StubBlobClient(
            StorageKey,
            Hash,
            17,
            FirstSeenAtUtc,
            StagedArtifactDisposition.Pending,
            etag);
        var completedKey = StorageKey.Replace("10213243", "20213243", StringComparison.Ordinal);
        var completed = new StubBlobClient(
            completedKey,
            Hash,
            17,
            FirstSeenAtUtc,
            StagedArtifactDisposition.Completed,
            etag);
        var store = CreateStore(new StubBlobContainerClient(pending, completed));

        var pendingDeleted = await store.DeleteCompletedStagedAsync(
            StorageKey,
            etag.ToString(),
            CancellationToken.None);
        var completedDeleted = await store.DeleteCompletedStagedAsync(
            completedKey,
            etag.ToString(),
            CancellationToken.None);

        Assert.False(pendingDeleted);
        Assert.Equal(0, pending.DeleteCount);
        Assert.True(completedDeleted);
        Assert.Equal(1, completed.DeleteCount);
        Assert.Equal(etag, completed.LastDeleteConditions?.IfMatch);
        Assert.Equal(
            "\"PegasusDisposition\" = 'Completed'",
            completed.LastDeleteConditions?.TagConditions);
        Assert.Equal(0, pending.DownloadCount);
        Assert.Equal(0, completed.DownloadCount);
    }

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

        public override BlobClient GetBlobClient(string blobName) => blobs[blobName];

        public override AsyncPageable<BlobItem> GetBlobsAsync(
            BlobTraits traits = BlobTraits.None,
            BlobStates states = BlobStates.None,
            string? prefix = null,
            CancellationToken cancellationToken = default)
        {
            LastListTraits = traits;
            LastListPrefix = prefix;
            var values = blobs.Values.Select(blob => blob.ToBlobItem()).ToArray();
            return AsyncPageable<BlobItem>.FromPages(
                [Page<BlobItem>.FromValues(values, null, new StubResponse())]);
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
        internal long ContentLength { get; }
        internal DateTimeOffset CreatedOn { get; }
        internal Dictionary<string, string> Metadata { get; private set; }
        internal Dictionary<string, string> Tags { get; private set; }
        internal ETag NextMetadataETag { get; init; } = new("\"etag-next\"");
        internal int DownloadCount { get; private set; }
        internal int DeleteCount { get; private set; }
        internal BlobRequestConditions? LastConditionalGetPropertiesConditions { get; private set; }
        internal BlobRequestConditions? LastSetMetadataConditions { get; private set; }
        internal BlobRequestConditions? LastSetTagsConditions { get; private set; }
        internal BlobRequestConditions? LastDeleteConditions { get; private set; }

        public override Task<Response<BlobProperties>> GetPropertiesAsync(
            BlobRequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            if (conditions is not null)
            {
                LastConditionalGetPropertiesConditions = conditions;
            }

            return Task.FromResult(Response.FromValue(
                BlobsModelFactory.BlobProperties(
                    lastModified: CreatedOn,
                    contentLength: ContentLength,
                    eTag: etag,
                    metadata: Metadata,
                    createdOn: CreatedOn),
                new StubResponse()));
        }

        public override Task<Response<GetBlobTagResult>> GetTagsAsync(
            BlobRequestConditions? conditions = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Response.FromValue(
                BlobsModelFactory.GetBlobTagResult(Tags),
                new StubResponse()));

        public override Task<Response<BlobInfo>> SetMetadataAsync(
            IDictionary<string, string> metadata,
            BlobRequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            LastSetMetadataConditions = conditions;
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
            LastSetTagsConditions = conditions;
            Tags = new Dictionary<string, string>(tags, StringComparer.Ordinal);
            return Task.FromResult<Response>(new StubResponse());
        }

        public override Task<Response<bool>> DeleteIfExistsAsync(
            DeleteSnapshotsOption snapshotsOption = default,
            BlobRequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            DeleteCount++;
            LastDeleteConditions = conditions;
            return Task.FromResult(Response.FromValue(true, new StubResponse()));
        }

        public override Task<Response<BlobDownloadResult>> DownloadContentAsync(
            CancellationToken cancellationToken)
        {
            DownloadCount++;
            throw new InvalidOperationException(
                "Inventory and disposition operations must not download blob bodies.");
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
