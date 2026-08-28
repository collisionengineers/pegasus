using System.Globalization;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Intake;

namespace Pegasus.Core.ProviderApi;

/// <summary>
/// What a provider says it is instructing (API-01; FRD-09 § Accepted API-01
/// submission contract).
///
/// The first accepted contract read the business values back out of the
/// submitted documents through the Principal's extraction policy. That policy
/// recognises QDOS only, so every other Principal was retained for sorting and
/// never allocated — and it had no caller, because QDOS arrives by e-mail and a
/// provider integrating over HTTP already holds the fields. The operator
/// replaced it with this declared contract on 2026-08-28.
///
/// A declaration is evidence like any other: it is recorded with its own
/// provenance (<see cref="IntakeEvidenceSource.ProviderDeclaration"/>) and is
/// never confused with something a document said or a person keyed.
/// </summary>
public enum ProviderInstructionKind
{
    Inspection,
    Audit,
    AuditReport,
    Triage
}

/// <summary>
/// The wire vocabulary, in one place. The values are the operator's own words
/// (2026-08-28) and are matched case-insensitively; the mapping onto the
/// domain's <see cref="CaseType"/> is stated here rather than inferred at each
/// call site.
/// </summary>
public static class ProviderInstructionKinds
{
    public const string Inspection = "inspection";
    public const string Audit = "audit";
    public const string AuditReport = "auditreport";
    public const string Triage = "triage";

    public static readonly string[] All = [Inspection, Audit, AuditReport, Triage];

    public static ProviderInstructionKind Parse(string? value)
    {
        var normalized = value?.Trim();
        return normalized switch
        {
            not null when Matches(normalized, Inspection) => ProviderInstructionKind.Inspection,
            not null when Matches(normalized, Audit) => ProviderInstructionKind.Audit,
            not null when Matches(normalized, AuditReport) => ProviderInstructionKind.AuditReport,
            not null when Matches(normalized, Triage) => ProviderInstructionKind.Triage,
            _ => throw new ArgumentException(
                $"The case type must be one of: {string.Join(", ", All)}.",
                nameof(value))
        };
    }

    public static string Format(ProviderInstructionKind kind) => kind switch
    {
        ProviderInstructionKind.Inspection => Inspection,
        ProviderInstructionKind.Audit => Audit,
        ProviderInstructionKind.AuditReport => AuditReport,
        ProviderInstructionKind.Triage => Triage,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), "The instruction kind is invalid.")
    };

    /// <summary>
    /// The Case type this kind allocates, or null for
    /// <see cref="ProviderInstructionKind.Triage"/>, which allocates no Case/PO
    /// at all and opens a Triage record instead (FRD-03).
    /// </summary>
    public static CaseType? ToCaseType(ProviderInstructionKind kind) => kind switch
    {
        ProviderInstructionKind.Inspection => CaseType.Inspection,
        ProviderInstructionKind.Audit => CaseType.Audit,
        ProviderInstructionKind.AuditReport => CaseType.InspectionAndAudit,
        ProviderInstructionKind.Triage => null,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), "The instruction kind is invalid.")
    };

    /// <summary>
    /// Whether the kind carries an incoming original report, and therefore a
    /// verdict on it.
    ///
    /// Only a standalone Audit does. Inspection + Audit is Collision Engineers
    /// inspecting and then auditing its <em>own</em> report (FRD-01 § Case
    /// types): there is no other firm's report to attach, and its reference is
    /// the ordinary Inspection Case/PO with no a./ap. prefix.
    /// </summary>
    public static bool RequiresOriginalReport(ProviderInstructionKind kind) =>
        kind is ProviderInstructionKind.Audit;

    private static bool Matches(string value, string expected) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The wire vocabulary for the original report's outcome. The operator ruled on
/// 2026-08-28 that a declared verdict decides the reference prefix, so this is
/// the value <see cref="AuditIdentity.Create"/> is given.
/// </summary>
public static class ProviderReportVerdicts
{
    public const string Repairable = "repairable";
    public const string TotalLoss = "total-loss";

    public static readonly string[] All = [Repairable, TotalLoss];

    public static AuditAssessment? Parse(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        return normalized switch
        {
            not null when Matches(normalized, Repairable) => AuditAssessment.Repairable,
            not null when Matches(normalized, TotalLoss) => AuditAssessment.TotalLoss,
            _ => throw new ArgumentException(
                $"The original report verdict must be one of: {string.Join(", ", All)}.",
                nameof(value))
        };
    }

    private static bool Matches(string value, string expected) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The role a provider gives one submitted file. Optional on the wire: absent,
/// the file is retained as an ordinary attachment and nothing is inferred about
/// what it is (operator decision, 2026-08-28).
/// </summary>
public static class ProviderFileRoles
{
    public const string Instruction = "instruction";
    public const string OriginalReport = "originalreport";
    public const string Image = "image";
    public const string Correspondence = "correspondence";
    public const string Other = "other";

    public static readonly string[] All =
        [Instruction, OriginalReport, Image, Correspondence, Other];

    public static DocumentSemanticRole? Parse(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        return normalized switch
        {
            not null when Matches(normalized, Instruction) => DocumentSemanticRole.Instruction,
            not null when Matches(normalized, OriginalReport) => DocumentSemanticRole.AuditReport,
            not null when Matches(normalized, Image) => DocumentSemanticRole.Image,
            not null when Matches(normalized, Correspondence) => DocumentSemanticRole.Correspondence,
            not null when Matches(normalized, Other) => DocumentSemanticRole.Other,
            _ => throw new ArgumentException(
                $"A file role must be one of: {string.Join(", ", All)}.",
                nameof(value))
        };
    }

    private static bool Matches(string value, string expected) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
}
