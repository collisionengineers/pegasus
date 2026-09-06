using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The durable record of page-restricted OCR operations, on the storage the
/// foundation froze for C-F02.
///
/// The operation id IS the durable external-work item id: the outbox row and
/// the operation it describes are the same identity, so a queue message can
/// only ever find the operation it was enqueued for, and a redelivery finds the
/// operation already terminal rather than starting a second one.
///
/// Every write is optimistic on the recorded version. A caller holding a stale
/// version loses, which is what stops two workers recording two outcomes for
/// one operation.
/// </summary>
public sealed class EfIntakeOcrOperationStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : IIntakeOcrOperationStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<IntakeOcrOperation?> FindAsync(
        Guid operationId,
        CancellationToken cancellationToken)
    {
        if (operationId == Guid.Empty)
        {
            return null;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<IntakeOcrOperationEntity>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == operationId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    /// <summary>
    /// Records the operation's identity before anything is sent anywhere.
    /// Idempotent on the operation key, under serializable isolation and behind
    /// the key's unique index: a replay returns the recorded operation, and a
    /// key already used for a DIFFERENT source or page set is refused rather
    /// than overwritten.
    /// </summary>
    public async Task<IntakeOcrOperation> BeginAsync(
        Guid operationId,
        IntakeOcrRequest request,
        CancellationToken cancellationToken)
    {
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("An OCR operation identifier is required.", nameof(operationId));
        }

        IntakeOcrRequest.Validate(request);
        var key = request.OperationKey.Trim();
        var pages = request.QualifiedPages.Order().ToArray();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var existing = await context.Set<IntakeOcrOperationEntity>()
            .SingleOrDefaultAsync(item => item.OperationKey == key, cancellationToken);
        if (existing is not null)
        {
            if (existing.Id != operationId
                || !string.Equals(existing.SourceSha256.TrimEnd(), request.SourceSha256, StringComparison.OrdinalIgnoreCase)
                || existing.DocumentVersionId != request.DocumentVersionId
                || existing.IntakeAssetId != request.IntakeAssetId
                || !Pages(existing.QualifiedPagesJson).SequenceEqual(pages))
            {
                throw new IntakeOcrOperationConflictException();
            }

            await transaction.CommitAsync(cancellationToken);
            return Map(existing);
        }

        var entity = new IntakeOcrOperationEntity
        {
            Id = operationId,
            DocumentVersionId = request.DocumentVersionId,
            IntakeAssetId = request.IntakeAssetId,
            SourceSha256 = request.SourceSha256,
            QualifiedPagesJson = JsonSerializer.Serialize(
                new OperationEnvelope(2, request.IntakeReceiptId, pages, 0, null, null),
                SerializerOptions),
            OperationKey = key,
            State = nameof(IntakeOcrState.Pending),
            Version = 1
        };
        context.Set<IntakeOcrOperationEntity>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    public Task<IntakeOcrOperation> RecordSubmitAttemptAsync(
        Guid operationId,
        long expectedVersion,
        DateTimeOffset attemptedAtUtc,
        CancellationToken cancellationToken) =>
        UpdateAsync(operationId, expectedVersion, entity =>
        {
            var envelope = Envelope(entity.QualifiedPagesJson);
            entity.QualifiedPagesJson = JsonSerializer.Serialize(
                envelope with { Version = 2, SubmitAttemptedAtUtc = attemptedAtUtc },
                SerializerOptions);
            entity.State = nameof(IntakeOcrState.Processing);
            entity.RetryAtUtc = null;
        }, cancellationToken);

    public Task<IntakeOcrOperation> RecordSubmittedAsync(
        Guid operationId,
        long expectedVersion,
        string providerOperationId,
        DateTimeOffset submittedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerOperationId);
        return UpdateAsync(
            operationId,
            expectedVersion,
            entity =>
            {
                entity.ProviderOperationId = providerOperationId.Trim();
                var envelope = Envelope(entity.QualifiedPagesJson);
                entity.QualifiedPagesJson = JsonSerializer.Serialize(
                    envelope with { Version = 2, SubmittedAtUtc = submittedAtUtc }, SerializerOptions);
                entity.State = nameof(IntakeOcrState.Processing);
                entity.RetryAtUtc = null;
            },
            cancellationToken);
    }

    /// <summary>
    /// One transaction stores the response hash, the provider identity, the page
    /// output AND the Completed state. Re-analysis is triggered only after this
    /// returns, so it can never read a completed operation whose output has not
    /// landed.
    /// </summary>
    public Task<IntakeOcrOperation> CompleteAsync(
        Guid operationId,
        long expectedVersion,
        IntakeOcrResult result,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (string.IsNullOrWhiteSpace(result.ResponseSha256))
        {
            throw new ArgumentException(
                "A completed OCR result carries the hash of the response it was read from.",
                nameof(result));
        }

        return UpdateAsync(
            operationId,
            expectedVersion,
            entity =>
            {
                entity.State = nameof(IntakeOcrState.Completed);
                entity.ProviderOperationId = result.ProviderOperationId ?? entity.ProviderOperationId;
                entity.ResponseSha256 = result.ResponseSha256;
                entity.ResultJson = JsonSerializer.Serialize(
                    new ResultEnvelope(
                        1,
                        result.Provider,
                        result.ModelId,
                        result.ApiVersion,
                        result.PageResults),
                    SerializerOptions);
                entity.LastError = null;
                entity.RetryAtUtc = null;
            },
            cancellationToken);
    }

    public Task<IntakeOcrOperation> RecordOutcomeAsync(
        Guid operationId,
        long expectedVersion,
        IntakeOcrState state,
        IntakeOcrFailure failure,
        DateTimeOffset? retryAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(failure);
        if (state is IntakeOcrState.Completed)
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                "A completion is recorded with its result, not as an outcome.");
        }

        return UpdateAsync(
            operationId,
            expectedVersion,
            entity =>
            {
                entity.State = state.ToString();
                entity.LastError = $"{failure.Code}: {failure.Reason}";
                entity.RetryAtUtc = retryAtUtc;
                var envelope = Envelope(entity.QualifiedPagesJson);
                // The attempt count lives with the request envelope rather than
                // in a column of its own: the frozen storage records the pages
                // and the operation, and how many times we have tried is part of
                // that same request record.
                entity.QualifiedPagesJson = JsonSerializer.Serialize(
                    envelope with
                    {
                        Version = 2,
                        AttemptCount = envelope.AttemptCount + 1,
                        SubmitAttemptedAtUtc = state == IntakeOcrState.RetryScheduled
                            ? null
                            : envelope.SubmitAttemptedAtUtc
                    },
                    SerializerOptions);
            },
            cancellationToken);
    }

    private async Task<IntakeOcrOperation> UpdateAsync(
        Guid operationId,
        long expectedVersion,
        Action<IntakeOcrOperationEntity> apply,
        CancellationToken cancellationToken)
    {
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("An OCR operation identifier is required.", nameof(operationId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var entity = await context.Set<IntakeOcrOperationEntity>()
            .SingleOrDefaultAsync(item => item.Id == operationId, cancellationToken)
            ?? throw new IntakeOcrOperationConflictException();
        if (entity.Version != expectedVersion)
        {
            throw new IntakeOcrOperationConflictException();
        }

        apply(entity);
        entity.Version++;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    private static IntakeOcrOperation Map(IntakeOcrOperationEntity entity)
    {
        var envelope = Envelope(entity.QualifiedPagesJson);
        var result = entity.ResultJson is null
            ? null
            : JsonSerializer.Deserialize<ResultEnvelope>(entity.ResultJson, SerializerOptions);
        return new(
            entity.Id,
            envelope.IntakeReceiptId,
            entity.DocumentVersionId,
            entity.IntakeAssetId,
            entity.SourceSha256.TrimEnd(),
            envelope.Pages,
            entity.OperationKey,
            Enum.Parse<IntakeOcrState>(entity.State),
            entity.Version,
            entity.ProviderOperationId,
            entity.ResponseSha256?.TrimEnd(),
            entity.LastError,
            entity.RetryAtUtc,
            envelope.AttemptCount,
            result?.Pages,
            envelope.SubmitAttemptedAtUtc,
            envelope.SubmittedAtUtc);
    }

    private static int[] Pages(string qualifiedPagesJson) => Envelope(qualifiedPagesJson).Pages;

    private static OperationEnvelope Envelope(string qualifiedPagesJson) =>
        JsonSerializer.Deserialize<OperationEnvelope>(qualifiedPagesJson, SerializerOptions)
        ?? throw new InvalidDataException("The OCR operation request envelope is unreadable.");

    /// <param name="AttemptCount">
    /// How many non-completing outcomes this operation has recorded. It bounds
    /// the retry schedule; it never authorizes a resend of an operation whose
    /// side effect is uncertain.
    /// </param>
    private sealed record OperationEnvelope(
        int Version,
        Guid IntakeReceiptId,
        int[] Pages,
        int AttemptCount,
        DateTimeOffset? SubmitAttemptedAtUtc,
        DateTimeOffset? SubmittedAtUtc);

    private sealed record ResultEnvelope(
        int Version,
        string Provider,
        string ModelId,
        string ApiVersion,
        IReadOnlyList<IntakeOcrPage> Pages);
}
