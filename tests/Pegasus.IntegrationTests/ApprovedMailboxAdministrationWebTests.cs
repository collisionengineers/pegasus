using System.Net;
using System.Text.RegularExpressions;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The administration surface is where a mailbox identity is bound, so it is where the
/// fail-closed and immutability rules must be visible to a person rather than only to a
/// unit test.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class ApprovedMailboxAdministrationWebTests
{
    private const string NewAddress = "estate@collisionengineers.co.uk";

    [Fact]
    public async Task AddingAMailboxWithItsIdentitiesRoundTripsOntoThePage()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var page = await GetPageAsync(client);
        var response = await PostAsync(client, new()
        {
            ["MailboxId"] = NewMailboxId(page),
            ["ExpectedVersion"] = "0",
            ["OperationKey"] = OperationKey(page),
            ["Address"] = NewAddress,
            ["MailboxIdentity"] = "estate-mailbox",
            ["InboxFolderIdentity"] = "estate-inbox",
            ["SelectedRouteScopes"] = "InboundIntake",
            ["SelectedState"] = "Approved",
            ["Reason"] = "Add the second approved mailbox",
            ["__RequestVerificationToken"] = AntiforgeryToken(page)
        });

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var reloaded = await GetPageAsync(client);
        Assert.Contains(NewAddress, reloaded, StringComparison.Ordinal);
        Assert.Contains("estate-mailbox", reloaded, StringComparison.Ordinal);
        Assert.Contains("estate-inbox", reloaded, StringComparison.Ordinal);
        // A bound identity is shown, not editable.
        Assert.Contains("readonly", reloaded, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnApprovedSaveWithoutTheRequiredIdentityIsRefusedWithItsReason()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var page = await GetPageAsync(client);
        var response = await PostAsync(client, new()
        {
            ["MailboxId"] = NewMailboxId(page),
            ["ExpectedVersion"] = "0",
            ["OperationKey"] = OperationKey(page),
            ["Address"] = NewAddress,
            ["SelectedRouteScopes"] = "InboundIntake",
            ["SelectedState"] = "Approved",
            ["Reason"] = "Approve before the tenant grant exists",
            ["__RequestVerificationToken"] = AntiforgeryToken(page)
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("needs its mailbox identity", html, StringComparison.Ordinal);
        Assert.DoesNotContain(
            $"<td>{NewAddress}</td>",
            await GetPageAsync(client),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RebindingASavedMailboxIdentityIsRefusedWithTheImmutabilityReason()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var page = await GetPageAsync(client);
        var mailboxId = NewMailboxId(page);
        var created = await PostAsync(client, new()
        {
            ["MailboxId"] = mailboxId,
            ["ExpectedVersion"] = "0",
            ["OperationKey"] = OperationKey(page),
            ["Address"] = NewAddress,
            ["MailboxIdentity"] = "estate-mailbox",
            ["InboxFolderIdentity"] = "estate-inbox",
            ["SelectedRouteScopes"] = "InboundIntake",
            ["SelectedState"] = "Approved",
            ["Reason"] = "Add the second approved mailbox",
            ["__RequestVerificationToken"] = AntiforgeryToken(page)
        });
        Assert.Equal(HttpStatusCode.Found, created.StatusCode);

        var reloaded = await GetPageAsync(client);
        var response = await PostAsync(client, new()
        {
            ["MailboxId"] = mailboxId,
            ["ExpectedVersion"] = "1",
            ["OperationKey"] = Guid.NewGuid().ToString("N"),
            ["Address"] = NewAddress,
            ["MailboxIdentity"] = "a-different-mailbox",
            ["InboxFolderIdentity"] = "estate-inbox",
            ["SelectedRouteScopes"] = "InboundIntake",
            ["SelectedState"] = "Approved",
            ["Reason"] = "Attempt to point this row at another mailbox",
            ["__RequestVerificationToken"] = AntiforgeryToken(reloaded)
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("cannot be changed once saved", html, StringComparison.Ordinal);
        Assert.DoesNotContain("a-different-mailbox", await GetPageAsync(client), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThePageStatesThatApprovalGrantsNoExchangeAccess()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var page = await GetPageAsync(client);

        Assert.Contains(
            "does not grant Exchange access",
            page,
            StringComparison.Ordinal);
        Assert.Contains("mailbox_access_denied", page, StringComparison.Ordinal);
        // The per-mailbox polling column is present for the seeded mailbox.
        Assert.Contains("Not yet polled.", page, StringComparison.Ordinal);
    }

    private static async Task<string> GetPageAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/Administration/Mailboxes");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        Dictionary<string, string> fields) =>
        client.PostAsync(
            "/Administration/Mailboxes?handler=Update",
            new FormUrlEncodedContent(fields));

    private static string AntiforgeryToken(string html) =>
        Value(AntiforgeryTagRegex().Match(html).Value);

    // The add form is the last of these on the page; the earlier ones belong to the
    // per-row update forms.
    private static string NewMailboxId(string html) =>
        Value(NewMailboxIdTagRegex().Matches(html)[^1].Value);

    private static string OperationKey(string html) =>
        Value(OperationKeyTagRegex().Matches(html)[^1].Value);

    private static string Value(string tag)
    {
        var match = ValueRegex().Match(tag);
        Assert.True(match.Success, $"No value attribute in '{tag}'.");
        return match.Groups["value"].Value;
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex AntiforgeryTagRegex();

    [GeneratedRegex("<input[^>]*name=\"MailboxId\"[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex NewMailboxIdTagRegex();

    [GeneratedRegex("<input[^>]*name=\"OperationKey\"[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex OperationKeyTagRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex ValueRegex();
}
