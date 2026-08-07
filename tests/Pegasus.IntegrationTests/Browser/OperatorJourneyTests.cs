using Microsoft.Playwright;

namespace Pegasus.IntegrationTests.Browser;

[Trait("Category", "SqlServer")]
[Trait("Category", "Browser")]
public sealed class OperatorJourneyTests
{
    [Fact]
    public async Task OperationsFirstJourneyUsesAuthenticatedRealHttpRoutes()
    {
        await using var support = await BrowserTestSupport.StartAsync();

        var operationsResponse = await support.GoToAsync("/");

        Assert.Equal(200, operationsResponse.Status);
        Assert.Equal(
            "Dashboard",
            await support.Page.GetByRole(
                AriaRole.Heading,
                new PageGetByRoleOptions { Name = "Dashboard", Exact = true }).InnerTextAsync());
        Assert.Contains(
            "development-offline-administrator",
            await support.Page.Locator("[aria-label='User']").InnerTextAsync(),
            StringComparison.Ordinal);

        var navigation = await support.Page.Locator("nav[aria-label='Primary']").InnerTextAsync();
        // The navigation speaks the business's language, not the pipeline's:
        // "Intake" was internal vocabulary for what the office calls the Inbox,
        // and "Triage" is a reserved business term that was being spent on a
        // screen which is not about Triage-type work at all.
        AssertOrdered(
            navigation,
            "Dashboard",
            "Inbox",
            "Upload",
            "Queues",
            "Cases",
            "Administration",
            "development-offline-administrator");

        // The three sections an operator actually opens this screen to read.
        // Lowercased because the section labels are uppercased by the
        // stylesheet, so the rendered text is the styling, not the copy.
        var dashboard = (await support.Page.Locator("main").InnerTextAsync()).ToLowerInvariant();
        AssertOrdered(dashboard, "active cases", "e-mail activity", "today and this week");

        // Every metric opens the exact filtered list behind it. Review is the
        // case stage, and the tile is backed by a count of cases in it — it
        // used to render an intake-receipt count and link into the intake
        // queue, which is a different entity on a different screen.
        await support.Page.Locator(".metric-strip a.metric", new PageLocatorOptions { HasText = "Review" }).ClickAsync();
        Assert.Equal("/Triage?queue=review", new Uri(support.Page.Url).PathAndQuery);

        await support.GoToAsync("/");
        await support.Page.Locator(".metric-strip a.metric", new PageLocatorOptions { HasText = "Needs sorting" }).ClickAsync();
        Assert.Equal("/Received?decision=needs_sorting", new Uri(support.Page.Url).PathAndQuery);
    }

    [Fact]
    public async Task UnimplementedAndExternalBoundariesAreObservableAndFailClosed()
    {
        await using var support = await BrowserTestSupport.StartAsync();
        await support.GoToAsync("/");

        // The invariant is now the opposite of what it was. This screen used to
        // ship nine tiles and two cards hardcoded to the literal string
        // "Unavailable", so a first-run operator met a wall of failure chrome
        // on a healthy system. A tile whose query does not exist is not
        // shipped; every tile that is shipped renders a number, and 0 is a
        // number.
        Assert.Equal(0, await support.Page.Locator("[data-queue-state='unavailable']").CountAsync());
        var metricValues = await support.Page.Locator(".metric .metric__value").AllInnerTextsAsync();
        Assert.NotEmpty(metricValues);
        Assert.All(metricValues, value => Assert.Matches(@"^\d+$", value.Trim()));

        var unknownRequest = await support.GoToAsync("/Uploads/not-an-accepted-token");
        Assert.Equal(404, unknownRequest.Status);

        var unknownEvaHandoff = await support.GoToAsync($"/Received/EvaHandoff/{Guid.NewGuid():D}");
        Assert.Equal(404, unknownEvaHandoff.Status);
    }

    [Fact]
    public async Task KeyboardJourneyExposesSkipLinkAndVisibleFocus()
    {
        await using var support = await BrowserTestSupport.StartAsync();
        await support.GoToAsync("/");

        await support.Page.Keyboard.PressAsync("Tab");
        var skipLink = support.Page.Locator(".skip-link");
        await Assertions.Expect(skipLink).ToBeFocusedAsync();
        Assert.True(await skipLink.IsVisibleAsync());

        await support.Page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(support.Page.Locator("#main-content")).ToBeFocusedAsync();
    }

    private static void AssertOrdered(string value, params string[] fragments)
    {
        var previous = -1;
        foreach (var fragment in fragments)
        {
            var current = value.IndexOf(fragment, StringComparison.Ordinal);
            Assert.True(current > previous, $"Expected '{fragment}' after the prior navigation item in '{value}'.");
            previous = current;
        }
    }
}
