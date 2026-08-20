using System.Buffers.Binary;
using System.Text;
using Pegasus.Infrastructure.Intake.DocumentExtraction.Cfb;

namespace Pegasus.IntegrationTests.DocumentExtraction;

/// <summary>
/// Builds minimal genuine Outlook <c>.msg</c> bytes — a real MS-CFB container
/// with root-message MAPI property streams held in the mini stream — for
/// end-to-end intake tests. No corpus material is involved.
/// </summary>
internal sealed class MsgFileBuilder
{
    private const int SectorSize = 512;
    private const int MiniSectorSize = 64;
    private const int DirectoryEntrySize = 128;

    private readonly Node _root = new("Root Entry", CompoundFileObjectType.RootStorage);

    private sealed class Node(string name, CompoundFileObjectType type)
    {
        public string Name { get; } = name;
        public CompoundFileObjectType Type { get; } = type;
        public byte[] Content { get; set; } = [];
        public List<Node> Children { get; } = [];
        public int EntryId { get; set; }
        public uint StartMiniSector { get; set; } = CompoundFileConstants.EndOfChain;
        public uint LeftSibling { get; set; } = CompoundFileConstants.NoStream;
        public uint RightSibling { get; set; } = CompoundFileConstants.NoStream;
        public uint Child { get; set; } = CompoundFileConstants.NoStream;
    }

    public MsgFileBuilder WithRootMessage(string messageClass, string subject, string body, string? senderSmtpAddress = null)
    {
        var records = new List<(ushort Id, ushort Type, byte[] Inline, byte[]? Stream)>
        {
            (0x001A, 0x001F, new byte[8], Utf16z(messageClass)),
            (0x0037, 0x001F, new byte[8], Utf16z(subject)),
            (0x1000, 0x001F, new byte[8], Utf16z(body)),
        };
        if (senderSmtpAddress is not null)
        {
            records.Add((0x5D01, 0x001F, new byte[8], Utf16z(senderSmtpAddress)));
        }

        AddPropertyStreams(_root, headerLength: 32, records);
        return this;
    }

    public MsgFileBuilder WithByValueAttachment(string fileName, string mediaType, byte[] content)
    {
        var attachment = new Node($"__attach_version1.0_#{_root.Children.Count(child => child.Type == CompoundFileObjectType.Storage):x8}", CompoundFileObjectType.Storage);
        _root.Children.Add(attachment);
        var method = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(method, 1);
        AddPropertyStreams(attachment, headerLength: 8,
        [
            (0x3705, 0x0003, method, null),
            (0x3701, 0x0102, new byte[8], content),
            (0x3707, 0x001F, new byte[8], Utf16z(fileName)),
            (0x370E, 0x001F, new byte[8], Utf16z(mediaType)),
        ]);
        return this;
    }

