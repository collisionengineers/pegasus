using System.Text;
using ModelContextProtocol.Protocol;
using Pegasus.Core.Documents;
using Pegasus.Web.Mcp;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// The batch download's one walk (R192, R211) in
/// <see cref="AutomationFileContent.DeliverBatchAsync"/>: every item charged at
/// the encoded size of what is sent, in request order, a whole file fitting or
/// withheld with its reason, and the walk stopping at the first item that does
/// not fit whole. The renderer, the extractor and the content fetch are
/// recording fakes, so what each was asked for is part of the evidence.
/// </summary>
public sealed class AutomationFileContentBatchTests
{
    [Fact]
    public async Task ItemsAreChargedInRequestOrderAtTheirEncodedSize()
    {
        var renderer = new Renderer().Renders("photo.jpg", 301);
        var extractor = new Extractor().Reads("report.pdf", "Total £120 due on the nearside door");
        Doc[] docs =
        [
            Text("note.txt", "£50 café"),
            Binary("photo.jpg", "image/jpeg", 5000),
            Binary("report.pdf", "application/pdf", 4000)
        ];

        var (wide, _) = await WalkAsync(100_000, renderer, extractor, docs);
        Assert.All(wide, delivery => Assert.True(delivery.ContentIncluded));
        var charged = wide.Sum(Charged);

        // Exactly what was sent fits: the caption and the base64 image block,
        // then the PDF's UTF-8 bytes, in that order.
        var (exact, _) = await WalkAsync(charged, renderer, extractor, docs);
        Assert.All(exact, delivery => Assert.True(delivery.ContentIncluded));
        Assert.Equal(charged, exact.Sum(Charged));

        // One byte less and the last item in request order is the one that
        // does not fit whole; the image was charged at its base64 length.
        var (oneShort, _) = await WalkAsync(charged - 1, renderer, extractor, docs);
        Assert.True(oneShort[0].ContentIncluded);
        Assert.True(oneShort[1].ContentIncluded);
        Assert.False(oneShort[2].ContentIncluded);
        Assert.Equal(AutomationFileContent.DoesNotFitWholeNotice(charged - 1), oneShort[2].Notice);
    }

    [Fact]
    public async Task UnusedBytesCarryToTheNextItem()
    {
        const int inlineLimit = 10_000;
        var renderer = new Renderer().Renders("first.jpg", 120).RendersToTheBound("second.jpg");

        var (deliveries, _) = await WalkAsync(
            inlineLimit,
            renderer,
            new Extractor(),
            Binary("first.jpg", "image/jpeg", 9000),
            Binary("second.jpg", "image/jpeg", 9000));

        Assert.All(deliveries, delivery => Assert.True(delivery.ContentIncluded));
        // No share: the first image is offered everything after its caption,
        // and the second everything the first did not use.
        Assert.Equal(Bound(inlineLimit, CaptionBytes(deliveries[0])), renderer.Calls[0].MaximumBytes);
        Assert.Equal(
            Bound(inlineLimit - Charged(deliveries[0]), CaptionBytes(deliveries[1])),
            renderer.Calls[1].MaximumBytes);
        Assert.True(deliveries.Sum(Charged) <= inlineLimit);
    }

    [Fact]
    public async Task TheFirstItemThatDoesNotFitStopsTheWalkAndLaterItemsAreNeverFetched()
    {
        const int inlineLimit = 600;
        var renderer = new Renderer().Renders("d.jpg", 10);
        var extractor = new Extractor().Reads("big.pdf", [.. Enumerable.Repeat(new string('x', 400), 3)]);

        var (deliveries, fetch) = await WalkAsync(
            inlineLimit,
            renderer,
            extractor,
            Text("a.txt", "alpha"),
            Binary("big.pdf", "application/pdf", 9000),
            Text("c.txt", "gamma"),
            Binary("d.jpg", "image/jpeg", 1000));

        Assert.True(deliveries[0].ContentIncluded);
        Assert.False(deliveries[1].ContentIncluded);
        Assert.Equal(AutomationFileContent.DoesNotFitWholeNotice(inlineLimit), deliveries[1].Notice);
        Assert.Equal(AutomationFileContent.AfterTheStopNotice, deliveries[2].Notice);
        Assert.Equal(AutomationFileContent.AfterTheStopNotice, deliveries[3].Notice);
        Assert.Equal([0, 1], fetch.Reads);
        Assert.Empty(renderer.Calls);
    }

