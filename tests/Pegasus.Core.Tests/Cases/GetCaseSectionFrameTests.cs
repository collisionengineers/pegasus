using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Cases;

/// <summary>
/// <see cref="GetCaseSectionFrame"/>, the Cases queue detail's frame-only read
/// (PERF-WEB-03), applies the Case actor boundary
/// (<see cref="StaffAccessRight.PerformCasework"/>) and case-identifier
/// validation before any store call, then returns the store's frame.
/// </summary>
public sealed class GetCaseSectionFrameTests
{
    [Fact]
    public async Task RequiresAuthorizedStaffBeforeCallingTheStore()
    {
        var store = new RecordingStore();

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => new GetCaseSectionFrame(store).ExecuteAsync(
            new(Guid.NewGuid(), ActionActor.SystemWorker("case-section-frame-test")),
            CancellationToken.None));

        Assert.Empty(store.Asked);
    }

    [Fact]
    public async Task RequiresACaseIdentifier()
    {
        var store = new RecordingStore();

        await Assert.ThrowsAsync<ArgumentException>(() => new GetCaseSectionFrame(store).ExecuteAsync(
            new(Guid.Empty, ActionActor.Staff(Guid.NewGuid(), [StaffRole.User])),
            CancellationToken.None));

        Assert.Empty(store.Asked);
    }

    [Fact]
    public async Task ReturnsTheStoresFrame()
    {
        var store = new RecordingStore();
        var caseId = Guid.NewGuid();

        var frame = await new GetCaseSectionFrame(store).ExecuteAsync(
            new(caseId, ActionActor.Staff(Guid.NewGuid(), [StaffRole.User])),
            CancellationToken.None);

        Assert.Equal(caseId, Assert.Single(store.Asked));
        Assert.Same(RecordingStore.Frame, frame);
    }

    [Fact]
    public async Task ReturnsNullWhenTheStoreHasNoWorkflow()
    {
        var store = new RecordingStore { Answer = null };

        var frame = await new GetCaseSectionFrame(store).ExecuteAsync(
            new(Guid.NewGuid(), ActionActor.Staff(Guid.NewGuid(), [StaffRole.User])),
            CancellationToken.None);

        Assert.Null(frame);
        Assert.Single(store.Asked);
    }

    private sealed class RecordingStore : ICaseQueryStore
    {
        public static readonly CaseSectionFrame Frame = new(
            new CaseSearchItem(
                Guid.NewGuid(), "REF-1", null, CaseType.Inspection, "QDOS",
                CaseLifecycleState.NotReady, null, null, null, null,
                DateTimeOffset.UnixEpoch, "Email", DateTimeOffset.UnixEpoch),
            new CaseWorkflowRecord(
                Guid.NewGuid(),
                new CaseIdentity(Guid.NewGuid(), "QDOS", 2031, 1, "QDOS/2031/001"),
                CaseLifecycleState.NotReady,
                null, null, null, null, null, null, null, 1),
            null);

        public List<Guid> Asked { get; } = [];

        public CaseSectionFrame? Answer { get; init; } = Frame;

        public Task<CaseSectionFrame?> GetSectionFrameAsync(Guid caseId, CancellationToken cancellationToken)
        {
            Asked.Add(caseId);
            return Task.FromResult(Answer);
        }

        public Task<SearchCasesResult> SearchAsync(SearchCasesQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseHeader?> GetHeaderAsync(GetCaseHeaderQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CaseHistoryEntry>> ListHistoryAsync(Guid caseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CaseQueryItem>> ListCaseQueriesAsync(Guid caseId, CancellationToken cancellationToken) =>
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
