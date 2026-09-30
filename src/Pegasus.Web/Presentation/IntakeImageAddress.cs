using Pegasus.Core.Documents;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The addresses of a retained intake image, and when a browser may keep what
/// they answer.
/// </summary>
/// <remarks>
/// A browser keeps an intake image for a week only when its address names
/// exactly what it is answered with: <c>v=</c> is the image's current content
/// hash and, for a tile, <c>prep=</c> and <c>renderer=</c> are the current
/// preparation version and renderer. Every other address is answered
/// <c>private, no-store</c>, so a changed image or crop is never served from a
/// browser's old copy.
/// </remarks>
public static class IntakeImageAddress
{
    /// <summary>What a current, content-named intake image address sends.</summary>
    public const string KeptCacheControl = "private, max-age=604800, immutable";

    /// <summary>What every other intake image response sends.</summary>
    public const string UncachedCacheControl = "private, no-store";

    /// <summary>The retained asset, named by its content hash when it is known.</summary>
    public static string Asset(Guid receiptId, Guid assetId, string? contentHash) =>
        $"/Received/{receiptId:D}/Asset/{assetId:D}{Query(contentHash)}";

    /// <summary>The receipt's own image, named by its source hash when it is known.</summary>
    public static string Image(Guid receiptId, string? contentHash) =>
        $"/Received/{receiptId:D}/Image{Query(contentHash)}";

    /// <summary>
    /// A pre-Case image's tile: the recorded crop and rotation, or the whole
    /// frame when none is recorded, named by the content hash, the preparation
    /// version (<see cref="Pegasus.Core.ImageIntake.PreCaseImagePreparation.TileVersion"/>)
    /// and the renderer.
    /// </summary>
    public static string Tile(
        Guid receiptId,
        Guid assetId,
        string? contentHash,
        long preparationVersion) =>
        $"/Received/{receiptId:D}/Asset/{assetId:D}?size={CaseDocumentThumbnails.ThumbSizeToken}"
        + (string.IsNullOrEmpty(contentHash) ? string.Empty : $"&v={Uri.EscapeDataString(contentHash)}")
        + $"&prep={preparationVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
        + $"&renderer={CaseDocumentThumbnails.RendererIdentity}";

    /// <summary>Whether a requested <c>v=</c> is the content hash being served.</summary>
    public static bool NamesContent(string? requested, string contentHash) =>
        !string.IsNullOrEmpty(requested)
        && contentHash is { Length: 64 }
        && contentHash.All(char.IsAsciiHexDigit)
        && string.Equals(requested, contentHash, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a tile address names the preparation version and renderer the
    /// rendering was drawn with.
    /// </summary>
    public static bool NamesPreparation(string? requestedPreparation, string? requestedRenderer, long preparationVersion) =>
        long.TryParse(
            requestedPreparation,
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out var requested)
        && requested == preparationVersion
        && string.Equals(requestedRenderer, CaseDocumentThumbnails.RendererIdentity, StringComparison.Ordinal);

    /// <summary>A strong validator: the bytes are exactly what the hash names.</summary>
    public static string ContentETag(string contentHash) =>
        $"\"{contentHash.ToLowerInvariant()}\"";

    /// <summary>The validator of a tile: its source, preparation and renderer.</summary>
    public static string TileETag(string contentHash, long preparationVersion) =>
        $"\"{contentHash.ToLowerInvariant()}-p{preparationVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)}-{CaseDocumentThumbnails.RendererIdentity}\"";

    private static string Query(string? contentHash) =>
        string.IsNullOrEmpty(contentHash) ? string.Empty : $"?v={Uri.EscapeDataString(contentHash)}";
}
