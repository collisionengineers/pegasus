using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The guide source cards on the one Case save (23 September 2026): a card has
/// no Save of its own, its boxes belong to the Case form, and the ribbon Save
/// carries a changed card to the workspace save as a guide entry.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseValuationWebTests
{
    /// <summary>
    /// A typed card reaches the one workspace save inside the leased envelope,
    /// stamped with the moment of the save; a card left blank records nothing.
    /// </summary>
    [Theory]
    [InlineData(StaffRole.Administrator)]
    [InlineData(StaffRole.Engineer)]
    [InlineData(StaffRole.User)]
    public async Task TheCaseSaveCarriesAChangedGuideCard(StaffRole role)
    {
        var store = new RecordingCaseDetailsStore { AcceptWorkspaceSaves = true };
        using var workspace = await EnterEngineerEditModeAsync(
            store,
            services => Substitute<ISaveCaseWorkspace>(services, store),
            role);
        var before = store.CaseVersion;

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(
                DetailsModelOperationKey,
                "Recorded the Glass's figure",
                ("guideEntries[0].Source", nameof(ValuationSource.Glasses)),
                ("guideEntries[0].GuideMonth", "2031-05"),
                ("guideEntries[0].Mileage", "42000"),
                ("guideEntries[0].RetailValue", "12500.00"),
                ("guideEntries[0].TradeValue", "10250.00"),
                ("guideEntries[1].Source", nameof(ValuationSource.Brego)),
                ("guideEntries[1].GuideMonth", ""),
                ("guideEntries[1].Mileage", ""),
                ("guideEntries[1].RetailValue", ""),
                ("guideEntries[1].TradeValue", "")));

        AssertPrg(response, store.CaseId);
        var saved = Assert.Single(store.Saves);
        Assert.True(saved.Actor.IsInRole(role));
        AssertLeasedMutation(workspace, saved, DetailsModelOperationKey, "Recorded the Glass's figure", before);
        // A card alone adopts nothing: no calculation was posted.
        Assert.Null(saved.Valuation!.Adoption);
        var card = Assert.Single(saved.Valuation.GuideEntries!);
        Assert.Equal(ValuationSource.Glasses, card.Source);
        Assert.Equal(42_000, card.Mileage);
        Assert.Equal(12_500m, card.RetailValue);
        Assert.Equal(10_250m, card.TradeValue);
        Assert.Equal(new DateOnly(2031, 5, 1), card.GuideMonth);
        var today = Pegasus.Core.LondonCalendar.DateAt(DateTimeOffset.UtcNow);
        Assert.InRange(card.Date, today.AddDays(-1), today.AddDays(1));
        Assert.Contains("data-case-editing=\"true\"", await workspace.GetWorkspaceAsync(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A card saves whatever was entered (operator, 23 September 2026): any of
    /// its boxes may be left blank, and a blank box is recorded as absent.
    /// </summary>
    [Fact]
    public async Task APartlyFilledGuideCardSavesWhatWasEntered()
    {
        var store = new RecordingCaseDetailsStore { AcceptWorkspaceSaves = true };
        using var workspace = await EnterEngineerEditModeAsync(
            store,
            services => Substitute<ISaveCaseWorkspace>(services, store));

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(
                DetailsModelOperationKey,
                "Part of a card",
                ("guideEntries[0].Source", nameof(ValuationSource.SuperCap)),
                ("guideEntries[0].GuideMonth", ""),
                ("guideEntries[0].Mileage", ""),
                ("guideEntries[0].RetailValue", "12500.00"),
                ("guideEntries[0].TradeValue", "")));

        AssertPrg(response, store.CaseId);
        var card = Assert.Single(Assert.Single(store.Saves).Valuation!.GuideEntries!);
        Assert.Equal(ValuationSource.SuperCap, card.Source);
        Assert.Equal(12_500m, card.RetailValue);
        Assert.Null(card.TradeValue);
        Assert.Null(card.Mileage);
        Assert.Null(card.GuideMonth);
    }
}
