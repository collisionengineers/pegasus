using System.Globalization;
using Pegasus.Core.Assessment;
using Pegasus.Core.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static Pegasus.Infrastructure.Reports.ReportChrome;

namespace Pegasus.Infrastructure.Reports;

internal static class EstimateDocumentLayout
{
    internal static Document Compose(EstimateDocumentSnapshot snapshot, byte[] logo) =>
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginTop(8, Unit.Millimetre);
            page.MarginHorizontal(12, Unit.Millimetre);
            page.MarginBottom(8, Unit.Millimetre);
            page.DefaultTextStyle(style => style
                .FontFamily(FontFamily)
                .FontSize(DataRegister)
                .FontColor(Ink)
                .LineHeight(BodyLineHeight));
            page.Header().Height(8, Unit.Millimetre).Row(row =>
            {
                row.RelativeItem().Text(text =>
                    text.Span($"{snapshot.EstimateName} · v{snapshot.EstimateVersion}")
                        .Bold()
                        .BreakAnywhere());
                row.AutoItem().Text(snapshot.Status).Bold().FontColor(Brand);
            });
            page.Content().Column(column => Content(column, snapshot, logo));
            page.Footer()
                .Height(14, Unit.Millimetre)
                .AlignBottom()
                .Element(footer => Footer(
                    footer,
                    $"{snapshot.Registration} · {snapshot.OurReference}{FooterSeparator}{CompanyName}{FooterSeparator}{CompanyWebsite}"));
        }));

    private static void Content(ColumnDescriptor column, EstimateDocumentSnapshot snapshot, byte[] logo)
    {
        Letterhead(column, logo, references => references
            .MaxWidth(94, Unit.Millimetre)
            .Column(lines =>
            {
                Reference(lines, "Date:", Date(snapshot.DocumentDate));
                Reference(lines, "Our Ref:", snapshot.OurReference);
                Reference(lines, "Your Ref:", snapshot.YourReference ?? "—");
                EstimateReference(
                    lines,
                    $"{snapshot.EstimateName} · {snapshot.Status} (v{snapshot.EstimateVersion})");
            }));
        Title(column, "ESTIMATE", italic: true);
        column.Item().PaddingBottom(3, Unit.Millimetre).Row(row =>
        {
            row.Spacing(3, Unit.Millimetre);
            Badge(row.AutoItem(), snapshot.Status, Charcoal);
            Badge(row.AutoItem(), Route(snapshot.Route), Brand);
            if (snapshot.VatTreatmentPending)
            {
                Badge(row.AutoItem(), "VAT treatment pending", Charcoal);
            }
            if (snapshot.UnpricedItemCount > 0)
            {
                Badge(row.AutoItem(), $"Unpriced items: {snapshot.UnpricedItemCount}", Charcoal);
            }
        });

        IdentityTable(column.Item(), snapshot);
        LineTable(column.Item(), snapshot.Lines);

        if (snapshot.OtherCosts is not null)
        {
            Section(column, "Adjustments", section => section.Item().ShowEntire().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(38, Unit.Millimetre);
                });
                HeaderCell(table.Cell(), "Recorded adjustment");
                HeaderCell(table.Cell(), "Amount", alignRight: true);
                if (snapshot.OtherCosts is { } costs)
                {
                    BodyCell(table.Cell(), false).Text("Additional costs");
                    BodyCell(table.Cell(), false).AlignRight().Text(Money(costs));
                }
            }));
        }

        Section(column, "Hours", section => HoursTable(section.Item().ShowEntire(), snapshot.Hours));
        Section(column, "Rate and discounts", section => RateTable(section.Item().ShowEntire(), snapshot));
        Section(column, "Totals", section => Totals(section.Item().ShowEntire(), snapshot));
    }

    private static void EstimateReference(ColumnDescriptor column, string value) =>
        column.Item().Row(row =>
        {
            row.ConstantItem(18, Unit.Millimetre)
                .PaddingVertical(1, Unit.Millimetre)
                .PaddingHorizontal(2, Unit.Millimetre)
                .AlignRight()
                .Text("Estimate:")
                .Bold()
                .FontColor(Color.FromHex("#111111"));
            row.RelativeItem()
                .PaddingVertical(1, Unit.Millimetre)
                .PaddingLeft(6, Unit.Millimetre)
                .Text(text => text.Span(value).Bold().BreakAnywhere());
        });

    private static void IdentityTable(IContainer container, EstimateDocumentSnapshot snapshot) => container
        .PaddingBottom(3, Unit.Millimetre)
        .Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(26, Unit.Millimetre);
                columns.RelativeColumn();
                columns.ConstantColumn(30, Unit.Millimetre);
                columns.RelativeColumn();
            });
            IdentityRow(table, "Claimant", snapshot.ClaimantName, "Registration", snapshot.Registration);
            IdentityRow(table, "Vehicle", snapshot.VehicleDescription, "Repairer VAT", VatStatus(snapshot.Totals.VatPolicy));
        });

    private static void IdentityRow(
        TableDescriptor table,
        string firstLabel,
        string firstValue,
        string secondLabel,
        string secondValue)
    {
        IdentityLabel(table.Cell(), firstLabel);
        IdentityValue(table.Cell(), firstValue);
        IdentityLabel(table.Cell(), secondLabel);
        IdentityValue(table.Cell(), secondValue);
    }

    private static void IdentityLabel(IContainer cell, string value) => cell
        .Border(0.4f).BorderColor(Rule).Background(Shade).Padding(1.6f, Unit.Millimetre)
        .Text(value).Bold();

    private static void IdentityValue(IContainer cell, string value) => cell
        .Border(0.4f).BorderColor(Rule).Padding(1.6f, Unit.Millimetre).Text(value);

    private static void LineTable(IContainer container, IReadOnlyList<EstimateDocumentLine> lines) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(10, Unit.Millimetre);
                columns.RelativeColumn(4);
                columns.RelativeColumn(1.35f);
                columns.ConstantColumn(17, Unit.Millimetre);
                columns.ConstantColumn(17, Unit.Millimetre);
                columns.ConstantColumn(20, Unit.Millimetre);
                columns.ConstantColumn(22, Unit.Millimetre);
            });
            table.Header(header =>
            {
                HeaderCell(header.Cell(), "Qty");
                HeaderCell(header.Cell(), "Description");
                HeaderCell(header.Cell(), "Type");
                HeaderCell(header.Cell(), "Hours", alignRight: true);
                HeaderCell(header.Cell(), "Paint", alignRight: true);
                HeaderCell(header.Cell(), "Materials", alignRight: true);
                HeaderCell(header.Cell(), "Unit price", alignRight: true);
            });
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                var even = index % 2 == 1;
                BodyCell(table.Cell(), even).AlignRight().Text(line.Quantity.ToString(CultureInfo.InvariantCulture));
                BodyCell(table.Cell(), even).Text(text =>
                {
                    text.Line(line.Description);
                    if (line.PartNumber is { } partNumber)
                    {
                        text.Span(partNumber).FontSize(7.5f).FontColor(Muted);
                    }
                });
                BodyCell(table.Cell(), even).Text(Operation(line.Operation));
                BodyCell(table.Cell(), even).AlignRight().Text(OptionalHours(line.Hours));
                BodyCell(table.Cell(), even).AlignRight().Text(OptionalHours(line.PaintHours));
                BodyCell(table.Cell(), even).AlignRight().Text(OptionalMoney(line.Materials));
                BodyCell(table.Cell(), even).AlignRight().Text(
                    line.Unpriced ? "To be confirmed" : OptionalMoney(line.UnitPrice));
            }
        });

    private static void HoursTable(IContainer container, EstimateHours hours)
    {
        var entries = new List<(string Label, decimal Value)>
        {
            ("New", hours.Replace), ("Repair", hours.Repair),
            ("R & R", hours.RemoveAndRefit), ("Paint", hours.PaintTotal),
            ("Blend", hours.BlendTotal), ("Specialist", hours.SpecialistPriced),
            ("Check", hours.Check), ("Total", hours.PricedTotal),
        };
        if (hours.UnpricedSpecialist != 0m)
        {
            entries.Add(("Specialist (not priced)", hours.UnpricedSpecialist));
        }
        TileTable(container, entries.Select(entry => (entry.Label, Hours(entry.Value))).ToArray());
    }

    private static void RateTable(IContainer container, EstimateDocumentSnapshot snapshot) =>
        TileTable(container,
        [
            ("Labour rate", Money(snapshot.HourlyRate)),
            ("Parts discount", Percent(snapshot.Discounts.Parts)),
            ("Materials discount", Percent(snapshot.Discounts.Materials)),
            ("Specialist discount", Percent(snapshot.Discounts.Specialist)),
            ("Overall discount", Percent(snapshot.Discounts.Overall)),
        ]);

    private static void TileTable(IContainer container, IReadOnlyList<(string Label, string Value)> entries) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                foreach (var _ in entries)
                {
                    columns.RelativeColumn();
                }
            });
            foreach (var entry in entries)
            {
                table.Cell().Border(0.4f).BorderColor(Rule).Background(Shade)
                    .Padding(1.2f, Unit.Millimetre).AlignCenter().Text(entry.Label).FontSize(7.3f).Bold();
            }
            foreach (var entry in entries)
            {
                table.Cell().Border(0.4f).BorderColor(Rule)
                    .Padding(1.5f, Unit.Millimetre).AlignCenter().Text(entry.Value).Bold();
            }
        });

    private static void Totals(IContainer container, EstimateDocumentSnapshot snapshot)
    {
        var printed = snapshot.Totals.Printed;
        container.Row(row =>
        {
            row.RelativeItem().PaddingRight(4, Unit.Millimetre).Element(left => TileTable(left,
            [
                ("Labour", Money(printed.PanelLabour + printed.PaintLabour)),
                ("Materials", Money(printed.Materials)),
                ("Parts", Money(printed.Parts)),
                ("Specialist / Other", Money(printed.Specialist)),
            ]));
            row.ConstantItem(64, Unit.Millimetre).Column(totals =>
            {
                TotalRow(totals.Item(), "Net", Money(printed.Net));
                TotalRow(totals.Item(), VatLabel(snapshot.Totals), Money(printed.Vat));
                TotalRow(
                    totals.Item().BorderTop(1.2f).BorderColor(Brand).Background(Shade),
                    "Gross",
                    Money(printed.Gross),
                    bold: true);
            });
        });
    }

    private static void TotalRow(IContainer container, string label, string value, bool bold = false)
    {
        if (bold)
        {
            container = container.DefaultTextStyle(style => style.Bold());
        }
        container.Padding(1.6f, Unit.Millimetre).Row(content =>
        {
            content.RelativeItem().Text(label);
            content.AutoItem().Text(value);
        });
    }

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

    private static string Route(RepairSpecificationSourceRoute route) => route switch
    {
        RepairSpecificationSourceRoute.Manual => "Manual",
        RepairSpecificationSourceRoute.Glasses => "Glass's",
        RepairSpecificationSourceRoute.AudatexPdf => "Audatex",
        RepairSpecificationSourceRoute.AiDraft => "AI",
        RepairSpecificationSourceRoute.Json => "JSON",
        _ => throw new ReportRenderRejectedException("The estimate document has an unsupported source route."),
    };

    private static string VatStatus(EstimateVatPolicy policy) => policy.RepairerStatus switch
    {
        RepairerVatStatus.Registered => "Registered",
        RepairerVatStatus.NotRegistered => "Not registered",
        RepairerVatStatus.Unknown when policy.CategoriesOverridden => "Categories selected",
        _ => "Unknown",
    };

    private static string VatLabel(EstimateTotals totals)
    {
        var categories = new[]
        {
            (EstimateVatCategories.Labour, "labour"),
            (EstimateVatCategories.Parts, "parts"),
            (EstimateVatCategories.Materials, "materials"),
            (EstimateVatCategories.Specialist, "specialist"),
        }.Where(item => totals.VatPolicy.Charges(item.Item1)).Select(item => item.Item2).ToArray();
        var charged = categories.Length == 0 ? "nothing" : string.Join(", ", categories);
        return $"VAT ({totals.VatPercent.ToString("0.##", CultureInfo.InvariantCulture)} %) on {charged}";
    }

    private static string OptionalHours(decimal? value) => value is { } amount ? Hours(amount) : string.Empty;
    private static string OptionalMoney(decimal? value) => value is { } amount ? Money(amount) : string.Empty;
    private static string Percent(decimal fraction) =>
        (fraction * 100m).ToString("0.##", CultureInfo.InvariantCulture) + " %";
}
