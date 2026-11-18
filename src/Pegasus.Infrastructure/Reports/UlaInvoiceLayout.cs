using System.Globalization;
using Pegasus.Core.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pegasus.Infrastructure.Reports;

/// <summary>
/// The Principal's own invoice, the ULA Invoice (R1147 to R1150, R1157,
/// R1160): an A4 page set as its draft <c>ULA_Invoice_257922_DRAFT.docx</c>
/// is, composed from the report's frozen <see cref="AssessmentReportSnapshot"/>.
/// The draft's measurements are kept in millimetres and points; its Rubik is
/// set in the report's own Liberation Sans. Every word it prints is Core's
/// (<see cref="UlaInvoiceWording"/>, <see cref="UlaInvoiceCalculation"/>) or
/// data, and every figure is <see cref="UlaInvoiceCalculation"/>'s, so the
/// invoice can never disagree with the report.
/// </summary>
internal static class UlaInvoiceLayout
{
    private const float PageWidth = 210f;
    private const float PageHeight = 297f;
    private const float PointToMillimetre = 25.4f / 72f;

    // The draft's page margins: 907, 1020 and 2721 twips. The footer banner
    // is anchored to the page, full width, its foot on the page's foot.
    private const float PageTop = 16f;
    private const float PageSide = 18f;
    private const float BottomMargin = 48f;

    // Type, in points: the draft's 9.5 throughout, its title at 26, the line
    // table's heading at 8.5 and its total at 11.5.
    private const float TextSize = 9.5f;
    private const float TitleSize = 26f;
    private const float HeadingSize = 8.5f;
    private const float TotalSize = 11.5f;

    // The draft's lines are 1.15 and, for the reference rows, 1.25 single
    // lines of Rubik, whose single line is 1.185 times its type size.
    private const float Line = 1.36f;
    private const float ReferenceLine = 1.48f;

    private static readonly Color Ink = ReportChrome.Ink;
    private static readonly Color Label = Color.FromHex("#758fa5");
    private static readonly Color Navy = Color.FromHex("#245284");
    private static readonly Color Shade = Color.FromHex("#eef4f6");
    private static readonly Color Accent = Color.FromHex("#4c90a3");
    private static readonly Color Rule = Color.FromHex("#d5e0e7");
    private static readonly Color Sum = ReportChrome.Muted;

    // The cells' margins: 170 twips at the sides, 147 above and below, 181 on
    // a line's rows and 227 inside the Subject bar's accent.
    private const float CellSide = 3f;
    private const float CellEdge = 2.59f;
    private const float RowEdge = 3.19f;
    private const float BarInset = 4f;

    // Rules, in points: the Subject bar's accent, a row's rule, the last row's.
    private const float AccentWidth = 3.75f;
    private const float RowRule = 0.5f;
    private const float LastRowRule = 1.5f;

    // The logo: 60.06 by 19.84, 3 above the issuer's name.
    private const float LogoWidth = 60.06f;
    private const float AfterLogo = 3f;
    private const float AfterTitle = 4.99f;

    // The gaps between the blocks: the draft spaces them with a paragraph's
    // spacing and, below the addressee, the line table and its totals, an
    // empty line of 9.5 point type as well.
    private const float EmptyLine = TextSize * Line * PointToMillimetre;
    private const float BeforeAddressee = 12f;
    private const float BeforeSubject = 9f + EmptyLine;
    private const float AfterSubject = 2f + EmptyLine;
    private const float AfterTable = 3f + EmptyLine;
    private const float BeforeTotal = 2f;

    /// <summary>The line table's columns, in the draft's twips.</summary>
    private static readonly float[] ColumnWidths = [510f, 3234f, 907f, 1361f, 1020f, 1247f, 1587f];

    /// <summary>The totals stand under the last two columns' share: 5784 twips in, 2041 and 2041 wide.</summary>
    private const float TotalsIndent = 5784f;
    private const float TotalsWidth = 2041f * 2;

