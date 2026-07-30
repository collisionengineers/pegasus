using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Triage;

public sealed record SentEmailEvidence(
    Guid Id,
    Guid TriageId,
    string MessageIdentity,
    string Subject,
    IReadOnlyList<string> Recipients,
    string MimeSha256,
    DateTimeOffset SentAtUtc,
    DateTimeOffset ChaseDueAtUtc,
    long Version);

public sealed record RecordSentEmailEvidenceRequest(
    Guid TriageId,
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


public sealed record SentEmailEvidenceReplay(
    string ReplayId,
    Guid TriageId,
    long ExpectedTriageVersion,
    string MessageIdentity,
    string Subject,
    IReadOnlyList<string> Recipients,
    string MimeSha256,
    DateTimeOffset SentAtUtc,
    DateTimeOffset ChaseDueAtUtc);

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


public sealed class ReplaySentEmailEvidence(IRecordSentEmailEvidence recordSentEmailEvidence)
{
    private const int MaximumActorLength = 200;
    private const string SystemWorkerActorPrefix = "system-worker:";

    public Task<SentEmailEvidence> ExecuteAsync(
        SentEmailEvidenceReplay replay,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replay);
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Kind != ActorKind.SystemWorker)
        {
            throw new UnauthorizedAccessException("Sent email evidence replay requires a system-worker actor.");
        }

        Validate(replay);
        var actorCode = $"{SystemWorkerActorPrefix}{actor.SubjectId}";
        if (actorCode.Length > MaximumActorLength)
        {
            throw new ArgumentException(
                $"The system-worker identity must be {MaximumActorLength - SystemWorkerActorPrefix.Length} characters or fewer.",
                nameof(actor));
        }

        return recordSentEmailEvidence.ExecuteAsync(
            new(
                replay.TriageId,
                replay.ExpectedTriageVersion,
                replay.MessageIdentity.Trim(),
                replay.Subject.Trim(),
                replay.Recipients.Select(recipient => recipient.Trim()).ToArray(),
                replay.MimeSha256.ToLowerInvariant(),
                replay.SentAtUtc,
                replay.ChaseDueAtUtc,
                actorCode,
                CreateOperationKey(replay.ReplayId)),
            cancellationToken);
    }

    private static void Validate(SentEmailEvidenceReplay replay)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replay.ReplayId);
        ArgumentException.ThrowIfNullOrWhiteSpace(replay.MessageIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(replay.Subject);
        ArgumentNullException.ThrowIfNull(replay.Recipients);
        ArgumentException.ThrowIfNullOrWhiteSpace(replay.MimeSha256);
        if (replay.TriageId == Guid.Empty)
        {
            throw new ArgumentException("Sent email evidence must identify a triage record.", nameof(replay));
        }

        if (replay.ExpectedTriageVersion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(replay), "The expected triage version cannot be negative.");
        }

        if (replay.ReplayId.Length > 200 || replay.MessageIdentity.Trim().Length > 200)
        {
            throw new ArgumentException("The replay and message identities cannot exceed 200 characters.", nameof(replay));
        }

        if (replay.Subject.Trim().Length > 500)
        {
            throw new ArgumentException("The email subject cannot exceed 500 characters.", nameof(replay));
        }

        if (replay.Recipients.Count == 0 || replay.Recipients.Count > 100
            || replay.Recipients.Any(recipient => string.IsNullOrWhiteSpace(recipient)
                || recipient.Trim().Length > 320
                || recipient.Any(char.IsControl)))
        {
            throw new ArgumentException("Sent email evidence requires between one and 100 valid recipient addresses.", nameof(replay));
        }

        if (replay.MimeSha256.Length != 64 || replay.MimeSha256.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException("The MIME evidence SHA-256 must be 64 hexadecimal characters.", nameof(replay));
        }

        if (replay.ChaseDueAtUtc <= replay.SentAtUtc)
        {
            throw new ArgumentException("The chase due time must be after the sent time.", nameof(replay));
        }
    }

    private static string CreateOperationKey(string replayId)
    {
        var identityBytes = Encoding.UTF8.GetBytes(replayId);
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(identityBytes, hash);
        return $"email-evidence-replay:{Convert.ToHexString(hash)}";
    }
}
