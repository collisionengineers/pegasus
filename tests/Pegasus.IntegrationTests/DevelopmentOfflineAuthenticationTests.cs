using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

public sealed partial class DevelopmentOfflineAuthenticationTests
{
    private const string ClientId = "pegasus-development-mcp";
    private const string RedirectUri = "http://127.0.0.1:7890/callback";
    private const string Issuer = "https://localhost:7139/";
    private const string Resource = "https://localhost:7139/mcp";

    [Fact]
    public async Task InitializationCreatesOnePasswordlessEnabledAdministratorAndIsIdempotent()
    {
        using var factory = new IntakeWebApplicationFactory();
        _ = factory.Services;
        var before = await ReadInitializationStateAsync(factory.DatabasePath);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await DevelopmentOfflineInitialization.InitializeAsync(scope.ServiceProvider);

            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<PegasusIdentityUser>>();
            var user = await userManager.FindByIdAsync(
                DevelopmentOfflineIdentity.AdministratorId.ToString("D"));
            Assert.NotNull(user);
            Assert.Equal(DevelopmentOfflineIdentity.UserName, user.UserName);
            Assert.True(user.IsEnabled);
            Assert.False(user.MustChangePassword);
            Assert.Null(user.PasswordHash);
            Assert.Equal(
                [StaffRoleNames.Administrator],
                await userManager.GetRolesAsync(user));

            var applicationManager = scope.ServiceProvider
                .GetRequiredService<IOpenIddictApplicationManager>();
            Assert.NotNull(await applicationManager.FindByClientIdAsync(ClientId));
        }

