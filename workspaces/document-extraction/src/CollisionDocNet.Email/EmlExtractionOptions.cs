namespace CollisionDocNet.Email;

public sealed record EmlExtractionOptions
{
    public static EmlExtractionOptions Strict { get; } = new();

    public bool AllowLfOnlyLines { get; init; } = true;
    public int MaximumHeaderLineBytes { get; init; } = 998;
    public int MaximumHeaderCount { get; init; } = 10_000;
}
