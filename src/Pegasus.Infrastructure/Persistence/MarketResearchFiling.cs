using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// What the two ways research reaches a Case share (FRD-27): a result from
/// the AI job list and a submission from Pegasus Desktop both file a report as
/// Valuation Evidence marked as the AI's, write the one AI market research
/// card for the guide month with its history, and take back what they filed
/// in custody when the transaction fails.
/// </summary>
internal static class MarketResearchFiling
{
    /// <summary>
    /// The findings or report document of a research result: Valuation
    /// Evidence in the Case's folder with the Automation source, which draws
    /// its AI tag in the Documents list, whoever files it (R1306). It claims
    /// no Case version and no edit lease.
    /// </summary>
    public static AddCaseDocumentCommand ReportDocument(
        Guid caseId,
        string fileName,
        string mediaType,
        ReadOnlyMemory<byte> content,
        string sourceOccurrenceIdentity,
        ActionActor actor,
        string operationKey) =>
        new(
            caseId,
            fileName,
            mediaType,
            content,
            DocumentSemanticRole.ValuationEvidence,
            DocumentSource.Automation,
            sourceOccurrenceIdentity,
            actor,
            operationKey,
            0,
            string.Empty);

    /// <summary>
    /// Writes the research card on the Case's current work and its history
    /// at the Case's current version. Research for a month that already has
    /// a card replaces that card (<see cref="ValuationPolicy.Replaces"/>); the
    /// earlier figures stay in the history. The Engineer's Value is not
    /// touched. <paramref name="eventType"/> is one the per-version history
    /// index leaves out, since the Case's version does not move.
    /// </summary>
    public static async Task<CaseValuation> WriteCardAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        ValuationDetails details,
        ActionActor actor,
        string operationKey,
        string eventType,
        string attachedReason,
        string replacedReason,
        string requestHash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var workId = await CaseWorkScope.CurrentIdAsync(context, workflow.CaseId, cancellationToken);
        var replaced = await EfValuationStore.FindReplacedAsync(context, workId, details, cancellationToken);
        var before = replaced is null ? null : EfValuationStore.Map(replaced);
        var entity = replaced ?? new CaseValuationEntity
        {
            Id = Guid.NewGuid(),
            WorkId = workId,
            Source = details.Source.ToString(),
            RecordedBy = actor.SubjectId,
            RecordedAtUtc = now
        };
        EfValuationStore.Write(entity, details, replaced is null ? null : actor.SubjectId, now);
        if (replaced is null)
        {
            context.CaseValuations.Add(entity);
        }

        var valuation = EfValuationStore.Map(entity);
        EfValuationStore.AddHistory(
            context,
            workflow,
            actor,
            operationKey,
            replaced is null ? attachedReason : replacedReason,
            eventType,
            requestHash,
            valuation,
            before,
            now);
        return valuation;
    }

    /// <summary>
    /// Rolls a failed filing back and removes from custody each file it had
    /// created there, so no file is left that no document names. A file that
    /// was already there is left alone. Every created file is tried, and what
    /// could not be confirmed is thrown together with
    /// <paramref name="failure"/>; otherwise the caller rethrows it.
    /// </summary>
    public static async Task RollBackAsync(
        IDbContextFactory<PegasusDbContext> contextFactory,
        IDocumentContentStore contentStore,
        IDbContextTransaction transaction,
        Guid caseId,
        string caseReference,
        IEnumerable<EfDocumentCustodyStore.PendingDocumentAdd> written,
        Exception failure)
    {
        Exception? rollbackFailure = null;
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception caught)
        {
            rollbackFailure = caught;
        }

        List<Exception> cleanupFailures = [];
        foreach (var pending in written)
        {
            if (pending.ContentWrite.Disposition != DocumentContentWriteDisposition.Created)
            {
                continue;
            }
            try
            {
                await DocumentContentRollback.RemoveOrphanAsync(
                    contextFactory,
                    contentStore,
                    caseId,
                    caseReference,
                    pending.Version.Id,
                    failure);
            }
            catch (Exception cleanupFailure)
            {
                cleanupFailures.Add(cleanupFailure);
            }
        }

        if (rollbackFailure is not null && cleanupFailures.Count > 0)
        {
            List<Exception> failures = [failure, rollbackFailure, .. cleanupFailures];
            throw new AggregateException(
                "The document database write failed, its rollback could not be confirmed, and custody cleanup did not complete.",
                failures);
        }
        if (rollbackFailure is not null)
        {
            throw new AggregateException(
                "The document database transaction failed and its rollback could not be confirmed.",
                failure,
                rollbackFailure);
        }
        if (cleanupFailures.Count == 1)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(cleanupFailures[0]);
        }
        if (cleanupFailures.Count > 1)
        {
            throw new AggregateException(cleanupFailures);
        }
    }
}
