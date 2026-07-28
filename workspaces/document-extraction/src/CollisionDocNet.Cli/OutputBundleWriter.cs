using System.Text.Json;
using CollisionDocNet.Core;
using CollisionDocNet.Model;

namespace CollisionDocNet.Cli;

internal static class OutputBundleWriter
{
    public static async Task WriteAsync(string outputPath, ExtractionResult result,
        ICliFileSystem fileSystem, CancellationToken cancellationToken)
    {
        string destination = CliPathPolicy.ResolveNewOutput(outputPath, fileSystem);
        string parent = fileSystem.GetDirectoryName(destination) ?? throw new IOException("The output path has no parent directory.");
        string staging = fileSystem.GetFullPath(fileSystem.Combine(parent,
            $".{fileSystem.GetFileName(destination)}.{fileSystem.GetRandomFileName()}.tmp"));
        CliPathPolicy.RequireDirectChild(parent, staging, fileSystem);
        fileSystem.CreateDirectory(staging);
        bool moved = false;
        try
        {
            var assets = new List<ReviewAsset>(result.Assets.Length);
            CollectAssets(result, assets);
            var files = new List<BundleAssetFile>(assets.Count);
            if (assets.Count > 0)
            {
                string assetsDirectory = fileSystem.Combine(staging, "assets");
                fileSystem.CreateDirectory(assetsDirectory);
                var stableIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (ReviewAsset asset in assets)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!stableIds.Add(asset.StableId))
                    {
                        throw new InvalidDataException(
                            $"The extraction boundary supplied duplicate bundle asset stable ID '{asset.StableId}'.");
                    }
                    if (asset.Kind != "image" || asset.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) != true)
                    {
                        throw new InvalidDataException("The extraction boundary supplied a non-image payload asset.");
                    }
                    string relativePath = $"assets/{asset.StableId}{SafeExtension(asset.MediaType)}";
                    string assetPath = fileSystem.Combine(staging, relativePath.Replace('/', Path.DirectorySeparatorChar));
                    await WriteFileAsync(assetPath, asset.Content.AsMemory(), fileSystem, cancellationToken).ConfigureAwait(false);
                    Sha256Digest actual = await ComputeHashAsync(assetPath, fileSystem, cancellationToken).ConfigureAwait(false);
                    if (actual != asset.ContentHash)
                    {
                        throw new IOException("An asset failed post-write hash verification.");
                    }
                    files.Add(new(asset.StableId, relativePath, actual.Hex, asset.Length));
                }
            }

            using JsonDocument resultDocument = JsonDocument.Parse(ExtractionResultJson.SerializeToUtf8Bytes(result));
            var bundle = new BundleDocument("collisiondocnet-bundle/1", resultDocument.RootElement.Clone(), files);
            byte[] json = CliJson.SerializeBundle(bundle);
            await WriteFileAsync(fileSystem.Combine(staging, "result.json"), json, fileSystem, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            fileSystem.MoveDirectory(staging, destination);
            moved = true;
        }
        finally
        {
            if (!moved && fileSystem.DirectoryExists(staging))
            {
                CliPathPolicy.RequireDirectChild(parent, staging, fileSystem);
                fileSystem.DeleteDirectory(staging, recursive: true);
            }
        }
    }

    private static void CollectAssets(ExtractionResult result, List<ReviewAsset> assets)
    {
        assets.AddRange(result.Assets);
        foreach (ExtractionResult nested in result.NestedResults)
        {
            CollectAssets(nested, assets);
        }
    }

    private static async Task WriteFileAsync(string path, ReadOnlyMemory<byte> bytes,
        ICliFileSystem fileSystem, CancellationToken cancellationToken)
    {
        await using Stream stream = fileSystem.CreateNewFile(path);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<Sha256Digest> ComputeHashAsync(string path, ICliFileSystem fileSystem,
        CancellationToken cancellationToken)
    {
        await using Stream stream = fileSystem.OpenRead(path);
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(
            System.Security.Cryptography.HashAlgorithmName.SHA256);
        byte[] buffer = new byte[16 * 1024];
        while (true)
        {
            int read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            hash.AppendData(buffer, 0, read);
        }
        string hex = Convert.ToHexStringLower(hash.GetHashAndReset());
        if (!Sha256Digest.TryParse(hex, out Sha256Digest digest))
        {
            throw new IOException("A generated SHA-256 digest was not canonical.");
        }
        return digest;
    }

    private static string SafeExtension(string? mediaType) => mediaType?.ToLowerInvariant() switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/gif" => ".gif",
        "image/tiff" => ".tif",
        "image/bmp" => ".bmp",
        "image/webp" => ".webp",
        "image/x-icon" => ".ico",
        "image/wmf" => ".wmf",
        "image/emf" => ".emf",
        _ => ".bin",
    };
}

internal sealed record BundleDocument(string SchemaVersion, JsonElement Result, IReadOnlyList<BundleAssetFile> AssetFiles);
internal sealed record BundleAssetFile(string StableId, string Path, string Sha256, long Length);
