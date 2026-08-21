namespace Pegasus.Core.Eva;

public enum EvaEvidenceStatus
{
    Suggested,
    Accepted,
    Corrected
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
        var values = fields.ToDictionary(
            field => field.Name,
            field => NormalizeValue(field.Value.Value)!,
            StringComparer.Ordinal);
        var mapped = new EvaReplayFields(
            values["Work Provider"],
            NormalizeRegistration(values["VRM"]),
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
        var provenance = fields
            .Select(field => new EvaFieldProvenance(
                field.Name,
                field.Name == "VRM"
                    ? NormalizeRegistration(field.Value.Value)!
                    : NormalizeValue(field.Value.Value)!,
                field.Value.Status,
                field.Value.Source.Trim(),
                field.Value.SourceVersion.Trim()))
            .ToArray();

        return new(new(
            mapped,
            provenance,
            MappingKey,
            MappingVersion,
            acceptance.EvidenceReference!.Trim()), []);
    }

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
