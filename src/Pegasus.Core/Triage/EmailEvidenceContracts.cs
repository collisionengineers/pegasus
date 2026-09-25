using Pegasus.Core.Identity;

namespace Pegasus.Core.Triage;

public sealed record SentEmailEvidence(
    Guid Id,
    Guid CaseId,
    string MessageIdentity,
    string Subject,
    IReadOnlyList<string> Recipients,
    string MimeSha256,
    DateTimeOffset SentAtUtc,
    DateTimeOffset ChaseDueAtUtc,
    long Version);

public sealed record RecordSentEmailEvidenceRequest(
    Guid CaseId,
    long ExpectedTriageVersion,
    string MessageIdentity,
    string Subject,
    IReadOnlyList<string> Recipients,
    string MimeSha256,
    DateTimeOffset SentAtUtc,
    DateTimeOffset ChaseDueAtUtc,
    string Actor,
    string OperationKey);

public sealed record RecordEmailResponseEvidenceRequest(
    Guid SentEvidenceId,
    long ExpectedSentEvidenceVersion,
    Guid PollOutcomeId,
    string PollLeaseToken,
    string MailboxId,
    string MailboxAddress,
    string SentFolderIdentity,
    string ImmutableItemIdentity,
    string MessageIdentity,
    string ConversationIdentity,
    string ReplyChainIdentity,
    IReadOnlyList<string> InReplyToIdentities,
    string SourceOccurrenceIdentity,
    string SourceSha256,
    string CurrentLocationIdentity,
    string MimeSha256,
    DateTimeOffset SentAtUtc,
    DateTimeOffset DiscoveredAtUtc,
    ActionActor Actor,
    string OperationKey,
    string PollOutcomeOperationKey,
    string CursorAfterItem,
    string Reason);

public sealed record ExactEmailResponseEvidenceCandidate(
    Guid SentEvidenceId,
    long ExpectedSentEvidenceVersion,
    string ReplyChainIdentity,
    string? RecordedResponseMessageIdentity);

public interface IRecordSentEmailEvidence
{
    Task<SentEmailEvidence> ExecuteAsync(
        RecordSentEmailEvidenceRequest request,
        CancellationToken cancellationToken);
}

public interface IRecordEmailResponseEvidence
{
    Task ExecuteAsync(
        RecordEmailResponseEvidenceRequest request,
        CancellationToken cancellationToken);
}

public interface IExactEmailResponseEvidenceQueries
{
    Task<IReadOnlyList<ExactEmailResponseEvidenceCandidate>> FindExactCandidatesAsync(
        IReadOnlyList<string> replyChainIdentities,
        CancellationToken cancellationToken);
}
