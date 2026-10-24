using System.Security.Cryptography;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Pegasus.Core.Documents;
using Pegasus.Infrastructure.Custody;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// The Blob staging adapter against an in-memory container: no-overwrite
/// uploads under <c>uploads/</c>, a repeated part compared by the SHA-256 in
/// its metadata without reading its body, and a delete conditioned on the
/// object being no younger than the cutoff.
/// </summary>
public sealed class AzureBlobDocumentUploadStagingTests
{
    private static readonly Guid UploadId = new("1c2d3e4f-5061-7283-94a5-b6c7d8e9f0a1");
    private static readonly DateTimeOffset Written = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task TheManifestAndEachPartAreUploadedOnceUnderUploads()
    {
        var container = new InMemoryContainer();
        var staging = new AzureBlobDocumentUploadStaging(container);
        var manifest = new DocumentUploadManifest(UploadId, "pegasus-automation", 10, new string('B', 64), Written);
        byte[] part = [7, 8, 9];
        byte[] other = [1, 1, 1];

        Assert.True(await staging.CreateManifestAsync(manifest, default));
        Assert.False(await staging.CreateManifestAsync(manifest with { Owner = "another-client" }, default));
        Assert.Equal(manifest, await staging.ReadManifestAsync(UploadId, default));
        Assert.Null(await staging.ReadManifestAsync(Guid.NewGuid(), default));

        Assert.Equal(DocumentUploadPartWrite.Created, await staging.CreatePartAsync(UploadId, 1, part, Sha256Of(part), default));
        var stored = container.Blobs[$"uploads/{UploadId:D}/0001"];
        var reads = stored.Downloads;
        Assert.Equal(DocumentUploadPartWrite.AlreadyStored, await staging.CreatePartAsync(UploadId, 1, part, Sha256Of(part), default));
        Assert.Equal(DocumentUploadPartWrite.Conflict, await staging.CreatePartAsync(UploadId, 1, other, Sha256Of(other), default));
        Assert.Equal(reads, stored.Downloads);
        Assert.Equal(part, stored.Body);
        Assert.Equal(Sha256Of(part), stored.Metadata["sha256"]);

        Assert.Equal([1], await staging.ListPartNumbersAsync(UploadId, default));
        Assert.Equal(part, (await staging.ReadPartAsync(UploadId, 1, default))!.Value.ToArray());
        Assert.Null(await staging.ReadPartAsync(UploadId, 2, default));
        Assert.All(container.Blobs.Keys, name => Assert.StartsWith("uploads/", name, StringComparison.Ordinal));
        Assert.All(container.Uploads, options => Assert.Equal(ETag.All, options.Conditions?.IfNoneMatch));
    }

    [Fact]
    public async Task TheListingPagesUnderUploadsAndADeleteIsConditionedOnAge()
    {
        var container = new InMemoryContainer();
        var staging = new AzureBlobDocumentUploadStaging(container);
        foreach (var partNumber in new[] { 1, 2, 3 })
        {
            byte[] part = [(byte)partNumber];
            await staging.CreatePartAsync(UploadId, partNumber, part, Sha256Of(part), default);
        }

        var prefix = $"uploads/{UploadId:D}/";
        var first = await staging.ListAsync(2, null, default);
        var second = await staging.ListAsync(2, first.NextContinuation, default);

        Assert.Equal("uploads/", container.LastListPrefix);
        Assert.Equal(new[] { prefix + "0001", prefix + "0002" }, first.Items.Select(item => item.Name));
        Assert.All(first.Items, item => Assert.Equal(Written, item.CreatedAtUtc));
        Assert.Equal(prefix + "0003", Assert.Single(second.Items).Name);
        Assert.Null(second.NextContinuation);

        Assert.False(await staging.DeleteAsync(prefix + "0001", Written.AddSeconds(-1), default));
        Assert.True(await staging.DeleteAsync(prefix + "0001", Written, default));
        Assert.Equal(Written, container.LastDeleteConditions!.IfUnmodifiedSince);
        Assert.False(container.Blobs.ContainsKey(prefix + "0001"));
        await Assert.ThrowsAsync<ArgumentException>(() => staging.DeleteAsync("staging/x/y", Written, default));
    }