    [Fact]
    public async Task AMetadataOnlyItemKeepsItsWholeLineAndDoesNotStop()
    {
        const int inlineLimit = 2_000;
        Doc[] docs =
        [
            Binary("clip.mp4", "video/mp4", 5000),
            Binary("empty.jpg", "image/jpeg", 0),
            Binary("huge.jpg", "image/jpeg", AutomationMcpErrors.MaximumDocumentBytes + 1L),
            Text("note.txt", "kept")
        ];
        var renderer = new Renderer();

        var (deliveries, fetch) = await WalkAsync(inlineLimit, renderer, new Extractor(), docs);

        for (var index = 0; index < 3; index++)
        {
            var item = docs[index].Item;
            Assert.False(deliveries[index].ContentIncluded);
            Assert.Equal(
                AutomationFileContent.InlineLimitNotice(item.ContentLength, item.MediaType, inlineLimit),
                deliveries[index].Notice);
            AssertWholeLine(deliveries[index], item);
        }
        Assert.True(deliveries[3].ContentIncluded);
        Assert.Equal(3, Assert.Single(fetch.Reads));
        Assert.Empty(renderer.Calls);

        // A line is never cut, even when nothing remains for it.
        var (tight, _) = await WalkAsync(1, renderer, new Extractor(), docs[0]);
        AssertWholeLine(tight[0], docs[0].Item);
    }

    [Fact]
    public async Task APdfThatDoesNotFitWholeIsWithheldNotCut()
    {
        const int inlineLimit = 500;
        var extractor = new Extractor().Reads("long.pdf", [new string('x', 300), new string('y', 300)]);
        var doc = Binary("long.pdf", "application/pdf", 8000);

        var (deliveries, fetch) = await WalkAsync(inlineLimit, new Renderer(), extractor, doc);

        var delivery = Assert.Single(deliveries);
        Assert.False(delivery.ContentIncluded);
        Assert.Equal(AutomationFileContent.DoesNotFitWholeNotice(inlineLimit), delivery.Notice);
        AssertWholeLine(delivery, doc.Item);
        Assert.Equal(0, Assert.Single(fetch.Reads));
        // The text was asked for only up to the room left, plus one character.
        Assert.True(extractor.Calls[0].MaximumCharacters <= inlineLimit);
    }

    [Fact]
    public async Task ANonAsciiPdfIsChargedInUtf8Bytes()
    {
        var extractor = new Extractor().Reads("pounds.pdf", new string('£', 100));
        var doc = Binary("pounds.pdf", "application/pdf", 3000);

        var (wide, _) = await WalkAsync(100_000, new Renderer(), extractor, doc);
        var bytes = Charged(wide[0]);
        var characters = wide[0].Blocks.Sum(block => TextOf(block).Length);
        Assert.True(characters < bytes - 1, $"The fixture must be fewer characters ({characters}) than UTF-8 bytes ({bytes}).");

        var (exact, _) = await WalkAsync(bytes, new Renderer(), extractor, doc);
        Assert.True(exact[0].ContentIncluded);
        Assert.Contains("£", TextOf(exact[0].Blocks[1]), StringComparison.Ordinal);

        // Its characters would fit one byte less; its UTF-8 bytes do not.
        var (under, _) = await WalkAsync(bytes - 1, new Renderer(), extractor, doc);
        Assert.False(under[0].ContentIncluded);
        Assert.Equal(AutomationFileContent.DoesNotFitWholeNotice(bytes - 1), under[0].Notice);
    }

