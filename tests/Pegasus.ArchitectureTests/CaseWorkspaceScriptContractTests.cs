using System.Text.RegularExpressions;

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

    /// <summary>
    /// The edit heartbeat stops on a 404, a Case that no longer exists (a stale tab
    /// after a wipe), as it does on a refused or expired lease. It still beats in a
    /// hidden tab, because the edit lease relies on those beats to stay alive.
    /// </summary>
    [Fact]
    public void TheHeartbeatStopsOnA404AsItDoesOnAnExpiredLeaseAndStillBeatsInAHiddenTab()
    {
        var script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));

        var beat = FunctionBody(script, "function beat() {");
        Assert.Contains(
            "response.status === 409 || response.status === 403 || response.status === 404",
            beat,
            StringComparison.Ordinal);
        Assert.DoesNotContain("document.hidden", beat, StringComparison.Ordinal);
    }

    /// <summary>
    /// Choosing a repair spec keeps the page where it is (operator, 1 October 2026).
    /// A spec tab, New repair spec and Compare's From and To redraw only the Repair
    /// Spec section and the dialogs drawn after it, never navigating to the page top
    /// and jumping back, and the address keeps the chosen spec for a reload.
    /// </summary>
    [Fact]
    public void ChoosingARepairSpecRedrawsOnlyTheRepairSpecPart()
    {
        var script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));

        Assert.Contains(
            "if (link.matches('[data-estimate-tab], [data-estimate-new]')) {",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "showEstimate(link.href)",
            script,
            StringComparison.Ordinal);
        var compare = script.IndexOf("if (form.hasAttribute('data-estimate-compare-form')) {", StringComparison.Ordinal);
        Assert.True(compare >= 0, "Compare's form does not redraw the Repair Spec part.");
        Assert.Contains(
            "return showEstimate(action)",
            script.Substring(compare, 200),
            StringComparison.Ordinal);

        var start = script.IndexOf("function showEstimate(href) {", StringComparison.Ordinal);
        Assert.True(start >= 0, "function showEstimate(href) is missing from case-workspace.js.");
        var show = script[start..script.IndexOf("\n    }", start, StringComparison.Ordinal)];
        Assert.Contains("var section = sectionFor('estimate');", show, StringComparison.Ordinal);
        Assert.Contains("section.replaceWith.apply(section, incoming);", show, StringComparison.Ordinal);
        Assert.Contains("window.history.replaceState(null, '', href);", show, StringComparison.Ordinal);
        Assert.DoesNotContain("swap(", show, StringComparison.Ordinal);
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

    /// <summary>
    /// Roadmap Lane H (FRD-16): the server answers a commit with the parts the script
    /// redraws after one, so a root the script gains and the answer never draws would
    /// silently stop updating. The script keeps the sections and the viewer, and every
    /// other root it names must be drawn by the answer's partials. Every input it
    /// carries forward must be drawn there too, or the next commit sends a stale key,
    /// version, lease or valuation calculation.
    /// </summary>
    [Fact]
    public void TheCommitAnswerDrawsEveryPartTheScriptRedrawsAfterACommit()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(
            root, "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));
        var cases = Path.Combine(root, "src", "Pegasus.Web", "Pages", "Cases");
        var shared = Path.Combine(cases, "Shared");

        Assert.Contains(
            "selector === '#case-main' || selector === '[data-case-viewer-host]'",
            script,
            StringComparison.Ordinal);
        var roots = Regex.Match(script, @"var swapRoots = \[(?<list>.*?)\];").Groups["list"].Value;
        var redrawn = Regex.Matches(roots, @"'\[(?<attribute>[a-z-]+)\]'")
            .Select(match => match.Groups["attribute"].Value)
            .Where(attribute => attribute != "data-case-viewer-host")
            .ToArray();
        Assert.Equal(5, redrawn.Length);

        var parts = new Dictionary<string, string>(StringComparer.Ordinal);
        var pending = new Queue<string>(["_CaseCommitResult"]);
        while (pending.TryDequeue(out var name))
        {
            if (parts.ContainsKey(name) || !File.Exists(Path.Combine(shared, name + ".cshtml")))
            {
                continue;
            }

            var markup = File.ReadAllText(Path.Combine(shared, name + ".cshtml"));
            parts[name] = markup;
            foreach (Match part in Regex.Matches(markup, "<partial name=\"Cases/Shared/(?<part>_[A-Za-z]+)\""))
            {
                pending.Enqueue(part.Groups["part"].Value);
            }
        }

        var drawn = string.Concat(parts.Values);
        foreach (var attribute in redrawn)
        {
            Assert.Contains(attribute, drawn, StringComparison.Ordinal);
        }

        var carriers = Directory.GetFiles(cases, "*.cshtml", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains("data-carry-forward", StringComparison.Ordinal))
            .Select(file => Path.GetFileNameWithoutExtension(file)!)
            .ToArray();
        Assert.NotEmpty(carriers);
        Assert.All(carriers, carrier => Assert.Contains(carrier, parts.Keys));
    }
}
