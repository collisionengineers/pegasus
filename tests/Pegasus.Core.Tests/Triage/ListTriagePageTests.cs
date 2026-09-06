using Pegasus.Core;
using Pegasus.Core.Identity;
using Pegasus.Core.Triage;

namespace Pegasus.Core.Tests.Triage;

public sealed class ListTriagePageTests
{
    private static readonly DateTimeOffset Now =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    private static ActionActor StaffActor() => ActionActor.Staff(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        [StaffRole.Engineer]);

    [Fact]
    public async Task TheFirstPageAsksForNoPositionAndMintsTheNextCursor()
    {
        var queries = new RecordingQueries(
            new(
                [Summary("T-00001", Now), Summary("T-00002", Now.AddMinutes(-1))],
                new(Now.AddMinutes(-1), Guid.Parse("22222222-2222-2222-2222-222222222222"))));
        var protector = new FakeCursorProtector();
        var page = await new ListTriagePage(queries, protector).ExecuteAsync(
            new(StaffActor(), TriageState.Open, Cursor: null, Limit: 2));

        Assert.Null(queries.LastPosition);
        Assert.Equal(2, queries.LastLimit);
        Assert.Equal(TriageState.Open, queries.LastState);
        Assert.Equal(2, page.Items.Count);
        Assert.NotNull(page.NextCursor);
    }

    [Fact]
    public async Task TheReturnedCursorDecodesBackToTheExactPositionItWasMintedFrom()
    {
        var id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var position = new TriageListPosition(Now.AddMinutes(-1), id);
        var queries = new RecordingQueries(new([Summary("T-00001", Now)], position));
        var protector = new FakeCursorProtector();
        var useCase = new ListTriagePage(queries, protector);
        var first = await useCase.ExecuteAsync(new(StaffActor(), null, null, 1));

        queries.Next = new([Summary("T-00002", Now.AddMinutes(-1))], null);
        var second = await useCase.ExecuteAsync(new(StaffActor(), null, first.NextCursor, 1));

        Assert.Equal(position, queries.LastPosition);
        Assert.Null(second.NextCursor);
    }

    [Fact]
    public async Task ACursorFromAnotherFilterActorOrOrderIsRefused()
    {
        var queries = new RecordingQueries(
            new(
                [Summary("T-00001", Now)],
                new(Now, Guid.Parse("22222222-2222-2222-2222-222222222222"))));
        var protector = new FakeCursorProtector();
        var useCase = new ListTriagePage(queries, protector);
        var open = await useCase.ExecuteAsync(new(StaffActor(), TriageState.Open, null, 1));

        // The same cursor, presented against a different state filter and
        // against a different actor. Both are a different scope.
        await Assert.ThrowsAsync<CursorRejectedException>(() =>
            useCase.ExecuteAsync(new(StaffActor(), TriageState.Completed, open.NextCursor, 1)));
        await Assert.ThrowsAsync<CursorRejectedException>(() =>
            useCase.ExecuteAsync(new(
                ActionActor.Staff(
                    Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    [StaffRole.Engineer]),
                TriageState.Open,
                open.NextCursor,
                1)));
    }

    [Fact]
    public async Task ANonCaseworkActorAndAnUnsupportedLimitAreRefusedBeforeAnyRead()
    {
        var queries = new RecordingQueries(new([], null));
        var useCase = new ListTriagePage(queries, new FakeCursorProtector());

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            useCase.ExecuteAsync(new(ActionActor.RequestLink(Guid.NewGuid()), null, null, 10)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            useCase.ExecuteAsync(new(StaffActor(), null, null, 0)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            useCase.ExecuteAsync(new(StaffActor(), null, null, CursorPaging.MaximumLimit + 1)));
        Assert.Equal(0, queries.Calls);
    }

    private static TriageSummary Summary(string reference, DateTimeOffset createdAtUtc) => new(
        Guid.NewGuid(),
        "AB12CDE",
        TriageState.Open,
        AssigneeId: null,
        LinkedCaseId: null,
        createdAtUtc,
        Version: 0,
        Reference: reference,
        Provider: "QDOS");

    private sealed class RecordingQueries(TriageListSlice next) : ITriageQueries
    {
        public TriageListSlice Next { get; set; } = next;

        public int Calls { get; private set; }

        public TriageState? LastState { get; private set; }

        public TriageListPosition? LastPosition { get; private set; }

        public int LastLimit { get; private set; }

        public Task<TriageListSlice> ListPageAsync(
            TriageState? state,
            TriageListPosition? after,
            int limit,
            CancellationToken cancellationToken)
        {
            Calls++;
            LastState = state;
            LastPosition = after;
            LastLimit = limit;
            return Task.FromResult(Next);
        }

        public Task<IReadOnlyList<TriageSummary>> ListAsync(
            TriageState? state,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TriageDetail?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TriageSummary?> GetByOriginReceiptAsync(
            Guid originReceiptId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// Stands in for the host's data-protection protector: it keeps the scope
    /// beside the payload so a cursor presented under a different scope is
    /// refused exactly as the real one refuses a failed unprotect.
    /// </summary>
    private sealed class FakeCursorProtector : ICursorProtector
    {
        private readonly Dictionary<string, (string Scope, string SortKey, Guid Id)> issued = [];

        public string Protect(string scope, string sortKey, Guid id)
        {
            var cursor = $"cursor-{issued.Count + 1}";
            issued[cursor] = (scope, sortKey, id);
            return cursor;
        }

        public (string SortKey, Guid Id) Unprotect(string cursor, string scope)
        {
            if (!issued.TryGetValue(cursor, out var payload)
                || !string.Equals(payload.Scope, scope, StringComparison.Ordinal))
            {
                throw new CursorRejectedException();
            }

            return (payload.SortKey, payload.Id);
        }
    }
}
