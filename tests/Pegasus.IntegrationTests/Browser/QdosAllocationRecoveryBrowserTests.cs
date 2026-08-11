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
        await using var support = await BrowserTestSupport.StartAsync(
            useIntegrationTestAuthentication: true);
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            support.Services,
            CaseType.Inspection,
            "NOTACTIVE");
        await using (var scope = support.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAllocateIntake>()
                .AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());
        }

        await support.GoToAsync("/Received");
        var strandedRow = support.Page.Locator("tbody tr", new() { HasText = "Case not created" });
        Assert.True(await strandedRow.IsVisibleAsync());
        Assert.DoesNotContain(
            receipt.Id.ToString("D"),
            await strandedRow.InnerTextAsync(),
            StringComparison.OrdinalIgnoreCase);

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

        // A valid form submitted by an authenticated but roleless principal is
        // denied before allocation and leaves the durable failure untouched.
        await support.Context.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
        {
            ["X-Test-Roleless"] = "1"
        });
        var deniedReason = support.Page.GetByLabel("Reason for retrying case creation");
        await deniedReason.FillAsync("This roleless retry must be denied.");
        var deniedResponse = support.Page.WaitForResponseAsync(response =>
            response.Url.Contains("/Received/", StringComparison.OrdinalIgnoreCase)
            && response.Request.Method == "POST");
        await support.Page.GetByRole(
            AriaRole.Button,
            new PageGetByRoleOptions { Name = "Retry case creation" }).ClickAsync();
        Assert.Equal(403, (await deniedResponse).Status);
        await support.Context.SetExtraHTTPHeadersAsync(new Dictionary<string, string>());
        await support.GoToAsync($"/Received/{receipt.Id:D}");

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
        var retryForm = correctedRetry.Locator("xpath=ancestor::form");
        var replayAction = Assert.IsType<string>(await retryForm.GetAttributeAsync("action"));
        var replayFields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await retryForm
                .Locator("input[name='__RequestVerificationToken']").InputValueAsync(),
            ["expectedVersion"] = await retryForm.Locator("input[name='expectedVersion']").InputValueAsync(),
            ["expectedAttemptId"] = await retryForm.Locator("input[name='expectedAttemptId']").InputValueAsync(),
            ["operationKey"] = await retryForm.Locator("input[name='operationKey']").InputValueAsync(),
            ["reason"] = await correctedReason.InputValueAsync()
        };
        await correctedRetry.FocusAsync();
        var successNavigation = support.Page.WaitForURLAsync("**/Cases/**");
        await correctedRetry.PressAsync("Enter");
        await successNavigation;
        var firstSuccessUrl = support.Page.Url;
        var caseId = Guid.Parse(new Uri(firstSuccessUrl).Segments.Last().Trim('/'));
        var caseReference = (await support.Page.GetByRole(
            AriaRole.Heading,
            new PageGetByRoleOptions { Level = 1 }).InnerTextAsync()).Trim();
        Assert.True(
            new Uri(support.Page.Url).AbsolutePath.Contains("/Cases/", StringComparison.OrdinalIgnoreCase),
            await support.Page.Locator("main").InnerTextAsync());
        Assert.DoesNotContain(
            "Retry case creation",
            await support.Page.Locator("main").InnerTextAsync(),
            StringComparison.OrdinalIgnoreCase);

        // Re-submit the exact successful form from the same authenticated
        // browser context. Fetch follows the replay redirect, which must resolve
        // to the same immutable Case without presenting a second success.
        var replayUrl = await support.Page.EvaluateAsync<string>(
            """
            async request => {
                const response = await fetch(request.action, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                    body: new URLSearchParams(request.fields)
                });
                return response.url;
            }
            """,
            new { action = replayAction, fields = replayFields });
        Assert.Equal(firstSuccessUrl, replayUrl);

        await support.GoToAsync($"/Received/{receipt.Id:D}");
        var successReceiptText = await support.Page.Locator("main").InnerTextAsync();
        Assert.Contains(caseReference, successReceiptText, StringComparison.Ordinal);
        Assert.DoesNotContain(receipt.Id.ToString("D"), successReceiptText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(caseId.ToString("D"), successReceiptText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Retry case creation", successReceiptText, StringComparison.OrdinalIgnoreCase);

        await support.GoToAsync("/Received");
        var successRow = support.Page.Locator("tbody tr", new() { HasText = caseReference });
        Assert.True(await successRow.IsVisibleAsync());
        var successRowText = await successRow.InnerTextAsync();
        Assert.DoesNotContain(receipt.Id.ToString("D"), successRowText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(caseId.ToString("D"), successRowText, StringComparison.OrdinalIgnoreCase);
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
