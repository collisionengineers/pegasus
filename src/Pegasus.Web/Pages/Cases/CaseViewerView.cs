namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// What the full-screen viewer partial is told: the Case its Pop out opens
/// the images window for, and whether it is that window itself
/// (<see cref="Standalone"/>), which has no Pop out of its own.
/// </summary>
public sealed record CaseViewerView(Guid CaseId, bool Standalone);
