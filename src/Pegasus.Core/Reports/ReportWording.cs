using System.Globalization;
using Pegasus.Core.Assessment;

namespace Pegasus.Core.Reports;

/// <summary>
/// One narrative block of the assessment report (v28 P30, ruled 20 September
/// 2026: the report is composed from the Engineer's wording blocks in their
/// order, replacing FRD-11's fixed template). A block's text is composed from
/// the accepted Case facts unless the Engineer has written their own, which
/// <see cref="HandEdited"/> records.
/// </summary>
public sealed record ReportWordingBlock(
    string Key,
    string Title,
    string Text,
    int Order,
    bool HandEdited = false,
    bool Manual = false,
    bool Included = true);

/// <summary>
/// What an Engineer changed about one block, held per Case and written by the
/// one Case save. A null <see cref="Title"/> or <see cref="Text"/> means the
/// block still tracks its fields.
/// </summary>
public sealed record CaseReportWording(
    string Key,
    string? Title,
    string? Text,
    int? Order,
    bool Included = true,
    bool Manual = false);

/// <summary>
/// The report's narrative blocks: which there are, what each says when nobody
/// has written their own, and how an Engineer's changes are laid over them.
/// Every heading and composed sentence here is copied word for word from one
/// of two sources (operator, 27 September 2026): the template
/// (<c>reference/rendererref1</c>) or the manager's case page
/// (<c>v27_planning/pegasus_case_dashboard_2026-09-15.html</c> in pegasus-design-archive).
/// Nothing is worded here that neither source words.
/// </summary>
public static class ReportWordingComposition
{
    public const string NatureOfIncident = "nature";
    public const string EngineersComments = "comments";
    public const string SupplementaryDamage = "supplementary";
    public const string ValuationCommentary = "commentary";
    public const string UnrelatedDamage = "unrelated";
    public const string VehicleHistoryCheck = "history";
    public const string PreIncidentCondition = "condition";
    public const string Settlement = "settlement";
    public const string Salvage = "salvage";

    public const int MaximumTitleLength = 80;
    public const int MaximumTextLength = 4000;
    public const string ManualKeyPrefix = "manual:";

    /// <summary>The heading of a paragraph the Engineer adds, as the manager's case page names it.</summary>
    public const string ManualTitle = "New paragraph";

    /// <summary>
    /// The blocks in the order the report prints them unless the Engineer
    /// moves one. The template prints six of the headings; Supplementary
    /// damage, PAV commentary and Unrelated damage are the manager's case
    /// page's, as it writes them.
    /// </summary>
    public static IReadOnlyList<(string Key, string Title)> Standard { get; } =
    [
        (NatureOfIncident, "Nature of Incident"),
        (EngineersComments, "Engineer's Comments"),
        (SupplementaryDamage, "Supplementary damage"),
        (ValuationCommentary, "PAV commentary"),
        (UnrelatedDamage, "Unrelated damage"),
        (VehicleHistoryCheck, "Vehicle History Check"),
        (PreIncidentCondition, "Pre-Incident Condition"),
        (Settlement, "Settlement"),
        (Salvage, "Salvage"),
    ];

    /// <summary>
    /// The blocks as they print: the composed text of each, with an Engineer's
    /// own wording, title and order laid over it. A block whose text composes
    /// to nothing, or which the Engineer took off the report, is absent.
    /// </summary>
    public static IReadOnlyList<ReportWordingBlock> Compose(
        AssessmentReportSnapshot snapshot,
        IReadOnlyList<CaseReportWording> saved) =>
        [.. Offered(snapshot, saved)
            .Where(block => block.Included && !string.IsNullOrWhiteSpace(block.Text))];

    /// <summary>
    /// Every block the Report section offers, in the Engineer's order and
    /// including the ones taken off the report, so putting one back is the
    /// same control that took it off. A block with nothing to say and no
    /// change held is not offered, except the two a report switch owns,
    /// which are always offered and on the report while their switch is
    /// (v45 A); the commentary's words are always the Valuation commentary
    /// field's (v45 E).
    /// </summary>
    public static IReadOnlyList<ReportWordingBlock> Offered(
        AssessmentReportSnapshot snapshot,
        IReadOnlyList<CaseReportWording> saved)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(saved);
        var byKey = saved
            .Where(item => !item.Manual)
            .ToDictionary(item => item.Key, StringComparer.Ordinal);
        var presentation = snapshot.Presentation();
        var blocks = new List<ReportWordingBlock>();

