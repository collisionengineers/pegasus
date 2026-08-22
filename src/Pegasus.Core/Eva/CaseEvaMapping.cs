using System.Globalization;

namespace Pegasus.Core.Eva;

public enum EvaEvidenceStatus
{
    Suggested,
    Accepted,
    Corrected,

    /// <summary>
    /// The case holds no value for this field at all. Only an operator export
    /// can carry one (CASE-019); a hand-off refuses long before here, because
    /// this is not an accepted status.
    /// </summary>
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
    public bool IsAccepted => Status is EvaEvidenceStatus.Accepted or EvaEvidenceStatus.Corrected;
}

public sealed record EvaAddressResolution(
    EvaInspectionMode Mode,
    EvaEvidenceValue Evidence)
{
    public bool IsResolved => Evidence.IsAccepted
        && (Mode == EvaInspectionMode.ImageBasedAssessment
            ? string.Equals(Evidence.Value, CaseEvaMapping.ImageBasedAssessment, StringComparison.Ordinal)
            : !string.IsNullOrWhiteSpace(Evidence.Value));
}

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

public sealed record EvaMappingResult(
    EvaBundleSource? Source,
    IReadOnlyList<string> BlockingReasons)
{
    public EvaReplayFields? Fields => Source?.Fields;

    public IReadOnlyList<EvaFieldProvenance> Provenance => Source?.Provenance ?? [];

    public bool IsReady => Source is not null && BlockingReasons.Count == 0;
}

/// <summary>
/// Maps only staff-accepted, source-versioned case evidence into the fixed EVA field shape.
/// Suggested extraction and unresolved address evidence fail closed.
/// </summary>
public static class CaseEvaMapping
{
    public const string ImageBasedAssessment = "Image Based Assessment";
    public const string MappingKey = "qdos-eva-13-field-mapping";
    public const int MappingVersion = 1;
    public const string ActivationGateReason =
        "EVA hand-off is not switched on.";

    /// <summary>
    /// Named source for an inspection date the case did not carry, so
    /// provenance.json says where the value came from rather than implying the
    /// instruction supplied it. Mirrors the existing "SystemDefault:Receipt
    /// date" treatment of an absent instruction date.
    /// </summary>
    public const string ExportDateSource = "SystemDefault:Export date";

    /// <summary>
    /// Whether the EVA hand-off is switched on at all: the operator-accepted
    /// mapping must be present and be exactly the mapping this code writes.
    /// The one owner of that question — the mapping enforces it, and the
    /// operator surface reads it to decide whether an EVA panel is
    /// meaningful to show (PLAT-031). Enforcement never depends on the
    /// display: an unaccepted mapping still fails closed here.
    /// </summary>
    public static bool IsSwitchedOn(EvaMappingAcceptance acceptance)
    {
        ArgumentNullException.ThrowIfNull(acceptance);
        return string.Equals(acceptance.MappingKey, MappingKey, StringComparison.Ordinal)
            && acceptance.MappingVersion == MappingVersion
            && !string.IsNullOrWhiteSpace(acceptance.EvidenceReference);
    }

    public static EvaMappingResult MapForProduction(
        EvaAcceptedCaseEvidence evidence,
        EvaMappingAcceptance acceptance)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(acceptance);

        var reasons = ValidateAcceptedEvidence(evidence).ToList();
        if (!IsSwitchedOn(acceptance))
        {
            reasons.Insert(0, ActivationGateReason);
        }

        var blockingReasons = reasons.ToArray();
        if (blockingReasons.Length != 0)
        {
            return new(null, blockingReasons);
        }

        var fields = RequiredMappedFields(evidence).ToArray();
        return new(new(
            ToReplayFields(fields),
            fields
                .Select(field => new EvaFieldProvenance(
                    field.Name,
                    NormalizedValue(field)!,
                    field.Value.Status,
                    field.Value.Source.Trim(),
                    field.Value.SourceVersion.Trim()))
                .ToArray(),
            MappingKey,
            MappingVersion,
            acceptance.EvidenceReference!.Trim()), []);
    }

    /// <summary>
    /// Maps a case for an operator's own export of it (CASE-019).
    ///
    /// This is not the hand-off. <see cref="MapForProduction"/> guards delivery
    /// to EVA and fails closed on anything short of accepted, provenanced
    /// evidence for all thirteen fields — that bar is unchanged and still the
    /// only thing a hand-off can pass. An operator downloading their own case
    /// is a different act: they are entitled to the file even when the case is
    /// still missing something, so a gap is reported rather than refused.
    ///
    /// Three differences, and no others. The field set, its order and its
    /// normalization are shared with the hand-off, so the archive an operator
    /// downloads is the same shape EVA would receive:
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
            NormalizeValue(fields.InspectionAddress),
            NormalizeValue(fields.AccidentCircumstances),
            NormalizeValue(fields.VatStatus),
            NormalizeValue(fields.Mileage),
            NormalizeValue(fields.MileageUnit));
    }

    private static IEnumerable<string> ValidateAcceptedEvidence(EvaAcceptedCaseEvidence evidence)
    {
        if (evidence.CaseId == Guid.Empty || !evidence.CaseAccepted)
        {
            yield return "The case has not been accepted.";
        }

        if (evidence.CaseVersion < 0)
        {
            yield return "The accepted case version is invalid.";
        }

        if (!evidence.InstructionComplete || !evidence.ImagesComplete)
        {
            yield return "Completeness has not been confirmed.";
        }

        if (!evidence.Inspection.IsResolved)
        {
            yield return "The inspection address or exact Image Based Assessment mode is unresolved.";
        }

        foreach (var field in RequiredMappedFields(evidence))
        {
            if (!field.Value.IsAccepted || string.IsNullOrWhiteSpace(field.Value.Value))
            {
                yield return $"{field.Name} does not have accepted evidence.";
                continue;
            }

            if (string.IsNullOrWhiteSpace(field.Value.Source)
                || string.IsNullOrWhiteSpace(field.Value.SourceVersion))
            {
                yield return $"{field.Name} accepted evidence lacks source/version provenance.";
            }
        }
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
    /// The thirteen ordered field values as the replay record, written once so
    /// the hand-off and an operator export can never drift into two orders.
    /// A value the case does not hold is empty rather than absent: every key
    /// is always present in the archive.
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

    /// <summary>One field's value, with the VRM's own normalization applied.</summary>
    private static string? NormalizedValue((string Name, EvaEvidenceValue Value) field) =>
        field.Name == "VRM"
            ? NormalizeRegistration(field.Value.Value)
            : NormalizeValue(field.Value.Value);

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
