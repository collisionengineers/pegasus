namespace CollisionDocNet.Core;

/// <summary>A validated half-open byte range within a known containing length.</summary>
public readonly record struct BinaryRange
{
    private BinaryRange(long offset, long length)
    {
        Offset = offset;
        Length = length;
    }

    public long Offset { get; }

    public long Length { get; }

    public long End => checked(Offset + Length);

    public static BinaryRange Create(long offset, long length, long containingLength)
    {
        if (!TryCreate(offset, length, containingLength, out BinaryRange range))
        {
            throw new ArgumentOutOfRangeException(nameof(length), "The range is outside the containing length.");
        }

        return range;
    }

    public static bool TryCreate(
        long offset,
        long length,
        long containingLength,
        out BinaryRange range)
    {
        range = default;
        if (offset < 0 || length < 0 || containingLength < 0 || offset > containingLength)
        {
            return false;
        }

        if (length > containingLength - offset)
        {
            return false;
        }

        range = new BinaryRange(offset, length);
        return true;
    }
}
