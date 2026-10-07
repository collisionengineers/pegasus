using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Documents;

/// <summary>
/// How an image in the report prints: the one Overview, the one Close-up, or
/// one of the ordered Supporting images. It is never chosen or stored: the
/// image's tags decide it (operator, 26 September 2026), through
/// <see cref="CaseAssetPreparationPolicy.ForReport"/>. Distinct from
/// <see cref="DocumentSemanticRole"/>, which intake and case export eligibility read.
/// </summary>
public enum CaseAssetReportRole
{
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
/// version), whether the report uses it, and the mutable order, rotation and
/// crop an Engineer chooses. A new image is in the report (operator, 26
/// September 2026). <see cref="TagIds"/> are the image's tags, which decide
/// how it prints. Keyed on <see cref="OccurrenceId"/>.
/// </summary>
/// <remarks>
/// Only an image that can print counts (operator, 26 September 2026):
/// <see cref="CanPrint"/> says the version this occurrence names is
/// custody-confirmed, current and not removed. An image that cannot print is
/// never one of the report's images and never raises a blocker; it joins the
/// report when its storage confirms.
/// </remarks>
public sealed record CaseAssetPreparation(
    Guid CaseId,
    Guid OccurrenceId,
    Guid DocumentId,
    Guid VersionId,
    int SourceVersion,
    string SourceSha256,
    string SourceContentType,
    bool InReport,
    int? Order,
    CaseAssetRotation Rotation,
    CaseAssetCrop Crop,
    long PreparationVersion,
    string? PreparedBy,
    DateTimeOffset? PreparedAtUtc,
    bool FullPage = false)
{
    public IReadOnlyList<Guid> TagIds { get; init; } = [];

    /// <summary>The file name of the version this occurrence names.</summary>
    public required string SourceFileName { get; init; }

    /// <summary>When the image arrived on the Case.</summary>
    public required DateTimeOffset RecordedAtUtc { get; init; }

    /// <summary>
    /// Whether the version this occurrence names is custody-confirmed,
    /// current and not removed, so the report can print it.
    /// </summary>
    public required bool CanPrint { get; init; }
}

/// <summary>
/// One requested change to a single occurrence's preparation, guarded by its
/// own optimistic <see cref="ExpectedPreparationVersion"/> in addition to the
/// enclosing request's Case version and edit lease. Keyboard reordering and
/// drag reordering both submit the same shape: a full desired
/// <see cref="Order"/> per moved occurrence, which the store renormalizes to
/// a contiguous sequence of the images in the report. Whether the report
/// uses the image is its own immediate command, not part of the Case save.
/// </summary>
public sealed record CaseAssetPreparationEdit(
    Guid OccurrenceId,
    long ExpectedPreparationVersion,
    int? Order,
    CaseAssetRotation Rotation,
    CaseAssetCrop Crop,
    bool FullPage = false);

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
/// One image as the report will use it: its confirmed source identity/hash,
/// how it prints, and its order/rotation/crop/full-page choice. Files and
/// Report read the same preparation through this and
/// <see cref="ICaseAssetPreparationQueries"/>.
/// </summary>
public sealed record PreparedReportImage(
    Guid OccurrenceId,
    Guid VersionId,
    string Sha256,
    string ContentType,
    CaseAssetReportRole Role,
    int? Order,
    CaseAssetRotation Rotation,
    CaseAssetCrop Crop,
    bool FullPage = false);

public interface ICaseAssetPreparationQueries
{
    /// <summary>
    /// Reads one image occurrence's current preparation snapshot. The Case and
    /// occurrence identities are both required so callers which already
    /// authorized a Case do not load every preparation just to render a tile.
    /// </summary>
    Task<CaseAssetPreparation?> GetForOccurrenceAsync(
        Guid caseId,
        Guid occurrenceId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CaseAssetPreparation>> ListForCaseAsync(
        Guid caseId,
        CancellationToken cancellationToken);
}

/// <summary>
/// One occurrence's preparation moved between the request's expected version
/// and the persisted row — a concurrent save touched the same occurrence
/// first.
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
/// Staff put one image in the report or take it out, at once rather than
/// with the Case save, so readiness reads the choice on the next view
/// (operator, 26 September 2026). Carries the Case's edit lease, expected
/// version and an operation key, like a tag.
/// </summary>
public sealed record SetCaseImageInReportCommand(
    Guid CaseId,
    Guid OccurrenceId,
    bool InReport,
    ActionActor Actor,
    string OperationKey,
    long ExpectedCaseVersion,
    string EditLeaseToken);

public interface ISetCaseImageInReport
{
    Task ExecuteAsync(SetCaseImageInReportCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// The one save-rule and report-projection owner for case asset preparation.
/// A second implementation of either rule anywhere else is a stop condition.
/// </summary>
public static class CaseAssetPreparationPolicy
{
    /// <summary>
    /// Validates and renormalizes a proposed complete preparation set for one
    /// Case: the images in the report renumbered to a contiguous order from 1,
    /// an accepted report content type for each of them, no order and no
    /// full page for an image out of the report, a validated crop for every
    /// item, no cross-Case reference, and — for every occurrence this call is
    /// told the confirmed source of — that the occurrence's pinned version is
    /// still that confirmed source.
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

        var normalized = new List<CaseAssetPreparation>(proposed.Count);
        var inReport = new List<CaseAssetPreparation>();

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

            if (item.InReport)
            {
                RequireAcceptedContentType(item);
                inReport.Add(item);
                continue;
            }
            if (item.Order is not null)
            {
                throw new InvalidOperationException(
                    "An image out of the report cannot carry a report order.");
            }
            // An image the report does not use never claims a page of its own.
            normalized.Add(item with { FullPage = false });
        }

        var placed = Placed(InReportOrder(inReport));
        for (var index = 0; index < placed.Count; index++)
        {
            normalized.Add(placed[index].Item with { Order = index + 1 });
        }

        return normalized;
    }

