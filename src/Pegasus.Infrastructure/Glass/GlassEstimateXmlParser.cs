using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Pegasus.Core.Assessment;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// The vehicle and document facts a Glass's export states about itself. They
/// are the identity a later step reconciles the export against the Case with
/// (registration, mileage, the Glass's type number Pegasus records as the
/// NatCode); nothing here is enforced by the parser, because a mismatch is a
/// gateway decision about the wrong document, not an unreadable one.
/// </summary>
public sealed record GlassEstimateIdentity(
    string? RegistrationPlate,
    int? Mileage,
    string? MileageUnitCode,
    string? TypeNumber,
    string? Vin);

/// <summary>
/// The calculation sheet Glass's embeds in its own export, already decoded
/// and proven to be a PDF. It is the document the retention step takes into
/// custody beside the XML; the parser never writes it anywhere.
/// </summary>
public sealed record GlassEstimateAttachment(string FileName, ReadOnlyMemory<byte> Content);

/// <summary>
/// One Glass's export read whole: the estimate the canonical import lands,
/// the identity facts the gateway reconciles, and the embedded calculation
/// sheet the custody step retains.
/// </summary>
public sealed record GlassEstimateExport(
    ParsedEstimate Estimate,
    GlassEstimateIdentity Identity,
    GlassEstimateAttachment? CalculationSheet);

