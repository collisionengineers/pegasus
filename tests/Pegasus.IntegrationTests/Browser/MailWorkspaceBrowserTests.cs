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
        await SeedMessageAsync(support.Services);

        var response = await support.GoToAsync("/Inbox");
        Assert.Equal(200, response.Status);

        var row = support.Page.Locator("[data-mail-preview-row]").First;
        var trigger = row.Locator("[data-mail-preview-trigger]");
        var preview = support.Page.Locator("[data-mail-preview]");
        await trigger.FocusAsync();
        await preview.Locator("[data-mail-preview-subject]").WaitForAsync();

        Assert.True(await preview.IsVisibleAsync());
        Assert.Equal("true", await trigger.GetAttributeAsync("aria-expanded"));
        Assert.True((await row.GetAttributeAsync("class") ?? string.Empty)
            .Contains("is-preview-selected", StringComparison.Ordinal));
        Assert.Equal(
            "Browser preview message",
            await preview.Locator("[data-mail-preview-subject]").TextContentAsync());
        Assert.Equal(
            "estimate.pdf",
            await preview.Locator("[data-mail-preview-attachments] li").TextContentAsync());
        Assert.Equal(
            "Not associated",
            await preview.Locator("[data-mail-preview-association]").TextContentAsync());
        Assert.Empty(await support.FindAccessibilityViolationIdsAsync());

        var desktopRelationship = await support.Page.EvaluateAsync<bool>(
            "(() => {" +
            " const table = document.querySelector('.mail-workspace > .table-wrap').getBoundingClientRect();" +
            " const preview = document.querySelector('[data-mail-preview]').getBoundingClientRect();" +
            " return Math.abs(table.top - preview.top) < 2 && preview.left >= table.right;" +
            "})()");
        Assert.True(desktopRelationship);

        await support.Page.Locator("#mail-search").FocusAsync();
        await preview.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });
        Assert.Equal("false", await trigger.GetAttributeAsync("aria-expanded"));

        await row.HoverAsync();
        await preview.Locator("[data-mail-preview-subject]").WaitForAsync();
        Assert.True(await preview.IsVisibleAsync());

        await support.Page.SetViewportSizeAsync(640, 800);
        await trigger.FocusAsync();
        await preview.Locator("[data-mail-preview-subject]").WaitForAsync();
        Assert.False(await HasHorizontalDocumentOverflowAsync(support.Page));
        Assert.True(await support.Page.EvaluateAsync<bool>(
            "(() => {" +
            " const table = document.querySelector('.mail-workspace > .table-wrap').getBoundingClientRect();" +
            " const preview = document.querySelector('[data-mail-preview]').getBoundingClientRect();" +
            " return preview.top >= table.bottom;" +
            "})()"));
        Assert.Empty(await support.FindAccessibilityViolationIdsAsync());
    }

    [Fact]
    public async Task SubjectRemainsTheFullDetailLinkWithoutJavaScript()
    {
        await using var support = await BrowserTestSupport.StartAsync(
            javaScriptEnabled: false,
            useIntegrationTestAuthentication: true);
        var messageId = await SeedMessageAsync(support.Services);

        var response = await support.GoToAsync("/Inbox");
        Assert.Equal(200, response.Status);
        Assert.True(await support.Page.Locator("[data-mail-preview]").IsHiddenAsync());

        var trigger = support.Page.Locator("[data-mail-preview-trigger]");
        Assert.Equal($"/Inbox/{messageId:D}", await trigger.GetAttributeAsync("href"));
        await trigger.ClickAsync();
        await support.Page.WaitForURLAsync($"**/Inbox/{messageId:D}");
        Assert.Equal("Browser preview message", await support.Page.Locator("h1").TextContentAsync());
    }

    private static Task<bool> HasHorizontalDocumentOverflowAsync(IPage page) =>
        page.EvaluateAsync<bool>("document.documentElement.scrollWidth > window.innerWidth");

    private static async Task<Guid> SeedMessageAsync(IServiceProvider services)
    {
        const string mailboxId = "browser-mail";
        const string mailboxAddress = "browser-mail@collisionengineers.co.uk";
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            context.ApprovedInboxPollStates.Add(new()
            {
                MailboxId = mailboxId,
                MailboxAddress = mailboxAddress,
                DueAtUtc = NowUtc,
                LastCompletedAtUtc = NowUtc.AddMinutes(-1)
            });
            await context.SaveChangesAsync();
        }

        await scope.ServiceProvider.GetRequiredService<EfRetainedMailboxMessageStore>()
            .RetainAsync(
                new(
                    mailboxId,
                    mailboxAddress,
                    "browser-message-1",
                    $"{mailboxId.Length}:{mailboxId}browser-message-1",
                    NowUtc,
                    1024,
                    new string('A', 64),
                    new(
                        "inbox",
                        "browser-conversation",
                        "<browser-message-1@example.invalid>",
                        "sender@example.invalid",
                        "Browser Sender",
                        [mailboxAddress],
                        [],
                        "Browser preview message",
                        "The browser preview preserves this retained excerpt.",
                        [new("estimate.pdf", "application/pdf", 2048)],
                        IsRead: false),
                    NowUtc),
                CancellationToken.None);

        await using var readContext = await contextFactory.CreateDbContextAsync();
        return await readContext.RetainedMailboxMessages
            .Where(item => item.MailboxId == mailboxId)
            .Select(item => item.Id)
            .SingleAsync();
    }
}
