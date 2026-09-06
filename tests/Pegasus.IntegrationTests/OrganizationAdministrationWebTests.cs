using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class OrganizationAdministrationWebTests
{
    [Fact]
    public async Task AdministratorRoutesAreDiscoverableAndPostThroughCoreEfCallers()
    {
        // C06 review R-1: this test drives the shared EvaSubmission page (a
        // page model is activated per request, not at host startup), so it
        // needs the full C06 composition — not just the optional-resolution
        // bridge — to prove the page's real behaviour rather than only its
        // degraded one.
        using var factory = new IntakeWebApplicationFactory();
        using var host = factory.WithC06Adapters();
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        using var landingResponse = await client.GetAsync("/Administration");
        var landingHtml = await landingResponse.Content.ReadAsStringAsync();
        landingResponse.EnsureSuccessStatusCode();
        Assert.Contains("/Administration/Organizations", landingHtml, StringComparison.Ordinal);
        Assert.Contains("/Administration/Principals", landingHtml, StringComparison.Ordinal);

        using var organizationGet = await client.GetAsync("/Administration/Organizations");
        var organizationHtml = await organizationGet.Content.ReadAsStringAsync();
        organizationGet.EnsureSuccessStatusCode();
        Assert.Contains("Create organization", organizationHtml, StringComparison.Ordinal);
        var organizationForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(
                organizationHtml,
                "__RequestVerificationToken"),
            ["OperationKey"] = InputValue(organizationHtml, "OperationKey"),
            ["OrganizationName"] = "Web Caller Provider",
            ["WorkProvider"] = bool.TrueString,
            ["InstructionIntermediary"] = bool.FalseString
        };
        using var organizationPost = await client.PostAsync(
            "/Administration/Organizations?handler=Create",
            new FormUrlEncodedContent(organizationForm));
        Assert.Equal(HttpStatusCode.Redirect, organizationPost.StatusCode);
        var organizationId = await factory.Database.ScalarAsync<Guid>(
            "SELECT Id FROM Organizations WHERE Name = 'Web Caller Provider';");
        var organizationEditPath = $"/Administration/Organizations/Edit/{organizationId:D}";
        using var organizationEditGet = await client.GetAsync(organizationEditPath);
        var organizationEditHtml = await organizationEditGet.Content.ReadAsStringAsync();
        organizationEditGet.EnsureSuccessStatusCode();
        // The lede is gone: a page's heading and its content are the
        // explanation, and "Roles are independently selectable" described the
        // checkboxes the operator was already looking at.
        Assert.DoesNotContain("class=\"lede\"", organizationEditHtml, StringComparison.Ordinal);
        Assert.Contains("Organization roles", organizationEditHtml, StringComparison.Ordinal);
        var organizationEditForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(
                organizationEditHtml,
                "__RequestVerificationToken"),
            ["OperationKey"] = InputValue(organizationEditHtml, "OperationKey"),
            ["ExpectedVersion"] = InputValue(organizationEditHtml, "ExpectedVersion"),
            ["WorkProvider"] = bool.TrueString,
            ["InstructionIntermediary"] = bool.TrueString,
            ["Reason"] = "Web caller role update proof"
        };
        using var organizationEditPost = await client.PostAsync(
            $"{organizationEditPath}?handler=Update",
            new FormUrlEncodedContent(organizationEditForm));
        Assert.Equal(HttpStatusCode.Redirect, organizationEditPost.StatusCode);
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM OrganizationRoles WHERE OrganizationId = '{organizationId:D}' AND Role = 'instruction_intermediary';"));


        using var principalGet = await client.GetAsync(
            $"/Administration/Principals/Create?organizationId={organizationId:D}");
        var principalHtml = await principalGet.Content.ReadAsStringAsync();
        principalGet.EnsureSuccessStatusCode();
        Assert.Contains("cannot be edited", principalHtml, StringComparison.Ordinal);
        var principalForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(
                principalHtml,
                "__RequestVerificationToken"),
            ["OperationKey"] = InputValue(principalHtml, "OperationKey"),
            ["OrganizationId"] = organizationId.ToString("D"),
            ["Code"] = "WEBP"
        };
        using var principalPost = await client.PostAsync(
            "/Administration/Principals/Create?handler=Create",
            new FormUrlEncodedContent(principalForm));
        Assert.Equal(HttpStatusCode.Redirect, principalPost.StatusCode);
        var principalId = await factory.Database.ScalarAsync<Guid>(
            "SELECT Id FROM Principals WHERE Code = 'WEBP';");

        using var principalIndex = await client.GetAsync("/Administration/Principals");
        var principalIndexHtml = await principalIndex.Content.ReadAsStringAsync();
        principalIndex.EnsureSuccessStatusCode();
        Assert.Contains("WEBP", principalIndexHtml, StringComparison.Ordinal);
        Assert.Contains("Replace", principalIndexHtml, StringComparison.Ordinal);
        Assert.Contains("EVA API", principalIndexHtml, StringComparison.Ordinal);

        var evaSubmissionPath =
            $"/Administration/Principals/EvaSubmission/{organizationId:D}/{principalId:D}";
        // GetHtmlAsync so a Test UI capture records this page (it asserts 200).
        var evaSubmissionHtml = await IntakeWebDriver.GetHtmlAsync(client, evaSubmissionPath);
        Assert.Contains("Settings for WEBP", evaSubmissionHtml, StringComparison.Ordinal);
        // EXT-18 item 7: automatic EVA submission is retired from this page.
        Assert.DoesNotContain("EvaAutomaticSubmission", evaSubmissionHtml, StringComparison.Ordinal);
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
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                $"SELECT CASE WHEN EvaManualSubmission = 1 AND EvaAutomaticSubmission = 0 THEN 1 ELSE 0 END FROM Principals WHERE Id = '{principalId:D}';"));

        var replacePath =
            $"/Administration/Principals/Replace/{organizationId:D}/{principalId:D}";
        using var replaceGet = await client.GetAsync(replacePath);
        var replaceHtml = await replaceGet.Content.ReadAsStringAsync();
        replaceGet.EnsureSuccessStatusCode();
        Assert.Contains("cases, references, and reference ownership will not be edited", replaceHtml, StringComparison.Ordinal);
        var replacementOperationKey = InputValue(replaceHtml, "OperationKey");
        var replaceForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(
                replaceHtml,
                "__RequestVerificationToken"),
            ["OperationKey"] = replacementOperationKey,
            ["ExpectedVersion"] = InputValue(replaceHtml, "ExpectedVersion"),
            ["SuccessorOrganizationId"] = organizationId.ToString("D"),
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
            2,
            await factory.Database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM ActionHistory WHERE CorrelationId = '{replacementOperationKey}' AND ActorSubjectId = '{DevelopmentOfflineIdentity.AdministratorId:D}' AND Reason = 'Web caller replacement proof';"));
    }

    [Fact]
    public async Task DirectOrganizationAndPrincipalRoutesDenyNonAdministratorSession()
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
            "/Administration/Organizations",
            $"/Administration/Organizations/Edit/{id:D}",
            "/Administration/Principals",
            "/Administration/Principals/Create",
            $"/Administration/Principals/Replace/{id:D}/{id:D}",
            $"/Administration/Principals/EvaSubmission/{id:D}/{id:D}"
        ];
        foreach (var route in routes)
        {
            using var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

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
