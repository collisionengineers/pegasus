using System.Globalization;
using CollisionDocNet.Model;

namespace CollisionDocNet.Extraction;

/// <summary>Enforces EXT-API-003 at the single supported extraction boundary.</summary>
internal static class ImagePayloadPolicy
{
    internal static bool TryNormalize(ReviewAsset source, out ReviewAsset image)
    {
        string? mediaType = DetectMediaType(source.Content.AsSpan());
        if (mediaType is null)
        {
            image = null!;
            return false;
        }

        image = new ReviewAsset(source.StableId, "image", mediaType, source.OriginalName,
            source.Content.AsMemory(), source.SourceLocation);
        return true;
    }

    internal static bool IsClaimedImage(ReviewAsset asset) =>
        asset.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true ||
        asset.Kind is "image" or "inline-image" or "picture" or "drawing-data";

    internal static string Describe(ReviewAsset asset) => string.Create(CultureInfo.InvariantCulture,
        $"stableId={asset.StableId};kind={asset.Kind};mediaType={asset.MediaType ?? "unknown"};length={asset.Length};sha256={asset.ContentHash.Hex}");

    private static string? DetectMediaType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a })) return "image/png";
        if (bytes.StartsWith(new byte[] { 0xff, 0xd8, 0xff })) return "image/jpeg";
        if (bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8)) return "image/gif";
        if (bytes.StartsWith(new byte[] { 0x49, 0x49, 0x2a, 0x00 }) || bytes.StartsWith(new byte[] { 0x4d, 0x4d, 0x00, 0x2a })) return "image/tiff";
        if (bytes.StartsWith("BM"u8)) return "image/bmp";
        if (bytes.Length >= 12 && bytes.StartsWith("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8)) return "image/webp";
        if (bytes.StartsWith(new byte[] { 0x00, 0x00, 0x01, 0x00 })) return "image/x-icon";
        if (bytes.StartsWith(new byte[] { 0xd7, 0xcd, 0xc6, 0x9a })) return "image/wmf";
        if (bytes.Length >= 44 && bytes[..4].SequenceEqual(new byte[] { 0x01, 0x00, 0x00, 0x00 }) &&
            bytes[40..44].SequenceEqual(new byte[] { 0x20, 0x45, 0x4d, 0x46 })) return "image/emf";
        return null;
    }
}
