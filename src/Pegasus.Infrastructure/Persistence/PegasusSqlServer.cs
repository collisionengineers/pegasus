using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The one SQL Server configuration both hosts apply to the Pegasus context.
/// </summary>
public static class PegasusSqlServer
{
    public static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        options.UseSqlServer(
            connectionString,
            sql => sql.ExecutionStrategy(dependencies => new PegasusSqlServerExecutionStrategy(dependencies)));
    }
}

/// <summary>
/// SQL Server's retrying strategy, scoped to work that can be retried. Every
/// store opens its own Serializable transaction, and Entity Framework refuses
/// to run any operation inside a user transaction under a strategy that
/// retries, so inside one this strategy neither refuses nor retries: the
/// operation runs once and a fault surfaces to the store exactly as before.
/// Outside a transaction (read queries, the Worker's polls, the heartbeat's
/// reads) a transient fault retries with SQL Server's defaults.
/// </summary>
/// <remarks>
/// <see cref="ExecutionStrategy.RetriesOnFailure"/> is deliberately not
/// overridden: Entity Framework reads it while composing its own services,
/// before the context's database facade can be resolved, so a getter that
/// looks at the current transaction re-enters that resolution and never
/// returns. The two hooks below run only while an operation executes.
/// </remarks>
public sealed class PegasusSqlServerExecutionStrategy(ExecutionStrategyDependencies dependencies)
    : SqlServerRetryingExecutionStrategy(dependencies)
{
    private bool InsideTransaction =>
        Dependencies.CurrentContext.Context.Database.CurrentTransaction is not null;

    protected override void OnFirstExecution()
    {
        if (InsideTransaction)
        {
            ExceptionsEncountered.Clear();
            return;
        }

        base.OnFirstExecution();
    }

    protected override bool ShouldRetryOn(Exception exception) =>
        !InsideTransaction && base.ShouldRetryOn(exception);
}
