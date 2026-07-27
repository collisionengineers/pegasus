using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;

namespace CollisionDocNet.Storage.Ole;

public sealed record OlePropertySetLimits
{
    public static OlePropertySetLimits Default { get; } = new();

    public int MaximumInputBytes { get; init; } = 16 * 1024 * 1024;

    public int MaximumSections { get; init; } = 2;

    public int MaximumPropertiesPerSection { get; init; } = 100_000;

    public int MaximumValueBytes { get; init; } = 16 * 1024 * 1024;
}

public enum OlePropertySetReadError
{
    None = 0,
    InputLimitExceeded,
    InvalidHeader,
    InvalidSection,
    PropertyLimitExceeded,
    ValueLimitExceeded,
    Cancelled,
}

public enum OlePropertyValueKind
{
    Empty,
    SignedInteger,
    UnsignedInteger,
    FloatingPoint,
    Boolean,
    FileTime,
    Text,
    Blob,
    Identifier,
    Unsupported,
}

public sealed record OleProperty(
    uint PropertyId,
    ushort VariantType,
    OlePropertyValueKind Kind,
    object? Value,
    ImmutableArray<byte> RawValue);

public sealed record OlePropertySection(
    Guid FormatId,
    int CodePage,
    ImmutableArray<OleProperty> Properties);

public sealed record OlePropertySet(
    ushort FormatVersion,
    uint SystemIdentifier,
    Guid ClassId,
    ImmutableArray<OlePropertySection> Sections);

public readonly record struct OlePropertySetReadResult(
    OlePropertySet? PropertySet,
    OlePropertySetReadError Error,
    int? Offset)
{
    public bool IsSuccess => Error == OlePropertySetReadError.None && PropertySet is not null;
}

