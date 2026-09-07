using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Cases;

public sealed class CaseSearchTests
{
    [Fact]
    public async Task SearchRequiresAuthorizedStaffBeforeCallingStore()
    {
        var store = new RecordingStore();
        var search = new SearchCases(store);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => search.ExecuteAsync(
            new(
                ActionActor.SystemWorker("case-search-test"),
                new CaseSearchFilters(Query: "QDOS")),
            default));

        Assert.Null(store.Query);
    }

    [Fact]
    public async Task GlobalQueryIsTrimmedAndBoundedThroughSharedSearchContract()
    {
        var store = new RecordingStore();
        var search = new SearchCases(store);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        await search.ExecuteAsync(
            new(actor, new CaseSearchFilters(Query: "  QDOS/2026/001  "), 2, 25),
            default);

        var query = Assert.IsType<SearchCasesQuery>(store.Query);
        Assert.Same(actor, query.Actor);
        Assert.Equal("QDOS/2026/001", query.Filters.Query);
        Assert.Equal(2, query.Page);
        Assert.Equal(25, query.PageSize);
    }

    [Fact]
    public async Task GlobalQueryOverMaximumLengthNeverCallsStore()
    {
        var store = new RecordingStore();
        var search = new SearchCases(store);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => search.ExecuteAsync(
            new(
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
                new CaseSearchFilters(Query: new string('q', 301))),
            default));

        Assert.Null(store.Query);
    }

    private sealed class RecordingStore : ICaseQueryStore
    {
        public SearchCasesQuery? Query { get; private set; }

        public Task<SearchCasesResult> SearchAsync(
            SearchCasesQuery query,
            CancellationToken cancellationToken)
        {
            Query = query;
            return Task.FromResult(new SearchCasesResult([], query.Page, query.PageSize, false, false));
        }

        public Task<CaseDetails?> GetAsync(
            GetCaseQuery query,
            CancellationToken cancellationToken) => Task.FromResult<CaseDetails?>(null);

        public Task<CaseHeader?> GetHeaderAsync(
            GetCaseHeaderQuery query,
            CancellationToken cancellationToken) => Task.FromResult<CaseHeader?>(null);

        public Task<IReadOnlyList<CaseSearchItem>> SearchByCursorAsync(
            CaseSearchFilters filters, CaseSearchOrder order, DateTimeOffset? afterReceivedAtUtc,
            string? afterSortText, Guid? afterId, int fetchCount, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CaseDocumentPageItem>> ListDocumentsByCursorAsync(
            Guid caseId, DateTimeOffset? afterRecordedAtUtc, Guid? afterId, int fetchCount,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CaseHistoryEntry>> ListHistoryByCursorAsync(
            Guid caseId, DateTimeOffset? afterOccurredAtUtc, Guid? afterId, int fetchCount,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
