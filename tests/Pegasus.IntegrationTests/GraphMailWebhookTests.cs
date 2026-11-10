using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pegasus.Core.Identity;

namespace Pegasus.IntegrationTests;

public sealed class GraphMailWebhookTests
{
    [Fact]
    public async Task ValidationHandshakeReturnsDecodedOpaqueTokenAsPlainText()
    {
        using var factory = IntakeWebApplicationFactory.WithoutDatabase();
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
        using var baseFactory = IntakeWebApplicationFactory.WithoutDatabase();
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

    [Fact]
    public async Task AMailboxSubscribedByAddressQueuesTheNotificationGraphNamesByUserId()
    {
        // Graph names the mailbox by its user id in a notification even when the
        // subscription was made by address; the subscription id, client state
        // and tenant identify the mailbox (#1826, R883).
        var mailboxId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var enqueuer = new RecordingEnqueuer();
        var store = new SubscriptionStore(new(mailboxId, subscriptionId.ToString("D"),
            "users/desk%40collisionengineers.co.uk/mailFolders/inbox-id/messages",
            DateTimeOffset.UtcNow.AddDays(1),
            ApprovedMailboxSubscriptionLifecycleState.Active, null, null, 6));

        await HandleDirectlyAsync(Notification(
            subscriptionId,
            resource: "Users/0b0e4f4e-21c5-4a0e-9f0f-6c1d2e3f4a5b/Messages/AAMkAGI2THVSAAA%3D"),
            store, enqueuer, NullLoggerFactory.Instance);

        Assert.Equal((mailboxId, subscriptionId, 6L, MailboxWakeKind.Created, "AAMkAGI2THVSAAA="),
            Assert.Single(enqueuer.Messages));
    }

    [Fact]
    public async Task ASkippedNotificationWritesOneLineNamingItsSubscriptionAndReason()
    {
        var unknownId = Guid.NewGuid();
        var enqueuer = new RecordingEnqueuer();
        var store = new SubscriptionStore(new(Guid.NewGuid(), Guid.NewGuid().ToString("D"),
            "users/mailbox-id/mailFolders/inbox-id/messages", DateTimeOffset.UtcNow.AddDays(1),
            ApprovedMailboxSubscriptionLifecycleState.Active, null, null, 3));
        var logs = new RecordingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));

        await HandleDirectlyAsync(Notification(unknownId), store, enqueuer, loggerFactory);
        await HandleDirectlyAsync(Notification(unknownId, clientState: "wrong-secret"), store, enqueuer, loggerFactory);

        Assert.Empty(enqueuer.Messages);
        Assert.Collection(
            logs.Messages,
            line =>
            {
                Assert.Contains(unknownId.ToString("D"), line, StringComparison.Ordinal);
                Assert.Contains("no active subscription", line, StringComparison.Ordinal);
            },
            line =>
            {
                Assert.Contains(unknownId.ToString("D"), line, StringComparison.Ordinal);
                Assert.Contains("client state", line, StringComparison.Ordinal);
                Assert.DoesNotContain("wrong-secret", line, StringComparison.Ordinal);
            });
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
        using var baseFactory = IntakeWebApplicationFactory.WithoutDatabase();
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
        using var baseFactory = IntakeWebApplicationFactory.WithoutDatabase();
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
        using var baseFactory = IntakeWebApplicationFactory.WithoutDatabase();
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
        using var baseFactory = IntakeWebApplicationFactory.WithoutDatabase();
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

