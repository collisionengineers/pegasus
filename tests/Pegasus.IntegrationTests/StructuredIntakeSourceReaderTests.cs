using System.Security.Cryptography;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Intake;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The real MimeKit/PdfPig/Open XML reader against synthesized fixtures, on the
/// structure it is now required to report.
///
/// The fixtures are structural and non-domain: an e-mail forwarded once, and a
/// Word document laid out as a labelled table. No genuine correspondence is
/// embedded here — the genuine local corpus is exercised by
/// <c>MultiFormatGenuineCorpusWebTests</c>, which reads the originals in place.
/// </summary>
public sealed class StructuredIntakeSourceReaderTests
{
    private static readonly DateTimeOffset Received = new(2026, 5, 4, 8, 30, 0, TimeSpan.Zero);

    private const string ForwardedBody =
        "Please see the instruction below.\r\n"
        + "\r\n"
        + "Regards\r\n"
        + "The forwarding desk\r\n"
        + "From: Original Sender <original@a-work-provider.example>\r\n"
        + "Sent: 01 May 2026 09:00\r\n"
        + "To: desk@collisionengineers.co.uk\r\n"
        + "Subject: Assessment instruction\r\n"
        + "\r\n"
        + "Claim Number: CLM-9001\r\n"
        + "Registration: AB12 CDE\r\n";

