using System.Globalization;
using Pegasus.Core.Reports;
using QuestPDF.Elements.Table;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pegasus.Infrastructure.Reports;

/// <summary>Shared Collision Engineers print chrome; no report workflow policy lives here.</summary>
internal static class ReportChrome
{
    internal const string FontFamily = "Liberation Sans";
    internal const float DataRegister = 8.8f;
    internal const float LetterRegister = 10f;
    internal const float BodyLineHeight = 1.22f;
    internal const float SectionGap = 3.5f;
    internal const float ParagraphGap = 3.1f;
    internal const float HeadingGap = 2f;

    internal static readonly Color Ink = Color.FromHex("#222222");
    internal static readonly Color Muted = Color.FromHex("#555555");
    internal static readonly Color Brand = Color.FromHex("#c80a32");
    internal static readonly Color Charcoal = Color.FromHex("#2c2a27");
    internal static readonly Color Rule = Color.FromHex("#bebebe");
    internal static readonly Color Zebra = Color.FromHex("#f5f5f5");
    internal static readonly Color Shade = Color.FromHex("#f2f2f2");
    internal static readonly Color DiagramInk = Color.FromHex("#75828a");

    internal const string CompanyName = "Collision Engineers Ltd";
    internal const string CompanyEmail = "Engineers@CollisionEngineers.co.uk";
    internal const string CompanyWebsite = "www.CollisionEngineers.co.uk";
    internal const string FooterSeparator = " | ";

    internal static void Footer(
        IContainer container,
        AssessmentReportSnapshot snapshot,
        bool feeNote)
    {
        var centre = feeNote
            ? $"{snapshot.Vehicle.Registration} · {snapshot.OurReference}{FooterSeparator}{CompanyName}{FooterSeparator}VAT No: {AssessmentReportContract.VatNumber}"
            : $"{snapshot.Vehicle.Registration} · {snapshot.OurReference}{FooterSeparator}{CompanyName}{FooterSeparator}{CompanyWebsite}";
        Footer(container, centre);
    }

