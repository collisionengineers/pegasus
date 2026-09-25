using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Fills an Audit's Original report cells inside the caller's transaction —
/// the Case's acceptance or the Mark as original report — as
/// <see cref="OriginalReportPrefillPolicy"/> decides. A staff-confirmed cell is
/// never touched and no cell is cleared; each value lands unconfirmed as
/// <see cref="OriginalReportPrefillPolicy.RecorderId"/>, so the section tags it
/// Extracted until a staff Save confirms it.
/// </summary>
internal static class OriginalReportPrefillWriter
{
    /// <summary>
    /// Returns the cells it wrote, each with its value before and after, for
    /// the caller's history record.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, (string? Before, string After)>> ApplyAsync(
        PegasusDbContext context,
        Guid workId,
        OriginalReportReading? reading,
        AuditAssessment? intakeVerdict,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var writes = OriginalReportPrefillPolicy.Writes(reading, intakeVerdict);
        var written = new Dictionary<string, (string? Before, string After)>(StringComparer.Ordinal);
        if (writes.Count == 0)
        {
            return written;
        }

        var paths = writes.Keys.ToArray();
        var rows = await context.CaseAssessmentFields
            .Where(item => item.WorkId == workId && paths.Contains(item.FieldPath))
            .ToListAsync(cancellationToken);
        foreach (var (path, value) in writes)
        {
            var existing = rows.SingleOrDefault(item => item.FieldPath == path);
            if (!OriginalReportPrefillPolicy.Fills(existing?.ConfirmedBy is not null)
                || string.Equals(existing?.Value, value, StringComparison.Ordinal))
            {
                continue;
            }

            var before = existing?.Value;
            AssessmentFieldWriter.Write(
                context, workId, existing, path, value,
                ActorKind.Automation, OriginalReportPrefillPolicy.RecorderId, now, confirmedBy: null);
            written[path] = (before, value);
        }

        return written;
    }
}
