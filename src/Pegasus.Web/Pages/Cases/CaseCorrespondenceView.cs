using Pegasus.Core.Cases;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// What the Files section's Correspondence tab draws: the retained e-mails.
/// The Case record and the Triage Case both draw it.
/// </summary>
public sealed record CaseCorrespondenceView(
    IReadOnlyList<CaseCorrespondenceEmail> Emails)
{
    /// <summary>
    /// The Case whose Correspondence tab offers Open query on a received
    /// e-mail that holds no non-withdrawn query, once the report is sent.
    /// Null when Open query is not offered (a Triage Case, or the report is
    /// not yet sent).
    /// </summary>
    public Guid? OpenQueryCaseId { get; init; }
}
