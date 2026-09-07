using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Documents;

/// <summary>
/// How a case document occurrence is used on the generated report: at most
/// one Close-up and one Overview, any number of ordered Supporting images, or
/// excluded entirely. Distinct from <see cref="DocumentSemanticRole"/>, which
/// intake, EVA eligibility and the third-party vehicle guard read — this is a
/// report-authoring choice layered on top of an occurrence, not a change to
/// what the occurrence intrinsically is.
/// </summary>
public enum CaseAssetReportRole
{
    NotUsed,
    CloseUp,
    Overview,
    Supporting
}

/// <summary>
/// The whole-turn clockwise rotation applied to the confirmed source image
/// before crop fractions are interpreted. The values are the exact degrees
/// the <c>DocumentOccurrenceEntity.RotationDegrees</c> database check
/// constrains the column to.
/// </summary>
public enum CaseAssetRotation
{
    None = 0,
    Clockwise90 = 90,
    Half = 180,
    Clockwise270 = 270
}

/// <summary>
/// A crop rectangle expressed as fractions of the <em>rotated</em> source
/// image. Rotation is applied first and the crop is never re-expressed when
/// rotation later changes — a saved crop is always relative to whatever the
/// current <see cref="CaseAssetRotation"/> already produced. Each fraction is
/// bounded to 7 decimal places, matching the <c>decimal(8,7)</c> database
/// columns.
/// </summary>
public sealed record CaseAssetCrop(decimal Left, decimal Top, decimal Width, decimal Height)
{
    /// <summary>The whole rotated source, with no crop applied.</summary>
    public static readonly CaseAssetCrop Full = new(0m, 0m, 1m, 1m);

    /// <summary>Whether this crop selects the entire rotated source.</summary>
    public bool IsFull => this == Full;

    /// <summary>
    /// Fails closed on an out-of-range, over-precise, or degenerate crop.
    /// </summary>
    public void Validate()
    {
        RequireScale(Left, nameof(Left));
        RequireScale(Top, nameof(Top));
        RequireScale(Width, nameof(Width));
        RequireScale(Height, nameof(Height));
        if (Left < 0m || Left > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(Left), Left, "A crop left offset must be within [0, 1].");
        }
        if (Top < 0m || Top > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(Top), Top, "A crop top offset must be within [0, 1].");
        }
        if (Width <= 0m || Width > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(Width), Width, "A crop width must be within (0, 1].");
        }
        if (Height <= 0m || Height > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(Height), Height, "A crop height must be within (0, 1].");
        }
        if (Left + Width > 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Width), Width, "A crop cannot extend past the right edge of the rotated source.");
        }
        if (Top + Height > 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Height), Height, "A crop cannot extend past the bottom edge of the rotated source.");
        }
    }

    private static void RequireScale(decimal value, string parameterName)
    {
        if (decimal.Round(value, 7) != value)
        {
            throw new ArgumentOutOfRangeException(
                parameterName, value, "A crop fraction cannot carry more than 7 decimal places.");
        }
    }
}

/// <summary>
/// The report-preparation state of one case document occurrence: the
/// immutable source facts of the exact version this occurrence names (never
/// touched by preparation, and never re-read from a later superseding
/// version), plus the mutable role/order/rotation/crop an Engineer chooses.
/// Keyed on <see cref="OccurrenceId"/>.
/// </summary>
public sealed record CaseAssetPreparation(
    Guid CaseId,
    Guid OccurrenceId,
    Guid DocumentId,
    Guid VersionId,
    int SourceVersion,
    string SourceSha256,
    string SourceContentType,
    CaseAssetReportRole Role,
    int? Order,
    CaseAssetRotation Rotation,
    CaseAssetCrop Crop,
    long PreparationVersion,
    string? PreparedBy,
    DateTimeOffset? PreparedAtUtc);

