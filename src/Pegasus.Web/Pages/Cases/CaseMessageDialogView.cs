using Pegasus.Core.Cases;

namespace Pegasus.Web.Pages.Cases;

/// <summary>The message one Case message dialog shows, and the Case/PO its Reply, Reply all and Forward carry.</summary>
public sealed record CaseMessageDialogView(CaseCorrespondenceEmail Email, string CaseReference);
