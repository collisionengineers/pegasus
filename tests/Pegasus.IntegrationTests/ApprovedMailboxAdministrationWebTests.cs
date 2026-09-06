using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;

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
    public async Task NonAdministratorCannotOpenMailSettings()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "User");

        using var response = await client.GetAsync("/Administration/Mailboxes");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("Update")]
    [InlineData("ResolveFolders")]
    [InlineData("SaveCategory")]
    public async Task NonAdministratorCannotPostMailSettingsHandlers(string handler)
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "User");

        using var response = await client.PostAsync(
            $"/Administration/Mailboxes?handler={handler}",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdministratorSeesTheAdminLayoutBothTablesAndEveryHandler()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var page = await GetPageAsync(client);

        Assert.Contains("class=\"admin-layout\"", page, StringComparison.Ordinal);
        Assert.Contains("<h2 id=\"mail-settings-title\">Mail settings</h2>", page, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\"", page, StringComparison.Ordinal);
        Assert.Contains("<caption class=\"sr-only\">Approved mailboxes</caption>", page, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Mailbox</th>", page, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Scope</th>", page, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Last update</th>", page, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Activated</th>", page, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Subscription</th>", page, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Review folders / Refresh</th>", page, StringComparison.Ordinal);
        Assert.Contains("<caption class=\"sr-only\">Mail categories</caption>", page, StringComparison.Ordinal);
        Assert.Contains("?handler=Update", page, StringComparison.Ordinal);
        Assert.Contains("?handler=SaveCategory", page, StringComparison.Ordinal);
        Assert.Contains("href=\"https://github.com/collisionengineers/pegasus/blob/dev/docs/runbook.md#approved-mailbox-estate\"", page, StringComparison.Ordinal);
        Assert.Contains("value=\"StaffSend\"", page, StringComparison.Ordinal);
        Assert.Contains("Verified encoded-message size limit (bytes)", page, StringComparison.Ordinal);
    }

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
            ["MailboxForm.MailboxId"] = NewMailboxId(page),
            ["MailboxForm.ExpectedVersion"] = "0",
            ["MailboxForm.OperationKey"] = OperationKey(page),
            ["MailboxForm.Address"] = NewAddress,
            ["MailboxForm.SelectedRouteScopes"] = "InboundIntake",
            ["MailboxForm.SelectedState"] = "Approved",
            ["MailboxForm.Reason"] = "Add the second approved mailbox",
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
            ["MailboxForm.MailboxId"] = NewMailboxId(page),
            ["MailboxForm.ExpectedVersion"] = "0",
            ["MailboxForm.OperationKey"] = OperationKey(page),
            ["MailboxForm.Address"] = NewAddress,
            ["MailboxForm.SelectedRouteScopes"] = "InboundIntake",
            ["MailboxForm.SelectedState"] = "Approved",
            ["MailboxForm.Reason"] = "Add an address the tenant does not recognise",
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
    public async Task ReadAccessIsCheckedBeforeApprovalAndFailureDoesNotEnableTheMailbox()
    {
        var resolver = new AccessResolver(new("resolved-mailbox-id", "resolved-inbox-id", "resolved-sent-id"), canRead: false);
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, approvedMailboxIdentityResolver: resolver);
        using var client = IntakeWebDriver.CreateClient(factory);
        var page = await GetPageAsync(client);

        var response = await PostAsync(client, new()
        {
            ["MailboxForm.MailboxId"] = NewMailboxId(page),
            ["MailboxForm.ExpectedVersion"] = "0",
            ["MailboxForm.OperationKey"] = OperationKey(page),
            ["MailboxForm.Address"] = NewAddress,
            ["MailboxForm.SelectedRouteScopes"] = "InboundIntake",
            ["MailboxForm.SelectedState"] = "Approved",
            ["MailboxForm.Reason"] = "Verify the mailbox before enabling it",
            ["__RequestVerificationToken"] = AntiforgeryToken(page)
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, resolver.AccessChecks);
        Assert.Contains("could not verify read access", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.DoesNotContain($"<td>{NewAddress}</td>", await GetPageAsync(client), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StaffSendCannotBeEnabledWithoutAVerifiedEncodedMessageLimit()
    {
        var resolver = new AccessResolver(new("resolved-mailbox-id", "resolved-inbox-id", "resolved-sent-id"), canRead: true);
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, approvedMailboxIdentityResolver: resolver);
        using var client = IntakeWebDriver.CreateClient(factory);
        var page = await GetPageAsync(client);

        var response = await PostAsync(client, new()
        {
            ["MailboxForm.MailboxId"] = NewMailboxId(page),
            ["MailboxForm.ExpectedVersion"] = "0",
            ["MailboxForm.OperationKey"] = OperationKey(page),
            ["MailboxForm.Address"] = NewAddress,
            ["MailboxForm.SelectedRouteScopes"] = "StaffSend",
            ["MailboxForm.SelectedState"] = "Approved",
            ["MailboxForm.Reason"] = "Enable staff send",
            ["__RequestVerificationToken"] = AntiforgeryToken(page)
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, resolver.AccessChecks);
        Assert.Contains("verified encoded-message size limit", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DisabledMailboxCanChangeCoordinatesThenReenableWithANewGeneration()
    {
        var replacementAddress = "replacement@collisionengineers.co.uk";
        var resolver = new SequencedResolver(
            new("first-mailbox-id", "first-inbox-id", "first-sent-id"),
            new("replacement-mailbox-id", "replacement-inbox-id", "replacement-sent-id"),
            new("replacement-mailbox-id", "replacement-inbox-id", "replacement-sent-id"));
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, approvedMailboxIdentityResolver: resolver);
        using var client = IntakeWebDriver.CreateClient(factory);

        var page = await GetPageAsync(client);
        var mailboxId = NewMailboxId(page);
        var created = await PostAsync(client, new()
        {
            ["MailboxForm.MailboxId"] = mailboxId,
            ["MailboxForm.ExpectedVersion"] = "0",
            ["MailboxForm.OperationKey"] = OperationKey(page),
            ["MailboxForm.Address"] = NewAddress,
            ["MailboxForm.SelectedRouteScopes"] = "InboundIntake",
            ["MailboxForm.SelectedState"] = "Disabled",
            ["MailboxForm.Reason"] = "Record an inactive mailbox before activation",
            ["__RequestVerificationToken"] = AntiforgeryToken(page)
        });
        Assert.Equal(HttpStatusCode.Found, created.StatusCode);

        var disabled = await GetPageAsync(client);
        var replaced = await PostAsync(client, new()
        {
            ["MailboxForm.MailboxId"] = mailboxId,
            ["MailboxForm.ExpectedVersion"] = "1",
            ["MailboxForm.OperationKey"] = Guid.NewGuid().ToString("N"),
            ["MailboxForm.Address"] = replacementAddress,
            ["MailboxForm.SelectedRouteScopes"] = "InboundIntake",
            ["MailboxForm.SelectedState"] = "Disabled",
            ["MailboxForm.Reason"] = "Correct the inactive mailbox coordinates",
            ["__RequestVerificationToken"] = AntiforgeryToken(disabled)
        });
        Assert.Equal(HttpStatusCode.Found, replaced.StatusCode);

        var replacement = await GetPageAsync(client);
        var reenabled = await PostAsync(client, new()
        {
            ["MailboxForm.MailboxId"] = mailboxId,
            ["MailboxForm.ExpectedVersion"] = "2",
            ["MailboxForm.OperationKey"] = Guid.NewGuid().ToString("N"),
            ["MailboxForm.Address"] = replacementAddress,
            ["MailboxForm.SelectedRouteScopes"] = "InboundIntake",
            ["MailboxForm.SelectedState"] = "Approved",
            ["MailboxForm.Reason"] = "Enable the verified replacement mailbox",
            ["__RequestVerificationToken"] = AntiforgeryToken(replacement)
        });
        Assert.Equal(HttpStatusCode.Found, reenabled.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var stored = await context.ApprovedMailboxes.SingleAsync(item =>
            item.Id == Guid.Parse(mailboxId));
        Assert.Equal(replacementAddress, stored.Address);
        Assert.Equal("replacement-mailbox-id", stored.MailboxIdentity);
        Assert.Equal(1, stored.MailboxGeneration);
        Assert.Equal(ApprovedMailboxState.Approved.ToString(), stored.State);
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
            ["MailboxForm.MailboxId"] = mailboxId,
            ["MailboxForm.ExpectedVersion"] = "0",
            ["MailboxForm.OperationKey"] = OperationKey(page),
            ["MailboxForm.Address"] = NewAddress,
            ["MailboxForm.SelectedRouteScopes"] = "InboundIntake",
            ["MailboxForm.SelectedState"] = "Approved",
            ["MailboxForm.Reason"] = "Add the second approved mailbox",
            ["__RequestVerificationToken"] = AntiforgeryToken(page)
        });
        Assert.Equal(HttpStatusCode.Found, created.StatusCode);

        var reloaded = await GetPageAsync(client);
        var response = await PostAsync(client, new()
        {
            ["MailboxForm.MailboxId"] = mailboxId,
            ["MailboxForm.ExpectedVersion"] = "1",
            ["MailboxForm.OperationKey"] = Guid.NewGuid().ToString("N"),
            ["MailboxForm.Address"] = "a-different-address@collisionengineers.co.uk",
            ["MailboxForm.SelectedRouteScopes"] = "InboundIntake",
            ["MailboxForm.SelectedState"] = "Approved",
            ["MailboxForm.Reason"] = "Attempt to point this row at another mailbox",
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
        // The seeded mailbox has not been activated and has no Graph subscription yet.
        Assert.Contains("<td>Not activated</td>", page, StringComparison.Ordinal);
        Assert.Contains("<td>None.</td>", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThePageShowsActivationAndSubscriptionHealthPerMailbox()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var mailboxId = TestMailboxId.From("instructions");
        var activatedAtUtc = new DateTimeOffset(2026, 8, 27, 10, 20, 33, TimeSpan.Zero);
        var expiresAtUtc = new DateTimeOffset(2026, 9, 2, 9, 5, 0, TimeSpan.Zero);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using (var context = await contextFactory.CreateDbContextAsync())
            {
                var mailbox = await context.ApprovedMailboxes.SingleAsync(item => item.Id == mailboxId);
                mailbox.ActivatedAtUtc = activatedAtUtc;
                var poll = await context.ApprovedInboxPollStates
                    .SingleOrDefaultAsync(item => item.ApprovedMailboxId == mailboxId);
                if (poll is null)
                {
                    poll = new()
                    {
                        ApprovedMailboxId = mailboxId,
                        MailboxAddress = mailbox.Address,
                        ScopeFingerprint = new string('0', 64),
                        ActivatedAtUtc = activatedAtUtc,
                        StartBoundaryUtc = activatedAtUtc,
                        DueAtUtc = activatedAtUtc
                    };
                    context.ApprovedInboxPollStates.Add(poll);
                }
                poll.StartBoundaryUtc = activatedAtUtc;
                poll.Generation = 1;
                poll.LastCompletedAtUtc = new DateTimeOffset(
                    2031, 5, 6, 10, 20, 0, TimeSpan.Zero);
                poll.LastFailureCode = "graph_unavailable";
                await context.SaveChangesAsync();
            }

            await scope.ServiceProvider.GetRequiredService<IApprovedMailboxSubscriptionStore>().SaveAsync(
                new(
                    mailboxId,
                    "subscription-id",
                    "users/mailbox-id/mailFolders/inbox-id/messages",
                    expiresAtUtc,
                    ApprovedMailboxSubscriptionLifecycleState.Missed,
                    activatedAtUtc.AddHours(6),
                    "graph_subscription_renew_failed",
                    1),
                null,
                CancellationToken.None);
        }

        var page = await GetPageAsync(client);

        // Office time (Europe/London) for both instants: BST is UTC+1.
        Assert.Contains("<td>27 Aug 2026 11:20</td>", page, StringComparison.Ordinal);
        Assert.Contains(
            "<td>Missed. Expires 02 Sep 2026 10:05. Last failure: Graph subscription renew failed.</td>",
            page,
            StringComparison.Ordinal);
        Assert.DoesNotContain("subscription-id", page, StringComparison.Ordinal);
        Assert.DoesNotContain("mailFolders", page, StringComparison.Ordinal);
        Assert.Contains("<dt>Start boundary</dt><dd>27 Aug 2026 11:20</dd>", page, StringComparison.Ordinal);
        Assert.Contains("<dt>Generation</dt><dd>1</dd>", page, StringComparison.Ordinal);
        Assert.Contains("<dt>Last success</dt><dd>06 May 2031 11:20</dd>", page, StringComparison.Ordinal);
        Assert.Contains("<dt>Last error</dt><dd>graph_unavailable</dd>", page, StringComparison.Ordinal);
        var health = await client.GetStringAsync("/Administration/Health");
        Assert.Contains("Last successful poll:", health, StringComparison.Ordinal);
        Assert.Contains("<td>graph_unavailable</td>", health, StringComparison.Ordinal);
        Assert.DoesNotContain("<th>Latest evidence</th>", health, StringComparison.Ordinal);
        Assert.Contains("<dt>Freshness</dt><dd>Fresh</dd>", page, StringComparison.Ordinal);
        Assert.Contains("<dt>Subscription expiry</dt><dd>02 Sep 2026 10:05</dd>", page, StringComparison.Ordinal);
        Assert.Contains(
            "<dt>Capabilities</dt><dd>New instructions and Triage mail (Inbox)</dd>",
            page,
            StringComparison.Ordinal);
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
            ["MailboxForm.MailboxId"] = mailboxId,
            ["MailboxForm.ExpectedVersion"] = "0",
            ["MailboxForm.OperationKey"] = OperationKey(page),
            ["MailboxForm.Address"] = NewAddress,
            ["MailboxForm.SelectedRouteScopes"] = "InboundIntake",
            ["MailboxForm.SelectedState"] = "Approved",
            ["MailboxForm.VerifiedEncodedMessageSizeLimit"] = "10485760",
            ["MailboxForm.Reason"] = "Add the second approved mailbox",
            ["__RequestVerificationToken"] = AntiforgeryToken(page)
        });
        Assert.Equal(HttpStatusCode.Found, created.StatusCode);

        var configured = await GetPageAsync(client);
        Assert.Contains("?handler=ResolveFolders", configured, StringComparison.Ordinal);
        Assert.Contains(">Review folders (1 of 13)</summary>", configured, StringComparison.Ordinal);
        AssertFolderBinding(configured, "Instructions", "Configured");
        AssertFolderBinding(configured, "Billing", "Not configured");
        Assert.DoesNotContain("instructions-id", configured, StringComparison.Ordinal);
        var operationKeys = OperationKeyTagRegex().Matches(configured);
        var refreshed = await client.PostAsync(
            "/Administration/Mailboxes?handler=ResolveFolders",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["MailboxForm.MailboxId"] = mailboxId,
                ["MailboxForm.ExpectedVersion"] = "1",
                ["MailboxForm.OperationKey"] = Value(operationKeys[^2].Value),
                ["MailboxForm.Address"] = NewAddress,
                ["MailboxForm.SelectedState"] = "Approved",
                ["MailboxForm.Reason"] = "Refresh approved logical folder bindings",
                ["__RequestVerificationToken"] = AntiforgeryToken(configured)
            }));

        Assert.Equal(HttpStatusCode.Found, refreshed.StatusCode);
        var reloaded = await GetPageAsync(client);
        AssertFolderBinding(reloaded, "Instructions", "Not configured");
        AssertFolderBinding(reloaded, "Billing", "Configured");
        Assert.DoesNotContain("billing-id", reloaded, StringComparison.Ordinal);
        Assert.Contains("value=\"10485760\"", reloaded, StringComparison.Ordinal);
    }

    /// <summary>
    /// Asserts that one logical folder is paired with one binding state in the
    /// per-mailbox disclosure. The page renders every folder in
    /// <c>MailLogicalFolders.All</c> unconditionally, so two independent
    /// substring checks on a &lt;dt&gt; and a &lt;dd&gt; cannot tell a bound
    /// folder from an unbound one; only the contiguous pair can.
    /// </summary>
    private static void AssertFolderBinding(string html, string folderLabel, string state) =>
        Assert.Contains(
            $"<dt>{folderLabel}</dt><dd>{state}</dd>",
            BetweenTagsWhitespaceRegex().Replace(html, "><"),
            StringComparison.Ordinal);

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
        : IResolveApprovedMailboxIdentity, ICheckApprovedMailboxAccess
    {
        public Task<ApprovedMailboxIdentityResolution?> ResolveAsync(
            string address,
            CancellationToken cancellationToken) => Task.FromResult(resolution);

        public Task<bool> CanReadInboxAsync(
            ApprovedMailboxIdentityResolution mailbox,
            CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class SequencedResolver(params ApprovedMailboxIdentityResolution[] resolutions)
        : IResolveApprovedMailboxIdentity, ICheckApprovedMailboxAccess
    {
        private int _index;

        public Task<ApprovedMailboxIdentityResolution?> ResolveAsync(
            string address,
            CancellationToken cancellationToken) =>
            Task.FromResult<ApprovedMailboxIdentityResolution?>(
                resolutions[Math.Min(_index++, resolutions.Length - 1)]);

        public Task<bool> CanReadInboxAsync(
            ApprovedMailboxIdentityResolution mailbox,
            CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class AccessResolver(
        ApprovedMailboxIdentityResolution resolution,
        bool canRead) : IResolveApprovedMailboxIdentity, ICheckApprovedMailboxAccess
    {
        public int AccessChecks { get; private set; }

        public Task<ApprovedMailboxIdentityResolution?> ResolveAsync(
            string address,
            CancellationToken cancellationToken) => Task.FromResult<ApprovedMailboxIdentityResolution?>(resolution);

        public Task<bool> CanReadInboxAsync(
            ApprovedMailboxIdentityResolution mailbox,
            CancellationToken cancellationToken)
        {
            AccessChecks++;
            return Task.FromResult(canRead);
        }
    }

    private static ApprovedMailboxIdentityResolution Resolution(
        ApprovedMailboxFolderBinding binding) => new(
        "resolved-mailbox-id",
        "resolved-inbox-id",
        "resolved-sent-id",
        [binding]);

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex AntiforgeryTagRegex();

    [GeneratedRegex("<input[^>]*name=\"MailboxForm\\.MailboxId\"[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex NewMailboxIdTagRegex();

    [GeneratedRegex("<input[^>]*name=\"MailboxForm\\.OperationKey\"[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex OperationKeyTagRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex ValueRegex();

    [GeneratedRegex(@">\s+<")]
    private static partial Regex BetweenTagsWhitespaceRegex();
}
