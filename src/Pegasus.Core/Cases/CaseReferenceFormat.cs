using System.Globalization;

namespace Pegasus.Core.Cases;

/// <summary>
/// The one owner of Case referencing. A Case's Case/PO is its Principal code,
/// two-digit year and annual sequence, prefixed by its type: <c>a.</c> for a
/// standalone Audit, <c>t.</c> for a Triage, nothing for an Inspection or an
/// Inspection + Audit. The Audit report of an Inspection + Audit Case is
/// referenced <c>a.{Case/PO}</c>.
/// </summary>
public static class CaseReferenceFormat
{
    public const string AuditPrefix = "a.";
    public const string TriagePrefix = "t.";

    public static string Base(string principalCode, int year, int sequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principalCode);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{principalCode}{year % 100:00}{sequence:000}");
    }

    public static string CasePo(CaseType caseType, string baseReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseReference);
        return caseType switch
        {
            CaseType.Inspection or CaseType.InspectionAndAudit => baseReference,
            CaseType.Audit => AuditPrefix + baseReference,
            CaseType.Triage => TriagePrefix + baseReference,
            _ => throw new ArgumentOutOfRangeException(nameof(caseType), caseType, null)
        };
    }

    /// <summary>The Audit report reference of an Inspection + Audit Case.</summary>
    public static string AuditReport(string casePo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(casePo);
        return AuditPrefix + casePo;
    }

    /// <summary>
    /// The reference a report prints as Our Ref, names its file and email
    /// subject with: the Audit work's report is <c>a.{Case/PO}</c>; every
    /// other report is the Case/PO itself (a standalone Audit's already
    /// carries its <c>a.</c>).
    /// </summary>
    public static string ReportReference(CaseIdentity identity, CaseWorkKind workKind)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return workKind == CaseWorkKind.Audit
            ? identity.AuditReference ?? AuditReport(identity.Reference)
            : identity.Reference;
    }
}
