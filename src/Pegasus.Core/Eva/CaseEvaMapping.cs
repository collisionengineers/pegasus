using System.Globalization;
using System.Text.RegularExpressions;

namespace Pegasus.Core.Eva;

public enum EvaEvidenceStatus
{
    Suggested,
    Accepted,

    /// <summary>The case holds no value for this field at all.</summary>
    Unrecorded
}

public enum EvaInspectionMode
{
    PhysicalAddress,
    ImageBasedAssessment
}

public sealed record EvaEvidenceValue(
    string? Value,
    EvaEvidenceStatus Status,
    string Source,
    string SourceVersion)
{
    public bool IsAccepted => Status is EvaEvidenceStatus.Accepted;
}

public sealed record EvaAddressResolution(
    EvaInspectionMode Mode,
    EvaEvidenceValue Evidence);

public sealed record EvaAcceptedCaseEvidence(
    Guid CaseId,
    long CaseVersion,
    bool CaseAccepted,
    bool InstructionComplete,
    bool ImagesComplete,
    EvaEvidenceValue Reference,
    EvaEvidenceValue WorkProvider,
    EvaEvidenceValue VehicleRegistration,
    EvaEvidenceValue VehicleModel,
    EvaEvidenceValue ClaimantName,
    EvaEvidenceValue IncidentDate,
    EvaEvidenceValue InstructionDate,
    EvaEvidenceValue InspectionDate,
    EvaAddressResolution Inspection,
    EvaEvidenceValue AccidentCircumstances,
    EvaEvidenceValue VatStatus,
    EvaEvidenceValue Mileage,
    EvaEvidenceValue MileageUnit);

public sealed record EvaMappingAcceptance(
    string? MappingKey,
    int? MappingVersion,
    string? EvidenceReference)
{
    public static EvaMappingAcceptance Unaccepted { get; } = new(null, null, null);
}

public sealed record EvaFieldProvenance(
    string Name,
    string Value,
    EvaEvidenceStatus Status,
    string Source,
    string SourceVersion);

public sealed record EvaBundleSource(
    EvaReplayFields Fields,
    IReadOnlyList<EvaFieldProvenance> Provenance,
    string MappingKey,
    int MappingVersion,
    string MappingAcceptanceEvidence);

/// <summary>
/// One operator export of a case: the bundle source, and the fields the case
/// simply does not hold, so the operator learns about a gap before the file
/// reaches EVA rather than after. <see cref="BlockingReasons"/> carries only
/// the activation gate — nothing about a case's own data blocks an export.
/// </summary>
public sealed record EvaOperatorExport(
    EvaBundleSource? Source,
    IReadOnlyList<string> UnrecordedFields,
    IReadOnlyList<string> BlockingReasons)
{
    public bool IsReady => Source is not null && BlockingReasons.Count == 0;
}

/// <summary>
/// Maps one case into the fixed thirteen-field EVA shape. Since ENG-016 there
/// is one mapping and one act: an operator export. A field the case does not
/// hold is emitted empty and named, never refused.
/// </summary>
public static partial class CaseEvaMapping
{
    /// <summary>
    /// The value the *case* stores for an image-based assessment, and the gate
    /// every resolution check compares against. It must stay byte-identical to
    /// <see cref="Address.Ext18InspectionAddressPolicy.ImageBasedAssessment"/>,
    /// which is what intake writes.
    /// </summary>
    public const string ImageBasedAssessment = "Image Based Assessment";

    /// <summary>
    /// What EVA is *sent* for the same thing — the original extractor's own
    /// literal, hyphenated and lower-case `b` (ENG-015). Deliberately not the
    /// same constant as <see cref="ImageBasedAssessment"/>: that one is a gate
    /// compared against stored case data, this one is an output value.
    /// </summary>
    public const string ImageBasedAssessmentExportValue = "Image-based Assessment";

    /// <summary>
    /// The inspection address is exported as exactly six lines — five body
    /// lines and a postcode — because the system EVA imports into requires
    /// that shape and rejects a bare string. The rule is unconditional: an
    /// address the case does not hold still exports as five newlines.
    /// </summary>
    private const int InspectionAddressLines = 6;

    public const string MappingKey = "qdos-eva-13-field-mapping";
    public const int MappingVersion = 1;
    public const string ActivationGateReason =
        "EVA hand-off is not switched on.";

    /// <summary>
    /// Named source for an inspection date the case did not carry, so the
    /// field's recorded provenance does not imply the instruction supplied it.
    /// Mirrors the existing "SystemDefault:Receipt date" treatment of an absent
    /// instruction date. It reaches no shipped file: since ENG-014 the archive
    /// carries the thirteen-key JSON and Images/ only, and provenance is an
    /// in-memory guard inside EvaBundleSchema.ValidateSource.
    /// </summary>
    public const string ExportDateSource = "SystemDefault:Export date";

