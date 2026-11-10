using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Reads case asset report preparation from
/// <see cref="DocumentOccurrenceEntity"/>'s preparation columns and the
/// image's tags, and applies a preparation edit inside the Case workspace
/// save's own transaction with a per-row <c>PreparationVersion</c> optimistic
/// check. Original bytes and <c>DocumentVersion.Sha256</c> are never touched.
/// A Case's set carries the story place of each photo link photograph in
/// the report nobody has ordered (<see cref="StoryPlacesAsync"/>), which
/// orders it until someone does (R854).
/// </summary>
public sealed class EfCaseAssetPreparationStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : ICaseAssetPreparationQueries
{
    public async Task<CaseAssetPreparation?> GetForOccurrenceAsync(
        Guid caseId,
        Guid occurrenceId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }
        if (occurrenceId == Guid.Empty)
        {
            throw new ArgumentException("An occurrence identifier is required.", nameof(occurrenceId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var snapshot = await (
            from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
            join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                on occurrence.VersionId equals version.Id
            where occurrence.CaseId == caseId
                && occurrence.Id == occurrenceId
                && occurrence.SemanticRole == DocumentSemanticRole.Image
            select new { Occurrence = occurrence, Version = version })
            .SingleOrDefaultAsync(cancellationToken);

        if (snapshot is null)
        {
            return null;
        }

        var tags = await TagsAsync(context, [snapshot.Occurrence.Id], cancellationToken);
        return ToPreparation(snapshot.Occurrence, snapshot.Version, tags, NoStories);
    }

    public async Task<IReadOnlyList<CaseAssetPreparation>> ListForCaseAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await LoadCurrentAsync(context, caseId, cancellationToken);
    }

    /// <summary>
    /// Applies <paramref name="request"/>'s edits inside the caller's own
    /// context/transaction: per-row optimistic and current-confirmed-source
    /// checks, the <see cref="CaseAssetPreparationPolicy.ValidateSet"/>
    /// save rule, and the field writes. It commits nothing and never bumps
    /// the Case version — the caller (EfCaseWorkspaceStore's One Save) owns
    /// exactly one version bump and history record for its whole transaction.
    /// </summary>
    internal static async Task<IReadOnlyList<CaseAssetPreparation>> PrepareSaveAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        SaveCaseAssetPreparationRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(request);

        var editsByOccurrence = new Dictionary<Guid, CaseAssetPreparationEdit>();
        foreach (var edit in request.Edits)
        {
            if (!editsByOccurrence.TryAdd(edit.OccurrenceId, edit))
            {
                throw new ArgumentException(
                    "The same occurrence cannot be edited twice in one request.", nameof(request));
            }
        }

        var occurrences = await context.Set<DocumentOccurrenceEntity>()
            .Where(item => item.CaseId == workflow.CaseId && item.SemanticRole == DocumentSemanticRole.Image)
            .ToListAsync(cancellationToken);
        var occurrencesById = occurrences.ToDictionary(item => item.Id);
        if (editsByOccurrence.Keys.Any(occurrenceId => !occurrencesById.ContainsKey(occurrenceId)))
        {
            throw new InvalidOperationException("A requested case asset is unavailable for this case.");
        }

        foreach (var (occurrenceId, edit) in editsByOccurrence)
        {
            var occurrence = occurrencesById[occurrenceId];
            if (occurrence.PreparationVersion != edit.ExpectedPreparationVersion)
            {
                throw new CaseAssetPreparationVersionConflictException(
                    workflow.CaseId, occurrenceId, edit.ExpectedPreparationVersion, occurrence.PreparationVersion);
            }
        }

        var pinnedVersionIds = occurrences.Select(item => item.VersionId).Distinct().ToArray();
        var documentIds = editsByOccurrence.Keys
            .Select(occurrenceId => occurrencesById[occurrenceId].DocumentId)
            .Distinct()
            .ToArray();
        // The pinned versions and the current version of each edited document
        // are one read; the two sets are told apart in memory.
        var versions = await context.Set<DocumentVersionEntity>()
            .Where(version => pinnedVersionIds.Contains(version.Id)
                || (documentIds.Contains(version.DocumentId) && version.IsCurrent))
            .ToListAsync(cancellationToken);
        var pinnedVersionsById = versions.ToDictionary(version => version.Id);
        var currentVersionsByDocument = versions
            .Where(version => version.IsCurrent && documentIds.Contains(version.DocumentId))
            .ToDictionary(version => version.DocumentId);

        var confirmedSources = new Dictionary<Guid, DocumentVersion>();
        foreach (var occurrenceId in editsByOccurrence.Keys)
        {
            var documentId = occurrencesById[occurrenceId].DocumentId;
            if (currentVersionsByDocument.TryGetValue(documentId, out var confirmed))
            {
                confirmedSources[occurrenceId] = ToDocumentVersion(confirmed);
            }
        }

        var tags = await TagsAsync(context, [.. occurrences.Select(item => item.Id)], cancellationToken);
        // The save renumbers the report's images, so the story places its
        // unordered photo link photos as the read does, an edit's order
        // standing in for the stored one (R854).
        var stories = await StoryPlacesAsync(
            context,
            [.. occurrences.Where(item => CaseAssetPreparationPolicy.TakesStoryPlace(
                item.InReport,
                editsByOccurrence.TryGetValue(item.Id, out var pending) ? pending.Order : item.SupportingOrder))],
            cancellationToken);
        var proposedByOccurrence = occurrences.ToDictionary(
            occurrence => occurrence.Id,
            occurrence => ToPreparation(occurrence, pinnedVersionsById[occurrence.VersionId], tags, stories));
        foreach (var (occurrenceId, edit) in editsByOccurrence)
        {
            var existing = proposedByOccurrence[occurrenceId];
            proposedByOccurrence[occurrenceId] = existing with
            {
                Order = edit.Order,
                Rotation = edit.Rotation,
                Crop = edit.Crop,
                FullPage = edit.FullPage
            };
        }

        var validated = CaseAssetPreparationPolicy.ValidateSet(
            workflow.CaseId, proposedByOccurrence.Values.ToArray(), confirmedSources);
        var validatedByOccurrence = validated.ToDictionary(item => item.OccurrenceId);
        var actorStamp = $"{request.Actor.Kind}:{request.Actor.SubjectId}";

        foreach (var occurrence in occurrences)
        {
            var final = validatedByOccurrence[occurrence.Id];
            // Renumbering the images in the report can shift an unedited
            // neighbour's number even though nobody edited that row; persist
            // its new order so the database matches what this call returns.
            occurrence.SupportingOrder = final.Order;
            if (!editsByOccurrence.ContainsKey(occurrence.Id))
            {
                continue;
            }

            occurrence.PreparationFullPage = final.FullPage;
            occurrence.RotationDegrees = (short)final.Rotation;
            WriteCrop(occurrence, final.Crop);
            occurrence.PreparationVersion = checked(occurrence.PreparationVersion + 1);
            occurrence.PreparedBy = actorStamp;
            occurrence.PreparedAtUtc = now;
        }

        // Rebuilt from the now-mutated tracked entities, not the
        // pre-mutation `validated` set: PreparationVersion, PreparedBy and
        // PreparedAtUtc only take their final value in the loop above, so
        // returning `validated` directly would hand the caller a stale
        // PreparationVersion for every occurrence it just edited.
        return occurrences
            .Select(occurrence => ToPreparation(occurrence, pinnedVersionsById[occurrence.VersionId], tags, stories))
            .ToArray();
    }