    [Fact]
    public async Task ATextFileOrPdfThatCannotFitIsNotFetched()
    {
        // Fifty bytes cannot hold either summary, so neither is read.
        var extractor = new Extractor().Reads("report.pdf", "text");
        var (deliveries, fetch) = await WalkAsync(
            50,
            new Renderer(),
            extractor,
            Text("note.txt", "0123456789"),
            Binary("report.pdf", "application/pdf", 3000));

        Assert.Equal(AutomationFileContent.DoesNotFitWholeNotice(50), deliveries[0].Notice);
        Assert.Equal(AutomationFileContent.AfterTheStopNotice, deliveries[1].Notice);
        Assert.Empty(fetch.Reads);

        var (pdfOnly, pdfFetch) = await WalkAsync(
            50, new Renderer(), extractor, Binary("report.pdf", "application/pdf", 3000));
        Assert.Equal(AutomationFileContent.DoesNotFitWholeNotice(50), pdfOnly[0].Notice);
        Assert.Empty(pdfFetch.Reads);
        Assert.Empty(extractor.Calls);
    }

    [Fact]
    public async Task TheImageIsRenderedToWhatRemainsAfterItsCaptionAndBase64Growth()
    {
        const int inlineLimit = 10_000;
        var renderer = new Renderer().RendersToTheBound("photo.jpg");

        var (deliveries, _) = await WalkAsync(
            inlineLimit, renderer, new Extractor(), Binary("photo.jpg", "image/jpeg", 50_000));

        var delivery = Assert.Single(deliveries);
        Assert.True(delivery.ContentIncluded);
        var caption = TextOf(delivery.Blocks[0]);
        Assert.StartsWith("photo.jpg (image/jpeg, 50000 bytes", caption, StringComparison.Ordinal);
        Assert.EndsWith("Shown at 1568x1176; the original is unchanged at contentUrl.", caption, StringComparison.Ordinal);
        Assert.IsType<ImageContentBlock>(delivery.Blocks[1]);
        // The caption is taken off first and base64's four bytes for three
        // second, so the largest image the renderer may return still fits.
        Assert.Equal(Bound(inlineLimit, CaptionBytes(delivery)), renderer.Calls[0].MaximumBytes);
        Assert.True(Charged(delivery) <= inlineLimit);
    }

    [Fact]
    public async Task ContentBlocksFollowRequestOrder()
    {
        Doc[] docs =
        [
            Binary("b.pdf", "application/pdf", 3000),
            Binary("a.jpg", "image/jpeg", 3000),
            Text("c.txt", "sea"),
            Binary("d.mp4", "video/mp4", 3000)
        ];

        var (deliveries, fetch) = await WalkAsync(
            100_000,
            new Renderer().Renders("a.jpg", 30),
            new Extractor().Reads("b.pdf", "bee"),
            docs);

        for (var index = 0; index < docs.Length; index++)
        {
            Assert.StartsWith($"{docs[index].Item.FileName} (", TextOf(deliveries[index].Blocks[0]), StringComparison.Ordinal);
        }
        Assert.Equal(
            ["text", "text", "text", "image", "text", "text", "text"],
            deliveries.SelectMany(delivery => delivery.Blocks).Select(block => block.Type).ToArray());
        Assert.Equal([0, 1, 2], fetch.Reads);
    }

    [Fact]
    public async Task AnUnreadablePdfDoesNotStopTheWalk()
    {
        var (deliveries, fetch) = await WalkAsync(
            10_000,
            new Renderer(),
            new Extractor().CannotRead("broken.pdf"),
            Binary("broken.pdf", "application/pdf", 3000),
            Text("after.txt", "after"));

        Assert.False(deliveries[0].ContentIncluded);
        Assert.Equal(AutomationFileContent.UnreadablePdfNotice, deliveries[0].Notice);
        Assert.True(deliveries[1].ContentIncluded);
        Assert.Equal([0, 1], fetch.Reads);
    }

