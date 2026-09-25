using Pegasus.Core.Cases;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The one persisted vocabulary of <see cref="CaseType"/>. Parsing is exact:
/// a code that is not one of these four is corrupt data.
/// </summary>
internal static class CaseTypeCodes
{
    public const string Inspection = "inspection";
    public const string Audit = "audit";
    public const string InspectionAndAudit = "inspection_and_audit";
    public const string Triage = "triage";

    public static string ToCode(CaseType value) => value switch
    {
        CaseType.Inspection => Inspection,
        CaseType.Audit => Audit,
        CaseType.InspectionAndAudit => InspectionAndAudit,
        CaseType.Triage => Triage,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown case type.")
    };

    public static CaseType Parse(string value) => value switch
    {
        Inspection => CaseType.Inspection,
        Audit => CaseType.Audit,
        InspectionAndAudit => CaseType.InspectionAndAudit,
        Triage => CaseType.Triage,
        _ => throw new InvalidDataException($"Unknown persisted case type '{value}'.")
    };
}
