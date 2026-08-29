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
    public void SourceVocabularyIsClosedAndHasOneNamePerSource()
    {
        Assert.Equal(Enum.GetValues<ValuationSource>(), ValuationSources.All.Select(item => item.Source));
        Assert.All(ValuationSources.All, item => Assert.False(string.IsNullOrWhiteSpace(item.Name)));
        Assert.Equal(
            ValuationSources.All.Count,
            ValuationSources.All.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count());
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

    [Fact]
    public async Task SaveAndEditUseCasesRequireAnEngineerAndForwardValidatedRequests()
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
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            save.ExecuteAsync(SaveRequest(User, "valuation-user"), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            edit.ExecuteAsync(editRequest with { ValuationId = Guid.Empty }, CancellationToken.None));
    }

    [Fact]
    public async Task CurrentEngineersValueUsesLatestEnteredLondonDateAndTime()
    {
        var store = new RecordingStore();
        var older = Record(
            ValuationSource.EngineersValue,
            new DateOnly(2030, 10, 27),
            new TimeOnly(1, 15),
            10000m);
        var newer = Record(
            ValuationSource.EngineersValue,
            new DateOnly(2030, 10, 27),
            new TimeOnly(1, 45),
            11000m);
        store.Listed =
        [
            Record(ValuationSource.Cazana, new DateOnly(2030, 10, 28), new TimeOnly(9, 0), 12000m),
            older,
            newer,
        ];

        var current = await new GetCurrentEngineersValue(store)
            .ExecuteAsync(CaseId, CancellationToken.None);

        Assert.Equal(newer, current);
        Assert.Equal(
            LondonCalendar.ToUtc(new DateTime(2030, 10, 27, 1, 45, 0)),
            ValuationPolicy.ValuedAtUtc(newer.Details));
    }

    [Fact]
    public async Task ListAndCurrentQueriesRejectAnEmptyCaseId()
    {
        var store = new RecordingStore();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new ListCaseValuations(store).ExecuteAsync(Guid.Empty, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new GetCurrentEngineersValue(store).ExecuteAsync(Guid.Empty, CancellationToken.None));
    }

    private static SaveValuationRequest SaveRequest(ActionActor actor, string operationKey) => new(
        CaseId,
        3,
        actor,
        operationKey,
        "Recorded a valuation.",
        Lease,
        Details());

    private static ValuationDetails Details(
        ValuationSource source = ValuationSource.Glasses,
        long mileage = 45000,
        decimal retail = 12000m,
        decimal trade = 10000m) =>
        new(source, new DateOnly(2030, 5, 6), new TimeOnly(10, 30), mileage, retail, trade);

    private static CaseValuation Record(
        ValuationSource source,
        DateOnly date,
        TimeOnly time,
        decimal retail) =>
        new(
            Guid.NewGuid(),
            CaseId,
            new(source, date, time, 45000, retail, retail - 1000m),
            Engineer.SubjectId,
            Now);

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
