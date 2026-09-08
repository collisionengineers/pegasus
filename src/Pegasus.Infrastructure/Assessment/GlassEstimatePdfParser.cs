using System.Globalization;
using System.Text.RegularExpressions;
using Pegasus.Core.Assessment;
using Pegasus.Infrastructure.Glass;
using static Pegasus.Infrastructure.Assessment.PdfEstimateDocumentParser;

namespace Pegasus.Infrastructure.Assessment;

/// <summary>
/// Reads Glass's printed calculation tables, not flattened text. Included work
/// and the two appendices remain evidence; only the main rows carry charges.
/// Printed PDF hours are already net and never use the XML overlap rule.
/// </summary>
internal static class GlassEstimatePdfParser
{
    internal static ParsedEstimate Parse(IReadOnlyList<VisualRow> source)
    {
        var reader = new Reader();
        foreach (var row in Coalesce(source)) reader.Read(row);
        return reader.Complete();
    }

    private static IEnumerable<VisualRow> Coalesce(IReadOnlyList<VisualRow> source)
    {
        // Small guide/overlap type has a slightly different baseline, unlike
        // Audatex's deliberately separate value rows. The Glass row pitch is 10pt.
        for (var index = 0; index < source.Count; index++)
        {
            var row = source[index];
            List<PlacedWord> words = [.. row.Words];
            while (index + 1 < source.Count && source[index + 1].Page == row.Page
                && row.Y - source[index + 1].Y < 1.5)
                words.AddRange(source[++index].Words);
            var sorted = words.OrderBy(word => word.X).ToArray();
            yield return new(row.Page, row.Y, sorted, string.Join(' ', sorted.Select(word => word.Text)));
        }
    }

    private enum Section { None, Body, Auxiliary, Paint, Summary, Parts, Positions, End }

    private sealed class Row
    {
        public required Section Section { get; init; }
        public required int Page { get; init; }
        public required int Position { get; init; }
        public required string Description { get; set; }
        public string? Guide { get; init; }
        public string? Operation { get; init; }
        public decimal? Hours { get; init; }
        public decimal? Overlap { get; init; }
        public decimal? Labour { get; init; }
        public decimal? Material { get; init; }
        public Row? Parent { get; init; }
        public string? PartNumber { get; set; }
        public List<string> Notes { get; } = [];
    }

    private sealed class Totals
    {
        public decimal? Labour { get; set; }
        public decimal? Material { get; set; }
        public decimal? Total { get; set; }
        public decimal? SummaryRate { get; set; }
        public decimal? SummaryHours { get; set; }
        public decimal? SummaryLabour { get; set; }
        public decimal? SummaryMaterial { get; set; }
    }

    private sealed class Reader
    {
        private readonly List<Row> rows = [];
        private readonly Dictionary<Section, Totals> totals = new()
        {
            [Section.Body] = new(), [Section.Auxiliary] = new(), [Section.Paint] = new(),
        };
        private readonly List<(string Description, string Part, decimal Amount)> parts = [];
        private readonly HashSet<Row> positioned = [];
        private Section section;
        private Row? parent;
        private bool annotation;
        private string? registration, vin, date, database;
        private bool hourUnit, pounds;
        private decimal? totalHours, totalLabour, totalMaterial, net, vat, vatPercent, gross, partsTotal;
        private string? positionDescription;
        private string? positionCodes;

