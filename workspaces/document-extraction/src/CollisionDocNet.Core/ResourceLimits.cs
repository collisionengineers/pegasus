namespace CollisionDocNet.Core;

/// <summary>Versioned hard ceilings for one extraction operation and its nested work.</summary>
public sealed record ResourceLimits
{
    public const string CollisionSpikeTenMegabytePolicy = "collision-spike-10mb/1";

    public ResourceLimits(
        string policyId,
        long maxInputBytes,
        long maxDecodedBytes,
        int maxObjects,
        int maxTextCharacters,
        int maxAssets,
        long maxAssetBytes,
        int maxNestingDepth,
        TimeSpan maxElapsed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInputBytes);
        if (maxInputBytes > Array.MaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxInputBytes),
                "Materialized input must fit in one managed byte array.");
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDecodedBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxObjects);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTextCharacters);
        ArgumentOutOfRangeException.ThrowIfNegative(maxAssets);
        ArgumentOutOfRangeException.ThrowIfNegative(maxAssetBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(maxNestingDepth);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxElapsed, TimeSpan.Zero);

        PolicyId = policyId;
        MaxInputBytes = maxInputBytes;
        MaxDecodedBytes = maxDecodedBytes;
        MaxObjects = maxObjects;
        MaxTextCharacters = maxTextCharacters;
        MaxAssets = maxAssets;
        MaxAssetBytes = maxAssetBytes;
        MaxNestingDepth = maxNestingDepth;
        MaxElapsed = maxElapsed;
    }

    public string PolicyId { get; }
    public long MaxInputBytes { get; }
    public long MaxDecodedBytes { get; }
    public int MaxObjects { get; }
    public int MaxTextCharacters { get; }
    public int MaxAssets { get; }
    public long MaxAssetBytes { get; }
    public int MaxNestingDepth { get; }
    public TimeSpan MaxElapsed { get; }

    public static ResourceLimits CreateCollisionSpikeDefault() =>
        new(
            CollisionSpikeTenMegabytePolicy,
            maxInputBytes: 10 * 1024 * 1024,
            maxDecodedBytes: 100 * 1024 * 1024,
            maxObjects: 250_000,
            maxTextCharacters: 10_000_000,
            maxAssets: 10_000,
            maxAssetBytes: 100 * 1024 * 1024,
            maxNestingDepth: 8,
            maxElapsed: TimeSpan.FromMinutes(2));
}
