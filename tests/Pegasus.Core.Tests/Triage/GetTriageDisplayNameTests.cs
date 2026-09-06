using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Triage;

/// <summary>
/// PLAT-011: the Triage history table (<c>Pages/Triage/Details.cshtml</c>) shows
/// the resolved staff name, never the raw actor subject id it used to render —
/// covers <c>GetTriage</c>'s resolution of <see cref="TriageHistoryEntry.Actor"/>.
/// </summary>
public sealed class GetTriageDisplayNameTests
{
    private static readonly DateTimeOffset NowUtc = new(2031, 9, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteResolvesHistoryActorsToStaffNamesAndNeverTheRawSubjectId()
    {
        var triageId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var record = new TriageRecord(
            triageId,
            new TriageOrigin(
                Guid.NewGuid(),
                new IntakeSourceIdentity(IntakeSourceChannel.Mailbox, "receipt-token"),
                "source-hash",
                Guid.NewGuid()),
            "AB12CDE",
            TriageState.AwaitingInformation,
            null,
            null,
            2);
        var detail = new TriageDetail(
            record,
            NowUtc.AddDays(-1),
            [],
            [],
            [
                new(
                    Guid.NewGuid(),
                    triageId,
                    "triage_assigned",
                    staffId.ToString("D"),
                    nameof(ActorKind.Staff),
                    "Assigned for review.",
                    "op-1",
                    NowUtc,
                    1,
                    2,
                    TriageState.AwaitingInformation,
                    null,
                    null)
            ],
            []);
        var queries = new Queries(detail);
        var staffAccounts = new FixedStaffAccounts(staffId, "alex");
        var sut = new GetTriage(
            queries,
            queries,
            new NoPollOutcomes(),
            staffAccounts);

        var result = await sut.ExecuteAsync(
            new(triageId, Caseworker()),
            CancellationToken.None);

        var entry = Assert.Single(result!.History);
        Assert.Equal("alex", entry.ActorDisplayName);
        Assert.DoesNotContain(staffId.ToString("D"), entry.ActorDisplayName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteFallsBackHonestlyWhenTheStaffAccountNoLongerResolves()
    {
        var triageId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var record = new TriageRecord(
            triageId,
            new TriageOrigin(
                Guid.NewGuid(),
                new IntakeSourceIdentity(IntakeSourceChannel.Mailbox, "receipt-token"),
                "source-hash",
                Guid.NewGuid()),
            "AB12CDE",
            TriageState.Open,
            null,
            null,
            1);
        var detail = new TriageDetail(
            record,
            NowUtc.AddDays(-1),
            [],
            [],
            [
                new(
                    Guid.NewGuid(),
                    triageId,
                    "triage_created",
                    staffId.ToString("D"),
                    nameof(ActorKind.Staff),
                    "Created from mailbox intake.",
                    "op-1",
                    NowUtc,
                    0,
                    1,
                    TriageState.Open,
                    null,
                    null)
            ],
            []);
        var queries = new Queries(detail);
        var sut = new GetTriage(
            queries,
            queries,
            new NoPollOutcomes(),
            new FixedStaffAccounts(Guid.NewGuid(), "someone-else"));

        var result = await sut.ExecuteAsync(
            new(triageId, Caseworker()),
            CancellationToken.None);

        var entry = Assert.Single(result!.History);
        Assert.Equal(ActorDisplayNames.UnknownStaff, entry.ActorDisplayName);
    }

    private static ActionActor Caseworker() =>
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    private sealed class Queries(TriageDetail detail) :
        ITriageQueries,
        ITriageResponseEvidenceCandidateQueries
    {
        public Task<IReadOnlyList<TriageSummary>> ListAsync(
            TriageState? state,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<TriageDetail?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<TriageDetail?>(detail);

        public Task<TriageSummary?> GetByOriginReceiptAsync(
            Guid originReceiptId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<IReadOnlyList<TriageSentEvidenceReference>> ListSentEvidenceReferencesAsync(
            Guid triageId,
            int maximumResults,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TriageSentEvidenceReference>>([]);
    }

    private sealed class NoPollOutcomes : ISentEvidencePollOutcomeQueries
    {
        public Task<IReadOnlyList<UnlinkedSentEvidenceCandidate>> ListUnlinkedReplyCandidatesAsync(
            IReadOnlyList<string> exactReplyChainIdentities,
            int maximumResults,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<UnlinkedSentEvidenceCandidate>>([]);
    }

    private sealed class FixedStaffAccounts(Guid staffId, string userName) : IStaffAccountQueries
    {
        public Task<StaffAccountQuerySlice> ListAsync(
            int offset,
            int limit,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<StaffAccountSummary?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(id == staffId
                ? new StaffAccountSummary(staffId, userName, true, false, [StaffRole.User])
                : null);

        public Task<IReadOnlyList<SignOffEngineerProfile>> ListSignOffEngineersAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<SignOffEngineerProfile?> GetSignOffEngineerAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");
    }
}
