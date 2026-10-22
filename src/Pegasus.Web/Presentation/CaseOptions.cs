namespace Pegasus.Web.Presentation;

/// <summary>One option of a Case picker's list: the Case or Triage Case by its reference.</summary>
public sealed record CaseOption(string Reference, string? Registration, string? Claimant);

/// <summary>
/// The Find a Case option list a page renders for its picker: the matching
/// Cases, the id prefix the picker addresses options by and, where choosing
/// an option posts, the select handler it posts to and the field that carries
/// the chosen reference. With no handler an option only fills the picker's
/// field (the Inbox's Case / PO, whose reason dialog confirms the link, and
/// the Triage Link case field, whose own form then posts it).
/// </summary>
public sealed record CaseOptions(
    IReadOnlyList<CaseOption> Items,
    string IdPrefix,
    string? FormAction = null,
    string? FieldName = null);
