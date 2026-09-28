using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

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
                ("guideEntries[0].RetailValue", "12500.00"),
                ("guideEntries[0].TradeValue", "10250.00"),
                ("guideEntries[1].Source", nameof(ValuationSource.Brego)),
                ("guideEntries[1].GuideMonth", ""),
                ("guideEntries[1].RetailValue", ""),
                ("guideEntries[1].TradeValue", "")));

        AssertPrg(response, store.CaseId);
        var saved = Assert.Single(store.Saves);
        Assert.True(saved.Actor.IsInRole(role));
        AssertLeasedMutation(workspace, saved, DetailsModelOperationKey, "Recorded the Glass's figure", before);
        // A card alone records no calculation and no values: none was posted.
        Assert.Null(saved.Valuation!.Adoption);
        Assert.Null(saved.Valuation.AssessmentFields);
        var card = Assert.Single(saved.Valuation.GuideEntries!);
        Assert.Equal(ValuationSource.Glasses, card.Source);
        Assert.Null(card.Mileage);
        Assert.Equal(12_500m, card.RetailValue);
        Assert.Equal(10_250m, card.TradeValue);
        Assert.Equal(new DateOnly(2031, 5, 1), card.GuideMonth);
        var today = Pegasus.Core.LondonCalendar.DateAt(DateTimeOffset.UtcNow);
        Assert.InRange(card.Date, today.AddDays(-1), today.AddDays(1));
        Assert.Contains("data-case-editing=\"true\"", await workspace.GetWorkspaceAsync(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Retail, Trade and Engineer's value are boxes of the Case form
    /// (operator, 26 September 2026): typed with no mileage and no card, the
    /// Save carries them as the Valuation section's own fields, with no
    /// calculation, and a box emptied is carried as a clear.
    /// </summary>
    [Fact]
    public async Task TheCaseSaveCarriesTheThreeTypedValues()
    {
        var store = new RecordingCaseDetailsStore
        {
            AcceptWorkspaceSaves = true,
            State = CaseLifecycleState.ReportPreparation
        };
        using var workspace = await EnterEngineerEditModeAsync(
            store,
            services => Substitute<ISaveCaseWorkspace>(services, store));

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(
                DetailsModelOperationKey,
                "Typed the values",
                (Pegasus.Web.Presentation.CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.ValueRetail), "12500.00"),
                (Pegasus.Web.Presentation.CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.ValueTrade), ""),
                (Pegasus.Web.Presentation.CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.ValueEngineer), "12750.00")));

        AssertPrg(response, store.CaseId);
        var valuation = Assert.Single(store.Saves).Valuation!;
        Assert.Null(valuation.Adoption);
        Assert.Empty(valuation.GuideEntries!);
        Assert.Equal("12500.00", valuation.AssessmentFields![AssessmentVocabulary.ValueRetail]);
        Assert.True(string.IsNullOrEmpty(valuation.AssessmentFields[AssessmentVocabulary.ValueTrade]));
        Assert.Equal("12750.00", valuation.AssessmentFields[AssessmentVocabulary.ValueEngineer]);
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
