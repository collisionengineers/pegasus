using System.Globalization;

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
/// The composed words are the accepted report wording, so a Case nobody has
/// edited prints the sentences the report has always printed.
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
    public const string ManualTitle = "Report paragraph";

    /// <summary>The blocks in the order the report prints them unless the Engineer moves one.</summary>
    public static IReadOnlyList<(string Key, string Title)> Standard { get; } =
    [
        (NatureOfIncident, "Nature of Incident"),
        (EngineersComments, "Engineer's Comments"),
        (SupplementaryDamage, "Supplementary Damage"),
        (ValuationCommentary, "Valuation Commentary"),
        (UnrelatedDamage, "Unrelated Damage"),
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
    /// change held is not offered.
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
            var text = string.IsNullOrWhiteSpace(change?.Text) ? composed : change!.Text!.Trim();
            var hasChange = change is not null
                && (change.Title is not null || change.Text is not null
                    || change.Order is not null || !change.Included);
            if (string.IsNullOrWhiteSpace(text) && !hasChange)
            {
                continue;
            }
            var standardTitle = key == Settlement ? presentation.SettlementHeading : title;
            blocks.Add(new(
                key,
                string.IsNullOrWhiteSpace(change?.Title) ? standardTitle : change!.Title!.Trim(),
                text,
                change?.Order ?? index,
                HandEdited: !string.IsNullOrWhiteSpace(change?.Text)
                    && !string.Equals(change!.Text!.Trim(), composed, StringComparison.Ordinal),
                Manual: false,
                Included: change?.Included ?? true));
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
            NatureOfIncident =>
                $"The vehicle has suffered {Display(snapshot.ImpactSeverity)} collision/impact damage to the {Display(snapshot.ImpactLocation)}.",
            EngineersComments => Comments(snapshot),
            SupplementaryDamage => snapshot.SupplementaryStatement ?? string.Empty,
            ValuationCommentary => snapshot.Content.IncludeValuationCommentary
                ? snapshot.ValuationCommentary ?? string.Empty
                : string.Empty,
            UnrelatedDamage => Unrelated(snapshot),
            VehicleHistoryCheck => snapshot.HistoryCheck,
            PreIncidentCondition =>
                $"The vehicle is considered to be in {Display(snapshot.Vehicle.Condition)} condition for its age and type.",
            Settlement => presentation.SettlementText,
            Salvage => SalvageText(snapshot),
            _ => string.Empty,
        };
    }

    /// <summary>
    /// The one shape a stored change takes, so the record never holds a title
    /// or text the report would refuse to print.
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
        var text = string.IsNullOrWhiteSpace(block.Text) ? null : block.Text.Trim();
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
        return block with { Title = title, Text = text };
    }

    /// <summary>
    /// The mileage sentence the report prints, by its recorded source. An
    /// unrecognised source is refused rather than printed around.
    /// </summary>
    public static string MileageSentence(string source) => source switch
    {
        "online_data" => "The mileage has been calculated from online data.",
        "owner" => "The mileage has been provided by the owner.",
        "repairer" => "The mileage has been provided by the repairer.",
        "principal" => "The mileage has been provided by the instructing principal.",
        "average" => "The mileage has been calculated from average mileage data.",
        "tbc" => "The mileage is to be confirmed.",
        _ => throw new ReportRenderRejectedException("Unsupported mileage source."),
    };

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

    private static string Comments(AssessmentReportSnapshot snapshot)
    {
        var parts = new List<string> { MileageSentence(snapshot.Vehicle.MileageSource) };
        if (snapshot.LegalStatus.Equals("unroadworthy", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add($"Please note the vehicle is unroadworthy due to {snapshot.UnroadworthyReason}.");
        }
        if (!string.IsNullOrWhiteSpace(snapshot.EngineerComments))
        {
            parts.Add(snapshot.EngineerComments);
        }
        return string.Join("\n\n", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string Unrelated(AssessmentReportSnapshot snapshot)
    {
        if (!snapshot.Content.IncludeUnrelatedDamage
            || string.IsNullOrWhiteSpace(snapshot.Damage.Unrelated))
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
            "A" => "this is Category A (scrap only: the vehicle must be crushed in its entirety with no parts recovery).",
            "B" => "this is Category B (break for spare parts: the body shell must be crushed).",
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

    private static string Display(string value) =>
        CultureInfo.GetCultureInfo("en-GB").TextInfo.ToTitleCase(value.Replace('_', ' ').ToLowerInvariant());

    private static string Money(decimal value) =>
        value.ToString("£#,##0.00", CultureInfo.GetCultureInfo("en-GB"));
}
