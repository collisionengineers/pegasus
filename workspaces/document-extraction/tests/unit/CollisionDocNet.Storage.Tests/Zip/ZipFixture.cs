using System.IO.Compression;
using System.Buffers.Binary;
using System.Text;

namespace CollisionDocNet.Storage.Tests.Zip;

internal static class ZipFixture
{
    internal static byte[] Create(params (string Name, byte[] Content)[] entries)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, byte[] content) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                using Stream stream = entry.Open();
                stream.Write(content);
            }
        }

        return output.ToArray();
    }

    internal static byte[] CreateWithDataDescriptor(string name, byte[] content)
    {
        using var output = new MemoryStream();
        using (var nonSeekable = new NonSeekableWriteStream(output))
        using (var archive = new ZipArchive(nonSeekable, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            using Stream stream = entry.Open();
            stream.Write(content);
        }

        return output.ToArray();
    }

    internal static byte[] PromoteDataDescriptorSizesToZip64(byte[] original)
    {
        int descriptor = FindSignature(original, 0x08074b50);
        int central = FindSignature(original, 0x02014b50, descriptor + 4);
        int end = FindSignature(original, 0x06054b50, central + 4);
        if (descriptor < 0 || central < 0 || end < 0 ||
            BinaryPrimitives.ReadUInt16LittleEndian(original.AsSpan(central + 30)) != 0)
        {
            throw new InvalidOperationException("Expected a descriptor archive without central extra fields.");
        }

        uint crc = BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(descriptor + 4));
        uint compressed = BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(descriptor + 8));
        uint expanded = BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(descriptor + 12));
        ushort nameLength = BinaryPrimitives.ReadUInt16LittleEndian(original.AsSpan(central + 28));
        int centralLength = 46 + nameLength;
        byte[] promoted = new byte[original.Length + 28];

        original.AsSpan(0, descriptor + 8).CopyTo(promoted);
        BinaryPrimitives.WriteUInt64LittleEndian(promoted.AsSpan(descriptor + 8), compressed);
        BinaryPrimitives.WriteUInt64LittleEndian(promoted.AsSpan(descriptor + 16), expanded);

        int newCentral = central + 8;
        original.AsSpan(central, centralLength).CopyTo(promoted.AsSpan(newCentral));
        BinaryPrimitives.WriteUInt16LittleEndian(promoted.AsSpan(newCentral + 6), 45);
        BinaryPrimitives.WriteUInt32LittleEndian(promoted.AsSpan(newCentral + 20), uint.MaxValue);
        BinaryPrimitives.WriteUInt32LittleEndian(promoted.AsSpan(newCentral + 24), uint.MaxValue);
        BinaryPrimitives.WriteUInt16LittleEndian(promoted.AsSpan(newCentral + 30), 20);
        int extra = newCentral + centralLength;
        BinaryPrimitives.WriteUInt16LittleEndian(promoted.AsSpan(extra), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(promoted.AsSpan(extra + 2), 16);
        BinaryPrimitives.WriteUInt64LittleEndian(promoted.AsSpan(extra + 4), expanded);
        BinaryPrimitives.WriteUInt64LittleEndian(promoted.AsSpan(extra + 12), compressed);

        int newEnd = end + 28;
        original.AsSpan(end).CopyTo(promoted.AsSpan(newEnd));
        uint oldCentralSize = BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(end + 12));
        BinaryPrimitives.WriteUInt32LittleEndian(promoted.AsSpan(newEnd + 12), oldCentralSize + 20);
        BinaryPrimitives.WriteUInt32LittleEndian(promoted.AsSpan(newEnd + 16), (uint)newCentral);
        return promoted;
    }

    internal static byte[] AddTrailingByteToDeflatePayload(byte[] original)
    {
        int central = FindSignature(original, 0x02014b50);
        int end = FindSignature(original, 0x06054b50, central + 4);
        if (central < 0 || end < 0 || BinaryPrimitives.ReadUInt16LittleEndian(original.AsSpan(8)) != 8)
        {
            throw new InvalidOperationException("Expected a single deflated entry.");
        }

        uint compressed = BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(18));
        ushort nameLength = BinaryPrimitives.ReadUInt16LittleEndian(original.AsSpan(26));
        ushort extraLength = BinaryPrimitives.ReadUInt16LittleEndian(original.AsSpan(28));
        int dataEnd = checked(30 + nameLength + extraLength + (int)compressed);
        byte[] altered = new byte[original.Length + 1];
        original.AsSpan(0, dataEnd).CopyTo(altered);
        altered[dataEnd] = 0x5a;
        original.AsSpan(dataEnd).CopyTo(altered.AsSpan(dataEnd + 1));

        BinaryPrimitives.WriteUInt32LittleEndian(altered.AsSpan(18), compressed + 1);
        int newCentral = central + 1;
        BinaryPrimitives.WriteUInt32LittleEndian(altered.AsSpan(newCentral + 20), compressed + 1);
        int newEnd = end + 1;
        BinaryPrimitives.WriteUInt32LittleEndian(altered.AsSpan(newEnd + 16), (uint)newCentral);
        return altered;
    }

    internal static byte[] CreateOverlappingLocalRecords()
    {
        byte[] secondRecord;
        using (var second = new MemoryStream())
        using (var writer = new BinaryWriter(second, Encoding.UTF8, leaveOpen: true))
        {
            WriteLocal(writer, "two", [0x42], Crc32([0x42]));
            secondRecord = second.ToArray();
        }

        uint firstCrc = Crc32(secondRecord);
        using var output = new MemoryStream();
        using var archive = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        WriteLocal(archive, "one", secondRecord, firstCrc);
        int centralOffset = checked((int)output.Position);
        WriteCentral(archive, "one", secondRecord.Length, firstCrc, 0);
        WriteCentral(archive, "two", 1, Crc32([0x42]), 33);
        int centralSize = checked((int)output.Position - centralOffset);
        archive.Write(0x06054b50u);
        archive.Write((ushort)0);
        archive.Write((ushort)0);
        archive.Write((ushort)2);
        archive.Write((ushort)2);
        archive.Write((uint)centralSize);
        archive.Write((uint)centralOffset);
        archive.Write((ushort)0);
        return output.ToArray();
    }

    internal static byte[] RemoveDataDescriptorSignature(byte[] signed)
    {
        int descriptor = FindSignature(signed, 0x08074b50);
        int central = FindSignature(signed, 0x02014b50, descriptor + 4);
        if (descriptor < 0 || central < 0)
        {
            throw new InvalidOperationException("Expected signed data descriptor archive.");
        }

        byte[] unsigned = new byte[signed.Length - 4];
        signed.AsSpan(0, descriptor).CopyTo(unsigned);
        signed.AsSpan(descriptor + 4).CopyTo(unsigned.AsSpan(descriptor));
        int end = unsigned.Length - 22;
        uint centralOffset = BinaryPrimitives.ReadUInt32LittleEndian(unsigned.AsSpan(end + 16));
        BinaryPrimitives.WriteUInt32LittleEndian(unsigned.AsSpan(end + 16), centralOffset - 4);
        return unsigned;
    }

    internal static byte[] CreateMinimalDocx(string? relationshipTarget = null)
    {
        const string contentTypes = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
            </Types>
            """;
        const string document = """
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body/></w:document>
            """;
        if (relationshipTarget is null)
        {
            return Create(
                ("[Content_Types].xml", Encoding.UTF8.GetBytes(contentTypes)),
                ("word/document.xml", Encoding.UTF8.GetBytes(document)));
        }

        string relationships = $"""
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="urn:test" Target="{relationshipTarget}"/>
            </Relationships>
            """;
        return Create(
            ("[Content_Types].xml", Encoding.UTF8.GetBytes(contentTypes)),
            ("word/document.xml", Encoding.UTF8.GetBytes(document)),
            ("word/_rels/document.xml.rels", Encoding.UTF8.GetBytes(relationships)));
    }

    internal static byte[] PromoteEndRecordToZip64(byte[] original)
    {
        int end = original.Length - 22;
        uint centralSize = BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(end + 12));
        uint centralOffset = BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(end + 16));
        ushort entries = BinaryPrimitives.ReadUInt16LittleEndian(original.AsSpan(end + 10));
        byte[] bytes = new byte[end + 56 + 20 + 22];
        original.AsSpan(0, end).CopyTo(bytes);
        int zip64 = end;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(zip64), 0x06064b50);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(zip64 + 4), 44);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(zip64 + 12), 45);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(zip64 + 14), 45);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(zip64 + 24), entries);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(zip64 + 32), entries);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(zip64 + 40), centralSize);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(zip64 + 48), centralOffset);
        int locator = zip64 + 56;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(locator), 0x07064b50);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(locator + 8), (ulong)zip64);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(locator + 16), 1);
        int newEnd = locator + 20;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(newEnd), 0x06054b50);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(newEnd + 8), ushort.MaxValue);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(newEnd + 10), ushort.MaxValue);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(newEnd + 12), uint.MaxValue);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(newEnd + 16), uint.MaxValue);
        return bytes;
    }

    internal static int FindSignature(byte[] bytes, uint signature, int start = 0)
    {
        Span<byte> pattern = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(pattern, signature);
        return bytes.AsSpan(start).IndexOf(pattern) is int relative && relative >= 0
            ? start + relative
            : -1;
    }

    private static void WriteLocal(BinaryWriter writer, string name, byte[] content, uint crc)
    {
        byte[] nameBytes = Encoding.ASCII.GetBytes(name);
        writer.Write(0x04034b50u);
        writer.Write((ushort)20);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(0u);
        writer.Write(crc);
        writer.Write((uint)content.Length);
        writer.Write((uint)content.Length);
        writer.Write((ushort)nameBytes.Length);
        writer.Write((ushort)0);
        writer.Write(nameBytes);
        writer.Write(content);
    }

    private static void WriteCentral(
        BinaryWriter writer, string name, int size, uint crc, uint localOffset)
    {
        byte[] nameBytes = Encoding.ASCII.GetBytes(name);
        writer.Write(0x02014b50u);
        writer.Write((ushort)20);
        writer.Write((ushort)20);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(0u);
        writer.Write(crc);
        writer.Write((uint)size);
        writer.Write((uint)size);
        writer.Write((ushort)nameBytes.Length);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(0u);
        writer.Write(localOffset);
        writer.Write(nameBytes);
    }

    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in bytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? 0xedb88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        return ~crc;
    }

    private sealed class NonSeekableWriteStream(Stream inner) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        public override void Write(ReadOnlySpan<byte> buffer) => inner.Write(buffer);
    }
}