    [Fact]
    public async Task AWakeIsStillQueuedWhenGraphHangsUpMidDelivery()
    {
        // Called directly: the test server finishes a cancelled call only once
        // the handler returns, so it cannot show the handler outliving Graph.
        var subscriptionId = Guid.NewGuid();
        var enqueuer = new HeldEnqueuer();
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(
            JsonSerializer.SerializeToUtf8Bytes(Notification(subscriptionId), JsonSerializerOptions.Web));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Graph:ChangeNotificationClientState"] = "integration-client-state",
            ["Graph:TenantId"] = "858cf5b3-aa0a-47a6-9b40-4851fd0afa94"
        }).Build();
        using var hangUp = new CancellationTokenSource();

        var handled = Pegasus.Web.GraphMailWebhook.HandleAsync(
            context.Request,
            new SubscriptionStore(new(Guid.NewGuid(), subscriptionId.ToString("D"),
                "users/mailbox-id/mailFolders/inbox-id/messages", DateTimeOffset.UtcNow.AddDays(1),
                ApprovedMailboxSubscriptionLifecycleState.Active, null, null, 3)),
            enqueuer,
            configuration,
            TimeProvider.System,
            NullLoggerFactory.Instance,
            hangUp.Token);
        await enqueuer.Entered.WaitAsync(TimeSpan.FromMinutes(1));
        await hangUp.CancelAsync();
        enqueuer.Release();

        Assert.False(await enqueuer.Cancelled.WaitAsync(TimeSpan.FromMinutes(1)));
        var result = await handled.WaitAsync(TimeSpan.FromMinutes(1));
        Assert.Equal(StatusCodes.Status202Accepted,
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task ABatchLooksEachDistinctSubscriptionUpOnce()
    {
        var subscriptionId = Guid.NewGuid();
        var unknownId = Guid.NewGuid();
        var enqueuer = new RecordingEnqueuer();
        var store = new SubscriptionStore(new(Guid.NewGuid(), subscriptionId.ToString("D"),
            "users/mailbox-id/mailFolders/inbox-id/messages", DateTimeOffset.UtcNow.AddDays(1),
            ApprovedMailboxSubscriptionLifecycleState.Active, null, null, 3));
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new
        {
            value = new[]
            {
                NotificationValue(subscriptionId),
                NotificationValue(unknownId),
                NotificationValue(subscriptionId),
                NotificationValue(unknownId)
            }
        }, JsonSerializerOptions.Web));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Graph:ChangeNotificationClientState"] = "integration-client-state",
            ["Graph:TenantId"] = "858cf5b3-aa0a-47a6-9b40-4851fd0afa94"
        }).Build();

        await Pegasus.Web.GraphMailWebhook.HandleAsync(
            context.Request, store, enqueuer, configuration, TimeProvider.System,
            NullLoggerFactory.Instance, CancellationToken.None);

        Assert.Equal(2, store.ActiveLookups);
        Assert.Equal(2, enqueuer.Messages.Count);
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

    private static async Task HandleDirectlyAsync(
        object body,
        IApprovedMailboxSubscriptionStore store,
        IMailboxWakeEnqueuer enqueuer,
        ILoggerFactory loggerFactory)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(body, JsonSerializerOptions.Web));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Graph:ChangeNotificationClientState"] = "integration-client-state",
            ["Graph:TenantId"] = "858cf5b3-aa0a-47a6-9b40-4851fd0afa94"
        }).Build();
        await Pegasus.Web.GraphMailWebhook.HandleAsync(
            context.Request, store, enqueuer, configuration, TimeProvider.System,
            loggerFactory, CancellationToken.None);
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Logger(Messages);

        public void Dispose()
        {
        }

        private sealed class Logger(List<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter) =>
                messages.Add(formatter(state, exception));
        }
    }

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

    /// <summary>Holds the send until released, and reports whether its token was cancelled first.</summary>
    private sealed class HeldEnqueuer : IMailboxWakeEnqueuer
    {
        private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => entered.Task;

        public Task<bool> Cancelled => cancelled.Task;

        public void Release() => release.TrySetResult();

        public async Task EnqueueAsync(Guid approvedMailboxId, Guid subscriptionId, long generation,
            MailboxWakeKind wakeKind, string? immutableMessageId, CancellationToken cancellationToken)
        {
            entered.TrySetResult();
            await Task.WhenAny(release.Task, Task.Delay(Timeout.Infinite, cancellationToken));
            cancelled.TrySetResult(cancellationToken.IsCancellationRequested);
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

        public int ActiveLookups { get; private set; }

        public Task<ApprovedMailboxSubscription?> GetActiveAsync(string subscriptionId,
            DateTimeOffset nowUtc, CancellationToken cancellationToken)
        {
            ActiveLookups++;
            return Task.FromResult<ApprovedMailboxSubscription?>(
                string.Equals(subscription.SubscriptionId, subscriptionId, StringComparison.Ordinal)
                    ? subscription
                    : null);
        }

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
