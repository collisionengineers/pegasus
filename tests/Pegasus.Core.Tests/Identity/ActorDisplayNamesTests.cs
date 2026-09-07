using Pegasus.Core.Actors;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Identity;

/// <summary>
/// The single place a persisted actor kind + subject id becomes an operator-facing
/// name — covers PLAT-011: a staff subject never renders as its raw GUID, and an
/// unresolvable actor gets an honest label rather than an invented one.
/// </summary>
public sealed class ActorDisplayNamesTests
{
    [Fact]
    public void ResolveReturnsTheStaffUsernameWhenTheSubjectIsKnown()
    {
        var staffId = Guid.NewGuid();
        var staffNames = new Dictionary<Guid, string> { [staffId] = "alex" };

        var label = ActorDisplayNames.Resolve(ActorKind.Staff, staffId.ToString("D"), staffNames);

        Assert.Equal("alex", label);
    }

    [Fact]
    public void ResolveFallsBackHonestlyForAStaffSubjectThatNoLongerResolves()
    {
        var staffId = Guid.NewGuid();
        var staffNames = new Dictionary<Guid, string>();

        var label = ActorDisplayNames.Resolve(ActorKind.Staff, staffId.ToString("D"), staffNames);

        Assert.Equal(ActorDisplayNames.UnknownStaff, label);
        Assert.DoesNotContain(staffId.ToString("D"), label, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveFallsBackHonestlyForAStaffSubjectThatDoesNotParseAsAGuid()
    {
        var staffNames = new Dictionary<Guid, string>();

        var label = ActorDisplayNames.Resolve(ActorKind.Staff, "not-a-guid", staffNames);

        Assert.Equal(ActorDisplayNames.UnknownStaff, label);
    }

    [Theory]
    [InlineData(ActorKind.SystemWorker, ActorDisplayNames.SystemWorker)]
    [InlineData(ActorKind.Automation, ActorDisplayNames.Automation)]
    [InlineData(ActorKind.RequestLink, ActorDisplayNames.RequestLink)]
    public void ResolveNeverExposesTheRawSubjectForANonStaffActorKind(ActorKind kind, string expectedLabel)
    {
        var subjectId = Guid.NewGuid().ToString("D");
        var staffNames = new Dictionary<Guid, string>();

        var label = ActorDisplayNames.Resolve(kind, subjectId, staffNames);

        Assert.Equal(expectedLabel, label);
        Assert.DoesNotContain(subjectId, label, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveStaffNamesAsyncQueriesEachDistinctSubjectOnceAndSkipsUnresolvedOnes()
    {
        var knownId = Guid.NewGuid();
        var missingId = Guid.NewGuid();
        var queries = new RecordingStaffAccountQueries(knownId, "alex");

        var names = await ActorDisplayNames.ResolveStaffNamesAsync(
            queries,
            [knownId, knownId, missingId, Guid.Empty],
            CancellationToken.None);

        Assert.Equal("alex", names[knownId]);
        Assert.False(names.ContainsKey(missingId));
        Assert.False(names.ContainsKey(Guid.Empty));
        Assert.Equal(2, queries.RequestedIds.Count);
        Assert.Contains(knownId, queries.RequestedIds);
        Assert.Contains(missingId, queries.RequestedIds);
    }

    private sealed class RecordingStaffAccountQueries(Guid knownId, string knownUserName)
        : IStaffAccountQueries
    {
        public List<Guid> RequestedIds { get; } = [];

        public Task<StaffAccountQuerySlice> ListAsync(
            int offset,
            int limit,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<StaffAccountSummary?> GetAsync(Guid staffId, CancellationToken cancellationToken)
        {
            RequestedIds.Add(staffId);
            return Task.FromResult(staffId == knownId
                ? new StaffAccountSummary(staffId, knownUserName, true, false, [StaffRole.User])
                : null);
        }

        public Task<IReadOnlyList<SignOffEngineerProfile>> ListSignOffEngineersAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<SignOffEngineerProfile?> GetSignOffEngineerAsync(
            Guid staffId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");
    }
}
