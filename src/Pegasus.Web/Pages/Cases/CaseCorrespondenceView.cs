using Pegasus.Core.Cases;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// What the Files section's Correspondence tab draws: the retained e-mails,
/// the Case/PO the message dialog and its Reply, Reply all and Forward carry,
/// and whether the tab offers Compose. The Case record and the Triage Case
/// both draw it.
/// </summary>
public sealed record CaseCorrespondenceView(
    IReadOnlyList<CaseCorrespondenceEmail> Emails,
    string CaseReference,
    bool OffersCompose);