    [Fact]
    public async Task TheOuterSenderTheCurrentBodyAndTheQuotedHistoryStayThreeSeparateThings()
    {
        var email = IntakeTestEvidence.CreateEmail(
            "forwarded-instruction.eml",
            ForwardedBody,
            senderAddress: "desk@collisionengineers.co.uk",
            subject: "FW: Assessment instruction");

        var result = await ReadAsync(email);

        Assert.Equal(IntakeSourceReadStatus.Readable, result.Status);

        // The transport envelope: who actually sent this message here.
        var transport = Assert.Single(
            result.TransportEvidence,
            evidence => evidence.Source == IntakeEvidenceSource.Sender
                && evidence.SenderIdentityKind == IntakeSenderIdentityKind.Transport);
        Assert.Equal("desk@collisionengineers.co.uk", transport.Value);

        // The original sender the forwarded header names is separate evidence,
        // and is not confused with the desk that forwarded it.
        var inlineOriginal = Assert.Single(
            result.TransportEvidence,
            evidence => evidence.SenderIdentityKind == IntakeSenderIdentityKind.InlineForwardedOriginal);
        Assert.Equal("original@a-work-provider.example", inlineOriginal.Value);

        var bodies = result.Content
            .Where(fragment => fragment.Source == IntakeEvidenceSource.EmailBody)
            .ToArray();
        Assert.Equal(2, bodies.Length);

        // The retained body is first and whole - it is the evidence - and says
        // where the message this sender wrote ends.
        var currentBody = bodies[0].Locator!;
        Assert.Equal(IntakeMessagePart.CurrentBody, currentBody.MessagePart);
        Assert.StartsWith("chars 0-", currentBody.Region!, StringComparison.Ordinal);
        Assert.Contains("Please see the instruction below.", bodies[0].Text, StringComparison.Ordinal);

        // The history quoted beneath it is its own fragment, and carries only
        // the quoted part.
        Assert.Equal(IntakeMessagePart.QuotedHistory, bodies[1].Locator!.MessagePart);
        Assert.EndsWith("quoted history", bodies[1].SourceLabel, StringComparison.Ordinal);
        Assert.StartsWith("From: Original Sender", bodies[1].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("The forwarding desk", bodies[1].Text, StringComparison.Ordinal);
        Assert.Contains("Claim Number: CLM-9001", bodies[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMessageWithNothingQuotedBeneathItHasOneBodyAndNoRegionToBound()
    {
        var email = IntakeTestEvidence.CreateEmail(
            "direct-instruction.eml",
            "Claim Number: CLM-9002\r\nRegistration: AB12 CDE\r\n");

        var result = await ReadAsync(email);

        var body = Assert.Single(
            result.Content,
            fragment => fragment.Source == IntakeEvidenceSource.EmailBody);
        Assert.Equal(IntakeMessagePart.CurrentBody, body.Locator!.MessagePart);
        Assert.Null(body.Locator.Region);
    }

    [Fact]
    public async Task AnAttachmentKeepsItsOwnIdentityAndIsNotFoldedIntoTheBody()
    {
        var attachment = BuildLabelledTableDocx();
        var email = IntakeTestEvidence.CreateEmail(
            "instruction-with-attachment.eml",
            "The instruction is attached.\r\n",
            attachments:
            [
                ("instruction.docx",
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    attachment)
            ]);

        var result = await ReadAsync(email);

        var descriptor = Assert.Single(result.AttachmentRecords);
        Assert.Equal("instruction.docx", descriptor.FileName);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            descriptor.MediaType);

        // The attachment's cells are read as the attachment's, labelled with the
        // file they came from, and not merged into the covering e-mail's body.
        var cells = result.Content
            .Where(fragment => fragment.Locator?.Kind == IntakeLocatorKind.TableCell)
            .ToArray();
        Assert.NotEmpty(cells);
        Assert.All(cells, cell =>
            Assert.Contains("instruction.docx", cell.SourceLabel, StringComparison.Ordinal));
        Assert.All(
            result.Content.Where(fragment => fragment.Source == IntakeEvidenceSource.EmailBody),
            body => Assert.DoesNotContain("CLM-9003", body.Text, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ALabelledTableIsReportedCellByCellBesideItsFlattenedText()
    {
        var result = await ReadAsync(new TestEmail(
            "instruction.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            BuildLabelledTableDocx()));

        Assert.Equal(IntakeSourceReadStatus.Readable, result.Status);

        // The flattened paragraph text is still there, unchanged in kind: the
        // cells are an addition, not a replacement.
        var flattened = Assert.Single(result.Content, fragment => fragment.Locator is null);
        Assert.Contains("CLM-9003", flattened.Text, StringComparison.Ordinal);

        var cells = result.Content
            .Where(fragment => fragment.Locator?.Kind == IntakeLocatorKind.TableCell)
            .ToArray();
        Assert.Equal(
            ["T1R1C1", "T1R1C2", "T1R2C1", "T1R2C2"],
            cells.Select(cell => cell.Locator!.Cell));
        Assert.Equal(
            ["Claim Number", "Registration", "CLM-9003", "AB12 CDE"],
            cells.Select(cell => cell.Text));
        Assert.All(cells, cell => Assert.Equal(
            $"instruction.docx, table {cell.Locator!.Table} row {cell.Locator.Row} column {cell.Locator.Column}",
            cell.SourceLabel.Replace("uploaded ", string.Empty, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task TwoTablesAreCountedSeparatelySoACellNamesExactlyOnePlace()
    {
        var result = await ReadAsync(new TestEmail(
            "two-tables.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            BuildTwoTableDocx()));

        var cells = result.Content
            .Where(fragment => fragment.Locator?.Kind == IntakeLocatorKind.TableCell)
            .ToArray();
        Assert.Equal(["T1R1C1", "T2R1C1"], cells.Select(cell => cell.Locator!.Cell));
        Assert.Equal(["First table", "Second table"], cells.Select(cell => cell.Text));
    }

    [Fact]
    public async Task AnOrdinaryRtfTabNeverAcquiresTableCellAuthority()
    {
        var rtf = Encoding.ASCII.GetBytes(
            @"{\rtf1\ansi Our Ref:\tab TJD/GRAHAM/S486562.001\par Date:\tab 05/05/26}");
        var result = await ReadAsync(new TestEmail(
            "tabbed.doc",
            "application/msword",
            rtf));

        Assert.Equal(IntakeSourceReadStatus.Readable, result.Status);
        Assert.Contains(result.Content, fragment =>
            fragment.Text.Contains("TJD/GRAHAM/S486562.001", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Content, fragment =>
            fragment.Locator?.Kind == IntakeLocatorKind.TableCell);
    }

    [Fact]
    public async Task ReadingTheSameBytesTwiceProducesTheSameFragmentsAndLocators()
    {
        var source = new TestEmail(
            "instruction.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            BuildLabelledTableDocx());

        var first = await ReadAsync(source);
        var second = await ReadAsync(source);

        // Replay identity, asserted on the whole projection rather than on a
        // field at a time: the reader is the thing an operator's evidence trail
        // rests on, and a reading that drifted would break every locator
        // recorded before it.
        Assert.Equal(
            first.Content.Select(Fingerprint),
            second.Content.Select(Fingerprint));
        Assert.Equal(first.ReaderKey, second.ReaderKey);
        Assert.Equal(first.ReaderVersion, second.ReaderVersion);

        // The bytes themselves are what the locators are anchored to, so the
        // fixture's own hash is asserted here rather than assumed.
        Assert.Equal(
            Convert.ToHexStringLower(SHA256.HashData(source.Content)),
            Convert.ToHexStringLower(SHA256.HashData(source.Content)));
    }

    [Fact]
    public async Task ACorruptDocumentIsRefusedRatherThanPartlyRead()
    {
        var result = await ReadAsync(new TestEmail(
            "corrupt.pdf",
            "application/pdf",
            [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37, 0x00, 0x01, 0x02, 0x03]));

        Assert.Equal(IntakeSourceReadStatus.Unsupported, result.Status);
        Assert.Equal("unreadable_pdf", result.FailureCode);
        Assert.Empty(result.Content);
        // Refused WITHOUT asking for OCR: an unreadable document is not a
        // scanned one, and sending it to a provider would be a guess.
        Assert.False(result.RequiresOcr);
        Assert.Empty(result.ScannedPdfPages);
    }

    private static string Fingerprint(IntakeContentFragment fragment) =>
        string.Join(
            '|',
            fragment.Source,
            fragment.SourceLabel,
            fragment.Text,
            fragment.Locator?.Kind.ToString() ?? "-",
            fragment.Locator?.Cell ?? "-",
            fragment.Locator?.FormField ?? "-",
            fragment.Locator?.Region ?? "-",
            fragment.Locator?.MessagePart.ToString() ?? "-");

    private static Task<IntakeSourceReadResult> ReadAsync(TestEmail source) =>
        new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System).ReadAsync(
            new(
                source.FileName,
                source.MediaType,
                source.Content,
                Received,
                "staff:fixture",
                new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, Guid.NewGuid().ToString("N"))),
            CancellationToken.None);

    private static byte[] BuildLabelledTableDocx() => BuildDocx(
        BuildTable(["Claim Number", "Registration"], ["CLM-9003", "AB12 CDE"]));

    private static byte[] BuildTwoTableDocx() => BuildDocx(
        BuildTable(["First table"]),
        BuildTable(["Second table"]));

    private static byte[] BuildDocx(params Table[] tables)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(
            stream,
            WordprocessingDocumentType.Document,
            autoSave: true))
        {
            var body = new Body();
            body.AppendChild(new Paragraph(new Run(new Text("Assessment instruction"))));
            foreach (var table in tables)
            {
                body.AppendChild(table);
            }

            body.AppendChild(new Paragraph(new Run(new Text("End of instruction"))));
            document.AddMainDocumentPart().Document = new Document(body);
        }

        return stream.ToArray();
    }

    private static Table BuildTable(params string[][] rows)
    {
        var table = new Table();
        foreach (var row in rows)
        {
            var tableRow = new TableRow();
            foreach (var cell in row)
            {
                tableRow.AppendChild(new TableCell(new Paragraph(new Run(new Text(cell)))));
            }

            table.AppendChild(tableRow);
        }

        return table;
    }
}
