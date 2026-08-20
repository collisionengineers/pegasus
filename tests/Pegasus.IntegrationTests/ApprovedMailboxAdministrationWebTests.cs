using System.Net;
using System.Text.RegularExpressions;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The administration surface is where a mailbox address is turned into an approved
/// mailbox, so it is where the fail-closed, immutability, and no-internal-identifier
/// rules must be visible to a person rather than only to a unit test (MAIL-002).
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class ApprovedMailboxAdministrationWebTests
{
    private const string NewAddress = "estate@collisionengineers.co.uk";

    [Fact]
    public async Task AddingAnAddressResolvesItsIdentityWithoutExposingItOnThePage()
    {
        var resolution = new ApprovedMailboxIdentityResolution(
            "resolved-mailbox-id",
            "resolved-inbox-id",
            "resolved-sent-id");
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            approvedMailboxIdentityResolver: new StubResolver(resolution));
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
            ["Reason"] = "Add the second approved mailbox",
            ["__RequestVerificationToken"] = AntiforgeryToken(page)
        });

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var reloaded = await GetPageAsync(client);
        Assert.Contains(NewAddress, reloaded, StringComparison.Ordinal);
        // The resolved identity is bound in the database and never rendered.
        Assert.DoesNotContain("resolved-mailbox-id", reloaded, StringComparison.Ordinal);
        Assert.DoesNotContain("resolved-inbox-id", reloaded, StringComparison.Ordinal);
        Assert.DoesNotContain("resolved-sent-id", reloaded, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAddressThatCannotBeResolvedIsRefusedWithoutCreatingARow()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            approvedMailboxIdentityResolver: new StubResolver(resolution: null));
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
            ["Reason"] = "Add an address the tenant does not recognise",
            ["__RequestVerificationToken"] = AntiforgeryToken(page)
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("The address could not be found in the mail system.", html, StringComparison.Ordinal);
        Assert.DoesNotContain(
            $"<td>{NewAddress}</td>",
            await GetPageAsync(client),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RebindingAnEstablishedMailboxsAddressIsRefusedWithTheImmutabilityReason()
    {
        var resolution = new ApprovedMailboxIdentityResolution(
            "estate-mailbox-id",
            "estate-inbox-id",
            "estate-sent-id");
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            approvedMailboxIdentityResolver: new StubResolver(resolution));
        using var client = IntakeWebDriver.CreateClient(factory);

        var page = await GetPageAsync(client);
        var mailboxId = NewMailboxId(page);
        var created = await PostAsync(client, new()
        {
            ["MailboxId"] = mailboxId,
            ["ExpectedVersion"] = "0",
            ["OperationKey"] = OperationKey(page),
            ["Address"] = NewAddress,
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
            ["Address"] = "a-different-address@collisionengineers.co.uk",
            ["SelectedRouteScopes"] = "InboundIntake",
            ["SelectedState"] = "Approved",
            ["Reason"] = "Attempt to point this row at another mailbox",
            ["__RequestVerificationToken"] = AntiforgeryToken(reloaded)
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("cannot be changed once saved", html, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "a-different-address@collisionengineers.co.uk",
            await GetPageAsync(client),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThePageNeverShowsMailboxOrFolderIdentifiersOrDuplicatedRunbookNarration()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var page = await GetPageAsync(client);

        // The Exchange-tenant-permission explanation and mailbox_access_denied
        // failure mode are operational documentation, owned by docs/runbook.md's
        // "Approved mailbox estate" section, not UI copy (design authority:
        // docs/design/README.md line 160, no lede/subtitle narration).
        Assert.DoesNotContain("does not grant Exchange access", page, StringComparison.Ordinal);
        Assert.DoesNotContain("mailbox_access_denied", page, StringComparison.Ordinal);
        // No internal identifier is ever asked for or shown, for any role
        // (docs/design/README.md line 168; operator statement 2026-08-20).
        Assert.DoesNotContain("Mailbox identity", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Inbox folder identity", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Sent folder identity", page, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"MailboxIdentity\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"InboxFolderIdentity\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"SentFolderIdentity\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Version</th>", page, StringComparison.Ordinal);
        // The per-mailbox polling column is present for the seeded mailbox.
        Assert.Contains("Not yet polled.", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdministratorRefreshesOnlyServerResolvedLogicalFolderBindings()
    {
        var resolver = new SequencedResolver(
            Resolution(new(MailLogicalFolderType.Instructions, "instructions-id")),
            Resolution(new(MailLogicalFolderType.Billing, "billing-id")));
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            approvedMailboxIdentityResolver: resolver);
        using var client = IntakeWebDriver.CreateClient(factory);

        var page = await GetPageAsync(client);
        var mailboxId = NewMailboxId(page);
        var created = await PostAsync(client, new()
        {
            ["MailboxId"] = mailboxId,
            ["ExpectedVersion"] = "0",
            ["OperationKey"] = OperationKey(page),
            ["Address"] = NewAddress,
            ["SelectedRouteScopes"] = "InboundIntake",
            ["SelectedState"] = "Approved",
            ["Reason"] = "Add the second approved mailbox",
            ["__RequestVerificationToken"] = AntiforgeryToken(page)
        });
        Assert.Equal(HttpStatusCode.Found, created.StatusCode);

        var configured = await GetPageAsync(client);
        Assert.Contains("Instructions — Configured", configured, StringComparison.Ordinal);
        Assert.Contains("Billing — Not configured", configured, StringComparison.Ordinal);
        Assert.DoesNotContain("instructions-id", configured, StringComparison.Ordinal);
        var operationKeys = OperationKeyTagRegex().Matches(configured);
        var refreshed = await client.PostAsync(
            "/Administration/Mailboxes?handler=ResolveFolders",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["MailboxId"] = mailboxId,
                ["ExpectedVersion"] = "1",
                ["OperationKey"] = Value(operationKeys[^2].Value),
                ["Address"] = NewAddress,
                ["SelectedState"] = "Approved",
                ["Reason"] = "Refresh approved logical folder bindings",
                ["__RequestVerificationToken"] = AntiforgeryToken(configured)
            }));

        Assert.Equal(HttpStatusCode.Found, refreshed.StatusCode);
        var reloaded = await GetPageAsync(client);
        Assert.Contains("Instructions — Not configured", reloaded, StringComparison.Ordinal);
        Assert.Contains("Billing — Configured", reloaded, StringComparison.Ordinal);
        Assert.DoesNotContain("billing-id", reloaded, StringComparison.Ordinal);
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

    private sealed class StubResolver(ApprovedMailboxIdentityResolution? resolution)
        : IResolveApprovedMailboxIdentity
    {
        public Task<ApprovedMailboxIdentityResolution?> ResolveAsync(
            string address,
            CancellationToken cancellationToken) => Task.FromResult(resolution);
    }

    private sealed class SequencedResolver(params ApprovedMailboxIdentityResolution[] resolutions)
        : IResolveApprovedMailboxIdentity
    {
        private int _index;

        public Task<ApprovedMailboxIdentityResolution?> ResolveAsync(
            string address,
            CancellationToken cancellationToken) =>
            Task.FromResult<ApprovedMailboxIdentityResolution?>(
                resolutions[Math.Min(_index++, resolutions.Length - 1)]);
    }

    private static ApprovedMailboxIdentityResolution Resolution(
        ApprovedMailboxFolderBinding binding) => new(
        "resolved-mailbox-id",
        "resolved-inbox-id",
        "resolved-sent-id",
        [binding]);

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex AntiforgeryTagRegex();

    [GeneratedRegex("<input[^>]*name=\"MailboxId\"[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex NewMailboxIdTagRegex();

    [GeneratedRegex("<input[^>]*name=\"OperationKey\"[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex OperationKeyTagRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex ValueRegex();
}
