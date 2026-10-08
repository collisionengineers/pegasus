using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Documents;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure;
using static Pegasus.IntegrationTests.Reports.PrintedPages;

namespace Pegasus.IntegrationTests.Reports;

/// <summary>
/// The printed report against the operator's template
/// (<c>reference/rendererref1</c>, Design I). Each of the template's four
/// sample jobs is rendered, and the rendered report and the report the
/// template printed for the same job are read through one reader and set
/// side by side: the pages, every letter's place, size and colour, the
/// running header and footer, the image slots, the fills and the rules.
/// Places are compared in millimetres from the top left of the page, within
/// <see cref="Near"/>. The rendered reports are kept in the test's output
/// directory, under <c>rendered-reports</c>.
///
/// Not compared, because the two differ by design: what the photographs
/// show, the damage diagram's own drawing, and the typeface of the page
/// number. Not compared, because the reader cannot report it for the
/// template's file: the thickness of a rule.
/// </summary>
public sealed class AssessmentReportTemplateConformanceTests(RenderedTemplateSamples rendered)
    : IClassFixture<RenderedTemplateSamples>
{
    /// <summary>How far, in millimetres, a printed thing may stand from where the template sets it.</summary>
    private const double Near = 0.5;

    /// <summary>How far, in points, a type size may differ from the template's.</summary>
    private const double SameSize = 0.05;

    // The template's page, in millimetres.
    private const double BodyLeft = 22.117;
    private const double BodyRight = 187.883;
    private const double BodyTop = 36;
    private const double BodyBottom = 280;
    private const double HeaderRight = 190;
    private const double PageCentre = 105;
    private const double SlotWidth = 80.4;
    private const double LeadSlotHeight = 36;

    private const string Red = "#c80a32";
    private const string Charcoal = "#2c2a27";
    private const string LabelGrey = "#f2f2f2";
    private const string TotalGrey = "#efefef";
    private const string GridGrey = "#bebebe";
    private const string Ink = "#222222";
    private const string Muted = "#555555";
    private const string Quiet = "#777777";

    public static TheoryData<string> Samples
    {
        get
        {
            var samples = new TheoryData<string>();
            foreach (var sample in TemplateSampleJobs.All)
            {
                samples.Add(sample.Name);
            }
            return samples;
        }
    }

    /// <summary>
    /// The sample's costs pass through the one owner of estimate money and
    /// come out as the template's generator derives them.
    /// </summary>
    [Theory]
    [MemberData(nameof(Samples))]
    public void TheCostsAreTheFiguresTheTemplateDerives(string name)
    {
        var sample = rendered[name].Sample;
        var costs = sample.Snapshot.Costs;

        Assert.Equal(sample.Money.LabourHours, costs.TotalLabourHours);
        Assert.Equal(sample.Money.HourlyRate, costs.HourlyRate);
        Assert.Equal(sample.Money.TotalLabour, costs.TotalLabour);
        Assert.Equal(sample.Money.Parts, costs.Printed.Parts);
        Assert.Equal(sample.Money.PaintMaterials, costs.Printed.Materials);
        Assert.Equal(sample.Money.SpecialistOther, costs.Printed.Specialist);
        Assert.Equal(sample.Money.SubTotal, costs.Printed.Net);
        Assert.Equal(sample.Money.Vat, costs.Printed.Vat);
        Assert.Equal(sample.Money.Total, costs.Total);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void TheReportHasTheTemplatesPagesAndTheSameHeadingOnEachPage(string name)
    {
        var (_, report, template) = rendered[name];

        Assert.Equal(template.Count, report.Count);
        Assert.Equal(7, report.Count);
        for (var index = 0; index < template.Count; index++)
        {
            var expected = Lines(template[index].Letters.Where(letter => IsSize(letter, 11)));
            var printed = Lines(report[index].Letters.Where(letter => IsSize(letter, 11)));
            Assert.Equal(expected.Select(line => line.Text), printed.Select(line => line.Text));
            foreach (var (heading, place) in printed.Zip(expected))
            {
                Assert.True(heading.Bold, $"Page {index + 1}: '{heading.Text}' is not bold.");
                Assert.Equal(place.Colour, heading.Colour);
                Assert.InRange(heading.Left, BodyLeft - Near, BodyLeft + Near);
                Assert.InRange(heading.Baseline, place.Baseline - Near, place.Baseline + Near);
            }
        }
        // Every heading of the report is red. The fee note's Terms is the template's one dark heading.
        Assert.All(
            report.Take(6).SelectMany(page => Lines(page.Letters.Where(letter => IsSize(letter, 11)))),
            heading => Assert.Equal(Red, heading.Colour));
        Assert.Equal(
            [Red, Ink],
            Lines(report[6].Letters.Where(letter => IsSize(letter, 11))).Select(line => line.Colour));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void TheTitlesAreSetAsTheTemplateSetsThem(string name)
    {
        var (sample, report, template) = rendered[name];

        foreach (var (page, title, italic) in new[]
        {
            (0, sample.Snapshot.Presentation().Title, true),
            (6, AssessmentReportWording.FeeNoteTitle, false),
        })
        {
            var expected = Assert.Single(Lines(template[page].Letters.Where(letter => IsSize(letter, 16))));
            var printed = Assert.Single(Lines(report[page].Letters.Where(letter => IsSize(letter, 16))));

            Assert.Equal(Squeeze(title), printed.Text);
            Assert.Equal(expected.Text, printed.Text);
            Assert.Equal(Red, printed.Colour);
            Assert.True(printed.Bold);
            Assert.Equal(italic, printed.Italic);
            Assert.Equal(expected.Italic, printed.Italic);
            Assert.InRange(printed.Baseline, expected.Baseline - Near, expected.Baseline + Near);
            Assert.InRange(printed.Left, expected.Left - Near, expected.Left + Near);
            Assert.InRange(printed.Right, expected.Right - Near, expected.Right + Near);
            // The rule beneath the title runs the width of the body.
            var rule = Assert.Single(Rules(report[page], Red), line => line.Top < BodyBottom);
            var set = Assert.Single(Once(template[page].Shapes.Where(shape =>
                shape.Fill == Red && !shape.Clip && shape.Height < 1 && shape.Top < BodyBottom)));
            AssertNear(set, rule, $"{name} page {page + 1} title rule");
            Assert.InRange(rule.Left, BodyLeft - Near, BodyLeft + Near);
            Assert.InRange(rule.Right, BodyRight - Near, BodyRight + Near);
        }
    }

    /// <summary>
    /// The strongest of the comparisons: every letter the template prints
    /// is printed in the same place, size, colour, weight and slant, and the
    /// report prints no letter the template does not. The page number is
    /// left out: the template sets it in its generator's fallback serif.
    /// </summary>
    [Theory]
    [MemberData(nameof(Samples))]
    public void EveryLetterStandsWhereTheTemplateSetsIt(string name)
    {
        var (_, report, template) = rendered[name];

        var differences = new List<string>();
        for (var index = 0; index < template.Count; index++)
        {
            var printed = report[index].Letters.Where(letter => !IsPageNumber(letter)).ToList();
            foreach (var expected in template[index].Letters.Where(letter => !IsPageNumber(letter)))
            {
                var match = printed.FindIndex(letter =>
                    letter.Value == expected.Value
                    && Math.Abs(letter.Left - expected.Left) <= Near
                    && Math.Abs(letter.Baseline - expected.Baseline) <= Near
                    && Math.Abs(letter.Size - expected.Size) <= SameSize
                    && letter.Colour == expected.Colour
                    && letter.Bold == expected.Bold
                    && letter.Italic == expected.Italic);
                if (match < 0)
                {
                    differences.Add($"page {index + 1}: the template's {Describe(expected)} is not printed there");
                    continue;
                }
                printed.RemoveAt(match);
            }
            differences.AddRange(printed.Select(letter =>
                $"page {index + 1}: {Describe(letter)} is printed and the template has none there"));
        }

        Assert.True(
            differences.Count == 0,
            $"{name}: {differences.Count} letters differ from the template.\n"
                + string.Join('\n', differences.Take(40)));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void TheBodyBeginsAtTheTemplatesLeftEdge(string name)
    {
        var (_, report, template) = rendered[name];

        for (var index = 0; index < template.Count; index++)
        {
            Assert.InRange(LeftEdge(template[index]), BodyLeft - Near, BodyLeft + Near);
            Assert.InRange(LeftEdge(report[index]), BodyLeft - Near, BodyLeft + Near);
            Assert.InRange(RightEdge(report[index]), 0, BodyRight + Near);
        }
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void TheHeaderAndTheFooterRunOnEveryPage(string name)
    {
        var (sample, report, template) = rendered[name];

        for (var index = 0; index < template.Count; index++)
        {
            var feeNote = index == template.Count - 1;
            var where = $"{name} page {index + 1}";

            // The company block, against the right edge.
            var block = Lines(report[index].Letters.Where(letter => letter.Baseline < BodyTop));
            var set = Lines(template[index].Letters.Where(letter => letter.Baseline < BodyTop));
            Assert.Equal(
                AssessmentReportWording.CompanyBlock(feeNote).Select(Squeeze),
                block.Select(line => line.Text));
            Assert.Equal(set.Select(line => line.Text), block.Select(line => line.Text));
            foreach (var (line, place) in block.Zip(set))
            {
                Assert.InRange(line.Right, HeaderRight - Near, HeaderRight + Near);
                Assert.InRange(line.Baseline, place.Baseline - Near, place.Baseline + Near);
                Assert.InRange(line.Size, place.Size - SameSize, place.Size + SameSize);
                Assert.Equal(place.Colour, line.Colour);
                Assert.Equal(place.Bold, line.Bold);
                Assert.Equal(place.Italic, line.Italic);
            }
            Assert.Equal((10d, Ink, true, false), (block[0].Size, block[0].Colour, block[0].Bold, block[0].Italic));
            Assert.Equal((8.5d, Muted, false, true), (block[1].Size, block[1].Colour, block[1].Bold, block[1].Italic));
            Assert.All(block.Skip(2), line => Assert.Equal((8.5d, Muted, false, false), (line.Size, line.Colour, line.Bold, line.Italic)));

            // The logo.
            var logo = Assert.Single(report[index].Images, image => image.Bottom < BodyTop);
            AssertNear(
                Assert.Single(Once(template[index].Images.Where(image => image.Bottom < BodyTop))),
                logo,
                where + " logo");

            // The footer, centred beneath its rule, and the page number at the right.
            var footer = Assert.Single(Lines(report[index].Letters.Where(letter =>
                letter.Baseline > BodyBottom && IsSize(letter, 8))));
            var setFooter = Assert.Single(Lines(template[index].Letters.Where(letter =>
                letter.Baseline > BodyBottom && IsSize(letter, 8))));
            Assert.Equal(Squeeze(AssessmentReportWording.Footer(sample.Snapshot, feeNote)), footer.Text);
            Assert.Equal(setFooter.Text, footer.Text);
            Assert.Equal(Quiet, footer.Colour);
            Assert.InRange((footer.Left + footer.Right) / 2, PageCentre - Near, PageCentre + Near);
            Assert.InRange(footer.Baseline, setFooter.Baseline - Near, setFooter.Baseline + Near);

            var number = Assert.Single(Lines(report[index].Letters.Where(IsPageNumber)));
            Assert.Equal(Squeeze(AssessmentReportWording.PageNumber(index + 1, template.Count)), number.Text);
            Assert.Equal(number.Text, Assert.Single(Lines(template[index].Letters.Where(IsPageNumber))).Text);
            Assert.Equal((7.5d, Quiet), (number.Size, number.Colour));
            Assert.InRange(number.Right, HeaderRight - Near, HeaderRight + Near);

            var rule = Assert.Single(Rules(report[index], Red), line => line.Top > BodyBottom);
            var setRule = Assert.Single(Once(template[index].Shapes.Where(shape =>
                shape.Fill == Red && !shape.Clip && shape.Top > BodyBottom)));
            Assert.InRange(rule.Top, setRule.Top - Near, setRule.Top + Near);
            Assert.InRange(rule.Left, setRule.Left - Near, setRule.Left + Near);
            Assert.InRange(rule.Right, setRule.Right - Near, setRule.Right + Near);
        }
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void TheImagesFillTheTemplatesSlots(string name)
    {
        var (_, report, template) = rendered[name];

        // Page 1: the Overview at the left and the damage diagram at the right, each slot 80.4 by 36 mm.
        var slots = Slots(template[0], LeadSlotHeight);
        Assert.Equal(2, slots.Count);
        var lead = Assert.Single(report[0].Images, image => image.Top > BodyTop);
        AssertFitsWhole(slots[0], lead, name + " page 1 image");
        var drawing = report[0].Shapes
            .Where(shape => !shape.Clip && shape.Top > slots[1].Top - Near && shape.Bottom < BodyBottom)
            .ToArray();
        Assert.NotEmpty(drawing);
        Assert.All(drawing, shape =>
        {
            Assert.InRange(shape.Left, slots[1].Left - Near, slots[1].Right + Near);
            Assert.InRange(shape.Right, slots[1].Left - Near, slots[1].Right + Near);
            Assert.InRange(shape.Top, slots[1].Top - Near, slots[1].Bottom + Near);
            Assert.InRange(shape.Bottom, slots[1].Top - Near, slots[1].Bottom + Near);
        });
        // The drawing stands in the middle of its slot, as the template's picture does.
        var picture = Assert.Single(template[0].Images, image => image.Left > PageCentre);
        Assert.InRange(
            (drawing.Min(shape => shape.Top) + drawing.Max(shape => shape.Bottom)) / 2,
            (picture.Top + picture.Bottom) / 2 - Near,
            (picture.Top + picture.Bottom) / 2 + Near);
        Assert.InRange(
            (drawing.Min(shape => shape.Left) + drawing.Max(shape => shape.Right)) / 2,
            (slots[1].Left + slots[1].Right) / 2 - 1,
            (slots[1].Left + slots[1].Right) / 2 + 1);

        // The image page: six images two across, each at the column's 80.4 mm
        // width and its own shape (operator, 8 October 2026), so it departs
        // from the template's fixed slots. A row's images share a middle.
        var images = report[4].Images
            .Where(image => image.Top > BodyTop)
            .OrderBy(image => Math.Round((image.Top + image.Bottom) / 2))
            .ThenBy(image => image.Left)
            .ToArray();
        Assert.Equal(6, images.Length);
        for (var index = 0; index < images.Length; index++)
        {
            var image = images[index];
            Assert.InRange(image.Width, SlotWidth - Near, SlotWidth + Near);
            var left = index % 2 == 0 ? BodyLeft : BodyRight - SlotWidth;
            Assert.InRange(image.Left, left - Near, left + Near);
        }
        // No other page carries a photograph.
        Assert.All(
            report.Where((_, index) => index is not (0 or 4 or 5)),
            page => Assert.DoesNotContain(page.Images, image => image.Top > BodyTop));
    }

    /// <summary>
    /// The signature stands in the template's slot, 52 by 27.4 mm.
    /// </summary>
    [Theory]
    [MemberData(nameof(Samples))]
    public void TheSignatureStandsInTheTemplatesSlot(string name)
    {
        var (_, report, template) = rendered[name];

        var signature = Assert.Single(report[5].Images, image => image.Top > BodyTop);
        AssertNear(Assert.Single(template[5].Images, image => image.Top > BodyTop), signature, name + " signature");
        Assert.InRange(signature.Width, 52 - 0.2, 52 + 0.2);
        Assert.InRange(signature.Height, 27.4 - 0.2, 27.4 + 0.2);
    }

    /// <summary>
    /// The label cells, the tiles, the total rows, the badges and the red
    /// header rows: the same fills in the same places. The roadworthiness
    /// badge is red only on an unroadworthy vehicle's report.
    /// </summary>
    [Theory]
    [MemberData(nameof(Samples))]
    public void TheFillsAreTheTemplates(string name)
    {
        var (sample, report, template) = rendered[name];

        for (var index = 0; index < template.Count; index++)
        {
            foreach (var colour in new[] { LabelGrey, TotalGrey, Charcoal })
            {
                var expected = Fills(template[index], colour);
                var printed = Fills(report[index], colour);
                Assert.True(
                    expected.Count == printed.Count,
                    $"{name} page {index + 1}: the template fills {expected.Count} boxes {colour} and the report {printed.Count}.");
                foreach (var (box, place) in printed.Zip(expected))
                {
                    AssertNear(place, box, $"{name} page {index + 1} fill {colour}");
                }
            }

            // The template draws a red border as a red box beneath what it
            // borders, so its red boxes are more than the report's. Each of
            // the report's is one of the template's, and each of the
            // template's that is not a border or a rule is one of the report's.
            var reds = Fills(template[index], Red);
            var blocks = Fills(report[index], Red);
            foreach (var block in blocks)
            {
                Assert.Contains(reds, red => IsNear(red, block));
            }
            var labels = Fills(template[index], LabelGrey);
            foreach (var red in reds.Where(red =>
                red.Height > 1 && red.Top < BodyBottom && !labels.Any(label => Holds(red, label))))
            {
                Assert.Contains(blocks, block => IsNear(red, block));
            }
        }

        Assert.Equal(8, Fills(report[0], LabelGrey).Count);
        Assert.Equal(6, Fills(report[6], LabelGrey).Count);
        var badges = Fills(report[0], Charcoal).Count;
        Assert.Equal(sample.Snapshot.IsUnroadworthy ? 1 : 2, badges);
    }

    /// <summary>
    /// The hairline rules and grid lines: the report draws a line wherever
    /// the template draws one and nowhere else. The value box's border is
    /// the template's.
    /// </summary>
    [Theory]
    [MemberData(nameof(Samples))]
    public void TheRulesAreTheTemplates(string name)
    {
        var (_, report, template) = rendered[name];

        for (var index = 0; index < template.Count; index++)
        {
            // The image page's frames are gone (operator, 8 October 2026).
            if (index == 4)
            {
                continue;
            }
            var expected = Segments(template[index]);
            var printed = Segments(report[index]);
            var where = $"{name} page {index + 1}";
            Assert.All(Points(expected), point => Assert.True(
                printed.Any(line => Distance(point, line) <= Near),
                $"{where}: the template rules through {point.X:0.0}, {point.Y:0.0} and the report does not."));
            Assert.All(Points(printed), point => Assert.True(
                expected.Any(line => Distance(point, line) <= Near),
                $"{where}: the report rules through {point.X:0.0}, {point.Y:0.0} and the template does not."));
        }

        // The value box: a red border about the template's box, on page 2.
        var border = Assert.Single(Once(report[1].Shapes.Where(shape => shape.Stroke == Red && shape.Height > 1)));
        var outer = border with
        {
            Left = border.Left - border.LineWidth / 2,
            Top = border.Top - border.LineWidth / 2,
            Right = border.Right + border.LineWidth / 2,
            Bottom = border.Bottom + border.LineWidth / 2,
        };
        var label = Assert.Single(Fills(template[1], LabelGrey));
        AssertNear(
            Assert.Single(Fills(template[1], Red), red => Holds(red, label)),
            outer,
            name + " value box");
    }

    // ---- Reading a printed page --------------------------------------------

    private static bool IsSize(PrintedLetter letter, double size) => Math.Abs(letter.Size - size) <= SameSize;

    /// <summary>The page number stands at the foot of the page, at the right.</summary>
    private static bool IsPageNumber(PrintedLetter letter) =>
        letter.Baseline > BodyBottom && IsSize(letter, 7.5);

    private static double LeftEdge(PrintedPage page) => Math.Min(
        page.Letters.Where(InBody).Select(letter => letter.Left).DefaultIfEmpty(double.MaxValue).Min(),
        page.Shapes
            .Where(shape => !shape.Clip && shape.Fill is not null && shape.Top > BodyTop && shape.Bottom < BodyBottom)
            .Select(shape => shape.Left)
            .DefaultIfEmpty(double.MaxValue)
            .Min());

    private static double RightEdge(PrintedPage page) => Math.Max(
        page.Letters.Where(InBody).Select(letter => letter.Right).DefaultIfEmpty(0).Max(),
        page.Images.Where(image => image.Top > BodyTop).Select(image => image.Right).DefaultIfEmpty(0).Max());

    private static bool InBody(PrintedLetter letter) => letter.Baseline > BodyTop && letter.Baseline < BodyBottom;

    /// <summary>The boxes a page fills with one colour, each once, top to bottom and left to right.</summary>
    private static List<PrintedBox> Fills(PrintedPage page, string colour) =>
    [
        .. Once(page.Shapes.Where(shape =>
                !shape.Clip && shape.Fill == colour && shape.Height > 1 && shape.Width > 1))
            .OrderBy(shape => Math.Round(shape.Top))
            .ThenBy(shape => shape.Left),
    ];

    /// <summary>Each box once. The template draws some of its boxes twice over.</summary>
    private static IEnumerable<PrintedBox> Once(IEnumerable<PrintedBox> boxes) =>
        boxes.DistinctBy(box => (
            Math.Round(box.Left, 1), Math.Round(box.Top, 1),
            Math.Round(box.Right, 1), Math.Round(box.Bottom, 1)));

    /// <summary>
    /// The rules of one colour a page draws, as the boxes they cover. The
    /// report strokes a rule about its middle; the template fills one.
    /// </summary>
    private static List<PrintedBox> Rules(PrintedPage page, string colour) =>
    [
        .. page.Shapes
            .Where(shape => !shape.Clip && shape.Stroke == colour && shape.Height < 0.1)
            .Select(shape => shape with
            {
                Top = shape.Top - shape.LineWidth / 2,
                Bottom = shape.Bottom + shape.LineWidth / 2,
            }),
    ];

    /// <summary>The template's image slots of one height: what it clips its images to.</summary>
    private static List<PrintedBox> Slots(PrintedPage page, double height) =>
    [
        .. Once(page.Shapes.Where(shape =>
                shape.Clip
                && Math.Abs(shape.Height - height) <= 0.2
                && Math.Abs(shape.Width - SlotWidth) <= 0.2))
            .OrderBy(shape => Math.Round(shape.Top))
            .ThenBy(shape => shape.Left),
    ];

    /// <summary>
    /// The grey lines a page draws, as lines along their middles: a rule or
    /// grid line as itself, and the frame about an image as its four sides.
    /// </summary>
    private static List<PrintedSegment> Segments(PrintedPage page)
    {
        var segments = new List<PrintedSegment>();
        foreach (var shape in page.Shapes.Where(shape =>
            !shape.Clip && (shape.Fill == GridGrey || shape.Stroke == GridGrey)))
        {
            var middleX = (shape.Left + shape.Right) / 2;
            var middleY = (shape.Top + shape.Bottom) / 2;
            if (shape.Height < 0.6)
            {
                segments.Add(new(shape.Left, middleY, shape.Right, middleY));
            }
            else if (shape.Width < 0.6)
            {
                segments.Add(new(middleX, shape.Top, middleX, shape.Bottom));
            }
            else
            {
                segments.Add(new(shape.Left, shape.Top, shape.Right, shape.Top));
                segments.Add(new(shape.Left, shape.Bottom, shape.Right, shape.Bottom));
                segments.Add(new(shape.Left, shape.Top, shape.Left, shape.Bottom));
                segments.Add(new(shape.Right, shape.Top, shape.Right, shape.Bottom));
            }
        }
        return segments;
    }

    /// <summary>Points along lines, no more than two millimetres apart, and their ends.</summary>
    private static IEnumerable<(double X, double Y)> Points(IEnumerable<PrintedSegment> segments)
    {
        foreach (var segment in segments)
        {
            var length = Math.Sqrt(
                Math.Pow(segment.ToX - segment.FromX, 2) + Math.Pow(segment.ToY - segment.FromY, 2));
            var steps = Math.Max(1, (int)Math.Ceiling(length / 2));
            for (var step = 0; step <= steps; step++)
            {
                var along = (double)step / steps;
                yield return (
                    segment.FromX + (segment.ToX - segment.FromX) * along,
                    segment.FromY + (segment.ToY - segment.FromY) * along);
            }
        }
    }

    private static double Distance((double X, double Y) point, PrintedSegment segment)
    {
        var x = Math.Clamp(point.X, Math.Min(segment.FromX, segment.ToX), Math.Max(segment.FromX, segment.ToX));
        var y = Math.Clamp(point.Y, Math.Min(segment.FromY, segment.ToY), Math.Max(segment.FromY, segment.ToY));
        return Math.Sqrt(Math.Pow(point.X - x, 2) + Math.Pow(point.Y - y, 2));
    }

    private static bool Holds(PrintedBox outer, PrintedBox inner) =>
        inner.Left >= outer.Left - Near && inner.Right <= outer.Right + Near
        && inner.Top >= outer.Top - Near && inner.Bottom <= outer.Bottom + Near;

    private static bool IsNear(PrintedBox expected, PrintedBox printed) =>
        Math.Abs(printed.Left - expected.Left) <= Near
        && Math.Abs(printed.Top - expected.Top) <= Near
        && Math.Abs(printed.Right - expected.Right) <= Near
        && Math.Abs(printed.Bottom - expected.Bottom) <= Near;

    private static void AssertNear(PrintedBox expected, PrintedBox printed, string what) => Assert.True(
        IsNear(expected, printed),
        string.Create(
            CultureInfo.InvariantCulture,
            $"{what}: the template sets {expected.Left:0.0} {expected.Top:0.0} {expected.Right:0.0} {expected.Bottom:0.0} and the report {printed.Left:0.0} {printed.Top:0.0} {printed.Right:0.0} {printed.Bottom:0.0}."));

    /// <summary>
    /// An image prints whole in its slot (operator, 7 October 2026): inside
    /// it, centred, and meeting the slot's edges across or down.
    /// </summary>
    private static void AssertFitsWhole(PrintedBox slot, PrintedBox printed, string what) => Assert.True(
        printed.Left >= slot.Left - Near
        && printed.Right <= slot.Right + Near
        && printed.Top >= slot.Top - Near
        && printed.Bottom <= slot.Bottom + Near
        && Math.Abs((printed.Left + printed.Right - slot.Left - slot.Right) / 2) <= Near
        && Math.Abs((printed.Top + printed.Bottom - slot.Top - slot.Bottom) / 2) <= Near
        && (Math.Abs(printed.Width - slot.Width) <= Near || Math.Abs(printed.Height - slot.Height) <= Near),
        string.Create(
            CultureInfo.InvariantCulture,
            $"{what}: the template's slot is {slot.Left:0.0} {slot.Top:0.0} {slot.Right:0.0} {slot.Bottom:0.0} and the report's image {printed.Left:0.0} {printed.Top:0.0} {printed.Right:0.0} {printed.Bottom:0.0}."));

    private static string Describe(PrintedLetter letter) => string.Create(
        CultureInfo.InvariantCulture,
        $"'{letter.Value}' at {letter.Left:0.0}, {letter.Baseline:0.0} ({letter.Size:0.#}pt {letter.Colour}{(letter.Bold ? " bold" : null)}{(letter.Italic ? " italic" : null)})");
}

/// <summary>One sample job, the report rendered for it and the report the template printed for it.</summary>
internal sealed record RenderedTemplateSample(
    TemplateSample Sample,
    IReadOnlyList<PrintedPage> Report,
    IReadOnlyList<PrintedPage> Template);

/// <summary>
/// The four sample jobs rendered once for the class, and each rendered
/// report written beneath the test's output directory so that a run can keep
/// it.
/// </summary>
public sealed class RenderedTemplateSamples : IAsyncLifetime
{
    /// <summary>The folder beneath the test's output directory that holds the rendered reports.</summary>
    public const string KeptFolder = "rendered-reports";

    private readonly Dictionary<string, RenderedTemplateSample> samples = new(StringComparer.Ordinal);

    internal RenderedTemplateSample this[string name] => samples[name];

    public async Task InitializeAsync()
    {
        var reference = Path.Combine(RepositoryRoot(), "reference", "rendererref1");
        var kept = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, KeptFolder)).FullName;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPegasusReportRendering();
        await using var provider = services.BuildServiceProvider();
        var draft = new GenerateAssessmentReportDraft(provider.GetRequiredService<IAssessmentReportRenderer>());
        foreach (var (name, _, _) in TemplateSampleJobs.All)
        {
            var sample = TemplateSampleJobs.Load(reference, name);
            var artifact = await draft.ExecuteAsync(sample.Snapshot, CaseReportArtifactKind.AssessmentReport);
            await File.WriteAllBytesAsync(Path.Combine(kept, name + ".pdf"), artifact.Pdf);
            samples[name] = new(
                sample,
                PrintedPages.Read(artifact.Pdf),
                PrintedPages.Read(await File.ReadAllBytesAsync(sample.ReferencePdf)));
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Pegasus.slnx")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
