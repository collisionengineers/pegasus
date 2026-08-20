using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

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

        // A real upload through the real page: an instruction document that
        // matches no case resolves to the open create-or-attach decision.
        var email = IntakeTestEvidence.CreateEmail(
            "browser-instruction.eml",
            "QDOS instruction\r\nClaimant Name: Browser Claimant\r\nClaim Number: BRWS-DOC-01\r\nVehicle Registration: CD34 EFG");
        await support.GoToAsync("/Upload");
        await support.Page.Locator("[data-dropzone] input[type=file]").SetInputFilesAsync(
            new FilePayload
            {
                Name = email.FileName,
                MimeType = email.MediaType,
                Buffer = email.Content
            });
        await support.Page.Locator("form[data-upload-progress] button[type=submit]").ClickAsync();
        await support.Page.WaitForURLAsync("**/Upload/Status/**");

        var stagedReceiptId = Guid.Parse(
            new Uri(support.Page.Url).AbsolutePath.Split('/').Last());
        await using (var scope = support.Services.CreateAsyncScope())
        {
            await IntakeWebDriver.DrainStagedAsync(scope.ServiceProvider, stagedReceiptId);
        }

        await support.GoToAsync($"/Upload/Status/{stagedReceiptId:D}");
        await support.Page.Locator("details.upload-attach > summary").ClickAsync();

        var input = support.Page.Locator("[data-case-search-input]");
        Assert.Equal("combobox", await input.GetAttributeAsync("role"));
        Assert.Equal("false", await input.GetAttributeAsync("aria-expanded"));
        Assert.NotNull(await input.GetAttributeAsync("aria-controls"));

        await input.FillAsync("BRWS");
        var list = support.Page.Locator("[data-case-search-list]");
        await list.Locator("[role=option]").First.WaitForAsync();
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
        Assert.True(await list.IsHiddenAsync());
        Assert.Equal("false", await input.GetAttributeAsync("aria-expanded"));

        Assert.Empty(await support.FindAccessibilityViolationIdsAsync());

        await support.Page.Locator("[data-case-search] textarea[name=reason]").FillAsync(
            "Staff matched the document to the existing case in the browser journey.");
        await support.Page.Locator("[data-case-search] button[type=submit]").ClickAsync();
        await support.Page.WaitForURLAsync("**/Upload/Status/**");

        var confirmation = support.Page.Locator(".status-card");
        await confirmation.WaitForAsync();
        Assert.Contains("added to case BRWS01", await confirmation.TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains(
            "This was added to case BRWS01",
            await support.Page.ContentAsync(),
            StringComparison.Ordinal);
        Assert.Empty(await support.FindAccessibilityViolationIdsAsync());
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
