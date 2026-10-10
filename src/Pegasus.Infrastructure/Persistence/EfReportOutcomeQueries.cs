using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Outcomes (item O): every confirmed report produced in the period, read the
/// way MI-02 confirms it (the artifact's hash matches its custody-confirmed
/// document version), with the outcome frozen in its snapshot. An Audit
/// report carries the reviewed report's recorded outcome, read from the Case's
/// own work by the Case list's rule (<see cref="CaseListPolicy.OriginalOutcomeCode(Pegasus.Core.Cases.CaseType, CaseListWorkFacts)"/>).
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
        var reports = await (
            from generation in db.Set<CaseReportGenerationEntity>().AsNoTracking()
            join artifact in db.Set<GeneratedCaseArtifactEntity>().AsNoTracking()
                on generation.Id equals artifact.GenerationId
            join documentVersion in db.Set<DocumentVersionEntity>().AsNoTracking()
                on artifact.VersionId equals (Guid?)documentVersion.Id
            join @case in db.Cases.AsNoTracking() on generation.CaseId equals @case.Id
            join work in db.CaseWorks.AsNoTracking() on generation.WorkId equals work.Id
            where generation.GeneratedAtUtc >= fromUtc && generation.GeneratedAtUtc < toUtc
                && artifact.Kind == nameof(CaseReportArtifactKind.AssessmentReport)
                && artifact.Sha256 != null
                && artifact.Sha256 == documentVersion.Sha256
                && documentVersion.CustodyStatus == DocumentCustodyStatus.Confirmed
            select new ReportRow(
                @case.PrincipalId,
                @case.Principal.Code,
                @case.Id,
                @case.Type,
                generation.Id,
                work.Kind,
                SqlJson.Value(generation.SnapshotJson, OutcomePath)))
            .ToListAsync(cancellationToken);

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
                report.Code,
                OutcomeOf(report),
                report.IsAudit,
                report.IsAudit
                    ? CaseListPolicy.OriginalOutcomeCode(
                        CaseTypeCodes.Parse(report.CaseType),
                        primaryFacts.GetValueOrDefault(report.CaseId) ?? CaseListWorkFacts.Empty)
                    : null))
            .ToList();
    }

    /// <summary>The snapshot writes the outcome as its number; a snapshot without one is unreadable, never a zero.</summary>
    private static AssessmentReportOutcome OutcomeOf(ReportRow report) =>
        int.TryParse(report.Outcome, NumberStyles.None, CultureInfo.InvariantCulture, out var outcome)
            ? (AssessmentReportOutcome)outcome
            : throw new InvalidDataException(
                $"The frozen snapshot of report generation '{report.GenerationId}' has no outcome.");

    private sealed record ReportRow(
        Guid PrincipalId,
        string Code,
        Guid CaseId,
        string CaseType,
        Guid GenerationId,
        string WorkKind,
        string? Outcome)
    {
        public bool IsAudit => CaseWorkKinds.IsAuditReport(CaseType, WorkKind);
    }
}