/// <summary>
/// Passive MS-OLEPS reader. It preserves every property's raw bytes and adds
/// typed scalar projections for the common fixed-width, string, FILETIME,
/// BLOB and CLSID variants. Dictionaries, arrays, vectors and indirect values
/// remain explicitly Unsupported rather than being discarded.
/// </summary>
public static class OlePropertySetReader
{
    public static OlePropertySetReadResult Read(
        ReadOnlyMemory<byte> bytes,
        OlePropertySetLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        limits ??= OlePropertySetLimits.Default;
        if (!AreValid(limits) || bytes.Length > limits.MaximumInputBytes)
        {
            return Failure(OlePropertySetReadError.InputLimitExceeded);
        }

        try
        {
            return Parse(bytes.Span, limits, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Failure(OlePropertySetReadError.Cancelled);
        }
        catch (OverflowException)
        {
            return Failure(OlePropertySetReadError.InvalidSection);
        }
    }

    private static OlePropertySetReadResult Parse(
        ReadOnlySpan<byte> bytes,
        OlePropertySetLimits limits,
        CancellationToken cancellationToken)
    {
        if (!Has(bytes, 0, 28) || U16(bytes, 0) != 0xfffe)
        {
            return Failure(OlePropertySetReadError.InvalidHeader);
        }

        ushort version = U16(bytes, 2);
        uint system = U32(bytes, 4);
        Guid classId = new(bytes.Slice(8, 16));
        uint sectionCount = U32(bytes, 24);
        if (sectionCount == 0 || sectionCount > limits.MaximumSections ||
            !Has(bytes, 28, checked((int)sectionCount * 20)))
        {
            return Failure(OlePropertySetReadError.InvalidHeader, 24);
        }

        var descriptors = new List<(Guid FormatId, uint Offset)>(checked((int)sectionCount));
        for (int index = 0; index < sectionCount; index++)
        {
            int descriptor = 28 + (index * 20);
            uint offset = U32(bytes, descriptor + 16);
            if ((offset & 3) != 0)
            {
                return Failure(OlePropertySetReadError.InvalidSection, descriptor + 16);
            }

            descriptors.Add((new(bytes.Slice(descriptor, 16)), offset));
        }

        var sectionRanges = new List<(uint Start, uint End)>(descriptors.Count);
        foreach ((_, uint offset) in descriptors)
        {
            if (offset < 28 + (sectionCount * 20) || offset > int.MaxValue ||
                !Has(bytes, (int)offset, 8))
            {
                return Failure(OlePropertySetReadError.InvalidSection, checked((int)offset));
            }

            uint size = U32(bytes, (int)offset);
            if (size < 8 || (size & 3) != 0 || size > int.MaxValue ||
                !Has(bytes, (int)offset, (int)size))
            {
                return Failure(OlePropertySetReadError.InvalidSection, (int)offset);
            }

            sectionRanges.Add((offset, checked(offset + size)));
        }

        sectionRanges.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        for (int index = 1; index < sectionRanges.Count; index++)
        {
            if (sectionRanges[index].Start < sectionRanges[index - 1].End)
            {
                return Failure(OlePropertySetReadError.InvalidSection, (int)sectionRanges[index].Start);
            }
        }

        var sections = ImmutableArray.CreateBuilder<OlePropertySection>(checked((int)sectionCount));
        foreach ((Guid formatId, uint sectionOffsetValue) in descriptors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sectionOffsetValue > int.MaxValue || !Has(bytes, (int)sectionOffsetValue, 8))
            {
                return Failure(OlePropertySetReadError.InvalidSection, checked((int)sectionOffsetValue));
            }

            int sectionOffset = (int)sectionOffsetValue;
            uint sectionSize = U32(bytes, sectionOffset);
            uint propertyCount = U32(bytes, sectionOffset + 4);
            if (sectionSize < 8 || (sectionSize & 3) != 0 || sectionSize > int.MaxValue ||
                !Has(bytes, sectionOffset, (int)sectionSize) ||
                propertyCount > limits.MaximumPropertiesPerSection ||
                !Has(bytes, sectionOffset + 8, checked((int)propertyCount * 8)))
            {
                return Failure(
                    propertyCount > limits.MaximumPropertiesPerSection
                        ? OlePropertySetReadError.PropertyLimitExceeded
                        : OlePropertySetReadError.InvalidSection,
                    sectionOffset);
            }

            var propertyDescriptors = new List<(uint Id, uint Offset)>(checked((int)propertyCount));
            var offsets = new HashSet<uint>();
            var identifiers = new HashSet<uint>();
            for (int index = 0; index < propertyCount; index++)
            {
                int descriptor = sectionOffset + 8 + (index * 8);
                uint id = U32(bytes, descriptor);
                uint offset = U32(bytes, descriptor + 4);
                if ((offset & 3) != 0 || offset < 8 + propertyCount * 8 ||
                    offset >= sectionSize || !offsets.Add(offset) || !identifiers.Add(id))
                {
                    return Failure(OlePropertySetReadError.InvalidSection, descriptor);
                }

                propertyDescriptors.Add((id, offset));
            }

            propertyDescriptors.Sort(static (left, right) => left.Offset.CompareTo(right.Offset));
            int codePage = ReadCodePage(bytes, sectionOffset, sectionSize, propertyDescriptors);
            var properties = ImmutableArray.CreateBuilder<OleProperty>(propertyDescriptors.Count);
            for (int index = 0; index < propertyDescriptors.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                (uint id, uint relativeOffset) = propertyDescriptors[index];
                uint next = index + 1 < propertyDescriptors.Count
                    ? propertyDescriptors[index + 1].Offset
                    : sectionSize;
                int length = checked((int)(next - relativeOffset));
                if (length > limits.MaximumValueBytes)
                {
                    return Failure(OlePropertySetReadError.ValueLimitExceeded, sectionOffset + (int)relativeOffset);
                }

                ReadOnlySpan<byte> raw = bytes.Slice(sectionOffset + (int)relativeOffset, length);
                properties.Add(ParseProperty(id, raw, codePage));
            }

            sections.Add(new(formatId, codePage, properties.MoveToImmutable()));
        }

        return new(new(version, system, classId, sections.MoveToImmutable()),
            OlePropertySetReadError.None, null);
    }

    private static int ReadCodePage(
        ReadOnlySpan<byte> bytes,
        int sectionOffset,
        uint sectionSize,
        List<(uint Id, uint Offset)> descriptors)
    {
        int index = descriptors.FindIndex(static item => item.Id == 1);
        if (index < 0)
        {
            return 0;
        }

        uint next = index + 1 < descriptors.Count ? descriptors[index + 1].Offset : sectionSize;
        ReadOnlySpan<byte> raw = bytes.Slice(
            sectionOffset + (int)descriptors[index].Offset,
            checked((int)(next - descriptors[index].Offset)));
        if (raw.Length >= 6 && U16(raw, 0) == 2)
        {
            return U16(raw, 4);
        }

        return 0;
    }