        public void Read(VisualRow row)
        {
            if (row.Y < 30 || row.Words.Count == 0) return;
            var text = row.JoinedText.Replace('\u00a0', ' ').Trim();
            if (section == Section.None)
            {
                Capture(row, "Vehicle Registration Number:", ref registration);
                Capture(row, "VIN:", ref vin);
                Capture(row, "Date:", ref date);
                Capture(row, "Database version:", ref database);
                if (text.StartsWith("Labour time unit:", StringComparison.Ordinal))
                    hourUnit = Cell(row, 190, 580) == "1 Hour";
                if (text.StartsWith("Currency:", StringComparison.Ordinal))
                    pounds = Cell(row, 190, 580) is "£" or "GBP";
            }
            if (text.StartsWith("Abbreviations ", StringComparison.Ordinal))
            {
                FlushPosition();
                section = Section.End;
            }
            if (section == Section.End) return;
            if (text.StartsWith("Summary ", StringComparison.Ordinal)) { section = Section.Summary; return; }
            if (text.StartsWith("Part name ", StringComparison.Ordinal))
            {
                FlushPosition();
                section = text.Contains("Previous part no.", StringComparison.Ordinal) ? Section.Parts : Section.Positions;
                return;
            }
            if (section == Section.Summary) { ReadSummary(row, text); return; }
            if (section == Section.Parts) { ReadParts(row, text); return; }
            if (section == Section.Positions) { ReadPosition(row); return; }

            if (text.Contains("Overlap-time", StringComparison.Ordinal))
            {
                var next = text.StartsWith("Body ", StringComparison.Ordinal) ? Section.Body
                    : text.StartsWith("Auxiliary work ", StringComparison.Ordinal) ? Section.Auxiliary : Section.None;
                Start(next);
                return;
            }
            if (text.StartsWith("Paint Paint type", StringComparison.Ordinal)) { Start(Section.Paint); return; }
            if (section == Section.None) return;
            var sum = totals[section];
            if (text.StartsWith("Labour costs ", StringComparison.Ordinal)) { RequireUnset(sum.Labour); sum.Labour = LastAmount(row); return; }
            if (text.StartsWith("Material costs ", StringComparison.Ordinal)) { RequireUnset(sum.Material); sum.Material = LastAmount(row); return; }
            if (text.StartsWith("Total ", StringComparison.Ordinal)) { RequireUnset(sum.Total); sum.Total = LastAmount(row); return; }
            if (sum.Total is not null) throw Reject("Unexpected rows after a section total");
            // The second line of the printed column headings.
            if (row.Words[0].X > 225 && text.Contains("time", StringComparison.Ordinal)) return;
            if (section == Section.Paint) ReadPaint(row);
            else ReadOperation(row);
        }

        private void Start(Section next)
        {
            if (next == Section.None || totals[next].Total is not null)
                throw Reject("The main section headings are inconsistent");
            if (section != next) { parent = null; annotation = false; }
            section = next;
        }

        private static void Capture(VisualRow row, string label, ref string? value)
        {
            if (!row.JoinedText.StartsWith(label, StringComparison.Ordinal)) return;
            var found = Cell(row, 190, 580);
            if (found.Length == 0 || value is not null && value != found) throw Reject("The source identity is ambiguous");
            value = found;
        }

        private void ReadOperation(VisualRow row)
        {
            var guide = Cell(row, 0, 90);
            if (guide.Length == 0)
            {
                AttachText(row);
                return;
            }
            if (guide != "NN" && (guide.Length > 50 || !guide.All(char.IsAsciiLetterOrDigit)))
                throw Reject("A guide position is unreadable");
            var included = row.Words[0].X > 31;
            var operation = included ? null : Cell(row, 230, 285);
            if (!included && operation is not ("RP" or "R" or "PR" or "UI" or "EC"))
                throw Reject("A repair operation is unknown");
            if (included && (parent is null || row.Words.Any(word => word.X >= 300)))
                throw Reject("Included work has ambiguous parent or amounts");
            var item = new Row
            {
                Section = section, Page = row.Page, Position = rows.Count(item => item.Section == section) + 1,
                Guide = guide, Operation = operation, Parent = included ? parent : null,
                Description = Cell(row, 90, included ? 580 : 230),
                Hours = included ? null : AmountCell(row, 285, 350, required: true),
                Overlap = included ? null : AmountCell(row, 350, 440),
                Labour = included ? null : AmountCell(row, 440, 505),
                Material = included ? null : AmountCell(row, 505, 555, required: true),
            };
            if (item.Description.Length == 0) throw Reject("An operation has no description");
            if (!included)
            {
                parent = item;
                var flags = Cell(row, 555, 600);
                if (flags.Length > 0) item.Notes.Add(flags);
                foreach (var word in row.Words.Where(word => word.Text.Contains('*')))
                    item.Notes.Add($"Modified source value: {word.Text}");
            }
            rows.Add(item);
            annotation = false;
        }

