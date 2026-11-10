namespace Pegasus.ArchitectureTests;

/// <summary>
/// FRD-21, R916: the Delete case correspondence tick is unticked each time its
/// form opens. A dialog closed without sending keeps its fields while the page
/// lives, and the Send report dialog is carried across the Case page's
/// in-place updates, so site.js puts every such tick back as the server drew
/// it when its dialog opens. Each surface that offers the tick carries the
/// hook the script reads.
/// </summary>
public sealed class DeleteCaseCorrespondenceTickScriptContractTests
{
    private const string Hook = "data-delete-case-correspondence";

    [Fact]
    public void EachOpenPutsTheTickBackAsTheServerDrewIt()
    {
        var site = Read("wwwroot", "js", "site.js");
        var ticks = site.IndexOf($"dialog.querySelectorAll('[{Hook}]')", StringComparison.Ordinal);
        Assert.True(ticks >= 0, "site.js reads the ticks of the dialog that opened.");
        var listener = site.LastIndexOf(
            "document.addEventListener('pegasus:dialog-open'", ticks, StringComparison.Ordinal);
        Assert.True(listener >= 0, "The ticks are read when a dialog opens.");
        var end = site.IndexOf("})();", ticks, StringComparison.Ordinal);
        Assert.Contains("tick.checked = tick.defaultChecked;", site[ticks..end], StringComparison.Ordinal);
        // Nothing between the listener and the reset opens another listener.
        Assert.Equal(1, CountOf(site[listener..end], "addEventListener("));
    }

    [Theory]
    [InlineData("Cases", "_CaseSendReportDialog.cshtml")]
    [InlineData("Cases", "_TriageCase.cshtml")]
    [InlineData("Mail", "_MessageComposer.cshtml")]
    public void EverySurfaceThatOffersTheTickCarriesTheHookOnce(string area, string partial)
    {
        var markup = Read("Pages", area, "Shared", partial);

        Assert.Equal(1, CountOf(markup, Hook));
    }

    private static int CountOf(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal);
             index >= 0;
             index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }
        return count;
    }

    private static string Read(params string[] path) => File.ReadAllText(Path.Combine(
        [RepositoryRoot.Find(), "src", "Pegasus.Web", .. path]));
}
