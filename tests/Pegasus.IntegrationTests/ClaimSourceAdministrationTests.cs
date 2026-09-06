using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// EXT-19/S13 item 8: the Claim Sources administration surface — v3 admin
/// page conventions, Administrator authorization, expected version, reason
/// and idempotent operation key. Disable is the same Edit form with the
/// active flag cleared, and a changed record never rewrites a Case that
/// already copied its snapshot.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class ClaimSourceAdministrationTests
{
    [Fact]
    public async Task CreateEditAndDisableRoundTripAllSixDataFieldsThroughCoreEfCallers()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var host = factory.WithC06Adapters();
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        using var indexGet = await client.GetAsync("/Administration/ClaimSources");
        var indexHtml = await indexGet.Content.ReadAsStringAsync();
        indexGet.EnsureSuccessStatusCode();
        Assert.Contains("Create claim source", indexHtml, StringComparison.Ordinal);

        var createForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(indexHtml, "__RequestVerificationToken"),
            ["OperationKey"] = InputValue(indexHtml, "OperationKey"),
            ["Reason"] = InputValue(indexHtml, "Reason"),
            ["Name"] = "Web Caller Claim Source",
            ["ContactName"] = "Pat Example",
            ["Telephone"] = "01234 000111",
            ["Email"] = "pat@claimsource.example",
            ["Notes"] = "Created by the web caller proof"
        };
        using var createPost = await client.PostAsync(
            "/Administration/ClaimSources?handler=Create",
            new FormUrlEncodedContent(createForm));
        Assert.Equal(HttpStatusCode.Redirect, createPost.StatusCode);

        var claimSourceId = await factory.Database.ScalarAsync<Guid>(
            "SELECT Id FROM ClaimSources WHERE Name = 'Web Caller Claim Source';");

        using var editGet = await client.GetAsync($"/Administration/ClaimSources/Edit/{claimSourceId:D}");
        var editHtml = await editGet.Content.ReadAsStringAsync();
        editGet.EnsureSuccessStatusCode();
        Assert.Contains("Pat Example", editHtml, StringComparison.Ordinal);

        var editOperationKey = InputValue(editHtml, "OperationKey");
        var editForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(editHtml, "__RequestVerificationToken"),
            ["OperationKey"] = editOperationKey,
            ["ExpectedVersion"] = InputValue(editHtml, "ExpectedVersion"),
            ["Name"] = "Web Caller Claim Source Renamed",
            ["ContactName"] = "Pat Example",
            ["Telephone"] = "01234 000111",
            ["Email"] = "pat@claimsource.example",
            ["Notes"] = "Renamed by the web caller proof",
            ["Active"] = bool.TrueString,
            ["Reason"] = "Web caller rename proof"
        };
        using var editPost = await client.PostAsync(
            $"/Administration/ClaimSources/Edit/{claimSourceId:D}?handler=Update",
            new FormUrlEncodedContent(editForm));
        Assert.Equal(HttpStatusCode.Redirect, editPost.StatusCode);
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM ClaimSources WHERE Id = '{claimSourceId:D}' AND Name = 'Web Caller Claim Source Renamed' AND Version = 1;"));

        using var reeditGet = await client.GetAsync($"/Administration/ClaimSources/Edit/{claimSourceId:D}");
        var reeditHtml = await reeditGet.Content.ReadAsStringAsync();
        reeditGet.EnsureSuccessStatusCode();
        var disableForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(reeditHtml, "__RequestVerificationToken"),
            ["OperationKey"] = InputValue(reeditHtml, "OperationKey"),
            ["ExpectedVersion"] = InputValue(reeditHtml, "ExpectedVersion"),
            ["Name"] = "Web Caller Claim Source Renamed",
            ["ContactName"] = "Pat Example",
            ["Telephone"] = "01234 000111",
            ["Email"] = "pat@claimsource.example",
            ["Notes"] = "Renamed by the web caller proof",
            ["Active"] = bool.FalseString,
            ["Reason"] = "Web caller disable proof"
        };
        using var disablePost = await client.PostAsync(
            $"/Administration/ClaimSources/Edit/{claimSourceId:D}?handler=Update",
            new FormUrlEncodedContent(disableForm));
        Assert.Equal(HttpStatusCode.Redirect, disablePost.StatusCode);
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM ClaimSources WHERE Id = '{claimSourceId:D}' AND Active = 0 AND Version = 2;"));

        // Reusing an already-consumed operation key for a different payload
        // is a conflict, never a silent no-op or a fresh mutation.
        var conflictingForm = new Dictionary<string, string>(editForm)
        {
            ["OperationKey"] = editOperationKey,
            ["Notes"] = "A different payload reusing the same operation key"
        };
        using var replayPost = await client.PostAsync(
            $"/Administration/ClaimSources/Edit/{claimSourceId:D}?handler=Update",
            new FormUrlEncodedContent(conflictingForm));
        var replayHtml = await replayPost.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, replayPost.StatusCode);
        Assert.Contains("already used for a different operation", replayHtml, StringComparison.Ordinal);
    }

    /// <summary>
    /// C06 review R-13: the concurrency fix in <c>OnPostUpdateAsync</c> (the
    /// posted <c>ExpectedVersion</c> reaches the store untouched, refreshed
    /// only on the redisplay path) had no test posting a genuinely stale
    /// version — only the Core <c>RequireCurrentVersion</c> unit test
    /// covered "stale writes fail". Advance the record to version 1 through
    /// an ordinary edit, then post the version the very first GET rendered
    /// (now stale) with a fresh operation key and assert the store refuses
    /// it and the first edit's values are what persisted.
    /// </summary>
    [Fact]
    public async Task EditRefusesAStalePostedExpectedVersion()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var host = factory.WithC06Adapters();
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        using var indexGet = await client.GetAsync("/Administration/ClaimSources");
        var indexHtml = await indexGet.Content.ReadAsStringAsync();
        indexGet.EnsureSuccessStatusCode();
        var createForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(indexHtml, "__RequestVerificationToken"),
            ["OperationKey"] = InputValue(indexHtml, "OperationKey"),
            ["Reason"] = InputValue(indexHtml, "Reason"),
            ["Name"] = "Stale Write Claim Source",
            ["ContactName"] = "Sam Example",
            ["Telephone"] = "01234 000222",
            ["Email"] = "sam@claimsource.example",
            ["Notes"] = "Created for the stale-write proof"
        };
        using var createPost = await client.PostAsync(
            "/Administration/ClaimSources?handler=Create",
            new FormUrlEncodedContent(createForm));
        Assert.Equal(HttpStatusCode.Redirect, createPost.StatusCode);
        var claimSourceId = await factory.Database.ScalarAsync<Guid>(
            "SELECT Id FROM ClaimSources WHERE Name = 'Stale Write Claim Source';");

        using var firstEditGet = await client.GetAsync($"/Administration/ClaimSources/Edit/{claimSourceId:D}");
        var firstEditHtml = await firstEditGet.Content.ReadAsStringAsync();
        firstEditGet.EnsureSuccessStatusCode();
        var staleExpectedVersion = InputValue(firstEditHtml, "ExpectedVersion");

        // Advance the record to version 1 through a normal edit.
        var firstEditForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(firstEditHtml, "__RequestVerificationToken"),
            ["OperationKey"] = InputValue(firstEditHtml, "OperationKey"),
            ["ExpectedVersion"] = staleExpectedVersion,
            ["Name"] = "Stale Write Claim Source",
            ["ContactName"] = "Sam Example",
            ["Telephone"] = "01234 000222",
            ["Email"] = "sam@claimsource.example",
            ["Notes"] = "First edit",
            ["Active"] = bool.TrueString,
            ["Reason"] = "First edit for the stale-write proof"
        };
        using var firstEditPost = await client.PostAsync(
            $"/Administration/ClaimSources/Edit/{claimSourceId:D}?handler=Update",
            new FormUrlEncodedContent(firstEditForm));
        Assert.Equal(HttpStatusCode.Redirect, firstEditPost.StatusCode);

        // Re-GET for a fresh operation key and antiforgery token, but post
        // the version the very FIRST GET rendered — the record has already
        // moved to version 1, so this is now stale.
        using var secondEditGet = await client.GetAsync($"/Administration/ClaimSources/Edit/{claimSourceId:D}");
        var secondEditHtml = await secondEditGet.Content.ReadAsStringAsync();
        secondEditGet.EnsureSuccessStatusCode();
        var staleForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(secondEditHtml, "__RequestVerificationToken"),
            ["OperationKey"] = InputValue(secondEditHtml, "OperationKey"),
            ["ExpectedVersion"] = staleExpectedVersion,
            ["Name"] = "Stale Write Claim Source",
            ["ContactName"] = "Sam Example",
            ["Telephone"] = "01234 000222",
            ["Email"] = "sam@claimsource.example",
            ["Notes"] = "Should not be saved",
            ["Active"] = bool.TrueString,
            ["Reason"] = "Stale write proof"
        };
        using var staleEditPost = await client.PostAsync(
            $"/Administration/ClaimSources/Edit/{claimSourceId:D}?handler=Update",
            new FormUrlEncodedContent(staleForm));
        var staleEditHtml = await staleEditPost.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, staleEditPost.StatusCode);
        Assert.Contains("changed after this page was loaded", staleEditHtml, StringComparison.Ordinal);
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM ClaimSources WHERE Id = '{claimSourceId:D}' AND Notes = 'First edit' AND Version = 1;"));
    }

    [Fact]
    public async Task DirectClaimSourceRoutesDenyNonAdministratorSession()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: false);
        using var host = factory.WithC06Adapters();
        await using (var scope = host.Services.CreateAsyncScope())
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
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        var id = Guid.Parse("2eeea2b1-3e1d-4a0a-8205-0c25396206e9");
        string[] routes =
        [
            "/Administration/ClaimSources",
            $"/Administration/ClaimSources/Edit/{id:D}"
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
}
