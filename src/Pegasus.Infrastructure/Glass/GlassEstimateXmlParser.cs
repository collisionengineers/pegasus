using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Pegasus.Core.Assessment;

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
/// <para><b>Time.</b> <c>Rate/Other/TimeUnit</c> states the calculation's time
/// unit and the parser refuses any value but <c>60</c> rather than guess at an
/// unfamiliar one. At <c>TimeUnit 60</c> a Position's <c>Time</c> is decimal
/// hours, which the reference exports prove against their own arithmetic: the
/// eight-position export prints <c>LabourRate/PanelBeater</c> 80.00 and
/// <c>TotalAmountLabourCosts</c> 488.00, and its Positions' <c>Time</c> sums
/// to exactly 6.1 — 6.1 × 80 = 488.00. Reading <c>Time</c> as sixtieths would
/// have made that estimate's labour £8.13. The figure is retained at
/// <see cref="EstimatePolicy.WorkUnitDecimals"/> exactly as printed and is
/// never rounded to the editor's 0.1 step.
/// </para>
///
/// <para><b>Which hours a row costs.</b> Glass's states each row's gross time
/// and then two deductions inside the same row: <c>OverlapTime</c> is time
/// this row shares with another and <c>Part_InclusiveSparePart</c> is an
/// operation whose time is already inside its parent row's. Pegasus costs an
/// estimate from its own rows, so a row carries the time it actually adds:
/// <c>Time − OverlapTime</c>, and zero for an inclusive row. The same two
/// reference exports prove that rule against the figure Glass's printed —
/// 6.1 − 0 − 0 = 6.1 hours and 27.3 − 3.1 − 4.5 = 19.7 hours, at 80.00 the
/// printed 488.00 and 1,576.00. The gross figures stay in the document; the
/// import records the row values as read here.
/// </para>
///
/// <para><b>Parts and paint.</b> <c>PosType</c> owns the split, not
/// <c>RepairKind</c>: a <c>Part_*</c> row prices a part, so its <c>Price</c>
/// is the unit amount and <c>RepairKind</c> chooses the operation; a
/// <c>Paint_*</c> row prices paint material, so its <c>Price</c> is the row's
/// materials and its time is paint time. That is what the document's own
/// statistics say — <c>TotalAmountParts</c> is the sum of the <c>Part_*</c>
/// prices and <c>TotalAmountPaint</c> the sum of the <c>Paint_*</c> prices.
/// </para>
///
/// <para><b>Totals are evidence.</b> <c>ExclVatStatisticResults</c> and
/// <c>Result</c> are returned as <see cref="ParsedEstimate.SourceTotals"/> and
/// never reconciled against the rows: Pegasus costs the estimate from its own
/// rows at its own rate, discounts and VAT categories, and a printed figure
/// that disagrees is retained beside that calculation rather than dropped or
/// forced to agree.
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
/// <c>RepairKind</c>, an unreadable number, an over-long document and an
/// attachment that is not a PDF each reject the whole import with
/// <see cref="EstimateParseRejectedException"/> — nothing is guessed and no
/// partial line set is ever returned.
/// </para>
/// </summary>
public sealed class GlassEstimateXmlParser : IEstimateDocumentParser
{
    /// <summary>Titles the Draft an import of this document lands as.</summary>
    public const string ProviderName = "Glass's";

    /// <summary>The one time unit this format is read at; see the class remarks.</summary>
    public const int SupportedTimeUnit = 60;

    /// <summary>An export beyond this size is refused unread.</summary>
    public const int MaximumDocumentBytes = 16 * 1024 * 1024;

    /// <summary>An embedded calculation sheet beyond this size is refused.</summary>
    public const int MaximumAttachmentBytes = 8 * 1024 * 1024;

    /// <summary>The document root this format is recognized by.</summary>
    private const string RootName = "Estimation";

    /// <summary>An operation whose time its parent position already carries.</summary>
    private const string InclusivePosition = "Part_InclusiveSparePart";

    /// <summary>
    /// A general entity cannot be declared without a DTD, which is prohibited
    /// above; the cap is stated anyway so the reader refuses expansion even if
    /// that ever changes.
    /// </summary>
    private const long MaximumEntityCharacters = 1024;

    private const int MoneyDecimals = 2;
    private const int MaximumDescriptionLength = 300;
    private const int MaximumGuideCodeLength = 50;
    private const int MaximumPartNumberLength = 100;
    private const int MaximumSourceVersionLength = 100;
    private const int MaximumAttachmentNameLength = 200;

    /// <summary>Enough tail to hold the cross-reference trailer and its marker.</summary>
    private const int PdfEndMarkerWindow = 2048;

    private static readonly byte[] Utf8ByteOrderMark = [0xEF, 0xBB, 0xBF];
    private static readonly byte[] PdfPrefix = "%PDF-"u8.ToArray();
    private static readonly byte[] PdfEndMarker = "%%EOF"u8.ToArray();

    public RepairSpecificationSourceRoute Route => RepairSpecificationSourceRoute.Glasses;

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
        RequireSupportedTimeUnit(calculation);

