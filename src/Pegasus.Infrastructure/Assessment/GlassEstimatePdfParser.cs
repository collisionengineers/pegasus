using System.Globalization;
using System.Text.RegularExpressions;
using Pegasus.Core.Assessment;
using Pegasus.Infrastructure.Glass;
using static Pegasus.Infrastructure.Assessment.PdfEstimateDocumentParser;

namespace Pegasus.Infrastructure.Assessment;

/// <summary>
/// Reads Glass's printed calculation tables, not flattened text. Included work
/// and the two appendices remain evidence; only the main rows carry charges.
/// A sheet prints any of the operation sections Glass's defines (Body,
/// Mechanical, Electrical, Auxiliary work, Upholstery, Dent repair,
/// Mechatronics, Special work), each in the Body column layout, then Paint,
/// then Additional costs; at least one operation section and Paint are
/// required, and every section a sheet prints must reconcile.
/// Auxiliary work (EC) and Additional costs (EC) are Specialist, as the XML
/// reader files them. The paint level a sheet prints lands as the line type
/// <see cref="GlassPaintLevels"/> gives it, the same table the XML reader uses.
/// Printed PDF hours are already net and never use the XML overlap rule.
/// The sheet's <c>Labour time unit</c> is <c>1 Hour</c> or <c>N WU</c>, N work
/// units to the hour; every printed time is a count of those units. The sums
/// that tie rows to their summary and total hours are checked in printed units,
/// each rate check converts to hours first (units divided by N), and every
/// time the reader returns is in hours. Any other unit refuses the sheet.
/// A sheet whose <c>VIN:</c> or <c>Vehicle Registration Number:</c> line is
/// printed blank is read; the source identity then omits that part.
/// Printed section labour is section hours multiplied by the printed rate and
/// rounded once, while each printed row must still reconcile to that rate. An
/// Additional costs section prints no rate or hours of its own: its labour is
/// checked at the Body rate, its hours are the sum of its rows, and Total
/// Labour hours exclude them while Total Labour cost includes them.
/// </summary>
internal static class GlassEstimatePdfParser
{
    /// <summary>The note a row carries when Glass's marked up its material (the printed <c>Z</c> flag).</summary>
    private const string MarkupMaterialNote = "Markup material";

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

    private enum Section
    {
        None, Body, Mechanical, Electrical, Auxiliary, Upholstery, DentRepair, Mechatronics, SpecialWork,
        Paint, Additional, Summary, Parts, Positions, End,
    }

    /// <summary>
    /// The one label per section, as the sheet prints it in a heading, a
    /// section total and the summary table. Paint has its own heading rule.
    /// </summary>
    private static readonly (string Label, Section Section)[] SectionLabels =
    [
        ("Body", Section.Body), ("Mechanical", Section.Mechanical), ("Electrical", Section.Electrical),
        ("Auxiliary work", Section.Auxiliary), ("Upholstery", Section.Upholstery), ("Paint", Section.Paint),
        ("Dent repair", Section.DentRepair), ("Mechatronics", Section.Mechatronics),
        ("Special work", Section.SpecialWork), ("Additional costs", Section.Additional),
    ];

    /// <summary>Paint rows with neither a printed level nor a paint type's own work: their descriptions say what they are.</summary>
    private static readonly string[] PaintPreparation =
        ["Surcharge", "First Colour", "Prep.", "Colour mixing", "Sample colour creation"];

    private static Section SectionOf(string text) => SectionLabels
        .FirstOrDefault(entry => text.StartsWith(entry.Label + " ", StringComparison.Ordinal)).Section;

    /// <summary>A section whose rows are operations in the Body column layout.</summary>
    private static bool IsOperationSection(Section section) => section is not (Section.Paint or Section.Additional);

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

        /// <summary>
        /// Text printed under the row after its first line: the rest of a wrapped
        /// description, or a work description Glass's prints under it. Which
        /// is which is only known once the appendix names the row (see <see cref="Fit"/>).
        /// </summary>
        public List<string> Wrapped { get; } = [];

        private string DescriptionWith(int lines) =>
            lines == 0 ? Description : $"{Description} {string.Join(' ', Wrapped.Take(lines))}";

        /// <summary>
        /// The most printed lines that make the row's name equal <paramref name="name"/>,
        /// or -1 when no count of them does.
        /// </summary>
        public int Fit(string name)
        {
            for (var lines = Wrapped.Count; lines >= 0; lines--)
                if (Name(DescriptionWith(lines)) == name) return lines;
            return -1;
        }

