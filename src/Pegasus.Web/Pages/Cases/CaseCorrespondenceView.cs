using Pegasus.Core.Cases;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// What the Files section's Correspondence tab draws: the retained e-mails,
/// and the Case/PO the message dialog and its Reply, Reply all and Forward
/// carry. The Case record and the Triage Case both draw it.
/// </summary>
public sealed record CaseCorrespondenceView(
    IReadOnlyList<CaseCorrespondenceEmail> Emails,
    string CaseReference)
{
    /// <summary>
    /// The Case whose Correspondence tab offers Tag as query on each received
    /// row (operator, 11 October 2026); null on a Triage Case, which takes no
    /// query.
    /// </summary>
    public Guid? TagAsQueryCaseId { get; init; }
}
