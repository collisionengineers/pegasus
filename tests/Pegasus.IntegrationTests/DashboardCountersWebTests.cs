using System.Net;
using System.Text.RegularExpressions;

namespace Pegasus.IntegrationTests;

/// <summary>
/// V26 (Work Centre D7): the Work Centre's five metrics are
/// page-queried figures, each an exact link to the Cases tab behind it. The
/// fifth, Triages, counts the active Triage Cases.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class DashboardCountersWebTests
{
    [Fact]
    public async Task EveryMetricLinksToItsCasesTab()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        foreach (var key in new[] { "not_ready", "review", "held", "triage" })
        {
            Assert.Contains($"data-value=\"{key}\" href=\"/Cases?tab={key}\"", html);
        }
        // The Unidentified metric opens Unassigned Images on its Unidentified items.
        Assert.Contains(
            "data-value=\"unidentified\" href=\"/Cases?tab=unassigned&amp;show=unidentified\"",
            html,
            StringComparison.Ordinal);

        Assert.Equal(5, Regex.Count(html, "class=\"metric\" data-value="));
    }

    /// <summary>
    /// A link whose `asp-page` names a page that does not exist
    /// renders `href=""`. Whatever the Work Centre draws, no anchor may carry
    /// an empty href.
    /// </summary>
    [Fact]
    public async Task TheWorkCentreRendersNoEmptyLink()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(" href=\"\"", html, StringComparison.Ordinal);
    }
}
