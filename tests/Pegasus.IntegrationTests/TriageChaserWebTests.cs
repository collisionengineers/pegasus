using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Core.Triage;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class QdosTriageIntegrationTests
{
    private static readonly DateTimeOffset ChaserNowUtc = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetWhenTriageOriginIsMailboxRendersChaserSectionWithPrefilledValues()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = ConfigureStaffSend(baseFactory, send);
        using var client = factory.CreateClient();

        var fixture = await SeedMailboxTriageAsync(factory);

        using var response = await client.GetAsync($"/Triage/{fixture.TriageId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Chaser correspondence", html, StringComparison.Ordinal);
        Assert.Contains("Send chaser", html, StringComparison.Ordinal);
        Assert.Contains("reply@example.invalid", html, StringComparison.Ordinal);
        Assert.Contains("Re: Originating triage subject", html, StringComparison.Ordinal);
        Assert.Equal(0, send.SendCalls);
    }

    [Fact]
    public async Task GetWhenTriageOriginIsNotMailboxOmitsChaserSection()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = ConfigureStaffSend(baseFactory, send);
        using var client = factory.CreateClient();

        var fixture = await SeedNonMailboxTriageAsync(factory);

        using var response = await client.GetAsync($"/Triage/{fixture.TriageId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Chaser correspondence", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Send chaser", html, StringComparison.Ordinal);
        Assert.Equal(0, send.SendCalls);
    }

    [Fact]
    public async Task SendChaserWhenTriageOriginIsNotMailboxFailsValidationAndDoesNotSend()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = ConfigureStaffSend(baseFactory, send);
        using var client = factory.CreateClient();

        var fixture = await SeedNonMailboxTriageAsync(factory);
        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/Triage/{fixture.TriageId}?handler=SendChaser");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["expectedVersion"] = fixture.Version.ToString(CultureInfo.InvariantCulture),
            ["operationKey"] = $"retained:{Guid.NewGuid():N}:{Guid.NewGuid():N}",
            ["to"] = "provider@example.invalid",
            ["subject"] = "Chaser subject",
            ["body"] = "Chaser message body."
        });
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("A chaser reply can only be sent for mailbox intake.", html, StringComparison.Ordinal);
        Assert.Equal(0, send.SendCalls);
    }

    [Fact]
    public async Task SendChaserWhenActorLacksCaseworkRightReturnsForbidden()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = ConfigureStaffSend(baseFactory, send);
        using var client = factory.CreateClient();

        var fixture = await SeedMailboxTriageAsync(factory);
        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/Triage/{fixture.TriageId}?handler=SendChaser");
        request.Headers.Add("X-Test-Roleless", "1");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["expectedVersion"] = fixture.Version.ToString(CultureInfo.InvariantCulture),
            ["operationKey"] = $"retained:{fixture.RetainedMessageId:N}:{Guid.NewGuid():N}",
            ["to"] = "provider@example.invalid",
            ["subject"] = "Chaser subject",
            ["body"] = "Chaser message body."
        });
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, send.SendCalls);
    }

    [Fact]
    public async Task SendChaserWhenValidSendsChaserReplyAndRedirects()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = ConfigureStaffSend(baseFactory, send);
        using var client = factory.CreateClient();

        var fixture = await SeedMailboxTriageAsync(factory);
        var (operationKey, token) = await TriageChaserTokensAsync(client, $"/Triage/{fixture.TriageId}");

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/Triage/{fixture.TriageId}?handler=SendChaser");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["expectedVersion"] = fixture.Version.ToString(CultureInfo.InvariantCulture),
            ["operationKey"] = operationKey,
            ["to"] = "reply@example.invalid",
            ["cc"] = "supervisor@example.invalid",
            ["subject"] = "Re: Originating triage subject",
            ["body"] = "Please supply the requested additional vehicle details."
        });
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Triage/{fixture.TriageId:D}", response.Headers.Location?.OriginalString);

        Assert.Equal(1, send.SendCalls);
        var command = Assert.Single(send.Commands);
        Assert.Equal(StaffMailPurpose.TriageChaser, command.Purpose);
        Assert.Equal(fixture.TriageId, command.ContextId);
        Assert.Equal(fixture.Version, command.ExpectedContextVersion);
        Assert.Equal(StaffMailComposeMode.Reply, command.ComposeMode);
        Assert.NotNull(command.OriginalMessage);
        Assert.Equal(fixture.RetainedMessageId, command.OriginalMessage.RetainedMessageId);
        Assert.Equal(fixture.MailboxId, command.OriginalMessage.ApprovedMailboxId);
        Assert.Equal(operationKey, command.OperationKey);
        Assert.Equal("reply@example.invalid", Assert.Single(command.To).Address);
        Assert.Equal("supervisor@example.invalid", Assert.Single(command.Cc).Address);
        Assert.Equal("Re: Originating triage subject", command.Subject);
        Assert.Equal("Please supply the requested additional vehicle details.", command.Body);
    }

    [Fact]
    public async Task SendChaserWhenStaleTriageVersionReturnsErrorWithoutSending()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = ConfigureStaffSend(baseFactory, send);
        using var client = factory.CreateClient();

        var fixture = await SeedMailboxTriageAsync(factory);
        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/Triage/{fixture.TriageId}?handler=SendChaser");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["expectedVersion"] = (fixture.Version + 5).ToString(CultureInfo.InvariantCulture),
            ["operationKey"] = $"retained:{fixture.RetainedMessageId:N}:{Guid.NewGuid():N}",
            ["to"] = "reply@example.invalid",
            ["subject"] = "Chaser subject",
            ["body"] = "Chaser body."
        });
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("The triage record changed while this was being prepared.", html, StringComparison.Ordinal);
        Assert.Equal(0, send.SendCalls);
    }

    [Fact]
    public async Task SendChaserWhenOperationKeyIsInvalidFailsValidationWithoutSending()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = ConfigureStaffSend(baseFactory, send);
        using var client = factory.CreateClient();

        var fixture = await SeedMailboxTriageAsync(factory);
        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/Triage/{fixture.TriageId}?handler=SendChaser");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["expectedVersion"] = fixture.Version.ToString(CultureInfo.InvariantCulture),
            ["operationKey"] = "not-a-valid-retained-key",
            ["to"] = "reply@example.invalid",
            ["subject"] = "Chaser subject",
            ["body"] = "Chaser body."
        });
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("The send operation key is invalid or has expired.", html, StringComparison.Ordinal);
        Assert.Equal(0, send.SendCalls);
    }

    [Fact]
    public async Task SendChaserWhenActiveOperationExistsReportsSendConflict()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = ConfigureStaffSend(baseFactory, send);
        using var client = factory.CreateClient();

        var fixture = await SeedMailboxTriageAsync(factory);
        var (firstKey, token) = await TriageChaserTokensAsync(client, $"/Triage/{fixture.TriageId}");

        // First send establishes an active (Submitted) operation
        send.NextState = StaffMailState.Submitted;
        using (var firstRequest = new HttpRequestMessage(
            HttpMethod.Post, $"/Triage/{fixture.TriageId}?handler=SendChaser"))
        {
            firstRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["expectedVersion"] = fixture.Version.ToString(CultureInfo.InvariantCulture),
                ["operationKey"] = firstKey,
                ["to"] = "reply@example.invalid",
                ["subject"] = "Chaser subject",
                ["body"] = "Chaser body 1."
            });
            using var firstResponse = await client.SendAsync(firstRequest);
            Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);
        }
        Assert.Equal(1, send.SendCalls);

        // Second send with a fresh operation key for the same retained message must observe the send conflict
        var freshKey = $"retained:{fixture.RetainedMessageId:N}:{Guid.NewGuid():N}";
        using (var secondRequest = new HttpRequestMessage(
            HttpMethod.Post, $"/Triage/{fixture.TriageId}?handler=SendChaser"))
        {
            secondRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["expectedVersion"] = fixture.Version.ToString(CultureInfo.InvariantCulture),
                ["operationKey"] = freshKey,
                ["to"] = "reply@example.invalid",
                ["subject"] = "Chaser subject",
                ["body"] = "Chaser body 2."
            });
            using var secondResponse = await client.SendAsync(secondRequest);
            Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
            var html = await secondResponse.Content.ReadAsStringAsync();
            Assert.Contains("The existing correspondence operation must finish or be resolved before another action.", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task SendChaserWhenNonActiveOperationInvalidOperationThrownPropagatesException()
    {
        var send = new RecordingStaffMailSend
        {
            ThrowOnSend = new InvalidOperationException("Underlying provider failure.")
        };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = ConfigureStaffSend(baseFactory, send);
        using var client = factory.CreateClient();

        var fixture = await SeedMailboxTriageAsync(factory);
        var (operationKey, token) = await TriageChaserTokensAsync(client, $"/Triage/{fixture.TriageId}");

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/Triage/{fixture.TriageId}?handler=SendChaser");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["expectedVersion"] = fixture.Version.ToString(CultureInfo.InvariantCulture),
            ["operationKey"] = operationKey,
            ["to"] = "reply@example.invalid",
            ["subject"] = "Chaser subject",
            ["body"] = "Chaser body."
        });
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Underlying provider failure.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("The existing correspondence operation must finish or be resolved before another action.", html, StringComparison.Ordinal);
        Assert.Equal(0, send.SendCalls);
    }

    [Fact]
    public async Task ReconcileChaserWhenValidInvokesReconcileAndRedirects()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = ConfigureStaffSend(baseFactory, send);
        using var client = factory.CreateClient();

        var fixture = await SeedMailboxTriageAsync(factory);
        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);
        var operationId = Guid.NewGuid();

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/Triage/{fixture.TriageId}?handler=ReconcileChaser");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["operationId"] = operationId.ToString("D"),
            ["expectedOperationVersion"] = "1"
        });
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Triage/{fixture.TriageId:D}", response.Headers.Location?.OriginalString);
        Assert.Equal(1, send.ReconcileCalls);
    }

    private static WebApplicationFactory<Program> ConfigureStaffSend(
        IntakeWebApplicationFactory baseFactory,
        RecordingStaffMailSend send) =>
        baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStaffMailSend>();
                services.AddSingleton<IStaffMailSend>(send);
                services.RemoveAll<IApprovedMailboxStore>();
                services.AddScoped<IApprovedMailboxStore>(provider =>
                    new StaffSendCapableMailboxStore(
                        provider.GetRequiredService<EfApprovedMailboxStore>(),
                        null));
            }));

    private static async Task<(string OperationKey, string AntiforgeryToken)> TriageChaserTokensAsync(
        HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        var operationKeyTag = Regex.Match(
            html, "<input[^>]*name=\"operationKey\"[^>]*value=\"(?<val>retained:[^\"]*)\"[^>]*>", RegexOptions.IgnoreCase);
        Assert.True(operationKeyTag.Success, "Details must render a retained operationKey field.");
        var operationKey = WebUtility.HtmlDecode(operationKeyTag.Groups["val"].Value);

        var antiforgeryTag = Regex.Match(
            html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"(?<val>[^\"]*)\"[^>]*>", RegexOptions.IgnoreCase);
        Assert.True(antiforgeryTag.Success, "Details must render an antiforgery token.");
        var antiforgeryToken = WebUtility.HtmlDecode(antiforgeryTag.Groups["val"].Value);

        return (operationKey, antiforgeryToken);
    }

    private sealed record TriageMailboxFixture(
        Guid TriageId,
        long Version,
        Guid ReceiptId,
        Guid RetainedMessageId,
        Guid MailboxId,
        string MailboxAddress);

    private static async Task<TriageMailboxFixture> SeedMailboxTriageAsync(
        WebApplicationFactory<Program> factory)
    {
        var receiptId = Guid.NewGuid();
        var externalToken = $"12:instructions{Guid.NewGuid():N}";
        const string mailboxAddress = "sc08-sender@collisionengineers.co.uk";
        const string immutableMessageId = "triage-msg-001";
        const string internetMessageId = "<triage-msg-001@example.invalid>";
        const string conversationId = "conv-triage-001";

        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var mailbox = await context.ApprovedMailboxes
                .SingleOrDefaultAsync(m => m.Address == mailboxAddress);
            Guid mailboxId;
            if (mailbox is null)
            {
                mailboxId = Guid.NewGuid();
                context.ApprovedMailboxes.Add(new()
                {
                    Id = mailboxId,
                    Address = mailboxAddress,
                    State = "Approved",
                    AllowStaffSend = true,
                    AllowSentEvidence = true,
                    AllowInboundIntake = true,
                    MailboxGeneration = 1,
                    Version = 1,
                    ActivatedAtUtc = ChaserNowUtc
                });
            }
            else
            {
                mailboxId = mailbox.Id;
            }

            if (!await context.ApprovedInboxPollStates.AnyAsync(p => p.ApprovedMailboxId == mailboxId))
            {
                context.ApprovedInboxPollStates.Add(new()
                {
                    ApprovedMailboxId = mailboxId,
                    MailboxAddress = mailboxAddress,
                    ScopeFingerprint = new string('A', 64),
                    ActivatedAtUtc = ChaserNowUtc.AddDays(-1),
                    DueAtUtc = ChaserNowUtc,
                    LastCompletedAtUtc = ChaserNowUtc.AddMinutes(-1)
                });
            }

            context.IntakeReceipts.Add(new()
            {
                Id = receiptId,
                SourceFileName = "triage-instruction.eml",
                MediaType = "message/rfc822",
                SourceLength = 2048,
                SourceHash = new string('A', 64),
                SourceChannel = "mailbox",
                ExternalReceiptToken = externalToken,
                ReceivedAtUtc = ChaserNowUtc,
                ProcessedAtUtc = ChaserNowUtc,
                SourceReaderKey = "protocol_reader",
                SourceReaderVersion = "1",
                Version = 0,
                Decision = EfIntakeReceiptStore.ToCode(IntakeDecision.NeedsSorting),
                DecisionReason = "Triage instruction received.",
                EvidenceJson = EfIntakeReceiptStore.SerializeEvidence([
                    new(IntakeEvidenceSource.SystemDefault, IntakeEvidenceStrength.Strong, IntakeEvidenceFinding.AcceptedTriageMatch, "triage_signal", "Accepted triage match", "qdos_triage", 1)
                ]),
                FieldsJson = EfIntakeReceiptStore.SerializeFields([]),
                OcrCandidatesJson = EfIntakeReceiptStore.SerializeEnvelope<IReadOnlyList<ScannedPdfOcrCandidate>>([])
            });

            await context.SaveChangesAsync();
        }

        var store = scope.ServiceProvider.GetRequiredService<EfRetainedMailboxMessageStore>();
        await store.RetainAsync(
            new(
                await GetMailboxIdAsync(factory, mailboxAddress),
                mailboxAddress,
                immutableMessageId,
                externalToken,
                ChaserNowUtc,
                2048,
                new string('B', 64),
                new(
                    "inbox",
                    conversationId,
                    internetMessageId,
                    "sender@example.invalid",
                    "Sender Name",
                    [mailboxAddress],
                    [],
                    ["reply@example.invalid"],
                    "Originating triage subject",
                    "Originating triage body.",
                    [],
                    IsRead: false),
                ChaserNowUtc),
            CancellationToken.None);

        await using var read = await contextFactory.CreateDbContextAsync();
        var messageId = await read.RetainedMailboxMessages
            .Where(item => item.ExternalReceiptToken == externalToken)
            .Select(item => item.Id)
            .SingleAsync();

        var createTriage = scope.ServiceProvider.GetRequiredService<ICreateTriageFromIntake>();
        var triageRecord = await createTriage.ExecuteAsync(
            new(
                new(receiptId, new(IntakeSourceChannel.Mailbox, externalToken), new string('A', 64), Guid.NewGuid()),
                "AB12CDE",
                new IntakeEvidence(
                    IntakeEvidenceSource.SystemDefault,
                    IntakeEvidenceStrength.Strong,
                    IntakeEvidenceFinding.AcceptedTriageMatch,
                    "triage_signal",
                    "Accepted triage match",
                    "qdos_triage",
                    1),
                ActionActor.SystemWorker("intake-processing"),
                $"triage-init:{Guid.NewGuid():N}"),
            CancellationToken.None);

        return new(
            triageRecord.Id,
            triageRecord.Version,
            receiptId,
            messageId,
            await GetMailboxIdAsync(factory, mailboxAddress),
            mailboxAddress);
    }

    private static async Task<TriageMailboxFixture> SeedNonMailboxTriageAsync(
        WebApplicationFactory<Program> factory)
    {
        var receiptId = Guid.NewGuid();
        var externalToken = $"upload:{Guid.NewGuid():N}";

        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            context.IntakeReceipts.Add(new()
            {
                Id = receiptId,
                SourceFileName = "upload.png",
                MediaType = "image/png",
                SourceLength = 1024,
                SourceHash = new string('C', 64),
                SourceChannel = "manual-upload",
                ExternalReceiptToken = externalToken,
                ReceivedAtUtc = ChaserNowUtc,
                ProcessedAtUtc = ChaserNowUtc,
                SourceReaderKey = "upload_reader",
                SourceReaderVersion = "1",
                Version = 0,
                Decision = EfIntakeReceiptStore.ToCode(IntakeDecision.NeedsSorting),
                DecisionReason = "Manual upload.",
                EvidenceJson = EfIntakeReceiptStore.SerializeEvidence([
                    new(IntakeEvidenceSource.SystemDefault, IntakeEvidenceStrength.Strong, IntakeEvidenceFinding.AcceptedTriageMatch, "triage_signal", "Accepted triage match", "qdos_triage", 1)
                ]),
                FieldsJson = EfIntakeReceiptStore.SerializeFields([]),
                OcrCandidatesJson = EfIntakeReceiptStore.SerializeEnvelope<IReadOnlyList<ScannedPdfOcrCandidate>>([])
            });
            await context.SaveChangesAsync();
        }

        var createTriage = scope.ServiceProvider.GetRequiredService<ICreateTriageFromIntake>();
        var triageRecord = await createTriage.ExecuteAsync(
            new(
                new(receiptId, new(IntakeSourceChannel.ManualUpload, externalToken), new string('C', 64), Guid.NewGuid()),
                "XY99ZZZ",
                new IntakeEvidence(
                    IntakeEvidenceSource.SystemDefault,
                    IntakeEvidenceStrength.Strong,
                    IntakeEvidenceFinding.AcceptedTriageMatch,
                    "triage_signal",
                    "Accepted triage match",
                    "qdos_triage",
                    1),
                ActionActor.SystemWorker("intake-processing"),
                $"triage-init:{Guid.NewGuid():N}"),
            CancellationToken.None);

        return new(
            triageRecord.Id,
            triageRecord.Version,
            receiptId,
            Guid.Empty,
            Guid.Empty,
            string.Empty);
    }

    private static async Task<Guid> GetMailboxIdAsync(
        WebApplicationFactory<Program> factory,
        string address)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IApprovedMailboxStore>();
        var mailbox = (await store.ListAsync(CancellationToken.None))
            .Single(item => item.Address == address);
        return mailbox.Id;
    }

    private sealed class StaffSendCapableMailboxStore(
        IApprovedMailboxStore inner,
        MailboxCapability? mailboxCapability) : IApprovedMailboxStore
    {
        public async Task<IReadOnlyList<ApprovedMailbox>> ListAsync(CancellationToken cancellationToken)
        {
            var mailboxes = await inner.ListAsync(cancellationToken);
            return mailboxes
                .Select(mailbox => mailbox.RouteScopes.Contains(ApprovedMailboxRouteScope.SentEvidence)
                    ? mailbox with
                    {
                        RouteScopes = [.. mailbox.RouteScopes, ApprovedMailboxRouteScope.StaffSend],
                        Generation = mailboxCapability is not null
                            && mailbox.Id == mailboxCapability.MailboxId
                                ? mailboxCapability.Generation
                                : mailbox.Generation > 0 ? mailbox.Generation : 1
                    }
                    : mailbox)
                .ToArray();
        }

        public Task<ApprovedMailbox> UpdateAsync(
            UpdateApprovedMailboxRequest request, CancellationToken cancellationToken) =>
            inner.UpdateAsync(request, cancellationToken);

        public Task<bool> IsApprovedAsync(
            string mailboxAddress, ApprovedMailboxRouteScope routeScope, CancellationToken cancellationToken) =>
            inner.IsApprovedAsync(mailboxAddress, routeScope, cancellationToken);
    }

    private sealed record MailboxCapability(Guid MailboxId, long Generation);

    private sealed class RecordingStaffMailSend : IStaffMailSend
    {
        private readonly object sync = new();
        private readonly List<StaffMailSendCommand> commands = [];
        private readonly Dictionary<string, StaffMailOperation> byOperationKey = [];
        private readonly Dictionary<Guid, StaffMailOperation> byOperationId = [];

        public int SendCalls
        {
            get { lock (sync) return commands.Count; }
        }

        public int ReconcileCalls { get; private set; }

        public IReadOnlyList<StaffMailSendCommand> Commands
        {
            get { lock (sync) return commands.ToArray(); }
        }

        public StaffMailState NextState { get; set; } = StaffMailState.Submitted;

        public DateTimeOffset NextPreparedAtUtc { get; set; } = ChaserNowUtc;

        public Exception? ThrowOnSend { get; set; }

        public Task<StaffMailOperation> SendAsync(
            StaffMailSendCommand command, CancellationToken cancellationToken)
        {
            if (ThrowOnSend is { } failure)
            {
                throw failure;
            }

            lock (sync)
            {
                if (byOperationKey.TryGetValue(command.OperationKey, out var existing))
                {
                    return Task.FromResult(existing);
                }

                var originalId = command.OriginalMessage?.RetainedMessageId;
                if (originalId is not null && commands.Any(item =>
                        item.OriginalMessage?.RetainedMessageId == originalId
                        && IsActive(byOperationKey[item.OperationKey].State)))
                {
                    throw new InvalidOperationException(
                        "An active correspondence operation already exists for this retained message.");
                }

                commands.Add(command);
                var preparedAtUtc = NextPreparedAtUtc;
                var operation = new StaffMailOperation(
                    Guid.NewGuid(),
                    NextState,
                    StaffMailAttemptStage.CreateDraft,
                    Version: 1,
                    preparedAtUtc,
                    SubmittedAtUtc: NextState is StaffMailState.Submitted or StaffMailState.Sent
                        ? preparedAtUtc : null,
                    ObservedSentAtUtc: NextState is StaffMailState.Sent ? preparedAtUtc : null,
                    FailureCode: NextState is StaffMailState.Failed ? "test_failure" : null,
                    command.ApprovedMailboxId,
                    command.ExpectedMailboxGeneration,
                    "test_hash",
                    AttemptRequestedAtUtc: null,
                    UploadSessionExpiresAtUtc: null);

                byOperationKey[command.OperationKey] = operation;
                byOperationId[operation.Id] = operation;
                return Task.FromResult(operation);
            }
        }

        public Task<StaffMailOperation?> GetAsync(
            ActionActor actor, Guid operationId, CancellationToken cancellationToken)
        {
            lock (sync)
            {
                byOperationId.TryGetValue(operationId, out var operation);
                return Task.FromResult(operation);
            }
        }

        public Task<StaffMailOperation?> GetLatestForOriginalAsync(
            ActionActor actor, Guid retainedMessageId, CancellationToken cancellationToken)
        {
            lock (sync)
            {
                var matching = commands
                    .Where(c => c.OriginalMessage?.RetainedMessageId == retainedMessageId)
                    .Select(c => byOperationKey[c.OperationKey])
                    .LastOrDefault();
                return Task.FromResult(matching);
            }
        }

        public Task<StaffMailOperation> ReconcileAsync(
            ActionActor actor, Guid operationId, long expectedVersion, CancellationToken cancellationToken)
        {
            ReconcileCalls++;
            lock (sync)
            {
                var current = byOperationId.TryGetValue(operationId, out var existing)
                    ? existing
                    : new StaffMailOperation(
                        operationId,
                        StaffMailState.Sent,
                        StaffMailAttemptStage.ObserveSent,
                        expectedVersion + 1,
                        ChaserNowUtc,
                        ChaserNowUtc,
                        ChaserNowUtc,
                        null,
                        Guid.NewGuid(),
                        1,
                        "test_hash",
                        null,
                        null);
                var updated = current with
                {
                    State = StaffMailState.Sent,
                    Version = expectedVersion + 1,
                    ObservedSentAtUtc = ChaserNowUtc
                };
                byOperationId[operationId] = updated;
                return Task.FromResult(updated);
            }
        }

        public Task<StaffMailOperation> CancelAsync(
            ActionActor actor, Guid operationId, long expectedVersion, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        private static bool IsActive(StaffMailState state) =>
            state is not StaffMailState.Sent
                and not StaffMailState.Failed
                and not StaffMailState.Cancelled;
    }
}
