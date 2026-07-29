using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Pegasus.IntegrationTests;

public sealed class NegativeMatrixTests
{
    [Theory]
    [Trait("Category", "QdosAlphaAcceptance")]
    [InlineData("empty.eml", "message/rfc822", "", "The selected file is empty.")]
    [InlineData("not-email.txt", "text/plain", "not an email", "The selected file must be an .eml email.")]
    public async Task InvalidCampaignInputCannotCreateEvidenceOrDurableIntake(
        string fileName,
        string mediaType,
        string body,
        string expectedFailure)
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var token = await OfflineAcceptanceTests.GetAntiforgeryTokenAsync(client);

        using var response = await OfflineAcceptanceTests.PostBatchAsync(
            client,
            token,
            new OfflineAcceptanceTests.OfflineEmail[]
            {
                new(fileName, mediaType, System.Text.Encoding.UTF8.GetBytes(body)),
            });
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expectedFailure, html, StringComparison.Ordinal);
        Assert.Empty(OfflineAcceptanceTests.ReportFiles(factory));
        await OfflineAcceptanceTests.AssertNoDurableIntakeReceiptsAsync(factory);
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task ForgedRequestWithoutAntiforgeryEvidenceIsRejectedBeforeEvaluation()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = OfflineAcceptanceTests.CreateEmail(
            "forged.eml",
            "QDOS instruction\r\nClaimant Name: Forged\r\nClaim Number: FORGED-001\r\nVehicle Registration: AB12 CDE");
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(email.Content);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(email.MediaType);
        form.Add(content, "Upload", email.FileName);

        using var response = await client.PostAsync("/Intake/EmailEvaluation", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(OfflineAcceptanceTests.ReportFiles(factory));
        await OfflineAcceptanceTests.AssertNoDurableIntakeReceiptsAsync(factory);
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task DuplicateContentRemainsExplicitlyBlockedInRetainedEvidence()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var token = await OfflineAcceptanceTests.GetAntiforgeryTokenAsync(client);
        var original = OfflineAcceptanceTests.CreateEmail(
            "duplicate-a.eml",
            "QDOS instruction\r\nClaimant Name: Duplicate\r\nClaim Number: DUP-001\r\nVehicle Registration: AB12 CDE");
        var duplicate = new OfflineAcceptanceTests.OfflineEmail(
            "duplicate-b.eml",
            original.MediaType,
            original.Content);

        using var response = await OfflineAcceptanceTests.PostBatchAsync(client, token, original, duplicate);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var report = JsonDocument.Parse(await File.ReadAllBytesAsync(
            Assert.Single(OfflineAcceptanceTests.ReportFiles(factory))));
        Assert.False(report.RootElement.GetProperty("activationAllowed").GetBoolean());
        Assert.Equal(1, report.RootElement.GetProperty("summary").GetProperty("duplicateSources").GetInt32());
        Assert.Contains(
            report.RootElement.GetProperty("blockingReasons").EnumerateArray().Select(item => item.GetString()),
            reason => reason is not null && reason.Contains("Duplicate source content", StringComparison.Ordinal));
        await OfflineAcceptanceTests.AssertNoDurableIntakeReceiptsAsync(factory);
    }

    [Theory]
    [Trait("Category", "QdosAlphaAcceptance")]
    [InlineData("")]
    [InlineData("../secrets.json")]
    [InlineData("evaluation-reports/sha256/00/not-a-hash.json")]
    public async Task ReportDownloadRejectsMissingOrForgedEvidenceKeys(string reportKey)
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync(
            $"/Intake/EmailEvaluation?handler=Report&reportKey={Uri.EscapeDataString(reportKey)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(OfflineAcceptanceTests.ReportFiles(factory));
    }
}