/// <summary>
/// One requested change to a single occurrence's preparation, guarded by its
/// own optimistic <see cref="ExpectedPreparationVersion"/> in addition to the
/// enclosing request's Case version and edit lease. Keyboard reordering and
/// drag reordering both submit the same shape: a full desired
/// <see cref="Order"/> per moved occurrence, which the store renormalizes to
/// a contiguous Supporting sequence.
/// </summary>
public sealed record CaseAssetPreparationEdit(
    Guid OccurrenceId,
    long ExpectedPreparationVersion,
    CaseAssetReportRole Role,
    int? Order,
    CaseAssetRotation Rotation,
    CaseAssetCrop Crop);

public sealed record SaveCaseAssetPreparationRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    IReadOnlyList<CaseAssetPreparationEdit> Edits)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

/// <summary>
/// Restores the named occurrences to their original presentation: Not used,
/// no order, no rotation, no crop. The originals bytes are never touched —
/// this only clears the preparation columns.
/// </summary>
public sealed record ResetCaseAssetPreparationRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    IReadOnlyList<Guid> OccurrenceIds)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

/// <summary>
/// One image as the report will use it: its confirmed source identity/hash
/// and the prepared role/order/rotation/crop. Files and Report read the same
/// preparation through this and <see cref="ICaseAssetPreparationQueries"/>.
/// </summary>
public sealed record PreparedReportImage(
    Guid OccurrenceId,
    Guid VersionId,
    string Sha256,
    string ContentType,
    CaseAssetReportRole Role,
    int? Order,
    CaseAssetRotation Rotation,
    CaseAssetCrop Crop);

public interface ICaseAssetPreparationQueries
{
    Task<IReadOnlyList<CaseAssetPreparation>> ListForCaseAsync(
        Guid caseId,
        CancellationToken cancellationToken);
}

public interface ICaseAssetPreparationStore
{
    Task<IReadOnlyList<CaseAssetPreparation>> SaveAsync(
        SaveCaseAssetPreparationRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CaseAssetPreparation>> ResetAsync(
        ResetCaseAssetPreparationRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// One occurrence's preparation moved between the request's expected version
/// and the persisted row — a concurrent Save or Reset touched the same
/// occurrence first.
/// </summary>
public sealed class CaseAssetPreparationVersionConflictException(
    Guid caseId,
    Guid occurrenceId,
    long expectedVersion,
    long actualVersion)
    : InvalidOperationException(
        $"Case asset '{occurrenceId}' on case '{caseId}' is at preparation version {actualVersion}, not expected version {expectedVersion}.")
{
    public Guid CaseId { get; } = caseId;
    public Guid OccurrenceId { get; } = occurrenceId;
    public long ExpectedVersion { get; } = expectedVersion;
    public long ActualVersion { get; } = actualVersion;
}

/// <summary>
/// The one save-rule and report-projection owner for case asset preparation.
/// A second implementation of either rule anywhere else is a stop condition.
/// </summary>
public static class CaseAssetPreparationPolicy
{
    /// <summary>
    /// Validates and renormalizes a proposed complete preparation set for one
    /// Case: at most one Close-up and one Overview (exactly one each is
    /// report readiness, evaluated elsewhere — this is only the save rule),
    /// Supporting orders renormalized to a contiguous sequence from 1,
    /// accepted report content type for every used role, a validated crop for
    /// every item, no cross-Case reference, and — for every occurrence this
    /// call is told the confirmed source of — that the occurrence's pinned
    /// version is still that confirmed source.
    /// </summary>
    /// <param name="caseId">The Case every item must belong to.</param>
    /// <param name="proposed">
    /// The complete preparation set after edits are merged with the
    /// untouched existing rows.
    /// </param>
    /// <param name="confirmedSourcesByOccurrence">
    /// For every occurrence the caller can verify against live custody, the
    /// document's current confirmed version. An occurrence with no entry is
    /// accepted without a freshness check (used for rows the caller already
    /// knows are untouched and previously valid).
    /// </param>
    public static IReadOnlyList<CaseAssetPreparation> ValidateSet(
        Guid caseId,
        IReadOnlyList<CaseAssetPreparation> proposed,
        IReadOnlyDictionary<Guid, DocumentVersion> confirmedSourcesByOccurrence)
    {
        ArgumentNullException.ThrowIfNull(proposed);
        ArgumentNullException.ThrowIfNull(confirmedSourcesByOccurrence);

        var closeUps = 0;
        var overviews = 0;
        var normalized = new List<CaseAssetPreparation>(proposed.Count);
        var supporting = new List<CaseAssetPreparation>();

        foreach (var item in proposed)
        {
            if (item.CaseId != caseId)
            {
                throw new InvalidOperationException(
                    "A case asset preparation cannot reference another case's document.");
            }
            if (!Enum.IsDefined(item.Rotation))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(proposed), item.Rotation, "An unrecognized case asset rotation was supplied.");
            }

            item.Crop.Validate();

            if (confirmedSourcesByOccurrence.TryGetValue(item.OccurrenceId, out var confirmed))
            {
                RequireCurrentConfirmedSource(item, confirmed);
            }

            switch (item.Role)
            {
                case CaseAssetReportRole.NotUsed:
                    if (item.Order is not null)
                    {
                        throw new InvalidOperationException(
                            "An unused case asset cannot carry a supporting order.");
                    }
                    normalized.Add(item);
                    break;
                case CaseAssetReportRole.CloseUp:
                    RequireAcceptedContentType(item);
                    if (++closeUps > 1)
                    {
                        throw new InvalidOperationException("At most one Close-up image is permitted.");
                    }
                    normalized.Add(item with { Order = null });
                    break;
                case CaseAssetReportRole.Overview:
                    RequireAcceptedContentType(item);
                    if (++overviews > 1)
                    {
                        throw new InvalidOperationException("At most one Overview image is permitted.");
                    }
                    normalized.Add(item with { Order = null });
                    break;
                case CaseAssetReportRole.Supporting:
                    RequireAcceptedContentType(item);
                    supporting.Add(item);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(proposed), item.Role, "An unrecognized case asset role was supplied.");
            }
        }

        var orderedSupporting = supporting
            .OrderBy(item => item.Order ?? int.MaxValue)
            .ThenBy(item => item.OccurrenceId)
            .ToArray();
        for (var index = 0; index < orderedSupporting.Length; index++)
        {
            normalized.Add(orderedSupporting[index] with { Order = index + 1 });
        }

        return normalized;
    }

