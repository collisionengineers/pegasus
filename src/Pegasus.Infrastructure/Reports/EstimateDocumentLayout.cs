using System.Globalization;
using Pegasus.Core.Assessment;
using Pegasus.Core.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static Pegasus.Core.Reports.AssessmentReportWording;

namespace Pegasus.Infrastructure.Reports;

/// <summary>
/// The Repair Spec printout, set as the EVA breakdown (operator, 6 November
/// 2026; R750 to R754). Every measurement is the EVA example's, in
/// millimetres from the page's top left. The logo band, the identity lines
/// and the EVA band print on the first page only; the line table's heading
/// repeats on each page it runs to; the summary boxes follow the table whole.
/// </summary>
internal static class EstimateDocumentLayout
{
    private const float PageWidth = 210f;
    private const float PageHeight = 297f;

    // Liberation Sans sets its baseline this fraction of the type size below
    // the top of a line.
    private const float Ascent = 0.905f;
    private const float PointToMillimetre = 25.4f / 72f;

    private const float LineSize = 7.44f;
    private const float IdentitySize = 10.08f;
    private const float SummarySize = 7.92f;

    private static readonly Color Rule = Color.FromHex("#a9a9a9");
    private static readonly Color Zebra = Color.FromHex("#dcdcdc");
    private const float RuleWidth = 0.5f;

    // The bands: the logo band at the top of the first page, the EVA band at its foot,
    // each stretched to its rectangle as the example stretches it.
    private const float BandLeft = 5.08f;
    private const float BandWidth = 194.99f;
    private const float HeaderBandHeight = 44.03f;
    private const float FooterBandTop = 264.08f;
    private const float FooterBandHeight = 25.99f;
    private const float BottomMargin = PageHeight - FooterBandTop - FooterBandHeight;

    // The identity lines: labels end at 140.40, values start at 141.56.
    private const float IdentityLabelRight = 140.40f;
    private const float IdentityValueLeft = 141.56f;
    private static readonly float[] IdentityBaselines = [46.23f, 53.25f, 60.20f, 67.22f, 73.24f];

    // The line table: from 6.01 to 203.03, its heading from 84.07, 4 mm rows.
    private const float TableLeft = 6.01f;
    private const float TableRight = 203.03f;
    private const float TableTop = 84.07f;
    private const float TableHeadingHeight = 3.98f;
    private const float RowHeight = 4f;
    private const float TableRadius = 3f;
    private const float RowBaseline = 2.37f;
    private const float DescriptionIndent = 17.44f - 12.55f;

    /// <summary>The line table's column edges; Qty and the figures sit against the right edge.</summary>
    private static readonly float[] Edges =
        [TableLeft, 12.55f, 70.44f, 86.0f, 100.60f, 116.61f, 132.61f, 148.61f, 164.62f, 180.53f, 199.58f, TableRight];

    private static readonly string[] ColumnHeadings =
        ["Qty", "Description", "Type", "Labour", "Paint", "Litres", "Mat Price", "Anti-C", "A/C Price", "Price", ""];

    private const int Description = 1;
    private const int Type = 2;

    // The summary boxes, from the top of the summary: 64.01 tall (with 1 spare for the
    // outline), 1.95 below the table.
    // On a page of their own they open at 9.06, so a continuation page's body opens at 7.11.
    private const float SummaryGap = 1.95f;
    private const float ContinuationTop = 9.06f - SummaryGap;
    private const float SummaryHeight = 65.01f;

