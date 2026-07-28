using CollisionDocNet.Storage.CompoundFile;

namespace CollisionDocNet.Writer;

public sealed record WordBinaryExtractionLimits
{
    public static WordBinaryExtractionLimits Default { get; } = new();

    public int MaximumInputBytes { get; init; } = 10 * 1024 * 1024;

    public uint MaximumCharacters { get; init; } = 16 * 1024 * 1024;

    public int MaximumPieces { get; init; } = 1_000_000;

    public int MaximumPropertyRuns { get; init; } = 1_000_000;

    public int MaximumSprmsPerRun { get; init; } = 16_384;

    public int MaximumStructureRecords { get; init; } = 1_000_000;

    public int MaximumPassiveAssets { get; init; } = 65_536;

    public long MaximumPassiveAssetBytes { get; init; } = 10 * 1024 * 1024;

    public CompoundFileReadLimits CompoundFile { get; init; } = CompoundFileReadLimits.Default;
}
