namespace CollisionDocNet.Core;

public enum ResourceKind
{
    InputBytes = 0,
    DecodedBytes,
    Objects,
    TextCharacters,
    Assets,
    AssetBytes,
}

public readonly record struct ResourceBudgetSnapshot(
    long InputBytes,
    long DecodedBytes,
    int Objects,
    int TextCharacters,
    int Assets,
    long AssetBytes,
    int MaximumNestingDepth);

/// <summary>Thread-safe cumulative accounting shared by a root extraction and nested work.</summary>
public sealed class ResourceBudget
{
    private readonly object _sync = new();
    private long _inputBytes;
    private long _decodedBytes;
    private int _objects;
    private int _textCharacters;
    private int _assets;
    private long _assetBytes;
    private int _maximumNestingDepth;

    public ResourceBudget(ResourceLimits limits) =>
        Limits = limits ?? throw new ArgumentNullException(nameof(limits));

    public ResourceLimits Limits { get; }

    public bool TryCharge(ResourceKind kind, long amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        lock (_sync)
        {
            return kind switch
            {
                ResourceKind.InputBytes => TryAdd(ref _inputBytes, amount, Limits.MaxInputBytes),
                ResourceKind.DecodedBytes => TryAdd(ref _decodedBytes, amount, Limits.MaxDecodedBytes),
                ResourceKind.Objects => TryAddInt(ref _objects, amount, Limits.MaxObjects),
                ResourceKind.TextCharacters => TryAddInt(
                    ref _textCharacters,
                    amount,
                    Limits.MaxTextCharacters),
                ResourceKind.Assets => TryAddInt(ref _assets, amount, Limits.MaxAssets),
                ResourceKind.AssetBytes => TryAdd(ref _assetBytes, amount, Limits.MaxAssetBytes),
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
        }
    }

    public bool TryObserveNestingDepth(int depth)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(depth);
        lock (_sync)
        {
            if (depth > Limits.MaxNestingDepth)
            {
                return false;
            }

            _maximumNestingDepth = Math.Max(_maximumNestingDepth, depth);
            return true;
        }
    }

    public ResourceBudgetSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return new ResourceBudgetSnapshot(
                _inputBytes,
                _decodedBytes,
                _objects,
                _textCharacters,
                _assets,
                _assetBytes,
                _maximumNestingDepth);
        }
    }

    private static bool TryAdd(ref long current, long amount, long maximum)
    {
        if (amount > maximum - current)
        {
            return false;
        }

        current += amount;
        return true;
    }

    private static bool TryAddInt(ref int current, long amount, int maximum)
    {
        if (amount > maximum - current)
        {
            return false;
        }

        current += checked((int)amount);
        return true;
    }
}
