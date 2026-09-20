using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Pegasus.Core.Identity;
using Pegasus.Core.ReleaseNotes;
using Pegasus.Core.Support;

namespace Pegasus.Web.Presentation;

/// <summary>
/// Builds the problem-report request from what the page posted and what the
/// request itself knows: the build, the person, the trace, and the exception
/// the Error page remembered for that trace. The browser facts come from the
/// form's hidden fields, filled by site.js when the person presses Send.
/// </summary>
public static class ProblemReportRequests
{
    private const int MaximumRouteLength = 400;
    private const int MaximumReferenceLength = 40;
    private const int MaximumErrorLength = 300;
    private static readonly TimeSpan RememberedExceptionLifetime = TimeSpan.FromHours(1);

    public sealed record PostedFacts(
        string? Description,
        string? Route,
        string? TraceId,
        string? CaseReference,
        string? Viewport,
        bool Editing,
        string? ErrorsJson);

    /// <summary>The Error page calls this so a report raised from it can name the fault.</summary>
    public static void RememberException(IMemoryCache cache, string traceId, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(exception);
        if (string.IsNullOrWhiteSpace(traceId))
        {
            return;
        }

        cache.Set(CacheKey(traceId), (exception.GetType().FullName ?? exception.GetType().Name, exception.Message), RememberedExceptionLifetime);
    }

    public static ProblemReportRequest Build(
        HttpContext http,
        ActionActor actor,
        ApplicationBuild build,
        IMemoryCache cache,
        PostedFacts posted)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(posted);

        var traceId = Clean(posted.TraceId, 200) ?? Activity.Current?.Id ?? http.TraceIdentifier;
        (string Type, string Message)? exception = null;
        if (cache.TryGetValue(CacheKey(traceId), out (string Type, string Message) remembered))
        {
            exception = remembered;
        }

        var route = Clean(posted.Route, MaximumRouteLength) is { } postedRoute && postedRoute.StartsWith('/')
            ? postedRoute
            : http.Request.Path + http.Request.QueryString;
        var role = http.User.FindFirst(ClaimTypes.Role)?.Value;
        return new ProblemReportRequest(
            actor,
            posted.Description,
            build.Version,
            build.SourceSha,
            route,
            http.Request.Method,
            traceId,
            http.User.Identity?.Name ?? actor.SubjectId,
            OperatorLabels.StaffRole(role),
            Clean(posted.CaseReference, MaximumReferenceLength),
            exception?.Type,
            exception?.Message,
            new ProblemReportClientFacts(
                Clean(posted.Viewport, 40),
                Clean(http.Request.Headers.UserAgent.ToString(), 300),
                posted.Editing,
                ParseErrors(posted.ErrorsJson)));
    }

    private static string[] ParseErrors(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var entries = JsonSerializer.Deserialize<string[]>(json) ?? [];
            return entries
                .Select(entry => Clean(entry, MaximumErrorLength))
                .Where(entry => entry is not null)
                .Select(entry => entry!)
                .Take(ProblemReportPolicy.RecentErrorCount)
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? Clean(string? value, int length)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = new string(value.Trim().Where(character => !char.IsControl(character)).ToArray());
        return cleaned.Length > length ? cleaned[..length] : cleaned;
    }

    private static string CacheKey(string traceId) => "problem-report:exception:" + traceId;
}
