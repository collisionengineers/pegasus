using System.Globalization;
using System.Text;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Pegasus.Core.Documents;

namespace Pegasus.Web.Mcp;

/// <summary>
/// What a download tool hands back: the content blocks the client shows its
/// model, and whether the file's content was among them.
/// </summary>
internal sealed record AutomationFileDelivery(
    bool ContentIncluded,
    string? Notice,
    IReadOnlyList<ContentBlock> Blocks);

/// <summary>
/// How the Automation download tools deliver a file's content as native MCP
/// content rather than as base64 text. An MCP client renders an <c>image</c>
/// block and reads a <c>text</c> block; it cannot do anything with a base64
/// string, and Claude's hosted connector refuses a binary embedded resource.
/// So a photograph arrives as one JPEG image block re-encoded to the byte
/// budget, a PDF as its page text, and a small text file as its text. Anything
/// else, and any file past the custody bound, arrives as metadata plus the
/// authenticated content URL. The structured result always carries the file's
/// identity, size and SHA-256, so a scripted caller loses nothing.
/// </summary>
internal static class AutomationFileContent
{
    /// <summary>
    /// The default byte budget for inline content. Claude.ai and Claude Desktop
    /// cap a tool result at roughly 150k characters, and base64 costs a third
    /// again, so 100 KiB of image fits with room for the text around it.
    /// </summary>
    public const int DefaultInlineBytes = 100 * 1024;

    /// <summary>The longest edge Claude reads a photograph at without resizing it again.</summary>
    public const int LongestEdge = 1568;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Zero selects the default; otherwise 1 to the custody bound.</summary>
    public static int NormalizeInlineLimit(int maxInlineBytes)
    {
        var inlineLimit = maxInlineBytes == 0 ? DefaultInlineBytes : maxInlineBytes;
        if (inlineLimit is < 1 or > AutomationMcpErrors.MaximumDocumentBytes)
        {
            throw new McpException(
                $"maxInlineBytes must be between 1 and {AutomationMcpErrors.MaximumDocumentBytes}.");
        }
        return inlineLimit;
    }

    /// <summary>
    /// Whether the content is worth reading for inline delivery: an image or a
    /// PDF within the custody bound (both are re-rendered to the budget), or a
    /// text file that already fits it. A metadata-only answer reads no bytes.
    /// </summary>
    public static bool CanDeliverInline(string mediaType, long contentLength, int inlineLimit) =>
        contentLength > 0
        && contentLength <= AutomationMcpErrors.MaximumDocumentBytes
        && (IsImage(mediaType) || IsPdf(mediaType) || (IsText(mediaType) && contentLength <= inlineLimit));

    public static async Task<AutomationFileDelivery> DeliverAsync(
        IRenderImageForDelivery images,
        IExtractPdfPageText pdfText,
        string fileName,
        string mediaType,
        long contentLength,
        string sha256,
        ReadOnlyMemory<byte> content,
        int inlineLimit,
        string contentUrl,
        CancellationToken cancellationToken)
    {
        var summary = Summary(fileName, mediaType, contentLength, sha256);
        if (IsImage(mediaType))
        {
            var rendered = await images.RenderAsync(content, LongestEdge, inlineLimit, cancellationToken);
            if (rendered is not null)
            {
                var caption = rendered.Downscaled
                    ? $"{summary} Shown at {rendered.Width}x{rendered.Height}; the original is unchanged at contentUrl."
                    : $"{summary} Shown at {rendered.Width}x{rendered.Height}.";
                return new AutomationFileDelivery(
                    true,
                    null,
                    [
                        new TextContentBlock { Text = caption },
                        ImageContentBlock.FromBytes(rendered.Content, rendered.MediaType)
                    ]);
            }
            return Withheld(
                summary,
                $"The image could not be rendered within {inlineLimit} bytes; fetch contentUrl with this bearer token.",
                contentUrl);
        }

        if (IsPdf(mediaType))
        {
            var extracted = await pdfText.ExtractAsync(content, inlineLimit, cancellationToken);
            if (extracted is not null)
            {
                var blocks = new List<ContentBlock>(extracted.Pages.Count + 1)
                {
                    new TextContentBlock
                    {
                        Text = $"{summary} {extracted.PageCount} page(s); text follows per page. The PDF itself is at contentUrl."
                    }
                };
                foreach (var page in extracted.Pages)
                {
                    var heading = new StringBuilder()
                        .Append(CultureInfo.InvariantCulture, $"Page {page.Number} of {extracted.PageCount}");
                    if (page.NeedsOcr)
                    {
                        heading.Append(" (little readable text; this page needs OCR)");
                    }
                    if (page.Truncated)
                    {
                        heading.Append(" (truncated at the inline limit)");
                    }
                    blocks.Add(new TextContentBlock { Text = $"{heading}:\n{page.Text}" });
                }
                var notice = extracted.Pages.Count < extracted.PageCount
                    ? $"Only {extracted.Pages.Count} of {extracted.PageCount} pages fit the inline limit of {inlineLimit} characters; raise maxInlineBytes or fetch contentUrl."
                    : null;
                if (notice is not null)
                {
                    blocks.Add(new TextContentBlock { Text = notice });
                }
                return new AutomationFileDelivery(true, notice, blocks);
            }
            return Withheld(summary, "The PDF could not be read for text; fetch contentUrl with this bearer token.", contentUrl);
        }

        if (IsText(mediaType) && contentLength <= inlineLimit)
        {
            return new AutomationFileDelivery(
                true,
                null,
                [
                    new TextContentBlock { Text = summary },
                    new TextContentBlock { Text = Encoding.UTF8.GetString(content.Span) }
                ]);
        }

        return Withheld(
            summary,
            $"The content ({contentLength} bytes, {mediaType}) is not delivered inline; fetch contentUrl with this bearer token.",
            contentUrl);
    }

    /// <summary>The metadata-only answer, with the reason and where the bytes are.</summary>
    public static AutomationFileDelivery Withheld(
        string fileName,
        string mediaType,
        long contentLength,
        string sha256,
        string notice,
        string contentUrl) =>
        Withheld(Summary(fileName, mediaType, contentLength, sha256), notice, contentUrl);

    public static CallToolResult ToResult(AutomationFileDelivery delivery, object structured) => new()
    {
        Content = [.. delivery.Blocks],
        StructuredContent = JsonSerializer.SerializeToElement(structured, structured.GetType(), Json)
    };

    private static AutomationFileDelivery Withheld(string summary, string notice, string contentUrl) =>
        new(false, notice, [new TextContentBlock { Text = $"{summary} {notice} contentUrl: {contentUrl}" }]);

    private static string Summary(string fileName, string mediaType, long contentLength, string sha256) =>
        $"{fileName} ({mediaType}, {contentLength} bytes, SHA-256 {sha256}).";

    private static bool IsImage(string mediaType) => CaseDocumentThumbnails.IsThumbnailable(mediaType);

    private static bool IsPdf(string mediaType) =>
        string.Equals(mediaType, "application/pdf", StringComparison.OrdinalIgnoreCase);

    private static bool IsText(string mediaType) =>
        mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mediaType, "application/xml", StringComparison.OrdinalIgnoreCase);
}