    [Fact]
    public async Task OnlyAnImageForWhichNoQualityFitsStopsTheWalk()
    {
        const int inlineLimit = 10_000;
        var renderer = new Renderer()
            .Refuses("broken.jpg", DeliveryImageRefusal.Undecodable)
            .Refuses("huge.jpg", DeliveryImageRefusal.Oversized)
            .Refuses("tight.jpg", DeliveryImageRefusal.NoQualityFits);

        var (deliveries, fetch) = await WalkAsync(
            inlineLimit,
            renderer,
            new Extractor(),
            Binary("broken.jpg", "image/jpeg", 3000),
            Binary("huge.jpg", "image/jpeg", 3000),
            Binary("tight.jpg", "image/jpeg", 3000),
            Text("after.txt", "after"));

        Assert.Equal(AutomationFileContent.UndecodableImageNotice, deliveries[0].Notice);
        Assert.Equal(AutomationFileContent.OversizedImageNotice, deliveries[1].Notice);
        Assert.Equal(AutomationFileContent.DoesNotFitWholeNotice(inlineLimit), deliveries[2].Notice);
        Assert.Equal(AutomationFileContent.AfterTheStopNotice, deliveries[3].Notice);
        Assert.All(deliveries, delivery => Assert.False(delivery.ContentIncluded));
        Assert.Equal(["broken.jpg", "huge.jpg", "tight.jpg"], renderer.Calls.Select(call => call.Name).ToArray());
        Assert.Equal([0, 1, 2], fetch.Reads);
    }

    [Fact]
    public async Task NoNoticeStatesAZeroLimit()
    {
        var fill = Text("fill.txt", "fills the limit exactly");
        var (alone, _) = await WalkAsync(100_000, new Renderer(), new Extractor(), fill);
        var inlineLimit = Charged(alone[0]);

        var (deliveries, _) = await WalkAsync(
            inlineLimit,
            new Renderer().Renders("late.jpg", 10),
            new Extractor(),
            fill,
            Binary("clip.mp4", "video/mp4", 3000),
            Binary("late.jpg", "image/jpeg", 3000),
            Text("late.txt", "late"));

        // Nothing remains, and every notice still names the caller's limit.
        Assert.True(deliveries[0].ContentIncluded);
        Assert.Equal(AutomationFileContent.InlineLimitNotice(3000, "video/mp4", inlineLimit), deliveries[1].Notice);
        Assert.Equal(AutomationFileContent.DoesNotFitWholeNotice(inlineLimit), deliveries[2].Notice);
        Assert.Equal(AutomationFileContent.AfterTheStopNotice, deliveries[3].Notice);
    }

    private static async Task<(AutomationFileDelivery[] Deliveries, Fetch Fetch)> WalkAsync(
        int inlineLimit,
        Renderer renderer,
        Extractor extractor,
        params Doc[] docs)
    {
        var fetch = new Fetch(docs);
        var deliveries = await AutomationFileContent.DeliverBatchAsync(
            renderer,
            extractor,
            [.. docs.Select(doc => doc.Item)],
            inlineLimit,
            fetch.ReadAsync,
            CancellationToken.None);
        return (deliveries, fetch);
    }

    /// <summary>What a delivery puts in <c>content</c>: UTF-8 text and base64 image data.</summary>
    private static int Charged(AutomationFileDelivery delivery) =>
        delivery.Blocks.Sum(block => block switch
        {
            TextContentBlock text => Encoding.UTF8.GetByteCount(text.Text),
            ImageContentBlock image => image.Data.Length,
            _ => throw new InvalidOperationException($"Unexpected block {block.Type}.")
        });

    /// <summary>The largest image whose base64 fits what remains after its caption.</summary>
    private static int Bound(int remaining, int captionBytes) => (remaining - captionBytes) / 4 * 3;

    private static int CaptionBytes(AutomationFileDelivery delivery) =>
        Encoding.UTF8.GetByteCount(TextOf(delivery.Blocks[0]));

    private static string TextOf(ContentBlock block) => Assert.IsType<TextContentBlock>(block).Text;

    private static void AssertWholeLine(AutomationFileDelivery delivery, AutomationFileItem item)
    {
        var line = TextOf(Assert.Single(delivery.Blocks));
        Assert.StartsWith($"{item.FileName} ({item.MediaType}, {item.ContentLength} bytes, SHA-256 {item.Sha256}).", line, StringComparison.Ordinal);
        Assert.Contains(delivery.Notice!, line, StringComparison.Ordinal);
        Assert.EndsWith($"contentUrl: {item.ContentUrl}", line, StringComparison.Ordinal);
    }

    private static Doc Text(string fileName, string text)
    {
        var content = Encoding.UTF8.GetBytes(text);
        return new(Item(fileName, "text/plain", content.Length), content);
    }

