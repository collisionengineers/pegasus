using System.Globalization;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Settlement and Report sections' read helpers (v26 § Settlement,
/// § Report): how an assessment value prints, which decision rows carry an
/// AI proposal, the figures strip, and the report's title and content line.
/// Values only — every rule they read belongs to the vocabulary or the
/// report projection.
/// </summary>
public sealed partial class DetailsModel
{
    private static readonly CultureInfo Pounds = CultureInfo.GetCultureInfo("en-GB");

    /// <summary>The five decision fields the Decisions strip carries, in order.</summary>
    public static readonly IReadOnlyList<string> DecisionPaths =
    [
        AssessmentVocabulary.Outcome,
        AssessmentVocabulary.ValueEngineer,
        AssessmentVocabulary.SalvageCategory,
        AssessmentVocabulary.SalvageValue,
        AssessmentVocabulary.LegalStatus
    ];

    /// <summary>Whether the recorded value at <paramref name="path"/> is absent.</summary>
    public bool AssessmentIsAbsent(string path) =>
        string.IsNullOrWhiteSpace(Assessment?.Field(path)?.Value);

    /// <summary>
    /// The recorded value as an operator reads it: money as pounds, a flag as
    /// Yes/No, a date as "14 Jul 2026", a code as words, else the text; the
    /// record's absent word when nothing is held.
    /// </summary>
    public string AssessmentDisplay(string path) =>
        DisplayAssessmentValue(path, Assessment?.Field(path)?.Value);

    /// <summary>The same reading over any raw value the vocabulary defines at <paramref name="path"/>.</summary>
    public static string DisplayAssessmentValue(string path, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return OperatorLabels.CaseWorkspace.AbsentValue;
        }

