using System.IO.Compression;
using System.Buffers.Binary;
using System.Text;

namespace CollisionDocNet.Writer.OpenXml.Tests;

internal static class DocxFixture
{
    internal const string TransitionalWord = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    internal const string StrictWord = "http://purl.oclc.org/ooxml/wordprocessingml/main";
    internal const string PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    internal const string OfficeRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    internal const string StrictOfficeRelationships = "http://purl.oclc.org/ooxml/officeDocument/relationships";

    internal static byte[] Create(
        string body,
        bool strict = false,
        bool includeHeader = false,
        bool externalLink = false,
        bool passiveAssets = false,
        bool mce = false,
        bool dependencyParts = false,
        bool macroEnabled = false)
    {
        string word = strict ? StrictWord : TransitionalWord;
        string officeRelationships = strict ? StrictOfficeRelationships : OfficeRelationships;
        string mainType = macroEnabled
            ? "application/vnd.ms-word.document.macroEnabled.main+xml"
            : "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";
        var entries = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["[Content_Types].xml"] = ContentTypes(mainType, includeHeader, passiveAssets, dependencyParts, macroEnabled),
            ["_rels/.rels"] = $"<Relationships xmlns=\"{PackageRelationships}\"><Relationship Id=\"rId1\" Type=\"{officeRelationships}/officeDocument\" Target=\"word/document.xml\"/><Relationship Id=\"rIdProps\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/></Relationships>",
            ["word/document.xml"] = $"<w:document xmlns:w=\"{word}\"{(mce ? " xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\"" : string.Empty)}><w:body>{body}</w:body></w:document>",
            ["docProps/core.xml"] = "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\"><dc:title>Synthetic title</dc:title></cp:coreProperties>",
        };

        var documentRelationships = new List<string>();
        if (includeHeader)
        {
            entries["word/header1.xml"] = $"<w:hdr xmlns:w=\"{word}\"><w:p><w:r><w:t>Header text</w:t></w:r></w:p></w:hdr>";
            documentRelationships.Add($"<Relationship Id=\"rIdHeader\" Type=\"{officeRelationships}/header\" Target=\"header1.xml\"/>");
        }

        if (externalLink)
        {
            documentRelationships.Add($"<Relationship Id=\"rIdExternal\" Type=\"{officeRelationships}/hyperlink\" Target=\"https://example.invalid/evidence\" TargetMode=\"External\"/>");
        }

        if (passiveAssets)
        {
            entries["word/media/image1.png"] = "synthetic-image";
            entries["word/embeddings/embedded1.bin"] = "synthetic-embedding";
            entries["customXml/item1.xml"] = "<root>custom evidence</root>";
            documentRelationships.Add($"<Relationship Id=\"rIdImage\" Type=\"{officeRelationships}/image\" Target=\"media/image1.png\"/>");
            documentRelationships.Add($"<Relationship Id=\"rIdPackage\" Type=\"{officeRelationships}/package\" Target=\"embeddings/embedded1.bin\"/>");
            documentRelationships.Add($"<Relationship Id=\"rIdCustom\" Type=\"{officeRelationships}/customXml\" Target=\"../customXml/item1.xml\"/>");
        }

        if (macroEnabled)
        {
            entries["word/vbaProject.bin"] = "synthetic-macro";
            documentRelationships.Add($"<Relationship Id=\"rIdMacro\" Type=\"{officeRelationships}/vbaProject\" Target=\"vbaProject.bin\"/>");
        }

        if (dependencyParts)
        {
            entries["word/styles.xml"] = $"<w:styles xmlns:w=\"{word}\"><w:style w:styleId=\"Normal\"/></w:styles>";
            entries["word/numbering.xml"] = $"<w:numbering xmlns:w=\"{word}\"><w:abstractNum w:abstractNumId=\"0\"/></w:numbering>";
            documentRelationships.Add($"<Relationship Id=\"rIdStyles\" Type=\"{officeRelationships}/styles\" Target=\"styles.xml\"/>");
            documentRelationships.Add($"<Relationship Id=\"rIdNumbering\" Type=\"{officeRelationships}/numbering\" Target=\"numbering.xml\"/>");
        }

        if (documentRelationships.Count != 0)
        {
            entries["word/_rels/document.xml.rels"] = $"<Relationships xmlns=\"{PackageRelationships}\">{string.Concat(documentRelationships)}</Relationships>";
        }

        return Zip(entries);
    }

    internal static byte[] Zip(IReadOnlyDictionary<string, string> entries)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, string content) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                using Stream stream = entry.Open();
                byte[] bytes = Encoding.UTF8.GetBytes(content);
                stream.Write(bytes);
            }
        }

        return output.ToArray();
    }

    internal static byte[] Rewrite(byte[] source, Action<Dictionary<string, string>> change)
    {
        using var input = new MemoryStream(source);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read);
        var entries = archive.Entries.ToDictionary(
            static entry => entry.FullName,
            static entry =>
            {
                using Stream stream = entry.Open();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                return reader.ReadToEnd();
            }, StringComparer.Ordinal);
        change(entries);
        return Zip(entries);
    }

    internal static byte[] EncryptedCompoundWrapper()
    {
        const uint free = 0xffffffff;
        const uint end = 0xfffffffe;
        const uint fatSector = 0xfffffffd;
        byte[] bytes = new byte[3 * 512];
        new byte[] { 0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1 }.CopyTo(bytes, 0);
        U16(bytes, 24, 0x003e);
        U16(bytes, 26, 3);
        U16(bytes, 28, 0xfffe);
        U16(bytes, 30, 9);
        U16(bytes, 32, 6);
        U32(bytes, 44, 1);
        U32(bytes, 48, 1);
        U32(bytes, 56, 4096);
        U32(bytes, 60, end);
        U32(bytes, 68, end);
        for (int index = 0; index < 109; index++) U32(bytes, 76 + (index * 4), free);
        U32(bytes, 76, 0);
        Span<byte> fat = bytes.AsSpan(512, 512);
        for (int offset = 0; offset < fat.Length; offset += 4) BinaryPrimitives.WriteUInt32LittleEndian(fat[offset..], free);
        BinaryPrimitives.WriteUInt32LittleEndian(fat, fatSector);
        BinaryPrimitives.WriteUInt32LittleEndian(fat[4..], end);

        Span<byte> directory = bytes.AsSpan(1024, 512);
        DirectoryEntry(directory[..128], "Root Entry", 5, 1, 0xffffffff);
        DirectoryEntry(directory.Slice(128, 128), "EncryptionInfo", 2, 0xffffffff, 2);
        DirectoryEntry(directory.Slice(256, 128), "EncryptedPackage", 2, 0xffffffff, 0xffffffff);
        for (int stream = 3; stream < 4; stream++)
        {
            Span<byte> entry = directory.Slice(stream * 128, 128);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[68..], 0xffffffff);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[72..], 0xffffffff);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[76..], 0xffffffff);
        }
        return bytes;
    }

    private static void DirectoryEntry(Span<byte> entry, string name, byte type, uint child, uint right)
    {
        int count = Encoding.Unicode.GetBytes(name, entry);
        BinaryPrimitives.WriteUInt16LittleEndian(entry[64..], checked((ushort)(count + 2)));
        entry[66] = type;
        entry[67] = type == 5 || name == "EncryptionInfo" ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt32LittleEndian(entry[68..], 0xffffffff);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[72..], right);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[76..], child);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[116..], 0xfffffffe);
    }

    private static void U16(Span<byte> bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes[offset..], value);
    private static void U32(Span<byte> bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes[offset..], value);

    private static string ContentTypes(string mainType, bool header, bool assets, bool dependencies, bool macro) =>
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
        "<Default Extension=\"png\" ContentType=\"image/png\"/>" +
        "<Default Extension=\"bin\" ContentType=\"application/octet-stream\"/>" +
        $"<Override PartName=\"/word/document.xml\" ContentType=\"{mainType}\"/>" +
        "<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>" +
        (header ? "<Override PartName=\"/word/header1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml\"/>" : string.Empty) +
        (dependencies ? "<Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/><Override PartName=\"/word/numbering.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml\"/>" : string.Empty) +
        (macro ? "<Override PartName=\"/word/vbaProject.bin\" ContentType=\"application/vnd.ms-office.vbaProject\"/>" : string.Empty) +
        "</Types>";
}
