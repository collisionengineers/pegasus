using System.Globalization;
using System.Text.RegularExpressions;

namespace Pegasus.Core.CaseExport;

public enum CaseExportEvidenceStatus
{
    Suggested,
    Accepted,

    /// <summary>The case holds no value for this field at all.</summary>
    Unrecorded
}

public enum CaseExportInspectionMode
{
    PhysicalAddress,
    ImageBasedAssessment
}

public sealed record CaseExportEvidenceValue(
    string? Value,
    CaseExportEvidenceStatus Status,
    string Source,
    string SourceVersion)
{
    public bool IsAccepted => Status is CaseExportEvidenceStatus.Accepted;
}

public sealed record CaseExportAddressResolution(
    CaseExportInspectionMode Mode,
    CaseExportEvidenceValue Evidence);

public sealed record CaseExportEvidence(
    Guid CaseId,
    long CaseVersion,
    bool CaseAccepted,
    bool InstructionComplete,
    bool ImagesComplete,
    CaseExportEvidenceValue Reference,
    CaseExportEvidenceValue WorkPrincipal,
    CaseExportEvidenceValue VehicleRegistration,
    CaseExportEvidenceValue VehicleModel,
    CaseExportEvidenceValue ClaimantName,
    CaseExportEvidenceValue IncidentDate,
    DateOnly ReceivedDate,
    CaseExportEvidenceValue InspectionDate,
    CaseExportAddressResolution Inspection,
    CaseExportEvidenceValue AccidentCircumstances,
    CaseExportEvidenceValue VatStatus,
    CaseExportEvidenceValue Mileage,
    CaseExportEvidenceValue MileageUnit);

public sealed record CaseExportFieldProvenance(
    string Name,
    string Value,
    CaseExportEvidenceStatus Status,
    string Source,
    string SourceVersion);

public sealed record CaseExportSource(
    CaseExportFields Fields,
    IReadOnlyList<CaseExportFieldProvenance> Provenance,
    string MappingKey,
    int MappingVersion);

/// <summary>
/// One operator export of a case: the bundle source, and the fields the case
/// simply does not hold, so the operator learns about a gap when they
/// download rather than later.
/// </summary>
public sealed record CaseOperatorExport(
    CaseExportSource Source,
    IReadOnlyList<string> UnrecordedFields);

/// <summary>
/// Maps one case into the fixed thirteen-field export shape. There is one
/// mapping and one act: an operator export. A field the case does not hold is
/// emitted empty and named, never refused.
/// </summary>
public static partial class CaseExportMapping
{
    /// <summary>
    /// The value the *case* stores for an image-based assessment, and the gate
    /// every resolution check compares against. It must stay byte-identical to
    /// <see cref="Address.Ext18InspectionAddressPolicy.ImageBasedAssessment"/>,
    /// which is what intake writes.
    /// </summary>
    public const string ImageBasedAssessment = "Image Based Assessment";

    /// <summary>
    /// What the export *writes* for the same thing — the original extractor's own
    /// literal, hyphenated and lower-case `b`. Deliberately not the
    /// same constant as <see cref="ImageBasedAssessment"/>: that one is a gate
    /// compared against stored case data, this one is an output value.
    /// </summary>
    public const string ImageBasedAssessmentExportValue = "Image-based Assessment";

    /// <summary>
    /// The inspection address is exported as exactly six lines — five body
    /// lines and a postcode — because the importing system requires that
    /// shape and rejects a bare string. The rule is unconditional: an
    /// address the case does not hold still exports as five newlines.
    /// </summary>
    private const int InspectionAddressLines = 6;

    public const string MappingKey = "case-export-13-field-mapping";
    public const int MappingVersion = 3;

    /// <summary>
    /// Named source for an inspection date the case did not carry, so the
    /// field's recorded provenance does not imply the instruction supplied it.
    /// It reaches no shipped file: the archive carries the thirteen-key JSON
    /// and Images/ only, and provenance is an in-memory guard inside
    /// CaseExportArchive.ValidateSource.
    /// </summary>
    public const string ExportDateSource = "SystemDefault:Export date";

