using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// CASE-047 B04: the per-Engineer Glass repair-estimate credential page.
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

    private const string Route = "/Administration/Glass";
    private const string FixtureUsername = "glass-fixture-account";
    private const string FixturePassword = "glass-fixture-value-not-a-secret";

    private static string PageFor(Guid staffId) => $"{Route}/{staffId:D}";

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
            $"{PageFor(StaffId)}?handler=Save",
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
    public async Task TheCredentialPageStatesTheStoredAccountNameAndNeverTheSecret()
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
        // No inert required control: the administration port takes neither a
        // reason nor an operation key, so neither is asked for.
        Assert.DoesNotContain("name=\"Reason\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("OperationKey", html, StringComparison.Ordinal);
        // No hint sentence, no how-it-works copy, no empty-state panel.
        Assert.DoesNotContain("<p>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("field-hint", html, StringComparison.Ordinal);
        Assert.DoesNotContain("empty-state", html, StringComparison.Ordinal);
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
        Assert.Contains("handler=Save", html, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=Clear", html, StringComparison.Ordinal);
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
        var save = FormOf(html, "Save");

        using (var response = await client.PostAsync(
            $"{PageFor(StaffId)}?handler=Save",
            Form(
                html,
                ("ExpectedVersion", InputValue(save, "ExpectedVersion")),
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
    public async Task ClearingForwardsTheExpectedVersionToTheStore()
    {
        var store = new RecordingCredentialAdministration
        {
            Status = Configured(version: 3, generation: 1)
        };
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory, store);
        var html = await GetHtmlAsync(client, PageFor(StaffId));
        var clear = FormOf(html, "Clear");

        using (var response = await client.PostAsync(
            $"{PageFor(StaffId)}?handler=Clear",
            Form(
                html,
                ("ExpectedVersion", InputValue(clear, "ExpectedVersion")))))
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
        var save = FormOf(html, "Save");

        using var response = await client.PostAsync(
            $"{PageFor(StaffId)}?handler=Save",
            Form(
                html,
                ("ExpectedVersion", InputValue(save, "ExpectedVersion")),
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
        var save = FormOf(html, "Save");

        using var response = await client.PostAsync(
            $"{PageFor(StaffId)}?handler=Save",
            Form(
                html,
                ("ExpectedVersion", InputValue(save, "ExpectedVersion")),
                ("username", FixtureUsername),
                ("password", FixturePassword)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(
            "The credential changed after this page was loaded.",
            body,
            StringComparison.Ordinal);
        Assert.DoesNotContain(FixturePassword, body, StringComparison.Ordinal);
        Assert.Empty(store.Replaced);
        // The refusal reloads: the status was read again, and the version the
        // refused page offers is the one the store holds now.
        Assert.Equal(2, store.Reads);
        Assert.Equal("5", InputValue(FormOf(body, "Save"), "ExpectedVersion"));
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
        var save = FormOf(html, "Save");

        // The host's error handling turns an unhandled exception into a server
        // error response rather than a refusal notice or a redirect: the page
        // neither caught it nor reported it as something to retry.
        using var response = await client.PostAsync(
            $"{PageFor(StaffId)}?handler=Save",
            Form(
                html,
                ("ExpectedVersion", InputValue(save, "ExpectedVersion")),
                ("username", FixtureUsername),
                ("password", FixturePassword)));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(store.Replaced);
    }

    /// <summary>
    /// The credential page is published at exactly one staff-scoped route: the
    /// conventional page paths are not addressable.
    /// </summary>
    [Fact]
    public async Task TheCredentialPageIsPublishedOnlyAtTheStaffScopedRoute()
    {
        var store = new RecordingCredentialAdministration();
        using var factory = new IntakeWebApplicationFactory();
        using var client = CreateClient(factory, store);

        using (var scoped = await client.GetAsync(PageFor(StaffId)))
        {
            Assert.Equal(HttpStatusCode.OK, scoped.StatusCode);
        }

        foreach (var absent in new[] { Route, $"{Route}/Index", $"{Route}/not-a-guid" })
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

        public Task<PerUserExternalCredentialStatus> ReplaceAsync(
            ActionActor actor,
            Guid pegasusUserId,
            ExternalCredentialProvider provider,
            long expectedVersion,
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
                expectedVersion,
                username,
                string.Equals(password, FixturePassword, StringComparison.Ordinal),
                enabled));
            Status = Status with
            {
                Configured = true,
                Enabled = enabled,
                Username = username,
                CredentialGeneration = Status.CredentialGeneration + 1,
                Version = expectedVersion + 1
            };
            return Task.FromResult(Status);
        }

        public Task ClearAsync(
            ActionActor actor,
            Guid pegasusUserId,
            ExternalCredentialProvider provider,
            long expectedVersion,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(actor);
            if (Refusal is not null)
            {
                throw Refusal;
            }

            Cleared.Add(new(pegasusUserId, provider, expectedVersion));
            Status = Status with
            {
                Configured = false,
                Enabled = false,
                Username = null,
                Version = expectedVersion + 1
            };
            return Task.CompletedTask;
        }
    }

    private sealed record ReplaceCall(
        Guid PegasusUserId,
        ExternalCredentialProvider Provider,
        long ExpectedVersion,
        string Username,
        bool PasswordMatchesFixture,
        bool Enabled);

    private sealed record ClearCall(
        Guid PegasusUserId,
        ExternalCredentialProvider Provider,
        long ExpectedVersion);

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
