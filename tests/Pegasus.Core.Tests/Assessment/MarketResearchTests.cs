using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Assessment;

public sealed class MarketResearchTests
{
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);
    private static readonly ActionActor Engineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

    [Fact]
    public void ASameSourceAndMonthValuationReplacesTheEarlierCard()
    {
        var june = new DateOnly(2026, 6, 1);
        var existing = new CaseValuation(
            Guid.NewGuid(), CaseId, Details(ValuationSource.Glasses, june), "staff", Now);
        var otherMonth = existing with { ValuationId = Guid.NewGuid(), Details = Details(ValuationSource.Glasses, new DateOnly(2026, 5, 1)) };
        var otherSource = existing with { ValuationId = Guid.NewGuid(), Details = Details(ValuationSource.Brego, june) };
        var undated = existing with { ValuationId = Guid.NewGuid(), Details = Details(ValuationSource.Glasses, null) };

        Assert.True(ValuationPolicy.Replaces(Details(ValuationSource.Glasses, june), existing.Details));
        // A card without a month replaces the source's card without one
        // (operator, 23 September 2026), and never a dated card.
        Assert.True(ValuationPolicy.Replaces(Details(ValuationSource.Glasses, null), undated.Details));
        Assert.False(ValuationPolicy.Replaces(Details(ValuationSource.Glasses, null), existing.Details));
        Assert.Same(
            existing,
            ValuationPolicy.FindReplaced(Details(ValuationSource.Glasses, june), [otherMonth, otherSource, undated, existing]));
        Assert.Null(ValuationPolicy.FindReplaced(Details(ValuationSource.SuperCap, june), [otherMonth, otherSource, undated, existing]));
    }

    [Fact]
    public async Task StartingResearchCreatesTheMarketResearchJobForTheMonth()
    {
        var jobs = new FakeJobs();
        var create = new RecordingCreate();
        var sut = new StartMarketResearch(new MarketResearchQueries(jobs), create);

        var job = await sut.ExecuteAsync(new(CaseId, new DateOnly(2026, 6, 1), Engineer, " research-1 "), default);

        var command = Assert.Single(create.Commands);
        Assert.Equal(AiJobKind.MarketResearch, command.Kind);
        Assert.Equal(CaseId, command.SubjectId);
        Assert.Equal("research-1", command.OperationKey);
        Assert.Contains("2026-06", command.Instruction, StringComparison.Ordinal);
        Assert.Equal(AiJobKind.MarketResearch, job.Kind);
    }

    [Fact]
    public async Task APendingJobIsReturnedInsteadOfStartingAnother()
    {
        var pending = Job(AiJobKind.MarketResearch, AiJobState.Taken);
        var jobs = new FakeJobs(pending, Job(AiJobKind.Estimate, AiJobState.Queued), Job(AiJobKind.MarketResearch, AiJobState.DraftReady));
        var create = new RecordingCreate();
        var sut = new StartMarketResearch(new MarketResearchQueries(jobs), create);

        var job = await sut.ExecuteAsync(new(CaseId, new DateOnly(2026, 6, 1), Engineer, "research-2"), default);

        Assert.Same(pending, job);
        Assert.Empty(create.Commands);
        Assert.Same(pending, await new MarketResearchQueries(jobs).GetPendingAsync(CaseId, default));
    }

    [Fact]
    public async Task ADraftReadyOrClosedJobIsNotPendingAndDoesNotBlockANewMonth()
    {
        var jobs = new FakeJobs(Job(AiJobKind.MarketResearch, AiJobState.DraftReady), Job(AiJobKind.MarketResearch, AiJobState.Completed));
        var create = new RecordingCreate();

        Assert.Null(await new MarketResearchQueries(jobs).GetPendingAsync(CaseId, default));
        await new StartMarketResearch(new MarketResearchQueries(jobs), create)
            .ExecuteAsync(new(CaseId, new DateOnly(2026, 7, 1), Engineer, "research-3"), default);

        Assert.Single(create.Commands);
    }

    [Fact]
    public async Task TheGuideMonthMustBeAMonth()
    {
        var sut = new StartMarketResearch(new MarketResearchQueries(new FakeJobs()), new RecordingCreate());

        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.ExecuteAsync(new(CaseId, new DateOnly(2026, 6, 15), Engineer, "research-4"), default));
    }

    private static ValuationDetails Details(ValuationSource source, DateOnly? guideMonth) =>
        new(source, new DateOnly(2026, 6, 10), new TimeOnly(9, 0), 42000, 8000m, 6500m, guideMonth);

    private static AiJobRecord Job(AiJobKind kind, AiJobState state) => new(
        Guid.NewGuid(), kind, AiJobSubjectKind.Case, CaseId, "QDOS260001",
        "Do the work.", null, null, state, ActorKind.Staff, "staff", Now,
        Now + AiJobPolicy.DefaultExpiry, null, null, null, null, null, null, null, null, null, 1);

    private sealed class FakeJobs(params AiJobRecord[] jobs) : IAiJobQueries
    {
        public Task<IReadOnlyList<AiJobRecord>> ListOpenAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AiJobQueryPage> ListOpenPageAsync(AiJobKind? kind, string grantId, DateTimeOffset? afterCreatedAtUtc, Guid? afterJobId, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<AiJobRecord>> ListForSubjectAsync(Guid subjectId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AiJobRecord>>(jobs.Where(job => job.SubjectId == subjectId).ToArray());

        public Task<IReadOnlyList<AiJobRecord>> ListRecentAsync(int max, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<AiJobRecord>> ListTerminalInWindowAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AiJobCounts> GetCountsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class RecordingCreate : ICreateAiJob
    {
        public List<CreateAiJobCommand> Commands { get; } = [];

        public Task<AiJobRecord> ExecuteAsync(CreateAiJobCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.FromResult(Job(command.Kind, AiJobState.Queued));
        }
    }
}
