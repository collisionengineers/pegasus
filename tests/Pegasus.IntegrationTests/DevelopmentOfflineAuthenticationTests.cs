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
using Pegasus.Web.Mcp;

namespace Pegasus.IntegrationTests;

[Collection(LocalDbFixtureDefinition.Name)]
[Trait("Category", "SqlServer")]
public sealed partial class DevelopmentOfflineAuthenticationTests
{
    private const string ClientId = "pegasus-development-mcp";
    private const string RedirectUri = "http://127.0.0.1:7890/callback";
    private const string Issuer = "https://localhost:7139/";
    private const string Resource = "https://localhost:7139/mcp";
    private const string McpProtocolVersion = "2025-11-25";

    [Fact]
    public async Task InitializationCreatesOnePasswordlessEnabledAdministratorAndIsIdempotent()
    {
        using var factory = new IntakeWebApplicationFactory();
        _ = factory.Services;
        var before = await ReadInitializationStateAsync(factory.Database);

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

        var after = await ReadInitializationStateAsync(factory.Database);
        Assert.Equal(before, after);
        Assert.Equal(1L, after.Users);
        Assert.Equal(3L, after.Roles);
        Assert.Equal(1L, after.AdministratorAssignments);
        Assert.Equal(1L, after.Clients);
        Assert.Equal(1L, after.ClientRegistrationEvents);
        Assert.Equal(1L, after.QdosOrganizations);
        Assert.Equal(1L, after.ActiveQdosPrincipals);
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
    public async Task AuthorizationServerAdvertisesOnlySha256Pkce()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/.well-known/openid-configuration");
        response.EnsureSuccessStatusCode();
        using var metadata = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            OpenIddictConstants.CodeChallengeMethods.Sha256,
            Assert.Single(
                metadata.RootElement
                    .GetProperty("code_challenge_methods_supported")
                    .EnumerateArray())
                .GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(OpenIddictConstants.CodeChallengeMethods.Plain)]
    [InlineData("S512")]
    public async Task AuthorizationRejectsPkceThatIsNotSha256BeforeConsent(string? method)
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var parameters = new Dictionary<string, string?>
        {
            ["client_id"] = ClientId,
            ["redirect_uri"] = RedirectUri,
            ["response_type"] = OpenIddictConstants.ResponseTypes.Code,
            ["scope"] = StaffMcpClientContract.ReadScope,
            ["code_challenge"] = new string('a', 64),
            ["resource"] = Resource,
            ["state"] = "rejected-pkce"
        };
        if (method is not null)
        {
            parameters["code_challenge_method"] = method;
        }

