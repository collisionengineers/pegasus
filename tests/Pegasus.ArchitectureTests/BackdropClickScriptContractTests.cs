using System.Text.RegularExpressions;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// R711 (operator, 5 November 2026): a click on a backdrop closes the dialog,
/// the Evidence viewer or the Case viewer only when the press also began on the
/// backdrop. A text selection dragged out of the dialog and released on the
/// backdrop fires its click there, and closes nothing.
/// </summary>
public sealed partial class BackdropClickScriptContractTests
{
    [Fact]
    public void ABackdropClickClosesOnlyWhenThePressBeganOnTheBackdrop()
    {
        var site = Script("site.js");

        Assert.Contains("pressedOnBackdrop = event.target === backdrop;", site, StringComparison.Ordinal);
        Assert.Contains("if (pressed && event.target === backdrop) {", site, StringComparison.Ordinal);
        Assert.Contains("onBackdropClick: onBackdropClick", site, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryDialogAndViewerClosesThroughTheOneBackdropHelper()
    {
        var site = Script("site.js");
        var workspace = Script("case-workspace.js");

        Assert.Contains("window.pegasusUi.onBackdropClick(dialog, function () {", site, StringComparison.Ordinal);
        Assert.Contains("window.pegasusUi.onBackdropClick(viewer, close);", site, StringComparison.Ordinal);
        Assert.Contains("window.pegasusUi.onBackdropClick(host, close);", workspace, StringComparison.Ordinal);
        Assert.DoesNotMatch(BareBackdropClose(), site);
        Assert.DoesNotMatch(BareBackdropClose(), workspace);
    }

    private static string Script(string name) => File.ReadAllText(Path.Combine(
        RepositoryRoot.Find(), "src", "Pegasus.Web", "wwwroot", "js", name));

    /// <summary>A click handler that closes on its target alone, the shape R711 retired.</summary>
    [GeneratedRegex(@"event\.target === \w+\)\s*\{\s*(if \(cancel\(\)\)|close\(\);)")]
    private static partial Regex BareBackdropClose();
}