        var definition = AssessmentVocabulary.Definitions.GetValueOrDefault(path);
        return definition?.Type switch
        {
            AssessmentFieldType.Money => decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var money)
                ? FormatPounds(money)
                : raw,
            AssessmentFieldType.Flag => string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase) ? "Yes" : "No",
            AssessmentFieldType.Date => DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date.ToString("d MMM yyyy", CultureInfo.InvariantCulture)
                : raw,
            AssessmentFieldType.Enumerated => CodeWords(raw),
            _ => raw
        };
    }

    /// <summary>A vocabulary code as words: "total_loss" reads "Total loss"; "N/A" stays.</summary>
    public static string CodeWords(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || !code.Contains('_') && code.Length <= 3)
        {
            return code;
        }
        var spaced = code.Replace('_', ' ');
        return char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }

    public static string FormatPounds(decimal value) => "£" + value.ToString("N2", Pounds);

    /// <summary>
    /// The AI's proposal for a decision field with its derived status: the
    /// recorded proposal row when there is one; otherwise, for a value the
    /// Automation actor wrote before proposals were recorded and no member of
    /// staff has confirmed, that value as Awaiting. Null when nothing was
    /// proposed.
    /// </summary>
    public CaseFieldProposal? ProposalFor(string path)
    {
        if (Proposals.FirstOrDefault(proposal => string.Equals(proposal.FieldPath, path, StringComparison.Ordinal)) is { } recorded)
        {
            return recorded;
        }

        return Assessment?.Field(path) is { RecordedByKind: ActorKind.Automation, IsConfirmed: false } field
            ? new CaseFieldProposal(path, field.Value, field.RecordedBy, field.RecordedAtUtc, CaseFieldProposalStatus.Awaiting, null, null)
            : null;
    }

    /// <summary>The proposed value while it still awaits a person's decision, else null.</summary>
    public string? ProposedDecision(string path) =>
        ProposalFor(path) is { Status: CaseFieldProposalStatus.Awaiting } proposal ? proposal.ProposedValue : null;

    /// <summary>Whether any decision row carries a proposal in any status, which is what draws the Proposed column.</summary>
    public bool HasProposedDecisions => DecisionPaths.Append(AssessmentVocabulary.UnroadworthyReason)
        .Any(path => ProposalFor(path) is not null);

    /// <summary>The rows still awaiting a person's decision.</summary>
    public int AwaitingDecisionCount => DecisionPaths.Append(AssessmentVocabulary.UnroadworthyReason)
        .Count(path => ProposedDecision(path) is not null);

    /// <summary>The status word a proposal reads with.</summary>
    public static string ProposalStatusWord(CaseFieldProposalStatus status) => status switch
    {
        CaseFieldProposalStatus.Accepted => CaseWorkspaceLabels.Settlement.Accepted,
        CaseFieldProposalStatus.Corrected => CaseWorkspaceLabels.Settlement.Corrected,
        _ => CaseWorkspaceLabels.Settlement.Awaiting
    };

    /// <summary>The status chip's colour: amber waits, green accepted, blue corrected.</summary>
    public static string ProposalStatusClass(CaseFieldProposalStatus status) => status switch
    {
        CaseFieldProposalStatus.Accepted => "status--green",
        CaseFieldProposalStatus.Corrected => "status--blue",
        _ => "status--amber"
    };

    /// <summary>The recorded outcome code, confirmed or proposed, else null.</summary>
    public string? RecordedOutcome => Assessment?.Field(AssessmentVocabulary.Outcome)?.Value;

    public bool IsTotalLoss => string.Equals(RecordedOutcome, "total_loss", StringComparison.Ordinal);

    public bool IsUnroadworthy => string.Equals(
        Assessment?.Field(AssessmentVocabulary.LegalStatus)?.Value, "unroadworthy", StringComparison.Ordinal);

    /// <summary>The recorded salvage value as a figure, if one is held.</summary>
    public decimal? SalvageValueFigure =>
        decimal.TryParse(Assessment?.Field(AssessmentVocabulary.SalvageValue)?.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    /// <summary>The Current estimate's labour hours (panel and paint), for the figures strip.</summary>
    public decimal? LabourHours => AcceptedSpecification is { } estimate
        ? ReportRepairCosts.For(estimate).LabourHours + ReportRepairCosts.For(estimate).PaintHours
        : null;

    /// <summary>The report's title as the preview card and the draft file name it: "Total Loss Report — MA59BDY".</summary>
    public string ReportTitle
    {
        get
        {
            var outcome = RecordedOutcome;
            var kind = outcome is null
                ? "Assessment"
                : string.Equals(outcome, "total_loss", StringComparison.Ordinal)
                    ? "Total Loss"
                    : CodeWords(outcome);
            var registration = Case?.Summary.Registration;
            return string.IsNullOrWhiteSpace(registration) ? $"{kind} Report" : $"{kind} Report — {registration}";
        }
    }

    /// <summary>The Report content line the three switches read as: "Guide source disclosed · valuation commentary · unrelated damage".</summary>
    public string ReportContentSummary
    {
        get
        {
            bool On(string path) => string.Equals(Assessment?.Field(path)?.Value, "true", StringComparison.OrdinalIgnoreCase);
            var parts = new List<string>
            {
                On(AssessmentVocabulary.ReportDiscloseGuideSource) ? "Guide source disclosed" : "Guide source not disclosed"
            };
            if (On(AssessmentVocabulary.ReportValuationCommentary))
            {
                parts.Add("valuation commentary");
            }
            if (On(AssessmentVocabulary.ReportIncludeUnrelatedDamage))
            {
                parts.Add("unrelated damage");
            }
            return string.Join(" · ", parts);
        }
    }

    /// <summary>Every Case image with its preparation, for the "Images in report" strip: readable files only.</summary>
    public IReadOnlyList<(CaseFile File, CaseAssetPreparation Preparation)> ReportImageTiles
    {
        get
        {
            if (Case is not { } details)
            {
                return [];
            }
            var preparations = AssetPreparations.ToDictionary(item => item.OccurrenceId);
            return
            [
                .. CaseFiles.Current(details.Documents)
                    .Where(file => file.Occurrence.SemanticRole == DocumentSemanticRole.Image
                        && file.Version.CustodyStatus == DocumentCustodyStatus.Confirmed
                        && preparations.ContainsKey(file.Occurrence.Id))
                    .Select(file => (file, preparations[file.Occurrence.Id]))
            ];
        }
    }
}
