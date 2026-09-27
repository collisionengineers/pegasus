using System.Data;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The Worker's settle pass over generated report files. Custody files a
/// version in Box on its own sweep; this pass then confirms the report's own
/// record of that file through the confirmation the Web request uses, so the
/// history, the generation state and the ready event are written one way.
/// </summary>
public sealed class EfSettleFiledCaseReportArtifacts(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : ISettleFiledCaseReportArtifacts
{
    internal const string ActorId = "report-artifact-settlement";

    /// <summary>
    /// How long a report and its file are left to the request producing them.
    /// </summary>
    internal static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(3);

    private static readonly ActionActor Actor = ActionActor.SystemWorker(ActorId);

    public async Task<int> ExecuteAsync(int maximumItems, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var filed = await ReadFiledAsync(maximumItems, cancellationToken).ConfigureAwait(false);

        var settled = 0;
        foreach (var file in filed)
        {
            try
            {
                await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
                await using var transaction = await context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable, cancellationToken);
                if (!await EfCaseReportGenerationStore.ConfirmArtifactAsync(
                        context,
                        new ConfirmCaseReportArtifactRequest(
                            Actor,
                            file.CaseId,
                            file.GenerationId,
                            file.ArtifactId,
                            file.DocumentId,
                            file.VersionId,
                            file.Sha256,
                            file.ContentLength,
                            file.FileName,
                            file.MediaType,
                            file.BoxFileId,
                            file.BoxVersionId,
                            timeProvider.GetUtcNow()),
                        cancellationToken).ConfigureAwait(false))
                {
                    // A retry of Generate report confirmed it first.
                    continue;
                }

                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                settled++;
            }
            catch (Exception exception) when (CaseAllocationRetry.IsRetryable(exception))
            {
                // A retry of Generate report reached the same artifact and
                // the database ended this transaction; the next pass reads the
                // row as the retry left it. Any other failure (a denied
                // permission above all) propagates and fails the sweep visibly.
            }
        }

        return settled;
    }

    private async Task<FiledArtifact[]> ReadFiledAsync(int maximumItems, CancellationToken cancellationToken)
    {
        // Both the report and its file are older than the grace period: a
        // companion document joins a generation frozen long before, so the
        // file's own record is what keeps the pass away from its request.
        var leftAloneSince = timeProvider.GetUtcNow() - GracePeriod;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await (
            from artifact in context.Set<GeneratedCaseArtifactEntity>().AsNoTracking()
            join generation in context.Set<CaseReportGenerationEntity>().AsNoTracking()
                on artifact.GenerationId equals generation.Id
            join occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                on artifact.OperationKey equals occurrence.OperationKey
            join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                on occurrence.VersionId equals version.Id
            where artifact.State != nameof(CaseReportArtifactStatus.Confirmed)
                && occurrence.CaseId == generation.CaseId
                && version.DocumentId == occurrence.DocumentId
                && version.CustodyStatus == DocumentCustodyStatus.Confirmed
                && !version.IsLogicallyRemoved
                && generation.GeneratedAtUtc < leftAloneSince
                && version.CreatedAtUtc < leftAloneSince
            orderby generation.GeneratedAtUtc, artifact.Id
            select new FiledArtifact(
                artifact.Id,
                artifact.GenerationId,
                generation.CaseId,
                version.DocumentId,
                version.Id,
                version.Sha256,
                version.ContentLength,
                version.FileName,
                version.MediaType,
                version.BoxFileId,
                version.BoxVersionId))
            .Take(maximumItems)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private sealed record FiledArtifact(
        Guid ArtifactId,
        Guid GenerationId,
        Guid CaseId,
        Guid DocumentId,
        Guid VersionId,
        string Sha256,
        long ContentLength,
        string FileName,
        string MediaType,
        string? BoxFileId,
        string? BoxVersionId);
}
