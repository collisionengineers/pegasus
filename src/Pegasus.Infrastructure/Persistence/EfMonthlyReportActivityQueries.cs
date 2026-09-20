using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core;
using Pegasus.Core.Assessment;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// MI-02's periods: per Principal and London month, the confirmed report and
/// fee-note artifacts produced, the reports sent, and the agreed fees on the
/// Cases whose reports were produced that month (each Case's fee counted once,
/// in the month of its first report). Reads the same records as the
/// per-Principal report so the two agree.
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
            select new ArtifactRow(@case.PrincipalId, @case.Principal.Code, @case.Id, generation.GeneratedAtUtc, artifact.Kind))
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
        var fees = caseIds.Length == 0
            ? new Dictionary<Guid, decimal>()
            : (await db.CaseAssessmentFields.AsNoTracking()
                .Where(field => caseIds.Contains(field.CaseId) && field.FieldPath == AssessmentVocabulary.AgreedFee)
                .Select(field => new { field.CaseId, field.Value })
                .ToListAsync(cancellationToken))
                .Where(field => decimal.TryParse(field.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                .ToDictionary(field => field.CaseId, field => decimal.Parse(field.Value, NumberStyles.Number, CultureInfo.InvariantCulture));

        // A Case's fee belongs to the month of its first report in the period.
        var feeMonthByCase = artifacts
            .Where(x => x.Kind == nameof(CaseReportArtifactKind.AssessmentReport))
            .GroupBy(x => x.CaseId)
            .ToDictionary(group => group.Key, group => MonthOf(group.Min(x => x.GeneratedAtUtc)));

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
                        && artifacts.Any(x => x.CaseId == pair.Key && x.PrincipalId == key.PrincipalId)
                        && fees.ContainsKey(pair.Key))
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

    private sealed record ArtifactRow(Guid PrincipalId, string Code, Guid CaseId, DateTimeOffset GeneratedAtUtc, string Kind);

    private sealed record SentRow(Guid PrincipalId, string Code, DateTimeOffset ObservedSentAtUtc);
}
