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

    /// <summary>
    /// Whether guided photo capture (FRD-28) is composed in this deployment:
    /// the <c>Features:PhotoCapture</c> switch. Off, its rail item, its pages'
    /// entry points and the Case ribbon's Send photo link are absent.
    /// </summary>
    public static bool PhotoCaptureEnabled(IWebHostEnvironment environment, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return PhotoCaptureEnabled(configuration);
    }

    /// <inheritdoc cref="PhotoCaptureEnabled(IWebHostEnvironment, IConfiguration)"/>
    public static bool PhotoCaptureEnabled(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration.GetValue<bool>(Pegasus.Web.PhotoCapture.PhotoCaptureSurface.FeatureFlag);
    }
}