        using var response = await client.GetAsync(
            QueryHelpers.AddQueryString("/connect/authorize", parameters));
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("id=\"staff-mcp-consent\"", responseBody, StringComparison.Ordinal);
        if (response.Headers.Location is { } callback)
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var callbackQuery = QueryHelpers.ParseQuery(callback.Query);
            Assert.Equal(OpenIddictConstants.Errors.InvalidRequest, Assert.Single(callbackQuery["error"]));
            Assert.False(callbackQuery.ContainsKey("code"));
        }
        else
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(
                $"error:{OpenIddictConstants.Errors.InvalidRequest}",
                responseBody,
                StringComparison.Ordinal);
            Assert.DoesNotContain("\ncode:", responseBody, StringComparison.Ordinal);
        }

        Assert.Equal(
            0L,
            await CountAsync(
                factory.Database,
                "SELECT COUNT(*) FROM OpenIddictAuthorizations;"));
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
                ["scope"] = StaffMcpClientContract.ReadScope,
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
                factory.Database,
                "SELECT COUNT(*) FROM OpenIddictAuthorizations " +
                "WHERE LOWER(Subject) = 'd47fbbae-ea22-4ca6-b983-01e2ed1fbd13';"));

        using var mcpRequest = CreateMcpInitializeRequest(accessToken!);
        using var mcpResponse = await client.SendAsync(mcpRequest);
        var mcpBody = await mcpResponse.Content.ReadAsStringAsync();
        Assert.True(
            mcpResponse.IsSuccessStatusCode,
            $"The persisted offline subject's token was rejected by /mcp: {(int)mcpResponse.StatusCode} {mcpBody}");
        using var initializeDocument = ParseMcpResponse(mcpBody);
        Assert.False(
            initializeDocument.RootElement.TryGetProperty("error", out _),
            $"Authenticated MCP initialization returned a JSON-RPC error: {mcpBody}");
        var negotiatedProtocolVersion = Assert.IsType<string>(
            initializeDocument.RootElement
                .GetProperty("result")
                .GetProperty("protocolVersion")
                .GetString());
        Assert.True(
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "MCP-Protocol-Version",
                negotiatedProtocolVersion));
        using var initializedRequest = CreateMcpInitializedNotification(accessToken!);
        using var initializedResponse = await client.SendAsync(initializedRequest);
        Assert.Equal(HttpStatusCode.Accepted, initializedResponse.StatusCode);

        using var listRequest = CreateMcpRequest(
            accessToken!,
            requestId: 2,
            method: "tools/list",
            parameters: new { });
        using var listResponse = await client.SendAsync(listRequest);
        var listBody = await listResponse.Content.ReadAsStringAsync();
        Assert.True(
            listResponse.IsSuccessStatusCode,
            $"Authenticated MCP discovery failed: {(int)listResponse.StatusCode} {listBody}");
        using var listDocument = ParseMcpResponse(listBody);
        var discovered = listDocument.RootElement
            .GetProperty("result")
            .GetProperty("tools")
            .EnumerateArray()
            .ToArray();
        var discoveredNames = discovered
            .Select(tool => Assert.IsType<string>(tool.GetProperty("name").GetString()))
            .ToArray();
        Assert.Equal(AlphaMcpToolManifest.Tools.Length, discovered.Length);
        Assert.Equal(
            discoveredNames.Length,
            discoveredNames.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            AlphaMcpToolManifest.Tools
                .Select(tool => tool.Name)
                .Order(StringComparer.Ordinal),
            discoveredNames.Order(StringComparer.Ordinal));

        var expectedByName = AlphaMcpToolManifest.Tools.ToDictionary(
            tool => tool.Name,
            StringComparer.Ordinal);
        foreach (var descriptor in discovered)
        {
            var name = Assert.IsType<string>(descriptor.GetProperty("name").GetString());
            var expected = expectedByName[name].Hints;
            var annotations = descriptor.GetProperty("annotations");
            Assert.Equal(expected.ReadOnly, annotations.GetProperty("readOnlyHint").GetBoolean());
            Assert.Equal(expected.Destructive, annotations.GetProperty("destructiveHint").GetBoolean());
            Assert.Equal(expected.Idempotent, annotations.GetProperty("idempotentHint").GetBoolean());
            Assert.Equal(expected.OpenWorld, annotations.GetProperty("openWorldHint").GetBoolean());
            Assert.Equal(JsonValueKind.Object, descriptor.GetProperty("inputSchema").ValueKind);
            Assert.Equal(JsonValueKind.Object, descriptor.GetProperty("outputSchema").ValueKind);
        }

        using var operationsRequest = CreateMcpRequest(
            accessToken!,
            requestId: 3,
            method: "tools/call",
            parameters: new
            {
                name = AlphaMcpToolNames.OperationsGet,
                arguments = new { }
            });
        using var operationsResponse = await client.SendAsync(operationsRequest);
        var operationsBody = await operationsResponse.Content.ReadAsStringAsync();
        Assert.True(
            operationsResponse.IsSuccessStatusCode,
            $"Authenticated operations.get failed: {(int)operationsResponse.StatusCode} {operationsBody}");
        using var operationsDocument = ParseMcpResponse(operationsBody);
        var operationsResult = operationsDocument.RootElement.GetProperty("result");
        if (operationsResult.TryGetProperty("isError", out var operationsError))
        {
            Assert.False(operationsError.GetBoolean());
        }
        Assert.Equal(
            JsonValueKind.Object,
            operationsResult.GetProperty("structuredContent").ValueKind);

        var leaseOperationsBefore = await CountAsync(
            factory.Database,
            "SELECT COUNT(*) FROM CaseEditLeaseOperations;");
        for (var attempt = 0; attempt < StaffMcpToolRateLimiter.MutationPermitLimit; attempt++)
        {
            using var deniedMutationRequest = CreateMcpRequest(
                accessToken!,
                requestId: 100 + attempt,
                method: "tools/call",
                parameters: new
                {
                    name = AlphaMcpToolNames.CasesAcquireEditLease,
                    arguments = new
                    {
                        caseId = Guid.NewGuid(),
                        expectedVersion = 1,
                        operationId = Guid.NewGuid()
                    }
                });
            using var deniedMutationResponse = await client.SendAsync(deniedMutationRequest);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, deniedMutationResponse.StatusCode);
        }

        using (var rateLimitedMutationRequest = CreateMcpRequest(
            accessToken!,
            requestId: 120,
            method: "tools/call",
            parameters: new
            {
                name = AlphaMcpToolNames.CasesAcquireEditLease,
                arguments = new
                {
                    caseId = Guid.NewGuid(),
                    expectedVersion = 1,
                    operationId = Guid.NewGuid()
                }
            }))
        using (var rateLimitedMutationResponse = await client.SendAsync(rateLimitedMutationRequest))
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, rateLimitedMutationResponse.StatusCode);
            Assert.Equal(
                "60",
                Assert.Single(rateLimitedMutationResponse.Headers.GetValues("Retry-After")));
        }
        Assert.Equal(
            leaseOperationsBefore,
            await CountAsync(factory.Database, "SELECT COUNT(*) FROM CaseEditLeaseOperations;"));

        for (var attempt = 1; attempt < StaffMcpToolRateLimiter.ReadPermitLimit; attempt++)
        {
            using var permittedReadRequest = CreateMcpRequest(
                accessToken!,
                requestId: 200 + attempt,
                method: "tools/call",
                parameters: new
                {
                    name = AlphaMcpToolNames.OperationsGet,
                    arguments = new { }
                });
            using var permittedReadResponse = await client.SendAsync(permittedReadRequest);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, permittedReadResponse.StatusCode);
        }

        using (var rateLimitedReadRequest = CreateMcpRequest(
            accessToken!,
            requestId: 260,
            method: "tools/call",
            parameters: new
            {
                name = AlphaMcpToolNames.OperationsGet,
                arguments = new { }
            }))
        using (var rateLimitedReadResponse = await client.SendAsync(rateLimitedReadRequest))
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, rateLimitedReadResponse.StatusCode);
            Assert.Equal(
                "60",
                Assert.Single(rateLimitedReadResponse.Headers.GetValues("Retry-After")));
        }
        Assert.Equal(
            1L,
            await CountAsync(
                factory.Database,
                "SELECT COUNT(*) FROM SecurityEvents " +
                "WHERE Type = 'RateLimited' AND ReasonCode = 'mcp_read_rate_limited';"));
        Assert.Equal(
            1L,
            await CountAsync(
                factory.Database,
                "SELECT COUNT(*) FROM SecurityEvents " +
                "WHERE Type = 'RateLimited' AND ReasonCode = 'mcp_mutation_rate_limited';"));

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
        using var factory = new ConfiguredWebApplicationFactory(
            "Production",
            new Dictionary<string, string?>
            {
                ["Runtime:Profile"] = "Production",
                ["ConnectionStrings:Pegasus"] =
                    "Server=127.0.0.1,1;Database=Pegasus_MustNotBeAccessed;" +
                    "Integrated Security=true;Encrypt=false;Connect Timeout=1",
                ["Features:LocalIntake"] = "false",
                ["Features:LocalDocumentCustody"] = "false"
            });
        await using var scope = factory.Services.CreateAsyncScope();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => DevelopmentOfflineInitialization.InitializeAsync(scope.ServiceProvider));

        Assert.Contains("local test-fixture operation", exception.Message, StringComparison.Ordinal);
    }

    private static HttpRequestMessage CreateMcpInitializeRequest(string accessToken) =>
        CreateMcpRequest(
            accessToken,
            requestId: 1,
            method: "initialize",
            parameters: new
            {
                protocolVersion = McpProtocolVersion,
                capabilities = new { },
                clientInfo = new { name = "Pegasus integration tests", version = "1" }
            });

    private static HttpRequestMessage CreateMcpInitializedNotification(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                method = "notifications/initialized"
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return request;
    }

    private static HttpRequestMessage CreateMcpRequest(
        string accessToken,
        int requestId,
        string method,
        object parameters)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = requestId,
                method,
                @params = parameters
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return request;
    }

    private static JsonDocument ParseMcpResponse(string response)
    {
        const string DataPrefix = "data: ";
        if (response.TrimStart().StartsWith('{'))
        {
            return JsonDocument.Parse(response);
        }

        var data = response
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Last(line => line.StartsWith(DataPrefix, StringComparison.Ordinal));
        return JsonDocument.Parse(data[DataPrefix.Length..]);
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

    private static async Task<InitializationState> ReadInitializationStateAsync(
        LocalDbTestDatabase database) =>
        new(
            await CountAsync(database, "SELECT COUNT(*) FROM AspNetUsers;"),
            await CountAsync(database, "SELECT COUNT(*) FROM AspNetRoles;"),
            await CountAsync(
                database,
                "SELECT COUNT(*) FROM AspNetUserRoles ur " +
                "INNER JOIN AspNetUsers u ON u.Id = ur.UserId " +
                "INNER JOIN AspNetRoles r ON r.Id = ur.RoleId " +
                "WHERE u.NormalizedUserName = 'DEVELOPMENT-OFFLINE-ADMINISTRATOR' " +
                "AND r.NormalizedName = 'ADMINISTRATOR';"),
            await CountAsync(database, "SELECT COUNT(*) FROM OpenIddictApplications;"),
            await CountAsync(
                database,
                "SELECT COUNT(*) FROM SecurityEvents " +
                "WHERE ReasonCode = 'development_mcp_client_registered';"),
            await CountAsync(
                database,
                "SELECT COUNT(*) FROM Organizations WHERE Name = 'QDOS development fixture';"),
            await CountAsync(
                database,
                "SELECT COUNT(*) FROM Principals WHERE Code = 'QDOS' AND IsActive = 1;"));

    private static Task<long> CountAsync(
        LocalDbTestDatabase database,
        string commandText) =>
        database.ScalarAsync<long>(commandText);

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
        long ClientRegistrationEvents,
        long QdosOrganizations,
        long ActiveQdosPrincipals);
}
