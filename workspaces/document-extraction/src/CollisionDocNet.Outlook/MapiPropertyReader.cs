using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using CollisionDocNet.Storage.CompoundFile;

namespace CollisionDocNet.Outlook;

internal enum PropertyStreamContext
{
    RootMessage,
    EmbeddedMessage,
    RecipientOrAttachment,
}

internal sealed class MsgReadState(MsgReadLimits limits)
{
    private int _properties;
    private int _values;
    private int _recipients;
    private int _attachments;
    private int _modelItems;
    private int _childStorages;
    private long _decodedBytes;

    internal bool ResourceLimitExceeded { get; private set; }

    internal bool TakeProperty() => Take(ref _properties, 1, limits.MaximumProperties);
    internal bool TakeValue() => Take(ref _values, 1, limits.MaximumValues);
    internal bool TakeRecipient() => Take(ref _recipients, 1, limits.MaximumRecipients) && TakeModelItem();
    internal bool TakeAttachment() => Take(ref _attachments, 1, limits.MaximumAttachments) && TakeModelItem();
    internal bool TakeModelItem() => Take(ref _modelItems, 1, limits.MaximumModelItems);
    internal bool TakeChildStorage() => Take(ref _childStorages, 1, limits.MaximumChildStorages);

    internal bool TakeDecodedBytes(int count)
    {
        if (count < 0)
        {
            ResourceLimitExceeded = true;
            return false;
        }

        try
        {
            long next = checked(_decodedBytes + count);
            if (next > limits.MaximumDecodedBytes)
            {
                ResourceLimitExceeded = true;
                return false;
            }

            _decodedBytes = next;
            return true;
        }
        catch (OverflowException)
        {
            ResourceLimitExceeded = true;
            return false;
        }
    }

    private bool Take(ref int counter, int amount, int maximum)
    {
        try
        {
            int next = checked(counter + amount);
            if (next > maximum)
            {
                ResourceLimitExceeded = true;
                return false;
            }

            counter = next;
            return true;
        }
        catch (OverflowException)
        {
            ResourceLimitExceeded = true;
            return false;
        }
    }
}

internal sealed class MapiPropertyReader
{
    private const ushort MultiValueFlag = 0x1000;
    private const string Windows1252Controls = "€\u0081‚ƒ„…†‡ˆ‰Š‹Œ\u008dŽ\u008f\u0090‘’“”•–—˜™š›œ\u009džŸ";
    private readonly MsgReadLimits _limits;
    private readonly List<MsgIssue> _issues;
    private readonly CancellationToken _cancellationToken;
    private readonly MsgReadState _state;
    private readonly Dictionary<uint, CompoundFileDirectoryEntry[]> _childrenByParent;
    private ImmutableDictionary<ushort, NamedPropertyIdentity> _namedProperties =
        ImmutableDictionary<ushort, NamedPropertyIdentity>.Empty;

    internal MapiPropertyReader(
        CompoundFile file,
        MsgReadLimits limits,
        List<MsgIssue> issues,
        MsgReadState state,
        CancellationToken cancellationToken)
    {
        _limits = limits;
        _issues = issues;
        _cancellationToken = cancellationToken;
        _state = state;
        _childrenByParent = BuildChildrenIndex(file);
    }

    internal ImmutableDictionary<ushort, NamedPropertyIdentity> NamedProperties => _namedProperties;
    internal MsgReadState State => _state;

