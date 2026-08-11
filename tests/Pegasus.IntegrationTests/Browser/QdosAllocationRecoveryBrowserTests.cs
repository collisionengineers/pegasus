using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;

namespace Pegasus.IntegrationTests.Browser;

[Trait("Category", "SqlServer")]
[Trait("Category", "Browser")]
public sealed class QdosAllocationRecoveryBrowserTests
{
    [Fact]
    public async Task FailedAllocationShowsSafeRecoveryWithoutRawIdentifiers()
    {
        await using var support = await BrowserTestSupport.StartAsync();
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            support.Services,
            CaseType.Inspection,
            "NOTACTIVE");
        await using (var scope = support.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAllocateIntake>()
                .AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());
        }

        var response = await support.GoToAsync($"/Received/{receipt.Id:D}");

        Assert.Equal(200, response.Status);
        Assert.Equal(
            "Case not created",
            await support.Page.GetByRole(
                AriaRole.Heading,
                new PageGetByRoleOptions { Name = "Case not created", Exact = true })
                .Last.InnerTextAsync(),
            ignoreCase: true);
        Assert.True(await support.Page.GetByRole(
            AriaRole.Link,
            new PageGetByRoleOptions { Name = "Open Principal administration" })
            .IsVisibleAsync());
        Assert.True(await support.Page.GetByRole(
            AriaRole.Button,
            new PageGetByRoleOptions { Name = "Retry case creation" })
            .IsVisibleAsync());
        var visibleText = await support.Page.Locator("main").InnerTextAsync();
        Assert.DoesNotContain(receipt.Id.ToString("D"), visibleText, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await support.FindAccessibilityViolationIdsAsync());

        // Native required-field validation prevents an unreasoned retry.
        var retryReason = support.Page.GetByLabel("Reason for retrying case creation");
        var retryButton = support.Page.GetByRole(
            AriaRole.Button,
            new PageGetByRoleOptions { Name = "Retry case creation" });
        await retryButton.ClickAsync();
        Assert.False(await retryReason.EvaluateAsync<bool>("element => element.checkValidity()"));
        Assert.Contains("/Received/", support.Page.Url, StringComparison.OrdinalIgnoreCase);

        // A reasoned retry before correction fails visibly and stays on the
        // same receipt. Submit by keyboard so the recovery path is not
        // pointer-only.
        await retryReason.FillAsync("Retry before correcting the Principal.");
        await retryButton.FocusAsync();
        await retryButton.PressAsync("Enter");
        Assert.Contains(
            "selected Principal is not available",
            await support.Page.Locator("main").InnerTextAsync(),
            StringComparison.OrdinalIgnoreCase);

        // Correct the real prerequisite through the existing administration
        // screens, then perform a new reasoned retry.
        var organizationName = $"Recovery provider {Guid.NewGuid():N}";
        await support.GoToAsync("/Administration/Organizations");
        await support.Page.GetByLabel("Organization name").FillAsync(organizationName);
        await support.Page.GetByLabel("Work Provider", new() { Exact = true }).CheckAsync();
        await support.Page.GetByRole(
            AriaRole.Button,
            new PageGetByRoleOptions { Name = "Create organization" }).ClickAsync();
        var organizationRow = support.Page.Locator("tbody tr", new() { HasText = organizationName });
        await organizationRow.GetByRole(
            AriaRole.Link,
            new() { Name = "Create principal" }).ClickAsync();
        await support.Page.GetByLabel("Principal code").FillAsync("NOTACTIVE");
        await support.Page.GetByRole(
            AriaRole.Button,
            new PageGetByRoleOptions { Name = "Create principal" }).ClickAsync();
        Assert.Contains(
            "principal was created",
            await support.Page.Locator("main").InnerTextAsync(),
            StringComparison.OrdinalIgnoreCase);

        await support.GoToAsync($"/Received/{receipt.Id:D}");
        var correctedReason = support.Page.GetByLabel("Reason for retrying case creation");
        await correctedReason.FillAsync("Principal created and allocation reviewed.");
        var correctedRetry = support.Page.GetByRole(
            AriaRole.Button,
            new PageGetByRoleOptions { Name = "Retry case creation" });
        await correctedRetry.FocusAsync();
        var successNavigation = support.Page.WaitForURLAsync("**/Cases/**");
        await correctedRetry.PressAsync("Enter");
        await successNavigation;
        Assert.True(
            new Uri(support.Page.Url).AbsolutePath.Contains("/Cases/", StringComparison.OrdinalIgnoreCase),
            await support.Page.Locator("main").InnerTextAsync());
        Assert.DoesNotContain(
            "Retry case creation",
            await support.Page.Locator("main").InnerTextAsync(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TypedStandaloneAuditOpensWithEvidenceControlsReachable()
    {
        await using var support = await BrowserTestSupport.StartAsync(javaScriptEnabled: false);
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            support.Services,
            CaseType.Audit,
            "NOTACTIVE");

        var response = await support.GoToAsync($"/Cases/Create?receiptId={receipt.Id:D}");

        Assert.Equal(200, response.Status);
        Assert.Equal("Audit", await support.Page.Locator("[data-case-type-selector]").InputValueAsync());
        Assert.True(await support.Page.Locator("[data-standalone-audit-fields]").IsVisibleAsync());
    }

    [Fact]
    public async Task CaseTypeSelectorProgressivelyDisclosesStandaloneAuditEvidence()
    {
        await using var support = await BrowserTestSupport.StartAsync();
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            support.Services,
            CaseType.Inspection,
            "NOTACTIVE");
        await support.GoToAsync($"/Cases/Create?receiptId={receipt.Id:D}");
        var selector = support.Page.Locator("[data-case-type-selector]");
        var fields = support.Page.Locator("[data-standalone-audit-fields]");

        Assert.False(await fields.IsVisibleAsync());
        await selector.SelectOptionAsync("Audit");
        Assert.True(await fields.IsVisibleAsync());
        await selector.SelectOptionAsync("Inspection");
        Assert.False(await fields.IsVisibleAsync());
    }
}
