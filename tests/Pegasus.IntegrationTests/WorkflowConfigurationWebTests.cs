using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class WorkflowConfigurationWebTests
{
    private const string Route = "/Administration/Configuration";

    [Fact]
    public async Task AdministratorSeesTheAdminLayoutAndTheBackedConfigurationForm()
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
        Assert.Contains("Staff review requirements", html, StringComparison.Ordinal);
        Assert.Matches(PolicyVersionMetaRegex(), html);

        var form = ConfigurationFormRegex().Match(html);
        Assert.True(form.Success, "The page must post to the workflow configuration handler.");
        Assert.Equal(2, CheckboxRegex().Count(form.Value));
        Assert.Contains(
            "name=\"RequireStaffInstructionReviewBeforeEngineerAssignment\"",
            form.Value,
            StringComparison.Ordinal);
        Assert.Contains(
            "name=\"RequireStaffImageReviewBeforeEngineerAssignment\"",
            form.Value,
            StringComparison.Ordinal);
        Assert.Contains("name=\"Reason\"", form.Value, StringComparison.Ordinal);
        Assert.Contains("aria-required=\"true\"", form.Value, StringComparison.Ordinal);
        Assert.Contains("name=\"ExpectedVersion\"", form.Value, StringComparison.Ordinal);
        Assert.Contains("name=\"OperationKey\"", form.Value, StringComparison.Ordinal);
        Assert.Contains("name=\"__RequestVerificationToken\"", form.Value, StringComparison.Ordinal);
        Assert.Contains("Save configuration", form.Value, StringComparison.Ordinal);

        Assert.DoesNotContain("Relaxing a gate applies", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Instruction document required", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Eligible images required", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Chase interval", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdministratorSavesBothReviewSettingsThroughTheConfigurationHandler()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var page = await GetPageAsync(client);

        using var response = await client.PostAsync(
            Route,
            new FormUrlEncodedContent(
            [
                new("ExpectedVersion", InputValue(page, "ExpectedVersion")),
                new("OperationKey", InputValue(page, "OperationKey")),
                new("Reason", "Set the current staff review requirements"),
                new("RequireStaffInstructionReviewBeforeEngineerAssignment", bool.FalseString),
                new("RequireStaffImageReviewBeforeEngineerAssignment", bool.TrueString),
                new("__RequestVerificationToken", InputValue(page, "__RequestVerificationToken"))
            ]));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(Route, response.Headers.Location?.OriginalString);

        var reloaded = await GetPageAsync(client);
        Assert.DoesNotContain(
            "checked=\"checked\"",
            InputTag(reloaded, "RequireStaffInstructionReviewBeforeEngineerAssignment", "checkbox"),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "checked=\"checked\"",
            InputTag(reloaded, "RequireStaffImageReviewBeforeEngineerAssignment", "checkbox"),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("was recorded.", reloaded, StringComparison.Ordinal);
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

    private static string InputValue(string html, string name)
    {
        var tag = InputTag(html, name);
        var value = ValueAttributeRegex().Match(tag);
        Assert.True(value.Success, $"Input '{name}' must render a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    private static string InputTag(string html, string name, string? type = null)
    {
        var tag = InputTagRegex().Matches(html)
            .Cast<Match>()
            .FirstOrDefault(candidate =>
                HasAttribute(candidate.Value, "name", name)
                && (type is null || HasAttribute(candidate.Value, "type", type)));
        Assert.NotNull(tag);
        return tag.Value;
    }

    private static bool HasAttribute(string tag, string name, string value) =>
        Regex.IsMatch(
            tag,
            $"\\b{Regex.Escape(name)}=\"{Regex.Escape(value)}\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [GeneratedRegex(
        "<a(?=[^>]*href=\"/Administration/Configuration\")(?=[^>]*aria-current=\"page\")[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CurrentAreaLinkRegex();

    [GeneratedRegex(
        "<form(?=[^>]*method=\"post\")(?=[^>]*action=\"/Administration/Configuration\")[^>]*>[\\s\\S]*?</form>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ConfigurationFormRegex();

    [GeneratedRegex(
        "<input(?=[^>]*type=\"checkbox\")[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CheckboxRegex();

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

    [GeneratedRegex("<input[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InputTagRegex();

    [GeneratedRegex(
        "\\bvalue=\"(?<value>[^\"]*)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ValueAttributeRegex();
}
