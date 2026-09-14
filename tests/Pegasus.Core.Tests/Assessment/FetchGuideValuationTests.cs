using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Assessment;

public sealed class FetchGuideValuationTests
{
    private static readonly ActionActor Engineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

    [Fact]
    public async Task ASourceWithoutAConnectedProviderIsRefusedBeforeTheCaseIsRead()
    {
        var caseData = new RecordingCaseData();
        var saves = new RecordingSaveValuation();
        var fetch = new FetchGuideValuation([], caseData, saves, TimeProvider.System);

        var refusal = await Assert.ThrowsAsync<GuideValuationProviderUnavailableException>(() =>
            fetch.ExecuteAsync(Request(ValuationSource.Glasses), default));

        Assert.Equal(ValuationSource.Glasses, refusal.ValuationSource);
        Assert.Equal(0, caseData.Reads);
        Assert.Empty(saves.Saved);
    }

    [Theory]
    [InlineData(ValuationSource.EngineersValue)]
    [InlineData(ValuationSource.AiMarketResearch)]
    [InlineData(ValuationSource.Cazana)]
    public async Task OnlyAGuideSourceCanBeFetched(ValuationSource source)
    {
        var fetch = new FetchGuideValuation(
            [], new RecordingCaseData(), new RecordingSaveValuation(), TimeProvider.System);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            fetch.ExecuteAsync(Request(source), default));
    }

    [Fact]
    public async Task AnActorWithoutCaseworkOrAnEmptyKeyIsRefused()
    {
        var fetch = new FetchGuideValuation(
            [], new RecordingCaseData(), new RecordingSaveValuation(), TimeProvider.System);
        var reader = ActionActor.SystemWorker("valuation-worker");

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            fetch.ExecuteAsync(Request(ValuationSource.Brego) with { Actor = reader }, default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            fetch.ExecuteAsync(Request(ValuationSource.Brego) with { OperationKey = " " }, default));
    }

    private static FetchGuideValuationRequest Request(ValuationSource source) => new(
        Guid.NewGuid(),
        3,
        Engineer,
        "get-valuation-test",
        "opaque-live-case-lease",
        source,
        new DateOnly(2026, 9, 14));

    private sealed class RecordingCaseData : ICaseDataQueries
    {
        public int Reads { get; private set; }

        public Task<CaseDataProjection?> GetAsync(Guid caseId, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult<CaseDataProjection?>(null);
        }
    }

    private sealed class RecordingSaveValuation : ISaveValuation
    {
        public List<SaveValuationRequest> Saved { get; } = [];

        public Task<CaseValuation> ExecuteAsync(SaveValuationRequest request, CancellationToken cancellationToken)
        {
            Saved.Add(request);
            return Task.FromResult(new CaseValuation(
                Guid.NewGuid(), request.CaseId, request.Details, request.Actor.SubjectId, DateTimeOffset.UtcNow));
        }
    }
}