    internal static void Footer(IContainer container, string centre) =>
        container.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted)).Row(row =>
        {
            row.RelativeItem().Text(centre);
            row.AutoItem().Text(text =>
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        });

    internal static void Letterhead(
        ColumnDescriptor column,
        byte[] logo,
        Action<IContainer> rightHandBlock) =>
        column.Item().PaddingBottom(9, Unit.Millimetre).Row(row =>
        {
            row.RelativeItem()
                .PaddingLeft(7, Unit.Millimetre)
                .AlignLeft()
                .Width(53, Unit.Millimetre)
                .Height(30.3f, Unit.Millimetre)
                .Image(logo)
                .WithCompressionQuality(ImageCompressionQuality.VeryHigh)
                .FitArea();
            var right = row.AutoItem().PaddingTop(10, Unit.Millimetre).MinWidth(54, Unit.Millimetre);
            rightHandBlock(right);
        });

    internal static void Reference(ColumnDescriptor column, string label, string value) => column.Item().Row(row =>
    {
        row.ConstantItem(18, Unit.Millimetre)
            .PaddingVertical(1, Unit.Millimetre)
            .PaddingHorizontal(2, Unit.Millimetre)
            .AlignRight()
            .Text(label)
            .Bold()
            .FontColor(Color.FromHex("#111111"));
        row.AutoItem()
            .PaddingVertical(1, Unit.Millimetre)
            .PaddingLeft(6, Unit.Millimetre)
            .Text(value)
            .Bold();
    });

    internal static void Title(ColumnDescriptor column, string title, bool italic)
    {
        var text = column.Item()
            .PaddingBottom(1, Unit.Millimetre)
            .AlignCenter()
            .Text(title.ToUpperInvariant())
            .FontSize(14)
            .Bold()
            .FontColor(Brand);
        if (italic)
        {
            text.Italic().LetterSpacing(0.08f);
        }
        column.Item()
            .PaddingTop(1, Unit.Millimetre)
            .PaddingBottom(4, Unit.Millimetre)
            .LineHorizontal(1.5f)
            .LineColor(Brand);
    }

    internal static void Badge(IContainer container, string text, Color background) => container
        .Background(background)
        .PaddingVertical(2, Unit.Millimetre)
        .PaddingHorizontal(3, Unit.Millimetre)
        .Text(text)
        .Bold()
        .FontColor(Colors.White);

    internal static void Tile(IContainer container, string label, string value, bool highlight)
    {
        var ink = highlight ? Colors.White : Ink;
        container
            .Border(0.4f)
            .BorderColor(Rule)
            .Background(highlight ? Brand : Colors.White)
            .Padding(2, Unit.Millimetre)
            .Column(tile =>
            {
                tile.Item().AlignCenter().Text(label.ToUpperInvariant()).FontSize(7.5f).Bold().FontColor(ink);
                tile.Item().PaddingTop(1, Unit.Millimetre).AlignCenter().Text(value).FontSize(11).Bold().FontColor(ink);
            });
    }

    internal static void Section(
        ColumnDescriptor column,
        string title,
        Action<ColumnDescriptor> body,
        float bottomGap = 0) =>
        column.Item()
            .PaddingTop(SectionGap, Unit.Millimetre)
            .PaddingBottom(bottomGap, Unit.Millimetre)
            .Column(section =>
            {
                section.Item()
                    .PaddingBottom(HeadingGap, Unit.Millimetre)
                    .BorderBottom(1.5f)
                    .BorderColor(Brand)
                    .PaddingBottom(1, Unit.Millimetre)
                    .Text(title)
                    .FontSize(10.6f)
                    .Bold();
                body(section);
            });

    internal static void HeaderCell(
        IContainer cell,
        string text,
        float fontSize = DataRegister,
        bool alignRight = false,
        float verticalPadding = 1.4f,
        float horizontalPadding = 1.4f)
    {
        var box = cell
            .Background(Brand)
            .PaddingVertical(verticalPadding, Unit.Millimetre)
            .PaddingHorizontal(horizontalPadding, Unit.Millimetre);
        if (alignRight)
        {
            box = box.AlignRight();
        }
        box.Text(text).FontSize(fontSize).Bold().FontColor(Colors.White);
    }

    internal static void DataTable(
        IContainer container,
        float labelWidthMillimetres,
        IEnumerable<(string Label, string Value)> rows) => container
        .PaddingTop(1, Unit.Millimetre)
        .PaddingBottom(3, Unit.Millimetre)
        .Border(1.2f)
        .BorderColor(Brand)
        .DefaultTextStyle(style => style.FontSize(9.2f))
        .Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(labelWidthMillimetres, Unit.Millimetre);
                columns.RelativeColumn();
            });
            foreach (var (label, value) in rows)
            {
                table.Cell().Background(Brand).Padding(1.4f, Unit.Millimetre)
                    .AlignMiddle().Text(label).Bold().FontColor(Colors.White);
                table.Cell().Border(0.6f).BorderColor(Brand)
                    .PaddingVertical(1.8f, Unit.Millimetre)
                    .PaddingHorizontal(2, Unit.Millimetre).AlignMiddle().Text(value);
            }
        });

    internal static IContainer BodyCell(ITableCellContainer cell, bool even) => cell
        .Border(0.4f)
        .BorderColor(Rule)
        .Background(even ? Zebra : Colors.White)
        .Padding(1.4f, Unit.Millimetre);

    internal static void CostTable(IContainer container, ReportRepairCosts costs) => container.Table(table =>
    {
        table.ColumnsDefinition(columns =>
        {
            columns.RelativeColumn();
            columns.ConstantColumn(35, Unit.Millimetre);
        });
        table.Header(header =>
        {
            HeaderCell(header.Cell(), "Item");
            HeaderCell(header.Cell(), "Amount", alignRight: true);
        });
        var rows = CostRows(costs);
        for (var i = 0; i < rows.Length; i++)
        {
            var even = i % 2 == 1;
            BodyCell(table.Cell(), even).Text(rows[i].Label);
            BodyCell(table.Cell(), even).AlignRight().Text(rows[i].Value);
        }
    });

    private static (string Label, string Value)[] CostRows(ReportRepairCosts costs) =>
    [
        ("Labour Hours", Hours(costs.LabourHours)),
        ("Paint Hours", Hours(costs.PaintHours)),
        ("Hourly Rate", Money(costs.HourlyRate)),
        ("Parts", Money(costs.Printed.Parts)),
        ("Panel Labour", Money(costs.Printed.PanelLabour)),
        ("Paint Labour", Money(costs.Printed.PaintLabour)),
        ("Paint Materials", Money(costs.Printed.Materials)),
        ("Specialist / Other", Money(costs.Printed.Specialist)),
        ("Sub Total", Money(costs.Printed.Net)),
        (costs.VatLabel, Money(costs.Printed.Vat)),
        ("Total Estimated Repair Cost", Money(costs.Total)),
    ];

    internal static string Hours(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    internal static string Date(DateOnly value) => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    internal static string Date(DateOnly? value) => value is { } date ? Date(date) : "—";
    internal static string Money(decimal value) =>
        value.ToString("£#,##0.00", CultureInfo.GetCultureInfo("en-GB"));
    internal static string Number(decimal value) =>
        value.ToString("#,##0.00", CultureInfo.GetCultureInfo("en-GB"));
    internal static string Slug(string value) => new(value.ToUpperInvariant()
        .Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
}
