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

    // The Automation's own Glass's sessions (design 07 section 9 question 1,
    // option a): each tool's description and the sentences a refused call
    // answers with, in one place.

    // Operator, 18 November 2026: R1178
    public const string GlassSessionGet =
        "The Automation's own Glass's session on a Case: its state and version, whether the Glass's account is free or which Case holds it, each position it added or removed with Glass's own figures, and the Draft repair spec a finished session landed as. With readEstimate, Glass's is asked once for the positions the estimate now holds.";

    // Operator, 18 November 2026: R1178
    public const string GlassSessionAct =
        "Start, finish or close the Automation's own Glass's session on a Case. start makes a new Glass's estimate for the Case's vehicle and opens it, or continues the Automation's session already open on that Case. finish saves the estimate in Glass's and brings it back as a Draft repair spec; only staff put a repair spec in use. close ends the session without saving, with a reason. The Glass's account holds one session at a time: while it is in use on another Case, start is refused and names that Case. start and finish take the Case version and hold its edit lease for this one command; finish and close take the session version. A call waits up to 45 seconds; read a session that is still working with pegasus_glass_session_get.";

    // Operator, 18 November 2026: R1178
    public static string GlassAccountHeldByStaff(string caseReference) =>
        $"The Glass's account is in use by a member of staff on Case {caseReference}.";

    // Operator, 18 November 2026: R1178
    public static string GlassAccountHeldByAutomation(string caseReference) =>
        $"The Glass's account is in use by the Automation's session on Case {caseReference}; finish or close it first.";

    // Operator, 18 November 2026: R1178
    public const string GlassNoSession = "The Automation has no Glass's session open on this Case.";

    // Operator, 18 November 2026: R1178
    public const string GlassEstimateEmpty = "The estimate has no positions; close the session instead.";
}
