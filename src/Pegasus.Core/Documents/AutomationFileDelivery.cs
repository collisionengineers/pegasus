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
/// Renders an image for delivery. Answers <c>null</c> when the bytes cannot be
/// rendered within the bounds (not a decodable image, too many pixels, or no
/// quality fits the budget), so the caller falls back to the metadata and the
/// authenticated content URL. A failed rendering is never an error.
/// </summary>
public interface IRenderImageForDelivery
{
    Task<DeliveryImage?> RenderAsync(
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
