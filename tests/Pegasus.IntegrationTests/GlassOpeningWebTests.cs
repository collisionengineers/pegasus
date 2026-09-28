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
        Assert.Contains("handler=ResumeGlass", html, StringComparison.Ordinal);
        Assert.Empty(workspace.Mva.Requests);
    }

    /// <summary>
    /// A return whose import never ran was claimed, so the provider may have
    /// been told nothing or everything: the window settles it Unknown, the
    /// account stays held and the owner may close it, and nothing is relayed.
    /// </summary>
    [Fact]
    public async Task AReturnWhoseImportNeverRanIsSettledUnknownAndNothingIsRelayed()
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
    }
}