    private static Doc Binary(string fileName, string mediaType, long contentLength) =>
        new(Item(fileName, mediaType, contentLength), Encoding.UTF8.GetBytes(fileName));

    private static AutomationFileItem Item(string fileName, string mediaType, long contentLength) =>
        new(fileName, mediaType, contentLength, new string('a', 64), $"/automation/documents/{fileName}");

    /// <summary>One item and the bytes its fetch answers; a binary's bytes are its name, so the fakes know it.</summary>
    private sealed record Doc(AutomationFileItem Item, byte[] Content);

    private sealed class Fetch(Doc[] docs)
    {
        public List<int> Reads { get; } = [];

        public ValueTask<ReadOnlyMemory<byte>> ReadAsync(int index, CancellationToken cancellationToken)
        {
            Reads.Add(index);
            return ValueTask.FromResult<ReadOnlyMemory<byte>>(docs[index].Content);
        }
    }

    private sealed class Renderer : IRenderImageForDelivery
    {
        private readonly Dictionary<string, Func<int, DeliveryImageRendering>> answers = new(StringComparer.Ordinal);

        public List<(string Name, int MaximumBytes)> Calls { get; } = [];

        /// <summary>An image of <paramref name="bytes"/> bytes whenever the bound allows it.</summary>
        public Renderer Renders(string fileName, int bytes)
        {
            answers[fileName] = maximumBytes => bytes <= maximumBytes
                ? DeliveryImageRendering.Rendered(Image(bytes))
                : DeliveryImageRendering.Refused(DeliveryImageRefusal.NoQualityFits);
            return this;
        }

        public Renderer RendersToTheBound(string fileName)
        {
            answers[fileName] = maximumBytes => DeliveryImageRendering.Rendered(Image(maximumBytes));
            return this;
        }

        public Renderer Refuses(string fileName, DeliveryImageRefusal refusal)
        {
            answers[fileName] = _ => DeliveryImageRendering.Refused(refusal);
            return this;
        }

        public Task<DeliveryImageRendering> RenderAsync(
            ReadOnlyMemory<byte> content,
            int longestEdge,
            int maximumBytes,
            CancellationToken cancellationToken)
        {
            var fileName = Encoding.UTF8.GetString(content.Span);
            Calls.Add((fileName, maximumBytes));
            return Task.FromResult(answers[fileName](maximumBytes));
        }

        // Four-digit sides, so the caption is as long as the walk reserves.
        private static DeliveryImage Image(int bytes) => new(new byte[bytes], "image/jpeg", 1568, 1176, Downscaled: true);
    }

    /// <summary>Pages until the characters run out, the last one cut, as the PdfPig extractor reads them.</summary>
    private sealed class Extractor : IExtractPdfPageText
    {
        private readonly Dictionary<string, string[]?> documents = new(StringComparer.Ordinal);

        public List<(string Name, int MaximumCharacters)> Calls { get; } = [];

        public Extractor Reads(string fileName, string page) => Reads(fileName, [page]);

        public Extractor Reads(string fileName, string[] pages)
        {
            documents[fileName] = pages;
            return this;
        }

        public Extractor CannotRead(string fileName)
        {
            documents[fileName] = null;
            return this;
        }

        public Task<PdfTextExtraction?> ExtractAsync(
            ReadOnlyMemory<byte> content,
            int maximumCharacters,
            CancellationToken cancellationToken)
        {
            var fileName = Encoding.UTF8.GetString(content.Span);
            Calls.Add((fileName, maximumCharacters));
            if (documents[fileName] is not { } pages)
            {
                return Task.FromResult<PdfTextExtraction?>(null);
            }

            var read = new List<PdfPageText>();
            var remaining = maximumCharacters;
            for (var number = 1; number <= pages.Length && remaining > 0; number++)
            {
                var text = pages[number - 1];
                var truncated = text.Length > remaining;
                if (truncated)
                {
                    text = text[..remaining];
                }
                remaining -= text.Length;
                read.Add(new PdfPageText(number, text, NeedsOcr: false, truncated));
            }

            return Task.FromResult<PdfTextExtraction?>(new PdfTextExtraction(pages.Length, read));
        }
    }
}
