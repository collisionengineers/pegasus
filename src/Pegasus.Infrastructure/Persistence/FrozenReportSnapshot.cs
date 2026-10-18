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
}
