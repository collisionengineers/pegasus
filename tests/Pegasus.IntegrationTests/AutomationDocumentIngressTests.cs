using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Workflow;
using Pegasus.Web.Mcp;

namespace Pegasus.IntegrationTests;

/// <summary>
/// MCP-04 caller evidence: document add, download, and export through the
/// gated /mcp host. Ingress gate/token/inventory stays in
/// <see cref="AutomationMcpIngressTests"/>.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class AutomationDocumentIngressTests
{
    [Fact]
    public async Task AddAndDownloadOverHttpReplayAndAttributeHistory()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = AutomationMcpTestSupport.WithAutomationMcp(factory);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await AutomationMcpTestSupport.RequestTokenAsync(
            client,
            AutomationMcpTestSupport.AllScopes);
        var (caseVersion, leaseToken) = await AutomationMcpTestSupport.BeginEditAsync(
            client,
            token,
            caseId,
            expectedVersion: 0,
            rpcId: 41);
        var content = "mcp-04 document fixture"u8.ToArray();
        const string operationKey = "mcp:document-add-success";

        Guid occurrenceId;
        Guid versionId;
        using (var response = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AddPayload(41, caseId, content, "Other", caseVersion, leaseToken, operationKey)))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var structured = await AutomationMcpTestSupport.ReadStructuredContentAsync(response);
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

        using (var replay = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AddPayload(42, caseId, content, "Other", caseVersion, leaseToken, operationKey)))
        {
            var structured = await AutomationMcpTestSupport.ReadStructuredContentAsync(replay);
            Assert.True(structured.GetProperty("isReplay").GetBoolean());
            Assert.Equal(occurrenceId, structured.GetProperty("occurrenceId").GetGuid());
            Assert.Equal(versionId, structured.GetProperty("versionId").GetGuid());
        }

        using (var download = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AutomationMcpTestSupport.ToolCallPayload(
                43,
                "pegasus_document_download",
                new { caseId, occurrenceId, versionId })))
        {
            var structured = await AutomationMcpTestSupport.ReadStructuredContentAsync(download);
            Assert.True(structured.GetProperty("contentIncluded").GetBoolean());
            Assert.Equal(
                content,
                Convert.FromBase64String(structured.GetProperty("contentBase64").GetString()!));
        }

        using (var oversize = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AutomationMcpTestSupport.ToolCallPayload(
                44,
                "pegasus_document_download",
                new { caseId, occurrenceId, versionId, maxInlineBytes = 1 })))
        {
            var structured = await AutomationMcpTestSupport.ReadStructuredContentAsync(oversize);
            Assert.False(structured.GetProperty("contentIncluded").GetBoolean());
            Assert.True(
                !structured.TryGetProperty("contentBase64", out var omitted)
                || omitted.ValueKind is JsonValueKind.Null,
                "Oversized download must not return inline content.");
            Assert.Contains(
                "exceeds the inline limit",
                structured.GetProperty("notice").GetString(),
                StringComparison.Ordinal);
            var contentUrl = structured.GetProperty("contentUrl").GetString();
            Assert.False(string.IsNullOrWhiteSpace(contentUrl));
            using var streamRequest = new HttpRequestMessage(HttpMethod.Get, contentUrl);
            streamRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            streamRequest.Headers.Range = new RangeHeaderValue(0, 2);
            using var streamed = await client.SendAsync(streamRequest);
            Assert.Equal(HttpStatusCode.PartialContent, streamed.StatusCode);
            Assert.Equal(content[..3], await streamed.Content.ReadAsByteArrayAsync());

            using var unauthenticated = await client.GetAsync(contentUrl);
            Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
            var wrongScopeToken = await AutomationMcpTestSupport.RequestTokenAsync(
                client, AutomationMcp.CasesScope);
            using var wrongScopeRequest = new HttpRequestMessage(HttpMethod.Get, contentUrl);
            wrongScopeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", wrongScopeToken);
            using var wrongScope = await client.SendAsync(wrongScopeRequest);
            Assert.Equal(HttpStatusCode.Forbidden, wrongScope.StatusCode);
        }

        Assert.Equal(2, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_document_download'
              AND Outcome = N'Succeeded'
              AND ActorSubjectId = N'pegasus-automation'
            """));
    }

    [Fact]
    public async Task ExportRefusesWhenTheCaseIsNotInReview()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = AutomationMcpTestSupport.WithAutomationMcp(factory);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(
            mcpFactory,
            new CaseCompleteness(false, false, false, false));
        using var client = mcpFactory.CreateClient();
        var token = await AutomationMcpTestSupport.RequestTokenAsync(
            client,
            AutomationMcpTestSupport.AllScopes);

        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            var workflow = await scope.ServiceProvider
                .GetRequiredService<ICaseWorkflowQueries>()
                .GetAsync(caseId, CancellationToken.None);
            Assert.Equal(CaseLifecycleState.NotReady, workflow?.State);
        }

        var (caseVersion, leaseToken) = await AutomationMcpTestSupport.BeginEditAsync(
            client,
            token,
            caseId,
            expectedVersion: 0,
            rpcId: 50);
        using var export = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AutomationMcpTestSupport.ToolCallPayload(
                51,
                "pegasus_document_export",
                new
                {
                    caseId,
                    selections = new[]
                    {
                        new { occurrenceId = Guid.NewGuid(), versionId = Guid.NewGuid() }
                    },
                    expectedCaseVersion = caseVersion,
                    editLeaseToken = leaseToken,
                    operationKey = "mcp:document-export-not-review"
                }));
        using var document = await AutomationMcpTestSupport.ReadJsonRpcAsync(export);
        var body = document.RootElement.ToString();
        Assert.Contains("can only be exported while it is in Review", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportSucceedsAfterReturnToReview()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = AutomationMcpTestSupport.WithAutomationMcp(factory);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await AutomationMcpTestSupport.RequestTokenAsync(
            client,
            AutomationMcpTestSupport.AllScopes);
        var (caseVersion, leaseToken) = await AutomationMcpTestSupport.BeginEditAsync(
            client,
            token,
            caseId,
            expectedVersion: 0,
            rpcId: 60);
        var content = "export fixture"u8.ToArray();

        Guid occurrenceId;
        Guid versionId;
        using (var add = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AddPayload(61, caseId, content, "Other", caseVersion, leaseToken, "mcp:document-export-add")))
        {
            var structured = await AutomationMcpTestSupport.ReadStructuredContentAsync(add);
            occurrenceId = structured.GetProperty("occurrenceId").GetGuid();
            versionId = structured.GetProperty("versionId").GetGuid();
        }

        await AutomationMcpTestSupport.EnsureInReviewAsync(mcpFactory, client, token, caseId);
        var exportLease = await AutomationMcpTestSupport.BeginEditAsync(
            client,
            token,
            caseId,
            await AutomationMcpTestSupport.GetWorkflowVersionAsync(mcpFactory, caseId),
            rpcId: 62);
        using (var export = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AutomationMcpTestSupport.ToolCallPayload(
                63,
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
            var structured = await AutomationMcpTestSupport.ReadStructuredContentAsync(export);
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
        using var mcpFactory = AutomationMcpTestSupport.WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();
        var token = await AutomationMcpTestSupport.RequestTokenAsync(client, "automation.documents");
        var leaked = new string('d', 64);

        using (var badRole = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AddPayload(70, Guid.NewGuid(), "x"u8.ToArray(), "NotARole", 0, leaked, "mcp:document-bad-role")))
        {
            var body = (await AutomationMcpTestSupport.ReadJsonRpcAsync(badRole)).RootElement.ToString();
            Assert.Contains("semantic role", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(leaked, body, StringComparison.Ordinal);
        }

        using (var missingLease = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AddPayload(71, Guid.NewGuid(), "x"u8.ToArray(), "Other", 0, "", "mcp:document-missing-lease")))
        {
            var body = (await AutomationMcpTestSupport.ReadJsonRpcAsync(missingLease)).RootElement.ToString();
            Assert.Contains("edit lease token is required", body, StringComparison.OrdinalIgnoreCase);
        }

        using (var emptyVersion = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AutomationMcpTestSupport.ToolCallPayload(
                73,
                "pegasus_document_download",
                new { caseId = Guid.NewGuid(), occurrenceId = Guid.NewGuid(), versionId = Guid.Empty })))
        {
            var body = (await AutomationMcpTestSupport.ReadJsonRpcAsync(emptyVersion)).RootElement.ToString();
            Assert.Contains("version identifier", body, StringComparison.OrdinalIgnoreCase);
        }

        using (var emptyExport = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AutomationMcpTestSupport.ToolCallPayload(
                72,
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
            var body = (await AutomationMcpTestSupport.ReadJsonRpcAsync(emptyExport)).RootElement.ToString();
            Assert.Contains("selection is required", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(leaked, body, StringComparison.Ordinal);
        }

        Assert.Equal(4, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind IN (N'pegasus_document_add', N'pegasus_document_download', N'pegasus_document_export')
              AND Outcome = N'Failed'
            """));
    }

    [Fact]
    public async Task DocumentToolsEnforceTheDocumentsScope()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = AutomationMcpTestSupport.WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();
        var casesOnlyToken = await AutomationMcpTestSupport.RequestTokenAsync(client, "automation.cases");

        var payloads = new[]
        {
            AddPayload(
                80,
                Guid.NewGuid(),
                "x"u8.ToArray(),
                "Other",
                0,
                new string('e', 64),
                "mcp:document-scope-denied"),
            AutomationMcpTestSupport.ToolCallPayload(
                81,
                "pegasus_document_download",
                new { caseId = Guid.NewGuid(), occurrenceId = Guid.NewGuid(), versionId = Guid.NewGuid() }),
            AutomationMcpTestSupport.ToolCallPayload(
                82,
                "pegasus_document_export",
                new
                {
                    caseId = Guid.NewGuid(),
                    selections = new[] { new { occurrenceId = Guid.NewGuid(), versionId = Guid.NewGuid() } },
                    expectedCaseVersion = 0,
                    editLeaseToken = new string('e', 64),
                    operationKey = "mcp:document-export-scope-denied"
                })
        };

        foreach (var payload in payloads)
        {
            using var response = await AutomationMcpTestSupport.PostMcpAsync(client, casesOnlyToken, payload);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = await AutomationMcpTestSupport.ReadJsonRpcAsync(response);
            Assert.Contains(
                "automation.documents",
                document.RootElement.ToString(),
                StringComparison.Ordinal);
        }

        Assert.Equal(payloads.Length, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'automation_scope_denied'
              AND Outcome = N'Denied'
              AND SubjectId = N'pegasus-automation'
            """));
    }

    private static string AddPayload(
        int id,
        Guid caseId,
        byte[] content,
        string semanticRole,
        long expectedCaseVersion,
        string editLeaseToken,
        string operationKey) =>
        AutomationMcpTestSupport.ToolCallPayload(
            id,
            "pegasus_document_add",
            new
            {
                caseId,
                fileName = "instruction.txt",
                mediaType = "text/plain",
                contentBase64 = Convert.ToBase64String(content),
                semanticRole,
                expectedCaseVersion,
                editLeaseToken,
                operationKey
            });
}
