using Pegasus.Core.Documents;

namespace Pegasus.IntegrationTests.Support;

/// <summary>
/// One read-cache copy a filing published: the identity it was published under,
/// its hash and length, and either the bytes the caller held or the storage key
/// of the retained intake copy the publisher was told to read.
/// </summary>
internal sealed record PublishedCacheCopy(
    DocumentContentCacheKey Key,
    byte[]? Bytes,
    string? StorageKey,
    string Sha256,
    long ContentLength);

/// <summary>
/// A test double for <see cref="IDocumentContentCachePublisher"/> that records
/// what each filing publishes instead of writing a cache. The optional callback
/// runs at the moment of each publish, so a test can prove what the database
/// held by then: a filing publishes only after its transaction has committed.
/// </summary>
internal sealed class RecordingCachePublisher(Func<DocumentContentCacheKey, Task>? onPublish = null)
    : IDocumentContentCachePublisher
{
    private readonly List<PublishedCacheCopy> published = [];

    public IReadOnlyList<PublishedCacheCopy> Published
    {
        get
        {
            lock (published)
            {
                return [.. published];
            }
        }
    }

    public async Task PublishAsync(
        DocumentContentCacheKey key,
        Stream content,
        string sha256,
        long contentLength,
        CancellationToken cancellationToken)
    {
        using var copy = new MemoryStream();
        content.Position = 0;
        await content.CopyToAsync(copy, cancellationToken);
        if (onPublish is not null)
        {
            await onPublish(key);
        }
        Record(new(key, copy.ToArray(), null, sha256, contentLength));
    }

    public async Task PublishRetainedIntakeCopyAsync(
        DocumentContentCacheKey key,
        string storageKey,
        string sha256,
        long contentLength,
        CancellationToken cancellationToken)
    {
        if (onPublish is not null)
        {
            await onPublish(key);
        }
        Record(new(key, null, storageKey, sha256, contentLength));
    }

    private void Record(PublishedCacheCopy copy)
    {
        lock (published)
        {
            published.Add(copy);
        }
    }
}