    private static OleProperty ParseProperty(uint id, ReadOnlySpan<byte> raw, int codePage)
    {
        ImmutableArray<byte> preserved = [.. raw];
        if (raw.Length < 4)
        {
            return new(id, 0, OlePropertyValueKind.Unsupported, null, preserved);
        }

        ushort type = U16(raw, 0);
        if (U16(raw, 2) != 0)
        {
            return new(id, type, OlePropertyValueKind.Unsupported, null, preserved);
        }
        ReadOnlySpan<byte> value = raw[4..];
        return type switch
        {
            0 => new(id, type, OlePropertyValueKind.Empty, null, preserved),
            2 when value.Length >= 2 => new(id, type, OlePropertyValueKind.SignedInteger, (long)I16(value, 0), preserved),
            3 when value.Length >= 4 => new(id, type, OlePropertyValueKind.SignedInteger, (long)I32(value, 0), preserved),
            16 when value.Length >= 1 => new(id, type, OlePropertyValueKind.SignedInteger, (long)(sbyte)value[0], preserved),
            17 when value.Length >= 1 => new(id, type, OlePropertyValueKind.UnsignedInteger, (ulong)value[0], preserved),
            18 when value.Length >= 2 => new(id, type, OlePropertyValueKind.UnsignedInteger, (ulong)U16(value, 0), preserved),
            19 when value.Length >= 4 => new(id, type, OlePropertyValueKind.UnsignedInteger, (ulong)U32(value, 0), preserved),
            20 when value.Length >= 8 => new(id, type, OlePropertyValueKind.SignedInteger, I64(value, 0), preserved),
            21 when value.Length >= 8 => new(id, type, OlePropertyValueKind.UnsignedInteger, U64(value, 0), preserved),
            4 when value.Length >= 4 => new(id, type, OlePropertyValueKind.FloatingPoint, BitConverter.Int32BitsToSingle(I32(value, 0)), preserved),
            5 when value.Length >= 8 => new(id, type, OlePropertyValueKind.FloatingPoint, BitConverter.Int64BitsToDouble(I64(value, 0)), preserved),
            11 when value.Length >= 2 => new(id, type, OlePropertyValueKind.Boolean, I16(value, 0) != 0, preserved),
            30 => ParseAnsiString(id, type, value, preserved, codePage),
            31 => ParseUnicodeString(id, type, value, preserved),
            64 when value.Length >= 8 => new(id, type, OlePropertyValueKind.FileTime, I64(value, 0), preserved),
            65 => ParseBlob(id, type, value, preserved),
            72 when value.Length >= 16 => new(id, type, OlePropertyValueKind.Identifier, new Guid(value[..16]), preserved),
            _ => new(id, type, OlePropertyValueKind.Unsupported, null, preserved),
        };
    }

    private static OleProperty ParseAnsiString(
        uint id,
        ushort type,
        ReadOnlySpan<byte> value,
        ImmutableArray<byte> raw,
        int codePage)
    {
        if (value.Length < 4)
        {
            return new(id, type, OlePropertyValueKind.Unsupported, null, raw);
        }

        uint count = U32(value, 0);
        if (count == 0 || count > int.MaxValue || !Has(value, 4, (int)count))
        {
            return new(id, type, OlePropertyValueKind.Unsupported, null, raw);
        }

        ReadOnlySpan<byte> text = value.Slice(4, (int)count);
        if (text[^1] == 0)
        {
            text = text[..^1];
        }

        string? decoded = codePage switch
        {
            65001 => TryUtf8(text),
            1252 => DecodeWindows1252(text),
            _ => null,
        };
        return decoded is null
            ? new(id, type, OlePropertyValueKind.Unsupported, null, raw)
            : new(id, type, OlePropertyValueKind.Text, decoded, raw);
    }

    private static OleProperty ParseUnicodeString(
        uint id,
        ushort type,
        ReadOnlySpan<byte> value,
        ImmutableArray<byte> raw)
    {
        if (value.Length < 4)
        {
            return new(id, type, OlePropertyValueKind.Unsupported, null, raw);
        }

        uint characters = U32(value, 0);
        ulong byteCount = (ulong)characters * 2;
        if (characters == 0 || byteCount > int.MaxValue || !Has(value, 4, (int)byteCount))
        {
            return new(id, type, OlePropertyValueKind.Unsupported, null, raw);
        }

        ReadOnlySpan<byte> text = value.Slice(4, (int)byteCount);
        if (text.Length >= 2 && U16(text, text.Length - 2) == 0)
        {
            text = text[..^2];
        }

        return new(id, type, OlePropertyValueKind.Text, Encoding.Unicode.GetString(text), raw);
    }

    private static OleProperty ParseBlob(
        uint id,
        ushort type,
        ReadOnlySpan<byte> value,
        ImmutableArray<byte> raw)
    {
        if (value.Length < 4)
        {
            return new(id, type, OlePropertyValueKind.Unsupported, null, raw);
        }

        uint count = U32(value, 0);
        return count <= int.MaxValue && Has(value, 4, (int)count)
            ? new(id, type, OlePropertyValueKind.Blob, ImmutableArray.Create(value.Slice(4, (int)count).ToArray()), raw)
            : new(id, type, OlePropertyValueKind.Unsupported, null, raw);
    }

