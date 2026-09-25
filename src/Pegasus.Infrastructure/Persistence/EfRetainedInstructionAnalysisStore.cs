using System.Data;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The retained-instruction analysis record and its source candidates.
///
/// A pre-case receipt has no Case document to point at, so every candidate is
/// keyed on the retained <c>IntakeAsset</c> and carries null document ids — the
/// shape the table's own check constraint enforces.
/// </summary>
public sealed class EfRetainedInstructionAnalysisStore(
    IDbContextFactory<PegasusDbContext> contextFactory)
    : IRetainedInstructionAnalysisStore
{
    /// <summary>
    /// The unique index is on the (receipt, asset, key) TRIPLE, so an operation
    /// key is not by itself unique in the database. This lookup exists for the
    /// command's pre-read, which has only the key, so it takes the newest row
    /// under a deterministic order rather than asserting a uniqueness the schema
    /// does not enforce — a `Single` here would throw
    /// <see cref="InvalidOperationException"/> instead of the documented
    /// conflict. <see cref="RecordAsync"/> probes the full triple and is what
    /// actually decides replay against conflict.
    /// </summary>
    public async Task<RetainedInstructionAnalysis?> FindByOperationKeyAsync(
        string operationKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        var key = operationKey.Trim();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var analysis = await context.Set<RetainedInstructionAnalysisEntity>().AsNoTracking()
            .Where(item => item.OperationKey == key)
            .OrderByDescending(item => item.CompletedAtUtc)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return analysis is null ? null : await MapAsync(context, analysis, cancellationToken);
    }

    public async Task<(RetainedInstructionAnalysis Analysis, bool IsReplay)> RecordAsync(
        RetainedInstructionAnalysis analysis,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        var key = analysis.OperationKey.Trim();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        // Probed on the same (receipt, asset, key) triple the unique index
        // covers, so the probe asserts exactly the uniqueness the database
        // enforces and no more. Under serializable isolation this is what makes
        // a replay quiet; the index is the backstop that keeps two racing
        // writers from ever producing two candidate sets.
        var existing = await context.Set<RetainedInstructionAnalysisEntity>()
            .SingleOrDefaultAsync(
                item => item.IntakeReceiptId == analysis.ReceiptId
                    && item.IntakeAssetId == analysis.IntakeAssetId
                    && item.OperationKey == key,
                cancellationToken);
        if (existing is not null)
        {
            if (existing.ExpectedReceiptVersion != analysis.ExpectedReceiptVersion)
            {
                throw new RetainedInstructionAnalysisConflictException();
            }

            return (await MapAsync(context, existing, cancellationToken), true);
        }

        // A key already spent on a DIFFERENT receipt or asset is a conflict too:
        // the triple above cannot see it, and letting it through would bind one
        // key to two analyses and break the command's replay contract.
        if (await context.Set<RetainedInstructionAnalysisEntity>()
            .AnyAsync(item => item.OperationKey == key, cancellationToken))
        {
            throw new RetainedInstructionAnalysisConflictException();
        }

        var entity = new RetainedInstructionAnalysisEntity
        {
            Id = analysis.Id,
            IntakeReceiptId = analysis.ReceiptId,
            IntakeAssetId = analysis.IntakeAssetId,
            SourceSha256 = analysis.SourceSha256,
            OperationKey = key,
            State = analysis.Outcome.ToString(),
            ExpectedReceiptVersion = analysis.ExpectedReceiptVersion,
            CompletedAtUtc = analysis.CompletedAtUtc
        };
        context.Set<RetainedInstructionAnalysisEntity>().Add(entity);
        foreach (var candidate in analysis.Candidates)
        {
            context.Set<IntakeSourceCandidateEntity>().Add(new IntakeSourceCandidateEntity
            {
                Id = candidate.Id,
                AnalysisId = entity.Id,
                DocumentVersionId = null,
                IntakeAssetId = analysis.IntakeAssetId,
                SourceSha256 = analysis.SourceSha256,
                Occurrence = candidate.Occurrence,
                DocumentRole = candidate.DocumentRole,
                Field = candidate.Field,
                PartyRole = candidate.PartyRole,
                ReferenceRole = candidate.ReferenceRole,
                RawValue = candidate.RawValue,
                NormalizedValue = candidate.NormalizedValue,
                Unit = candidate.Unit,
                Currency = candidate.Currency,
                LocatorJson = AnalyzeRetainedInstruction.LocatorJson(
                    candidate.SourceLabel, candidate.Page, candidate.Locator),
                ReaderKey = candidate.ReaderKey,
                ReaderVersion = candidate.ReaderVersion,
                PolicyKey = candidate.PolicyKey,
                PolicyVersion = candidate.PolicyVersion,
                Disposition = candidate.Disposition.ToString()
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (analysis with { OperationKey = key }, false);
    }

    private static async Task<RetainedInstructionAnalysis> MapAsync(
        PegasusDbContext context,
        RetainedInstructionAnalysisEntity entity,
        CancellationToken cancellationToken)
    {
        var candidates = await context.Set<IntakeSourceCandidateEntity>().AsNoTracking()
            .Where(item => item.AnalysisId == entity.Id)
            .OrderBy(item => item.Field)
            .ThenBy(item => item.Occurrence)
            .ToArrayAsync(cancellationToken);
        return new(
            entity.Id,
            entity.IntakeReceiptId,
            entity.IntakeAssetId,
            entity.SourceSha256,
            entity.OperationKey,
            Enum.Parse<RetainedInstructionAnalysisOutcome>(entity.State),
            entity.ExpectedReceiptVersion,
            entity.CompletedAtUtc,
            candidates.Select(Map).ToArray());
    }

    private static RetainedInstructionCandidate Map(IntakeSourceCandidateEntity entity)
    {
        var (sourceLabel, page, locator) = AnalyzeRetainedInstruction.ReadLocator(entity.LocatorJson);
        return new(
            entity.Id,
            entity.DocumentRole,
            entity.Field,
            entity.PartyRole,
            entity.ReferenceRole,
            entity.RawValue,
            entity.NormalizedValue,
            entity.Unit,
            entity.Currency,
            sourceLabel,
            page,
            entity.Occurrence,
            entity.ReaderKey,
            entity.ReaderVersion,
            entity.PolicyKey,
            entity.PolicyVersion,
            Enum.Parse<SourceCandidateDisposition>(entity.Disposition),
            locator);
    }
}
