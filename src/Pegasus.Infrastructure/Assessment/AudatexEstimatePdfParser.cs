using System.Globalization;
using Pegasus.Core.Assessment;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Pegasus.Infrastructure.Assessment;

/// <summary>
/// Deterministic parser for the Audatex full-report estimate PDF (ENG-002).
///
/// The format prints tables whose numeric column (work units, prices) sits on
/// its own text baseline roughly one point below the description row, so
/// line-flattening text extraction mis-associates values with rows — exactly
/// the wrong-money hazard this parser exists to avoid. It therefore works on
/// word coordinates: words are grouped into visual rows by baseline, each
/// numeric value row is paired with the single description row within a
/// tolerance far smaller than the row pitch, and any ambiguity rejects the
/// whole import.
///
/// Fail-closed verification: the document prints its own section totals
/// (labour and paint "Total Work Units", the parts "Sub Total", the extras
/// "Total Extras"). Parsed lines must reproduce those sums exactly or the
/// import is rejected — no partial or silently dropped line can survive,
/// because a dropped costed line breaks its section's sum.
///
/// Those same printed totals are returned as
/// <see cref="ParsedEstimate.SourceTotals"/>. They are the document's own
/// arithmetic, kept as evidence beside the estimate Pegasus costs from the
/// rows at its own rate, discounts and VAT categories: a figure that
/// disagrees with the calculation is recorded, never dropped and never
/// allowed to overrule <see cref="EstimateTotals"/>.
/// </summary>
public sealed class AudatexEstimatePdfParser : IEstimateDocumentParser
{
    /// <summary>Titles the Draft an import of this document lands as.</summary>
    public const string ProviderName = "Audatex";

    /// <summary>A value row sits ~1pt below its description row; the row pitch is ~11-12pt.</summary>
    private const double ValuePairingTolerance = 3.5;

    /// <summary>Rows above this are the repeating page header block, below the lower bound the footer.</summary>
    private const double PageBodyTop = 720;
    private const double PageBodyBottom = 30;

    public RepairSpecificationSourceRoute Route => RepairSpecificationSourceRoute.AudatexPdf;

