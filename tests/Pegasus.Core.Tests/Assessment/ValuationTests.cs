using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Assessment;

public sealed class ValuationTests
{
    private static readonly DateTimeOffset Now =
        new(2030, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly ActionActor Engineer =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
    private static readonly ActionActor User =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
    private static readonly string Lease = new('l', CaseEditAuthority.LeaseTokenLength);

    [Fact]
    public void SourceVocabularyIsClosed()
    {
        Assert.All(
            Enum.GetValues<ValuationSource>(),
            source => Assert.True(ValuationSources.IsSupported(source)));
        Assert.False(ValuationSources.IsSupported((ValuationSource)99));
        Assert.Equal(
            [ValuationSource.Glasses, ValuationSource.Cazana, ValuationSource.EngineersValue,
                ValuationSource.AiMarketResearch, ValuationSource.Brego, ValuationSource.SuperCap],
            Enum.GetValues<ValuationSource>());
    }

    /// <summary>
    /// Collision Engineers reads the Glass's, Brego and Super CAP guides and
    /// types the figure in; none of them is a live call here. Cazana stays a
    /// disabled seam and AI market research is written only by the automation
    /// completion, so neither is offered to the staff save and edit actions.
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
                ValuationSource.EngineersValue
            },
            source => Assert.True(ValuationPolicy.IsManuallyRecordable(source)));
        Assert.False(ValuationPolicy.IsManuallyRecordable(ValuationSource.Cazana));
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

    [Fact]
    public async Task SaveAndEditUseCasesForwardValidatedRequests()
    {
        var store = new RecordingStore();
        var save = new SaveValuation(store);
        var edit = new EditValuation(store);
        var saveRequest = SaveRequest(Engineer, "valuation-save");

        var saved = await save.ExecuteAsync(saveRequest, CancellationToken.None);

        Assert.Equal(saveRequest, Assert.Single(store.Saves));
        Assert.Equal(saveRequest.Details, saved.Details);

        var editRequest = new EditValuationRequest(
            CaseId,
            4,
            Engineer,
            "valuation-edit",
            "Corrected the recorded valuation.",
            Lease,
            saved.ValuationId,
            Details(retail: 12345.67m));
        var edited = await edit.ExecuteAsync(editRequest, CancellationToken.None);

        Assert.Equal(editRequest, Assert.Single(store.Edits));
        Assert.Equal(12345.67m, edited.Details.RetailValue);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            edit.ExecuteAsync(editRequest with { ValuationId = Guid.Empty }, CancellationToken.None));
    }

    /// <summary>
    /// Recording a market valuation is ordinary casework, as the ticket
    /// specifies. An Engineer's Value row is not: it carries the confirmed
    /// assessment.values.engineer professional finding, so it takes that
    /// field's own authority rule from AssessmentPolicy.
    /// </summary>
    [Fact]
    public async Task CaseworkRecordsAMarketValuationAndOnlyAnEngineerRecordsAnEngineersValue()
    {
        var store = new RecordingStore();
        var save = new SaveValuation(store);
        var edit = new EditValuation(store);

        var glasses = await save.ExecuteAsync(
            SaveRequest(User, "valuation-user-glasses"),
            CancellationToken.None);

        var brego = await save.ExecuteAsync(
            SaveRequest(User, "valuation-user-brego", ValuationSource.Brego),
            CancellationToken.None);
        var superCap = await save.ExecuteAsync(
            SaveRequest(User, "valuation-user-super-cap", ValuationSource.SuperCap),
            CancellationToken.None);

        Assert.Equal(ValuationSource.Glasses, glasses.Details.Source);
        Assert.Equal(ValuationSource.Brego, brego.Details.Source);
        Assert.Equal(ValuationSource.SuperCap, superCap.Details.Source);
        Assert.Equal(3, store.Saves.Count);

        // Cazana is a disabled seam, not a source staff may type in.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            save.ExecuteAsync(
                SaveRequest(User, "valuation-user-cazana", ValuationSource.Cazana),
                CancellationToken.None));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            save.ExecuteAsync(
                SaveRequest(User, "valuation-user-engineers", ValuationSource.EngineersValue),
                CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            edit.ExecuteAsync(
                new EditValuationRequest(
                    CaseId,
                    3,
                    User,
                    "valuation-user-engineers-edit",
                    "Corrected the recorded valuation.",
                    Lease,
                    Guid.NewGuid(),
                    Details(source: ValuationSource.EngineersValue)),
                CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            save.ExecuteAsync(
                SaveRequest(
                    ActionActor.Automation("pegasus-automation"),
                    "valuation-automation",
                    ValuationSource.AiMarketResearch),
                CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            save.ExecuteAsync(
                SaveRequest(ActionActor.RequestLink(Guid.NewGuid()), "valuation-request-link"),
                CancellationToken.None));

        var engineers = await save.ExecuteAsync(
            SaveRequest(Engineer, "valuation-engineer", ValuationSource.EngineersValue),
            CancellationToken.None);
        Assert.Equal(ValuationSource.EngineersValue, engineers.Details.Source);
    }

    [Fact]
    public void AutomationCompletionAdmitsOnlyAiMarketResearch()
    {
        var details = Details(source: ValuationSource.AiMarketResearch);
        Assert.Equal(details, ValuationPolicy.ValidateAutomationMarketResearch(details));
        Assert.Throws<InvalidOperationException>(() =>
            ValuationPolicy.ValidateAutomationMarketResearch(Details(ValuationSource.Glasses)));
    }

    [Fact]
    public async Task ListRejectsAnEmptyCaseId()
    {
        var store = new RecordingStore();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new ListCaseValuations(store).ExecuteAsync(Guid.Empty, CancellationToken.None));
    }

    private static SaveValuationRequest SaveRequest(
        ActionActor actor,
        string operationKey,
        ValuationSource source = ValuationSource.Glasses) => new(
        CaseId,
        3,
        actor,
        operationKey,
        "Recorded a valuation.",
        Lease,
        Details(source));

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
        public List<SaveValuationRequest> Saves { get; } = [];
        public List<EditValuationRequest> Edits { get; } = [];
        public IReadOnlyList<CaseValuation> Listed { get; set; } = [];

        public Task<CaseValuation> SaveAsync(
            SaveValuationRequest request,
            CancellationToken cancellationToken)
        {
            Saves.Add(request);
            return Task.FromResult(new CaseValuation(
                Guid.NewGuid(),
                request.CaseId,
                request.Details,
                request.Actor.SubjectId,
                Now));
        }

        public Task<CaseValuation> EditAsync(
            EditValuationRequest request,
            CancellationToken cancellationToken)
        {
            Edits.Add(request);
            return Task.FromResult(new CaseValuation(
                request.ValuationId,
                request.CaseId,
                request.Details,
                request.Actor.SubjectId,
                Now,
                request.Actor.SubjectId,
                Now));
        }

        public Task<IReadOnlyList<CaseValuation>> ListForCaseAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Listed);
    }
}
