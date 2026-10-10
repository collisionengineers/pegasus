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
    /// Editing never expires while the page is open (operator, 6 October 2026). The
    /// heartbeat's answer is the Case's version, and one system work moved starts a
    /// catch up; a 409 (a colleague holds the Case now) draws the Case as it stands;
    /// a 403 or a 404 (a Case that no longer exists, a stale tab after a wipe) stops
    /// the beat. Nothing offers to renew editing. It still beats in a hidden tab,
    /// because the edit lease relies on those beats to stay alive.
    /// </summary>
    [Fact]
    public void TheHeartbeatCatchesUpWithTheCaseAndNeverOffersToRenew()
    {
        var script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));

        var beat = FunctionBody(script, "function beat() {");
        Assert.Contains(
            "Number(answer.version) > Number(record.getAttribute('data-case-version'))",
            beat,
            StringComparison.Ordinal);
        Assert.Contains("response.status === 409", beat, StringComparison.Ordinal);
        var redirected = beat.IndexOf("if (response.redirected) {", StringComparison.Ordinal);
        Assert.True(redirected >= 0 && redirected < beat.IndexOf("if (response.ok) {", StringComparison.Ordinal));
        Assert.Contains("stopHeartbeat();", beat[redirected..beat.IndexOf("if (response.ok) {", StringComparison.Ordinal)], StringComparison.Ordinal);
        Assert.Contains("response.status === 403 || response.status === 404", beat, StringComparison.Ordinal);
        Assert.Equal(2, beat.Split("requestCatchUp();", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("document.hidden", beat, StringComparison.Ordinal);
        Assert.DoesNotContain("data-case-renew", script, StringComparison.Ordinal);
        Assert.DoesNotContain("is-expiring", script, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Image Intake edit heartbeat ends quietly when its post is redirected to
    /// sign-in: the timer stops and the named visibility listener is removed.
    /// </summary>
    [Fact]
    public void TheEditScopeHeartbeatStopsWhenSignInRedirectsIt()
    {
        var site = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "Pegasus.Web", "wwwroot", "js", "site.js"));

        var start = site.IndexOf("function bindEditScopeHeartbeats(root) {", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var body = site[start..];
        var ok = body.IndexOf("if (response.ok) {", StringComparison.Ordinal);
        var redirected = body.IndexOf("if (response.redirected) {", StringComparison.Ordinal);
        Assert.True(redirected >= 0 && redirected < ok);
        var branch = body[redirected..ok];
        Assert.Contains("window.clearInterval(timer);", branch, StringComparison.Ordinal);
        Assert.Contains("document.removeEventListener('visibilitychange', onVisible);", branch, StringComparison.Ordinal);
        Assert.Contains("document.addEventListener('visibilitychange', onVisible);", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Catching up never takes what the operator typed: a section holding a value not
    /// yet sent stays as they have it, and a landed commit makes what it sent each
    /// control's default, so only a later change counts as unsent.
    /// </summary>
    [Fact]
    public void CatchingUpKeepsEverySectionHoldingAValueNotYetSent()
    {
        var script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));

        Assert.Contains(
            "if (!current || !main.contains(current) || holdsUnsent(current)) { return; }",
            script,
            StringComparison.Ordinal);
        Assert.Contains("markSent(command.sent);", script, StringComparison.Ordinal);
        Assert.Contains("command.sent = sendingState(form);", script, StringComparison.Ordinal);
        Assert.Contains("if (catchUpWanted) { catchUp(); return; }", script, StringComparison.Ordinal);
    }

    /// <summary>
    /// A section showing another's value follows a save (operator, 7 October 2026):
    /// a landed commit asks for a catch up, which runs once nothing waits on the
    /// queue, so Done or a link away reads nothing extra first. Files and Notes stay
    /// as loaded, and the control the operator is in is the one they are in when the
    /// redraw lands, found again by its place when it has no id.
    /// </summary>
    [Fact]
    public void ALandedSaveRedrawsTheSectionsThatShowWhatItChanged()
    {
        var script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));

        var settle = FunctionBody(script, "function settleQueue() {", "    ");
        Assert.Contains(
            "if (redrawWanted && !commitWaiters.length) { catchUp({ afterSave: true }); return; }",
            settle,
            StringComparison.Ordinal);
        Assert.True(
            settle.IndexOf("if (catchUpWanted) { catchUp(); return; }", StringComparison.Ordinal)
                < settle.IndexOf("if (redrawWanted", StringComparison.Ordinal),
            "A catch up for system work runs before the redraw a save asked for.");

        var swap = FunctionBody(script, "function swap(html, command, preferred, options) {", "    ");
        Assert.Contains("redrawWanted = true;", swap, StringComparison.Ordinal);
        Assert.Contains("if (!keepSections) { redrawWanted = false; }", swap, StringComparison.Ordinal);
        Assert.Contains("if (afterSave && next.hasAttribute('data-lazy')) { return; }", swap, StringComparison.Ordinal);

        var catchUp = FunctionBody(script, "function catchUp(options) {", "    ");
        Assert.True(
            catchUp.IndexOf("var focused = focusedControl();", StringComparison.Ordinal)
                > catchUp.IndexOf("return response.text();", StringComparison.Ordinal),
            "The focused control is read when the redraw lands, not when the read begins.");
        Assert.Contains("index: host ? Array.prototype.indexOf.call(host.querySelectorAll(FOCUSABLE), control) : -1,", script, StringComparison.Ordinal);
    }

    /// <summary>
    /// A tag or In report can clear the last report blocker, so the Report's head
    /// offers Generate report with no reload (operator, 9 October 2026): a landed
    /// tile-changing document action asks for the same redraw a landed save does.
    /// </summary>
    [Fact]
    public void ALandedDocumentActionRedrawsTheSectionsAsASaveDoes()
    {
        var script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));

        var action = FunctionBody(script, "function submitDocumentAction(form, submitter) {", "    ");
        Assert.Contains(
            "if (changesTile && incoming.getAttribute('data-case-editing') === 'true') { redrawWanted = true; }",
            action,
            StringComparison.Ordinal);
        Assert.Contains("settleQueue();", action, StringComparison.Ordinal);
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

    private static string FunctionBody(string script, string signature, string indent = "        ")
    {
        var start = script.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"{signature} is missing from case-workspace.js.");
        var end = script.IndexOf("\n" + indent + "}", start, StringComparison.Ordinal);
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
