using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using CollisionDocNet.Storage.CompoundFile;
using CollisionDocNet.Storage.Detection;

namespace CollisionDocNet.Performance;

internal sealed record InputCase(string Name, string FileName, string MediaType, string ExpectedFormat, byte[] Bytes);

internal sealed class SyntheticInputSet
{
    internal static FileFormatDetectionLimits DetectionLimits { get; } = new() { MaximumInputBytes = 10 * 1024 * 1024 };

    private SyntheticInputSet(byte[] pdf, byte[] doc, byte[] docx, byte[] msg, byte[] eml, byte[] pdfOneMegabyte, byte[] docxOneMegabyte, byte[] emlOneMegabyte)
    {
        Pdf = pdf;
        Doc = doc;
        Docx = docx;
        Msg = msg;
        Eml = eml;
        PdfOneMegabyte = pdfOneMegabyte;
        DocxOneMegabyte = docxOneMegabyte;
        EmlOneMegabyte = emlOneMegabyte;
        Cases =
        [
            new("pdf", "evidence.pdf", "application/pdf", "Pdf", Pdf),
            new("doc", "evidence.doc", "application/msword", "WordBinary", Doc),
            new("docx", "evidence.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "WordprocessingMl", Docx),
            new("msg", "evidence.msg", "application/vnd.ms-outlook", "OutlookItem", Msg),
            new("eml", "evidence.eml", "message/rfc822", "InternetMessage", Eml),
        ];
    }

    internal byte[] Pdf { get; }
    internal byte[] Doc { get; }
    internal byte[] Docx { get; }
    internal byte[] Msg { get; }
    internal byte[] Eml { get; }
    internal byte[] PdfOneMegabyte { get; }
    internal byte[] DocxOneMegabyte { get; }
    internal byte[] EmlOneMegabyte { get; }
    internal InputCase[] Cases { get; }

    internal static SyntheticInputSet Create() => new(
        PdfDocument(4 * 1024),
        WordDocument("Synthetic performance evidence."),
        DocxDocument(4 * 1024),
        MsgDocument(),
        EmlMessage(4 * 1024),
        PdfDocument(1024 * 1024),
        DocxDocument(1024 * 1024),
        EmlMessage(1024 * 1024));

    private static byte[] EmlMessage(int bodyCharacters) => Encoding.ASCII.GetBytes(
        "From: sender@example.invalid\r\nTo: receiver@example.invalid\r\nSubject: synthetic performance evidence\r\n" +
        "Content-Type: text/plain; charset=us-ascii\r\n\r\n" + new string('e', bodyCharacters) + "\r\n");

    private static byte[] DocxDocument(int textCharacters)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", "<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
            WriteEntry(archive, "_rels/.rels", "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
            WriteEntry(archive, "word/document.xml", "<?xml version=\"1.0\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>" + new string('d', textCharacters) + "</w:t></w:r></w:p></w:body></w:document>");
        }
        return output.ToArray();
    }