    public bool CanParse(string fileName, string mediaType) =>
        string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mediaType, "application/pdf", StringComparison.OrdinalIgnoreCase);

    public ParsedEstimate Parse(ReadOnlyMemory<byte> content)
    {
        List<VisualRow> rows;
        try
        {
            using var document = PdfDocument.Open(content.ToArray());
            rows = CollectRows(document);
        }
        catch (Exception exception) when (exception is not EstimateParseRejectedException)
        {
            throw new EstimateParseRejectedException(
                "The file could not be read as a PDF, so nothing was imported.");
        }

        var reader = new ReportReader();
        foreach (var row in rows)
        {
            reader.Read(row);
        }

        return reader.Complete();
    }

    /// <summary>Words grouped into visual rows by shared baseline, in reading order.</summary>
    private static List<VisualRow> CollectRows(PdfDocument document)
    {
        var rows = new List<VisualRow>();
        for (var pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
        {
            var page = document.GetPage(pageNumber);
            var grouped = page.GetWords()
                .GroupBy(word => Math.Round(word.BoundingBox.Bottom, 1))
                .OrderByDescending(group => group.Key);
            foreach (var group in grouped)
            {
                var words = group.OrderBy(word => word.BoundingBox.Left)
                    .Select(word => new PlacedWord(word.BoundingBox.Left, word.Text))
                    .ToArray();
                rows.Add(new VisualRow(
                    group.Key,
                    words,
                    string.Join(' ', words.Select(word => word.Text))));
            }
        }

        return rows;
    }

    private sealed record PlacedWord(double X, string Text);

    /// <summary>
    /// One baseline's words in reading order. The joined text is built once at
    /// collection, because every classification step below reads it again.
    /// </summary>
    private sealed record VisualRow(double Y, IReadOnlyList<PlacedWord> Words, string JoinedText);

    private enum Section
    {
        None,
        Labour,
        Paint,
        Parts,
        Extras,
        Ignored,
    }

    /// <summary>One table row still waiting for its value row (or confirmed valueless).</summary>
    private sealed class PendingLine
    {
        public required double Y { get; init; }

        public required string? GuideCode { get; init; }

        public required string Description { get; set; }

        public string? PartNumber { get; init; }

        public string? Betterment { get; init; }

        public decimal? Value { get; set; }
    }

    private sealed class SectionState
    {
        public double? DescriptionX { get; set; }

        public double? PartNumberX { get; set; }

        public double? BettermentX { get; set; }

        public double? ValueZoneX { get; set; }

        public bool Closed { get; set; }

        public decimal? PrintedTotal { get; set; }

        public List<EstimateLineInput> Lines { get; } = [];

        public List<decimal> Values { get; } = [];
    }

    private sealed class ReportReader
    {
        private readonly Dictionary<Section, SectionState> sections = new()
        {
            [Section.Labour] = new(),
            [Section.Paint] = new(),
            [Section.Parts] = new(),
            [Section.Extras] = new(),
        };

        private Section current = Section.None;
        private PendingLine? pending;

        /// <summary>
        /// A description-only row is ambiguous when it arrives: it is either
        /// continuation text under the previous line, or a new line whose
        /// value is printed on the next baseline. It is held here and
        /// resolved by the following row — a value row promotes it to a
        /// line; anything else folds it into the previous line's text.
        /// </summary>
        private PendingLine? bare;
        private string? assessmentNumber;
        private string? documentVersion;
        private bool sawAudatexFooter;

        public void Read(VisualRow row)
        {
            if (row.Words.Count == 0)
            {
                return;
            }

            CaptureIdentity(row);
            if (row.JoinedText.StartsWith("Audatex System", StringComparison.Ordinal))
            {
                sawAudatexFooter = true;
            }
            if (row.Y is > PageBodyTop or < PageBodyBottom)
            {
                return;
            }
            if (TrySwitchSection(row))
            {
                return;
            }
            if (current is Section.None or Section.Ignored || sections[current].Closed)
            {
                return;
            }

            var state = sections[current];
            if (TryCloseWithTotal(row, state))
            {
                return;
            }
            if (TryCaptureColumnAnchors(row, state))
            {
                return;
            }
            if (state.DescriptionX is null)
            {
                // Rows before the section's column headings carry no lines.
                return;
            }
            if (TryAttachValueRow(row, state))
            {
                return;
            }

            ReadLineRow(row, state);
        }

        public ParsedEstimate Complete()
        {
            var lastState = sections.GetValueOrDefault(current);
            ResolveBare(lastState);
            FlushPending(lastState);
            if (!sawAudatexFooter || sections.Values.All(state => state.Lines.Count == 0))
            {
                throw new EstimateParseRejectedException(
                    "This file was not recognized as an Audatex estimate report, so nothing was imported.");
            }
            if (assessmentNumber is null || documentVersion is null)
            {
                throw new EstimateParseRejectedException(
                    "The estimate's assessment number and version could not be read, so nothing was imported.");
            }

            VerifySection(Section.Labour, "labour");
            VerifySection(Section.Paint, "paint");
            VerifySection(Section.Parts, "parts");
            VerifySection(Section.Extras, "specialist charges");

            var lines = sections[Section.Labour].Lines
                .Concat(sections[Section.Paint].Lines)
                .Concat(sections[Section.Parts].Lines)
                .Concat(sections[Section.Extras].Lines)
                .ToArray();
            if (lines.Length > AssessmentPolicy.MaximumEstimateLines)
            {
                throw new EstimateParseRejectedException(
                    $"The estimate carries more than {AssessmentPolicy.MaximumEstimateLines} lines, so nothing was imported.");
            }

            return new ParsedEstimate(
                $"{assessmentNumber} {documentVersion}",
                lines,
                ProviderName,
                new EstimateSourceTotals(
                    Parts: sections[Section.Parts].PrintedTotal,
                    PanelWorkUnits: sections[Section.Labour].PrintedTotal,
                    PaintWorkUnits: sections[Section.Paint].PrintedTotal,
                    Specialist: sections[Section.Extras].PrintedTotal));
        }

        /// <summary>
        /// A section that produced lines must carry the document's own total
        /// for those lines, and the parsed values must reproduce it exactly.
        /// </summary>
        private void VerifySection(Section section, string name)
        {
            var state = sections[section];
            if (state.Lines.Count == 0)
            {
                return;
            }
            if (state.PrintedTotal is null)
            {
                throw new EstimateParseRejectedException(
                    $"The document's own total for its {name} lines could not be found, so nothing was imported.");
            }
            var sum = state.Values.Sum();
            if (sum != state.PrintedTotal.Value)
            {
                throw new EstimateParseRejectedException(
                    $"The {name} lines do not add up to the document's own printed total, so nothing was imported.");
            }
        }

        private void CaptureIdentity(VisualRow row)
        {
            if (documentVersion is null
                && row.Words.Count > 1
                && row.Words[0].Text == "Version:"
                && row.Words[1].X < 400)
            {
                documentVersion = row.Words[1].Text;
            }
            if (assessmentNumber is not null)
            {
                return;
            }

            // "Assessment Number:" carries its value as the next word along.
            for (var index = 1; index < row.Words.Count - 1; index++)
            {
                var next = row.Words[index + 1];
                if (row.Words[index].Text == "Number:"
                    && row.Words[index - 1].Text == "Assessment"
                    && next.X < 400)
                {
                    assessmentNumber = next.Text;
                    return;
                }
            }
        }

        private bool TrySwitchSection(VisualRow row)
        {
            if (row.Words[0].X >= 40)
            {
                return false;
            }

            var text = row.JoinedText;
            var target = text switch
            {
                _ when text.StartsWith("LABOUR", StringComparison.Ordinal) => Section.Labour,
                _ when text.StartsWith("PAINT WORK", StringComparison.Ordinal) => Section.Paint,
                _ when text.StartsWith("PARTS", StringComparison.Ordinal) => Section.Parts,
                _ when text.StartsWith("Extras", StringComparison.Ordinal) => Section.Extras,
                _ when text.StartsWith("MATERIAL COST", StringComparison.Ordinal)
                    || text.StartsWith("Cost Summary", StringComparison.Ordinal)
                    || text.StartsWith("Calculation", StringComparison.Ordinal)
                    || text.StartsWith("Assessment Notes", StringComparison.Ordinal)
                    || text.StartsWith("Addresses", StringComparison.Ordinal)
                    || text.StartsWith("Summary Information", StringComparison.Ordinal)
                    || text.StartsWith("Vehicle ", StringComparison.Ordinal) => Section.Ignored,
                _ => Section.None,
            };
            if (target == Section.None)
            {
                return false;
            }

            var previousState = sections.GetValueOrDefault(current);
            ResolveBare(previousState);
            FlushPending(previousState);
            current = target;
            return true;
        }

        /// <summary>
        /// The "Total Work Units" / "Sub Total" / "Total Extras" row closes
        /// its section's line collection and carries the printed checksum.
        /// </summary>
        private bool TryCloseWithTotal(VisualRow row, SectionState state)
        {
            var text = row.JoinedText;
            var closes = current switch
            {
                Section.Labour or Section.Paint => text.StartsWith("Total Work Units", StringComparison.Ordinal),
                Section.Parts => text.StartsWith("Sub Total", StringComparison.Ordinal),
                Section.Extras => text.StartsWith("Total Extras", StringComparison.Ordinal),
                _ => false,
            };
            if (!closes)
            {
                return false;
            }

            ResolveBare(state);
            FlushPending(state);
            var lastWord = row.Words[^1].Text;
            state.PrintedTotal = ParseAmount(lastWord)
                ?? throw new EstimateParseRejectedException(
                    "A printed section total could not be read as an amount, so nothing was imported.");
            state.Closed = true;
            return true;
        }

        /// <summary>
        /// The section's column-heading row anchors where each column starts;
        /// heading rows never carry lines.
        /// </summary>
        private static bool TryCaptureColumnAnchors(VisualRow row, SectionState state)
        {
            var description = row.Words.FirstOrDefault(word => word.Text == "Description");
            if (description is not null)
            {
                state.DescriptionX = description.X;
                var part = row.Words.FirstOrDefault(word => word.Text == "Part");
                state.PartNumberX = part?.X;
                var betterment = row.Words.FirstOrDefault(
                    word => word.Text is "Bet." or "Betterment");
                state.BettermentX = betterment?.X;
                var price = row.Words.LastOrDefault(word => word.Text == "Price");
                if (price is not null)
                {
                    state.ValueZoneX = price.X - 40;
                }
                var work = row.Words.FirstOrDefault(word => word.Text == "Work");
                if (work is not null)
                {
                    state.ValueZoneX ??= work.X - 30;
                }
                return true;
            }

            for (var index = 0; index < row.Words.Count - 1; index++)
            {
                if (row.Words[index].Text == "Work" && row.Words[index + 1].Text == "Units")
                {
                    state.ValueZoneX ??= row.Words[index].X - 30;
                    return true;
                }
            }

            return row.JoinedText.StartsWith("Price Valid", StringComparison.Ordinal);
        }

        /// <summary>
        /// A row whose only content is one number in the value zone is the
        /// value printed one baseline below its description row. It must pair
        /// with the immediately preceding line, still valueless, within the
        /// pairing tolerance — anything else is ambiguous and rejects the
        /// import rather than risking money on the wrong line.
        /// </summary>
        private bool TryAttachValueRow(VisualRow row, SectionState state)
        {
            var words = row.Words.Where(word => word.Text != "*").ToArray();
            if (words.Length != 1 || state.ValueZoneX is not { } valueZoneX || words[0].X < valueZoneX)
            {
                return false;
            }

            var amount = ParseAmount(words[0].Text)
                ?? throw new EstimateParseRejectedException(
                    "An amount in the estimate could not be read, so nothing was imported.");
            if (bare is not null)
            {
                if (bare.Y - row.Y > ValuePairingTolerance)
                {
                    throw new EstimateParseRejectedException(
                        "An amount in the estimate could not be matched to its line, so nothing was imported.");
                }
                FlushPending(state);
                bare.Value = amount;
                pending = bare;
                bare = null;
                return true;
            }
            if (pending is null || pending.Value is not null || pending.Y - row.Y > ValuePairingTolerance)
            {
                throw new EstimateParseRejectedException(
                    "An amount in the estimate could not be matched to its line, so nothing was imported.");
            }

            pending.Value = amount;
            return true;
        }

        private void ReadLineRow(VisualRow row, SectionState state)
        {
            var descriptionX = state.DescriptionX!.Value;
            var partX = state.PartNumberX;
            var bettermentX = state.BettermentX;
            var valueZoneX = state.ValueZoneX;

            var guide = new List<string>();
            var description = new List<string>();
            var partNumber = new List<string>();
            var betterment = new List<string>();
            decimal? inlineValue = null;
            foreach (var word in row.Words)
            {
                if (word.Text == "*" || (current == Section.Extras && word.Text == "Specialist"))
                {
                    continue;
                }
                if (valueZoneX is { } valueX && word.X >= valueX)
                {
                    inlineValue = ParseAmount(word.Text)
                        ?? throw new EstimateParseRejectedException(
                            "An amount in the estimate could not be read, so nothing was imported.");
                    continue;
                }
                if (bettermentX is { } betX && word.X >= betX - 8)
                {
                    betterment.Add(word.Text);
                    continue;
                }
                if (partX is { } partNumberX && word.X >= partNumberX - 8)
                {
                    partNumber.Add(word.Text);
                    continue;
                }
                if (word.X >= descriptionX - 8)
                {
                    description.Add(word.Text);
                    continue;
                }
                guide.Add(word.Text);
            }

            if (description.Count == 0)
            {
                // A label or spacer row (e.g. "Repair / Guide", "Number") carries no line.
                return;
            }

            var line = new PendingLine
            {
                Y = row.Y,
                GuideCode = guide.Count == 0 ? null : string.Join(' ', guide),
                Description = string.Join(' ', description),
                PartNumber = partNumber.Count == 0 ? null : string.Join(' ', partNumber),
                Betterment = betterment.Count == 0 ? null : string.Join(' ', betterment),
                Value = inlineValue,
            };
            ResolveBare(state);
            if (line is { GuideCode: null, PartNumber: null, Betterment: null, Value: null })
            {
                bare = line;
                return;
            }

            FlushPending(state);
            pending = line;
        }

        /// <summary>
        /// The held description-only row received no value row, so it was
        /// continuation text under the previous line; it never carries money.
        /// </summary>
        private void ResolveBare(SectionState? state)
        {
            if (bare is null)
            {
                return;
            }
            if (state is null)
            {
                bare = null;
                return;
            }
            if (pending is not null)
            {
                pending.Description = $"{pending.Description} {bare.Description}";
            }
            else if (state.Lines.Count > 0)
            {
                var previous = state.Lines[^1];
                state.Lines[^1] = previous with
                {
                    Description = $"{previous.Description} {bare.Description}",
                };
            }
            else
            {
                throw new EstimateParseRejectedException(
                    "Text in the estimate could not be attributed to a line, so nothing was imported.");
            }
            bare = null;
        }

        /// <summary>Emits the line still waiting for a value row, valueless.</summary>
        private void FlushPending(SectionState? state)
        {
            if (pending is null || state is null)
            {
                pending = null;
                return;
            }

            // The row's identity in the document is its section and its
            // ordinal within that section, so it survives the concatenation
            // of the four sections into one ordered line set.
            state.Lines.Add(ToLine(pending) with
            {
                SourceRowIdentity = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{current.ToString().ToLowerInvariant()}:{state.Lines.Count + 1}"),
            });
            if (pending.Value is { } value)
            {
                state.Values.Add(value);
            }
            pending = null;
        }

        private EstimateLineInput ToLine(PendingLine line)
        {
            var description = line.Description;
            return current switch
            {
                Section.Labour => new(
                    LabourType(description), line.GuideCode, description,
                    WorkUnits: line.Value, Price: null, Unpriced: false,
                    PartNumber: null, Betterment: null,
                    Status: "provisional", EvidenceLabel: "case", Justification: null),
                Section.Paint => new(
                    PaintType(description), line.GuideCode, description,
                    WorkUnits: line.Value, Price: null, Unpriced: false,
                    PartNumber: null, Betterment: null,
                    Status: "provisional", EvidenceLabel: "case", Justification: null),
                Section.Parts => new(
                    "new_part", line.GuideCode, description,
                    WorkUnits: null, Price: line.Value, Unpriced: line.Value is null,
                    PartNumber: line.PartNumber, Betterment: line.Betterment,
                    Status: "provisional", EvidenceLabel: "case", Justification: null),
                Section.Extras => new(
                    "specialist_fixed", line.GuideCode, description,
                    WorkUnits: null, Price: line.Value, Unpriced: line.Value is null,
                    PartNumber: null, Betterment: line.Betterment,
                    Status: "provisional", EvidenceLabel: "case", Justification: null),
                _ => throw new EstimateParseRejectedException(
                    "Text in the estimate could not be attributed to a line, so nothing was imported."),
            };
        }

        private static string LabourType(string description) => description switch
        {
            _ when description.StartsWith("REPAIR", StringComparison.OrdinalIgnoreCase) => "repair",
            _ when description.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase) => "check_labour",
            _ => "rnr",
        };

        private static string PaintType(string description) => description switch
        {
            _ when description.Contains("NEW PART PAINT", StringComparison.OrdinalIgnoreCase) => "paint_new",
            _ when description.Contains("BLEND", StringComparison.OrdinalIgnoreCase) => "paint_blend",
            _ when description.Contains("PREPARATION", StringComparison.OrdinalIgnoreCase) => "paint_prep",
            _ => "paint_repair",
        };

        /// <summary>
        /// Reads a printed amount ("16.2", "£1,843.49") strictly; anything
        /// else returns null so the caller rejects instead of guessing.
        /// </summary>
        private static decimal? ParseAmount(string text)
        {
            var cleaned = text.TrimStart('£').Replace(",", "", StringComparison.Ordinal);
            return decimal.TryParse(
                cleaned,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var amount) && amount >= 0
                ? amount
                : null;
        }
    }
}
