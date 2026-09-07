using Microsoft.Playwright;

namespace Pegasus.IntegrationTests.Browser;

[Trait("Category", "SqlServer")]
[Trait("Category", "Browser")]
public sealed class AccessibilityTests
{
    // Every authenticated route that renders without a seeded record id.
    // /Account/SignIn is absent on purpose: the DevelopmentOffline profile
    // authenticates automatically and redirects it, so it cannot return 200
    // through this harness. It shares the auth-card markup with
    // /Account/PasswordChange, which is covered here.
    public static readonly string[] AuthenticatedRouteList =
    [
        "/",
        "/Inbox",
        "/Operations",
        // The Cases workflow tabs (EPIC-011 §1.4; the old /Triage queues).
        "/Cases",
        "/Cases?tab=triage",
        "/Cases?tab=awaiting",
        // The Unidentified tab is a distinct rendered shape from the default
        // Not-ready view (INTK-009 folded the standalone /Unidentified page
        // in here as a tab, so it earns its own accessibility pass).
        "/Cases?tab=unidentified",
        // The case search (EPIC-011 §1.7; the old /Cases).
        "/Search",
        "/Administration",
        "/Administration/Accounts",
        "/Administration/Roles",
        "/Administration/Organizations",
        "/Administration/Principals",
        "/Administration/Principals/Create",
        "/Administration/Configuration",
        "/Administration/Mailboxes",
        "/Administration/MailCategories",
        "/Administration/Automation",
        "/Administration/Automation/Activity",
        "/Account/PasswordChange",
        "/Account/AccessDenied",
        // The designed answer to a status code. Before it existed, an unknown
        // record URL and a dead public upload link both rendered the browser's
        // own error page, which is on no design system at all.
        "/status/404"
    ];

    public static TheoryData<string> AuthenticatedRoutes
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var route in AuthenticatedRouteList)
            {
                data.Add(route);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(AuthenticatedRoutes))]
    public async Task RealAuthenticatedRouteHasNoAxeViolationsAndNoInlineStyleAttribute(string route)
    {
        await using var support = await BrowserTestSupport.StartAsync();

        var response = await support.GoToAsync(route);

        Assert.Equal(200, response.Status);
        Assert.Empty(await support.FindAccessibilityViolationIdsAsync());
        Assert.Equal(1, await support.Page.Locator("main").CountAsync());
        Assert.Equal(1, await support.Page.Locator("h1").CountAsync());

        // The production CSP (default-src 'self', no style-src) discards
        // inline style attributes, so any styling delivered through one works
        // locally and silently vanishes on the deployed site — that is how
        // the sprite's inline display:none shipped a ~1,900px blank band on
        // every page. The invariant is mechanical: server markup never
        // carries a style attribute; everything styles through site.css.
        // The one allowed [style] carrier is the validation-summary tag
        // helper's valid-state <li> placeholder — framework output we cannot
        // author away; site.css hides its .validation-summary-valid parent so
        // the discarded inline style has nothing left to break. Error-state
        // summaries carry no such placeholder, so they stay covered.
        var inlineStyled = await support.Page.EvaluateAsync<string[]>(
            "Array.from(document.querySelectorAll('[style]'))" +
            ".filter(element => !(element.tagName === 'LI' && element.closest('[data-valmsg-summary].validation-summary-valid')))" +
            ".map(element => element.tagName + '.' + element.getAttribute('class'))");
        Assert.Empty(inlineStyled);
        Assert.Equal(
            "none",
            await support.Page.EvaluateAsync<string>(
                "getComputedStyle(document.querySelector('svg.sprite-sheet')).display"));
        // The blank-band guard. On an application screen the rail (a header
        // inside .app-shell) is the first thing rendered and belongs at the
        // very top; the external frame is deliberately navless and centres
        // its card, so the same assertion there would be asserting the
        // opposite of the design. What must hold on both is that nothing
        // renders a tall empty band above the content, which the sprite
        // assertion above already covers, plus: the content is inside the
        // viewport without scrolling.
        Assert.True(await support.Page.EvaluateAsync<bool>(
            "(() => {"
            + "  const rail = document.querySelector('.app-shell > .app-rail');"
            + "  if (rail) { return rail.getBoundingClientRect().top < 10; }"
            + "  const card = document.querySelector('.auth-card');"
            + "  return card !== null && card.getBoundingClientRect().top < window.innerHeight;"
            + "})()"));
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
            new PageGetByRoleOptions { Name = "Work Centre", Exact = true }).IsVisibleAsync());
        // The Work Centre port (wave 2) retired the legacy metric__value
        // class from the page body, so the ported vocabulary is the only
        // spelling this page may carry.
        Assert.True(await support.Page.Locator(".metric .metric-value").First.IsVisibleAsync());
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
    public async Task MetricStateIsNotCommunicatedByColourAlone()
    {
        await using var support = await BrowserTestSupport.StartAsync();
        await support.GoToAsync("/");

        // A metric's tone is a second signal only. Each tile carries its own
        // label and its own number, so removing every colour from the page
        // loses nothing an operator needs.
        var metrics = support.Page.Locator(".metric");
        var count = await metrics.CountAsync();
        Assert.True(count > 0);

        for (var index = 0; index < count; index++)
        {
            var text = await metrics.Nth(index).InnerTextAsync();
            Assert.Matches(@"\d", text);
            Assert.Matches(@"[A-Za-z]", text);
        }
    }

    private static Task<bool> HasHorizontalOverflowAsync(IPage page) =>
        page.EvaluateAsync<bool>("document.documentElement.scrollWidth > window.innerWidth");
}
