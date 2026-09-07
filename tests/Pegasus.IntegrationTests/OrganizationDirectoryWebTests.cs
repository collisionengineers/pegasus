using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace Pegasus.IntegrationTests;

/// <summary>
/// EXT-18/S05 items 6-7: the web-facing surface for a principal's default
/// inspection-location choice and its one remaining explicit EVA setting.
/// Automatic EVA submission has no control here, and a physical default
/// change is retained alongside its reason without ever implying CE
/// attendance or changing B's separate assessment method.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class OrganizationDirectoryWebTests
{
    [Fact]
    public async Task PrincipalSettingsPageSavesDefaultLocationAndManualEvaIndependently()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var principalGet = await client.GetAsync("/Administration/Principals/Create");
        var principalHtml = await principalGet.Content.ReadAsStringAsync();
        principalGet.EnsureSuccessStatusCode();
        var principalForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(principalHtml, "__RequestVerificationToken"),
            ["OperationKey"] = InputValue(principalHtml, "OperationKey"),
            ["Name"] = "Directory Web Caller Provider",
            ["Code"] = "DIRW",
            ["InspectionMode"] = "PhysicalAddress"
        };
        using var principalPost = await client.PostAsync(
            "/Administration/Principals/Create?handler=Create",
            new FormUrlEncodedContent(principalForm));
        Assert.Equal(HttpStatusCode.Redirect, principalPost.StatusCode);
        var principalId = await factory.Database.ScalarAsync<Guid>(
            "SELECT Id FROM Principals WHERE Code = 'DIRW';");

        var settingsPath = $"/Administration/Principals/Settings/{principalId:D}";
        using var settingsGet = await client.GetAsync(settingsPath);
        var settingsHtml = await settingsGet.Content.ReadAsStringAsync();
        settingsGet.EnsureSuccessStatusCode();
        Assert.DoesNotContain("Automatic API submission", settingsHtml, StringComparison.Ordinal);

        var locationForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(settingsHtml, "__RequestVerificationToken"),
            ["LocationOperationKey"] = InputValue(settingsHtml, "LocationOperationKey"),
            ["ExpectedVersion"] = InputValue(settingsHtml, "ExpectedVersion"),
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

        using var settingsAfterLocationGet = await client.GetAsync(settingsPath);
        var settingsAfterLocationHtml = await settingsAfterLocationGet.Content.ReadAsStringAsync();
        settingsAfterLocationGet.EnsureSuccessStatusCode();
        var evaForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(settingsAfterLocationHtml, "__RequestVerificationToken"),
            ["EvaOperationKey"] = InputValue(settingsAfterLocationHtml, "EvaOperationKey"),
            ["ExpectedVersion"] = InputValue(settingsAfterLocationHtml, "ExpectedVersion"),
            ["EvaManualSubmission"] = bool.TrueString,
            ["EvaReason"] = "Web caller manual EVA proof"
        };
        using var evaPost = await client.PostAsync(
            $"{settingsPath}?handler=UpdateEva",
            new FormUrlEncodedContent(evaForm));
        Assert.True(
            evaPost.StatusCode == HttpStatusCode.Redirect,
            $"Expected a redirect but got {evaPost.StatusCode}. " +
                $"Validation errors: {await DescribeValidationErrorsAsync(evaPost)}");
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM Principals WHERE Id = '{principalId:D}' AND EvaManualSubmission = 1 AND DefaultInspectionAddress = '1 Directory Way, DW1 2EF';"));

        using var indexGet = await client.GetAsync("/Administration/Principals");
        var indexHtml = await indexGet.Content.ReadAsStringAsync();
        indexGet.EnsureSuccessStatusCode();
        Assert.Contains("Directory Web Caller Provider", indexHtml, StringComparison.Ordinal);
        using var settingsAfterEvaGet = await client.GetAsync(settingsPath);
        settingsAfterEvaGet.EnsureSuccessStatusCode();
        Assert.Contains("Directory Web Caller Yard", await settingsAfterEvaGet.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.DoesNotContain("Automatic", indexHtml, StringComparison.Ordinal);
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
        using var response = await client.GetAsync($"/Administration/Principals/Settings/{principalId:D}");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Matches(
            """<input\b(?=[^>]*name="LocationIsImageBasedAssessment")(?=[^>]*checked="checked")[^>]*>""",
            html);
        foreach (var domain in Pegasus.Core.Intake.QdosMailRoutePolicy.AcceptedDirectDomains)
        {
            Assert.Contains(domain, html, StringComparison.Ordinal);
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
