using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Cases;

/// <summary>
/// CASE-047 (Stream A review): <see cref="GetCaseHeader"/> applies the same
/// actor boundary and case-identifier validation as <see cref="GetCase"/>
/// (<see cref="StaffAccessRight.PerformCasework"/>) before delegating to the
/// store's bounded, counted read.
/// </summary>
public sealed class CaseHeaderTests
{
    [Fact]
    public async Task RequiresAuthorizedStaffBeforeCallingStore()
    {
        var store = new RecordingStore();
        var getHeader = new GetCaseHeader(store);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => getHeader.ExecuteAsync(
            new(Guid.NewGuid(), ActionActor.SystemWorker("case-header-test")),
            CancellationToken.None));

        Assert.Null(store.Query);
    }

    [Fact]
    public async Task RequiresACaseIdentifier()
    {
        var store = new RecordingStore();
        var getHeader = new GetCaseHeader(store);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        await Assert.ThrowsAsync<ArgumentException>(() => getHeader.ExecuteAsync(
            new(Guid.Empty, actor), CancellationToken.None));

        Assert.Null(store.Query);
    }

    [Fact]
    public async Task DelegatesToTheStoreAndReturnsItsHeader()
    {
        var store = new RecordingStore();
        var getHeader = new GetCaseHeader(store);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var caseId = Guid.NewGuid();

        var header = await getHeader.ExecuteAsync(new(caseId, actor), CancellationToken.None);

        Assert.NotNull(store.Query);
        Assert.Equal(caseId, store.Query!.CaseId);
        Assert.Same(actor, store.Query.Actor);
        Assert.Same(RecordingStore.Header, header);
    }

    private sealed class RecordingStore : ICaseQueryStore
    {
        public static readonly CaseHeader Header = new(
            new CaseSearchItem(
                Guid.NewGuid(), "REF-1", null, CaseType.Inspection, "QDOS",
                CaseLifecycleState.Review, null, null, null, null,
                DateTimeOffset.UnixEpoch, null, "Email", DateTimeOffset.UnixEpoch),
            new CaseWorkflowRecord(
                Guid.NewGuid(),
                new CaseIdentity(Guid.NewGuid(), "QDOS", 2031, 1, "QDOS/2031/001"),
                CaseLifecycleState.Review,
                null, null, null, null, null, null, null, 1),
            null,
            DocumentCount: 3,
            HistoryCount: 5,
            OpenTaskCount: 1);

        public GetCaseHeaderQuery? Query { get; private set; }

        public Task<SearchCasesResult> SearchAsync(SearchCasesQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseDetails?> GetAsync(GetCaseQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseHeader?> GetHeaderAsync(GetCaseHeaderQuery query, CancellationToken cancellationToken)
        {
            Query = query;
            return Task.FromResult<CaseHeader?>(Header);
        }

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
