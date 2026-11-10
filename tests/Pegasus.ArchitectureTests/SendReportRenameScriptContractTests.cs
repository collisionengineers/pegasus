namespace Pegasus.ArchitectureTests;

/// <summary>
/// #1911: a Files row's Rename box in the Send report dialog owns its Escape,
/// which puts the name back and leaves the dialog open; leaving the box for
/// another field keeps the name and leaves focus in that field.
/// </summary>
public sealed class SendReportRenameScriptContractTests
{
    [Fact]
    public void TheDialogLeavesEscapeToAFieldThatOwnsIt()
    {
        var site = Script("site.js");
        var escape = site.IndexOf("if (event.key === 'Escape') {", StringComparison.Ordinal);
        var optOut = site.IndexOf("event.target.closest('[data-own-escape]')", StringComparison.Ordinal);
        var close = site.IndexOf("close();", escape, StringComparison.Ordinal);

        Assert.True(escape >= 0 && optOut > escape && optOut < close,
            "The dialog's Escape handler steps aside for a [data-own-escape] field before it closes.");
    }

    [Fact]
    public void EveryRenameBoxOwnsItsEscapeAndBlurDoesNotMoveFocus()
    {
        var dialog = File.ReadAllText(Path.Combine(
            RepositoryRoot.Find(), "src", "Pegasus.Web", "Pages", "Cases", "Shared", "_CaseSendReportDialog.cshtml"));
        var workspace = Script("case-workspace.js");

        Assert.Equal(
            CountOf(dialog, "data-send-file-stem"),
            CountOf(dialog, "data-send-file-stem data-own-escape"));
        Assert.Contains("if (refocus) { rename.focus(); }", workspace, StringComparison.Ordinal);
        Assert.Contains("end(true, false); } }, 0);", workspace, StringComparison.Ordinal);
        Assert.Contains("if (event.key === 'Escape') { event.preventDefault(); end(false, true); }", workspace, StringComparison.Ordinal);
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

    private static string Script(string name) => File.ReadAllText(Path.Combine(
        RepositoryRoot.Find(), "src", "Pegasus.Web", "wwwroot", "js", name));
}
