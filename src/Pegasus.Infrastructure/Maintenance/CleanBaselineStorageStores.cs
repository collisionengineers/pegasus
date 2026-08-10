using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

namespace Pegasus.Infrastructure.Maintenance;

internal sealed class CleanBaselineBlobStore(BlobContainerClient container) : ICleanBaselineBlobStore
{
    internal static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan LeaseReleaseTimeout = TimeSpan.FromSeconds(10);

    public async Task<IReadOnlyList<CleanBaselineBlobItem>> InspectExactAsync(
        IReadOnlyDictionary<string, (int Total, int Target)> references,
        CancellationToken cancellationToken)
    {
        var result = new List<CleanBaselineBlobItem>(references.Count);
        foreach (var (name, counts) in references.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var client = container.GetBlobClient(name);
            Response<BlobProperties> response;
            try
            {
                response = await client.GetPropertiesAsync(cancellationToken: cancellationToken);
            }
            catch (RequestFailedException exception) when (exception.Status == 404)
            {
                throw new InvalidOperationException(
                    "A SQL-referenced transient-intake Blob is missing; cleanup cannot prove custody.",
                    exception);
            }
            var properties = response.Value;
            var contentHash = properties.Metadata.TryGetValue("sha256", out var metadataHash)
                && IsSha256(metadataHash)
                    ? metadataHash.ToLowerInvariant()
                    : HashFromContentAddress(name);
            result.Add(new(
                name,
                properties.ETag.ToString(),
                properties.ContentLength,
                contentHash,
                counts.Total,
                counts.Target));
        }
        return result;
    }

    public async Task<int> DeleteExactAsync(
        IReadOnlyList<CleanBaselineBlobItem> blobs,
        CancellationToken cancellationToken)
    {
        await using var prepared = await PrepareDeleteAsync(blobs, cancellationToken);
        return await prepared.DeleteAsync(cancellationToken);
    }

    public async Task<ICleanBaselinePreparedDeletion> PrepareDeleteAsync(
        IReadOnlyList<CleanBaselineBlobItem> blobs,
        CancellationToken cancellationToken)
    {
        var prepared = new List<(CleanBaselineBlobItem Item, BlobClient Blob, BlobLeaseClient Lease)>();
        try
        {
            foreach (var item in blobs)
            {
                if (item.TotalReferenceCount != item.TargetReferenceCount)
                {
                    throw new InvalidOperationException("A shared Blob cannot be deleted.");
                }
                var blob = container.GetBlobClient(item.Name);
                var lease = blob.GetBlobLeaseClient(Guid.NewGuid().ToString("D"));
                try
                {
                    await lease.AcquireAsync(
                        LeaseDuration,
                        new RequestConditions { IfMatch = new ETag(item.ETag) },
                        cancellationToken);
                }
                catch (RequestFailedException exception) when (exception.Status is 404 or 409 or 412)
                {
                    throw new InvalidOperationException(
                        "A Blob identity, ETag, or lease state drifted before the destructive phase.",
                        exception);
                }
                prepared.Add((item, blob, lease));
            }
            return new PreparedBlobDeletion(prepared);
        }
        catch (Exception preparationException)
        {
            var releaseFailures = await ReleaseEveryLeaseAsync(
                prepared.Select(entry => Release(entry.Lease)).ToArray(),
                LeaseReleaseTimeout);
            if (releaseFailures.Count > 0)
            {
                throw new AggregateException(
                    "Blob preparation failed and one or more finite lease releases also failed.",
                    new[] { preparationException }.Concat(releaseFailures));
            }
            throw;
        }
    }

