using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class WorkflowConfigurationWebTests
{
    private const string Route = "/Administration/Configuration";

    [Fact]
    public async Task AdministratorSeesReadOnlyPolicyVersionWithoutReviewControls()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetPageAsync(client);

        Assert.Contains("<div class=\"admin-layout\">", html, StringComparison.Ordinal);
        Assert.Matches(CurrentAreaLinkRegex(), html);
        // The area label is the panel heading (§1.12); the page heading is the
        // administration area itself, so the same words never stack twice.
        Assert.Contains("<h1>Administration</h1>", html, StringComparison.Ordinal);
        Assert.Contains("<h2 id=\"workflow-configuration-title\">Workflow configuration</h2>", html, StringComparison.Ordinal);
        Assert.Single(
            HeadingRegex().Matches(html).Cast<Match>(),
            heading => heading.Groups["text"].Value.Trim() == "Workflow configuration");
        Assert.Matches(PolicyVersionMetaRegex(), html);
        Assert.DoesNotContain("workflow-review-title", html, StringComparison.Ordinal);
        Assert.DoesNotContain("RequireStaffInstructionReviewBeforeEngineerAssignment", html, StringComparison.Ordinal);
        Assert.DoesNotContain("RequireStaffImageReviewBeforeEngineerAssignment", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"Reason\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Save configuration", html, StringComparison.Ordinal);
        Assert.DoesNotMatch(ConfigurationFormRegex(), html);

        Assert.DoesNotContain("Relaxing a gate applies", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Instruction document required", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Eligible images required", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Chase interval", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ComposedAutomationIsListedInThisPageAdministrationRail()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var mcpFactory = AutomationMcpTestSupport.WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost:7139")
            });

        // The sibling page proves the ingress really is composed in this host,
        // so the assertion below cannot pass by both rails being short.
        var siblingRail = await AdminRailAsync(client, "/Administration");
        Assert.Contains("Automation &amp; AI", siblingRail, StringComparison.Ordinal);

        // This page must pass the same composition through to _AdminNav, so its
        // rail lists the same areas as every sibling administration page.
        var rail = await AdminRailAsync(client, Route);
        Assert.Equal(AdminRailLinks(siblingRail), AdminRailLinks(rail));
    }

    [Fact]
    public async Task NonAdministratorCannotReachWorkflowConfiguration()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "User");

        using var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<string> AdminRailAsync(HttpClient client, string route)
    {
        using var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rail = AdminNavRegex().Match(await response.Content.ReadAsStringAsync());
        Assert.True(rail.Success, $"'{route}' must render the administration rail.");
        return rail.Value;
    }

    private static string[] AdminRailLinks(string rail) =>
        [.. AdminRailLinkRegex().Matches(rail).Select(link => link.Groups["href"].Value)];

    private static async Task<string> GetPageAsync(HttpClient client)
    {
        using var response = await client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    [GeneratedRegex(
        "<a(?=[^>]*href=\"/Administration/Configuration\")(?=[^>]*aria-current=\"page\")[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CurrentAreaLinkRegex();

    [GeneratedRegex(
        "<form(?=[^>]*method=\"post\")(?=[^>]*action=\"/Administration/Configuration\")[^>]*>[\\s\\S]*?</form>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ConfigurationFormRegex();

    [GeneratedRegex(
        "<nav[^>]*class=\"admin-nav[^\"]*\"[^>]*>[\\s\\S]*?</nav>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AdminNavRegex();

    [GeneratedRegex(
        "<a[^>]*href=\"(?<href>[^\"]*)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AdminRailLinkRegex();

    [GeneratedRegex(
        "<div class=\"panel-title-meta\">\\s*Version \\d+\\s*</div>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PolicyVersionMetaRegex();

    [GeneratedRegex(
        "<h[12][^>]*>(?<text>[^<]*)</h[12]>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HeadingRegex();

}
