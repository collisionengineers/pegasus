using System.Security.Cryptography;

namespace Pegasus.Infrastructure.Reports;

/// <summary>
/// The prepared signature PNG, kept by the SHA-256 of the stored signature
/// it was prepared from, so a signatory's signature is decoded and re-encoded
/// once rather than for every artifact rendered. A changed signature has a new
/// hash and so a new entry; the oldest entry goes when the cache is full. A
/// preparation that fails is never kept: the next render prepares it again and
/// fails the same way.
/// </summary>
internal sealed class PreparedSignatureCache(int capacity)
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, byte[]> prepared = [];
    private readonly Queue<string> oldestFirst = new();

    internal int Count
    {
        get
        {
            lock (gate)
            {
                return prepared.Count;
            }
        }
    }

    /// <summary>
    /// The prepared bytes for <paramref name="stored"/>. The preparation runs
    /// outside the lock; two renders that miss together both prepare it and
    /// the first result stays.
    /// </summary>
    internal byte[] GetOrPrepare(byte[] stored, Func<byte[], byte[]> prepare)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(prepare);
        var key = Convert.ToHexStringLower(SHA256.HashData(stored));
        lock (gate)
        {
            if (prepared.TryGetValue(key, out var kept))
            {
                return kept;
            }
        }

        var fresh = prepare(stored);
        lock (gate)
        {
            if (prepared.TryGetValue(key, out var raced))
            {
                return raced;
            }

            if (prepared.Count >= capacity)
            {
                prepared.Remove(oldestFirst.Dequeue());
            }

            prepared[key] = fresh;
            oldestFirst.Enqueue(key);
            return fresh;
        }
    }
}