    private sealed class PreparedBlobDeletion(
        IReadOnlyList<(CleanBaselineBlobItem Item, BlobClient Blob, BlobLeaseClient Lease)> prepared)
        : ICleanBaselinePreparedDeletion
    {
        private readonly HashSet<string> deletedNames = new(StringComparer.Ordinal);

        public async Task<int> DeleteAsync(CancellationToken cancellationToken)
        {
            foreach (var (item, blob, lease) in prepared)
            {
                try
                {
                    await lease.RenewAsync(cancellationToken: cancellationToken);
                }
                catch (RequestFailedException exception) when (exception.Status is 404 or 409 or 412)
                {
                    throw new InvalidOperationException(
                        "A finite Blob lease expired or drifted before exact deletion.",
                        exception);
                }
                var response = await blob.DeleteIfExistsAsync(
                    DeleteSnapshotsOption.IncludeSnapshots,
                    new BlobRequestConditions
                    {
                        IfMatch = new ETag(item.ETag),
                        LeaseId = lease.LeaseId
                    },
                    cancellationToken);
                if (!response.Value)
                {
                    throw new InvalidOperationException(
                        "A leased manifest Blob disappeared before exact deletion.");
                }
                deletedNames.Add(item.Name);
            }
            if (deletedNames.Count != prepared.Count)
            {
                throw new InvalidOperationException(
                    "Exact Blob deletion did not remove every manifest Blob.");
            }
            return deletedNames.Count;
        }

        public async ValueTask DisposeAsync()
        {
            var releases = prepared
                .Where(entry => !deletedNames.Contains(entry.Item.Name))
                .Select(entry => Release(entry.Lease))
                .ToArray();
            var releaseFailures = await ReleaseEveryLeaseAsync(releases, LeaseReleaseTimeout);
            if (releaseFailures.Count > 0)
            {
                throw new AggregateException(
                    "One or more finite Blob leases could not be released; each remaining lease expires automatically.",
                    releaseFailures);
            }
        }
    }

    private static Func<CancellationToken, Task> Release(BlobLeaseClient lease) => async cancellationToken =>
    {
        await lease.ReleaseAsync(cancellationToken: cancellationToken);
    };

    internal static async Task<IReadOnlyList<Exception>> ReleaseEveryLeaseAsync(
        IReadOnlyList<Func<CancellationToken, Task>> releases,
        TimeSpan timeoutPerLease)
    {
        var failures = new List<Exception>();
        foreach (var release in releases)
        {
            using var timeout = new CancellationTokenSource(timeoutPerLease);
            try
            {
                await release(timeout.Token);
            }
            catch (RequestFailedException exception) when (exception.Status is 404 or 409 or 412)
            {
                // The finite lease is already absent, expired, or its Blob drifted.
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }
        return failures;
    }

    public async Task<int> CountExistingAsync(
        IReadOnlyList<CleanBaselineBlobItem> blobs,
        CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var item in blobs)
        {
            if (await container.GetBlobClient(item.Name).ExistsAsync(cancellationToken))
            {
                count++;
            }
        }
        return count;
    }

    internal static CleanBaselineBlobStore ForProduction(
        string account,
        string container,
        TokenCredential credential) => new(
            new BlobServiceClient(new Uri($"https://{account}.blob.core.windows.net"), credential)
                .GetBlobContainerClient(container));

    internal static CleanBaselineBlobStore ForLocalFixture(
        string connectionString,
        string container) => new(new BlobContainerClient(connectionString, container));

    private static string? HashFromContentAddress(string name)
    {
        var candidate = name.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return candidate is not null && IsSha256(candidate)
            ? candidate.ToLowerInvariant()
            : null;
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);
}