    private const int NumberColumn = 0;
    private const int DescriptionColumn = 1;
    private const int AmountColumn = 6;

    // The draft's spaces between a label and its value, and between the
    // Subject bar's fields.
    private const string LabelSpace = "   ";
    private const string SubjectSpace = "    ";
    private const string FieldSpace = "          ";

    /// <summary>
    /// <c>ULA_Invoice_{ClaimRef}.pdf</c> (R1148), the claim reference made
    /// safe for a file name as the report's own names are.
    /// </summary>
    internal static string FileName(AssessmentReportSnapshot snapshot) =>
        $"ULA_Invoice_{ReportChrome.Slug(Printable(snapshot).ClaimReference)}.pdf";

    internal static Document Compose(AssessmentReportSnapshot snapshot, PageCountProbe pageCount)
    {
        ArgumentNullException.ThrowIfNull(pageCount);
        var (invoice, figures, claimReference) = Printable(snapshot);
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageWidth, PageHeight, Unit.Millimetre);
            page.Margin(0);
            page.DefaultTextStyle(style => style
                .FontFamily(ReportChrome.FontFamily)
                .FontSize(TextSize)
                .FontColor(Ink)
                .LineHeight(Line));
            page.Content()
                .PaddingTop(PageTop, Unit.Millimetre)
                .PaddingHorizontal(PageSide, Unit.Millimetre)
                .Column(column =>
                {
                    Heading(column, snapshot, invoice, claimReference);
                    ReportChrome.Gap(column, BeforeAddressee);
                    foreach (var line in invoice.Addressee)
                    {
                        column.Item().Text(line);
                    }
                    ReportChrome.Gap(column, BeforeSubject);
                    SubjectBar(column.Item(), snapshot, invoice, claimReference);
                    ReportChrome.Gap(column, AfterSubject);
                    LineTable(column.Item(), figures);
                    ReportChrome.Gap(column, AfterTable);
                    Totals(column.Item(), snapshot, figures);
                });
            page.Footer()
                .Height(BottomMargin, Unit.Millimetre)
                .Layers(layers =>
                {
                    layers.PrimaryLayer()
                        .AlignBottom()
                        .Image(ReportResources.UlaFooter())
                        .FitWidth();
                    layers.Layer().Dynamic(pageCount);
                });
        }));
    }

    /// <summary>
    /// What the invoice needs beyond the report: the Principal's invoice
    /// facts frozen with the snapshot, the claim reference it is numbered by,
    /// and figures, which a Total loss or a Cash in lieu does not have. Any of
    /// them missing fails closed, as a kind the renderer cannot print does.
    /// </summary>
    private static (UlaInvoiceSnapshot Invoice, UlaInvoiceFigures Figures, string ClaimReference) Printable(
        AssessmentReportSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.PrincipalInvoice is not { } invoice
            || snapshot.YourReference is not { } claimReference
            || UlaInvoiceCalculation.For(snapshot) is not { } figures)
        {
            throw new ReportRenderRejectedException(
                $"Unsupported report artifact kind '{CaseReportArtifactKind.PrincipalInvoice}'.");
        }
        invoice.Validate();
        // The VAT total prints the report's own label: refused as the report is without one.
        snapshot.Costs.Validate();
        return (invoice, figures, claimReference);
    }

    /// <summary>
    /// The draft's two top cells: at the left the logo, the issuer and its
    /// VAT number; at the right the title and the invoice's references, each
    /// set against the right edge. A blank VAT number or payment terms leaves
    /// its row out (R1148).
    /// </summary>
    private static void Heading(
        ColumnDescriptor column, AssessmentReportSnapshot snapshot, UlaInvoiceSnapshot invoice, string claimReference)
    {
        var vatNumber = invoice.VatNumber?.Trim();
        var terms = invoice.PaymentTerms?.Trim();
        column.Item().Row(row =>
        {
            row.RelativeItem()
                .PaddingRight(CellSide, Unit.Millimetre)
                .PaddingBottom(CellEdge, Unit.Millimetre)
                .Column(left =>
                {
                    left.Item()
                        .PaddingBottom(AfterLogo, Unit.Millimetre)
                        .AlignLeft()
                        .Width(LogoWidth, Unit.Millimetre)
                        .Image(ReportResources.UlaLogo())
                        .WithCompressionQuality(ImageCompressionQuality.VeryHigh)
                        .FitWidth();
                    foreach (var line in invoice.Issuer)
                    {
                        left.Item().Text(line);
                    }
                    if (!string.IsNullOrEmpty(vatNumber))
                    {
                        left.Item().Text($"{UlaInvoiceWording.VatNumberLead}  {vatNumber}");
                    }
                });
            row.RelativeItem()
                .PaddingHorizontal(CellSide, Unit.Millimetre)
                .PaddingBottom(CellEdge, Unit.Millimetre)
                .Column(right =>
                {
                    right.Item()
                        .PaddingBottom(AfterTitle, Unit.Millimetre)
                        .AlignRight()
                        .Text(UlaInvoiceWording.Title)
                        .FontSize(TitleSize)
                        .Bold()
                        .FontColor(Navy);
                    Reference(right, UlaInvoiceWording.InvoiceNumberLabel, claimReference);
                    Reference(right, UlaInvoiceWording.InvoiceDateLabel, AssessmentReportWording.Date(snapshot.ReportDate));
                    if (!string.IsNullOrEmpty(terms))
                    {
                        Reference(right, UlaInvoiceWording.TermsLabel, terms);
                    }
                });
        });
    }

    /// <summary>One reference row: its label in the draft's blue-grey, then its value.</summary>
    private static void Reference(ColumnDescriptor column, string label, string value) =>
        column.Item()
            .AlignRight()
            .Text(text =>
            {
                text.DefaultTextStyle(style => style.LineHeight(ReferenceLine));
                text.Span(label).FontColor(Label);
                text.Span(LabelSpace);
                text.Span(value);
            });

    /// <summary>
    /// The shaded Subject bar with its accent at the left: the vehicle, its
    /// registration in bold and the claim reference again as Ref (R1148,
    /// R1149).
    /// </summary>
    private static void SubjectBar(
        IContainer container, AssessmentReportSnapshot snapshot, UlaInvoiceSnapshot invoice, string claimReference) =>
        container
            .Background(Shade)
            .BorderLeft(AccentWidth)
            .BorderColor(Accent)
            .PaddingVertical(CellEdge, Unit.Millimetre)
            .PaddingLeft(BarInset, Unit.Millimetre)
            .PaddingRight(CellSide, Unit.Millimetre)
            .Text(text =>
            {
                text.Span(UlaInvoiceWording.SubjectLabel).FontColor(Label);
                text.Span(SubjectSpace);
                text.Span(invoice.SubjectVehicle);
                text.Span(FieldSpace);
                text.Span(snapshot.Vehicle.Registration).Bold().FontColor(Navy);
                text.Span(FieldSpace);
                text.Span(UlaInvoiceWording.ReferenceLabel).FontColor(Label);
                text.Span(SubjectSpace);
                text.Span(claimReference);
            });

    /// <summary>
    /// The seven-column line table: its navy heading repeated on any page it
    /// runs to, a pale rule beneath each row and the accent beneath the last,
    /// the row number in blue-grey and the Amount shaded in bold navy. Rows
    /// print without a pound sign.
    /// </summary>
    private static void LineTable(IContainer container, UlaInvoiceFigures figures) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                foreach (var width in ColumnWidths)
                {
                    columns.RelativeColumn(width);
                }
            });
            table.Header(header =>
            {
                for (var index = 0; index < UlaInvoiceWording.ColumnHeadings.Count; index++)
                {
                    Aligned(
                        header.Cell()
                            .Background(Navy)
                            .PaddingVertical(CellEdge, Unit.Millimetre)
                            .PaddingHorizontal(CellSide, Unit.Millimetre)
                            .DefaultTextStyle(style => style.FontSize(HeadingSize).Bold().FontColor(Colors.White)),
                        index)
                        .Text(UlaInvoiceWording.ColumnHeadings[index]);
                }
            });
            for (var rowIndex = 0; rowIndex < figures.Rows.Count; rowIndex++)
            {
                var row = figures.Rows[rowIndex];
                var last = rowIndex == figures.Rows.Count - 1;
                string[] cells =
                [
                    row.Number.ToString(CultureInfo.InvariantCulture),
                    row.Description,
                    AssessmentReportWording.Number(row.Quantity),
                    AssessmentReportWording.Number(row.Rate),
                    AssessmentReportWording.Number(row.VatPercent),
                    AssessmentReportWording.Number(row.Vat),
                    AssessmentReportWording.Number(row.Amount),
                ];
                for (var index = 0; index < cells.Length; index++)
                {
                    IContainer cell = table.Cell();
                    if (index == AmountColumn)
                    {
                        cell = cell.Background(Shade);
                    }
                    cell = cell
                        .BorderBottom(last ? LastRowRule : RowRule)
                        .BorderColor(last ? Accent : Rule)
                        .PaddingVertical(RowEdge, Unit.Millimetre)
                        .PaddingHorizontal(CellSide, Unit.Millimetre);
                    var text = Aligned(cell, index).Text(cells[index]);
                    if (index == NumberColumn)
                    {
                        text.FontColor(Label);
                    }
                    else if (index == AmountColumn)
                    {
                        text.Bold().FontColor(Navy);
                    }
                }
            }
        });

    /// <summary>The number and the description stand at the left of their cells; the figures at the right.</summary>
    private static IContainer Aligned(IContainer cell, int column) =>
        column is NumberColumn or DescriptionColumn ? cell.AlignLeft() : cell.AlignRight();

    /// <summary>
    /// Sub Total and the VAT total, each over a pale rule, then the Total in
    /// white on navy, beneath the line table's last columns, each with a
    /// pound sign. The VAT total is labelled as the report labels it.
    /// </summary>
    private static void Totals(IContainer container, AssessmentReportSnapshot snapshot, UlaInvoiceFigures figures) =>
        container.Row(row =>
        {
            row.RelativeItem(TotalsIndent);
            row.RelativeItem(TotalsWidth).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });
                SumRow(table, UlaInvoiceWording.SubTotalLabel, AssessmentReportWording.Money(figures.SubTotal));
                SumRow(table, snapshot.Costs.VatLabel!, AssessmentReportWording.Money(figures.Vat));
                table.Cell().ColumnSpan(2).Height(BeforeTotal, Unit.Millimetre);
                TotalCell(table.Cell()).Text(UlaInvoiceWording.TotalLabel);
                TotalCell(table.Cell()).AlignRight().Text(AssessmentReportWording.Money(figures.Total));
            });
        });

    private static void SumRow(TableDescriptor table, string label, string value)
    {
        SumCell(table.Cell()).Text(label);
        SumCell(table.Cell()).AlignRight().Text(value);
    }

    private static IContainer SumCell(IContainer cell) => cell
        .BorderBottom(RowRule)
        .BorderColor(Rule)
        .PaddingVertical(CellEdge, Unit.Millimetre)
        .PaddingHorizontal(CellSide, Unit.Millimetre)
        .DefaultTextStyle(style => style.FontColor(Sum));

    private static IContainer TotalCell(IContainer cell) => cell
        .Background(Navy)
        .PaddingVertical(CellEdge, Unit.Millimetre)
        .PaddingHorizontal(CellSide, Unit.Millimetre)
        .DefaultTextStyle(style => style.FontSize(TotalSize).Bold().FontColor(Colors.White));
}
