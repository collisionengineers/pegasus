using System.Collections.Concurrent;
using Pegasus.Core.Actors;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The staff cookie check's account read and the staff display names, each
/// kept for <see cref="StaffSessionPolicy.AccountReadLifetime"/>. Every account
/// writer in this process evicts the account after its commit, so a change made
/// in Pegasus is seen on the next request; a change made outside it (direct
/// SQL, the intake wipe) is seen once the read expires. The Web runs as one
/// instance (ADR-0058), which is what makes in-process eviction exact.
/// </summary>
public sealed class StaffAccountReads(TimeProvider timeProvider)
{
    // One entry per staff account that has signed in; there are few, so nothing prunes.
    private readonly ConcurrentDictionary<Guid, (StaffAccountRead Read, DateTimeOffset ReadAt)> reads = new();
    private readonly ConcurrentDictionary<Guid, (string Name, DateTimeOffset ReadAt)> names = new();
    private long evictions;

    /// <summary>Taken before reading the account; <see cref="Set"/> needs it.</summary>
    public long Generation => Interlocked.Read(ref evictions);

    public StaffAccountRead? TryGet(Guid staffId) =>
        reads.TryGetValue(staffId, out var entry)
            && timeProvider.GetUtcNow() - entry.ReadAt < StaffSessionPolicy.AccountReadLifetime
            ? entry.Read
            : null;

    /// <summary>
    /// Keeps a read unless an eviction happened since <paramref name="generation"/>:
    /// a row read before a commit must not outlive the commit's eviction.
    /// </summary>
    public void Set(Guid staffId, StaffAccountRead read, long generation)
    {
        reads[staffId] = (read, timeProvider.GetUtcNow());
        if (Interlocked.Read(ref evictions) != generation)
        {
            reads.TryRemove(staffId, out _);
        }
    }

    /// <summary>The kept username of an account, or null when none is kept or it has expired.</summary>
    public string? TryGetName(Guid staffId) =>
        names.TryGetValue(staffId, out var entry)
            && timeProvider.GetUtcNow() - entry.ReadAt < StaffSessionPolicy.AccountReadLifetime
            ? entry.Name
            : null;

    /// <summary>
    /// Keeps a username unless an eviction happened since <paramref name="generation"/>,
    /// as <see cref="Set"/> does for the account read.
    /// </summary>
    public void SetName(Guid staffId, string name, long generation)
    {
        names[staffId] = (name, timeProvider.GetUtcNow());
        if (Interlocked.Read(ref evictions) != generation)
        {
            names.TryRemove(staffId, out _);
        }
    }

    public void Evict(Guid staffId)
    {
        Interlocked.Increment(ref evictions);
        reads.TryRemove(staffId, out _);
        names.TryRemove(staffId, out _);
    }
}

/// <summary>The account fields the staff cookie check needs.</summary>
public sealed record StaffAccountRead(string? SecurityStamp, bool IsEnabled, bool MustChangePassword);