    private static string? TryUtf8(ReadOnlySpan<byte> bytes)
    {
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static string DecodeWindows1252(ReadOnlySpan<byte> bytes)
    {
        const string replacements = "€\u0081‚ƒ„…†‡ˆ‰Š‹Œ\u008dŽ\u008f\u0090‘’“”•–—˜™š›œ\u009džŸ";
        return string.Create(bytes.Length, (Bytes: bytes.ToArray(), Replacements: replacements),
            static (destination, state) =>
            {
                for (int index = 0; index < state.Bytes.Length; index++)
                {
                    byte value = state.Bytes[index];
                    destination[index] = value is >= 0x80 and <= 0x9f
                        ? state.Replacements[value - 0x80]
                        : (char)value;
                }
            });
    }

    private static bool AreValid(OlePropertySetLimits limits) =>
        limits.MaximumInputBytes >= 28 && limits.MaximumSections is > 0 and <= 2 &&
        limits.MaximumPropertiesPerSection > 0 && limits.MaximumValueBytes >= 4;

    private static bool Has(ReadOnlySpan<byte> bytes, int offset, int length) =>
        offset >= 0 && length >= 0 && offset <= bytes.Length - length;

    private static ushort U16(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
    private static short I16(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..]);
    private static uint U32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    private static int I32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..]);
    private static ulong U64(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(bytes[offset..]);
    private static long I64(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadInt64LittleEndian(bytes[offset..]);

    private static OlePropertySetReadResult Failure(OlePropertySetReadError error, int? offset = null) =>
        new(null, error, offset);
}

public enum OleEmbeddedObjectDescriptorError
{
    None = 0,
    InputLimitExceeded,
    InvalidStructure,
}

public sealed record OleEmbeddedObjectDescriptor(
    string Label,
    string OriginalFileName,
    string Command,
    ImmutableArray<byte> Payload);

public readonly record struct OleEmbeddedObjectDescriptorResult(
    OleEmbeddedObjectDescriptor? Descriptor,
    OleEmbeddedObjectDescriptorError Error)
{
    public bool IsSuccess => Error == OleEmbeddedObjectDescriptorError.None && Descriptor is not null;
}

/// <summary>Reads the common ANSI Ole10Native embedded-package descriptor passively.</summary>
public static class OleEmbeddedObjectDescriptorReader
{
    public static OleEmbeddedObjectDescriptorResult ReadOle10Native(
        ReadOnlySpan<byte> bytes,
        int maximumInputBytes = 16 * 1024 * 1024)
    {
        if (maximumInputBytes < 0 || bytes.Length > maximumInputBytes)
        {
            return new(null, OleEmbeddedObjectDescriptorError.InputLimitExceeded);
        }

        if (bytes.Length < 12)
        {
            return new(null, OleEmbeddedObjectDescriptorError.InvalidStructure);
        }

        uint declaredSize = U32(bytes, 0);
        if (declaredSize < 8 || declaredSize > int.MaxValue || declaredSize > bytes.Length - 4)
        {
            return new(null, OleEmbeddedObjectDescriptorError.InvalidStructure);
        }

        int declaredEnd = checked(4 + (int)declaredSize);
        ReadOnlySpan<byte> declared = bytes[..declaredEnd];

        int cursor = 6;
        if (!TryCString(declared, ref cursor, out string label) ||
            !TryCString(declared, ref cursor, out string fileName) || cursor > declared.Length - 4)
        {
            return new(null, OleEmbeddedObjectDescriptorError.InvalidStructure);
        }

        cursor += 4;
        if (!TryCString(declared, ref cursor, out string command) || cursor > declared.Length - 4)
        {
            return new(null, OleEmbeddedObjectDescriptorError.InvalidStructure);
        }

        uint payloadSize = U32(declared, cursor);
        cursor += 4;
        if (payloadSize > int.MaxValue || cursor != declared.Length - (int)payloadSize)
        {
            return new(null, OleEmbeddedObjectDescriptorError.InvalidStructure);
        }

        return new(new(label, fileName, command, [.. declared.Slice(cursor, (int)payloadSize)]),
            OleEmbeddedObjectDescriptorError.None);
    }

    private static bool TryCString(ReadOnlySpan<byte> bytes, ref int cursor, out string value)
    {
        if ((uint)cursor > (uint)bytes.Length)
        {
            value = string.Empty;
            return false;
        }

        int end = bytes[cursor..].IndexOf((byte)0);
        if (end < 0)
        {
            value = string.Empty;
            return false;
        }

        value = Encoding.Latin1.GetString(bytes.Slice(cursor, end));
        cursor += end + 1;
        return true;
    }

    private static uint U32(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
}
