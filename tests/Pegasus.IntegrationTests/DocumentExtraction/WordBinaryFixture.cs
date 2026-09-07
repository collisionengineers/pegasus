using System.Buffers.Binary;
using System.Collections.Immutable;
using Pegasus.Infrastructure.Intake.DocumentExtraction.Cfb;
using Pegasus.Infrastructure.Intake.DocumentExtraction.Word;

namespace Pegasus.IntegrationTests.DocumentExtraction;

internal sealed record FixturePiece(uint CpStart, uint CpEnd, uint FileOffset, bool Unicode, string Text, ushort Prm = 0);

internal static class WordBinaryFixture
{
    internal static byte[] CreateRawCfb(IReadOnlyList<FixturePiece> pieces, uint[]? storyLengths = null)
    {
        CompoundFile logical = Create(pieces, storyLengths);
        byte[] word = logical.DirectoryEntries.Single(static entry => entry.Name == "WordDocument").Content.ToArray();
        byte[] table = logical.DirectoryEntries.Single(static entry => entry.Name == "0Table").Content.ToArray();
        const int sectorSize = 512;
        const int sectorCount = 18;
        byte[] bytes = new byte[(sectorCount + 1) * sectorSize];
        byte[] signature = [0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1];
        signature.CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(24), 0x003e);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(26), 3);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(28), 0xfffe);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(30), 9);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(32), 6);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(44), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(48), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(56), 4096);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(60), CompoundFileConstants.EndOfChain);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(68), CompoundFileConstants.EndOfChain);
        for (int index = 0; index < CompoundFileConstants.HeaderDifatEntryCount; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(76 + (index * 4)), CompoundFileConstants.FreeSector);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(76), 0);

        Span<byte> fat = Sector(bytes, 0);
        for (int offset = 0; offset < fat.Length; offset += 4)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(fat[offset..], CompoundFileConstants.FreeSector);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(fat, CompoundFileConstants.FatSector);
        BinaryPrimitives.WriteUInt32LittleEndian(fat[4..], CompoundFileConstants.EndOfChain);
        WriteChain(fat, 2, 9);
        WriteChain(fat, 10, 17);

        Span<byte> directory = Sector(bytes, 1);
        WriteDirectoryEntry(directory[..128], "Root Entry", CompoundFileObjectType.RootStorage,
            CompoundFileNodeColor.Black, CompoundFileConstants.NoStream, CompoundFileConstants.NoStream, 1,
            CompoundFileConstants.EndOfChain, 0);
        WriteDirectoryEntry(directory.Slice(128, 128), "WordDocument", CompoundFileObjectType.Stream,
            CompoundFileNodeColor.Black, 2, CompoundFileConstants.NoStream, CompoundFileConstants.NoStream, 2, 4096);
        WriteDirectoryEntry(directory.Slice(256, 128), "0Table", CompoundFileObjectType.Stream,
            CompoundFileNodeColor.Red, CompoundFileConstants.NoStream, CompoundFileConstants.NoStream,
            CompoundFileConstants.NoStream, 10, 4096);
        for (int streamId = 3; streamId < 4; streamId++)
        {
            Span<byte> unused = directory.Slice(streamId * 128, 128);
            BinaryPrimitives.WriteUInt32LittleEndian(unused[68..], CompoundFileConstants.NoStream);
            BinaryPrimitives.WriteUInt32LittleEndian(unused[72..], CompoundFileConstants.NoStream);
            BinaryPrimitives.WriteUInt32LittleEndian(unused[76..], CompoundFileConstants.NoStream);
        }

        word.CopyTo(bytes.AsSpan(3 * sectorSize));
        table.CopyTo(bytes.AsSpan(11 * sectorSize));
        return bytes;
    }

    internal static CompoundFile Create(
        IReadOnlyList<FixturePiece> pieces,
        uint[]? storyLengths = null,
        bool useOneTable = false,
        bool encrypted = false,
        bool obfuscated = false,
        bool hasPictures = false,
        ushort identifier = 0xa5ec,
        ushort version = 0x00c1,
        ushort effectiveVersion = 0,
        short nextFibPage = 0,
        ushort characterSet = 0,
        bool includeSelectedTable = true,
        bool malformedReservedFc = false,
        bool addUnprocessedRange = false,
        bool complex = true,
        Action<byte[], byte[]>? configureStreams = null,
        IReadOnlyList<CompoundFileDirectoryEntry>? additionalEntries = null)
    {
        storyLengths ??= [pieces.Count == 0 ? 0 : pieces[^1].CpEnd, 0, 0, 0, 0, 0, 0, 0];
        byte[] wordDocument = new byte[2048];
        foreach (FixturePiece piece in pieces)
        {
            if (piece.Unicode)
            {
                for (int index = 0; index < piece.Text.Length; index++)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(wordDocument.AsSpan((int)piece.FileOffset + (index * 2)), piece.Text[index]);
                }
            }
            else
            {
                for (int index = 0; index < piece.Text.Length; index++)
                {
                    wordDocument[checked((int)piece.FileOffset + index)] = Encode1252(piece.Text[index]);
                }
            }
        }

        byte[] table = BuildTable(pieces, malformedReservedFc);
        WriteFib(wordDocument, storyLengths, useOneTable, encrypted, obfuscated, hasPictures,
            identifier, version, effectiveVersion, nextFibPage, characterSet,
            checked((uint)(12 * pieces.Count + 9)), addUnprocessedRange, complex);
        configureStreams?.Invoke(wordDocument, table);

        var entries = ImmutableArray.CreateBuilder<CompoundFileDirectoryEntry>();
        entries.Add(Entry(0, "Root Entry", CompoundFileObjectType.RootStorage, null, []));
        entries.Add(Entry(1, "WordDocument", CompoundFileObjectType.Stream, 0, wordDocument));
        if (includeSelectedTable)
        {
            entries.Add(Entry(2, useOneTable ? "1Table" : "0Table", CompoundFileObjectType.Stream, 0, table));
        }

        if (additionalEntries is not null)
        {
            entries.AddRange(additionalEntries);
        }

        return new CompoundFile(
            new CompoundFileHeader(0x003e, 3, 512, 64, 0, 1, 0, 0, 4096, 0xfffffffe, 0, 0xfffffffe, 0, []),
            [], [], [], entries.ToImmutable());
    }

    internal static CompoundFileDirectoryEntry AdditionalEntry(
        uint id,
        string name,
        CompoundFileObjectType type,
        uint? parent,
        byte[] content,
        Guid? classId = null) =>
        Entry(id, name, type, parent, content) with { ClassId = classId ?? Guid.Empty };

    internal static void SetFibRange(byte[] wordDocument, int index, uint offset, uint length)
    {
        const int catalogue = 154;
        BinaryPrimitives.WriteUInt32LittleEndian(wordDocument.AsSpan(catalogue + (index * 8)), offset);
        BinaryPrimitives.WriteUInt32LittleEndian(wordDocument.AsSpan(catalogue + (index * 8) + 4), length);
    }

    private static byte[] BuildTable(IReadOnlyList<FixturePiece> pieces, bool malformedReservedFc)
    {
        int plcLength = 12 * pieces.Count + 4;
        byte[] table = new byte[512];
        int clx = 64;
        table[clx] = 0x02;
        BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(clx + 1), (uint)plcLength);
        int cpOffset = clx + 5;
        for (int index = 0; index < pieces.Count; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(cpOffset + (index * 4)), pieces[index].CpStart);
        }

        BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(cpOffset + (pieces.Count * 4)), pieces.Count == 0 ? 0 : pieces[^1].CpEnd);
        int pcdOffset = cpOffset + ((pieces.Count + 1) * 4);
        for (int index = 0; index < pieces.Count; index++)
        {
            FixturePiece piece = pieces[index];
            uint encoded = piece.Unicode ? piece.FileOffset : 0x40000000u | checked(piece.FileOffset * 2);
            if (malformedReservedFc && index == 0) encoded |= 0x80000000u;
            BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(pcdOffset + (index * 8) + 2), encoded);
            BinaryPrimitives.WriteUInt16LittleEndian(table.AsSpan(pcdOffset + (index * 8) + 6), piece.Prm);
        }

        return table;
    }

    private static void WriteFib(
        byte[] bytes,
        uint[] stories,
        bool oneTable,
        bool encrypted,
        bool obfuscated,
        bool hasPictures,
        ushort identifier,
        ushort version,
        ushort effectiveVersion,
        short nextFibPage,
        ushort characterSet,
        uint clxLength,
        bool addUnprocessedRange,
        bool complex)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0), identifier);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), version);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 0x0409);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), nextFibPage);
        ushort flags = 0;
        // fComplex: fast-saved (incremental) status. Real Word writes a CLX
        // piece table with this flag UNSET on a clean single save.
        if (complex) flags |= 0x0004;
        if (oneTable) flags |= 0x0200;
        if (encrypted) flags |= 0x0100;
        if (obfuscated) flags |= 0x8000;
        if (hasPictures) flags |= 0x0008;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), flags);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(14), encrypted && !obfuscated ? 1u : 0u);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(20), characterSet);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24), 512);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28), (uint)bytes.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(32), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(62), 22);
        // FibRgLw97[0] is cbMac, the declared meaningful extent of the stream.
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(64), (uint)bytes.Length);
        for (int index = 0; index < stories.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(64 + ((3 + index) * 4)), stories[index]);
        }

        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(152), 34);
        int catalogue = 154;
        if (addUnprocessedRange)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(catalogue), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(catalogue + 4), 1);
        }

        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(catalogue + (33 * 8)), 64);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(catalogue + (33 * 8) + 4), clxLength);
        if (effectiveVersion != 0)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(426), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(428), effectiveVersion);
        }
    }

    private static CompoundFileDirectoryEntry Entry(uint id, string name, CompoundFileObjectType type, uint? parent, byte[] content) =>
        new(id, name, checked((ushort)((name.Length + 1) * 2)), type, CompoundFileNodeColor.Black,
            0xffffffff, 0xffffffff, 0xffffffff, Guid.Empty, 0, 0, 0, 0xfffffffe,
            (ulong)content.Length, parent, ImmutableArray.CreateRange(content));

    private static byte Encode1252(char value) => value switch
    {
        '\u20ac' => 0x80,
        <= '\u00ff' => (byte)value,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static Span<byte> Sector(byte[] bytes, int sector) => bytes.AsSpan((sector + 1) * 512, 512);

    private static void WriteChain(Span<byte> fat, int first, int last)
    {
        for (int sector = first; sector <= last; sector++)
        {
            uint next = sector == last ? CompoundFileConstants.EndOfChain : (uint)(sector + 1);
            BinaryPrimitives.WriteUInt32LittleEndian(fat[(sector * 4)..], next);
        }
    }

    private static void WriteDirectoryEntry(
        Span<byte> entry,
        string name,
        CompoundFileObjectType type,
        CompoundFileNodeColor color,
        uint left,
        uint right,
        uint child,
        uint start,
        ulong size)
    {
        int nameBytes = System.Text.Encoding.Unicode.GetBytes(name, entry);
        BinaryPrimitives.WriteUInt16LittleEndian(entry[64..], checked((ushort)(nameBytes + 2)));
        entry[66] = (byte)type;
        entry[67] = (byte)color;
        BinaryPrimitives.WriteUInt32LittleEndian(entry[68..], left);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[72..], right);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[76..], child);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[116..], start);
        BinaryPrimitives.WriteUInt64LittleEndian(entry[120..], size);
    }
}
