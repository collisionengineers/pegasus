namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The blocker edit partial's model: the page, the control on the Repair
/// Spec section that clears the blocker (<see cref="DetailsModel.BlockerEditFocus"/>),
/// the claim's words and its button class: in the Report not ready card the
/// blocker's requirement as its link, as the Next action step the section's
/// name as a full-width button.
/// </summary>
public sealed record CaseBlockerEditView(DetailsModel Page, string Focus, string Label, string ButtonClass);
