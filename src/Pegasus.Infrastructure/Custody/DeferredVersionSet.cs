namespace Pegasus.Infrastructure.Custody;

/// <summary>
/// The versions a Worker sweep has set aside for now: this process does not
/// list one again until its interval has passed. At most
/// <see cref="Maximum"/> versions are remembered; past that the one deferred
/// longest ago is dropped.
/// </summary>
internal sealed class DeferredVersionSet(TimeProvider timeProvider, TimeSpan interval)
{
    private const int Maximum = 500;

    private readonly Lock gate = new();

    /// <summary>Deferred version → when it may be listed again.</summary>
    private readonly Dictionary<Guid, DateTimeOffset> deferred = [];

    /// <summary>The versions still deferred; any whose interval has passed are forgotten.</summary>
    public HashSet<Guid> Current(DateTimeOffset now)
    {
        lock (gate)
        {
            foreach (var expired in deferred.Where(entry => entry.Value <= now).Select(entry => entry.Key).ToArray())
            {
                deferred.Remove(expired);
            }
            return [.. deferred.Keys];
        }
    }

    public void Defer(Guid versionId)
    {
        var until = timeProvider.GetUtcNow() + interval;
        lock (gate)
        {
            deferred[versionId] = until;
            if (deferred.Count > Maximum)
            {
                deferred.Remove(deferred.MinBy(entry => entry.Value).Key);
            }
        }
    }
}
