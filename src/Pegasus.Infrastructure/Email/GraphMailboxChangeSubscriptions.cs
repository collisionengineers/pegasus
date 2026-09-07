using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Email;

public sealed class GraphMailboxChangeSubscriptions(
    TokenCredential credential,
    GraphApprovedMailboxOptions options,
    HttpClient httpClient)
{
    private static readonly TokenRequestContext TokenContext =
        new(["https://graph.microsoft.com/.default"]);

    public async Task<ApprovedMailboxSubscription> MaintainAsync(
        ApprovedMailboxSubscriptionMaintenanceCandidate candidate,
        Uri callbackUri,
        string clientState,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var expiry = nowUtc.AddDays(6);
        var resource = Resource(candidate.GraphMailboxId, candidate.InboxFolderIdentity);
        var current = candidate.Subscription;
        var canRenew = current is not null
            && current.LifecycleState != ApprovedMailboxSubscriptionLifecycleState.Removed
            && current.ExpiresAtUtc > nowUtc
            && current.Generation == candidate.Generation
            && string.Equals(current.Resource, resource, StringComparison.Ordinal);
        using var request = canRenew
            ? new HttpRequestMessage(
                HttpMethod.Patch,
                new Uri(options.BaseUri, $"subscriptions/{Uri.EscapeDataString(current!.SubscriptionId)}"))
            : new HttpRequestMessage(HttpMethod.Post, new Uri(options.BaseUri, "subscriptions"));
        request.Content = JsonContent.Create(!canRenew
            ? new
            {
                changeType = "created",
                notificationUrl = callbackUri.AbsoluteUri,
                lifecycleNotificationUrl = callbackUri.AbsoluteUri,
                resource,
                expirationDateTime = expiry,
                clientState,
                includeResourceData = false
            }
            : (object)new { expirationDateTime = expiry });
        var token = await credential.GetTokenAsync(TokenContext, cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var subscriptionId = root.TryGetProperty("id", out var idValue)
            ? idValue.GetString()
            : current?.SubscriptionId;
        var actualExpiry = root.TryGetProperty("expirationDateTime", out var expiryValue)
            && expiryValue.TryGetDateTimeOffset(out var parsedExpiry)
                ? parsedExpiry
                : expiry;
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            throw new InvalidDataException("Microsoft Graph returned no subscription identifier.");
        }

        return new(
            candidate.ApprovedMailboxId,
            subscriptionId,
            resource,
            actualExpiry,
            ApprovedMailboxSubscriptionLifecycleState.Active,
            nowUtc,
            null,
            candidate.Generation);
    }

    public static string Resource(string mailboxId, string inboxFolderId) =>
        $"users/{Uri.EscapeDataString(mailboxId)}/mailFolders/{Uri.EscapeDataString(inboxFolderId)}/messages";

    public static bool RequiresWrite(
        ApprovedMailboxSubscriptionMaintenanceCandidate candidate,
        DateTimeOffset nowUtc,
        DateTimeOffset renewBeforeUtc)
    {
        var current = candidate.Subscription;
        return current is null
            || current.LifecycleState != ApprovedMailboxSubscriptionLifecycleState.Active
            || current.ExpiresAtUtc <= nowUtc
            || current.ExpiresAtUtc <= renewBeforeUtc
            || current.Generation != candidate.Generation
            || !string.Equals(
                current.Resource,
                Resource(candidate.GraphMailboxId, candidate.InboxFolderIdentity),
                StringComparison.Ordinal);
    }
}
