using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure.Reports;

namespace Pegasus.IntegrationTests;

/// <summary>The workbook writer's output, reopened with the same package library: sheets, header, typed cells and totals.</summary>
public sealed class OpenXmlWorkbookWriterTests
{
    [Fact]
    public void WritesEverySheetWithAFrozenFilteredHeaderTypedCellsAndTotals()
    {
        var sheets = new List<WorkbookSheet>
        {
            new("Engineer activity",
                [new("Person", WorkbookColumnKind.Text), new("Reports sent", WorkbookColumnKind.Count), new("Received to sent", WorkbookColumnKind.Duration)],
                [["alex", 4, TimeSpan.FromHours(36)], ["sam", 2, null]],
                Totals: true),
            new("By month",
                [new("Month", WorkbookColumnKind.Text), new("Agreed fees", WorkbookColumnKind.Money), new("When", WorkbookColumnKind.DateTime)],
                [["Aug 2026", 250.5m, new DateTimeOffset(2026, 8, 3, 9, 0, 0, TimeSpan.Zero)]],
                Totals: false)
        };

        var bytes = new OpenXmlWorkbookWriter().Write(sheets);

        using var stream = new MemoryStream(bytes);
        using var document = SpreadsheetDocument.Open(stream, false);
        var workbook = document.WorkbookPart!;
        var names = workbook.Workbook!.Sheets!.Elements<Sheet>().Select(sheet => sheet.Name!.Value!).ToArray();
        Assert.Equal(["Engineer activity", "By month"], names);

        var first = (WorksheetPart)workbook.GetPartById(workbook.Workbook!.Sheets!.Elements<Sheet>().First().Id!.Value!)!;
        var rows = first.Worksheet!.GetFirstChild<SheetData>()!.Elements<Row>().ToArray();
        Assert.Equal(4, rows.Length); // header, two rows, totals
        Assert.Equal(["Person", "Reports sent", "Received to sent"], rows[0].Elements<Cell>().Select(cell => cell.InlineString!.Text!.Text));
        var reportsSent = rows[1].Elements<Cell>().ElementAt(1);
        Assert.Equal(CellValues.Number, reportsSent.DataType!.Value);
        Assert.Equal("4", reportsSent.CellValue!.Text);
        var duration = rows[1].Elements<Cell>().ElementAt(2);
        Assert.Equal("1.5", duration.CellValue!.Text); // 36 hours as a fraction of a day
        var totals = rows[3].Elements<Cell>().ToArray();
        Assert.Equal("Total", totals[0].InlineString!.Text!.Text);
        Assert.Equal("SUM(B2:B3)", totals[1].CellFormula!.Text);
        Assert.NotNull(first.Worksheet!.GetFirstChild<AutoFilter>());
        Assert.Equal("A1:C3", first.Worksheet!.GetFirstChild<AutoFilter>()!.Reference!.Value);
        var pane = first.Worksheet!.GetFirstChild<SheetViews>()!.GetFirstChild<SheetView>()!.GetFirstChild<Pane>()!;
        Assert.Equal(PaneStateValues.Frozen, pane.State!.Value);

        var second = (WorksheetPart)workbook.GetPartById(workbook.Workbook!.Sheets!.Elements<Sheet>().Last().Id!.Value!)!;
        var monthRows = second.Worksheet!.GetFirstChild<SheetData>()!.Elements<Row>().ToArray();
        Assert.Equal(2, monthRows.Length); // no totals row
        Assert.Equal("250.5", monthRows[1].Elements<Cell>().ElementAt(1).CellValue!.Text);
        var when = monthRows[1].Elements<Cell>().ElementAt(2);
        Assert.Equal(CellValues.Number, when.DataType!.Value);
        Assert.Equal(
            new DateTime(2026, 8, 3, 10, 0, 0),
            DateTime.FromOADate(double.Parse(when.CellValue!.Text!, CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void RefusesAnEmptyWorkbook()
    {
        Assert.Throws<ArgumentException>(() => new OpenXmlWorkbookWriter().Write([]));
    }
}