    /// <summary>
    /// The report's ordered image set (operator, 7 October 2026): of the
    /// images in the report, the first tagged Overview prints at place 1, the
    /// first other one tagged Close-up at place 2, and the rest as Supporting
    /// in the order staff set. The tags fix those two places; ordering moves
    /// only the rest. Each image's <see cref="PreparedReportImage.Order"/> is
    /// its place. An image out of the report is left out, and so is one that
    /// cannot print.
    /// </summary>
    public static IReadOnlyList<PreparedReportImage> ForReport(IReadOnlyList<CaseAssetPreparation> current)
    {
        ArgumentNullException.ThrowIfNull(current);
        return
        [
            .. Placed(InReportOrder(current.Where(item => item.InReport && item.CanPrint)))
                .Select((placed, index) => Prepared(placed.Item, placed.Role, index + 1))
        ];
    }

    /// <summary>
    /// The images in the report in their places: the first tagged Overview,
    /// then the first other one tagged Close-up, then the rest in staff order.
    /// An image tagged both prints once, as the Overview.
    /// </summary>
    private static List<(CaseAssetPreparation Item, CaseAssetReportRole Role)> Placed(
        List<CaseAssetPreparation> ordered)
    {
        var overview = ordered.FirstOrDefault(item => item.TagIds.Contains(ImageTagVocabulary.OverviewId));
        var closeUp = ordered.FirstOrDefault(item =>
            item.OccurrenceId != overview?.OccurrenceId
            && item.TagIds.Contains(ImageTagVocabulary.CloseUpId));
        return
        [
            .. overview is null ? [] : new[] { (overview, CaseAssetReportRole.Overview) },
            .. closeUp is null ? [] : new[] { (closeUp, CaseAssetReportRole.CloseUp) },
            .. ordered
                .Where(item => item.OccurrenceId != overview?.OccurrenceId && item.OccurrenceId != closeUp?.OccurrenceId)
                .Select(item => (item, CaseAssetReportRole.Supporting))
        ];
    }

    /// <summary>
    /// The images in the report in the order staff set, which is the order
    /// the gallery shows. Those not yet ordered follow the ordered ones in
    /// the order they arrived, then by file name.
    /// </summary>
    private static List<CaseAssetPreparation> InReportOrder(IEnumerable<CaseAssetPreparation> inReport) =>
    [
        .. inReport
            .OrderBy(item => item.Order ?? int.MaxValue)
            .ThenBy(item => item.RecordedAtUtc)
            .ThenBy(item => item.SourceFileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.OccurrenceId)
    ];

    private static PreparedReportImage Prepared(CaseAssetPreparation item, CaseAssetReportRole role, int? order) =>
        new(
            item.OccurrenceId,
            item.VersionId,
            item.SourceSha256,
            item.SourceContentType,
            role,
            order,
            item.Rotation,
            item.Crop,
            item.FullPage);
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
