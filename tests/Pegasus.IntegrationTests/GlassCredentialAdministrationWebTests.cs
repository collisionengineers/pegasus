using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The per-staff-account Glass repair-estimate credential dialog on the
/// Accounts page, deep-linked with <c>?glassStaffId=</c>.
/// Administrator-only, write-only about the secret, and version-checked on
/// every write.
/// </summary>
/// <remarks>
/// The administration port is substituted by a recording fake rather than the
/// EF store: this suite proves what the page forwards and what it renders, and
/// the store's own behaviour is proved where the store lives. The fixture
/// password below is an obviously synthetic string and is never written into an
/// assertion message.
/// </remarks>
[Trait("Category", "SqlServer")]
public sealed partial class GlassCredentialAdministrationWebTests
{
    /// <summary>The seeded staff account every case in this suite administers.</summary>
    private static readonly Guid StaffId = DevelopmentOfflineIdentity.AdministratorId;

    private static readonly Guid UnknownStaffId =
        Guid.Parse("00000000-0000-4000-8000-00000000c047");

    private const string Route = "/Administration/Accounts";
    private const string FixtureUsername = "glass-fixture-account";
    private const string FixturePassword = "glass-fixture-value-not-a-secret";

    private static string PageFor(Guid staffId) => $"{Route}?glassStaffId={staffId:D}";

    [Theory]
    [InlineData("Engineer")]
    [InlineData("User")]
    public async Task NonAdministratorIsRefusedTheGlassCredentialPage(string role)
    {
        var store = new RecordingCredentialAdministration();
        using var factory = new IntakeWebApplicationFactory(
            useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory, store);
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);

        using (var read = await client.GetAsync(PageFor(StaffId)))
        {
            Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        }

        using (var written = await client.PostAsync(
            $"{Route}?handler=SaveGlass",
            new FormUrlEncodedContent(new Dictionary<string, string>())))
        {
            Assert.Equal(HttpStatusCode.Forbidden, written.StatusCode);
        }

