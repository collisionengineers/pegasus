using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.AiWork;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class SendToAiIntegrationTests
{
    private const string ChannelToken = "integration-test-send-to-ai-channel-token-0123456789";
    private static readonly DateTimeOffset FixedUtcNow = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task GateOffExposesNoSendBehaviour()
    {
        using var factory = new IntakeWebApplicationFactory();
        var caseId = await SeedAcceptedCaseAsync(factory);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");
        Assert.Contains("Not available", html, StringComparison.Ordinal);
        Assert.Contains("not composed", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"send-to-claude-form\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GateOnRoundTripHandsOffReconcilesAndRecordsCorrelatedHistory()
    {
        await using var receiver = new FakeChannelReceiver();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithSendToAi(baseFactory, receiver.BaseUrl);
        var caseId = await SeedAcceptedCaseAsync(factory);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");
        Assert.Contains("send-to-claude-form", html, StringComparison.Ordinal);
        using (var response = await client.PostAsync(
            $"/Cases/{caseId:D}/Assessment?handler=Send",
            Form(
                AntiforgeryValue(html),
                ("operationKey", InputValue(html, "operationKey")))))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var request = Assert.Single(
            receiver.Requests,
            item => item.Path.StartsWith("/send", StringComparison.Ordinal));
        Assert.Equal($"Bearer {ChannelToken}", request.Authorization);
        Assert.Contains("\"schema_version\":1", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"case_reference\":", request.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("claimant", request.Body, StringComparison.OrdinalIgnoreCase);

        var sentHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");
        Assert.Contains(
            "Sent. Changes will appear on this case for your review.",
            sentHtml,
            StringComparison.Ordinal);

        Guid requestId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var record = await scope.ServiceProvider
                .GetRequiredService<IAiWorkRequestStore>()
                .GetLatestForCaseAsync(caseId, CancellationToken.None);
            Assert.NotNull(record);
            Assert.Equal(AiWorkRequestState.HandedOff, record!.State);
            requestId = record.RequestId;
        }

        receiver.ReplyStatus = "done";
        receiver.ReplyMessage = "Assessment recorded.";
        using (var response = await client.PostAsync(
            $"/Cases/{caseId:D}/Assessment?handler=Reconcile",
            Form(
                AntiforgeryValue(sentHtml),
                ("requestId", requestId.ToString("D")),
                ("operationKey", InputValue(sentHtml, "operationKey")))))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var completedHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");
        Assert.Contains("Claude has finished", completedHtml, StringComparison.Ordinal);

        using var database = factory.Services.CreateScope();
        var contextFactory = database.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var history = await context.ActionHistory.AsNoTracking()
            .Where(item => item.AggregateType == "ai_work_request")
            .ToArrayAsync();
        Assert.Equal(3, history.Length);
        Assert.All(history, entry =>
            Assert.Equal(requestId.ToString("D"), entry.CorrelationId));
        Assert.Contains(history, entry => entry.EventKind == "ai_work_request_completed");
    }

    [Fact]
    public async Task ARefusedChannelIsAVisibleFailureWithTheCaseUnchanged()
    {
        await using var receiver = new FakeChannelReceiver
        {
            SendStatusCode = 401,
            SendBody = """{"error":"unauthorized"}"""
        };
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithSendToAi(baseFactory, receiver.BaseUrl);
        var caseId = await SeedAcceptedCaseAsync(factory);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");
        using (var response = await client.PostAsync(
            $"/Cases/{caseId:D}/Assessment?handler=Send",
            Form(
                AntiforgeryValue(html),
                ("operationKey", InputValue(html, "operationKey")))))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var failedHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");
        Assert.Contains("Nothing was sent", failedHtml, StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var record = await scope.ServiceProvider
            .GetRequiredService<IAiWorkRequestStore>()
            .GetLatestForCaseAsync(caseId, CancellationToken.None);
        Assert.Equal(AiWorkRequestState.Failed, record!.State);
        var caseData = await scope.ServiceProvider
            .GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CancellationToken.None);
        Assert.Equal(0, caseData!.Version);
    }

    [Fact]
    public async Task TheAdministratorSwitchRefusesNewHandOffsAndIsRecorded()
    {
        await using var receiver = new FakeChannelReceiver();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = WithSendToAi(baseFactory, receiver.BaseUrl);
        var caseId = await SeedAcceptedCaseAsync(factory);
        using var client = CreateClient(factory);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var control = scope.ServiceProvider.GetRequiredService<ISendToAiControl>();
            var enabled = await control.SetEnabledAsync(
                enabled: false,
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                "Integration-test switch",
                Guid.NewGuid().ToString("N"),
                CancellationToken.None);
            Assert.False(enabled);
        }

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");
        Assert.Contains(
            "disabled by an Administrator",
            html,
            StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"send-to-claude-form\"", html, StringComparison.Ordinal);
        Assert.Empty(receiver.Requests);

        using var database = factory.Services.CreateScope();
        var contextFactory = database.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        Assert.Equal(1, await context.ActionHistory.AsNoTracking()
            .CountAsync(item => item.AggregateType == "send_to_ai"
                && item.EventKind == "send_to_ai_disabled"));
    }

    private static WebApplicationFactory<Program> WithSendToAi(
        IntakeWebApplicationFactory factory,
        string channelBaseUrl) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Features:SendToAi", "true");
            builder.UseSetting("SendToAi:ChannelBaseUrl", channelBaseUrl);
            builder.UseSetting("SendToAi:ChannelToken", ChannelToken);
            builder.UseSetting("SendToAi:TimeoutSeconds", "5");
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private static async Task<Guid> SeedAcceptedCaseAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var email = IntakeTestEvidence.CreateEmail(
            $"send-to-ai-{Guid.NewGuid():N}.eml",
            "QDOS instruction\r\nClaimant Name: Send Test\r\nClaim Number: STA-001\r\nVehicle Registration: AB12 CDE");
        var receipt = await services.GetRequiredService<ProcessIntake>()
            .ExecuteAsync(
                new(
                    email.FileName,
                    email.MediaType,
                    email.Content,
                    FixedUtcNow,
                    "send-to-ai-test",
                    new(
                        IntakeSourceChannel.ManualUpload,
                        $"send-to-ai-source:{Guid.NewGuid():N}")),
                CancellationToken.None);
        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);
        await SeedPrincipalAsync(services);
        var outcome = await services.GetRequiredService<IAcceptIntake>()
            .ExecuteAsync(
                new(
                    receipt.Id,
                    0,
                    ActionActor.SystemWorker("send-to-ai-integration"),
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
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {"Send to AI test organization"}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO OrganizationRoles (OrganizationId, Role) VALUES ({organizationId}, {"work_provider"})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {FixedUtcNow})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO Principals
                (Id, OrganizationId, Code, SequenceLineageId, PredecessorId, SuccessorId, IsActive, Version)
            VALUES
                ({principalId}, {organizationId}, {principalCode}, {lineageId}, NULL, NULL, {true}, {0L})
            """);
        await transaction.CommitAsync();
    }

    private static async Task<string> GetHtmlAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static FormUrlEncodedContent Form(
        string antiforgeryToken,
        params (string Name, string Value)[] values)
    {
        var fields = values.ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);
        fields["__RequestVerificationToken"] = antiforgeryToken;
        return new(fields);
    }

    private static string InputValue(string html, string name)
    {
        var tag = Regex.Match(
            html,
            $"<input[^>]*name=\\\"{Regex.Escape(name)}\\\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(tag.Success, $"The page must render '{name}'.");
        var value = ValueRegex().Match(tag.Value);
        Assert.True(value.Success, $"The field '{name}' must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    private static string AntiforgeryValue(string html)
    {
        var tag = AntiforgeryTagRegex().Match(html);
        Assert.True(tag.Success, "The page must render an antiforgery token.");
        var value = ValueRegex().Match(tag.Value);
        Assert.True(value.Success, "The antiforgery token must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryTagRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ValueRegex();

    /// <summary>
    /// A local double for the channel connector: records every request,
    /// asserts nothing itself, and serves the scripted /send and /events
    /// responses on a loopback port.
    /// </summary>
    private sealed class FakeChannelReceiver : IAsyncDisposable
    {
        private readonly HttpListener listener;
        private readonly Task loop;

        public FakeChannelReceiver()
        {
            var port = GetFreePort();
            BaseUrl = $"http://127.0.0.1:{port}";
            listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();
            loop = Task.Run(LoopAsync);
        }

        public string BaseUrl { get; }

        public int SendStatusCode { get; set; } = 202;

        public string SendBody { get; set; } = """{"status":"forwarded","schema_version":1}""";

        public string? ReplyStatus { get; set; }

        public string? ReplyMessage { get; set; }

        public ConcurrentQueue<(string Path, string? Authorization, string Body)> Requests { get; }
            = new();

        public async ValueTask DisposeAsync()
        {
            try
            {
                listener.Stop();
                listener.Close();
                await loop.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception)
            {
                // Shutdown noise from the listener loop is not a test signal.
            }
        }

        private async Task LoopAsync()
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync();
                }
                catch (Exception)
                {
                    return;
                }

                string body;
                using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                {
                    body = await reader.ReadToEndAsync();
                }

                var path = context.Request.RawUrl ?? "/";
                Requests.Enqueue((
                    path,
                    context.Request.Headers["Authorization"],
                    body));
                byte[] responseBytes;
                if (path.StartsWith("/send", StringComparison.Ordinal))
                {
                    context.Response.StatusCode = SendStatusCode;
                    responseBytes = Encoding.UTF8.GetBytes(SendBody);
                }
                else if (path.StartsWith("/events", StringComparison.Ordinal))
                {
                    context.Response.StatusCode = 200;
                    var requestId = context.Request.QueryString["request_id"] ?? string.Empty;
                    responseBytes = Encoding.UTF8.GetBytes(ReplyStatus is null
                        ? """{"events":[]}"""
                        : System.Text.Json.JsonSerializer.Serialize(new
                        {
                            events = new[]
                            {
                                new
                                {
                                    request_id = requestId,
                                    schema_version = 1,
                                    status = "replied",
                                    reply = new
                                    {
                                        status = ReplyStatus,
                                        message = ReplyMessage,
                                        replied_at = "2031-05-06T11:00:00Z"
                                    }
                                }
                            }
                        }));
                }
                else
                {
                    context.Response.StatusCode = 404;
                    responseBytes = [];
                }

                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(responseBytes);
                context.Response.Close();
            }
        }

        private static int GetFreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }
    }
}
