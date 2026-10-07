using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Caller-equivalent evidence for the Automation Actor's outward sending
/// (ADR-0064 phase 4): real HTTP against the gated /mcp surface and real
/// LocalDB persistence. The send boundaries themselves are recorded, so each
/// test proves the tool hands the staff command the Automation Actor and what
/// staff would choose; the boundaries' own persistence is proved by
/// StaffMailSendPersistenceTests and the report delivery tests.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class AutomationSendIngressTests
{
    private const string SendScopes = "automation.cases automation.send";

    [Fact]
    public async Task SendingNeedsTheSendScope()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        // Every casework scope but sending: a grant can hold casework without sending.
        var token = await RequestTokenAsync(client, AllScopes + " automation.jobs");

        using (var mail = await PostMcpAsync(client, token, ToolCallPayload(1, "pegasus_mail_send", new
        {
            mode = "new",
            caseId,
            expectedVersion = 0,
            subject = "Subject",
            body = "Body",
            operationKey = "mcp:no-send-scope-mail",
            to = new[] { "handler@principal.example" },
        })))
        {
            using var document = await ReadJsonRpcAsync(mail);
            Assert.Contains("automation.send", document.RootElement.ToString(), StringComparison.Ordinal);
        }
        using (var report = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_report_send", new
        {
            caseId,
            expectedVersion = 0,
            operationKey = "mcp:no-send-scope-report",
            coveringMessage = "Please find our report attached.",
        })))
        {
            using var document = await ReadJsonRpcAsync(report);
            Assert.Contains("automation.send", document.RootElement.ToString(), StringComparison.Ordinal);
        }

        Assert.Equal(2, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'automation_scope_denied'
              AND Outcome = N'Denied'
              AND SubjectId = N'pegasus-automation'
            """));
    }

    [Fact]
    public async Task ReportSendHandsTheStaffCommandTheAutomationActorAndTheChosenDelivery()
    {
        var generationId = Guid.NewGuid();
        var sends = new RecordingReportSend();
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISendCaseReport>();
                services.AddSingleton<ISendCaseReport>(sends);
                WithCurrentGeneration(services, generationId);
            }));
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, SendScopes);
        await EnsureInReviewAsync(mcpFactory, client, token, caseId);
        var version = await GetWorkflowVersionAsync(mcpFactory, caseId);

        using (var badAttach = await PostMcpAsync(client, token, ToolCallPayload(10, "pegasus_report_send", new
        {
            caseId,
            expectedVersion = version,
            operationKey = "mcp:report-send-bad-attach",
            coveringMessage = "Please find our report attached.",
            attach = new[] { "Invoice" },
        })))
        {
            Assert.Contains("attach takes", await ReadErrorTextAsync(badAttach), StringComparison.Ordinal);
        }
        Assert.Empty(sends.Requests);

        using var sent = await PostMcpAsync(client, token, ToolCallPayload(11, "pegasus_report_send", new
        {
            caseId,
            expectedVersion = version,
            operationKey = "mcp:report-send",
            coveringMessage = "Please find our report attached.",
            to = new[] { "handler@principal.example" },
            cc = new[] { "desk@principal.example" },
            attach = new[] { "FeeNote" },
        }));
        var result = await ReadStructuredContentAsync(sent);
        Assert.Equal(generationId, result.GetProperty("generationId").GetGuid());
        Assert.Equal(nameof(StaffMailState.Submitted), result.GetProperty("state").GetString());
        Assert.Equal("current", result.GetProperty("work").GetString());

        var request = Assert.Single(sends.Requests);
        Assert.Equal(ActorKind.Automation, request.Actor.Kind);
        Assert.Equal(ClientId, request.Actor.SubjectId);
        Assert.Equal(caseId, request.CaseId);
        Assert.Equal(version, request.ExpectedCaseVersion);
        Assert.False(string.IsNullOrWhiteSpace(request.LeaseToken));
        Assert.Equal(generationId, request.GenerationId);
        Assert.Equal(CurrentGeneration.Version, request.ExpectedGenerationVersion);
        Assert.Equal("mcp:report-send", request.OperationKey);
        Assert.Equal("Please find our report attached.", request.CoveringMessage);
        Assert.Equal(["handler@principal.example"], request.ReviewedRecipients!.To);
        Assert.Equal(["desk@principal.example"], request.ReviewedRecipients.Cc);
        Assert.Equal([CaseReportArtifactKind.FeeNote], request.Attach);
        Assert.Equal(CaseWorkSelector.Current, request.Work);

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'automation_mcp'
              AND ActorKind = N'Automation'
              AND EventKind = N'pegasus_report_send'
              AND AggregateId = N'{caseId:D}'
              AND Outcome = N'Succeeded'
            """));
    }

    [Fact]
    public async Task OmittedRecipientsSendToThePrincipalsSuggestedRecipients()
    {
        var sends = new RecordingReportSend();
        var generationId = Guid.NewGuid();
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISendCaseReport>();
                services.AddSingleton<ISendCaseReport>(sends);
                WithCurrentGeneration(services, generationId);
            }));
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, SendScopes);
        await EnsureInReviewAsync(mcpFactory, client, token, caseId);
        var version = await GetWorkflowVersionAsync(mcpFactory, caseId);

        using var sent = await PostMcpAsync(client, token, ToolCallPayload(12, "pegasus_report_send", new
        {
            caseId,
            expectedVersion = version,
            operationKey = "mcp:report-send-suggested",
            coveringMessage = "Please find our report attached.",
        }));
        _ = await ReadStructuredContentAsync(sent);

        var request = Assert.Single(sends.Requests);
        // No review: the send boundary addresses the Principal's suggested recipients.
        Assert.Null(request.ReviewedRecipients);
        // No choice: every confirmed document the generation holds attaches.
        Assert.Null(request.Attach);
    }

    [Fact]
    public async Task NewMailIsTheComposersSendFromTheDefaultMailboxAsTheAutomationActor()
    {
        var mail = new RecordingMailSend();
        var mailbox = ReadyDefaultMailbox();
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithMailSend(factory, mail, mailbox);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, SendScopes);
        var version = await GetWorkflowVersionAsync(mcpFactory, caseId);

        // Refusals before anything is sent.
        using (var stale = await PostMcpAsync(client, token, ToolCallPayload(20, "pegasus_mail_send", new
        {
            mode = "new", caseId, expectedVersion = version + 7, subject = "Images", body = "Please send the images.",
            operationKey = "mcp:mail-stale", to = new[] { "handler@principal.example" },
        })))
        {
            Assert.Contains("changed since it was read", await ReadErrorTextAsync(stale), StringComparison.Ordinal);
        }
        using (var noMessage = await PostMcpAsync(client, token, ToolCallPayload(21, "pegasus_mail_send", new
        {
            mode = "reply", caseId, expectedVersion = version, subject = "Re: Images", body = "Thanks.",
            operationKey = "mcp:mail-reply-no-message",
        })))
        {
            Assert.Contains("needs messageId", await ReadErrorTextAsync(noMessage), StringComparison.Ordinal);
        }
        using (var badAddress = await PostMcpAsync(client, token, ToolCallPayload(22, "pegasus_mail_send", new
        {
            mode = "new", caseId, expectedVersion = version, subject = "Images", body = "Please send the images.",
            operationKey = "mcp:mail-bad-address", to = new[] { "not an address" },
        })))
        {
            Assert.Contains("plain e-mail address", await ReadErrorTextAsync(badAddress), StringComparison.Ordinal);
        }
        Assert.Empty(mail.Commands);

        using var sent = await PostMcpAsync(client, token, ToolCallPayload(23, "pegasus_mail_send", new
        {
            mode = "new",
            caseId,
            expectedVersion = version,
            subject = "Images",
            body = "Please send the images.",
            operationKey = "mcp:mail-new",
            to = new[] { "handler@principal.example" },
            cc = new[] { "desk@principal.example" },
            chaser = true,
        }));
        var result = await ReadStructuredContentAsync(sent);
        Assert.Equal("new", result.GetProperty("mode").GetString());
        Assert.Equal(nameof(StaffMailPurpose.CaseChaser), result.GetProperty("purpose").GetString());
        Assert.Equal(nameof(StaffMailState.Submitted), result.GetProperty("state").GetString());
        Assert.Equal(caseId, result.GetProperty("caseId").GetGuid());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("messageId").ValueKind);

        var command = Assert.Single(mail.Commands);
        Assert.Equal(ActorKind.Automation, command.Actor.Kind);
        Assert.Equal(ClientId, command.Actor.SubjectId);
        Assert.Equal(mailbox.Id, command.ApprovedMailboxId);
        Assert.Equal(mailbox.Generation, command.ExpectedMailboxGeneration);
        Assert.Equal(StaffMailPurpose.CaseChaser, command.Purpose);
        Assert.Equal(caseId, command.ContextId);
        Assert.Equal(version, command.ExpectedContextVersion);
        Assert.Equal(StaffMailComposeMode.New, command.ComposeMode);
        Assert.Null(command.OriginalMessage);
        Assert.Equal(["handler@principal.example"], command.To.Select(item => item.Address));
        Assert.Equal(["desk@principal.example"], command.Cc.Select(item => item.Address));
        Assert.Equal("Images", command.Subject);
        Assert.Equal("Please send the images.", command.Body);
        Assert.Empty(command.Attachments);
        Assert.Equal("mcp:mail-new", command.OperationKey);

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'automation_mcp'
              AND ActorKind = N'Automation'
              AND EventKind = N'pegasus_mail_send'
              AND AggregateId = N'{caseId:D}'
              AND Outcome = N'Succeeded'
            """));
    }

    /// <summary>
    /// Through the real engine the composition gates sending exactly as it
    /// gates staff: the DevelopmentOffline profile has no mail transport, so
    /// the send is refused with the staff wording and nothing is recorded.
    /// </summary>
    [Fact]
    public async Task WithoutAMailTransportTheSendIsRefusedAsStaffSendsAre()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApprovedMailboxStore>();
                services.AddSingleton<IApprovedMailboxStore>(new FixedMailboxes(ReadyDefaultMailbox()));
            }));
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, SendScopes);
        var version = await GetWorkflowVersionAsync(mcpFactory, caseId);

        using var refused = await PostMcpAsync(client, token, ToolCallPayload(30, "pegasus_mail_send", new
        {
            mode = "new", caseId, expectedVersion = version, subject = "Images", body = "Please send the images.",
            operationKey = "mcp:mail-offline", to = new[] { "handler@principal.example" },
        }));

        Assert.Contains("unavailable", await ReadErrorTextAsync(refused), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await factory.Database.ScalarAsync<int>("SELECT COUNT(*) FROM StaffMailSendOperations"));
    }

    /// <summary>The real generation store, answering one confirmed current generation for the work.</summary>
    private static void WithCurrentGeneration(IServiceCollection services, Guid generationId)
    {
        var registered = services.Last(item => item.ServiceType == typeof(ICaseReportGenerationStore));
        services.RemoveAll<ICaseReportGenerationStore>();
        services.AddScoped<ICaseReportGenerationStore>(provider => new CurrentGeneration(
            registered.ImplementationFactory is { } create
                ? (ICaseReportGenerationStore)create(provider)
                : (ICaseReportGenerationStore)ActivatorUtilities.CreateInstance(
                    provider, registered.ImplementationType!),
            generationId));
    }
    private static WebApplicationFactory<Program> WithMailSend(
        IntakeWebApplicationFactory factory,
        RecordingMailSend mail,
        ApprovedMailbox mailbox) =>
        WithAutomationMcp(factory).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStaffMailSend>();
                services.AddSingleton<IStaffMailSend>(mail);
                services.RemoveAll<IApprovedMailboxStore>();
                services.AddSingleton<IApprovedMailboxStore>(new FixedMailboxes(mailbox));
            }));

    private static ApprovedMailbox ReadyDefaultMailbox() => new(
        Guid.NewGuid(),
        "claims@collisionengineers.example",
        [ApprovedMailboxRouteScope.StaffSend, ApprovedMailboxRouteScope.SentEvidence],
        ApprovedMailboxState.Approved,
        "graph-mailbox",
        "inbox",
        "sent-items",
        IdentityIsBound: true,
        ActivatedAtUtc: SeedUtcNow,
        Version: 1,
        FolderBindings: [],
        Generation: 3,
        VerifiedEncodedMessageSizeLimit: 25_000_000,
        IsDefaultStaffSend: true);

    private sealed class FixedMailboxes(ApprovedMailbox mailbox) : IApprovedMailboxStore
    {
        public Task<IReadOnlyList<ApprovedMailbox>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ApprovedMailbox>>([mailbox]);

        public Task<ApprovedMailbox> UpdateAsync(UpdateApprovedMailboxRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ApprovedMailbox> SetDefaultAsync(SetDefaultApprovedMailboxRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> IsApprovedAsync(
            string mailboxAddress, ApprovedMailboxRouteScope routeScope, CancellationToken cancellationToken) =>
            Task.FromResult(string.Equals(mailboxAddress, mailbox.Address, StringComparison.OrdinalIgnoreCase)
                && mailbox.RouteScopes.Contains(routeScope));
    }

    private sealed class RecordingMailSend : IStaffMailSend
    {
        private readonly List<StaffMailSendCommand> commands = [];

        public IReadOnlyList<StaffMailSendCommand> Commands
        {
            get
            {
                lock (commands)
                {
                    return [.. commands];
                }
            }
        }

        public Task<StaffMailOperation> SendAsync(StaffMailSendCommand command, CancellationToken cancellationToken)
        {
            lock (commands)
            {
                commands.Add(command);
            }
            return Task.FromResult(Submitted(command.Purpose, command.ContextId, command.ApprovedMailboxId));
        }

        public Task<StaffMailOperation?> GetAsync(ActionActor actor, Guid operationId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StaffMailOperation?> GetLatestForOriginalAsync(
            ActionActor actor, Guid retainedMessageId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StaffMailOperation> CancelAsync(
            ActionActor actor, Guid operationId, long expectedVersion, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingReportSend : ISendCaseReport
    {
        private readonly List<SendCaseReportRequest> requests = [];

        public IReadOnlyList<SendCaseReportRequest> Requests
        {
            get
            {
                lock (requests)
                {
                    return [.. requests];
                }
            }
        }

        public Task<StaffMailOperation> ExecuteAsync(SendCaseReportRequest request, CancellationToken cancellationToken)
        {
            lock (requests)
            {
                requests.Add(request);
            }
            return Task.FromResult(Submitted(StaffMailPurpose.CaseReport, request.GenerationId, Guid.NewGuid()));
        }
    }

    /// <summary>The real generation store, with one confirmed current generation for the work.</summary>
    private sealed class CurrentGeneration(ICaseReportGenerationStore inner, Guid generationId) : ICaseReportGenerationStore
    {
        public const long Version = 2;

        private CaseReportGenerationRecord Record(Guid caseId) => new(
            generationId, caseId, 1, Version, new string('A', 64), null!, "template", "renderer",
            CaseReportGenerationState.Confirmed, SeedUtcNow, null, []);

        public Task<CaseReportGenerationRecord?> GetCurrentAsync(
            ActionActor actor, Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
            Task.FromResult<CaseReportGenerationRecord?>(Record(caseId));

        public Task<IReadOnlyList<CaseReportGenerationRecord>> ListAsync(
            ActionActor actor, Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CaseReportGenerationRecord>>([Record(caseId)]);

        public Task<CaseReportFreezeResult> FreezeAsync(
            FreezeCaseReportGenerationRequest request, CancellationToken cancellationToken) =>
            inner.FreezeAsync(request, cancellationToken);

        public Task<CaseReportGenerationRecord> ConfirmArtifactAsync(
            ConfirmCaseReportArtifactRequest request, CancellationToken cancellationToken) =>
            inner.ConfirmArtifactAsync(request, cancellationToken);

        public Task<CaseReportGenerationRecord> RecordArtifactOutcomeAsync(
            RecordCaseReportArtifactOutcomeRequest request, CancellationToken cancellationToken) =>
            inner.RecordArtifactOutcomeAsync(request, cancellationToken);

        public Task<CaseReportGenerationRecord?> GetAsync(
            ActionActor actor, Guid caseId, Guid generationId, CancellationToken cancellationToken) =>
            inner.GetAsync(actor, caseId, generationId, cancellationToken);

        public Task<CaseReportGenerationRecord> GetForDeliveryAsync(
            SendCaseReportRequest request, CancellationToken cancellationToken) =>
            inner.GetForDeliveryAsync(request, cancellationToken);

        public Task<int> MarkStaleAsync(Guid caseId, string reasonCode, CancellationToken cancellationToken) =>
            inner.MarkStaleAsync(caseId, reasonCode, cancellationToken);

        public Task RecordDraftPreviewedAsync(
            RecordCaseReportDraftPreviewedRequest request, CancellationToken cancellationToken) =>
            inner.RecordDraftPreviewedAsync(request, cancellationToken);
    }

    private static StaffMailOperation Submitted(StaffMailPurpose purpose, Guid contextId, Guid mailboxId) => new(
        Guid.NewGuid(), StaffMailState.Submitted, StaffMailAttemptStage.ObserveSent, 5, SeedUtcNow,
        SeedUtcNow, null, null, mailboxId, 3, new string('A', 64), SeedUtcNow, null,
        purpose, contextId, 1, null);
}
