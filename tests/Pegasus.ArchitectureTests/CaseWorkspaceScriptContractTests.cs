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
            RepositoryRoot.Find(), "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));

        Assert.Contains(
            "var version = form.querySelector('[name=\"expectedVersion\"], [name=\"expectedCaseVersion\"]');",
            script,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Tasks is the Actions menu's dialog (operator, 13 October 2026): its body is
    /// fetched from the section fragment route as it opens, and a page opened with
    /// <c>?dialog=tasks</c> does not open the dialog again at every catch up.
    /// </summary>
    [Fact]
    public void TheTasksDialogLoadsItsBodyAsItOpensAndACatchUpOpensNoDialog()
    {
        var script = File.ReadAllText(Path.Combine(
            RepositoryRoot.Find(), "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));

        Assert.Contains("event.target.closest('[data-dialog-open=\"case-tasks-dialog\"]')", script, StringComparison.Ordinal);
        Assert.Contains("fetch(fragmentPath + '?section=tasks'", script, StringComparison.Ordinal);
        var catchUp = FunctionBody(script, "function catchUp(options) {", "    ");
        Assert.Contains("current.searchParams.delete('dialog');", catchUp, StringComparison.Ordinal);
        Assert.Contains("fetch(current.toString(), {", catchUp, StringComparison.Ordinal);
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
            RepositoryRoot.Find(), "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));

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
            RepositoryRoot.Find(), "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));

        var beat = FunctionBody(script, "function beat() {");
        Assert.Contains(
            "Number(answer.version) > Number(record.getAttribute('data-case-version'))",
            beat,
            StringComparison.Ordinal);
        Assert.Contains("response.status === 409", beat, StringComparison.Ordinal);
        var ok = beat.IndexOf("if (response.ok) {", StringComparison.Ordinal);
        var redirected = beat.IndexOf("if (response.type === 'opaqueredirect') {", StringComparison.Ordinal);
        Assert.True(redirected >= 0 && redirected < ok);
        Assert.Contains("stopHeartbeat();", beat[redirected..ok], StringComparison.Ordinal);
        Assert.Contains("redirect: 'manual'", beat, StringComparison.Ordinal);
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
            RepositoryRoot.Find(), "src", "Pegasus.Web", "wwwroot", "js", "site.js"));

        var start = site.IndexOf("function bindEditScopeHeartbeats(root) {", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var body = site[start..];
        var ok = body.IndexOf("if (response.ok) {", StringComparison.Ordinal);
        var redirected = body.IndexOf("if (response.type === 'opaqueredirect') {", StringComparison.Ordinal);
        Assert.True(redirected >= 0 && redirected < ok);
        Assert.Contains("redirect: 'manual'", body[..redirected], StringComparison.Ordinal);
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
            RepositoryRoot.Find(), "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));

        Assert.Contains(
            "if (!current || !main.contains(current) || holdsUnsent(current)) { return; }",
            script,
            StringComparison.Ordinal);
        Assert.Contains("markSent(command.sent);", script, StringComparison.Ordinal);
        Assert.Contains("command.sent = sendingState(form).filter(function (state) {", script, StringComparison.Ordinal);
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
            RepositoryRoot.Find(), "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));

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
            RepositoryRoot.Find(), "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));

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
            RepositoryRoot.Find(), "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));

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
        // The part is fetched from the fragment route with the lease header
        // (not by a full-page GET), and a choice that cannot be redrawn still
        // takes the navigation it replaced.
        Assert.Contains(
            "fetch(fragmentPath + new URL(href, window.location.href).search,",
            show,
            StringComparison.Ordinal);
        Assert.Contains("headers['X-Pegasus-Edit-Lease'] = lease;", show, StringComparison.Ordinal);
        Assert.Contains("window.location.assign(href);", show, StringComparison.Ordinal);
    }

    /// <summary>
    /// The five-minute Work Centre refresh waits for five minutes since the last
    /// applied refresh, and only an applied refresh sets that time (operator,
    /// 11 October 2026).
    /// </summary>
    [Fact]
    public void TheWorkCentreIntervalWaitsFiveMinutesFromTheLastAppliedRefresh()
    {
        var script = File.ReadAllText(Path.Combine(
            RepositoryRoot.Find(), "src", "Pegasus.Web", "wwwroot", "js", "work-centre.js"));

        Assert.Contains(
            "if (document.hidden || Date.now() - lastRefresh < FIVE_MINUTES) { return; }",
            script,
            StringComparison.Ordinal);
        Assert.Equal(2, script.Split("lastRefresh = Date.now();", StringSplitOptions.None).Length - 1);
    }

    /// <summary>
    /// A saved Mine scope is carried on the brand and navigation links by the
    /// layout-wide script, read defensively and only for Mine (operator, 11 October
    /// 2026).
    /// </summary>
    [Fact]
    public void TheLayoutScriptCarriesASavedMineScopeOnTheWorkCentreLinks()
    {
        var script = File.ReadAllText(Path.Combine(
            RepositoryRoot.Find(), "src", "Pegasus.Web", "wwwroot", "js", "site.js"));

        var start = script.IndexOf("window.localStorage.getItem('pegasus.workCentre.scope')", StringComparison.Ordinal);
        Assert.True(start >= 0, "The saved Work Centre scope is not read in site.js.");
        var block = script[start..];
        Assert.Contains("} catch (error) {", block, StringComparison.Ordinal);
        Assert.Contains("if (scope !== 'mine') { return; }", block, StringComparison.Ordinal);
        Assert.Contains("document.querySelectorAll('a.brand, a.nav-link')", block, StringComparison.Ordinal);
        Assert.Contains("link.pathname === '/' && !link.search", block, StringComparison.Ordinal);
        Assert.Contains("link.search = '?scope=mine';", block, StringComparison.Ordinal);
    }

    /// <summary>
    /// #1800 (R572): the report's wording blocks travel with a commit only when the
    /// Engineer changed them. Rows drawn before the report stopped projecting would
    /// otherwise make the Save re-read a report that cannot be projected and refuse a
    /// commit that never touched the wording. A commit and the closing tab's beacon
    /// both drop unchanged rows, and a landed commit makes what it sent the panel's
    /// baseline. #1814 (R891): wording the Case refused because its report is not
    /// ready, known by the sentence the panel carries, does not travel again as it
    /// stands, so the saves after it land; and wording a commit left out is not
    /// marked sent when that commit lands, so its section keeps it on screen.
    /// </summary>
    [Fact]
    public void TheWordingBlocksTravelWithACommitOnlyWhenTheyChanged()
    {
        var script = File.ReadAllText(Path.Combine(
            RepositoryRoot.Find(), "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));

        var helper = FunctionBody(script, "function withoutUnchangedWording(form, body) {", "    ");
        Assert.Matches(
            new Regex(@"if \(wording\.changed\(\)\) \{\s*asComposed\(panel, body\);\s*return \{ panel: panel, state: wording\.state\(\) \};"),
            helper);
        // #2042: a block still showing the sentence or heading it was drawn with travels
        // blank, which the Save stores as no change, so a section kept on screen while the
        // Case moved on stores nothing the Engineer did not write.
        var asComposed = FunctionBody(script, "function asComposed(panel, body) {", "    ");
        Assert.Contains(
            "if (text && same(text.value, row.getAttribute('data-wording-composed'))) { body.set(text.name, ''); }",
            asComposed,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (title && same(title.value, row.getAttribute('data-wording-heading'))) { body.set(title.name, ''); }",
            asComposed,
            StringComparison.Ordinal);
        Assert.Contains("if (name.indexOf('wordingEdits[') === 0) { body.delete(name); }", helper, StringComparison.Ordinal);

        var submit = FunctionBody(script, "function submitInPlace(form, submitter) {", "    ");
        Assert.Contains("var wordingSent = withoutUnchangedWording(form, body);", submit, StringComparison.Ordinal);
        Assert.Contains("wording: wordingSent", submit, StringComparison.Ordinal);
        Assert.Matches(
            @"var body = new FormData\(form\);\r?\n\s*withoutUnchangedWording\(form, body\);\r?\n\s*navigator\.sendBeacon\(form\.getAttribute\('action'\) \|\| window\.location\.href, body\);",
            script);

        Assert.Contains("command.wording.panel.pegasusWording.landed(command.wording.state);", script, StringComparison.Ordinal);
        Assert.Contains(
            "changed: function () { var now = state(); return now !== landed && now !== refused; },",
            script,
            StringComparison.Ordinal);
        Assert.Contains("landed: function (sent) { landed = sent; refused = null; },", script, StringComparison.Ordinal);
        Assert.Contains("refused: function (sent) { refused = sent; }", script, StringComparison.Ordinal);
        // #2043 (R894): the page offers refused wording again itself, once for each refused
        // wording, when a catch up shows the Case drawing its wording blocks again.
        Assert.Contains(
            "if (refused === null || refused !== state() || refused === offered) { return false; }",
            script,
            StringComparison.Ordinal);

        var swap = FunctionBody(script, "function swap(html, command, preferred, options) {", "    ");
        Assert.Matches(
            new Regex(@"var wordingRefusal = command\.wording \? command\.wording\.panel\.getAttribute\('data-wording-refusal'\) : null;\s*if \(wordingRefusal && refusal && refusal\.textContent\.indexOf\(wordingRefusal\) >= 0\) \{\s*command\.wording\.panel\.pegasusWording\.refused\(command\.wording\.state\);"),
            swap);
        Assert.Matches(
            new Regex(@"command\.sent = sendingState\(form\)\.filter\(function \(state\) \{\s*return wordingSent \|\| \(state\.control\.name \|\| ''\)\.indexOf\('wordingEdits\['\) !== 0;"),
            submit);

        Assert.Matches(
            new Regex(@"if \(keptWording && keptWording\.pegasusWording\s*&& parsed\.querySelector\('\[data-report-wording\] \[data-wording-row\]'\)\s*&& keptWording\.pegasusWording\.offerAgain\(\)\) \{\s*commitPending = true;"),
            swap);

        var panel = File.ReadAllText(Path.Combine(
            RepositoryRoot.Find(), "src", "Pegasus.Web", "Pages", "Cases", "Shared", "_CaseReport.cshtml"));
        Assert.Contains(
            "data-report-wording data-wording-refusal=\"@ReportWordingEdits.UnavailableOnPage\"",
            panel,
            StringComparison.Ordinal);
        Assert.Contains("data-wording-composed=\"@composed\" data-wording-heading=\"@heading\">", panel, StringComparison.Ordinal);
    }

    private static string FunctionBody(string script, string signature, string indent = "        ")
    {
        var start = script.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"{signature} is missing from case-workspace.js.");
        var end = script.IndexOf("\n" + indent + "}", start, StringComparison.Ordinal);
        Assert.True(end > start, $"{signature} has no closing brace at the viewer's indent.");
        return script[start..end];
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
        var root = RepositoryRoot.Find();
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

    /// <summary>
    /// A new repair spec's first commit creates it. The editor posts the
    /// written spec's id from then on and the address names it, so the catch
    /// up after the commit draws the saved spec, not a blank new one, and the
    /// next commit edits that spec rather than creating another.
    /// </summary>
    [Fact]
    public void ACommitThatWroteANewSpecMovesTheEditorAndAddressOntoIt()
    {
        var script = File.ReadAllText(Path.Combine(
            RepositoryRoot.Find(), "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));

        var written = script.IndexOf("if (carrier.matches('[data-estimate-written]')) {", StringComparison.Ordinal);
        Assert.True(written >= 0);
        var handler = script[written..script.IndexOf("return;", written, StringComparison.Ordinal)];
        Assert.Contains("form.querySelector('input[name=\"estimateId\"]')", handler, StringComparison.Ordinal);
        Assert.Contains("posted.value = carrier.value;", handler, StringComparison.Ordinal);
        Assert.Contains("address.searchParams.set('estimate', carrier.value);", handler, StringComparison.Ordinal);
        Assert.Contains("window.history.replaceState(", handler, StringComparison.Ordinal);
    }

    /// <summary>
    /// Issues 1463, 1496 and 1497 (FRD-16, R888): a jump lands in one scroll made at
    /// once, the section under the section row or its marked field centred when the
    /// field lies deeper, and never by <c>scrollIntoView</c>, which would glide and
    /// back the page up into the section before. It settles again as each section
    /// mounts, in one pass over the placeholders: those before the target are read and
    /// waited on, those after it only while the document is too short to land. A read
    /// the Case moved on under is made again, or settles the jump when its placeholder
    /// has gone. The clicked link stays current only while the jump is pending or the
    /// page stands at its bottom. What the page then does is
    /// <c>scripts/Test-CasePageBrowser.mjs</c>'s to show; this pins the shape.
    /// </summary>
    [Fact]
    public void JumpingToASectionSettlesAcrossLazyMountsAndKeepsTheClickedLinkCurrent()
    {
        var script = File.ReadAllText(Path.Combine(
            RepositoryRoot.Find(), "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));

        var mount = FunctionBody(script, "function mount(placeholder, then) {", "    ");
        var mounted = mount[mount.IndexOf("placeholder.replaceWith(host);", StringComparison.Ordinal)..];
        var waiting = mounted.IndexOf("waiting.splice(0, waiting.length)", StringComparison.Ordinal);
        var settleInMount = mounted.IndexOf("settlePendingAnchor();", StringComparison.Ordinal);
        var spyInMount = mounted.IndexOf("spy();", StringComparison.Ordinal);
        Assert.True(waiting >= 0 && waiting < settleInMount && settleInMount < spyInMount);

        var thrownAway = FunctionBody(mount, "function thrownAway() {");
        var readAgain = thrownAway.IndexOf("mount(placeholder);", StringComparison.Ordinal);
        var settledInstead = thrownAway.IndexOf("settlePendingAnchor();", StringComparison.Ordinal);
        Assert.Contains("if (placeholder.isConnected && main.contains(placeholder)) {", thrownAway, StringComparison.Ordinal);
        Assert.True(
            readAgain >= 0 && settledInstead > readAgain,
            "A read thrown away is made again while its placeholder stands, and settles the jump otherwise.");
        Assert.Equal(2, Regex.Count(mount, @"if \(!stillCurrent\(\)\) \{\s*thrownAway\(\);\s*return;"));

        // Worked out from the reading line the block holds once stuck, or a jump from the
        // top of the page stops short by the page's padding.
        var land = FunctionBody(script, "function land(host) {", "    ");
        Assert.Contains("var line = stuckLine();", land, StringComparison.Ordinal);
        Assert.Contains(
            "return isNaN(top) ? readingLine() : Math.min(readingLine(), top + block.offsetHeight);",
            FunctionBody(script, "function stuckLine() {", "    "),
            StringComparison.Ordinal);
        Assert.Contains("top = Math.max(top, ", land, StringComparison.Ordinal);
        Assert.Contains("window.scrollTo({ top: top, behavior: 'instant' });", land, StringComparison.Ordinal);
        Assert.Contains("return window.scrollY < top - 1;", land, StringComparison.Ordinal);

        var settle = FunctionBody(script, "function settlePendingAnchor() {", "    ");
        Assert.Equal(1, Regex.Count(settle, @"querySelectorAll\('\[data-lazy\]'\)"));
        var beforeTarget = settle.IndexOf("Node.DOCUMENT_POSITION_FOLLOWING) !== 0) {", StringComparison.Ordinal);
        var readBefore = settle.IndexOf("mount(placeholder);", StringComparison.Ordinal);
        var keptAfter = settle.IndexOf("after.push(placeholder);", StringComparison.Ordinal);
        Assert.True(
            beforeTarget >= 0 && readBefore > beforeTarget && keptAfter > readBefore,
            "A placeholder the target follows is read; one after it is kept for the landing to weigh.");
        Assert.Matches(new Regex(@"if \(target\.hasAttribute\('data-lazy'\)\) \{\s*mount\(target\);\s*return;"), settle);
        Assert.Contains("var short = land(target);", settle, StringComparison.Ordinal);
        Assert.Contains("var reached = after.filter(approaching);", settle, StringComparison.Ordinal);
        Assert.Contains("if (!before && !(short && reached.length)) {", settle, StringComparison.Ordinal);

        var jumpTo = FunctionBody(script, "function jumpTo(key, focus, tab, field) {", "    ");
        Assert.Contains("jumpedKey = ownerKey(key);", jumpTo, StringComparison.Ordinal);
        Assert.Contains("pendingAnchor = { key: key };", jumpTo, StringComparison.Ordinal);
        Assert.Contains("mount(target, arrive);", jumpTo, StringComparison.Ordinal);
        Assert.EndsWith("settlePendingAnchor();\n        spy();", jumpTo.ReplaceLineEndings("\n"), StringComparison.Ordinal);

        // The landing is the jump's one scroll.
        var markField = FunctionBody(script, "function markField(host, field) {", "    ");
        foreach (var body in new[] { settle, jumpTo, markField })
        {
            Assert.DoesNotContain("scrollIntoView", body, StringComparison.Ordinal);
            Assert.DoesNotContain("scrollTo(", body, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("function pendingLazyFor(", script, StringComparison.Ordinal);

        var spy = FunctionBody(script, "function spy() {", "    ");
        Assert.Contains(
            "var atBottom = window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - 40;",
            spy,
            StringComparison.Ordinal);
        var heldJump = spy.IndexOf("if (jumpedKey && (pendingAnchor || atBottom)) {", StringComparison.Ordinal);
        var releasedJump = spy.IndexOf("jumpedKey = null;", StringComparison.Ordinal);
        Assert.True(
            heldJump >= 0 && releasedJump > heldJump,
            "The jumped link is held only while the jump is pending or the page is clamped at its bottom, and released otherwise.");

        Assert.Contains(
            "window.addEventListener(name, function () { pendingAnchor = null; jumpedKey = null; navigationVersion += 1; }, { passive: true });",
            script,
            StringComparison.Ordinal);
        // A resize lands a jump still settling again and lets go only a link held at the bottom.
        Assert.Matches(
            new Regex(@"window\.addEventListener\('resize', function \(\) \{\s*if \(!pendingAnchor\) \{\s*jumpedKey = null;\s*\}\s*measure\(\);\s*settlePendingAnchor\(\);\s*spy\(\);"),
            script);
    }

    /// <summary>
    /// Requests to edit (R550–R557, 30 October 2026): the asking page claims the Case only when
    /// the long poll says its request was accepted and the viewer now holds the lease, never
    /// because it saw the Case free; the holder's Accept waits for a change not yet sent to land;
    /// the asking page withdraws its request as it goes; and Escape does not close the popup.
    /// </summary>
    [Fact]
    public void TheEditRequestWatchClaimsOnlyAnAcceptedHandOverAndAcceptWaitsForTheCommit()
    {
        var script = File.ReadAllText(Path.Combine(
            RepositoryRoot.Find(), "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));

        var watch = FunctionBody(script, "function onEditWatch(answer) {", "    ");
        var accepted = watch.IndexOf("if (state === 'accepted' && holder && holder.isViewer) {", StringComparison.Ordinal);
        Assert.True(accepted >= 0, "The asking page acts on an accepted request the viewer now holds.");
        var claimed = watch.IndexOf("enterHandedLease();", StringComparison.Ordinal);
        Assert.True(claimed > accepted, "The hand-over is taken up only inside the accepted branch.");
        Assert.DoesNotContain("requestSubmit", watch, StringComparison.Ordinal);
        Assert.DoesNotContain("requestSubmit", FunctionBody(script, "function bindEditWatch() {", "    "), StringComparison.Ordinal);
        // The hand-over is taken up from one place, and only the accepted branch reaches it.
        Assert.Equal(2, Regex.Count(script, @"enterHandedLease\("));
        var enter = FunctionBody(script, "function enterHandedLease() {", "    ");
        Assert.Contains("record.querySelector('[data-case-edit-form]')", enter, StringComparison.Ordinal);
        Assert.Contains("!claim.querySelector('[name=\"takeOver\"]')", enter, StringComparison.Ordinal);
        Assert.Contains("form.requestSubmit();", enter, StringComparison.Ordinal);
        Assert.Contains("afterCommit(function () {", enter, StringComparison.Ordinal);

        Assert.Contains("afterCommit(post, settled);", script, StringComparison.Ordinal);
        Assert.Matches(
            new Regex(@"window\.addEventListener\('pagehide', function \(\) \{\s*var withdraw = record\.querySelector\('\[data-case-edit-withdraw\]'\);\s*if \(withdraw && typeof navigator\.sendBeacon === 'function'\) \{"),
            script);
        Assert.Matches(
            new Regex(@"window\.addEventListener\('keydown', function \(event\) \{\s*var dialog = editRequestDialog\(\);\s*if \(event\.key === 'Escape' && dialog && !dialog\.hidden\) \{\s*event\.preventDefault\(\);\s*event\.stopPropagation\(\);\s*\}\s*\}, true\);"),
            script);
        // The watch follows every redraw as the heartbeat does.
        Assert.Equal(
            Regex.Count(script, @"\n\s*bindHeartbeat\(\);\r?\n\s*bindEditWatch\(\);"),
            Regex.Count(script, @"\n\s*bindHeartbeat\(\);"));
    }

    /// <summary>
    /// The valuation calculator keeps what the Save records nowhere (operator, 11 November
    /// 2026): an Other row ticked and still short of its name or amount (R934) and every control
    /// while no card is chosen (R936) are put back into a section drawn afresh, so a tick no
    /// longer disappears a second after it is made. Typing into a row ticks it (R935), a row
    /// still being typed asks for no figure, and the Previous total loss tick box, which is no
    /// field of the Case form, never stands as a change not yet sent.
    /// </summary>
    [Fact]
    public void TheValuationCalculatorPutsBackWhatTheSaveRecordsNowhere()
    {
        var script = File.ReadAllText(Path.Combine(
            RepositoryRoot.Find(), "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));

        var state = FunctionBody(script, "function otherState(row) {", "            ");
        Assert.Contains("counts: ticked && named && parseFloat(amount) > 0,", state, StringComparison.Ordinal);
        Assert.Contains("typing: ticked && !(named && parseFloat(amount) > 0),", state, StringComparison.Ordinal);

        var remember = FunctionBody(script, "function remember() {", "            ");
        Assert.Contains(".filter(function (row) { return otherState(row).typing; })", remember, StringComparison.Ordinal);
        Assert.Contains("unplaced: chosenCard() || !set ? null : {", remember, StringComparison.Ordinal);

        var putBack = FunctionBody(script, "function putBack() {", "            ");
        Assert.Contains("if (unplaced && unplaced.rows.length === all.length) {", putBack, StringComparison.Ordinal);
        Assert.Contains("filter(function (row) { return otherState(row).blank; });", putBack, StringComparison.Ordinal);
        // A card that arrived is worked out at once only into an empty Engineer's Value.
        Assert.Contains("if (chosenCard() && value && value.value === '') { schedule(); }", putBack, StringComparison.Ordinal);
        // What is put back was already sent, so it is the control's default.
        Assert.Contains("if (box) { box.checked = on; box.defaultChecked = on; }", script, StringComparison.Ordinal);
        Assert.Contains("if (box && value !== null) { box.value = value; box.defaultValue = value; }", script, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"\n\s*putBack\(\);\r?\n\s*paintAdditions\(\);\r?\n\s*paintPriorTotalLoss\(\);"), script);
        // Leaving the edit lets go of what was kept.
        Assert.Contains("if (!document.querySelector('[data-valuation-form]')) { kept = null; }", script, StringComparison.Ordinal);

        Assert.Contains(
            "if (toggle && typed !== toggle && event.isTrusted && typed.value !== '' && !toggle.checked) {",
            script,
            StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(script, @"remember\(\);\r?\n\s*if \(stillTyping\((control|typed)\)\) \{\r?\n\s*return;"));
        Assert.Contains("ptlToggle.defaultChecked = ptlToggle.checked;", script, StringComparison.Ordinal);
    }

    /// <summary>
    /// Taking a snapshot of a Case video updates Files, notices, ribbon and Case
    /// version in place while keeping the video viewer open at its playback
    /// timestamp so several frames can be captured in one pass (operator,
    /// 11 November 2026; R945).
    /// </summary>
    [Fact]
    public void TakingAVideoSnapshotUpdatesFilesAndVersionWhileKeepingTheViewerOpen()
    {
        var script = File.ReadAllText(Path.Combine(
            RepositoryRoot.Find(), "src", "Pegasus.Web", "wwwroot", "js", "case-workspace.js"));

        var applyStart = script.IndexOf("window.pegasusCaseWorkspaceApplySnapshot = function (html) {", StringComparison.Ordinal);
        Assert.True(applyStart >= 0, "window.pegasusCaseWorkspaceApplySnapshot is missing from case-workspace.js.");
        var applyEnd = script.IndexOf("\n    };", applyStart, StringComparison.Ordinal);
        var apply = script[applyStart..applyEnd];
        Assert.Contains("['[data-case-notices]', '[data-case-ribbon-facts]', '[data-case-ribbon-actions]', '[data-case-aside]']", apply, StringComparison.Ordinal);
        Assert.Contains("var nextFiles = parsed.getElementById('section-files');", apply, StringComparison.Ordinal);
        Assert.Contains("if (activeTab) { openSubTab(nextFiles, activeTab); }", apply, StringComparison.Ordinal);
        Assert.Contains("var version = form.querySelector('[name=\"expectedVersion\"], [name=\"expectedCaseVersion\"]');", apply, StringComparison.Ordinal);
        Assert.Contains("if (incoming.getAttribute('data-case-editing') === 'true') { redrawWanted = true; }", apply, StringComparison.Ordinal);
        Assert.DoesNotContain("data-case-viewer-host", apply, StringComparison.Ordinal);

        var snapshot = FunctionBody(script, "function snapshotVideo() {", "        ");
        Assert.Contains("video.pause();", snapshot, StringComparison.Ordinal);
        Assert.Contains("context2d.drawImage(video, 0, 0, canvas.width, canvas.height);", snapshot, StringComparison.Ordinal);
        Assert.Contains("canvas.toBlob(function (blob) {", snapshot, StringComparison.Ordinal);
        Assert.Contains("body.set('videoOccurrenceId', item.occurrenceId);", snapshot, StringComparison.Ordinal);
        Assert.Contains("body.set('timestampMs', String(timestampMs));", snapshot, StringComparison.Ordinal);
        Assert.Contains("body.set('operationKey', snapshotBaseKey + '-' + snapshotSequence);", snapshot, StringComparison.Ordinal);
        Assert.Contains("done = window.pegasusCaseWorkspaceApplySnapshot(html);", snapshot, StringComparison.Ordinal);
        Assert.Contains("data-evidence-occurrence=\"' + item.occurrenceId + '\"'", snapshot, StringComparison.Ordinal);
    }
}
