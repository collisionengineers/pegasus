using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Estimate header offers Send to AI only once the Engineer's Value is
/// confirmed; before that the control is absent, not drawn locked with an
/// explanation (design authority, no explanatory copy; 15 September walk).
/// </summary>
public sealed partial class CaseDetailsWebTests
{
    [Fact]
    public async Task WithoutAConfirmedEngineersValueTheEstimateHeaderShowsNeitherSendToAiNorALockedPill()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEngineerEditModeAsync(store, _ => { });

        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=estimate");

        Assert.Contains("data-case-editing=\"true\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-estimate-send-to-ai", html, StringComparison.Ordinal);
        Assert.DoesNotContain(
            OperatorLabels.CaseWorkspace.EngineerSections.ConfirmedEngineerValueRequired,
            html,
            StringComparison.Ordinal);
    }
}
