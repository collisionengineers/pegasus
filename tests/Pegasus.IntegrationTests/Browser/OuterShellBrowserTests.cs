using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Playwright;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;

namespace Pegasus.IntegrationTests.Browser;

/// <summary>
/// C08: the shell's Ctrl+K command palette, its keyboard/dialog contract and
/// the notifications menu, driven end to end. The palette itself predates
/// this ticket (it already submits typed text to <c>/Search?query=</c>); this
/// file is the browser proof the design contract asks for, not a rebuild.
/// </summary>
[Trait("Category", "SqlServer")]
[Trait("Category", "Browser")]
public sealed class OuterShellBrowserTests
{
    [Fact]
    public async Task CtrlKOpensThePaletteAndEnterNavigatesToSearch()
    {
        await using var support = await BrowserTestSupport.StartAsync(
            useIntegrationTestAuthentication: true);
        var response = await support.GoToAsync("/Search");
        Assert.Equal(200, response.Status);

        var dialog = support.Page.Locator("[data-dialog=\"command-dialog\"]");
        Assert.True(await dialog.IsHiddenAsync());

        await support.Page.Keyboard.PressAsync("Control+K");
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        Assert.True(await dialog.IsVisibleAsync());

        await support.Page.Locator("[data-command-palette-input]").FillAsync("registration ABC123");
        await support.Page.Keyboard.PressAsync("Enter");
        await support.Page.WaitForURLAsync("**/Search?query=*");

        Assert.Contains(
            "query=registration%20ABC123",
            support.Page.Url,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EscapeClosesThePaletteAndRestoresFocus()
    {
        await using var support = await BrowserTestSupport.StartAsync(
            useIntegrationTestAuthentication: true);
        await support.GoToAsync("/Search");

        var opener = support.Page.Locator("#global-search");
        var dialog = support.Page.Locator("[data-dialog=\"command-dialog\"]");

        await opener.FocusAsync();
        await support.Page.Keyboard.PressAsync("Enter");
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        await support.Page.Keyboard.PressAsync("Escape");
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        Assert.True(await dialog.IsHiddenAsync());

        // Escape returns focus to the utility search box that opened it, not
        // to the document body — the dialog contract's focus-return rule.
        Assert.Equal(
            "global-search",
            await support.Page.EvaluateAsync<string>("document.activeElement.id"));
    }

    [Fact]
    public async Task RepeatedCtrlKDoesNotReplaceTheDialogReleaseOrFocusOwner()
    {
        await using var support = await BrowserTestSupport.StartAsync(
            useIntegrationTestAuthentication: true);
        await support.GoToAsync("/Search");

        var opener = support.Page.Locator("#global-search");
        var dialog = support.Page.Locator("[data-dialog=\"command-dialog\"]");
        await opener.FocusAsync();
        await support.Page.Keyboard.PressAsync("Control+K");
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await support.Page.Keyboard.PressAsync("Control+K");
        await support.Page.Keyboard.PressAsync("Escape");
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden });

        Assert.False(await support.Page.EvaluateAsync<bool>(
            "Array.from(document.querySelectorAll('[inert]')).some(element => !element.closest('[data-dialog=command-dialog]'))"));
        Assert.Equal(
            "global-search",
            await support.Page.EvaluateAsync<string>("document.activeElement.id"));
    }

    [Theory]
    [InlineData(390)]
    [InlineData(1280)]
    public async Task LayoutRendersWithoutHorizontalOverflow(int width)
    {
        await using var support = await BrowserTestSupport.StartAsync(
            width: width,
            height: 800,
            useIntegrationTestAuthentication: true);
        var response = await support.GoToAsync("/Search");
        Assert.Equal(200, response.Status);

        Assert.False(await HasHorizontalDocumentOverflowAsync(support.Page));
    }

    [Fact]
    public async Task NotificationsControlShowsNothingWhenThereAreNoAttentionRows()
    {
        await using var support = await BrowserTestSupport.StartAsync(
            useIntegrationTestAuthentication: true,
            configureWebHost: builder => WithAttentionRows(builder, []));
        await support.GoToAsync("/Search");

        await support.Page.Locator("[data-dialog-open=\"notifications-dialog\"]").ClickAsync();
        var dialog = support.Page.Locator("[data-dialog=\"notifications-dialog\"]");
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        Assert.Equal(0, await dialog.Locator("[data-row-list] a").CountAsync());
    }

    [Fact]
    public async Task NotificationsControlShowsTheBoundedRowsItWasGiven()
    {
        var rows = new[]
        {
            new NeedsAttentionItem(
                NeedsAttentionKind.Triage, Guid.NewGuid(), "T/2031/004", "AB12 CDE",
                Detail: null, Reason: "open", NeedsAttentionPriority.High,
                Owner: null, Due: null, LastOutcome: null, Source: null, Attempts: null),
            new NeedsAttentionItem(
                NeedsAttentionKind.Mail, Guid.NewGuid(), "U/2031/009", "Unreadable attachment",
                Detail: null, Reason: "no_usable_identification", NeedsAttentionPriority.Normal,
                Owner: null, Due: null, LastOutcome: null, Source: "email", Attempts: null)
        };

        await using var support = await BrowserTestSupport.StartAsync(
            useIntegrationTestAuthentication: true,
            configureWebHost: builder => WithAttentionRows(builder, rows));
        await support.GoToAsync("/Search");

        await support.Page.Locator("[data-dialog-open=\"notifications-dialog\"]").ClickAsync();
        var dialog = support.Page.Locator("[data-dialog=\"notifications-dialog\"]");
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        var renderedRows = dialog.Locator("[data-row-list] a");
        Assert.Equal(rows.Length, await renderedRows.CountAsync());
        Assert.Contains("AB12 CDE", await renderedRows.Nth(0).TextContentAsync());
        Assert.Contains("Unreadable attachment", await renderedRows.Nth(1).TextContentAsync());
    }

    private static void WithAttentionRows(
        IWebHostBuilder builder, IReadOnlyList<NeedsAttentionItem> rows) =>
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGetAttentionRows>();
            services.AddSingleton<IGetAttentionRows>(new StubAttentionRows(rows));
        });

    private static Task<bool> HasHorizontalDocumentOverflowAsync(IPage page) =>
        page.EvaluateAsync<bool>("document.documentElement.scrollWidth > window.innerWidth");

    private sealed class StubAttentionRows(IReadOnlyList<NeedsAttentionItem> rows) : IGetAttentionRows
    {
        public Task<IReadOnlyList<NeedsAttentionItem>> ExecuteAsync(
            ActionActor actor, CancellationToken cancellationToken = default) =>
            Task.FromResult(rows);
    }
}
