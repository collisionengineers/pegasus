using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace Pegasus.IntegrationTests;

/// <summary>
/// EXT-18/S05 items 6-7: the web-facing surface for a principal's default
/// inspection-location choice and report-generation policy. A physical default
/// change is retained alongside its reason without ever implying CE attendance
/// or changing B's separate assessment method.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class OrganizationDirectoryWebTests
{
    [Fact]
    public async Task PrincipalSettingsPageSavesDefaultLocationAndReportPolicyIndependently()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (principalId, contactId) = await CreatePrincipalContactAsync(
            client, factory, "Directory Web Caller Provider", "DIRW");

        var settingsPath = $"/Administration/Contacts/Edit/{contactId:D}";
        var settingsHtml = await EditContactAsync(client, settingsPath);
        Assert.Contains("Pegasus", settingsHtml, StringComparison.Ordinal);
        Assert.Contains("EVA ZIP export", settingsHtml, StringComparison.Ordinal);
        Assert.Contains("EVA manual API submission", settingsHtml, StringComparison.Ordinal);
        Assert.Contains("EVA automatic API submission at Review", settingsHtml, StringComparison.Ordinal);

        var locationForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(settingsHtml, "__RequestVerificationToken"),
            ["LocationOperationKey"] = InputValue(settingsHtml, "LocationOperationKey"),
            ["PrincipalExpectedVersion"] = InputValue(settingsHtml, "PrincipalExpectedVersion"),
            ["ExpectedVersion"] = InputValue(settingsHtml, "ExpectedVersion"),
            ["LeaseToken"] = InputValue(settingsHtml, "LeaseToken"),
            ["LocationIsImageBasedAssessment"] = bool.FalseString,
            ["LocationLabel"] = "Directory Web Caller Yard",
            ["LocationAddress"] = "1 Directory Way, DW1 2EF",
            ["LocationPostcode"] = "DW1 2EF",
            ["LocationReason"] = "Web caller default location proof"
        };
        using var locationPost = await client.PostAsync(
            $"{settingsPath}?handler=UpdateLocation",
            new FormUrlEncodedContent(locationForm));
        Assert.True(
            locationPost.StatusCode == HttpStatusCode.Redirect,
            $"Expected a redirect but got {locationPost.StatusCode}. " +
                $"Validation errors: {await DescribeValidationErrorsAsync(locationPost)}");
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM Principals WHERE Id = '{principalId:D}' AND DefaultInspectionAddress = '1 Directory Way, DW1 2EF' AND DefaultInspectionLocationLabel = 'Directory Web Caller Yard';"));

        var settingsAfterLocationHtml = await EditContactAsync(client, settingsPath);
        var reportSettingsForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(settingsAfterLocationHtml, "__RequestVerificationToken"),
            ["ReportSettingsOperationKey"] = InputValue(settingsAfterLocationHtml, "ReportSettingsOperationKey"),
            ["PrincipalExpectedVersion"] = InputValue(settingsAfterLocationHtml, "PrincipalExpectedVersion"),
            ["ExpectedVersion"] = InputValue(settingsAfterLocationHtml, "ExpectedVersion"),
            ["LeaseToken"] = InputValue(settingsAfterLocationHtml, "LeaseToken"),
            ["ReportGenerationPolicy"] = "EvaManualApi",
            ["IncludeOriginalInstructionSender"] = bool.TrueString,
            ["AdditionalReportRecipients"] = "reports@example.test"
        };
        using var reportSettingsPost = await client.PostAsync(
            $"{settingsPath}?handler=UpdateReportSettings",
            new FormUrlEncodedContent(reportSettingsForm));
        Assert.True(
            reportSettingsPost.StatusCode == HttpStatusCode.Redirect,
            $"Expected a redirect but got {reportSettingsPost.StatusCode}. " +
                $"Validation errors: {await DescribeValidationErrorsAsync(reportSettingsPost)}");
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM Principals WHERE Id = '{principalId:D}' AND ReportGenerationPolicy = 'EvaManualApi' AND IncludeOriginalInstructionSender = 1 AND ReportRecipientAddressesJson LIKE '%reports@example.test%' AND DefaultInspectionAddress = '1 Directory Way, DW1 2EF';"));

        using var indexGet = await client.GetAsync("/Administration/Contacts");
        var indexHtml = await indexGet.Content.ReadAsStringAsync();
        indexGet.EnsureSuccessStatusCode();
        Assert.Contains("Directory Web Caller Provider", indexHtml, StringComparison.Ordinal);
        using var settingsAfterReportSettingsGet = await client.GetAsync(settingsPath);
        settingsAfterReportSettingsGet.EnsureSuccessStatusCode();
        Assert.Contains("Directory Web Caller Yard", await settingsAfterReportSettingsGet.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StaleContactVersionOnNestedReportSettingsReturnsTheConflictWithoutChangingThePrincipal()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (principalId, contactId) = await CreatePrincipalContactAsync(
            client, factory, "Stale contact version provider", "STALEW");
        var settingsPath = $"/Administration/Contacts/Edit/{contactId:D}";
        var settingsHtml = await EditContactAsync(client, settingsPath);
        var policyBefore = await factory.Database.ScalarAsync<string>(
            $"SELECT ReportGenerationPolicy FROM Principals WHERE Id = '{principalId:D}';");

        await factory.Database.ExecuteAsync(
            $"UPDATE Organizations SET Version = Version + 1 WHERE Id = '{contactId:D}';");
        var reportSettingsForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(settingsHtml, "__RequestVerificationToken"),
            ["ReportSettingsOperationKey"] = InputValue(settingsHtml, "ReportSettingsOperationKey"),
            ["PrincipalExpectedVersion"] = InputValue(settingsHtml, "PrincipalExpectedVersion"),
            ["ExpectedVersion"] = InputValue(settingsHtml, "ExpectedVersion"),
            ["LeaseToken"] = InputValue(settingsHtml, "LeaseToken"),
            ["ReportGenerationPolicy"] = "EvaManualApi"
        };

        using var response = await client.PostAsync(
            $"{settingsPath}?handler=UpdateReportSettings",
            new FormUrlEncodedContent(reportSettingsForm));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "This contact changed. Reload it before making further changes.",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
        Assert.Equal(
            policyBefore,
            await factory.Database.ScalarAsync<string>(
                $"SELECT ReportGenerationPolicy FROM Principals WHERE Id = '{principalId:D}';"));
    }

    /// <summary>
    /// C06 review R-4: item 6's expected output — the handoff's "QDOS
    /// defaults to the Image Based Assessment location" — was otherwise
    /// unasserted anywhere. Image Based Assessment is represented as every
    /// <c>Default*</c> column being null, which is indistinguishable from
    /// "never set" by reading the schema alone; this asserts what the page
    /// actually renders for the seeded row.
    /// </summary>
    [Fact]
    public async Task QdosPrincipalSettingsDefaultToImageBasedAssessmentAndShowAcceptedDomains()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var principalId = await factory.Database.ScalarAsync<Guid>(
            "SELECT Id FROM Principals WHERE Code = 'QDOS';");
        var contactId = await factory.Database.ScalarAsync<Guid>(
            "SELECT OrganizationId FROM Principals WHERE Id = '" + principalId + "';");
        using var response = await client.GetAsync($"/Administration/Contacts/Edit/{contactId:D}");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Matches(
            """<input\b(?=[^>]*name="LocationIsImageBasedAssessment")(?=[^>]*checked="checked")[^>]*>""",
            html);
        foreach (var domain in Pegasus.Core.Intake.PrincipalMailRoutePolicy.AcceptedIdentities["QDOS"])
        {
            Assert.Contains(domain, html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task YmlSettingsShowTheExactMailboxNotASharedDomain()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var principalId = await factory.Database.ScalarAsync<Guid>(
            "SELECT Id FROM Principals WHERE Code = 'YML';");
        var contactId = await factory.Database.ScalarAsync<Guid>(
            "SELECT OrganizationId FROM Principals WHERE Id = '" + principalId + "';");
        using var response = await client.GetAsync($"/Administration/Contacts/Edit/{contactId:D}");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Accepted e-mail identities", html, StringComparison.Ordinal);
        Assert.Contains("networkhduk@gmail.com", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Accepted e-mail domains", html, StringComparison.Ordinal);
    }

    private static async Task<string> EditContactAsync(HttpClient client, string path)
    {
        var readOnlyHtml = await IntakeWebDriver.GetHtmlAsync(client, path);
        using var response = await client.PostAsync(
            $"{path}?handler=Edit",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = InputValue(readOnlyHtml, "__RequestVerificationToken"),
                ["ContactId"] = InputValue(readOnlyHtml, "ContactId"),
                ["ExpectedVersion"] = InputValue(readOnlyHtml, "ExpectedVersion"),
                ["OperationKey"] = InputValue(readOnlyHtml, "OperationKey")
            }));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task<(Guid PrincipalId, Guid ContactId)> CreatePrincipalContactAsync(
        HttpClient client,
        IntakeWebApplicationFactory factory,
        string name,
        string code)
    {
        using var principalGet = await client.GetAsync("/Administration/Contacts?createType=Principal");
        var principalHtml = await principalGet.Content.ReadAsStringAsync();
        principalGet.EnsureSuccessStatusCode();
        var principalForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(principalHtml, "__RequestVerificationToken"),
            ["OperationKey"] = InputValue(principalHtml, "OperationKey"),
            ["ContactId"] = InputValue(principalHtml, "ContactId"),
            ["CreateType"] = "Principal",
            ["Name"] = name,
            ["PrincipalCode"] = code,
            ["PrincipalInspectionMode"] = "PhysicalAddress"
        };
        using var principalPost = await client.PostAsync(
            "/Administration/Contacts?handler=Create",
            new FormUrlEncodedContent(principalForm));
        Assert.Equal(HttpStatusCode.Redirect, principalPost.StatusCode);
        var principalId = await factory.Database.ScalarAsync<Guid>(
            $"SELECT Id FROM Principals WHERE Code = '{code}';");
        var contactId = await factory.Database.ScalarAsync<Guid>(
            $"SELECT OrganizationId FROM Principals WHERE Id = '{principalId:D}';");
        return (principalId, contactId);
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
