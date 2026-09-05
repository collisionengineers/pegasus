using Microsoft.Playwright;

namespace Pegasus.IntegrationTests.Browser;

[Trait("Category", "SqlServer")]
[Trait("Category", "Browser")]
public sealed class InspectionAddressChoiceBrowserTests
{
    [Fact]
    public async Task RecordedImageBasedAndManualChoicesUpdateTheInspectionAddress()
    {
        await using var support = await BrowserTestSupport.StartAsync(
            useIntegrationTestAuthentication: true);
        await support.Page.RouteAsync("**/inspection-address-choice-probe", route =>
            route.FulfillAsync(new()
            {
                ContentType = "text/html",
                Body =
                    """
                    <form id="case-edit-form">
                      <input name="inspectionMode" value="PhysicalAddress">
                    </form>
                    <select data-inspection-address-choice>
                      <option value="ClaimantAddress" data-address="1 Claimant Road">Claimant address</option>
                      <option value="ImageBasedAssessment" data-address="Image Based Assessment">Image Based Assessment</option>
                      <option value="ManualEntry">Manual entry</option>
                    </select>
                    <div data-inspection-address-field>
                      <input data-inspection-address-input value="Typed address">
                    </div>
                    <div data-inspection-provider-default hidden>Provider default</div>
                    <script src="/js/site.js"></script>
                    """
            }));

        await support.GoToAsync("/inspection-address-choice-probe");
        var select = support.Page.Locator("[data-inspection-address-choice]");
        var address = support.Page.Locator("[data-inspection-address-input]");
        var mode = support.Page.Locator("input[name=inspectionMode]");
        var physicalField = support.Page.Locator("[data-inspection-address-field]");
        var providerDefault = support.Page.Locator("[data-inspection-provider-default]");

        await select.SelectOptionAsync("ClaimantAddress");
        Assert.Equal("1 Claimant Road", await address.InputValueAsync());
        Assert.Equal("PhysicalAddress", await mode.InputValueAsync());

        await select.SelectOptionAsync("ImageBasedAssessment");
        Assert.Equal("Image Based Assessment", await address.InputValueAsync());
        Assert.Equal("ImageBasedAssessment", await mode.InputValueAsync());
        Assert.True(await physicalField.IsHiddenAsync());
        Assert.True(await providerDefault.IsVisibleAsync());

        await select.SelectOptionAsync("ManualEntry");
        Assert.Equal(string.Empty, await address.InputValueAsync());
        Assert.Equal(string.Empty, await mode.InputValueAsync());
        Assert.True(await physicalField.IsVisibleAsync());
        await address.FillAsync("9 Manual Close");
        Assert.Equal("PhysicalAddress", await mode.InputValueAsync());

        await select.SelectOptionAsync("ClaimantAddress");
        await select.SelectOptionAsync("ManualEntry");
        Assert.Equal("1 Claimant Road", await address.InputValueAsync());
    }
}
