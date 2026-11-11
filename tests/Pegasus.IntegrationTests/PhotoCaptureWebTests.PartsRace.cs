using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Documents;

namespace Pegasus.IntegrationTests;

public sealed partial class PhotoCaptureWebTests
{
    /// <summary>
    /// A retried commit that read the manifest and the part list before the
    /// first commit kept the item and cleared the parts finds a part gone when
    /// it opens it; it is answered as the first commit was, not as a different
    /// file the phone would give up on.
    /// </summary>
    [Fact]
    public async Task ARetryOverlappingTheCommitThatKeptTheItemIsAnsweredAsAReplay()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var stale = new StaleListingStaging.Switch();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                var original = services.Last(descriptor => descriptor.ServiceType == typeof(IDocumentUploadStaging));
                services.RemoveAll<IDocumentUploadStaging>();
                services.AddSingleton<IDocumentUploadStaging>(provider => new StaleListingStaging(
                    original.ImplementationFactory is { } create
                        ? (IDocumentUploadStaging)create(provider)
                        : (IDocumentUploadStaging)(original.ImplementationInstance
                            ?? ActivatorUtilities.CreateInstance(provider, original.ImplementationType!)),
                    stale));
            }));
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(baseFactory);
        var link = await IssueLinkAsync(baseFactory, caseId);
        var itemId = Guid.NewGuid();
        var file = TwoPartPdf();
        var query = $"{PaperworkQuery}&totalBytes={file.Length}";

        await PutPartsAsync(client, link.Token, itemId, query, file);
        using (var committed = await CommitPartsAsync(client, link.Token, itemId, query, file.Length, Sha256Of(file)))
        {
            Assert.Equal(HttpStatusCode.Created, committed.StatusCode);
            Assert.Equal("created", (await JsonAsync(committed)).GetProperty("write").GetString());
        }

        // The retry sees the manifest and the parts as they were before the
        // first commit cleared them; opening a part finds it gone.
        stale.On = true;
        using (var retried = await CommitPartsAsync(client, link.Token, itemId, query, file.Length, Sha256Of(file)))
        {
            Assert.Equal(HttpStatusCode.Created, retried.StatusCode);
            Assert.Equal("replayed", (await JsonAsync(retried)).GetProperty("write").GetString());
        }

        Assert.Single(await ItemsAsync(baseFactory, link.Capture.Id));
    }

    /// <summary>
    /// Staging that remembers each upload's manifest and part list and, once
    /// switched on, answers with them after the upload was cleared.
    /// </summary>
    private sealed class StaleListingStaging(IDocumentUploadStaging inner, StaleListingStaging.Switch stale) : IDocumentUploadStaging
    {
        private readonly ConcurrentDictionary<Guid, DocumentUploadManifest> manifests = new();
        private readonly ConcurrentDictionary<Guid, IReadOnlyList<int>> parts = new();

        public sealed class Switch
        {
            public bool On { get; set; }
        }

        public Task<bool> CreateManifestAsync(DocumentUploadManifest manifest, CancellationToken cancellationToken) =>
            inner.CreateManifestAsync(manifest, cancellationToken);

        public async Task<DocumentUploadManifest?> ReadManifestAsync(Guid uploadId, CancellationToken cancellationToken)
        {
            if (stale.On && manifests.TryGetValue(uploadId, out var remembered))
            {
                return remembered;
            }

            var manifest = await inner.ReadManifestAsync(uploadId, cancellationToken);
            if (manifest is not null)
            {
                manifests[uploadId] = manifest;
            }

            return manifest;
        }

        public Task<DocumentUploadPartWrite> CreatePartAsync(
            Guid uploadId,
            int partNumber,
            ReadOnlyMemory<byte> content,
            string sha256,
            CancellationToken cancellationToken) =>
            inner.CreatePartAsync(uploadId, partNumber, content, sha256, cancellationToken);

        public async Task<IReadOnlyList<int>> ListPartNumbersAsync(Guid uploadId, CancellationToken cancellationToken)
        {
            if (stale.On && parts.TryGetValue(uploadId, out var remembered))
            {
                return remembered;
            }

            var listed = await inner.ListPartNumbersAsync(uploadId, cancellationToken);
            if (listed.Count > 0)
            {
                parts[uploadId] = listed;
            }

            return listed;
        }

        public Task<ReadOnlyMemory<byte>?> ReadPartAsync(Guid uploadId, int partNumber, CancellationToken cancellationToken) =>
            inner.ReadPartAsync(uploadId, partNumber, cancellationToken);

        public Task<Stream?> OpenPartAsync(Guid uploadId, int partNumber, CancellationToken cancellationToken) =>
            inner.OpenPartAsync(uploadId, partNumber, cancellationToken);

        public Task DeleteUploadAsync(Guid uploadId, int partCount, CancellationToken cancellationToken) =>
            inner.DeleteUploadAsync(uploadId, partCount, cancellationToken);

        public Task<DocumentUploadStagedPage> ListAsync(int maximumItems, string? continuation, CancellationToken cancellationToken) =>
            inner.ListAsync(maximumItems, continuation, cancellationToken);

        public Task<bool> DeleteAsync(string name, DateTimeOffset createdAtOrBeforeUtc, CancellationToken cancellationToken) =>
            inner.DeleteAsync(name, createdAtOrBeforeUtc, cancellationToken);
    }
}