        var after = await ReadInitializationStateAsync(factory.DatabasePath);
        Assert.Equal(before, after);
        Assert.Equal(1L, after.Users);
        Assert.Equal(3L, after.Roles);
        Assert.Equal(1L, after.AdministratorAssignments);
        Assert.Equal(1L, after.Clients);
        Assert.Equal(1L, after.ClientRegistrationEvents);
    }

    [Fact]
    public async Task DisabledPersistedOfflineAdministratorIsNotAuthenticated()
    {
        using var factory = new IntakeWebApplicationFactory();
        _ = factory.Services;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<PegasusIdentityUser>>();
            var user = await userManager.FindByIdAsync(
                DevelopmentOfflineIdentity.AdministratorId.ToString("D"));
            Assert.NotNull(user);
            user.IsEnabled = false;
            Assert.True((await userManager.UpdateAsync(user)).Succeeded);
        }

        using var client = IntakeWebDriver.CreateClient(factory);
        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OfflineIdentityWithoutPersistedAdministratorRoleIsNotAuthenticated()
    {
        using var factory = new IntakeWebApplicationFactory();
        _ = factory.Services;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<PegasusIdentityUser>>();
            var user = await userManager.FindByIdAsync(
                DevelopmentOfflineIdentity.AdministratorId.ToString("D"));
            Assert.NotNull(user);
            Assert.True((
                await userManager.RemoveFromRoleAsync(
                    user,
                    StaffRoleNames.Administrator)).Succeeded);
        }

        using var client = IntakeWebDriver.CreateClient(factory);
        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FreshInitializedDatabaseCompletesExplicitConsentAndAuthorizationCodePkce()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var verifier = new string('a', 64);
        var challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var authorizationUri = QueryHelpers.AddQueryString(
            "/connect/authorize",
            new Dictionary<string, string?>
            {
                ["client_id"] = ClientId,
                ["redirect_uri"] = RedirectUri,
                ["response_type"] = OpenIddictConstants.ResponseTypes.Code,
                ["scope"] = "pegasus.mcp.read",
                ["code_challenge"] = challenge,
                ["code_challenge_method"] = OpenIddictConstants.CodeChallengeMethods.Sha256,
                ["resource"] = Resource,
                ["state"] = "persisted-offline-subject"
            });

        using var consentResponse = await client.GetAsync(authorizationUri);
        var consentHtml = await consentResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, consentResponse.StatusCode);
        Assert.Contains("Pegasus Development MCP client", consentHtml, StringComparison.Ordinal);
        Assert.Contains(Resource, consentHtml, StringComparison.Ordinal);
        var approvalForm = ReadHiddenFormValues(consentHtml);

        approvalForm.Add(KeyValuePair.Create("decision", "approve"));
        using var approvalResponse = await client.PostAsync(
            "/connect/authorize",
            new FormUrlEncodedContent(approvalForm));
        Assert.True(
            approvalResponse.StatusCode == HttpStatusCode.Redirect,
            $"Expected consent redirect, received {(int)approvalResponse.StatusCode}; fields: {string.Join(", ", approvalForm.Select(field => field.Key))}; body: {await approvalResponse.Content.ReadAsStringAsync()}");
        var callback = Assert.IsType<Uri>(approvalResponse.Headers.Location);
        Assert.Equal(new Uri(RedirectUri).GetLeftPart(UriPartial.Path), callback.GetLeftPart(UriPartial.Path));
        var callbackQuery = QueryHelpers.ParseQuery(callback.Query);
        Assert.Equal("persisted-offline-subject", Assert.Single(callbackQuery["state"]));
        var code = Assert.Single(callbackQuery["code"]);
        Assert.False(string.IsNullOrWhiteSpace(code));

        using var tokenResponse = await client.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = OpenIddictConstants.GrantTypes.AuthorizationCode,
                ["client_id"] = ClientId,
                ["redirect_uri"] = RedirectUri,
                ["code"] = code!,
                ["code_verifier"] = verifier,
                ["resource"] = Resource
            }));
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        using var tokenDocument = JsonDocument.Parse(tokenJson);
        var accessToken = tokenDocument.RootElement.GetProperty("access_token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        Assert.Equal(
            "Bearer",
            tokenDocument.RootElement.GetProperty("token_type").GetString(),
            ignoreCase: true);

        Assert.Equal(
            1L,
            await CountAsync(
                factory.DatabasePath,
                "SELECT COUNT(*) FROM OpenIddictAuthorizations " +
                "WHERE LOWER(Subject) = 'd47fbbae-ea22-4ca6-b983-01e2ed1fbd13';"));

        using var mcpRequest = CreateMcpInitializeRequest(accessToken!);
        using var mcpResponse = await client.SendAsync(mcpRequest);
        var mcpBody = await mcpResponse.Content.ReadAsStringAsync();
        Assert.True(
            mcpResponse.IsSuccessStatusCode,
            $"The persisted offline subject's token was rejected by /mcp: {(int)mcpResponse.StatusCode} {mcpBody}");

        await AssertProtectedResourceMetadataAsync(
            client,
            "/.well-known/oauth-protected-resource");
        await AssertProtectedResourceMetadataAsync(
            client,
            "/.well-known/oauth-protected-resource/mcp");
    }

    [Fact]
    public async Task ProductionRefusesDevelopmentOfflineInitializationBeforeDatabaseAccess()
    {
        var workingDirectory = Path.Combine(
            Path.GetTempPath(),
            "Pegasus.IntegrationTests",
            Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(workingDirectory, "must-not-be-created.db");
        try
        {
            using var factory = new ConfiguredWebApplicationFactory(
                "Production",
                new Dictionary<string, string?>
                {
                    ["Runtime:Profile"] = "Production",
                    ["Database:Provider"] = "Sqlite",
                    ["Database:LocalPath"] = databasePath,
                    ["Features:LocalIntake"] = "false",
                    ["Features:LocalDocumentCustody"] = "false"
                });
            await using var scope = factory.Services.CreateAsyncScope();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => DevelopmentOfflineInitialization.InitializeAsync(scope.ServiceProvider));

            Assert.Contains("local test-fixture operation", exception.Message, StringComparison.Ordinal);
            Assert.False(File.Exists(databasePath));
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    private static HttpRequestMessage CreateMcpInitializeRequest(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new
                {
                    protocolVersion = "2025-11-25",
                    capabilities = new { },
                    clientInfo = new { name = "Pegasus integration tests", version = "1" }
                }
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return request;
    }

    private static async Task AssertProtectedResourceMetadataAsync(
        HttpClient client,
        string path)
    {
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        using var metadata = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(Resource, metadata.RootElement.GetProperty("resource").GetString());
        Assert.Equal(
            Issuer,
            Assert.Single(metadata.RootElement.GetProperty("authorization_servers").EnumerateArray())
                .GetString());
    }

    private static List<KeyValuePair<string, string>> ReadHiddenFormValues(string html)
    {
        var form = ConsentFormRegex().Match(html);
        Assert.True(form.Success, "The consent form must be present.");

        var fields = new List<KeyValuePair<string, string>>();
        foreach (Match tag in HiddenInputTagRegex().Matches(form.Groups["content"].Value))
        {
            var name = InputNameRegex().Match(tag.Value);
            if (!name.Success)
            {
                continue;
            }

            var value = InputValueRegex().Match(tag.Value);
            fields.Add(KeyValuePair.Create(
                WebUtility.HtmlDecode(name.Groups["value"].Value),
                value.Success
                    ? WebUtility.HtmlDecode(value.Groups["value"].Value)
                    : string.Empty));
        }

        Assert.True(
            fields.Any(field =>
                field.Key.Equals(
                    "__RequestVerificationToken",
                    StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(field.Value)),
            "The consent form must render an antiforgery token.");
        return fields;
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static async Task<InitializationState> ReadInitializationStateAsync(string databasePath)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(
            new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly
            }.ToString());
        await connection.OpenAsync();
        return new(
            await CountAsync(connection, "SELECT COUNT(*) FROM AspNetUsers;"),
            await CountAsync(connection, "SELECT COUNT(*) FROM AspNetRoles;"),
            await CountAsync(
                connection,
                "SELECT COUNT(*) FROM AspNetUserRoles ur " +
                "INNER JOIN AspNetUsers u ON u.Id = ur.UserId " +
                "INNER JOIN AspNetRoles r ON r.Id = ur.RoleId " +
                "WHERE u.NormalizedUserName = 'DEVELOPMENT-OFFLINE-ADMINISTRATOR' " +
                "AND r.NormalizedName = 'ADMINISTRATOR';"),
            await CountAsync(connection, "SELECT COUNT(*) FROM OpenIddictApplications;"),
            await CountAsync(
                connection,
                "SELECT COUNT(*) FROM SecurityEvents " +
                "WHERE ReasonCode = 'development_mcp_client_registered';"));
    }

    private static async Task<long> CountAsync(string databasePath, string commandText)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(
            new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly
            }.ToString());
        await connection.OpenAsync();
        return await CountAsync(connection, commandText);
    }

    private static async Task<long> CountAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        return (long)(await command.ExecuteScalarAsync())!;
    }

    [GeneratedRegex("<form[^>]*id=\"staff-mcp-consent\"[^>]*>(?<content>.*?)</form>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex ConsentFormRegex();

    [GeneratedRegex("<input[^>]*type=\"hidden\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HiddenInputTagRegex();

    [GeneratedRegex("\\sname=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InputNameRegex();

    [GeneratedRegex("\\svalue=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InputValueRegex();

    private sealed record InitializationState(
        long Users,
        long Roles,
        long AdministratorAssignments,
        long Clients,
        long ClientRegistrationEvents);
}
