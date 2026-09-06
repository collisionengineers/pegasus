using System.Net;
using System.Net.Http.Headers;
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

    public static async Task<Guid> SeedAcceptedCaseAsync(
        WebApplicationFactory<Program> factory,
        CaseCompleteness? completeness = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var email = IntakeTestEvidence.CreateEmail(
            $"mcp-ingress-{Guid.NewGuid():N}.eml",
            "QDOS instruction\r\nClaimant Name: MCP Ingress\r\nClaim Number: MCP-001\r\nVehicle Registration: AB12 CDE");
        var receipt = await services.GetRequiredService<ProcessIntake>()
            .ExecuteAsync(
                new(
                    email.FileName,
                    email.MediaType,
                    email.Content,
                    SeedUtcNow,
                    "mcp-ingress-test",
                    new(
                        IntakeSourceChannel.ManualUpload,
                        $"mcp-ingress-source:{Guid.NewGuid():N}")),
                CancellationToken.None);
        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);
        await SeedPrincipalAsync(services);
        var outcome = await services.GetRequiredService<IAcceptIntake>()
            .ExecuteAsync(
                new(
                    receipt.Id,
                    0,
                    ActionActor.SystemWorker("mcp-ingress-integration"),
                    $"case-accept:{Guid.NewGuid():N}",
                    "Integration fixture confirmed complete intake evidence.",
                    CaseType.Inspection,
                    QdosPrincipal.Code,
                    completeness ?? new(true, true, true, true)),
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
                "pegasus_case_edit_begin",
                new
                {
                    caseId,
                    expectedVersion,
                    operationKey = $"mcp:lease-{rpcId}-{Guid.NewGuid():N}"
                }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lease = await ReadStructuredContentAsync(response);
        return (lease.GetProperty("caseVersion").GetInt64(), lease.GetProperty("leaseToken").GetString()!);
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
    /// Walks Not ready → Review through <see cref="IReturnCaseToReview"/>,
    /// the same Core command staff use. Does not write workflow state in SQL.
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
        await scope.ServiceProvider.GetRequiredService<IReturnCaseToReview>()
            .ExecuteAsync(
                new(
                    caseId,
                    lease.CaseVersion,
                    ActionActor.Automation(ClientId),
                    Guid.NewGuid().ToString("N"),
                    "Integration fixture returned the case to Review.",
                    lease.LeaseToken,
                    new(true, true, "mcp-ingress-review-readiness")),
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
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {"MCP ingress organization"}, {0L})");
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
}
