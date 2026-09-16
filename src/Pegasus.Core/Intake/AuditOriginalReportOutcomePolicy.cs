using System.Text.RegularExpressions;
using Pegasus.Core.Cases;

namespace Pegasus.Core.Intake;

/// <summary>Reads the single fact an original Audit report may contribute to intake: its outcome.</summary>
public static partial class AuditOriginalReportOutcomePolicy
{
    public static StandaloneAuditReportEvaluation? Evaluate(
        IntakeSourceReadResult read,
        string assetSourceLabel)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetSourceLabel);
        if (read.Status != IntakeSourceReadStatus.Readable || read.IsIncomplete || read.RequiresOcr)
        {
            return null;
        }

        var texts = read.Content
            .Where(fragment => fragment.Source is IntakeEvidenceSource.DocumentContent or IntakeEvidenceSource.PdfContent)
            .Select(fragment => fragment.Text)
            .ToArray();
        var repairable = texts.Any(HasRepairable);
        var totalLoss = texts.Any(HasTotalLoss);
        return repairable == totalLoss
            ? null
            : new(assetSourceLabel, repairable ? AuditAssessment.Repairable : AuditAssessment.TotalLoss);
    }

    public static bool HasRepairable(string text) =>
        RepairableLiteralRegex().IsMatch(text) && !NegatedRepairableLiteralRegex().IsMatch(text);

    public static bool HasTotalLoss(string text) =>
        TotalLossLiteralRegex().IsMatch(text) && !NegatedTotalLossLiteralRegex().IsMatch(text);

    [GeneratedRegex(@"\brepairable\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RepairableLiteralRegex();

    [GeneratedRegex(@"\b(?:not|no)\b(?:\s+(?:a|the))?[\s-]+repairable\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NegatedRepairableLiteralRegex();

    [GeneratedRegex(@"\btotal[\s-]+loss\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TotalLossLiteralRegex();

    [GeneratedRegex(@"\b(?:not|no)\b(?:\s+(?:a|the))?[\s-]+total[\s-]+loss\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NegatedTotalLossLiteralRegex();
}
