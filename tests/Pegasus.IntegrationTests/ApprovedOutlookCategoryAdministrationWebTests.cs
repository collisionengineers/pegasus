using System.Net;
using System.Text.RegularExpressions;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class ApprovedOutlookCategoryAdministrationWebTests
{
    [Fact]
    public async Task NonAdministratorCannotOpenCatalogue()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "User");

        using var response = await client.GetAsync("/Administration/MailCategories");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdministratorAddsDisplayNameWithoutGraphMetadata()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var page = await GetPageAsync(client);

        using var response = await client.PostAsync(
            "/Administration/MailCategories?handler=Save",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["CategoryId"] = Value(CategoryIdRegex().Matches(page)[^1].Value),
                ["ExpectedVersion"] = "0",
                ["OperationKey"] = Value(OperationKeyRegex().Matches(page)[^1].Value),
                ["DisplayName"] = "Awaiting engineer",
                ["SelectedState"] = "Active",
                ["Reason"] = "Approve the display name for exact-message actions",
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

    private static async Task<string> GetPageAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/Administration/MailCategories");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static string Value(string tag)
    {
        var match = ValueRegex().Match(tag);
        Assert.True(match.Success);
        return match.Groups["value"].Value;
    }

    [GeneratedRegex("<input[^>]*name=\"CategoryId\"[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex CategoryIdRegex();
    [GeneratedRegex("<input[^>]*name=\"OperationKey\"[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex OperationKeyRegex();
    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex AntiforgeryRegex();
    [GeneratedRegex("value=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase)] private static partial Regex ValueRegex();
}
