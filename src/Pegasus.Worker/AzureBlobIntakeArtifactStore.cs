using System.Security.Cryptography;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Pegasus.Core.Intake;

namespace Pegasus.Worker;

internal sealed class AzureBlobIntakeArtifactStore : IIntakeArtifactStore
{
    private readonly BlobContainerClient container;

    public AzureBlobIntakeArtifactStore(IConfiguration configuration)
    {
        var serviceUri = configuration["IntakeStorage:ServiceUri"];
        BlobServiceClient service;
        if (!string.IsNullOrWhiteSpace(serviceUri))
        {
            service = new BlobServiceClient(new Uri(serviceUri, UriKind.Absolute), new DefaultAzureCredential());
        }
        else
        {
            var connectionString = configuration["IntakeStorage:ConnectionString"]
                ?? configuration.GetConnectionString("IntakeStorage")
                ?? configuration["AzureWebJobsStorage"]
                ?? throw new InvalidOperationException(
                    "IntakeStorage:ServiceUri or a Development storage connection is required.");
            service = new BlobServiceClient(connectionString);
        }

        container = service.GetBlobContainerClient("intake-staging");
    }

    public async Task<string> StoreAsync(
        string contentHash,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        var hash = NormalizeHash(contentHash);
        if (!string.Equals(
            Convert.ToHexString(SHA256.HashData(content.Span)),
            hash,
            StringComparison.Ordinal))
        {
            throw new IntakeArtifactIntegrityException();
        }

        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var storageKey = $"sha256/{hash[..2]}/{hash}";
        var blob = container.GetBlobClient(storageKey);
        using var stream = new MemoryStream(content.ToArray(), writable: false);
        try
        {
            await blob.UploadAsync(stream, new BlobUploadOptions
            {
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["sha256"] = hash
                }
            }, cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status is 409 or 412)
        {
            var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
            if (properties.Value.ContentLength != content.Length
                || !properties.Value.Metadata.TryGetValue("sha256", out var storedHash)
                || !string.Equals(storedHash, hash, StringComparison.Ordinal))
            {
                throw new IntakeArtifactIntegrityException();
            }
        }

        return storageKey;
    }

    public async Task<ReadOnlyMemory<byte>?> ReadAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var content = await container.GetBlobClient(storageKey)
                .DownloadContentAsync(cancellationToken: cancellationToken);
            return content.Value.Content.ToMemory();
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    private static string NormalizeHash(string contentHash)
    {
        if (contentHash.Length != 64 || !contentHash.All(char.IsAsciiHexDigit))
        {
            throw new IntakeArtifactIntegrityException();
        }

        return contentHash.ToUpperInvariant();
    }
}
