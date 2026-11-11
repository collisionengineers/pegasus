namespace Pegasus.ArchitectureTests;

/// <summary>
/// FRD-21, R916, R960: the Delete case correspondence tick is unticked each
/// time its form opens, a refused send's redisplay included. A dialog closed
/// without sending keeps its fields while the page lives, and the Send report
/// dialog is carried across the Case page's in-place updates, so site.js
/// clears every such tick when its dialog opens, and no surface draws the
/// tick from what was posted. Each surface that offers the tick carries the
/// hook the script reads.
/// </summary>
public sealed class DeleteCaseCorrespondenceTickScriptContractTests
{
    private const string Hook = "data-delete-case-correspondence";

    [Fact]
    public void EachOpenClearsTheTick()
    {
        var site = Read("wwwroot", "js", "site.js");
        var ticks = site.IndexOf($"dialog.querySelectorAll('[{Hook}]')", StringComparison.Ordinal);
        Assert.True(ticks >= 0, "site.js reads the ticks of the dialog that opened.");
        var listener = site.LastIndexOf(
            "document.addEventListener('pegasus:dialog-open'", ticks, StringComparison.Ordinal);
        Assert.True(listener >= 0, "The ticks are read when a dialog opens.");
        var end = site.IndexOf("})();", ticks, StringComparison.Ordinal);
        Assert.Contains("tick.checked = false;", site[ticks..end], StringComparison.Ordinal);
        Assert.DoesNotContain("defaultChecked", site[listener..end], StringComparison.Ordinal);
        // Nothing between the listener and the reset opens another listener.
        Assert.Equal(1, CountOf(site[listener..end], "addEventListener("));
    }

    [Theory]
    [InlineData("Cases", "_CaseSendReportDialog.cshtml")]
    [InlineData("Cases", "_TriageCase.cshtml")]
    [InlineData("Mail", "_MessageComposer.cshtml")]
    public void EverySurfaceThatOffersTheTickCarriesTheHookOnceAndNeverDrawsItTicked(string area, string partial)
    {
        var markup = Read("Pages", area, "Shared", partial);

        Assert.Equal(1, CountOf(markup, Hook));
        // A plain input: neither a checked attribute nor a binding to the posted value.
        var hook = markup.IndexOf(Hook, StringComparison.Ordinal);
        var start = markup.LastIndexOf("<input", hook, StringComparison.Ordinal);
        var tag = markup[start..(markup.IndexOf("/>", hook, StringComparison.Ordinal) + 2)];
        Assert.DoesNotContain("checked=", tag, StringComparison.Ordinal);
        Assert.DoesNotContain("asp-for", tag, StringComparison.Ordinal);
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
