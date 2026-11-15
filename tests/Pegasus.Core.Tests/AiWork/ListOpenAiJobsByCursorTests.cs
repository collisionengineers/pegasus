using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.AiWork;

/// <summary>
/// The open AI job list pages under the scope the pegasus_ai_job_list tool
/// minted its cursors with, so a cursor a client already holds still decodes.
/// </summary>
public sealed class ListOpenAiJobsByCursorTests
{
    private static readonly DateTimeOffset Now = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private static readonly ActionActor Client = ActionActor.Automation("pegasus-automation");

    [Fact]
    public async Task APageCarriesTheToolsCursorAndTheNextPageStartsAfterItsLastJob()
    {
        var oldest = Job(Now);
        var queries = new Jobs { PageToReturn = new([oldest], HasMore: true) };
        var protector = new RecordingCursorProtector();
        var list = new ListOpenAiJobsByCursor(queries, protector);

        var first = await list.ExecuteAsync(
            new(Client, "grant-1", AiJobKind.QueryResponse), CancellationToken.None);

        var toolScope = CursorPaging.CreateScope(
            "pegasus_ai_job_list", Client, "QueryResponse", "created-id-asc");
        var issued = Assert.Single(protector.Protected);
        Assert.Equal(toolScope, issued.Scope);
        Assert.Equal(CursorPaging.EncodeUtcTimestamp(Now), issued.SortKey);
        Assert.Equal(oldest.JobId, issued.Id);
        Assert.Equal(issued.Cursor, first.NextCursor);
        Assert.Equal(oldest, Assert.Single(first.Items));
        var firstRead = Assert.Single(queries.Reads);
        Assert.Equal((AiJobKind?)AiJobKind.QueryResponse, firstRead.Kind);
        Assert.Equal("grant-1", firstRead.GrantId);
        Assert.Null(firstRead.AfterCreatedAtUtc);
        Assert.Equal(CursorPaging.DefaultLimit, firstRead.Limit);

        queries.PageToReturn = new([], HasMore: false);
        var second = await list.ExecuteAsync(
            new(Client, "grant-1", AiJobKind.QueryResponse, first.NextCursor, 10), CancellationToken.None);

        Assert.Null(second.NextCursor);
        Assert.Equal(Now, queries.Reads[1].AfterCreatedAtUtc);
        Assert.Equal(oldest.JobId, queries.Reads[1].AfterJobId);
        Assert.Equal(10, queries.Reads[1].Limit);
    }

    [Fact]
    public async Task ACursorForAnotherKindIsRefusedBeforeAnyRead()
    {
        var queries = new Jobs { PageToReturn = new([Job(Now)], HasMore: true) };
        var protector = new RecordingCursorProtector();
        var list = new ListOpenAiJobsByCursor(queries, protector);
        var first = await list.ExecuteAsync(new(Client, "grant-1", null), CancellationToken.None);
        Assert.Equal(
            CursorPaging.CreateScope("pegasus_ai_job_list", Client, string.Empty, "created-id-asc"),
            Assert.Single(protector.Protected).Scope);

        await Assert.ThrowsAsync<CursorRejectedException>(() =>
            list.ExecuteAsync(
                new(Client, "grant-1", AiJobKind.Estimate, first.NextCursor), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            list.ExecuteAsync(new(Client, "grant-1", null, null, 101), CancellationToken.None));
        Assert.Single(queries.Reads);
    }

    private static AiJobRecord Job(DateTimeOffset createdAtUtc) => new(
        Guid.NewGuid(), AiJobKind.QueryResponse, AiJobSubjectKind.Case, Guid.NewGuid(), "REF-1",
        "Do the work.", null, null, AiJobState.Queued, ActorKind.Staff, "staff", createdAtUtc,
        createdAtUtc + AiJobPolicy.DefaultExpiry, null, null, null, null, null, null, null, null, null, 0);

    private sealed class Jobs : IAiJobQueries
    {
        public AiJobQueryPage PageToReturn { get; set; } = new([], false);

        public List<(AiJobKind? Kind, string GrantId, DateTimeOffset? AfterCreatedAtUtc, Guid? AfterJobId, int Limit)> Reads { get; } = [];

        public Task<AiJobQueryPage> ListOpenPageAsync(
            AiJobKind? kind,
            string grantId,
            DateTimeOffset? afterCreatedAtUtc,
            Guid? afterJobId,
            int limit,
            CancellationToken cancellationToken)
        {
            Reads.Add((kind, grantId, afterCreatedAtUtc, afterJobId, limit));
            return Task.FromResult(PageToReturn);
        }

        public Task<IReadOnlyList<AiJobRecord>> ListOpenAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AiJobRecord>> ListOpenForSubjectAsync(Guid subjectId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AiJobRecord?> GetPendingMarketResearchAsync(Guid subjectId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AiJobRecord>> ListRecentAsync(int max, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AiJobCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingCursorProtector : ICursorProtector
    {
        public List<(string Scope, string SortKey, Guid Id, string Cursor)> Protected { get; } = [];

        public string Protect(string scope, string sortKey, Guid id)
        {
            var cursor = $"cursor-{Protected.Count + 1}";
            Protected.Add((scope, sortKey, id, cursor));
            return cursor;
        }

        public (string SortKey, Guid Id) Unprotect(string cursor, string scope)
        {
            var issued = Protected.Find(item => string.Equals(item.Cursor, cursor, StringComparison.Ordinal));
            return issued.Cursor is not null && string.Equals(issued.Scope, scope, StringComparison.Ordinal)
                ? (issued.SortKey, issued.Id)
                : throw new CursorRejectedException();
        }
    }
}