        Assert.Empty(store.Replaced);
        Assert.Empty(store.Cleared);
    }

    /// <summary>
    /// Labels, values and controls: the account, the external account name and
    /// the version the store holds — and no field, hint or panel that could
    /// carry the secret back to the browser.
    /// </summary>
    [Fact]
    public async Task TheCredentialDialogStatesTheStoredAccountNameAndNeverTheSecret()
    {
        var store = new RecordingCredentialAdministration
        {
            Status = Configured(version: 4, generation: 2)
        };
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory, store);

        var html = await GetHtmlAsync(client, PageFor(StaffId));

        Assert.Equal(FixtureUsername, FactValue(html, "Username"));
        Assert.Equal("2", FactValue(html, "Generation"));
        Assert.Equal("4", FactValue(html, "Version"));
        Assert.Equal("Enabled", ChipText(html, "glass-credential-title"));
        Assert.Contains("type=\"password\"", html, StringComparison.Ordinal);
        Assert.Contains("autocomplete=\"new-password\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"__RequestVerificationToken\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain(FixturePassword, html, StringComparison.Ordinal);
        // The credential mutation is directly available with both current
        // expected versions and no edit-scope token.
        Assert.DoesNotContain("name=\"Reason\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"ExpectedVersion\"", FormOf(html, "SaveGlass"), StringComparison.Ordinal);
        Assert.Contains("name=\"ExpectedStaffAccountVersion\"", FormOf(html, "SaveGlass"), StringComparison.Ordinal);
        Assert.DoesNotContain("EditLeaseToken", html, StringComparison.Ordinal);
        // The credential dialog has no hint sentence or empty-state panel.
        // The shared Add dialog can explain the instruction-upload route.
        var dialog = DialogOf(html);
        Assert.DoesNotContain("<p>", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("field-hint", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("empty-state", dialog, StringComparison.Ordinal);
        // The dialog opens over the Accounts area itself: the deep link is a
        // query of this page, the credential dialog is the one page-owned
        // dialog that auto-opens, and the settings dialog is not open beside
        // it. (A pending shell "What's new" note may still open above; that is
        // the shell's designed stack, not a second page-owned dialog.)
        Assert.Contains("data-dialog=\"glass-credential-dialog\" data-dialog-open-on-load=\"true\"", html, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(DialogOf(html), Regex.Escape("data-dialog-open-on-load=\"true\"")));
        Assert.DoesNotContain("settings-", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// An unconfigured credential offers the save form and nothing to clear.
    /// </summary>
    [Fact]
    public async Task AnUnconfiguredCredentialStatesItsStateAndOffersOnlyTheSave()
    {
        var store = new RecordingCredentialAdministration();
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory, store);

        var html = await GetHtmlAsync(client, PageFor(StaffId));

        Assert.Equal("Not configured", ChipText(html, "glass-credential-title"));
        Assert.Contains("handler=SaveGlass\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=ClearGlass\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SavingForwardsTheAccountNameSecretAndExpectedVersionToTheStore()
    {
        var store = new RecordingCredentialAdministration
        {
            Status = Configured(version: 7, generation: 1)
        };
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory, store);
        var html = await GetHtmlAsync(client, PageFor(StaffId));
        var save = FormOf(html, "SaveGlass");

        using (var response = await client.PostAsync(
            $"{Route}?handler=SaveGlass",
            Form(
                html,
                ("staffId", StaffId.ToString("D")),
                ("ExpectedVersion", InputValue(save, "ExpectedVersion")),
                ("ExpectedStaffAccountVersion", InputValue(save, "ExpectedStaffAccountVersion")),
                ("username", FixtureUsername),
                ("password", FixturePassword))))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(PageFor(StaffId), response.Headers.Location?.OriginalString);
        }

        var call = Assert.Single(store.Replaced);
        Assert.Equal(StaffId, call.PegasusUserId);
        Assert.Equal(ExternalCredentialProvider.GlassRepairEstimate, call.Provider);
        Assert.Equal(7, call.ExpectedVersion);
        Assert.Equal(FixtureUsername, call.Username);
        Assert.True(call.PasswordMatchesFixture);
        Assert.True(call.Enabled);

        // The confirmation the operator lands on carries the message, not the
        // material the post supplied.
        var confirmed = await GetHtmlAsync(client, PageFor(StaffId));
        Assert.Contains("The credential was saved.", confirmed, StringComparison.Ordinal);
        Assert.DoesNotContain(FixturePassword, confirmed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdministratorCanSaveAGlassCredentialForAUser()
    {
        var store = new RecordingCredentialAdministration
        {
            Status = Configured(version: 0, generation: 0)
        };
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory, store);
        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IStaffAccountQueries>();
        var administrator = (await queries.ListAsync(0, ListStaffAccounts.MaximumPageSize, default))
            .Accounts.Single(item => item.UserName == DevelopmentOfflineIdentity.UserName);
        var user = await scope.ServiceProvider.GetRequiredService<ICreateStaffAccount>()
            .ExecuteAsync(
                new(
                    ActionActor.Staff(administrator.Id, [administrator.Role]),
                    "glass-credential-user",
                    "Tempor4ryPassword!",
                    Guid.NewGuid().ToString("D")),
                default);
        Assert.Equal(StaffRole.User, user.Account.Role);

        var html = await GetHtmlAsync(client, PageFor(user.Account.Id));
        var save = FormOf(html, "SaveGlass");
        using var response = await client.PostAsync(
            $"{Route}?handler=SaveGlass",
            Form(
                html,
                ("staffId", user.Account.Id.ToString("D")),
                ("ExpectedVersion", InputValue(save, "ExpectedVersion")),
                ("ExpectedStaffAccountVersion", InputValue(save, "ExpectedStaffAccountVersion")),
                ("username", FixtureUsername),
                ("password", FixturePassword)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(user.Account.Id, Assert.Single(store.Replaced).PegasusUserId);
    }

    /// <summary>
    /// The list behind the dialogs shows its first page only. An account
    /// beyond that page still opens the credential dialog from its deep link,
    /// a save still lands back on that dialog, and the dialog's own exit still
    /// opens the account's settings: each dialog resolves its account directly
    /// rather than finding it among the rendered rows.
    /// </summary>
    [Fact]
    public async Task AnAccountBeyondTheFirstListPageStillOpensItsDialogs()
    {
        var store = new RecordingCredentialAdministration
        {
            Status = Configured(version: 0, generation: 0)
        };
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory, store);
        // Sorted before the seeded administrator, these fill the first page
        // and leave the administrator as the 101st account.
        await SeedUserAccountsAsync(factory, ListStaffAccounts.MaximumPageSize);

        var html = await GetHtmlAsync(client, PageFor(StaffId));
        // Razor encodes the plus sign, so the meta is read decoded.
        Assert.Contains($"{ListStaffAccounts.MaximumPageSize}+ accounts", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
        Assert.DoesNotContain($"glassStaffId={StaffId:D}", html, StringComparison.Ordinal);
        var save = FormOf(DialogOf(html), "SaveGlass");
        var accountVersion = InputValue(save, "ExpectedStaffAccountVersion");

        using (var response = await client.PostAsync(
            $"{Route}?handler=SaveGlass",
            Form(
                html,
                ("staffId", StaffId.ToString("D")),
                ("ExpectedVersion", InputValue(save, "ExpectedVersion")),
                ("ExpectedStaffAccountVersion", accountVersion),
                ("username", FixtureUsername),
                ("password", FixturePassword))))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(PageFor(StaffId), response.Headers.Location?.ToString(), ignoreCase: true);
        }
        Assert.Equal(StaffId, Assert.Single(store.Replaced).PegasusUserId);
        DialogOf(await GetHtmlAsync(client, PageFor(StaffId)));

        var settings = await GetHtmlAsync(
            client,
            $"{Route}?editStaffId={StaffId:D}&expectedVersion={accountVersion}");
        Assert.Equal(StaffId.ToString("D"), InputValue(FormOf(settings, "Settings"), "staffId"));
    }

    [Fact]
    public async Task ClearingForwardsTheExpectedVersionToTheStore()
    {
        var store = new RecordingCredentialAdministration
        {
            Status = Configured(version: 3, generation: 1)
        };
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory, store);
        var html = await GetHtmlAsync(client, PageFor(StaffId));
        var clear = FormOf(html, "ClearGlass");

        using (var response = await client.PostAsync(
            $"{Route}?handler=ClearGlass",
            Form(
                html,
                ("staffId", StaffId.ToString("D")),
                ("ExpectedVersion", InputValue(clear, "ExpectedVersion")),
                ("ExpectedStaffAccountVersion", InputValue(clear, "ExpectedStaffAccountVersion")))))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var call = Assert.Single(store.Cleared);
        Assert.Equal(StaffId, call.PegasusUserId);
        Assert.Equal(ExternalCredentialProvider.GlassRepairEstimate, call.Provider);
        Assert.Equal(3, call.ExpectedVersion);
        Assert.Empty(store.Replaced);
    }

    /// <summary>
    /// A save the page itself refuses says so, keeps the account name the
    /// operator typed, writes nothing — and leaves the secret field empty.
    /// </summary>
    [Fact]
    public async Task ARefusedSaveReportsKeepsTheAccountNameAndWritesNothing()
    {
        var store = new RecordingCredentialAdministration
        {
            Status = Configured(version: 2, generation: 1)
        };
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory, store);
        var html = await GetHtmlAsync(client, PageFor(StaffId));
        var save = FormOf(html, "SaveGlass");

        using var response = await client.PostAsync(
            $"{Route}?handler=SaveGlass",
            Form(
                html,
                ("staffId", StaffId.ToString("D")),
                ("ExpectedVersion", "4"),
                ("ExpectedStaffAccountVersion", InputValue(save, "ExpectedStaffAccountVersion")),
                ("username", FixtureUsername),
                ("password", "   ")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Enter a password.", body, StringComparison.Ordinal);
        Assert.Contains($"value=\"{FixtureUsername}\"", body, StringComparison.Ordinal);
        // The password field is redisplayed with no value at all, so nothing
        // the post carried can come back through it.
        Assert.DoesNotContain("value=", PasswordField(body), StringComparison.Ordinal);
        Assert.Empty(store.Replaced);
    }

    /// <summary>
    /// The store's own refusal — a version the credential has moved past,
    /// which the store raises as EF Core's concurrency exception — reaches the
    /// operator as a refusal, not as a silent success, and the page it lands
    /// on is a fresh read of what the store holds now.
    /// </summary>
    [Fact]
    public async Task AStaleVersionIsReportedReloadedAndWritesNothing()
    {
        var store = new RecordingCredentialAdministration
        {
            Status = Configured(version: 5, generation: 1),
            Refusal = new DbUpdateConcurrencyException(
                "The credential is at another version than the one this write expected.")
        };
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory, store);
        var html = await GetHtmlAsync(client, PageFor(StaffId));
        var save = FormOf(html, "SaveGlass");

        using var response = await client.PostAsync(
            $"{Route}?handler=SaveGlass",
            Form(
                html,
                ("staffId", StaffId.ToString("D")),
                ("ExpectedVersion", InputValue(save, "ExpectedVersion")),
                ("username", FixtureUsername),
                ("password", FixturePassword)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(
            "The staff account or Glass's credential changed. Reload this page before trying again.",
            WebUtility.HtmlDecode(body),
            StringComparison.Ordinal);
        Assert.DoesNotContain(FixturePassword, body, StringComparison.Ordinal);
        Assert.Empty(store.Replaced);
        // The status was reloaded, but the failed form keeps the stale version
        // that was submitted until the operator reloads the page explicitly.
        Assert.Equal(2, store.Reads);
        Assert.Equal("5", InputValue(FormOf(body, "SaveGlass"), "ExpectedVersion"));
    }

    /// <summary>
    /// The page over the registered credential store, not a fake: replace
    /// the credential, see the store's own version and username back, post
    /// the version the page offered before the replace and be refused with a
    /// reload, then clear. This runs only where the host composes Stream A's
    /// store; on a branch that does not register the administration port the
    /// host does not build.
    /// </summary>
    [Fact]
    public async Task TheRegisteredStoreReplacesRefusesAStaleVersionAndClears()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });
        var initial = await GetHtmlAsync(client, PageFor(StaffId));
        var initialSave = FormOf(initial, "SaveGlass");
        var initialVersion = InputValue(initialSave, "ExpectedVersion");
        Assert.Contains(">Not configured<", initial, StringComparison.Ordinal);

        using (var replaced = await client.PostAsync(
            $"{Route}?handler=SaveGlass",
            Form(
                initial,
                ("staffId", StaffId.ToString("D")),
                ("ExpectedVersion", initialVersion),
                ("ExpectedStaffAccountVersion", InputValue(initialSave, "ExpectedStaffAccountVersion")),
                ("username", FixtureUsername),
                ("password", FixturePassword))))
        {
            Assert.Equal(HttpStatusCode.Redirect, replaced.StatusCode);
            Assert.Equal(PageFor(StaffId), replaced.Headers.Location?.OriginalString);
        }

        var configured = await GetHtmlAsync(client, PageFor(StaffId));
        Assert.Equal(FixtureUsername, FactValue(configured, "Username"));
        Assert.DoesNotContain(FixturePassword, configured, StringComparison.Ordinal);
        var configuredSave = FormOf(configured, "SaveGlass");
        var currentVersion = InputValue(configuredSave, "ExpectedVersion");
        Assert.NotEqual(initialVersion, currentVersion);

        // The version the page offered before the replace is stale now: the
        // registered store refuses it, and the page reloads with the version
        // it holds rather than writing the second account name.
        string staleBody;
        using (var stale = await client.PostAsync(
            $"{Route}?handler=SaveGlass",
            Form(
                configured,
                ("staffId", StaffId.ToString("D")),
                ("ExpectedVersion", initialVersion),
                ("ExpectedStaffAccountVersion", InputValue(configuredSave, "ExpectedStaffAccountVersion")),
                ("username", FixtureUsername + "-replaced"),
                ("password", FixturePassword))))
        {
            Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
            staleBody = await stale.Content.ReadAsStringAsync();
            Assert.Contains(
                "The staff account or Glass's credential changed. Reload this page before trying again.",
                WebUtility.HtmlDecode(staleBody),
                StringComparison.Ordinal);
            Assert.Equal(initialVersion, InputValue(FormOf(staleBody, "SaveGlass"), "ExpectedVersion"));
            Assert.DoesNotContain(FixturePassword, staleBody, StringComparison.Ordinal);
        }
        var unchanged = await GetHtmlAsync(client, PageFor(StaffId));
        Assert.Equal(FixtureUsername, FactValue(unchanged, "Username"));

        using (var cleared = await client.PostAsync(
            $"{Route}?handler=ClearGlass",
            Form(
                staleBody,
                ("staffId", StaffId.ToString("D")),
                ("ExpectedVersion", currentVersion),
                ("ExpectedStaffAccountVersion", InputValue(FormOf(staleBody, "ClearGlass"), "ExpectedStaffAccountVersion")))))
        {
            Assert.Equal(HttpStatusCode.Redirect, cleared.StatusCode);
        }
        var afterClear = await GetHtmlAsync(client, PageFor(StaffId));
        Assert.Contains(">Not configured<", afterClear, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=ClearGlass\"", afterClear, StringComparison.Ordinal);
        Assert.DoesNotContain(FixtureUsername, afterClear, StringComparison.Ordinal);
    }

    /// <summary>
    /// Only the store's named refusals become an operator message. A failure
    /// the page cannot interpret propagates and surfaces as a server error,
    /// because reporting it as a refusal would invite a retry of something
    /// that did not refuse.
    /// </summary>
    [Fact]
    public async Task AnUnrelatedFailureIsNotSwallowed()
    {
        var store = new RecordingCredentialAdministration
        {
            Status = Configured(version: 5, generation: 1),
            Refusal = new InvalidOperationException("the credential store is unreachable")
        };
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory, store);
        var html = await GetHtmlAsync(client, PageFor(StaffId));
        var save = FormOf(html, "SaveGlass");

        // The host's error handling turns an unhandled exception into a server
        // error response rather than a refusal notice or a redirect: the page
        // neither caught it nor reported it as something to retry.
        using var response = await client.PostAsync(
            $"{Route}?handler=SaveGlass",
            Form(
                html,
                ("staffId", StaffId.ToString("D")),
                ("ExpectedVersion", InputValue(save, "ExpectedVersion")),
                ("username", FixtureUsername),
                ("password", FixturePassword)));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(store.Replaced);
    }

    /// <summary>
    /// The credential is published at exactly one surface: the Accounts page's
    /// <c>?glassStaffId=</c> deep link. The retired standalone
    /// <c>/Administration/Glass</c> routes are not addressable.
    /// </summary>
    [Fact]
    public async Task TheCredentialIsPublishedOnlyOnTheAccountsDeepLink()
    {
        var store = new RecordingCredentialAdministration();
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory, store);

        using (var scoped = await client.GetAsync(PageFor(StaffId)))
        {
            Assert.Equal(HttpStatusCode.OK, scoped.StatusCode);
        }

        foreach (var absent in new[]
                 {
                     "/Administration/Glass",
                     "/Administration/Glass/Index",
                     "/Administration/Glass/not-a-guid"
                 })
        {
            using var response = await client.GetAsync(absent);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task AnUnknownStaffIdIsNotFound()
    {
        var store = new RecordingCredentialAdministration();
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory, store);

        using var response = await client.GetAsync(PageFor(UnknownStaffId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Enabled User-role accounts written straight to the store, one row and
    /// one role each, with no password to hash.
    /// </summary>
    private static async Task SeedUserAccountsAsync(IntakeWebApplicationFactory factory, int count)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var role = await context.Roles.SingleAsync(item => item.Name == StaffRoleNames.User);
        for (var index = 0; index < count; index++)
        {
            var userName = $"bulk-{index:D3}";
            var user = new PegasusIdentityUser
            {
                Id = Guid.NewGuid(),
                UserName = userName,
                NormalizedUserName = userName.ToUpperInvariant(),
                IsEnabled = true,
                MustChangePassword = false,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            };
            context.Users.Add(user);
            context.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = role.Id });
        }
        await context.SaveChangesAsync();
    }

    private static PerUserExternalCredentialStatus Configured(long version, long generation) =>
        new(
            StaffId,
            ExternalCredentialProvider.GlassRepairEstimate,
            Configured: true,
            Enabled: true,
            FixtureUsername,
            generation,
            version,
            new DateTimeOffset(2026, 9, 1, 9, 30, 0, TimeSpan.Zero));

    /// <summary>
    /// Records what the page forwards. The submitted secret is compared against
    /// the fixture and kept as a verdict, never re-exposed as a value.
    /// </summary>
    private sealed class RecordingCredentialAdministration : IPerUserExternalCredentialAdministration
    {
        public List<ReplaceCall> Replaced { get; } = [];

        public List<ClearCall> Cleared { get; } = [];

        public PerUserExternalCredentialStatus Status { get; set; } =
            new(
                StaffId,
                ExternalCredentialProvider.GlassRepairEstimate,
                Configured: false,
                Enabled: false,
                Username: null,
                CredentialGeneration: 0,
                Version: 0,
                UpdatedAtUtc: null);

        /// <summary>The exception the next write throws, when the case sets one.</summary>
        public Exception? Refusal { get; set; }

        /// <summary>How many times the page has read the stored status.</summary>
        public int Reads { get; private set; }

        public Task<PerUserExternalCredentialStatus> GetAsync(
            ActionActor actor,
            Guid pegasusUserId,
            ExternalCredentialProvider provider,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(actor);
            Reads++;
            return Task.FromResult(Status with
            {
                PegasusUserId = pegasusUserId,
                Provider = provider
            });
        }

        public Task<IReadOnlyDictionary<Guid, PerUserExternalCredentialStatus>> GetManyAsync(
            ActionActor actor,
            IReadOnlyCollection<Guid> pegasusUserIds,
            ExternalCredentialProvider provider,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(actor);
            var statuses = pegasusUserIds.Distinct().ToDictionary(
                userId => userId,
                userId => Status with { PegasusUserId = userId, Provider = provider });
            return Task.FromResult<IReadOnlyDictionary<Guid, PerUserExternalCredentialStatus>>(statuses);
        }

        public Task<PerUserExternalCredentialStatus> ReplaceAsync(
            ActionActor actor,
            Guid pegasusUserId,
            ExternalCredentialProvider provider,
            long expectedCredentialVersion,
            long expectedStaffAccountVersion,
            string username,
            string password,
            bool enabled,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(actor);
            if (Refusal is not null)
            {
                throw Refusal;
            }

            Replaced.Add(new(
                pegasusUserId,
                provider,
                expectedCredentialVersion,
                expectedStaffAccountVersion,
                username,
                string.Equals(password, FixturePassword, StringComparison.Ordinal),
                enabled));
            Status = Status with
            {
                Configured = true,
                Enabled = enabled,
                Username = username,
                CredentialGeneration = Status.CredentialGeneration + 1,
                Version = expectedCredentialVersion + 1
            };
            return Task.FromResult(Status);
        }

        public Task ClearAsync(
            ActionActor actor,
            Guid pegasusUserId,
            ExternalCredentialProvider provider,
            long expectedCredentialVersion,
            long expectedStaffAccountVersion,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(actor);
            if (Refusal is not null)
            {
                throw Refusal;
            }

            Cleared.Add(new(
                pegasusUserId,
                provider,
                expectedCredentialVersion,
                expectedStaffAccountVersion));
            Status = Status with
            {
                Configured = false,
                Enabled = false,
                Username = null,
                Version = expectedCredentialVersion + 1
            };
            return Task.CompletedTask;
        }
    }

    private sealed record ReplaceCall(
        Guid PegasusUserId,
        ExternalCredentialProvider Provider,
        long ExpectedVersion,
        long ExpectedStaffAccountVersion,
        string Username,
        bool PasswordMatchesFixture,
        bool Enabled);

    private sealed record ClearCall(
        Guid PegasusUserId,
        ExternalCredentialProvider Provider,
        long ExpectedVersion,
        long ExpectedStaffAccountVersion);

    private static HttpClient CreateClient(
        IntakeWebApplicationFactory factory,
        IPerUserExternalCredentialAdministration administration) =>
        factory
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.AddSingleton(administration)))
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost:7139")
            });

    private static async Task<string> GetHtmlAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>One handler's own form, so a field is read from the form that owns it.</summary>
    private static string FormOf(string html, string handler)
    {
        var start = html.IndexOf($"handler={handler}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"The page must render the '{handler}' form.");
        return html[start..html.IndexOf("</form>", start, StringComparison.Ordinal)];
    }

    /// <summary>
    /// The rendered credential dialog, so its content is read apart from the
    /// Accounts page around it. The dialog is the last body content in
    /// <c>main</c>.
    /// </summary>
    private static string DialogOf(string html)
    {
        var start = html.IndexOf("data-dialog=\"glass-credential-dialog\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The page must render the Glass's credential dialog.");
        var end = html.IndexOf("</main>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The credential dialog must close within the page body.");
        return html[start..end];
    }

    private static FormUrlEncodedContent Form(
        string html,
        params (string Name, string Value)[] values)
    {
        var fields = values.ToDictionary(
            item => item.Name,
            item => item.Value,
            StringComparer.Ordinal);
        fields["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken");
        return new(fields);
    }

    /// <summary>The value of one definition cell, by its label.</summary>
    private static string FactValue(string html, string label)
    {
        var match = Regex.Match(
            html,
            $"<dt>{Regex.Escape(label)}</dt>\\s*<dd[^>]*>(?<value>[^<]*)</dd>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"The page must render the '{label}' cell.");
        return WebUtility.HtmlDecode(match.Groups["value"].Value).Trim();
    }

    /// <summary>The state chip beside one panel heading.</summary>
    private static string ChipText(string html, string headingId)
    {
        var match = Regex.Match(
            html,
            $"id=\"{Regex.Escape(headingId)}\"[^>]*>[^<]*</h2>\\s*<span class=\"status[^\"]*\">(?<value>[^<]*)</span>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"The panel '{headingId}' must render a state chip.");
        return WebUtility.HtmlDecode(match.Groups["value"].Value).Trim();
    }

    /// <summary>The rendered secret field, so its attributes can be read.</summary>
    private static string PasswordField(string html)
    {
        var tag = Regex.Match(
            html,
            "<input[^>]*name=\"password\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(tag.Success, "The save form must render the password field.");
        return tag.Value;
    }

    private static string InputValue(string html, string name)
    {
        var tag = Regex.Match(
            html,
            $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(tag.Success, $"The form must render '{name}'.");
        var value = ValueRegex().Match(tag.Value);
        Assert.True(value.Success, $"The field '{name}' must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    [GeneratedRegex("value=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ValueRegex();
}
