using Pegasus.Core.Actors;
using Pegasus.Core.Cases;
using GlassLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.GlassSession;

namespace Pegasus.Web.Pages.Integrations.Glass;

/// <summary>
/// What the Glass's window shows while it waits (Shared/_GlassWaiting): the
/// status words, the Case it belongs to, and the addresses its script reads.
/// A window that waits on provider work names where it asks and where it
/// goes; a window handing its outcome back names the Case address; the window
/// drawn at the press names neither.
/// </summary>
public sealed record GlassWaitingView(string Words, string? CaseLine)
{
    public string? StateUrl { get; init; }

    public string? GoUrl { get; init; }

    public string? ReturnUrl { get; init; }

    /// <summary>The line asking staff to keep the window open; absent as the window leaves.</summary>
    public bool KeepOpen { get; init; } = true;

    /// <summary>
    /// The Case line for a window, read with the Case's frame; null when the
    /// Case cannot be read, so the window says nothing rather than failing.
    /// </summary>
    public static async Task<string?> CaseLineAsync(
        IGetCaseSectionFrame frames, ActionActor actor, Guid caseId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(frames);
        var frame = await frames.ExecuteAsync(new(caseId, actor), cancellationToken);
        return frame is null ? null : GlassLabels.CaseLine(frame.Summary.Reference, frame.Summary.Registration);
    }
}
