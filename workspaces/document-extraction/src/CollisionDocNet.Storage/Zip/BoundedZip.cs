using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Text;

namespace CollisionDocNet.Storage.Zip;

public sealed record BoundedZipLimits
{
    public static BoundedZipLimits Default { get; } = new();

    public int MaximumInputBytes { get; init; } = 16 * 1024 * 1024;

    public int MaximumEntries { get; init; } = 100_000;

    public long MaximumEntryBytes { get; init; } = 64 * 1024 * 1024;

    public long MaximumTotalExpandedBytes { get; init; } = 256 * 1024 * 1024;

    public int MaximumCompressionRatio { get; init; } = 1_000;

    public int MaximumNameBytes { get; init; } = 4_096;
}

public enum BoundedZipReadError
{
    None = 0,
    InputLimitExceeded,
    EndRecordMissing,
    InvalidStructure,
    Zip64Invalid,
    EntryCountLimitExceeded,
    EntrySizeLimitExceeded,
    TotalExpandedLimitExceeded,
    CompressionRatioLimitExceeded,
    InvalidName,
    DuplicateName,
    OverlappingEntries,
    EncryptedEntry,
    UnsupportedCompression,
    CrcMismatch,
    DecompressionFailed,
    Cancelled,
}

public sealed record BoundedZipEntry(
    string Name,
    ushort CompressionMethod,
    uint Crc32,
    long CompressedSize,
    long ExpandedSize,
    long LocalHeaderOffset,
    ImmutableArray<byte> Content);

public sealed record BoundedZipArchive(
    bool UsesZip64,
    ImmutableArray<BoundedZipEntry> Entries);

public readonly record struct BoundedZipReadResult(
    BoundedZipArchive? Archive,
    BoundedZipReadError Error,
    long? Offset)
{
    public bool IsSuccess => Error == BoundedZipReadError.None && Archive is not null;
}

/// <summary>
/// Strict in-memory ZIP reader for stored and raw-deflate entries. It validates
/// the central/local records, ZIP64 size fields, CRCs and non-overlapping data
/// ranges before exposing bytes. Multi-disk, encryption and legacy non-ASCII
/// names without the UTF-8 flag are deliberately unsupported.
/// </summary>
public static class BoundedZipReader
{
    private const uint LocalSignature = 0x04034b50;
    private const uint CentralSignature = 0x02014b50;
    private const uint EndSignature = 0x06054b50;
    private const uint Zip64EndSignature = 0x06064b50;
    private const uint Zip64LocatorSignature = 0x07064b50;

    public static BoundedZipReadResult Read(
        ReadOnlyMemory<byte> bytes,
        BoundedZipLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        limits ??= BoundedZipLimits.Default;
        if (!AreValid(limits) || bytes.Length > limits.MaximumInputBytes)
        {
            return Failure(BoundedZipReadError.InputLimitExceeded);
        }

        try
        {
            return new Parser(bytes, limits, cancellationToken).Read();
        }
        catch (OperationCanceledException)
        {
            return Failure(BoundedZipReadError.Cancelled);
        }
        catch (OverflowException)
        {
            return Failure(BoundedZipReadError.InvalidStructure);
        }
        catch (InvalidDataException)
        {
            return Failure(BoundedZipReadError.DecompressionFailed);
        }
    }

    private static bool AreValid(BoundedZipLimits limits) =>
        limits.MaximumInputBytes >= 22 && limits.MaximumEntries > 0 &&
        limits.MaximumEntryBytes >= 0 && limits.MaximumTotalExpandedBytes >= 0 &&
        limits.MaximumCompressionRatio > 0 && limits.MaximumNameBytes > 0;

    private static BoundedZipReadResult Failure(BoundedZipReadError error, long? offset = null) =>
        new(null, error, offset);

