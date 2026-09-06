using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.AiWork;

public sealed class AdministrationAiJobsTests
{
    [Fact]
    public async Task AdministratorGetsABoundedStablePageAndRuntimeState()
    {
        var query = new Query(Enumerable.Range(0, 51).Select(_ => Job()).ToArray());
        var result = await new GetAdministrationAiJobs(query, new Jobs(), new Control(true))
            .ExecuteAsync(Admin(), 1, transportComposed: true, cancellationToken: CancellationToken.None);

        Assert.Equal((0, 51), query.Request);
        Assert.Equal(50, result.Jobs.Count);
        Assert.True(result.HasMore);
        Assert.True(result.TransportComposed);
        Assert.True(result.SendToAiSwitchEnabled);
        Assert.Equal(new AiJobCounts(2, 1), result.Counts);
    }

    [Fact]
    public async Task ViewerRequiresAutomationAdministrationAccess()
    {
        var query = new Query([]);
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new GetAdministrationAiJobs(query, new Jobs(), new Control(false)).ExecuteAsync(
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]), 1, transportComposed: false, cancellationToken: CancellationToken.None));
        Assert.Null(query.Request);
    }

    [Fact]
    public async Task LaterPagesAdvanceTheBoundedOffset()
    {
        var query = new Query(Enumerable.Range(0, 51).Select(_ => Job()).ToArray());

        _ = await new GetAdministrationAiJobs(query, new Jobs(), new Control(true))
            .ExecuteAsync(Admin(), 3, transportComposed: true, cancellationToken: CancellationToken.None);

        Assert.Equal((100, 51), query.Request);
    }

    private static ActionActor Admin() => ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
    private static AiJobRecord Job() => new(Guid.NewGuid(), AiJobKind.Estimate, AiJobSubjectKind.Case,
        Guid.NewGuid(), "CASE", "instruction", null, null, AiJobState.Queued, ActorKind.Staff,
        "staff", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), null, null, null, null,
        null, null, null, null, null, 0);

    private sealed class Query(IReadOnlyList<AiJobRecord> rows) : IAdministrationAiJobQueries
    {
        public (int Offset, int Limit)? Request { get; private set; }
        public Task<IReadOnlyList<AiJobRecord>> ListAsync(int offset, int limit, CancellationToken cancellationToken)
        {
            Request = (offset, limit);
            return Task.FromResult(rows);
        }
    }
    private sealed class Jobs : IAiJobQueries
    {
        public Task<IReadOnlyList<AiJobRecord>> ListOpenAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AiJobRecord>>([]);
        public Task<AiJobQueryPage> ListOpenPageAsync(AiJobKind? kind, string grantId, DateTimeOffset? afterCreatedAtUtc, Guid? afterJobId, int limit, CancellationToken cancellationToken) => Task.FromResult(new AiJobQueryPage([], false));
        public Task<IReadOnlyList<AiJobRecord>> ListForSubjectAsync(Guid subjectId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AiJobRecord>>([]);
        public Task<IReadOnlyList<AiJobRecord>> ListRecentAsync(int max, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AiJobRecord>>([]);
        public Task<AiJobCounts> GetCountsAsync(CancellationToken cancellationToken) => Task.FromResult(new AiJobCounts(2, 1));
    }
    private sealed class Control(bool enabled) : ISendToAiControl
    {
        public Task<bool> IsEnabledAsync(CancellationToken cancellationToken) => Task.FromResult(enabled);
        public Task<bool> SetEnabledAsync(bool value, ActionActor actor, string reason, string operationKey, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
