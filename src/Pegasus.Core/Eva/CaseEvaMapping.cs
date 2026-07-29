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
    string Reference,
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

public sealed record EvaMappingResult(
    EvaReplayFields? Fields,
    IReadOnlyList<string> BlockingReasons)
{
    public bool IsReady => Fields is not null && BlockingReasons.Count == 0;
}

/// <summary>
/// Owns the fail-closed boundary between accepted Pegasus evidence and the externally
/// observed EVA field shape. The observed key order is stable, but its production source
/// mapping remains disabled until genuine-case mapping and drag/drop evidence are accepted.
/// </summary>
public static class CaseEvaMapping
{
    public const string ImageBasedAssessment = "Image Based Assessment";
    public const string ActivationGateCode = "eva-source-mapping-not-accepted";
    public const string ActivationGateReason =
        "The exact EVA source mapping and an approved genuine drag-and-drop result have not been accepted.";

    public static EvaMappingResult MapForProduction(EvaAcceptedCaseEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var reasons = ValidateAcceptedEvidence(evidence).ToList();
        reasons.Insert(0, ActivationGateReason);
        return new(null, reasons);
    }

    /// <summary>
    /// Normalizes explicitly supplied replay fields without claiming that any Pegasus field
    /// is the approved production source. This is the deterministic offline evidence seam.
    /// </summary>
    public static EvaReplayFields MapOfflineReplay(EvaReplayFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        return new(
            Normalize(fields.WorkProvider),
            NormalizeRegistration(fields.Vrm),
            Normalize(fields.VehicleModel),
            Normalize(fields.ClaimantName),
            Normalize(fields.Reference),
            Normalize(fields.IncidentDate),
            Normalize(fields.InstructionDate),
            Normalize(fields.InspectionDate),
            Normalize(fields.InspectionAddress),
            Normalize(fields.AccidentCircumstances),
            Normalize(fields.VatStatus),
            Normalize(fields.Mileage),
            Normalize(fields.MileageUnit));
    }

    private static IEnumerable<string> ValidateAcceptedEvidence(EvaAcceptedCaseEvidence evidence)
    {
        if (evidence.CaseId == Guid.Empty || !evidence.CaseAccepted)
        {
            yield return "The case has not been accepted.";
        }

        if (!evidence.InstructionComplete || !evidence.ImagesComplete)
        {
            yield return "Instruction and image completeness must both be confirmed.";
        }

        if (!evidence.Inspection.IsResolved)
        {
            yield return "The inspection address or exact Image Based Assessment mode is unresolved.";
        }

        foreach (var field in RequiredAcceptedFields(evidence))
        {
            if (!field.Value.IsAccepted || string.IsNullOrWhiteSpace(field.Value.Value))
            {
                yield return $"{field.Name} does not have accepted evidence.";
            }
        }
    }

    private static IEnumerable<(string Name, EvaEvidenceValue Value)> RequiredAcceptedFields(
        EvaAcceptedCaseEvidence evidence)
    {
        yield return ("Work Provider", evidence.WorkProvider);
        yield return ("VRM", evidence.VehicleRegistration);
        yield return ("Vehicle Model", evidence.VehicleModel);
        yield return ("Claimant Name", evidence.ClaimantName);
        yield return ("Reference", new(evidence.Reference, EvaEvidenceStatus.Accepted, "case", evidence.CaseVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        yield return ("Incident Date", evidence.IncidentDate);
        yield return ("Instruction Date", evidence.InstructionDate);
        yield return ("Accident Circumstances", evidence.AccidentCircumstances);
        yield return ("VAT Status", evidence.VatStatus);
        yield return ("Mileage", evidence.Mileage);
        yield return ("Mileage Unit", evidence.MileageUnit);
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string? NormalizeRegistration(string? value)
    {
        var normalized = Normalize(value);
        return normalized?.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
    }
}
