using Pegasus.Core.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pegasus.Infrastructure.Reports;

/// <summary>
/// The gaps around a section heading, in millimetres: above it after other
/// content, above it when it opens a page, and beneath it. The template sets
/// the fee note closer than the report.
/// </summary>
internal readonly record struct SectionRhythm(float Before, float AtPageTop, float After);

/// <summary>
/// The gaps around a document's title, in millimetres: above it, between it
/// and its rule, and beneath the rule.
/// </summary>
internal readonly record struct TitleRhythm(float Above, float ToRule, float Beneath);

/// <summary>
/// The printed page every Collision Engineers document shares, set to the
/// template's measurements (<c>reference/rendererref1</c>, Design I): the
/// running header and footer, the type sizes, the colours and the tables.
/// Geometry only. Every word it prints is handed to it, and no report
/// workflow policy lives here.
/// </summary>
internal static class ReportChrome
{
    internal const string FontFamily = "Liberation Sans";

    // Type, in points. One line is 1.55 times its type size throughout.
    internal const float BodySize = 9.8f;
    internal const float TableSize = 9.5f;
    internal const float GridSize = 9f;
    internal const float HeadingSize = 11f;
    internal const float TitleSize = 16f;
    internal const float DetailSize = 8.5f;
    internal const float FigureSize = 18f;
    internal const float LineSpacing = 1.55f;
    internal const float AddressSpacing = 1.5f;

    internal static readonly Color Ink = Color.FromHex("#222222");
    internal static readonly Color Muted = Color.FromHex("#555555");
    internal static readonly Color TileLabel = Color.FromHex("#666666");
    internal static readonly Color Quiet = Color.FromHex("#777777");
    internal static readonly Color Brand = Color.FromHex("#c80a32");
    internal static readonly Color Charcoal = Color.FromHex("#2c2a27");
    internal static readonly Color Grid = Color.FromHex("#bebebe");
    internal static readonly Color LabelShade = Color.FromHex("#f2f2f2");
    internal static readonly Color TotalShade = Color.FromHex("#efefef");

    // The page, in millimetres: A4. The header and footer reach 20 from
    // each side; the body is set in a further 6 points.
    internal const float PageWidth = 210f;
    internal const float PageHeight = 297f;
    internal const float HeaderEdge = 20f;
    internal const float BodyEdge = 22.117f;
    internal const float ReportBodyTop = 39f;
    internal const float FeeNoteBodyTop = 37f;
    internal const float FooterBand = 22f;

    // The template's gaps, measured from its sample reports.
    internal static readonly TitleRhythm ReportTitle = new(0.12f, 2.96f, 6.02f);
    internal static readonly TitleRhythm FeeNoteTitle = new(0, 1.91f, 4.03f);
    internal static readonly SectionRhythm ReportRhythm = new(6.5f, 4.5f, 2.5f);
    internal static readonly SectionRhythm FeeNoteRhythm = new(5.1f, 5.1f, 2f);

    /// <summary>The gap above the matter line, and above the badges and the fee table that follow it.</summary>
    internal const float MatterGap = 4f;
    internal const float AfterMatterGap = 5.03f;

    /// <summary>The room, in points, a heading needs to stay on its page: itself and two lines beneath it.</summary>
    private const float HeadingWithContent = 80f;

    /// <summary>The gap between two paragraphs of one block.</summary>
    internal const float ParagraphGap = 3.5f;

    /// <summary>The gap between two tables.</summary>
    internal const float TableGap = 5.13f;

    // An image slot, in millimetres: the grid's and page 1's.
    internal const float SlotWidth = 80.4f;
    internal const float GridSlotHeight = 48f;
    internal const float LeadSlotHeight = 36f;
    internal const float GridRowGap = 6.84f;

    /// <summary>The gap between what stands above a row of slots and the row.</summary>
    internal const float SlotGap = 5f;
    internal const int GridImagesPerPage = 6;

