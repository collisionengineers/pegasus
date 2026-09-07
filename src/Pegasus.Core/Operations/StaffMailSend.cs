using Pegasus.Core.Identity;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Operations;

public enum StaffMailPurpose { CaseReport, GeneralCorrespondence, TriageChaser }
public enum StaffMailComposeMode { New, Reply, ReplyAll, Forward }
public enum StaffMailState { Prepared, DraftCreating, DraftReady, Sending, Submitted, Sent, Failed, Unknown, Cancelled }
public enum StaffMailAttemptStage { CreateDraft, Attach, Send, ObserveSent }

public sealed record StaffMailRecipient(string Address, string? DisplayName);
public sealed record StaffMailOriginalMessage(
    Guid RetainedMessageId, Guid ApprovedMailboxId, string ImmutableMessageId,
    string? InternetMessageId, string? ConversationId);
public sealed record StaffMailAttachment(
    Guid DocumentId, Guid VersionId, string Sha256, long ContentLength,
    string FileName, string MediaType);
public sealed record StaffMailSendCommand(
    ActionActor Actor, Guid ApprovedMailboxId, long ExpectedMailboxGeneration,
    StaffMailPurpose Purpose, Guid ContextId, long ExpectedContextVersion,
    StaffMailComposeMode ComposeMode, StaffMailOriginalMessage? OriginalMessage,
    IReadOnlyList<StaffMailRecipient> To, IReadOnlyList<StaffMailRecipient> Cc,
    string Subject, string Body, IReadOnlyList<StaffMailAttachment> Attachments,
    string OperationKey);
public sealed record StaffReportSendCommand(
    StaffMailSendCommand Mail, ReportSendReadinessRequest Report);
public sealed record StaffMailOperation(
    Guid Id, StaffMailState State, StaffMailAttemptStage? AttemptStage, long Version,
    DateTimeOffset PreparedAtUtc, DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ObservedSentAtUtc, string? FailureCode,
    Guid ApprovedMailboxId, long MailboxGeneration, string PayloadHash,
    DateTimeOffset? AttemptRequestedAtUtc, DateTimeOffset? UploadSessionExpiresAtUtc);

public interface IStaffMailSend
{
    Task<StaffMailOperation> SendAsync(StaffMailSendCommand command, CancellationToken cancellationToken);
    Task<StaffMailOperation?> GetAsync(ActionActor actor, Guid operationId, CancellationToken cancellationToken);
    Task<StaffMailOperation?> GetLatestForOriginalAsync(
        ActionActor actor, Guid retainedMessageId, CancellationToken cancellationToken);
    Task<StaffMailOperation> ReconcileAsync(ActionActor actor, Guid operationId,
        long expectedVersion, CancellationToken cancellationToken);
    Task<StaffMailOperation> CancelAsync(ActionActor actor, Guid operationId,
        long expectedVersion, CancellationToken cancellationToken);
}
public interface IStaffReportSend
{
    Task<StaffMailOperation> SendAsync(StaffReportSendCommand command, CancellationToken cancellationToken);
}