    /// <summary>
    /// Whether the EVA field mapping is switched on at all: the
    /// operator-accepted mapping must be present and be exactly the mapping
    /// this code writes. The one owner of that question, and the only thing
    /// that can block an export. <see cref="ActivationGateReason"/> keeps its
    /// existing operator-facing wording: message text is a closed,
    /// operator-approved list, not this ticket's to reword.
    /// </summary>
    public static bool IsSwitchedOn(EvaMappingAcceptance acceptance)
    {
        ArgumentNullException.ThrowIfNull(acceptance);
        return string.Equals(acceptance.MappingKey, MappingKey, StringComparison.Ordinal)
            && acceptance.MappingVersion == MappingVersion
            && !string.IsNullOrWhiteSpace(acceptance.EvidenceReference);
    }

    /// <summary>
    /// Maps a case for the operator's export of it (CASE-019) — since ENG-016
    /// the only mapping, because there is only one act.
    ///
    /// It had a sibling, <c>MapForProduction</c>, which guarded EVA delivery
    /// and failed closed on anything short of accepted, provenanced evidence
    /// for all thirteen fields. Collapsing two acts into one means one bar, and
    /// the operator chose this one (2026-08-22): *"A blank field does not block
    /// the download."* The fail-closed guard was deleted, not merged.
    ///
    /// The rules, all of them:
    ///
    /// 1. Only an unaccepted mapping blocks. Nothing about the case does.
    /// 2. A missing inspection date becomes <paramref name="today"/>, per
    ///    operator direction (2026-08-22), recorded as a system default the
    ///    same way an absent instruction date already resolves to the receipt
    ///    date.
    /// 3. Any other absent field is emitted empty, keeps status
    ///    <see cref="EvaEvidenceStatus.Unrecorded"/>, and is named in
    ///    <see cref="EvaOperatorExport.UnrecordedFields"/> so the operator is
    ///    told before they download rather than after they import.
    ///
    /// A value the case holds only as a suggestion — a lookup-derived mileage,
    /// say — travels with its real <see cref="EvaEvidenceStatus.Suggested"/>
    /// status. The archive never claims something was accepted that was not.
    /// </summary>
    public static EvaOperatorExport MapForOperatorExport(
        EvaAcceptedCaseEvidence evidence,
        EvaMappingAcceptance acceptance,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(acceptance);
        if (!IsSwitchedOn(acceptance))
        {
            return new(null, [], [ActivationGateReason]);
        }

        var resolved = RequiredMappedFields(evidence)
            .Select(field => field.Name == "Inspection Date"
                    && NormalizeValue(field.Value.Value) is null
                ? (field.Name, Value: new EvaEvidenceValue(
                    today.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    EvaEvidenceStatus.Accepted,
                    ExportDateSource,
                    $"{MappingKey}/v{MappingVersion}"))
                : field)
            .ToArray();
        var provenance = resolved
            .Select(field => new EvaFieldProvenance(
                field.Name,
                NormalizedValue(field) ?? string.Empty,
                NormalizeValue(field.Value.Value) is null
                    ? EvaEvidenceStatus.Unrecorded
                    : field.Value.Status,
                Provenanced(field.Value.Source),
                Provenanced(field.Value.SourceVersion)))
            .ToArray();
        var unrecorded = provenance
            .Where(field => field.Status == EvaEvidenceStatus.Unrecorded)
            .Select(field => field.Name)
            .ToArray();

        return new(
            new(
                ToReplayFields(resolved),
                provenance,
                MappingKey,
                MappingVersion,
                acceptance.EvidenceReference!.Trim()),
            unrecorded,
            []);
    }

    /// <summary>
    /// The bundle requires every provenance entry to name a source and a
    /// version. A field the case never held has neither, and saying so is
    /// more honest than leaving it blank.
    /// </summary>
    private static string Provenanced(string? value) =>
        NormalizeValue(value) ?? "unrecorded";

    /// <summary>
    /// Normalizes explicitly supplied replay fields without inferring evidence or calling a provider.
    /// </summary>
    public static EvaReplayFields MapOfflineReplay(EvaReplayFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        return new(
            NormalizeValue(fields.WorkProvider),
            NormalizeRegistration(fields.Vrm),
            NormalizeValue(fields.VehicleModel),
            NormalizeValue(fields.ClaimantName),
            NormalizeValue(fields.Reference),
            NormalizeValue(fields.IncidentDate),
            NormalizeValue(fields.InstructionDate),
            NormalizeValue(fields.InspectionDate),
            NormalizeInspectionAddress(fields.InspectionAddress),
            NormalizeValue(fields.AccidentCircumstances),
            NormalizeValue(fields.VatStatus),
            NormalizeValue(fields.Mileage),
            NormalizeValue(fields.MileageUnit));
    }

