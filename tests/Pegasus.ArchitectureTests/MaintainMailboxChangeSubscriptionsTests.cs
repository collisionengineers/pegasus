using System.Net;
using System.Text;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Email;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// The Graph change-subscription maintenance pass that runs before the approved-inbox
/// fallback poll, over a fake Graph endpoint and a fake subscription store.
/// </summary>
public sealed class MaintainMailboxChangeSubscriptionsTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 10, 5, 3, TimeSpan.Zero);
    private const string SubscriptionId = "11111111-2222-3333-4444-555555555555";

    [Fact]
    public async Task WithoutAGraphAdapterItReadsNothing()
    {
        var store = new FakeStore();
        var pass = Pass(store, graph: null);

        await pass.ExecuteAsync(default);

        Assert.Empty(store.Reads);
    }

    [Theory]
    [InlineData("Graph:ChangeNotificationUrl")]
    [InlineData("Graph:ChangeNotificationClientState")]
    public async Task AMissingNotificationSettingFailsThePassBeforeTheStoreIsRead(string missingKey)
    {
        var store = new FakeStore();
        var fake = new FakeGraph();
        var values = Configuration();
        values.Remove(missingKey);
        var pass = Pass(store, fake, values);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => pass.ExecuteAsync(default));

        Assert.Contains(missingKey, failure.Message, StringComparison.Ordinal);
        Assert.Empty(store.Reads);
        Assert.Empty(fake.Requests);
    }

    [Fact]
    public async Task AMailboxWithNoSubscriptionGetsOneCreatedAndSaved()
    {
        var store = new FakeStore(Candidate(subscription: null));
        var fake = new FakeGraph();
        var pass = Pass(store, fake);

        await pass.ExecuteAsync(default);

        var request = Assert.Single(fake.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v1.0/subscriptions", request.Path);
        Assert.Contains("\"clientState\":\"state\"", request.Body, StringComparison.Ordinal);
        var saved = Assert.Single(store.Saved);
        Assert.Equal(SubscriptionId, saved.Subscription.SubscriptionId);
        Assert.Null(saved.ExpectedPriorSubscriptionId);
        Assert.Equal(ApprovedMailboxSubscriptionLifecycleState.Active, saved.Subscription.LifecycleState);
        Assert.Empty(store.Failures);
    }

    [Fact]
    public async Task ASubscriptionInsideTheRenewalWindowIsRenewedInPlace()
    {
        var current = Active(expiresAtUtc: Now.AddHours(47));
        var store = new FakeStore(Candidate(current));
        var fake = new FakeGraph();
        var pass = Pass(store, fake);

        await pass.ExecuteAsync(default);

        var request = Assert.Single(fake.Requests);
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.EndsWith($"/subscriptions/{SubscriptionId}", request.Path, StringComparison.Ordinal);
        Assert.Equal(SubscriptionId, Assert.Single(store.Saved).ExpectedPriorSubscriptionId);
    }

    [Fact]
    public async Task ASubscriptionOutsideTheRenewalWindowMakesNoGraphCallAndIsStamped()
    {
        var current = Active(expiresAtUtc: Now.AddHours(49));
        var store = new FakeStore(Candidate(current));
        var fake = new FakeGraph();
        var pass = Pass(store, fake);

        await pass.ExecuteAsync(default);

        Assert.Empty(fake.Requests);
        var saved = Assert.Single(store.Saved);
        Assert.Equal(Now, saved.Subscription.LastMaintainedAtUtc);
        Assert.Null(saved.Subscription.LastMaintenanceFailureCode);
        Assert.Equal(current.ExpiresAtUtc, saved.Subscription.ExpiresAtUtc);
    }

    [Fact]
    public async Task AGraphFailureIsRecordedAgainstThatMailboxAndTheNextMailboxIsStillMaintained()
    {
        var failing = Candidate(subscription: null);
        var healthy = Candidate(subscription: null);
        var store = new FakeStore(failing, healthy);
        var fake = new FakeGraph(HttpStatusCode.ServiceUnavailable, HttpStatusCode.Created);
        var pass = Pass(store, fake);

        await pass.ExecuteAsync(default);

        Assert.Equal(2, fake.Requests.Count);
        var failure = Assert.Single(store.Failures);
        Assert.Equal(failing.ApprovedMailboxId, failure.ApprovedMailboxId);
        Assert.Equal("graph_subscription_maintenance_failed", failure.Code);
        Assert.Equal(Now, failure.AttemptedAtUtc);
        Assert.Equal(healthy.ApprovedMailboxId, Assert.Single(store.Saved).Subscription.ApprovedMailboxId);
    }

    [Fact]
    public async Task ALostMaintenanceRaceIsDeferredWithoutRecordingAFailure()
    {
        var lost = Candidate(subscription: null);
        var healthy = Candidate(subscription: null);
        var store = new FakeStore(lost, healthy) { LoseSaveFor = lost.ApprovedMailboxId };
        var fake = new FakeGraph();
        var logger = new CountingLogger();
        var pass = Pass(store, fake, logger: logger);

        await pass.ExecuteAsync(default);

        Assert.Empty(store.Failures);
        Assert.Equal(healthy.ApprovedMailboxId, Assert.Single(store.Saved).Subscription.ApprovedMailboxId);
        Assert.Equal(1, logger.Information);
    }

    [Fact]
    public async Task CancellationStopsThePassInsteadOfRecordingAFailure()
    {
        var store = new FakeStore(Candidate(subscription: null));
        var fake = new FakeGraph();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var pass = Pass(store, fake);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pass.ExecuteAsync(cancelled.Token));

        Assert.Empty(store.Failures);
        Assert.Empty(store.Saved);
    }

    private static MaintainMailboxChangeSubscriptions Pass(
        FakeStore store,
        FakeGraph? graph,
        Dictionary<string, string?>? values = null,
        ILogger<MaintainMailboxChangeSubscriptions>? logger = null)
    {
        var adapter = graph is null
            ? Array.Empty<GraphMailboxChangeSubscriptions>()
            : [new GraphMailboxChangeSubscriptions(
                new FixedCredential(),
                GraphApprovedMailboxOptions.Create("https://graph.microsoft.com/v1.0/"),
                new HttpClient(graph))];
        return new MaintainMailboxChangeSubscriptions(
            store,
            adapter,
            new ConfigurationBuilder().AddInMemoryCollection(values ?? Configuration()).Build(),
            new FixedClock(Now),
            logger ?? NullLogger<MaintainMailboxChangeSubscriptions>.Instance);
    }

    private static Dictionary<string, string?> Configuration() => new()
    {
        ["Graph:ChangeNotificationUrl"] = "https://pegasus.example.test/hooks/microsoft-graph/mail",
        ["Graph:ChangeNotificationClientState"] = "state"
    };

    private static ApprovedMailboxSubscriptionMaintenanceCandidate Candidate(
        ApprovedMailboxSubscription? subscription)
    {
        var mailboxId = subscription?.ApprovedMailboxId ?? Guid.NewGuid();
        return new(mailboxId, "mailbox-id", "inbox-folder", subscription, 1);
    }

    private static ApprovedMailboxSubscription Active(DateTimeOffset expiresAtUtc) => new(
        Guid.NewGuid(),
        SubscriptionId,
        GraphMailboxChangeSubscriptions.Resource("mailbox-id", "inbox-folder"),
        expiresAtUtc,
        ApprovedMailboxSubscriptionLifecycleState.Active,
        Now.AddHours(-1),
        null,
        1);

    private sealed record SavedSubscription(
        ApprovedMailboxSubscription Subscription,
        string? ExpectedPriorSubscriptionId);

    private sealed record RecordedFailure(
        Guid ApprovedMailboxId,
        string Code,
        DateTimeOffset AttemptedAtUtc);

    private sealed class FakeStore(params ApprovedMailboxSubscriptionMaintenanceCandidate[] candidates)
        : IApprovedMailboxSubscriptionStore
    {
        public List<string> Reads { get; } = [];
        public List<SavedSubscription> Saved { get; } = [];
        public List<RecordedFailure> Failures { get; } = [];
        public Guid? LoseSaveFor { get; init; }

        public Task<IReadOnlyList<ApprovedMailboxSubscriptionMaintenanceCandidate>> ListMaintenanceCandidatesAsync(
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken)
        {
            Reads.Add(nameof(ListMaintenanceCandidatesAsync));
            return Task.FromResult<IReadOnlyList<ApprovedMailboxSubscriptionMaintenanceCandidate>>(candidates);
        }

        public Task SaveAsync(
            ApprovedMailboxSubscription subscription,
            string? expectedPriorSubscriptionId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (subscription.ApprovedMailboxId == LoseSaveFor)
            {
                throw new ApprovedMailboxSubscriptionMaintenanceLostException();
            }

            Saved.Add(new(subscription, expectedPriorSubscriptionId));
            return Task.CompletedTask;
        }

        public Task RecordMaintenanceFailureAsync(
            Guid approvedMailboxId,
            long expectedGeneration,
            string? expectedSubscriptionId,
            string failureCode,
            DateTimeOffset attemptedAtUtc,
            CancellationToken cancellationToken)
        {
            Failures.Add(new(approvedMailboxId, failureCode, attemptedAtUtc));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ApprovedMailboxSubscription>> ListAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ApprovedMailboxSubscription?> GetActiveAsync(
            string subscriptionId,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed record GraphRequest(HttpMethod Method, string Path, string Body);

    /// <summary>A Graph endpoint that answers each call with the next status, then repeats the last.</summary>
    private sealed class FakeGraph(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        private int calls;

        public List<GraphRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new(request.Method, request.RequestUri!.AbsolutePath, body));
            var status = statuses.Length == 0
                ? HttpStatusCode.Created
                : statuses[Math.Min(calls++, statuses.Length - 1)];
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(
                    $"{{\"id\":\"{SubscriptionId}\",\"expirationDateTime\":\"2026-10-06T10:05:03Z\"}}",
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class CountingLogger : ILogger<MaintainMailboxChangeSubscriptions>
    {
        public int Information { get; private set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Information)
            {
                Information++;
            }
        }
    }

    private sealed class FixedCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }
}
