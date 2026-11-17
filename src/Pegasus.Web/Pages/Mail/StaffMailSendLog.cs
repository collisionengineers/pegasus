namespace Pegasus.Web.Pages.Mail;

/// <summary>
/// The one log line both composers write when the provider refuses a staff
/// send. The failure code is the engine's; no provider response body is logged.
/// </summary>
internal static partial class StaffMailSendLog
{
    [LoggerMessage(
        EventId = 7401,
        Level = LogLevel.Warning,
        Message = "Staff mail operation {OperationId} from mailbox {MailboxId} was refused: {FailureCode}.")]
    public static partial void Refused(ILogger logger, Guid operationId, Guid mailboxId, string? failureCode);

    /// <summary>
    /// A send the engine recorded Unknown, which the page now shows in place
    /// rather than letting it reach the error page (R1069): the provider's
    /// fault is logged in full, as the host logged it before.
    /// </summary>
    [LoggerMessage(
        EventId = 7402,
        Level = LogLevel.Error,
        Message = "Staff mail operation {OperationId} from mailbox {MailboxId} has an unknown provider outcome.")]
    public static partial void OutcomeUnknown(
        ILogger logger, Guid operationId, Guid mailboxId, Exception exception);
}