    private sealed class Parser(
        ReadOnlyMemory<byte> source,
        BoundedZipLimits limits,
        CancellationToken cancellationToken)
    {
        private readonly ReadOnlyMemory<byte> _source = source;

        internal BoundedZipReadResult Read()
        {
            ReadOnlySpan<byte> bytes = _source.Span;
            int endOffset = FindEndRecord(bytes);
            if (endOffset < 0)
            {
                return Failure(BoundedZipReadError.EndRecordMissing);
            }

            ushort disk = U16(bytes, endOffset + 4);
            ushort centralDisk = U16(bytes, endOffset + 6);
            ushort diskEntries16 = U16(bytes, endOffset + 8);
            ushort totalEntries16 = U16(bytes, endOffset + 10);
            uint centralSize32 = U32(bytes, endOffset + 12);
            uint centralOffset32 = U32(bytes, endOffset + 16);
            if (disk != 0 || centralDisk != 0 || diskEntries16 != totalEntries16)
            {
                return Failure(BoundedZipReadError.InvalidStructure, endOffset);
            }

            bool usesZip64 = totalEntries16 == ushort.MaxValue ||
                centralSize32 == uint.MaxValue || centralOffset32 == uint.MaxValue;
            ulong totalEntries = totalEntries16;
            ulong centralSize = centralSize32;
            ulong centralOffset = centralOffset32;
            if (usesZip64)
            {
                BoundedZipReadResult? failure = ReadZip64End(
                    bytes, endOffset, out totalEntries, out centralSize, out centralOffset);
                if (failure is not null)
                {
                    return failure.Value;
                }
            }

            if (totalEntries > (ulong)limits.MaximumEntries)
            {
                return Failure(BoundedZipReadError.EntryCountLimitExceeded, endOffset);
            }

            ulong centralEnd = checked(centralOffset + centralSize);
            if (centralOffset > (ulong)bytes.Length || centralEnd > (ulong)endOffset)
            {
                return Failure(BoundedZipReadError.InvalidStructure, checked((long)centralOffset));
            }

            var metadata = new List<EntryMetadata>(checked((int)totalEntries));
            var names = new HashSet<string>(StringComparer.Ordinal);
            int cursor = checked((int)centralOffset);
            long totalExpanded = 0;
            for (ulong index = 0; index < totalEntries; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Has(bytes, cursor, 46) || U32(bytes, cursor) != CentralSignature)
                {
                    return Failure(BoundedZipReadError.InvalidStructure, cursor);
                }

                ushort flags = U16(bytes, cursor + 8);
                ushort method = U16(bytes, cursor + 10);
                uint crc = U32(bytes, cursor + 16);
                uint compressed32 = U32(bytes, cursor + 20);
                uint expanded32 = U32(bytes, cursor + 24);
                ushort nameLength = U16(bytes, cursor + 28);
                ushort extraLength = U16(bytes, cursor + 30);
                ushort commentLength = U16(bytes, cursor + 32);
                ushort startingDisk = U16(bytes, cursor + 34);
                uint localOffset32 = U32(bytes, cursor + 42);
                int recordLength = checked(46 + nameLength + extraLength + commentLength);
                if (!Has(bytes, cursor, recordLength) || (ulong)(cursor + recordLength) > centralEnd)
                {
                    return Failure(BoundedZipReadError.InvalidStructure, cursor);
                }

                if (nameLength == 0 || nameLength > limits.MaximumNameBytes)
                {
                    return Failure(BoundedZipReadError.InvalidName, cursor);
                }

                if ((flags & 0x2061) != 0)
                {
                    return Failure(BoundedZipReadError.EncryptedEntry, cursor);
                }

                if (method is not (0 or 8))
                {
                    return Failure(BoundedZipReadError.UnsupportedCompression, cursor);
                }

                ReadOnlySpan<byte> nameBytes = bytes.Slice(cursor + 46, nameLength);
                if (!TryDecodeName(nameBytes, (flags & 0x0800) != 0, out string name) ||
                    !IsSafeName(name))
                {
                    return Failure(BoundedZipReadError.InvalidName, cursor);
                }

                if (!names.Add(name))
                {
                    return Failure(BoundedZipReadError.DuplicateName, cursor);
                }

                ulong compressed = compressed32;
                ulong expanded = expanded32;
                ulong localOffset = localOffset32;
                if (compressed32 == uint.MaxValue || expanded32 == uint.MaxValue ||
                    localOffset32 == uint.MaxValue || startingDisk == ushort.MaxValue)
                {
                    ReadOnlySpan<byte> extra = bytes.Slice(cursor + 46 + nameLength, extraLength);
                    if (!TryReadZip64Extra(extra, expanded32, compressed32, localOffset32, startingDisk,
                        out expanded, out compressed, out localOffset, out uint diskNumber) || diskNumber != 0)
                    {
                        return Failure(BoundedZipReadError.Zip64Invalid, cursor);
                    }
                }
                else if (startingDisk != 0)
                {
                    return Failure(BoundedZipReadError.InvalidStructure, cursor);
                }

                if (expanded > (ulong)limits.MaximumEntryBytes)
                {
                    return Failure(BoundedZipReadError.EntrySizeLimitExceeded, cursor);
                }

                totalExpanded = checked(totalExpanded + checked((long)expanded));
                if (totalExpanded > limits.MaximumTotalExpandedBytes)
                {
                    return Failure(BoundedZipReadError.TotalExpandedLimitExceeded, cursor);
                }

                if (expanded > 0 && ExceedsCompressionRatio(
                    expanded, compressed, (ulong)limits.MaximumCompressionRatio))
                {
                    return Failure(BoundedZipReadError.CompressionRatioLimitExceeded, cursor);
                }

                bool usesZip64Sizes = compressed32 == uint.MaxValue || expanded32 == uint.MaxValue;
                metadata.Add(new(name, flags, method, crc, compressed, expanded, localOffset,
                    usesZip64Sizes));
                cursor += recordLength;
            }

            if ((ulong)cursor != centralEnd)
            {
                return Failure(BoundedZipReadError.InvalidStructure, cursor);
            }

            var ranges = new List<(ulong Start, ulong End)>(metadata.Count);
            var entries = ImmutableArray.CreateBuilder<BoundedZipEntry>(metadata.Count);
            foreach (EntryMetadata item in metadata)
            {
                BoundedZipReadResult? failure = ReadEntry(bytes, item, centralOffset, ranges, out BoundedZipEntry? entry);
                if (failure is not null)
                {
                    return failure.Value;
                }

                entries.Add(entry!);
            }

            ranges.Sort(static (left, right) => left.Start.CompareTo(right.Start));
            for (int index = 1; index < ranges.Count; index++)
            {
                if (ranges[index].Start < ranges[index - 1].End)
                {
                    return Failure(BoundedZipReadError.OverlappingEntries, checked((long)ranges[index].Start));
                }
            }

            return new(new(usesZip64, entries.MoveToImmutable()), BoundedZipReadError.None, null);
        }

