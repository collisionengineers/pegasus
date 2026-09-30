using Pegasus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Pegasus.Web.Health;

/// <summary>
/// The database is reachable and its schema is current. The schema can only
/// change at a release, which starts a new process, so once a probe has found
/// no pending migration the check remembers it for the life of the process and
/// later probes only connect. A probe that fails, because the database cannot
/// be reached or migrations are pending, remembers nothing, and the next probe
/// looks again. It is registered once for the process, so the memory outlasts
/// each probe.
/// </summary>
internal sealed class DatabaseReadinessHealthCheck(
    IDbContextFactory<PegasusDbContext> contextFactory) : IHealthCheck
{
    private int schemaCurrent;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
            if (!await database.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy("The configured database is unavailable.");
            }

            if (Volatile.Read(ref schemaCurrent) == 0)
            {
                if ((await database.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
                {
                    return HealthCheckResult.Unhealthy("The configured database schema is not current.");
                }

                Volatile.Write(ref schemaCurrent, 1);
            }

            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return HealthCheckResult.Unhealthy("The database readiness check failed.");
        }
    }
}
