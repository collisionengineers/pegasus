using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Reports;

/// <summary>
/// Writes Core's typed sheets as one .xlsx: a bold frozen header with a
/// filter, typed cells (numbers stay numbers, money and durations carry a
/// number format, instants are spreadsheet dates), sized columns and, where a
/// sheet asks for them, SUM formulas under every Count and Money column.
/// </summary>
public sealed class OpenXmlWorkbookWriter : IWorkbookWriter
{
    // Cell styles by index in the stylesheet below.
    private const uint HeaderStyle = 1;
    private const uint IntegerStyle = 2;
    private const uint MoneyStyle = 3;
    private const uint DurationStyle = 4;
    private const uint DateTimeStyle = 5;
    private const uint TotalIntegerStyle = 6;
    private const uint TotalMoneyStyle = 7;
    private const uint TotalTextStyle = 8;

    public byte[] Write(IReadOnlyList<WorkbookSheet> sheets)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        if (sheets.Count == 0)
        {
            throw new ArgumentException("A workbook has at least one sheet.", nameof(sheets));
        }

        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            workbookPart.AddNewPart<WorkbookStylesPart>().Stylesheet = BuildStylesheet();
            var sheetList = workbookPart.Workbook.AppendChild(new Sheets());
            var definedNames = new DefinedNames();
            uint sheetId = 1;
            foreach (var sheet in sheets)
            {
                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                worksheetPart.Worksheet = BuildWorksheet(sheet);
                var relationshipId = workbookPart.GetIdOfPart(worksheetPart);
                var name = SheetName(sheet.Name);
                sheetList.Append(new Sheet { Id = relationshipId, SheetId = sheetId, Name = name });
                // The filter needs a defined name to survive a reopen in Excel.
                var lastColumn = ColumnLetters(sheet.Columns.Count);
                var lastRow = sheet.Rows.Count + 1;
                definedNames.Append(new DefinedName($"'{name}'!$A$1:${lastColumn}${lastRow}")
                {
                    Name = "_xlnm._FilterDatabase",
                    LocalSheetId = sheetId - 1,
                    Hidden = true
                });
                sheetId++;
            }

            workbookPart.Workbook.Append(definedNames);
            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    private static Worksheet BuildWorksheet(WorkbookSheet sheet)
    {
        var worksheet = new Worksheet();
        var columns = new Columns();
        for (var index = 0; index < sheet.Columns.Count; index++)
        {
            var width = Math.Clamp(
                Math.Max(sheet.Columns[index].Title.Length, sheet.Rows.Select(row => Text(row[index]).Length).DefaultIfEmpty(0).Max()) + 2,
                10,
                60);
            var column = (uint)(index + 1);
            columns.Append(new Column { Min = column, Max = column, Width = width, CustomWidth = true });
        }

        worksheet.Append(new SheetViews(new SheetView
        {
            WorkbookViewId = 0,
            TabSelected = false
        }.WithFrozenHeader()));
        worksheet.Append(columns);

        var data = new SheetData();
        var header = new Row { RowIndex = 1 };
        for (var index = 0; index < sheet.Columns.Count; index++)
        {
            header.Append(TextCell(Reference(index, 1), sheet.Columns[index].Title, HeaderStyle));
        }

        data.Append(header);
        uint rowIndex = 2;
        foreach (var values in sheet.Rows)
        {
            var row = new Row { RowIndex = rowIndex };
            for (var index = 0; index < sheet.Columns.Count; index++)
            {
                row.Append(ValueCell(Reference(index, rowIndex), sheet.Columns[index].Kind, values[index]));
            }

            data.Append(row);
            rowIndex++;
        }

        if (sheet.Totals && sheet.Rows.Count > 0)
        {
            var totals = new Row { RowIndex = rowIndex };
            for (var index = 0; index < sheet.Columns.Count; index++)
            {
                var kind = sheet.Columns[index].Kind;
                var reference = Reference(index, rowIndex);
                if (kind is WorkbookColumnKind.Count or WorkbookColumnKind.Money)
                {
                    var letters = ColumnLetters(index + 1);
                    totals.Append(new Cell
                    {
                        CellReference = reference,
                        StyleIndex = kind == WorkbookColumnKind.Money ? TotalMoneyStyle : TotalIntegerStyle,
                        CellFormula = new CellFormula($"SUM({letters}2:{letters}{rowIndex - 1})")
                    });
                }
                else
                {
                    totals.Append(TextCell(reference, index == 0 ? "Total" : string.Empty, TotalTextStyle));
                }
            }

            data.Append(totals);
        }

        worksheet.Append(data);
        worksheet.Append(new AutoFilter { Reference = $"A1:{ColumnLetters(sheet.Columns.Count)}{sheet.Rows.Count + 1}" });
        return worksheet;
    }