/// <summary>
/// Deterministic parser for the Glass's Repair Estimate (ERE) export
/// document, route <see cref="RepairSpecificationSourceRoute.Glasses"/>.
///
/// <para>
/// The document is an <c>&lt;Estimation&gt;</c> element carrying
/// <c>GlobalSetting</c>, <c>FileDamage</c>, <c>Vehicle</c>, <c>Valuation</c>,
/// <c>Calculation</c> and an optional base64 <c>Attachment</c>. Every costed
/// row is a <c>Calculation/Position</c>; <c>Calculation/Result</c> prints the
/// document's own totals and <c>Calculation/Rate</c> the rates and
/// percentages it computed them at.
/// </para>
///
/// <para><b>Time.</b> <c>Rate/Other/TimeUnit</c> states how the calculation
/// counts time. <c>60</c> is the code of the Hour option, not a divisor: at
/// <c>TimeUnit 60</c> a Position's <c>Time</c> is decimal hours, which the
/// reference exports prove against their own arithmetic: the eight-position
/// export prints <c>LabourRate/PanelBeater</c> 80.00 and
/// <c>TotalAmountLabourCosts</c> 488.00, and its Positions' <c>Time</c> sums
/// to exactly 6.1 — 6.1 × 80 = 488.00. Reading <c>Time</c> as sixtieths would
/// have made that estimate's labour £8.13. Any other positive whole number N
/// counts work units, N to the hour, and every <c>Time</c>, <c>OverlapTime</c>
/// and <c>EtgTime</c> of the document is a count of them: hours are the stated
/// figure divided by N. The EVA-profile exports prove it at 10 and 12: at
/// <c>TimeUnit 10</c> a set-up <c>Time</c> of 2.00 is 0.2 h, and 0.2 × 83.28 is
/// the 16.66 the same export prints as <c>TotalAmountAdditionalCosts</c>. A
/// zero, negative, absent or non-integer unit refuses the document. The stated
/// figure is validated as printed, and the hours are retained at
/// <see cref="EstimatePolicy.WorkUnitDecimals"/>, never rounded to the
/// editor's 0.1 step.
/// </para>
///
/// <para><b>Which hours a row costs.</b> Glass's states each row's gross time
/// and then two deductions inside the same row: <c>OverlapTime</c> is time
/// this row shares with another and <c>Part_InclusiveSparePart</c> is an
/// operation whose time is already inside its parent row's. Pegasus costs an
/// estimate from its own rows, so a row carries the time it actually adds:
/// <c>Time − OverlapTime</c>. The same two reference exports prove that rule
/// against the figure Glass's printed — 6.1 − 0 − 0 = 6.1 hours and
/// 27.3 − 3.1 − 4.5 = 19.7 hours, at 80.00 the printed 488.00 and 1,576.00.
/// The gross figures stay in the document; the import records the row values
/// as read here.
/// </para>
///
/// <para><b>Included operations.</b> An inclusive row charges nothing of its
/// own, so it lands as a no-charge Other line with neither hours nor a price,
/// noted as included in the nearest part row before it — Glass's lists an
/// inclusive row after its parent. That keeps an included Front Grille out of
/// the new parts, and is how the Glass's calculation PDF reader lands the same row.
/// </para>
///
/// <para><b>Parts and paint.</b> <c>PosType</c> owns the split, not
/// <c>RepairKind</c>: a <c>Part_*</c> row prices a part, so its <c>Price</c>
/// is the unit amount and <c>RepairKind</c> chooses the operation; a
/// <c>Paint_*</c> row prices paint material, so its <c>Price</c> is the row's
/// materials and its time is paint time. That is what the document's own
/// statistics say — <c>TotalAmountParts</c> is the sum of the <c>Part_*</c>
/// prices and <c>TotalAmountPaint</c> the sum of the <c>Paint_*</c> prices.
/// A <c>Paint_Part</c> row's line type is its paint level, not its repair
/// kind: <see cref="GlassPaintLevels"/> maps the row's <c>PaintMatKind</c>
/// and <c>PaintLevel</c>, the same table the calculation PDF reader maps its
/// printed level with. <c>Paint_PreparationScratchResistantClearCoatWork</c>
/// and <c>Paint_ClearVarnish</c> are paint preparation, like the four
/// preparation rows before them, and <c>PaintMatExtraAppl</c> is the sheet's
/// printed <c>Z</c> flag, noted as markup material.
/// </para>
///
/// <para><b>Additional operations.</b> Glass's writes a user-defined
/// additional operation — a road test, a sundries charge, a collection — as a
/// <c>Free_Part</c> row, read the same way as a part row, whose
/// <c>RepairKind</c> is <c>Extra costs</c>. Its own set-up time is the
/// <c>Part_SetUpTime</c> row of the same kind. It is a Specialist line, as EVA
/// files it: a row with hours costs them at the estimate's rate, and a row
/// without prices a fixed Specialist amount. Glass's counts those amounts in
/// <c>TotalAmountParts</c>; Pegasus does not.
/// </para>
///
/// <para><b>What the figures do not say.</b> The side Glass's prints beside a
/// part (<c>Place</c>, <c>L</c> or <c>R</c>) follows the description as the
/// calculation sheet prints it, so a left and a right part are two different
/// lines. A guide time or price the engineer changed (<c>TimeMarker</c>,
/// <c>PriceMarker</c>) and Glass's own reason for it are kept as the line's
/// note, in the wording the calculation PDF reader uses. So are the row's time
/// and price annotations (<c>TimeAnnot</c>, <c>PriceAnnot</c>) and each of its
/// criteria Glass's marked as selected, in the sheet's own wording.
/// </para>
///
/// <para><b>Totals are not stored.</b> <c>ExclVatStatisticResults</c> and
/// <c>Result</c> are returned as <see cref="ParsedEstimate.SourceTotals"/> and
/// never reconciled against the rows: Pegasus costs the estimate from its own
/// rows at its own rate, discounts and VAT categories. The import stores none
/// of them. A discount, surcharge, small-material, sourcing, disposal or
/// environmental-fee value the <c>Rate</c> or <c>Result</c> block prints as
/// anything but zero changes no line and no total; it is noted on the first
/// line that has room, so the staff member sees that Glass's applied it.
/// </para>
///
/// <para><b>Zero positions.</b> An ERE calculation saved before any damage was
/// costed exports a well-formed <c>&lt;Estimation&gt;</c> with no Position and
/// no Attachment, and its statistics print 0.000000. That is a valid empty
/// estimate, not a failed parse.
/// </para>
///
/// <para><b>Fail closed.</b> The document is read through an
/// <see cref="XmlReader"/> with DTDs prohibited, no resolver and explicit
/// entity and document caps, so no external entity, no DTD and no entity
/// expansion can be reached. An unknown <c>PosType</c>, an unknown
/// <c>RepairKind</c>, a paint level outside <see cref="GlassPaintLevels"/>, a
/// time unit that is not a positive whole number, an
/// unreadable number, an over-long document and an
/// attachment that is not a PDF each reject the whole import with
/// <see cref="EstimateParseRejectedException"/> — nothing is guessed and no
/// partial line set is ever returned.
/// </para>
/// </summary>
public sealed class GlassEstimateXmlParser : IEstimateDocumentParser
{
    public IReadOnlyList<string> FileExtensions { get; } = [".xml"];

    /// <summary>Titles the Draft an import of this document lands as.</summary>
    public const string ProviderName = "Glass's";

    /// <summary>The <c>TimeUnit</c> code of the Hour option, at which a stated time is already hours; see the class remarks.</summary>
    private const int HourTimeUnit = 60;

