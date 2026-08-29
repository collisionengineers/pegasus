using System.Net;
using System.Text.RegularExpressions;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class ApprovedOutlookCategoryAdministrationWebTests
{
    [Fact]
    public async Task NonAdministratorCannotOpenMailSettings()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "User");

        using var response = await client.GetAsync("/Administration/Mailboxes");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NonAdministratorCannotPostCategoryChange()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "User");

        using var response = await client.PostAsync(
            "/Administration/Mailboxes?handler=SaveCategory",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SupersededCategoryRouteRedirectsToMailSettings()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/Administration/MailCategories");

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("/Administration/Mailboxes", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task AdministratorAddsDisplayNameWithoutGraphMetadata()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var page = await GetPageAsync(client);

        using var response = await client.PostAsync(
            "/Administration/Mailboxes?handler=SaveCategory",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["CategoryForm.CategoryId"] = Value(CategoryIdRegex().Matches(page)[^1].Value),
                ["CategoryForm.ExpectedVersion"] = "0",
                ["CategoryForm.OperationKey"] = Value(OperationKeyRegex().Matches(page)[^1].Value),
                ["CategoryForm.DisplayName"] = "Awaiting engineer",
                ["CategoryForm.SelectedState"] = "Active",
                ["CategoryForm.Reason"] = "Approve the display name for exact-message actions",
                ["__RequestVerificationToken"] = Value(AntiforgeryRegex().Match(page).Value)
            }));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var reloaded = await GetPageAsync(client);
        Assert.Contains("Awaiting engineer", reloaded, StringComparison.Ordinal);
        Assert.DoesNotContain("Graph", reloaded, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Category color", reloaded, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"Color\"", reloaded, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("category identity", reloaded, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdministratorReplaysAddDisablesAndSeesStaleAndOperationConflicts()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var page = await GetPageAsync(client);
        var add = Form(
            page,
            Value(CategoryIdRegex().Matches(page)[^1].Value),
            "0",
            Value(OperationKeyRegex().Matches(page)[^1].Value),
            "Awaiting engineer",
            "Active",
            "Approve the exact display name");

        using (var created = await PostAsync(client, add))
            Assert.Equal(HttpStatusCode.Found, created.StatusCode);
        using (var replay = await PostAsync(client, add))
            Assert.Equal(HttpStatusCode.Found, replay.StatusCode);

        page = await GetPageAsync(client);
        var categoryId = Value(CategoryIdRegex().Match(page).Value);
        var originalVersion = Value(ExpectedVersionRegex().Match(page).Value);
        var disableOperation = Value(OperationKeyRegex().Match(page).Value);
        var disable = Form(
            page, categoryId, originalVersion, disableOperation,
            "Awaiting engineer", "Disabled", "Retire the exact display name");
        using (var disabled = await PostAsync(client, disable))
            Assert.Equal(HttpStatusCode.Found, disabled.StatusCode);

        var stale = Form(
            await GetPageAsync(client), categoryId, originalVersion,
            Guid.NewGuid().ToString("N"),
            "Awaiting engineer", "Active", "Attempt a stale update");
        using (var response = await PostAsync(client, stale))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(
                "The category policy changed. Review it and retry.",
                await response.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
        }

        var reused = disable.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        reused["CategoryForm.DisplayName"] = "A different category";
        using (var response = await PostAsync(client, reused))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(
                "This form was already used for another change. Review and retry.",
                await response.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
        }

        Assert.Contains(">Disabled<", await GetPageAsync(client), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdministratorValidationKeepsCatalogueUnchanged()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var page = await GetPageAsync(client);
        var invalid = Form(
            page,
            Value(CategoryIdRegex().Matches(page)[^1].Value),
            "0",
            Value(OperationKeyRegex().Matches(page)[^1].Value),
            "",
            "Unsupported",
            "");

        using var response = await PostAsync(client, invalid);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Select a supported state.", body, StringComparison.Ordinal);
        Assert.Contains("No mail categories", body, StringComparison.Ordinal);
    }

    private static async Task<string> GetPageAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/Administration/Mailboxes");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static string Value(string tag)
    {
        var match = ValueRegex().Match(tag);
        Assert.True(match.Success);
        return match.Groups["value"].Value;
    }

    private static Dictionary<string, string> Form(
        string page,
        string categoryId,
        string expectedVersion,
        string operationKey,
        string displayName,
        string state,
        string reason) => new()
    {
        ["CategoryForm.CategoryId"] = categoryId,
        ["CategoryForm.ExpectedVersion"] = expectedVersion,
        ["CategoryForm.OperationKey"] = operationKey,
        ["CategoryForm.DisplayName"] = displayName,
        ["CategoryForm.SelectedState"] = state,
        ["CategoryForm.Reason"] = reason,
        ["__RequestVerificationToken"] = Value(AntiforgeryRegex().Match(page).Value)
    };

    private static Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        IReadOnlyDictionary<string, string> form) =>
        client.PostAsync(
            "/Administration/Mailboxes?handler=SaveCategory",
            new FormUrlEncodedContent(form));

    [GeneratedRegex("<input[^>]*name=\"CategoryForm\\.CategoryId\"[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex CategoryIdRegex();
    [GeneratedRegex("<input[^>]*name=\"CategoryForm\\.OperationKey\"[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex OperationKeyRegex();
    [GeneratedRegex("<input[^>]*name=\"CategoryForm\\.ExpectedVersion\"[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex ExpectedVersionRegex();
    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex AntiforgeryRegex();
    [GeneratedRegex("value=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase)] private static partial Regex ValueRegex();
}
