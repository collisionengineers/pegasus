using System.Globalization;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Graphics.Colors;

namespace Pegasus.IntegrationTests.Reports;

/// <summary>One printed letter: where it begins, its baseline, and how it is set.</summary>
internal sealed record PrintedLetter(
    string Value, double Left, double Right, double Baseline, double Size, string Colour, bool Bold, bool Italic);

/// <summary>A line of letters of one style, with the spaces taken out of its text.</summary>
internal sealed record PrintedLine(
    string Text, double Left, double Right, double Baseline, double Size, string Colour, bool Bold, bool Italic);

/// <summary>
/// A drawn shape or a placed image, as the box that bounds it. A shape
/// that is filled has its colour, one that is stroked has its colour and the
/// line's width, and one that only clips what follows it has neither.
/// </summary>
internal sealed record PrintedBox(
    double Left, double Top, double Right, double Bottom,
    string? Fill = null, string? Stroke = null, double LineWidth = 0, bool Clip = false)
{
    public double Width => Right - Left;

    public double Height => Bottom - Top;
}

internal sealed record PrintedSegment(double FromX, double FromY, double ToX, double ToY);

internal sealed record PrintedPage(
    IReadOnlyList<PrintedLetter> Letters,
    IReadOnlyList<PrintedBox> Shapes,
    IReadOnlyList<PrintedBox> Images);

/// <summary>
/// A PDF's pages as what is printed on them, in millimetres from the top
/// left of the page. One reader reads the rendered report and the template's.
/// </summary>
internal static class PrintedPages
{
    private const double Millimetres = 25.4 / 72.0;

    internal static IReadOnlyList<PrintedPage> Read(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return [.. document.GetPages().Select(page =>
        {
            double Across(double x) => x * Millimetres;
            double Down(double y) => (page.Height - y) * Millimetres;

            var letters = page.Letters
                .Where(letter => !string.IsNullOrWhiteSpace(letter.Value))
                .Select(letter => new PrintedLetter(
                    letter.Value,
                    Across(letter.StartBaseLine.X),
                    Across(letter.BoundingBox.Right),
                    Down(letter.StartBaseLine.Y),
                    Math.Round(letter.PointSize, 2),
                    Colour(letter.Color) ?? string.Empty,
                    (letter.FontName ?? string.Empty).Contains("Bold", StringComparison.OrdinalIgnoreCase),
                    (letter.FontName ?? string.Empty).Contains("Italic", StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            var shapes = page.Paths
                .Select(path => (Path: path, Bounds: path.GetBoundingRectangle()))
                .Where(shape => shape.Bounds is not null)
                .Select(shape => new PrintedBox(
                    Across(shape.Bounds!.Value.Left),
                    Down(shape.Bounds.Value.Top),
                    Across(shape.Bounds.Value.Right),
                    Down(shape.Bounds.Value.Bottom),
                    shape.Path.IsFilled ? Colour(shape.Path.FillColor) : null,
                    shape.Path.IsStroked ? Colour(shape.Path.StrokeColor) : null,
                    shape.Path.IsStroked ? (double)shape.Path.LineWidth * Millimetres : 0,
                    shape.Path.IsClipping))
                .ToArray();
            var images = page.GetImages()
                .Select(image => new PrintedBox(
                    Across(image.BoundingBox.Left),
                    Down(image.BoundingBox.Top),
                    Across(image.BoundingBox.Right),
                    Down(image.BoundingBox.Bottom)))
                .ToArray();
            return new PrintedPage(letters, shapes, images);
        })];
    }

    /// <summary>
    /// The letters as lines of one style, top to bottom: their text with
    /// the spaces taken out, where they begin and end, and their baseline.
    /// </summary>
    internal static List<PrintedLine> Lines(IEnumerable<PrintedLetter> letters) =>
    [
        .. letters
            .GroupBy(letter => (
                Baseline: Math.Round(letter.Baseline, 1),
                letter.Size, letter.Colour, letter.Bold, letter.Italic))
            .OrderBy(line => line.Key.Baseline)
            .ThenBy(line => line.Min(letter => letter.Left))
            .Select(line => new PrintedLine(
                string.Concat(line.OrderBy(letter => letter.Left).Select(letter => letter.Value)),
                line.Min(letter => letter.Left),
                line.Max(letter => letter.Right),
                line.Key.Baseline,
                line.Key.Size,
                line.Key.Colour,
                line.Key.Bold,
                line.Key.Italic)),
    ];

    /// <summary>A text with its spaces taken out, as <see cref="Lines"/> reads one.</summary>
    internal static string Squeeze(string text) =>
        string.Concat(text.Where(character => !char.IsWhiteSpace(character)));

    private static string? Colour(IColor? colour)
    {
        if (colour is null)
        {
            return null;
        }
        var (red, green, blue) = colour.ToRGBValues();
        return string.Create(
            CultureInfo.InvariantCulture,
            $"#{Channel(red):x2}{Channel(green):x2}{Channel(blue):x2}");
    }

    private static int Channel(double value) => (int)Math.Round(value * 255, MidpointRounding.AwayFromZero);
}
