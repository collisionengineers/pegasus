using Microsoft.AspNetCore.Hosting;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The page that asked for the Report a problem dialog, as the request names
/// it: the trace and the method the report carries.
/// </summary>
public sealed record ShellProblemView(string TraceId, string Method);

/// <summary>What the rail and the shell dialogs share about the deployment.</summary>
public static class ShellRail
{
    /// <summary>
    /// Whether the Inbox and Upload routes are composed in this deployment: a
    /// capability that is not composed is absent, never a disabled item.
    /// </summary>
    public static bool InboxEnabled(IWebHostEnvironment environment, IConfiguration configuration) =>
        environment.IsProduction()
        || (environment.IsDevelopment() && configuration.GetValue<bool>("Features:LocalIntake"));
}
