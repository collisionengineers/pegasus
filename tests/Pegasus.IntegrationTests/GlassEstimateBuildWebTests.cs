using System.Net;
using System.Text.Json;
using Pegasus.Core.Assessment;
using GlassLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.GlassSession;
using static Pegasus.IntegrationTests.GlassRepairEstimateCallbackWebTests;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Glass's on a repair spec made outside Pegasus (operator, 18 November 2026;
/// R1171 to R1173), through the host's own composition: the question, its
/// two answers, the window while the build runs, and the list on the spec
/// the build landed as.
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
    /// building and how far it has gone, then hands back to the Case, where
    /// the new Glass's spec stands beside the outside one with its list.
    /// </summary>
    [Fact]
    public async Task ImportAndOpenBuildsTheSpecAndListsWhatWasSent()
    {
        // A short pause between the editor's requests keeps the build running
        // while the window is first read.
        await using var workspace = await Workspace.CreateAsync(editorPauseMilliseconds: 250);
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

        using var settled = await workspace.FollowAsync(opening);

        await AssertHandsBackToTheEstimateSectionAsync(settled, workspace.CaseId);
        var session = Assert.Single(await workspace.SessionsAsync());
        Assert.Equal(GlassRepairEstimateSessionState.Completed, session.State);
        var build = Assert.IsType<GlassEstimateBuild>(session.Build);
        Assert.Equal(outside, build.SourceSpecificationId);
        var built = Assert.IsType<Guid>(build.BuiltSpecificationId);
        var estimates = await workspace.EstimatesAsync();
        Assert.Equal(2, estimates.Count);
        Assert.Equal(
            RepairSpecificationSourceRoute.Glasses,
            Assert.Single(estimates, estimate => estimate.SpecificationId == built).Source.Route);

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
    }
}
