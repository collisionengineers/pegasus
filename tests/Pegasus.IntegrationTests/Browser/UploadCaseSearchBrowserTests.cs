using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using System.Text.Json;

namespace Pegasus.IntegrationTests.Browser;

/// <summary>
/// INTK-016: the confirmation surface's "Add to an existing case" search as a
/// real keyboard-driven combobox — the ARIA wiring script adds, arrow-key
/// navigation, selection, and the completed staff decision — through the real
/// browser against the real Web host.
/// </summary>
[Trait("Category", "SqlServer")]
[Trait("Category", "Browser")]
public sealed class UploadCaseSearchBrowserTests
{
    [Fact]
    public async Task CaseSearchComboboxIsKeyboardOperableAndCompletesTheAttachDecision()
    {
        await using var support = await BrowserTestSupport.StartAsync(
            useIntegrationTestAuthentication: true);
        var caseId = await SeedSearchableCaseAsync(support.Services, "BRWS01");
        var processedReceiptId = await UploadAndOpenAttachAsync(support);

        var input = support.Page.Locator("[data-case-search-input]");
        Assert.Equal("combobox", await input.GetAttributeAsync("role"));
        Assert.Equal("false", await input.GetAttributeAsync("aria-expanded"));
        Assert.NotNull(await input.GetAttributeAsync("aria-controls"));

        var searchResponse = support.Page.WaitForResponseAsync(response =>
            response.Request.Method == "GET"
            && response.Url.Contains("handler=CaseSearch", StringComparison.Ordinal)
            && response.Url.Contains($"receiptId={processedReceiptId:D}", StringComparison.Ordinal)
            && response.Url.Contains("term=BRWS", StringComparison.Ordinal));
        await input.FillAsync("BRWS");
        var list = support.Page.Locator("[data-case-search-list]");
        await list.Locator("[role=option]").First.WaitForAsync();
        var response = await searchResponse;
        using var results = JsonDocument.Parse(await response.TextAsync());
        var selectedCaseVersion = results.RootElement[0].GetProperty("version").GetInt64().ToString();
        Assert.Equal("true", await input.GetAttributeAsync("aria-expanded"));

        await input.PressAsync("ArrowDown");
        var active = list.Locator("[role=option].is-active");
        Assert.Equal("true", await active.GetAttributeAsync("aria-selected"));
        Assert.Equal(
            await active.GetAttributeAsync("id"),
            await input.GetAttributeAsync("aria-activedescendant"));

        await input.PressAsync("Enter");
        Assert.Equal("BRWS01", await input.InputValueAsync());
        Assert.Equal(
            caseId.ToString("D"),
            await support.Page.Locator("[data-case-search-value]").InputValueAsync());
        Assert.Equal(
            selectedCaseVersion,
            await support.Page.Locator("[data-case-search-version]").InputValueAsync());
        Assert.True(await list.IsHiddenAsync());
        Assert.Equal("false", await input.GetAttributeAsync("aria-expanded"));

        Assert.Empty(await support.FindAccessibilityViolationIdsAsync());
        await AssertReceiptAssociationAsync(support.Services, processedReceiptId, expectedCaseId: null);

        await support.Page.Locator("[data-case-search] textarea[name=reason]").FillAsync(
            "Staff matched the document to the existing case in the browser journey.");
        await support.Page.Locator("[data-case-search] button[type=submit]").ClickAsync();
        await support.Page.WaitForURLAsync("**/Upload/Status/**");

        var confirmation = support.Page.Locator("[data-confirmation]");
        await confirmation.WaitForAsync();
        Assert.Contains("added to case BRWS01", await confirmation.TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains(
            "This was added to case BRWS01",
            await support.Page.ContentAsync(),
            StringComparison.Ordinal);
        await AssertReceiptAssociationAsync(support.Services, processedReceiptId, caseId);
        Assert.Empty(await support.FindAccessibilityViolationIdsAsync());
    }

    [Fact]
    public async Task CaseSearchDistinguishesAnHttpFailureAndIgnoresAStaleResponseAfterClear()
    {
        await using var support = await BrowserTestSupport.StartAsync(
            useIntegrationTestAuthentication: true);
        // Exercise the sequence guard directly: the fallback without an abort
        // controller leaves the old response in flight after the input clears.
        await support.Page.AddInitScriptAsync("window.AbortController = undefined;");
        var staleRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStaleResponse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var staleResponseFulfilled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await support.Page.RouteAsync("**/*handler=CaseSearch*", async route =>
        {
            if (route.Request.Url.Contains("term=EMPTY", StringComparison.Ordinal))
            {
                await route.FulfillAsync(new() { ContentType = "application/json", Body = "[]" });
                return;
            }
            if (route.Request.Url.Contains("term=FAILED", StringComparison.Ordinal))
            {
                await route.FulfillAsync(new() { Status = 503, ContentType = "application/json", Body = "{}" });
                return;
            }
            if (route.Request.Url.Contains("term=STALE", StringComparison.Ordinal))
            {
                staleRequest.TrySetResult();
                await releaseStaleResponse.Task;
                await route.FulfillAsync(new()
                {
                    ContentType = "application/json",
                    Body = "[{\"caseId\":\"d3c27ec8-1635-432d-a421-e7c08db53237\",\"reference\":\"STALE01\",\"registration\":\"AB12 CDE\",\"claimant\":\"Stale claimant\",\"stage\":\"Not ready\",\"version\":4}]"
                });
                staleResponseFulfilled.TrySetResult();
                return;
            }
            await route.ContinueAsync();
        });

        try
        {
            await UploadAndOpenAttachAsync(support);
            var input = support.Page.Locator("[data-case-search-input]");
            var list = support.Page.Locator("[data-case-search-list]");

            await input.FillAsync("EMPTY");
            await list.GetByText("No matching cases found", new() { Exact = true }).WaitForAsync();

            await input.FillAsync("FAILED");
            await list.GetByText(
                "Case search is unavailable. Type the exact reference or try again.",
                new() { Exact = true }).WaitForAsync();
            Assert.Equal(0, await list.Locator("[role=option]").CountAsync());

            await input.FillAsync("STALE");
            await staleRequest.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await input.FillAsync(string.Empty);
            Assert.Equal(string.Empty, await support.Page.Locator("[data-case-search-value]").InputValueAsync());
            Assert.Equal(string.Empty, await support.Page.Locator("[data-case-search-version]").InputValueAsync());
            Assert.True(await list.IsHiddenAsync());

            releaseStaleResponse.TrySetResult();
            await staleResponseFulfilled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await support.Page.WaitForTimeoutAsync(50);
            Assert.True(await list.IsHiddenAsync());
            Assert.Equal(0, await list.Locator("[role=option]").CountAsync());
            Assert.Equal("false", await input.GetAttributeAsync("aria-expanded"));
        }
        finally
        {
            releaseStaleResponse.TrySetResult();
        }
    }

    private static async Task<Guid> UploadAndOpenAttachAsync(BrowserTestSupport support)
    {
        var email = IntakeTestEvidence.CreateEmail(
            "browser-instruction.eml",
            "QDOS instruction\r\nClaimant Name: Browser Claimant\r\nClaim Number: BRWS-DOC-01\r\nVehicle Registration: CD34 EFG");
        await support.GoToAsync("/Upload");
        await support.Page.Locator("[data-dropzone] input[type=file]").SetInputFilesAsync(
            new FilePayload { Name = email.FileName, MimeType = email.MediaType, Buffer = email.Content });
        await support.Page.Locator("form[data-upload-progress] button[type=submit]").ClickAsync();
        await support.Page.WaitForURLAsync("**/Upload/Status/**");

        var stagedReceiptId = Guid.Parse(new Uri(support.Page.Url).AbsolutePath.Split('/').Last());
        await support.Page.GotoAsync("about:blank");
        await using (var scope = support.Services.CreateAsyncScope())
        {
            await IntakeWebDriver.DrainStagedAsync(scope.ServiceProvider, stagedReceiptId);
        }

        await support.GoToAsync($"/Upload/Status/{stagedReceiptId:D}");
        await support.Page.Locator("details.upload-attach > summary").ClickAsync();
        return Guid.Parse(await support.Page.Locator(
            "form[data-case-search] input[name=receiptId]").InputValueAsync());
    }

    private static async Task AssertReceiptAssociationAsync(
        IServiceProvider services,
        Guid receiptId,
        Guid? expectedCaseId)
    {
        await using var scope = services.CreateAsyncScope();
        var receipt = await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        Assert.NotNull(receipt);
        Assert.Equal(expectedCaseId, receipt.CurrentCaseId);
    }

    /// <summary>
    /// A directly seeded case reachable by the search: the origin receipt is
    /// real (the cases table enforces it), the case row and workflow state
    /// come from the existing seeding helper.
    /// </summary>
    private static async Task<Guid> SeedSearchableCaseAsync(
        IServiceProvider services,
        string reference)
    {
        Guid originReceiptId;
        await using (var scope = services.CreateAsyncScope())
        {
            var scopedServices = scope.ServiceProvider;
            var now = scopedServices.GetRequiredService<TimeProvider>().GetUtcNow();
            var email = IntakeTestEvidence.CreateEmail(
                "browser-case-origin.eml",
                $"QDOS instruction\r\nClaimant Name: Case Claimant\r\nClaim Number: {reference}\r\nVehicle Registration: AB12 CDE");
            var receipt = await scopedServices.GetRequiredService<ProcessIntake>()
                .ExecuteAsync(
                    new IntakeSource(
                        email.FileName,
                        email.MediaType,
                        email.Content,
                        now,
                        "browser-case-search-fixture",
                        new(IntakeSourceChannel.ManualUpload, $"browser-case-search:{Guid.NewGuid():N}")),
                    CancellationToken.None);
            originReceiptId = receipt.Id;
        }

        return await ImageIntakeTestData.SeedCaseAsync(
            services,
            originReceiptId,
            reference,
            nameof(CaseLifecycleState.NotReady));
    }
}