    private static void WriteCrop(DocumentOccurrenceEntity occurrence, CaseAssetCrop crop)
    {
        if (crop.IsFull)
        {
            occurrence.CropLeft = null;
            occurrence.CropTop = null;
            occurrence.CropWidth = null;
            occurrence.CropHeight = null;
            return;
        }

        occurrence.CropLeft = crop.Left;
        occurrence.CropTop = crop.Top;
        occurrence.CropWidth = crop.Width;
        occurrence.CropHeight = crop.Height;
    }

    internal static async Task<IReadOnlyList<CaseAssetPreparation>> LoadCurrentAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var occurrences = await context.Set<DocumentOccurrenceEntity>()
            .AsNoTracking()
            .Where(item => item.CaseId == caseId && item.SemanticRole == DocumentSemanticRole.Image)
            .ToArrayAsync(cancellationToken);
        if (occurrences.Length == 0)
        {
            return [];
        }

        var versionIds = occurrences.Select(item => item.VersionId).Distinct().ToArray();
        var versionsById = await context.Set<DocumentVersionEntity>()
            .AsNoTracking()
            .Where(version => versionIds.Contains(version.Id))
            .ToDictionaryAsync(version => version.Id, cancellationToken);
        var tags = await TagsAsync(context, [.. occurrences.Select(item => item.Id)], cancellationToken);
        var stories = await StoryPlacesAsync(
            context,
            [.. occurrences.Where(item => CaseAssetPreparationPolicy.TakesStoryPlace(item.InReport, item.SupportingOrder))],
            cancellationToken);