    /// <summary>
    /// One A4 page of a document: the running header and footer on every
    /// page it flows onto, and the body between them. A document that names
    /// itself on every page hands the line that does so, which then opens
    /// each page's body.
    /// </summary>
    internal static void Page(
        PageDescriptor page,
        Image logo,
        IReadOnlyList<string> companyBlock,
        string footer,
        float bodyTop,
        Action<ColumnDescriptor> body,
        Action<IContainer>? runningLine = null)
    {
        page.Size(PageWidth, PageHeight, Unit.Millimetre);
        page.Margin(0);
        page.DefaultTextStyle(style => style
            .FontFamily(FontFamily)
            .FontSize(BodySize)
            .FontColor(Ink)
            .LineHeight(LineSpacing));
        page.Header()
            .Height(bodyTop, Unit.Millimetre)
            .Element(header => RunningHeader(header, logo, companyBlock));
        var content = page.Content().PaddingHorizontal(BodyEdge, Unit.Millimetre);
        if (runningLine is null)
        {
            content.Column(body);
        }
        else
        {
            content.Decoration(decoration =>
            {
                decoration.Before(runningLine);
                decoration.Content().Column(body);
            });
        }
        page.Footer()
            .Height(FooterBand, Unit.Millimetre)
            .Element(band => RunningFooter(band, footer));
    }

    /// <summary>
    /// The logo at the left and the company block at the right. The block's
    /// first line is the company, its second the strapline, and the rest its
    /// details.
    /// </summary>
    private static void RunningHeader(IContainer container, Image logo, IReadOnlyList<string> companyBlock) =>
        container
            .PaddingHorizontal(HeaderEdge, Unit.Millimetre)
            .PaddingTop(8.2f, Unit.Millimetre)
            .Row(row =>
            {
                row.RelativeItem()
                    .AlignLeft()
                    .Width(44, Unit.Millimetre)
                    .Image(logo)
                    .WithCompressionQuality(ImageCompressionQuality.VeryHigh)
                    .FitWidth();
                row.AutoItem().Column(block =>
                {
                    for (var index = 0; index < companyBlock.Count; index++)
                    {
                        var line = block.Item().AlignRight();
                        switch (index)
                        {
                            case 0:
                                line.Text(companyBlock[index])
                                    .FontSize(10).Bold().LineHeight(AddressSpacing);
                                break;
                            case 1:
                                line.PaddingBottom(1, Unit.Millimetre).Text(companyBlock[index])
                                    .FontSize(DetailSize).Italic().FontColor(Muted).LineHeight(AddressSpacing);
                                break;
                            default:
                                line.Text(companyBlock[index])
                                    .FontSize(DetailSize).FontColor(Muted).LineHeight(AddressSpacing);
                                break;
                        }
                    }
                });
            });

