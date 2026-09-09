using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Allocates the permanent portion of a Case identity. Receipt-backed and
/// receiptless creation share the same Principal lineage and annual sequence.
/// </summary>
internal static class CaseIdentityAllocator
{
    private static readonly TimeZoneInfo LondonTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    public static async Task<AllocatedCaseIdentity> AllocateAsync(
        PegasusDbContext context,
        PrincipalEntity principal,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        var year = TimeZoneInfo.ConvertTime(createdAtUtc, LondonTimeZone).Year;
        var sequence = await context.CaseSequences.SingleOrDefaultAsync(
            item => item.SequenceLineageId == principal.SequenceLineageId && item.Year == year,
            cancellationToken);
        if (sequence is null)
        {
            sequence = new CaseSequenceEntity
            {
                SequenceLineageId = principal.SequenceLineageId,
                Year = year,
                LastAllocatedSequence = 0
            };
            context.CaseSequences.Add(sequence);
        }

        if (sequence.LastAllocatedSequence >= 999)
        {
            throw new CaseIdentitySequenceExhaustedException(principal.Code, year);
        }

        var number = ++sequence.LastAllocatedSequence;
        return new(year, number, $"{principal.Code}{year % 100:00}{number:000}");
    }
}

internal sealed record AllocatedCaseIdentity(int Year, int Sequence, string Reference);
