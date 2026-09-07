using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests.Browser;

[Trait("Category", "SqlServer")]
[Trait("Category", "Browser")]
public sealed class MailWorkspaceBrowserTests
{
    private static readonly DateTimeOffset NowUtc =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task QuickPreviewWorksByKeyboardAndPointerAndStacksWithoutOverflow()
    {
        await using var support = await BrowserTestSupport.StartAsync(
            width: 1280,
            height: 800,
            useIntegrationTestAuthentication: true);
        await SeedMessagesAsync(support.Services);

        var response = await support.GoToAsync("/Inbox");
        Assert.Equal(200, response.Status);

        var row = support.Page.Locator("[data-mail-preview-row]").First;
        var trigger = row.Locator("[data-mail-preview-trigger]");
        var preview = support.Page.Locator("[data-mail-preview]");
        await trigger.FocusAsync();
        await preview.Locator("[data-mail-preview-subject]").WaitForAsync();

        Assert.True(await preview.IsVisibleAsync());
        Assert.Equal("true", await trigger.GetAttributeAsync("aria-expanded"));
        // The selected row's affordance is aria-current — the attribute the
        // row-button styles key on — not a JS-only class.
        Assert.Equal("true", await trigger.GetAttributeAsync("aria-current"));
        Assert.Equal(
            "Browser preview message",
            await preview.Locator("[data-mail-preview-subject]").TextContentAsync());
        Assert.Equal(
            "estimate.pdf",
            await preview.Locator("[data-mail-preview-attachments] li").TextContentAsync());
        Assert.Equal(
            "No case",
            await preview.Locator("[data-mail-preview-association]").TextContentAsync());
        Assert.Empty(await support.FindAccessibilityViolationIdsAsync());

        // The port wrapped the preview in the drawn third pane with its own
        // "Message preview" head, so the side-by-side relationship is measured
        // between the panes — the preview article itself starts below that
        // head by design.
        var desktopRelationship = await support.Page.EvaluateAsync<bool>(
            "(() => {" +
            " const messages = document.querySelector('[data-mail-preview-workspace] > .pane:nth-child(2)').getBoundingClientRect();" +
            " const previewPane = document.querySelector('[data-mail-preview-workspace] > .pane:nth-child(3)').getBoundingClientRect();" +
            " const preview = document.querySelector('[data-mail-preview]').getBoundingClientRect();" +
            " return Math.abs(messages.top - previewPane.top) < 2 && previewPane.left >= messages.right && preview.left >= messages.right;" +
            "})()");
        Assert.True(desktopRelationship);

        // Focus leaving the rows restores the selected message: the pane is
        // a fixture of the page, not a tooltip that dismisses to blank.
        await support.Page.Locator("#mail-search").FocusAsync();
        Assert.True(await preview.IsVisibleAsync());
        Assert.Equal(
            "Browser preview message",
            await preview.Locator("[data-mail-preview-subject]").TextContentAsync());
        Assert.Equal("true", await trigger.GetAttributeAsync("aria-expanded"));

        // Hovering a row other than the selected one is the one pointer path
        // that exercises the JSON preview handler end to end.
        await support.Page.Locator("[data-mail-preview-row]").Nth(1).HoverAsync();
        await WaitForPreviewSubjectAsync(support.Page, "Older browser message");
        Assert.True(await preview.IsVisibleAsync());

        await support.Page.SetViewportSizeAsync(640, 800);
        await trigger.FocusAsync();
        await preview.Locator("[data-mail-preview-subject]").WaitForAsync();
        // The pointer still rests on a row, and the resize re-fires
        // pointerenter under it, so the pane can be mid-transient here.
        // Parking the pointer on the pane itself settles the preview back on
        // the selected message before the constrained-width checks.
        await preview.HoverAsync();
        await WaitForPreviewSubjectAsync(support.Page, "Browser preview message");
        Assert.False(await HasHorizontalDocumentOverflowAsync(support.Page));
        Assert.True(await support.Page.EvaluateAsync<bool>(
            "(() => {" +
            " const messages = document.querySelector('[data-mail-preview-workspace] > .pane:nth-child(2)').getBoundingClientRect();" +
            " const previewPane = document.querySelector('[data-mail-preview-workspace] > .pane:nth-child(3)').getBoundingClientRect();" +
            " return previewPane.top >= messages.bottom;" +
            "})()"));
        Assert.Empty(await support.FindAccessibilityViolationIdsAsync());
    }

    [Fact]
    public async Task SubjectSelectsTheServerRenderedPreviewAndThePaneOpensFullDetailWithoutJavaScript()
    {
        await using var support = await BrowserTestSupport.StartAsync(
            javaScriptEnabled: false,
            useIntegrationTestAuthentication: true);
        var (newestId, _) = await SeedMessagesAsync(support.Services);

        // The newest row renders its preview server-side before any click.
        var response = await support.GoToAsync("/Inbox");
        Assert.Equal(200, response.Status);
        var preview = support.Page.Locator("[data-mail-preview]");
        Assert.True(await preview.IsVisibleAsync());
        Assert.Equal(
            "Browser preview message",
            await preview.Locator("[data-mail-preview-subject]").TextContentAsync());

        // The subject selects its row: same page, selected state in the query.
        var trigger = support.Page.Locator("[data-mail-preview-trigger]").First;
        await trigger.ClickAsync();
        await support.Page.WaitForURLAsync($"**/Inbox?selected={newestId:D}");
        Assert.Equal(
            "Browser preview message",
            await preview.Locator("[data-mail-preview-subject]").TextContentAsync());

        // The pane, not the row, is the full-detail entry.
        await preview.Locator("a.btn--dark").ClickAsync();
        await support.Page.WaitForURLAsync($"**/Inbox/{newestId:D}");
        Assert.Equal("Browser preview message", await support.Page.Locator("h1").TextContentAsync());
    }