    /// <summary>An export beyond this size is refused unread.</summary>
    public const int MaximumDocumentBytes = 16 * 1024 * 1024;

    /// <summary>An embedded calculation sheet beyond this size is refused.</summary>
    public const int MaximumAttachmentBytes = 8 * 1024 * 1024;

    /// <summary>The document root this format is recognized by.</summary>
    private const string RootName = "Estimation";

    /// <summary>An operation whose time its parent position already carries.</summary>
    private const string InclusivePosition = "Part_InclusiveSparePart";

    /// <summary>A user-defined operation, read the same way as a part row.</summary>
    private const string FreePosition = "Free_Part";

    /// <summary>Glass's own set-up time: an <c>Extra costs</c> row, read the same way as a part row.</summary>
    private const string SetUpTimePosition = "Part_SetUpTime";

    /// <summary>The paint row whose level and material kind choose its line type.</summary>
    private const string PaintPosition = "Paint_Part";

    /// <summary>The note a paint row carries when Glass's marked up its paint material (the sheet's <c>Z</c>).</summary>
    private const string MarkupMaterialNote = "Markup material";

    /// <summary>
    /// A general entity cannot be declared without a DTD, which is prohibited
    /// above; the cap is stated anyway so the reader refuses expansion even if
    /// that ever changes.
    /// </summary>
    private const long MaximumEntityCharacters = 1024;

    /// <summary>The adjustments one note lists before it summarizes the rest.</summary>
    private const int MaximumListedAdjustments = 8;

    /// <summary>
    /// The <c>Rate</c> block's discounts, surcharges and fees: its child
    /// block, the element, the words the note uses, and the element naming
    /// its unit where it has one.
    /// </summary>
    private static readonly (string Block, string Element, string Label, string? Specifier)[] RateAdjustments =
    [
        ("Discount", "DiscMatPart", "parts material discount", "DiscMatPartSpecifier"),
        ("Discount", "DiscMatPaint", "paint material discount", "DiscMatPaintSpecifier"),
        ("Discount", "DiscWorkPart", "parts labour discount", "DiscWorkPartSpecifier"),
        ("Discount", "DiscWorkPaint", "paint labour discount", "DiscWorkPaintSpecifier"),
        ("Discount", "DiscOverall", "overall discount", null),
        ("SurCharge", "SurChargeMatPart", "parts material surcharge", "SurChargeMatPartSpecifier"),
        ("SurCharge", "SurChargeMatPaint", "paint material surcharge", "SurChargeMatPaintSpecifier"),
        ("SurCharge", "SurChargeWorkPart", "parts labour surcharge", "SurChargeWorkPartSpecifier"),
        ("SurCharge", "SurChargeWorkPaint", "paint labour surcharge", "SurChargeWorkPaintSpecifier"),
        ("Other", "SmallMaterial", "small materials", "SmallMaterialSpecifier"),
        ("Other", "SourcingCostPCNT", "sourcing cost (percent)", null),
        ("Other", "DisposalCostPCNT", "disposal cost (percent)", null),
        ("Other", "EnvironmentalFee", "environmental fee", null),
    ];

    /// <summary>The <c>Result</c> block's discount and surcharge amounts: the element and the words the note uses.</summary>
    private static readonly (string Element, string Label)[] ResultAdjustments =
    [
        ("DiscMatParts", "parts material discount"),
        ("DiscMatPaint", "paint material discount"),
        ("DiscWorkParts", "parts labour discount"),
        ("DiscWorkPaint", "paint labour discount"),
        ("SurChargeMatParts", "parts material surcharge"),
        ("SurChargeMatPaint", "paint material surcharge"),
        ("SurChargeWorkParts", "parts labour surcharge"),
        ("SurChargeWorkPaint", "paint labour surcharge"),
        ("DiscOverall", "overall discount"),
    ];

    private const int MoneyDecimals = 2;
    private const int MaximumDescriptionLength = 300;
    private const int MaximumNoteLength = 500;
    private const int MaximumGuideCodeLength = 50;
    private const int MaximumPartNumberLength = 100;
    private const int MaximumSourceVersionLength = 100;
    private const int MaximumAttachmentNameLength = 200;

    /// <summary>Enough tail to hold the cross-reference trailer and its marker.</summary>
    private const int PdfEndMarkerWindow = 2048;

    private static readonly byte[] Utf8ByteOrderMark = [0xEF, 0xBB, 0xBF];
    private static readonly byte[] PdfPrefix = "%PDF-"u8.ToArray();
    private static readonly byte[] PdfEndMarker = "%%EOF"u8.ToArray();

