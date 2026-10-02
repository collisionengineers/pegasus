using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// One record's Work Centre dismissal (FRD-15): its rows that began at or
/// before <see cref="DismissedAtUtc"/> are hidden for everyone. The record is a
/// Case, an Unidentified item or an AI job, so there is no foreign key.
/// </summary>
internal sealed class WorkCentreDismissalEntity
{
    public Guid RecordId { get; set; }
    public DateTimeOffset DismissedAtUtc { get; set; }
    public required string DismissedBySubjectId { get; set; }
}

internal static class WorkCentreDismissalModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<WorkCentreDismissalEntity>(entity =>
        {
            entity.ToTable("WorkCentreDismissals");
            entity.HasKey(item => item.RecordId);
            entity.Property(item => item.RecordId).ValueGeneratedNever();
            entity.Property(item => item.DismissedBySubjectId).HasMaxLength(200).IsRequired();
        });
    }
}

internal sealed class EfWorkCentreDismissalStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : IWorkCentreDismissalStore
{
    public async Task DismissAsync(
        Guid recordId,
        DateTimeOffset dismissedAtUtc,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            context.Set<WorkCentreDismissalEntity>().Add(new WorkCentreDismissalEntity
            {
                RecordId = recordId,
                DismissedAtUtc = dismissedAtUtc,
                DismissedBySubjectId = actor.SubjectId
            });
            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException exception) when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 })
            {
                // Dismissed before: the instant moves on below.
            }
        }

        await using var retry = await contextFactory.CreateDbContextAsync(cancellationToken);
        await retry.Set<WorkCentreDismissalEntity>()
            .Where(item => item.RecordId == recordId && item.DismissedAtUtc < dismissedAtUtc)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(item => item.DismissedAtUtc, dismissedAtUtc)
                    .SetProperty(item => item.DismissedBySubjectId, actor.SubjectId),
                cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, DateTimeOffset>> ListAsync(
        IReadOnlyCollection<Guid> recordIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recordIds);
        if (recordIds.Count == 0)
        {
            return new Dictionary<Guid, DateTimeOffset>();
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // One JSON parameter however many records the list holds.
        var ids = recordIds.ToArray();
        return await context.Set<WorkCentreDismissalEntity>().AsNoTracking()
            .Where(item => EF.Parameter(ids).Contains(item.RecordId))
            .ToDictionaryAsync(item => item.RecordId, item => item.DismissedAtUtc, cancellationToken);
    }
}