        private BoundedZipReadResult? ReadEntry(
            ReadOnlySpan<byte> bytes,
            EntryMetadata item,
            ulong centralOffset,
            List<(ulong Start, ulong End)> ranges,
            out BoundedZipEntry? entry)
        {
            entry = null;
            cancellationToken.ThrowIfCancellationRequested();
            if (item.LocalOffset > int.MaxValue || !Has(bytes, (int)item.LocalOffset, 30) ||
                U32(bytes, (int)item.LocalOffset) != LocalSignature)
            {
                return Failure(BoundedZipReadError.InvalidStructure, checked((long)item.LocalOffset));
            }

            int local = checked((int)item.LocalOffset);
            ushort localFlags = U16(bytes, local + 6);
            ushort localMethod = U16(bytes, local + 8);
            ushort nameLength = U16(bytes, local + 26);
            ushort extraLength = U16(bytes, local + 28);
            int dataOffset = checked(local + 30 + nameLength + extraLength);
            ulong dataEnd = checked((ulong)dataOffset + item.Compressed);
            if (!Has(bytes, local, checked(30 + nameLength + extraLength)) ||
                dataEnd > centralOffset || localFlags != item.Flags || localMethod != item.Method)
            {
                return Failure(BoundedZipReadError.InvalidStructure, local);
            }

            ReadOnlySpan<byte> localNameBytes = bytes.Slice(local + 30, nameLength);
            if (!TryDecodeName(localNameBytes, (localFlags & 0x0800) != 0, out string localName) ||
                !string.Equals(localName, item.Name, StringComparison.Ordinal))
            {
                return Failure(BoundedZipReadError.InvalidStructure, local);
            }

            uint localCrc = U32(bytes, local + 14);
            uint localCompressed32 = U32(bytes, local + 18);
            uint localExpanded32 = U32(bytes, local + 22);
            bool hasDescriptor = (localFlags & 0x0008) != 0;
            if (!hasDescriptor)
            {
                ulong localCompressed = localCompressed32;
                ulong localExpanded = localExpanded32;
                if (localCompressed32 == uint.MaxValue || localExpanded32 == uint.MaxValue)
                {
                    ReadOnlySpan<byte> localExtra = bytes.Slice(local + 30 + nameLength, extraLength);
                    if (!TryReadLocalZip64Extra(localExtra, localExpanded32, localCompressed32,
                        out localExpanded, out localCompressed))
                    {
                        return Failure(BoundedZipReadError.Zip64Invalid, local);
                    }
                }

                if (localCrc != item.Crc || localCompressed != item.Compressed ||
                    localExpanded != item.Expanded)
                {
                    return Failure(BoundedZipReadError.InvalidStructure, local);
                }
            }
            else if (localCrc != 0 || (localCompressed32 is not (0 or uint.MaxValue)) ||
                (localExpanded32 is not (0 or uint.MaxValue)))
            {
                return Failure(BoundedZipReadError.InvalidStructure, local);
            }

            ulong occupiedEnd = dataEnd;
            if (hasDescriptor)
            {
                BoundedZipReadResult? descriptorFailure = ReadDataDescriptor(
                    bytes, dataEnd, centralOffset, item, out occupiedEnd);
                if (descriptorFailure is not null)
                {
                    return descriptorFailure.Value;
                }
            }

            ranges.Add(((ulong)local, occupiedEnd));
            byte[] content = new byte[checked((int)item.Expanded)];
            ReadOnlySpan<byte> compressed = bytes.Slice(dataOffset, checked((int)item.Compressed));
            if (item.Method == 0)
            {
                if (item.Compressed != item.Expanded)
                {
                    return Failure(BoundedZipReadError.InvalidStructure, dataOffset);
                }

                compressed.CopyTo(content);
            }
            else
            {
                using var compressedStream = new ExactConsumptionReadStream(compressed.ToArray());
                using var inflater = new DeflateStream(compressedStream, CompressionMode.Decompress, leaveOpen: false);
                int written = 0;
                while (written < content.Length)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int count = inflater.Read(content, written, content.Length - written);
                    if (count == 0)
                    {
                        return Failure(BoundedZipReadError.DecompressionFailed, dataOffset);
                    }

                    written += count;
                }

                if (inflater.ReadByte() != -1)
                {
                    return Failure(BoundedZipReadError.EntrySizeLimitExceeded, dataOffset);
                }

                if (compressedStream.Position != compressedStream.Length)
                {
                    return Failure(BoundedZipReadError.DecompressionFailed, dataOffset);
                }
            }

