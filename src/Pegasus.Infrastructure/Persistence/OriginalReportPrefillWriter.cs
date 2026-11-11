using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Fills an Audit's Original report cells, and its empty mileage (R930),
/// inside the caller's transaction — the Case's acceptance or the Mark as
/// original report — as <see cref="OriginalReportPrefillPolicy"/> decides. A
/// cell staff recorded is never touched and no cell is cleared; each value lands as
/// <see cref="OriginalReportPrefillPolicy.RecorderId"/>, so the section tags it
/// Extracted until staff change it.
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
            if (!AssessmentPolicy.FillLands(AssessmentFieldWriter.RecordedByKind(existing), existing?.RecordedBy)
                || string.Equals(existing?.Value, value, StringComparison.Ordinal))
            {
                continue;
            }

            var before = existing?.Value;
            AssessmentFieldWriter.Write(
                context, workId, existing, path, value,
                ActorKind.Automation, OriginalReportPrefillPolicy.RecorderId, now);
            written[path] = (before, value);
        }

        return written;
    }

    /// <summary>
    /// Fills the work's empty mileage, with its unit, from the mileage the
    /// report states (R930), on the snapshot's own field list: the caller's
    /// acceptance snapshot before its gate is evaluated, or a tracked one. It
    /// fills where the work holds neither an extracted fact nor a confirmed
    /// value (<see cref="VehicleLookupFillPolicy.Fills"/>), so a mileage from
    /// the instruction, a lookup or staff is never replaced. It lands as
    /// intake evidence, the provenance FRD-06 gives an engineer report, so the
    /// report prints the principal mileage sentence. Returns the fields it
    /// filled, by name, with their values: none, or the mileage and, where
    /// the work holds no unit of its own, its unit.
    /// </summary>
    public static Dictionary<string, string> FillMileage(
        CaseDataSnapshotEntity snapshot,
        OriginalReportReading? reading,
        string sourceIdentity,
        string sourceLabel)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var filled = new Dictionary<string, string>(StringComparer.Ordinal);
        if (reading is not { Mileage: { } mileage, MileageUnit: { } unit }
            || !Fills(snapshot, CaseDataFieldNames.VehicleMileage))
        {
            return filled;
        }

        Add(CaseDataFieldNames.VehicleMileage, CaseDataCodes.Integer, mileage.ToString(CultureInfo.InvariantCulture));
        if (Fills(snapshot, CaseDataFieldNames.VehicleMileageUnit))
        {
            Add(CaseDataFieldNames.VehicleMileageUnit, CaseDataCodes.Text, unit);
        }

        return filled;

        void Add(string fieldName, string valueType, string value)
        {
            snapshot.Fields.Add(new()
            {
                WorkId = snapshot.WorkId,
                Snapshot = snapshot,
                FieldName = fieldName,
                ValueKind = CaseDataCodes.Fact,
                ValueType = valueType,
                Value = value,
                SourceKind = CaseDataCodes.IntakeEvidence,
                SourceIdentity = sourceIdentity,
                SourceLabel = sourceLabel,
                PolicyKey = OriginalReportPrefillPolicy.MileagePolicyKey,
                PolicyVersion = OriginalReportPrefillPolicy.MileagePolicyVersion
            });
            filled[fieldName] = value;
        }
    }

    private static bool Fills(CaseDataSnapshotEntity snapshot, string fieldName) =>
        VehicleLookupFillPolicy.Fills(
            snapshot.Fields.Any(item => item.FieldName == fieldName && item.ValueKind == CaseDataCodes.Fact),
            snapshot.Fields.Any(item => item.FieldName == fieldName && item.ValueKind == CaseDataCodes.Confirmed));
}
