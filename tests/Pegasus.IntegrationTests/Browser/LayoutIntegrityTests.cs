using Microsoft.Playwright;

namespace Pegasus.IntegrationTests.Browser;

/// <summary>
/// PLAT-029: the Integrated Operations Workspace shell at the three contract
/// widths (EPIC-011 §1.1) — 1580 (content cap), 1100 (three-pane reflow) and
/// 760 (single column). Every authenticated route must lay out without a
/// horizontal scrollbar, without clipping its own text, with exactly one
/// main and one h1, and with no inline style attribute for the production
/// CSP to discard.
/// </summary>
[Trait("Category", "SqlServer")]
[Trait("Category", "Browser")]
public sealed class LayoutIntegrityTests
{
    public static TheoryData<string, int> RoutesAndViewports()
    {
        var data = new TheoryData<string, int>();
        foreach (var route in AccessibilityTests.AuthenticatedRouteList)
        {
            foreach (var width in new[] { 1580, 1100, 760 })
            {
                data.Add(route, width);
            }
        }

        return data;
    }

    // Containers that scroll by design: a scrolling pane, a wide table, the
    // horizontal rail and tab strips, a textarea, a select, and anything a
    // page marks [data-allow-clip].
    private const string AllowedClipSelector =
        ".pane-scroll, .table-wrap, .primary-nav, .workspace-tabs, .tabs, .estimate-table, "
        + ".command-results, .report-preview, .row-excerpt, .ribbon-value, textarea, select, [data-allow-clip]";

    [Theory]
    [MemberData(nameof(RoutesAndViewports))]
    public async Task RouteLaysOutWithoutOverflowClippingOrInlineStyle(string route, int width)
    {
        await using var support = await BrowserTestSupport.StartAsync(width: width, height: 900);

        var response = await support.GoToAsync(route);
        Assert.Equal(200, response.Status);

        Assert.False(
            await support.Page.EvaluateAsync<bool>("document.documentElement.scrollWidth > window.innerWidth"),
            $"{route} at {width}px scrolls horizontally.");

        var clipped = await support.Page.EvaluateAsync<string[]>(
            "(allowed) => Array.from(document.querySelectorAll('body *'))"
            + ".filter(element => {"
            + "  if (element.closest(allowed)) { return false; }"
            + "  if (element.closest('[hidden], svg, .sprite-sheet, .sr-only, .skip-link, .dialog-backdrop')) { return false; }"
            + "  const style = getComputedStyle(element);"
            + "  const clips = ['hidden', 'clip'];"
            + "  if (!clips.includes(style.overflowX) && !clips.includes(style.overflowY)) { return false; }"
            + "  if (style.display === 'none' || style.visibility === 'hidden') { return false; }"
            + "  if (!element.textContent || !element.textContent.trim()) { return false; }"
            + "  return element.scrollWidth - element.clientWidth > 1 || element.scrollHeight - element.clientHeight > 1;"
            + "})"
            + ".map(element => element.tagName + '.' + (element.getAttribute('class') || '') + ' [' + element.textContent.trim().slice(0, 40) + ']')",
            AllowedClipSelector);
        Assert.Empty(clipped);

        Assert.Equal(1, await support.Page.Locator("main").CountAsync());
        Assert.Equal(1, await support.Page.Locator("h1").CountAsync());

        // The one allowed [style] carrier is the validation-summary tag
        // helper's valid-state <li> placeholder (see AccessibilityTests).
        var inlineStyled = await support.Page.EvaluateAsync<string[]>(
            "Array.from(document.querySelectorAll('[style]'))"
            + ".filter(element => !(element.tagName === 'LI' && element.closest('[data-valmsg-summary].validation-summary-valid')))"
            + ".map(element => element.tagName + '.' + element.getAttribute('class'))");
        Assert.Empty(inlineStyled);
    }
}
