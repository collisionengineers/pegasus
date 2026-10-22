using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The one reading of a frozen report snapshot's figures. A figure is read in
/// the database as text (<see cref="SqlJson.Value"/>) and made a number here, so
/// a snapshot without a valid figure stops the read; it is never a zero.
/// </summary>
internal static class FrozenReportSnapshot
{
    /// <summary>SQL Server's error for JSON text that is not well formed.</summary>
    private const int JsonNotWellFormed = 13609;

    /// <summary>Where the snapshot freezes the agreed fee.</summary>
    public const string AgreedFeePath = "$.agreedFee";

    public static decimal? Number(string? value) =>
        decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;

    public static decimal AgreedFee(Guid generationId, string? value) =>
        Number(value)
            ?? throw new InvalidDataException(
                $"The frozen snapshot of report generation '{generationId}' has no valid agreed fee.");

    /// <summary>
    /// Runs a read that takes figures from snapshots. SQL Server raises when a
    /// snapshot is not JSON at all; that leaves as the read's own failure.
    /// </summary>
    public static async Task<List<T>> ReadAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
    {
        try
        {
            return await query.ToListAsync(cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == JsonNotWellFormed)
        {
            throw new InvalidDataException("A frozen report snapshot is unreadable.", exception);
        }
    }

    /// <summary>
    /// Each work's first confirmed AssessmentReport generation globally, kept
    /// only where it falls in the period (<see cref="FirstReportFeeAttribution.InPeriod"/>),
    /// with its agreed fee. One statement: the first in-period generation is
    /// chosen in the database and only it has its fee read from the snapshot,
    /// so a later generation's snapshot is never opened. Generations that
    /// share the earliest instant all come back and the lowest id is the first.
    /// </summary>
    public static async Task<Dictionary<Guid, FirstReportFee>> ReadFirstInPeriodFeesAsync(
        PegasusDbContext db,
        IReadOnlyCollection<Guid> workIds,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        if (workIds.Count == 0)
        {
            return [];
        }

        var confirmed = ConfirmedAssessmentReports(db);
        var ids = workIds.ToArray();
        var rows = await ReadAsync(
            from report in confirmed
            where ids.Contains(report.WorkId)
                && report.GeneratedAtUtc >= fromUtc
                && report.GeneratedAtUtc < toUtc
                && !confirmed.Any(earlier =>
                    earlier.WorkId == report.WorkId && earlier.GeneratedAtUtc < report.GeneratedAtUtc)
            select new FirstReportRow(
                report.WorkId,
                report.GenerationId,
                report.GeneratedAtUtc,
                SqlJson.Value(report.SnapshotJson, AgreedFeePath)),
            cancellationToken);

        return rows
            .GroupBy(row => row.WorkId)
            .Select(group => group.OrderBy(row => row.GenerationId).First())
            .ToDictionary(
                first => first.WorkId,
                first => new FirstReportFee(first.GeneratedAtUtc, AgreedFee(first.GenerationId, first.AgreedFee)));
    }

    /// <summary>A confirmed report: its document version exists, carries the artifact's hash and is in confirmed custody.</summary>
    private static IQueryable<ConfirmedAssessmentReport> ConfirmedAssessmentReports(PegasusDbContext db) =>
        from generation in db.Set<CaseReportGenerationEntity>().AsNoTracking()
        join artifact in db.Set<GeneratedCaseArtifactEntity>().AsNoTracking()
            on generation.Id equals artifact.GenerationId
        join documentVersion in db.Set<DocumentVersionEntity>().AsNoTracking()
            on artifact.VersionId equals (Guid?)documentVersion.Id
        where artifact.Kind == nameof(CaseReportArtifactKind.AssessmentReport)
            && artifact.Sha256 != null
            && artifact.Sha256 == documentVersion.Sha256
            && documentVersion.CustodyStatus == DocumentCustodyStatus.Confirmed
        select new ConfirmedAssessmentReport(
            generation.WorkId,
            generation.Id,
            generation.GeneratedAtUtc,
            generation.SnapshotJson);

    private sealed record ConfirmedAssessmentReport(
        Guid WorkId,
        Guid GenerationId,
        DateTimeOffset GeneratedAtUtc,
        string SnapshotJson);

    private sealed record FirstReportRow(
        Guid WorkId,
        Guid GenerationId,
        DateTimeOffset GeneratedAtUtc,
        string? AgreedFee);
}

/// <summary>A work's first report: when it was generated and the agreed fee frozen in it.</summary>
internal readonly record struct FirstReportFee(DateTimeOffset GeneratedAtUtc, decimal AgreedFee);