    private static byte[] PdfDocument(int textCharacters)
    {
        string content = "BT /F1 12 Tf (" + new string('p', textCharacters) + ") Tj ET";
        var bytes = new List<byte>();
        var offsets = new List<int> { 0 };
        Add(bytes, "%PDF-2.0\n");
        AddObject(bytes, offsets, "<< /Type /Catalog /Pages 2 0 R >>");
        AddObject(bytes, offsets, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        AddObject(bytes, offsets, "<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>");
        int streamNumber = offsets.Count;
        offsets.Add(bytes.Count);
        Add(bytes, $"{streamNumber} 0 obj\n<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream\nendobj\n");
        AddObject(bytes, offsets, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        int xref = bytes.Count;
        Add(bytes, $"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        for (int index = 1; index < offsets.Count; index++) Add(bytes, $"{offsets[index]:D10} 00000 n \n");
        Add(bytes, $"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return bytes.ToArray();
    }

    private static byte[] WordDocument(string text)
    {
        byte[] word = new byte[4096];
        byte[] table = new byte[4096];
        Encoding.Unicode.GetBytes(text, word.AsSpan(512));
        WriteFib(word, text.Length);
        WritePieceTable(table, text.Length);
        return TwoStreamCompound("WordDocument", word, "0Table", table);
    }

    private static byte[] MsgDocument()
    {
        byte[] properties = new byte[4096];
        byte[] messageClass = new byte[4096];
        byte[] bodyBytes = Encoding.Unicode.GetBytes("IPM.Note\0");
        bodyBytes.CopyTo(messageClass, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(properties.AsSpan(32), 0x001A001F);
        int propertyCount = (properties.Length - 32) / 16;
        for (int index = 1; index < propertyCount; index++)
        {
            int offset = 32 + (index * 16);
            uint propertyId = checked((uint)(0x4000 + index));
            BinaryPrimitives.WriteUInt32LittleEndian(properties.AsSpan(offset), (propertyId << 16) | 0x0003);
            BinaryPrimitives.WriteInt32LittleEndian(properties.AsSpan(offset + 8), index);
        }
        return TwoStreamCompound("__properties_version1.0", properties, "__substg1.0_001a001f", messageClass);
    }

    private static byte[] TwoStreamCompound(string firstName, byte[] first, string secondName, byte[] second)
    {
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
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(76 + (index * 4)), CompoundFileConstants.FreeSector);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(76), 0);

        Span<byte> fat = Sector(bytes, 0);
        for (int offset = 0; offset < fat.Length; offset += 4)
            BinaryPrimitives.WriteUInt32LittleEndian(fat[offset..], CompoundFileConstants.FreeSector);
        BinaryPrimitives.WriteUInt32LittleEndian(fat, CompoundFileConstants.FatSector);
        BinaryPrimitives.WriteUInt32LittleEndian(fat[4..], CompoundFileConstants.EndOfChain);
        WriteChain(fat, 2, 9);
        WriteChain(fat, 10, 17);

        Span<byte> directory = Sector(bytes, 1);
        WriteDirectoryEntry(directory[..128], "Root Entry", CompoundFileObjectType.RootStorage,
            CompoundFileNodeColor.Black, CompoundFileConstants.NoStream, CompoundFileConstants.NoStream, 1,
            CompoundFileConstants.EndOfChain, 0);
        WriteDirectoryEntry(directory.Slice(128, 128), firstName, CompoundFileObjectType.Stream,
            CompoundFileNodeColor.Black, 2, CompoundFileConstants.NoStream, CompoundFileConstants.NoStream, 2, 4096);
        WriteDirectoryEntry(directory.Slice(256, 128), secondName, CompoundFileObjectType.Stream,
            CompoundFileNodeColor.Red, CompoundFileConstants.NoStream, CompoundFileConstants.NoStream,
            CompoundFileConstants.NoStream, 10, 4096);
        Span<byte> unused = directory.Slice(384, 128);
        BinaryPrimitives.WriteUInt32LittleEndian(unused[68..], CompoundFileConstants.NoStream);
        BinaryPrimitives.WriteUInt32LittleEndian(unused[72..], CompoundFileConstants.NoStream);
        BinaryPrimitives.WriteUInt32LittleEndian(unused[76..], CompoundFileConstants.NoStream);

        first.CopyTo(bytes.AsSpan(3 * sectorSize));
        second.CopyTo(bytes.AsSpan(11 * sectorSize));
        return bytes;
    }

    private static void WriteFib(byte[] bytes, int textLength)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0), 0xa5ec);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 0x00c1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 0x0409);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 0x0004);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24), 512);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28), (uint)bytes.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(32), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(62), 22);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(76), checked((uint)textLength));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(152), 34);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(154 + (33 * 8)), 64);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(154 + (33 * 8) + 4), 21);
    }

    private static void WritePieceTable(byte[] table, int textLength)
    {
        const int clx = 64;
        table[clx] = 0x02;
        BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(clx + 1), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(clx + 5), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(clx + 9), checked((uint)textLength));
        BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(clx + 15), 512);
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
        using Stream stream = entry.Open();
        stream.Write(Encoding.UTF8.GetBytes(content));
    }

    private static void AddObject(List<byte> bytes, List<int> offsets, string body)
    {
        int number = offsets.Count;
        offsets.Add(bytes.Count);
        Add(bytes, $"{number} 0 obj\n{body}\nendobj\n");
    }

    private static void Add(List<byte> bytes, string text) => bytes.AddRange(Encoding.Latin1.GetBytes(text));

    private static Span<byte> Sector(byte[] bytes, int sector) => bytes.AsSpan((sector + 1) * 512, 512);

    private static void WriteChain(Span<byte> fat, int first, int last)
    {
        for (int sector = first; sector <= last; sector++)
        {
            uint next = sector == last ? CompoundFileConstants.EndOfChain : (uint)(sector + 1);
            BinaryPrimitives.WriteUInt32LittleEndian(fat[(sector * 4)..], next);
        }
    }

    private static void WriteDirectoryEntry(Span<byte> entry, string name, CompoundFileObjectType type,
        CompoundFileNodeColor color, uint left, uint right, uint child, uint start, ulong size)
    {
        int nameBytes = Encoding.Unicode.GetBytes(name, entry);
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
