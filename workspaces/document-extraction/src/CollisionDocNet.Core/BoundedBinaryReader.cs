using System.Buffers.Binary;

namespace CollisionDocNet.Core;

/// <summary>Checked random access over caller-supplied immutable memory.</summary>
public readonly struct BoundedBinaryReader
{
    private readonly ReadOnlyMemory<byte> _bytes;

    public BoundedBinaryReader(ReadOnlyMemory<byte> bytes) => _bytes = bytes;

    public int Length => _bytes.Length;

    public bool TryReadByte(long offset, out byte value)
    {
        value = default;
        if (!TryGetSpan(offset, sizeof(byte), out ReadOnlySpan<byte> span))
        {
            return false;
        }

        value = span[0];
        return true;
    }

    public bool TryReadUInt16LittleEndian(long offset, out ushort value)
    {
        value = default;
        if (!TryGetSpan(offset, sizeof(ushort), out ReadOnlySpan<byte> span))
        {
            return false;
        }

        value = BinaryPrimitives.ReadUInt16LittleEndian(span);
        return true;
    }

    public bool TryReadUInt32LittleEndian(long offset, out uint value)
    {
        value = default;
        if (!TryGetSpan(offset, sizeof(uint), out ReadOnlySpan<byte> span))
        {
            return false;
        }

        value = BinaryPrimitives.ReadUInt32LittleEndian(span);
        return true;
    }

    public bool TrySlice(long offset, long length, out ReadOnlyMemory<byte> slice)
    {
        slice = default;
        if (!BinaryRange.TryCreate(offset, length, _bytes.Length, out _)
            || offset > int.MaxValue
            || length > int.MaxValue)
        {
            return false;
        }

        slice = _bytes.Slice((int)offset, (int)length);
        return true;
    }

    private bool TryGetSpan(long offset, int length, out ReadOnlySpan<byte> span)
    {
        if (!TrySlice(offset, length, out ReadOnlyMemory<byte> memory))
        {
            span = default;
            return false;
        }

        span = memory.Span;
        return true;
    }
}
