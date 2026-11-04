using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

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
    /// The agreed fee frozen in each named generation: a report's fee is the
    /// one frozen in the report its first send carried (R673). One statement,
    /// the generations passed as one parameter; only these snapshots are
    /// opened.
    /// </summary>
    public static async Task<Dictionary<Guid, decimal>> ReadAgreedFeesAsync(
        PegasusDbContext db,
        IReadOnlyCollection<Guid> generationIds,
        CancellationToken cancellationToken)
    {
        if (generationIds.Count == 0)
        {
            return [];
        }

        var ids = generationIds.ToHashSet();
        var rows = await ReadAsync(
            db.Set<CaseReportGenerationEntity>().AsNoTracking()
                .Where(generation => EF.Parameter(ids).Contains(generation.Id))
                .Select(generation => new FeeRow
                {
                    GenerationId = generation.Id,
                    AgreedFee = SqlJson.Value(generation.SnapshotJson, AgreedFeePath),
                }),
            cancellationToken);
        return rows.ToDictionary(row => row.GenerationId, row => AgreedFee(row.GenerationId, row.AgreedFee));
    }

    private sealed class FeeRow
    {
        public Guid GenerationId { get; init; }

        public string? AgreedFee { get; init; }
    }
}
