using Pegasus.Infrastructure.Intake.DocumentExtraction.Cfb;

namespace Pegasus.Infrastructure.Intake.DocumentExtraction.Word;

internal sealed record WordBinaryExtractionLimits
{
    public static WordBinaryExtractionLimits Default { get; } = new();

    public int MaximumInputBytes { get; init; } = 10 * 1024 * 1024;

    public uint MaximumCharacters { get; init; } = 16 * 1024 * 1024;

    public int MaximumPieces { get; init; } = 1_000_000;

    public CompoundFileReadLimits CompoundFile { get; init; } = CompoundFileReadLimits.Default;
}
