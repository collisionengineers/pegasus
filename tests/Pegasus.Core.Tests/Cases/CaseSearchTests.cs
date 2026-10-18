using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

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

    [Fact]
    public async Task StateFilterIsDistinctOrderedAndEmptyMeansEveryState()
    {
        var store = new RecordingStore();
        var search = new SearchCases(store);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        await search.ExecuteAsync(
            new(actor, new CaseSearchFilters(States:
                [CaseLifecycleState.ReportSent, CaseLifecycleState.ReportPreparation, CaseLifecycleState.ReportSent])),
            default);
        Assert.Equal(
            new[] { CaseLifecycleState.ReportPreparation, CaseLifecycleState.ReportSent },
            store.Query!.Filters.States);

        await search.ExecuteAsync(new(actor, new CaseSearchFilters(States: [])), default);
        Assert.Null(store.Query!.Filters.States);
    }

    [Fact]
    public async Task UndefinedStateInTheFilterNeverCallsStore()
    {
        var store = new RecordingStore();
        var search = new SearchCases(store);

        await Assert.ThrowsAsync<ArgumentException>(() => search.ExecuteAsync(
            new(
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
                new CaseSearchFilters(States: [CaseLifecycleState.Review, (CaseLifecycleState)999])),
            default));

        Assert.Null(store.Query);
    }

    [Fact]
    public async Task CircumstancesRequireAuthorizedStaffBeforeCallingStore()
    {
        var circumstances = new RecordingCircumstances("Rear-ended at lights");
        var read = new GetCaseCircumstances(circumstances);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => read.ExecuteAsync(
            ActionActor.SystemWorker("case-circumstances-test"),
            Guid.NewGuid(),
            default));

        Assert.Empty(circumstances.CaseIds);
    }

    [Fact]
    public async Task CircumstancesAreReadForTheCaseTheStaffActorNames()
    {
        var caseId = Guid.NewGuid();
        var circumstances = new RecordingCircumstances("Rear-ended at lights");
        var read = new GetCaseCircumstances(circumstances);

        var text = await read.ExecuteAsync(
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
            caseId,
            default);

        Assert.Equal("Rear-ended at lights", text);
        Assert.Equal(caseId, Assert.Single(circumstances.CaseIds));
    }

    private sealed class RecordingCircumstances(string? answer) : ICaseCircumstancesQueries
    {
        public List<Guid> CaseIds { get; } = [];

        public Task<string?> GetAsync(Guid caseId, CancellationToken cancellationToken)
        {
            CaseIds.Add(caseId);
            return Task.FromResult(answer);
        }
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

        public Task<CaseHeader?> GetHeaderAsync(
            GetCaseHeaderQuery query,
            CancellationToken cancellationToken) => Task.FromResult<CaseHeader?>(null);

        public Task<IReadOnlyList<CaseHistoryEntry>> ListHistoryAsync(Guid caseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CaseQueryItem>> ListCaseQueriesAsync(Guid caseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseSectionFrame?> GetSectionFrameAsync(Guid caseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CasePageFrameData?> GetPageFrameAsync(Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseFilesSectionData?> GetFilesSectionAsync(Guid caseId, bool includeDocuments, CaseSectionFrame? frame, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseRenderLeaseValidation?> GetRenderLeaseValidationAsync(Guid caseId, string presentedToken, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, string>> GetReferencesAsync(IReadOnlyCollection<Guid> caseIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

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
