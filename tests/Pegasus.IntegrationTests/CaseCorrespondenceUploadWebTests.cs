using System.Net;
using System.Text.RegularExpressions;

namespace Pegasus.IntegrationTests;

/// <summary>
/// An uploaded .eml is correspondence: it lists on the Case's Correspondence
/// tab with its sender and subject, opens in a dialog over the Case and in the
/// mail viewer, and is not a row on the Documents tab.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseCorrespondenceUploadWebTests
{
    [Fact]
    public async Task AnUploadedEmailListsAsCorrespondenceOpensOverTheCaseAndLeavesDocuments()
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

        // Open message opens the row's dialog over the Case; without script it
        // is still a link to the record. The dialog's content request names
        // this Case, for the record's Reply and Forward.
        var messageId = Guid.Parse(row.Groups[1].Value);
        var dialogId = $"case-message-{messageId:N}";
        var reference = Regex.Match(html, "<title>Case (?<reference>[^ <]+) · Pegasus</title>").Groups["reference"].Value;
        Assert.NotEmpty(reference);
        Assert.Contains($"data-dialog-open=\"{dialogId}\"", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Inbox/{messageId:D}\"", html, StringComparison.Ordinal);
        Assert.Contains($"data-dialog=\"{dialogId}\" data-case-message-dialog", html, StringComparison.Ordinal);
        Assert.Contains(
            $"data-case-message-url=\"/Inbox/{messageId:D}?correspondenceCaseReference={reference}&amp;handler=Content\"",
            html,
            StringComparison.Ordinal);
        Assert.Contains($"id=\"{dialogId}-title\" class=\"wrap\" tabindex=\"-1\">QDOS test instruction</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<span>Open full message</span>", html, StringComparison.Ordinal);

        // The dialog's body is the record's content, for an uploaded email too.
        // Offline there is no send, so no Reply or Forward either.
        using var content = await client.GetAsync(
            $"/Inbox/{messageId:D}?handler=Content&correspondenceCaseReference={reference}");
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);
        var fragment = await content.Content.ReadAsStringAsync();
        Assert.Contains("Please see the attached instruction.", fragment, StringComparison.Ordinal);
        Assert.Contains("<li>instruction.pdf</li>", fragment, StringComparison.Ordinal);
        Assert.Contains("To intake@example.test", fragment, StringComparison.Ordinal);
        Assert.DoesNotContain("data-message-actions", fragment, StringComparison.Ordinal);
        Assert.DoesNotContain("compose=", fragment, StringComparison.Ordinal);

        using var message = await client.GetAsync($"/Inbox/{messageId:D}");
        Assert.Equal(HttpStatusCode.OK, message.StatusCode);
        var messageHtml = await message.Content.ReadAsStringAsync();
        Assert.Contains("QDOS test instruction", messageHtml, StringComparison.Ordinal);
    }
}