    public byte[] Build()
    {
        // Assign directory entry ids in a stable walk and lay every stream
        // into the mini stream (all fixture streams are far below the 4,096
        // byte cutoff).
        var entries = new List<Node>();
        Collect(_root, entries);
        for (var index = 0; index < entries.Count; index++)
        {
            entries[index].EntryId = index;
        }

        var miniStream = new MemoryStream();
        var miniFat = new List<uint>();
        foreach (var node in entries.Where(node => node.Type == CompoundFileObjectType.Stream))
        {
            var sectors = Math.Max(1, (node.Content.Length + MiniSectorSize - 1) / MiniSectorSize);
            node.StartMiniSector = (uint)miniFat.Count;
            for (var sector = 0; sector < sectors; sector++)
            {
                miniFat.Add(sector == sectors - 1 ? CompoundFileConstants.EndOfChain : (uint)(miniFat.Count + 1));
            }

            miniStream.Write(node.Content);
            var padding = (sectors * MiniSectorSize) - node.Content.Length;
            miniStream.Write(new byte[padding]);
        }

        foreach (var node in entries.Where(node => node.Children.Count > 0))
        {
            node.Child = BuildSiblingTree(node.Children);
        }

        var miniStreamBytes = miniStream.ToArray();
        var miniStreamSectors = Math.Max(1, (miniStreamBytes.Length + SectorSize - 1) / SectorSize);
        var directorySectors = Math.Max(1, (entries.Count + 3) / 4);

        // Sector layout: FAT | directory chain | miniFAT | mini stream chain.
        var fatSector = 0;
        var firstDirectorySector = 1;
        var miniFatSector = firstDirectorySector + directorySectors;
        var firstMiniStreamSector = miniFatSector + 1;
        var sectorCount = firstMiniStreamSector + miniStreamSectors;
        var bytes = new byte[(sectorCount + 1) * SectorSize];

        WriteHeader(bytes, (uint)miniFatSector, (uint)firstDirectorySector);
        WriteFat(bytes, fatSector, firstDirectorySector, directorySectors, miniFatSector, firstMiniStreamSector, miniStreamSectors);
        WriteMiniFat(bytes, miniFatSector, miniFat);
        miniStreamBytes.CopyTo(bytes.AsSpan((firstMiniStreamSector + 1) * SectorSize));
        WriteDirectory(bytes, firstDirectorySector, directorySectors, entries, (uint)firstMiniStreamSector, (ulong)miniStreamBytes.Length);
        return bytes;
    }

    private static void Collect(Node node, List<Node> entries)
    {
        entries.Add(node);
        foreach (var child in node.Children)
        {
            Collect(child, entries);
        }
    }

    private static uint BuildSiblingTree(List<Node> children)
    {
        var ordered = children
            .OrderBy(child => child.Name.Length)
            .ThenBy(child => child.Name.ToUpperInvariant(), StringComparer.Ordinal)
            .ToList();
        return LinkSubtree(ordered, 0, ordered.Count - 1);
    }

    private static uint LinkSubtree(List<Node> ordered, int low, int high)
    {
        if (low > high)
        {
            return CompoundFileConstants.NoStream;
        }

        var middle = (low + high) / 2;
        var node = ordered[middle];
        node.LeftSibling = LinkSubtree(ordered, low, middle - 1);
        node.RightSibling = LinkSubtree(ordered, middle + 1, high);
        return (uint)node.EntryId;
    }

    private static void AddPropertyStreams(Node parent, int headerLength, List<(ushort Id, ushort Type, byte[] Inline, byte[]? Stream)> records)
    {
        var propertyBytes = new byte[headerLength + (records.Count * 16)];
        for (var index = 0; index < records.Count; index++)
        {
            var (id, type, inline, stream) = records[index];
            var offset = headerLength + (index * 16);
            BinaryPrimitives.WriteUInt32LittleEndian(propertyBytes.AsSpan(offset), ((uint)id << 16) | type);
            inline.CopyTo(propertyBytes.AsSpan(offset + 8));
            if (stream is not null)
            {
                parent.Children.Add(new Node($"__substg1.0_{id:X4}{type:X4}", CompoundFileObjectType.Stream)
                {
                    Content = stream,
                });
            }
        }

        parent.Children.Add(new Node("__properties_version1.0", CompoundFileObjectType.Stream)
        {
            Content = propertyBytes,
        });
    }

    private static byte[] Utf16z(string value) => Encoding.Unicode.GetBytes(value + '\0');

