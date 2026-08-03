using Microsoft.Playwright;

namespace Pegasus.IntegrationTests.Browser;

[Trait("Category", "SqlServer")]
[Trait("Category", "Browser")]
public sealed class AccessibilityTests
{
    // Every authenticated route that renders without a seeded record id.
    // /Account/SignIn is absent on purpose: the DevelopmentOffline profile
    // authenticates automatically and redirects it, so it cannot return 200
    // through this harness. It shares the auth-panel markup with
    // /Account/PasswordChange, which is covered here.
    public static TheoryData<string> AuthenticatedRoutes => new()
    {
        "/",
        "/Intake",
        "/ImageIntake",
        "/Triage",
        "/Cases",
        "/Search",
        "/Operations/Email",
        "/Operations/Requests",
        "/Administration",
        "/Administration/Accounts",
        "/Administration/Roles",
        "/Administration/Access",
        "/Administration/Organizations",
        "/Administration/Principals",
        "/Administration/Principals/Create",
        "/Administration/Configuration",
        "/Administration/Mailboxes",
        "/Administration/Automation",
        "/Administration/Automation/Activity",
        "/Account/PasswordChange",
        "/Account/AccessDenied"
    };

    [Theory]
    [MemberData(nameof(AuthenticatedRoutes))]
    public async Task RealAuthenticatedRouteHasNoAutomatedAxeViolations(string route)
    {
        await using var support = await BrowserTestSupport.StartAsync();

        var response = await support.GoToAsync(route);

        Assert.Equal(200, response.Status);
        Assert.Empty(await support.FindAccessibilityViolationIdsAsync());
        Assert.Equal(1, await support.Page.Locator("main").CountAsync());
        Assert.Equal(1, await support.Page.Locator("h1").CountAsync());
    }

    [Fact]
    public async Task OperationsRemainsUsableAtConstrainedDesktopAndTwoHundredPercentEquivalentViewport()
    {
        await using var support = await BrowserTestSupport.StartAsync(width: 1024, height: 768);
        var response = await support.GoToAsync("/");
        Assert.Equal(200, response.Status);
        Assert.False(await HasHorizontalOverflowAsync(support.Page));
        Assert.Empty(await support.FindAccessibilityViolationIdsAsync());

        await support.Page.SetViewportSizeAsync(512, 768);
        Assert.False(await HasHorizontalOverflowAsync(support.Page));
        Assert.True(await support.Page.GetByRole(
            AriaRole.Heading,
            new PageGetByRoleOptions { Name = "Operations", Exact = true }).IsVisibleAsync());
        Assert.True(await support.Page.Locator("[data-queue-state='current']").First.IsVisibleAsync());
    }

    [Fact]
    public async Task ForcedColoursAndReducedMotionRenderTheRealOperationsCaller()
    {
        await using var support = await BrowserTestSupport.StartAsync(
            width: 1280,
            height: 720,
            forcedColors: ForcedColors.Active);

        var response = await support.GoToAsync("/");

        Assert.Equal(200, response.Status);
        Assert.Empty(await support.FindAccessibilityViolationIdsAsync());
        Assert.Equal("active", await support.Page.EvaluateAsync<string>("matchMedia('(forced-colors: active)').matches ? 'active' : 'none'"));
        Assert.True(await support.Page.EvaluateAsync<bool>("matchMedia('(prefers-reduced-motion: reduce)').matches"));
    }

    [Fact]
    public async Task QueueStateIsNotCommunicatedByColourAlone()
    {
        await using var support = await BrowserTestSupport.StartAsync();
        await support.GoToAsync("/");

        var states = support.Page.Locator("[data-queue-state]");
        var count = await states.CountAsync();
        Assert.True(count > 0);

        for (var index = 0; index < count; index++)
        {
            var state = states.Nth(index);
            var stateName = await state.GetAttributeAsync("data-queue-state");
            var text = await state.InnerTextAsync();
            if (stateName == "unavailable")
            {
                Assert.Contains("unavailable", text, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                Assert.Matches(@"\d", text);
            }
        }
    }

    [Theory]
    [MemberData(nameof(AuthenticatedRoutes))]
    public async Task ServerRenderedMarkupCarriesNoInlineStyleAttribute(string route)
    {
        // The production CSP (default-src 'self', no style-src) discards
        // inline style attributes, so any styling delivered through one works
        // locally and silently vanishes on the deployed site — that is how
        // the sprite's inline display:none shipped a ~1,900px blank band on
        // every page. The invariant is mechanical: server markup never
        // carries a style attribute; everything styles through site.css.
        await using var support = await BrowserTestSupport.StartAsync();

        await support.GoToAsync(route);

        // The one allowed [style] carrier is the validation-summary tag
        // helper's valid-state <li> placeholder — framework output we cannot
        // author away; site.css hides its .validation-summary-valid parent so
        // the discarded inline style has nothing left to break.
        var inlineStyled = await support.Page.EvaluateAsync<string[]>(
            "Array.from(document.querySelectorAll('[style]'))" +
            ".filter(element => !(element.tagName === 'LI' && element.closest('[data-valmsg-summary]')))" +
            ".map(element => element.tagName + '.' + element.getAttribute('class'))");
        Assert.Empty(inlineStyled);
        Assert.Equal(
            "none",
            await support.Page.EvaluateAsync<string>(
                "getComputedStyle(document.querySelector('svg.sprite-sheet')).display"));
        Assert.True(await support.Page.EvaluateAsync<bool>(
            "document.querySelector('.app-nav').getBoundingClientRect().top < 10"));
    }

    private static Task<bool> HasHorizontalOverflowAsync(IPage page) =>
        page.EvaluateAsync<bool>("document.documentElement.scrollWidth > window.innerWidth");
}
