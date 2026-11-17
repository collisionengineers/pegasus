using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.PhotoCapture;
using Pegasus.Web.Mcp;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;
using Labels = Pegasus.Web.Presentation.OperatorLabels.PhotoCapture;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Caller-equivalent evidence for the Automation Actor's photo links
/// (FRD-28, FRD-10; R1039, R1040): real HTTP against /mcp and real LocalDB
/// persistence. The link acts go through the Core commands the Photo capture
/// pages call and record the Automation Actor; sending is the photo link's
/// staff send, under <c>automation.send</c> alone.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class AutomationPhotoLinkIngressTests
{
    private const string CasesOnly = "automation.cases";
    private const string CasesAndSend = "automation.cases automation.send";

    private static readonly ApprovedMailboxRouteScope[] Outgoing = [ApprovedMailboxRouteScope.Outgoing];

    [Fact]
    public async Task ALinksCaseworkRunsThroughTheStaffCommandsUnderTheCasesScope()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, CasesOnly);
        var issue = ToolCallPayload(1, "pegasus_photo_link_action", new
        {
            action = "issue",
            caseId,
            operationKey = "mcp:photo-issue",
            customerName = "Sam Taylor",
        });

        Guid captureId;
        DateTimeOffset expiresBefore;
        using (var issued = await PostMcpAsync(client, token, issue))
        {
            var result = await ReadStructuredContentAsync(issued);
            Assert.False(result.GetProperty("replayed").GetBoolean());
            Assert.Contains("/c/", result.GetProperty("linkUrl").GetString(), StringComparison.Ordinal);
            var link = result.GetProperty("link");
            captureId = link.GetProperty("captureId").GetGuid();
            expiresBefore = link.GetProperty("expiresAtUtc").GetDateTimeOffset();
            Assert.Equal(nameof(PhotoCaptureOrigin.CaseLink), link.GetProperty("origin").GetString());
            Assert.Equal(nameof(PhotoCaptureState.SentNotOpened), link.GetProperty("state").GetString());
            Assert.Equal("Sam Taylor", link.GetProperty("customerName").GetString());
        }

        // The same key issues nothing more and shows no address again.
        using (var again = await PostMcpAsync(client, token, issue))
        {
            var result = await ReadStructuredContentAsync(again);
            Assert.True(result.GetProperty("replayed").GetBoolean());
            Assert.True(IsNullOrAbsent(result, "linkUrl"));
        }

        Assert.Equal(1, await (await factory.DatabaseAsync).ScalarAsync<int>(
            $"SELECT COUNT(*) FROM PhotoCaptures WHERE CaseId = '{caseId:D}'"));

        using (var forCase = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_photo_link_list", new { caseId })))
        {
            var item = Assert.Single((await ReadStructuredContentAsync(forCase)).GetProperty("items").EnumerateArray());
            Assert.Equal(captureId, item.GetProperty("captureId").GetGuid());
            Assert.True(item.GetProperty("isLive").GetBoolean());
        }

        using (var direct = await PostMcpAsync(client, token, ToolCallPayload(3, "pegasus_photo_link_list", new { scope = "all", search = "Sam Taylor" })))
        {
            var result = await ReadStructuredContentAsync(direct);
            Assert.Contains(
                result.GetProperty("items").EnumerateArray(),
                item => item.GetProperty("captureId").GetGuid() == captureId);
            Assert.True(result.GetProperty("countsByScope").GetProperty("awaiting").GetInt32() >= 1);
        }

        // A lifetime Core does not offer is refused with Core's own sentence.
        using (var refused = await PostMcpAsync(client, token, ToolCallPayload(4, "pegasus_photo_link_action", new
        {
            action = "extend", captureId, days = 5, operationKey = "mcp:photo-extend-5",
        })))
        {
            Assert.Contains(
                "The extension is not one of the offered lifetimes.",
                await ReadErrorTextAsync(refused),
                StringComparison.Ordinal);
        }

        using (var extended = await PostMcpAsync(client, token, ToolCallPayload(5, "pegasus_photo_link_action", new
        {
            action = "extend", captureId, days = 14, operationKey = "mcp:photo-extend",
        })))
        {
            var link = (await ReadStructuredContentAsync(extended)).GetProperty("link");
            Assert.True(link.GetProperty("expiresAtUtc").GetDateTimeOffset() > expiresBefore);
        }

        using (var called = await PostMcpAsync(client, token, ToolCallPayload(6, "pegasus_photo_link_action", new
        {
            action = "log_call", captureId, operationKey = "mcp:photo-call",
        })))
        {
            await ReadStructuredContentAsync(called);
        }

        using (var got = await PostMcpAsync(client, token, ToolCallPayload(7, "pegasus_photo_link_get", new { captureId })))
        {
            var result = await ReadStructuredContentAsync(got);
            var history = result.GetProperty("history");
            Assert.Equal(Labels.Created, history[0].GetProperty("label").GetString());
            Assert.Contains(history.EnumerateArray(), line => line.GetProperty("label").GetString() == Labels.LinkExtended);
            Assert.Contains(history.EnumerateArray(), line => line.GetProperty("label").GetString() == Labels.CallLoggedAt);
            Assert.True(result.GetProperty("offers").GetProperty("cancel").GetBoolean());
            Assert.Empty(result.GetProperty("photos").EnumerateArray());
        }

        using (var cancelled = await PostMcpAsync(client, token, ToolCallPayload(8, "pegasus_photo_link_action", new
        {
            action = "cancel", captureId, operationKey = "mcp:photo-cancel",
        })))
        {
            var link = (await ReadStructuredContentAsync(cancelled)).GetProperty("link");
            Assert.Equal(nameof(PhotoCaptureState.Cancelled), link.GetProperty("state").GetString());
            Assert.False(link.GetProperty("isLive").GetBoolean());
        }

        // Resend on a cancelled link is refused as Resend refuses it (R863).
        using (var resent = await PostMcpAsync(client, token, ToolCallPayload(9, "pegasus_photo_link_action", new
        {
            action = "resend_copy", captureId, operationKey = "mcp:photo-resend-copy",
        })))
        {
            Assert.Contains(PhotoLinkActions.LinkCancelled, await ReadErrorTextAsync(resent), StringComparison.Ordinal);
        }

        // A token without automation.send cannot send a link.
        using (var send = await PostMcpAsync(client, token, ToolCallPayload(10, "pegasus_photo_link_send", new
        {
            action = "send", channel = "email", caseId, operationKey = "mcp:photo-no-send-scope", email = "sam.taylor@example.com",
        })))
        {
            await AssertScopeDeniedAsync(send, AutomationMcp.SendScope);
        }

        // The link's own history names the Automation Actor for each act.
        Assert.Equal(1, await (await factory.DatabaseAsync).ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'photo_capture'
              AND AggregateId = N'{captureId:D}'
              AND EventKind = N'{PhotoLinkActs.Cancelled}'
              AND ActorKind = N'Automation'
            """));
    }

    [Fact]
    public async Task SendingByEmailIsThePhotoLinkStaffSendFromTheNamedMailbox()
    {
        var mail = new RecordingMailSend();
        var mailbox = OutgoingMailbox();
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStaffMailSend>();
                services.AddSingleton<IStaffMailSend>(mail);
                services.RemoveAll<IApprovedMailboxStore>();
                services.AddSingleton<IApprovedMailboxStore>(new FixedMailboxes(mailbox));
            }));
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, CasesAndSend);
        var send = ToolCallPayload(1, "pegasus_photo_link_send", new
        {
            action = "send",
            channel = "email",
            caseId,
            operationKey = "mcp:photo-send",
            email = "sam.taylor@example.com",
            customerName = "Sam Taylor",
            fromMailbox = mailbox.Address,
        });

        string linkUrl;
        using (var sent = await PostMcpAsync(client, token, send))
        {
            var result = await ReadStructuredContentAsync(sent);
            Assert.Equal(nameof(StaffMailState.Submitted), result.GetProperty("state").GetString());
            Assert.False(result.GetProperty("replayed").GetBoolean());
            linkUrl = result.GetProperty("linkUrl").GetString()!;
        }

        var command = Assert.Single(mail.Commands);
        Assert.Equal(ActorKind.Automation, command.Actor.Kind);
        Assert.Equal(StaffMailPurpose.PhotoLink, command.Purpose);
        Assert.Equal(StaffMailComposeMode.New, command.ComposeMode);
        Assert.Equal(caseId, command.ContextId);
        Assert.Equal(mailbox.Id, command.ApprovedMailboxId);
        Assert.Equal("sam.taylor@example.com", Assert.Single(command.To).Address);
        Assert.Contains(linkUrl, command.Body, StringComparison.Ordinal);
        Assert.Equal("mcp:photo-send", command.OperationKey);

        // The same key again makes and sends nothing more.
        using (var again = await PostMcpAsync(client, token, send))
        {
            Assert.True((await ReadStructuredContentAsync(again)).GetProperty("replayed").GetBoolean());
        }

        Assert.Single(mail.Commands);
        Assert.Equal(1, await (await factory.DatabaseAsync).ScalarAsync<int>(
            $"SELECT COUNT(*) FROM PhotoCaptures WHERE CaseId = '{caseId:D}'"));
    }

    /// <summary>
    /// A photo link e-mail whose provider answer is ambiguous (a Graph server
    /// error or a timeout) answers its Unknown operation with the link, as a
    /// returned Unknown does, never "The automation action failed." (R1123).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendingByEmailWithAnUnknownOutcomeAnswersItsUnknownOperation(bool timedOut)
    {
        var mail = new RecordingMailSend
        {
            ProviderFault = timedOut
                ? new TaskCanceledException("The request timed out.", new TimeoutException())
                : new HttpRequestException(
                    "Microsoft Graph returned 503.", null, System.Net.HttpStatusCode.ServiceUnavailable)
        };
        var mailbox = OutgoingMailbox();
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStaffMailSend>();
                services.AddSingleton<IStaffMailSend>(mail);
                services.RemoveAll<IApprovedMailboxStore>();
                services.AddSingleton<IApprovedMailboxStore>(new FixedMailboxes(mailbox));
            }));
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, CasesAndSend);

        using var sent = await PostMcpAsync(client, token, ToolCallPayload(1, "pegasus_photo_link_send", new
        {
            action = "send",
            channel = "email",
            caseId,
            operationKey = "mcp:photo-send-unknown",
            email = "sam.taylor@example.com",
            customerName = "Sam Taylor",
            fromMailbox = mailbox.Address,
        }));
        var result = await ReadStructuredContentAsync(sent);

        Assert.Single(mail.Commands);
        Assert.Equal(nameof(StaffMailState.Unknown), result.GetProperty("state").GetString());
        Assert.False(result.GetProperty("replayed").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(result.GetProperty("linkUrl").GetString()));
    }

    private static bool IsNullOrAbsent(JsonElement element, string property) =>
        !element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null;

    /// <summary>An Outgoing mailbox that can send.</summary>
    private static ApprovedMailbox OutgoingMailbox() => new(
        Guid.NewGuid(),
        "claims@collisionengineers.example",
        Outgoing,
        ApprovedMailboxState.Approved,
        "graph-mailbox",
        "inbox",
        "sent-items",
        IdentityIsBound: true,
        ActivatedAtUtc: SeedUtcNow,
        Version: 1,
        Generation: 3);

    private sealed class FixedMailboxes(params ApprovedMailbox[] mailboxes) : IApprovedMailboxStore
    {
        public Task<IReadOnlyList<ApprovedMailbox>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ApprovedMailbox>>(mailboxes);

        public Task<ApprovedMailbox> UpdateAsync(UpdateApprovedMailboxRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> IsApprovedAsync(
            string mailboxAddress, ApprovedMailboxRouteScope routeScope, CancellationToken cancellationToken) =>
            Task.FromResult(mailboxes.Any(mailbox =>
                string.Equals(mailboxAddress, mailbox.Address, StringComparison.OrdinalIgnoreCase)
                && mailbox.RouteScopes.Contains(routeScope)));
    }

    /// <summary>
    /// A staff send that records each message and reports it Submitted; with
    /// <see cref="ProviderFault"/>, it records it Unknown and throws as the
    /// engine does when the provider's answer is ambiguous.
    /// </summary>
    private sealed class RecordingMailSend : IStaffMailSend
    {
        private readonly List<StaffMailSendCommand> commands = [];

        public Exception? ProviderFault { get; init; }

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

            if (ProviderFault is { } fault)
            {
                throw new StaffMailOutcomeUnknownException(
                    new StaffMailOperation(
                        Guid.NewGuid(), StaffMailState.Unknown, StaffMailAttemptStage.Send, 5, SeedUtcNow,
                        null, null, "provider_outcome_unknown", command.ApprovedMailboxId, 3, new string('A', 64),
                        SeedUtcNow, null, command.Purpose, command.ContextId, 1, null),
                    fault);
            }

            return Task.FromResult(new StaffMailOperation(
                Guid.NewGuid(), StaffMailState.Submitted, StaffMailAttemptStage.ObserveSent, 5, SeedUtcNow,
                SeedUtcNow, null, null, command.ApprovedMailboxId, 3, new string('A', 64), SeedUtcNow, null,
                command.Purpose, command.ContextId, 1, null));
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
}
