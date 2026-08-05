using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Web.Authentication;
using Pegasus.Web.Mcp;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class AutomationMcpIngressTests
{
    private const string ClientId = "pegasus-automation";
    private const string ClientSecret = "integration-test-automation-secret-0123456789";
    private const string AllScopes =
        "automation.cases automation.intake automation.documents automation.assessment";

    private static readonly string[] ExpectedTools =
    [
        "pegasus_case_search",
        "pegasus_case_get",
        "pegasus_case_edit_begin",
        "pegasus_case_edit_end",
        "pegasus_case_update_details",
        "pegasus_intake_queue_list",
        "pegasus_intake_submit",
        "pegasus_document_add",
        "pegasus_document_download",
        "pegasus_document_export",
        "pegasus_assessment_get",
        "pegasus_assessment_update",
        "pegasus_eva_bundle_generate",
        "pegasus_eva_handoff_status"
    ];

    [Fact]
    public async Task GateOffExposesNoAutomationSurface()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var client = factory.CreateClient();

        // With the composition gate off the automation routes must be absent:
        // /mcp and /connect/token behave exactly like any unmapped route
        // (no challenge, no OAuth error, no MCP response).
        using var unmappedBaseline = await client.PostAsync(
            "/no-such-route",
            new StringContent("{}", Encoding.UTF8, "application/json"));
        using var mcpResponse = await client.PostAsync(
            "/mcp",
            new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(unmappedBaseline.StatusCode, mcpResponse.StatusCode);
        Assert.Empty(mcpResponse.Headers.WwwAuthenticate);

        using var mcpGetResponse = await client.GetAsync("/mcp");
        Assert.Equal(HttpStatusCode.NotFound, mcpGetResponse.StatusCode);

        using var tokenResponse = await client.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret
            }));
        Assert.Equal(unmappedBaseline.StatusCode, tokenResponse.StatusCode);
        Assert.Empty(await tokenResponse.Content.ReadAsStringAsync());

        using var metadataResponse = await client.GetAsync(
            "/.well-known/oauth-protected-resource/mcp");
        Assert.Equal(HttpStatusCode.NotFound, metadataResponse.StatusCode);
    }

    [Fact]
    public async Task IngressIsBearerOnlyWithDiscoveryAndTheApprovedToolInventory()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();

        // A staff browser identity is not a substitute for the Automation
        // actor: the DevelopmentOffline profile signs staff requests in
        // automatically, yet /mcp still refuses the call without a bearer
        // token and points at the RFC 9728 resource metadata.
        using (var unauthenticated = await PostMcpAsync(client, accessToken: null, ToolsListPayload(1)))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
            var challenge = string.Join(
                " ",
                unauthenticated.Headers.WwwAuthenticate.Select(value => value.ToString()));
            Assert.Contains("resource_metadata", challenge, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'automation_access_denied' AND Outcome = N'Denied'
            """));

        var metadata = await client.GetStringAsync("/.well-known/oauth-protected-resource/mcp");
        Assert.Contains("automation.cases", metadata, StringComparison.Ordinal);
        Assert.Contains("/mcp", metadata, StringComparison.Ordinal);

        var accessToken = await RequestTokenAsync(client, AllScopes);
        using var toolsResponse = await PostMcpAsync(client, accessToken, ToolsListPayload(2));
        Assert.Equal(HttpStatusCode.OK, toolsResponse.StatusCode);
        using var toolsDocument = await ReadJsonRpcAsync(toolsResponse);
        var toolNames = toolsDocument.RootElement
            .GetProperty("result")
            .GetProperty("tools")
            .EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString())
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            ExpectedTools.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            toolNames);
    }

    [Fact]
    public async Task ToolCallsAttributeHistoryAndEnforcePerAreaScopes()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();

        // Happy path: the intake queue list reaches Core and the invocation
        // is recorded in permanent history attributed to the Automation actor.
        var fullToken = await RequestTokenAsync(client, AllScopes);
        using (var response = await PostMcpAsync(
            client,
            fullToken,
            ToolCallPayload(3, "pegasus_intake_queue_list", new { page = 1, pageSize = 10 })))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = await ReadJsonRpcAsync(response);
            var result = document.RootElement.GetProperty("result");
            Assert.False(result.TryGetProperty("isError", out var isError) && isError.GetBoolean());
            var structured = result.GetProperty("structuredContent");
            Assert.Equal(0, structured.GetProperty("totalCount").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(
                structured.GetProperty("correlationId").GetString()));
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_intake_queue_list'
              AND Outcome = N'Succeeded'
              AND ActorSubjectId = N'pegasus-automation'
            """));

        // Validation failure: recorded as a failed Automation action, and the
        // refusal message is content-safe.
        using (var response = await PostMcpAsync(
            client,
            fullToken,
            ToolCallPayload(
                4,
                "pegasus_case_get",
                new { caseId = Guid.Empty })))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = await ReadJsonRpcAsync(response);
            Assert.Contains(
                "case identifier",
                document.RootElement.ToString(),
                StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_case_get'
              AND Outcome = N'Failed'
            """));

        // Out-of-scope: a token holding only automation.cases cannot invoke
        // an intake tool; the denial writes an attributable security event.
        var casesOnlyToken = await RequestTokenAsync(client, "automation.cases");
        using (var response = await PostMcpAsync(
            client,
            casesOnlyToken,
            ToolCallPayload(5, "pegasus_intake_queue_list", new { page = 1, pageSize = 10 })))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = await ReadJsonRpcAsync(response);
            Assert.Contains(
                "automation.intake",
                document.RootElement.ToString(),
                StringComparison.Ordinal);
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'automation_scope_denied'
              AND Outcome = N'Denied'
              AND SubjectId = N'pegasus-automation'
            """));
    }

    [Fact]
    public async Task AdministratorDisableTakesImmediateEffectAndIsRecorded()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();
        var administrator = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);

        var accessToken = await RequestTokenAsync(client, AllScopes);
        using (var response = await PostMcpAsync(
            client,
            accessToken,
            ToolCallPayload(6, "pegasus_intake_queue_list", new { page = 1, pageSize = 10 })))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // The Administrator kill switch: disabling the registration takes
        // immediate effect for the already-issued token and refuses new ones.
        using (var scope = mcpFactory.Services.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<AutomationClientRegistry>();
            var disabled = await registry.SetEnabledAsync(
                enabled: false,
                administrator,
                "Integration-test kill switch",
                Guid.NewGuid().ToString("N"),
                CancellationToken.None);
            Assert.False(disabled.IsEnabled);
        }

        using (var response = await PostMcpAsync(
            client,
            accessToken,
            ToolCallPayload(7, "pegasus_intake_queue_list", new { page = 1, pageSize = 10 })))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = await ReadJsonRpcAsync(response);
            Assert.Contains(
                "disabled",
                document.RootElement.ToString(),
                StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'automation_client_disabled'
              AND Outcome = N'Denied'
              AND SubjectId = N'pegasus-automation'
            """));

        using (var tokenResponse = await client.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["scope"] = AllScopes
            })))
        {
            Assert.False(tokenResponse.IsSuccessStatusCode);
            var body = await tokenResponse.Content.ReadAsStringAsync();
            Assert.Contains("unauthorized_client", body, StringComparison.Ordinal);
        }

        Assert.True(await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'automation_token_rejected' AND Outcome = N'Denied'
            """) >= 1);

        // Re-enabling restores the ingress, and both administrator actions
        // are permanent attributable history.
        using (var scope = mcpFactory.Services.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<AutomationClientRegistry>();
            var enabled = await registry.SetEnabledAsync(
                enabled: true,
                administrator,
                "Integration-test re-enable",
                Guid.NewGuid().ToString("N"),
                CancellationToken.None);
            Assert.True(enabled.IsEnabled);
        }

        var restoredToken = await RequestTokenAsync(client, AllScopes);
        using (var response = await PostMcpAsync(
            client,
            restoredToken,
            ToolCallPayload(8, "pegasus_intake_queue_list", new { page = 1, pageSize = 10 })))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = await ReadJsonRpcAsync(response);
            var result = document.RootElement.GetProperty("result");
            Assert.False(result.TryGetProperty("isError", out var isError) && isError.GetBoolean());
        }

        Assert.Equal(2, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'automation_client'
              AND ActorKind = N'Staff'
              AND ActorSubjectId = N'{DevelopmentOfflineIdentity.AdministratorId:D}'
              AND EventKind IN (N'automation_client_disabled', N'automation_client_enabled')
            """));
    }

    private static WebApplicationFactory<Program> WithAutomationMcp(
        IntakeWebApplicationFactory factory) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Features:AutomationMcp", "true");
            builder.UseSetting("AutomationMcp:ClientId", ClientId);
            builder.UseSetting("AutomationMcp:ClientSecret", ClientSecret);
            builder.UseSetting("AutomationMcp:PublicOrigin", "http://localhost/");
            builder.UseSetting("AutomationMcp:RegistrationCacheSeconds", "0");
        });

    private static async Task<string> RequestTokenAsync(HttpClient client, string scope)
    {
        using var response = await client.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["scope"] = scope
            }));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Token issuance failed with {(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("The token response is missing access_token.");
    }

    private static async Task<HttpResponseMessage> PostMcpAsync(
        HttpClient client,
        string? accessToken,
        string payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await client.SendAsync(request);
    }

    private static async Task<JsonDocument> ReadJsonRpcAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
        {
            var data = body
                .Split('\n')
                .Select(line => line.TrimEnd('\r'))
                .Where(line => line.StartsWith("data:", StringComparison.Ordinal))
                .Select(line => line[5..].Trim())
                .First(line => line.Length > 0);
            return JsonDocument.Parse(data);
        }

        return JsonDocument.Parse(body);
    }

    private static string ToolsListPayload(int id) =>
        JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            method = "tools/list"
        });

    private static string ToolCallPayload(int id, string tool, object arguments) =>
        JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            method = "tools/call",
            @params = new
            {
                name = tool,
                arguments
            }
        });
}
