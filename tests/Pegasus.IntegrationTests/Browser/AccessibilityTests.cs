using Microsoft.Playwright;

namespace Pegasus.IntegrationTests.Browser;

[Collection(LocalDbFixtureDefinition.Name)]
[Trait("Category", "SqlServer")]
[Trait("Category", "Browser")]
public sealed class AccessibilityTests
{
    public static TheoryData<string> AuthenticatedRoutes => new()
    {
        "/",
        "/Intake",
        "/Triage",
        "/Administration",
        "/Account/PasswordChange",
        "/Cases"
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

    private static Task<bool> HasHorizontalOverflowAsync(IPage page) =>
        page.EvaluateAsync<bool>("document.documentElement.scrollWidth > window.innerWidth");
}
