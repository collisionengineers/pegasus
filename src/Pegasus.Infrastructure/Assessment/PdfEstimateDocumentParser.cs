using Pegasus.Core.Assessment;
using UglyToad.PdfPig;

namespace Pegasus.Infrastructure.Assessment;

/// <summary>The single PDF container; providers are identified only from readable content.</summary>
public sealed class PdfEstimateDocumentParser : IEstimateDocumentParser
{
    public bool CanParse(string fileName, string mediaType) =>
        string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mediaType, "application/pdf", StringComparison.OrdinalIgnoreCase);

    public ParsedEstimate Parse(ReadOnlyMemory<byte> content)
    {
        List<VisualRow> rows = [];
        try
        {
            using var document = PdfDocument.Open(content.ToArray());
            foreach (var page in document.GetPages())
            {
                var words = page.GetWords().Select(word =>
                    (Y: word.BoundingBox.Bottom, Word: new PlacedWord(word.BoundingBox.Left, word.Text)));
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
        return glass ? GlassEstimatePdfParser.Parse(rows) : AudatexEstimatePdfParser.Parse(rows);
    }

    internal sealed record PlacedWord(double X, string Text);
    internal sealed record VisualRow(int Page, double Y, IReadOnlyList<PlacedWord> Words, string JoinedText);
}
