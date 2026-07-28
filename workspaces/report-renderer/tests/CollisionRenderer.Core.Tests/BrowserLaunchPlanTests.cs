using CollisionRenderer.Core.Rendering;
using Xunit;

namespace CollisionRenderer.Core.Tests;

/// <summary>
/// Guards the browser fallback order: the bundled headless shell must stay the default,
/// the system channels must always remain in the plan (that is the whole point of the
/// fallback), and Claude Desktop's unexpanded <c>${user_config…}</c> literal — passed
/// verbatim when the optional field was never set — must read as "unset", never as a pin.
/// </summary>
public class BrowserLaunchPlanTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bundled")]
    [InlineData("BUNDLED")]
    [InlineData("download")]
    [InlineData("something-unknown")]
    [InlineData("${user_config.browser_channel}")]
    public void Defaults_to_bundled_first(string? pin)
    {
        var plan = BrowserLaunchPlan.Build(pin);

        Assert.Equal(new[] { "bundled", "msedge", "chrome" }, plan.Select(c => c.Kind).ToArray());
        Assert.Null(plan[0].Channel);
        Assert.Equal("msedge", plan[1].Channel);
        Assert.Equal("chrome", plan[2].Channel);
    }

    [Theory]
    [InlineData("msedge")]
    [InlineData("edge")]
    [InlineData("Microsoft-Edge")]
    [InlineData(" MSEDGE ")]
    public void Edge_pin_moves_edge_first_but_keeps_all_candidates(string pin)
    {
        var plan = BrowserLaunchPlan.Build(pin);

        Assert.Equal(new[] { "msedge", "bundled", "chrome" }, plan.Select(c => c.Kind).ToArray());
    }

    [Theory]
    [InlineData("chrome")]
    [InlineData("google-chrome")]
    [InlineData("Chrome")]
    public void Chrome_pin_moves_chrome_first_but_keeps_all_candidates(string pin)
    {
        var plan = BrowserLaunchPlan.Build(pin);

        Assert.Equal(new[] { "chrome", "bundled", "msedge" }, plan.Select(c => c.Kind).ToArray());
    }

    [Fact]
    public void Every_plan_contains_all_three_candidates_exactly_once()
    {
        foreach (var pin in new[] { null, "bundled", "msedge", "chrome", "nonsense" })
        {
            var kinds = BrowserLaunchPlan.Build(pin).Select(c => c.Kind).ToArray();
            Assert.Equal(3, kinds.Length);
            Assert.Equal(kinds.Distinct().Count(), kinds.Length);
        }
    }
}
