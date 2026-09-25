using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class WorkflowConfigurationWebTests
{
    private const string Route = "/Administration/Configuration";

    [Fact]
    public async Task AdministratorSeesEditableWorkflowAndRateCardForms()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var html = await GetPageAsync(client);
        Assert.Contains("Case workflow", html, StringComparison.Ordinal);
        Assert.Contains("Chase interval", html, StringComparison.Ordinal);
        Assert.Contains("Labour-rate cards", html, StringComparison.Ordinal);
        Assert.DoesNotContain("There are no editable settings", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"LeaseToken\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"WorkflowExpectedVersion\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"ChaseIntervalDays\"", html, StringComparison.Ordinal);
        Assert.Contains("handler=SaveWorkflow", html, StringComparison.Ordinal);
        Assert.Contains("handler=NewCard", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkflowSettingsSaveDirectlyAndKeepRangeValidation()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var html = await GetPageAsync(client);
        Assert.Matches("name=\"ChaseIntervalDays\"[^>]*min=\"1\"[^>]*max=\"365\"", html);
        foreach (var field in new[] { "UnidentifiedTargetDays", "TriageTargetDays", "HeldTargetDays", "ReviewTargetDays", "AiDraftTargetDays" })
        {
            Assert.Matches($"name=\"{field}\"[^>]*min=\"0\"[^>]*max=\"365\"", html);
        }

        var fields = new Dictionary<string, string>
        {
            ["WorkflowExpectedVersion"] = Field(html, "WorkflowExpectedVersion"),
            ["WorkflowOperationKey"] = Field(html, "WorkflowOperationKey"),
            ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken"),
            ["RequireInstructions"] = "true", ["RequireImages"] = "false",
            ["ChaseIntervalDays"] = "7", ["UnidentifiedTargetDays"] = "0",
            ["TriageTargetDays"] = "400", ["HeldTargetDays"] = "7",
            ["ReviewTargetDays"] = "1", ["AiDraftTargetDays"] = "1"
        };
        using var refused = await client.PostAsync(Route + "?handler=SaveWorkflow", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains("Triage target must be between 0 and 365 days.", await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        fields["TriageTargetDays"] = "3";
        fields["UnidentifiedTargetDays"] = "2";
        fields["HeldTargetDays"] = "4";
        fields["ReviewTargetDays"] = "5";
        fields["AiDraftTargetDays"] = "6";
        using var saved = await client.PostAsync(Route + "?handler=SaveWorkflow", new FormUrlEncodedContent(fields));
        Assert.True(
            saved.StatusCode == HttpStatusCode.Redirect,
            $"Expected a redirect but got {saved.StatusCode}. {await ValidationMessagesAsync(saved)}");
        var after = await GetPageAsync(client);
        Assert.Matches("name=\"TriageTargetDays\"[^>]*value=\"3\"", after);
        Assert.Matches("name=\"AiDraftTargetDays\"[^>]*value=\"6\"", after);
    }

    /// <summary>
    /// Configuration (13 September): the page renders the workflow's six
    /// editable settings and carries versioned form metadata directly on load.
    /// </summary>
    [Fact]
    public async Task WorkflowSettingsCarryVersionedFormMetadataOnLoad()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var html = await GetPageAsync(client);
        Assert.Contains("name=\"WorkflowExpectedVersion\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"WorkflowOperationKey\"", html, StringComparison.Ordinal);
    }

    private static string Field(string html, string name)
    {
        var tag = Regex.Match(html, "<input[^>]*name=\"" + Regex.Escape(name) + "\"[^>]*>");
        Assert.True(tag.Success, "Missing field " + name);
        return WebUtility.HtmlDecode(Regex.Match(tag.Value, "value=\"([^\"]*)\"").Groups[1].Value);
    }

    [Fact]
    public async Task RateCardCanBeAddedAndSavedWithoutOpeningAnEditScope()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var html = await GetPageAsync(client);
        using var opened = await client.PostAsync(Route + "?handler=NewCard", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken") }));
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        var openedHtml = await opened.Content.ReadAsStringAsync();
        Assert.DoesNotContain("WorkflowOperationKey field is required", openedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("CardOperationKey field is required", openedHtml, StringComparison.Ordinal);
        var addForm = Regex.Match(openedHtml, "<form id=\"rate-card-add\"[^>]*>[\\s\\S]*?</form>").Value;
        Assert.NotEmpty(addForm);
        var fields = new Dictionary<string, string>
        {
            ["CardId"] = Field(addForm, "CardId"),
            ["CardExpectedVersion"] = Field(addForm, "CardExpectedVersion"),
            ["CardOperationKey"] = Field(addForm, "CardOperationKey"),
            ["__RequestVerificationToken"] = Field(addForm, "__RequestVerificationToken"),
            ["CardName"] = "New integration rate",
            ["HourlyRate"] = "92.50",
            ["Enabled"] = "true"
        };
        using var saved = await client.PostAsync(Route + "?handler=SaveCard", new FormUrlEncodedContent(fields));
        Assert.True(
            saved.StatusCode == HttpStatusCode.Redirect,
            $"Expected a redirect but got {saved.StatusCode}. {await ValidationMessagesAsync(saved)}");
        Assert.Contains("New integration rate", await GetPageAsync(client), StringComparison.Ordinal);
    }

    private static async Task<string> ValidationMessagesAsync(HttpResponseMessage response)
    {
        var html = await response.Content.ReadAsStringAsync();
        var messages = Regex.Matches(html, "<li>(?<message>[\\s\\S]*?)</li>")
            .Cast<Match>()
            .Select(match => WebUtility.HtmlDecode(Regex.Replace(match.Groups["message"].Value, "<[^>]+>", string.Empty)).Trim())
            .Where(message => message.Length > 0);
        var summary = string.Join(" | ", messages);
        return summary.Length == 0 ? "No validation messages were rendered." : summary;
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
        "<nav[^>]*class=\"admin-nav[^\"]*\"[^>]*>[\\s\\S]*?</nav>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AdminNavRegex();

    [GeneratedRegex(
        "<a[^>]*href=\"(?<href>[^\"]*)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AdminRailLinkRegex();
}
