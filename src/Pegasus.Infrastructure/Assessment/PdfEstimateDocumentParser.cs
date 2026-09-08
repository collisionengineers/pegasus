using Pegasus.Core.Assessment;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Intake;
using UglyToad.PdfPig;

namespace Pegasus.Infrastructure.Assessment;

/// <summary>The single PDF container; providers are identified only from readable content.</summary>
public sealed class PdfEstimateDocumentParser : IEstimateDocumentParser
{
    public bool CanParse(string fileName, string mediaType) =>
        string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mediaType, "application/pdf", StringComparison.OrdinalIgnoreCase);

    public EstimateDocumentReadResult Parse(ReadOnlyMemory<byte> content, IReadOnlyList<IntakeOcrPage>? ocrPages = null)
    {
        List<VisualRow> rows = [];
        try
        {
            using var document = PdfDocument.Open(content.ToArray());
            var qualified = document.GetPages().Where(page => PdfOcrQualification.HasUnusableTextMap(document, page))
                .Select(page => page.Number).ToArray();
            if (qualified.Length > 0 && ocrPages is null)
                return new(null, qualified);
            if (ocrPages is not null && !ocrPages.Select(page => page.Number).Order().SequenceEqual(qualified))
                throw new EstimateParseRejectedException("OCR pages do not match this PDF's qualified pages.");

            foreach (var page in document.GetPages())
            {
                IEnumerable<(double Y, PlacedWord Word)> words;
                if (ocrPages?.SingleOrDefault(item => item.Number == page.Number) is { } ocrPage)
                {
                    words = ocrPage.Lines.SelectMany(line => line.Words).Select(word =>
                    {
                        var bounds = word.Bounds
                            ?? throw new EstimateParseRejectedException("An OCR word has no attributable position.");
                        var scale = bounds.Unit switch
                        {
                            "inch" => 72d,
                            "point" => 1d,
                            _ => throw new EstimateParseRejectedException("The OCR coordinate unit is unsupported."),
                        };
                        if (!double.IsFinite(bounds.Left) || !double.IsFinite(bounds.Right)
                            || !double.IsFinite(bounds.Top) || !double.IsFinite(bounds.Bottom)
                            || bounds.Left < 0 || bounds.Top < 0 || bounds.Right <= bounds.Left || bounds.Bottom <= bounds.Top
                            || bounds.Right * scale > page.Width + 1 || bounds.Bottom * scale > page.Height + 1
                            || word.Confidence is { } confidence && (!double.IsFinite(confidence) || confidence is < 0 or > 1))
                            throw new EstimateParseRejectedException("An OCR word has invalid source coordinates or confidence.");
                        return (page.Height - bounds.Bottom * scale, new PlacedWord(bounds.Left * scale, word.Text, word.Confidence));
                    });
                }
                else
                {
                    words = page.GetWords().Select(word =>
                        (word.BoundingBox.Bottom, new PlacedWord(word.BoundingBox.Left, word.Text)));
                }
                foreach (var group in words.GroupBy(word => Math.Round(word.Y, 1)).OrderByDescending(group => group.Key))
                {
                    var placed = group.Select(word => word.Word).OrderBy(word => word.X).ToArray();
                    rows.Add(new(page.Number, group.Key, placed, string.Join(' ', placed.Select(word => word.Text))));
                }
            }
        }
        catch (EstimateParseRejectedException) { throw; }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new EstimateParseRejectedException("The file could not be read as a PDF, so nothing was imported.");
        }

        var glass = rows.Any(row => row.JoinedText == "Glass's Information Services");
        var audatex = rows.Any(row => row.JoinedText.StartsWith("Audatex System", StringComparison.Ordinal));
        if (glass == audatex)
            throw new EstimateParseRejectedException("The PDF does not identify exactly one supported estimate provider.");
        return new(glass ? GlassEstimatePdfParser.Parse(rows) : AudatexEstimatePdfParser.Parse(rows), []);
    }

    internal sealed record PlacedWord(double X, string Text, double? Confidence = null);
    internal sealed record VisualRow(int Page, double Y, IReadOnlyList<PlacedWord> Words, string JoinedText);
}
