using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Actors;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Web.Authentication;
using Pegasus.Web.Mcp;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class AutomationMcpIngressTests
{
    // The approved inventory, 69 tools on the one server (ADR-0087): the
    // edit-lease tools once, the shared reads once, sorted as a list is read.
    private static readonly string[] ExpectedTools =
    [
        "pegasus_ai_job_complete_market_research",
        "pegasus_ai_job_create",
        "pegasus_ai_job_list",
        "pegasus_ai_job_transition",
        "pegasus_assessment_get",
        "pegasus_assessment_update",
        "pegasus_case_action",
        "pegasus_case_create",
        "pegasus_case_get",
        "pegasus_case_note_add",
        "pegasus_case_query",
        "pegasus_case_search",
        "pegasus_case_update_details",
        "pegasus_directory_search",
        "pegasus_document_action",
        "pegasus_document_add",
        "pegasus_document_download",
        "pegasus_document_download_batch",
        "pegasus_edit_begin",
        "pegasus_edit_end",
        "pegasus_edit_renew",
        "pegasus_estimate_act",
        "pegasus_estimate_get",
        "pegasus_estimate_import",
        "pegasus_estimate_list",
        "pegasus_estimate_save",
        "pegasus_image_preparation_get",
        "pegasus_image_prepare",
        "pegasus_intake_action",
        "pegasus_intake_get",
        "pegasus_intake_queue_list",
        "pegasus_intake_submit",
        "pegasus_mail_action",
        "pegasus_mail_correct_classification",
        "pegasus_mail_get",
        "pegasus_mail_link_case",
        "pegasus_mail_link_destinations",
        "pegasus_mail_list",
        "pegasus_mail_open_query",
        "pegasus_mail_send",
        "pegasus_report_action",
        "pegasus_report_list",
        "pegasus_report_send",
        "pegasus_report_wording_get",
        "pegasus_report_wording_save",
        "pegasus_triage_assign",
        "pegasus_triage_await_information",
        "pegasus_triage_cancel",
        "pegasus_triage_case_link",
        "pegasus_triage_complete",
        "pegasus_triage_get",
        "pegasus_triage_list",
        "pegasus_triage_note_add",
        "pegasus_triage_record_finding",
        "pegasus_triage_reopen",
        "pegasus_triage_response_evidence",
        "pegasus_triage_source_download",
        "pegasus_unidentified_get",
        "pegasus_unidentified_list",
        "pegasus_unidentified_reopen",
        "pegasus_unidentified_resolve",
        "pegasus_unidentified_source_download",
        "pegasus_upload_begin",
        "pegasus_upload_part",
        "pegasus_valuation_get",
        "pegasus_valuation_list",
        "pegasus_valuation_save",
        "pegasus_vocabulary_get",
        "pegasus_work_centre_dismiss"
    ];

    [Fact]
    public async Task GateOffExposesNoAutomationSurface()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var client = factory.CreateClient();

        // With the composition gate off the automation routes must be absent:
        // the server path and /connect/token behave exactly like any unmapped
        // route (no challenge, no OAuth error, no MCP response).
        using var unmappedBaseline = await client.PostAsync(
            "/no-such-route",
            new StringContent("{}", Encoding.UTF8, "application/json"));
        using var mcpResponse = await client.PostAsync(
            McpServer,
            new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(unmappedBaseline.StatusCode, mcpResponse.StatusCode);
        Assert.Empty(mcpResponse.Headers.WwwAuthenticate);

        using var mcpGetResponse = await client.GetAsync(McpServer);
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
        // automatically, yet the server still refuses the call without a
        // bearer token and points at its RFC 9728 resource metadata.
        using (var unauthenticated = await PostMcpAsync(client, McpServer, accessToken: null, ToolsListPayload(1)))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
            Assert.Equal(
                "http://localhost/.well-known/oauth-protected-resource/mcp",
                ResourceMetadataUri(unauthenticated));
        }

        Assert.Equal(1, await (await factory.DatabaseAsync).ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'automation_access_denied' AND Outcome = N'Denied'
            """));

        // A content route's 401 names the same document.
        using (var content = await client.GetAsync(
            $"/automation/documents/{Guid.NewGuid():D}/versions/{Guid.NewGuid():D}"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, content.StatusCode);
            Assert.Equal(
                "http://localhost/.well-known/oauth-protected-resource/mcp",
                ResourceMetadataUri(content));
        }

        // The one document names the server's resource and lists every scope
        // (ADR-0087 decision 2).
        using (var metadata = JsonDocument.Parse(
            await client.GetStringAsync("/.well-known/oauth-protected-resource/mcp")))
        {
            Assert.Equal("http://localhost/mcp", metadata.RootElement.GetProperty("resource").GetString());
            Assert.Equal(
                AutomationMcp.Scopes,
                metadata.RootElement.GetProperty("scopes_supported").EnumerateArray()
                    .Select(scope => scope.GetString()!)
                    .ToArray());
        }

        // A token holding all seven scopes lists the whole inventory.
        var accessToken = await RequestTokenAsync(client, string.Join(' ', AutomationMcp.Scopes));
        var rpcId = 2;
        using var toolsResponse = await PostMcpAsync(client, McpServer, accessToken, ToolsListPayload(rpcId));
        Assert.Equal(HttpStatusCode.OK, toolsResponse.StatusCode);
        Assert.Equal(ExpectedTools, await ToolNamesAsync(toolsResponse));
        using var toolsDocument = await ReadJsonRpcAsync(toolsResponse);
        var updateDetails = toolsDocument.RootElement
            .GetProperty("result")
            .GetProperty("tools")
            .EnumerateArray()
            .Single(tool => tool.GetProperty("name").GetString() == "pegasus_case_update_details");
        var properties = updateDetails.GetProperty("inputSchema").GetProperty("properties");
        Assert.True(properties.TryGetProperty("inspectionDate", out _));
        Assert.False(properties.TryGetProperty("instructionDate", out _));

        // R185: market research carries its retail value only, so the tool
        // takes no trade value; its arguments are exactly these. It is listed
        // (ADR-0087), and the findings document comes as
        // contentBase64 or a staged uploadId (R203).
        using var jobsResponse = await PostMcpAsync(client, McpServer, accessToken, ToolsListPayload(rpcId + 1));
        using var jobsDocument = await ReadJsonRpcAsync(jobsResponse);
        var marketResearch = jobsDocument.RootElement
            .GetProperty("result")
            .GetProperty("tools")
            .EnumerateArray()
            .Single(tool => tool.GetProperty("name").GetString() == "pegasus_ai_job_complete_market_research");
        Assert.Equal(
            [
                "caseId", "contentBase64", "expectedJobVersion", "fileName", "guideMonth", "jobId",
                "mediaType", "mileage", "operationKey", "recordedDate", "recordedTime", "retailValue",
                "uploadId",
            ],
            marketResearch.GetProperty("inputSchema").GetProperty("properties")
                .EnumerateObject()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public async Task ToolsListHoldsOnlyTheScopesTheTokenCarries()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();
        var mailToken = await RequestTokenAsync(client, AutomationMcp.MailScope);
        string[] mailTools =
        [
            "pegasus_mail_action",
            "pegasus_mail_correct_classification",
            "pegasus_mail_get",
            "pegasus_mail_link_case",
            "pegasus_mail_link_destinations",
            "pegasus_mail_list",
            "pegasus_mail_open_query"
        ];

        // The second list is served from the one built for the scope set.
        foreach (var rpcId in new[] { 1, 2 })
        {
            using var response = await PostMcpAsync(client, McpServer, mailToken, ToolsListPayload(rpcId));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = await ReadJsonRpcAsync(response);
            var result = document.RootElement.GetProperty("result");
            Assert.Equal(
                mailTools,
                result.GetProperty("tools").EnumerateArray()
                    .Select(tool => tool.GetProperty("name").GetString())
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray());
            Assert.Equal("private", result.GetProperty("cacheScope").GetString());
        }
    }

    [Fact]
    public async Task TheServerNamesItselfWithItsInstructions()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();
        var accessToken = await RequestTokenAsync(client, string.Join(' ', AutomationMcp.Scopes));

        // One server at /mcp (ADR-0087 decision 1): initialize names it and
        // carries the one instructions text. A client attaching it spends
        // four requests of the 120 a minute: initialize, the initialized
        // notification, tools/list and the stream GET (a 405).
        using (var initialize = await PostMcpAsync(client, McpServer, accessToken, InitializePayload(1)))
        {
            Assert.Equal(HttpStatusCode.OK, initialize.StatusCode);
            using var document = await ReadJsonRpcAsync(initialize);
            var result = document.RootElement.GetProperty("result");
            Assert.Equal(AutomationMcp.ServerName, result.GetProperty("serverInfo").GetProperty("name").GetString());
            Assert.Equal(AutomationMcp.Instructions, result.GetProperty("instructions").GetString());
        }

        using (var initialized = await PostMcpAsync(
            client,
            McpServer,
            accessToken,
            JsonSerializer.Serialize(new { jsonrpc = "2.0", method = "notifications/initialized" })))
        {
            Assert.Equal(HttpStatusCode.Accepted, initialized.StatusCode);
        }

        using (var list = await PostMcpAsync(client, McpServer, accessToken, ToolsListPayload(2)))
        {
            Assert.Equal(ExpectedTools, await ToolNamesAsync(list));
        }

        using var stream = new HttpRequestMessage(HttpMethod.Get, McpServer);
        stream.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var streamResponse = await client.SendAsync(stream);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, streamResponse.StatusCode);
    }

    [Fact]
    public async Task ABearerGetOnMcpAnswers405InsteadOfTheStaffSignInRedirect()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = CreateCookielessClient(mcpFactory);
        var accessToken = await RequestTokenAsync(client, AutomationMcp.MailScope);

        // The stream GET an MCP client sends after initialize, repeated past
        // the staff sign-in limit as the classifier's reconnect loop did, and
        // a DELETE: the stateless endpoint offers neither, so each is a 405.
        HttpMethod[] methods =
        [
            .. Enumerable.Repeat(HttpMethod.Get, StaffSessionPolicy.SignInAttemptsPerClientPerMinute + 1),
            HttpMethod.Delete
        ];
        foreach (var method in methods)
        {
            using var request = new HttpRequestMessage(method, McpServer);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
            Assert.Equal("POST", Assert.Single(response.Content.Headers.Allow));
        }
    }

    [Fact]
    public async Task AnUnauthenticatedGetOnMcpIsChallengedForTheBearerNotRedirected()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = CreateCookielessClient(mcpFactory);

        // The same challenge and transport-denial row as an unauthenticated
        // POST, not the staff sign-in redirect.
        using var response = await client.GetAsync(McpServer);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            "http://localhost/.well-known/oauth-protected-resource/mcp",
            ResourceMetadataUri(response));
        Assert.Equal(1, await (await factory.DatabaseAsync).ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'automation_access_denied' AND Outcome = N'Denied'
            """));
    }

    [Fact]
    public async Task AnUnauthenticatedBrowserIsStillSentToStaffSignInWithMcpMapped()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = CreateCookielessClient(mcpFactory);

        // The 405 belongs to /mcp alone: a staff page and an unmapped path
        // still meet the fallback policy and its sign-in redirect.
        foreach (var path in new[] { "/Cases", "/no-such-route" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith(
                "/Account/SignIn",
                response.Headers.Location!.OriginalString,
                StringComparison.Ordinal);
        }
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
            McpServer,
            fullToken,
            ToolCallPayload(3, "pegasus_intake_queue_list", new { limit = 10 })))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = await ReadJsonRpcAsync(response);
            var result = document.RootElement.GetProperty("result");
            Assert.False(result.TryGetProperty("isError", out var isError) && isError.GetBoolean());
            var structured = result.GetProperty("structuredContent");
            Assert.Empty(structured.GetProperty("items").EnumerateArray());
            Assert.Equal(10, structured.GetProperty("limit").GetInt32());
            Assert.True(
                !structured.TryGetProperty("nextCursor", out var nextCursor)
                || nextCursor.ValueKind == JsonValueKind.Null);
            Assert.False(string.IsNullOrWhiteSpace(
                structured.GetProperty("correlationId").GetString()));
        }

        Assert.Equal(1, await (await factory.DatabaseAsync).ScalarAsync<int>(
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
            McpServer,
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

        Assert.Equal(1, await (await factory.DatabaseAsync).ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_case_get'
              AND Outcome = N'Failed'
            """));

        // Out-of-scope: a token holding only automation.cases reaches the
        // intake tool, which refuses it before anything runs, names the scope
        // and writes an attributable security event (ADR-0087 decision 3).
        var casesOnlyToken = await RequestTokenAsync(client, "automation.cases");
        using (var response = await PostMcpAsync(
            client,
            McpServer,
            casesOnlyToken,
            ToolCallPayload(5, "pegasus_intake_queue_list", new { limit = 10 })))
        {
            await AssertScopeDeniedAsync(response, "automation.intake");
        }

        // An edit-lease tool for a Triage record needs automation.intake too,
        // whatever the token's cases scope allows on a Case.
        using (var response = await PostMcpAsync(
            client,
            McpServer,
            casesOnlyToken,
            ToolCallPayload(
                9,
                "pegasus_edit_begin",
                new
                {
                    recordKind = "Triage",
                    recordId = Guid.NewGuid(),
                    expectedVersion = 0,
                    operationKey = "mcp:triage-lease-scope"
                })))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("automation.intake", await ReadErrorTextAsync(response), StringComparison.Ordinal);
        }

        Assert.Equal(2, await CountScopeDenialsAsync(factory));
    }

    [Fact]
    public async Task CaseGetUsesTheBoundedHeaderAndCursorSubLists()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, "automation.cases");

        using var response = await PostMcpAsync(
            client,
            McpServer,
            token,
            ToolCallPayload(6, "pegasus_case_get", new { caseId, limit = 1 }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var structured = await ReadStructuredContentAsync(response);
        Assert.Equal(caseId, structured.GetProperty("summary").GetProperty("caseId").GetGuid());
        Assert.InRange(structured.GetProperty("documents").GetArrayLength(), 0, 1);
        Assert.InRange(structured.GetProperty("recentHistory").GetArrayLength(), 0, 1);
        Assert.False(structured.TryGetProperty("documentEntryCount", out _));
        Assert.False(structured.TryGetProperty("historyEntryCount", out _));
        foreach (var cursorName in new[] { "nextDocumentCursor", "nextHistoryCursor" })
        {
            if (structured.TryGetProperty(cursorName, out var cursor))
            {
                Assert.True(cursor.ValueKind is JsonValueKind.Null or JsonValueKind.String);
                if (cursor.ValueKind == JsonValueKind.String)
                    Assert.False(string.IsNullOrWhiteSpace(cursor.GetString()));
            }
        }
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
            McpServer,
            accessToken,
            ToolCallPayload(6, "pegasus_intake_queue_list", new { limit = 10 })))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // Automation holds a Case lease and a Triage scope when it is stopped.
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        await BeginEditAsync(client, accessToken, caseId, 0, rpcId: 5);
        var triageId = Guid.NewGuid();
        await (await factory.DatabaseAsync).ExecuteAsync(
            $"""
            INSERT INTO EditScopes (ScopeKind, RecordId, HolderKind, Holder, TokenHash, ExpectedVersion, Generation, ExpiresAtUtc)
            VALUES (N'Triage', '{triageId:D}', N'Automation', N'stopped-grant', REPLICATE('a', 64), 0, 1, DATEADD(minute, 5, SYSDATETIMEOFFSET()))
            """);

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
            McpServer,
            accessToken,
            ToolCallPayload(7, "pegasus_intake_queue_list", new { limit = 10 })))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = await ReadJsonRpcAsync(response);
            Assert.Contains(
                "disabled",
                document.RootElement.ToString(),
                StringComparison.OrdinalIgnoreCase);
        }

        // Stop released both, so staff need not wait for the expiry, and the
        // Stop's history entry names the released Case.
        Assert.Equal(0, await (await factory.DatabaseAsync).ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseWorkflows
            WHERE CaseId = '{caseId:D}' AND EditLeaseHolderKind IS NOT NULL
            """));
        Assert.Equal(0, await (await factory.DatabaseAsync).ScalarAsync<int>(
            "SELECT COUNT(*) FROM EditScopes WHERE HolderKind = N'Automation'"));
        Assert.Equal(1, await (await factory.DatabaseAsync).ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE EventKind = N'automation_client_disabled'
              AND AfterJson LIKE N'%{caseId:D}%'
              AND AfterJson LIKE N'%"EditScopes":1%'
            """));

        Assert.Equal(1, await (await factory.DatabaseAsync).ScalarAsync<int>(
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

        Assert.True(await (await factory.DatabaseAsync).ScalarAsync<int>(
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
            McpServer,
            restoredToken,
            ToolCallPayload(8, "pegasus_intake_queue_list", new { limit = 10 })))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = await ReadJsonRpcAsync(response);
            var result = document.RootElement.GetProperty("result");
            Assert.False(result.TryGetProperty("isError", out var isError) && isError.GetBoolean());
        }

        Assert.Equal(2, await (await factory.DatabaseAsync).ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'automation_client'
              AND ActorKind = N'Staff'
              AND ActorSubjectId = N'{DevelopmentOfflineIdentity.AdministratorId:D}'
              AND EventKind IN (N'automation_client_disabled', N'automation_client_enabled')
            """));
    }

    [Fact]
    public async Task EditRenewUsesTheCoreUseCaseAndRefusalsNameTheGuardAndCurrentVersion()
    {
        var caseId = Guid.Parse("6f5a3d21-9c44-4f70-8f1a-7d2b0c9e4a55");
        var leases = new RecordingAutomationLeases(caseId);
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRenewCaseEditLease>();
                services.AddSingleton<IRenewCaseEditLease>(leases);
            }));
        using var client = mcpFactory.CreateClient();
        var accessToken = await RequestTokenAsync(client, "automation.cases");

        // The holder renews through the same Core use case the staff renew
        // control calls, and the tool returns the begin-shaped result.
        using (var response = await PostMcpAsync(
            client,
            McpServer,
            accessToken,
            ToolCallPayload(
                20,
                "pegasus_edit_renew",
                new
                {
                    recordKind = "Case",
                    recordId = caseId,
                    expectedVersion = 3,
                    editLeaseToken = RecordingAutomationLeases.HeldToken,
                    operationKey = "mcp:renew-held"
                })))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = await ReadJsonRpcAsync(response);
            var result = document.RootElement.GetProperty("result");
            Assert.False(result.TryGetProperty("isError", out var isError) && isError.GetBoolean());
            var structured = result.GetProperty("structuredContent");
            Assert.Equal("Case", structured.GetProperty("recordKind").GetString());
            Assert.Equal(caseId, structured.GetProperty("recordId").GetGuid());
            Assert.Equal(3, structured.GetProperty("version").GetInt64());
            Assert.Equal("mcp:renew-held", structured.GetProperty("operationKey").GetString());
        }

        var renewal = Assert.Single(leases.Renewals);
        Assert.Equal(caseId, renewal.CaseId);
        Assert.Equal(3, renewal.ExpectedVersion);
        Assert.Equal(RecordingAutomationLeases.HeldToken, renewal.LeaseToken);
        Assert.Equal("pegasus-automation", renewal.Actor.SubjectId);

        // A non-holder is refused, and the refusal names which guard refused
        // and the version the case now stands at, with no token disclosed.
        using (var response = await PostMcpAsync(
            client,
            McpServer,
            accessToken,
            ToolCallPayload(
                21,
                "pegasus_edit_renew",
                new
                {
                    recordKind = "Case",
                    recordId = caseId,
                    expectedVersion = 3,
                    editLeaseToken = new string('b', 64),
                    operationKey = "mcp:renew-non-holder"
                })))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = await ReadJsonRpcAsync(response);
            var body = document.RootElement.ToString();
            Assert.Contains("edit authority is already held", body, StringComparison.Ordinal);
            Assert.Contains("version 9", body, StringComparison.Ordinal);
            Assert.DoesNotContain(RecordingAutomationLeases.HeldToken, body, StringComparison.Ordinal);
        }

        Assert.Equal(1, await (await factory.DatabaseAsync).ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_edit_renew'
              AND Outcome = N'Failed'
            """));

        // The requirement classifies routine renewal as telemetry and keeps only a deliberate
        // recovery or a material denial in permanent history, so the successful renewal above must
        // leave nothing behind: an automation run that renews on a timer would otherwise write a
        // heartbeat into the case's permanent record.
        Assert.Equal(0, await (await factory.DatabaseAsync).ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_edit_renew'
              AND Outcome = N'Succeeded'
            """));
    }

    [Fact]
    public async Task ARefusedDocumentToolReportsTheRefusingGuardAndTheCurrentCaseVersion()
    {
        var caseId = Guid.Parse("3a9f2c17-58b6-4c0e-9a3d-1e6f7b2c8d40");
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAddCaseDocument>();
                services.AddSingleton<IAddCaseDocument>(new RefusingDocumentStore(caseId));
            }));
        using var client = mcpFactory.CreateClient();
        var accessToken = await RequestTokenAsync(client, "automation.documents");

        // MCP-04 inherits the shared guard: the refusal names which guard
        // refused and the version the case now stands at, with no token.
        using (var response = await PostMcpAsync(
            client,
            McpServer,
            accessToken,
            ToolCallPayload(
                30,
                "pegasus_document_add",
                new
                {
                    caseId,
                    fileName = "report.pdf",
                    mediaType = "application/pdf",
                    contentBase64 = Convert.ToBase64String("a document"u8.ToArray()),
                    semanticRole = "Other",
                    expectedCaseVersion = 4,
                    editLeaseToken = new string('c', 64),
                    operationKey = "mcp:document-add-refused"
                })))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = await ReadJsonRpcAsync(response);
            var body = document.RootElement.ToString();
            Assert.Contains("edit authority is already held", body, StringComparison.Ordinal);
            Assert.Contains("version 11", body, StringComparison.Ordinal);
            Assert.DoesNotContain(new string('c', 64), body, StringComparison.Ordinal);
        }

        Assert.Equal(1, await (await factory.DatabaseAsync).ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_document_add'
              AND Outcome = N'Failed'
            """));
    }

    private static string InitializePayload(int id) =>
        JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            method = "initialize",
            @params = new
            {
                protocolVersion = "2025-06-18",
                capabilities = new { },
                clientInfo = new { name = "pegasus-integration-test", version = "1" }
            }
        });

    private static async Task<string[]> ToolNamesAsync(HttpResponseMessage response)
    {
        using var document = await ReadJsonRpcAsync(response);
        return [.. document.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString()!)
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>The RFC 9728 document a 401 names in its bearer challenge.</summary>
    private static string ResourceMetadataUri(HttpResponseMessage response)
    {
        var challenge = string.Join(" ", response.Headers.WwwAuthenticate.Select(value => value.ToString()));
        const string marker = "resource_metadata=\"";
        var start = challenge.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, challenge);
        start += marker.Length;
        return challenge[start..challenge.IndexOf('"', start)];
    }

    /// <summary>
    /// A caller with no staff cookie, as the classifier is: the integration
    /// scheme answers anonymous and challenges with the sign-in redirect, which
    /// the client reports rather than follows.
    /// </summary>
    private static HttpClient CreateCookielessClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Anonymous", "1");
        return client;
    }

    private sealed class RefusingDocumentStore(Guid caseId) : IAddCaseDocument
    {
        public Task<AddCaseDocumentResult> ExecuteAsync(
            AddCaseDocumentCommand command,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new CaseEditLeaseConflictException(caseId, 11);
        }
    }

    private sealed class RecordingAutomationLeases(Guid caseId) : IRenewCaseEditLease
    {
        public const string HeldToken = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        public List<RenewCaseEditLeaseRequest> Renewals { get; } = [];

        public Task<CaseEditLease> ExecuteAsync(
            RenewCaseEditLeaseRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(request.LeaseToken, HeldToken, StringComparison.Ordinal))
            {
                throw new CaseEditLeaseConflictException(caseId, 9);
            }

            Renewals.Add(request);
            return Task.FromResult(
                new CaseEditLease(
                    caseId,
                    HeldToken,
                    request.Actor.SubjectId,
                    request.ExpectedVersion,
                    DateTimeOffset.UtcNow.AddMinutes(5)));
        }
    }
}
