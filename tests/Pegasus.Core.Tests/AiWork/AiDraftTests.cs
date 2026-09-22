using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.AiWork;

public sealed class AiDraftTests
{
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ACaseListsItsDraftReadyJobsWithTheirActionsAndMarketResearchIsExcluded()
    {
        var written = new DateTimeOffset(2026, 9, 12, 15, 0, 0, TimeSpan.Zero);
        var estimate = Job(AiJobKind.Estimate, AiJobState.DraftReady) with { DraftReadyAtUtc = written };
        var query = Job(AiJobKind.QueryResponse, AiJobState.DraftReady) with { DraftReadyAtUtc = written.AddHours(1) };
        var research = Job(AiJobKind.MarketResearch, AiJobState.DraftReady) with { DraftReadyAtUtc = written };
        var running = Job(AiJobKind.Estimate, AiJobState.Taken);
        var queries = new AiDraftQueries(new FakeJobs(query, running, research, estimate), new Configuration(1));

        var drafts = await queries.ListForCaseAsync(CaseId, default);

        Assert.Equal([estimate.JobId, query.JobId], drafts.Select(draft => draft.Job.JobId));
        Assert.Equal(AiDraftAction.ReviewEstimate, drafts[0].Action);
        Assert.Equal($"/Cases/{CaseId:D}?section=estimate", drafts[0].Route);
        Assert.Equal(AiDraftAction.OpenQuery, drafts[1].Action);
        // Written 12 September 16:00 London; a one-day target is due at midnight ending 13 September.
        Assert.Equal(new DateTimeOffset(2026, 9, 13, 23, 0, 0, TimeSpan.Zero), drafts[0].DueAtUtc);
    }

    [Fact]
    public void TheDueInstantIsMidnightLondonAfterTheTargetDays()
    {
        // 23:30 UTC on 13 September is 00:30 on 14 September in London.
        var lateEvening = new DateTimeOffset(2026, 9, 13, 23, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2026, 9, 14, 23, 0, 0, TimeSpan.Zero), WorkTargets.DueAt(lateEvening, 0));
        Assert.Equal(new DateTimeOffset(2026, 9, 21, 23, 0, 0, TimeSpan.Zero), WorkTargets.DueAt(lateEvening, 7));
        Assert.Equal(new DateTimeOffset(2026, 9, 24, 23, 0, 0, TimeSpan.Zero), WorkTargets.EndOfDay(new DateOnly(2026, 9, 24)));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkTargets.DueAt(lateEvening, -1));
    }

    private static AiJobRecord Job(AiJobKind kind, AiJobState state) => new(
        Guid.NewGuid(), kind, AiJobSubjectKind.Case, CaseId, "QDOS260001",
        "Do the work.", null, null, state, ActorKind.Staff, Guid.NewGuid().ToString("D"), Now,
        Now + AiJobPolicy.DefaultExpiry, "client", Now, null, null, null, null, null, null, null, 1);

    private sealed class Configuration(int aiDraftTargetDays) : ICaseWorkflowConfiguration
    {
        public Task<CaseWorkflowConfiguration> GetCurrentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CaseWorkflowConfiguration("case-workflow", 1) { AiDraftTargetDays = aiDraftTargetDays });
    }

    private sealed class FakeJobs(params AiJobRecord[] jobs) : IAiJobQueries
    {
        public Task<IReadOnlyList<AiJobRecord>> ListOpenAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AiJobRecord>>(jobs.Where(job => !AiJobStates.IsTerminal(job.State)).ToArray());

        public Task<AiJobQueryPage> ListOpenPageAsync(AiJobKind? kind, string grantId, DateTimeOffset? afterCreatedAtUtc, Guid? afterJobId, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<AiJobRecord>> ListForSubjectAsync(Guid subjectId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AiJobRecord>>(jobs.Where(job => job.SubjectId == subjectId).ToArray());

        public Task<IReadOnlyList<AiJobRecord>> ListRecentAsync(int max, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<AiJobRecord>> ListTerminalInWindowAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AiJobCounts> GetCountsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
