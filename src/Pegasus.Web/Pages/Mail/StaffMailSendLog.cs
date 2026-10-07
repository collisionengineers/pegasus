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
}
