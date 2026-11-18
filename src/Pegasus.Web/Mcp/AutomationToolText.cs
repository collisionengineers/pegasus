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

    // Operator, 18 November 2026: R1178
    public const string GlassCatalogueSearch =
        "Search the open Glass's estimate's part list and its special operations by name, part code or part number. Each row carries the value pegasus_glass_position_act adds it by, its side, and whether its part can be painted. Glass's is asked at most twice; nothing changes.";

    // Operator, 18 November 2026: R1178
    public const string GlassPositionAct =
        "Add a position to the Automation's open Glass's estimate, or remove one. add takes a row pegasus_glass_catalogue_search returned and posts Glass's own dialog as Glass's serves it: no price, hours or part number is typed. A part is added only when its row needs no equipment answer; otherwise it is refused with the reason and nothing is sent. remove takes a position id. Each takes the session version and an operation key; the same key answers the recorded outcome and never sends twice. not_confirmed means Glass's did not confirm the change: read the estimate before adding the same position again.";

    // The parameter and field descriptions and the refusals of the catalogue
    // search and the position act that the ruled set (R1178) does not hold.

    public const string GlassSearchTerm = "Matched against each row's description, part code or part number, ignoring case; 1 to 200 characters.";

    public const string GlassSearchKind = "part, special_operation or any; omit for any.";

    public const string GlassSearchSide = "L or R: only parts of that side, and no special operations.";

    public const string GlassSearchTermRefused = "term must be 1 to 200 characters.";

    public const string GlassSearchKindRefused = "kind must be one of: part, special_operation, any.";

    public const string GlassSideRefused = "side must be L or R.";

    public const string GlassCatalogueRow = "The value pegasus_glass_position_act adds the row by: a special operation's code, or a part's list row.";

    public const string GlassCatalogueKind = "part or special_operation.";

    public const string GlassCatalogueSide = "L, R, both, or none for a part with no side.";

    public const string GlassCataloguePaintable = "Whether the part can be painted.";

    public const string GlassCatalogueCode = "The glass.* code when Glass's did not answer a list; no rows are then answered.";

    public const string GlassExpectedSessionVersion = "The session version from pegasus_glass_session_get or the last Glass's tool answer; a stale value fails closed.";

    public const string GlassPositionAction = "add or remove.";

    public const string GlassAddKind = "add: part or special_operation.";

    public const string GlassAddRow = "add: the row value pegasus_glass_catalogue_search returned.";

    public const string GlassAddOperation = "add, a part only: part, repair or remove_and_refit; part_and_paint and paint are refused with their reason and nothing is sent.";

    public const string GlassAddSide = "add, a part only: L or R, checked against the row before anything is sent.";

    public const string GlassRemovePositionId = "remove: a position id from the acts or the estimate.";

    public const string GlassPositionActionRefused = "The action must be one of: add, remove.";

    public const string GlassAddKindRefused = "kind must be one of: part, special_operation.";

    public const string GlassAddRowRefused = "row is required for add.";

    public const string GlassAddOperationRefused = "operation must be one of: part, part_and_paint, paint, repair, remove_and_refit.";

    public const string GlassAddOperationMissing = "operation is required for a part.";

    public const string GlassAddOperationNotForSpecial = "operation is given for a part only.";

    public const string GlassRemovePositionIdRefused = "positionId is required for remove.";

    public const string GlassSessionWorking = "The Automation's Glass's session is working; read it with pegasus_glass_session_get and act again once it is not.";

    public const string GlassActOutcome = "pending, added, removed, not_confirmed or refused.";

    public const string GlassActCode = "The glass.* code of a refusal, or of a change Glass's did not confirm.";

    public const string GlassActPositionIds = "The positions the add put on the estimate.";

    public const string GlassActDescription = "The description Glass's dialog served.";

    public const string GlassActPrice = "The price Glass's dialog served.";

    public const string GlassActHours = "The hours Glass's dialog served.";

    public const string GlassActMandatoryParts = "The parts Glass's listed as mandatory; only those it ticked itself were added.";

    public const string GlassActs = "Each position act the Automation asked for on this session, in order.";
}
