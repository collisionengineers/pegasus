using Pegasus.Core.Intake;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The codes a mail folder and the mail freshness are written as: the Inbox's
/// route values and refresh status, and the Automation MCP mail tools alike.
/// </summary>
public static class MailListCodes
{
    public static string FolderCode(MailFolderScope folder) => folder switch
    {
        MailFolderScope.Inbox => "inbox",
        MailFolderScope.Sent => "sent",
        MailFolderScope.Upload => "upload",
        _ => throw new InvalidOperationException($"Unknown mail folder scope '{(int)folder}'.")
    };

    public static string FreshnessStatus(MailFreshnessState state) => state switch
    {
        MailFreshnessState.Current => "current",
        MailFreshnessState.Stale => "stale",
        MailFreshnessState.Unavailable => "unavailable",
        _ => "unavailable"
    };
}
