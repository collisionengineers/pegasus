using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Assessment;

/// <summary>
/// A specification is live (Draft) or discarded. Which live one the Case uses
/// is <see cref="RepairSpecificationVersion.IsCurrent"/>: a spec a staff member
/// creates is Current at once, and a Current spec stays editable while the
/// Case is writable. The numbers are pinned because report snapshots store
/// them.
/// </summary>
public enum RepairSpecificationState
{
    Draft = 0,
    Discarded = 3,
}

/// <summary>Where a specification's figures came from; the numbers are pinned as above.</summary>
public enum RepairSpecificationSourceRoute
{
    Manual = 1,
    Glasses = 2,
    AudatexPdf = 3,
    Json = 5,
    AiDraft = 6,
}

public sealed record RepairSpecificationSource(
    RepairSpecificationSourceRoute Route,
    string? ArtifactReference,
    string? SourceVersion,
    string? Sha256);

public sealed record RepairSpecificationVersion(
    Guid SpecificationId,
    Guid CaseId,
    int Version,
    RepairSpecificationState State,
    RepairSpecificationSource Source,
    IReadOnlyList<CaseEstimateLineRecord> Lines,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    EstimateDetails Details,
    bool IsCurrent = false,
    Guid? AiJobId = null,
    string? DiscardReason = null,
    RepairSpecificationSupplementary? Supplementary = null);

public static class RepairSpecificationPolicy
{
    public const string PolicyKey = "repair-specification";

    /// <summary>
    /// The calculation <see cref="EstimateTotals"/> stamps. v3 (B04): the
    /// printed projection — each discounted category and the VAT rounded to
    /// pence independently, net the sum of the printed components — over the
    /// seven closed line operations, the four discounts and the repairer's VAT
    /// categories. v4: Specialist work-unit hours are priced as panel labour;
    /// fixed-price Specialist hours remain recorded but are not priced.
    /// </summary>
    public const int PolicyVersion = 4;

    public static void RequireStaffAuthor(ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Kind != ActorKind.Staff)
        {
            throw new InvalidOperationException(
                "Only authenticated staff can change a repair specification.");
        }
    }

    /// <summary>
    /// A specification a staff member creates — typed in, imported or returned
    /// from Glass's — is the one the Case uses straight away (operator,
    /// 25 September 2026). An Automation (AI) draft only proposes, so it waits
    /// for Use repair spec.
    /// </summary>
    public static bool BecomesCurrentWhenCreated(ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return actor.Kind == ActorKind.Staff;
    }

    /// <summary>
    /// Routes that stand on a retained document; every other route (Manual,
    /// AiDraft) is typed into the estimate editor and carries no artifact.
    /// </summary>
    public static bool IsDocumentRoute(RepairSpecificationSourceRoute route) => route
        is RepairSpecificationSourceRoute.Glasses
        or RepairSpecificationSourceRoute.AudatexPdf
        or RepairSpecificationSourceRoute.Json;

    public static RepairSpecificationSource ValidateSource(RepairSpecificationSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!Enum.IsDefined(source.Route))
        {
            throw new InvalidOperationException("The repair-specification source names no known route.");
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

public interface IRepairSpecificationStore
{
    /// <summary>Checks persisted current edit authority without consuming it.</summary>
    Task RequireImportAuthorityAsync(ImportRawEstimateRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Checks the durable source-hash replay binding for an import operation
    /// without changing Case state. A result means the caller can return the
    /// already-imported estimate without retaining or opening another source.
    /// </summary>
    Task<EstimateImportResult?> ProbeSourceHashReplayAsync(
        Guid caseId,
        string operationKey,
        string sourceSha256,
        CancellationToken cancellationToken);

    /// <summary>
    /// Atomically binds a source-hash replay operation key to its existing
    /// estimate result. This is a no-op for Case version and edit-lease state.
    /// </summary>
    Task<EstimateImportResult> BindSourceHashReplayAsync(
        Guid caseId,
        string operationKey,
        string sourceSha256,
        Guid estimateId,
        ActionActor actor,
        CancellationToken cancellationToken);

    /// <summary>Only the canonical retained-document importer supplies these validated source-backed rows.</summary>
    Task<RepairSpecificationVersion> SaveImportedEstimateAsync(
        SaveEstimateRequest request, CancellationToken cancellationToken);

    Task<RepairSpecificationVersion?> GetVersionAsync(
        Guid caseId,
        Guid specificationId,
        CancellationToken cancellationToken);

    Task<RepairSpecificationVersion?> GetCurrentAsync(
        Guid caseId,
        CancellationToken cancellationToken);

    // Named estimates. Validation lives in EstimatePolicy and the
    // use cases in Estimates.cs; the store owns the transaction, the
    // replay-by-operation-key, and the one-Current-per-case invariant.
    Task<RepairSpecificationVersion> SaveEstimateAsync(
        SaveEstimateRequest request,
        CancellationToken cancellationToken);

    Task<RepairSpecificationVersion> ScaleAsync(
        ScaleRepairSpecificationRequest request,
        CancellationToken cancellationToken);

    Task<RepairSpecificationVersion> RemoveScalingAsync(
        RemoveRepairSpecificationScalingRequest request,
        CancellationToken cancellationToken);

    Task<RepairSpecificationVersion> RestoreSnapshotAsync(
        RestoreRepairSpecificationSnapshotRequest request,
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
        CaseWorkSelector work,
        CancellationToken cancellationToken);

    /// <summary>
    /// The keyset-paged sibling of <see cref="ListEstimatesAsync"/>:
    /// newest version first, then estimate id. The after-values
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