        private void ReadPaint(VisualRow row)
        {
            var hours = AmountCell(row, 350, 410);
            if (hours is null) { AttachText(row); return; }
            var item = new Row
            {
                Section = section, Page = row.Page, Position = rows.Count(item => item.Section == section) + 1,
                Description = Cell(row, 0, 230), Guide = Null(Cell(row, 230, 300)), Operation = Null(Cell(row, 300, 350)),
                Hours = hours, Labour = AmountCell(row, 410, 505), Material = AmountCell(row, 505, 555, required: true),
            };
            if (item.Guide is not (null or "200") || item.Operation is not (null or "B" or "I" or "II" or "III" or "K1R" or "K2"))
                throw Reject("A paint type or level is unsupported");
            if (item.Operation is null && !item.Description.StartsWith("Prep.", StringComparison.Ordinal)
                && !item.Description.StartsWith("Colour mixing", StringComparison.Ordinal)
                && !item.Description.StartsWith("Sample colour creation", StringComparison.Ordinal))
                throw Reject("A paint operation has no level");
            rows.Add(item); parent = item; annotation = false;
        }

        private void AttachText(VisualRow row)
        {
            if (parent is null || rows.Count == 0) throw Reject("Text has no source operation");
            var text = row.JoinedText;
            if (text.StartsWith("Annotation", StringComparison.Ordinal)
                || text.StartsWith("Selected criteria:", StringComparison.Ordinal)
                || text.StartsWith("Additional work", StringComparison.Ordinal))
                annotation = true;
            if (annotation) parent.Notes.Add(text);
            else rows[^1].Description += " " + text;
        }

        private void ReadSummary(VisualRow row, string text)
        {
            var target = text.StartsWith("Body ", StringComparison.Ordinal) ? Section.Body
                : text.StartsWith("Auxiliary work ", StringComparison.Ordinal) ? Section.Auxiliary
                : text.StartsWith("Paint ", StringComparison.Ordinal) ? Section.Paint : Section.None;
            if (target != Section.None)
            {
                var sum = totals[target];
                if (sum.SummaryHours is not null) throw Reject("A summary section is repeated");
                sum.SummaryRate = AmountCell(row, 225, 340, true);
                sum.SummaryHours = AmountCell(row, 340, 413, true);
                sum.SummaryLabour = AmountCell(row, 413, 483, true);
                sum.SummaryMaterial = AmountCell(row, 483, 580) ?? 0;
            }
            else if (text.StartsWith("Total Labour ", StringComparison.Ordinal))
            { RequireUnset(totalHours); totalHours = AmountCell(row, 340, 413, true); totalLabour = LastAmount(row); }
            else if (text.StartsWith("Total Material ", StringComparison.Ordinal)) { RequireUnset(totalMaterial); totalMaterial = LastAmount(row); }
            else if (text.StartsWith("Repair costs excl. VAT ", StringComparison.Ordinal)) { RequireUnset(net); net = LastAmount(row); }
            else if (text.StartsWith("VAT (", StringComparison.Ordinal))
            {
                RequireUnset(vat); vat = LastAmount(row);
                var start = text.IndexOf('(') + 1; var end = text.IndexOf('%');
                vatPercent = Amount(text[start..end].Trim());
            }
            else if (text.StartsWith("Repair costs incl. VAT ", StringComparison.Ordinal)) { RequireUnset(gross); gross = LastAmount(row); }
            else throw Reject("An unknown summary row was found");
        }