    [Fact]
    public async Task HoverPreviewRestoresTheSelectedMessageAndKeepsThePaneActionsReachable()
    {
        await using var support = await BrowserTestSupport.StartAsync(
            width: 1280,
            height: 800,
            useIntegrationTestAuthentication: true);
        var (newestId, _) = await SeedMessagesAsync(support.Services);

        var response = await support.GoToAsync("/Inbox");
        Assert.Equal(200, response.Status);

        var newestTrigger = support.Page.Locator("[data-mail-preview-trigger]").First;
        var olderRow = support.Page.Locator("[data-mail-preview-row]").Nth(1);
        var olderTrigger = olderRow.Locator("[data-mail-preview-trigger]");
        var preview = support.Page.Locator("[data-mail-preview]");

        // Hovering the older row previews it transiently; the pane's actions
        // belong to the selected message, so they step aside meanwhile.
        await olderRow.HoverAsync();
        await WaitForPreviewSubjectAsync(support.Page, "Older browser message");
        Assert.True(await preview.Locator("[data-mail-preview-actions]").IsHiddenAsync());

        // Moving the pointer off the list — here, toward the pane's actions —
        // restores the selected message instead of hiding the pane, and the
        // actions return with it.
        await preview.HoverAsync();
        await WaitForPreviewSubjectAsync(support.Page, "Browser preview message");
        Assert.True(await preview.IsVisibleAsync());
        Assert.True(await preview.Locator("[data-mail-preview-actions]").IsVisibleAsync());
        Assert.Equal("true", await newestTrigger.GetAttributeAsync("aria-expanded"));
        Assert.Equal("false", await olderTrigger.GetAttributeAsync("aria-expanded"));

        // The same restore holds for keyboard intent.
        await olderTrigger.FocusAsync();
        await WaitForPreviewSubjectAsync(support.Page, "Older browser message");
        await support.Page.Locator("#mail-search").FocusAsync();
        await WaitForPreviewSubjectAsync(support.Page, "Browser preview message");
        Assert.True(await preview.IsVisibleAsync());

        // The pane's full-detail link is reachable and opens the selected
        // message — the action the transient dismiss made unreachable.
        await preview.Locator("a.btn--dark").ClickAsync();
        await support.Page.WaitForURLAsync($"**/Inbox/{newestId:D}");
        Assert.Equal("Browser preview message", await support.Page.Locator("h1").TextContentAsync());
    }

    private static Task<bool> HasHorizontalDocumentOverflowAsync(IPage page) =>
        page.EvaluateAsync<bool>("document.documentElement.scrollWidth > window.innerWidth");

    private static Task<IJSHandle> WaitForPreviewSubjectAsync(IPage page, string subject) =>
        page.WaitForFunctionAsync(
            "subject => document.querySelector('[data-mail-preview-subject]')" +
            " !== null && document.querySelector('[data-mail-preview-subject]').textContent.trim()" +
            " === subject",
            subject);

    private const string MailboxKey = "browser-mail";
    private const string MailboxAddress = "browser-mail@collisionengineers.co.uk";

    private static async Task<(Guid NewestId, Guid OlderId)> SeedMessagesAsync(
        IServiceProvider services)
    {
        var mailboxId = TestMailboxId.From(MailboxKey);
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await TestMailboxId.EnsureApprovedAsync(
                context, MailboxKey, MailboxAddress, NowUtc.AddDays(-1));
            context.ApprovedInboxPollStates.Add(new()
            {
                ApprovedMailboxId = mailboxId,
                MailboxAddress = MailboxAddress,
                ScopeFingerprint = new string('A', 64),
                ActivatedAtUtc = NowUtc.AddDays(-1),
                DueAtUtc = NowUtc,
                LastCompletedAtUtc = NowUtc.AddMinutes(-1)
            });
            await context.SaveChangesAsync();
        }

        var store = scope.ServiceProvider.GetRequiredService<EfRetainedMailboxMessageStore>();
        await store.RetainAsync(
            Retained("browser-message-1", NowUtc, "Browser Sender",
                "Browser preview message",
                "The browser preview preserves this retained excerpt.",
                [new("estimate.pdf", "application/pdf", 2048)]),
            CancellationToken.None);
        // The older row is the transient hover target; the newest row is the
        // pane's server-rendered default selection.
        await store.RetainAsync(
            Retained("browser-message-0", NowUtc.AddHours(-1), "Older Browser Sender",
                "Older browser message",
                "An older retained excerpt for the transient hover.",
                []),
            CancellationToken.None);

        await using var readContext = await contextFactory.CreateDbContextAsync();
        var ids = await readContext.RetainedMailboxMessages
            .Where(item => item.MailboxId == mailboxId)
            .OrderByDescending(item => item.ReceivedAtUtc)
            .Select(item => item.Id)
            .ToListAsync();
        return (ids[0], ids[1]);
    }

    private static RetainedMailboxMessage Retained(
        string internetMessageId,
        DateTimeOffset receivedAtUtc,
        string sender,
        string subject,
        string excerpt,
        IReadOnlyList<RetainedMailboxAttachment> attachments) =>
        new(
            TestMailboxId.From(MailboxKey),
            MailboxAddress,
            internetMessageId,
            $"{MailboxKey.Length}:{MailboxKey}{internetMessageId}",
            receivedAtUtc,
            1024,
            new string('A', 64),
            new(
                "inbox",
                "browser-conversation",
                $"<{internetMessageId}@example.invalid>",
                "sender@example.invalid",
                sender,
                [MailboxAddress],
                [],
                [],
                subject,
                excerpt,
                attachments,
                IsRead: false),
            receivedAtUtc);
}
