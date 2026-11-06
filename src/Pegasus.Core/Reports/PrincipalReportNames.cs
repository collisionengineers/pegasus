using System.Text.RegularExpressions;
using Pegasus.Core.Cases;

namespace Pegasus.Core.Reports;

/// <summary>
/// What a Principal's reports are called, filed and sent (operator,
/// 6 November 2026; R740): one name for an Inspection report and one for an
/// Audit report, each for a first send and for a re-send. A name is text with
/// the tokens <c>{REG}</c>, <c>{CASE REF}</c> and <c>{OUTCOME}</c>, in any
/// letter case. An empty name is the standard name
/// (<see cref="CaseReportDeliveryNaming"/>).
/// </summary>
/// <remarks>
/// The Inspection names apply to an Inspection Case's report and to the
/// Inspection report of an Inspection + Audit Case; the Audit names to a
/// standalone Audit's report and to the Audit report of an Inspection + Audit
/// Case (<see cref="CaseWorkPolicy.IsAuditReport"/>). A report generated after
/// its work was already sent is filed under its Re-send name; an attachment
/// takes its Re-send name whenever its work was sent before.
/// </remarks>
public sealed record PrincipalReportNames(
    string? InspectionFirst,
    string? InspectionResend,
    string? AuditFirst,
    string? AuditResend)
{
    public static PrincipalReportNames None { get; } = new(null, null, null, null);

    /// <summary>The tokens a name may use, as the Report generation tab offers them.</summary>
    public static IReadOnlyList<string> Tokens { get; } = [Registration, CaseReference, Outcome];

    public const string Registration = "REG";
    public const string CaseReference = "CASE REF";
    public const string Outcome = "OUTCOME";

    /// <summary>
    /// Custody's longest file name, the extension included
    /// (<c>CustodyNames.SafeName</c>): a name that could make a longer one
    /// cannot be filed, so it is refused when it is saved.
    /// </summary>
    public const int MaximumFileNameLength = 120;

    /// <summary>The longest value each token can print.</summary>
    private static readonly IReadOnlyDictionary<string, int> LongestValue = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [Registration] = CaseDataLimits.VehicleRegistration,
        // An Audit report's reference: a. + the Case/PO and its -p suffix.
        [CaseReference] = 43,
        [Outcome] = Enum.GetValues<AssessmentReportOutcome>()
            .Max(outcome => CaseReportDeliveryNaming.OutcomeWords(outcome).Length),
    };

    private static readonly Regex TokenPattern = new(@"\{([^{}]*)\}", RegexOptions.CultureInvariant);

    /// <summary>The name this report takes, or null for the standard name.</summary>
    public string? For(bool auditReport, bool resend) => (auditReport, resend) switch
    {
        (false, false) => InspectionFirst,
        (false, true) => InspectionResend,
        (true, false) => AuditFirst,
        (true, true) => AuditResend,
    };

    /// <summary>The name this work's report takes, or null for the standard name.</summary>
    public string? For(CaseType? caseType, CaseWorkKind workKind, int sentCount) =>
        For(CaseWorkPolicy.IsAuditReport(caseType ?? CaseType.Inspection, workKind), sentCount > 0);

    /// <summary>
    /// The one shape stored names take: trimmed, empty as none, every token
    /// known and every result short enough to file.
    /// </summary>
    public static PrincipalReportNames Normalize(PrincipalReportNames? names)
    {
        if (names is null)
        {
            return None;
        }
        return new(
            Name(names.InspectionFirst, auditReport: false),
            Name(names.InspectionResend, auditReport: false),
            Name(names.AuditFirst, auditReport: true),
            Name(names.AuditResend, auditReport: true));
    }

    /// <summary>
    /// <paramref name="name"/> with each token replaced by its value; a token
    /// with no value leaves nothing. A token is matched in any letter case.
    /// </summary>
    public static string Fill(string name, string caseReference, string? registration, string? outcome)
    {
        ArgumentNullException.ThrowIfNull(name);
        return TokenPattern.Replace(name, match => Canonical(match.Groups[1].Value) switch
        {
            Registration => registration?.Trim() ?? string.Empty,
            CaseReference => caseReference.Trim(),
            Outcome => outcome?.Trim() ?? string.Empty,
            _ => match.Value,
        });
    }

    private static string? Name(string? value, bool auditReport)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var trimmed = value.Trim();
        var longest = trimmed.Length + CaseReportDeliveryNaming.Extension.Length;
        foreach (Match match in TokenPattern.Matches(trimmed))
        {
            var token = Canonical(match.Groups[1].Value);
            if (!LongestValue.TryGetValue(token, out var length))
            {
                throw new ReportNamesException(ReportNamesRule.UnknownToken, auditReport);
            }
            longest += length - match.Length;
        }
        if (longest > MaximumFileNameLength)
        {
            throw new ReportNamesException(ReportNamesRule.TooLong, auditReport);
        }
        return trimmed;
    }

    /// <summary>A token as written, in upper case with its spaces closed to one.</summary>
    private static string Canonical(string token) =>
        string.Join(' ', token.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
}

public enum ReportNamesRule
{
    UnknownToken,
    TooLong
}

/// <summary>A report name that cannot be saved, and whether it is an Audit report's.</summary>
public sealed class ReportNamesException(ReportNamesRule rule, bool auditReport)
    : ArgumentException("The report names are not valid.")
{
    public ReportNamesRule Rule { get; } = rule;

    public bool AuditReport { get; } = auditReport;
}