internal sealed class CleanBaselineQueueStore(IReadOnlyDictionary<string, QueueClient> queues)
    : ICleanBaselineQueueStore
{
    internal static readonly string[] QueueNames = ["intake-work", "intake-work-poison"];

    public async Task<CleanBaselineQueueInventory> InspectAsync(
        IReadOnlySet<Guid> targetStagedReceiptIds,
        CancellationToken cancellationToken)
    {
        var messages = new List<CleanBaselineQueueItem>();
        var stops = new List<CleanBaselineStopCondition>();
        foreach (var queueName in QueueNames)
        {
            var queue = RequireQueue(queueName);
            var properties = await queue.GetPropertiesAsync(cancellationToken);
            var approximateCount = properties.Value.ApproximateMessagesCount;
            if (approximateCount >= 32)
            {
                stops.Add(IntakeCleanBaselineService.Stop(
                    "queue_inventory_unbounded",
                    "Queue",
                    queueName,
                    "Azure Queue peek cannot prove a read-only inventory above 32 messages."));
                continue;
            }
            var response = await queue.PeekMessagesAsync(32, cancellationToken);
            if (response.Value.Length < approximateCount)
            {
                stops.Add(IntakeCleanBaselineService.Stop(
                    "queue_inventory_drift",
                    "Queue",
                    queueName,
                    "The queue count changed during the read-only inventory."));
            }
            foreach (var message in response.Value)
            {
                var body = message.Body.ToString().Trim();
                if (!Guid.TryParseExact(body, "D", out var stagedReceiptId))
                {
                    stops.Add(IntakeCleanBaselineService.Stop(
                        "unknown_queue_message",
                        "QueueMessage",
                        $"{queueName}:{message.MessageId}",
                        "The queue message is not one canonical staged-receipt GUID."));
                    continue;
                }
                if (!targetStagedReceiptIds.Contains(stagedReceiptId))
                {
                    stops.Add(IntakeCleanBaselineService.Stop(
                        "non_target_queue_message",
                        "QueueMessage",
                        $"{queueName}:{message.MessageId}",
                        "Execute requires quiescent queues containing only exact manifest targets."));
                    continue;
                }
                messages.Add(new(
                    queueName,
                    message.MessageId,
                    IntakeCleanBaselineService.Sha256(body),
                    stagedReceiptId,
                    message.InsertedOn,
                    message.ExpiresOn));
            }
        }
        return new(
            messages
                .OrderBy(item => item.Queue, StringComparer.Ordinal)
                .ThenBy(item => item.MessageId, StringComparer.Ordinal)
                .ToArray(),
            stops);
    }

    public async Task<int> DeleteExactAsync(
        IReadOnlyList<CleanBaselineQueueItem> messages,
        CancellationToken cancellationToken)
    {
        await using var prepared = await PrepareDeleteAsync(messages, cancellationToken);
        return await prepared.DeleteAsync(cancellationToken);
    }

    public async Task<ICleanBaselinePreparedDeletion> PrepareDeleteAsync(
        IReadOnlyList<CleanBaselineQueueItem> messages,
        CancellationToken cancellationToken)
    {
        var planned = messages.ToDictionary(
            item => $"{item.Queue}:{item.MessageId}",
            StringComparer.Ordinal);
        var receivedMessages = new List<(QueueClient Queue, string QueueName, QueueMessage Message)>();
        try
        {
            foreach (var queueName in QueueNames)
            {
                var queue = RequireQueue(queueName);
                var properties = await queue.GetPropertiesAsync(cancellationToken);
                if (properties.Value.ApproximateMessagesCount >= 32)
                {
                    throw new InvalidOperationException(
                        "Execute refuses a queue that cannot be completely enumerated in one bounded receive.");
                }
                var expectedCount = planned.Values.Count(item =>
                    item.Queue.Equals(queueName, StringComparison.Ordinal));
                if (properties.Value.ApproximateMessagesCount != expectedCount)
                {
                    throw new InvalidOperationException(
                        "The quiescent queue count differs from the exact manifest before receive.");
                }
                var received = await queue.ReceiveMessagesAsync(
                    maxMessages: 32,
                    visibilityTimeout: TimeSpan.FromMinutes(5),
                    cancellationToken: cancellationToken);
                if (received.Value.Length < properties.Value.ApproximateMessagesCount)
                {
                    throw new InvalidOperationException(
                        "The queue count drifted during the bounded Execute census.");
                }
                receivedMessages.AddRange(received.Value.Select(message => (queue, queueName, message)));
            }

            var observedPlanned = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (_, queueName, message) in receivedMessages)
            {
                var identity = $"{queueName}:{message.MessageId}";
                var body = message.Body.ToString().Trim();
                if (planned.TryGetValue(identity, out var expected))
                {
                    if (!string.Equals(
                            expected.BodySha256,
                            IntakeCleanBaselineService.Sha256(body),
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("A queue message body drifted after Plan.");
                    }
                    observedPlanned.Add(identity);
                }
                else if (!Guid.TryParseExact(body, "D", out var stagedReceiptId))
                {
                    throw new InvalidOperationException(
                        "An unknown queue message appeared after Plan; the whole run must stop.");
                }
                else
                {
                    throw new InvalidOperationException(
                        $"An unapproved queue identity for staged receipt {stagedReceiptId:D} appeared after Plan.");
                }
            }

            if (!observedPlanned.SetEquals(planned.Keys))
            {
                throw new InvalidOperationException(
                    "Execute did not receive every exact manifest queue message; queue identity drift is a stop condition.");
            }

            return new PreparedQueueDeletion(receivedMessages, planned);
        }
        catch
        {
            foreach (var (queue, _, message) in receivedMessages)
            {
                try
                {
                    await ReleaseAsync(queue, message, cancellationToken);
                }
                catch (RequestFailedException)
                {
                    // The five-minute receive visibility timeout remains the fail-safe release bound.
                }
            }
            throw;
        }
    }

    private sealed class PreparedQueueDeletion(
        IReadOnlyList<(QueueClient Queue, string QueueName, QueueMessage Message)> received,
        IReadOnlyDictionary<string, CleanBaselineQueueItem> planned)
        : ICleanBaselinePreparedDeletion
    {
        private readonly HashSet<string> deletedIdentities = new(StringComparer.Ordinal);

        public async Task<int> DeleteAsync(CancellationToken cancellationToken)
        {
            foreach (var (queue, queueName, message) in received)
            {
                var identity = $"{queueName}:{message.MessageId}";
                if (!planned.ContainsKey(identity))
                {
                    throw new InvalidOperationException(
                        "The prepared Queue census contains a non-manifest identity.");
                }
                await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
                deletedIdentities.Add(identity);
            }
            if (deletedIdentities.Count != planned.Count)
            {
                throw new InvalidOperationException(
                    "Exact queue deletion did not remove every manifest message.");
            }
            return deletedIdentities.Count;
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var (queue, queueName, message) in received)
            {
                if (deletedIdentities.Contains($"{queueName}:{message.MessageId}"))
                {
                    continue;
                }
                try
                {
                    await ReleaseAsync(queue, message, CancellationToken.None);
                }
                catch (RequestFailedException)
                {
                    // The bounded visibility timeout remains the fail-safe release path.
                }
            }
        }
    }

    public async Task<int> CountTargetMessagesAsync(
        IReadOnlySet<Guid> targetStagedReceiptIds,
        CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var queueName in QueueNames)
        {
            var queue = RequireQueue(queueName);
            var properties = await queue.GetPropertiesAsync(cancellationToken);
            if (properties.Value.ApproximateMessagesCount >= 32)
            {
                throw new InvalidOperationException("Verify cannot completely inspect a queue above 32 messages.");
            }
            var peeked = await queue.PeekMessagesAsync(32, cancellationToken);
            if (peeked.Value.Length < properties.Value.ApproximateMessagesCount)
            {
                throw new InvalidOperationException("The queue count drifted during Verify.");
            }
            foreach (var message in peeked.Value)
            {
                var body = message.Body.ToString().Trim();
                if (!Guid.TryParseExact(body, "D", out var stagedReceiptId))
                {
                    throw new InvalidOperationException(
                        "Verify found an unknown queue message and cannot prove the clean baseline.");
                }
                if (targetStagedReceiptIds.Contains(stagedReceiptId))
                {
                    count++;
                }
            }
        }
        return count;
    }

    internal static CleanBaselineQueueStore ForProduction(
        string account,
        TokenCredential credential)
    {
        var service = new QueueServiceClient(
            new Uri($"https://{account}.queue.core.windows.net"),
            credential);
        return new(QueueNames.ToDictionary(
            name => name,
            service.GetQueueClient,
            StringComparer.Ordinal));
    }

    internal static CleanBaselineQueueStore ForLocalFixture(string connectionString) => new(
        QueueNames.ToDictionary(
            name => name,
            name => new QueueClient(connectionString, name),
            StringComparer.Ordinal));

    private QueueClient RequireQueue(string name) =>
        queues.TryGetValue(name, out var queue)
            ? queue
            : throw new InvalidOperationException($"The exact queue client {name} is missing.");

    private static async Task ReleaseAsync(
        QueueClient queue,
        QueueMessage message,
        CancellationToken cancellationToken) =>
        await queue.UpdateMessageAsync(
            message.MessageId,
            message.PopReceipt,
            message.Body,
            TimeSpan.Zero,
            cancellationToken);
}
