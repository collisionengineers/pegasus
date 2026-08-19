using Pegasus.Core.Identity;

namespace Pegasus.Core.Assessment;

public enum RepairSpecificationPurpose
{
    OrdinaryAssessment,
    Audit,
}

public enum RepairSpecificationRole
{
    Ordinary,
    Conservative,
    Maximised,
}

public enum RepairSpecificationState
{
    Draft,
    Accepted,
    Superseded,
}

public enum RepairSpecificationSourceRoute
{
    LegacyUnresolved,
    Manual,
    Glasses,
    AudatexPdf,
    ApprovedAiProposal,
}

public enum RepairSpecificationDisplaySection
{
    NewParts,
    Repairs,
    AdditionalOperations,
}

public sealed record RepairSpecificationSource(
    RepairSpecificationSourceRoute Route,
    string? ArtifactReference,
    string? SourceVersion,
    string? Sha256);

public sealed record RepairCalculationBasis(
    decimal Labour,
    decimal Parts,
    decimal PaintMaterials,
    decimal SpecialistOther,
    bool RepairerVatRegistered,
    decimal Vat,
    decimal Total,
    string PolicyVersion);

public sealed record RepairSpecificationVersion(
    Guid SpecificationId,
    Guid CaseId,
    int Version,
    RepairSpecificationPurpose Purpose,
    RepairSpecificationRole Role,
    RepairSpecificationState State,
    RepairSpecificationSource Source,
    IReadOnlyList<CaseEstimateLineRecord> Lines,
    RepairCalculationBasis? CalculationBasis,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    string? AcceptedBy,
    DateTimeOffset? AcceptedAtUtc,
    Guid? SupersedesSpecificationId,
    string? SupersessionReason);

public sealed record RepairSpecificationDisplayLists(
    IReadOnlyList<string> NewParts,
    IReadOnlyList<string> Repairs,
    IReadOnlyList<string> AdditionalOperations);

public static class RepairSpecificationPolicy
{
    public const string PolicyKey = "repair-specification";
    public const int PolicyVersion = 1;

    public static void ValidatePurposeAndRole(
        RepairSpecificationPurpose purpose,
        RepairSpecificationRole role)
    {
        var valid = purpose switch
        {
            RepairSpecificationPurpose.OrdinaryAssessment => role == RepairSpecificationRole.Ordinary,
            RepairSpecificationPurpose.Audit => role is RepairSpecificationRole.Conservative
                or RepairSpecificationRole.Maximised,
            _ => false,
        };
        if (!valid)
        {
            throw new InvalidOperationException(
                $"Role '{role}' is not valid for repair-specification purpose '{purpose}'.");
        }
    }

