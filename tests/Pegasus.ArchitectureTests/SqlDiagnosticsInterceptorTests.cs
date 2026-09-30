using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Documents;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// The two SQL diagnostics interceptors both hosts share: a connection open of
/// 100 ms or more becomes the <c>db.connection.open</c> phase, and a deadlock
/// victim (1205) is logged with its statement. Neither changes what the caller
/// sees. The event data is built directly, and the SqlException through its
/// internal factory, so no database is needed.
/// </summary>
public sealed class SqlDiagnosticsInterceptorTests
{
    [Fact]
    public void BothHostsRegisterBothInterceptorsThroughTheSharedConfiguration()
    {
        var options = new DbContextOptionsBuilder<DbContext>();

        PegasusSqlServer.Configure(options, "Server=tcp:127.0.0.1,1;Database=Unused;Encrypt=False");

        var interceptors = options.Options.FindExtension<CoreOptionsExtension>()!.Interceptors!.ToArray();
        Assert.Contains(ConnectionOpenTimingInterceptor.Instance, interceptors);
        Assert.Contains(DeadlockLoggingInterceptor.Instance, interceptors);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(99, false)]
    [InlineData(100, true)]
    [InlineData(2500, true)]
    public async Task AnOpenRecordsOnePhaseOnlyAtOrOverTheThreshold(int milliseconds, bool recorded)
    {
        foreach (var isAsync in new[] { false, true })
        {
            var phases = new List<Activity>();
            using var scope = new Activity($"{nameof(AnOpenRecordsOnePhaseOnlyAtOrOverTheThreshold)}.{isAsync}")
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();
            using var listener = Listen(scope, phases);
            using var connection = new SqlConnection();
            var duration = TimeSpan.FromMilliseconds(milliseconds);
            var data = new ConnectionEndEventData(
                null!, null!, connection, null!, Guid.NewGuid(), isAsync, DateTimeOffset.UtcNow, duration);

            if (isAsync)
            {
                await ConnectionOpenTimingInterceptor.Instance.ConnectionOpenedAsync(connection, data);
            }
            else
            {
                ConnectionOpenTimingInterceptor.Instance.ConnectionOpened(connection, data);
            }

            if (!recorded)
            {
                Assert.Empty(phases);
                continue;
            }

            var phase = Assert.Single(phases);
            Assert.Equal("db.connection.open", phase.DisplayName);
            Assert.Same(scope, phase.Parent);
            Assert.True(phase.Duration >= duration, $"{phase.Duration} was shorter than {duration}.");
            Assert.Empty(phase.TagObjects);
        }
    }

    [Fact]
    public async Task ADeadlockVictimIsLoggedOnceAtWarningWithItsStatementAndElapsedTime()
    {
        foreach (var isAsync in new[] { false, true })
        {
            var logger = new RecordingLogger();
            var interceptor = new DeadlockLoggingInterceptor(logger);
            const string text = "UPDATE [CaseWorkflows] SET [EditLeaseToken] = @p0 WHERE [CaseId] = @p1";
            using var command = new SqlCommand(text);
            var data = Failure(command, Sql(1205), isAsync, TimeSpan.FromMilliseconds(3674));

            if (isAsync)
            {
                await interceptor.CommandFailedAsync(command, data);
            }
            else
            {
                interceptor.CommandFailed(command, data);
            }

            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("1205", entry.Message, StringComparison.Ordinal);
            Assert.Contains(text, entry.Message, StringComparison.Ordinal);
            Assert.Contains("3674", entry.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(2627)]
    [InlineData(2601)]
    [InlineData(-2)]
    public void AnotherSqlErrorLogsNothing(int number)
    {
        var logger = new RecordingLogger();
        using var command = new SqlCommand("INSERT INTO x VALUES (1)");

        new DeadlockLoggingInterceptor(logger).CommandFailed(
            command, Failure(command, Sql(number), isAsync: false, TimeSpan.FromMilliseconds(5)));

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void AFailureThatIsNotASqlErrorLogsNothing()
    {
        var logger = new RecordingLogger();
        using var command = new SqlCommand("SELECT 1");

        new DeadlockLoggingInterceptor(logger).CommandFailed(
            command, Failure(command, new InvalidOperationException("not sql"), isAsync: false, TimeSpan.Zero));

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void AnInterceptorWithNoLoggerAndNoContextNeitherLogsNorThrows()
    {
        using var command = new SqlCommand("UPDATE x SET y = 1");

        DeadlockLoggingInterceptor.Instance.CommandFailed(
            command, Failure(command, Sql(1205), isAsync: false, TimeSpan.FromMilliseconds(10)));
    }

    private static CommandErrorEventData Failure(
        DbCommand command, Exception exception, bool isAsync, TimeSpan duration) =>
        new(
            null!,
            null!,
            command.Connection!,
            command,
            command.CommandText,
            null!,
            DbCommandMethod.ExecuteNonQuery,
            Guid.NewGuid(),
            Guid.NewGuid(),
            exception,
            isAsync,
            logParameterValues: false,
            DateTimeOffset.UtcNow,
            duration,
            CommandSource.Unknown);

    /// <summary>
    /// A SqlException with the given error number. SqlClient offers no public
    /// constructor, so this goes through its internal factory (SqlClient 6.1);
    /// a package update that moves it fails here, not in production.
    /// </summary>
    private static SqlException Sql(int number)
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        Type[] errorParameters =
            [typeof(int), typeof(byte), typeof(byte), typeof(string), typeof(string), typeof(string), typeof(int), typeof(Exception)];
        var error = typeof(SqlError).GetConstructor(all, binder: null, errorParameters, modifiers: null)!
            .Invoke([number, (byte)0, (byte)0, "server", "sample error", string.Empty, 1, null]);
        var errors = typeof(SqlErrorCollection).GetConstructor(all, binder: null, Type.EmptyTypes, modifiers: null)!
            .Invoke([]);
        typeof(SqlErrorCollection).GetMethod("Add", all, [typeof(SqlError)])!.Invoke(errors, [error]);
        return (SqlException)typeof(SqlException)
            .GetMethod("CreateException", all, [typeof(SqlErrorCollection), typeof(string), typeof(Guid), typeof(Exception)])!
            .Invoke(null, [errors, "16.0.0", Guid.NewGuid(), null])!;
    }

    private static ActivityListener Listen(Activity scope, List<Activity> stopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DocumentReadTelemetry.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == scope.TraceId)
                {
                    stopped.Add(activity);
                }
            }
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
