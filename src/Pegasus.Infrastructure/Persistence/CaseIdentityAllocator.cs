using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Allocates the permanent portion of a Case identity. Every Case creator —
/// receipt-backed, receiptless, replacement and Triage — shares the same
/// Principal lineage and annual sequence, and each calls this first inside its
/// Serializable transaction so the sequence row is locked before anything
/// else. The allocation stays in memory until <c>SaveChanges</c>, so a creator
/// that returns early releases the number.
/// </summary>
internal static class CaseIdentityAllocator
{
    private static readonly TimeZoneInfo LondonTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    public static async Task<AllocatedCaseIdentity> AllocateAsync(
        PegasusDbContext context,
        PrincipalEntity principal,
        CaseType caseType,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        var year = TimeZoneInfo.ConvertTime(createdAtUtc, LondonTimeZone).Year;
        var lineageId = principal.SequenceLineageId;
        // UPDLOCK, HOLDLOCK: concurrent creators on one lineage queue here
        // instead of both reading the row shared and deadlocking on the update.
        // A missing row takes a range lock, which serialises its insert too.
        var sequence = context.Database.IsSqlServer()
            ? await context.CaseSequences
                .FromSqlInterpolated($"""
                    SELECT * FROM [CaseSequences] WITH (UPDLOCK, HOLDLOCK)
                    WHERE [SequenceLineageId] = {lineageId} AND [Year] = {year}
                    """)
                .SingleOrDefaultAsync(cancellationToken)
            : await context.CaseSequences.SingleOrDefaultAsync(
                item => item.SequenceLineageId == lineageId && item.Year == year,
                cancellationToken);
        if (sequence is null)
        {
            sequence = new CaseSequenceEntity
            {
                SequenceLineageId = lineageId,
                Year = year,
                LastAllocatedSequence = 0
            };
            context.CaseSequences.Add(sequence);
        }

        var number = checked(++sequence.LastAllocatedSequence);
        var baseReference = CaseReferenceFormat.Base(principal.Code, year, number);
        return new(year, number, CaseReferenceFormat.CasePo(caseType, baseReference));
    }
}

/// <summary>An allocated Case identity; <see cref="Reference"/> is the finished Case/PO.</summary>
internal sealed record AllocatedCaseIdentity(int Year, int Sequence, string Reference);
