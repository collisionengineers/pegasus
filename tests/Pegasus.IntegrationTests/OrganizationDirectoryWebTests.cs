using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Pegasus.Infrastructure.Persistence;

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
        using var host = factory.WithC06Adapters();
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        using var organizationGet = await client.GetAsync("/Administration/Organizations");
        var organizationHtml = await organizationGet.Content.ReadAsStringAsync();
        organizationGet.EnsureSuccessStatusCode();
        var organizationForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(organizationHtml, "__RequestVerificationToken"),
            ["OperationKey"] = InputValue(organizationHtml, "OperationKey"),
            ["OrganizationName"] = "Directory Web Caller Provider",
            ["WorkProvider"] = bool.TrueString,
            ["InstructionIntermediary"] = bool.FalseString
        };
        using var organizationPost = await client.PostAsync(
            "/Administration/Organizations?handler=Create",
            new FormUrlEncodedContent(organizationForm));
        Assert.Equal(HttpStatusCode.Redirect, organizationPost.StatusCode);
        var organizationId = await factory.Database.ScalarAsync<Guid>(
            "SELECT Id FROM Organizations WHERE Name = 'Directory Web Caller Provider';");

        using var principalGet = await client.GetAsync(
            $"/Administration/Principals/Create?organizationId={organizationId:D}");
        var principalHtml = await principalGet.Content.ReadAsStringAsync();
        principalGet.EnsureSuccessStatusCode();
        // EXT-18 item 7: automatic EVA submission has no control on this page.
        Assert.DoesNotContain("EvaAutomaticSubmission", principalHtml, StringComparison.Ordinal);
        var principalForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(principalHtml, "__RequestVerificationToken"),
            ["OperationKey"] = InputValue(principalHtml, "OperationKey"),
            ["OrganizationId"] = organizationId.ToString("D"),
            ["Code"] = "DIRW",
            ["InspectionMode"] = "PhysicalAddress"
        };
        using var principalPost = await client.PostAsync(
            "/Administration/Principals/Create?handler=Create",
            new FormUrlEncodedContent(principalForm));
        Assert.Equal(HttpStatusCode.Redirect, principalPost.StatusCode);
        var principalId = await factory.Database.ScalarAsync<Guid>(
            "SELECT Id FROM Principals WHERE Code = 'DIRW';");

        var settingsPath = $"/Administration/Principals/EvaSubmission/{organizationId:D}/{principalId:D}";
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
        Assert.Equal(HttpStatusCode.Redirect, locationPost.StatusCode);
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
        Assert.Equal(HttpStatusCode.Redirect, evaPost.StatusCode);
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM Principals WHERE Id = '{principalId:D}' AND EvaManualSubmission = 1 AND EvaAutomaticSubmission = 0 AND DefaultInspectionAddress = '1 Directory Way, DW1 2EF';"));

        using var indexGet = await client.GetAsync("/Administration/Principals");
        var indexHtml = await indexGet.Content.ReadAsStringAsync();
        indexGet.EnsureSuccessStatusCode();
        Assert.Contains("Directory Web Caller Yard", indexHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Automatic", indexHtml, StringComparison.Ordinal);
    }

    /// <summary>
    /// C06: <see cref="InspectionAddressChoicesQueries"/> resolves
    /// <c>IOrganizationDirectoryQueries</c> through an optional constructor
    /// dependency rather than a required one, because this branch does not
    /// yet carry Stream A's registration for it — and that class is already
    /// registered for <c>IInspectionAddressChoicesQueries</c>, so a required
    /// dependency there would fail ASP.NET's startup service-graph
    /// validation and break every page in the host. This is the bridge
    /// proof: with none of the C06 registrations present at all (today's
    /// state on this branch), the host still starts and an ordinary
    /// administration page still renders, never a failed host.
    /// </summary>
    [Fact]
    public async Task PrincipalsIndexStillRendersWhenNoC06RegistrationsArePresent()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/Administration/Principals");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<h1>Principals</h1>", html, StringComparison.Ordinal);
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
}
