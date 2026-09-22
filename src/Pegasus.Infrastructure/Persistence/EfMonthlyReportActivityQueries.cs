using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// MI-02's periods: per Principal and London month, the confirmed report and
/// fee-note artifacts produced, the reports sent, and the agreed fees on the
/// Cases whose first reports were produced in the selected period (each Case's
/// fee counted once, in the month of its first qualifying report globally). Reads the same
/// records as the per-Principal report so the two agree.
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
            where generation.GeneratedAtUtc >= fromUtc && generation.GeneratedAtUtc < toUtc
                && artifact.Sha256 != null
                && artifact.Sha256 == documentVersion.Sha256
                && documentVersion.CustodyStatus == Pegasus.Core.Documents.DocumentCustodyStatus.Confirmed
            select new ArtifactRow(
                @case.PrincipalId,
                @case.Principal.Code,
                @case.Id,
                generation.GeneratedAtUtc,
                artifact.Kind))
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
            .Join(db.Cases.AsNoTracking(), x => x.generation.CaseId, @case => @case.Id,
                (x, @case) => new SentRow(@case.PrincipalId, @case.Principal.Code, x.operation.ObservedSentAtUtc!.Value))
            .ToListAsync(cancellationToken);

        var caseIds = artifacts.Select(x => x.CaseId).Distinct().ToArray();
        var firstReports = caseIds.Length == 0
            ? []
            : await (
                from generation in db.Set<CaseReportGenerationEntity>().AsNoTracking()
                join artifact in db.Set<GeneratedCaseArtifactEntity>().AsNoTracking()
                    on generation.Id equals artifact.GenerationId
                join documentVersion in db.Set<DocumentVersionEntity>().AsNoTracking()
                    on artifact.VersionId equals (Guid?)documentVersion.Id
                where caseIds.Contains(generation.CaseId)
                    && artifact.Kind == nameof(CaseReportArtifactKind.AssessmentReport)
                    && artifact.Sha256 != null
                    && artifact.Sha256 == documentVersion.Sha256
                    && documentVersion.CustodyStatus == Pegasus.Core.Documents.DocumentCustodyStatus.Confirmed
                select new FrozenReportRow(
                    generation.CaseId,
                    generation.Id,
                    generation.GeneratedAtUtc,
                    generation.SnapshotJson))
                .ToListAsync(cancellationToken);

        var firstReportByCase = firstReports
            .GroupBy(x => x.CaseId)
            .Select(group => group
                .OrderBy(x => x.GeneratedAtUtc)
                .ThenBy(x => x.GenerationId)
                .First())
            .Where(first => FirstReportFeeAttribution.InPeriod(first.GeneratedAtUtc, fromUtc, toUtc))
            .ToDictionary(first => first.CaseId);
        var fees = firstReportByCase.ToDictionary(
            pair => pair.Key,
            pair => FrozenFeeOf(pair.Value));
        var feeMonthByCase = firstReportByCase.ToDictionary(
            pair => pair.Key,
            pair => MonthOf(pair.Value.GeneratedAtUtc));

        var keys = artifacts.Select(x => (x.PrincipalId, x.Code, Month: MonthOf(x.GeneratedAtUtc)))
            .Concat(sent.Select(x => (x.PrincipalId, x.Code, Month: MonthOf(x.ObservedSentAtUtc))))
            .Distinct();
        return keys.Select(key => new MonthlyReportActivity(
                key.PrincipalId,
                key.Code,
                key.Month.Year,
                key.Month.Month,
                artifacts.Count(x => x.PrincipalId == key.PrincipalId && MonthOf(x.GeneratedAtUtc) == key.Month
                    && x.Kind == nameof(CaseReportArtifactKind.AssessmentReport)),
                artifacts.Count(x => x.PrincipalId == key.PrincipalId && MonthOf(x.GeneratedAtUtc) == key.Month
                    && x.Kind == nameof(CaseReportArtifactKind.FeeNote)),
                sent.Count(x => x.PrincipalId == key.PrincipalId && MonthOf(x.ObservedSentAtUtc) == key.Month),
                feeMonthByCase
                    .Where(pair => pair.Value == key.Month
                        && artifacts.Any(x => x.CaseId == pair.Key && x.PrincipalId == key.PrincipalId))
                    .Sum(pair => fees[pair.Key])))
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
        Guid CaseId,
        DateTimeOffset GeneratedAtUtc,
        string Kind);

    private sealed record FrozenReportRow(
        Guid CaseId,
        Guid GenerationId,
        DateTimeOffset GeneratedAtUtc,
        string SnapshotJson);

    private sealed record SentRow(Guid PrincipalId, string Code, DateTimeOffset ObservedSentAtUtc);
}
