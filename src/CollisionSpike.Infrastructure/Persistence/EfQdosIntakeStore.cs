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
    private const string PrincipalCode = "QDOS";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly string[] ActionableDecisionNames =
    [
        nameof(QdosIntakeDecision.NeedsSorting),
        nameof(QdosIntakeDecision.OcrRequired),
        nameof(QdosIntakeDecision.Unsupported),
        nameof(QdosIntakeDecision.TechnicalFailure)
    ];

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
                && exception is not CaseReferenceSequenceExhaustedException
                && IsRetryableConcurrencyFailure(exception))
            {
                var duplicate = await FindByHashAsync(draft.SourceHash, true, cancellationToken);
                if (duplicate is not null)
                {
                    return duplicate;
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
            item => ActionableDecisionNames.Contains(item.Decision),
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
            query = query.Where(item => ActionableDecisionNames.Contains(item.Decision));
        }
        else if (decision is not null)
        {
            var decisionName = decision.Value.ToString();
            query = query.Where(item => item.Decision == decisionName);
        }

        var entities = await query
            .Include(item => item.Case)
            .ToListAsync(cancellationToken);
        return entities
            .OrderByDescending(item => item.ReceivedAtUtc)
            .Take(100)
            .Select(item => new QdosIntakeSummary(
                item.Id,
                item.SourceFileName,
                item.ReceivedAtUtc,
                Enum.Parse<QdosIntakeDecision>(item.Decision),
                item.Case?.CaseReference,
                item.FailureReason))
            .ToArray();
    }

    public async Task<QdosIntakeRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.QdosIntakeReceipts
            .AsNoTracking()
            .Include(item => item.Case)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entity is null ? null : Map(entity, false);
    }

    private async Task<QdosIntakeRecord> StoreOnceAsync(
        QdosIntakeDraft draft,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existing = await context.QdosIntakeReceipts
            .AsNoTracking()
            .Include(item => item.Case)
            .SingleOrDefaultAsync(item => item.SourceHash == draft.SourceHash, cancellationToken);
        if (existing is not null)
        {
            return Map(existing, true);
        }

        CaseEntity? qdosCase = null;
        if (draft.Decision == QdosIntakeDecision.ConfirmedQdos
            && draft.CaseCreationAuthorized)
        {
            var year = draft.ReferenceYear;
            var counter = await context.PrincipalYearCounters.SingleOrDefaultAsync(
                item => item.PrincipalCode == PrincipalCode && item.Year == year,
                cancellationToken);
            if (counter is null)
            {
                counter = new()
                {
                    PrincipalCode = PrincipalCode,
                    Year = year,
                    CurrentSequence = 0
                };
                context.PrincipalYearCounters.Add(counter);
            }

            if (counter.CurrentSequence >= 999)
            {
                throw new CaseReferenceSequenceExhaustedException(PrincipalCode, year);
            }

            counter.CurrentSequence++;
            qdosCase = new()
            {
                Id = Guid.NewGuid(),
                PrincipalCode = PrincipalCode,
                CaseReference = $"{PrincipalCode}{year % 100:00}{counter.CurrentSequence:000}",
                CreatedAtUtc = draft.ProcessedAtUtc
            };
            context.Cases.Add(qdosCase);
        }

        var receipt = new QdosIntakeReceiptEntity
        {
            Id = Guid.NewGuid(),
            SourceFileName = draft.SourceFileName,
            MediaType = draft.MediaType,
            SourceLength = draft.SourceLength,
            SourceHash = draft.SourceHash,
            ReceivedAtUtc = draft.ReceivedAtUtc,
            Decision = draft.Decision.ToString(),
            DecisionReason = draft.DecisionReason,
            EvidenceJson = JsonSerializer.Serialize(draft.Evidence, JsonOptions),
            FieldsJson = JsonSerializer.Serialize(draft.Fields, JsonOptions),
            FailureCode = draft.FailureCode,
            FailureReason = draft.FailureReason,
            CaseId = qdosCase?.Id,
            Case = qdosCase
        };
        context.QdosIntakeReceipts.Add(receipt);
        context.AuditEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            IntakeReceiptId = receipt.Id,
            CaseId = qdosCase?.Id,
            EventType = "QdosIntakeReceived",
            Actor = draft.Actor,
            OccurredAtUtc = draft.ProcessedAtUtc,
            DetailsJson = JsonSerializer.Serialize(new
            {
                decision = draft.Decision.ToString(),
                caseReference = qdosCase?.CaseReference,
                caseCreationAuthorized = draft.CaseCreationAuthorized,
                sourceHash = draft.SourceHash
            }, JsonOptions)
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(receipt, false);
    }

    private async Task<QdosIntakeRecord?> FindByHashAsync(
        string sourceHash,
        bool isDuplicate,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.QdosIntakeReceipts
            .AsNoTracking()
            .Include(item => item.Case)
            .SingleOrDefaultAsync(item => item.SourceHash == sourceHash, cancellationToken);
        return entity is null ? null : Map(entity, isDuplicate);
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
            entity.ReceivedAtUtc,
            Enum.Parse<QdosIntakeDecision>(entity.Decision),
            entity.DecisionReason,
            JsonSerializer.Deserialize<IReadOnlyList<QdosEvidence>>(entity.EvidenceJson, JsonOptions) ?? [],
            fields,
            missingFields,
            entity.CaseId,
            entity.Case?.CaseReference,
            entity.FailureCode,
            entity.FailureReason,
            isDuplicate);
    }

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
