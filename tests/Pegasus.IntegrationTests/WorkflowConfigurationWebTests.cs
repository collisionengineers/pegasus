using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class WorkflowConfigurationWebTests
{
    private const string Route = "/Administration/Configuration";

    [Fact]
    public async Task AdministratorSeesActualWorkflowValuesAndExplicitEdit()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var html = await GetPageAsync(client);
        Assert.Contains("Case workflow", html, StringComparison.Ordinal);
        Assert.Contains("Chase interval", html, StringComparison.Ordinal);
        Assert.Contains("Labour-rate cards", html, StringComparison.Ordinal);
        Assert.Contains("handler=Edit", html, StringComparison.Ordinal);
        Assert.DoesNotContain("There are no editable settings", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"LeaseToken\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveRequiresLeaseAndCancelDiscardsConfiguredValues()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var html = await GetPageAsync(client);
        var id = Pegasus.Core.Workflow.GetWorkflowConfiguration.RecordId.ToString("D");
        using var edit = await client.PostAsync(Route + "?handler=Edit", new FormUrlEncodedContent(new Dictionary<string,string>
        {
            ["recordId"] = id, ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken")
        }));
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var editor = await edit.Content.ReadAsStringAsync();
        var fields = new Dictionary<string,string>
        {
            ["EditingId"] = id, ["ExpectedVersion"] = Field(editor, "ExpectedVersion"),
            ["LeaseToken"] = Field(editor, "LeaseToken"), ["OperationKey"] = Field(editor, "OperationKey"),
            ["__RequestVerificationToken"] = Field(editor, "__RequestVerificationToken"),
            ["RequireInstructions"] = "true", ["RequireImages"] = "false", ["ChaseIntervalDays"] = "12",
            ["Reason"] = "Change the schedule"
        };
        var forged = new Dictionary<string,string>(fields) { ["LeaseToken"] = new string('a', 64) };
        using var refused = await client.PostAsync(Route + "?handler=Save", new FormUrlEncodedContent(forged));
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains("could not be saved", await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        using var cancel = await client.PostAsync(Route + "?handler=Cancel", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, cancel.StatusCode);
        Assert.Contains("7 days", await GetPageAsync(client), StringComparison.Ordinal);
        using var again = await client.PostAsync(Route + "?handler=Edit", new FormUrlEncodedContent(new Dictionary<string,string>
        {
            ["recordId"] = id, ["__RequestVerificationToken"] = fields["__RequestVerificationToken"]
        }));
        editor = await again.Content.ReadAsStringAsync();
        fields["LeaseToken"] = Field(editor, "LeaseToken");
        fields["OperationKey"] = Field(editor, "OperationKey");
        using var saved = await client.PostAsync(Route + "?handler=Save", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Contains("12 days", await GetPageAsync(client), StringComparison.Ordinal);
    }

    private static string Field(string html, string name)
    {
        var tag = Regex.Match(html, "<input[^>]*name=\"" + Regex.Escape(name) + "\"[^>]*>");
        Assert.True(tag.Success, "Missing field " + name);
        return WebUtility.HtmlDecode(Regex.Match(tag.Value, "value=\"([^\"]*)\"").Groups[1].Value);
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
