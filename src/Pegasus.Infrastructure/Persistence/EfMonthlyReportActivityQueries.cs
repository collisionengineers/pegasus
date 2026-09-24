using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// MI-02's periods: per Principal and London month, the confirmed report and
/// fee-note artifacts produced, the reports sent, and the agreed fees on the
/// works whose first reports were produced in the selected period (each work's
/// fee counted once, in the month of its first qualifying report globally), each
/// with its Audit share. Reads the same records as the per-Principal report so
/// the two agree.
/// </summary>
internal sealed class EfMonthlyReportActivityQueries(
    IDbContextFactory<PegasusDbContext> factory) : IMonthlyReportActivityQueries
{
    public async Task<IReadOnlyList<MonthlyReportActivity>> GetAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        // A confirmed artifact is the same rule the per-Principal report uses:
        // its document version exists, carries the artifact's hash and is in
        // confirmed custody.
        var artifacts = (await (
            from generation in db.Set<CaseReportGenerationEntity>().AsNoTracking()
            join artifact in db.Set<GeneratedCaseArtifactEntity>().AsNoTracking()
                on generation.Id equals artifact.GenerationId
            join documentVersion in db.Set<DocumentVersionEntity>().AsNoTracking()
                on artifact.VersionId equals (Guid?)documentVersion.Id
            join @case in db.Cases.AsNoTracking() on generation.CaseId equals @case.Id
            join work in db.CaseWorks.AsNoTracking() on generation.WorkId equals work.Id
            where generation.GeneratedAtUtc >= fromUtc && generation.GeneratedAtUtc < toUtc
                && artifact.Sha256 != null
                && artifact.Sha256 == documentVersion.Sha256
                && documentVersion.CustodyStatus == Pegasus.Core.Documents.DocumentCustodyStatus.Confirmed
            select new ArtifactRow(
                @case.PrincipalId,
                @case.Principal.Code,
                generation.WorkId,
                generation.GeneratedAtUtc,
                artifact.Kind,
                @case.Type,
                work.Kind))
            .ToListAsync(cancellationToken));

        var sent = await db.Set<StaffMailSendOperationEntity>().AsNoTracking()
            .Where(x => x.Purpose == StaffMailPurpose.CaseReport
                && x.State == StaffMailState.Sent
                && x.ObservedSentAtUtc >= fromUtc
                && x.ObservedSentAtUtc < toUtc)
            .Join(db.Set<CaseReportGenerationEntity>().AsNoTracking(),
                operation => operation.ContextId,
                generation => generation.Id,
                (operation, generation) => new { operation, generation })
            .Join(db.CaseWorks.AsNoTracking(), x => x.generation.WorkId, work => work.Id,
                (x, work) => new { x.operation, x.generation, WorkKind = work.Kind })
            .Join(db.Cases.AsNoTracking(), x => x.generation.CaseId, @case => @case.Id,
                (x, @case) => new SentRow(
                    @case.PrincipalId,
                    @case.Principal.Code,
                    x.operation.ObservedSentAtUtc!.Value,
                    @case.Type,
                    x.WorkKind))
            .ToListAsync(cancellationToken);

        // The fee belongs to each work's first confirmed report: an
        // Inspection + Audit Case's Audit carries its own fee.
        var workIds = artifacts.Select(x => x.WorkId).Distinct().ToArray();
        var firstReports = workIds.Length == 0
            ? []
            : await (
                from generation in db.Set<CaseReportGenerationEntity>().AsNoTracking()
                join artifact in db.Set<GeneratedCaseArtifactEntity>().AsNoTracking()
                    on generation.Id equals artifact.GenerationId
                join documentVersion in db.Set<DocumentVersionEntity>().AsNoTracking()
                    on artifact.VersionId equals (Guid?)documentVersion.Id
                where workIds.Contains(generation.WorkId)
                    && artifact.Kind == nameof(CaseReportArtifactKind.AssessmentReport)
                    && artifact.Sha256 != null
                    && artifact.Sha256 == documentVersion.Sha256
                    && documentVersion.CustodyStatus == Pegasus.Core.Documents.DocumentCustodyStatus.Confirmed
                select new FrozenReportRow(
                    generation.WorkId,
                    generation.Id,
                    generation.GeneratedAtUtc,
                    generation.SnapshotJson))
                .ToListAsync(cancellationToken);

        var firstReportByWork = firstReports
            .GroupBy(x => x.WorkId)
            .Select(group => group
                .OrderBy(x => x.GeneratedAtUtc)
                .ThenBy(x => x.GenerationId)
                .First())
            .Where(first => FirstReportFeeAttribution.InPeriod(first.GeneratedAtUtc, fromUtc, toUtc))
            .ToDictionary(first => first.WorkId);
        var fees = firstReportByWork.ToDictionary(
            pair => pair.Key,
            pair => FrozenFeeOf(pair.Value));
        var feeMonthByWork = firstReportByWork.ToDictionary(
            pair => pair.Key,
            pair => MonthOf(pair.Value.GeneratedAtUtc));
        var auditWorkIds = artifacts
            .Where(x => x.IsAudit)
            .Select(x => x.WorkId)
            .ToHashSet();

        var keys = artifacts.Select(x => (x.PrincipalId, x.Code, Month: MonthOf(x.GeneratedAtUtc)))
            .Concat(sent.Select(x => (x.PrincipalId, x.Code, Month: MonthOf(x.ObservedSentAtUtc))))
            .Distinct();
        return keys.Select(key =>
            {
                var feeWorkIds = feeMonthByWork
                    .Where(pair => pair.Value == key.Month
                        && artifacts.Any(x => x.WorkId == pair.Key && x.PrincipalId == key.PrincipalId))
                    .Select(pair => pair.Key)
                    .ToList();
                var reports = artifacts
                    .Where(x => x.PrincipalId == key.PrincipalId && MonthOf(x.GeneratedAtUtc) == key.Month
                        && x.Kind == nameof(CaseReportArtifactKind.AssessmentReport))
                    .ToList();
                var sends = sent
                    .Where(x => x.PrincipalId == key.PrincipalId && MonthOf(x.ObservedSentAtUtc) == key.Month)
                    .ToList();
                return new MonthlyReportActivity(
                    key.PrincipalId,
                    key.Code,
                    key.Month.Year,
                    key.Month.Month,
                    reports.Count,
                    artifacts.Count(x => x.PrincipalId == key.PrincipalId && MonthOf(x.GeneratedAtUtc) == key.Month
                        && x.Kind == nameof(CaseReportArtifactKind.FeeNote)),
                    sends.Count,
                    feeWorkIds.Sum(workId => fees[workId]),
                    reports.Count(x => x.IsAudit),
                    sends.Count(x => x.IsAudit),
                    feeWorkIds.Where(auditWorkIds.Contains).Sum(workId => fees[workId]));
            })
            .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
            .ThenBy(x => x.PrincipalCode, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static (int Year, int Month) MonthOf(DateTimeOffset instant)
    {
        var local = LondonCalendar.DateAt(instant);
        return (local.Year, local.Month);
    }

    private static decimal FrozenFeeOf(FrozenReportRow report)
    {
        try
        {
            using var document = JsonDocument.Parse(report.SnapshotJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("agreedFee", out var fee)
                || fee.ValueKind != JsonValueKind.Number
                || !fee.TryGetDecimal(out _))
            {
                throw new InvalidDataException(
                    $"The frozen snapshot of report generation '{report.GenerationId}' has no valid agreed fee.");
            }

            return fee.GetDecimal();
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"The frozen snapshot of report generation '{report.GenerationId}' is unreadable.",
                exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException(
                $"The frozen snapshot of report generation '{report.GenerationId}' is unreadable.",
                exception);
        }
    }

    private sealed record ArtifactRow(
        Guid PrincipalId,
        string Code,
        Guid WorkId,
        DateTimeOffset GeneratedAtUtc,
        string Kind,
        string CaseType,
        string WorkKind)
    {
        /// <summary>MI-02's split: an Audit report (<see cref="CaseWorkKinds.IsAuditReport"/>) or an Inspection report.</summary>
        public bool IsAudit => CaseWorkKinds.IsAuditReport(CaseType, WorkKind);
    }

    private sealed record FrozenReportRow(
        Guid WorkId,
        Guid GenerationId,
        DateTimeOffset GeneratedAtUtc,
        string SnapshotJson);

    private sealed record SentRow(
        Guid PrincipalId,
        string Code,
        DateTimeOffset ObservedSentAtUtc,
        string CaseType,
        string WorkKind)
    {
        public bool IsAudit => CaseWorkKinds.IsAuditReport(CaseType, WorkKind);
    }
}