        var positions = calculation.Elements("Position").ToArray();
        if (positions.Length > AssessmentPolicy.MaximumEstimateLines)
        {
            throw new EstimateParseRejectedException(
                $"The export carries more than {AssessmentPolicy.MaximumEstimateLines} positions, "
                + "so nothing was imported.");
        }

        var lines = new List<EstimateLineInput>(positions.Length);
        for (var index = 0; index < positions.Length; index++)
        {
            lines.Add(ReadPosition(positions[index], index + 1));
        }

        return new GlassEstimateExport(
            new ParsedEstimate(SourceVersion(root, calculation), lines, ProviderName, ReadTotals(calculation)),
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

    private static void RequireSupportedTimeUnit(XElement calculation)
    {
        var stated = Text(calculation.Element("Rate")?.Element("Other")?.Element("TimeUnit"))
            ?? throw new EstimateParseRejectedException(
                "The calculation states no time unit, so nothing was imported.");
        if (!int.TryParse(stated, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeUnit)
            || timeUnit != SupportedTimeUnit)
        {
            throw new EstimateParseRejectedException(
                $"The calculation states time unit '{stated}' and not {SupportedTimeUnit}, so nothing was imported.");
        }
    }

    private static EstimateLineInput ReadPosition(XElement position, int ordinal)
    {
        var posType = Text(position.Element("PosType"))
            ?? throw Reject(ordinal, "names no position type");
        var operation = Operation(Text(position.Element("RepairKind")), ordinal);
        var description = Bounded(
                Text(position.Element("Text")), MaximumDescriptionLength, ordinal, "description")
            ?? throw Reject(ordinal, "carries no description");

        var price = Money(position.Element("Price"), ordinal, "price");
        var hours = ChargeableHours(position, posType, ordinal);
        var (type, isPaint) = LineShape(posType, operation, ordinal);
        var guideCode = Bounded(Text(position.Element("MCode")), MaximumGuideCodeLength, ordinal, "MCode");

        return new EstimateLineInput(
            type,
            guideCode,
            description,
            // A Paint_* row prices paint material and states paint time; a
            // Part_* row prices the part and states panel time.
            WorkUnits: isPaint ? null : hours,
            Price: isPaint ? null : price,
            Unpriced: !isPaint && price is null && type == "new_part",
            PartNumber: Bounded(
                Text(position.Element("OEMPartNo")) ?? Text(position.Element("ManPartNo")),
                MaximumPartNumberLength,
                ordinal,
                "part number"),
            Betterment: null,
            // An imported provider document states values, not confirmations:
            // the Audatex report's own labels, because this is the same claim.
            Status: "provisional",
            EvidenceLabel: "case",
            Justification: null,
            PaintWorkUnits: isPaint ? hours : null,
            Quantity: null,
            Materials: isPaint ? price : null,
            SourceRowIdentity: guideCode is null
                ? ordinal.ToString(CultureInfo.InvariantCulture)
                : string.Create(CultureInfo.InvariantCulture, $"{ordinal}:{guideCode}"));
    }

    /// <summary>
    /// The time this row adds to the repair: its stated time less the time it
    /// shares with another row, and none at all for an operation whose time
    /// its parent row already carries. See the class remarks for the two
    /// exports that prove the rule against Glass's own printed labour.
    /// </summary>
    private static decimal ChargeableHours(XElement position, string posType, int ordinal)
    {
        var time = Hours(position.Element("Time"), ordinal, "time") ?? 0m;
        var overlap = Hours(position.Element("OverlapTime"), ordinal, "overlap time") ?? 0m;
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
    /// as, and whether it prices paint rather than a part. An unknown type is
    /// refused here rather than guessed at in either answer.
    /// </summary>
    private static (string Type, bool IsPaint) LineShape(
        string posType, EstimateOperation operation, int ordinal) => posType switch
    {
        "Part_SparePart" or InclusivePosition => (EstimateOperations.ToLineType(operation), false),
        // Painting a replaced panel and painting a repaired one are the same
        // operation at different labels; the repair kind chooses between them.
        "Paint_Part" => (operation == EstimateOperation.Replace ? "paint_new" : "paint_repair", true),
        "Paint_PreparationMetal" or "Paint_PreparationPlastic"
            or "Paint_ColourMixing" or "Paint_ColourSample" => ("paint_prep", true),
        _ => throw Reject(ordinal, $"carries the unknown position type '{posType}'"),
    };

    private static EstimateOperation Operation(string? repairKind, int ordinal) => repairKind switch
    {
        "Replace" => EstimateOperation.Replace,
        "Repair" => EstimateOperation.Repair,
        "Uninstall and install" => EstimateOperation.RemoveAndRefit,
        "Control" or "Sealing" or "Adjust" or "Air out" => EstimateOperation.Other,
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

    private static decimal? Hours(XElement? element, int ordinal, string field)
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
        return value;
    }

    /// <summary>Ordinal zero names the document's own totals, which sit outside the positions.</summary>
    private static EstimateParseRejectedException Reject(int ordinal, string problem) => new(
        ordinal > 0
            ? string.Create(CultureInfo.InvariantCulture, $"Position {ordinal} {problem}, so nothing was imported.")
            : $"The export's printed totals block {problem}, so nothing was imported.");
}