        /// <summary>The first <paramref name="lines"/> lines are the description; any further text is the row's note.</summary>
        public void Settle(int lines)
        {
            var rest = Wrapped.Skip(lines).ToArray();
            Description = DescriptionWith(lines);
            Wrapped.Clear();
            Notes.InsertRange(0, rest);
        }
    }

    private sealed class Totals
    {
        public decimal? Labour { get; set; }
        public decimal? Material { get; set; }
        public decimal? Total { get; set; }
        public bool Summarised { get; set; }
        public decimal? SummaryRate { get; set; }
        public decimal? SummaryHours { get; set; }
        public decimal? SummaryLabour { get; set; }
        public decimal? SummaryMaterial { get; set; }
    }

    private sealed class Reader
    {
        private readonly List<Row> rows = [];
        private readonly Dictionary<Section, Totals> totals = [];
        private readonly List<(string Description, string Part, decimal Amount)> parts = [];
        private readonly HashSet<Row> positioned = [];
        private Section section;
        private Row? parent;
        private bool annotation;
        private string? registration, vin, date, database;
        private decimal? unitDivisor;
        private bool pounds;
        private decimal? totalHours, totalLabour, totalMaterial, net, vat, vatPercent, gross, partsTotal;
        private string? positionDescription;
        private string? positionCodes;

        public void Read(VisualRow row)
        {
            if (row.Y < 30 || row.Words.Count == 0) return;
            var text = row.JoinedText.Replace(' ', ' ').Trim();
            if (section == Section.None)
            {
                Capture(row, "Vehicle Registration Number:", ref registration, allowEmpty: true);
                Capture(row, "VIN:", ref vin, allowEmpty: true);
                Capture(row, "Date:", ref date);
                Capture(row, "Database version:", ref database);
                if (text.StartsWith("Labour time unit:", StringComparison.Ordinal))
                    unitDivisor = TimeDivisor(Cell(row, 190, 580));
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
                var next = SectionOf(text);
                Start(next == Section.Paint ? Section.None : next);
                return;
            }
            if (text.StartsWith("Paint Paint type", StringComparison.Ordinal)) { Start(Section.Paint); return; }
            if (section == Section.None) return;
            var sum = Printed(section);
            if (text.StartsWith("Labour costs ", StringComparison.Ordinal)) { RequireUnset(sum.Labour); sum.Labour = LastAmount(row); return; }
            if (text.StartsWith("Material costs ", StringComparison.Ordinal)) { RequireUnset(sum.Material); sum.Material = LastAmount(row); return; }
            if (text.StartsWith("Total ", StringComparison.Ordinal)) { RequireUnset(sum.Total); sum.Total = LastAmount(row); return; }
            if (sum.Total is not null) throw Reject("Unexpected rows after a section total");
            // The second line of the printed column headings.
            if (row.Words[0].X > 225 && text.Contains("time", StringComparison.Ordinal)) return;
            if (section == Section.Paint) ReadPaint(row);
            else if (section == Section.Additional) ReadAdditional(row);
            else ReadOperation(row);
        }

        private Totals Printed(Section key) =>
            totals.TryGetValue(key, out var found) ? found : throw Reject("The main section headings are inconsistent");

        private void Start(Section next)
        {
            if (next == Section.None || (totals.TryGetValue(next, out var started) && started.Total is not null))
                throw Reject("The main section headings are inconsistent");
            if (section != next) { parent = null; annotation = false; }
            if (!totals.ContainsKey(next)) totals[next] = new();
            section = next;
        }

        /// <summary>A label's printed value. A blank one is kept as empty only where the sheet may print it so.</summary>
        private static void Capture(VisualRow row, string label, ref string? value, bool allowEmpty = false)
        {
            if (!row.JoinedText.StartsWith(label, StringComparison.Ordinal)) return;
            var found = Cell(row, 190, 580);
            if (found.Length == 0 && !allowEmpty || value is not null && value != found) throw Reject("The source identity is ambiguous");
            value = found;
        }

        /// <summary>What a printed time is divided by to give hours: <c>1 Hour</c> is 1 and <c>N WU</c> is N; anything else is unknown.</summary>
        private static decimal? TimeDivisor(string stated)
        {
            if (stated == "1 Hour") return 1;
            var parts = stated.Split(' ');
            return parts.Length == 2 && parts[1] == "WU"
                && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var units) && units > 0
                ? units : null;
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
            // The code-to-line map is fixed here (S and CA are check labour, as
            // the XML's Sealing and Control are); the Abbreviations table is not read.
            if (!included && operation is not ("RP" or "R" or "PR" or "UI" or "EC" or "S" or "CA"))
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
                AddFlags(item, row);
            }
            rows.Add(item);
            annotation = false;
        }

        /// <summary>
        /// An Additional costs row has no guide: its description starts at the
        /// left margin and Glass's prints its operation (EC), hours, labour and
        /// material in the same columns as every other section.
        /// </summary>
        private void ReadAdditional(VisualRow row)
        {
            var hours = AmountCell(row, 285, 350);
            if (hours is null) { AttachText(row); return; }
            var item = new Row
            {
                Section = section, Page = row.Page, Position = rows.Count(item => item.Section == section) + 1,
                Operation = Cell(row, 230, 285), Description = Cell(row, 0, 230), Hours = hours,
                Overlap = AmountCell(row, 350, 440), Labour = AmountCell(row, 440, 505),
                Material = AmountCell(row, 505, 555, required: true),
            };
            if (item.Operation != "EC") throw Reject("A repair operation is unknown");
            if (item.Description.Length == 0) throw Reject("An operation has no description");
            parent = item;
            AddFlags(item, row);
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
            // The paint type is evidence (200, 201, 310, 340, 410 are observed); the level is the paint work.
            if (item.Guide is { } type && !(type.Length == 3 && type.All(char.IsAsciiDigit))
                || item.Operation is { } level && GlassPaintLevels.LineTypeOfPrinted(level) is null)
                throw Reject("A paint type or level is unsupported");
            if (item.Operation is null && !PaintPreparation.Any(prefix => item.Description.StartsWith(prefix, StringComparison.Ordinal)))
                throw Reject("A paint operation has no level");
            AddFlags(item, row);
            rows.Add(item); parent = item; annotation = false;
        }

        /// <summary>The printed flag column, and any value Glass's marks as modified with an asterisk.</summary>
        private static void AddFlags(Row item, VisualRow row)
        {
            var flags = Cell(row, 555, 600);
            if (flags.Length > 0) item.Notes.Add(flags == "Z" ? MarkupMaterialNote : flags);
            foreach (var word in row.Words.Where(word => word.Text.Contains('*')))
                item.Notes.Add($"Modified source value: {word.Text}");
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
            else rows[^1].Wrapped.Add(text);
        }

        private void ReadSummary(VisualRow row, string text)
        {
            var target = SectionOf(text);
            if (target != Section.None)
            {
                var sum = totals.TryGetValue(target, out var printed)
                    ? printed : throw Reject("A summary row names a section the sheet does not print");
                if (sum.Summarised) throw Reject("A summary section is repeated");
                sum.Summarised = true;
                // Additional costs prints labour and material only.
                if (target == Section.Additional)
                {
                    if (Cell(row, 225, 413).Length != 0) throw Reject("An unknown summary row was found");
                }
                else
                {
                    sum.SummaryRate = AmountCell(row, 225, 340, true);
                    sum.SummaryHours = AmountCell(row, 340, 413, true);
                }
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
            // A name that ends where the number column starts is printed as one word with the number.
            if (number.Length == 0 && row.Words.Any(word => word.X >= 200)
                && TrySplitGlued(description, out var name, out var glued))
            {
                description = name;
                number = glued;
            }
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
            var pending = positionDescription;
            if (pending is not null && Continues(pending, description, codes))
            {
                if (codes is not null) throw Reject("The position appendix is ambiguous");
                var combined = pending + " " + description;
                if (TrySplitGlued(combined, out var wrappedName, out var wrappedCodes))
                {
                    positionDescription = wrappedName; positionCodes = wrappedCodes;
                }
                else positionDescription = combined;
            }
            else
            {
                FlushPosition();
                if (codes is null && TrySplitGlued(description, out var name, out var glued))
                {
                    description = name; codes = glued;
                }
                positionDescription = description; positionCodes = codes;
            }
        }

        /// <summary>
        /// Whether a position name read so far goes on over this appendix row.
        /// It does not when it already equals a main row's whole name. A name
        /// that equals only the first lines of a row (the rest being text Glass's
        /// prints under it) is complete only if the next row cannot extend it
        /// to a name; a name that fits no row is wrapped, as it always was.
        /// </summary>
        private bool Continues(string pending, string description, string? codes)
        {
            var fits = MainRows().Select(item => (Lines: item.Fit(pending), Count: item.Wrapped.Count))
                .Where(fit => fit.Lines >= 0).ToArray();
            if (fits.Length == 0) return true;
            if (fits.Any(fit => fit.Lines == fit.Count)) return false;
            return codes is null && MainRows().Any(item => item.Fit(pending + " " + description) >= 0);
        }

        private IEnumerable<Row> MainRows() =>
            rows.Where(item => item.Parent is null && item.Section != Section.Paint && item.Section != Section.Additional);

        /// <summary>
        /// A name that ends exactly where the number column starts is printed
        /// with no gap, so it reaches this reader as one word, name and number
        /// together. It splits at the longest prefix that is a main row's name;
        /// the rest is the number. A text that is already a name, or whose
        /// prefixes are none, is not split and refuses as before.
        /// </summary>
        private bool TrySplitGlued(string text, out string name, out string glued)
        {
            name = text; glued = string.Empty;
            if (MainRows().Any(item => item.Fit(text) >= 0)) return false;
            var wordStart = text.LastIndexOf(' ') + 1;
            for (var cut = text.Length - 1; cut > wordStart; cut--)
            {
                var rest = text[cut..];
                if (!rest.All(character => char.IsAsciiLetterOrDigit(character) || character is '+' or '-')
                    || !rest.Any(char.IsAsciiDigit))
                    continue;
                var head = text[..cut];
                if (!MainRows().Any(item => item.Fit(head) >= 0)) continue;
                name = head; glued = rest;
                return true;
            }
            return false;
        }

        private void FlushPosition()
        {
            var pending = positionDescription;
            if (pending is null) return;
            var candidates = MainRows().Where(item => !positioned.Contains(item) && item.Fit(pending) >= 0).ToArray();
            if (candidates.Length == 0) throw Reject("A position appendix row does not match its main operation");
            var matching = positionCodes is null ? candidates : candidates.Where(item => item.Guide == positionCodes.Split('+')[0]).ToArray();
            if (matching.Length == 0) throw Reject("The main and appendix position disagree");
            var found = matching[0]; // repeated names retain their printed order, never a dictionary overwrite
            // The text the appendix does not name under the row is not its description.
            found.Settle(found.Fit(pending));
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

        private decimal? SummaryHoursOf(Section key) => totals.TryGetValue(key, out var found) ? found.SummaryHours : null;

        public ParsedEstimate Complete()
        {
            FlushPosition();
            if (registration is null || vin is null || date is null || database is null || unitDivisor is not { } divisor || !pounds
                || rows.Count is 0 or > AssessmentPolicy.MaximumEstimateLines || section != Section.End
                || !totals.ContainsKey(Section.Paint) || !totals.Keys.Any(IsOperationSection))
                throw Reject("The source identity, units or complete tables are missing");

            // Additional costs prints no rate: it is the Body rate, and Body and Paint must agree on it.
            totals.TryGetValue(Section.Body, out var body);
            totals.TryGetValue(Section.Paint, out var paint);
            if (totals.ContainsKey(Section.Additional) && body?.SummaryRate is { } bodyRate && paint?.SummaryRate is { } paintRate
                && bodyRate != paintRate)
                throw Reject("The additional costs cannot be checked at one printed rate");
            var additionalRate = body?.SummaryRate ?? paint?.SummaryRate;

            decimal operationHours = 0, labour = 0, material = 0;
            foreach (var (key, sum) in totals)
            {
                var own = rows.Where(item => item.Section == key).ToArray();
                var additional = key == Section.Additional;
                var rate = additional ? additionalRate : sum.SummaryRate;
                var hours = additional ? own.Sum(item => item.Hours ?? 0) : sum.SummaryHours;
                if (own.Length == 0 || !sum.Summarised || rate is null || hours is null
                    || sum.Labour is null || sum.Material is null || sum.Total is null
                    || own.Sum(item => item.Hours ?? 0) != hours
                    || decimal.Round(hours.Value / divisor * rate.Value, 2, MidpointRounding.AwayFromZero) != sum.Labour
                    || sum.Labour != sum.SummaryLabour
                    || own.Sum(item => item.Material ?? 0) != sum.Material || sum.Material != sum.SummaryMaterial
                    || sum.Labour + sum.Material != sum.Total
                    || own.Any(item => item.Hours is { } rowHours && decimal.Round(rowHours / divisor * rate.Value, 2, MidpointRounding.AwayFromZero) != (item.Labour ?? 0)))
                    throw Reject("The main rows disagree with their printed section totals or rate");
                if (!additional) operationHours += hours.Value;
                labour += sum.Labour.Value;
                material += sum.Material.Value;
            }
            if (totalHours is null || totalLabour is null || totalMaterial is null || net is null || vat is null || vatPercent is null || gross is null
                || operationHours != totalHours || labour != totalLabour || material != totalMaterial
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
            // Rows the appendix does not name (included work, paint) keep every line they printed.
            foreach (var item in rows) item.Settle(item.Wrapped.Count);
            var bodyHours = SummaryHoursOf(Section.Body); var auxiliaryHours = SummaryHoursOf(Section.Auxiliary);
            var identity = string.Join(' ', new[] { registration, vin, date, database }.Where(part => !string.IsNullOrEmpty(part)));
            return new(identity, rows.Select(ToLine).ToArray(), GlassEstimateXmlParser.ProviderName,
                RepairSpecificationSourceRoute.Glasses, new(Parts: partsTotal,
                    PanelWorkUnits: bodyHours is null && auxiliaryHours is null ? null : InHours((bodyHours ?? 0) + (auxiliaryHours ?? 0)),
                    PaintWorkUnits: InHours(SummaryHoursOf(Section.Paint)), Materials: totalMaterial - partsTotal,
                    Net: net, Vat: vat, Gross: gross));
        }

        /// <summary>A printed count of the sheet's time unit, as hours at the precision an estimate line keeps.</summary>
        private decimal? InHours(decimal? printed) => printed is { } units
            ? decimal.Round(units / unitDivisor!.Value, EstimatePolicy.WorkUnitDecimals, MidpointRounding.AwayFromZero)
            : null;

        private EstimateLineInput ToLine(Row row)
        {
            var paint = row.Section == Section.Paint;
            var hours = InHours(row.Hours);
            var type = paint ? PaintLineType(row.Operation) : row.Operation switch
            {
                "RP" => "new_part", "R" or "PR" => "repair", "UI" => "rnr",
                "EC" => EstimateOperations.ToLineType(EstimateOperation.Specialist, hours),
                _ => "check_labour",
            };
            // A part and an additional operation print their amount as a unit
            // amount; every other row's printed material is row materials.
            var priced = row.Operation is "RP" or "EC";
            List<string> notes = [.. row.Notes];
            if (row.Parent is { } parent) notes.Insert(0, $"Included in {parent.Section} row {parent.Position}; no separate charge.");
            if (row.Operation is { } operation) notes.Insert(0, $"Printed {(paint ? "paint level" : "operation")}: {operation}.");
            if (InHours(row.Overlap) is { } overlap) notes.Add(FormattableString.Invariant($"Printed overlap: {overlap:0.00} h (net hours retained)."));
            if (row.Labour is { } labour) notes.Add(FormattableString.Invariant($"Printed labour: {labour:0.00} GBP."));
            return new(type, row.Guide, row.Description, paint ? null : hours,
                priced ? row.Material : null, false, row.PartNumber, null, "reference",
                notes.Count == 0 ? null : string.Join(' ', notes), PaintWorkUnits: paint ? hours : null,
                Materials: priced ? null : row.Material,
                SourceRowIdentity: FormattableString.Invariant($"{row.Section.ToString().ToLowerInvariant()}:p{row.Page}:r{row.Position}:{row.Guide}"));
        }
    }

    /// <summary>A paint row with no printed level is preparation; any printed level is the Core table's.</summary>
    private static string PaintLineType(string? level) => level is null
        ? "paint_prep"
        : GlassPaintLevels.LineTypeOfPrinted(level) ?? throw Reject("A paint type or level is unsupported");

    private static string Name(string value) => Regex.Replace(value, @" \((?:L|R|Both)\)$", string.Empty,
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static string? Null(string value) => value.Length == 0 ? null : value;
    private static string Cell(VisualRow row, double start, double end) =>
        string.Join(' ', row.Words.Where(word => word.X >= start && word.X < end).Select(word => word.Text)).Replace(' ', ' ').Trim();
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