    public static RepairSpecificationSource ValidateSource(RepairSpecificationSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Route == RepairSpecificationSourceRoute.LegacyUnresolved)
        {
            throw new InvalidOperationException(
                "Legacy repair lines require authoritative source review before acceptance.");
        }
        Required(source.ArtifactReference, nameof(source.ArtifactReference));
        Required(source.SourceVersion, nameof(source.SourceVersion));
        if (source.Sha256 is null || source.Sha256.Length != 64 || !source.Sha256.All(Uri.IsHexDigit))
        {
            throw new InvalidOperationException("Repair-specification source evidence requires a SHA-256 hash.");
        }
        return source with
        {
            ArtifactReference = source.ArtifactReference!.Trim(),
            SourceVersion = source.SourceVersion!.Trim(),
            Sha256 = source.Sha256!.ToLowerInvariant(),
        };
    }

    public static RepairCalculationBasis ValidateCalculationBasis(RepairCalculationBasis basis)
    {
        ArgumentNullException.ThrowIfNull(basis);
        if (basis.Labour < 0 || basis.Parts < 0 || basis.PaintMaterials < 0
            || basis.SpecialistOther < 0 || basis.Vat < 0 || basis.Total < 0)
        {
            throw new InvalidOperationException("Repair calculation inputs and totals cannot be negative.");
        }
        var subtotal = basis.Labour + basis.Parts + basis.PaintMaterials + basis.SpecialistOther;
        if (basis.Total != subtotal + basis.Vat)
        {
            throw new InvalidOperationException(
                "Repair calculation total does not match its accepted raw inputs and recorded VAT.");
        }
        Required(basis.PolicyVersion, nameof(basis.PolicyVersion));
        return basis;
    }

    public static void ValidateAcceptance(
        RepairSpecificationVersion specification,
        ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentNullException.ThrowIfNull(actor);
        ValidatePurposeAndRole(specification.Purpose, specification.Role);
        if (actor.Kind != ActorKind.Staff || !actor.IsInRole(StaffRole.Engineer))
        {
            throw new InvalidOperationException(
                "Only an authenticated staff Engineer can accept a repair specification.");
        }
        if (specification.State != RepairSpecificationState.Draft)
        {
            throw new InvalidOperationException("Only a draft repair specification can be accepted.");
        }
        if (specification.Lines.Count == 0 || specification.Lines.Any(line => !line.IsConfirmed))
        {
            throw new InvalidOperationException(
                "Every accepted repair specification requires confirmed ordered lines.");
        }
        _ = ValidateSource(specification.Source);
        if (specification.CalculationBasis is null)
        {
            throw new InvalidOperationException("An accepted repair specification requires its calculation basis.");
        }
        _ = ValidateCalculationBasis(specification.CalculationBasis);
    }

    public static RepairSpecificationDisplayLists ToDisplayLists(RepairSpecificationVersion specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        if (specification.State != RepairSpecificationState.Accepted)
        {
            throw new InvalidOperationException("Only an accepted repair specification can feed report lists.");
        }
        var ordered = specification.Lines.OrderBy(line => line.Position).ToArray();
        return new(
            Names(ordered, RepairSpecificationDisplaySection.NewParts),
            Names(ordered, RepairSpecificationDisplaySection.Repairs),
            Names(ordered, RepairSpecificationDisplaySection.AdditionalOperations));
    }

    public static RepairSpecificationDisplaySection DisplaySection(string lineType) => lineType switch
    {
        "new_part" => RepairSpecificationDisplaySection.NewParts,
        "rnr" or "repair" => RepairSpecificationDisplaySection.Repairs,
        "check_labour" or "paint_new" or "paint_repair" or "paint_blend" or "paint_prep"
            or "specialist_fixed" or "specialist_wu" => RepairSpecificationDisplaySection.AdditionalOperations,
        _ => throw new InvalidOperationException($"Unknown estimate line type '{lineType}'."),
    };

    private static string[] Names(
        IReadOnlyList<CaseEstimateLineRecord> lines,
        RepairSpecificationDisplaySection section) => lines
        .Where(line => DisplaySection(line.Type) == section)
        .Select(line => !string.IsNullOrWhiteSpace(line.Description)
            ? line.Description!
            : !string.IsNullOrWhiteSpace(line.GuideCode)
                ? line.GuideCode!
                : throw new InvalidOperationException(
                    $"Estimate line {line.Position} requires a description or guide code for report display."))
        .ToArray();

    private static void Required(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{name} is required.");
        }
    }
}

public sealed record StartRepairSpecificationDraftRequest(
    Guid CaseId,
    long ExpectedCaseVersion,
    RepairSpecificationPurpose Purpose,
    RepairSpecificationRole Role,
    RepairSpecificationSource Source,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    Guid? SupersedesSpecificationId = null,
    IReadOnlyList<EstimateLineInput>? Lines = null);

public sealed record AcceptRepairSpecificationRequest(
    Guid CaseId,
    long ExpectedCaseVersion,
    Guid SpecificationId,
    int ExpectedSpecificationVersion,
    RepairSpecificationSource Source,
    RepairCalculationBasis CalculationBasis,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken);

public interface IRepairSpecificationStore
{
    Task<RepairSpecificationVersion> StartDraftAsync(
        StartRepairSpecificationDraftRequest request,
        CancellationToken cancellationToken);

    Task<RepairSpecificationVersion> AcceptAsync(
        AcceptRepairSpecificationRequest request,
        CancellationToken cancellationToken);

    Task<RepairSpecificationVersion?> GetVersionAsync(
        Guid caseId,
        Guid specificationId,
        CancellationToken cancellationToken);

    Task<RepairSpecificationVersion?> GetCurrentAcceptedAsync(
        Guid caseId,
        RepairSpecificationPurpose purpose,
        RepairSpecificationRole role,
        CancellationToken cancellationToken);
}
