using System.Net;
using System.Text.RegularExpressions;

namespace Pegasus.IntegrationTests;

/// <summary>
/// UIIMP-008, v26 (Work Centre D7): the Work Centre's four metrics are
/// page-queried figures, each an exact link to the Cases tab behind it. Blocked
/// is no longer an operator concept, so no Blocked metric is drawn.
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

        foreach (var key in new[] { "not_ready", "review", "held", "unidentified" })
        {
            Assert.Contains($"data-value=\"{key}\" href=\"/Cases?tab={key}\"", html);
        }

        Assert.Equal(4, Regex.Count(html, "class=\"metric\" data-value="));
        Assert.DoesNotContain("data-value=\"blocked\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// UIIMP-008: a link whose `asp-page` names a page that does not exist
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
