using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using Pegasus.Core.Intake;

namespace Pegasus.IntegrationTests;

public sealed partial class EmailEvaluationWebTests
{
    private const string EvaluationMarker = "LOCAL_EMAIL_EVALUATION_MARKER_2031";

    [Fact]
    public async Task EvaluatesEmailThroughReaderAndPolicyWithoutPersistence()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var beforeArtifacts = ArtifactFiles(factory);
        var token = await GetAntiforgeryTokenAsync(client);
        Assert.Equal(beforeArtifacts, ArtifactFiles(factory));
        var source = Serialize(CreateMessage(
            "Synthetic local evaluation",
            $"QDOS instruction\r\n{EvaluationMarker}\r\nClaimant Name: Local Evaluation Claimant\r\nClaim Number: LOCAL-EVAL-001\r\nVehicle Registration: AB12 CDE\r\n<script>literal body text</script>"));
        var beforeReceipts = await ListReceiptsAsync(factory);

        using var response = await PostAsync(client, token, "local-evaluation.eml", source);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(EvaluationMarker, html, StringComparison.Ordinal);
        Assert.Contains("mimekit_pdfpig_openxml", html, StringComparison.Ordinal);
        Assert.Contains("mimekit-4.17.0;pdfpig-0.1.15;openxml-3.5.1", html, StringComparison.Ordinal);
        Assert.Contains("engineers@qdosassist.co.uk", html, StringComparison.Ordinal);
        Assert.Contains("qdos_instruction", html, StringComparison.Ordinal);
        Assert.Contains("Applicable", html, StringComparison.Ordinal);
        Assert.Contains("Local Evaluation Claimant", html, StringComparison.Ordinal);
        Assert.Contains("LOCAL-EVAL-001", html, StringComparison.Ordinal);
        Assert.Contains("AB12CDE", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;literal body text&lt;/script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Html.Raw", html, StringComparison.Ordinal);
        Assert.DoesNotContain(factory.ArtifactDirectory, html, StringComparison.OrdinalIgnoreCase);

        var afterReceipts = await ListReceiptsAsync(factory);
        Assert.Equal(beforeReceipts.Select(receipt => receipt.Id), afterReceipts.Select(receipt => receipt.Id));
        Assert.Equal(beforeArtifacts, ArtifactFiles(factory));
    }

    [Fact]
    public async Task ValidationAndMalformedEmailRemainTransient()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var token = await GetAntiforgeryTokenAsync(client);
        var cases = new[]
        {
            new EmailCase(null, null, "Choose an .eml email to evaluate."),
            new EmailCase("empty.eml", [], "The selected file is empty."),
            new EmailCase("not-an-email.txt", "content"u8.ToArray(), "The selected file must be an .eml email."),
            new EmailCase("oversized.eml", new byte[10 * 1024 * 1024 + 1], "The selected file must be 10 MB or smaller."),
            new EmailCase("malformed.eml", "not a MIME message"u8.ToArray(), "unreadable_email")
        };

