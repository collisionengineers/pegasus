using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Identity;

namespace Pegasus.Web;

internal static class GraphMailWebhook
{
    private const int MaximumNotifications = 100;

    public static async Task<IResult> HandleAsync(
        HttpRequest request,
        IApprovedMailboxSubscriptionStore subscriptions,
        IMailboxWakeEnqueuer enqueuer,
        IConfiguration configuration,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
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

        foreach (var notification in batch.Value)
        {
            if (!MatchesSecret(notification.ClientState, expectedClientState)
                || !string.Equals(notification.TenantId, expectedTenantId, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(notification.SubscriptionId))
            {
                continue;
            }

            var subscription = await subscriptions.GetActiveAsync(
                notification.SubscriptionId,
                timeProvider.GetUtcNow(),
                cancellationToken);
            if (subscription is null
                || !MatchesSubscribedMailbox(subscription.Resource, notification.Resource)
                || !TryParseWakeKind(notification, out var wakeKind))
            {
                continue;
            }

            if (!Guid.TryParse(subscription.SubscriptionId, out var subscriptionId))
            {
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
                cancellationToken);
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

    private static bool MatchesSubscribedMailbox(string subscribedResource, string? notifiedResource)
    {
        if (string.IsNullOrWhiteSpace(notifiedResource))
        {
            return false;
        }

        var folderSegment = subscribedResource.IndexOf("/mailFolders/", StringComparison.OrdinalIgnoreCase);
        if (folderSegment <= 0)
        {
            return false;
        }

        var mailboxPrefix = subscribedResource[..folderSegment];
        return notifiedResource.StartsWith(
                subscribedResource + "/",
                StringComparison.OrdinalIgnoreCase)
            || notifiedResource.StartsWith(
                mailboxPrefix + "/messages/",
                StringComparison.OrdinalIgnoreCase);
    }

    private sealed record GraphNotificationBatch(IReadOnlyList<GraphNotification>? Value);

    private sealed record GraphNotification(
        string? SubscriptionId,
        string? ClientState,
        string? TenantId,
        string? Resource,
        string? ChangeType,
        string? LifecycleEvent);
}
