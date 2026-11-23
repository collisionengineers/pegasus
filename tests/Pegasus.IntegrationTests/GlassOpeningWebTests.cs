using System.Net;
using System.Text.Json;
using Pegasus.Core.Assessment;
using Pegasus.Infrastructure.Glass;
using GlassLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.GlassSession;
using static Pegasus.IntegrationTests.GlassRepairEstimateCallbackWebTests;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Glass's window at <c>/Integrations/Glass/Opening/{sessionId}</c>, where
/// a launch, a resume or a return waits while its provider work runs in the
/// background, through the host's own composition and the scripted provider.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class GlassOpeningWebTests
{
    /// <summary>
    /// The launch answers at once; the window says Glass's is being prepared,
    /// its state answer says only whether work runs and whether the estimator
    /// is open, and Go alone carries the estimator's address.
    /// </summary>
    [Fact]
    public async Task TheWindowWaitsForTheLaunchThenOpensTheEstimatorWithoutRenderingItsAddress()
    {
        await using var workspace = await Workspace.CreateAsync();
        await workspace.ClaimLeaseAsync();

        using var posted = await workspace.PostAsync("LaunchGlass", await workspace.LaunchFormAsync());

        var opening = AssertWaitsOnTheGlassWindow(posted);
        var estimatorHost = GlassProviderFixture.EstimatorBase.Host;
        using (var page = await workspace.Client.GetAsync(opening))
        {
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.True(page.Headers.CacheControl?.NoStore);
            var html = await page.Content.ReadAsStringAsync();
            Assert.Contains(GlassLabels.Preparing, WebUtility.HtmlDecode(html), StringComparison.Ordinal);
            Assert.Contains("data-glass-opening-state=\"" + opening + "?handler=State\"", html, StringComparison.Ordinal);
            Assert.Contains("data-glass-opening-go=\"" + opening + "?handler=Go\"", html, StringComparison.Ordinal);
            // MapStaticAssets fingerprints the file name in the rendered script URL.
            Assert.Matches(
                """<script\b[^>]*\bsrc="/js/glass-opening(?:\.[A-Za-z0-9]+)?\.js(?:\?v=[^"]*)?"[^>]*></script>""",
                html);
            Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
            Assert.DoesNotContain(estimatorHost, html, StringComparison.Ordinal);
            // v52: the window is the navless frame's one card, with the Case
            // it belongs to, one caption and no link; a browser without
            // script is carried on by a timed reload.
            AssertIsTheWaitingCard(html);
            Assert.Contains(
                "<noscript><meta http-equiv=\"refresh\" content=\"2;url=" + opening + "?handler=Go\" /></noscript>",
                html,
                StringComparison.Ordinal);
        }

        using var settled = await workspace.FollowAsync(opening);

        using (var state = await workspace.Client.GetAsync(opening + "?handler=State"))
        {
            var body = await state.Content.ReadAsStringAsync();
            using var json = JsonDocument.Parse(body);
            Assert.False(json.RootElement.GetProperty("pending").GetBoolean());
            Assert.True(json.RootElement.GetProperty("open").GetBoolean());
            Assert.DoesNotContain(estimatorHost, body, StringComparison.Ordinal);
        }
        Assert.Equal(estimatorHost, ReadEstimator(settled).Host);
        Assert.Equal(
            GlassRepairEstimateSessionState.Active,
            Assert.Single(await workspace.SessionsAsync()).State);
    }

    /// <summary>
    /// v52 G: the Case page opens the Glass's window on Pegasus's own waiting
    /// card, so it is never blank while the launch is on its way. The card
    /// names the Case, asks nothing and goes nowhere; a Case that does not
    /// exist has no window.
    /// </summary>
    [Fact]
    public async Task TheWindowOpensOnTheWaitingCardBeforeTheLaunchAnswers()
    {
        await using var workspace = await Workspace.CreateAsync();
        await workspace.ClaimLeaseAsync();
        var starting = "/Integrations/Glass/Starting/" + workspace.CaseId.ToString("D");

        Assert.Contains(
            "data-glass-starting=\"" + starting + "\"", await workspace.CaseHtmlAsync(), StringComparison.Ordinal);
        using (var page = await workspace.Client.GetAsync(starting))
        {
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.True(page.Headers.CacheControl?.NoStore);
            var html = await page.Content.ReadAsStringAsync();
            Assert.Contains(GlassLabels.Preparing, WebUtility.HtmlDecode(html), StringComparison.Ordinal);
            AssertIsTheWaitingCard(html);
            Assert.DoesNotContain("data-glass-opening-state", html, StringComparison.Ordinal);
            Assert.DoesNotContain("http-equiv=\"refresh\"", html, StringComparison.Ordinal);
        }

        using var unknown = await workspace.Client.GetAsync(
            "/Integrations/Glass/Starting/" + Guid.NewGuid().ToString("D"));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    /// <summary>
    /// The window's card as v52 settled it: the navless frame, the title, the
    /// Case's reference and registration, the status, the one caption, and no
    /// link anywhere on the page.
    /// </summary>
    private static void AssertIsTheWaitingCard(string html)
    {
        var text = WebUtility.HtmlDecode(html);
        Assert.Contains("class=\"external-shell\"", html, StringComparison.Ordinal);
        Assert.Contains("<h1>" + GlassLabels.Launch + "</h1>", text, StringComparison.Ordinal);
        Assert.Matches(
            "<p class=\"glass-case\">[^<·]+ · " + GlassProviderFixture.Registration + "</p>", text);
        // The status carries its words alone: site.js's busy step draws the
        // loader beside them (BusyButtonContractTests), so the page has none.
        Assert.Matches("""<p class="glass-wait" data-glass-wait\b""", html);
        Assert.DoesNotContain("icon-loader", html, StringComparison.Ordinal);
        Assert.Contains("<p>" + GlassLabels.KeepOpen + "</p>", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<a ", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// While the launch's work runs, a second click of the same form waits on
    /// that same work rather than settling it, and the session cannot be
    /// closed: the Close control is absent and a posted Close is refused, so
    /// nothing the launch creates at the provider is left without a session.
    /// </summary>
    [Fact]
    public async Task WhileTheLaunchRunsASecondClickWaitsOnItAndTheSessionCannotBeClosed()
    {
        var launchGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var workspace = await Workspace.CreateAsync(fault: new GatewayFault { LaunchGate = launchGate });
        await workspace.ClaimLeaseAsync();
        var form = await workspace.LaunchFormAsync();

        using var first = await workspace.PostAsync("LaunchGlass", form);
        using var second = await workspace.PostAsync("LaunchGlass", form);

        var opening = AssertWaitsOnTheGlassWindow(first);
        Assert.Equal(opening, AssertWaitsOnTheGlassWindow(second));
        Assert.True(await workspace.PendingAsync(opening));
        using (var early = await workspace.Client.GetAsync(opening + "?handler=Go"))
        {
            Assert.Equal(opening, AssertWaitsOnTheGlassWindow(early));
        }
        var running = Assert.Single(await workspace.SessionsAsync());
        Assert.Equal(GlassRepairEstimateSessionState.Prepared, running.State);
        Assert.Null(running.FailureCode);

        var html = await workspace.CaseHtmlAsync();
        Assert.DoesNotContain("handler=CloseGlass", html, StringComparison.Ordinal);
        var close = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["__RequestVerificationToken"] = FormFor(html, "LaunchGlass")["__RequestVerificationToken"],
            ["sessionId"] = running.Id.ToString("D"),
            ["expectedSessionVersion"] = running.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["reason"] = "Closed in Glass's.",
            ["externalSessionClosed"] = "true",
        };
        using (var refused = await workspace.PostAsync("CloseGlass", close))
        {
            Assert.Equal(HttpStatusCode.Found, refused.StatusCode);
        }
        Assert.Equal(running, Assert.Single(await workspace.SessionsAsync()));
        Assert.Contains(
            GlassLabels.CloseWhileWorking,
            WebUtility.HtmlDecode(await workspace.CaseHtmlAsync()),
            StringComparison.Ordinal);

        launchGate.SetResult();
        using var opened = await workspace.FollowAsync(opening);

        _ = ReadEstimator(opened);
        Assert.Equal(1, workspace.Mva.Count("POST /ere/start-ere"));
        Assert.Equal(
            GlassRepairEstimateSessionState.Active,
            Assert.Single(await workspace.SessionsAsync()).State);
        Assert.Contains("handler=CloseGlass", await workspace.CaseHtmlAsync(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Another staff member's session is not there at all, whether the page,
    /// its state or its Go is asked for, and nothing about it changes; a
    /// signed-out browser is sent to sign in.
    /// </summary>
    [Fact]
    public async Task OnlyTheOwnerCanSeeOrMoveTheWindow()
    {
        await using var workspace = await Workspace.CreateAsync();
        await workspace.SeedAnotherEngineersSessionAsync();
        var session = Assert.Single(await workspace.SessionsAsync());
        var opening = OpeningRoute + session.Id.ToString("D");

        foreach (var address in new[] { opening, opening + "?handler=State", opening + "?handler=Go" })
        {
            using var refused = await workspace.Client.GetAsync(address);
            Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        }
        using (var missing = await workspace.Client.GetAsync(OpeningRoute + Guid.NewGuid().ToString("D")))
        {
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
        using var anonymous = new HttpRequestMessage(HttpMethod.Get, opening + "?handler=State");
        anonymous.Headers.Add("X-Test-Anonymous", "true");
        using var challenged = await workspace.Client.SendAsync(anonymous);
        Assert.Equal(HttpStatusCode.Redirect, challenged.StatusCode);
        Assert.StartsWith("/Account/SignIn", challenged.Headers.Location!.OriginalString, StringComparison.Ordinal);

        Assert.Equal(session, Assert.Single(await workspace.SessionsAsync()));
        Assert.Empty(workspace.Mva.Requests);
    }

    /// <summary>
    /// A launch whose work never ran — the host stopped straight after the
    /// claim — is settled when the window asks: it stays Prepared and
    /// resumable, and the Case says the estimate did not start.
    /// </summary>
    [Fact]
    public async Task APreparedLaunchWhoseWorkNeverRanStaysResumableAndSaysItDidNotStart()
    {
        await using var workspace = await Workspace.CreateAsync();
        await workspace.ClaimLeaseAsync();
        var prepared = await workspace.PrepareLaunchWithoutWorkAsync();
        var opening = OpeningRoute + prepared.Id.ToString("D");

        Assert.False(await workspace.PendingAsync(opening));
        using var settled = await workspace.FollowAsync(opening);

        await AssertHandsBackToTheEstimateSectionAsync(settled, workspace.CaseId);
        var session = Assert.Single(await workspace.SessionsAsync());
        Assert.Equal(GlassRepairEstimateSessionState.Prepared, session.State);
        Assert.Equal(GlassFailure.Interrupted, session.FailureCode);
        var html = WebUtility.HtmlDecode(await workspace.CaseHtmlAsync());
        Assert.Contains(GlassLabels.LaunchRefused, html, StringComparison.Ordinal);
        Assert.Contains("handler=LaunchGlass", html, StringComparison.Ordinal);
        Assert.Empty(workspace.Mva.Requests);
    }

    /// <summary>
    /// A return whose import never ran — the host stopped while it was queued —
    /// is settled Unknown by the window: the account stays held and the owner
    /// may close it, and nothing is relayed. The claim kept the provider's
    /// message, so Resume makes the relay once and the estimate is recorded.
    /// </summary>
    [Fact]
    public async Task AReturnWhoseImportNeverRanIsSettledUnknownAndResumeRelaysItOnce()
    {
        await using var workspace = await Workspace.CreateAsync();
        await workspace.ClaimLeaseAsync();
        var correlation = await workspace.LaunchAndReadCorrelationAsync();
        var accepted = await workspace.AcceptReturnWithoutWorkAsync(correlation);
        var opening = OpeningRoute + accepted.Id.ToString("D");

        using (var page = await workspace.Client.GetAsync(opening))
        {
            Assert.Contains(
                GlassLabels.BringingBack,
                WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync()),
                StringComparison.Ordinal);
        }
        using var settled = await workspace.FollowAsync(opening);

        await AssertHandsBackToTheEstimateSectionAsync(settled, workspace.CaseId);
        var session = Assert.Single(await workspace.SessionsAsync());
        Assert.Equal(GlassRepairEstimateSessionState.Unknown, session.State);
        Assert.Equal(GlassFailure.Interrupted, session.FailureCode);
        Assert.True(GlassRepairEstimateSessionPolicy.OccupiesAccount(session.State));
        var html = WebUtility.HtmlDecode(await workspace.CaseHtmlAsync());
        Assert.Contains(GlassLabels.OutcomeUnknown, html, StringComparison.Ordinal);
        Assert.Contains("handler=CloseGlass", html, StringComparison.Ordinal);
        Assert.Equal(0, workspace.Mva.Count("GET /ere/ere-callback/"));
        Assert.Empty(await workspace.EstimatesAsync());

        using var resumed = await workspace.PostGlassAsync("LaunchGlass", await workspace.LaunchFormAsync());

        await AssertHandsBackToTheEstimateSectionAsync(resumed, workspace.CaseId);
        Assert.Equal(
            GlassRepairEstimateSessionState.Completed,
            Assert.Single(await workspace.SessionsAsync()).State);
        Assert.Equal(1, workspace.Mva.Count("GET /ere/ere-callback/"));
        Assert.Single(await workspace.EstimatesAsync());
    }
}
