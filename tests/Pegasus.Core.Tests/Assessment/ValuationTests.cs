using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Assessment;

public sealed class ValuationTests
{
    private static readonly ActionActor Engineer =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
    private static readonly ActionActor User =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

    [Fact]
    public void SourceVocabularyIsClosed()
    {
        Assert.All(
            Enum.GetValues<ValuationSource>(),
            source => Assert.True(ValuationSources.IsSupported(source)));
        Assert.False(ValuationSources.IsSupported((ValuationSource)99));
        Assert.Equal(
            [ValuationSource.Glasses, ValuationSource.Cazana, ValuationSource.EngineersValue,
                ValuationSource.AiMarketResearch, ValuationSource.Brego, ValuationSource.SuperCap, ValuationSource.Cap],
            Enum.GetValues<ValuationSource>());
    }

    /// <summary>
    /// Collision Engineers reads the Glass's, Brego, Super CAP, CAP and Cazana
    /// guides and types the figure in; none of them is a live call here. AI
    /// market research is written only by the automation completion, so staff
    /// never record it.
    /// </summary>
    [Fact]
    public void OnlyTheTypedGuidesAndTheEngineersValueAreManuallyRecordable()
    {
        Assert.All(
            new[]
            {
                ValuationSource.Glasses,
                ValuationSource.Brego,
                ValuationSource.SuperCap,
                ValuationSource.Cap,
                ValuationSource.Cazana,
                ValuationSource.EngineersValue
            },
            source => Assert.True(ValuationPolicy.IsManuallyRecordable(source)));
        Assert.False(ValuationPolicy.IsManuallyRecordable(ValuationSource.AiMarketResearch));
    }

    /// <summary>
    /// The guide month is the month the figure was published, which is a
    /// different fact from the day it was recorded. It is held as the first
    /// day of that month so two cards for the same month compare as one
    /// value.
    /// </summary>
    [Fact]
    public void AGuideMonthIsHeldAsTheFirstDayOfItsMonth()
    {
        Assert.Throws<ArgumentException>(() =>
            ValuationPolicy.ValidateDetails(Details(guideMonth: new DateOnly(2030, 4, 2))));

        var details = Details(guideMonth: new DateOnly(2030, 4, 1));
        Assert.Equal(details, ValuationPolicy.ValidateDetails(details));
        Assert.Null(ValuationPolicy.ValidateDetails(Details()).GuideMonth);
    }

