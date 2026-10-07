using Pegasus.Core.Cases;
using Pegasus.Core.Intake;

namespace Pegasus.Core.PrincipalApi;

/// <summary>
/// How a declared instruction is recorded: its policy and reader identity, the
/// labels its files are retained under, and the review fields and evidence it
/// produces. Which fields must be present is not restated here —
/// <see cref="InstructionDraftCompleteness"/> already owns both the required
/// and the identity-critical lists, and callers ask it.
/// </summary>
public static class PrincipalInstructionPolicy
{
    public const string PolicyKey = "principal_api_declared_instruction";
    public const int PolicyVersion = 1;

    /// <summary>
    /// The reader identity a declared instruction is recorded under. No file was
    /// parsed to obtain these values and the receipt must not claim one was.
    /// </summary>
    public const string ReaderKey = "principal_api_declaration";
    public const string ReaderVersion = "1";

    /// <summary>The retained source's own name and type: the request as sent.</summary>
    public const string SourceFileName = "instruction.json";
    public const string SourceMediaType = "application/json";

    /// <summary>
    /// The source label the declared original report is retained under. It is a
    /// fixed name rather than an ordinal so that the Audit evidence can find the
    /// report without a second record of which file it was.
    /// </summary>
    public const string OriginalReportSourceLabel = "principal-original-report";

    /// <summary>Every other submitted file, labelled by its position in the request.</summary>
    public static string AssetSourceLabel(int index) => $"principal-file:{index}";

    /// <summary>
    /// One review field per declared value, each with exactly one candidate
    /// naming the declaration as its source. The case snapshot refuses a draft
    /// value with no unambiguous provenance, and this is that provenance: the
    /// Principal said so.
    /// </summary>
    public static IReadOnlyList<InstructionReviewField> ReviewFields(InstructionDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return InstructionDraftFields.All
            .Select(field => (field.Label, Value: field.Value(draft)))
            .Where(field => !string.IsNullOrWhiteSpace(field.Value))
            .Select(field => new InstructionReviewField(
                field.Label,
                field.Value,
                [new(field.Value!, IntakeEvidenceSource.PrincipalDeclaration, PolicyKey)],
                IsDefaulted: false,
                HasConflict: false))
            .ToArray();
    }

    /// <summary>
    /// The evidence a declared Triage carries. It is the same shape the accepted
    /// route classification produces, because the gate downstream reads exactly
    /// one Strong AcceptedTriageMatch with a matcher key and version — a
    /// declaration satisfies it as an e-mail tell does.
    /// </summary>
    public static IntakeEvidence TriageEvidence() =>
        new(
            IntakeEvidenceSource.PrincipalDeclaration,
            IntakeEvidenceStrength.Strong,
            IntakeEvidenceFinding.AcceptedTriageMatch,
            PrincipalInstructionVocabulary.WireName(CaseType.Triage),
            "The authenticated Principal declared this submission a Triage request.",
            PolicyKey,
            PolicyVersion);

    public static IntakeEvidence DeclarationEvidence(CaseType caseType) =>
        new(
            IntakeEvidenceSource.PrincipalDeclaration,
            IntakeEvidenceStrength.Strong,
            IntakeEvidenceFinding.Information,
            PrincipalInstructionVocabulary.WireName(caseType),
            "The authenticated Principal declared this instruction over the Principal API.",
            PolicyKey,
            PolicyVersion);
}
