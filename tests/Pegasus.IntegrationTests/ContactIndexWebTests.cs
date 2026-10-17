using System.Collections.Specialized;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.Web.Presentation;

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
            "SELECT COUNT(*) FROM Organizations WHERE Name = 'Wizard Claim Source' AND Role = 'claim_source';"));
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
            ["OperationKey"] = InputValue(html, "OperationKey"),
            ["Name"] = "Lettered Telephone Contact",
            ["Telephone"] = "01234 56789O"
        };
        using var post = await client.PostAsync(
            "/Administration/Contacts?handler=Create",
            new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var body = await post.Content.ReadAsStringAsync();
        Assert.Contains("Phone must be digits.", body, StringComparison.Ordinal);
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

    /// <summary>
    /// A contact has one type, so a possible match is only a link to the
    /// existing contact; checking for matches never changes it.
    /// </summary>
    [Fact]
    public async Task APossibleMatchLinksToTheExistingContact()
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
            ContactRole.ClaimSource,
            null,
            CaseInspectionMode.PhysicalAddress,
            [],
            "contact-index:seed"), default);

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
        Assert.Contains("Match Contact · Claim Source", foundHtml, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Administration/Contacts/Edit/{existingId:D}\"", foundHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Use this contact", foundHtml, StringComparison.Ordinal);
        Assert.Equal("claim_source", await factory.Database.ScalarAsync<string>(
            $"SELECT Role FROM Organizations WHERE Id = '{existingId:D}';"));
    }

    /// <summary>
    /// v40 (operator, 13 October 2026): one panel per type in the fixed order,
    /// each with its own columns and sorted by name; no Sort, no Type column and
    /// no lede; a type with no matching contact is absent, and Type narrows the
    /// list to one panel.
    /// </summary>
    [Fact]
    public async Task TheListIsOnePanelPerTypeWithTheTypesOwnColumns()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var administration = scope.ServiceProvider.GetRequiredService<IContactDirectoryAdministration>();
            Task<ContactDirectoryRecord> Save(string name, ContactRole role, string? code = null, string? postcode = null, Guid[]? links = null) =>
                administration.SaveAsync(new(
                    Administrator, Guid.NewGuid(), 0, name, null, null, null, null, postcode, true, role, code,
                    CaseInspectionMode.PhysicalAddress, links ?? [], $"contact-index:{name}"), default);
            var principal = await Save("Grouped Fixture Principal", ContactRole.Principal, code: "GFP1");
            await Save("Grouped Fixture Zulu Repairer", ContactRole.Repairer, postcode: "S9 2RQ", links: [principal.OwnPrincipalIds!.Single()]);
            await Save("Grouped Fixture Alpha Repairer", ContactRole.Repairer);
            await Save("Grouped Fixture Claims", ContactRole.ClaimSource);
        }
        using var client = IntakeWebDriver.CreateClient(factory);

        // The cells are compared without the whitespace between tags.
        var html = Regex.Replace(await IntakeWebDriver.GetHtmlAsync(client, "/Administration/Contacts?search=Grouped%20Fixture"), @">\s+<", "><");

        Assert.DoesNotContain("name=\"Sort\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Principals, claim sources, repairers", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<th>Type</th>", html, StringComparison.Ordinal);
        var principals = html.IndexOf(">Principals</h2>", StringComparison.Ordinal);
        var claimSources = html.IndexOf(">Claim Sources</h2>", StringComparison.Ordinal);
        var repairers = html.IndexOf(">Repairers</h2>", StringComparison.Ordinal);
        Assert.True(principals > 0 && principals < claimSources && claimSources < repairers, "The panels follow the type order.");
        Assert.DoesNotContain(">Storage</h2>", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Third Party Engineers</h2>", html, StringComparison.Ordinal);
        Assert.Contains(
            "<th>Principal code</th><th>Name</th><th>Contact person</th><th>Email</th><th>Phone</th><th>Last case</th><th>Status</th>",
            html, StringComparison.Ordinal);
        Assert.Contains(
            "<th>Name</th><th>Contact person</th><th>Email</th><th>Phone</th><th>Linked principals</th><th>Last case</th><th>Status</th>",
            html[claimSources..repairers], StringComparison.Ordinal);
        var repairerPanel = html[repairers..];
        Assert.Contains(
            "<th>Name</th><th>Contact person</th><th>Email</th><th>Phone</th><th>Postcode</th><th>Linked principals</th><th>Last case</th><th>Status</th>",
            repairerPanel, StringComparison.Ordinal);
        Assert.Contains("<td class=\"mono\">GFP1</td><td>Grouped Fixture Principal</td>", html, StringComparison.Ordinal);
        Assert.Contains("<td>S9 2RQ</td><td class=\"mono\">GFP1</td>", repairerPanel, StringComparison.Ordinal);
        Assert.True(
            repairerPanel.IndexOf("Grouped Fixture Alpha Repairer", StringComparison.Ordinal)
                < repairerPanel.IndexOf("Grouped Fixture Zulu Repairer", StringComparison.Ordinal),
            "Each panel is sorted by name.");

        var repairersOnly = await IntakeWebDriver.GetHtmlAsync(client, "/Administration/Contacts?search=Grouped%20Fixture&type=Repairer");
        Assert.Contains(">Repairers</h2>", repairersOnly, StringComparison.Ordinal);
        Assert.DoesNotContain(">Principals</h2>", repairersOnly, StringComparison.Ordinal);
        Assert.DoesNotContain(">Claim Sources</h2>", repairersOnly, StringComparison.Ordinal);

        var create = await IntakeWebDriver.GetHtmlAsync(client, "/Administration/Contacts?createType=Repairer");
        Assert.Contains("<label for=\"Telephone\">Phone</label>", create, StringComparison.Ordinal);
        Assert.DoesNotContain("Enter the details for this", create, StringComparison.Ordinal);
    }

    /// <summary>
    /// R128: a panel shows 50 contacts, and in the all-types view its Next opens
    /// that type's own list at page 2; a panel that fits one page has no pager,
    /// and the all-types view ignores a page number.
    /// </summary>
    [Fact]
    public async Task AMoreThanFiftyTypeShowsItsFirstPageAndLinksToTheNext()
    {
        using var factory = new IntakeWebApplicationFactory();
        await SeedContactsAsync(factory, "repairer", RepairerPrefix, first: 0, count: 120);
        await SeedContactsAsync(factory, "claim_source", ClaimsPrefix, first: 0, count: 3);
        using var client = IntakeWebDriver.CreateClient(factory);

        foreach (var path in new[] { "/Administration/Contacts?search=Paged%20Fixture", "/Administration/Contacts?search=Paged%20Fixture&pageNumber=2" })
        {
            var html = await IntakeWebDriver.GetHtmlAsync(client, path);

            Assert.Equal(Enumerable.Range(0, 50), RowNumbers(html, RepairerPrefix));
            Assert.Equal(Enumerable.Range(0, 3), RowNumbers(html, ClaimsPrefix));
            var pager = Assert.Single(Pagers(html));
            Assert.StartsWith(OperatorLabels.Contacts.Plural(ContactRole.Repairer), pager.Label, StringComparison.Ordinal);
            Assert.Equal(1, pager.Page);
            var next = Assert.Single(pager.Links);
            Assert.Equal("/Administration/Contacts", next.Path);
            Assert.Equal("Repairer", next.Query["type"]);
            Assert.Equal("Paged Fixture", next.Query["search"]);
            Assert.Equal("2", next.Query["pageNumber"]);
        }
    }

    /// <summary>
    /// A type's own view walks its rows 50 at a time: every row once, in name
    /// order; Previous and Next appear only where they apply and keep the type
    /// and the search; a page past the end shows the empty state; the toolbar
    /// returns to page 1 and Add contact keeps the page.
    /// </summary>
    [Fact]
    public async Task ThePagedTypeViewWalksEveryRowOnce()
    {
        using var factory = new IntakeWebApplicationFactory();
        await SeedContactsAsync(factory, "repairer", RepairerPrefix, first: 0, count: 120);
        using var client = IntakeWebDriver.CreateClient(factory);
        const string typeView = "/Administration/Contacts?type=Repairer&search=Paged%20Fixture";

        var walked = new List<int>();
        foreach (var (page, expectedRows, expectedLinkPages) in new[] { (1, 50, new[] { 2 }), (2, 50, new[] { 1, 3 }), (3, 20, new[] { 2 }) })
        {
            var html = await IntakeWebDriver.GetHtmlAsync(client, $"{typeView}&pageNumber={page}");

            var rows = RowNumbers(html, RepairerPrefix);
            walked.AddRange(rows);
            Assert.Equal(expectedRows, rows.Count);
            var pager = Assert.Single(Pagers(html));
            Assert.Equal(page, pager.Page);
            Assert.Equal(expectedLinkPages, pager.Links.Select(link => int.Parse(link.Query["pageNumber"]!, CultureInfo.InvariantCulture)));
            Assert.All(pager.Links, link =>
            {
                Assert.Equal("Repairer", link.Query["type"]);
                Assert.Equal("Paged Fixture", link.Query["search"]);
            });
            Assert.DoesNotContain("pageNumber", ToolbarForm(html), StringComparison.OrdinalIgnoreCase);
            var addContacts = AddContactLinks(html);
            Assert.NotEmpty(addContacts);
            Assert.All(addContacts, link =>
            {
                Assert.Equal("Repairer", link.Query["type"]);
                Assert.Equal("Paged Fixture", link.Query["search"]);
                Assert.Equal(page.ToString(CultureInfo.InvariantCulture), link.Query["pageNumber"]);
            });
        }
        Assert.Equal(Enumerable.Range(0, 120), walked);

        // A page number below 1 opens page 1; a page past the end has nothing to show.
        Assert.Equal(1, Assert.Single(Pagers(await IntakeWebDriver.GetHtmlAsync(client, $"{typeView}&pageNumber=-3"))).Page);
        var beyond = await IntakeWebDriver.GetHtmlAsync(client, $"{typeView}&pageNumber=4");
        Assert.Empty(RowNumbers(beyond, RepairerPrefix));
        Assert.Empty(Pagers(beyond));
        Assert.Contains("<div class=\"empty\">", beyond, StringComparison.Ordinal);
    }

    /// <summary>
    /// The list reads no panel once per row: with the same types present, six
    /// contacts send the same SQL commands as three (R25).
    /// </summary>
    [Fact]
    public async Task TheContactsListReadsTheSameForThreeRowsAndSixRows()
    {
        var counter = new CommandCountingInterceptor();
        using var factory = new IntakeWebApplicationFactory("Development", true, commandInterceptor: counter);
        using var client = IntakeWebDriver.CreateClient(factory);
        const string path = "/Administration/Contacts?search=Paged%20Fixture";
        await SeedContactsAsync(factory, "repairer", RepairerPrefix, first: 0, count: 2);
        await SeedContactsAsync(factory, "claim_source", ClaimsPrefix, first: 0, count: 1);
        // The first load pays one-off work; the counts are later ones.
        _ = await IntakeWebDriver.GetHtmlAsync(client, path);

        counter.Reset();
        var withThreeHtml = await IntakeWebDriver.GetHtmlAsync(client, path);
        var withThreeRows = counter.Count;
        var withThreeDescription = counter.Describe();

        await SeedContactsAsync(factory, "repairer", RepairerPrefix, first: 2, count: 2);
        await SeedContactsAsync(factory, "claim_source", ClaimsPrefix, first: 1, count: 1);
        counter.Reset();
        var withSixHtml = await IntakeWebDriver.GetHtmlAsync(client, path);
        var withSixRows = counter.Count;

        Assert.True(withThreeRows > 0, "The interceptor observed no commands at all.");
        Assert.Equal(3, RowNumbers(withThreeHtml, RepairerPrefix).Count + RowNumbers(withThreeHtml, ClaimsPrefix).Count);
        Assert.Equal(6, RowNumbers(withSixHtml, RepairerPrefix).Count + RowNumbers(withSixHtml, ClaimsPrefix).Count);
        Assert.True(
            withThreeRows == withSixRows,
            $"The Contacts list grew with the rows: {withThreeRows} commands for three and {withSixRows} for six."
            + Environment.NewLine + withThreeDescription);
    }

    /// <summary>
    /// Names are unique per type: the contact's page refuses a second contact
    /// of the same type and name with the one sentence the list's Add uses. The
    /// page carries its type as a greyed box and Linked principals as one list.
    /// </summary>
    [Fact]
    public async Task EditRefusesADuplicateNameOfTheSameType()
    {
        using var factory = new IntakeWebApplicationFactory();
        var secondId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var administration = scope.ServiceProvider.GetRequiredService<IContactDirectoryAdministration>();
            foreach (var (id, name) in new[] { (Guid.NewGuid(), "Duplicate Fixture One"), (secondId, "Duplicate Fixture Two") })
            {
                await administration.SaveAsync(new(
                    Administrator, id, 0, name, null, null, null, null, null, true, ContactRole.Storage, null,
                    CaseInspectionMode.PhysicalAddress, [], $"contact-edit:{name}"), default);
            }
        }
        using var client = IntakeWebDriver.CreateClient(factory);
        var path = $"/Administration/Contacts/Edit/{secondId:D}";
        var editor = await IntakeWebDriver.GetHtmlAsync(client, path);
        Assert.Contains("<input id=\"contact-type\" value=\"Storage\" readonly", editor, StringComparison.Ordinal);
        Assert.Contains("<label for=\"Telephone\">Phone</label>", editor, StringComparison.Ordinal);
        Assert.Contains("<div class=\"contact-links\">", editor, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"tabs\"", editor, StringComparison.Ordinal);
        Assert.DoesNotContain("Guidance for new Cases", editor, StringComparison.Ordinal);

        using var refused = await client.PostAsync(
            $"{path}?handler=Save",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = InputValue(editor, "__RequestVerificationToken"),
                ["ContactId"] = secondId.ToString("D"),
                ["ExpectedVersion"] = InputValue(editor, "ExpectedVersion"),
                ["OperationKey"] = InputValue(editor, "OperationKey"),
                ["Name"] = "Duplicate Fixture One",
                ["Active"] = bool.TrueString
            }));

        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains("A contact with that name already exists.", await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal("Duplicate Fixture Two", await factory.Database.ScalarAsync<string>(
            $"SELECT Name FROM Organizations WHERE Id = '{secondId:D}';"));
    }

    /// <summary>
    /// R119: a contact's link to an inactive Principal is offered, ticked, in
    /// Linked principals on its page, so saving the page keeps the link.
    /// </summary>
    [Fact]
    public async Task APageSaveKeepsALinkToAnInactivePrincipal()
    {
        using var factory = new IntakeWebApplicationFactory();
        var repairerId = Guid.NewGuid();
        Guid inactivePrincipalId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var administration = scope.ServiceProvider.GetRequiredService<IContactDirectoryAdministration>();
            var principal = new SaveContactRequest(
                Administrator, Guid.NewGuid(), 0, "Inactive Link Fixture Principal", null, null, null, null, null, true,
                ContactRole.Principal, "ILFP1", CaseInspectionMode.PhysicalAddress, [], "contact-edit:inactive-principal");
            var created = await administration.SaveAsync(principal, default);
            await administration.SaveAsync(principal with
            {
                ExpectedVersion = created.Version,
                Active = false,
                OperationKey = "contact-edit:inactive-principal:disable"
            }, default);
            inactivePrincipalId = created.OwnPrincipalIds!.Single();
            await administration.SaveAsync(new(
                Administrator, repairerId, 0, "Inactive Link Fixture Repairer", null, null, null, null, null, true,
                ContactRole.Repairer, null, CaseInspectionMode.PhysicalAddress, [inactivePrincipalId],
                "contact-edit:inactive-link"), default);
        }
        using var client = IntakeWebDriver.CreateClient(factory);
        var path = $"/Administration/Contacts/Edit/{repairerId:D}";
        var editor = await IntakeWebDriver.GetHtmlAsync(client, path);
        var ticked = Assert.Single(CheckedLinkRegex().Matches(editor)).Groups["id"].Value;
        Assert.Equal(inactivePrincipalId.ToString("D"), ticked);

        using var saved = await client.PostAsync(
            $"{path}?handler=Save",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = InputValue(editor, "__RequestVerificationToken"),
                ["ContactId"] = InputValue(editor, "ContactId"),
                ["ExpectedVersion"] = InputValue(editor, "ExpectedVersion"),
                ["OperationKey"] = InputValue(editor, "OperationKey"),
                ["Name"] = "Inactive Link Fixture Repairer renamed",
                ["Active"] = bool.TrueString,
                ["LinkedPrincipalIds"] = ticked
            }));

        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal("Inactive Link Fixture Repairer renamed", await factory.Database.ScalarAsync<string>(
            $"SELECT Name FROM Organizations WHERE Id = '{repairerId:D}';"));
        Assert.Equal(inactivePrincipalId, await factory.Database.ScalarAsync<Guid>(
            $"SELECT PrincipalId FROM ContactPrincipalLinks WHERE OrganizationId = '{repairerId:D}';"));
    }

    [GeneratedRegex(
        "<input type=\"checkbox\" name=\"LinkedPrincipalIds\" value=\"(?<id>[^\"]+)\" checked",
        RegexOptions.CultureInvariant)]
    private static partial Regex CheckedLinkRegex();

    private const string RepairerPrefix = "Paged Fixture Repairer";
    private const string ClaimsPrefix = "Paged Fixture Claims";

    /// <summary>Adds <paramref name="count"/> contacts of one type in one save, named
    /// "{prefix} 000" onward from <paramref name="first"/>, so name order is number order.</summary>
    private static async Task SeedContactsAsync(
        IntakeWebApplicationFactory factory, string typeCode, string prefix, int first, int count)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        context.Organizations.AddRange(Enumerable.Range(first, count).Select(number => new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            Name = $"{prefix} {number:D3}",
            Role = typeCode,
            Active = true,
            Version = 0
        }));
        await context.SaveChangesAsync();
    }

    /// <summary>The numbers in the names of the listed contacts, in page order.</summary>
    private static List<int> RowNumbers(string html, string prefix) =>
    [.. Regex.Matches(html, $"<td>{Regex.Escape(prefix)} (?<number>\\d{{3}})</td>")
        .Select(match => int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture))];

    private sealed record PageLink(string Path, NameValueCollection Query);

    private sealed record Pager(string Label, int Page, IReadOnlyList<PageLink> Links);

    private static PageLink ToLink(string href)
    {
        var parts = WebUtility.HtmlDecode(href).Split('?', 2);
        return new(parts[0], HttpUtility.ParseQueryString(parts.Length > 1 ? parts[1] : string.Empty));
    }

    private static List<PageLink> LinksIn(string html, string cssClass) =>
    [.. Regex.Matches(html, $"<a\\b(?=[^>]*\\bclass=\"{Regex.Escape(cssClass)}\")[^>]*\\bhref=\"(?<href>[^\"]*)\"")
        .Select(match => ToLink(match.Groups["href"].Value))];

    private static List<Pager> Pagers(string html) =>
    [.. PagerRegex().Matches(html).Select(match => new Pager(
        WebUtility.HtmlDecode(match.Groups["label"].Value),
        int.Parse(PageRegex().Match(match.Groups["body"].Value).Groups["page"].Value, CultureInfo.InvariantCulture),
        LinksIn(match.Groups["body"].Value, "btn btn--small")))];

    private static List<PageLink> AddContactLinks(string html) => LinksIn(html, "contact-kind");

    private static string ToolbarForm(string html) => ToolbarRegex().Match(html).Value;

    [GeneratedRegex(
        "<div class=\"panel-foot\">\\s*<nav class=\"pagination\" aria-label=\"(?<label>[^\"]*)\">(?<body>.*?)</nav>",
        RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex PagerRegex();

    [GeneratedRegex("<span>Page (?<page>\\d+)</span>", RegexOptions.CultureInvariant)]
    private static partial Regex PageRegex();

    [GeneratedRegex("<form\\b[^>]*class=\"contact-toolbar\".*?</form>", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex ToolbarRegex();

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