        for (var index = 0; index < Standard.Count; index++)
        {
            var (key, title) = Standard[index];
            byKey.TryGetValue(key, out var change);
            var composed = ComposedText(key, snapshot, presentation);
            // The commentary's words are the Valuation commentary field (v45 E).
            var text = key == ValuationCommentary ? composed : Worded(change, composed);
            var switchedOn = SwitchedOn(key, snapshot.Content);
            var hasChange = change is not null
                && (change.Title is not null || change.Text is not null
                    || change.Order is not null || !change.Included);
            if (switchedOn is null && string.IsNullOrWhiteSpace(text) && !hasChange)
            {
                continue;
            }
            var standardTitle = key == Settlement ? presentation.SettlementHeading : title;
            blocks.Add(new(
                key,
                string.IsNullOrWhiteSpace(change?.Title) ? standardTitle : change!.Title!.Trim(),
                text,
                change?.Order ?? index,
                HandEdited: key != ValuationCommentary
                    && !string.IsNullOrWhiteSpace(change?.Text)
                    && !string.Equals(change!.Text!.Trim(), composed, StringComparison.Ordinal),
                Manual: false,
                Included: switchedOn ?? change?.Included ?? true));
        }

        // A paragraph the Engineer wrote themselves has no composed source.
        foreach (var manual in saved.Where(item => item.Manual))
        {
            if (string.IsNullOrWhiteSpace(manual.Text))
            {
                continue;
            }
            blocks.Add(new(
                manual.Key,
                string.IsNullOrWhiteSpace(manual.Title) ? ManualTitle : manual.Title!.Trim(),
                manual.Text!.Trim(),
                manual.Order ?? Standard.Count,
                HandEdited: true,
                Manual: true,
                Included: manual.Included));
        }