    /// <summary>
    /// The export is XML by name or media type. The format itself is proven
    /// by its <c>&lt;Estimation&gt;</c> root inside <see cref="Parse"/>, which
    /// is the only place the bytes are available.
    /// </summary>
    public bool CanParse(string fileName, string mediaType) =>
        string.Equals(Path.GetExtension(fileName), ".xml", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mediaType, "application/xml", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mediaType, "text/xml", StringComparison.OrdinalIgnoreCase);

    public ParsedEstimate Parse(ReadOnlyMemory<byte> content) => Read(content).Estimate;

    /// <summary>
    /// The whole export: the estimate <see cref="Parse"/> returns, the
    /// identity facts a Glass's session reconciles against its Case, and the
    /// decoded calculation sheet the custody step retains.
    /// </summary>
    public static GlassEstimateExport Read(ReadOnlyMemory<byte> content)
    {
        if (content.Length > MaximumDocumentBytes)
        {
            throw new EstimateParseRejectedException(
                $"The export is larger than {MaximumDocumentBytes} bytes, so nothing was imported.");
        }

        var root = ReadRoot(TrimTrailingByteOrderMark(content));
        var calculation = root.Element("Calculation")
            ?? throw new EstimateParseRejectedException(
                "The export carries no calculation, so nothing was imported.");
        var divisor = ReadTimeDivisor(calculation);

        var positions = calculation.Elements("Position").ToArray();
        if (positions.Length > AssessmentPolicy.MaximumEstimateLines)
        {
            throw new EstimateParseRejectedException(
                $"The export carries more than {AssessmentPolicy.MaximumEstimateLines} positions, "
                + "so nothing was imported.");
        }

        var lines = new List<EstimateLineInput>(positions.Length);
        int? parent = null;
        for (var index = 0; index < positions.Length; index++)
        {
            var ordinal = index + 1;
            var (line, charge) = ReadPosition(positions[index], ordinal, parent, divisor);
            lines.Add(line);
            if (charge == Charge.Part)
            {
                parent = ordinal;
            }
        }

        var totals = ReadTotals(calculation);
        NoteAdjustments(lines, calculation, totals?.Net);
        return new GlassEstimateExport(
            new ParsedEstimate(SourceVersion(root, calculation), lines, ProviderName, RepairSpecificationSourceRoute.Glasses, totals),
            ReadIdentity(root),
            ReadAttachment(root));
    }

