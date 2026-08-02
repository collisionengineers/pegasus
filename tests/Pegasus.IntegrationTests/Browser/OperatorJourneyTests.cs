using Microsoft.Playwright;

namespace Pegasus.IntegrationTests.Browser;

[Collection(LocalDbFixtureDefinition.Name)]
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
            "Operations",
            await support.Page.GetByRole(
                AriaRole.Heading,
                new PageGetByRoleOptions { Name = "Operations", Exact = true }).InnerTextAsync());
        Assert.Contains(
            "development-offline-administrator",
            await support.Page.Locator("[aria-label='User']").InnerTextAsync(),
            StringComparison.Ordinal);

        var navigation = await support.Page.Locator("nav[aria-label='Primary']").InnerTextAsync();
        AssertOrdered(
            navigation,
            "Operations",
            "Intake",
            "Triage",
            "Cases",
            "Administration",
            "Search",
            "development-offline-administrator");

        var boundary = await support.Page.Locator(".acceptance-boundary").InnerTextAsync();
        Assert.Contains("local workflow evidence only", boundary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not prove production behavior", boundary, StringComparison.Ordinal);

        await support.Page.Locator("a.queue-card", new PageLocatorOptions { HasText = "Review" }).ClickAsync();
        Assert.Equal("/Intake?decision=draft_ready", new Uri(support.Page.Url).PathAndQuery);
        Assert.Contains(
            "Instruction drafts",
            await support.Page.Locator("main").InnerTextAsync(),
            StringComparison.Ordinal);

        await support.GoToAsync("/");
        await support.Page.Locator("a.queue-card", new PageLocatorOptions { HasText = "Triage" }).ClickAsync();
        Assert.Equal("/Triage", new Uri(support.Page.Url).AbsolutePath.TrimEnd('/'));
        Assert.Contains(
            "No triage records match this view.",
            await support.Page.Locator("main").InnerTextAsync(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnimplementedAndExternalBoundariesAreObservableAndFailClosed()
    {
        await using var support = await BrowserTestSupport.StartAsync();
        await support.GoToAsync("/");

        var unavailableQueues = support.Page.Locator("[data-queue-state='unavailable']");
        Assert.True(await unavailableQueues.CountAsync() >= 8);
        var unavailableText = await unavailableQueues.AllInnerTextsAsync();
        Assert.All(unavailableText, text => Assert.Contains("unavailable", text, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            unavailableText,
            text => text.Contains('0'));

        var unknownRequest = await support.GoToAsync("/Uploads/not-an-accepted-token");
        Assert.Equal(404, unknownRequest.Status);

        var unknownEvaHandoff = await support.GoToAsync($"/Intake/EvaHandoff/{Guid.NewGuid():D}");
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
