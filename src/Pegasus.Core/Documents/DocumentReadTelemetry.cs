using System.Diagnostics;

namespace Pegasus.Core.Documents;

/// <summary>
/// Allowlisted server-side timing spans for document preview work. The names
/// describe only a bounded operation phase; callers must not add document,
/// case, actor, URL, file-name, token, or content values as tags.
/// </summary>
public static class DocumentReadTelemetry
{
    public const string ActivitySourceName = "Pegasus.Documents";

    private static readonly ActivitySource Source = new(ActivitySourceName);

    private static readonly HashSet<string> AllowedPhases = new(StringComparer.Ordinal)
    {
        "web.case.main",
        "web.workcentre.resource",
        "web.workcentre.result",
        "web.workcentre.main",
        "web.workcentre.attention",
        "web.workcentre.newcases",
        "web.workcentre.aijobs",
        "web.workcentre.activity",
        "web.workcentre.refresh.resource",
        "web.workcentre.refresh.result",
        "web.workcentre.refresh.main",
        "web.case.frame",
        "web.case.workspace",
        "web.case.direct-sections",
        "web.case.engineer-sections",
        "web.case.extras",
        "web.case.commit",
        "web.case.resource",
        "web.case.result",
        "web.case.section.resource",
        "web.case.section.result",
        "web.auth.validation",
        "web.shell.counts",
        "web.shell.notifications",
        "report.renderer.initialize",
        "report.photos.prepare",
        "report.pdf.generate",
        "report.pdf.pagecount",
        "db.connection.open",
        "web.case.fragment.vehicle",
        "web.case.fragment.valuation",
        "web.case.fragment.files",
        "web.case.fragment.notes",
        "web.case.fragment.tasks",
        "document.preview",
        "document.preparation.lookup",
        "document.thumbnail.open",
        "document.original.open",
        "document.provider.gate",
        "document.provider.read",
        "document.original.cache.read",
        "document.original.cache.write",
        "document.content.verify",
        "document.thumbnail.cache.read",
        "document.thumbnail.cache.write",
        "document.thumbnail.render",
        "document.thumbnail.render.gate",
        "document.thumbnail.decode.gate"
    };

    public static Activity? Start(string phase)
    {
        if (!IsAllowedPhase(phase))
        {
            throw new ArgumentOutOfRangeException(nameof(phase), phase, "The telemetry phase is not allowlisted.");
        }

        return Source.StartActivity(phase, ActivityKind.Internal);
    }

    /// <summary>
    /// Records a phase that has already finished. The activity starts
    /// <paramref name="duration"/> before now and stops at once, so the bridge
    /// reports the measured time under the current parent. Used where the
    /// duration is handed over by a framework callback that fires after the
    /// work, such as a database connection opening.
    /// </summary>
    public static void Record(string phase, TimeSpan duration)
    {
        if (!IsAllowedPhase(phase))
        {
            throw new ArgumentOutOfRangeException(nameof(phase), phase, "The telemetry phase is not allowlisted.");
        }

        // CreateActivity, Start and Stop keep the ordinary parent handling, so
        // the ambient activity is current again once the phase has stopped.
        if (Source.CreateActivity(phase, ActivityKind.Internal) is not { } activity)
        {
            return;
        }

        var end = DateTime.UtcNow;
        activity.SetStartTime(end - (duration < TimeSpan.Zero ? TimeSpan.Zero : duration));
        activity.Start();
        activity.SetEndTime(end);
        activity.Stop();
    }

    /// <summary>
    /// Whether a phase is safe for the document-read telemetry bridge to emit.
    /// </summary>
    public static bool IsAllowedPhase(string phase) => AllowedPhases.Contains(phase);
}
