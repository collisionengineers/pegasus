using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
internal static partial class AutomationMcpTestSupport
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

    // The staff cookies (identity, antiforgery) are Secure, so the browser
    // half of an authorization-code flow talks to the test host over https
    // like the case web tests do; the connector half uses the same origin so
    // tokens issued and validated by the local server share one issuer.
    public static HttpClient CreateBrowser(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });

    public static HttpClient CreateConnector(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
            BaseAddress = new Uri("https://localhost")
        });

    public static (string Verifier, string Challenge) Pkce()
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static Dictionary<string, string?> ParseQuery(Uri uri)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var key = Uri.UnescapeDataString(separator < 0 ? pair : pair[..separator]);
            values[key] = separator < 0 ? string.Empty : Uri.UnescapeDataString(pair[(separator + 1)..]);
        }

        return values;
    }

    /// <summary>
    /// Re-posts exactly what the page rendered between two markers: the
    /// echoed OAuth parameters, the operation key, the antiforgery token and
    /// any scope tick boxes, every one as rendered (ticked) unless the test
    /// unticks some.
    /// </summary>
    public static FormUrlEncodedContent RenderedForm(
        string html,
        string startMarker,
        string endMarker,
        IReadOnlyCollection<string>? tickedScopes = null)
    {
        var start = html.IndexOf(startMarker, StringComparison.Ordinal);
        var end = start < 0 ? -1 : html.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"The form marked {startMarker} was not found.");
        var panel = html[start..end];
        var fields = new List<KeyValuePair<string, string>>();
        foreach (Match input in InputTag().Matches(panel))
        {
            var tag = input.Value;
            var hidden = tag.Contains("type=\"hidden\"", StringComparison.Ordinal);
            var tickBox = tag.Contains("type=\"checkbox\"", StringComparison.Ordinal);
            if (!hidden && !tickBox)
            {
                continue;
            }

            var name = NameAttribute().Match(tag);
            var value = ValueAttribute().Match(tag);
            if (!name.Success || !value.Success)
            {
                continue;
            }

            var decodedValue = WebUtility.HtmlDecode(value.Groups["v"].Value);
            if (tickBox)
            {
                Assert.Contains(" checked", tag, StringComparison.Ordinal);
                if (tickedScopes is not null && !tickedScopes.Contains(decodedValue, StringComparer.Ordinal))
                {
                    continue;
                }
            }

            fields.Add(new(WebUtility.HtmlDecode(name.Groups["v"].Value), decodedValue));
        }

        Assert.Contains(fields, field => field.Key == "__RequestVerificationToken");
        Assert.Contains(fields, field => field.Key == "OperationKey");
        return new FormUrlEncodedContent(fields);
    }

    [GeneratedRegex("<input[^>]*>")]
    private static partial Regex InputTag();

    [GeneratedRegex(@"\sname=""(?<v>[^""]+)""")]
    private static partial Regex NameAttribute();

    [GeneratedRegex(@"\svalue=""(?<v>[^""]*)""")]
    private static partial Regex ValueAttribute();

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

    /// <param name="caseType">The type the Case is accepted as; an Inspection unless a test names another.</param>
    /// <param name="principalAuditWorkflowCode">
    /// The Audit workflow the Principal is given before the Case is accepted,
    /// which a standalone Audit takes (R1138); the Principal's own when null.
    /// </param>
    public static async Task<Guid> SeedAcceptedCaseAsync(
        WebApplicationFactory<Program> factory,
        CaseCompleteness? completeness = null,
        CaseType caseType = CaseType.Inspection,
        string? principalAuditWorkflowCode = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        // MCP tests start from a processed receipt; source classification is
        // proved by the intake corpus tests, not this allocation setup.
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            services,
            caseType,
            QdosPrincipal.Code);
        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);
        await SeedPrincipalAsync(services);
        if (principalAuditWorkflowCode is not null)
        {
            await using var context = await services
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
                .CreateDbContextAsync();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Principals SET AuditWorkflow = {principalAuditWorkflowCode} WHERE Code = {QdosPrincipal.Code}");
        }

        var outcome = await services.GetRequiredService<IAcceptIntake>()
            .ExecuteAsync(
                new(
                    receipt.Id,
                    0,
                    ActionActor.SystemWorker("mcp-ingress-integration"),
                    $"case-accept:{Guid.NewGuid():N}",
                    caseType,
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
