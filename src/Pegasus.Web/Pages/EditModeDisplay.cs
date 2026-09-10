using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages;

/// <summary>
/// Operator wording for the one server-owned edit authority a case carries. Other authorised staff
/// stay read-only and are told who is editing; the holder is named by staff account only, never by
/// identifier, and an unresolved holder is described without one. No colleague may take an edit
/// from another; the only take-over control is the one <see cref="HeldElsewhere"/> offers a holder
/// over their own second window.
/// </summary>
/// <remarks>
/// None of this copy names a time. An open editor's page keeps its own lease alive, so the moment
/// editing becomes available is not knowable when the sentence is written: naming one would tell a
/// colleague to come back at a time the case is still locked.
/// </remarks>
public static class EditModeDisplay
{
    public static string HeldBy(CaseEditAuthorityHolder holder, bool isSelf)
    {
        ArgumentNullException.ThrowIfNull(holder);
        return isSelf
            ? "You are editing this case."
            // A plain hyphen, not an en/em dash: the default HTML encoder emits non-ASCII as a
            // numeric entity, and this sentence is read far more often than it is authored.
            : $"Case locked - {Editor(holder)} is editing.";
    }

    /// <summary>
    /// The same disclosure where a case is reached from another workspace and the sentence has to
    /// say that case editing, not this page, is what is unavailable.
    /// </summary>
    public static string CaseHeldBy(CaseEditAuthorityHolder holder, bool isSelf)
    {
        ArgumentNullException.ThrowIfNull(holder);
        return isSelf
            ? "Case editing is unavailable here because you are editing the case elsewhere."
            : $"Case locked - {Editor(holder)} is editing the case.";
    }

    /// <summary>
    /// The same disclosure for one of the non-Case records that carry an edit scope. The holder is
    /// never named as the blocker of themselves: their own live scope belongs to another window of
    /// their own session, and the sentence says so and offers the take-over that ends it.
    /// </summary>
    /// <param name="recordName">
    /// The record as the operator reading the sentence names it, lower case unless it is a proper
    /// term ("preset", "account", "Triage record").
    /// </param>
    public static string HeldBy(string recordName, CaseEditAuthorityHolder holder, bool isSelf)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordName);
        ArgumentNullException.ThrowIfNull(holder);
        return isSelf
            ? HeldElsewhere(recordName)
            : $"{HolderName(holder)} is editing this {recordName}.";
    }

    /// <summary>
    /// The holder's own second window. This is the only edit-ownership sentence with a control
    /// beside it, because the operator it addresses is the one person entitled to end the other
    /// window's claim.
    /// </summary>
    public static string HeldElsewhere(string recordName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordName);
        return $"You are editing this {recordName} in another window.";
    }

    /// <summary>
    /// The holder as a value rather than a sentence, for the surfaces that name the edit
    /// authority beside other facts. The naming rules are the sentence's, stated once.
    /// </summary>
    public static string HolderName(CaseEditAuthorityHolder holder)
    {
        ArgumentNullException.ThrowIfNull(holder);
        return holder.IsAutomation
            ? "AI"
            : string.IsNullOrWhiteSpace(holder.DisplayName)
                ? "Another member of staff"
                : holder.DisplayName!;
    }

    /// <summary>
    /// The Automation Actor is named as itself rather than as staff, because ADR-0011 requires it to
    /// stay attributable without impersonating a person. A staff account that cannot be resolved is
    /// still described without an identifier.
    /// </summary>
    private static string Editor(CaseEditAuthorityHolder holder) =>
        holder.IsAutomation
            ? "AI"
            : string.IsNullOrWhiteSpace(holder.DisplayName)
                ? "another member of staff"
                : holder.DisplayName;
}