    internal void ReadNamedProperties(uint ownerStorageId)
    {
        CompoundFileDirectoryEntry? storage = Children(ownerStorageId)
            .FirstOrDefault(static entry => entry.ObjectType == CompoundFileObjectType.Storage &&
                entry.Name.Equals("__nameid_version1.0", StringComparison.Ordinal));
        if (storage is null) return;

        byte[] guidBytes = Stream(storage.StreamId, "__substg1.0_00020102")?.Content.ToArray() ?? [];
        byte[] entryBytes = Stream(storage.StreamId, "__substg1.0_00030102")?.Content.ToArray() ?? [];
        byte[] stringBytes = Stream(storage.StreamId, "__substg1.0_00040102")?.Content.ToArray() ?? [];
        if ((guidBytes.Length % 16) != 0 || (entryBytes.Length % 8) != 0)
        {
            _issues.Add(new("MSG_NAMED_PROPERTY_STREAM_INVALID", "Named-property mapping streams have invalid lengths.", storage.StreamId));
            return;
        }

        var map = ImmutableDictionary.CreateBuilder<ushort, NamedPropertyIdentity>();
        for (int offset = 0; offset < entryBytes.Length; offset += 8)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            uint nameOrOffset = BinaryPrimitives.ReadUInt32LittleEndian(entryBytes.AsSpan(offset));
            ushort guidAndKind = BinaryPrimitives.ReadUInt16LittleEndian(entryBytes.AsSpan(offset + 4));
            ushort propertyIndex = BinaryPrimitives.ReadUInt16LittleEndian(entryBytes.AsSpan(offset + 6));
            if (propertyIndex > 0x7FFF)
            {
                _issues.Add(new("MSG_NAMED_PROPERTY_INDEX_INVALID", "Named-property index is outside the MAPI named-property range.", storage.StreamId));
                continue;
            }

            ushort propertyId = (ushort)(0x8000 + propertyIndex);
            if (map.ContainsKey(propertyId))
            {
                _issues.Add(new("MSG_NAMED_PROPERTY_DUPLICATE", "A duplicate named-property mapping was ignored.", storage.StreamId, propertyId));
                continue;
            }

            int guidIndex = guidAndKind >> 1;
            bool isString = (guidAndKind & 1) != 0;
            Guid propertySet = guidIndex switch
            {
                1 => new Guid("00020328-0000-0000-c000-000000000046"),
                2 => new Guid("00020329-0000-0000-c000-000000000046"),
                >= 3 when (guidIndex - 3) * 16 + 16 <= guidBytes.Length =>
                    new Guid(guidBytes.AsSpan((guidIndex - 3) * 16, 16)),
                _ => Guid.Empty,
            };
            if (propertySet == Guid.Empty)
            {
                _issues.Add(new("MSG_NAMED_PROPERTY_GUID_INVALID", "Named-property entry references an unavailable property-set GUID.", storage.StreamId));
            }

            string? stringName = null;
            uint? numericName = isString ? null : nameOrOffset;
            if (isString)
            {
                if (nameOrOffset > int.MaxValue || nameOrOffset + 4L > stringBytes.Length)
                {
                    _issues.Add(new("MSG_NAMED_PROPERTY_STRING_INVALID", "Named-property string offset is outside the string stream.", storage.StreamId));
                    continue;
                }

                int nameOffset = (int)nameOrOffset;
                int byteLength = BinaryPrimitives.ReadInt32LittleEndian(stringBytes.AsSpan(nameOffset));
                if (byteLength < 0 || (byteLength & 1) != 0 || nameOffset + 4L + byteLength > stringBytes.Length)
                {
                    _issues.Add(new("MSG_NAMED_PROPERTY_STRING_INVALID", "Named-property string has an invalid bounded length.", storage.StreamId));
                    continue;
                }

                stringName = Encoding.Unicode.GetString(stringBytes, nameOffset + 4, byteLength).TrimEnd('\0');
            }

            map.Add(propertyId, new(propertySet, numericName, stringName));
        }