        return occurrences
            .Select(occurrence => ToPreparation(occurrence, versionsById[occurrence.VersionId], tags, stories))
            .OrderBy(item => item.InReport ? 0 : 1)
            .ThenBy(item => item.Order ?? int.MaxValue)
            .ToArray();
    }

    /// <summary>The tags each of <paramref name="occurrenceIds"/> wears, which decide how it prints.</summary>
    private static async Task<ILookup<Guid, Guid>> TagsAsync(
        PegasusDbContext context,
        IReadOnlyList<Guid> occurrenceIds,
        CancellationToken cancellationToken) =>
        (await context.Set<DocumentOccurrenceTagEntity>()
            .AsNoTracking()
            .Where(item => occurrenceIds.Contains(item.OccurrenceId))
            .Select(item => new { item.OccurrenceId, item.TagId })
            .ToArrayAsync(cancellationToken))
        .ToLookup(item => item.OccurrenceId, item => item.TagId);

    private static readonly IReadOnlyDictionary<Guid, CaseAssetStoryPlace> NoStories =
        new Dictionary<Guid, CaseAssetStoryPlace>();

    /// <summary>
    /// The story place of each of <paramref name="unordered"/> a photo link
    /// sent, by occurrence, in one statement for the Case. The callers pass
    /// only the images a story can place
    /// (<see cref="CaseAssetPreparationPolicy.TakesStoryPlace"/>), so a Case
    /// whose images are all ordered, or none of whose unordered images came
    /// through intake, sends no statement (R854). A
    /// photo link's image is filed from its intake asset, which the
    /// occurrence names directly or through a Vehicle images record's fold
    /// (<see cref="IntakeImageOccurrenceIdentity"/>), and the capture item
    /// names the same asset through its filtered index.
    /// </summary>
    private static async Task<IReadOnlyDictionary<Guid, CaseAssetStoryPlace>> StoryPlacesAsync(
        PegasusDbContext context,
        IReadOnlyCollection<DocumentOccurrenceEntity> unordered,
        CancellationToken cancellationToken)
    {
        var assetByOccurrence = new Dictionary<Guid, Guid>();
        foreach (var occurrence in unordered)
        {
            if (occurrence.Source == DocumentSource.Intake
                && IntakeImageOccurrenceIdentity.TryReadAssetId(occurrence.SourceOccurrenceIdentity, out var assetId))
            {
                assetByOccurrence[occurrence.Id] = assetId;
            }
        }

        if (assetByOccurrence.Count == 0)
        {
            return NoStories;
        }

        // The IS NOT NULL test lets the read seek the filtered asset index.
        var ids = assetByOccurrence.Values.Distinct().Select(id => (Guid?)id).ToArray();
        var rows = await context.PhotoCaptureItems.AsNoTracking()
            .Where(item => item.IntakeAssetId != null && ids.Contains(item.IntakeAssetId))
            .Select(item => new { item.IntakeAssetId, item.CaptureId, item.Batch, item.StoryGroup, item.StorySequence, item.Ordinal })
            .ToListAsync(cancellationToken);
        var byAsset = rows
            .DistinctBy(row => row.IntakeAssetId)
            .ToDictionary(
                row => row.IntakeAssetId!.Value,
                row => new CaseAssetStoryPlace(row.CaptureId, row.Batch, row.StoryGroup, row.StorySequence, row.Ordinal));
        var stories = new Dictionary<Guid, CaseAssetStoryPlace>();
        foreach (var (occurrenceId, assetId) in assetByOccurrence)
        {
            if (byAsset.TryGetValue(assetId, out var story))
            {
                stories[occurrenceId] = story;
            }
        }

        return stories;
    }

    private static CaseAssetPreparation ToPreparation(
        DocumentOccurrenceEntity occurrence,
        DocumentVersionEntity pinnedVersion,
        ILookup<Guid, Guid> tags,
        IReadOnlyDictionary<Guid, CaseAssetStoryPlace> stories) =>
        new(
            occurrence.CaseId,
            occurrence.Id,
            occurrence.DocumentId,
            occurrence.VersionId,
            pinnedVersion.Version,
            pinnedVersion.Sha256,
            pinnedVersion.MediaType,
            occurrence.InReport,
            occurrence.SupportingOrder,
            (CaseAssetRotation)occurrence.RotationDegrees,
            ToCrop(occurrence),
            occurrence.PreparationVersion,
            occurrence.PreparedBy,
            occurrence.PreparedAtUtc,
            occurrence.PreparationFullPage)
        {
            TagIds = [.. tags[occurrence.Id]],
            SourceFileName = pinnedVersion.FileName,
            RecordedAtUtc = occurrence.RecordedAtUtc,
            CanPrint = pinnedVersion.CustodyStatus == DocumentCustodyStatus.Confirmed
                && pinnedVersion.IsCurrent
                && !pinnedVersion.IsLogicallyRemoved,
            Story = stories.GetValueOrDefault(occurrence.Id)
        };

    private static DocumentVersion ToDocumentVersion(DocumentVersionEntity value) =>
        new(
            value.Id,
            value.DocumentId,
            value.Version,
            value.FileName,
            value.MediaType,
            value.ContentLength,
            value.Sha256,
            value.CustodyStatus,
            value.CreatedAtUtc,
            value.CreatedBy,
            value.IsCurrent,
            value.IsLogicallyRemoved,
            value.RemovalReason);

    private static CaseAssetCrop ToCrop(DocumentOccurrenceEntity occurrence) =>
        occurrence.CropLeft is null
            || occurrence.CropTop is null
            || occurrence.CropWidth is null
            || occurrence.CropHeight is null
            ? CaseAssetCrop.Full
            : new(
                occurrence.CropLeft.Value,
                occurrence.CropTop.Value,
                occurrence.CropWidth.Value,
                occurrence.CropHeight.Value);
}