        foreach (var testCase in cases)
        {
            var beforeReceipts = await ListReceiptsAsync(factory);
            var beforeArtifacts = ArtifactFiles(factory);
            using var response = await PostAsync(client, token, testCase.FileName, testCase.Bytes);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(testCase.ExpectedText, html, StringComparison.Ordinal);
            var afterReceipts = await ListReceiptsAsync(factory);
            Assert.Equal(beforeReceipts.Select(receipt => receipt.Id), afterReceipts.Select(receipt => receipt.Id));
            Assert.Equal(beforeArtifacts, ArtifactFiles(factory));
        }
    }

    [Fact]
    public async Task AcceptedBatchCreatesOneDeterministicReport()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var token = await GetAntiforgeryTokenAsync(client);
        var beforeArtifacts = ArtifactFiles(factory);
        var first = new EmailCase(
            "batch-a.eml",
            Serialize(CreateMessage(
                "Batch evaluation A",
                "QDOS instruction\r\nClaimant Name: Batch Claimant A\r\nClaim Number: BATCH-001\r\nVehicle Registration: AB12 CDE")),
            string.Empty);
        var second = new EmailCase(
            "batch-b.eml",
            Serialize(CreateMessage(
                "Batch evaluation B",
                "QDOS instruction\r\nClaimant Name: Batch Claimant B\r\nClaim Number: BATCH-002\r\nVehicle Registration: XY34 ZZZ")),
            string.Empty);

        using (var invalidResponse = await PostBatchAsync(
                   client,
                   token,
                   first,
                   new("empty.eml", [], string.Empty)))
        {
            var invalidHtml = await invalidResponse.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, invalidResponse.StatusCode);
            Assert.Contains("The selected file is empty.", invalidHtml, StringComparison.Ordinal);
            Assert.Equal(beforeArtifacts, ArtifactFiles(factory));
        }

        using var response = await PostBatchAsync(client, token, first, second);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Download deterministic JSON report", html, StringComparison.Ordinal);
        var afterArtifacts = ArtifactFiles(factory);
        var reportFile = Assert.Single(
            afterArtifacts.Except(beforeArtifacts, StringComparer.Ordinal));
        Assert.Equal(".json", Path.GetExtension(reportFile));
        Assert.Contains("\"total\": 2", await File.ReadAllTextAsync(reportFile), StringComparison.Ordinal);

        using var repeatedResponse = await PostBatchAsync(client, token, second, first);
        Assert.Equal(HttpStatusCode.OK, repeatedResponse.StatusCode);
        Assert.Equal(afterArtifacts, ArtifactFiles(factory));
    }

    private static async Task<IReadOnlyList<IntakeReceiptSummary>> ListReceiptsAsync(
        IntakeWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
        return await queries.ListAsync(null, CancellationToken.None);
    }

    private static string[] ArtifactFiles(IntakeWebApplicationFactory factory) =>
        Directory.Exists(factory.ArtifactDirectory)
            ? Directory.EnumerateFiles(factory.ArtifactDirectory, "*", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .ToArray()
            : [];

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/Intake/EmailEvaluation");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var tag = AntiforgeryTagRegex().Match(html);
        Assert.True(tag.Success, "The email evaluation page must render an antiforgery token.");
        var value = AntiforgeryValueRegex().Match(tag.Value);
        Assert.True(value.Success, "The antiforgery token must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    private static Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string antiforgeryToken,
        string? fileName,
        byte[]? bytes) =>
        PostBatchAsync(
            client,
            antiforgeryToken,
            new EmailCase(fileName, bytes, string.Empty));

    private static async Task<HttpResponseMessage> PostBatchAsync(
        HttpClient client,
        string antiforgeryToken,
        params EmailCase[] cases)
    {
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(antiforgeryToken), "__RequestVerificationToken");
        foreach (var testCase in cases)
        {
            if (testCase.FileName is not null && testCase.Bytes is not null)
            {
                var file = new ByteArrayContent(testCase.Bytes);
                file.Headers.ContentType = MediaTypeHeaderValue.Parse("message/rfc822");
                multipart.Add(file, "Upload", testCase.FileName);
            }
        }

        return await client.PostAsync("/Intake/EmailEvaluation", multipart);
    }

    private static MimeMessage CreateMessage(string subject, string body)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Synthetic QDOS Sender", "engineers@qdosassist.co.uk"));
        message.To.Add(new MailboxAddress("Synthetic Intake", "intake@example.test"));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };
        return message;
    }

    private static byte[] Serialize(MimeMessage message)
    {
        using var output = new MemoryStream();
        message.WriteTo(output);
        return output.ToArray();
    }

    private sealed record EmailCase(string? FileName, byte[]? Bytes, string ExpectedText);

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryTagRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryValueRegex();
}
