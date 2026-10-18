using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.CaseExport;
using Pegasus.Core.Documents;
using Pegasus.Infrastructure.Persistence;
using CaseDataHarness = Pegasus.IntegrationTests.CaseDataCompletenessPersistenceTests.CaseDataHarness;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Case export against SQL, with a recording content store standing in
/// for Box: the archive asks for its photographs one at a time, builds from
/// exactly those bytes, and the history record carries its fingerprint. The
/// store-level export test beside the corpus is not run by CI; synthetic
/// bytes only here.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseExportStorePersistenceTests
{
    [Fact]
    public async Task EachPhotographIsReadOnItsOwnInOrdinalOrderAndTheArchiveHoldsThem()
    {
        await using var harness = await Harness.CreateAsync(photographCount: 3);

        var export = await harness.ExportAsync(Guid.NewGuid().ToString("N"));

        var bundle = Assert.IsType<CaseExportBundle>(export?.Bundle);
        Assert.Equal<int>([1, 2, 3], harness.Content.Reads.Select(read => read.Address.OccurrenceOrdinal));
        Assert.Equal(1, harness.Content.MostOpenAtOnce);
        Assert.All(harness.Content.Reads, read => Assert.Equal("case-root-id", read.Address.CaseRootRemoteId));
        Assert.Equal(
            harness.Photographs.Select(photograph => photograph.Sha256),
            harness.Content.Reads.Select(read => read.ExpectedSha256));

        using var archive = new ZipArchive(new MemoryStream(bundle.Content.ToArray()), ZipArchiveMode.Read);
        Assert.Equal(
            [
                $"{harness.Reference}.json",
                "Images/001 photo-1.jpg",
                "Images/002 photo-2.jpg",
                "Images/003 photo-3.jpg"
            ],
            archive.Entries.Select(entry => entry.FullName));
        foreach (var photograph in harness.Photographs)
        {
            Assert.Equal(photograph.Content, ReadEntry(archive, $"Images/{photograph.Ordinal:000} {photograph.FileName}"));
        }
        Assert.Equal(
            Convert.ToHexStringLower(SHA256.HashData(bundle.Content.Span)),
            bundle.Sha256);
    }

    [Fact]
    public async Task TheHistoryRecordCarriesTheArchiveFingerprintAndEveryPhotographsIdentity()
    {
        await using var harness = await Harness.CreateAsync(photographCount: 2);
        var operationKey = Guid.NewGuid().ToString("N");

        var export = await harness.ExportAsync(operationKey);

        var bundle = Assert.IsType<CaseExportBundle>(export?.Bundle);
        var afterJson = Assert.Single(await harness.AfterJsonAsync(operationKey));
        Assert.NotNull(afterJson);
        using var recorded = JsonDocument.Parse(afterJson);
        Assert.Equal(bundle.Sha256, recorded.RootElement.GetProperty("bundleSha256").GetString());
        Assert.Equal(bundle.JsonSha256, recorded.RootElement.GetProperty("jsonSha256").GetString());
        var images = recorded.RootElement.GetProperty("images").EnumerateArray().ToArray();
        Assert.Equal(harness.Photographs.Count, images.Length);
        for (var index = 0; index < images.Length; index++)
        {
            var photograph = harness.Photographs[index];
            Assert.Equal(photograph.OccurrenceId, images[index].GetProperty("occurrenceId").GetGuid());
            Assert.Equal(photograph.DocumentId, images[index].GetProperty("documentId").GetGuid());
            Assert.Equal(photograph.VersionId, images[index].GetProperty("versionId").GetGuid());
            Assert.Equal(photograph.Sha256, images[index].GetProperty("sha256").GetString());
        }
    }

    [Fact]
    public async Task ARepeatedOperationKeyReturnsTheSameArchiveAndWritesNoSecondRecord()
    {
        await using var harness = await Harness.CreateAsync(photographCount: 2);
        var operationKey = Guid.NewGuid().ToString("N");

        var first = await harness.ExportAsync(operationKey);
        var replay = await harness.ExportAsync(operationKey);

        Assert.Equal(
            Assert.IsType<CaseExportBundle>(first?.Bundle).Sha256,
            Assert.IsType<CaseExportBundle>(replay?.Bundle).Sha256);
        Assert.Single(await harness.AfterJsonAsync(operationKey));
    }

    private static byte[] ReadEntry(ZipArchive archive, string name)
    {
        using var stream = archive.GetEntry(name)!.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private sealed record Photograph(
        int Ordinal,
        Guid OccurrenceId,
        Guid DocumentId,
        Guid VersionId,
        string FileName,
        byte[] Content)
    {
        public string Sha256 { get; } = Convert.ToHexStringLower(SHA256.HashData(Content));
    }

    /// <summary>
    /// Answers each read from the seeded bytes, and notes how many reads were
    /// open at the same moment. It holds no other copy of a photograph.
    /// </summary>
    private sealed class RecordingContentStore(IReadOnlyList<Photograph> photographs) : IDocumentContentStore
    {
        private int open;

        public List<ManagedDocumentContentRead> Reads { get; } = [];

        public int MostOpenAtOnce { get; private set; }

        public Task<Stream> OpenReadVersionAsync(
            ManagedDocumentContentAddress address,
            string expectedSha256,
            long expectedLength,
            CancellationToken cancellationToken)
        {
            Reads.Add(new(address, expectedSha256, expectedLength));
            MostOpenAtOnce = Math.Max(MostOpenAtOnce, ++open);
            var content = photographs.Single(photograph => photograph.Ordinal == address.OccurrenceOrdinal).Content;
            return Task.FromResult<Stream>(new OpenReadStream(content, () => open--));
        }

        public Task<Stream> OpenReadAsync(
            Guid caseId, string caseReference, Guid versionId, string expectedSha256,
            long expectedLength, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The export must read a managed version by its address.");

        public Task StoreAsync(
            Guid caseId, string caseReference, Guid versionId, ReadOnlyMemory<byte> content,
            string expectedSha256, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<DocumentContentWriteResult> StoreVersionAsync(
            ManagedDocumentContentAddress address,
            Stream content,
            long contentLength,
            string expectedSha256,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DeleteAsync(
            Guid caseId, string caseReference, Guid versionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class OpenReadStream(byte[] content, Action closed) : MemoryStream(content, writable: false)
    {
        private bool isClosed;

        protected override void Dispose(bool disposing)
        {
            if (disposing && !isClosed)
            {
                isClosed = true;
                closed();
            }
            base.Dispose(disposing);
        }
    }

    private sealed class Harness : IAsyncDisposable
    {
        private static readonly DateTimeOffset RecordedUtc = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
        private readonly CaseDataHarness caseData;
        private readonly EfCaseExportStore store;

        private Harness(
            CaseDataHarness caseData,
            string reference,
            IReadOnlyList<Photograph> photographs,
            RecordingContentStore content)
        {
            this.caseData = caseData;
            Reference = reference;
            Photographs = photographs;
            Content = content;
            store = new EfCaseExportStore(
                caseData.Factory,
                caseData.DataStore,
                new EfVehicleWorkflowStore(caseData.Factory, caseData.TimeProvider),
                new CaseExportImageReader(content),
                caseData.TimeProvider);
        }

        public string Reference { get; }

        public IReadOnlyList<Photograph> Photographs { get; }

        public RecordingContentStore Content { get; }

        public static async Task<Harness> CreateAsync(int photographCount)
        {
            var caseData = await CaseDataHarness.CreateAsync();
            try
            {
                var reference = (await caseData.GetRequiredDataAsync()).Identity.Reference;
                var photographs = await SeedPhotographsAsync(caseData, photographCount);
                return new(caseData, reference, photographs, new RecordingContentStore(photographs));
            }
            catch
            {
                await caseData.DisposeAsync();
                throw;
            }
        }

        public Task<ExportCaseBundleResult?> ExportAsync(string operationKey) => store.ExecuteAsync(
            new(caseData.CaseId, caseData.StaffActor, operationKey),
            CancellationToken.None);

        /// <summary>The <c>case_exported</c> records written under one operation key.</summary>
        public async Task<string?[]> AfterJsonAsync(string operationKey)
        {
            await using var context = await caseData.Factory.CreateDbContextAsync();
            return await context.ActionHistory.AsNoTracking()
                .Where(item => item.AggregateType == "Case"
                    && item.AggregateId == caseData.CaseId.ToString("D")
                    && item.EventKind == CaseExportPolicy.BundleExportedHistoryEventKind
                    && item.CorrelationId == operationKey)
                .Select(item => item.AfterJson)
                .ToArrayAsync();
        }

        public async ValueTask DisposeAsync() => await caseData.DisposeAsync();

        private static async Task<IReadOnlyList<Photograph>> SeedPhotographsAsync(
            CaseDataHarness caseData,
            int count)
        {
            await using var context = await caseData.Factory.CreateDbContextAsync();
            var caseEntity = await context.Cases.SingleAsync(item => item.Id == caseData.CaseId);
            caseEntity.CustodyRootRemoteId = "case-root-id";
            var photographs = new List<Photograph>(count);
            for (var ordinal = 1; ordinal <= count; ordinal++)
            {
                var photograph = new Photograph(
                    ordinal,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    $"photo-{ordinal}.jpg",
                    Encoding.UTF8.GetBytes($"synthetic photograph {ordinal}"));
                photographs.Add(photograph);
                context.AddRange(
                    new CaseDocumentEntity
                    {
                        Id = photograph.DocumentId,
                        CaseId = caseData.CaseId,
                        Ordinal = ordinal,
                        SourceOccurrenceIdentity = $"photo:{ordinal}"
                    },
                    new DocumentVersionEntity
                    {
                        Id = photograph.VersionId,
                        DocumentId = photograph.DocumentId,
                        Version = 1,
                        FileName = photograph.FileName,
                        MediaType = "image/jpeg",
                        ContentLength = photograph.Content.Length,
                        Sha256 = photograph.Sha256,
                        CustodyStatus = DocumentCustodyStatus.Confirmed,
                        BoxFileId = $"box-file-{ordinal}",
                        BoxVersionId = $"box-version-{ordinal}",
                        CreatedAtUtc = RecordedUtc,
                        CreatedBy = "Staff:test",
                        IsCurrent = true
                    },
                    new DocumentOccurrenceEntity
                    {
                        Id = photograph.OccurrenceId,
                        CaseId = caseData.CaseId,
                        DocumentId = photograph.DocumentId,
                        VersionId = photograph.VersionId,
                        Ordinal = ordinal,
                        SemanticRole = DocumentSemanticRole.Image,
                        Source = DocumentSource.StaffUpload,
                        SourceOccurrenceIdentity = $"photo:{ordinal}",
                        RecordedAtUtc = RecordedUtc,
                        OperationKey = $"seed-photo:{ordinal}"
                    });
            }
            await context.SaveChangesAsync();
            return photographs;
        }
    }
}
