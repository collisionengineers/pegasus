namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The blocker edit partial's model: the page, and the control on the Repair
/// Spec section that clears the blocker (<see cref="DetailsModel.BlockerEditFocus"/>).
/// </summary>
public sealed record CaseBlockerEditView(DetailsModel Page, string Focus);