    /// <summary>
    /// Named source for the Instruction Date field: the Case's Received date, which
    /// is its instruction date (operator, 24 September 2026). Every Case has one,
    /// so the field is always accepted and never unrecorded.
    /// </summary>
    public const string ReceivedDateSource = "Case:Received date";

    /// <summary>
    /// Maps a case for the operator's export of it — the only mapping, because
    /// there is only one act. The operator chose its bar (2026-08-22): *"A
    /// blank field does not block the download."*
    ///
    /// The rules, all of them:
    ///
    /// 1. Instruction Date is the Case's Received date (operator, 24 September
    ///    2026), accepted under <see cref="ReceivedDateSource"/>; every Case has
    ///    one, so it is never unrecorded.
    /// 2. A missing inspection date becomes <paramref name="today"/>, per
    ///    operator direction (2026-08-22), recorded as a named system default
    ///    (<see cref="ExportDateSource"/>).
    /// 3. Any other absent field is emitted empty, keeps status
    ///    <see cref="CaseExportEvidenceStatus.Unrecorded"/>, and is named in
    ///    <see cref="CaseOperatorExport.UnrecordedFields"/> so the operator is
    ///    told before they download rather than after they import.
    ///
    /// A value the case holds only as a suggestion — a lookup-derived mileage,
    /// say — travels with its real <see cref="CaseExportEvidenceStatus.Suggested"/>
    /// status. The archive never claims something was accepted that was not.
    /// </summary>
    public static CaseOperatorExport MapForOperatorExport(
        CaseExportEvidence evidence,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var resolved = RequiredMappedFields(evidence)
            .Select(field => field.Name == "Inspection Date"
                    && NormalizeValue(field.Value.Value) is null
                ? (field.Name, Value: new CaseExportEvidenceValue(
                    today.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    CaseExportEvidenceStatus.Accepted,
                    ExportDateSource,
                    $"{MappingKey}/v{MappingVersion}"))
                : field)
            .ToArray();
        var provenance = resolved
            .Select(field => new CaseExportFieldProvenance(
                field.Name,
                NormalizedValue(field) ?? string.Empty,
                NormalizeValue(field.Value.Value) is null
                    ? CaseExportEvidenceStatus.Unrecorded
                    : field.Value.Status,
                Provenanced(field.Value.Source),
                Provenanced(field.Value.SourceVersion)))
            .ToArray();
        var unrecorded = provenance
            .Where(field => field.Status == CaseExportEvidenceStatus.Unrecorded)
            .Select(field => field.Name)
            .ToArray();

        return new(
            new(
                ToReplayFields(resolved),
                provenance,
                MappingKey,
                MappingVersion),
            unrecorded);
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
    public static CaseExportFields NormalizeFields(CaseExportFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        return new(
            NormalizeValue(fields.WorkPrincipal),
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

    private static IEnumerable<(string Name, CaseExportEvidenceValue Value)> RequiredMappedFields(
        CaseExportEvidence evidence)
    {
        yield return ("Work Provider", evidence.WorkPrincipal);
        yield return ("VRM", evidence.VehicleRegistration);
        yield return ("Vehicle Model", evidence.VehicleModel);
        yield return ("Claimant Name", evidence.ClaimantName);
        yield return ("Reference", evidence.Reference);
        yield return ("Incident Date", evidence.IncidentDate);
        yield return ("Instruction Date", new CaseExportEvidenceValue(
            evidence.ReceivedDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            CaseExportEvidenceStatus.Accepted,
            ReceivedDateSource,
            $"{MappingKey}/v{MappingVersion}"));
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
    private static CaseExportFields ToReplayFields(
        IReadOnlyList<(string Name, CaseExportEvidenceValue Value)> fields)
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
    private static string? NormalizedValue((string Name, CaseExportEvidenceValue Value) field) =>
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
    /// by one line.
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
