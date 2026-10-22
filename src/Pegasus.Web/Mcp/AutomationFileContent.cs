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
/// One file a batch download names, as its metadata gives it before any byte
/// is read, with the authenticated URL of its original bytes.
/// </summary>
internal sealed record AutomationFileItem(
    string FileName,
    string MediaType,
    long ContentLength,
    string Sha256,
    string ContentUrl);

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
            var rendering = await images.RenderAsync(content, LongestEdge, inlineLimit, cancellationToken);
            if (rendering.Image is { } rendered)
            {
                return new AutomationFileDelivery(
                    true,
                    null,
                    [
                        new TextContentBlock { Text = summary + CaptionTail(rendered.Width, rendered.Height, rendered.Downscaled) },
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
                var blocks = PdfBlocks(summary + PdfHeaderTail(extracted.PageCount), extracted);
                var notice = extracted.Pages.Count < extracted.PageCount
                    ? $"Only {extracted.Pages.Count} of {extracted.PageCount} pages fit the inline limit of {inlineLimit} characters; raise maxInlineBytes or fetch contentUrl."
                    : null;
                if (notice is not null)
                {
                    blocks.Add(new TextContentBlock { Text = notice });
                }
                return new AutomationFileDelivery(true, notice, blocks);
            }
            return Withheld(summary, UnreadablePdfNotice, contentUrl);
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

    /// <summary>
    /// The notice a download uses when the bytes are not handed back inline.
    /// </summary>
    public static string InlineLimitNotice(long contentLength, string mediaType, int inlineLimit) =>
        $"The content ({contentLength} bytes, {mediaType}) exceeds the inline limit of {inlineLimit} bytes or is not an image, PDF or text file; fetch contentUrl with this bearer token.";

    /// <summary>The reason a batch item that does not fit whole in what remains is withheld (R192).</summary>
    public static string DoesNotFitWholeNotice(int inlineLimit) =>
        $"The whole file does not fit in what remains of the inline limit of {inlineLimit} bytes; fetch contentUrl with this bearer token.";

    /// <summary>The reason every batch item after the first that does not fit whole is withheld unread (R192).</summary>
    public const string AfterTheStopNotice =
        "The batch stopped at an earlier item that does not fit whole; fetch contentUrl with this bearer token.";

    /// <summary>The reason an image that is not a decodable image is withheld (R211).</summary>
    public const string UndecodableImageNotice =
        "The image is undecodable; fetch contentUrl with this bearer token.";

    /// <summary>The reason an image past the renderer's decode bound is withheld (R211).</summary>
    public static readonly string OversizedImageNotice = string.Create(
        CultureInfo.InvariantCulture,
        $"The image is oversized (over {DeliveryImageRendering.MaximumPixels:N0} pixels); fetch contentUrl with this bearer token.");

    public const string UnreadablePdfNotice =
        "The PDF could not be read for text; fetch contentUrl with this bearer token.";

    /// <summary>
    /// The batch download (R192): one walk over the items in request order,
    /// charging what is sent in <c>content</c> at its encoded size, text at
    /// its UTF-8 bytes and an image block at its base64 length, each once. A
    /// whole file either fits what remains or is withheld with its reason, and
    /// the walk stops at the first item that does not fit whole: that item
    /// and every later one are metadata only, and no later item's bytes are
    /// read. An item its type or size never admits is metadata only without
    /// stopping the walk, as is a PDF that cannot be read and, of the three
    /// reasons the renderer gives, an image that is undecodable or oversized;
    /// only "no quality fits" stops it at an image (R211). A metadata-only
    /// answer is one whole line, never cut, charged to what remains down to
    /// zero. The single download keeps its own character cut
    /// (<see cref="DeliverAsync"/>).
    /// </summary>
    public static async Task<AutomationFileDelivery[]> DeliverBatchAsync(
        IRenderImageForDelivery images,
        IExtractPdfPageText pdfText,
        AutomationFileItem[] items,
        int inlineLimit,
        Func<int, CancellationToken, ValueTask<ReadOnlyMemory<byte>>> readContent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(readContent);
        var deliveries = new AutomationFileDelivery[items.Length];
        var remaining = inlineLimit;
        var stopped = false;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var summary = Summary(item.FileName, item.MediaType, item.ContentLength, item.Sha256);
            var admitted = CanDeliverInline(item.MediaType, item.ContentLength, inlineLimit);
            if (stopped || remaining == 0)
            {
                // Nothing reads what remains from here on, so no line is
                // counted. With nothing left, an item the walk admits cannot
                // send even its summary: it is the item that does not fit whole.
                var notice = !admitted
                    ? InlineLimitNotice(item.ContentLength, item.MediaType, inlineLimit)
                    : stopped ? AfterTheStopNotice : DoesNotFitWholeNotice(inlineLimit);
                deliveries[index] = Withheld(summary, notice, item.ContentUrl);
                stopped |= admitted;
                continue;
            }

            // The summary opens every block an item sends; it is counted once.
            var entry = new Entry(item, summary, Encoding.UTF8.GetByteCount(summary));
            Walked walked;
            if (!admitted)
            {
                walked = WithheldLine(entry, InlineLimitNotice(item.ContentLength, item.MediaType, inlineLimit));
            }
            else if (IsImage(item.MediaType))
            {
                walked = await WalkImageAsync(
                    images, entry, remaining, inlineLimit, readContent, index, cancellationToken);
            }
            else if (IsPdf(item.MediaType))
            {
                walked = await WalkPdfAsync(
                    pdfText, entry, remaining, inlineLimit, readContent, index, cancellationToken);
            }
            else
            {
                walked = await WalkTextAsync(
                    entry, remaining, inlineLimit, readContent, index, cancellationToken);
            }

            deliveries[index] = walked.Delivery;
            if (walked.Stops)
            {
                stopped = true;
            }
            else
            {
                remaining = Math.Max(0, remaining - walked.Charged);
            }
        }

        return deliveries;
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

    public static CallToolResult ToResult(AutomationFileDelivery delivery, object structured) =>
        ToResult(delivery.Blocks, structured);

    public static CallToolResult ToResult(IEnumerable<ContentBlock> blocks, object structured) => new()
    {
        Content = [.. blocks],
        StructuredContent = JsonSerializer.SerializeToElement(structured, structured.GetType(), Json)
    };

    public static bool IsImage(string mediaType) => CaseDocumentThumbnails.IsThumbnailable(mediaType);

    /// <summary>
    /// The image's bound is what remains after the longest caption it can
    /// carry, less the base64 growth, so the caption is charged before the
    /// render and never cut. Its bytes are read only when that bound reaches
    /// the renderer's floor, the fewest bytes any rendering takes.
    /// </summary>
    private static async Task<Walked> WalkImageAsync(
        IRenderImageForDelivery images,
        Entry entry,
        int remaining,
        int inlineLimit,
        Func<int, CancellationToken, ValueTask<ReadOnlyMemory<byte>>> readContent,
        int index,
        CancellationToken cancellationToken)
    {
        var maximumBytes = Math.Max(0, remaining - entry.SummaryBytes - LongestCaptionTailBytes) / 4 * 3;
        if (maximumBytes < images.MinimumBytes)
        {
            return DoesNotFitWhole(entry, inlineLimit);
        }

        var rendering = await images.RenderAsync(
            await readContent(index, cancellationToken),
            LongestEdge,
            maximumBytes,
            cancellationToken);
        if (rendering.Image is { } rendered)
        {
            var captionTail = CaptionTail(rendered.Width, rendered.Height, rendered.Downscaled);
            return new(
                new AutomationFileDelivery(
                    true,
                    null,
                    [
                        new TextContentBlock { Text = entry.Summary + captionTail },
                        ImageContentBlock.FromBytes(rendered.Content, rendered.MediaType)
                    ]),
                entry.SummaryBytes + Encoding.UTF8.GetByteCount(captionTail) + Base64Length(rendered.Content.Length),
                Stops: false);
        }

        return rendering.Refusal switch
        {
            DeliveryImageRefusal.Undecodable => WithheldLine(entry, UndecodableImageNotice),
            DeliveryImageRefusal.Oversized => WithheldLine(entry, OversizedImageNotice),
            _ => DoesNotFitWhole(entry, inlineLimit)
        };
    }

    /// <summary>
    /// A PDF is not read when what remains cannot hold its summary, the
    /// header's tail and one page heading, the least any PDF sends. Otherwise
    /// it is read once, its text extracted to one character past the room
    /// left (characters never outnumber UTF-8 bytes), and it is delivered only
    /// when every page came back whole and its blocks fit what remains. Each
    /// heading and page text is counted once, in one pass that ends at the
    /// first page cut short or past what remains, and the blocks are built
    /// only when the whole file fits.
    /// </summary>
    private static async Task<Walked> WalkPdfAsync(
        IExtractPdfPageText pdfText,
        Entry entry,
        int remaining,
        int inlineLimit,
        Func<int, CancellationToken, ValueTask<ReadOnlyMemory<byte>>> readContent,
        int index,
        CancellationToken cancellationToken)
    {
        var room = remaining - entry.SummaryBytes - ShortestPdfTailBytes;
        if (room < 0)
        {
            return DoesNotFitWhole(entry, inlineLimit);
        }

        var extracted = await pdfText.ExtractAsync(
            await readContent(index, cancellationToken),
            room + 1,
            cancellationToken);
        if (extracted is null)
        {
            return WithheldLine(entry, UnreadablePdfNotice);
        }
        if (extracted.Pages.Count < extracted.PageCount)
        {
            return DoesNotFitWhole(entry, inlineLimit);
        }

        var headerTail = PdfHeaderTail(extracted.PageCount);
        var charged = entry.SummaryBytes + Encoding.UTF8.GetByteCount(headerTail);
        var headings = new string[extracted.Pages.Count];
        for (var at = 0; at < headings.Length; at++)
        {
            var page = extracted.Pages[at];
            if (page.Truncated)
            {
                return DoesNotFitWhole(entry, inlineLimit);
            }
            headings[at] = PageHeading(page, extracted.PageCount);
            charged += Encoding.UTF8.GetByteCount(headings[at]) + Encoding.UTF8.GetByteCount(page.Text);
            if (charged > remaining)
            {
                return DoesNotFitWhole(entry, inlineLimit);
            }
        }

        return new(
            new AutomationFileDelivery(true, null, PdfBlocks(entry.Summary + headerTail, extracted, headings)),
            charged,
            Stops: false);
    }

    /// <summary>
    /// A text file is not read when its summary and length already exceed
    /// what remains; otherwise it is decoded once and charged its exact UTF-8
    /// total.
    /// </summary>
    private static async Task<Walked> WalkTextAsync(
        Entry entry,
        int remaining,
        int inlineLimit,
        Func<int, CancellationToken, ValueTask<ReadOnlyMemory<byte>>> readContent,
        int index,
        CancellationToken cancellationToken)
    {
        if (entry.SummaryBytes + entry.Item.ContentLength > remaining)
        {
            return DoesNotFitWhole(entry, inlineLimit);
        }

        var text = Encoding.UTF8.GetString((await readContent(index, cancellationToken)).Span);
        var charged = entry.SummaryBytes + Encoding.UTF8.GetByteCount(text);
        return charged <= remaining
            ? new(
                new AutomationFileDelivery(
                    true,
                    null,
                    [new TextContentBlock { Text = entry.Summary }, new TextContentBlock { Text = text }]),
                charged,
                Stops: false)
            : DoesNotFitWhole(entry, inlineLimit);
    }

    /// <summary>
    /// The item that does not fit whole: withheld with its reason, and the
    /// walk stops. Nothing reads what remains after it, so its line is not
    /// counted.
    /// </summary>
    private static Walked DoesNotFitWhole(Entry entry, int inlineLimit) =>
        new(
            Withheld(entry.Summary, DoesNotFitWholeNotice(inlineLimit), entry.Item.ContentUrl),
            Charged: 0,
            Stops: true);

    /// <summary>A metadata-only answer, one whole line charged at its UTF-8 bytes.</summary>
    private static Walked WithheldLine(Entry entry, string notice)
    {
        var tail = WithheldTail(notice, entry.Item.ContentUrl);
        return new(
            new AutomationFileDelivery(false, notice, [new TextContentBlock { Text = entry.Summary + tail }]),
            entry.SummaryBytes + Encoding.UTF8.GetByteCount(tail),
            Stops: false);
    }

    /// <summary>
    /// The page text of a PDF: its header block, then one block per page, each
    /// its heading and then its text. The batch passes the headings it has
    /// already built to count them.
    /// </summary>
    private static List<ContentBlock> PdfBlocks(
        string header,
        PdfTextExtraction extracted,
        string[]? headings = null)
    {
        var blocks = new List<ContentBlock>(extracted.Pages.Count + 2)
        {
            new TextContentBlock { Text = header }
        };
        for (var at = 0; at < extracted.Pages.Count; at++)
        {
            var page = extracted.Pages[at];
            blocks.Add(new TextContentBlock
            {
                Text = (headings?[at] ?? PageHeading(page, extracted.PageCount)) + page.Text
            });
        }
        return blocks;
    }

    /// <summary>One page's heading, which its text follows in the page's block.</summary>
    private static string PageHeading(PdfPageText page, int pageCount)
    {
        var heading = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"Page {page.Number} of {pageCount}");
        if (page.NeedsOcr)
        {
            heading.Append(" (little readable text; this page needs OCR)");
        }
        if (page.Truncated)
        {
            heading.Append(" (truncated at the inline limit)");
        }
        return heading.Append(":\n").ToString();
    }

    /// <summary>
    /// The longest caption tail an image can carry: both sides are at most
    /// <see cref="LongestEdge"/>, and the downscaled form is the longer one.
    /// </summary>
    private static readonly int LongestCaptionTailBytes =
        Encoding.UTF8.GetByteCount(CaptionTail(LongestEdge, LongestEdge, downscaled: true));

    /// <summary>
    /// The least a PDF sends after its summary: the header's tail for one
    /// page and that page's heading with no text and no remark.
    /// </summary>
    private static readonly int ShortestPdfTailBytes =
        Encoding.UTF8.GetByteCount(PdfHeaderTail(1))
        + Encoding.UTF8.GetByteCount(PageHeading(new PdfPageText(1, string.Empty, NeedsOcr: false, Truncated: false), 1));

    /// <summary>What follows the summary in an image's caption.</summary>
    private static string CaptionTail(int width, int height, bool downscaled) =>
        downscaled
            ? $" Shown at {width}x{height}; the original is unchanged at contentUrl."
            : $" Shown at {width}x{height}.";

    /// <summary>What follows the summary in a PDF's header block.</summary>
    private static string PdfHeaderTail(int pageCount) =>
        $" {pageCount} page(s); text follows per page. The PDF itself is at contentUrl.";

    /// <summary>The length of <paramref name="bytes"/> bytes as base64 text.</summary>
    private static int Base64Length(int bytes) => (bytes + 2) / 3 * 4;

    private static AutomationFileDelivery Withheld(string summary, string notice, string contentUrl) =>
        new(false, notice, [new TextContentBlock { Text = summary + WithheldTail(notice, contentUrl) }]);

    /// <summary>What follows the summary in a metadata-only line: the reason and where the bytes are.</summary>
    private static string WithheldTail(string notice, string contentUrl) =>
        $" {notice} contentUrl: {contentUrl}";

    private static string Summary(string fileName, string mediaType, long contentLength, string sha256) =>
        $"{fileName} ({mediaType}, {contentLength} bytes, SHA-256 {sha256}).";

    private static bool IsPdf(string mediaType) =>
        string.Equals(mediaType, "application/pdf", StringComparison.OrdinalIgnoreCase);

    private static bool IsText(string mediaType) =>
        mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mediaType, "application/xml", StringComparison.OrdinalIgnoreCase);

    /// <summary>One batch item with its summary, the summary's UTF-8 length counted once.</summary>
    private readonly record struct Entry(AutomationFileItem Item, string Summary, int SummaryBytes);

    /// <summary>One item's answer, what it charged, and whether it stops the walk.</summary>
    private readonly record struct Walked(AutomationFileDelivery Delivery, int Charged, bool Stops);
}
