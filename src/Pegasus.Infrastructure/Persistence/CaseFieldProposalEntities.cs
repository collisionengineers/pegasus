using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The latest AI proposal on one decision field of one Case and how staff
/// resolved it (<see cref="CaseFieldProposalPolicy"/>). One row per field:
/// a later Automation proposal replaces the row.
/// </summary>
internal sealed class CaseFieldProposalEntity
{
    public Guid WorkId { get; set; }
    public required string FieldPath { get; set; }
    public required string ProposedValue { get; set; }
    public required string ProposedBy { get; set; }
    public DateTimeOffset ProposedAtUtc { get; set; }
    public string? Resolution { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }
}

internal static class CaseFieldProposalModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<CaseFieldProposalEntity>(entity =>
        {
            entity.ToTable("CaseFieldProposals", table => table.HasCheckConstraint(
                "CK_CaseFieldProposals_Resolution",
                "[Resolution] IS NULL OR [Resolution] IN ('Accepted', 'Corrected')"));
            entity.HasKey(item => new { item.WorkId, item.FieldPath });
            entity.Property(item => item.FieldPath).HasMaxLength(200);
            entity.Property(item => item.ProposedValue).HasMaxLength(4000).IsRequired();
            entity.Property(item => item.ProposedBy).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Resolution).HasMaxLength(20);
            entity.Property(item => item.ResolvedBy).HasMaxLength(200);
            entity.HasOne<CaseWorkEntity>().WithMany().HasForeignKey(item => item.WorkId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

/// <summary>
/// Moves the proposal row for one field write, inside the caller's context
/// and transaction. The rule is Core's; this only reads and stamps the row.
/// </summary>
internal static class CaseFieldProposalWriter
{
    public static void Track(
        PegasusDbContext context,
        Guid caseId,
        string path,
        string? value,
        ActorKind actorKind,
        string actorSubjectId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!CaseFieldProposalPolicy.DecisionPaths.Contains(path))
        {
            return;
        }

        var row = context.CaseFieldProposals.Find(caseId, path);
        var next = CaseFieldProposalPolicy.Next(
            row is null ? null : Map(row), path, value, actorKind, actorSubjectId, now);
        if (next is null)
        {
            return;
        }

        if (row is null)
        {
            row = new CaseFieldProposalEntity
            {
                WorkId = caseId,
                FieldPath = path,
                ProposedValue = next.ProposedValue,
                ProposedBy = next.ProposedBy
            };
            context.CaseFieldProposals.Add(row);
        }

        row.ProposedValue = next.ProposedValue;
        row.ProposedBy = next.ProposedBy;
        row.ProposedAtUtc = next.ProposedAtUtc;
        row.Resolution = next.Status == CaseFieldProposalStatus.Awaiting ? null : next.Status.ToString();
        row.ResolvedBy = next.ResolvedBy;
        row.ResolvedAtUtc = next.ResolvedAtUtc;
    }

    public static CaseFieldProposal Map(CaseFieldProposalEntity row) => new(
        row.FieldPath,
        row.ProposedValue,
        row.ProposedBy,
        row.ProposedAtUtc,
        row.Resolution switch
        {
            null => CaseFieldProposalStatus.Awaiting,
            nameof(CaseFieldProposalStatus.Accepted) => CaseFieldProposalStatus.Accepted,
            nameof(CaseFieldProposalStatus.Corrected) => CaseFieldProposalStatus.Corrected,
            _ => throw new InvalidDataException($"Unknown persisted proposal resolution '{row.Resolution}'.")
        },
        row.ResolvedBy,
        row.ResolvedAtUtc);
}

internal sealed class EfCaseFieldProposalQueries(IDbContextFactory<PegasusDbContext> contextFactory)
    : ICaseFieldProposalQueries
{
    public async Task<IReadOnlyList<CaseFieldProposal>> ListForCaseAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.CaseFieldProposals.AsNoTracking()
            .Where(item => item.WorkId == caseId)
            .OrderBy(item => item.FieldPath)
            .ToArrayAsync(cancellationToken);
        return [.. rows.Select(CaseFieldProposalWriter.Map)];
    }
}
