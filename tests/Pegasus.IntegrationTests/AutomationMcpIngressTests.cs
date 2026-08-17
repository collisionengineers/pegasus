using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
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
        "pegasus_case_edit_renew",
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
            accessToken,
            ToolCallPayload(
                20,
                "pegasus_case_edit_renew",
                new
                {
                    caseId,
                    expectedVersion = 3,
                    leaseToken = RecordingAutomationLeases.HeldToken,
                    operationKey = "mcp:renew-held"
                })))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = await ReadJsonRpcAsync(response);
            var result = document.RootElement.GetProperty("result");
            Assert.False(result.TryGetProperty("isError", out var isError) && isError.GetBoolean());
            var structured = result.GetProperty("structuredContent");
            Assert.Equal(caseId, structured.GetProperty("caseId").GetGuid());
            Assert.Equal(3, structured.GetProperty("caseVersion").GetInt64());
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
            accessToken,
            ToolCallPayload(
                21,
                "pegasus_case_edit_renew",
                new
                {
                    caseId,
                    expectedVersion = 3,
                    leaseToken = new string('b', 64),
                    operationKey = "mcp:renew-non-holder"
                })))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = await ReadJsonRpcAsync(response);
            var body = document.RootElement.ToString();
            Assert.Contains("held by another actor", body, StringComparison.Ordinal);
            Assert.Contains("version 9", body, StringComparison.Ordinal);
            Assert.DoesNotContain(RecordingAutomationLeases.HeldToken, body, StringComparison.Ordinal);
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_case_edit_renew'
              AND Outcome = N'Failed'
            """));

        // The requirement classifies routine renewal as telemetry and keeps only a deliberate
        // recovery or a material denial in permanent history, so the successful renewal above must
        // leave nothing behind: an automation run that renews on a timer would otherwise write a
        // heartbeat into the case's permanent record.
        Assert.Equal(0, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_case_edit_renew'
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
            Assert.Contains("held by another actor", body, StringComparison.Ordinal);
            Assert.Contains("version 11", body, StringComparison.Ordinal);
            Assert.DoesNotContain(new string('c', 64), body, StringComparison.Ordinal);
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_document_add'
              AND Outcome = N'Failed'
            """));
    }

    [Fact]
    public async Task DocumentToolsAddDownloadAndExportOverHttpWithReplayAndHistory()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        await factory.Database.ExecuteAsync(
            $"""
            UPDATE CaseWorkflows
            SET State = N'{nameof(CaseLifecycleState.Review)}'
            WHERE CaseId = '{caseId:D}'
            """);

        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);
        var (caseVersion, leaseToken) = await BeginEditAsync(client, token, caseId, expectedVersion: 0, 40);
        var content = "mcp-04 document fixture"u8.ToArray();
        var operationKey = "mcp:document-add-success";

        Guid occurrenceId;
        Guid versionId;
        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                41,
                "pegasus_document_add",
                new
                {
                    caseId,
                    fileName = "instruction.txt",
                    mediaType = "text/plain",
                    contentBase64 = Convert.ToBase64String(content),
                    semanticRole = "Other",
                    expectedCaseVersion = caseVersion,
                    editLeaseToken = leaseToken,
                    operationKey
                })))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var structured = await ReadStructuredContentAsync(response);
            Assert.False(structured.GetProperty("isReplay").GetBoolean());
            occurrenceId = structured.GetProperty("occurrenceId").GetGuid();
            versionId = structured.GetProperty("versionId").GetGuid();
            Assert.Equal("instruction.txt", structured.GetProperty("fileName").GetString());
            Assert.False(string.IsNullOrWhiteSpace(structured.GetProperty("sha256").GetString()));
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_document_add'
              AND Outcome = N'Succeeded'
              AND ActorSubjectId = N'pegasus-automation'
            """));

        using (var replay = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                42,
                "pegasus_document_add",
                new
                {
                    caseId,
                    fileName = "instruction.txt",
                    mediaType = "text/plain",
                    contentBase64 = Convert.ToBase64String(content),
                    semanticRole = "Other",
                    expectedCaseVersion = caseVersion,
                    editLeaseToken = leaseToken,
                    operationKey
                })))
        {
            var structured = await ReadStructuredContentAsync(replay);
            Assert.True(structured.GetProperty("isReplay").GetBoolean());
            Assert.Equal(occurrenceId, structured.GetProperty("occurrenceId").GetGuid());
            Assert.Equal(versionId, structured.GetProperty("versionId").GetGuid());
        }

        using (var download = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                43,
                "pegasus_document_download",
                new { caseId, occurrenceId, versionId })))
        {
            var structured = await ReadStructuredContentAsync(download);
            Assert.True(structured.GetProperty("contentIncluded").GetBoolean());
            Assert.Equal(
                content,
                Convert.FromBase64String(structured.GetProperty("contentBase64").GetString()!));
        }

        using (var oversize = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                44,
                "pegasus_document_download",
                new { caseId, occurrenceId, versionId, maxInlineBytes = 1 })))
        {
            var structured = await ReadStructuredContentAsync(oversize);
            Assert.False(structured.GetProperty("contentIncluded").GetBoolean());
            Assert.True(
                !structured.TryGetProperty("contentBase64", out var omitted)
                || omitted.ValueKind is JsonValueKind.Null,
                "Oversized download must not return inline content.");
            Assert.Contains(
                "exceeds the inline limit",
                structured.GetProperty("notice").GetString(),
                StringComparison.Ordinal);
        }

        Assert.Equal(
            nameof(CaseLifecycleState.Review),
            await factory.Database.ScalarAsync<string>(
                $"SELECT State FROM CaseWorkflows WHERE CaseId = '{caseId:D}'"));
        // A successful add completes the mutation guard: version advances and
        // the lease is cleared, same as a staff save. Export needs a fresh claim.
        var afterAdd = await GetCaseVersionAsync(client, token, caseId, 45);
        var exportLease = await BeginEditAsync(client, token, caseId, afterAdd, 46);
        using (var export = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                47,
                "pegasus_document_export",
                new
                {
                    caseId,
                    selections = new[] { new { occurrenceId, versionId } },
                    expectedCaseVersion = exportLease.CaseVersion,
                    editLeaseToken = exportLease.LeaseToken,
                    operationKey = "mcp:document-export-success",
                    maxInlineBytes = 10 * 1024 * 1024
                })))
        {
            using var document = await ReadJsonRpcAsync(export);
            var result = document.RootElement.GetProperty("result");
            Assert.False(
                result.TryGetProperty("isError", out var isError) && isError.GetBoolean(),
                result.ToString());
            var structured = result.GetProperty("structuredContent");
            Assert.True(structured.GetProperty("contentIncluded").GetBoolean());
            Assert.Equal(1, structured.GetProperty("manifest").GetArrayLength());
            Assert.Equal(
                occurrenceId,
                structured.GetProperty("manifest")[0].GetProperty("occurrenceId").GetGuid());
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_document_export'
              AND Outcome = N'Succeeded'
            """));
    }

    [Fact]
    public async Task DocumentToolsRefuseValidationFailuresWithoutLeakingTheLeaseToken()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, "automation.documents");
        var leaked = new string('d', 64);

        using (var badRole = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                50,
                "pegasus_document_add",
                new
                {
                    caseId = Guid.NewGuid(),
                    fileName = "report.pdf",
                    mediaType = "application/pdf",
                    contentBase64 = Convert.ToBase64String("x"u8.ToArray()),
                    semanticRole = "NotARole",
                    expectedCaseVersion = 0,
                    editLeaseToken = leaked,
                    operationKey = "mcp:document-bad-role"
                })))
        {
            var body = (await ReadJsonRpcAsync(badRole)).RootElement.ToString();
            Assert.Contains("semantic role", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(leaked, body, StringComparison.Ordinal);
        }

        using (var missingLease = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                51,
                "pegasus_document_add",
                new
                {
                    caseId = Guid.NewGuid(),
                    fileName = "report.pdf",
                    mediaType = "application/pdf",
                    contentBase64 = Convert.ToBase64String("x"u8.ToArray()),
                    semanticRole = "Other",
                    expectedCaseVersion = 0,
                    editLeaseToken = "",
                    operationKey = "mcp:document-missing-lease"
                })))
        {
            var body = (await ReadJsonRpcAsync(missingLease)).RootElement.ToString();
            Assert.Contains("edit lease token is required", body, StringComparison.OrdinalIgnoreCase);
        }

        using (var emptyExport = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                52,
                "pegasus_document_export",
                new
                {
                    caseId = Guid.NewGuid(),
                    selections = Array.Empty<object>(),
                    expectedCaseVersion = 0,
                    editLeaseToken = leaked,
                    operationKey = "mcp:document-empty-export"
                })))
        {
            var body = (await ReadJsonRpcAsync(emptyExport)).RootElement.ToString();
            Assert.Contains("selection is required", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(leaked, body, StringComparison.Ordinal);
        }

        Assert.Equal(3, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind IN (N'pegasus_document_add', N'pegasus_document_export')
              AND Outcome = N'Failed'
            """));
    }

    [Fact]
    public async Task DocumentToolsEnforceTheDocumentsScope()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();
        var casesOnlyToken = await RequestTokenAsync(client, "automation.cases");

        using var response = await PostMcpAsync(
            client,
            casesOnlyToken,
            ToolCallPayload(
                60,
                "pegasus_document_add",
                new
                {
                    caseId = Guid.NewGuid(),
                    fileName = "report.pdf",
                    mediaType = "application/pdf",
                    contentBase64 = Convert.ToBase64String("x"u8.ToArray()),
                    semanticRole = "Other",
                    expectedCaseVersion = 0,
                    editLeaseToken = new string('e', 64),
                    operationKey = "mcp:document-scope-denied"
                }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonRpcAsync(response);
        Assert.Contains(
            "automation.documents",
            document.RootElement.ToString(),
            StringComparison.Ordinal);

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'automation_scope_denied'
              AND Outcome = N'Denied'
              AND SubjectId = N'pegasus-automation'
            """));
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

    private static readonly DateTimeOffset SeedUtcNow = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    private static async Task<Guid> SeedAcceptedCaseAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var email = IntakeTestEvidence.CreateEmail(
            $"mcp-04-{Guid.NewGuid():N}.eml",
            "QDOS instruction\r\nClaimant Name: Document Ingress\r\nClaim Number: DOC-001\r\nVehicle Registration: AB12 CDE");
        var receipt = await services.GetRequiredService<ProcessIntake>()
            .ExecuteAsync(
                new(
                    email.FileName,
                    email.MediaType,
                    email.Content,
                    SeedUtcNow,
                    "mcp-04-document-test",
                    new(
                        IntakeSourceChannel.ManualUpload,
                        $"mcp-04-source:{Guid.NewGuid():N}")),
                CancellationToken.None);
        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);
        await SeedPrincipalAsync(services);
        var outcome = await services.GetRequiredService<IAcceptIntake>()
            .ExecuteAsync(
                new(
                    receipt.Id,
                    0,
                    ActionActor.SystemWorker("mcp-04-document-integration"),
                    $"case-accept:{Guid.NewGuid():N}",
                    "Integration fixture confirmed complete intake evidence.",
                    CaseType.Inspection,
                    QdosPrincipal.Code,
                    new(true, true, true, true)),
                CancellationToken.None);
        return outcome.Identity.CaseId;
    }

    private static async Task SeedPrincipalAsync(IServiceProvider services)
    {
        const string principalCode = QdosPrincipal.Code;
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        if (await context.Principals.AnyAsync(
                value => value.Code == principalCode && value.IsActive,
                CancellationToken.None))
        {
            return;
        }

        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {"MCP-04 document organization"}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO OrganizationRoles (OrganizationId, Role) VALUES ({organizationId}, {"work_provider"})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {SeedUtcNow})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO Principals
                (Id, OrganizationId, Code, SequenceLineageId, PredecessorId, SuccessorId, IsActive, Version)
            VALUES
                ({principalId}, {organizationId}, {principalCode}, {lineageId}, NULL, NULL, {true}, {0L})
            """);
        await transaction.CommitAsync();
    }

    private static async Task<(long CaseVersion, string LeaseToken)> BeginEditAsync(
        HttpClient client,
        string token,
        Guid caseId,
        long expectedVersion,
        int rpcId)
    {
        using var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                rpcId,
                "pegasus_case_edit_begin",
                new
                {
                    caseId,
                    expectedVersion,
                    operationKey = $"mcp:document-lease-{rpcId}"
                }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lease = await ReadStructuredContentAsync(response);
        return (lease.GetProperty("caseVersion").GetInt64(), lease.GetProperty("leaseToken").GetString()!);
    }

    private static async Task<long> GetCaseVersionAsync(
        HttpClient client,
        string token,
        Guid caseId,
        int rpcId)
    {
        using var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(rpcId, "pegasus_case_get", new { caseId }));
        var structured = await ReadStructuredContentAsync(response);
        return structured.GetProperty("caseVersion").GetInt64();
    }

    private static async Task<JsonElement> ReadStructuredContentAsync(HttpResponseMessage response)
    {
        using var document = await ReadJsonRpcAsync(response);
        var result = document.RootElement.GetProperty("result");
        Assert.False(result.TryGetProperty("isError", out var isError) && isError.GetBoolean());
        return result.GetProperty("structuredContent").Clone();
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
