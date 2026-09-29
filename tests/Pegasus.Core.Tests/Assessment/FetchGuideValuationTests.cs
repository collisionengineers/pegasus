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
        var fetch = new FetchGuideValuation([], caseData);

        await Assert.ThrowsAsync<GuideValuationProviderUnavailableException>(() =>
            fetch.ExecuteAsync(Request(ValuationSource.Glasses), default));

        Assert.Equal(0, caseData.Reads);
    }

    [Theory]
    [InlineData(ValuationSource.EngineersValue)]
    [InlineData(ValuationSource.AiMarketResearch)]
    public async Task OnlyAGuideSourceCanBeFetched(ValuationSource source)
    {
        var fetch = new FetchGuideValuation([], new RecordingCaseData());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            fetch.ExecuteAsync(Request(source), default));
    }

    /// <summary>
    /// The Case says which sources are connected before Get valuation is
    /// pressed (operator, 28 September 2026): only a guide source with a
    /// registered provider is.
    /// </summary>
    [Fact]
    public void OnlyAGuideSourceWithAProviderIsConnected()
    {
        var fetch = new FetchGuideValuation([new StubProvider(ValuationSource.Brego)], new RecordingCaseData());

        Assert.True(fetch.IsConnected(ValuationSource.Brego));
        Assert.False(fetch.IsConnected(ValuationSource.Glasses));
        Assert.False(fetch.IsConnected(ValuationSource.EngineersValue));
        Assert.False(new FetchGuideValuation([], new RecordingCaseData()).IsConnected(ValuationSource.Brego));
    }

    [Fact]
    public async Task AnActorWithoutCaseworkOrAnEmptyKeyIsRefused()
    {
        var fetch = new FetchGuideValuation([], new RecordingCaseData());
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

    private sealed class StubProvider(ValuationSource source) : IGuideValuationProvider
    {
        public ValuationSource Source => source;

        public Task<GuideValuationQuote> GetAsync(GuideValuationRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingCaseData : ICaseDataQueries
    {
        public int Reads { get; private set; }

        public Task<CaseDataProjection?> GetAsync(Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult<CaseDataProjection?>(null);
        }
    }
}
