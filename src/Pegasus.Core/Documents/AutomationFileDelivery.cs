namespace Pegasus.Core.Documents;

/// <summary>
/// One image rendered for delivery to an MCP client: the verified bytes
/// decoded, EXIF orientation applied, the longest edge bounded and the result
/// encoded as JPEG at the highest quality that fits the byte budget. Custody
/// keeps the original; this is a view of it, as a gallery thumbnail is.
/// </summary>
public sealed record DeliveryImage(
    byte[] Content,
    string MediaType,
    int Width,
    int Height,
    bool Downscaled);

/// <summary>
/// Why <see cref="IRenderImageForDelivery"/> rendered no image. Only
/// <see cref="NoQualityFits"/> is a question of the byte budget.
/// </summary>
public enum DeliveryImageRefusal
{
    /// <summary>The bytes are not a decodable image.</summary>
    Undecodable,

    /// <summary>The image has more pixels than <see cref="DeliveryImageRendering.MaximumPixels"/>.</summary>
    Oversized,

    /// <summary>
    /// No size and quality down to the renderer's floor fits the byte budget;
    /// an image already under the floor's edge is tried at its own size.
    /// </summary>
    NoQualityFits
}

/// <summary>
/// What a rendering answered: the image, or why there is none.
/// </summary>
public sealed record DeliveryImageRendering
{
    /// <summary>The decoded-pixel bound past which an image is not rendered.</summary>
    public const long MaximumPixels = 40_000_000;

    private DeliveryImageRendering(DeliveryImage? image, DeliveryImageRefusal? refusal)
    {
        Image = image;
        Refusal = refusal;
    }

    /// <summary>The rendered image; null when the rendering was refused.</summary>
    public DeliveryImage? Image { get; }

    /// <summary>Why there is no image; null when there is one.</summary>
    public DeliveryImageRefusal? Refusal { get; }

    public static DeliveryImageRendering Rendered(DeliveryImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return new(image, null);
    }

    public static DeliveryImageRendering Refused(DeliveryImageRefusal refusal) => new(null, refusal);
}

/// <summary>
/// Renders an image for delivery. When the bytes cannot be rendered within
/// the bounds it answers why (<see cref="DeliveryImageRefusal"/>), so the
/// caller falls back to the metadata and the authenticated content URL with
/// that reason. A failed rendering is never an error.
/// </summary>
public interface IRenderImageForDelivery
{
    /// <summary>
    /// The fewest bytes any rendering takes, whatever the image. A smaller
    /// budget is <see cref="DeliveryImageRefusal.NoQualityFits"/> before the
    /// bytes are looked at, so a caller can know it without reading them.
    /// </summary>
    int MinimumBytes { get; }

    Task<DeliveryImageRendering> RenderAsync(
        ReadOnlyMemory<byte> content,
        int longestEdge,
        int maximumBytes,
        CancellationToken cancellationToken);
}

/// <summary>
/// The text of one PDF page. <see cref="NeedsOcr"/> says the page carries too
/// little readable text to be a text page, so a scanned page is named rather
/// than passed off as empty. <see cref="Truncated"/> says the character budget
/// ran out part-way through the page.
/// </summary>
public sealed record PdfPageText(
    int Number,
    string Text,
    bool NeedsOcr,
    bool Truncated);

/// <summary>
/// The text of a PDF, page by page, within a character budget. Pages past the
/// budget are omitted; <see cref="PageCount"/> is the document's whole count,
/// so the reader can tell how much it did not get.
/// </summary>
public sealed record PdfTextExtraction(
    int PageCount,
    IReadOnlyList<PdfPageText> Pages);

/// <summary>
/// Extracts page text from a PDF for delivery to an MCP client. Answers
/// <c>null</c> when the bytes are not a readable PDF.
/// </summary>
public interface IExtractPdfPageText
{
    Task<PdfTextExtraction?> ExtractAsync(
        ReadOnlyMemory<byte> content,
        int maximumCharacters,
        CancellationToken cancellationToken);
}
