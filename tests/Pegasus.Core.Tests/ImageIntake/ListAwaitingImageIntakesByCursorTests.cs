using Pegasus.Core.ImageIntake;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.ImageIntake;

/// <summary>
/// The Vehicle images awaiting instruction page under the scope the
/// pegasus_image_intake_list tool minted its cursors with, so a cursor a
/// client already holds still decodes. The sort key is the offset after the
/// page; a page resumes after its last record, or at that offset once the
/// record has left the list.
/// </summary>
public sealed class ListAwaitingImageIntakesByCursorTests
{
    private static readonly DateTimeOffset Now = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private static readonly ActionActor Client = ActionActor.Automation("pegasus-automation");

    private static readonly string ToolScope = CursorPaging.CreateScope(
        "pegasus_image_intake_list", Client, "unassociated", "awaiting-instruction", "newest-first");

    [Fact]
    public async Task TheFirstPageReadsOneMoreThanItsLimitAndMintsTheToolsCursor()
    {
        var queries = new Intakes(Summaries(5));
        var protector = new RecordingCursorProtector();
        var list = new ListAwaitingImageIntakesByCursor(queries, protector);

        var page = await list.ExecuteAsync(Client, null, 2, CancellationToken.None);

        Assert.Equal(queries.Rows.Take(2), page.Items);
        Assert.Equal((0, 3), Assert.Single(queries.Windows));
        Assert.Empty(queries.PositionReads);
        var issued = Assert.Single(protector.Protected);
        Assert.Equal(ToolScope, issued.Scope);
        Assert.Equal("2", issued.SortKey);
        Assert.Equal(queries.Rows[1].Id, issued.Id);
        Assert.Equal(issued.Cursor, page.NextCursor);
    }

    [Fact]
    public async Task TheNextPageResumesAfterTheLastRecordWhereverItNowStands()
    {
        var queries = new Intakes(Summaries(5));
        var protector = new RecordingCursorProtector();
        var list = new ListAwaitingImageIntakesByCursor(queries, protector);
        var first = await list.ExecuteAsync(Client, null, 2, CancellationToken.None);

        // A newer record arrives at the head: the second record now stands third.
        queries.Rows.Insert(0, Summary(Now.AddMinutes(1)));
        var second = await list.ExecuteAsync(Client, first.NextCursor, 2, CancellationToken.None);

        Assert.Equal(first.Items[^1].Id, Assert.Single(queries.PositionReads));
        Assert.Equal((3, 3), queries.Windows[1]);
        Assert.Equal(queries.Rows.Skip(3).Take(2), second.Items);
        Assert.Equal("5", protector.Protected[1].SortKey);
    }

    [Fact]
    public async Task ARecordThatHasLeftTheListResumesAtTheOffsetTheCursorRecorded()
    {
        var queries = new Intakes(Summaries(5));
        var protector = new RecordingCursorProtector();
        var list = new ListAwaitingImageIntakesByCursor(queries, protector);
        var first = await list.ExecuteAsync(Client, null, 2, CancellationToken.None);

        queries.Rows.RemoveAt(1);
        var second = await list.ExecuteAsync(Client, first.NextCursor, 2, CancellationToken.None);

        Assert.Equal((2, 3), queries.Windows[1]);
        Assert.Equal(queries.Rows.Skip(2).Take(2), second.Items);
    }

    [Fact]
    public async Task TheLastPageHasNoNextCursor()
    {
        var queries = new Intakes(Summaries(3));
        var protector = new RecordingCursorProtector();
        var list = new ListAwaitingImageIntakesByCursor(queries, protector);
        var first = await list.ExecuteAsync(Client, null, 2, CancellationToken.None);

        var last = await list.ExecuteAsync(Client, first.NextCursor, 2, CancellationToken.None);

        Assert.Equal(queries.Rows[2], Assert.Single(last.Items));
        Assert.Null(last.NextCursor);
        Assert.Single(protector.Protected);
    }

    [Fact]
    public async Task ACursorFromAnotherScopeOrActorOrWithAMalformedSortKeyIsRefused()
    {
        var queries = new Intakes(Summaries(3));
        var protector = new RecordingCursorProtector();
        var list = new ListAwaitingImageIntakesByCursor(queries, protector);
        var first = await list.ExecuteAsync(Client, null, 1, CancellationToken.None);
        var otherScope = protector.Issue(
            CursorPaging.CreateScope("pegasus_mail_list", Client, "unassociated", "awaiting-instruction", "newest-first"),
            "1",
            queries.Rows[0].Id);
        var malformed = protector.Issue(ToolScope, "one", Guid.NewGuid());

        await Assert.ThrowsAsync<CursorRejectedException>(() =>
            list.ExecuteAsync(ActionActor.Automation("another-client"), first.NextCursor, 1, CancellationToken.None));
        await Assert.ThrowsAsync<CursorRejectedException>(() =>
            list.ExecuteAsync(Client, otherScope, 1, CancellationToken.None));
        await Assert.ThrowsAsync<CursorRejectedException>(() =>
            list.ExecuteAsync(Client, malformed, 1, CancellationToken.None));
        Assert.Single(queries.Windows);
    }

