using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Logs SQL Server's deadlock-victim error (1205) with the statement that
/// lost, once per failed command. SQL Server's deadlock graph is not
/// available to the app, so the statement and how long it ran are the
/// evidence that names the lock pair the next time one recurs. It changes
/// nothing else: the exception still reaches the caller untouched, and no
/// command is retried here.
/// </summary>
public sealed partial class DeadlockLoggingInterceptor : DbCommandInterceptor
{
    private const int DeadlockVictim = 1205;

    private readonly ILogger? logger;

    /// <summary>The instance both hosts register; it finds its logger on the failing context.</summary>
    public static DeadlockLoggingInterceptor Instance { get; } = new();

    /// <summary>Without a logger the interceptor finds one on the failing context.</summary>
    public DeadlockLoggingInterceptor(ILogger? logger = null) => this.logger = logger;

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData) =>
        LogDeadlock(command, eventData);

    public override Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        LogDeadlock(command, eventData);
        return Task.CompletedTask;
    }

    private void LogDeadlock(DbCommand command, CommandErrorEventData eventData)
    {
        if (eventData.Exception is not SqlException { Number: DeadlockVictim })
        {
            return;
        }

        try
        {
            var target = logger
                ?? eventData.Context?.GetService<ILoggerFactory>().CreateLogger<DeadlockLoggingInterceptor>();
            if (target is not null)
            {
                LogDeadlockVictim(target, eventData.Duration.TotalMilliseconds, command.CommandText);
            }
        }
        catch (Exception)
        {
            // Diagnostics must never replace the deadlock the caller is about to see.
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "SQL deadlock victim (1205) after {ElapsedMs} ms running: {CommandText}")]
    private static partial void LogDeadlockVictim(ILogger logger, double elapsedMs, string commandText);
}