    private static string Sha256Of(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    /// <summary>A container that keeps blobs in memory, each written at <see cref="Written"/>.</summary>
    private sealed class InMemoryContainer : BlobContainerClient
    {
        internal Dictionary<string, InMemoryBlob> Blobs { get; } = new(StringComparer.Ordinal);
        internal List<BlobUploadOptions> Uploads { get; } = [];
        internal string? LastListPrefix { get; private set; }
        internal BlobRequestConditions? LastDeleteConditions { get; set; }

        public override BlobClient GetBlobClient(string blobName) =>
            Blobs.TryGetValue(blobName, out var blob) ? blob : new InMemoryBlob(this, blobName);

        public override AsyncPageable<BlobItem> GetBlobsAsync(
            BlobTraits traits = BlobTraits.None,
            BlobStates states = BlobStates.None,
            string? prefix = null,
            CancellationToken cancellationToken = default)
        {
            LastListPrefix = prefix;
            return new Pageable(
                [.. Blobs.Values
                    .Where(blob => prefix is null || blob.Name.StartsWith(prefix, StringComparison.Ordinal))
                    .OrderBy(blob => blob.Name, StringComparer.Ordinal)
                    .Select(blob => BlobsModelFactory.BlobItem(
                        name: blob.Name,
                        properties: BlobsModelFactory.BlobItemProperties(accessTierInferred: false, createdOn: Written)))]);
        }

        /// <summary>Pages in name order from the continuation, the last name returned.</summary>
        private sealed class Pageable(BlobItem[] items) : AsyncPageable<BlobItem>
        {
            public override IAsyncEnumerable<Page<BlobItem>> AsPages(
                string? continuationToken = null,
                int? pageSizeHint = null)
            {
                var remaining = items
                    .Where(item => continuationToken is null
                        || string.CompareOrdinal(item.Name, continuationToken) > 0)
                    .ToArray();
                var values = remaining.Take(pageSizeHint ?? remaining.Length).ToArray();
                var next = values.Length < remaining.Length ? values[^1].Name : string.Empty;
                return AsyncPageable<BlobItem>.FromPages(
                    [Page<BlobItem>.FromValues(values, next, new OkResponse())]).AsPages();
            }
        }
    }

    private sealed class InMemoryBlob(InMemoryContainer container, string name) : BlobClient
    {
        public override string Name { get; } = name;
        internal byte[] Body { get; private set; } = [];
        internal IDictionary<string, string> Metadata { get; private set; } = new Dictionary<string, string>();
        internal int Downloads { get; private set; }

        public override async Task<Response<BlobContentInfo>> UploadAsync(
            Stream content,
            BlobUploadOptions options,
            CancellationToken cancellationToken = default)
        {
            container.Uploads.Add(options);
            if (container.Blobs.ContainsKey(Name))
            {
                throw new RequestFailedException(409, "The blob already exists.");
            }

            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            Body = buffer.ToArray();
            Metadata = options.Metadata ?? new Dictionary<string, string>();
            container.Blobs[Name] = this;
            return Response.FromValue(
                BlobsModelFactory.BlobContentInfo(new ETag("\"e\""), Written, null, null, 0),
                new OkResponse());
        }

        public override Task<Response<BlobProperties>> GetPropertiesAsync(
            BlobRequestConditions? conditions = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Response.FromValue(
                BlobsModelFactory.BlobProperties(
                    lastModified: Written,
                    createdOn: Written,
                    contentLength: Body.Length,
                    metadata: Metadata),
                (Response)new OkResponse()));

        public override Task<Response<BlobDownloadResult>> DownloadContentAsync(CancellationToken cancellationToken)
        {
            Downloads++;
            return container.Blobs.ContainsKey(Name)
                ? Task.FromResult(Response.FromValue(
                    BlobsModelFactory.BlobDownloadResult(BinaryData.FromBytes(Body)),
                    (Response)new OkResponse()))
                : throw new RequestFailedException(404, "Blob not found.");
        }

        public override Task<Response<bool>> DeleteIfExistsAsync(
            DeleteSnapshotsOption snapshotsOption = default,
            BlobRequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            container.LastDeleteConditions = conditions;
            if (!container.Blobs.ContainsKey(Name))
            {
                return Task.FromResult(Response.FromValue(false, (Response)new OkResponse()));
            }

            if (conditions?.IfUnmodifiedSince is { } since && Written > since)
            {
                throw new RequestFailedException(412, "The condition was not met.");
            }

            container.Blobs.Remove(Name);
            return Task.FromResult(Response.FromValue(true, (Response)new OkResponse()));
        }
    }

    private sealed class OkResponse : Response
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

        protected override bool TryGetHeaderValues(string name, out IEnumerable<string> values)
        {
            values = [];
            return false;
        }
    }
}