    private static void WriteHeader(byte[] bytes, uint miniFatSector, uint firstDirectorySector)
    {
        byte[] signature = [0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1];
        signature.CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(24), 0x003e);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(26), 3);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(28), 0xfffe);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(30), 9);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(32), 6);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(44), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(48), firstDirectorySector);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(56), 4096);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(60), miniFatSector);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(64), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(68), CompoundFileConstants.EndOfChain);
        for (var index = 0; index < CompoundFileConstants.HeaderDifatEntryCount; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(76 + (index * 4)), CompoundFileConstants.FreeSector);
        }

        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(76), 0);
    }

    private static void WriteFat(
        byte[] bytes,
        int fatSector,
        int firstDirectorySector,
        int directorySectors,
        int miniFatSector,
        int firstMiniStreamSector,
        int miniStreamSectors)
    {
        var fat = Sector(bytes, fatSector);
        for (var offset = 0; offset < fat.Length; offset += 4)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(fat[offset..], CompoundFileConstants.FreeSector);
        }

        BinaryPrimitives.WriteUInt32LittleEndian(fat, CompoundFileConstants.FatSector);
        for (var sector = 0; sector < directorySectors; sector++)
        {
            var value = sector == directorySectors - 1
                ? CompoundFileConstants.EndOfChain
                : (uint)(firstDirectorySector + sector + 1);
            BinaryPrimitives.WriteUInt32LittleEndian(fat[((firstDirectorySector + sector) * 4)..], value);
        }

        BinaryPrimitives.WriteUInt32LittleEndian(fat[(miniFatSector * 4)..], CompoundFileConstants.EndOfChain);
        for (var sector = 0; sector < miniStreamSectors; sector++)
        {
            var value = sector == miniStreamSectors - 1
                ? CompoundFileConstants.EndOfChain
                : (uint)(firstMiniStreamSector + sector + 1);
            BinaryPrimitives.WriteUInt32LittleEndian(fat[((firstMiniStreamSector + sector) * 4)..], value);
        }
    }

    private static void WriteMiniFat(byte[] bytes, int miniFatSector, List<uint> miniFat)
    {
        var sector = Sector(bytes, miniFatSector);
        for (var offset = 0; offset < sector.Length; offset += 4)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(sector[offset..], CompoundFileConstants.FreeSector);
        }

        for (var index = 0; index < miniFat.Count; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(sector[(index * 4)..], miniFat[index]);
        }
    }

    private static void WriteDirectory(
        byte[] bytes,
        int firstDirectorySector,
        int directorySectors,
        List<Node> entries,
        uint miniStreamStartSector,
        ulong miniStreamLength)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            var node = entries[index];
            var sector = firstDirectorySector + (index / 4);
            var entry = Sector(bytes, sector).Slice((index % 4) * DirectoryEntrySize, DirectoryEntrySize);
            var nameBytes = Encoding.Unicode.GetBytes(node.Name, entry);
            BinaryPrimitives.WriteUInt16LittleEndian(entry[64..], checked((ushort)(nameBytes + 2)));
            entry[66] = (byte)node.Type;
            entry[67] = (byte)CompoundFileNodeColor.Black;
            BinaryPrimitives.WriteUInt32LittleEndian(entry[68..], node.LeftSibling);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[72..], node.RightSibling);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[76..], node.Child);
            if (node.Type == CompoundFileObjectType.RootStorage)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(entry[116..], miniStreamStartSector);
                BinaryPrimitives.WriteUInt64LittleEndian(entry[120..], miniStreamLength);
            }
            else if (node.Type == CompoundFileObjectType.Stream)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(entry[116..], node.StartMiniSector);
                BinaryPrimitives.WriteUInt64LittleEndian(entry[120..], (ulong)node.Content.Length);
            }
            else
            {
                BinaryPrimitives.WriteUInt32LittleEndian(entry[116..], CompoundFileConstants.EndOfChain);
            }
        }

        for (var index = entries.Count; index < directorySectors * 4; index++)
        {
            var sector = firstDirectorySector + (index / 4);
            var entry = Sector(bytes, sector).Slice((index % 4) * DirectoryEntrySize, DirectoryEntrySize);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[68..], CompoundFileConstants.NoStream);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[72..], CompoundFileConstants.NoStream);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[76..], CompoundFileConstants.NoStream);
        }
    }

    private static Span<byte> Sector(byte[] bytes, int sector) => bytes.AsSpan((sector + 1) * SectorSize, SectorSize);
}