            if (Crc32.Compute(content) != item.Crc)
            {
                return Failure(BoundedZipReadError.CrcMismatch, dataOffset);
            }

            entry = new(item.Name, item.Method, item.Crc, checked((long)item.Compressed),
                checked((long)item.Expanded), checked((long)item.LocalOffset), [.. content]);
            return null;
        }

        private static BoundedZipReadResult? ReadDataDescriptor(
            ReadOnlySpan<byte> bytes,
            ulong descriptorOffset,
            ulong centralOffset,
            EntryMetadata item,
            out ulong occupiedEnd)
        {
            occupiedEnd = descriptorOffset;
            if (descriptorOffset > int.MaxValue || descriptorOffset >= centralOffset)
            {
                return Failure(BoundedZipReadError.InvalidStructure, checked((long)descriptorOffset));
            }

            int cursor = (int)descriptorOffset;
            if (Has(bytes, cursor, 4) && U32(bytes, cursor) == 0x08074b50)
            {
                if (TryReadDataDescriptorPayload(
                    bytes, cursor + 4, centralOffset, item, out occupiedEnd))
                {
                    return null;
                }
            }

            return TryReadDataDescriptorPayload(bytes, cursor, centralOffset, item, out occupiedEnd)
                ? null
                : Failure(BoundedZipReadError.InvalidStructure, cursor);
        }

        private static bool TryReadDataDescriptorPayload(
            ReadOnlySpan<byte> bytes,
            int cursor,
            ulong centralOffset,
            EntryMetadata item,
            out ulong occupiedEnd)
        {
            occupiedEnd = (ulong)cursor;
            bool zip64 = item.UsesZip64Sizes;
            int payloadLength = zip64 ? 20 : 12;
            if (!Has(bytes, cursor, payloadLength) ||
                checked((ulong)(cursor + payloadLength)) > centralOffset)
            {
                return false;
            }

            uint crc = U32(bytes, cursor);
            ulong compressed = zip64 ? U64(bytes, cursor + 4) : U32(bytes, cursor + 4);
            ulong expanded = zip64 ? U64(bytes, cursor + 12) : U32(bytes, cursor + 8);
            if (crc != item.Crc || compressed != item.Compressed || expanded != item.Expanded)
            {
                return false;
            }

            occupiedEnd = checked((ulong)(cursor + payloadLength));
            return true;
        }

        private static bool TryReadLocalZip64Extra(
            ReadOnlySpan<byte> extra,
            uint expanded32,
            uint compressed32,
            out ulong expanded,
            out ulong compressed)
        {
            expanded = expanded32;
            compressed = compressed32;
            int cursor = 0;
            while (cursor + 4 <= extra.Length)
            {
                ushort id = U16(extra, cursor);
                ushort size = U16(extra, cursor + 2);
                cursor += 4;
                if (cursor + size > extra.Length)
                {
                    return false;
                }

                if (id != 1)
                {
                    cursor += size;
                    continue;
                }

                int valueCursor = cursor;
                int end = cursor + size;
                return (expanded32 != uint.MaxValue || TryU64(extra, ref valueCursor, end, out expanded)) &&
                    (compressed32 != uint.MaxValue || TryU64(extra, ref valueCursor, end, out compressed));
            }

            return false;
        }

        private static bool ExceedsCompressionRatio(ulong expanded, ulong compressed, ulong maximumRatio)
        {
            if (compressed == 0)
            {
                return true;
            }

            return compressed <= ulong.MaxValue / maximumRatio &&
                expanded > compressed * maximumRatio;
        }

        private static int FindEndRecord(ReadOnlySpan<byte> bytes)
        {
            int minimum = Math.Max(0, bytes.Length - (ushort.MaxValue + 22));
            for (int offset = bytes.Length - 22; offset >= minimum; offset--)
            {
                if (U32(bytes, offset) == EndSignature)
                {
                    ushort commentLength = U16(bytes, offset + 20);
                    if (offset + 22 + commentLength == bytes.Length)
                    {
                        return offset;
                    }
                }
            }

            return -1;
        }

        private static BoundedZipReadResult? ReadZip64End(
            ReadOnlySpan<byte> bytes,
            int endOffset,
            out ulong entries,
            out ulong centralSize,
            out ulong centralOffset)
        {
            entries = centralSize = centralOffset = 0;
            int locator = endOffset - 20;
            if (!Has(bytes, locator, 20) || U32(bytes, locator) != Zip64LocatorSignature ||
                U32(bytes, locator + 4) != 0 || U32(bytes, locator + 16) != 1)
            {
                return Failure(BoundedZipReadError.Zip64Invalid, locator);
            }

            ulong zip64Offset = U64(bytes, locator + 8);
            if (zip64Offset > int.MaxValue || !Has(bytes, (int)zip64Offset, 56) ||
                U32(bytes, (int)zip64Offset) != Zip64EndSignature)
            {
                return Failure(BoundedZipReadError.Zip64Invalid, checked((long)zip64Offset));
            }

            int record = (int)zip64Offset;
            ulong recordSize = U64(bytes, record + 4);
            if (recordSize < 44 || checked(zip64Offset + 12 + recordSize) > (ulong)locator ||
                U32(bytes, record + 16) != 0 || U32(bytes, record + 20) != 0)
            {
                return Failure(BoundedZipReadError.Zip64Invalid, record);
            }

            ulong diskEntries = U64(bytes, record + 24);
            entries = U64(bytes, record + 32);
            centralSize = U64(bytes, record + 40);
            centralOffset = U64(bytes, record + 48);
            return diskEntries == entries
                ? null
                : Failure(BoundedZipReadError.Zip64Invalid, record);
        }

        private static bool TryReadZip64Extra(
            ReadOnlySpan<byte> extra,
            uint expanded32,
            uint compressed32,
            uint offset32,
            ushort disk16,
            out ulong expanded,
            out ulong compressed,
            out ulong offset,
            out uint disk)
        {
            expanded = expanded32;
            compressed = compressed32;
            offset = offset32;
            disk = disk16;
            int cursor = 0;
            while (cursor + 4 <= extra.Length)
            {
                ushort id = U16(extra, cursor);
                ushort size = U16(extra, cursor + 2);
                cursor += 4;
                if (cursor + size > extra.Length)
                {
                    return false;
                }

                if (id != 1)
                {
                    cursor += size;
                    continue;
                }

                int valueCursor = cursor;
                int end = cursor + size;
                if (expanded32 == uint.MaxValue && !TryU64(extra, ref valueCursor, end, out expanded) ||
                    compressed32 == uint.MaxValue && !TryU64(extra, ref valueCursor, end, out compressed) ||
                    offset32 == uint.MaxValue && !TryU64(extra, ref valueCursor, end, out offset) ||
                    disk16 == ushort.MaxValue && !TryU32(extra, ref valueCursor, end, out disk))
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        private static bool TryDecodeName(ReadOnlySpan<byte> bytes, bool utf8, out string name)
        {
            try
            {
                if (!utf8)
                {
                    foreach (byte value in bytes)
                    {
                        if (value is < 0x20 or > 0x7e)
                        {
                            name = string.Empty;
                            return false;
                        }
                    }
                }

                name = (utf8 ? new UTF8Encoding(false, true) : Encoding.ASCII).GetString(bytes);
                return name.IndexOf('\0') < 0;
            }
            catch (DecoderFallbackException)
            {
                name = string.Empty;
                return false;
            }
        }

        private static bool IsSafeName(string name)
        {
            if (name.Length == 0 || name[0] == '/' || name.Contains('\\', StringComparison.Ordinal) ||
                name.Contains(':', StringComparison.Ordinal))
            {
                return false;
            }

            string[] segments = name.Split('/', StringSplitOptions.None);
            for (int index = 0; index < segments.Length; index++)
            {
                string segment = segments[index];
                bool finalDirectoryMarker = index == segments.Length - 1 && segment.Length == 0;
                if (!finalDirectoryMarker && (segment.Length == 0 || segment is "." or ".."))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Has(ReadOnlySpan<byte> bytes, int offset, int length) =>
            offset >= 0 && length >= 0 && offset <= bytes.Length - length;

        private static ushort U16(ReadOnlySpan<byte> bytes, int offset) =>
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);

        private static uint U32(ReadOnlySpan<byte> bytes, int offset) =>
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);

        private static ulong U64(ReadOnlySpan<byte> bytes, int offset) =>
            BinaryPrimitives.ReadUInt64LittleEndian(bytes[offset..]);

        private static bool TryU64(ReadOnlySpan<byte> bytes, ref int offset, int end, out ulong value)
        {
            if (offset > end - 8)
            {
                value = 0;
                return false;
            }

            value = U64(bytes, offset);
            offset += 8;
            return true;
        }

        private static bool TryU32(ReadOnlySpan<byte> bytes, ref int offset, int end, out uint value)
        {
            if (offset > end - 4)
            {
                value = 0;
                return false;
            }

            value = U32(bytes, offset);
            offset += 4;
            return true;
        }

        private readonly record struct EntryMetadata(
            string Name,
            ushort Flags,
            ushort Method,
            uint Crc,
            ulong Compressed,
            ulong Expanded,
            ulong LocalOffset,
            bool UsesZip64Sizes);

        private sealed class ExactConsumptionReadStream(byte[] bytes) : MemoryStream(bytes, writable: false)
        {
            public override int Read(byte[] buffer, int offset, int count) =>
                base.Read(buffer, offset, BoundedReadCount(count));

            public override int Read(Span<byte> buffer) =>
                base.Read(buffer[..BoundedReadCount(buffer.Length)]);

            private int BoundedReadCount(int requested)
            {
                const int exactTailBytes = 64;
                long remaining = Length - Position;
                int maximum = remaining <= exactTailBytes
                    ? 1
                    : checked((int)Math.Min(int.MaxValue, remaining - exactTailBytes));
                return Math.Min(requested, maximum);
            }
        }
    }

    private static class Crc32
    {
        private static readonly uint[] Table = CreateTable();

        internal static uint Compute(ReadOnlySpan<byte> bytes)
        {
            uint crc = uint.MaxValue;
            foreach (byte value in bytes)
            {
                crc = Table[(crc ^ value) & 0xff] ^ (crc >> 8);
            }

            return ~crc;
        }

        private static uint[] CreateTable()
        {
            var table = new uint[256];
            for (uint index = 0; index < table.Length; index++)
            {
                uint value = index;
                for (int bit = 0; bit < 8; bit++)
                {
                    value = (value & 1) != 0 ? 0xedb88320u ^ (value >> 1) : value >> 1;
                }

                table[index] = value;
            }

            return table;
        }
    }
}
