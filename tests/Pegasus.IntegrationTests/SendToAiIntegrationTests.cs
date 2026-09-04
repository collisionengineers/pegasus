using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// ENG-025 / FRD-11 § AI Job List: the Assessment page's Send to Claude
/// queues an Estimate-kind AI job through <see cref="ICreateAiJob"/> (the
/// AUTO-011 ledger superseded the AI-09 push hand-off on this surface). The
/// switch-off gate stays visible as the control's condition, and the
/// handler surfaces Core's refusal sentences unchanged. The ledger's own
/// state machine is owned by the AUTO-011 Core tests; the channel connector
/// seam is proven directly in SendToAiConnectorAdministrationTests, whose
/// fixtures live in this partial class.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class SendToAiIntegrationTests
{
    private const string ChannelToken = "integration-test-send-to-ai-channel-token-0123456789";
    private static readonly DateTimeOffset FixedUtcNow = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    internal static WebApplicationFactory<Program> WithSendToAi(
        IntakeWebApplicationFactory factory,
        string channelBaseUrl) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Features:SendToAi", "true");
            builder.UseSetting("SendToAi:ChannelBaseUrl", channelBaseUrl);
            builder.UseSetting("SendToAi:ChannelToken", ChannelToken);
            builder.UseSetting("SendToAi:TimeoutSeconds", "5");
        });

    /// <summary>
    /// Seeds an accepted QDOS case with a current export proxy: the state
    /// the Assessment gate and the AI seams share.
    /// </summary>
    internal static async Task<Guid> SeedAcceptedCaseAsync(WebApplicationFactory<Program> factory)
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
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var workflowVersion = await context.CaseWorkflows
            .Where(item => item.CaseId == outcome.Identity.CaseId)
            .Select(item => item.Version)
            .SingleAsync();
        context.EvaFirstHandoffProxies.Add(new()
        {
            CaseId = outcome.Identity.CaseId,
            AdapterKey = "integration-test",
            AdapterVersion = "1",
            RecordedAtUtc = FixedUtcNow,
            LatestExportedWorkflowVersion = workflowVersion,
            ActorSubjectId = "send-to-ai-integration"
        });
        await context.SaveChangesAsync();
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

    /// <summary>
    /// A local double for the channel connector: records every request,
    /// asserts nothing itself, and serves the scripted /send and /events
    /// responses on a loopback port (SendToAiConnectorAdministrationTests).
    /// </summary>
    internal sealed class FakeChannelReceiver : IAsyncDisposable
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

    [Fact]
    public async Task ASwitchedOffControlStatesTheConditionAndIsNotOffered()
    {
        var caseId = Guid.NewGuid();
        using var factory = Compose(caseId, controlEnabled: false);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        Assert.DoesNotContain("data-dialog=\"send-to-claude-dialog\"", html, StringComparison.Ordinal);
        // The condition is read off the Send to Claude control itself: the
        // record bar carries several gated seams, so the first
        // data-condition on the page is not necessarily this one.
        var sendGate = Regex.Match(
            html,
            "<span class=\"gated\" data-condition=\"(?<value>[^\"]+)\">[^<]*<button[^>]*>(?:(?!</button>).)*?Send to Claude",
            RegexOptions.Singleline);
        Assert.True(sendGate.Success, "Send to Claude renders as a gated, disabled control.");
        Assert.Contains(
            "disabled by an Administrator",
            sendGate.Groups["value"].Value,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendingRecordsAnEstimateJobWithTheDirectionAndTarget()
    {
        var caseId = Guid.NewGuid();
        using var factory = Compose(caseId);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        Assert.Contains("data-dialog=\"send-to-claude-dialog\"", html, StringComparison.Ordinal);
        Assert.Contains("data-range-base=\"9000\"", html, StringComparison.Ordinal);
        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=SendToClaude&section=estimate",
            Form(
                AntiforgeryValue(html),
                ("operationKey", InputValue(html, "operationKey")),
                ("direction", "Target the repair, not the paint."),
                ("targetPercent", "80")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var command = Assert.Single(((RecordingCreateAiJob)GetJobFactory(factory)).Commands);
        Assert.Equal(AiJobKind.Estimate, command.Kind);
        Assert.Equal(caseId, command.SubjectId);
        Assert.Equal("QDOS-2026-00042", command.SubjectReference);
        Assert.Equal("Target the repair, not the paint.", command.Instruction);
        Assert.Equal(80, command.TargetPercentOfEngineerValue);

        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        Assert.Contains("Sent to Claude", afterHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEmptyDirectionFallsBackToANamedInstruction()
    {
        var caseId = Guid.NewGuid();
        using var factory = Compose(caseId);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=SendToClaude&section=estimate",
            Form(
                AntiforgeryValue(html),
                ("operationKey", InputValue(html, "operationKey")),
                ("direction", "   "),
                ("targetPercent", "75")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var command = Assert.Single(((RecordingCreateAiJob)GetJobFactory(factory)).Commands);
        Assert.Equal("Draft an estimate for case QDOS-2026-00042.", command.Instruction);
    }

    /// <summary>
    /// Core owns the refusal (no confirmed Engineer's Value, wrong state,
    /// switch off); the page surfaces the sentence it is given rather than
    /// rewriting it.
    /// </summary>
    [Fact]
    public async Task ACoreRefusalIsSurfacedUnchanged()
    {
        var caseId = Guid.NewGuid();
        using var factory = Compose(
            caseId,
            refusal: "An estimate job needs a confirmed Engineer's Value on the case.");
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=SendToClaude&section=estimate",
            Form(
                AntiforgeryValue(html),
                ("operationKey", InputValue(html, "operationKey")),
                ("direction", "Draft the estimate"),
                ("targetPercent", "80")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        // Decoded first: Razor encodes the apostrophe in "Engineer's", and
        // the claim is that Core's sentence reaches the operator unrewritten.
        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        Assert.Contains(
            "An estimate job needs a confirmed Engineer's Value on the case.",
            WebUtility.HtmlDecode(afterHtml),
            StringComparison.Ordinal);
    }

    private static ICreateAiJob GetJobFactory(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ICreateAiJob>();
    }

    private static WebApplicationFactory<Program> Compose(
        Guid caseId,
        bool controlEnabled = true,
        string? refusal = null)
    {
        var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var source = new FakeGetCase(caseId);
        var jobs = new RecordingCreateAiJob(refusal);
        return baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IGetAssessmentAccess>();
                services.RemoveAll<IGetAssessmentWorkspace>();
                services.RemoveAll<ICreateAiJob>();
                services.RemoveAll<ISendToAiControl>();
                services.AddSingleton<IGetCase>(source);
                services.AddSingleton<IGetAssessmentAccess>(new FakeGetAssessmentAccess());
                services.AddSingleton<IGetAssessmentWorkspace>(source);
                services.AddSingleton<ICreateAiJob>(jobs);
                services.AddSingleton<ISendToAiControl>(new FixedSendToAiControl(controlEnabled));
            }));
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

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
    /// One recording fake for the page's job creation: it remembers the
    /// command, or throws the refusal sentence the real Core use case
    /// would, so the page's surfacing is exercised without the ledger's
    /// own state machine.
    /// </summary>
    private sealed class RecordingCreateAiJob(string? refusal) : ICreateAiJob
    {
        public List<CreateAiJobCommand> Commands { get; } = [];

        public Task<AiJobRecord> ExecuteAsync(
            CreateAiJobCommand command,
            CancellationToken cancellationToken = default)
        {
            if (refusal is not null)
            {
                throw new InvalidOperationException(refusal);
            }
            Commands.Add(command);
            return Task.FromResult(new AiJobRecord(
                JobId: Guid.NewGuid(),
                Kind: command.Kind,
                SubjectKind: AiJobSubjectKind.Case,
                SubjectId: command.SubjectId,
                SubjectReference: command.SubjectReference ?? string.Empty,
                Instruction: command.Instruction,
                TargetPercentOfEngineerValue: command.TargetPercentOfEngineerValue,
                EngineerValueAtSend: null,
                State: AiJobState.Queued,
                CreatedByKind: command.Actor.Kind,
                CreatedBy: command.Actor.SubjectId,
                CreatedAtUtc: DateTimeOffset.UtcNow,
                ExpiresAtUtc: DateTimeOffset.UtcNow.AddHours(6),
                TakenBy: null,
                TakenAtUtc: null,
                LeaseExpiresAtUtc: null,
                ProgressNote: null,
                ResultKind: null,
                ResultReference: null,
                ResultText: null,
                ClosedAtUtc: null,
                ClosureReason: null,
                Version: 1));
        }
    }

    private sealed class FixedSendToAiControl(bool enabled) : ISendToAiControl
    {
        public Task<bool> IsEnabledAsync(CancellationToken cancellationToken) =>
            Task.FromResult(enabled);

        public Task<bool> SetEnabledAsync(
            bool enabled,
            ActionActor actor,
            string reason,
            string operationKey,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeGetCase(Guid caseId) : IGetCase, IGetAssessmentWorkspace
    {
        public Task<CaseDetails?> ExecuteAsync(GetCaseQuery query, CancellationToken cancellationToken)
        {
            if (query.CaseId != caseId)
            {
                return Task.FromResult<CaseDetails?>(null);
            }

            var identity = new CaseIdentity(caseId, "QDOS", 2026, 42, "QDOS-2026-00042");
            var workflow = new CaseWorkflowRecord(
                caseId, identity, CaseLifecycleState.ReportPreparation, null, null,
                null, null, null, null, null, 7);
            var summary = new CaseSearchItem(
                caseId, identity.Reference, null, CaseType.Inspection, "Approved Principal",
                workflow.State, null, "AB12CDE", "Alex Example", "P-100",
                DateTimeOffset.UtcNow, new DateOnly(2026, 8, 1), "Email", DateTimeOffset.UtcNow);
            CaseDetails details = new(
                summary, workflow, null, [], null, CaseCustodyState.Pending, [], [], []);
            return Task.FromResult<CaseDetails?>(details);
        }

        public async Task<AssessmentWorkspace?> ExecuteAsync(
            GetAssessmentWorkspaceQuery query,
            CancellationToken cancellationToken = default)
        {
            var details = await ExecuteAsync(new GetCaseQuery(query.CaseId, query.Actor), cancellationToken);
            if (details is null)
            {
                return null;
            }
            IReadOnlyList<AssessmentFieldValue> fields =
            [
                new(
                    AssessmentVocabulary.ValueEngineer,
                    "9000",
                    ActorKind.Staff,
                    "engineer-1",
                    DateTimeOffset.UtcNow,
                    "engineer-1",
                    DateTimeOffset.UtcNow)
            ];
            var assessment = new CaseAssessmentProjection(
                caseId, "QDOS-2026-00042", 7, CaseLifecycleState.ReportPreparation, null,
                fields, [], new(null, null, null, null, null, null, null, null, null));
            return AssessmentWorkspaceTestData.Create(details, assessment);
        }
    }
}
