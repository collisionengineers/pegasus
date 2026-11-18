using System.Net;
using System.Text.Json;
using Pegasus.Core.Assessment;
using Pegasus.Infrastructure.Glass;
using GlassLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.GlassSession;
using static Pegasus.IntegrationTests.GlassRepairEstimateCallbackWebTests;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Glass's on a repair spec made outside Pegasus (operator, 18 November 2026;
/// R1171 to R1173), through the host's own composition: the question, its
/// two answers, the window while the build runs, the reopen of the spec the
/// build landed as in the same window, and the list on that spec.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class GlassEstimateBuildWebTests
{
    /// <summary>
    /// Glass's asks first: the slot opens the question, which offers Import
    /// and open, Open an empty estimate and Cancel in the operator's words.
    /// Each answer is its own Glass's window form, and only Import and open
    /// carries the build.
    /// </summary>
    [Fact]
    public async Task ASpecMadeOutsidePegasusAsksBeforeGlassOpens()
    {
        await using var workspace = await Workspace.CreateAsync();
        var outside = await workspace.SeedOutsideSpecAsync();
        await workspace.ClaimLeaseAsync();

        var html = await workspace.CaseHtmlAsync();
        var text = WebUtility.HtmlDecode(html);

        Assert.Contains("data-dialog-open=\"glass-build-dialog\"", html, StringComparison.Ordinal);
        Assert.Contains("dialog=glass-build", html, StringComparison.Ordinal);
        Assert.Contains("id=\"glass-build-dialog\"", html, StringComparison.Ordinal);
        Assert.Contains(GlassLabels.BuildTitle, text, StringComparison.Ordinal);
        Assert.Contains(GlassLabels.BuildPrompt, text, StringComparison.Ordinal);
        Assert.Contains(GlassLabels.BuildImport, text, StringComparison.Ordinal);
        Assert.Contains(GlassLabels.BuildEmpty, text, StringComparison.Ordinal);
        Assert.Contains("build=true", html, StringComparison.Ordinal);
        Assert.Contains("data-glass-slot=\"launch\"", html, StringComparison.Ordinal);
        Assert.Contains("data-glass-slot=\"build\"", html, StringComparison.Ordinal);
        Assert.Equal(outside.ToString("D"), FormFor(html, "LaunchGlass")["estimate"]);

        using var noScript = await workspace.Client.GetAsync(
            $"/Cases/{workspace.CaseId:D}?section=estimate&estimate={outside:D}&dialog=glass-build");
        Assert.Matches(
            "data-dialog=\"glass-build-dialog\"\\s+data-dialog-open-on-load=\"true\"",
            await noScript.Content.ReadAsStringAsync());
    }

    /// <summary>Open an empty estimate is the launch as it was: a new estimate and no build.</summary>
    [Fact]
    public async Task OpenAnEmptyEstimateIsTheLaunchAsItWas()
    {
        await using var workspace = await Workspace.CreateAsync();
        await workspace.SeedOutsideSpecAsync();
        await workspace.ClaimLeaseAsync();

        using var opened = await workspace.PostGlassAsync("LaunchGlass", await workspace.LaunchFormAsync());

        _ = ReadEstimator(opened);
        var session = Assert.Single(await workspace.SessionsAsync());
        Assert.Equal(GlassRepairEstimateSessionState.Active, session.State);
        Assert.Null(session.Build);
        Assert.Equal(0, workspace.Mva.Count("GET /ere/acolib/aco_call_xml.php"));
    }

    /// <summary>
    /// Import and open builds the spec in Glass's: the window says it is
    /// building and how far it has gone, the new Glass's spec lands beside
    /// the outside one, and the same work reopens it, so the window follows
    /// on to that session and opens the estimator with no second press
    /// (R1171). The reopen is the launch Glass's on that spec makes: no
    /// second vehicle, and one more start on the build's own.
    /// </summary>
    [Fact]
    public async Task ImportAndOpenBuildsTheSpecThenOpensItInTheSameWindow()
    {
        // A short pause between the editor's requests keeps the build running
        // while the window is first read.
        await using var workspace = await Workspace.CreateAsync(
            editorPauseMilliseconds: 250, clock: new TickingClock());
        var outside = await workspace.SeedOutsideSpecAsync();
        await workspace.ClaimLeaseAsync();
        var form = await workspace.LaunchFormAsync();

        using var posted = await workspace.Client.PostAsync(
            $"/Cases/{workspace.CaseId:D}?section=estimate&build=true&handler=LaunchGlass",
            new FormUrlEncodedContent(form));

        var opening = AssertWaitsOnTheGlassWindow(posted);
        using (var page = await workspace.Client.GetAsync(opening))
        {
            var html = WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());
            Assert.Contains(GlassLabels.Building, html, StringComparison.Ordinal);
            Assert.Contains("data-glass-opening-progress", html, StringComparison.Ordinal);
        }
        using (var state = await workspace.Client.GetAsync(opening + "?handler=State"))
        {
            using var json = JsonDocument.Parse(await state.Content.ReadAsStringAsync());
            var building = json.RootElement.GetProperty("building");
            if (building.ValueKind == JsonValueKind.String)
            {
                Assert.EndsWith(" of 1", building.GetString(), StringComparison.Ordinal);
            }
        }

        using var built = await workspace.FollowAsync(opening);

        var reopening = AssertWaitsOnTheGlassWindow(built);
        Assert.NotEqual(opening, reopening);
        using var opened = await workspace.FollowAsync(reopening);
        Assert.Equal(GlassProviderFixture.EstimatorBase.Host, ReadEstimator(opened).Host);

        var sessions = await workspace.SessionsAsync();
        Assert.Equal(2, sessions.Count);
        var (build, reopen) = (sessions[0], sessions[1]);
        Assert.Equal(GlassRepairEstimateSessionState.Completed, build.State);
        var record = Assert.IsType<GlassEstimateBuild>(build.Build);
        Assert.Equal(outside, record.SourceSpecificationId);
        var builtId = Assert.IsType<Guid>(record.BuiltSpecificationId);
        Assert.EndsWith(reopen.Id.ToString("D"), reopening, StringComparison.Ordinal);
        Assert.Equal(GlassRepairEstimateSessionState.Active, reopen.State);
        Assert.Null(reopen.Build);
        Assert.Equal(build.ProviderVehicleId, reopen.ProviderVehicleId);
        Assert.Equal(build.OperationKey + ":reopen", reopen.OperationKey);
        Assert.Equal(1, workspace.Mva.Count("GET /index/create-new-vehicle"));
        Assert.Equal(2, workspace.Mva.Count("POST /ere/start-ere"));
        var estimates = await workspace.EstimatesAsync();
        Assert.Equal(2, estimates.Count);
        Assert.Equal(
            RepairSpecificationSourceRoute.Glasses,
            Assert.Single(estimates, estimate => estimate.SpecificationId == builtId).Source.Route);

        // The reopen is part of the build: the list stays on the new spec (R1173).
        var onBuilt = await workspace.Client.GetStringAsync(
            $"/Cases/{workspace.CaseId:D}?section=estimate&estimate={builtId:D}");
        Assert.Contains("data-glass-build-list", onBuilt, StringComparison.Ordinal);
        Assert.Contains("data-glass-build-act=\"Added\"", onBuilt, StringComparison.Ordinal);
        Assert.Contains("data-glass-build-act=\"LeftOff\"", onBuilt, StringComparison.Ordinal);
        Assert.Contains("£176.96", WebUtility.HtmlDecode(onBuilt), StringComparison.Ordinal);
        Assert.DoesNotContain("glass-build-dialog", onBuilt, StringComparison.Ordinal);
        var onOutside = await workspace.Client.GetStringAsync(
            $"/Cases/{workspace.CaseId:D}?section=estimate&estimate={outside:D}");
        Assert.DoesNotContain("data-glass-build-list", onOutside, StringComparison.Ordinal);
    }

    /// <summary>
    /// The list stays through the build's own reopen, and through closing
    /// that reopen, which is not a press of Glass's; the member of staff's
    /// next press makes a third session and the list goes (R1173).
    /// </summary>
    [Fact]
    public async Task TheListStaysThroughTheReopenAndGoesAtTheNextPressOfGlasss()
    {
        await using var workspace = await Workspace.CreateAsync(clock: new TickingClock());
        await workspace.SeedOutsideSpecAsync();
        await workspace.ClaimLeaseAsync();
        using var posted = await workspace.Client.PostAsync(
            $"/Cases/{workspace.CaseId:D}?section=estimate&build=true&handler=LaunchGlass",
            new FormUrlEncodedContent(await workspace.LaunchFormAsync()));
        using var built = await workspace.FollowAsync(AssertWaitsOnTheGlassWindow(posted));
        using var opened = await workspace.FollowAsync(AssertWaitsOnTheGlassWindow(built));
        _ = ReadEstimator(opened);
        var builtId = Assert.IsType<Guid>(
            Assert.IsType<GlassEstimateBuild>((await workspace.SessionsAsync())[0].Build).BuiltSpecificationId);
        var onBuilt = $"/Cases/{workspace.CaseId:D}?section=estimate&estimate={builtId:D}";
        var html = await workspace.Client.GetStringAsync(onBuilt);
        Assert.Contains("data-glass-build-list", html, StringComparison.Ordinal);

        var close = FormFor(html, "CloseGlass");
        close["reason"] = "Closed in Glass's without saving.";
        close["externalSessionClosed"] = "true";
        using (var closed = await workspace.PostAsync("CloseGlass", close))
        {
            Assert.Equal(HttpStatusCode.Found, closed.StatusCode);
        }
        html = await workspace.Client.GetStringAsync(onBuilt);
        Assert.Contains("data-glass-build-list", html, StringComparison.Ordinal);

        using var pressed = await workspace.PostGlassAsync("LaunchGlass", FormFor(html, "LaunchGlass"));

        _ = ReadEstimator(pressed);
        Assert.Equal(3, (await workspace.SessionsAsync()).Count);
        Assert.DoesNotContain(
            "data-glass-build-list", await workspace.Client.GetStringAsync(onBuilt), StringComparison.Ordinal);
    }

    /// <summary>
    /// While the reopen after a build runs, its session is held busy: the
    /// build's window still waits, the Case offers no Close, Glass's on the
    /// Case waits on that same session rather than settling it, and Go on it
    /// sends its window back to wait. Nothing more reaches Glass's until the
    /// reopen goes on; then the build's window follows on to it.
    /// </summary>
    [Fact]
    public async Task WhileTheReopenRunsTheCaseWaitsOnItAndTheWindowFollowsOnToIt()
    {
        var reopenGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var workspace = await Workspace.CreateAsync(
            fault: new GatewayFault { ContinueLaunchGate = reopenGate }, clock: new TickingClock());
        await workspace.SeedOutsideSpecAsync();
        await workspace.ClaimLeaseAsync();
        using var posted = await workspace.Client.PostAsync(
            $"/Cases/{workspace.CaseId:D}?section=estimate&build=true&handler=LaunchGlass",
            new FormUrlEncodedContent(await workspace.LaunchFormAsync()));
        var opening = AssertWaitsOnTheGlassWindow(posted);

        var reopen = await ReopenAsync(workspace);

        Assert.Equal(GlassRepairEstimateSessionState.Prepared, reopen.State);
        Assert.True(await workspace.PendingAsync(opening));
        var html = await workspace.CaseHtmlAsync();
        Assert.DoesNotContain("handler=CloseGlass", html, StringComparison.Ordinal);
        using (var pressed = await workspace.PostAsync("LaunchGlass", FormFor(html, "LaunchGlass")))
        {
            Assert.EndsWith(reopen.Id.ToString("D"), AssertWaitsOnTheGlassWindow(pressed), StringComparison.Ordinal);
        }
        using (var early = await workspace.Client.GetAsync($"{OpeningRoute}{reopen.Id:D}?handler=Go"))
        {
            Assert.EndsWith(reopen.Id.ToString("D"), AssertWaitsOnTheGlassWindow(early), StringComparison.Ordinal);
        }
        Assert.Equal(reopen, (await workspace.SessionsAsync())[1]);
        Assert.Equal(1, workspace.Mva.Count("POST /ere/start-ere"));

        reopenGate.SetResult();
        using var built = await workspace.FollowAsync(opening);
        var reopening = AssertWaitsOnTheGlassWindow(built);
        using var opened = await workspace.FollowAsync(reopening);

        Assert.EndsWith(reopen.Id.ToString("D"), reopening, StringComparison.Ordinal);
        _ = ReadEstimator(opened);
        Assert.Equal(2, workspace.Mva.Count("POST /ere/start-ere"));
        Assert.Equal(2, (await workspace.SessionsAsync()).Count);
    }

    /// <summary>
    /// A reopen refused before it is recorded leaves the build as B1 left it:
    /// the window hands back to the Case, the new spec is there with the list
    /// of what was sent (R1173), and Glass's on it reopens it without sending
    /// any line of the build again.
    /// </summary>
    [Fact]
    public async Task AReopenThatCannotStartLeavesTheBuiltSpecForGlasssToReopen()
    {
        var buildGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fault = new GatewayFault { LaunchGate = buildGate };
        await using var workspace = await Workspace.CreateAsync(fault: fault, clock: new TickingClock());
        var outside = await workspace.SeedOutsideSpecAsync();
        await workspace.ClaimLeaseAsync();
        using var posted = await workspace.Client.PostAsync(
            $"/Cases/{workspace.CaseId:D}?section=estimate&build=true&handler=LaunchGlass",
            new FormUrlEncodedContent(await workspace.LaunchFormAsync()));
        var opening = AssertWaitsOnTheGlassWindow(posted);
        // The build is under way; the launch its reopen makes is refused.
        fault.LaunchFailure = new GlassRepairEstimateRefusalException("The reopen was refused.");
        buildGate.SetResult();

        using var settled = await workspace.FollowAsync(opening);

        await AssertHandsBackToTheEstimateSectionAsync(settled, workspace.CaseId);
        var build = Assert.Single(await workspace.SessionsAsync());
        Assert.Equal(GlassRepairEstimateSessionState.Completed, build.State);
        var built = Assert.IsType<Guid>(Assert.IsType<GlassEstimateBuild>(build.Build).BuiltSpecificationId);
        Assert.Equal(1, workspace.Mva.Count("POST /ere/start-ere"));
        var posts = workspace.Mva.Count("POST /ere/aco/compproc.php");

        var onBuilt = await workspace.Client.GetStringAsync(
            $"/Cases/{workspace.CaseId:D}?section=estimate&estimate={built:D}");
        Assert.Contains("data-glass-build-list", onBuilt, StringComparison.Ordinal);
        Assert.Contains("data-glass-build-act=\"Added\"", onBuilt, StringComparison.Ordinal);
        Assert.Contains("data-glass-build-act=\"LeftOff\"", onBuilt, StringComparison.Ordinal);
        Assert.Contains("£176.96", WebUtility.HtmlDecode(onBuilt), StringComparison.Ordinal);
        Assert.DoesNotContain("glass-build-dialog", onBuilt, StringComparison.Ordinal);
        var onOutside = await workspace.Client.GetStringAsync(
            $"/Cases/{workspace.CaseId:D}?section=estimate&estimate={outside:D}");
        Assert.DoesNotContain("data-glass-build-list", onOutside, StringComparison.Ordinal);

        fault.LaunchFailure = null;
        var form = FormFor(onBuilt, "LaunchGlass");
        Assert.Equal(built.ToString("D"), form["estimate"]);
        using var reopened = await workspace.PostGlassAsync("LaunchGlass", form);

        _ = ReadEstimator(reopened);
        Assert.Equal(2, (await workspace.SessionsAsync()).Count);
        Assert.Equal(1, workspace.Mva.Count("GET /index/create-new-vehicle"));
        Assert.Equal(2, workspace.Mva.Count("POST /ere/start-ere"));
        Assert.Equal(posts, workspace.Mva.Count("POST /ere/aco/compproc.php"));
        Assert.Equal(2, (await workspace.EstimatesAsync()).Count);
    }

    /// <summary>
    /// A reopen Glass's refuses once it is recorded fails as any reopen of a
    /// spec's estimate does, and frees the account: the build's window hands
    /// back to the Case as after any return, and Glass's on the new spec
    /// reopens it. No line of the build is sent again and no vehicle is made.
    /// </summary>
    [Fact]
    public async Task AReopenGlasssRefusesLeavesTheBuildsReturnAndGlasssOnTheNewSpecReopensIt()
    {
        await using var workspace = await Workspace.CreateAsync(clock: new TickingClock());
        // The build's own start is answered; the reopen's is refused.
        workspace.Mva.Enqueue(
            "POST /ere/start-ere",
            new Reply(HttpStatusCode.OK, GlassProviderFixture.StartEre(GlassProviderFixture.LaunchUrl())),
            new Reply(HttpStatusCode.OK, StartRefused));
        await workspace.SeedOutsideSpecAsync();
        await workspace.ClaimLeaseAsync();
        using var posted = await workspace.Client.PostAsync(
            $"/Cases/{workspace.CaseId:D}?section=estimate&build=true&handler=LaunchGlass",
            new FormUrlEncodedContent(await workspace.LaunchFormAsync()));

        using var settled = await workspace.FollowAsync(AssertWaitsOnTheGlassWindow(posted));

        await AssertHandsBackToTheEstimateSectionAsync(settled, workspace.CaseId);
        var sessions = await workspace.SessionsAsync();
        Assert.Equal(2, sessions.Count);
        Assert.Equal(GlassRepairEstimateSessionState.Completed, sessions[0].State);
        var built = Assert.IsType<Guid>(Assert.IsType<GlassEstimateBuild>(sessions[0].Build).BuiltSpecificationId);
        Assert.Equal(GlassRepairEstimateSessionState.Failed, sessions[1].State);
        Assert.Equal(GlassFailure.StartStatus, sessions[1].FailureCode);
        var posts = workspace.Mva.Count("POST /ere/aco/compproc.php");

        var form = FormFor(
            await workspace.Client.GetStringAsync($"/Cases/{workspace.CaseId:D}?section=estimate&estimate={built:D}"),
            "LaunchGlass");
        Assert.Equal(built.ToString("D"), form["estimate"]);
        using var reopened = await workspace.PostGlassAsync("LaunchGlass", form);

        _ = ReadEstimator(reopened);
        Assert.Equal(3, (await workspace.SessionsAsync()).Count);
        Assert.Equal(1, workspace.Mva.Count("GET /index/create-new-vehicle"));
        Assert.Equal(3, workspace.Mva.Count("POST /ere/start-ere"));
        Assert.Equal(posts, workspace.Mva.Count("POST /ere/aco/compproc.php"));
        Assert.Equal(2, (await workspace.EstimatesAsync()).Count);
    }

    /// <summary>Glass's answer to a start it will not make, as the gateway suite records it.</summary>
    private const string StartRefused =
        "{\"message\":\"Profile not available\",\"status\":\"error\",\"ere_url\":\"\"}";

    /// <summary>The session the build's own work recorded for its reopen, once it is there.</summary>
    private static async Task<GlassRepairEstimateSession> ReopenAsync(Workspace workspace)
    {
        for (var attempt = 0; ; attempt++)
        {
            var sessions = await workspace.SessionsAsync();
            if (sessions.Count == 2)
            {
                return sessions[1];
            }

            Assert.True(attempt < 300, "The build did not go on to reopen its estimate.");
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }
    }

    /// <summary>
    /// The suite's fixed instant, a millisecond later at each reading, so a
    /// staff member's sessions are newest in the order they were made, as
    /// they are on a real clock.
    /// </summary>
    private sealed class TickingClock : TimeProvider
    {
        private long readings;

        public override DateTimeOffset GetUtcNow() =>
            IntakeWebApplicationFactory.FixedUtcNow.AddMilliseconds(Interlocked.Increment(ref readings));
    }
}
