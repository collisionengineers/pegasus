using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Outcomes (item O; R673): every report first sent in the period
/// (<see cref="ReportSends"/>), with the outcome frozen in the report that
/// send carried. An Audit report carries the reviewed report's recorded
/// outcome, read from the Case's own work by the Case list's rule
/// (<see cref="CaseListPolicy.OriginalOutcomeCode(Pegasus.Core.Cases.CaseType, CaseListWorkFacts)"/>).
/// </summary>
internal sealed class EfReportOutcomeQueries(
    IDbContextFactory<PegasusDbContext> factory) : IReportOutcomeQueries
{
    /// <summary>The outcome the report was frozen with; read in SQL so the snapshot itself never leaves the database.</summary>
    private const string OutcomePath = "$.report.outcome";

    public async Task<IReadOnlyList<ReportOutcomeFact>> GetAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var reports = (await ReportSends.InPeriodAsync(db, fromUtc, toUtc, cancellationToken))
            .Where(send => send.IsFirst)
            .ToList();
        var generationIds = reports.Select(report => report.GenerationId).ToHashSet();
        var outcomes = generationIds.Count == 0
            ? new Dictionary<Guid, string?>()
            : (await FrozenReportSnapshot.ReadAsync(
                db.Set<CaseReportGenerationEntity>().AsNoTracking()
                    .Where(generation => EF.Parameter(generationIds).Contains(generation.Id))
                    .Select(generation => new OutcomeRow
                    {
                        GenerationId = generation.Id,
                        Outcome = SqlJson.Value(generation.SnapshotJson, OutcomePath)
                    }),
                cancellationToken))
                .ToDictionary(row => row.GenerationId, row => row.Outcome);

        var auditCaseIds = reports.Where(report => report.IsAudit).Select(report => report.CaseId).Distinct().ToArray();
        var paths = new[] { AssessmentVocabulary.Outcome, AssessmentVocabulary.OriginalReportOutcome };
        var primaryFacts = auditCaseIds.Length == 0
            ? new Dictionary<Guid, CaseListWorkFacts>()
            : (await (
                from field in db.CaseAssessmentFields.AsNoTracking()
                join work in db.CaseWorks.AsNoTracking() on field.WorkId equals work.Id
                where auditCaseIds.Contains(work.CaseId)
                    && work.Kind == CaseWorkKinds.Primary
                    && paths.Contains(field.FieldPath)
                select new { work.CaseId, field.FieldPath, field.Value })
                .ToListAsync(cancellationToken))
                .GroupBy(field => field.CaseId)
                .ToDictionary(
                    group => group.Key,
                    group => new CaseListWorkFacts(group.ToDictionary(field => field.FieldPath, field => field.Value, StringComparer.Ordinal)));

        return reports
            .Select(report => new ReportOutcomeFact(
                report.PrincipalId,
                report.PrincipalCode,
                OutcomeOf(report.GenerationId, outcomes[report.GenerationId]),
                report.IsAudit,
                report.IsAudit
                    ? CaseListPolicy.OriginalOutcomeCode(
                        CaseTypeCodes.Parse(report.CaseType),
                        primaryFacts.GetValueOrDefault(report.CaseId) ?? CaseListWorkFacts.Empty)
                    : null))
            .ToList();
    }

    /// <summary>The snapshot writes the outcome as its number; a snapshot without one is unreadable, never a zero.</summary>
    private static AssessmentReportOutcome OutcomeOf(Guid generationId, string? outcome) =>
        int.TryParse(outcome, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? (AssessmentReportOutcome)value
            : throw new InvalidDataException(
                $"The frozen snapshot of report generation '{generationId}' has no outcome.");

    private sealed class OutcomeRow
    {
        public Guid GenerationId { get; init; }

        public string? Outcome { get; init; }
    }
}
