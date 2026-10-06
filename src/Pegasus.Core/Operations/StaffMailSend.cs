using Pegasus.Core.Identity;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Operations;

public enum StaffMailPurpose { CaseReport, GeneralCorrespondence, TriageChaser, TriageOutcomeReply, CaseChaser }
public enum StaffMailComposeMode { New, Reply, ReplyAll, Forward }
public enum StaffMailState { Prepared, DraftCreating, DraftReady, Sending, Submitted, Sent, Failed, Unknown, Cancelled }
public enum StaffMailAttemptStage { CreateDraft, Attach, Send, ObserveSent }

public sealed record StaffMailRecipient(string Address, string? DisplayName);
public sealed record StaffMailOriginalMessage(
    Guid RetainedMessageId, Guid ApprovedMailboxId, string ImmutableMessageId,
    string? InternetMessageId, string? ConversationId);
public sealed record StaffMailAttachment(
    Guid? DocumentId, Guid? VersionId, string Sha256, long ContentLength,
    string FileName, string MediaType,
    Guid? IntakeAssetId = null, Guid? IntakeReceiptId = null);
public static class StaffMailCorrelationHeaders
{
    public const string OperationId = "X-Pegasus-Operation-Id";
    public const string MailboxId = "X-Pegasus-Mailbox-Id";
    public const string MailboxGeneration = "X-Pegasus-Mailbox-Generation";
    public const string PayloadSha256 = "X-Pegasus-Payload-Sha256";

    /// <summary>
    /// The domain of the Message-ID Pegasus assigns to every staff send:
    /// <c>&lt;{operationId:N}@pegasus.invalid&gt;</c>. The provider keeps the
    /// Message-ID on the Sent item where it drops the custom X- headers, so
    /// the Sent-evidence poll reads the operation from either.
    /// </summary>
    public const string MessageIdDomain = "pegasus.invalid";

    public static string MessageId(Guid operationId) => $"{operationId:N}@{MessageIdDomain}";

    /// <summary>
    /// The operation a Message-ID names, or null when it is not one Pegasus
    /// assigned. Accepts the bare and the angle-bracketed form.
    /// </summary>
    public static Guid? TryReadOperationId(string? messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return null;
        }
        var value = messageId.Trim().TrimStart('<').TrimEnd('>');
        var suffix = "@" + MessageIdDomain;
        if (!value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        var local = value[..^suffix.Length];
        return local.Length == 32
            && Guid.TryParseExact(local, "N", out var operationId)
            && operationId != Guid.Empty
                ? operationId
                : null;
    }
}
public sealed record StaffMailSendCommand(
    ActionActor Actor, Guid ApprovedMailboxId, long ExpectedMailboxGeneration,
    StaffMailPurpose Purpose, Guid ContextId, long ExpectedContextVersion,
    StaffMailComposeMode ComposeMode, StaffMailOriginalMessage? OriginalMessage,
    IReadOnlyList<StaffMailRecipient> To, IReadOnlyList<StaffMailRecipient> Cc,
    string Subject, string Body, IReadOnlyList<StaffMailAttachment> Attachments,
    string OperationKey)
{
    /// <summary>What a report send records with its operation; null for every other purpose.</summary>
    public ReportDispatchRecord? ReportDispatch { get; init; }
}
public sealed record StaffReportSendCommand(
    StaffMailSendCommand Mail, ReportSendReadinessRequest Report);
public sealed record StaffMailOperation(
    Guid Id, StaffMailState State, StaffMailAttemptStage? AttemptStage, long Version,
    DateTimeOffset PreparedAtUtc, DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ObservedSentAtUtc, string? FailureCode,
    Guid ApprovedMailboxId, long MailboxGeneration, string PayloadHash,
    DateTimeOffset? AttemptRequestedAtUtc, DateTimeOffset? UploadSessionExpiresAtUtc,
    StaffMailPurpose Purpose, Guid ContextId, long ExpectedContextVersion,
    Guid? OriginalRetainedMessageId,
    string? ReconciliationContinuation = null,
    string? DraftImmutableId = null);

public interface IStaffMailSend
{
    Task<StaffMailOperation> SendAsync(StaffMailSendCommand command, CancellationToken cancellationToken);
    Task<StaffMailOperation?> GetAsync(ActionActor actor, Guid operationId, CancellationToken cancellationToken);
    Task<StaffMailOperation?> GetLatestForOriginalAsync(
        ActionActor actor, Guid retainedMessageId, CancellationToken cancellationToken);
    Task<StaffMailOperation> CancelAsync(ActionActor actor, Guid operationId,
        long expectedVersion, CancellationToken cancellationToken);
}
public interface IStaffReportSend
{
    Task<StaffMailOperation> SendAsync(StaffReportSendCommand command, CancellationToken cancellationToken);
}
