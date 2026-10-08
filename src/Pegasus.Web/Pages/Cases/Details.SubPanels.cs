namespace Pegasus.Web.Pages.Cases;

/// <summary>A sub-card's fold (v28 P45): its cookie key and whether the server painted it folded.</summary>
public sealed record SubPanelToggle(string Key, bool Collapsed);

public sealed partial class DetailsModel
{
    /// <summary>
    /// The fold for a headed sub-card inside a section, keyed
    /// <c>case.&lt;section&gt;.&lt;card&gt;</c> and remembered per browser like
    /// the sections themselves.
    /// </summary>
    public SubPanelToggle SubPanel(string key) => new(key, CollapsedClass(key) is not null);

    /// <summary>
    /// The fold of a card folded until this browser opens it (its
    /// <c>data-collapse-folded</c>): the same cookie names the key once the
    /// card is opened, so the first paint, and every redraw, keeps it so.
    /// </summary>
    public SubPanelToggle FoldedUntilOpened(string key) =>
        new(key, !Pegasus.Web.Presentation.ShellPreferences.PanelCollapsed(Request, key));
}
