namespace CollisionDocNet.Storage.CompoundFile;

public sealed record CompoundFileReadLimits
{
    public static CompoundFileReadLimits Default { get; } = new();

    public int MaximumInputBytes { get; init; } = 16 * 1024 * 1024;

    public int MaximumSectors { get; init; } = 32_768;

    public int MaximumDirectoryEntries { get; init; } = 131_072;

    public long MaximumStreamBytes { get; init; } = 16 * 1024 * 1024;

    public long MaximumTotalStreamBytes { get; init; } = 64 * 1024 * 1024;
}
