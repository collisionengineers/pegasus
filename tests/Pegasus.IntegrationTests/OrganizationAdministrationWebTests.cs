using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Cases;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class OrganizationAdministrationWebTests
{
    [Fact]
    public async Task AdministratorRoutesAreDiscoverableAndPostThroughCoreEfCallers()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        using var landingResponse = await client.GetAsync("/Administration");
        var landingHtml = await landingResponse.Content.ReadAsStringAsync();
        landingResponse.EnsureSuccessStatusCode();
        Assert.DoesNotContain("/Administration/Organizations", landingHtml, StringComparison.Ordinal);
        Assert.Contains("/Administration/Principals", landingHtml, StringComparison.Ordinal);

        var claimSourcesHtml = await IntakeWebDriver.GetHtmlAsync(
            client,
            "/Administration/ClaimSources");
        Assert.Contains("Create claim source", claimSourcesHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Current claim sources", claimSourcesHtml, StringComparison.Ordinal);
        var claimSourceForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(
                claimSourcesHtml,
                "__RequestVerificationToken"),
            ["OperationKey"] = InputValue(claimSourcesHtml, "OperationKey"),
            ["Name"] = "Web Caller Claim Source",
            ["Reason"] = "Web caller claim source proof"
        };
        using var claimSourcePost = await client.PostAsync(
            "/Administration/ClaimSources?handler=Create",
            new FormUrlEncodedContent(claimSourceForm));
        Assert.Equal(HttpStatusCode.Redirect, claimSourcePost.StatusCode);
        var claimSourceId = await factory.Database.ScalarAsync<Guid>(
            "SELECT Id FROM ClaimSources WHERE Name = 'Web Caller Claim Source';");
        var claimSourceEditHtml = await IntakeWebDriver.GetHtmlAsync(
            client,
            $"/Administration/ClaimSources/Edit/{claimSourceId:D}");
        Assert.Contains("Edit Web Caller Claim Source</h1>", claimSourceEditHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Renamed", claimSourceEditHtml, StringComparison.Ordinal);

        using var retiredOrganizations = await client.GetAsync("/Administration/Organizations");
        Assert.Equal(HttpStatusCode.NotFound, retiredOrganizations.StatusCode);
        var principalHtml = await IntakeWebDriver.GetHtmlAsync(client, "/Administration/Principals/Create");
        Assert.Contains("Name", principalHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("OrganizationId", principalHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Select an organization", principalHtml, StringComparison.Ordinal);
        var principalForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(
                principalHtml,
                "__RequestVerificationToken"),
            ["OperationKey"] = InputValue(principalHtml, "OperationKey"),
            ["Name"] = "pegasustest",
            ["Code"] = "WEBP"
        };
        using var principalPost = await client.PostAsync(
            "/Administration/Principals/Create?handler=Create",
            new FormUrlEncodedContent(principalForm));
        Assert.Equal(HttpStatusCode.Redirect, principalPost.StatusCode);
        var principalId = await factory.Database.ScalarAsync<Guid>(
            "SELECT Id FROM Principals WHERE Code = 'WEBP';");

        var principalIndexHtml = await IntakeWebDriver.GetHtmlAsync(client, "/Administration/Principals");
        Assert.Contains("WEBP", principalIndexHtml, StringComparison.Ordinal);
        Assert.Contains("pegasustest", principalIndexHtml, StringComparison.Ordinal);
        Assert.Contains("Settings", principalIndexHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Work Provider", principalIndexHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Organization", principalIndexHtml, StringComparison.Ordinal);

        var evaSubmissionPath =
            $"/Administration/Principals/Settings/{principalId:D}";
        // GET the actual page and assert a successful response.
        var evaSubmissionHtml = await IntakeWebDriver.GetHtmlAsync(client, evaSubmissionPath);
        Assert.Contains("pegasustest</h1>", evaSubmissionHtml, StringComparison.Ordinal);
        var evaSubmissionForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(
                evaSubmissionHtml,
                "__RequestVerificationToken"),
            ["EvaOperationKey"] = InputValue(evaSubmissionHtml, "EvaOperationKey"),
            ["ExpectedVersion"] = InputValue(evaSubmissionHtml, "ExpectedVersion"),
            ["EvaManualSubmission"] = bool.TrueString,
            ["EvaReason"] = "Web caller EVA submission proof"
        };
        using var evaSubmissionPost = await client.PostAsync(
            $"{evaSubmissionPath}?handler=UpdateEva",
            new FormUrlEncodedContent(evaSubmissionForm));
        Assert.True(
            evaSubmissionPost.StatusCode == HttpStatusCode.Redirect,
            $"Expected a redirect but got {evaSubmissionPost.StatusCode}. " +
                $"Validation errors: {await DescribeValidationErrorsAsync(evaSubmissionPost)}");
        Assert.Equal(evaSubmissionPath, evaSubmissionPost.Headers.Location?.OriginalString);
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                $"SELECT CASE WHEN EvaManualSubmission = 1 THEN 1 ELSE 0 END FROM Principals WHERE Id = '{principalId:D}';"));

        var locationHtml = await IntakeWebDriver.GetHtmlAsync(client, evaSubmissionPath);
        using var locationPost = await client.PostAsync(
            $"{evaSubmissionPath}?handler=UpdateLocation",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = InputValue(locationHtml, "__RequestVerificationToken"),
                ["LocationOperationKey"] = InputValue(locationHtml, "LocationOperationKey"),
                ["ExpectedVersion"] = InputValue(locationHtml, "ExpectedVersion"),
                ["LocationIsImageBasedAssessment"] = bool.TrueString,
                ["LocationReason"] = "Image Based Assessment default"
            }));
        Assert.Equal(HttpStatusCode.Redirect, locationPost.StatusCode);
        Assert.Equal(evaSubmissionPath, locationPost.Headers.Location?.OriginalString);
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"SELECT CASE WHEN DefaultInspectionAddress IS NULL THEN 1 ELSE 0 END FROM Principals WHERE Id = '{principalId:D}';"));

        await AssertCredentialControlsAsync(factory, client, principalId, evaSubmissionPath);

        var replacePath =
            $"/Administration/Principals/Replace/{principalId:D}";
        var replaceHtml = await IntakeWebDriver.GetHtmlAsync(client, replacePath);
        Assert.Contains("existing cases and references stay unchanged", replaceHtml, StringComparison.Ordinal);
        var replacementOperationKey = InputValue(replaceHtml, "OperationKey");
        var replaceForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(
                replaceHtml,
                "__RequestVerificationToken"),
            ["OperationKey"] = replacementOperationKey,
            ["ExpectedVersion"] = InputValue(replaceHtml, "ExpectedVersion"),
            ["SuccessorCode"] = "WEBN",
            ["Reason"] = "Web caller replacement proof"
        };
        using var replacePost = await client.PostAsync(
            $"{replacePath}?handler=Replace",
            new FormUrlEncodedContent(replaceForm));
        Assert.Equal(HttpStatusCode.Redirect, replacePost.StatusCode);

        Assert.Equal(
            0,
            await factory.Database.ScalarAsync<int>(
                $"SELECT CASE WHEN IsActive = 0 AND Code = 'WEBP' THEN 0 ELSE 1 END FROM Principals WHERE Id = '{principalId:D}';"));
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM Principals WHERE Code = 'WEBN' AND PredecessorId = '{principalId:D}' AND IsActive = 1;"));
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM Principals p JOIN Principals previous ON previous.Id = p.PredecessorId WHERE p.Code = 'WEBN' AND p.OrganizationId = previous.OrganizationId;"));
        Assert.Equal(
            2,
            await factory.Database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM ActionHistory WHERE CorrelationId = '{replacementOperationKey}' AND ActorSubjectId = '{DevelopmentOfflineIdentity.AdministratorId:D}' AND Reason = 'Web caller replacement proof';"));
    }

    [Fact]
    public async Task PrincipalRoutesDenyNonAdministratorSession()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: false);
        _ = factory.Services;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<PegasusIdentityUser>>();
            var user = await userManager.FindByIdAsync(
                DevelopmentOfflineIdentity.AdministratorId.ToString("D"));
            Assert.NotNull(user);
            Assert.True((await userManager.RemoveFromRoleAsync(
                user,
                StaffRoleNames.Administrator)).Succeeded);
        }
        using var client = IntakeWebDriver.CreateClient(factory);

        var id = Guid.Parse("1eeea2b1-3e1d-4a0a-8205-0c25396206e8");
        string[] routes =
        [
            "/Administration/Principals",
            "/Administration/Principals/Create",
            $"/Administration/Principals/Replace/{id:D}",
            $"/Administration/Principals/Settings/{id:D}"
        ];
        foreach (var route in routes)
        {
            using var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    private static async Task AssertCredentialControlsAsync(
        IntakeWebApplicationFactory factory, HttpClient client, Guid principalId, string settingsPath)
    {
        var settings = await IntakeWebDriver.GetHtmlAsync(client, settingsPath);
        var issueForm = CredentialForm(settings);
        using var issued = await client.PostAsync(
            $"{settingsPath}?handler=IssueCredential", new FormUrlEncodedContent(issueForm));
        issued.EnsureSuccessStatusCode();
        Assert.True(issued.Headers.CacheControl?.NoStore);
        var issuedHtml = await issued.Content.ReadAsStringAsync();
        var secretMatch = Regex.Match(issuedHtml, "id=\"issued-api-key\" value=\"([^\"]+)\"");
        Assert.True(secretMatch.Success);
        var secret = WebUtility.HtmlDecode(secretMatch.Groups[1].Value);
        var keyId = Pegasus.Web.ProviderApi.ProviderApi.TryReadKeyId("Bearer " + secret)!;
        Assert.False(string.IsNullOrWhiteSpace(secret));

        using var replay = await client.PostAsync(
            $"{settingsPath}?handler=IssueCredential", new FormUrlEncodedContent(issueForm));
        replay.EnsureSuccessStatusCode();
        Assert.DoesNotContain(secret, await replay.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        settings = await IntakeWebDriver.GetHtmlAsync(client, settingsPath);
        Assert.DoesNotContain(secret, settings, StringComparison.Ordinal);
        Assert.DoesNotContain("issued-api-key", settings, StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var authenticate = scope.ServiceProvider.GetRequiredService<IAuthenticatePrincipalCredential>();
        Assert.NotNull(await authenticate.ExecuteAsync(keyId, secret, default));

        foreach (var handler in new[] { "PauseCredential", "ResumeCredential" })
        {
            using var response = await client.PostAsync(
                $"{settingsPath}?handler={handler}", new FormUrlEncodedContent(CredentialForm(settings)));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var authentication = await authenticate.ExecuteAsync(keyId, secret, default);
            Assert.NotNull(authentication);
            Assert.Equal(handler == "ResumeCredential", authentication.MaySubmit);
            settings = await IntakeWebDriver.GetHtmlAsync(client, settingsPath);
        }

        var stale = CredentialForm(settings);
        stale["CredentialVersion"] = "0";
        using var staleResponse = await client.PostAsync(
            $"{settingsPath}?handler=RevokeCredential", new FormUrlEncodedContent(stale));
        Assert.Equal(HttpStatusCode.OK, staleResponse.StatusCode);
        Assert.Contains("The API key changed.", await staleResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.NotNull(await authenticate.ExecuteAsync(keyId, secret, default));

        using var reset = await client.PostAsync(
            $"{settingsPath}?handler=IssueCredential", new FormUrlEncodedContent(CredentialForm(settings)));
        reset.EnsureSuccessStatusCode();
        var resetHtml = await reset.Content.ReadAsStringAsync();
        var resetMatch = Regex.Match(resetHtml, "id=\"issued-api-key\" value=\"([^\"]+)\"");
        Assert.True(resetMatch.Success);
        var resetSecret = WebUtility.HtmlDecode(resetMatch.Groups[1].Value);
        var resetKeyId = Pegasus.Web.ProviderApi.ProviderApi.TryReadKeyId("Bearer " + resetSecret)!;
        Assert.Null(await authenticate.ExecuteAsync(keyId, secret, default));
        Assert.NotNull(await authenticate.ExecuteAsync(resetKeyId, resetSecret, default));

        settings = await IntakeWebDriver.GetHtmlAsync(client, settingsPath);
        using var revoked = await client.PostAsync(
            $"{settingsPath}?handler=RevokeCredential", new FormUrlEncodedContent(CredentialForm(settings)));
        Assert.Equal(HttpStatusCode.Redirect, revoked.StatusCode);
        Assert.Null(await authenticate.ExecuteAsync(resetKeyId, resetSecret, default));
        Assert.Equal(5, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM ActionHistory WHERE AggregateId = '{principalId:D}' AND EventKind LIKE 'principal_credential_%';"));
    }

    private static Dictionary<string, string> CredentialForm(string html) => new()
    {
        ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
        ["CredentialOperationKey"] = InputValue(html, "CredentialOperationKey"),
        ["CredentialVersion"] = InputValue(html, "CredentialVersion"),
        ["CredentialReason"] = "Provider access administration"
    };

    private static string InputValue(string html, string name)
    {
        var match = InputTagRegex().Matches(html)
            .Cast<Match>()
            .FirstOrDefault(candidate => string.Equals(
                WebUtility.HtmlDecode(candidate.Groups["name"].Value),
                name,
                StringComparison.Ordinal));
        Assert.True(match is not null, $"The administration form must render input '{name}'.");
        return WebUtility.HtmlDecode(match!.Groups["value"].Value);
    }

    [GeneratedRegex(
        "<input\\b(?=[^>]*\\bname=\"(?<name>[^\"]+)\")(?=[^>]*\\bvalue=\"(?<value>[^\"]*)\")[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InputTagRegex();

    // C06 review R-20: when a POST unexpectedly redisplays the page instead
    // of redirecting, name the cause instead of leaving only a status-code
    // mismatch behind.
    private static async Task<string> DescribeValidationErrorsAsync(HttpResponseMessage response)
    {
        var html = await response.Content.ReadAsStringAsync();
        var texts = ValidationSummaryRegex().Matches(html)
            .Cast<Match>()
            .Concat(FieldValidationErrorRegex().Matches(html).Cast<Match>())
            .Select(match => WebUtility.HtmlDecode(
                Regex.Replace(match.Groups["text"].Value, "<[^>]+>", string.Empty)).Trim())
            .Where(text => text.Length > 0)
            .Distinct(StringComparer.Ordinal);
        var joined = string.Join(" | ", texts);
        return joined.Length > 0 ? joined : "(none found in response body)";
    }

    [GeneratedRegex(
        "<div[^>]*class=\"[^\"]*status-card--error[^\"]*\"[^>]*>(?<text>[\\s\\S]*?)</div>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ValidationSummaryRegex();

    [GeneratedRegex(
        "<span[^>]*class=\"[^\"]*field-validation-error[^\"]*\"[^>]*>(?<text>[\\s\\S]*?)</span>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FieldValidationErrorRegex();
}
