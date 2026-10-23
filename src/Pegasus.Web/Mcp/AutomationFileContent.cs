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
    /// used (up to three reads run ahead, so the ones after the stop may have
    /// been fetched and are dropped). An item its type or size never admits is
    /// metadata only without stopping the walk, as is a PDF that cannot be read and, of the three
    /// reasons the renderer gives, an image that is undecodable or oversized;
    /// only "no quality fits" stops it at an image (R211). A metadata-only
    /// answer is one whole line, never cut, charged to what remains down to
    /// zero. The single download keeps its own character cut
    /// (<see cref="DeliverAsync"/>). The walk's decisions, what is charged,
    /// rendered or stopped at, are taken one item at a time in request order;
    /// only the reads run ahead (<see cref="ReadAhead"/>).
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
        var entries = new Entry[items.Length];
        for (var index = 0; index < entries.Length; index++)
        {
            var item = items[index];
            // The summary opens every block an item sends; it is counted once.
            var summary = Summary(item.FileName, item.MediaType, item.ContentLength, item.Sha256);
            entries[index] = new Entry(
                item,
                summary,
                Encoding.UTF8.GetByteCount(summary),
                CanDeliverInline(item.MediaType, item.ContentLength, inlineLimit));
        }

        await using var reads = new ReadAhead(images, entries, readContent, cancellationToken);
        var remaining = inlineLimit;
        var stopped = false;
        for (var index = 0; index < items.Length; index++)
        {
            var entry = entries[index];
            var item = entry.Item;
            var summary = entry.Summary;
            var admitted = entry.Admitted;
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

            Walked walked;
            if (!admitted)
            {
                walked = WithheldLine(entry, InlineLimitNotice(item.ContentLength, item.MediaType, inlineLimit));
            }
            else if (IsImage(item.MediaType))
            {
                walked = await WalkImageAsync(
                    images, entry, remaining, inlineLimit, reads, index, cancellationToken);
            }
            else if (IsPdf(item.MediaType))
            {
                walked = await WalkPdfAsync(
                    pdfText, entry, remaining, inlineLimit, reads, index, cancellationToken);
            }
            else
            {
                walked = await WalkTextAsync(entry, remaining, inlineLimit, reads, index);
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
        ReadAhead reads,
        int index,
        CancellationToken cancellationToken)
    {
        var maximumBytes = ImageBound(entry, remaining);
        if (maximumBytes < images.MinimumBytes)
        {
            return DoesNotFitWhole(entry, inlineLimit);
        }

        var rendering = await images.RenderAsync(
            await reads.TakeAsync(index, remaining),
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
        ReadAhead reads,
        int index,
        CancellationToken cancellationToken)
    {
        var room = PdfRoom(entry, remaining);
        if (room < 0)
        {
            return DoesNotFitWhole(entry, inlineLimit);
        }

        var extracted = await pdfText.ExtractAsync(
            await reads.TakeAsync(index, remaining),
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
        ReadAhead reads,
        int index)
    {
        if (!TextFits(entry, remaining))
        {
            return DoesNotFitWhole(entry, inlineLimit);
        }

        var text = Encoding.UTF8.GetString((await reads.TakeAsync(index, remaining)).Span);
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

    /// <summary>
    /// One batch item with its summary, the summary's UTF-8 length counted once,
    /// and whether its type and size admit its content at all.
    /// </summary>
    private readonly record struct Entry(AutomationFileItem Item, string Summary, int SummaryBytes, bool Admitted);

    /// <summary>The largest image that fits what remains after the longest caption and base64's growth.</summary>
    private static int ImageBound(Entry entry, int remaining) =>
        Math.Max(0, remaining - entry.SummaryBytes - LongestCaptionTailBytes) / 4 * 3;

    /// <summary>What a PDF's page text may take of what remains, once its summary and its least tail are paid.</summary>
    private static int PdfRoom(Entry entry, int remaining) =>
        remaining - entry.SummaryBytes - ShortestPdfTailBytes;

    /// <summary>Whether a text file's summary and length fit what remains.</summary>
    private static bool TextFits(Entry entry, int remaining) =>
        entry.SummaryBytes + entry.Item.ContentLength <= remaining;

    /// <summary>
    /// Whether an admitted item's bytes are worth reading with
    /// <paramref name="remaining"/> left, the test each walk makes before it
    /// reads. What remains only falls, so an item that fails it with an upper
    /// bound on what remains fails it when the walk reaches it.
    /// </summary>
    private static bool ReadableWithin(IRenderImageForDelivery images, Entry entry, int remaining) =>
        IsImage(entry.Item.MediaType)
            ? ImageBound(entry, remaining) >= images.MinimumBytes
            : IsPdf(entry.Item.MediaType)
                ? PdfRoom(entry, remaining) >= 0
                : TextFits(entry, remaining);

    /// <summary>
    /// The reads of a batch, run ahead of the walk (R363): at most three are
    /// started and not yet finished at once, in request order, so the next blob
    /// is on its way while the item before it is rendered. The walk takes each
    /// in request order and every charge, render and stop is its own, one item
    /// at a time. A read is started ahead only for an item the walk would read
    /// with the most that can remain when it reaches it, and never past an
    /// admitted item it would not, since the walk stops there. A read the
    /// walk has taken is let go at once, so only the item in hand and the
    /// reads ahead of it hold bytes. A read ahead that fails raises nothing
    /// unless the walk takes it, and when the walk ends, stops or fails the
    /// reads still open are cancelled and finished.
    /// </summary>
    private sealed class ReadAhead(
        IRenderImageForDelivery images,
        Entry[] entries,
        Func<int, CancellationToken, ValueTask<ReadOnlyMemory<byte>>> read,
        CancellationToken cancellationToken) : IAsyncDisposable
    {
        private const int MaximumOpen = 3;

        private readonly CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        private readonly Task<ReadOnlyMemory<byte>>?[] started = new Task<ReadOnlyMemory<byte>>?[entries.Length];
        private int open;

        /// <summary>The item's bytes, its read started now if it is not on its way, with the reads after it topped up.</summary>
        public async ValueTask<ReadOnlyMemory<byte>> TakeAsync(int index, int remaining)
        {
            var task = started[index] ?? Start(index);
            StartAhead(index, remaining);
            var content = await task;
            // Taken: the finished read keeps its bytes, so it is let go, and
            // the disposal waits only for reads nobody took.
            started[index] = null;
            open--;
            // This read is over: the next goes while the item is rendered.
            StartAhead(index, remaining);
            return content;
        }

        public async ValueTask DisposeAsync()
        {
            await stop.CancelAsync();
            foreach (var task in started)
            {
                if (task is null)
                {
                    continue;
                }

                try
                {
                    await task;
                }
                catch (Exception)
                {
                    // A read ahead the walk never took raises nothing; one it
                    // took has raised already, to the walk.
                }
            }
            stop.Dispose();
        }

        /// <summary>
        /// Tops up the reads after item <paramref name="current"/>, which has
        /// <paramref name="remaining"/> before it is charged. Every item the
        /// walk takes before a later one charges at least its summary line (or
        /// charges all that is left, and nothing is read after that), so a
        /// later item can have no more than what remains less the summaries of
        /// the items from <paramref name="current"/> up to it.
        /// </summary>
        private void StartAhead(int current, int remaining)
        {
            var ceiling = remaining;
            for (var at = current + 1; at < entries.Length && open < MaximumOpen; at++)
            {
                ceiling -= entries[at - 1].SummaryBytes;
                if (!entries[at].Admitted)
                {
                    continue;
                }
                if (!ReadableWithin(images, entries[at], ceiling))
                {
                    return;
                }
                if (started[at] is null)
                {
                    Start(at);
                }
            }
        }

        private Task<ReadOnlyMemory<byte>> Start(int index)
        {
            open++;
            return started[index] = ReadAsync(index);
        }

        private async Task<ReadOnlyMemory<byte>> ReadAsync(int index) => await read(index, stop.Token);
    }

    /// <summary>One item's answer, what it charged, and whether it stops the walk.</summary>
    private readonly record struct Walked(AutomationFileDelivery Delivery, int Charged, bool Stops);
}
