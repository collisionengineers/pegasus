using Pegasus.Core.Identity;

namespace Pegasus.Core.Reports;

/// <summary>
/// One confirmed report produced in the period, as MI-02 counts it: its
/// Principal, the outcome frozen in its snapshot, whether it is an Audit
/// report (<c>CaseWorkPolicy.IsAuditReport</c>), and for an Audit report the
/// recorded outcome of the report it reviews (<see cref="CaseListPolicy.OriginalOutcomeCode"/>).
/// </summary>
public sealed record ReportOutcomeFact(
    Guid PrincipalId,
    string PrincipalCode,
    AssessmentReportOutcome Outcome,
    bool IsAudit,
    string? OriginalOutcomeCode);

public interface IReportOutcomeQueries
{
    Task<IReadOnlyList<ReportOutcomeFact>> GetAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);
}

/// <summary>
/// One Principal's reports produced in the period by outcome, and its Audit
/// reports that agree or differ with the report they review. The outcome
/// counts add up to the Principal's Reports produced.
/// </summary>
public sealed record PrincipalReportOutcomes(
    Guid PrincipalId,
    string PrincipalCode,
    int Repairable,
    int TotalLoss,
    int CashInLieu,
    int ContractRepair,
    int Agrees,
    int Differs);

public sealed record ReportOutcomesReport(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    IReadOnlyList<PrincipalReportOutcomes> Rows);

/// <summary>Outcomes (item O, 9 October 2026): the period's reports by outcome, with Audit agreement.</summary>
public sealed class GetReportOutcomes(IReportOutcomeQueries queries)
{
    /// <summary>The outcome columns in the order the page, CSV and workbook show them.</summary>
    public static IReadOnlyList<AssessmentReportOutcome> Order { get; } =
    [
        AssessmentReportOutcome.Repairable,
        AssessmentReportOutcome.TotalLoss,
        AssessmentReportOutcome.CashInLieu,
        AssessmentReportOutcome.ContractRepair
    ];

    public async Task<ReportOutcomesReport> ExecuteAsync(
        ActionActor actor,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.ViewOperationalReports);
        if (!ReportPeriods.IsValid(fromUtc, toUtc))
        {
            throw new ArgumentOutOfRangeException(nameof(toUtc));
        }

        var facts = await queries.GetAsync(fromUtc, toUtc, cancellationToken);
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.Any(fact => fact.PrincipalId == Guid.Empty
            || string.IsNullOrWhiteSpace(fact.PrincipalCode)
            || !Enum.IsDefined(fact.Outcome)))
        {
            throw new InvalidDataException("The report outcome query returned an invalid row.");
        }

        return new(fromUtc, toUtc, facts
            .GroupBy(fact => (fact.PrincipalId, fact.PrincipalCode))
            .Select(group =>
            {
                var agreement = group
                    .Where(fact => fact.IsAudit && fact.OriginalOutcomeCode is not null)
                    .Select(fact => AssessmentReportOutcomes.Parse(fact.OriginalOutcomeCode!) == fact.Outcome)
                    .ToList();
                return new PrincipalReportOutcomes(
                    group.Key.PrincipalId,
                    group.Key.PrincipalCode,
                    group.Count(fact => fact.Outcome == AssessmentReportOutcome.Repairable),
                    group.Count(fact => fact.Outcome == AssessmentReportOutcome.TotalLoss),
                    group.Count(fact => fact.Outcome == AssessmentReportOutcome.CashInLieu),
                    group.Count(fact => fact.Outcome == AssessmentReportOutcome.ContractRepair),
                    agreement.Count(agrees => agrees),
                    agreement.Count(agrees => !agrees));
            })
            .OrderBy(row => row.PrincipalCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.PrincipalId)
            .ToArray());
    }
}
