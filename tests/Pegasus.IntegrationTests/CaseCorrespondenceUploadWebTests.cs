using System.Net;
using System.Text.RegularExpressions;

namespace Pegasus.IntegrationTests;

/// <summary>
/// An uploaded .eml is correspondence: it lists on the Case's Correspondence
/// tab with its sender and subject, opens in the mail viewer, and is not a row
/// on the Documents tab.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseCorrespondenceUploadWebTests
{
    [Fact]
    public async Task AnUploadedEmailListsAsCorrespondenceOpensInTheViewerAndLeavesDocuments()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "AB12 CDE", "CORR-EML-01");

        var html = await IntakeWebDriver.GetHtmlAsync(client, $"/Cases/{caseId:D}?section=files");

        // The seeded case's origin is the uploaded case-corr-eml-01.eml.
        var row = Regex.Match(html, "data-correspondence-row=\"([0-9a-f-]{36})\"");
        Assert.True(row.Success, "The uploaded email is not listed as correspondence.");
        Assert.Contains("Synthetic sender", html, StringComparison.Ordinal);
        Assert.Contains("QDOS test instruction", html, StringComparison.Ordinal);
        Assert.DoesNotContain("case-corr-eml-01.eml</b>", html, StringComparison.Ordinal);

        using var message = await client.GetAsync($"/Inbox/{row.Groups[1].Value}");
        Assert.Equal(HttpStatusCode.OK, message.StatusCode);
        var messageHtml = await message.Content.ReadAsStringAsync();
        Assert.Contains("QDOS test instruction", messageHtml, StringComparison.Ordinal);
    }
}
