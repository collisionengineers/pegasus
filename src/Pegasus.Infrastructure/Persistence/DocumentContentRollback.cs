using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;

namespace Pegasus.Infrastructure.Persistence;

internal static class DocumentContentRollback
{
    public static async Task RemoveOrphanAsync(
        IDbContextFactory<PegasusDbContext> dbContextFactory,
        IDocumentContentStore contentStore,
        Guid caseId,
        string caseReference,
        Guid versionId,
        Exception databaseFailure)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(contentStore);
        ArgumentNullException.ThrowIfNull(databaseFailure);

        bool isReferenced;
        try
        {
            await using var verificationContext =
                await dbContextFactory.CreateDbContextAsync(CancellationToken.None);
            isReferenced = await verificationContext.Set<DocumentVersionEntity>()
                .AsNoTracking()
                .AnyAsync(value => value.Id == versionId, CancellationToken.None);
        }
        catch (Exception verificationFailure)
        {
            throw new AggregateException(
                "The document database write failed and durable custody references could not be verified; content was retained fail-closed.",
                databaseFailure,
                verificationFailure);
        }

        if (isReferenced)
        {
            return;
        }

        try
        {
            await contentStore.DeleteAsync(caseId, caseReference, versionId, CancellationToken.None);
        }
        catch (Exception cleanupFailure)
        {
            throw new AggregateException(
                "The document database write failed and its unreferenced custody content could not be cleaned up.",
                databaseFailure,
                cleanupFailure);
        }
    }
}
