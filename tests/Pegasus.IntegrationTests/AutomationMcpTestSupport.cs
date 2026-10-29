using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Shared DevelopmentOffline MCP harness for the ingress, document, and
/// assessment caller tests. Token, HTTP, seed, and lease helpers live here
/// once so each tranche file stays a set of facts.
/// </summary>
internal static class AutomationMcpTestSupport
{
    public const string ClientId = "pegasus-automation";
    public const string ClientSecret = "integration-test-automation-secret-0123456789";
    public const string ConnectorRedirectUri = "https://connector.example/api/mcp/auth_callback";
    public const string AllScopes =
        "automation.cases automation.intake automation.documents automation.assessment automation.mail";

    // The one server (ADR-0087), every tool at its path.
    public const string McpServer = "/mcp";

    public static readonly DateTimeOffset SeedUtcNow = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    public static WebApplicationFactory<Program> WithAutomationMcp(
        IntakeWebApplicationFactory factory) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Features:AutomationMcp", "true");
            builder.UseSetting("AutomationMcp:UseDevelopmentKeys", "true");
            builder.UseSetting("AutomationMcp:ClientId", ClientId);
            builder.UseSetting("AutomationMcp:ClientSecret", ClientSecret);
            builder.UseSetting("AutomationMcp:PublicOrigin", "http://localhost/");
            builder.UseSetting("AutomationMcp:RedirectUris", ConnectorRedirectUri);
            builder.UseSetting("AutomationMcp:RegistrationCacheSeconds", "0");
        });

    public static async Task<string> RequestTokenAsync(HttpClient client, string scope)
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

    public static async Task<HttpResponseMessage> PostMcpAsync(
        HttpClient client,
        string? accessToken,
        string payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, McpServer);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await client.SendAsync(request);
    }

    /// <summary>
    /// A tool refuses a token without its scope before anything runs
    /// (ADR-0087 decision 3): 200 with an error naming the scope, and an
    /// attributable security event, which <see cref="CountScopeDenialsAsync"/>
    /// counts.
    /// </summary>
    public static async Task AssertScopeDeniedAsync(HttpResponseMessage response, string scope)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonRpcAsync(response);
        Assert.Contains(scope, document.RootElement.ToString(), StringComparison.Ordinal);
    }

    public static Task<int> CountScopeDenialsAsync(IntakeWebApplicationFactory factory) =>
        factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'automation_scope_denied'
              AND Outcome = N'Denied'
              AND SubjectId = N'pegasus-automation'
            """);

    public static async Task<JsonDocument> ReadJsonRpcAsync(HttpResponseMessage response)
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

    public static async Task<JsonElement> ReadStructuredContentAsync(HttpResponseMessage response)
    {
        using var document = await ReadJsonRpcAsync(response);
        var result = document.RootElement.GetProperty("result");
        Assert.False(
            result.TryGetProperty("isError", out var isError) && isError.GetBoolean(),
            result.ToString());
        return result.GetProperty("structuredContent").Clone();
    }

    /// <summary>
    /// A successful tool result whole: its content blocks (what an MCP client
    /// shows its model) and its structured half.
    /// </summary>
    public static async Task<(JsonElement Content, JsonElement Structured)> ReadToolResultAsync(
        HttpResponseMessage response)
    {
        using var document = await ReadJsonRpcAsync(response);
        var result = document.RootElement.GetProperty("result");
        Assert.False(
            result.TryGetProperty("isError", out var isError) && isError.GetBoolean(),
            result.ToString());
        return (result.GetProperty("content").Clone(), result.GetProperty("structuredContent").Clone());
    }

    public static async Task<string> ReadErrorTextAsync(HttpResponseMessage response)
    {
        using var document = await ReadJsonRpcAsync(response);
        var result = document.RootElement.GetProperty("result");
        Assert.True(
            result.TryGetProperty("isError", out var isError) && isError.GetBoolean(),
            "Expected a refused tool call: " + result.ToString());
        return result.ToString();
    }

    public static string ToolsListPayload(int id) =>
        JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            method = "tools/list"
        });

    public static string ToolCallPayload(int id, string tool, object arguments) =>
        JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            method = "tools/call",
            @params = new { name = tool, arguments }
        });

    public static string Sha256Hex(byte[] content) => Convert.ToHexString(SHA256.HashData(content));

    /// <summary>
    /// Stages a document in parts (R203): begins the
    /// upload under the key, then sends every part with its own SHA-256.
    /// </summary>
    public static async Task<Guid> StageUploadAsync(
        HttpClient client,
        string token,
        byte[] document,
        string operationKey)
    {
        Guid uploadId;
        int partBytes;
        int partCount;
        using (var begin = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                1,
                "pegasus_upload_begin",
                new { totalBytes = document.Length, sha256 = Sha256Hex(document), operationKey })))
        {
            var ticket = await ReadStructuredContentAsync(begin);
            uploadId = ticket.GetProperty("uploadId").GetGuid();
            partBytes = ticket.GetProperty("partBytes").GetInt32();
            partCount = ticket.GetProperty("partCount").GetInt32();
        }

        for (var partNumber = 1; partNumber <= partCount; partNumber++)
        {
            var offset = (partNumber - 1) * partBytes;
            var part = document[offset..Math.Min(document.Length, offset + partBytes)];
            using var sent = await PostMcpAsync(
                client,
                token,
                ToolCallPayload(
                    1 + partNumber,
                    "pegasus_upload_part",
                    new { uploadId, partNumber, contentBase64 = Convert.ToBase64String(part), sha256 = Sha256Hex(part) }));
            await ReadStructuredContentAsync(sent);
        }

        return uploadId;
    }

    public static async Task<Guid> SeedAcceptedCaseAsync(
        WebApplicationFactory<Program> factory,
        CaseCompleteness? completeness = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        // MCP tests start from a processed receipt; source classification is
        // proved by the intake corpus tests, not this allocation setup.
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            services,
            CaseType.Inspection,
            QdosPrincipal.Code);
        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);
        await SeedPrincipalAsync(services);
        var outcome = await services.GetRequiredService<IAcceptIntake>()
            .ExecuteAsync(
                new(
                    receipt.Id,
                    0,
                    ActionActor.SystemWorker("mcp-ingress-integration"),
                    $"case-accept:{Guid.NewGuid():N}",
                    CaseType.Inspection,
                    QdosPrincipal.Code,
                    completeness ?? new(true, true)),
                CancellationToken.None);
        return outcome.Identity.CaseId;
    }

    public static async Task<(long CaseVersion, string LeaseToken)> BeginEditAsync(
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
                "pegasus_edit_begin",
                new
                {
                    recordKind = "Case",
                    recordId = caseId,
                    expectedVersion,
                    operationKey = $"mcp:lease-{rpcId}-{Guid.NewGuid():N}"
                }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lease = await ReadStructuredContentAsync(response);
        return (lease.GetProperty("version").GetInt64(), lease.GetProperty("editLeaseToken").GetString()!);
    }

    public static async Task<long> GetWorkflowVersionAsync(
        WebApplicationFactory<Program> factory,
        Guid caseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var workflow = await scope.ServiceProvider
            .GetRequiredService<ICaseWorkflowQueries>()
            .GetAsync(caseId, CancellationToken.None)
            ?? throw new InvalidOperationException("The seeded case has no workflow.");
        return workflow.Version;
    }

    /// <summary>
    /// Walks Not ready → Review through the store's gated
    /// <see cref="ICaseWorkflowStore.ReturnToReviewAsync"/>: neither the Core
    /// command nor the store admits Not ready, so the state is first set to With
    /// Engineer in SQL, which the store does admit.
    /// </summary>
    public static async Task EnsureInReviewAsync(
        WebApplicationFactory<Program> factory,
        HttpClient client,
        string token,
        Guid caseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var workflow = await scope.ServiceProvider
            .GetRequiredService<ICaseWorkflowQueries>()
            .GetAsync(caseId, CancellationToken.None)
            ?? throw new InvalidOperationException("The seeded case has no workflow.");
        if (workflow.State == CaseLifecycleState.Review)
        {
            return;
        }

        Assert.Equal(CaseLifecycleState.NotReady, workflow.State);
        var lease = await BeginEditAsync(client, token, caseId, workflow.Version, rpcId: 900);
        // Return to Review asks its state rule (With Engineer or Report sent) before the
        // readiness gate, so the fixture puts the Case in a state that passes it.
        await using (var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE CaseWorkflows SET State = {nameof(CaseLifecycleState.ReportPreparation)} WHERE CaseId = {caseId}");
        }

        await scope.ServiceProvider.GetRequiredService<ICaseWorkflowStore>()
            .ReturnToReviewAsync(
                new(
                    caseId,
                    lease.CaseVersion,
                    ActionActor.Automation(ClientId),
                    Guid.NewGuid().ToString("N"),
                    "Integration fixture returned the case to Review.",
                    lease.LeaseToken),
                CancellationToken.None);
        // The Automation lease stands after its write, so the fixture ends it.
        await scope.ServiceProvider.GetRequiredService<CaseEditLeases>()
            .ReleaseAsync(
                new(caseId, ActionActor.Automation(ClientId), Guid.NewGuid().ToString("N"), lease.LeaseToken),
                CancellationToken.None);
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
            $"INSERT INTO Organizations (Role, Id, Name, Version) VALUES (N'principal', {organizationId}, {"MCP ingress organization"}, {0L})");
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
}
