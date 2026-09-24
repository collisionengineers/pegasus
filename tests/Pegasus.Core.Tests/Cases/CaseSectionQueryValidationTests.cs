using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Cases;

public sealed class CaseSectionQueryValidationTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void RefusesAFrameWithEitherCaseIdentityMismatch(bool summaryMatches, bool workflowMatches)
    {
        var requestedCaseId = Guid.NewGuid();
        var otherCaseId = Guid.NewGuid();
        var frame = Frame(
            summaryMatches ? requestedCaseId : otherCaseId,
            workflowMatches ? requestedCaseId : otherCaseId);

        var exception = Assert.Throws<ArgumentException>(() => CaseSectionQueries.Validate(new(
            requestedCaseId,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
            Frame: frame)));

        Assert.Contains("section frame", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DirectSectionReadersReuseTheSuppliedFrame()
    {
        var caseId = Guid.NewGuid();
        var query = new GetCaseSectionQuery(
            caseId,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
            Data: Data(caseId),
            Frame: Frame(caseId));
        var store = new RecordingStore();

        var vehicle = new GetCaseVehicleSection(store, new EmptyWorkspace(), new EmptyCaseData(), new EmptyVehicleEvidence());
        var valuation = new GetCaseValuationSection(store, new EmptyWorkspace(), new EmptyCaseData());
        var vehicleResult = await vehicle.ExecuteAsync(query, CancellationToken.None);
        var valuationResult = await valuation.ExecuteAsync(query, CancellationToken.None);

        var notes = await new GetCaseNotesSection(store, new EmptyStaffAccounts())
            .ExecuteAsync(query, CancellationToken.None);
        var files = await new GetCaseFilesSection(store).ExecuteAsync(query, CancellationToken.None);

        Assert.NotNull(notes);
        Assert.Same(query.Frame, notes!.Frame);
        Assert.NotNull(files);
        Assert.Same(query.Frame, files!.Frame);
        Assert.NotNull(vehicleResult);
        Assert.Same(query.Frame, vehicleResult!.Frame);
        Assert.Same(query.Data, vehicleResult.Data);
        Assert.NotNull(valuationResult);
        Assert.Same(query.Frame, valuationResult!.Frame);
        Assert.Same(query.Data, valuationResult.Data);
        Assert.Same(query.Frame, store.FilesFrame);
        Assert.Equal(0, store.SectionFrameReads);
    }

    private static CaseSectionFrame Frame(Guid caseId) => Frame(caseId, caseId);

    private static CaseSectionFrame Frame(Guid summaryCaseId, Guid workflowCaseId)
    {
        var identity = new CaseIdentity(workflowCaseId, "QDOS", 2031, 1, "QDOS3100001");
        return new(
            new(
                summaryCaseId, identity.Reference, null, CaseType.Inspection, "QDOS",
                CaseLifecycleState.Review, null, null, null, null,
                DateTimeOffset.UnixEpoch, null, "Email", DateTimeOffset.UnixEpoch),
            new(workflowCaseId, identity, CaseLifecycleState.Review, null, null, null, null, null, null, null, 1),
            null,
            "case-root",
            CaseCustodyState.Confirmed);
    }

    private static CaseDataProjection Data(Guid caseId)
    {
        var text = new CaseField<string>(null, null, null);
        var date = new CaseField<DateOnly>(null, null, null);
        return new(
            new CaseIdentity(caseId, "QDOS", 2031, 1, "QDOS3100001"),
            new(null, null, null, null, null, null, null, null, null),
            DateTimeOffset.UnixEpoch,
            1,
            CaseLifecycleState.Review,
            new(new(false, false), new(false, "test", 1)),
            new(text),
            new(text, text, text),
            new(text),
            new(text, text, text, text, new(null, null, null), text),
            new(date, text),
            new(text, text, text),
            new(date, text),
            new(date, date, text, new(null, null, null)));
    }

    private sealed class RecordingStore : ICaseQueryStore
    {
        public int SectionFrameReads { get; private set; }
        public CaseSectionFrame? FilesFrame { get; private set; }

        public Task<SearchCasesResult> SearchAsync(SearchCasesQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseDetails?> GetAsync(GetCaseQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseHeader?> GetHeaderAsync(GetCaseHeaderQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseSectionFrame?> GetSectionFrameAsync(Guid caseId, CancellationToken cancellationToken)
        {
            SectionFrameReads++;
            throw new InvalidOperationException("A direct section must reuse its supplied frame.");
        }

        public Task<CasePageFrameData?> GetPageFrameAsync(Guid caseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CaseHistoryEntry>> ListHistoryAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CaseHistoryEntry>>([]);

        public Task<CaseFilesSectionData?> GetFilesSectionAsync(
            Guid caseId,
            bool includeDocuments,
            CaseSectionFrame? frame,
            CancellationToken cancellationToken)
        {
            FilesFrame = frame;
            return Task.FromResult<CaseFilesSectionData?>(new(
                frame ?? throw new InvalidOperationException("The direct Files section must pass its frame."),
                [],
                frame.CustodyFolderRemoteId,
                frame.CustodyState,
                []));
        }

        public Task<CaseRenderLeaseValidation?> GetRenderLeaseValidationAsync(
            Guid caseId,
            string presentedToken,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, string>> GetReferencesAsync(
            IReadOnlyCollection<Guid> caseIds,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<CaseSearchItem>> SearchByCursorAsync(
            CaseSearchFilters filters,
            CaseSearchOrder order,
            DateTimeOffset? afterReceivedAtUtc,
            string? afterSortText,
            Guid? afterId,
            int fetchCount,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<CaseDocumentPageItem>> ListDocumentsByCursorAsync(
            Guid caseId,
            DateTimeOffset? afterRecordedAtUtc,
            Guid? afterId,
            int fetchCount,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<CaseHistoryEntry>> ListHistoryByCursorAsync(
            Guid caseId,
            DateTimeOffset? afterOccurredAtUtc,
            Guid? afterId,
            int fetchCount,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class EmptyWorkspace : IGetAssessmentWorkspace
    {
        public Task<AssessmentWorkspace?> ExecuteAsync(
            GetAssessmentWorkspaceQuery query,
            CancellationToken cancellationToken) => Task.FromResult<AssessmentWorkspace?>(null);
    }

    private sealed class EmptyCaseData : ICaseDataQueries
    {
        public Task<CaseDataProjection?> GetAsync(Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
            Task.FromResult<CaseDataProjection?>(null);
    }

    private sealed class EmptyVehicleEvidence : IVehicleEvidenceQueries
    {
        public Task<CaseVehicleEvidence?> GetAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseVehicleEvidence?>(null);
    }

    private sealed class EmptyStaffAccounts : IStaffAccountQueries
    {
        public Task<StaffAccountQuerySlice> ListAsync(int offset, int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StaffAccountSummary?> GetAsync(Guid staffId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<StaffAccountSummary>> GetManyAsync(IReadOnlyCollection<Guid> staffIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SignOffEngineerProfile>> ListSignOffEngineersAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SignOffEngineerProfile?> GetSignOffEngineerAsync(Guid staffId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
