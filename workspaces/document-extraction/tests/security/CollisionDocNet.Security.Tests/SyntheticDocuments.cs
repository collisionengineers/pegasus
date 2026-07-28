using System.IO.Compression;
using System.Text;

namespace CollisionDocNet.Security.Tests;

internal static class SyntheticDocuments
{
    internal static byte[] Eml(string body, string? contentType = "text/html") => Encoding.ASCII.GetBytes(
        $"From: sender@example.test\r\nTo: receiver@example.test\r\nSubject: hostile regression\r\n" +
        $"Content-Type: {contentType}\r\n\r\n{body}\r\n");

    internal static byte[] MultipartEml(string boundary, string attachmentName, string attachmentBody) =>
        Encoding.ASCII.GetBytes(
            $"From: sender@example.test\r\nTo: receiver@example.test\r\nSubject: passive content\r\n" +
            $"Content-Type: multipart/mixed; boundary=\"{boundary}\"\r\n\r\n" +
            $"--{boundary}\r\nContent-Type: text/plain\r\n\r\nreview text\r\n" +
            $"--{boundary}\r\nContent-Type: application/octet-stream\r\n" +
            $"Content-Disposition: attachment; filename=\"{attachmentName}\"\r\n" +
            "Content-Transfer-Encoding: base64\r\n\r\n" +
            Convert.ToBase64String(Encoding.ASCII.GetBytes(attachmentBody)) + "\r\n" +
            $"--{boundary}--\r\n");

    internal static byte[] Docx(
        string documentXml,
        IEnumerable<(string Name, string Content)>? extraEntries = null,
        string? documentRelationships = null)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml",
                "<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Default Extension=\"bin\" ContentType=\"application/vnd.ms-office.vbaProject\"/>" +
                "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
                "</Types>");
            Write(archive, "_rels/.rels",
                "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
                "</Relationships>");
            Write(archive, "word/document.xml", documentXml);
            if (documentRelationships is not null)
            {
                Write(archive, "word/_rels/document.xml.rels", documentRelationships);
            }

            if (extraEntries is not null)
            {
                foreach ((string name, string content) in extraEntries)
                {
                    Write(archive, name, content);
                }
            }
        }

        return stream.ToArray();
    }

    internal static byte[] MinimalDocx(string text = "evidence") => Docx(
        "<?xml version=\"1.0\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">" +
        $"<w:body><w:p><w:r><w:t>{text}</w:t></w:r></w:p></w:body></w:document>");

    internal static byte[] Pdf(string content, string? catalogueExtras = null, string? additionalObject = null)
    {
        var bytes = new List<byte>();
        var offsets = new List<int> { 0 };
        Add(bytes, "%PDF-2.0\n");
        AddObject(bytes, offsets, $"<< /Type /Catalog /Pages 2 0 R {catalogueExtras} >>");
        AddObject(bytes, offsets, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        AddObject(bytes, offsets, "<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>");
        int streamNumber = offsets.Count;
        offsets.Add(bytes.Count);
        Add(bytes, $"{streamNumber} 0 obj\n<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream\nendobj\n");
        AddObject(bytes, offsets, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        if (additionalObject is not null)
        {
            AddObject(bytes, offsets, additionalObject);
        }

        int xref = bytes.Count;
        Add(bytes, $"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        for (int index = 1; index < offsets.Count; index++)
        {
            Add(bytes, $"{offsets[index]:D10} 00000 n \n");
        }

        Add(bytes, $"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return bytes.ToArray();
    }

    internal static byte[] CompoundSignature(string payload)
    {
        byte[] bytes = new byte[512];
        byte[] signature = [0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1];
        signature.CopyTo(bytes, 0);
        byte[] encodedPayload = Encoding.Unicode.GetBytes(payload);
        encodedPayload.AsSpan(0, Math.Min(encodedPayload.Length, 128)).CopyTo(bytes.AsSpan(64));
        return bytes;
    }

    private static void Write(ZipArchive archive, string name, string text)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.SmallestSize);
        using Stream output = entry.Open();
        output.Write(Encoding.UTF8.GetBytes(text));
    }

    private static void AddObject(List<byte> bytes, List<int> offsets, string body)
    {
        int number = offsets.Count;
        offsets.Add(bytes.Count);
        Add(bytes, $"{number} 0 obj\n{body}\nendobj\n");
    }

    private static void Add(List<byte> bytes, string text) => bytes.AddRange(Encoding.Latin1.GetBytes(text));
}
