using System.Globalization;

namespace Pegasus.Core.Cases;

/// <summary>
/// The one owner of Case referencing. A Case's Case/PO is its Principal code,
/// two-digit year and annual sequence, prefixed by its type: <c>a.</c> for a
/// standalone Audit, <c>t.</c> for a Triage, nothing for an Inspection or an
/// Inspection + Audit. The Audit report of an Inspection + Audit Case is
/// referenced <c>a.{Case/PO}</c>. A one-off Case for a private customer is
/// the exception: its Case/PO is the vehicle registration, and a later one-off
/// for the same registration is <c>{registration}.2</c>, then <c>.3</c>
/// (operator, 31 October 2026; R574, R575).
/// </summary>
public static class CaseReferenceFormat
{
    public const string AuditPrefix = "a.";
    public const string TriagePrefix = "t.";

    /// <summary>Between a one-off's registration and its repeat number.</summary>
    public const char RepeatSeparator = '.';

    /// <summary>
    /// A one-off Case's Case/PO: the normalised registration alone for the
    /// first one-off of that vehicle, <c>{registration}.{repeat}</c> after.
    /// The registration is already normalised to upper-case letters and
    /// digits by <see cref="CaseDataPolicy.Normalize"/>; anything else is
    /// refused here rather than written as a reference.
    /// </summary>
    public static string Registration(string normalisedRegistration, int repeat)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalisedRegistration);
        ArgumentOutOfRangeException.ThrowIfLessThan(repeat, 1);
        if (normalisedRegistration.Length > CaseDataLimits.VehicleRegistration
            || normalisedRegistration.Any(character => !(char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character))))
        {
            throw new ArgumentException(
                "A one-off Case's registration must be upper-case letters and digits with no separators.",
                nameof(normalisedRegistration));
        }

        return repeat == 1
            ? normalisedRegistration
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{normalisedRegistration}{RepeatSeparator}{repeat}");
    }

    /// <summary>
    /// The repeat number the next one-off of this registration takes, given
    /// every Case reference already held: one more than the highest taken,
    /// where the bare registration counts as 1. The highest is used, not a
    /// count, so a gap can never hand out a reference twice.
    /// </summary>
    public static int NextRegistrationRepeat(
        string normalisedRegistration,
        IEnumerable<string> existingReferences)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalisedRegistration);
        ArgumentNullException.ThrowIfNull(existingReferences);
        var highest = 0;
        var prefix = normalisedRegistration + RepeatSeparator;
        foreach (var reference in existingReferences)
        {
            if (string.Equals(reference, normalisedRegistration, StringComparison.Ordinal))
            {
                highest = Math.Max(highest, 1);
            }
            else if (reference.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(
                    reference.AsSpan(prefix.Length),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var repeat))
            {
                highest = Math.Max(highest, repeat);
            }
        }

        return highest + 1;
    }

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
