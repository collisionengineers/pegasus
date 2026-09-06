using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        "pegasus_estimate_list",
        "pegasus_estimate_save",
        "pegasus_assessment_get",
        "pegasus_assessment_update",
        "pegasus_mail_list",
        "pegasus_mail_get",
        "pegasus_mail_correct_classification",
        "pegasus_unidentified_list",
        "pegasus_unidentified_get",
        "pegasus_unidentified_resolve",
        "pegasus_unidentified_source_download",
        "pegasus_triage_list",
        "pegasus_triage_get",
        "pegasus_triage_source_download",
        "pegasus_triage_await_information",
        "pegasus_triage_record_finding",
        "pegasus_triage_supersede_finding",
        "pegasus_triage_response_link",
        "pegasus_triage_response_unlink",
        "pegasus_triage_complete",
        "pegasus_triage_cancel",
        "pegasus_triage_reopen",
        "pegasus_triage_case_link",
        "pegasus_triage_case_unlink",
        "pegasus_ai_job_list",
        "pegasus_ai_job_create",
        "pegasus_ai_job_take",
        "pegasus_ai_job_progress",
        "pegasus_ai_job_complete",
        "pegasus_ai_job_complete_market_research",
        "pegasus_ai_job_fail",
        "pegasus_ai_job_release"
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
            ToolCallPayload(5, "pegasus_intake_queue_list", new { limit = 10 })))
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
            ToolCallPayload(6, "pegasus_intake_queue_list", new { limit = 10 })))
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
            ToolCallPayload(7, "pegasus_intake_queue_list", new { limit = 10 })))
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
            ToolCallPayload(8, "pegasus_intake_queue_list", new { limit = 10 })))
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