    private static XElement ReadRoot(ReadOnlyMemory<byte> content)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = MaximumEntityCharacters,
            MaxCharactersInDocument = MaximumDocumentBytes,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = true,
            CloseInput = true,
        };

        XElement root;
        try
        {
            using var stream = new MemoryStream(content.ToArray(), writable: false);
            using var reader = XmlReader.Create(stream, settings);
            root = XDocument.Load(reader).Root
                ?? throw new EstimateParseRejectedException(
                    "The export carries no XML element, so nothing was imported.");
        }
        catch (XmlException)
        {
            throw new EstimateParseRejectedException(
                "The file could not be read as a Glass's XML export, so nothing was imported.");
        }

        return root.Name == RootName
            ? root
            : throw new EstimateParseRejectedException(
                $"The document's root is '{root.Name.LocalName}' and not '{RootName}', so nothing was imported.");
    }

    /// <summary>
    /// A UTF-8 byte-order mark is tolerated at either end. The reader detects
    /// a leading one itself; a trailing one is a stray marker rather than
    /// document content, and only that end needs removing.
    /// </summary>
    private static ReadOnlyMemory<byte> TrimTrailingByteOrderMark(ReadOnlyMemory<byte> content) =>
        content.Length >= Utf8ByteOrderMark.Length
            && content.Span[^Utf8ByteOrderMark.Length..].SequenceEqual(Utf8ByteOrderMark)
            ? content[..^Utf8ByteOrderMark.Length]
            : content;

    /// <summary>What a stated time is divided by to give hours: 1 at the Hour option, the unit's own count otherwise.</summary>
    private static decimal ReadTimeDivisor(XElement calculation)
    {
        var stated = Text(calculation.Element("Rate")?.Element("Other")?.Element("TimeUnit"))
            ?? throw new EstimateParseRejectedException(
                "The calculation states no time unit, so nothing was imported.");
        if (!int.TryParse(stated, NumberStyles.None, CultureInfo.InvariantCulture, out var timeUnit)
            || timeUnit <= 0)
        {
            throw new EstimateParseRejectedException(
                $"The calculation states time unit '{stated}', so nothing was imported.");
        }
        return timeUnit == HourTimeUnit ? 1m : timeUnit;
    }

    /// <summary>What a position charges, which decides the figures its line carries.</summary>
    private enum Charge
    {
        /// <summary>A part or operation row: panel hours and a unit amount.</summary>
        Part,

        /// <summary>An operation its parent row already carries: nothing of its own.</summary>
        Included,

        /// <summary>A paint row: paint hours and paint materials.</summary>
        Paint,
    }

    /// <param name="parent">The row an included operation belongs to: the nearest part row before it.</param>
    /// <param name="divisor">What the document's stated times are divided by to give hours.</param>
    private static (EstimateLineInput Line, Charge Charge) ReadPosition(
        XElement position, int ordinal, int? parent, decimal divisor)
    {
        var posType = Text(position.Element("PosType"))
            ?? throw Reject(ordinal, "names no position type");
        var operation = Operation(Text(position.Element("RepairKind")), ordinal);
        var description = Bounded(Description(position), MaximumDescriptionLength, ordinal, "description")
            ?? throw Reject(ordinal, "carries no description");

        var price = Money(position.Element("Price"), ordinal, "price");
        var time = Hours(position.Element("Time"), ordinal, "time", divisor) ?? 0m;
        var hours = ChargeableHours(position, time, posType, ordinal, divisor);
        var (type, charge) = LineShape(position, posType, operation, hours, ordinal);
        var guideCode = Bounded(Text(position.Element("MCode")), MaximumGuideCodeLength, ordinal, "MCode");
        var isPaint = charge == Charge.Paint;
        var isPart = charge == Charge.Part;

        return (new EstimateLineInput(
            type,
            guideCode,
            description,
            WorkUnits: isPart ? hours : null,
            Price: isPart ? price : null,
            Unpriced: isPart && price is null && type == "new_part",
            PartNumber: Bounded(
                Text(position.Element("OEMPartNo")) ?? Text(position.Element("ManPartNo")),
                MaximumPartNumberLength,
                ordinal,
                "part number"),
            Betterment: null,
            // The Audatex report's own evidence label, because this is the same claim.
            EvidenceLabel: "case",
            Justification: Note(position, ordinal, charge == Charge.Included, isPaint, parent, time, price, divisor),
            PaintWorkUnits: isPaint ? hours : null,
            Quantity: null,
            Materials: isPaint ? price : null,
            SourceRowIdentity: guideCode is null
                ? ordinal.ToString(CultureInfo.InvariantCulture)
                : string.Create(CultureInfo.InvariantCulture, $"{ordinal}:{guideCode}")), charge);
    }

    /// <summary>The row's text, followed by the side Glass's prints beside it.</summary>
    private static string? Description(XElement position)
    {
        var text = Text(position.Element("Text"));
        var place = Text(position.Element("Place"));
        return text is null || place is null ? text : $"{text} ({place})";
    }

    /// <summary>The line's note; see the class remarks. The markers are written true or false.</summary>
    private static string? Note(
        XElement position, int ordinal, bool included, bool paint, int? parent, decimal time, decimal? price, decimal divisor)
    {
        List<string> notes = [];
        if (included)
        {
            notes.Add(parent is { } row
                ? string.Create(CultureInfo.InvariantCulture, $"Included in row {row}; no separate charge.")
                : "Included; no separate charge.");
        }
        if (paint && Marked(position.Element("PaintMatExtraAppl")))
        {
            notes.Add(MarkupMaterialNote);
        }
        if (Marked(position.Element("TimeMarker")))
        {
            notes.Add(Modified(
                "time",
                time,
                Hours(position.Element("EtgTime"), ordinal, "guide time", divisor),
                "h"));
        }
        if (Marked(position.Element("PriceMarker")))
        {
            notes.Add(Modified(
                "price",
                price,
                Money(position.Element("EtgPrice"), ordinal, "guide price"),
                "GBP"));
        }
        if (Text(position.Element("TimeAnnot")) is { } timeAnnotation)
        {
            notes.Add($"Annotation (time): {timeAnnotation}");
        }
        if (Text(position.Element("PriceAnnot")) is { } priceAnnotation)
        {
            notes.Add($"Annotation (price): {priceAnnotation}");
        }
        foreach (var criterion in position.Elements("Criteria"))
        {
            if (Marked(criterion.Element("State")) && Text(criterion.Element("Text")) is { } selected)
            {
                notes.Add($"Selected criteria: {selected}");
            }
        }
        if (Text(position.Element("Reason")) is { } reason)
        {
            notes.Add(reason);
        }
        return notes.Count == 0
            ? null
            : Bounded(string.Join(' ', notes), MaximumNoteLength, ordinal, "note");
    }

    private static string Modified(string field, decimal? stated, decimal? guide, string unit) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Modified source value: {field} {(stated ?? 0m):0.00} {unit}, guide {(guide ?? 0m):0.00} {unit}.");

    private static bool Marked(XElement? marker) =>
        string.Equals(Text(marker), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The time this row adds to the repair: its stated time less the time it
    /// shares with another row, and none at all for an included operation,
    /// which charges nothing of its own. See the class remarks for the two
    /// exports that prove the rule against Glass's own printed labour.
    /// </summary>
    private static decimal ChargeableHours(XElement position, decimal time, string posType, int ordinal, decimal divisor)
    {
        var overlap = Hours(position.Element("OverlapTime"), ordinal, "overlap time", divisor) ?? 0m;
        if (posType == InclusivePosition)
        {
            return 0m;
        }
        return time >= overlap
            ? time - overlap
            : throw Reject(ordinal, "states more overlap time than time");
    }

    /// <summary>
    /// The one place a position type is read: the estimate line type it lands
    /// as, and what it charges. An unknown type is refused here rather than
    /// guessed at in either answer.
    /// </summary>
    private static (string Type, Charge Charge) LineShape(
        XElement position, string posType, EstimateOperation operation, decimal hours, int ordinal) => posType switch
    {
        "Part_SparePart" or FreePosition or SetUpTimePosition =>
            (EstimateOperations.ToLineType(operation, hours), Charge.Part),
        InclusivePosition => (EstimateOperations.ToLineType(EstimateOperation.Other), Charge.Included),
        // The paint level, not the repair kind, says what the paint work is
        // (every observed Paint_Part is a Replace); the sheet's own table maps it.
        PaintPosition => (PaintPartType(position, ordinal), Charge.Paint),
        "Paint_PreparationMetal" or "Paint_PreparationPlastic"
            or "Paint_ColourMixing" or "Paint_ColourSample"
            or "Paint_PreparationScratchResistantClearCoatWork" or "Paint_ClearVarnish" =>
            ("paint_prep", Charge.Paint),
        _ => throw Reject(ordinal, $"carries the unknown position type '{posType}'"),
    };

    /// <summary>The line type of a paint row's level; a pair outside <see cref="GlassPaintLevels"/> is refused.</summary>
    private static string PaintPartType(XElement position, int ordinal)
    {
        var kind = Text(position.Element("PaintMatKind"));
        var stated = Text(position.Element("PaintLevel"));
        var level = int.TryParse(stated, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : int.MinValue;
        return GlassPaintLevels.LineTypeOfExport(kind, level)
            ?? throw Reject(ordinal, $"carries the unknown paint level (material '{kind}', level '{stated}')");
    }

    private static EstimateOperation Operation(string? repairKind, int ordinal) => repairKind switch
    {
        "Replace" => EstimateOperation.Replace,
        "Repair" => EstimateOperation.Repair,
        "Uninstall and install" => EstimateOperation.RemoveAndRefit,
        "Control" or "Sealing" or "Adjust" or "Air out" => EstimateOperation.Other,
        "Extra costs" => EstimateOperation.Specialist,
        null => throw Reject(ordinal, "names no repair kind"),
        _ => throw Reject(ordinal, $"carries the unknown repair kind '{repairKind}'"),
    };

    /// <summary>
    /// The document's own arithmetic. It prints money totals only — a labour
    /// cost, never a work-unit count — so the work-unit members stay unstated
    /// rather than being back-derived from a rate.
    /// </summary>
    private static EstimateSourceTotals? ReadTotals(XElement calculation)
    {
        var result = calculation.Element("Result");
        if (result is null)
        {
            return null;
        }
        var statistics = result.Element("ExclVatStatisticResults");
        var exclusive = result.Element("ExclVatResults");
        var inclusive = result.Element("InclVatResults");
        return new EstimateSourceTotals(
            Parts: Money(statistics?.Element("TotalAmountParts"), 0, "parts total"),
            Materials: Money(statistics?.Element("TotalAmountPaint"), 0, "paint total"),
            Specialist: Money(statistics?.Element("TotalAmountAdditionalCosts"), 0, "additional costs total"),
            Net: Money(exclusive?.Element("TotRepCostExclVat"), 0, "net total"),
            Vat: Vat(inclusive),
            Gross: Money(inclusive?.Element("GrandTotal"), 0, "gross total"));
    }

    /// <summary>
    /// A discount, surcharge, small-material, sourcing, disposal or
    /// environmental-fee value Glass's printed as anything but zero. The
    /// record has no estimate-level note, so the note joins the first line's
    /// own (the line note is the one existing note slot) when it fits the
    /// note bound; nothing changes any line or total.
    /// </summary>
    private static void NoteAdjustments(List<EstimateLineInput> lines, XElement calculation, decimal? net)
    {
        var rate = calculation.Element("Rate");
        var result = calculation.Element("Result")?.Element("ExclVatResults");
        List<string> found = [];
        foreach (var (block, element, label, specifier) in RateAdjustments)
        {
            var parent = rate?.Element(block);
            AddAdjustment(found, parent?.Element(element), label, specifier is null ? null : parent?.Element(specifier));
        }
        foreach (var (element, label) in ResultAdjustments)
        {
            AddAdjustment(found, result?.Element(element), $"{label} amount", null);
        }
        if (found.Count == 0 || lines.Count == 0)
        {
            return;
        }

        List<string> listed = found.Count > MaximumListedAdjustments
            ? [.. found.Take(MaximumListedAdjustments), string.Create(CultureInfo.InvariantCulture, $"and {found.Count - MaximumListedAdjustments} more")]
            : found;
        var note = $"Glass's printed adjustments, not applied here: {string.Join(", ", listed)}."
            + (net is { } printed
                ? string.Create(CultureInfo.InvariantCulture, $" Glass's net {printed:0.00} GBP.")
                : string.Empty);
        for (var index = 0; index < lines.Count; index++)
        {
            var own = lines[index].Justification;
            var joined = own is null ? note : $"{own} {note}";
            if (joined.Length <= MaximumNoteLength)
            {
                lines[index] = lines[index] with { Justification = joined };
                return;
            }
        }
    }

    /// <summary>Adds a printed value that is not zero. A value that cannot be read as a number is not known to be zero, so it is listed as printed.</summary>
    private static void AddAdjustment(List<string> found, XElement? value, string label, XElement? specifier)
    {
        var stated = Text(value);
        if (stated is null
            || (decimal.TryParse(stated, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var parsed) && parsed == 0m))
        {
            return;
        }
        var unit = Text(specifier);
        found.Add(unit is null ? $"{label} {stated}" : $"{label} {stated} {unit}");
    }

    /// <summary>Glass's prints VAT split by material and labour; the record holds one figure.</summary>
    private static decimal? Vat(XElement? inclusive)
    {
        var material = Money(inclusive?.Element("VatMat"), 0, "material VAT");
        var labour = Money(inclusive?.Element("VatWork"), 0, "labour VAT");
        return material is null && labour is null ? null : (material ?? 0m) + (labour ?? 0m);
    }

    private static GlassEstimateIdentity ReadIdentity(XElement root)
    {
        var identification = root.Element("Vehicle")?.Element("Identification");
        var stated = Text(identification?.Element("Mileage"));
        int? mileage = null;
        if (stated is not null)
        {
            mileage = int.TryParse(stated, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : throw new EstimateParseRejectedException(
                    "The export states an unreadable mileage, so nothing was imported.");
        }
        return new GlassEstimateIdentity(
            Text(identification?.Element("RegPlt")),
            mileage,
            Text(identification?.Element("MilUnit")),
            Text(identification?.Element("TypeNo")),
            Text(identification?.Element("VIN")));
    }

    /// <summary>
    /// The export's own identity: the schema version it was written to and
    /// the moment the calculation it carries was last changed.
    /// </summary>
    private static string SourceVersion(XElement root, XElement calculation)
    {
        var schema = Text(root.Element("GlobalSetting")?.Element("XMLDocVers"))
            ?? throw new EstimateParseRejectedException(
                "The export states no document version, so nothing was imported.");
        var setting = calculation.Element("Setting");
        var stamp = Text(setting?.Element("Modified"))
            ?? Text(setting?.Element("Created"))
            ?? throw new EstimateParseRejectedException(
                "The calculation states no created or modified time, so nothing was imported.");
        var version = $"{schema} {stamp}";
        return version.Length <= MaximumSourceVersionLength
            ? version
            : throw new EstimateParseRejectedException(
                $"The export's own version exceeds {MaximumSourceVersionLength} characters, "
                + "so nothing was imported.");
    }

    private static GlassEstimateAttachment? ReadAttachment(XElement root)
    {
        var attachments = root.Elements("Attachment").ToArray();
        if (attachments.Length == 0)
        {
            return null;
        }
        if (attachments.Length > 1)
        {
            throw new EstimateParseRejectedException(
                "The export carries more than one attachment, so nothing was imported.");
        }

        var attachment = attachments[0];
        var type = attachment.Attribute("Type")?.Value.Trim();
        if (!string.Equals(type, "PDF", StringComparison.Ordinal))
        {
            throw new EstimateParseRejectedException(
                $"The export's attachment is of type '{type}' and not PDF, so nothing was imported.");
        }

        var encoded = Text(attachment.Element("Document"))
            ?? throw new EstimateParseRejectedException(
                "The export's attachment carries no document, so nothing was imported.");
        if (encoded.Length / 4 * 3 > MaximumAttachmentBytes)
        {
            throw new EstimateParseRejectedException(
                $"The export's attachment is larger than {MaximumAttachmentBytes} bytes, so nothing was imported.");
        }
        var decoded = new byte[((encoded.Length / 4) + 1) * 3];
        if (!Convert.TryFromBase64String(encoded, decoded, out var written))
        {
            throw new EstimateParseRejectedException(
                "The export's attachment is not readable base64, so nothing was imported.");
        }
        var content = decoded.AsMemory(0, written);
        RequirePdf(content.Span);

        return new GlassEstimateAttachment(AttachmentName(attachment), content);
    }

    private static void RequirePdf(ReadOnlySpan<byte> content)
    {
        if (content.Length <= PdfPrefix.Length || !content[..PdfPrefix.Length].SequenceEqual(PdfPrefix))
        {
            throw new EstimateParseRejectedException(
                "The export's attachment does not begin as a PDF, so nothing was imported.");
        }
        var tail = content.Length <= PdfEndMarkerWindow ? content : content[^PdfEndMarkerWindow..];
        if (tail.IndexOf(PdfEndMarker) < 0)
        {
            throw new EstimateParseRejectedException(
                "The export's attachment does not end as a PDF, so nothing was imported.");
        }
    }

    private static string AttachmentName(XElement attachment)
    {
        var name = Text(attachment.Element("Name"))
            ?? throw new EstimateParseRejectedException(
                "The export's attachment carries no name, so nothing was imported.");
        return name.Length <= MaximumAttachmentNameLength
            && name.AsSpan().IndexOfAny(Path.GetInvalidFileNameChars()) < 0
            && !name.Any(char.IsControl)
            ? name
            : throw new EstimateParseRejectedException(
                "The export's attachment carries an unusable name, so nothing was imported.");
    }

    /// <summary>An element's trimmed text, or null when it is absent or empty.</summary>
    private static string? Text(XElement? element)
    {
        var text = element?.Value.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static string? Bounded(string? value, int maximumLength, int ordinal, string field) =>
        value is null || value.Length <= maximumLength
            ? value
            : throw Reject(ordinal, $"has a {field} beyond {maximumLength} characters");

    /// <summary>
    /// A printed amount, read strictly. The zero-position exports print their
    /// totals to six places, so the rule is the value's own precision at two
    /// places rather than the count of digits it was written with.
    /// </summary>
    private static decimal? Money(XElement? element, int ordinal, string field)
    {
        var text = Text(element);
        if (text is null)
        {
            return null;
        }
        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            || value < 0
            || decimal.Round(value, MoneyDecimals) != value)
        {
            throw Reject(ordinal, $"has an unreadable {field}");
        }
        return value;
    }

    /// <summary>A stated time, validated as printed and returned in hours (the stated figure divided by <paramref name="divisor"/>).</summary>
    private static decimal? Hours(XElement? element, int ordinal, string field, decimal divisor)
    {
        var text = Text(element);
        if (text is null)
        {
            return null;
        }
        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            || value < 0
            || decimal.Round(value, EstimatePolicy.WorkUnitDecimals) != value
            || value > EstimatePolicy.MaximumLineWorkUnits)
        {
            throw Reject(ordinal, $"has an unreadable {field}");
        }
        return decimal.Round(value / divisor, EstimatePolicy.WorkUnitDecimals, MidpointRounding.AwayFromZero);
    }

    /// <summary>Ordinal zero names the document's own totals, which sit outside the positions.</summary>
    private static EstimateParseRejectedException Reject(int ordinal, string problem) => new(
        ordinal > 0
            ? string.Create(CultureInfo.InvariantCulture, $"Position {ordinal} {problem}, so nothing was imported.")
            : $"The export's printed totals block {problem}, so nothing was imported.");
}