    private static Cell ValueCell(string reference, WorkbookColumnKind kind, object? value)
    {
        if (value is null)
        {
            return new Cell { CellReference = reference };
        }

        switch (kind)
        {
            case WorkbookColumnKind.Count when value is int or long:
                return new Cell { CellReference = reference, StyleIndex = IntegerStyle, DataType = CellValues.Number, CellValue = new CellValue(Convert.ToString(value, CultureInfo.InvariantCulture)!) };
            case WorkbookColumnKind.Money when value is decimal money:
                return new Cell { CellReference = reference, StyleIndex = MoneyStyle, DataType = CellValues.Number, CellValue = new CellValue(money.ToString(CultureInfo.InvariantCulture)) };
            case WorkbookColumnKind.Duration when value is TimeSpan duration:
                // A spreadsheet duration is a fraction of a day.
                return new Cell { CellReference = reference, StyleIndex = DurationStyle, DataType = CellValues.Number, CellValue = new CellValue(duration.TotalDays.ToString("R", CultureInfo.InvariantCulture)) };
            case WorkbookColumnKind.DateTime when value is DateTimeOffset instant:
                return new Cell { CellReference = reference, StyleIndex = DateTimeStyle, DataType = CellValues.Number, CellValue = new CellValue(instant.UtcDateTime.ToOADate().ToString("R", CultureInfo.InvariantCulture)) };
            default:
                return TextCell(reference, Text(value), 0);
        }
    }

    private static Cell TextCell(string reference, string text, uint style) => new()
    {
        CellReference = reference,
        DataType = CellValues.InlineString,
        StyleIndex = style,
        InlineString = new InlineString(new Text(text) { Space = SpaceProcessingModeValues.Preserve })
    };

    private static string Text(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        TimeSpan duration => duration.ToString("c", CultureInfo.InvariantCulture),
        DateTimeOffset instant => instant.ToString("u", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static string Reference(int columnIndex, uint rowIndex) => ColumnLetters(columnIndex + 1) + rowIndex.ToString(CultureInfo.InvariantCulture);

    private static string ColumnLetters(int columnNumber)
    {
        var letters = string.Empty;
        while (columnNumber > 0)
        {
            var remainder = (columnNumber - 1) % 26;
            letters = (char)('A' + remainder) + letters;
            columnNumber = (columnNumber - 1) / 26;
        }

        return letters;
    }

    private static string SheetName(string name)
    {
        var cleaned = new string(name.Where(character => !@"\/?*[]:".Contains(character)).ToArray()).Trim();
        return cleaned.Length == 0 ? "Sheet" : cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }

    private static Stylesheet BuildStylesheet()
    {
        var fonts = new Fonts(
            new Font(new FontSize { Val = 11 }, new FontName { Val = "Calibri" }),
            new Font(new Bold(), new FontSize { Val = 11 }, new FontName { Val = "Calibri" }))
        { Count = 2 };
        var fills = new Fills(
            new Fill(new PatternFill { PatternType = PatternValues.None }),
            new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
            new Fill(new PatternFill(new ForegroundColor { Rgb = "FFEDEDED" }) { PatternType = PatternValues.Solid }))
        { Count = 3 };
        var borders = new Borders(
            new Border(new LeftBorder(), new RightBorder(), new TopBorder(), new BottomBorder(), new DiagonalBorder()),
            new Border(new LeftBorder(), new RightBorder(), new TopBorder(new Color { Rgb = "FF9A9A9A" }) { Style = BorderStyleValues.Thin }, new BottomBorder(), new DiagonalBorder()))
        { Count = 2 };
        var numberFormats = new NumberingFormats(
            new NumberingFormat { NumberFormatId = 164, FormatCode = "#,##0" },
            new NumberingFormat { NumberFormatId = 165, FormatCode = "\"£\"#,##0.00" },
            new NumberingFormat { NumberFormatId = 166, FormatCode = "[h]:mm" },
            new NumberingFormat { NumberFormatId = 167, FormatCode = "dd mmm yyyy hh:mm" })
        { Count = 4 };
        var cellFormats = new CellFormats(
            new CellFormat(),
            new CellFormat { FontId = 1, FillId = 2, ApplyFont = true, ApplyFill = true },
            new CellFormat { NumberFormatId = 164, ApplyNumberFormat = true },
            new CellFormat { NumberFormatId = 165, ApplyNumberFormat = true },
            new CellFormat { NumberFormatId = 166, ApplyNumberFormat = true },
            new CellFormat { NumberFormatId = 167, ApplyNumberFormat = true },
            new CellFormat { NumberFormatId = 164, FontId = 1, BorderId = 1, ApplyNumberFormat = true, ApplyFont = true, ApplyBorder = true },
            new CellFormat { NumberFormatId = 165, FontId = 1, BorderId = 1, ApplyNumberFormat = true, ApplyFont = true, ApplyBorder = true },
            new CellFormat { FontId = 1, BorderId = 1, ApplyFont = true, ApplyBorder = true })
        { Count = 9 };
        return new Stylesheet(numberFormats, fonts, fills, borders, cellFormats);
    }
}

internal static class SheetViewExtensions
{
    /// <summary>Freezes the header row so it stays in view while the rows scroll.</summary>
    public static SheetView WithFrozenHeader(this SheetView view)
    {
        view.Append(new Pane
        {
            VerticalSplit = 1,
            TopLeftCell = "A2",
            ActivePane = PaneValues.BottomLeft,
            State = PaneStateValues.Frozen
        });
        view.Append(new Selection { Pane = PaneValues.BottomLeft });
        return view;
    }
}