    internal static Document Compose(EstimateDocumentSnapshot snapshot, PageCountProbe pageCount) =>
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageWidth, PageHeight, Unit.Millimetre);
            page.Margin(0);
            page.MarginBottom(BottomMargin, Unit.Millimetre);
            page.DefaultTextStyle(style => style
                .FontFamily(ReportChrome.FontFamily)
                .FontColor(Colors.Black)
                .FontSize(LineSize));
            page.Header().Column(header =>
            {
                header.Item().ShowOnce().Height(TableTop, Unit.Millimetre).Layers(layers =>
                {
                    layers.PrimaryLayer()
                        .PaddingLeft(BandLeft, Unit.Millimetre)
                        .Width(BandWidth, Unit.Millimetre)
                        .Height(HeaderBandHeight, Unit.Millimetre)
                        .Image(ReportResources.EvaHeader())
                        .FitUnproportionally();
                    Identity(layers, snapshot);
                });
                header.Item().SkipOnce().Height(ContinuationTop, Unit.Millimetre);
            });
            page.Content().Column(content =>
            {
                content.Item()
                    .PaddingLeft(TableLeft, Unit.Millimetre)
                    .Width(TableRight - TableLeft, Unit.Millimetre)
                    .Element(table => LineTable(table, snapshot.Lines));
                content.Item()
                    .ShowEntire()
                    .PaddingTop(SummaryGap, Unit.Millimetre)
                    .Height(SummaryHeight, Unit.Millimetre)
                    .Layers(layers => Summary(layers, snapshot));
            });
            page.Footer().Column(footer =>
            {
                footer.Item().ShowOnce()
                    .PaddingLeft(BandLeft, Unit.Millimetre)
                    .Width(BandWidth, Unit.Millimetre)
                    .Height(FooterBandHeight, Unit.Millimetre)
                    .Image(ReportResources.EvaFooter())
                    .FitUnproportionally();
                footer.Item().Height(0).Dynamic(pageCount);
            });
        }));

    private static void Identity(LayersDescriptor layers, EstimateDocumentSnapshot snapshot)
    {
        (string Label, string Value)[] lines =
        [
            ("Date:", Date(snapshot.DocumentDate)),
            ("Our Ref:", snapshot.OurReference),
            ("Claimant:", snapshot.ClaimantName),
            ("Vehicle:", snapshot.VehicleDescription),
            ("Registration:", snapshot.Registration),
        ];
        for (var index = 0; index < lines.Length; index++)
        {
            var (label, value) = lines[index];
            layers.Layer()
                .PaddingTop(TopFor(IdentityBaselines[index], IdentitySize), Unit.Millimetre)
                .DefaultTextStyle(style => style.FontSize(IdentitySize))
                .Row(row =>
                {
                    row.ConstantItem(IdentityLabelRight, Unit.Millimetre).AlignRight().Text(label);
                    row.ConstantItem(IdentityValueLeft - IdentityLabelRight, Unit.Millimetre);
                    row.RelativeItem()
                        .PaddingRight(PageWidth - TableRight, Unit.Millimetre)
                        .Text(value)
                        .ClampLines(1);
                });
        }
    }

    private static void LineTable(IContainer container, IReadOnlyList<EstimateDocumentLine> lines) =>
        container
            .Border(RuleWidth, Unit.Millimetre)
            .BorderColor(Rule)
            .CornerRadius(TableRadius, Unit.Millimetre)
            .DefaultTextStyle(style => style.FontSize(LineSize).Bold())
            .Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    for (var index = 1; index < Edges.Length; index++)
                    {
                        columns.RelativeColumn(Edges[index] - Edges[index - 1]);
                    }
                });
                table.Header(header =>
                {
                    for (var column = 0; column < ColumnHeadings.Length; column++)
                    {
                        LineCell(
                            header.Cell()
                                .Height(TableHeadingHeight, Unit.Millimetre)
                                .BorderBottom(RuleWidth, Unit.Millimetre)
                                .BorderColor(Colors.Black),
                            column,
                            ColumnHeadings[column]);
                    }
                });
                for (var index = 0; index < lines.Count; index++)
                {
                    var line = lines[index];
                    string[] cells =
                    [
                        line.Quantity.ToString(CultureInfo.InvariantCulture),
                        line.Description,
                        Operation(line.Operation),
                        OptionalHours(line.Hours),
                        OptionalHours(line.PaintHours),
                        string.Empty,
                        OptionalMoney(line.Materials),
                        string.Empty,
                        string.Empty,
                        OptionalMoney(line.Price),
                        string.Empty,
                    ];
                    for (var column = 0; column < cells.Length; column++)
                    {
                        IContainer cell = table.Cell();
                        if (index % 2 == 1)
                        {
                            cell = cell.Background(Zebra);
                        }
                        LineCell(cell.MinHeight(RowHeight, Unit.Millimetre), column, cells[column]);
                    }
                }
            });

    private static void LineCell(IContainer cell, int column, string text)
    {
        cell = cell.PaddingTop(TopFor(RowBaseline, LineSize), Unit.Millimetre);
        cell = column switch
        {
            Description => cell.PaddingLeft(DescriptionIndent, Unit.Millimetre).AlignLeft(),
            Type => cell.AlignLeft(),
            _ => cell.AlignRight(),
        };
        cell.Text(text);
    }

    /// <summary>
    /// The three rows of boxes: the hours; the rate and discount rates beside
    /// the four categories before discount; the discounts beside the totals.
    /// </summary>
    private static void Summary(LayersDescriptor layers, EstimateDocumentSnapshot snapshot)
    {
        layers.PrimaryLayer();
        var hours = snapshot.Hours;
        var breakdown = snapshot.Breakdown;
        var printed = snapshot.Totals.Printed;

        Box(layers, 14.05f, 0f, [22f, 22f, 22f, 22f, 22f, 22f, 22f, 22.03f], 2f,
        [
            Headings(3.55f, "New", "Repair", "Paint", "R & R", "Blend", "Specialist", "Check", "Total Hours"),
            Values(3.64f,
                SummaryHours(hours.Replace), SummaryHours(hours.Repair), SummaryHours(hours.PaintTotal),
                SummaryHours(hours.RemoveAndRefit), SummaryHours(hours.BlendTotal),
                SummaryHours(hours.SpecialistPriced), SummaryHours(hours.Check), Hours(hours.PricedTotal)),
        ], [7.96f, 8.04f], centreShift: 0.55f);

        Box(layers, 12.02f, 17.95f, [22.02f, 22.01f, 22.01f, 22.02f], 2f,
        [
            Headings(3.64f, "Labour Rate", "Labour Disc", "Material Dis", "Parts Dis"),
            Values(3.56f,
                Money(snapshot.HourlyRate),
                Percent(breakdown.LabourDiscountRate),
                Percent(breakdown.MaterialsDiscountRate),
                Percent(breakdown.PartsDiscountRate)),
        ], [8.04f, 7.96f], centreShift: 0.55f);

        Box(layers, 104.06f, 17.95f, [22.01f, 22.01f, 21.93f, 22.01f], 2f,
        [
            Headings(3.64f, "Labour", "Materials", "Parts", "Other"),
            Values(3.56f,
                Money(breakdown.Labour), Money(breakdown.Materials),
                Money(breakdown.Parts), Money(breakdown.Other)),
        ], [8.04f, 7.96f], centreShift: 0.55f);

        Box(layers, 7.03f, 35.98f, [32f, 32f, 32.01f, 32f], 2f,
        [
            Headings(3.56f, "Labour Discount", "Materials Discount", "Parts Discount", "Other Discounts"),
            Values(2.63f,
                Money(breakdown.LabourDiscount), Money(breakdown.MaterialsDiscount),
                Money(breakdown.PartsDiscount), Money(breakdown.OtherDiscount)),
        ], [7.96f, 8.04f]);

        BoxCell[] Total(string label, decimal value) =>
            [new(label, Bold: true, 3.56f), new(Money(value), Bold: true, 3.56f, RightInset: 3.53f)];
        Box(layers, 149.01f, 35.98f, [23.03f, 23.03f], 1f,
        [
            Total("Total Disc", breakdown.TotalDiscount),
            Total("Nett", printed.Net),
            Total("Vat", printed.Vat),
            Total("Gross", printed.Gross),
        ], [7.03f, 7.0f, 7.0f, 7.0f], centreShift: -0.9f);
    }

    /// <summary>
    /// One box of the summary: a rounded grey outline with grey rules between
    /// its cells. A cell's text sits on the baseline it names, measured from
    /// the top of its row, centred unless it is set in from the right.
    /// </summary>
    private static void Box(
        LayersDescriptor layers,
        float left,
        float top,
        float[] widths,
        float radius,
        BoxCell[][] rows,
        float[] heights,
        float centreShift = 0f) =>
        layers.Layer()
            .PaddingLeft(left, Unit.Millimetre)
            .PaddingTop(top, Unit.Millimetre)
            .AlignLeft()
            .AlignTop()
            .Width(widths.Sum(), Unit.Millimetre)
            .Border(RuleWidth, Unit.Millimetre)
            .BorderColor(Rule)
            .CornerRadius(radius, Unit.Millimetre)
            .DefaultTextStyle(style => style.FontSize(SummarySize))
            .Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    foreach (var width in widths)
                    {
                        columns.RelativeColumn(width);
                    }
                });
                for (var row = 0; row < rows.Length; row++)
                {
                    for (var column = 0; column < widths.Length; column++)
                    {
                        var cell = table.Cell().Height(heights[row], Unit.Millimetre);
                        if (column > 0)
                        {
                            cell = cell.BorderLeft(RuleWidth, Unit.Millimetre).BorderColor(Rule);
                        }
                        if (row > 0)
                        {
                            cell = cell.BorderTop(RuleWidth, Unit.Millimetre).BorderColor(Rule);
                        }
                        var content = rows[row][column];
                        cell = cell.PaddingTop(TopFor(content.Baseline, SummarySize), Unit.Millimetre);
                        cell = content.RightInset is { } inset
                            ? cell.PaddingRight(inset, Unit.Millimetre).AlignRight()
                            : Centred(cell, centreShift);
                        var text = cell.Text(content.Text);
                        if (content.Bold)
                        {
                            text.Bold();
                        }
                    }
                }
            });

    /// <summary>
    /// Centres a box's text as the example does, which differs by box: up to
    /// 0.55 left (a positive shift) or 0.9 right of the middle of its cell.
    /// </summary>
    private static IContainer Centred(IContainer cell, float shift) => shift >= 0f
        ? cell.PaddingRight(2 * shift, Unit.Millimetre).AlignCenter()
        : cell.PaddingLeft(-2 * shift, Unit.Millimetre).AlignCenter();

    private readonly record struct BoxCell(string Text, bool Bold, float Baseline, float? RightInset = null);

    private static BoxCell[] Headings(float baseline, params string[] labels) =>
        labels.Select(label => new BoxCell(label, Bold: true, baseline)).ToArray();

    private static BoxCell[] Values(float baseline, params string[] values) =>
        values.Select(value => new BoxCell(value, Bold: false, baseline)).ToArray();

    /// <summary>How far below the top of its box a line of text starts, to sit on the baseline given.</summary>
    private static float TopFor(float baseline, float size) =>
        Math.Max(0f, baseline - Ascent * size * PointToMillimetre);

    private static string Operation(EstimateOperation operation) => operation switch
    {
        EstimateOperation.Replace => "New",
        EstimateOperation.Repair => "Repair",
        EstimateOperation.RemoveAndRefit => "R & R",
        EstimateOperation.Paint => "Paint",
        EstimateOperation.Blend => "Blend",
        EstimateOperation.Specialist => "Specialist",
        EstimateOperation.Other => "Check",
        _ => throw new ReportRenderRejectedException("The estimate document has an unsupported operation."),
    };

    // The example leaves a figure that is nought, or not recorded, blank.
    private static string OptionalHours(decimal? value) => value is { } amount && amount != 0m ? Hours(amount) : string.Empty;
    private static string OptionalMoney(decimal? value) => value is { } amount && amount != 0m ? Money(amount) : string.Empty;
    private static string SummaryHours(decimal value) => value != 0m ? Hours(value) : string.Empty;

    private static string Percent(decimal fraction) =>
        (fraction * 100m).ToString("0.00", CultureInfo.InvariantCulture) + "%";
}
