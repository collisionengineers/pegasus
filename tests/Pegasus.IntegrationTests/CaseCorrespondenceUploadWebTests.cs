using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// An uploaded .eml is correspondence: it lists on the Case's Correspondence
/// tab with its sender and subject, opens in the Inbox's own e-mail dialog
/// over the Case (R1278) and on the record page, and is not a row on the
/// Documents tab.
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

        // R1278: the subject opens the Inbox's own e-mail dialog over the Case,
        // in the page's one mount, which names the Case; without script it is
        // the record page. The Case draws no message dialog of its own.
        var messageId = Guid.Parse(row.Groups[1].Value);
        Assert.Equal(1, Regex.Count(html, "data-mail-dialog-root"));
        Assert.Contains(
            $"<div data-mail-dialog-root data-mail-return-case=\"{caseId:D}\"></div>", html, StringComparison.Ordinal);
        Assert.Contains("data-mail-list=\"files\" data-correspondence>", html, StringComparison.Ordinal);
        Assert.Contains($"data-mail-row=\"{messageId:D}\"", html, StringComparison.Ordinal);
        var subject = Regex.Match(html, $"<a[^>]*href=\"/Inbox/{messageId:D}\"[^>]*>");
        Assert.True(subject.Success, "The subject does not link to the record.");
        Assert.Contains("data-mail-open", subject.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("data-case-message-dialog", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-dialog-open=\"case-message-", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Open full message", html, StringComparison.Ordinal);

        // The dialog is the record's own, for an uploaded email too: its text,
        // its recipients and its file.
        var dialog = await IntakeWebDriver.GetHtmlAsync(
            client, $"/Inbox/{messageId:D}?handler=Dialog&returnCase={caseId:D}&returnSection=files");
        Assert.Contains("data-dialog=\"mailMessageDialog\"", dialog, StringComparison.Ordinal);
        Assert.Contains($"data-mail-message=\"{messageId:D}\"", dialog, StringComparison.Ordinal);
        Assert.Contains("Please see the attached instruction.", dialog, StringComparison.Ordinal);
        Assert.Contains("instruction.pdf", dialog, StringComparison.Ordinal);
        Assert.Contains("To intake@example.test", dialog, StringComparison.Ordinal);

        using var message = await client.GetAsync($"/Inbox/{messageId:D}");
        Assert.Equal(HttpStatusCode.OK, message.StatusCode);
        var messageHtml = await message.Content.ReadAsStringAsync();
        Assert.Contains("QDOS test instruction", messageHtml, StringComparison.Ordinal);
    }

    /// <summary>
    /// R1278: opened from the Case, the dialog's forms carry the Case back, a
    /// write lands on the Case at the list the message was opened from, and
    /// the dialog states no Inbox scope. A Case that is not the message's own
    /// is ignored, and the write lands on the Inbox as it always has.
    /// </summary>
    [Fact]
    public async Task TheDialogOpenedFromTheCaseReturnsThereAndIgnoresAnotherCase()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "AB12 CDE", "CORR-EML-03");
        var html = await IntakeWebDriver.GetHtmlAsync(client, $"/Cases/{caseId:D}?section=files");
        var messageId = Guid.Parse(Regex.Match(html, "data-correspondence-row=\"([0-9a-f-]{36})\"").Groups[1].Value);
        var fromCase = $"/Inbox/{messageId:D}?handler=Dialog&returnCase={caseId:D}&returnSection=files";

        var dialog = await IntakeWebDriver.GetHtmlAsync(client, fromCase);
        var dismiss = DismissalForm(dialog, "dismiss");
        var action = FormAction(dismiss);
        Assert.Contains("dialog=true", action, StringComparison.Ordinal);
        Assert.Contains($"returnCase={caseId:D}", action, StringComparison.Ordinal);
        Assert.Contains("returnSection=files", action, StringComparison.Ordinal);
        Assert.DoesNotContain(OutsideView, dialog, StringComparison.Ordinal);

        // Dismiss lands on the Case's Correspondence; with no row after it,
        // no message opens.
        using (var dismissed = await client.PostAsync(action, DismissalPost(dialog, dismiss)))
        {
            Assert.Equal(HttpStatusCode.Redirect, dismissed.StatusCode);
            var location = dismissed.Headers.Location?.OriginalString ?? string.Empty;
            Assert.StartsWith($"/Cases/{caseId:D}?", location, StringComparison.Ordinal);
            Assert.Contains("section=files", location, StringComparison.Ordinal);
            Assert.DoesNotContain("open=", location, StringComparison.Ordinal);
            Assert.EndsWith("#case-files-correspondence", location, StringComparison.Ordinal);
        }

        // Dismissed, the message is outside the Inbox's own view, which the
        // Inbox's dialog says; opened from the Case nothing is said.
        Assert.Contains(
            OutsideView,
            await IntakeWebDriver.GetHtmlAsync(client, $"/Inbox/{messageId:D}?handler=Dialog"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(OutsideView, await IntakeWebDriver.GetHtmlAsync(client, fromCase), StringComparison.Ordinal);

        // Another Case is not this message's: the forms carry no Case, and a
        // post that names one anyway lands on the Inbox.
        var stranger = Guid.NewGuid();
        var elsewhere = await IntakeWebDriver.GetHtmlAsync(
            client, $"/Inbox/{messageId:D}?handler=Dialog&returnCase={stranger:D}&returnSection=files");
        var restore = DismissalForm(elsewhere, "restore");
        Assert.DoesNotContain("returnCase=", FormAction(restore), StringComparison.Ordinal);
        using (var restored = await client.PostAsync(
            $"{FormAction(restore)}&returnCase={stranger:D}&returnSection=files",
            DismissalPost(elsewhere, restore)))
        {
            Assert.Equal(HttpStatusCode.Redirect, restored.StatusCode);
            Assert.Equal("/Inbox", restored.Headers.Location?.OriginalString);
        }
    }

    [Fact]
    public async Task ALinkedCancellationNamesItselfInTheNextActionUntilItIsCorrectedAway()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "AB12 CDE", "CORR-EML-02");
        var html = await IntakeWebDriver.GetHtmlAsync(client, $"/Cases/{caseId:D}?section=files");
        var messageId = Guid.Parse(Regex.Match(html, "data-correspondence-row=\"([0-9a-f-]{36})\"").Groups[1].Value);
        Assert.DoesNotContain("data-cancellation-received", html, StringComparison.Ordinal);

        // FRD-13: while the linked message's current classification is a
        // cancellation, the open Case's Next action names it and opens it.
        await SetClassificationAsync(factory, messageId, "cancellation");
        html = await IntakeWebDriver.GetHtmlAsync(client, $"/Cases/{caseId:D}");
        Assert.Contains($"data-cancellation-received=\"{messageId:D}\"", html, StringComparison.Ordinal);
        Assert.Contains("<span>Cancellation received</span>", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Inbox?open={messageId:D}\"", html, StringComparison.Ordinal);

        // Corrected to anything else, the row goes.
        await SetClassificationAsync(factory, messageId, "triage-request");
        html = await IntakeWebDriver.GetHtmlAsync(client, $"/Cases/{caseId:D}");
        Assert.DoesNotContain("data-cancellation-received", html, StringComparison.Ordinal);
    }

    private const string OutsideView = "This message is no longer in the view you opened it from.";

    /// <summary>The dialog's Dismiss or Restore form.</summary>
    private static string DismissalForm(string html, string kind)
    {
        var form = Regex.Match(html, $"<form[^>]*data-message-dismissal=\"{kind}\"[^>]*>[\\s\\S]*?</form>");
        Assert.True(form.Success, $"The dialog has no {kind} form.");
        return form.Value;
    }

    private static string FormAction(string form)
    {
        // Anchored on a preceding space so a handler query does not match.
        var match = Regex.Match(form, "\\saction=\"(?<action>[^\"]+)\"");
        Assert.True(match.Success, "The form has no action.");
        return WebUtility.HtmlDecode(match.Groups["action"].Value);
    }

    private static string FormInput(string html, string name)
    {
        var tag = Regex.Match(html, $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*>");
        Assert.True(tag.Success, $"The input '{name}' was not rendered.");
        return WebUtility.HtmlDecode(Regex.Match(tag.Value, "value=\"(?<value>[^\"]*)\"").Groups["value"].Value);
    }

    /// <summary>The Dismiss or Restore post, with no message after it in the list.</summary>
    private static FormUrlEncodedContent DismissalPost(string html, string form) =>
        new(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = FormInput(html, "__RequestVerificationToken"),
            ["operationKey"] = FormInput(form, "operationKey"),
            ["next"] = string.Empty
        });

    // The linked message's current classification, as the mailbox pipeline or
    // a correction would leave it.
    private static async Task SetClassificationAsync(
        IntakeWebApplicationFactory factory,
        Guid messageId,
        string category)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var token = await context.RetainedMailboxMessages
            .Where(item => item.Id == messageId)
            .Select(item => item.ExternalReceiptToken)
            .SingleAsync();
        var receipt = await context.IntakeReceipts
            .Include(item => item.MailClassificationDecision)
            .SingleAsync(item => item.ExternalReceiptToken == token);
        if (receipt.MailClassificationDecision is { } decision)
        {
            decision.Outcome = "classified";
            decision.Category = category;
        }
        else
        {
            receipt.MailClassificationDecision = new()
            {
                IntakeReceiptId = receipt.Id,
                Outcome = "classified",
                Category = category,
                IsReplyContext = false,
                PredicatesJson = "{\"version\":1,\"data\":[]}",
                Reason = "Fixture.",
                PolicyKey = "fixture",
                PolicyVersion = 1,
                DecidedByActor = "test",
                DecidedAtUtc = DateTimeOffset.UtcNow,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            };
        }
        await context.SaveChangesAsync();
    }
}
