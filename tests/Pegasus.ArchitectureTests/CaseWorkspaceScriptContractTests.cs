namespace Pegasus.ArchitectureTests;

/// <summary>
/// Issue 916: after a save, the retained sections' forms take the Case's new
/// version and edit lease together. The Glass's and report forms name the version
/// <c>expectedCaseVersion</c>, so the update must look for it as well as for
/// <c>expectedVersion</c>, or a retained form keeps a stale version beside a
/// fresh lease.
/// </summary>
public sealed class CaseWorkspaceScriptContractTests
{
    [Fact]
    public void ARetainedSectionsFormsAdvanceEitherNameOfTheCaseVersion()
    {
        var script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));

        Assert.Contains(
            "var version = form.querySelector('[name=\"expectedVersion\"], [name=\"expectedCaseVersion\"]');",
            script,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The viewer scrolls itself into view when it opens (operator, 30 September
    /// 2026). Every way in, a tile, a preview or a card's crop, shows the viewer
    /// through <c>show</c>, so that one place holds the scroll and nothing calls
    /// it twice.
    /// </summary>
    [Fact]
    public void TheViewerScrollsIntoViewInTheOnePlaceEveryOpenGoesThrough()
    {
        var script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));

        var show = FunctionBody(script, "function show(at) {");
        Assert.Contains("bringIntoView();", show, StringComparison.Ordinal);
        Assert.Contains("show(start < 0 ? 0 : start);", FunctionBody(script, "function open(trigger, extra) {"), StringComparison.Ordinal);
        Assert.Contains("show(0);", FunctionBody(script, "function openDocument(options) {"), StringComparison.Ordinal);

        var bring = FunctionBody(script, "function bringIntoView() {");
        Assert.Contains("host.scrollIntoView({ block: 'start', behavior:", bring, StringComparison.Ordinal);
        Assert.Contains("box.bottom <= window.innerHeight", bring, StringComparison.Ordinal);
        Assert.Equal(1, script.Split("host.scrollIntoView(", StringSplitOptions.None).Length - 1);
    }

    private static string FunctionBody(string script, string signature)
    {
        var start = script.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"{signature} is missing from case-workspace.js.");
        var end = script.IndexOf("\n        }", start, StringComparison.Ordinal);
        Assert.True(end > start, $"{signature} has no closing brace at the viewer's indent.");
        return script[start..end];
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Pegasus.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Pegasus repository root.");
    }
}
