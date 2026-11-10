using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Identity;

namespace Pegasus.Web;

internal static partial class GraphMailWebhook
{
    private const int MaximumNotifications = 100;

    // Graph hangs up on a slow answer and resends only minutes later, so a batch
    // it delivered is queued even if it has already gone. The bound stops a stuck
    // store or queue from holding the request open.
    private static readonly TimeSpan DeliveryBound = TimeSpan.FromSeconds(30);

    public static async Task<IResult> HandleAsync(
        HttpRequest request,
        IApprovedMailboxSubscriptionStore subscriptions,
        IMailboxWakeEnqueuer enqueuer,
        IConfiguration configuration,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(GraphMailWebhook));
        if (request.Query.TryGetValue("validationToken", out var token))
        {
            return Results.Text(token.ToString(), "text/plain", Encoding.UTF8);
        }

        GraphNotificationBatch? batch;
        try
        {
            batch = await request.ReadFromJsonAsync<GraphNotificationBatch>(
                cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return Results.Accepted();
        }

        if (batch?.Value is null || batch.Value.Count is 0 or > MaximumNotifications)
        {
            return Results.Accepted();
        }

        var expectedClientState = configuration["Graph:ChangeNotificationClientState"];
        var expectedTenantId = configuration["Graph:TenantId"];
        if (string.IsNullOrWhiteSpace(expectedClientState)
            || expectedClientState.Length > 128
            || string.IsNullOrWhiteSpace(expectedTenantId))
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        using var delivery = new CancellationTokenSource(DeliveryBound, timeProvider);
        var subscriptionsById = new Dictionary<string, ApprovedMailboxSubscription?>(StringComparer.Ordinal);
        foreach (var notification in batch.Value)
        {
            if (!MatchesSecret(notification.ClientState, expectedClientState)
                || !string.Equals(notification.TenantId, expectedTenantId, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(notification.SubscriptionId))
            {
                LogSkipped(logger, notification.SubscriptionId, "client state, tenant or subscription id refused");
                continue;
            }

            if (!TryParseWakeKind(notification, out var wakeKind))
            {
                LogSkipped(logger, notification.SubscriptionId, "not a created message or a known lifecycle event");
                continue;
            }

            if (!subscriptionsById.TryGetValue(notification.SubscriptionId, out var subscription))
            {
                subscription = await subscriptions.GetActiveAsync(
                    notification.SubscriptionId,
                    timeProvider.GetUtcNow(),
                    delivery.Token);
                subscriptionsById[notification.SubscriptionId] = subscription;
            }

            // The subscription id Graph issued, the client state and the tenant
            // name exactly one approved mailbox at its current generation; the
            // resource is read only for the message id, since Graph names the
            // mailbox by its user id whatever form the subscription used (R883).
            if (subscription is null)
            {
                LogSkipped(logger, notification.SubscriptionId,
                    "no active subscription for the mailbox's current generation");
                continue;
            }

            if (!Guid.TryParse(subscription.SubscriptionId, out var subscriptionId))
            {
                LogSkipped(logger, notification.SubscriptionId, "the subscription id is not a GUID");
                continue;
            }

            await enqueuer.EnqueueAsync(
                subscription.ApprovedMailboxId,
                subscriptionId,
                subscription.Generation,
                wakeKind,
                wakeKind == MailboxWakeKind.Created
                    ? ParseImmutableMessageId(notification.Resource)
                    : null,
                delivery.Token);
        }

        return Results.Accepted();
    }

    private static string? ParseImmutableMessageId(string? resource)
    {
        if (string.IsNullOrWhiteSpace(resource))
        {
            return null;
        }
        var marker = resource.LastIndexOf("/messages/", StringComparison.OrdinalIgnoreCase);
        if (marker < 0)
        {
            return null;
        }
        var encoded = resource[(marker + "/messages/".Length)..];
        if (encoded.Length == 0 || encoded.Contains('/'))
        {
            return null;
        }
        var value = Uri.UnescapeDataString(encoded);
        return value.Length <= 500
            && !value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character))
                ? value
                : null;
    }

    private static bool TryParseWakeKind(
        GraphNotification notification,
        out MailboxWakeKind wakeKind)
    {
        wakeKind = notification.LifecycleEvent switch
        {
            "missed" => MailboxWakeKind.Missed,
            "subscriptionRemoved" => MailboxWakeKind.SubscriptionRemoved,
            "reauthorizationRequired" => MailboxWakeKind.ReauthorizationRequired,
            _ => MailboxWakeKind.Created
        };
        return string.IsNullOrWhiteSpace(notification.LifecycleEvent)
            ? string.Equals(notification.ChangeType, "created", StringComparison.Ordinal)
            : notification.LifecycleEvent is "missed" or "subscriptionRemoved" or "reauthorizationRequired";
    }

    private static bool MatchesSecret(string? presented, string expected)
    {
        if (presented is null)
        {
            return false;
        }

        var presentedBytes = Encoding.UTF8.GetBytes(presented);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return presentedBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(presentedBytes, expectedBytes);
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Graph mail notification for subscription {SubscriptionId} was not queued: {Reason}.")]
    private static partial void LogNotQueued(ILogger logger, string subscriptionId, string reason);

    // The endpoint is anonymous: only a well-formed subscription id reaches the log.
    private static void LogSkipped(ILogger logger, string? presentedSubscriptionId, string reason) =>
        LogNotQueued(
            logger,
            Guid.TryParse(presentedSubscriptionId, out var subscriptionId) ? subscriptionId.ToString("D") : "not a subscription id",
            reason);

    private sealed record GraphNotificationBatch(IReadOnlyList<GraphNotification>? Value);

    private sealed record GraphNotification(
        string? SubscriptionId,
        string? ClientState,
        string? TenantId,
        string? Resource,
        string? ChangeType,
        string? LifecycleEvent);
}