        private void ReadParts(VisualRow row, string text)
        {
            if (text.StartsWith("Total Parts ", StringComparison.Ordinal)) { RequireUnset(partsTotal); partsTotal = LastAmount(row); return; }
            if (text.StartsWith("Attention:", StringComparison.Ordinal)) return;
            if (partsTotal is not null) throw Reject("Unexpected text after the parts total");
            var number = Cell(row, 200, 330);
            var description = Cell(row, 0, 200);
            if (number.Length == 0)
            {
                if (parts.Count == 0 || row.Words.Any(word => word.X >= 200)) throw Reject("An appendix part has no identity");
                var previous = parts[^1];
                parts[^1] = (previous.Description + " " + description, previous.Part, previous.Amount);
                return;
            }
            if (Cell(row, 330, 475).Length != 0) throw Reject("A previous part number requires source review");
            parts.Add((description, number, AmountCell(row, 475, 580, true)!.Value));
        }

        private void ReadPosition(VisualRow row)
        {
            var description = Cell(row, 0, 200);
            var codes = Null(Cell(row, 200, 580));
            // A wrapped position name completes a known main-table name.
            if (positionDescription is not null && !MainRows().Any(item => Name(item.Description) == positionDescription))
            {
                if (codes is not null) throw Reject("The position appendix is ambiguous");
                positionDescription += " " + description;
            }
            else
            {
                FlushPosition(); positionDescription = description; positionCodes = codes;
            }
        }

        private IEnumerable<Row> MainRows() => rows.Where(item => item.Parent is null && item.Section != Section.Paint);

        private void FlushPosition()
        {
            if (positionDescription is null) return;
            var candidates = MainRows().Where(item => !positioned.Contains(item) && Name(item.Description) == positionDescription).ToArray();
            if (candidates.Length == 0) throw Reject("A position appendix row does not match its main operation");
            var matching = positionCodes is null ? candidates : candidates.Where(item => item.Guide == positionCodes.Split('+')[0]).ToArray();
            if (matching.Length == 0) throw Reject("The main and appendix position disagree");
            var found = matching[0]; // repeated names retain their printed order, never a dictionary overwrite
            positioned.Add(found);
            if (positionCodes is not null)
            {
                found.Notes.Add($"Position appendix: {positionCodes}");
                foreach (var child in rows.Where(item => item.Parent == found && item.Guide != "NN"
                    && !positionCodes.Split('+').Contains(item.Guide, StringComparer.Ordinal)))
                    child.Notes.Add($"Main guide {child.Guide}; parent position appendix: {positionCodes}");
            }
            positionDescription = null; positionCodes = null;
        }

        public ParsedEstimate Complete()
        {
            FlushPosition();
            if (registration is null || vin is null || date is null || database is null || !hourUnit || !pounds
                || rows.Count is 0 or > AssessmentPolicy.MaximumEstimateLines || section != Section.End)
                throw Reject("The source identity, units or complete tables are missing");
            foreach (var (key, sum) in totals)
            {
                var own = rows.Where(item => item.Section == key).ToArray();
                if (own.Length == 0 || sum.SummaryRate is null || sum.SummaryHours is null
                    || sum.Labour is null || sum.Material is null || sum.Total is null
                    || own.Sum(item => item.Hours ?? 0) != sum.SummaryHours
                    || own.Sum(item => item.Labour ?? 0) != sum.Labour || sum.Labour != sum.SummaryLabour
                    || own.Sum(item => item.Material ?? 0) != sum.Material || sum.Material != sum.SummaryMaterial
                    || sum.Labour + sum.Material != sum.Total
                    || own.Any(item => item.Hours is { } hours && decimal.Round(hours * sum.SummaryRate.Value, 2, MidpointRounding.AwayFromZero) != (item.Labour ?? 0)))
                    throw Reject("The main rows disagree with their printed section totals or rate");
            }
            if (totalHours is null || totalLabour is null || totalMaterial is null || net is null || vat is null || vatPercent is null || gross is null
                || totals.Values.Sum(sum => sum.SummaryHours!.Value) != totalHours
                || totals.Values.Sum(sum => sum.Labour!.Value) != totalLabour
                || totals.Values.Sum(sum => sum.Material!.Value) != totalMaterial
                || totalLabour + totalMaterial != net || net + vat != gross
                || decimal.Round(net.Value * vatPercent.Value / 100, 2, MidpointRounding.AwayFromZero) != vat)
                throw Reject("The document's printed totals do not reconcile");
            var replacements = rows.Where(item => item.Operation == "RP").ToArray();
            if (parts.Count != replacements.Length || partsTotal is null || parts.Sum(item => item.Amount) != partsTotal
                || positioned.Count != MainRows().Count()) throw Reject("The source appendices are incomplete");
            for (var index = 0; index < parts.Count; index++)
            {
                var main = replacements[index]; var part = parts[index];
                if (Name(main.Description) != part.Description || main.Material != part.Amount)
                    throw Reject("A part number cannot be joined unambiguously to its source row");
                main.PartNumber = part.Part;
            }
            return new($"{registration} {vin} {date} {database}", rows.Select(ToLine).ToArray(), GlassEstimateXmlParser.ProviderName,
                RepairSpecificationSourceRoute.Glasses, new(Parts: partsTotal,
                    PanelWorkUnits: totals[Section.Body].SummaryHours + totals[Section.Auxiliary].SummaryHours,
                    PaintWorkUnits: totals[Section.Paint].SummaryHours, Materials: totalMaterial - partsTotal,
                    Net: net, Vat: vat, Gross: gross));
        }

