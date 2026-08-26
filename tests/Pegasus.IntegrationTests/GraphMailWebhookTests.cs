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
                    ApprovedMailboxSubscriptionLifecycleState.Active, null, null)));
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
        Assert.Equal((mailboxId, subscriptionId, MailboxWakeKind.Created), Assert.Single(enqueuer.Messages));
    }

    private sealed class RecordingEnqueuer : IMailboxWakeEnqueuer
    {
        public List<(Guid MailboxId, Guid SubscriptionId, MailboxWakeKind Kind)> Messages { get; } = [];

        public Task EnqueueAsync(Guid approvedMailboxId, Guid subscriptionId, MailboxWakeKind wakeKind,
            CancellationToken cancellationToken)
        {
            Messages.Add((approvedMailboxId, subscriptionId, wakeKind));
            return Task.CompletedTask;
        }
    }

    private sealed class SubscriptionStore(ApprovedMailboxSubscription subscription)
        : IApprovedMailboxSubscriptionStore
    {
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

        public Task SaveAsync(ApprovedMailboxSubscription value, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RecordMaintenanceFailureAsync(Guid approvedMailboxId, string failureCode,
            DateTimeOffset attemptedAtUtc, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
