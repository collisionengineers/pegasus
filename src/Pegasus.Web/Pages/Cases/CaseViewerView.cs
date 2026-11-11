namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// What the full-screen viewer partial is told: the Case its Pop out opens
/// the images window for, whether it is that window itself
/// (<see cref="Standalone"/>), which has no Pop out of its own, and whether
/// Case video snapshots may be filed onto the Case (R964).
/// </summary>
public sealed record CaseViewerView(Guid CaseId, bool Standalone, bool CanSnapshotVideos = false);