        private static EstimateLineInput ToLine(Row row)
        {
            var paint = row.Section == Section.Paint;
            var type = paint ? row.Operation switch
            {
                "B" => "paint_blend", "I" or "K1R" => "paint_new", null => "paint_prep", _ => "paint_repair",
            } : row.Operation switch
            {
                "RP" => "new_part", "R" or "PR" => "repair", "UI" => "rnr", _ => "check_labour",
            };
            List<string> notes = [.. row.Notes];
            if (row.Parent is { } parent) notes.Insert(0, $"Included in {parent.Section} row {parent.Position}; no separate charge.");
            if (row.Operation is { } operation) notes.Insert(0, $"Printed {(paint ? "paint level" : "operation")}: {operation}.");
            if (row.Overlap is { } overlap) notes.Add(FormattableString.Invariant($"Printed overlap: {overlap:0.00} h (net hours retained)."));
            if (row.Labour is { } labour) notes.Add(FormattableString.Invariant($"Printed labour: {labour:0.00} GBP."));
            return new(type, row.Guide, row.Description, paint ? null : row.Hours,
                row.Operation == "RP" ? row.Material : null, false, row.PartNumber, null, "estimated", "reference",
                notes.Count == 0 ? null : string.Join(' ', notes), PaintWorkUnits: paint ? row.Hours : null,
                Materials: row.Operation == "RP" ? null : row.Material,
                SourceRowIdentity: FormattableString.Invariant($"{row.Section.ToString().ToLowerInvariant()}:p{row.Page}:r{row.Position}:{row.Guide}"));
        }
    }

    private static string Name(string value) => Regex.Replace(value, @" \((?:L|R|Both)\)$", string.Empty,
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static string? Null(string value) => value.Length == 0 ? null : value;
    private static string Cell(VisualRow row, double start, double end) =>
        string.Join(' ', row.Words.Where(word => word.X >= start && word.X < end).Select(word => word.Text)).Replace('\u00a0', ' ').Trim();
    private static decimal? AmountCell(VisualRow row, double start, double end, bool required = false)
    {
        var text = Cell(row, start, end);
        if (text.Length == 0 && !required) return null;
        return Amount(text);
    }
    private static decimal LastAmount(VisualRow row) => Amount(row.Words[^1].Text.TrimStart('£'));
    private static decimal Amount(string text)
    {
        var cleaned = text.Trim().TrimEnd('*').Trim();
        if (!Regex.IsMatch(cleaned, @"^(?:[0-9]+|[0-9]{1,3}(?:,[0-9]{3})+)\.[0-9]{2}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))
            || !decimal.TryParse(cleaned, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value))
            throw Reject("A printed amount is unreadable");
        return value;
    }
    private static void RequireUnset(decimal? refValue)
    {
        if (refValue is not null) throw Reject("A printed total is repeated");
    }
    private static EstimateParseRejectedException Reject(string reason) => new($"{reason}; nothing was imported.");
}
