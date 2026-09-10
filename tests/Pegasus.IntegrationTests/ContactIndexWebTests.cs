using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class ContactIndexWebTests
{
    private static readonly ActionActor Administrator = ActionActor.Staff(
        DevelopmentOfflineIdentity.AdministratorId,
        [StaffRole.Administrator]);

    [Fact]
    public async Task TypedCreateDialogPostsANewContactWithoutLeavingTheIndex()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        using var get = await client.GetAsync("/Administration/Contacts?createType=ClaimSource");
        var html = await get.Content.ReadAsStringAsync();
        get.EnsureSuccessStatusCode();
        Assert.Contains("data-dialog=\"contact-type-dialog\"", html, StringComparison.Ordinal);
        Assert.Contains("data-dialog=\"contact-create-dialog\"", html, StringComparison.Ordinal);
        Assert.Contains("New Claim Source", html, StringComparison.Ordinal);
        Assert.Contains("Check existing contacts", html, StringComparison.Ordinal);

        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
            ["CreateType"] = "ClaimSource",
            ["ContactId"] = InputValue(html, "ContactId"),
            ["ExpectedVersion"] = InputValue(html, "ExpectedVersion"),
            ["OperationKey"] = InputValue(html, "OperationKey"),
            ["Name"] = "Wizard Claim Source"
        };
        using var post = await client.PostAsync(
            "/Administration/Contacts?handler=Create",
            new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM Organizations WHERE Name = 'Wizard Claim Source';"));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM ContactRoles r JOIN Organizations o ON o.Id = r.OrganizationId WHERE o.Name = 'Wizard Claim Source' AND r.Role = 'claim_source';"));
    }

    /// <summary>
    /// D7: a telephone with a letter in it is refused as a field error against
    /// the Telephone control, not a general notice, and no contact is created.
    /// </summary>
    [Fact]
    public async Task ALetteredTelephoneIsRefusedWithAFieldError()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        using var get = await client.GetAsync("/Administration/Contacts?createType=ClaimSource");
        var html = await get.Content.ReadAsStringAsync();
        get.EnsureSuccessStatusCode();

        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
            ["CreateType"] = "ClaimSource",
            ["ContactId"] = InputValue(html, "ContactId"),
            ["ExpectedVersion"] = InputValue(html, "ExpectedVersion"),
            ["OperationKey"] = InputValue(html, "OperationKey"),
            ["Name"] = "Lettered Telephone Contact",
            ["Telephone"] = "01234 56789O"
        };
        using var post = await client.PostAsync(
            "/Administration/Contacts?handler=Create",
            new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var body = await post.Content.ReadAsStringAsync();
        Assert.Contains("Telephone must be digits.", body, StringComparison.Ordinal);
        Assert.Equal(0, await factory.Database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM Organizations WHERE Name = 'Lettered Telephone Contact';"));
    }

    [Fact]
    public async Task ContactEditRequiresAnExistingContactRouteId()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/Administration/Contacts/Edit?role=ClaimSource");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ContactEditSaveRejectsANewRouteIdWithoutCreatingAContact()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        using var index = await client.GetAsync("/Administration/Contacts");
        var indexHtml = await index.Content.ReadAsStringAsync();
        index.EnsureSuccessStatusCode();
        var newId = Guid.NewGuid();

        using var response = await client.PostAsync(
            $"/Administration/Contacts/Edit/{newId:D}?handler=Save",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = InputValue(indexHtml, "__RequestVerificationToken"),
                ["ContactId"] = newId.ToString("D"),
                ["ExpectedVersion"] = "0",
                ["OperationKey"] = "contact-edit-new-route-id",
                ["Name"] = "Rejected edit contact",
                ["Roles"] = "ClaimSource"
            }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await factory.Database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM Organizations WHERE Name = 'Rejected edit contact';"));
    }

    [Fact]
    public async Task ExistingMatchClaimsTheContactAndAddsOnlyTheSelectedType()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var administration = scope.ServiceProvider.GetRequiredService<IContactDirectoryAdministration>();
        var existingId = Guid.NewGuid();
        await administration.SaveAsync(new(
            Administrator,
            existingId,
            0,
            "Match Contact",
            "Existing person",
            "existing@example.test",
            null,
            null,
            null,
            true,
            [ContactRole.ClaimSource],
            null,
            CaseInspectionMode.PhysicalAddress,
            [],
            "contact-index:seed",
            string.Empty), default);

        using var client = IntakeWebDriver.CreateClient(factory);
        using var start = await client.GetAsync("/Administration/Contacts?createType=ThirdPartyEngineer");
        var startHtml = await start.Content.ReadAsStringAsync();
        start.EnsureSuccessStatusCode();
        var findForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(startHtml, "__RequestVerificationToken"),
            ["CreateType"] = "ThirdPartyEngineer",
            ["ContactId"] = InputValue(startHtml, "ContactId"),
            ["OperationKey"] = InputValue(startHtml, "OperationKey"),
            ["Name"] = "Match"
        };
        using var found = await client.PostAsync(
            "/Administration/Contacts?handler=FindMatches",
            new FormUrlEncodedContent(findForm));
        var foundHtml = await found.Content.ReadAsStringAsync();
        found.EnsureSuccessStatusCode();
        Assert.Contains("Match Contact", foundHtml, StringComparison.Ordinal);
        Assert.Contains("Use this contact", foundHtml, StringComparison.Ordinal);

        var chooseForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(foundHtml, "__RequestVerificationToken"),
            ["CreateType"] = "ThirdPartyEngineer",
            ["ExistingContactId"] = existingId.ToString("D"),
            ["OperationKey"] = InputValue(foundHtml, "OperationKey")
        };
        using var chosen = await client.PostAsync(
            "/Administration/Contacts?handler=ChooseExisting",
            new FormUrlEncodedContent(chooseForm));
        var chosenHtml = await chosen.Content.ReadAsStringAsync();
        chosen.EnsureSuccessStatusCode();
        Assert.Contains("Adding <strong>Third Party Engineer</strong> to Match Contact", chosenHtml, StringComparison.Ordinal);

        var saveForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(chosenHtml, "__RequestVerificationToken"),
            ["CreateType"] = "ThirdPartyEngineer",
            ["ContactId"] = InputValue(chosenHtml, "ContactId"),
            ["ExistingContactId"] = InputValue(chosenHtml, "ExistingContactId"),
            ["ExpectedVersion"] = InputValue(chosenHtml, "ExpectedVersion"),
            ["LeaseToken"] = InputValue(chosenHtml, "LeaseToken"),
            ["OperationKey"] = InputValue(chosenHtml, "OperationKey"),
            ["Name"] = "Match Contact",
            ["ContactPerson"] = "Existing person",
            ["Email"] = "existing@example.test"
        };
        using var saved = await client.PostAsync(
            "/Administration/Contacts?handler=Create",
            new FormUrlEncodedContent(saveForm));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal(2, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM ContactRoles WHERE OrganizationId = '{existingId:D}' AND Role IN ('claim_source', 'third_party_engineer');"));
        Assert.Equal(0, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM EditScopes WHERE ScopeKind = 'Contact' AND RecordId = '{existingId:D}';"));
    }

    private static string InputValue(string html, string name)
    {
        var match = InputTagRegex().Matches(html)
            .Cast<Match>()
            .FirstOrDefault(candidate => string.Equals(
                System.Net.WebUtility.HtmlDecode(candidate.Groups["name"].Value),
                name,
                StringComparison.Ordinal));
        Assert.True(match is not null, $"The contact wizard must render input '{name}'.");
        return System.Net.WebUtility.HtmlDecode(match!.Groups["value"].Value);
    }

    [GeneratedRegex(
        "<input\\b(?=[^>]*\\bname=\"(?<name>[^\"]+)\")(?=[^>]*\\bvalue=\"(?<value>[^\"]*)\")[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InputTagRegex();
}
