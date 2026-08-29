using System.Net;
using System.Text.RegularExpressions;

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
        Assert.Contains("<h2 id=\"workflow-configuration-title\">Workflow configuration</h2>", html, StringComparison.Ordinal);
        Assert.Contains("Staff review requirements", html, StringComparison.Ordinal);
        Assert.Contains("2 settings", html, StringComparison.Ordinal);

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
    public async Task NonAdministratorCannotReachWorkflowConfiguration()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "User");

        using var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

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

    [GeneratedRegex("<input[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InputTagRegex();

    [GeneratedRegex(
        "\\bvalue=\"(?<value>[^\"]*)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ValueAttributeRegex();
}