    [Fact]
    public void DetailsRequireSupportedSourceMileageAndPenceAmounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ValuationPolicy.ValidateDetails(Details(source: (ValuationSource)99)));
        Assert.Throws<ArgumentException>(() =>
            ValuationPolicy.ValidateDetails(Details(mileage: -1)));
        Assert.Throws<ArgumentException>(() =>
            ValuationPolicy.ValidateDetails(Details(retail: 1.001m)));
        Assert.Throws<ArgumentException>(() =>
            ValuationPolicy.ValidateDetails(Details(trade: -0.01m)));

        Assert.Equal(Details(), ValuationPolicy.ValidateDetails(Details()));
    }

    /// <summary>
    /// An Engineer's Value row is the entry surface of the confirmed
    /// assessment.values.engineer finding, so it is refused when it cannot be
    /// written to that field rather than persisted and silently dropped.
    /// </summary>
    [Fact]
    public void AnEngineersValueRowIsRefusedWhenItCannotBecomeTheAssessmentField()
    {
        Assert.Throws<ArgumentException>(() =>
            ValuationPolicy.ValidateDetails(
                Details(source: ValuationSource.EngineersValue, retail: 0m)));

        Assert.Equal(
            Details(source: ValuationSource.EngineersValue),
            ValuationPolicy.ValidateDetails(Details(source: ValuationSource.EngineersValue)));
    }

    /// <summary>
    /// One owner: the number the product consumes is the assessment field, and
    /// its value comes from the Engineer's Value row's retail figure,
    /// canonicalized by the assessment vocabulary rather than by a second
    /// format of this file's own.
    /// </summary>
    [Fact]
    public void EngineersValueFieldIsTheCanonicalizedRetailFigureAndNothingElseWritesIt()
    {
        Assert.Equal(
            "12000.00",
            ValuationPolicy.EngineersValueField(Details(source: ValuationSource.EngineersValue)));
        Assert.Equal(
            "12345.67",
            ValuationPolicy.EngineersValueField(
                Details(source: ValuationSource.EngineersValue, retail: 12345.67m)));
        Assert.Null(ValuationPolicy.EngineersValueField(Details(source: ValuationSource.Glasses)));
        Assert.Null(ValuationPolicy.EngineersValueField(Details(source: ValuationSource.Cazana)));
        Assert.Null(ValuationPolicy.EngineersValueField(Details(source: ValuationSource.AiMarketResearch)));
        Assert.Equal(
            "12000.00",
            AssessmentPolicy.NormalizeFieldValue(AssessmentVocabulary.ValueEngineer, "12000"));
    }

    /// <summary>
    /// Any staff role may record a guide card. The Engineer's Value is adopted
    /// by the Case save's Apply, which owns its own authority check.
    /// </summary>
    [Theory]
    [InlineData(StaffRole.Administrator)]
    [InlineData(StaffRole.Engineer)]
    [InlineData(StaffRole.User)]
    public void EveryStaffRoleRecordsGuideCards(StaffRole role)
    {
        var actor = ActionActor.Staff(Guid.NewGuid(), [role]);

        Assert.All(
            new[]
            {
                ValuationSource.Glasses,
                ValuationSource.Brego,
                ValuationSource.SuperCap,
                ValuationSource.Cazana
            },
            source =>
            {
                var details = Details(source, guideMonth: new DateOnly(2030, 4, 1));
                Assert.Equal(details, ValuationPolicy.ValidateGuideEntry(actor, details));
            });
        Assert.Throws<StaffAuthorizationException>(() =>
            ValuationPolicy.ValidateGuideEntry(
                ActionActor.Provider(Guid.NewGuid()),
                Details(guideMonth: new DateOnly(2030, 4, 1))));
    }

    [Fact]
    public void AutomationCompletionAdmitsOnlyAiMarketResearch()
    {
        var details = Details(source: ValuationSource.AiMarketResearch);
        Assert.Equal(details, ValuationPolicy.ValidateAutomationMarketResearch(details));
        Assert.Throws<InvalidOperationException>(() =>
            ValuationPolicy.ValidateAutomationMarketResearch(Details(ValuationSource.Glasses)));
    }

    /// <summary>
    /// A guide source's card as the Case save records it (23 September 2026):
    /// any staff role may type a guide figure, the card names its guide month,
    /// and neither the Engineer's Value nor AI market research is a guide card.
    /// </summary>
    [Theory]
    [InlineData(ValuationSource.Glasses)]
    [InlineData(ValuationSource.Brego)]
    [InlineData(ValuationSource.SuperCap)]
    [InlineData(ValuationSource.Cap)]
    [InlineData(ValuationSource.Cazana)]
    public void AGuideCardNamesItsMonthAndIsRecordedByAnyStaffRole(ValuationSource source)
    {
        var details = Details(source, guideMonth: new DateOnly(2030, 4, 1));

        Assert.Equal(details, ValuationPolicy.ValidateGuideEntry(User, details));
        Assert.Equal(details, ValuationPolicy.ValidateGuideEntry(Engineer, details));
        // Any box of a guide card may be blank (operator, 23 September 2026).
        var blank = Details(source) with { Mileage = null, RetailValue = null, TradeValue = null };
        Assert.Equal(blank, ValuationPolicy.ValidateGuideEntry(User, blank));
        Assert.Throws<ArgumentException>(() =>
            ValuationPolicy.ValidateGuideEntry(User, Details(source, guideMonth: new DateOnly(2030, 4, 2))));
    }

    [Theory]
    [InlineData(ValuationSource.EngineersValue)]
    [InlineData(ValuationSource.AiMarketResearch)]
    public void TheEngineersValueAndAiMarketResearchAreNotGuideCards(ValuationSource source)
    {
        Assert.Throws<InvalidOperationException>(() =>
            ValuationPolicy.ValidateGuideEntry(Engineer, Details(source, guideMonth: new DateOnly(2030, 4, 1))));
        // Unlike a guide card, they always carry their figures.
        Assert.Throws<ArgumentException>(() =>
            ValuationPolicy.ValidateDetails(Details(source) with { RetailValue = null }));
    }

    [Fact]
    public void AGuideCardRequiresAStaffActor()
    {
        var details = Details(guideMonth: new DateOnly(2030, 4, 1));

        Assert.Throws<InvalidOperationException>(() =>
            ValuationPolicy.ValidateGuideEntry(ActionActor.Automation("pegasus-automation"), details));
    }

    [Fact]
    public async Task ListRejectsAnEmptyCaseId()
    {
        var store = new RecordingStore();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new ListCaseValuations(store).ExecuteAsync(Guid.Empty, CaseWorkSelector.Current, CancellationToken.None));
    }

    private static ValuationDetails Details(
        ValuationSource source = ValuationSource.Glasses,
        long mileage = 45000,
        decimal retail = 12000m,
        decimal trade = 10000m,
        DateOnly? guideMonth = null) =>
        new(
            source,
            new DateOnly(2030, 5, 6),
            new TimeOnly(10, 30),
            mileage,
            retail,
            trade,
            guideMonth);

    private sealed class RecordingStore : IValuationStore
    {
        public IReadOnlyList<CaseValuation> Listed { get; set; } = [];

        public Task<IReadOnlyList<CaseValuation>> ListForCaseAsync(
            Guid caseId,
            CaseWorkSelector work, CancellationToken cancellationToken) =>
            Task.FromResult(Listed);
    }
}