        _namedProperties = map.ToImmutable();
    }

    internal ImmutableArray<MapiProperty> ReadPropertyBag(
        uint storageId,
        PropertyStreamContext context,
        int? inheritedCodePage = null)
    {
        CompoundFileDirectoryEntry? propertyStream = Stream(storageId, "__properties_version1.0");
        if (propertyStream is null)
        {
            _issues.Add(new("MSG_PROPERTY_STREAM_MISSING", "The storage has no property stream.", storageId));
            return [];
        }

        ReadOnlySpan<byte> bytes = propertyStream.Content.AsSpan();
        int headerLength = HeaderLength(context);
        if (bytes.Length < headerLength || ((bytes.Length - headerLength) % 16) != 0)
        {
            _issues.Add(new("MSG_PROPERTY_STREAM_LENGTH_INVALID", "The property stream does not match its exact contextual header and 16-byte entries.", storageId));
            return [];
        }

        ValidateHeader(bytes[..headerLength], context, storageId);
        int? codePage = DiscoverCodePage(bytes, headerLength, storageId) ?? inheritedCodePage;
        var properties = ImmutableArray.CreateBuilder<MapiProperty>((bytes.Length - headerLength) / 16);
        for (int offset = headerLength; offset < bytes.Length; offset += 16)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (!_state.TakeProperty())
            {
                _issues.Add(new("MSG_RESOURCE_LIMIT", "The cumulative property-count limit was exceeded.", storageId));
                break;
            }

            uint tag = BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
            ushort type = (ushort)tag;
            ushort id = (ushort)(tag >> 16);
            uint flags = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 4)..]);
            if ((flags & ~0x7u) != 0)
            {
                _issues.Add(new("MSG_PROPERTY_FLAGS_RESERVED", "A property entry sets reserved flag bits.", storageId, id));
            }
            if (id == 0 || type == 0)
            {
                _issues.Add(new("MSG_PROPERTY_TAG_INVALID", "A property entry has an invalid zero identifier or type.", storageId, id));
            }

            ReadOnlySpan<byte> inline = bytes.Slice(offset + 8, 8);
            ImmutableArray<MapiValue> values = DecodeValues(storageId, id, type, inline, codePage);
            _namedProperties.TryGetValue(id, out NamedPropertyIdentity? named);
            properties.Add(new(storageId, id, type, flags, named, values));
        }

        return properties.ToImmutable();
    }

    private int? DiscoverCodePage(ReadOnlySpan<byte> bytes, int headerLength, uint storageId)
    {
        int? selected = null;
        for (int offset = headerLength; offset < bytes.Length; offset += 16)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            uint tag = BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
            if ((ushort)(tag >> 16) != 0x3FFD || (ushort)tag != 0x0003) continue;
            int candidate = BinaryPrimitives.ReadInt32LittleEndian(bytes[(offset + 8)..]);
            if (selected is null) selected = candidate;
            else if (selected != candidate)
            {
                _issues.Add(new("MSG_CODEPAGE_CONFLICT", "Conflicting message code-page properties were found; the first was used.", storageId, 0x3FFD));
            }
        }
        return selected;
    }

    private void ValidateHeader(ReadOnlySpan<byte> header, PropertyStreamContext context, uint storageId)
    {
        if (!header[..8].IsEmpty && ContainsNonZero(header[..8]))
        {
            _issues.Add(new("MSG_PROPERTY_HEADER_RESERVED", "The property stream reserved header bytes are non-zero.", storageId));
        }
        if (context == PropertyStreamContext.RootMessage && ContainsNonZero(header[24..32]))
        {
            _issues.Add(new("MSG_PROPERTY_HEADER_RESERVED", "The root property stream trailing reserved bytes are non-zero.", storageId));
        }
    }

    private static bool ContainsNonZero(ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes) if (value != 0) return true;
        return false;
    }

    private ImmutableArray<MapiValue> DecodeValues(
        uint storageId,
        ushort id,
        ushort type,
        ReadOnlySpan<byte> inline,
        int? codePage)
    {
        ushort baseType = (ushort)(type & ~MultiValueFlag);
        if ((type & MultiValueFlag) != 0)
        {
            return DecodeMultiValues(storageId, id, type, baseType, codePage);
        }

        if (!_state.TakeValue())
        {
            _issues.Add(new("MSG_RESOURCE_LIMIT", "The cumulative value-count limit was exceeded.", storageId, id));
            return [];
        }

        // MS-OXMSG 2.1.3 stores PtypGuid in its own value stream even though
        // its value has the fixed width of 16 bytes and its multi-valued form
        // remains a fixed-width array.
        if (IsSingleValueStreamType(baseType))
        {
            CompoundFileDirectoryEntry? stream = Stream(storageId, SubstreamName(id, type));
            if (stream is null)
            {
                _issues.Add(new("MSG_PROPERTY_VALUE_STREAM_MISSING", "A variable MAPI property value stream is missing.", storageId, id));
                return [new(0, MapiValueKind.Raw, null, [])];
            }
            return [DecodeOne(0, baseType, stream.Content.AsSpan(), codePage, storageId, id)];
        }

        int width = FixedWidth(baseType);
        if (width <= 0)
        {
            _issues.Add(new("MSG_PROPERTY_TYPE_UNSUPPORTED", $"MAPI type 0x{type:x4} is retained as raw bytes.", storageId, id));
            return [RetainRaw(0, inline, storageId, id)];
        }
        if (width > inline.Length)
        {
            _issues.Add(new("MSG_PROPERTY_VALUE_INLINE_INVALID", "A fixed MAPI property value is wider than the property entry; its available raw bytes were retained.", storageId, id));
            return [RetainRaw(0, inline, storageId, id)];
        }
        return [DecodeOne(0, baseType, inline[..width], codePage, storageId, id)];
    }

    private ImmutableArray<MapiValue> DecodeMultiValues(
        uint storageId,
        ushort id,
        ushort type,
        ushort baseType,
        int? codePage)
    {
        CompoundFileDirectoryEntry? table = Stream(storageId, SubstreamName(id, type));
        if (table is null)
        {
            _issues.Add(new("MSG_MULTIVALUE_TABLE_MISSING", "A multi-valued MAPI property table is missing.", storageId, id));
            return [];
        }

        ReadOnlySpan<byte> tableBytes = table.Content.AsSpan();
        var values = ImmutableArray.CreateBuilder<MapiValue>();
        if (IsVariableMultiValueElement(baseType))
        {
            int count = tableBytes.Length / 4;
            if ((tableBytes.Length % 4) != 0)
            {
                _issues.Add(new("MSG_MULTIVALUE_TABLE_INVALID", "Variable multi-value length table has trailing bytes outside its 4-byte length records.", storageId, id));
            }

            for (int index = 0; index < count; index++)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                if (!_state.TakeValue())
                {
                    _issues.Add(new("MSG_RESOURCE_LIMIT", "The cumulative value-count limit was exceeded.", storageId, id));
                    break;
                }

                uint declaredLength = BinaryPrimitives.ReadUInt32LittleEndian(tableBytes[(index * 4)..]);
                CompoundFileDirectoryEntry? item = Stream(storageId, $"{SubstreamName(id, type)}-{index:x8}");
                if (item is null)
                {
                    _issues.Add(new("MSG_MULTIVALUE_ITEM_INVALID", "A declared variable multi-value item stream is missing.", storageId, id));
                    values.Add(new(index, MapiValueKind.Raw, null, []));
                    continue;
                }

                if ((ulong)item.Content.Length != declaredLength)
                {
                    _issues.Add(new("MSG_MULTIVALUE_ITEM_INVALID", "A variable multi-value item disagrees with its declared length; its actual raw bytes were retained.", storageId, id));
                    values.Add(RetainRaw(index, item.Content.AsSpan(), storageId, id));
                    continue;
                }
                values.Add(DecodeOne(index, baseType, item.Content.AsSpan(), codePage, storageId, id));
            }

            if ((tableBytes.Length % 4) != 0 && _state.TakeValue())
            {
                values.Add(RetainRaw(count, tableBytes[(count * 4)..], storageId, id));
            }
        }
        else
        {
            int width = FixedWidth(baseType);
            if (width <= 0 || (tableBytes.Length % width) != 0)
            {
                _issues.Add(new("MSG_MULTIVALUE_TABLE_INVALID", "Fixed multi-value data has an invalid type or length.", storageId, id));
                if (_state.TakeValue()) values.Add(RetainRaw(0, tableBytes, storageId, id));
                return values.ToImmutable();
            }

            for (int offset = 0, index = 0; offset < tableBytes.Length; offset += width, index++)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                if (!_state.TakeValue())
                {
                    _issues.Add(new("MSG_RESOURCE_LIMIT", "The cumulative value-count limit was exceeded.", storageId, id));
                    break;
                }
                values.Add(DecodeOne(index, baseType, tableBytes.Slice(offset, width), codePage, storageId, id));
            }
        }

        return values.ToImmutable();
    }

    private MapiValue DecodeOne(
        int index,
        ushort type,
        ReadOnlySpan<byte> raw,
        int? codePage,
        uint storageId,
        ushort propertyId)
    {
        if (!_state.TakeDecodedBytes(raw.Length))
        {
            _issues.Add(new("MSG_RESOURCE_LIMIT", "The cumulative decoded-byte limit was exceeded; the value bytes were omitted.", storageId, propertyId));
            return new(index, MapiValueKind.Raw, null, []);
        }

        object? decoded;
        MapiValueKind kind;
        switch (type)
        {
            case 0x0002: kind = MapiValueKind.Integer16; decoded = BinaryPrimitives.ReadInt16LittleEndian(raw); break;
            case 0x0003: kind = MapiValueKind.Integer32; decoded = BinaryPrimitives.ReadInt32LittleEndian(raw); break;
            case 0x0004: kind = MapiValueKind.Real32; decoded = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(raw)); break;
            case 0x0005: kind = MapiValueKind.Real64; decoded = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(raw)); break;
            case 0x0006: kind = MapiValueKind.Currency; decoded = BinaryPrimitives.ReadInt64LittleEndian(raw) / 10000m; break;
            case 0x0007: kind = MapiValueKind.FloatingTime; decoded = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(raw)); break;
            case 0x000A: kind = MapiValueKind.Error; decoded = BinaryPrimitives.ReadUInt32LittleEndian(raw); break;
            case 0x000B:
                kind = MapiValueKind.Boolean;
                ushort boolean = BinaryPrimitives.ReadUInt16LittleEndian(raw);
                decoded = boolean != 0;
                if (boolean is not (0 or 1)) _issues.Add(new("MSG_BOOLEAN_NONCANONICAL", "Boolean value is neither zero nor one.", storageId, propertyId));
                break;
            case 0x0014: kind = MapiValueKind.Integer64; decoded = BinaryPrimitives.ReadInt64LittleEndian(raw); break;
            case 0x001E: kind = MapiValueKind.Text; decoded = DecodeString8(raw, codePage, storageId, propertyId); break;
            case 0x001F:
                kind = MapiValueKind.Text;
                if ((raw.Length & 1) != 0)
                {
                    _issues.Add(new("MSG_UNICODE_LENGTH_INVALID", "Unicode property has an odd byte length.", storageId, propertyId));
                    decoded = null;
                }
                else decoded = Encoding.Unicode.GetString(raw).TrimEnd('\0');
                break;
            case 0x0040:
                kind = MapiValueKind.FileTime;
                long fileTime = BinaryPrimitives.ReadInt64LittleEndian(raw);
                try { decoded = DateTimeOffset.FromFileTime(fileTime); }
                catch (ArgumentOutOfRangeException) { decoded = null; _issues.Add(new("MSG_FILETIME_INVALID", "FILETIME value is outside the supported range.", storageId, propertyId)); }
                break;
            case 0x0048: kind = MapiValueKind.Identifier; decoded = new Guid(raw); break;
            case 0x0102: kind = MapiValueKind.Binary; decoded = null; break;
            case 0x000D: kind = MapiValueKind.OpaqueObject; decoded = null; break;
            default: kind = MapiValueKind.Raw; decoded = null; break;
        }
        return new(index, kind, decoded, ImmutableArray.Create(raw.ToArray()));
    }

    private MapiValue RetainRaw(int index, ReadOnlySpan<byte> raw, uint storageId, ushort propertyId)
    {
        if (!_state.TakeDecodedBytes(raw.Length))
        {
            _issues.Add(new("MSG_RESOURCE_LIMIT", "The cumulative decoded-byte limit was exceeded; malformed raw bytes were omitted.", storageId, propertyId));
            return new(index, MapiValueKind.Raw, null, []);
        }
        return new(index, MapiValueKind.Raw, null, ImmutableArray.Create(raw.ToArray()));
    }

    private string? DecodeString8(ReadOnlySpan<byte> bytes, int? codePage, uint storageId, ushort propertyId)
    {
        int selected = codePage.GetValueOrDefault(1252);
        if (codePage is null or 0)
        {
            _issues.Add(new("MSG_CODEPAGE_FALLBACK", "String8 used the deterministic Windows-1252 fallback.", storageId, propertyId));
            selected = 1252;
        }

        string? value = selected switch
        {
            65001 => Encoding.UTF8.GetString(bytes),
            20127 => Encoding.ASCII.GetString(bytes),
            1252 => DecodeWindows1252(bytes),
            _ => null,
        };
        if (value is null)
        {
            _issues.Add(new("MSG_CODEPAGE_UNSUPPORTED", $"Code page {selected} is unsupported; raw bytes were retained.", storageId, propertyId));
            return null;
        }
        return value.TrimEnd('\0');
    }

    private static string DecodeWindows1252(ReadOnlySpan<byte> bytes) =>
        string.Create(bytes.Length, bytes.ToArray(), static (chars, state) =>
        {
            for (int index = 0; index < state.Length; index++)
            {
                byte value = state[index];
                chars[index] = value is >= 0x80 and <= 0x9F ? Windows1252Controls[value - 0x80] : (char)value;
            }
        });

    private static int HeaderLength(PropertyStreamContext context) => context switch
    {
        PropertyStreamContext.RootMessage => 32,
        PropertyStreamContext.EmbeddedMessage => 24,
        PropertyStreamContext.RecipientOrAttachment => 8,
        _ => throw new ArgumentOutOfRangeException(nameof(context)),
    };

    private static bool IsSingleValueStreamType(ushort type) =>
        IsVariableMultiValueElement(type) || type == 0x0048;

    private static bool IsVariableMultiValueElement(ushort type) =>
        type is 0x001E or 0x001F or 0x0102 or 0x000D;
    private static int FixedWidth(ushort type) => type switch
    {
        0x0002 or 0x000B => 2,
        0x0003 or 0x0004 or 0x000A => 4,
        0x0005 or 0x0006 or 0x0007 or 0x0014 or 0x0040 => 8,
        0x0048 => 16,
        _ => 0,
    };
    private static string SubstreamName(ushort id, ushort type) => $"__substg1.0_{id:x4}{type:x4}";

    internal CompoundFileDirectoryEntry[] Children(uint storageId) =>
        _childrenByParent.TryGetValue(storageId, out CompoundFileDirectoryEntry[]? children) ? children : [];

    internal CompoundFileDirectoryEntry? Stream(uint storageId, string name) =>
        Children(storageId).FirstOrDefault(entry =>
            entry.ObjectType == CompoundFileObjectType.Stream && entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    internal CompoundFileDirectoryEntry? Storage(uint storageId, string name) =>
        Children(storageId).FirstOrDefault(entry =>
            entry.ObjectType == CompoundFileObjectType.Storage && entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static Dictionary<uint, CompoundFileDirectoryEntry[]> BuildChildrenIndex(CompoundFile compoundFile)
    {
        var mutable = new Dictionary<uint, List<CompoundFileDirectoryEntry>>();
        foreach (CompoundFileDirectoryEntry entry in compoundFile.DirectoryEntries)
        {
            if (entry.ParentStreamId is not uint parent) continue;
            if (!mutable.TryGetValue(parent, out List<CompoundFileDirectoryEntry>? children))
            {
                children = [];
                mutable.Add(parent, children);
            }
            children.Add(entry);
        }

        var result = new Dictionary<uint, CompoundFileDirectoryEntry[]>(mutable.Count);
        foreach ((uint parent, List<CompoundFileDirectoryEntry> children) in mutable)
        {
            children.Sort(static (left, right) => left.StreamId.CompareTo(right.StreamId));
            result.Add(parent, [.. children]);
        }
        return result;
    }
}
