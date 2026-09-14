using System.Data;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;
using Pegasus.Core.ImageIntake;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Crop, rotation and tags on pre-Case images, keyed by the retained intake
/// asset. Every change is recorded in the action history and is idempotent on
/// its operation key, exactly as a Case image's tag is
/// (<see cref="EfDocumentCustodyStore"/>).
/// </summary>
internal sealed class EfPreCaseImagePreparationStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : IPreCaseImagePreparationStore
{
    private const string AggregateType = "intake_asset";
    internal const string CroppedEventKind = "pre_case_image_cropped";
    internal const string TaggedEventKind = "pre_case_image_tagged";
    internal const string UntaggedEventKind = "pre_case_image_untagged";

    public async Task<IReadOnlyDictionary<Guid, PreCaseImagePreparation>> ListAsync(
        IReadOnlyCollection<Guid> intakeAssetIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intakeAssetIds);
        var ids = intakeAssetIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, PreCaseImagePreparation>();
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var preparations = await context.Set<IntakeAssetPreparationEntity>().AsNoTracking()
            .Where(item => ids.Contains(item.IntakeAssetId))
            .ToDictionaryAsync(item => item.IntakeAssetId, cancellationToken);
        var tags = await ReadTagsAsync(context, ids, cancellationToken);
        return ids
            .Where(id => preparations.ContainsKey(id) || tags.Contains(id))
            .ToDictionary(id => id, id => Map(id, preparations.GetValueOrDefault(id), tags[id]));
    }

    public async Task<PreCaseImagePreparation> SaveCropAsync(
        SavePreCaseImageCropRequest request,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var operationKey = request.OperationKey.Trim();
        var assetId = request.IntakeAssetId.ToString("D");
        var afterJson = DocumentActionHistory.Serialize(new CropHistoryValue(
            request.IntakeAssetId,
            (int)request.Rotation,
            request.Crop.IsFull ? null : request.Crop));

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (await FindHistoryAsync(context, operationKey, cancellationToken) is { } replay)
        {
            DocumentActionHistory.RequireExactReplay(replay, AggregateType, assetId, CroppedEventKind, request.Actor, reason: null, afterJson);
            await transaction.CommitAsync(cancellationToken);
            return await ReadAsync(context, request.IntakeAssetId, cancellationToken);
        }

        await RequireImageAsync(context, request.IntakeAssetId, cancellationToken);
        var row = await context.Set<IntakeAssetPreparationEntity>()
            .SingleOrDefaultAsync(item => item.IntakeAssetId == request.IntakeAssetId, cancellationToken);
        if ((row?.Version ?? 0) != request.ExpectedVersion)
        {
            throw new InvalidOperationException("This image's crop changed while you were working. Reload and try again.");
        }

        var beforeJson = row is null
            ? null
            : DocumentActionHistory.Serialize(new CropHistoryValue(request.IntakeAssetId, row.RotationDegrees, CropOf(row)));
        if (row is null)
        {
            row = new IntakeAssetPreparationEntity { IntakeAssetId = request.IntakeAssetId };
            context.Add(row);
        }

        var crop = request.Crop.IsFull ? null : request.Crop;
        row.RotationDegrees = (short)request.Rotation;
        row.CropLeft = crop?.Left;
        row.CropTop = crop?.Top;
        row.CropWidth = crop?.Width;
        row.CropHeight = crop?.Height;
        row.Version = request.ExpectedVersion + 1;
        row.PreparedAtUtc = occurredAtUtc;
        row.PreparedByKind = request.Actor.Kind.ToString();
        row.PreparedBySubjectId = request.Actor.SubjectId;
        row.OperationKey = operationKey;
        context.ActionHistory.Add(DocumentActionHistory.Succeeded(
            AggregateType, assetId, CroppedEventKind, request.Actor, occurredAtUtc, operationKey,
            beforeJson: beforeJson, afterJson: afterJson));
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ReadAsync(context, request.IntakeAssetId, cancellationToken);
    }

    public async Task<PreCaseImagePreparation> SetTagAsync(
        TagPreCaseImageRequest request,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var operationKey = request.OperationKey.Trim();
        var assetId = request.IntakeAssetId.ToString("D");
        var eventKind = request.Applied ? TaggedEventKind : UntaggedEventKind;

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var tag = await context.Set<ImageTagEntity>()
            .SingleOrDefaultAsync(item => item.Id == request.TagId, cancellationToken)
            ?? throw new InvalidOperationException("The image tag is unavailable.");
        var afterJson = DocumentActionHistory.Serialize(new TagHistoryValue(request.IntakeAssetId, tag.Id, tag.Name));
        if (await FindHistoryAsync(context, operationKey, cancellationToken) is { } replay)
        {
            DocumentActionHistory.RequireExactReplay(replay, AggregateType, assetId, eventKind, request.Actor, reason: null, afterJson);
            await transaction.CommitAsync(cancellationToken);
            return await ReadAsync(context, request.IntakeAssetId, cancellationToken);
        }

        await RequireImageAsync(context, request.IntakeAssetId, cancellationToken);
        var assignment = await context.Set<IntakeAssetTagEntity>()
            .SingleOrDefaultAsync(item => item.IntakeAssetId == request.IntakeAssetId && item.TagId == tag.Id, cancellationToken);
        if (request.Applied)
        {
            if (assignment is not null)
            {
                throw new InvalidOperationException("This image already carries that tag.");
            }

            context.Add(new IntakeAssetTagEntity
            {
                IntakeAssetId = request.IntakeAssetId,
                TagId = tag.Id,
                AppliedByKind = request.Actor.Kind.ToString(),
                AppliedBySubjectId = request.Actor.SubjectId,
                AppliedAtUtc = occurredAtUtc,
                OperationKey = operationKey
            });
        }
        else
        {
            if (assignment is null)
            {
                throw new InvalidOperationException("This image does not carry that tag.");
            }

            context.Remove(assignment);
        }

        context.ActionHistory.Add(DocumentActionHistory.Succeeded(
            AggregateType, assetId, eventKind, request.Actor, occurredAtUtc, operationKey, afterJson: afterJson));
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ReadAsync(context, request.IntakeAssetId, cancellationToken);
    }

    /// <summary>
    /// A pre-Case image becoming a Case document keeps what staff already made of
    /// it: the rotation and crop land on the occurrence's preparation columns and
    /// each tag becomes an occurrence tag. Called inside the transaction that adds
    /// the occurrence.
    /// </summary>
    internal static async Task CopyToOccurrenceAsync(
        PegasusDbContext context,
        Guid intakeAssetId,
        DocumentOccurrenceEntity occurrence,
        CancellationToken cancellationToken)
    {
        var preparation = await context.Set<IntakeAssetPreparationEntity>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.IntakeAssetId == intakeAssetId, cancellationToken);
        if (preparation is not null)
        {
            occurrence.RotationDegrees = preparation.RotationDegrees;
            occurrence.CropLeft = preparation.CropLeft;
            occurrence.CropTop = preparation.CropTop;
            occurrence.CropWidth = preparation.CropWidth;
            occurrence.CropHeight = preparation.CropHeight;
            occurrence.PreparedBy = $"{preparation.PreparedByKind}:{preparation.PreparedBySubjectId}";
            occurrence.PreparedAtUtc = preparation.PreparedAtUtc;
        }

        var tags = await context.Set<IntakeAssetTagEntity>().AsNoTracking()
            .Where(item => item.IntakeAssetId == intakeAssetId)
            .ToListAsync(cancellationToken);
        foreach (var tag in tags)
        {
            context.Add(new DocumentOccurrenceTagEntity
            {
                OccurrenceId = occurrence.Id,
                TagId = tag.TagId,
                AppliedByKind = tag.AppliedByKind,
                AppliedBySubjectId = tag.AppliedBySubjectId,
                AppliedAtUtc = tag.AppliedAtUtc,
                OperationKey = $"pre-case-tag:{occurrence.Id:N}:{tag.TagId:N}"
            });
        }
    }

    private static async Task RequireImageAsync(PegasusDbContext context, Guid intakeAssetId, CancellationToken cancellationToken)
    {
        var mediaType = await context.IntakeAssets.AsNoTracking()
            .Where(item => item.Id == intakeAssetId)
            .Select(item => item.MediaType)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("The image is unavailable.");
        if (!mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only an image can be cropped or tagged.");
        }
    }

    private static Task<ActionHistoryEntity?> FindHistoryAsync(
        PegasusDbContext context,
        string operationKey,
        CancellationToken cancellationToken) =>
        context.ActionHistory.AsNoTracking()
            .Where(item => item.AggregateType == AggregateType && item.CorrelationId == operationKey)
            .FirstOrDefaultAsync(cancellationToken);

    private static async Task<PreCaseImagePreparation> ReadAsync(
        PegasusDbContext context,
        Guid intakeAssetId,
        CancellationToken cancellationToken)
    {
        var preparation = await context.Set<IntakeAssetPreparationEntity>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.IntakeAssetId == intakeAssetId, cancellationToken);
        var tags = await ReadTagsAsync(context, [intakeAssetId], cancellationToken);
        return Map(intakeAssetId, preparation, tags[intakeAssetId]);
    }

    private static async Task<ILookup<Guid, ImageTagAssignment>> ReadTagsAsync(
        PegasusDbContext context,
        IReadOnlyCollection<Guid> intakeAssetIds,
        CancellationToken cancellationToken)
    {
        var rows = await (
                from assignment in context.Set<IntakeAssetTagEntity>().AsNoTracking()
                join tag in context.Set<ImageTagEntity>().AsNoTracking() on assignment.TagId equals tag.Id
                where intakeAssetIds.Contains(assignment.IntakeAssetId)
                select new { assignment.IntakeAssetId, assignment.AppliedAtUtc, Tag = tag })
            .ToListAsync(cancellationToken);
        return rows
            .OrderBy(row => row.AppliedAtUtc)
            .ThenBy(row => row.Tag.Name, StringComparer.Ordinal)
            .ToLookup(
                row => row.IntakeAssetId,
                row => new ImageTagAssignment(
                    row.Tag.Id,
                    row.Tag.Name,
                    Enum.Parse<ImageTagColour>(row.Tag.Colour),
                    row.Tag.IsBuiltIn,
                    row.AppliedAtUtc));
    }

    private static PreCaseImagePreparation Map(
        Guid intakeAssetId,
        IntakeAssetPreparationEntity? preparation,
        IEnumerable<ImageTagAssignment> tags) =>
        new(
            intakeAssetId,
            preparation is null ? CaseAssetRotation.None : (CaseAssetRotation)preparation.RotationDegrees,
            preparation is null ? CaseAssetCrop.Full : CropOf(preparation) ?? CaseAssetCrop.Full,
            [.. tags],
            preparation?.Version ?? 0);

    private static CaseAssetCrop? CropOf(IntakeAssetPreparationEntity row) =>
        row is { CropLeft: { } left, CropTop: { } top, CropWidth: { } width, CropHeight: { } height }
            ? new CaseAssetCrop(left, top, width, height)
            : null;

    private sealed record CropHistoryValue(Guid IntakeAssetId, int RotationDegrees, CaseAssetCrop? Crop);

    private sealed record TagHistoryValue(Guid IntakeAssetId, Guid TagId, string TagName);
}