    [Fact]
    public async Task ANonCaseworkActorAndAnUnsupportedLimitAreRefusedBeforeAnyRead()
    {
        var queries = new Intakes(Summaries(1));
        var list = new ListAwaitingImageIntakesByCursor(queries, new RecordingCursorProtector());

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            list.ExecuteAsync(ActionActor.Principal(Guid.NewGuid()), null, 1, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            list.ExecuteAsync(Client, null, CursorPaging.MaximumLimit + 1, CancellationToken.None));
        Assert.Empty(queries.Windows);
    }

    private static List<ImageIntakeSummary> Summaries(int count) =>
        Enumerable.Range(0, count).Select(index => Summary(Now.AddMinutes(-index))).ToList();

    private static ImageIntakeSummary Summary(DateTimeOffset registeredAtUtc)
    {
        var id = Guid.NewGuid();
        return new(id, Guid.NewGuid(), $"AB12CDE-{id:N}", "AB12CDE", null, null, registeredAtUtc, null);
    }

    private sealed class Intakes(List<ImageIntakeSummary> rows) : IImageIntakeQueries
    {
        public List<ImageIntakeSummary> Rows { get; } = rows;

        public List<(int Skip, int Take)> Windows { get; } = [];

        public List<Guid> PositionReads { get; } = [];

        public Task<IReadOnlyList<ImageIntakeSummary>> ListWindowAsync(
            bool? associated,
            ImageInitiatedCaseState? state,
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            Assert.False(associated);
            Assert.Equal(ImageInitiatedCaseState.AwaitingInstruction, state);
            Windows.Add((skip, take));
            return Task.FromResult<IReadOnlyList<ImageIntakeSummary>>(Rows.Skip(skip).Take(take).ToArray());
        }

        public Task<int?> ListPositionAsync(
            bool? associated,
            ImageInitiatedCaseState? state,
            Guid id,
            CancellationToken cancellationToken)
        {
            Assert.False(associated);
            Assert.Equal(ImageInitiatedCaseState.AwaitingInstruction, state);
            PositionReads.Add(id);
            var place = Rows.FindIndex(row => row.Id == id);
            return Task.FromResult<int?>(place < 0 ? null : place);
        }

        public Task<IReadOnlyList<ImageIntakeSummary>> ListAsync(
            bool? associated,
            ImageInitiatedCaseState? state,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<ImageIntakeImage>>> ListImagesAsync(
            IReadOnlyCollection<Guid> imageIntakeIds,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ImageIntakeDetail?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ImageIntakeDetail?> GetByReferenceAsync(
            string imageIntakeReference,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ImageIntakeDetail?> GetByOriginReceiptAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ImageIntakeRecord?> GetRecordByOriginReceiptAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ImageIntakeSummary>> ListForCaseAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ImageIntakeSummary>> SearchByRegistrationAsync(
            string normalizedVehicleRegistration,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingCursorProtector : ICursorProtector
    {
        private readonly List<(string Scope, string SortKey, Guid Id, string Cursor)> issued = [];

        public List<(string Scope, string SortKey, Guid Id, string Cursor)> Protected { get; } = [];

        public string Protect(string scope, string sortKey, Guid id)
        {
            var cursor = Issue(scope, sortKey, id);
            Protected.Add((scope, sortKey, id, cursor));
            return cursor;
        }

        /// <summary>A cursor as a client might hold it, without counting as one this class minted.</summary>
        public string Issue(string scope, string sortKey, Guid id)
        {
            var cursor = $"cursor-{issued.Count + 1}";
            issued.Add((scope, sortKey, id, cursor));
            return cursor;
        }

        public (string SortKey, Guid Id) Unprotect(string cursor, string scope)
        {
            var match = issued.Find(item => string.Equals(item.Cursor, cursor, StringComparison.Ordinal));
            return match.Cursor is not null && string.Equals(match.Scope, scope, StringComparison.Ordinal)
                ? (match.SortKey, match.Id)
                : throw new CursorRejectedException();
        }
    }
}
