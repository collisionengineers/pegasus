using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Custody;
using Pegasus.Core.Intake;

namespace Pegasus.IntegrationTests;

[Collection(LocalDbFixtureDefinition.Name)]
public sealed class LocalCaseCustodyAtomicWriteTests
{
    [Fact]
    public async Task CancelledRootMetadataWriteLeavesNoImmutableDestinationAndRetrySucceeds()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var custody = scope.ServiceProvider.GetRequiredService<ICaseCustody>();
        var caseId = Guid.NewGuid();
        const string caseReference = "QDOS31001";
        const string operationKey = "custody-root:atomic-retry";
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            custody.CreateCaseRootAsync(
                caseId,
                caseReference,
                operationKey,
                cancellationSource.Token));

        var caseDirectory = Path.Combine(
            factory.ArtifactDirectory,
            "custody",
            "cases",
            caseId.ToString("N"));
        var destination = Path.Combine(caseDirectory, ".pegasus-case.json");
        Assert.False(File.Exists(destination));
        Assert.Empty(Directory.EnumerateFiles(caseDirectory, "*.tmp"));

        var root = await custody.CreateCaseRootAsync(
            caseId,
            caseReference,
            operationKey,
            CancellationToken.None);

        Assert.Equal(caseReference, root.Reference);
        Assert.True(File.Exists(destination));
        Assert.Empty(Directory.EnumerateFiles(caseDirectory, "*.tmp"));
    }

    [Fact]
    public async Task CancellationDuringContentWriteLeavesNoImmutableDestinationAndRetrySucceeds()
    {
        var content = "complete immutable custody content"u8.ToArray();
        using var cancellationSource = new CancellationTokenSource();
        var artifactStore = new CancelOnFirstReadArtifactStore(content, cancellationSource);
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            localIntakeEnabled: true,
            artifactStore: artifactStore);
        await using var scope = factory.Services.CreateAsyncScope();
        var custody = scope.ServiceProvider.GetRequiredService<ICaseCustody>();
        var caseId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var expectedHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var root = await custody.CreateCaseRootAsync(
            caseId,
            "QDOS31002",
            "custody-root:content-retry",
            CancellationToken.None);
        var source = new IntakeSourceCustodyReference(
            receiptId,
            "source.eml",
            "message/rfc822",
            expectedHash,
            CancelOnFirstReadArtifactStore.SourceObjectKey);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            custody.RetainAcceptedIntakeSourceAsync(
                root,
                source,
                "custody-content:atomic-retry",
                cancellationSource.Token));

        var contentDirectory = Path.Combine(
            factory.ArtifactDirectory,
            "custody",
            "cases",
            caseId.ToString("N"),
            "documents",
            receiptId.ToString("N"),
            expectedHash);
        var destination = Path.Combine(contentDirectory, "content");
        Assert.False(File.Exists(destination));
        Assert.Empty(Directory.EnumerateFiles(contentDirectory, "*.tmp"));

        var retained = await custody.RetainAcceptedIntakeSourceAsync(
            root,
            source,
            "custody-content:atomic-retry",
            CancellationToken.None);

        Assert.Equal(expectedHash, retained.ContentHash);
        Assert.Equal(content, await File.ReadAllBytesAsync(destination));
        Assert.Empty(Directory.EnumerateFiles(contentDirectory, "*.tmp"));
    }

    [Fact]
    public async Task LaterAuditFolderReusesAuthoritativeRootWithoutRelabelingItsCreationOperation()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var custody = scope.ServiceProvider.GetRequiredService<ICaseCustody>();
        var caseId = Guid.NewGuid();
        const string caseReference = "QDOS31003";
        var originalOperationKey = $"case-custody:{caseId:N}:root";
        var auditOperationKey = $"audit-custody:{caseId:N}";
        var created = await custody.CreateCaseRootAsync(
            caseId,
            caseReference,
            originalOperationKey,
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            custody.CreateCaseRootAsync(
                caseId,
                caseReference,
                $"{auditOperationKey}:root",
                CancellationToken.None));

        var existing = await custody.GetExistingCaseRootAsync(
            caseId,
            caseReference,
            CancellationToken.None);
        var auditFolder = await custody.CreateAuditReferenceFolderAsync(
            existing,
            "a.QDOS31003",
            $"{auditOperationKey}:audit",
            CancellationToken.None);

        Assert.Equal(created, existing);
        Assert.StartsWith($"{created.RemoteId}/audit/", auditFolder, StringComparison.Ordinal);
        Assert.True(Directory.Exists(Path.Combine(
            factory.ArtifactDirectory,
            "custody",
            auditFolder.Replace('/', Path.DirectorySeparatorChar))));
    }

    private sealed class CancelOnFirstReadArtifactStore(
        ReadOnlyMemory<byte> content,
        CancellationTokenSource cancellationSource) : IIntakeArtifactStore
    {
        public const string SourceObjectKey = "custody-source";
        private int readCount;

        public Task<string> StoreAsync(
            string contentHash,
            ReadOnlyMemory<byte> value,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The atomic custody test only reads its retained source.");

        public Task<ReadOnlyMemory<byte>?> ReadAsync(
            string storageKey,
            CancellationToken cancellationToken)
        {
            Assert.Equal(SourceObjectKey, storageKey);
            if (Interlocked.Increment(ref readCount) == 1)
            {
                cancellationSource.Cancel();
            }

            return Task.FromResult<ReadOnlyMemory<byte>?>(content);
        }
    }
}
