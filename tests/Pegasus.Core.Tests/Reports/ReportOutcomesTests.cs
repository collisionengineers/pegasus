using Pegasus.Core.Identity;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

public sealed class ReportOutcomesTests
{
    private static readonly DateTimeOffset From = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Item O: each report counts once under the outcome it was frozen with,
    /// so the counts add up to Reports produced; an Audit report agrees or
    /// differs with the reviewed report's recorded outcome, and one whose
    /// original is not recorded is neither.
    /// </summary>
    [Fact]
    public async Task CountsReportsByOutcomeAndAuditsByAgreement()
    {
        var qdos = Guid.NewGuid();
        var pch = Guid.NewGuid();
        var report = await new GetReportOutcomes(new Facts(
        [
            new(qdos, "QDOS", AssessmentReportOutcome.Repairable, IsAudit: false, null),
            new(qdos, "QDOS", AssessmentReportOutcome.TotalLoss, IsAudit: true, "total_loss"),
            new(qdos, "QDOS", AssessmentReportOutcome.Repairable, IsAudit: true, "total_loss"),
            new(qdos, "QDOS", AssessmentReportOutcome.CashInLieu, IsAudit: true, null),
            new(pch, "PCH", AssessmentReportOutcome.ContractRepair, IsAudit: false, null)
        ])).ExecuteAsync(Administrator(), From, To, default);

        Assert.Equal(
            [
                new PrincipalReportOutcomes(pch, "PCH", 0, 0, 0, 1, 0, 0),
                new PrincipalReportOutcomes(qdos, "QDOS", 2, 1, 1, 0, 1, 1)
            ],
            report.Rows);
    }

    [Fact]
    public async Task IsForAdministratorsOverAValidPeriodAndRefusesInvalidRows()
    {
        var empty = new GetReportOutcomes(new Facts([]));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            empty.ExecuteAsync(ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]), From, To, default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            empty.ExecuteAsync(Administrator(), To, From, default));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new GetReportOutcomes(new Facts([new(Guid.Empty, "QDOS", AssessmentReportOutcome.Repairable, false, null)]))
                .ExecuteAsync(Administrator(), From, To, default));
    }

    [Theory]
    [InlineData(ReportPeriod.ThisMonth, "2026-10-01T00:00:00+01:00", "2026-10-09T09:41:00+01:00")]
    [InlineData(ReportPeriod.LastMonth, "2026-09-01T00:00:00+01:00", "2026-10-01T00:00:00+01:00")]
    [InlineData(ReportPeriod.ThisQuarter, "2026-10-01T00:00:00+01:00", "2026-10-09T09:41:00+01:00")]
    [InlineData(ReportPeriod.Last12Months, "2025-10-09T09:41:00+01:00", "2026-10-09T09:41:00+01:00")]
    public void APeriodChoiceCoversWholeLondonMonths(ReportPeriod period, string from, string to)
    {
        var now = DateTimeOffset.Parse("2026-10-09T09:41:00+01:00", System.Globalization.CultureInfo.InvariantCulture);

        var (fromUtc, toUtc) = ReportPeriods.Resolve(period, now);

        Assert.Equal(DateTimeOffset.Parse(from, System.Globalization.CultureInfo.InvariantCulture), fromUtc);
        Assert.Equal(DateTimeOffset.Parse(to, System.Globalization.CultureInfo.InvariantCulture), toUtc);
        Assert.True(ReportPeriods.IsValid(fromUtc, toUtc));
    }

    /// <summary>#1153: a leap day and a BST start with a GMT end make the same London time a year earlier 366 days and an hour away.</summary>
    [Fact]
    public void Last12MonthsAcrossALeapDayAndTheClockChangeStaysWithinTheLimit()
    {
        var now = new DateTimeOffset(2028, 10, 29, 10, 0, 0, TimeSpan.Zero);

        var (fromUtc, toUtc) = ReportPeriods.Resolve(ReportPeriod.Last12Months, now);

        Assert.Equal(now - GetEngineerActivityReport.MaximumPeriod, fromUtc);
        Assert.Equal(now, toUtc);
        Assert.True(ReportPeriods.IsValid(fromUtc, toUtc));
    }

    [Fact]
    public void ThePreviousPeriodIsTheSameLengthJustBefore()
    {
        Assert.Equal((From.AddDays(-30), From), ReportPeriods.Previous(From, To));
        Assert.False(ReportPeriods.IsValid(To, From));
        Assert.False(ReportPeriods.IsValid(From, From.AddDays(367)));
    }

    private static ActionActor Administrator() => ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    private sealed class Facts(IReadOnlyList<ReportOutcomeFact> facts) : IReportOutcomeQueries
    {
        public Task<IReadOnlyList<ReportOutcomeFact>> GetAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken) =>
            Task.FromResult(facts);
    }
}
