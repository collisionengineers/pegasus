using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;

namespace Pegasus.Worker;

/// <summary>
/// Log filters for the worker process's own logs. <c>host.json</c> reaches only what the
/// Functions host itself writes, so anything the worker process logs is filtered here.
/// </summary>
public static class WorkerLogFilters
{
    /// <summary>
    /// The category whose failure line carries the whole SQL text, about 4 KB each. The
    /// exception is recorded separately, so the line adds only bytes.
    /// </summary>
    public const string EntityFrameworkCommandCategory = "Microsoft.EntityFrameworkCore.Database.Command";

    /// <summary>
    /// Drops the Entity Framework command lines from Application Insights. The rule names
    /// the provider on purpose: the Application Insights provider already carries its own
    /// Warning rule, and a rule that names a provider beats one that does not.
    /// </summary>
    public static ILoggingBuilder ApplyWorkerLogFilters(this ILoggingBuilder logging) =>
        logging.AddFilter<ApplicationInsightsLoggerProvider>(
            EntityFrameworkCommandCategory,
            LogLevel.None);
}
