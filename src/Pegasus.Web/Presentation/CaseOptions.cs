using Pegasus.Core.Cases;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The Find a Case option list a page renders for its picker: the matching
/// Cases, the select handler each option posts to, the field that carries
/// the chosen reference, and the id prefix the picker addresses options by.
/// </summary>
public sealed record CaseOptions(
    IReadOnlyList<CaseSearchItem> Items,
    string FormAction,
    string FieldName,
    string IdPrefix);
