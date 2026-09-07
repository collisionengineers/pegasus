using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Identity;

namespace Pegasus.IntegrationTests;

public sealed class GraphMailWebhookTests
{
    [Fact]
    public async Task ValidationHandshakeReturnsDecodedOpaqueTokenAsPlainText()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            "/hooks/microsoft-graph/mail?validationToken=opaque%20token%2Bvalue",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("opaque token+value", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ValidCreatedNotificationQueuesStableMailboxIdentifiers()
    {
        var mailboxId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var resource = "users/mailbox-id/mailFolders/inbox-id/messages";
        var enqueuer = new RecordingEnqueuer();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IApprovedMailboxSubscriptionStore>();
            services.AddSingleton<IApprovedMailboxSubscriptionStore>(new SubscriptionStore(
                new(mailboxId, subscriptionId.ToString("D"), resource,
                    DateTimeOffset.UtcNow.AddDays(1),
                    ApprovedMailboxSubscriptionLifecycleState.Active, null, null, 3)));
            services.RemoveAll<IMailboxWakeEnqueuer>();
            services.AddSingleton<IMailboxWakeEnqueuer>(enqueuer);
        }));
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/hooks/microsoft-graph/mail", new
        {
            value = new[]
            {
                new
                {
                    subscriptionId,
                    clientState = "integration-client-state",
                    tenantId = "858cf5b3-aa0a-47a6-9b40-4851fd0afa94",
                    resource = "users/mailbox-id/messages/message-id",
                    changeType = "created"
                }
            }
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal((mailboxId, subscriptionId, 3L, MailboxWakeKind.Created, "message-id"), Assert.Single(enqueuer.Messages));
    }

    [Theory]
    [InlineData("missed", MailboxWakeKind.Missed)]
    [InlineData("subscriptionRemoved", MailboxWakeKind.SubscriptionRemoved)]
    [InlineData("reauthorizationRequired", MailboxWakeKind.ReauthorizationRequired)]
    public async Task SupportedLifecycleNotificationQueuesTheTargetedMailboxWake(
        string lifecycleEvent,
        MailboxWakeKind expectedKind)
    {
        var mailboxId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var enqueuer = new RecordingEnqueuer();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = Configure(baseFactory, mailboxId, subscriptionId, enqueuer);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/hooks/microsoft-graph/mail", Notification(
            subscriptionId,
            lifecycleEvent: lifecycleEvent));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal((mailboxId, subscriptionId, 3L, expectedKind, (string?)null), Assert.Single(enqueuer.Messages));
    }

    [Theory]
    [InlineData("wrong-secret", "858cf5b3-aa0a-47a6-9b40-4851fd0afa94", "users/mailbox-id/messages/message-id", "created", null)]
    [InlineData("integration-client-state", "wrong-tenant", "users/mailbox-id/messages/message-id", "created", null)]
    [InlineData("integration-client-state", "858cf5b3-aa0a-47a6-9b40-4851fd0afa94", "users/other/messages/message-id", "created", null)]
    [InlineData("integration-client-state", "858cf5b3-aa0a-47a6-9b40-4851fd0afa94", "users/mailbox-id/messages/message-id", "updated", null)]
    [InlineData("integration-client-state", "858cf5b3-aa0a-47a6-9b40-4851fd0afa94", "users/mailbox-id/messages/message-id", null, "unknown")]
    public async Task InvalidNotificationIsAcknowledgedWithoutQueueingOrDisclosure(
        string clientState,
        string tenantId,
        string resource,
        string? changeType,
        string? lifecycleEvent)
    {
        var subscriptionId = Guid.NewGuid();
        var enqueuer = new RecordingEnqueuer();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = Configure(baseFactory, Guid.NewGuid(), subscriptionId, enqueuer);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/hooks/microsoft-graph/mail", Notification(
            subscriptionId, clientState, tenantId, resource, changeType, lifecycleEvent));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Empty(enqueuer.Messages);
        Assert.DoesNotContain("integration-client-state", await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownSubscriptionAndMalformedOrOversizedBatchesQueueNothing()
    {
        var knownSubscriptionId = Guid.NewGuid();
        var enqueuer = new RecordingEnqueuer();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = Configure(baseFactory, Guid.NewGuid(), knownSubscriptionId, enqueuer);
        using var client = factory.CreateClient();

        using var unknown = await client.PostAsJsonAsync(
            "/hooks/microsoft-graph/mail", Notification(Guid.NewGuid()));
        using var malformed = await client.PostAsync(
            "/hooks/microsoft-graph/mail", new StringContent("{", System.Text.Encoding.UTF8, "application/json"));
        using var oversized = await client.PostAsJsonAsync("/hooks/microsoft-graph/mail", new
        {
            value = Enumerable.Range(0, 101).Select(_ => NotificationValue(knownSubscriptionId)).ToArray()
        });

        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, malformed.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, oversized.StatusCode);
        Assert.Empty(enqueuer.Messages);
    }

    [Fact]
    public async Task ValidNotificationQueueFailureReturnsRetryableServerError()
    {
        var subscriptionId = Guid.NewGuid();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = Configure(
            baseFactory,
            Guid.NewGuid(),
            subscriptionId,
            new ThrowingEnqueuer());
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/hooks/microsoft-graph/mail", Notification(subscriptionId));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    private static WebApplicationFactory<Program> Configure(
        IntakeWebApplicationFactory baseFactory,
        Guid mailboxId,
        Guid subscriptionId,
        IMailboxWakeEnqueuer enqueuer)
    {
        const string resource = "users/mailbox-id/mailFolders/inbox-id/messages";
        return baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IApprovedMailboxSubscriptionStore>();
            services.AddSingleton<IApprovedMailboxSubscriptionStore>(new SubscriptionStore(
                new(mailboxId, subscriptionId.ToString("D"), resource,
                    DateTimeOffset.UtcNow.AddDays(1),
                    ApprovedMailboxSubscriptionLifecycleState.Active, null, null, 3)));
            services.RemoveAll<IMailboxWakeEnqueuer>();
            services.AddSingleton(enqueuer);
        }));
    }

    private static object Notification(
        Guid subscriptionId,
        string clientState = "integration-client-state",
        string tenantId = "858cf5b3-aa0a-47a6-9b40-4851fd0afa94",
        string resource = "users/mailbox-id/messages/message-id",
        string? changeType = "created",
        string? lifecycleEvent = null) => new
        {
            value = new[] { NotificationValue(subscriptionId, clientState, tenantId, resource, changeType, lifecycleEvent) }
        };

    private static object NotificationValue(
        Guid subscriptionId,
        string clientState = "integration-client-state",
        string tenantId = "858cf5b3-aa0a-47a6-9b40-4851fd0afa94",
        string resource = "users/mailbox-id/messages/message-id",
        string? changeType = "created",
        string? lifecycleEvent = null) => new
        {
            subscriptionId,
            clientState,
            tenantId,
            resource,
            changeType,
            lifecycleEvent
        };

    private sealed class RecordingEnqueuer : IMailboxWakeEnqueuer
    {
        public List<(Guid MailboxId, Guid SubscriptionId, long Generation, MailboxWakeKind Kind, string? MessageId)> Messages { get; } = [];

        public Task EnqueueAsync(Guid approvedMailboxId, Guid subscriptionId, long generation,
            MailboxWakeKind wakeKind, string? immutableMessageId,
            CancellationToken cancellationToken)
        {
            Messages.Add((approvedMailboxId, subscriptionId, generation, wakeKind, immutableMessageId));
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingEnqueuer : IMailboxWakeEnqueuer
    {
        public Task EnqueueAsync(Guid approvedMailboxId, Guid subscriptionId, long generation,
            MailboxWakeKind wakeKind, string? immutableMessageId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Queue unavailable.");
    }

    private sealed class SubscriptionStore(ApprovedMailboxSubscription subscription)
        : IApprovedMailboxSubscriptionStore
    {
        public Task<IReadOnlyList<ApprovedMailboxSubscription>> ListAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ApprovedMailboxSubscription>>([subscription]);

        public Task<ApprovedMailboxSubscription?> GetActiveAsync(string subscriptionId,
            DateTimeOffset nowUtc, CancellationToken cancellationToken) =>
            Task.FromResult<ApprovedMailboxSubscription?>(
                string.Equals(subscription.SubscriptionId, subscriptionId, StringComparison.Ordinal)
                    ? subscription
                    : null);

        public Task<IReadOnlyList<ApprovedMailboxSubscriptionMaintenanceCandidate>>
            ListMaintenanceCandidatesAsync(DateTimeOffset nowUtc,
                CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ApprovedMailboxSubscriptionMaintenanceCandidate>>([]);

        public Task SaveAsync(ApprovedMailboxSubscription value,
            string? expectedPriorSubscriptionId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RecordMaintenanceFailureAsync(Guid approvedMailboxId, long expectedGeneration,
            string? expectedSubscriptionId, string failureCode,
            DateTimeOffset attemptedAtUtc, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
