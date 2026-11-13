namespace Pegasus.Web.Mcp;

/// <summary>
/// The parameter descriptions many Automation tools share, word for word, so
/// one sentence is written once.
/// </summary>
internal static class AutomationToolText
{
    public const string CaseId = "The durable Pegasus case identifier.";

    public const string ExpectedCaseVersion = "The case version the caller observed; a stale value fails closed.";

    public const string OperationKey = "Caller idempotency key prefixed 'mcp:'.";

    public const string EditLeaseToken = "Edit lease token from pegasus_edit_begin for multi-step work, presented on every write until pegasus_edit_end; omit it and the tool holds the lease for this one command.";

    public const string Cursor = "Opaque cursor returned by the previous page; omit for the first page.";

    public const string PageSize = "Page size between 1 and 100; omit for 50.";
}