        return [.. blocks
            .OrderBy(block => block.Order)
            .ThenBy(block => StandardIndex(block.Key))];
    }

    /// <summary>The heading a block carries when the Engineer has not renamed it.</summary>
    public static string StandardTitle(string key, AssessmentReportPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        if (key == Settlement)
        {
            return presentation.SettlementHeading;
        }
        var index = StandardIndex(key);
        return index < 0 ? ManualTitle : Standard[index].Title;
    }

    /// <summary>What one block says when nobody has written their own.</summary>
    public static string ComposedText(
        string key,
        AssessmentReportSnapshot snapshot,
        AssessmentReportPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(presentation);
        return key switch
        {
            NatureOfIncident => NatureOfIncidentText(snapshot.Damage.Impacts),
            EngineersComments => Comments(snapshot),
            SupplementaryDamage => snapshot.SupplementaryStatement ?? string.Empty,
            ValuationCommentary => snapshot.ValuationCommentary ?? string.Empty,
            UnrelatedDamage => Unrelated(snapshot),
            VehicleHistoryCheck => snapshot.HistoryCheck,
            PreIncidentCondition =>
                $"The vehicle is considered to be in {Words(snapshot.Vehicle.Condition)} condition for its age and type.",
            Settlement => presentation.SettlementText,
            Salvage => SalvageText(snapshot),
            _ => string.Empty,
        };
    }

    /// <summary>
    /// The template's Nature of Incident sentence, with the
    /// <c>{severity}</c> and <c>{location}</c> an impact fills in. The Case
    /// page's Damage section fills the same pattern while it edits.
    /// </summary>
    public const string NatureOfIncidentPattern =
        "The vehicle has suffered {severity} collision/impact damage to the {location}.";

    /// <summary>
    /// The template's Nature of Incident sentence for one impact, its severity
    /// and location in small letters.
    /// </summary>
    public static string NatureOfIncidentSentence(string impactSeverity, string impactLocation)
    {
        ArgumentNullException.ThrowIfNull(impactSeverity);
        ArgumentNullException.ThrowIfNull(impactLocation);
        return NatureOfIncidentPattern
            .Replace("{severity}", Words(impactSeverity), StringComparison.Ordinal)
            .Replace("{location}", Words(impactLocation), StringComparison.Ordinal);
    }

    /// <summary>
    /// The Nature of Incident block's composed text: a separate sentence for
    /// each impact, one to a line, in the order staff entered them (v46 A and
    /// A2, operator, 3 November 2026). No impact composes nothing, so the
    /// block is not offered (v46 G).
    /// </summary>
    public static string NatureOfIncidentText(IReadOnlyList<ReportImpact> impacts)
    {
        ArgumentNullException.ThrowIfNull(impacts);
        return string.Join("\n", impacts.Select(impact => NatureOfIncidentSentence(impact.Severity, impact.Location)));
    }

    /// <summary>
    /// The contract repair sentence, as the template prints it. The Repair
    /// Spec section shows the same sentence once a sum is agreed (v28 P35), so
    /// the Case page and the report cannot disagree.
    /// </summary>
    public static string ContractRepairSentence(decimal agreedSum) =>
        $"A contract repair has been agreed for the sum of {Money(agreedSum)} including VAT. Costs cannot increase above this figure.";

    /// <summary>
    /// The Nature of Incident block's text whether or not a report can yet be
    /// projected: the Engineer's own wording when written, else the sentences
    /// composed from the impacts staff entered; null when there is neither.
    /// The Damage section's Incident narrative reads it (operator, 24 September
    /// 2026); whether the report carries the block is Report wording's choice and
    /// does not change it.
    /// </summary>
    public static string? NatureOfIncidentOf(AssessmentReportProjectionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var composed = NatureOfIncidentText(AssessmentReportProjection.ReportImpacts(
            AssessmentPolicy.ParseImpacts(input.Assessment.Field(AssessmentVocabulary.Impacts)?.Value)));
        var change = (input.Wording ?? []).FirstOrDefault(item =>
            !item.Manual && string.Equals(item.Key, NatureOfIncident, StringComparison.Ordinal));
        var text = Worded(change, composed);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    /// <summary>
    /// Whether the Engineer has written their own Nature of Incident wording,
    /// which the Damage section's Incident narrative then shows in place of
    /// the sentences the impacts compose.
    /// </summary>
    public static bool NatureOfIncidentIsWorded(AssessmentReportProjectionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return (input.Wording ?? []).Any(item =>
            !item.Manual
            && string.Equals(item.Key, NatureOfIncident, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(item.Text));
    }

    /// <summary>
    /// The one shape a stored change takes, so the record never holds a title
    /// or text the report would refuse to print. A browser posts a line break
    /// as two characters and a composed sentence holds one, so line breaks are
    /// stored as the composed sentences write them.
    /// </summary>
    public static CaseReportWording Validate(CaseReportWording block)
    {
        ArgumentNullException.ThrowIfNull(block);
        var manualKey = block.Key.StartsWith(ManualKeyPrefix, StringComparison.Ordinal);
        if (!manualKey && StandardIndex(block.Key) < 0)
        {
            throw new ArgumentException(
                $"'{block.Key}' is not a report wording block.", nameof(block));
        }
        if (block.Manual != manualKey)
        {
            throw new ArgumentException(
                "A paragraph the Engineer wrote is the one with a manual key.", nameof(block));
        }
        if (manualKey && block.Key.Length <= ManualKeyPrefix.Length)
        {
            throw new ArgumentException(
                "A paragraph the Engineer wrote needs its own key.", nameof(block));
        }
        var title = string.IsNullOrWhiteSpace(block.Title) ? null : block.Title.Trim();
        var text = string.IsNullOrWhiteSpace(block.Text) ? null : LineBreaks(block.Text.Trim());
        if (title is not null && (title.Length > MaximumTitleLength || title.Any(char.IsControl)))
        {
            throw new ArgumentException(
                $"A report wording heading cannot exceed {MaximumTitleLength} characters or contain a line break.",
                nameof(block));
        }
        if (text is { Length: > MaximumTextLength })
        {
            throw new ArgumentException(
                $"Report wording cannot exceed {MaximumTextLength} characters.", nameof(block));
        }
        if (block.Order is < 0)
        {
            throw new ArgumentException(
                "A report wording order cannot be negative.", nameof(block));
        }
        // A switch-owned block holds no on/off of its own, and the commentary's
        // words are the Valuation commentary field's (v45 A, E).
        return block with
        {
            Title = title,
            Text = block.Key == ValuationCommentary ? null : text,
            Included = SwitchOwned(block.Key) || block.Included,
        };
    }

    /// <summary>
    /// The mileage sentence the report prints, by its recorded source. The
    /// template prints the online data sentence; the other five are the
    /// manager's case page's. An unrecognised source is refused rather than
    /// printed around.
    /// </summary>
    public static string MileageSentence(string source) => source switch
    {
        "online_data" => "The mileage has been calculated from online data.",
        "owner" => "The mileage was advised by the owner.",
        "repairer" => "The mileage was advised by the repairer/storage yard.",
        "principal" => "The mileage was advised by our instructing principal.",
        "average" => "The mileage has been based on average mileage for the vehicle's age and type.",
        "tbc" => "The mileage is to be confirmed.",
        _ => throw new ReportRenderRejectedException("Unsupported mileage source."),
    };

    /// <summary>
    /// A recorded code as the words a sentence carries, in small letters as
    /// the template prints them: "right_rear" reads "right rear" and
    /// "light_to_moderate" reads "light to moderate".
    /// </summary>
    public static string Words(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return code.Replace('_', ' ').ToLowerInvariant();
    }

    /// <summary>
    /// The same words where they open a table cell or a listed line: the
    /// first letter is a capital and the rest are small, so "below_average"
    /// reads "Below average" and "left_front" reads "Left front".
    /// </summary>
    public static string LeadingWords(string code)
    {
        var words = Words(code);
        return words.Length == 0 ? words : char.ToUpperInvariant(words[0]) + words[1..];
    }

    /// <summary>A text with every line break written as the composed sentences write one.</summary>
    public static string LineBreaks(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.ReplaceLineEndings("\n");
    }

    public static int StandardIndex(string key)
    {
        for (var index = 0; index < Standard.Count; index++)
        {
            if (string.Equals(Standard[index].Key, key, StringComparison.Ordinal))
            {
                return index;
            }
        }
        return -1;
    }

    /// <summary>
    /// The opening of the unroadworthy sentence, the same words the Case page
    /// shows before the reason box so the Engineer finishes the sentence
    /// (operator, 2 November 2026; R607, R612). It ends at "unroadworthy"
    /// so the Engineer's reason chooses its own joining word (operator,
    /// 6 November 2026; R750).
    /// </summary>
    public const string UnroadworthyOpening = "Please note the vehicle is unroadworthy";

    /// <summary>
    /// The unroadworthy sentence as printed: the opening, the reason, one full
    /// stop. A full stop the Engineer typed at the end is not doubled
    /// (operator, 2 November 2026; R607).
    /// </summary>
    public static string UnroadworthySentence(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        return $"{UnroadworthyOpening} {reason.Trim().TrimEnd('.')}.";
    }

    private static string Comments(AssessmentReportSnapshot snapshot)
    {
        var parts = new List<string> { MileageSentence(snapshot.Vehicle.MileageSource) };
        if (snapshot.IsUnroadworthy)
        {
            parts.Add(UnroadworthySentence(snapshot.UnroadworthyReason ?? string.Empty));
        }
        if (!string.IsNullOrWhiteSpace(snapshot.EngineerComments))
        {
            parts.Add(snapshot.EngineerComments);
        }
        return string.Join("\n\n", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string Unrelated(AssessmentReportSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.Damage.Unrelated))
        {
            return string.Empty;
        }
        return $"Unrelated pre-existing damage was noted: {snapshot.Damage.Unrelated.TrimEnd('.')}. "
            + "This damage is inconsistent with the reported incident and has been disregarded for the purposes of this assessment.";
    }

    private static string SalvageText(AssessmentReportSnapshot snapshot)
    {
        if (snapshot.Outcome != AssessmentReportOutcome.TotalLoss || snapshot.SalvageValue is not { } salvage)
        {
            return string.Empty;
        }
        var category = (snapshot.SalvageCategory ?? string.Empty).Trim().ToUpperInvariant();
        var finding = category switch
        {
            "A" => "this is Category A (scrap only — the vehicle must be crushed in its entirety with no parts recovery).",
            "B" => "this is Category B (break for spare parts — the body shell must be crushed).",
            "S" => "this is Category S (structural damage) and can be sold as repairable salvage. Further information is available at www.abi.org.uk.",
            "N" => "this is Category N (non-structural damage) and can be sold as repairable salvage. Further information is available at www.abi.org.uk.",
            _ => string.Empty,
        };
        return finding.Length == 0
            ? string.Empty
            : "Under the current salvage categorisation matrix, within the scope of our inspection, we consider that "
                + finding
                + $" We suggest that the sale of the salvage will realise in the order of {Money(salvage)}."
                + " We have not taken any action towards removal of the salvage at this time.";
    }

    /// <summary>Whether a report content switch is the block's only on/off (v45 A).</summary>
    public static bool SwitchOwned(string key) => key is ValuationCommentary or UnrelatedDamage;

    /// <summary>
    /// Whether a block the report content switches own is on the report: the
    /// Valuation commentary and Unrelated damage switches are those two
    /// blocks' only on/off (operator, 2 November 2026; v45 A). Null for every
    /// other block, which carries its own.
    /// </summary>
    public static bool? SwitchedOn(string key, CaseReportContentSwitches content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return key switch
        {
            ValuationCommentary => content.IncludeValuationCommentary,
            UnrelatedDamage => content.IncludeUnrelatedDamage,
            _ => null,
        };
    }

    /// <summary>A block's text: the Engineer's own wording when written, else the composed sentence.</summary>
    private static string Worded(CaseReportWording? change, string composed) =>
        string.IsNullOrWhiteSpace(change?.Text) ? composed : change!.Text!.Trim();

    private static string Money(decimal value) =>
        value.ToString("£#,##0.00", CultureInfo.GetCultureInfo("en-GB"));
}
