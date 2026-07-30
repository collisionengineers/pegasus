using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using Pegasus.Core.Intake;

namespace Pegasus.IntegrationTests;

[Collection(LocalDbFixtureDefinition.Name)]
public sealed partial class OfflineAcceptanceTests
{
    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task ActualOfflineCallerProducesImmutableDeterministicEvidenceWithoutActivation()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var token = await GetAntiforgeryTokenAsync(client);
        var first = CreateEmail(
            "qdos-a.eml",
            "QDOS instruction\r\nClaimant Name: Alpha Claimant\r\nClaim Number: ALPHA-001\r\nVehicle Registration: AB12 CDE");
        var second = CreateEmail(
            "qdos-b.eml",
            "QDOS instruction\r\nClaimant Name: Beta Claimant\r\nClaim Number: ALPHA-002\r\nVehicle Registration: XY34 ZZZ");

        using var response = await PostBatchAsync(client, token, first, second);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Activation blocked", html, StringComparison.Ordinal);
        Assert.Contains("Download deterministic JSON report", html, StringComparison.Ordinal);
        var reportPath = Assert.Single(ReportFiles(factory));
        var originalBytes = await File.ReadAllBytesAsync(reportPath);
        var originalHash = Convert.ToHexString(SHA256.HashData(originalBytes));
        using (var report = JsonDocument.Parse(originalBytes))
        {
            var root = report.RootElement;
            Assert.Equal("local-email-evaluation-report-v1", root.GetProperty("schemaVersion").GetString());
            Assert.False(root.GetProperty("activationAllowed").GetBoolean());
            Assert.Equal("not-provided", root.GetProperty("approvalEvidenceStatus").GetString());
            Assert.Equal(2, root.GetProperty("summary").GetProperty("total").GetInt32());
            Assert.Equal(2, root.GetProperty("summary").GetProperty("acceptedRoutes").GetInt32());
            Assert.Equal(2, root.GetProperty("summary").GetProperty("applicableInstructions").GetInt32());
            Assert.Contains(
                root.GetProperty("blockingReasons").EnumerateArray().Select(item => item.GetString()),
                reason => reason is not null && reason.Contains("cannot create cases", StringComparison.Ordinal));
            Assert.All(root.GetProperty("items").EnumerateArray(), item =>
            {
                Assert.Equal("Accepted", item.GetProperty("routeDisposition").GetString());
                Assert.Equal("Applicable", item.GetProperty("extractionApplicability").GetString());
                Assert.StartsWith("local-email-evaluation:", item.GetProperty("replayIdentity").GetString(), StringComparison.Ordinal);
            });
        }

        await AssertNoDurableIntakeReceiptsAsync(factory);

        using var replay = await PostBatchAsync(client, token, second, first);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var retainedPath = Assert.Single(ReportFiles(factory));
        Assert.Equal(reportPath, retainedPath);
        Assert.Equal(originalHash, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(retainedPath))));
        await AssertNoDurableIntakeReceiptsAsync(factory);
    }

    internal static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/Development/EmailEvaluation");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var tag = AntiforgeryTagRegex().Match(html);
        Assert.True(tag.Success, "The offline evaluator must render an antiforgery token.");
        var value = AntiforgeryValueRegex().Match(tag.Value);
        Assert.True(value.Success, "The offline evaluator antiforgery token must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    internal static async Task<HttpResponseMessage> PostBatchAsync(
        HttpClient client,
        string antiforgeryToken,
        params OfflineEmail[] emails)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(antiforgeryToken), "__RequestVerificationToken");
        foreach (var email in emails)
        {
            var content = new ByteArrayContent(email.Content);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(email.MediaType);
            form.Add(content, "Upload", email.FileName);
        }

        return await client.PostAsync("/Development/EmailEvaluation", form);
    }

    internal static OfflineEmail CreateEmail(string fileName, string body)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("QDOS Alpha", "engineers@qdosassist.co.uk"));
        message.To.Add(new MailboxAddress("Pegasus Intake", "intake@example.test"));
        message.Subject = "QDOS offline acceptance";
        message.Body = new TextPart("plain") { Text = body };
        using var output = new MemoryStream();
        message.WriteTo(output);
        return new(fileName, "message/rfc822", output.ToArray());
    }

    internal static string[] ReportFiles(IntakeWebApplicationFactory factory) =>
        Directory.Exists(factory.ArtifactDirectory)
            ? Directory.EnumerateFiles(factory.ArtifactDirectory, "*.json", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .ToArray()
            : [];

    internal static async Task AssertNoDurableIntakeReceiptsAsync(IntakeWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var receipts = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
        Assert.Empty(await receipts.ListAsync(null, CancellationToken.None));
    }

    internal sealed record OfflineEmail(string FileName, string MediaType, byte[] Content);

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryTagRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryValueRegex();
}