    /// <summary>
    /// The red rule, the footer beneath it at the centre, and the page
    /// number at the right.
    /// </summary>
    private static void RunningFooter(IContainer container, string footer) =>
        container.Layers(layers =>
        {
            layers.PrimaryLayer()
                .PaddingTop(7, Unit.Millimetre)
                .PaddingHorizontal(39, Unit.Millimetre)
                .LineHorizontal(1.5f)
                .LineColor(Brand);
            layers.Layer()
                .PaddingTop(9.6f, Unit.Millimetre)
                .PaddingHorizontal(39, Unit.Millimetre)
                .AlignCenter()
                .Text(footer)
                .FontSize(8)
                .FontColor(Quiet);
            layers.Layer()
                .PaddingTop(8.45f, Unit.Millimetre)
                .PaddingRight(HeaderEdge, Unit.Millimetre)
                .AlignRight()
                .Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(7.5f).FontColor(Quiet));
                    text.Span(AssessmentReportWording.PageNumberLead);
                    text.CurrentPageNumber();
                    text.Span(AssessmentReportWording.PageNumberJoin);
                    text.TotalPages();
                });
        });

    /// <summary>
    /// The document's title, centred above a rule as wide as the body. The
    /// template spaces its letters by 2.25 points and sets a rule 1.5 points
    /// thick beneath it.
    /// </summary>
    internal static void Title(ColumnDescriptor column, string title, bool italic, TitleRhythm rhythm)
    {
        const float spacing = 2.25f;
        // The spacing falls after each letter in the template and either side
        // of it here, so the title is set back by half of one.
        var text = column.Item()
            .PaddingTop(rhythm.Above, Unit.Millimetre)
            .PaddingRight(spacing)
            .AlignCenter()
            .Text(title)
            .FontSize(TitleSize)
            .Bold()
            .FontColor(Brand)
            .LetterSpacing(spacing / TitleSize);
        if (italic)
        {
            text.Italic();
        }
        column.Item()
            .PaddingTop(rhythm.ToRule, Unit.Millimetre)
            .LineHorizontal(1.5f)
            .LineColor(Brand);
    }

    /// <summary>
    /// Who the document is for at the left, under its label, and the
    /// reference rows at the right, each set against the body's right edge.
    /// </summary>
    internal static void AddresseeBand(
        ColumnDescriptor column,
        float gapAbove,
        string label,
        IReadOnlyList<string> addressee,
        IReadOnlyList<ReportRow> references) =>
        column.Item()
            .PaddingTop(gapAbove, Unit.Millimetre)
            .DefaultTextStyle(style => style.FontSize(TableSize))
            .Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item()
                        .PaddingBottom(1, Unit.Millimetre)
                        .Text(label)
                        .Bold()
                        .LineHeight(AddressSpacing);
                    foreach (var line in addressee)
                    {
                        left.Item().Text(line).LineHeight(AddressSpacing);
                    }
                });
                row.AutoItem().Column(right =>
                {
                    foreach (var reference in references)
                    {
                        right.Item().AlignRight().Row(pair =>
                        {
                            pair.AutoItem().Text(reference.Label).Bold();
                            pair.AutoItem().PaddingLeft(2.9f, Unit.Millimetre).Text(reference.Value);
                        });
                    }
                });
            });

    /// <summary>A label in bold and what it labels, run on as one line of text.</summary>
    internal static void LabelledLine(ColumnDescriptor column, float gapAbove, string label, string value) =>
        column.Item()
            .PaddingTop(gapAbove, Unit.Millimetre)
            .Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(TableSize));
                text.Span(label).Bold();
                text.Span(" ");
                text.Span(value);
            });

    internal static void Badge(IContainer container, string text, Color background) => container
        .Background(background)
        .PaddingVertical(1.5f, Unit.Millimetre)
        .PaddingHorizontal(4, Unit.Millimetre)
        .Text(text)
        .FontSize(TableSize)
        .Bold()
        .FontColor(Colors.White)
        .LetterSpacing(0.75f / TableSize);

    /// <summary>
    /// The figure tiles across the width of the body, three or four, with
    /// 3 mm between them. The highlighted tile is red with white type.
    /// </summary>
    internal static void Tiles(IContainer container, IReadOnlyList<ReportTile> tiles) => container.Row(row =>
    {
        row.Spacing(3, Unit.Millimetre);
        foreach (var tile in tiles)
        {
            row.RelativeItem()
                .Background(tile.Highlight ? Brand : TotalShade)
                .Padding(3, Unit.Millimetre)
                .Column(content =>
                {
                    content.Item()
                        .AlignCenter()
                        .Text(tile.Label)
                        .FontSize(7)
                        .FontColor(tile.Highlight ? Colors.White : TileLabel)
                        .LetterSpacing(0.75f / 7f)
                        .AlignCenter();
                    content.Item()
                        .PaddingTop(1, Unit.Millimetre)
                        .AlignCenter()
                        .Text(tile.Value)
                        .FontSize(11.5f)
                        .Bold()
                        .FontColor(tile.Highlight ? Colors.White : Ink);
                });
        }
    });

    /// <summary>
    /// A gap that stands in the column by itself. What follows it and
    /// runs onto another page then begins that page at the top of the body,
    /// where a gap set about it would be left at the top of every page.
    /// </summary>
    internal static void Gap(ColumnDescriptor column, float millimetres)
    {
        if (millimetres > 0)
        {
            column.Item().Height(millimetres, Unit.Millimetre);
        }
    }

    /// <summary>
    /// A section: its heading, 11 point bold with no rule beneath it, and
    /// its content after it in the same column. A heading is never left at
    /// the foot of a page without the content it heads.
    /// </summary>
    internal static void Section(
        ColumnDescriptor column,
        string title,
        SectionRhythm rhythm,
        bool opensPage,
        Color colour,
        Action<ColumnDescriptor> body)
    {
        column.Item()
            .EnsureSpace(HeadingWithContent)
            .PaddingTop(opensPage ? rhythm.AtPageTop : rhythm.Before, Unit.Millimetre)
            .PaddingBottom(rhythm.After, Unit.Millimetre)
            .Text(title)
            .FontSize(HeadingSize)
            .Bold()
            .FontColor(colour);
        body(column);
    }

    /// <summary>
    /// A block of text set justified. Two line breaks part its paragraphs
    /// and one breaks a line within a paragraph.
    /// </summary>
    internal static void Paragraphs(ColumnDescriptor column, string text)
    {
        var first = true;
        foreach (var paragraph in text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            Paragraph(column, paragraph, first ? 0 : ParagraphGap);
            first = false;
        }
    }

    internal static void Paragraph(ColumnDescriptor column, string text, float gapAbove)
    {
        Gap(column, gapAbove);
        column.Item().Text(text).Justify();
    }

    // ---- Tables ------------------------------------------------------------

    private static IContainer HeaderCell(IContainer cell) => cell
        .Background(Brand)
        .BorderBottom(0.75f)
        .BorderColor(Grid)
        .PaddingTop(2.4f, Unit.Millimetre)
        .PaddingBottom(2.53f, Unit.Millimetre)
        .PaddingHorizontal(3, Unit.Millimetre)
        .DefaultTextStyle(style => style.FontSize(TableSize).Bold().FontColor(Colors.White));

    private static IContainer RowCell(IContainer cell, bool shaded = false) =>
        (shaded ? cell.Background(TotalShade) : cell)
        .BorderBottom(0.75f)
        .BorderColor(Grid)
        .PaddingVertical(2.333f, Unit.Millimetre)
        .PaddingHorizontal(3, Unit.Millimetre)
        .DefaultTextStyle(style => style.FontSize(TableSize));

    /// <summary>
    /// A table of facts whose red header row carries its title: one row to
    /// a fact, the label in bold, a hairline rule beneath each.
    /// </summary>
    internal static void FactTable(IContainer container, string title, IReadOnlyList<ReportRow> rows) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(35);
                columns.RelativeColumn(65);
            });
            table.Header(header => HeaderCell(header.Cell().ColumnSpan(2)).Text(title));
            foreach (var row in rows)
            {
                RowCell(table.Cell()).Text(row.Label).Bold();
                RowCell(table.Cell()).Text(row.Value);
            }
        });

    /// <summary>
    /// A table of money: the title and the amount heading in its red header
    /// row, a figure to a row, the sums beneath them set to the right in bold
    /// and the total shaded. It is as wide as the body unless a width is
    /// given.
    /// </summary>
    internal static void MoneyTable(
        IContainer container,
        float? widthMillimetres,
        float amountWidthMillimetres,
        string title,
        string amountHeading,
        IReadOnlyList<ReportCostRow> rows,
        Action<TableDescriptor>? figures = null) =>
        (widthMillimetres is { } width ? container.Width(width, Unit.Millimetre) : container)
        .Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.ConstantColumn(amountWidthMillimetres, Unit.Millimetre);
            });
            table.Header(header =>
            {
                HeaderCell(header.Cell()).Text(title);
                HeaderCell(header.Cell()).AlignRight().Text(amountHeading);
            });
            figures?.Invoke(table);
            foreach (var row in rows)
            {
                var shaded = row.Kind == ReportCostRowKind.Total;
                if (row.Kind == ReportCostRowKind.Figure)
                {
                    RowCell(table.Cell()).Text(row.Label);
                    RowCell(table.Cell()).AlignRight().Text(row.Value);
                    continue;
                }
                RowCell(table.Cell(), shaded).AlignRight().Text(row.Label).Bold();
                RowCell(table.Cell(), shaded).AlignRight().Text(row.Value).Bold();
            }
        });

    /// <summary>
    /// One row of a money table whose description runs to several lines:
    /// the first in bold, the amount level with the middle of them.
    /// </summary>
    internal static void DescribedFigure(
        TableDescriptor table, string title, IReadOnlyList<string> lines, string amount)
    {
        RowCell(table.Cell()).Column(description =>
        {
            description.Item().Text(title).Bold();
            foreach (var line in lines)
            {
                description.Item().Text(line);
            }
        });
        RowCell(table.Cell()).AlignMiddle().AlignRight().Text(amount);
    }

    /// <summary>
    /// A list under a red title row, its items in two columns reading across
    /// then down with a hairline rule beneath each row. The title row is
    /// repeated when the list runs onto another page.
    /// </summary>
    internal static void TwoColumnList(IContainer container, string title, IReadOnlyList<string> items) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
            });
            table.Header(header => HeaderCell(header.Cell().ColumnSpan(2)).Text(title));
            foreach (var item in items)
            {
                RowCell(table.Cell()).Text(item);
            }
            if (items.Count % 2 == 1)
            {
                // The rule runs the width of the table beneath a last item
                // that stands alone.
                RowCell(table.Cell());
            }
        });

    /// <summary>
    /// A grid of facts, two to a row: each label in a grey cell in bold, its
    /// value beside it, and grid lines around every cell.
    /// </summary>
    internal static void LabelGrid(
        IContainer container,
        float fontSize,
        (float Label, float Value) first,
        (float Label, float Value) second,
        IReadOnlyList<ReportRow> rows) => container
        // The grid's lines are drawn about its cells' edges, so the grid is
        // set in by half a line to keep its outer edge on the body's.
        .Padding(0.375f)
        .DefaultTextStyle(style => style.FontSize(fontSize))
        .Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(first.Label);
                columns.RelativeColumn(first.Value);
                columns.RelativeColumn(second.Label);
                columns.RelativeColumn(second.Value);
            });
            for (var index = 0; index < rows.Count; index++)
            {
                var top = index < 2;
                GridCell(table.Cell().Background(LabelShade), top, left: index % 2 == 0)
                    .Text(rows[index].Label).Bold();
                GridCell(table.Cell(), top, left: false).Text(rows[index].Value);
            }
        });

    /// <summary>
    /// One cell of a grid. Each line of the grid is drawn once: a cell draws
    /// the lines to its right and beneath it, and those above it and to its
    /// left only where no other cell draws them.
    /// </summary>
    private static IContainer GridCell(IContainer cell, bool top, bool left) => cell
        .BorderTop(top ? 0.75f : 0)
        .BorderLeft(left ? 0.75f : 0)
        .BorderRight(0.75f)
        .BorderBottom(0.75f)
        .BorderColor(Grid)
        .PaddingVertical(6.33f)
        .PaddingHorizontal(3.13f, Unit.Millimetre)
        .AlignMiddle();

    /// <summary>
    /// The value box: a red border, its label in grey cells at the left and
    /// its figure in red at the right.
    /// </summary>
    internal static void ValueBox(IContainer container, IReadOnlyList<string> labelCells, string figure) => container
        .ShowEntire()
        .Padding(0.5f)
        .Border(1)
        .BorderColor(Brand)
        .Padding(0.5f)
        .Row(row =>
        {
            row.RelativeItem(98.8f)
                .Background(LabelShade)
                .Padding(4, Unit.Millimetre)
                .DefaultTextStyle(style => style.FontSize(TableSize))
                .Row(cells =>
                {
                    cells.Spacing(4, Unit.Millimetre);
                    foreach (var cell in labelCells)
                    {
                        cells.RelativeItem().Text(cell);
                    }
                });
            row.RelativeItem(66.3f)
                .AlignMiddle()
                .AlignCenter()
                .Text(figure)
                .FontSize(FigureSize)
                .Bold()
                .FontColor(Brand)
                .LineHeight(1);
        });

    // ---- Images ------------------------------------------------------------

    /// <summary>
    /// The pages the images fill, in the Engineer's order: six ordinary
    /// images to a page, and an image flagged Full page alone on a page of
    /// its own. One paginator serves the report and the image pack.
    /// </summary>
    internal static IReadOnlyList<IReadOnlyList<PreparedReportPhoto>> ImagePages(
        IReadOnlyList<PreparedReportPhoto> photos)
    {
        var pages = new List<IReadOnlyList<PreparedReportPhoto>>();
        var ordinary = new List<PreparedReportPhoto>(GridImagesPerPage);

        void Flush()
        {
            if (ordinary.Count > 0)
            {
                pages.Add([.. ordinary]);
                ordinary.Clear();
            }
        }

        foreach (var photo in photos)
        {
            if (photo.FullPage)
            {
                Flush();
                pages.Add([photo]);
                continue;
            }
            ordinary.Add(photo);
            if (ordinary.Count == GridImagesPerPage)
            {
                Flush();
            }
        }
        Flush();
        return pages;
    }

    /// <summary>
    /// The image pages: the first follows what stands above it on its page,
    /// and each one after it opens a page of its own. A Full page image is
    /// fitted whole into what is left of its page.
    /// </summary>
    internal static void Images(
        ColumnDescriptor column,
        IReadOnlyList<PreparedReportPhoto> photos,
        float gapAbove)
    {
        var pages = ImagePages(photos);
        Gap(column, gapAbove);
        for (var index = 0; index < pages.Count; index++)
        {
            var page = pages[index];
            if (index > 0)
            {
                column.Item().PageBreak();
            }
            if (page[0].FullPage)
            {
                column.Item()
                    .AlignCenter()
                    .Image(page[0].Content)
                    .UseOriginalImage()
                    .FitArea();
                continue;
            }
            column.Item().ShowEntire().Column(grid =>
            {
                grid.Spacing(GridRowGap, Unit.Millimetre);
                for (var row = 0; row < page.Count; row += 2)
                {
                    var left = page[row];
                    var right = row + 1 < page.Count ? page[row + 1] : null;
                    grid.Item().Element(slots => ImageSlots(
                        slots,
                        GridSlotHeight,
                        slot => Frame(slot, left.Content),
                        right is null ? null : slot => Frame(slot, right.Content)));
                }
            });
        }
    }

    /// <summary>Two slots side by side, at the body's left edge and at its right.</summary>
    internal static void ImageSlots(
        IContainer container,
        float slotHeight,
        Action<IContainer>? left,
        Action<IContainer>? right) => container.Row(row =>
        {
            var leftSlot = row.ConstantItem(SlotWidth, Unit.Millimetre).Height(slotHeight, Unit.Millimetre);
            left?.Invoke(leftSlot);
            row.RelativeItem();
            var rightSlot = row.ConstantItem(SlotWidth, Unit.Millimetre).Height(slotHeight, Unit.Millimetre);
            right?.Invoke(rightSlot);
        });

    /// <summary>An image of the grid: it sits in its slot inside a hairline frame, as the template draws one.</summary>
    private static void Frame(IContainer slot, byte[] image) => slot
        .Border(0.5f)
        .BorderColor(Grid)
        .Element(framed => Fit(framed, image));

    /// <summary>
    /// An image prints whole in its slot (operator, 7 October 2026): fitted
    /// inside it at its own shape and centred, so nothing of it is trimmed.
    /// </summary>
    internal static void Fit(IContainer slot, byte[] image) => slot
        .AlignCenter()
        .AlignMiddle()
        .Image(image)
        .UseOriginalImage()
        .FitArea();

    internal static string Slug(string value) => new(value.ToUpperInvariant()
        .Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
}