    private static IEnumerable<(string Name, EvaEvidenceValue Value)> RequiredMappedFields(
        EvaAcceptedCaseEvidence evidence)
    {
        yield return ("Work Provider", evidence.WorkProvider);
        yield return ("VRM", evidence.VehicleRegistration);
        yield return ("Vehicle Model", evidence.VehicleModel);
        yield return ("Claimant Name", evidence.ClaimantName);
        yield return ("Reference", evidence.Reference);
        yield return ("Incident Date", evidence.IncidentDate);
        yield return ("Instruction Date", evidence.InstructionDate);
        yield return ("Inspection Date", evidence.InspectionDate);
        yield return ("Inspection Address", evidence.Inspection.Evidence);
        yield return ("Accident Circumstances", evidence.AccidentCircumstances);
        yield return ("VAT Status", evidence.VatStatus);
        yield return ("Mileage", evidence.Mileage);
        yield return ("Mileage Unit", evidence.MileageUnit);
    }

    /// <summary>
    /// The thirteen ordered field values as the replay record, in the one
    /// order <see cref="RequiredMappedFields"/> names. A value the case does
    /// not hold is empty rather than absent: every key is always present in
    /// the archive.
    /// </summary>
    private static EvaReplayFields ToReplayFields(
        IReadOnlyList<(string Name, EvaEvidenceValue Value)> fields)
    {
        var values = fields.ToDictionary(
            field => field.Name,
            NormalizedValue,
            StringComparer.Ordinal);
        return new(
            values["Work Provider"],
            values["VRM"],
            values["Vehicle Model"],
            values["Claimant Name"],
            values["Reference"],
            values["Incident Date"],
            values["Instruction Date"],
            values["Inspection Date"],
            values["Inspection Address"],
            values["Accident Circumstances"],
            values["VAT Status"],
            values["Mileage"],
            values["Mileage Unit"]);
    }

    /// <summary>
    /// One field's value, with the two fields that have their own shape
    /// handled: the VRM's spacing, and the inspection address's six lines.
    /// </summary>
    private static string? NormalizedValue((string Name, EvaEvidenceValue Value) field) =>
        field.Name switch
        {
            "VRM" => NormalizeRegistration(field.Value.Value),
            "Inspection Address" => NormalizeInspectionAddress(field.Value.Value),
            _ => NormalizeValue(field.Value.Value)
        };

    /// <summary>
    /// The inspection address in its six-line export shape: five body lines
    /// then the postcode, joined by five newlines, always.
    ///
    /// This field is exempt from <see cref="NormalizeValue"/>'s <c>Trim()</c>
    /// on purpose — the trailing blank lines are the payload, not padding, and
    /// trimming them is what made the export differ from the known-good sample
    /// by one line (ENG-015).
    ///
    /// Commas separate lines just as newlines do, because the case stores the
    /// address as a single collapsed line. Body content beyond five lines
    /// joins into line five rather than pushing the postcode out of line six.
    /// </summary>
    private static string NormalizeInspectionAddress(string? value)
    {
        var normalized = NormalizeValue(value);
        if (string.Equals(normalized, ImageBasedAssessment, StringComparison.Ordinal))
        {
            normalized = ImageBasedAssessmentExportValue;
        }

        var parts = (normalized ?? string.Empty)
            .Replace(',', '\n')
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        // The last part is the postcode only when it looks like one; an address
        // that does not end in a postcode leaves line six blank rather than
        // promoting its last body line into it.
        var hasPostcode = parts.Length > 1 && PostcodeRegex().IsMatch(parts[^1]);
        var body = hasPostcode ? parts[..^1] : parts;
        var postcode = hasPostcode ? parts[^1] : string.Empty;

        var lines = new string[InspectionAddressLines];
        Array.Fill(lines, string.Empty);
        var bodyLines = InspectionAddressLines - 1;
        for (var index = 0; index < body.Length; index++)
        {
            // Surplus body content joins the last body line with spaces.
            var target = Math.Min(index, bodyLines - 1);
            lines[target] = lines[target].Length == 0
                ? body[index]
                : $"{lines[target]} {body[index]}";
        }

        lines[^1] = postcode;
        return string.Join('\n', lines);
    }

    [GeneratedRegex(
        @"^[A-Za-z]{1,2}\d[A-Za-z\d]?\s*\d[A-Za-z]{2}$",
        RegexOptions.CultureInvariant,
        100)]
    private static partial Regex PostcodeRegex();

    private static string? NormalizeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
        return normalized.Length == 0 ? null : normalized;
    }

    private static string? NormalizeRegistration(string? value)
    {
        var normalized = NormalizeValue(value);
        return normalized is null
            ? null
            : string.Concat(normalized.Where(character => !char.IsWhiteSpace(character))).ToUpperInvariant();
    }
}
