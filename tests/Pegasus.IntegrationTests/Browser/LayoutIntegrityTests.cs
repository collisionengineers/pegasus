using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;

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

    // Containers that clip by design: the brand lockup (its decorative
    // texture bleeds past the edge), the legacy visually-hidden .vh, a
    // scrolling pane, a wide table, the horizontal rail and tab strips, a
    // textarea, a select, and anything a page marks [data-allow-clip].
    private const string AllowedClipSelector =
        ".brand, .vh, .pane-scroll, .table-wrap, .primary-nav, .workspace-tabs, .tabs, .estimate-table, "
        + ".command-results, .report-preview, .row-excerpt, .ribbon-value, .rail-user strong, .workspace-tab span, "
        + "textarea, select, [data-allow-clip]";

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

    /// <summary>
    /// CASE-038: the Case record is the one route the generic list cannot
    /// cover, because it needs a seeded case. At the three contract widths the
    /// single-scroll record lays out without overflow or clipping, its jump-nav
    /// moves the reader and the scroll-spy follows, and a section body below the
    /// fold mounts with its own controls bound.
    /// </summary>
    [Theory]
    [InlineData(1580)]
    [InlineData(1100)]
    [InlineData(760)]
    public async Task TheCaseRecordLaysOutAndScrollsAtEveryWidth(int width)
    {
        await using var support = await BrowserTestSupport.StartAsync(width: width, height: 900);
        var caseId = await SeedAcceptedCaseAsync(support.Services);

        var response = await support.GoToAsync($"/Cases/{caseId:D}");
        Assert.Equal(200, response.Status);

        Assert.False(
            await support.Page.EvaluateAsync<bool>(
                "document.documentElement.scrollWidth > window.innerWidth"),
            $"The Case record at {width}px scrolls horizontally.");
        Assert.Empty(await ClippedElementsAsync(support));
        Assert.Equal(1, await support.Page.Locator("main").CountAsync());
        Assert.Equal(1, await support.Page.Locator("h1").CountAsync());

        // D44: no staff-review control survives anywhere the frame renders.
        Assert.Equal(
            0,
            await support.Page.Locator(
                "[name*='ReviewedByStaff'], [name*='reviewedByStaff'], .staff-reviewed").CountAsync());

        // Eleven jump links over eleven hosts, and the first one is current
        // before anything is scrolled.
        Assert.Equal(11, await support.Page.Locator("[data-section-link]").CountAsync());
        Assert.Equal(11, await support.Page.Locator(".case-section").CountAsync());
        Assert.Equal("overview", await CurrentSectionAsync(support));

        // The jump-nav moves the reader and the scroll-spy follows it off
        // Overview. Which later section reads as current is a matter of where
        // the browser could stop scrolling at this width, so what is asserted
        // is that exactly one entry is current and it is no longer the first.
        await support.Page.Locator("[data-section-link='inspection']").ClickAsync();
        await support.Page.WaitForFunctionAsync(
            "() => { const links = document.querySelectorAll("
            + "  '[data-section-link][aria-current=\"true\"]');"
            + " return links.length === 1 && links[0].dataset.sectionLink !== 'overview'; }");
        Assert.NotEqual("overview", await CurrentSectionAsync(support));

        // Files is served below the fold as a fragment (the server marks it
        // `data-lazy`; CaseDetailsWebTests asserts that). However early the
        // reader reaches it, it ends up mounted with its own body.
        await support.Page.Locator("[data-section-link='files']").ClickAsync();
        await support.Page.Locator("#section-files:not([data-lazy])").WaitForAsync();
        Assert.True(await support.Page.Locator("#section-files .panel").CountAsync() > 0);

        // The mounted body's own controls are bound: the openers and evidence
        // triggers are bound by root, not once over the document at load, so a
        // section that arrives later still opens its dialogs and its viewer.
        Assert.Equal(
            0,
            await support.Page.EvaluateAsync<int>(
                "Array.from(document.querySelectorAll('[data-dialog-open]'))"
                + ".filter(control => document.querySelector("
                + "  '[data-dialog=' + JSON.stringify(control.getAttribute('data-dialog-open')) + ']'))"
                + ".filter(control => control.dataset.dialogOpenBound !== 'true').length"));
        Assert.Equal(
            0,
            await support.Page.EvaluateAsync<int>(
                "document.querySelectorAll('[data-evidence-item]:not([data-evidence-item-bound])').length"));

        Assert.Empty(await ClippedElementsAsync(support));
        Assert.False(
            await support.Page.EvaluateAsync<bool>(
                "document.documentElement.scrollWidth > window.innerWidth"),
            $"The Case record at {width}px scrolls horizontally after a section mounts.");

        // The record's only inline style is the sticky height its own script
        // measures; nothing is authored inline for the production CSP to drop.
        var inlineStyled = await support.Page.EvaluateAsync<string[]>(
            "Array.from(document.querySelectorAll('[style]'))"
            + ".filter(element => !element.classList.contains('record'))"
            + ".map(element => element.tagName + '.' + element.getAttribute('class'))");
        Assert.Empty(inlineStyled);
    }

    private static Task<string[]> ClippedElementsAsync(BrowserTestSupport support) =>
        support.Page.EvaluateAsync<string[]>(
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

    private static Task<string> CurrentSectionAsync(BrowserTestSupport support) =>
        support.Page.EvaluateAsync<string>(
            "(document.querySelector('[data-section-link][aria-current=\"true\"]')"
            + " || {}).dataset?.sectionLink ?? ''");

    /// <summary>
    /// The smallest accepted Case this scenario can render. Modelled on
    /// <c>OperatorJourneyTests.SeedCustodyRecoveryCaseAsync</c>, which is
    /// private to that class; this one seeds nothing the layout does not need.
    /// </summary>
    private static async Task<Guid> SeedAcceptedCaseAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var scoped = scope.ServiceProvider;
        var now = scoped.GetRequiredService<TimeProvider>().GetUtcNow();
        var email = IntakeTestEvidence.CreateEmail(
            $"case-record-layout-{Guid.NewGuid():N}.eml",
            "QDOS instruction\r\nClaimant Name: Layout Record\r\nClaim Number: LAY-001\r\n"
                + "Vehicle Registration: AB12 CDE");
        var receipt = await scoped.GetRequiredService<ProcessIntake>().ExecuteAsync(
            new(
                email.FileName,
                email.MediaType,
                email.Content,
                now,
                "case-record-layout",
                new(IntakeSourceChannel.ManualUpload, $"case-record-layout:{Guid.NewGuid():N}")),
            CancellationToken.None);
        await SeedPrincipalAsync(scoped, now);
        var accepted = await scoped.GetRequiredService<IAcceptIntake>().ExecuteAsync(
            new(
                receipt.Id,
                receipt.Version,
                ActionActor.SystemWorker("case-record-layout"),
                $"case-record-layout-accept:{Guid.NewGuid():N}",
                "The layout scenario's intake evidence is complete.",
                CaseType.Inspection,
                QdosPrincipal.Code,
                new(true, true, true, true)),
            CancellationToken.None);
        return accepted.Identity.CaseId;
    }

    private static async Task SeedPrincipalAsync(IServiceProvider services, DateTimeOffset now)
    {
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        if (await context.Principals.AnyAsync(
                item => item.Code == QdosPrincipal.Code && item.IsActive,
                CancellationToken.None))
        {
            return;
        }

        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {"Case record layout provider"}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO OrganizationRoles (OrganizationId, Role) VALUES ({organizationId}, {"work_provider"})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {now})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version) VALUES ({Guid.NewGuid()}, {organizationId}, {QdosPrincipal.Code}, {lineageId}, {true}, {0L})");
        await transaction.CommitAsync();
    }
}
