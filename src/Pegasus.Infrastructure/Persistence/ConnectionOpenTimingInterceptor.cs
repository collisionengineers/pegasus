using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pegasus.Core.Documents;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Records a database connection open that took long enough to matter as the
/// <c>db.connection.open</c> phase of the request that paid for it. A pooled
/// open is well under a millisecond; a new physical connection (token, TCP,
/// TLS, login) or a wait for a pool slot is not, and nothing else timed it.
/// Opens under <see cref="Threshold"/> record nothing: the SqlClient counters
/// give their totals. The interceptor holds no state, so one instance serves
/// every context.
/// </summary>
public sealed class ConnectionOpenTimingInterceptor : DbConnectionInterceptor
{
    public static readonly TimeSpan Threshold = TimeSpan.FromMilliseconds(100);

    public static ConnectionOpenTimingInterceptor Instance { get; } = new();

    private ConnectionOpenTimingInterceptor()
    {
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        Record(eventData);

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Record(eventData);
        return Task.CompletedTask;
    }

    private static void Record(ConnectionEndEventData eventData)
    {
        if (eventData.Duration >= Threshold)
        {
            DocumentReadTelemetry.Record("db.connection.open", eventData.Duration);
        }
    }
}
