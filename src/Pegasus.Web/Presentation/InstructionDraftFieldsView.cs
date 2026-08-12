using Pegasus.Core.Intake;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The editable instruction detail, and what extraction found behind each
/// value.
/// </summary>
/// <remarks>
/// Two screens ask for the same eleven values: the create screen, where a
/// person reviews or keys them while a reference is allocated, and the
/// received-item screen, where a blocked item is corrected so it has a route
/// forward. Two Web callers of one Core use case is not duplicate business
/// implementation — <see cref="IResolveIntake"/> owns the rule either way — but
/// a second copy of this markup would be a second place to forget a field. So
/// the markup lives once, in <c>_InstructionDraftFields.cshtml</c>, and this
/// says what to render.
///
/// The input names are the property names of the create screen's bound fields.
/// Handler-parameter binding is case-insensitive, so the same markup posts
/// correctly into the received-item screen's correction handler.
/// </remarks>
/// <param name="Draft">
/// The values to prefill. Every member may be null: a source that yielded no
/// text at all renders the same form, empty, for a person to key.
/// </param>
/// <param name="ExtractedFields">
/// What extraction proposed, shown beneath each input so the operator can see
/// what they are agreeing with or overruling. Empty for a hand-keyed item.
/// </param>
/// <param name="IncludePrincipalCode">
/// Whether to render the suggested principal here. The create screen asks for
/// the confirmed principal in its own section instead, because that is the
/// value the reference is allocated against.
/// </param>
/// <param name="IncludeInspectionAddress">
/// Whether to render the inspection address here. The create screen asks for
/// it separately, because EXT-18 requires an explicit choice between the
/// address that was found and one a person states.
/// </param>
public sealed record InstructionDraftFieldsView(
    InstructionDraft? Draft,
    IReadOnlyList<InstructionReviewField> ExtractedFields,
    bool IncludePrincipalCode,
    bool IncludeInspectionAddress)
{
    /// <summary>
    /// The candidate values extraction offered for a named field, in the
    /// operator's own field names.
    /// </summary>
    public IReadOnlyList<InstructionFieldCandidate> CandidatesFor(string fieldName) =>
        ExtractedFields
            .Where(field => string.Equals(field.Name, fieldName, StringComparison.Ordinal))
            .SelectMany(field => field.Candidates)
            .ToArray();

    /// <summary>
    /// Where the value in the box came from, in one word, per the operator
    /// notes' provenance rule. A value extraction proposed reads "Extracted";
    /// anything else on the screen is what a person put there.
    /// </summary>
    public string ProvenanceWord(string fieldName) =>
        CandidatesFor(fieldName).Count > 0 ? "Extracted" : "Staff";
}
