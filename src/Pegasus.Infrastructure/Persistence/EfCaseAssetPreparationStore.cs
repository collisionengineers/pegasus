using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Persists case asset report preparation over
/// <see cref="DocumentOccurrenceEntity"/>'s preparation columns: one
/// serializable transaction, operation-key replay via the case workflow
/// event stream (<see cref="EfCaseAssessmentStore"/>'s pattern), the
/// server-owned edit lease and optimistic Case version
/// (<see cref="CaseMutationGuard"/>/<see cref="ArchivedCaseGuard"/>), a
/// per-row <c>PreparationVersion</c> optimistic check, and the same three
/// history records (workflow event, permanent action history, case history)
/// the sibling case-mutation stores write. Original bytes and
/// <c>DocumentVersion.Sha256</c> are never touched.
/// </summary>
public sealed class EfCaseAssetPreparationStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : ICaseAssetPreparationStore, ICaseAssetPreparationQueries
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

    public async Task<IReadOnlyList<CaseAssetPreparation>> SaveAsync(
        SaveCaseAssetPreparationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateActor(request.Actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Reason);
        ArgumentNullException.ThrowIfNull(request.Edits);
        if (request.Edits.Count == 0)
        {
            throw new ArgumentException("At least one occurrence edit is required.", nameof(request));
        }
        var operationKey = ValidateOperationKey(request.OperationKey);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var requestHash = RequestHash("save_case_asset_preparation", request.CaseId, operationKey, request.Reason,
            request.Actor, JsonSerializer.Serialize(request.Edits, JsonOptions));
        var replay = await FindReplayAsync(context, request.CaseId, operationKey, cancellationToken);
        if (replay is not null)
        {
            RequireExactReplay(replay, requestHash, request.CaseId, operationKey);
            return await LoadCurrentAsync(context, request.CaseId, cancellationToken);
        }

        var workflow = await RequireWorkflowAsync(context, request.CaseId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        CaseMutationGuard.RequireVersion(workflow, request.ExpectedVersion);
        CaseMutationGuard.RequireLease(workflow, request.Actor, request.EditLeaseToken, now);
        ArchivedCaseGuard.RequireMutable(workflow);

        var beforeVersion = workflow.Version;
        var beforeState = await LoadCurrentAsync(context, request.CaseId, cancellationToken);
        var result = await PrepareSaveAsync(context, workflow, request, now, cancellationToken);

        AddHistory(
            context,
            workflow,
            "case_asset_preparation_saved",
            operationKey,
            requestHash,
            request.Actor,
            request.Reason,
            beforeVersion,
            Serialize(beforeState),
            Serialize(result),
            now);
        CaseMutationGuard.Complete(workflow);
        // Prepared image role, order, rotation and crop are frozen report
        // inputs: this edit stales the Case's current generation in the same
        // transaction.
        await EfCaseReportGenerationStore.MarkStaleAsync(
            context, request.CaseId, "asset_preparation_changed", now, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new CaseVersionConflictException(
                request.CaseId, request.ExpectedVersion, request.ExpectedVersion + 1);
        }

        return result;
    }

    public async Task<IReadOnlyList<CaseAssetPreparation>> ResetAsync(
        ResetCaseAssetPreparationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateActor(request.Actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Reason);
        ArgumentNullException.ThrowIfNull(request.OccurrenceIds);
        if (request.OccurrenceIds.Count == 0)
        {
            throw new ArgumentException("At least one occurrence is required.", nameof(request));
        }
        var occurrenceIds = request.OccurrenceIds.Distinct().ToArray();
        var operationKey = ValidateOperationKey(request.OperationKey);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var requestHash = RequestHash(
            "reset_case_asset_preparation",
            request.CaseId,
            operationKey,
            request.Reason,
            request.Actor,
            JsonSerializer.Serialize(occurrenceIds.OrderBy(id => id), JsonOptions));
        var replay = await FindReplayAsync(context, request.CaseId, operationKey, cancellationToken);
        if (replay is not null)
        {
            RequireExactReplay(replay, requestHash, request.CaseId, operationKey);
            return await LoadCurrentAsync(context, request.CaseId, cancellationToken);
        }

        var workflow = await RequireWorkflowAsync(context, request.CaseId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        CaseMutationGuard.RequireVersion(workflow, request.ExpectedVersion);
        CaseMutationGuard.RequireLease(workflow, request.Actor, request.EditLeaseToken, now);
        ArchivedCaseGuard.RequireMutable(workflow);

        var beforeVersion = workflow.Version;

        // The complete current case occurrence set — not just the requested
        // occurrences — because the returned/serialized state must match
        // what ListForCaseAsync would read, and every value here is read
        // from freshly tracked entities inside this same transaction (never
        // a separate no-tracking round trip before SaveChangesAsync, which
        // would still see the pre-mutation database row).
        var occurrences = await context.Set<DocumentOccurrenceEntity>()
            .Where(item => item.CaseId == request.CaseId && item.SemanticRole == DocumentSemanticRole.Image)
            .ToListAsync(cancellationToken);
        var occurrencesById = occurrences.ToDictionary(item => item.Id);
        if (occurrenceIds.Any(occurrenceId => !occurrencesById.ContainsKey(occurrenceId)))
        {
            throw new InvalidOperationException("A requested case asset is unavailable for this case.");
        }

        var pinnedVersionIds = occurrences.Select(item => item.VersionId).Distinct().ToArray();
        var pinnedVersionsById = await context.Set<DocumentVersionEntity>()
            .Where(version => pinnedVersionIds.Contains(version.Id))
            .ToDictionaryAsync(version => version.Id, cancellationToken);
        var proposedByOccurrence = occurrences.ToDictionary(
            occurrence => occurrence.Id,
            occurrence => ToPreparation(occurrence, pinnedVersionsById[occurrence.VersionId]));
        var beforeState = proposedByOccurrence.Values.ToArray();
        var occurrenceIdSet = occurrenceIds.ToHashSet();
        foreach (var occurrenceId in occurrenceIdSet)
        {
            var existing = proposedByOccurrence[occurrenceId];
            proposedByOccurrence[occurrenceId] = existing with
            {
                Role = CaseAssetReportRole.NotUsed,
                Order = null,
                Rotation = CaseAssetRotation.None,
                Crop = CaseAssetCrop.Full
            };
        }

        // Reused, not reimplemented: removing a Supporting image can leave a
        // gap in the remaining sequence, so the reset goes through the same
        // save-rule validation/renormalization as an ordinary Save. No
        // freshness re-check is needed — an item losing its role can never
        // violate the confirmed-source rule, and untouched items were
        // already valid.
        var validated = CaseAssetPreparationPolicy.ValidateSet(
            request.CaseId, proposedByOccurrence.Values.ToArray(), new Dictionary<Guid, DocumentVersion>());
        var validatedByOccurrence = validated.ToDictionary(item => item.OccurrenceId);

        var actorStamp = $"{request.Actor.Kind}:{request.Actor.SubjectId}";
        foreach (var occurrence in occurrences)
        {
            var final = validatedByOccurrence[occurrence.Id];
            occurrence.SupportingOrder = final.Order;
            if (!occurrenceIdSet.Contains(occurrence.Id))
            {
                continue;
            }

            occurrence.PreparationRole = nameof(CaseAssetReportRole.NotUsed);
            occurrence.RotationDegrees = 0;
            occurrence.CropLeft = null;
            occurrence.CropTop = null;
            occurrence.CropWidth = null;
            occurrence.CropHeight = null;
            occurrence.PreparationVersion = checked(occurrence.PreparationVersion + 1);
            occurrence.PreparedBy = actorStamp;
            occurrence.PreparedAtUtc = now;
        }

        var result = occurrences
            .Select(occurrence => ToPreparation(occurrence, pinnedVersionsById[occurrence.VersionId]))
            .ToArray();
        AddHistory(
            context,
            workflow,
            "case_asset_preparation_reset",
            operationKey,
            requestHash,
            request.Actor,
            request.Reason,
            beforeVersion,
            Serialize(beforeState),
            Serialize(result),
            now);
        CaseMutationGuard.Complete(workflow);
        // A reset changes the presentation a frozen report pinned just as a
        // save does; the current generation goes stale in the same
        // transaction.
        await EfCaseReportGenerationStore.MarkStaleAsync(
            context, request.CaseId, "asset_preparation_changed", now, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new CaseVersionConflictException(
                request.CaseId, request.ExpectedVersion, request.ExpectedVersion + 1);
        }

        return result;
    }

    /// <summary>
    /// Applies <paramref name="request"/>'s edits inside the caller's own
    /// context/transaction: per-row optimistic and current-confirmed-source
    /// checks, the <see cref="CaseAssetPreparationPolicy.ValidateSet"/>
    /// save rule, and the field writes. It commits nothing and never bumps
    /// the Case version — the caller (this store's own
    /// <see cref="SaveAsync"/>, or the future combined Case workspace save)
    /// owns exactly one version bump and history record for its whole
    /// transaction.
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
        var pinnedVersionsById = await context.Set<DocumentVersionEntity>()
            .Where(version => pinnedVersionIds.Contains(version.Id))
            .ToDictionaryAsync(version => version.Id, cancellationToken);

        var documentIds = editsByOccurrence.Keys
            .Select(occurrenceId => occurrencesById[occurrenceId].DocumentId)
            .Distinct()
            .ToArray();
        var currentVersionsByDocument = await context.Set<DocumentVersionEntity>()
            .Where(version => documentIds.Contains(version.DocumentId) && version.IsCurrent)
            .ToDictionaryAsync(version => version.DocumentId, cancellationToken);

        var confirmedSources = new Dictionary<Guid, DocumentVersion>();
        foreach (var occurrenceId in editsByOccurrence.Keys)
        {
            var documentId = occurrencesById[occurrenceId].DocumentId;
            if (currentVersionsByDocument.TryGetValue(documentId, out var confirmed))
            {
                confirmedSources[occurrenceId] = ToDocumentVersion(confirmed);
            }
        }

        var proposedByOccurrence = occurrences.ToDictionary(
            occurrence => occurrence.Id,
            occurrence => ToPreparation(occurrence, pinnedVersionsById[occurrence.VersionId]));
        foreach (var (occurrenceId, edit) in editsByOccurrence)
        {
            var existing = proposedByOccurrence[occurrenceId];
            proposedByOccurrence[occurrenceId] = existing with
            {
                Role = edit.Role,
                Order = edit.Order,
                Rotation = edit.Rotation,
                Crop = edit.Crop
            };
        }

        var validated = CaseAssetPreparationPolicy.ValidateSet(
            workflow.CaseId, proposedByOccurrence.Values.ToArray(), confirmedSources);
        var validatedByOccurrence = validated.ToDictionary(item => item.OccurrenceId);
        var actorStamp = $"{request.Actor.Kind}:{request.Actor.SubjectId}";

        foreach (var occurrence in occurrences)
        {
            var final = validatedByOccurrence[occurrence.Id];
            // Renormalizing a Supporting sequence can shift an unedited
            // neighbour's number even though nobody edited that row; persist
            // its new order so the database matches what this call returns.
            occurrence.SupportingOrder = final.Order;
            if (!editsByOccurrence.ContainsKey(occurrence.Id))
            {
                continue;
            }

            occurrence.PreparationRole = final.Role.ToString();
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
            .Select(occurrence => ToPreparation(occurrence, pinnedVersionsById[occurrence.VersionId]))
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

    private static async Task<IReadOnlyList<CaseAssetPreparation>> LoadCurrentAsync(
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

        return occurrences
            .Select(occurrence => ToPreparation(occurrence, versionsById[occurrence.VersionId]))
            .OrderBy(item => item.Role)
            .ThenBy(item => item.Order ?? int.MaxValue)
            .ToArray();
    }

    private static CaseAssetPreparation ToPreparation(
        DocumentOccurrenceEntity occurrence,
        DocumentVersionEntity pinnedVersion) =>
        new(
            occurrence.CaseId,
            occurrence.Id,
            occurrence.DocumentId,
            occurrence.VersionId,
            pinnedVersion.Version,
            pinnedVersion.Sha256,
            pinnedVersion.MediaType,
            ParseRole(occurrence.PreparationRole),
            occurrence.SupportingOrder,
            (CaseAssetRotation)occurrence.RotationDegrees,
            ToCrop(occurrence),
            occurrence.PreparationVersion,
            occurrence.PreparedBy,
            occurrence.PreparedAtUtc);

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

    private static CaseAssetReportRole ParseRole(string? role) =>
        role is null
            ? CaseAssetReportRole.NotUsed
            : Enum.TryParse<CaseAssetReportRole>(role, out var parsed)
                ? parsed
                : throw new InvalidDataException(
                    $"An unrecognized persisted case asset role '{role}' is retained.");

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

    private static async Task<CaseWorkflowEntity> RequireWorkflowAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        return await context.CaseWorkflows
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken)
            ?? throw new InvalidOperationException("The case is unavailable.");
    }

    private static Task<CaseWorkflowEventEntity?> FindReplayAsync(
        PegasusDbContext context,
        Guid caseId,
        string operationKey,
        CancellationToken cancellationToken) =>
        context.CaseWorkflowEvents.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CaseId == caseId && item.OperationKey == operationKey,
                cancellationToken);

    private static void RequireExactReplay(
        CaseWorkflowEventEntity replay,
        string requestHash,
        Guid caseId,
        string operationKey)
    {
        if (!FixedTimeEquals(replay.RequestHash, requestHash))
        {
            throw new CaseOperationConflictException(caseId, operationKey);
        }
    }

    private static void AddHistory(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        string eventType,
        string operationKey,
        string requestHash,
        ActionActor actor,
        string reason,
        long beforeVersion,
        string beforeJson,
        string afterJson,
        DateTimeOffset occurredAtUtc)
    {
        var rolesJson = JsonSerializer.Serialize(actor.Roles.OrderBy(role => role), JsonOptions);
        context.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Workflow = workflow,
            EventType = eventType,
            OperationKey = operationKey,
            RequestHash = requestHash,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = rolesJson,
            Reason = reason,
            OccurredAtUtc = occurredAtUtc,
            BeforeVersion = beforeVersion,
            AfterVersion = workflow.Version
        });
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = "case",
            AggregateId = workflow.CaseId.ToString("D"),
            EventKind = eventType,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = rolesJson,
            OccurredAtUtc = occurredAtUtc,
            Outcome = "Succeeded",
            CorrelationId = operationKey,
            Reason = reason,
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            PolicyVersion = "case_asset_preparation/v1"
        });
        context.CaseHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Case = workflow.Case,
            EventType = eventType,
            Actor = actor.SubjectId,
            Reason = reason,
            OccurredAtUtc = occurredAtUtc,
            OperationKey = operationKey,
            BeforeVersion = beforeVersion,
            AfterVersion = workflow.Version
        });
    }

    private static string Serialize(IReadOnlyList<CaseAssetPreparation> state) =>
        JsonSerializer.Serialize(
            state
                .OrderBy(item => item.OccurrenceId)
                .Select(item => new
                {
                    item.OccurrenceId,
                    item.DocumentId,
                    item.VersionId,
                    item.Role,
                    item.Order,
                    item.Rotation,
                    item.Crop,
                    item.PreparationVersion
                }),
            JsonOptions);

    private static void ValidateActor(ActionActor actor) =>
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);

    private static string ValidateOperationKey(string operationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        var normalized = operationKey.Trim();
        if (normalized.Length > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(operationKey), "The operation key cannot exceed 100 characters.");
        }

        return normalized;
    }

    private static string RequestHash(
        string command,
        Guid caseId,
        string operationKey,
        string reason,
        ActionActor actor,
        string payloadJson)
    {
        var material = JsonSerializer.Serialize(
            new
            {
                Command = command,
                CaseId = caseId,
                OperationKey = operationKey,
                Reason = reason,
                ActorKind = actor.Kind.ToString(),
                actor.SubjectId,
                Roles = actor.Roles.OrderBy(role => role).Select(role => role.ToString()).ToArray(),
                Payload = payloadJson
            },
            JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        if (left.Length != 64 || right.Length != 64
            || left.Any(character => !char.IsAsciiHexDigit(character))
            || right.Any(character => !char.IsAsciiHexDigit(character)))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(left),
            Convert.FromHexString(right));
    }
}