    /// <summary>
    /// The report's ordered image set: Close-up first, Overview second, then
    /// Supporting by its persisted order. Not used images are excluded.
    /// </summary>
    public static IReadOnlyList<PreparedReportImage> ForReport(IReadOnlyList<CaseAssetPreparation> current)
    {
        ArgumentNullException.ThrowIfNull(current);
        var closeUp = current.Where(item => item.Role == CaseAssetReportRole.CloseUp);
        var overview = current.Where(item => item.Role == CaseAssetReportRole.Overview);
        var supporting = current
            .Where(item => item.Role == CaseAssetReportRole.Supporting)
            .OrderBy(item => item.Order ?? int.MaxValue);

        return closeUp.Concat(overview).Concat(supporting)
            .Select(item => new PreparedReportImage(
                item.OccurrenceId,
                item.VersionId,
                item.SourceSha256,
                item.SourceContentType,
                item.Role,
                item.Order,
                item.Rotation,
                item.Crop))
            .ToArray();
    }

    private static void RequireAcceptedContentType(CaseAssetPreparation item)
    {
        if (!ReportImageEvidence.IsAcceptedContentType(item.SourceContentType))
        {
            throw new InvalidOperationException(
                $"The case asset '{item.OccurrenceId:D}' has an unsupported content type for report use.");
        }
    }

    private static void RequireCurrentConfirmedSource(CaseAssetPreparation item, DocumentVersion confirmed)
    {
        if (confirmed.DocumentId != item.DocumentId
            || confirmed.Id != item.VersionId
            || !confirmed.IsCurrent
            || confirmed.CustodyStatus != DocumentCustodyStatus.Confirmed
            || confirmed.IsLogicallyRemoved
            || !string.Equals(confirmed.Sha256, item.SourceSha256, StringComparison.Ordinal)
            || !string.Equals(confirmed.MediaType, item.SourceContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The case asset '{item.OccurrenceId:D}' no longer matches its current confirmed source.");
        }
    }
}
