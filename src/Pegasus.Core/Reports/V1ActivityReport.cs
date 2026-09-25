using Pegasus.Core.Identity;

namespace Pegasus.Core.Reports;

public sealed record PrincipalReportActivity(
    Guid PrincipalId,
    string PrincipalCode,
    int GenerationEvents,
    int GeneratedArtifacts,
    int Sent,
    int Ready,
    int MissingOriginForGeneratedTurnaround,
    int MissingOriginForReadyTurnaround,
    int MissingOriginForSentTurnaround,
    int MissingSentActor,
    TimeSpan? AverageReceivedToGeneration,
    TimeSpan? AverageReceivedToGeneratedArtifact,
    TimeSpan? AverageReceivedToReady,
    TimeSpan? AverageReceivedToSent,
    int CurrentTriage,
    DateTimeOffset? OldestCurrentTriageCreatedAtUtc,
    int CurrentHeldCases,
    DateTimeOffset? OldestHeldAtUtc,
    int HeldWithoutRecordedHoldEvent,
    IReadOnlyList<PrincipalReportArtifactTypeActivity> ArtifactTypes,
    decimal AgreedFeeTotal = 0,
    int AuditReportsProduced = 0,
    int AuditSent = 0,
    decimal AuditAgreedFeeTotal = 0)
{
    public int ReportsProduced => ArtifactTypes
        .Where(type => type.Kind == nameof(CaseReportArtifactKind.AssessmentReport))
        .Sum(type => type.Generated);

    // MI-02: each total splits into Inspection and Audit reports
    // (CaseWorkPolicy.IsAuditReport decides which).
    public int InspectionReportsProduced => ReportsProduced - AuditReportsProduced;

    public int InspectionSent => Sent - AuditSent;

    public decimal InspectionAgreedFeeTotal => AgreedFeeTotal - AuditAgreedFeeTotal;
}

/// <summary>The frozen fee belongs only to the exact period containing the first confirmed report.</summary>
public static class FirstReportFeeAttribution
{
    public static bool InPeriod(DateTimeOffset firstReportAtUtc, DateTimeOffset fromUtc, DateTimeOffset toUtc) =>
        firstReportAtUtc >= fromUtc && firstReportAtUtc < toUtc;
}

public sealed record PrincipalReportArtifactTypeActivity(
    string Kind,
    int Generated,
    int PendingOrFailed);

public sealed record PrincipalReportActivityReport(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    IReadOnlyList<PrincipalReportActivity> Rows);

public interface IV1ActivityReportQueries
{
    Task<IReadOnlyList<PrincipalReportActivity>> GetAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);
}

public sealed class GetV1ActivityReport(IV1ActivityReportQueries queries)
{
    public async Task<PrincipalReportActivityReport> ExecuteAsync(
        ActionActor actor,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.ViewOperationalReports);
        if (fromUtc >= toUtc || toUtc - fromUtc > TimeSpan.FromDays(366))
        {
            throw new ArgumentOutOfRangeException(nameof(toUtc));
        }

        var rows = await queries.GetAsync(fromUtc, toUtc, cancellationToken);
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Any(IsInvalid)
            || rows.Select(x => x.PrincipalId).Distinct().Count() != rows.Count)
        {
            throw new InvalidDataException("The principal report query returned an invalid row.");
        }

        return new(fromUtc, toUtc, rows
            .OrderBy(x => x.PrincipalCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.PrincipalId)
            .ToArray());
    }

    private static bool IsInvalid(PrincipalReportActivity row) =>
        row.PrincipalId == Guid.Empty
        || string.IsNullOrWhiteSpace(row.PrincipalCode)
        || row.GenerationEvents < 0
        || row.GeneratedArtifacts < 0
        || row.Sent < 0
        || row.Ready < 0
        || row.MissingOriginForGeneratedTurnaround < 0
        || row.MissingOriginForGeneratedTurnaround > row.GeneratedArtifacts
        || row.MissingOriginForReadyTurnaround < 0
        || row.MissingOriginForReadyTurnaround > row.Ready
        || row.MissingOriginForSentTurnaround < 0
        || row.MissingOriginForSentTurnaround > row.Sent
        || row.MissingSentActor < 0
        || row.MissingSentActor > row.Sent
        || row.AverageReceivedToGeneration < TimeSpan.Zero
        || row.AverageReceivedToGeneratedArtifact < TimeSpan.Zero
        || row.AverageReceivedToReady < TimeSpan.Zero
        || row.AverageReceivedToSent < TimeSpan.Zero
        || row.CurrentTriage < 0
        || row.CurrentHeldCases < 0
        || row.HeldWithoutRecordedHoldEvent < 0
        || row.HeldWithoutRecordedHoldEvent > row.CurrentHeldCases
        || row.AgreedFeeTotal < 0
        || row.ArtifactTypes is null
        || row.ArtifactTypes.Any(x => string.IsNullOrWhiteSpace(x.Kind)
            || x.Generated < 0 || x.PendingOrFailed < 0)
        // MI-02's Audit share is part of each total.
        || row.AuditReportsProduced < 0
        || row.AuditReportsProduced > row.ReportsProduced
        || row.AuditSent < 0
        || row.AuditSent > row.Sent
        || row.AuditAgreedFeeTotal < 0
        || row.AuditAgreedFeeTotal > row.AgreedFeeTotal;
}
