using Pegasus.Core.Cases;
using Pegasus.Core.Intake;

namespace Pegasus.Core.PrincipalApi;

/// <summary>
/// What a Principal says it is instructing (API-01; FRD-09 § Accepted API-01
/// submission contract): the Case type, the verdict on the original report an
/// Audit audits, and the instruction fields as the draft every downstream
/// owner already reads.
///
/// The first accepted contract read the business values back out of the
/// submitted documents through the Principal's extraction policy. That policy
/// recognises QDOS only, so every other Principal was retained for sorting and
/// never allocated — and it had no caller, because QDOS arrives by e-mail and a
/// Principal integrating over HTTP already holds the fields. The operator
/// replaced it with this declared contract on 2026-08-28.
///
/// A declaration is evidence like any other: it is recorded with its own
/// provenance (<see cref="IntakeEvidenceSource.PrincipalDeclaration"/>) and is
/// never confused with something a document said or a person keyed. The draft
/// carries no Principal code: the credential decides the Principal, and
/// processing stamps it from the submission's binding.
/// </summary>
public sealed record PrincipalInstruction(
    CaseType CaseType,
    AuditAssessment? OriginalReportVerdict,
    InstructionDraft Draft);

/// <summary>
/// One declared field the Principal got wrong, named by its path in the request
/// body so the refusal can say which field and why. This is deliberately not an
/// <see cref="ArgumentException"/>: the fault is in a submitted document's
/// field, not in a method parameter, and reporting it as the latter both
/// misnames the fault and misleads the caller.
/// </summary>
public sealed class PrincipalInstructionValidationException(string field, string message)
    : Exception(message)
{
    public string Field { get; } = field;
}

/// <summary>
/// The wire vocabulary, in one place. The values are the operator's own words
/// (2026-08-28) and are matched case-insensitively. Inspection and Audit is
/// <c>auditreport</c> on the wire: Collision Engineers inspects, then audits
/// its own report. A declared verdict decides the Audit reference prefix
/// (operator, 2026-08-28).
/// </summary>
public static class PrincipalInstructionVocabulary
{
    public static readonly IReadOnlyDictionary<string, CaseType> CaseTypes =
        new Dictionary<string, CaseType>(StringComparer.OrdinalIgnoreCase)
        {
            ["inspection"] = CaseType.Inspection,
            ["audit"] = CaseType.Audit,
            ["auditreport"] = CaseType.InspectionAndAudit,
            ["triage"] = CaseType.Triage
        };

    public static readonly IReadOnlyDictionary<string, AuditAssessment> ReportVerdicts =
        new Dictionary<string, AuditAssessment>(StringComparer.OrdinalIgnoreCase)
        {
            ["repairable"] = AuditAssessment.Repairable,
            ["total-loss"] = AuditAssessment.TotalLoss
        };

    public static string WireName(CaseType caseType) =>
        CaseTypes.Single(pair => pair.Value == caseType).Key;
}
