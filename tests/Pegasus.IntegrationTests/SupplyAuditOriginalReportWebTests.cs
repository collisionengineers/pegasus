using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.IntegrationTests.Support;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Audit that waits in Unidentified until its original report is supplied
/// (operator decision, 2026-09-11), exercised through the real host: the
/// instruction-only e-mail registers the AuditOriginalReportMissing item, the
/// page offers the Add-original-report form, and the staff-supplied report
/// rides the real retention, attachment, re-evaluation, allocation and
/// resolution pipeline — the supplied bytes read back through the retained
/// logical source, exactly as the Worker would read them.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class SupplyAuditOriginalReportWebTests
{
    private const string InstructionText =
        "AUDIT REPORT NOTIFICATION\nClaimant Name: Audit Claimant\nClaim Number: A-2026/77";

    private const string ReportFileName = "total-loss-report.pdf";

    [Fact]
    public async Task SupplyingTheOriginalReportResolvesTheWaitingAudit()
    {
        var retainedReader = new RecordingLogicalDocumentVersionReader();
        using var factory = new IntakeWebApplicationFactory();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<Pegasus.Core.Documents.IReadLogicalDocumentVersion>(retainedReader)));
        using var client = host.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        // 1. The Audit instruction arrives without the original report it
        //    audits: it waits in Unidentified under its own reason.
        var eml = SerializeAuditEmail();
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            host,
            client,
            "audit-missing-report.eml",
            "message/rfc822",
            eml,
            Guid.NewGuid().ToString("N"));
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receiptQueries = services.GetRequiredService<IIntakeReceiptQueries>();
        var receipt = await receiptQueries.GetAsync(receiptId, CancellationToken.None);
        Assert.NotNull(receipt);
        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        var classification = Assert.IsType<MailClassificationResult>(receipt.MailClassificationDecision);
        Assert.Equal(CaseType.Audit, classification.CaseType);
        Assert.Null(classification.StandaloneAuditReport);

        var store = services.GetRequiredService<IUnidentifiedStore>();
        var item = await store.GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId));
        Assert.NotNull(item);
        Assert.Equal(UnidentifiedReasonCode.AuditOriginalReportMissing, item.ReasonCode);
        Assert.Equal(UnidentifiedState.Open, item.State);

        // 2. The page shows the reason, the stored safe detail, and the one
        //    Add-original-report form — no resolution dialog.
        var page = await IntakeWebDriver.GetHtmlAsync(client, $"/Unidentified/{item.Id:D}");
        Assert.Contains("Audit is missing the original report", page, StringComparison.Ordinal);
        Assert.Contains(
            "The Audit instruction arrived without the original report it audits.",
            page,
            StringComparison.Ordinal);
        Assert.Contains("Add original report", page, StringComparison.Ordinal);
        Assert.Contains("enctype=\"multipart/form-data\"", page, StringComparison.Ordinal);
        // The resolve dialog is gone; the close form is disclosed inline.
        Assert.DoesNotContain("unidentified-resolve-dialog", page, StringComparison.Ordinal);

        // 3. The operator supplies the report through the real form.
        var formToken = AntiforgeryToken(page);
        var expectedVersion = InputValue(page, "ExpectedVersion");
        var supplyKey = InputValue(page, "SupplyOperationKey");
        var reportPdf = CreateTextPdf(["The vehicle is a total loss."]);

        using (var first = await PostSupplyAsync(
            client, item.Id, formToken, expectedVersion, supplyKey, reportPdf))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            var rendered = await first.Content.ReadAsStringAsync(CancellationToken.None);
            Assert.Contains(
                "Original report added. The Audit Case is being created; refresh in a moment.",
                rendered,
                StringComparison.Ordinal);
        }

        // 4. The report is retained as the receipt's single supplied asset and
        //    the re-evaluation is queued beside it.
        var supplied = await receiptQueries.GetAsync(receiptId, CancellationToken.None);
        var suppliedAsset = Assert.Single(supplied!.AssetRecords, asset =>
            asset.Disposition == IntakeAssetDisposition.SuppliedOriginalReport);
        Assert.Equal(ReportFileName, suppliedAsset.FileName);
        Assert.Equal(IntakeDecision.BlockedIntake, supplied.Decision);
        Assert.Equal("reevaluation_pending", supplied.FailureCode);

        // 5. A replayed POST with the same operation key is a no-op: the
        //    command replays through the mutation envelope, nothing is
        //    double-attached, and the page still reports success.
        using (var replay = await PostSupplyAsync(
            client, item.Id, formToken, expectedVersion, supplyKey, reportPdf))
        {
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            var rendered = await replay.Content.ReadAsStringAsync(CancellationToken.None);
            Assert.Contains(
                "Original report added. The Audit Case is being created; refresh in a moment.",
                rendered,
                StringComparison.Ordinal);
            Assert.DoesNotContain("already has a supplied original report", rendered, StringComparison.Ordinal);
        }

        var afterReplay = await receiptQueries.GetAsync(receiptId, CancellationToken.None);
        Assert.Single(afterReplay!.AssetRecords, asset =>
            asset.Disposition == IntakeAssetDisposition.SuppliedOriginalReport);

        // 6. The queued pass re-reads the retained source — the exact bytes
        //    the host retained, served by identity, hash and length.
        var source = IntakeFileIdentity.SourceAsset(supplied)!;
        retainedReader.Serve(new(
            source.Id,
            supplied.CurrentCaseId,
            supplied.Id,
            source.ContentHash,
            source.FileName,
            source.MediaType,
            eml));

        var dispatcher = new DispatchPendingIntakeWork(
            services.GetRequiredService<IIntakeWorkStore>(),
            new IntakeWebDriver.ImmediateIntakeWorkEnqueuer(
                IntakeWebDriver.CreateProcessor(services)),
            services.GetRequiredService<TimeProvider>());
        Assert.Equal(1, await dispatcher.ExecuteAsync(1, CancellationToken.None));

        // 7. The Audit allocated: the supplied report's literal outcome
        //    derived the ap. reference and the automatic evidence.
        var allocated = await receiptQueries.GetAsync(receiptId, CancellationToken.None);
        Assert.NotNull(allocated);
        Assert.Equal(IntakeDecision.CaseCreated, allocated.Decision);
        Assert.StartsWith("ap.", allocated.AcceptedCaseReference, StringComparison.Ordinal);
        var evidence = await services.GetRequiredService<IStandaloneAuditEvidenceQueries>()
            .GetForReceiptAsync(receiptId, CancellationToken.None);
        Assert.NotNull(evidence);
        Assert.Equal(AuditAssessment.TotalLoss, evidence.Assessment);
        Assert.Equal(suppliedAsset.Id, evidence.OriginalReportAssetId);

        // 8. The item resolved itself to the Case it created, with both the
        //    registration and the supplied-report entries on its history.
        var resolved = await store.GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId));
        Assert.NotNull(resolved);
        Assert.Equal(UnidentifiedState.Resolved, resolved.State);
        Assert.Equal(UnidentifiedResolutionTargetKind.InstructionCase, resolved.ResolutionTargetKind);
        Assert.Equal(allocated.AcceptedCaseId, Guid.Parse(resolved.ResolutionTargetId!));
        var history = await store.HistoryAsync(resolved.Id);
        Assert.Contains(history, entry => entry.Reason.StartsWith("Original report added:", StringComparison.Ordinal));
        Assert.Contains(history, entry => entry.NewState == UnidentifiedState.Resolved);

        // 9. The Unidentified tab no longer lists the item.
        var staffClient = client;
        var tab = await IntakeWebDriver.GetHtmlAsync(staffClient, "/Cases?tab=unidentified");
        Assert.DoesNotContain($">{resolved.Reference}<", tab, StringComparison.Ordinal);
    }

    private static async Task<HttpResponseMessage> PostSupplyAsync(
        HttpClient client,
        Guid itemId,
        string antiforgeryToken,
        string expectedVersion,
        string operationKey,
        byte[] reportPdf)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(antiforgeryToken), "__RequestVerificationToken");
        form.Add(new StringContent(expectedVersion), "ExpectedVersion");
        form.Add(new StringContent(operationKey), "SupplyOperationKey");
        var file = new ByteArrayContent(reportPdf);
        file.Headers.ContentType = new("application/pdf");
        form.Add(file, "Report", ReportFileName);
        return await client.PostAsync(
            $"/Unidentified/{itemId:D}?handler=SupplyOriginalReport",
            form);
    }

    private static byte[] SerializeAuditEmail()
    {
        var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress("QDOS", "instructions@qdosassist.co.uk"));
        message.To.Add(new MimeKit.MailboxAddress("Pegasus Intake", "intake@example.test"));
        message.Subject = "AUDIT REPORT NOTIFICATION";
        var multipart = new MimeKit.Multipart("mixed")
        {
            new MimeKit.TextPart("plain") { Text = "Please audit the attached engineer's report." },
            new MimeKit.MimePart("application/pdf")
            {
                Content = new MimeKit.MimeContent(new MemoryStream(CreateTextPdf([InstructionText]), writable: false)),
                ContentDisposition = new MimeKit.ContentDisposition(MimeKit.ContentDisposition.Attachment),
                ContentTransferEncoding = MimeKit.ContentEncoding.Base64,
                FileName = "audit-instructions.pdf"
            }
        };
        message.Body = multipart;
        using var output = new MemoryStream();
        message.WriteTo(output);
        return output.ToArray();
    }

    /// <summary>
    /// A minimal single-page PDF whose readable text is <paramref name="lines"/>,
    /// shaped like the other tests' synthetic PDFs.
    /// </summary>
    private static byte[] CreateTextPdf(string[] lines)
    {
        var content = new StringBuilder("BT /F1 12 Tf 72 720 Td\n");
        foreach (var line in lines)
        {
            var escaped = line.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("(", "\\(", StringComparison.Ordinal)
                .Replace(")", "\\)", StringComparison.Ordinal);
            content.Append('(').Append(escaped).Append(") Tj\n0 -14 Td\n");
        }
        content.Append("ET");
        var operators = Encoding.ASCII.GetBytes(content.ToString());
        var bodies = new List<byte[]>
        {
            Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
            Encoding.ASCII.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Encoding.ASCII.GetBytes(
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] "
                + "/Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>"),
            Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"),
            Encoding.ASCII.GetBytes($"<< /Length {operators.Length} >>\nstream\n")
                .Concat(operators)
                .Concat(Encoding.ASCII.GetBytes("\nendstream"))
                .ToArray()
        };

        using var output = new MemoryStream();
        output.Write(Encoding.ASCII.GetBytes("%PDF-1.4\n"));
        var offsets = new List<int>();
        foreach (var (body, number) in bodies.Select((body, index) => (body, index + 1)))
        {
            offsets.Add((int)output.Length);
            output.Write(Encoding.ASCII.GetBytes($"{number} 0 obj\n"));
            output.Write(body);
            output.Write(Encoding.ASCII.GetBytes("\nendobj\n"));
        }
        var xrefPosition = (int)output.Length;
        output.Write(Encoding.ASCII.GetBytes($"xref\n0 {bodies.Count + 1}\n0000000000 65535 f \n"));
        foreach (var offset in offsets)
        {
            output.Write(Encoding.ASCII.GetBytes($"{offset:D10} 00000 n \n"));
        }
        output.Write(Encoding.ASCII.GetBytes(
            $"trailer\n<< /Size {bodies.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefPosition}\n%%EOF\n"));
        return output.ToArray();
    }

    private static string AntiforgeryToken(string html) =>
        InputValue(html, "__RequestVerificationToken");

    private static string InputValue(string html, string name)
    {
        var match = Regex.Matches(
                html,
                "<input[^>]*name=\"(?<name>[^\"]+)\"[^>]*value=\"(?<value>[^\"]*)\"[^>]*/?>",
                RegexOptions.IgnoreCase)
            .Cast<Match>()
            .FirstOrDefault(candidate => string.Equals(
                HttpUtility.HtmlDecode(candidate.Groups["name"].Value),
                name,
                StringComparison.Ordinal))
            ?? Regex.Matches(
                    html,
                    "<input[^>]*value=\"(?<value>[^\"]*)\"[^>]*name=\"(?<name>[^\"]+)\"[^>]*/?>",
                    RegexOptions.IgnoreCase)
                .Cast<Match>()
                .FirstOrDefault(candidate => string.Equals(
                    HttpUtility.HtmlDecode(candidate.Groups["name"].Value),
                    name,
                    StringComparison.Ordinal));
        Assert.True(match is not null, $"The page must render input '{name}'.");
        return HttpUtility.HtmlDecode(match!.Groups["value"].Value);
    }
}
