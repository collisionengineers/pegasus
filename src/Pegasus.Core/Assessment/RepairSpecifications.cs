using Pegasus.Core.Identity;

namespace Pegasus.Core.Assessment;

public enum RepairSpecificationState
{
    Draft,
    Accepted,
    Superseded,
    Discarded,
}

public enum RepairSpecificationSourceRoute
{
    LegacyUnresolved,
    Manual,
    Glasses,
    AudatexPdf,
    ApprovedAiProposal,
    Json,
    AiDraft,
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

/// <summary>
/// What an accepted estimate was costed at. The four component amounts, the
/// VAT and the total are the printed figures (B04): each component rounded
/// to pence away from zero, the net their sum, the total the net plus the
/// printed VAT. <see cref="Printed"/> carries the same figures with panel
/// labour, paint labour and materials still separate, and
/// <see cref="VatPolicy"/> the categories the VAT was charged on.
/// </summary>
public sealed record RepairCalculationBasis(
    decimal Labour,
    decimal Parts,
    decimal PaintMaterials,
    decimal SpecialistOther,
    bool RepairerVatRegistered,
    decimal Vat,
    decimal Total,
    string PolicyVersion,
    EstimateVatPolicy? VatPolicy = null,
    EstimatePrintedTotals? Printed = null);

public sealed record RepairSpecificationVersion(
    Guid SpecificationId,
    Guid CaseId,
    int Version,
    RepairSpecificationState State,
    RepairSpecificationSource Source,
    IReadOnlyList<CaseEstimateLineRecord> Lines,
    RepairCalculationBasis? CalculationBasis,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    string? AcceptedBy,
    DateTimeOffset? AcceptedAtUtc,
    Guid? SupersedesSpecificationId,
    string? SupersessionReason,
    EstimateDetails Details,
    bool IsCurrent = false,
    Guid? AiJobId = null,
    string? DiscardReason = null);

public sealed record RepairSpecificationDisplayLists(
    IReadOnlyList<string> NewParts,
    IReadOnlyList<string> Repairs,
    IReadOnlyList<string> AdditionalOperations);

public static class RepairSpecificationPolicy
{
    public const string PolicyKey = "repair-specification";

    /// <summary>
    /// v2: the calculation basis of an estimate made Current is derived by
    /// <see cref="EstimateTotals"/> (FRD-11 § Estimate VAT on the rendered
    /// report) instead of being typed from the source document.
    /// v3 (B04): the basis is the printed projection — each discounted
    /// category and the VAT rounded to pence independently, net the sum of
    /// the printed components — over the seven closed line operations, the
    /// four discounts and the repairer's VAT categories.
    /// </summary>
    public const int PolicyVersion = 3;

    public static void RequireEngineer(ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Kind != ActorKind.Staff || !actor.IsInRole(StaffRole.Engineer))
        {
            throw new InvalidOperationException(
                "Only an authenticated staff Engineer can change or accept a repair specification.");
        }
    }

    /// <summary>
    /// Routes that stand on a retained document; every other route (Manual,
    /// AiDraft) is typed into the estimate editor and carries no artifact.
    /// </summary>
    public static bool IsDocumentRoute(RepairSpecificationSourceRoute route) => route
        is RepairSpecificationSourceRoute.Glasses
        or RepairSpecificationSourceRoute.AudatexPdf
        or RepairSpecificationSourceRoute.ApprovedAiProposal
        or RepairSpecificationSourceRoute.Json;

    public static RepairSpecificationSource ValidateSource(RepairSpecificationSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!Enum.IsDefined(source.Route))
        {
            throw new InvalidOperationException("The repair-specification source names no known route.");
        }
        if (source.Route == RepairSpecificationSourceRoute.LegacyUnresolved)
        {
            throw new InvalidOperationException(
                "Legacy repair lines require authoritative source review before acceptance.");
        }
        if (!IsDocumentRoute(source.Route))
        {
            return source with
            {
                ArtifactReference = Trimmed(source.ArtifactReference),
                SourceVersion = Trimmed(source.SourceVersion),
                Sha256 = source.Sha256?.ToLowerInvariant(),
            };
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
        var printedNet = basis.Labour + basis.Parts + basis.PaintMaterials + basis.SpecialistOther;
        if (basis.Total != printedNet + basis.Vat)
        {
            throw new InvalidOperationException(
                "Repair calculation total does not match its printed components and recorded VAT.");
        }
        if (basis.Printed is { } printed)
        {
            var components = printed.Parts + printed.PanelLabour + printed.PaintLabour
                + printed.Materials + printed.Specialist;
            if (printed.Net != components
                || printed.Gross != printed.Net + printed.Vat
                || printed.Net != printedNet
                || printed.Vat != basis.Vat
                || printed.Gross != basis.Total)
            {
                throw new InvalidOperationException(
                    "Printed repair calculation totals must be the sum of their printed components.");
            }
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
        RequireEngineer(actor);
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

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record StartRepairSpecificationDraftRequest(
    Guid CaseId,
    long ExpectedCaseVersion,
    RepairSpecificationSource Source,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    Guid? SupersedesSpecificationId = null,
    IReadOnlyList<EstimateLineInput>? Lines = null,
    string? Name = null);

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
        CancellationToken cancellationToken);

    Task<RepairSpecificationVersion?> GetCurrentDraftAsync(
        Guid caseId,
        CancellationToken cancellationToken);

    // Named estimates (ENG-026). Validation lives in EstimatePolicy and the
    // use cases in Estimates.cs; the store owns the transaction, the
    // replay-by-operation-key, and the one-Current-per-case invariant.
    Task<RepairSpecificationVersion> SaveEstimateAsync(
        SaveEstimateRequest request,
        CancellationToken cancellationToken);

    Task<RepairSpecificationVersion> DuplicateEstimateAsync(
        DuplicateEstimateRequest request,
        CancellationToken cancellationToken);

    Task<RepairSpecificationVersion> DiscardEstimateAsync(
        DiscardEstimateRequest request,
        CancellationToken cancellationToken);

    Task<RepairSpecificationVersion> SetCurrentEstimateAsync(
        SetCurrentEstimateRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RepairSpecificationVersion>> ListEstimatesAsync(
        Guid caseId,
        CancellationToken cancellationToken);

    /// <summary>
    /// The keyset-paged sibling of <see cref="ListEstimatesAsync"/>
    /// (CASE-047): newest version first, then estimate id. The after-values
    /// are the decoded cursor's sort position, both null on the first page;
    /// <paramref name="fetchCount"/> is the caller's limit plus one. Returns
    /// the bounded <see cref="CaseEstimatePageItem"/> header projection
    /// (Stream A review) rather than the full <see
    /// cref="RepairSpecificationVersion"/> — the page never needs, and never
    /// pays to read, a specification's lines.
    /// </summary>
    Task<IReadOnlyList<CaseEstimatePageItem>> ListByCursorAsync(
        Guid caseId,
        int? afterVersion,
        Guid? afterId,
        int fetchCount,
        CancellationToken cancellationToken);
}
