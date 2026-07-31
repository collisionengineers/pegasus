using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Workflow;

public sealed class ApprovedMailboxReportSentEvidenceTests
{
    private static readonly DateTimeOffset SentAtUtc =
        new(2026, 7, 29, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RetentionRequiresTrustedSystemWorker()
    {
        var store = new RecordingStore();
        var command = new RetainApprovedMailboxReportSentEvidence(store);
        var request = Request(ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]));

        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => command.ExecuteAsync(request, default));
        Assert.Empty(store.Requests);
    }

    [Fact]
    public async Task RetentionRejectsDiscoveryBeforeAuthoritativeSentTime()
    {
        var store = new RecordingStore();
        var command = new RetainApprovedMailboxReportSentEvidence(store);
        var request = Request(ActionActor.SystemWorker("sent-evidence-poll")) with
        {
            DiscoveredAtUtc = SentAtUtc.AddTicks(-1)
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => command.ExecuteAsync(request, default));
        Assert.Empty(store.Requests);
    }

    [Fact]
    public async Task ValidSystemEvidenceReachesRetentionPort()
    {
        var store = new RecordingStore();
        var command = new RetainApprovedMailboxReportSentEvidence(store);
        var request = Request(ActionActor.SystemWorker("sent-evidence-poll"));

        var result = await command.ExecuteAsync(request, default);

        Assert.Equal(request.EvidenceId, result.EvidenceId);
        Assert.Single(store.Requests);
    }

    private static RetainApprovedMailboxReportSentEvidenceRequest Request(ActionActor actor) => new(
        Guid.NewGuid(),
        "approved@example.test",
        "sent-items",
        "immutable-item",
        "<message@example.test>",
        "conversation",
        "reply-chain",
        "source-occurrence",
        new string('A', 64),
        new string('B', 64),
        SentAtUtc,
        SentAtUtc.AddMinutes(1),
        actor,
        "retain-report-sent");

    private sealed class RecordingStore : IApprovedMailboxReportSentEvidenceStore
    {
        public List<RetainApprovedMailboxReportSentEvidenceRequest> Requests { get; } = [];

        public Task<RetainedApprovedMailboxReportSentEvidence> RetainAsync(
            RetainApprovedMailboxReportSentEvidenceRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new RetainedApprovedMailboxReportSentEvidence(
                request.EvidenceId,
                request.MailboxIdentity,
                request.SentFolderIdentity,
                request.ImmutableItemIdentity,
                request.InternetMessageIdentity,
                request.ConversationIdentity,
                request.ReplyChainIdentity,
                request.SourceOccurrenceIdentity,
                request.SourceSha256,
                request.MimeSha256,
                request.SentAtUtc,
                request.DiscoveredAtUtc,
                request.DiscoveredBy));
        }

        public Task<RetainedApprovedMailboxReportSentEvidence?> GetAsync(
            Guid evidenceId,
            CancellationToken cancellationToken) => Task.FromResult<RetainedApprovedMailboxReportSentEvidence?>(null);

        public Task<IReadOnlyList<RetainedApprovedMailboxReportSentEvidence>> ListUnlinkedAsync(
            int maximumResults,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RetainedApprovedMailboxReportSentEvidence>>([]);
    }
}
