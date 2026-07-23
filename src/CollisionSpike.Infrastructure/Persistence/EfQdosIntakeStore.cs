using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using CollisionSpike.Core.Intake.Qdos;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CollisionSpike.Infrastructure.Persistence;

internal sealed class EfQdosIntakeStore(IDbContextFactory<CollisionSpikeDbContext> contextFactory)
    : IQdosIntakeStore, IQdosIntakeQueries
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    public async Task<QdosIntakeRecord> StoreAsync(
        QdosIntakeDraft draft,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                return await StoreOnceAsync(draft, cancellationToken);
            }
            catch (Exception exception) when (
                attempt < 3
                && IsRetryableConcurrencyFailure(exception))
            {
                var duplicate = await FindBySourceIdentityAsync(
                    draft.SourceIdentity,
                    cancellationToken);
                if (duplicate is not null)
                {
                    EnsureMatchingContent(duplicate, draft.SourceHash);
                    return duplicate with { IsDuplicate = true };
                }

                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }

        throw new InvalidOperationException("The intake receipt could not be stored after the concurrency retry limit.");
    }

    public async Task<QdosQueueCounts> GetCountsAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var review = await context.QdosIntakeReceipts.CountAsync(
            item => item.Decision == nameof(QdosIntakeDecision.ConfirmedQdos),
            cancellationToken);
        var needsSorting = await context.QdosIntakeReceipts.CountAsync(
            item => item.Decision == nameof(QdosIntakeDecision.NeedsSorting),
            cancellationToken);
        return new(review, needsSorting);
    }

    public async Task<IReadOnlyList<QdosIntakeSummary>> ListAsync(
        QdosIntakeDecision? decision,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.QdosIntakeReceipts.AsNoTracking();
        if (decision == QdosIntakeDecision.NeedsSorting)
        {
            var decisionName = decision.Value.ToString();
            query = query.Where(item => item.Decision == decisionName);
        }
        else if (decision is not null)
        {
            var decisionName = decision.Value.ToString();
            query = query.Where(item => item.Decision == decisionName);
        }

        var entities = await query
            .ToListAsync(cancellationToken);
        return entities
            .OrderByDescending(item => item.ReceivedAtUtc)
            .Take(100)
            .Select(item => new QdosIntakeSummary(
                item.Id,
                item.SourceFileName,
                item.ReceivedAtUtc,
                Enum.Parse<QdosIntakeDecision>(item.Decision),
                item.FailureReason))
            .ToArray();
    }

    public async Task<QdosIntakeRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.QdosIntakeReceipts
            .AsNoTracking()
            .Include(item => item.Assets)
            .Include(item => item.TypedDraft)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entity is null ? null : Map(entity, false);
    }

    public async Task<QdosIntakeRecord?> FindBySourceIdentityAsync(
        IntakeSourceIdentity sourceIdentity,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.QdosIntakeReceipts
            .AsNoTracking()
            .Include(item => item.Assets)
            .Include(item => item.TypedDraft)
            .SingleOrDefaultAsync(
                item => item.SourceChannel == sourceIdentity.Channel.ToString()
                    && item.ExternalReceiptToken == sourceIdentity.ExternalReceiptToken,
                cancellationToken);
        return entity is null ? null : Map(entity, false);
    }

    public async Task<IntakeAssetRecord?> GetAssetAsync(
        Guid receiptId,
        Guid assetId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.QdosIntakeAssets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.IntakeReceiptId == receiptId && item.Id == assetId,
                cancellationToken);
        return entity is null ? null : MapAsset(entity);
    }

    private async Task<QdosIntakeRecord> StoreOnceAsync(
        QdosIntakeDraft draft,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existingQuery = context.QdosIntakeReceipts
            .AsNoTracking()
            .Include(item => item.Assets)
            .Include(item => item.TypedDraft);
        if (context.Database.IsSqlServer())
        {
            existingQuery = context.QdosIntakeReceipts
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM [QdosIntakeReceipts] WITH (UPDLOCK, HOLDLOCK)
                    WHERE [SourceChannel] = {draft.SourceIdentity.Channel.ToString()}
                      AND [ExternalReceiptToken] = {draft.SourceIdentity.ExternalReceiptToken}
                """)
                .AsNoTracking()
                .Include(item => item.Assets)
                .Include(item => item.TypedDraft);
        }

        var existing = await existingQuery
            .SingleOrDefaultAsync(
                item => item.SourceChannel == draft.SourceIdentity.Channel.ToString()
                    && item.ExternalReceiptToken == draft.SourceIdentity.ExternalReceiptToken,
                cancellationToken);
        if (existing is not null)
        {
            EnsureMatchingContent(existing.SourceHash, draft.SourceHash);
            return Map(existing, true);
        }

        var receipt = new QdosIntakeReceiptEntity
        {
            Id = Guid.NewGuid(),
            SourceFileName = draft.SourceFileName,
            MediaType = draft.MediaType,
            SourceLength = draft.SourceLength,
            SourceHash = draft.SourceHash,
            SourceChannel = draft.SourceIdentity.Channel.ToString(),
            ExternalReceiptToken = draft.SourceIdentity.ExternalReceiptToken,
            ReceivedAtUtc = draft.ReceivedAtUtc,
            Decision = draft.Decision.ToString(),
            DecisionReason = draft.DecisionReason,
            EvidenceJson = JsonSerializer.Serialize(draft.Evidence, JsonOptions),
            FieldsJson = JsonSerializer.Serialize(draft.Fields, JsonOptions),
            OcrCandidatesJson = JsonSerializer.Serialize(draft.ScannedPdfPages, JsonOptions),
            FailureCode = draft.FailureCode,
            FailureReason = draft.FailureReason
        };
        if (draft.TypedDraft is not null)
        {
            receipt.TypedDraft = new()
            {
                IntakeReceiptId = receipt.Id,
                IntakeReceipt = receipt,
                PrincipalCode = draft.TypedDraft.PrincipalCode,
                ClaimantName = draft.TypedDraft.ClaimantName,
                ClaimNumber = draft.TypedDraft.ClaimNumber,
                VehicleRegistration = draft.TypedDraft.VehicleRegistration,
                VehicleMake = draft.TypedDraft.VehicleMake,
                VehicleModel = draft.TypedDraft.VehicleModel,
                VehicleMileage = draft.TypedDraft.VehicleMileage,
                AccidentCircumstances = draft.TypedDraft.AccidentCircumstances,
                DateOfIncident = draft.TypedDraft.DateOfIncident,
                InstructionDate = draft.TypedDraft.InstructionDate,
                InspectionAddress = draft.TypedDraft.InspectionAddress
            };
        }
        receipt.Assets.AddRange(draft.AssetRecords.Select(asset => new QdosIntakeAssetEntity
        {
            Id = asset.Id,
            IntakeReceiptId = receipt.Id,
            IntakeReceipt = receipt,
            SourceLabel = asset.SourceLabel,
            FileName = asset.FileName,
            MediaType = asset.MediaType,
            Kind = asset.Kind.ToString(),
            Disposition = asset.Disposition.ToString(),
            ContentLength = asset.ContentLength,
            ContentHash = asset.ContentHash,
            StorageKey = asset.StorageKey,
            PageNumber = asset.PageNumber,
            BoundsJson = asset.Bounds is null ? null : JsonSerializer.Serialize(asset.Bounds, JsonOptions),
            WidthPixels = asset.WidthPixels,
            HeightPixels = asset.HeightPixels
        }));
        context.QdosIntakeReceipts.Add(receipt);
        context.AuditEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            IntakeReceiptId = receipt.Id,
            EventType = "QdosIntakeReceived",
            Actor = draft.Actor,
            OccurredAtUtc = draft.ProcessedAtUtc,
            DetailsJson = JsonSerializer.Serialize(new
            {
                decision = draft.Decision.ToString(),
                sourceChannel = draft.SourceIdentity.Channel.ToString(),
                externalReceiptToken = draft.SourceIdentity.ExternalReceiptToken,
                sourceHash = draft.SourceHash
            }, JsonOptions)
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(receipt, false);
    }

    private static void EnsureMatchingContent(QdosIntakeRecord existing, string sourceHash) =>
        EnsureMatchingContent(existing.SourceHash, sourceHash);

    private static void EnsureMatchingContent(string existingSourceHash, string sourceHash)
    {
        if (!string.Equals(existingSourceHash, sourceHash, StringComparison.Ordinal))
        {
            throw new IntakeSourceIdentityConflictException();
        }
    }

    private static QdosIntakeRecord Map(QdosIntakeReceiptEntity entity, bool isDuplicate)
    {
        var fields = JsonSerializer.Deserialize<IReadOnlyList<QdosReviewField>>(entity.FieldsJson, JsonOptions) ?? [];
        var missingFields = fields
            .Where(field => field.Candidates.Count == 0)
            .Select(field => field.Name)
            .ToArray();

        return new(
            entity.Id,
            entity.SourceFileName,
            entity.MediaType,
            entity.SourceLength,
            entity.SourceHash,
            new(
                Enum.Parse<IntakeSourceChannel>(entity.SourceChannel),
                entity.ExternalReceiptToken),
            entity.ReceivedAtUtc,
            Enum.Parse<QdosIntakeDecision>(entity.Decision),
            entity.DecisionReason,
            JsonSerializer.Deserialize<IReadOnlyList<QdosEvidence>>(entity.EvidenceJson, JsonOptions) ?? [],
            fields,
            entity.TypedDraft is null ? null : MapTypedDraft(entity.TypedDraft),
            missingFields,
            entity.FailureCode,
            entity.FailureReason,
            isDuplicate,
            entity.Assets
                .OrderBy(asset => asset.Id)
                .Select(MapAsset)
                .ToArray(),
            JsonSerializer.Deserialize<IReadOnlyList<ScannedPdfOcrCandidate>>(
                entity.OcrCandidatesJson,
                JsonOptions) ?? []);
    }

    private static QdosTypedDraft MapTypedDraft(QdosTypedDraftEntity entity) => new(
        entity.PrincipalCode,
        entity.ClaimantName,
        entity.ClaimNumber,
        entity.VehicleRegistration,
        entity.VehicleMake,
        entity.VehicleModel,
        entity.VehicleMileage,
        entity.AccidentCircumstances,
        entity.DateOfIncident,
        entity.InstructionDate,
        entity.InspectionAddress);

    private static IntakeAssetRecord MapAsset(QdosIntakeAssetEntity entity) => new(
        entity.Id,
        entity.SourceLabel,
        entity.FileName,
        entity.MediaType,
        Enum.Parse<IntakeAssetKind>(entity.Kind),
        Enum.Parse<IntakeAssetDisposition>(entity.Disposition),
        entity.ContentLength,
        entity.ContentHash,
        entity.StorageKey,
        entity.PageNumber,
        entity.BoundsJson is null
            ? null
            : JsonSerializer.Deserialize<IntakeAssetBounds>(entity.BoundsJson, JsonOptions),
        entity.WidthPixels,
        entity.HeightPixels);

    private static bool IsRetryableConcurrencyFailure(Exception exception) => exception switch
    {
        SqlException { Number: 1205 or 2601 or 2627 } => true,
        SqliteException { SqliteErrorCode: 5 or 6 } => true,
        SqliteException { SqliteExtendedErrorCode: 1555 or 2067 } => true,
        _ when exception.InnerException is not null => IsRetryableConcurrencyFailure(exception.InnerException),
        _ => false
    };

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
