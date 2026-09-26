using System.Text.RegularExpressions;

namespace Pegasus.Core.Triage;

/// <summary>
/// Reply with outcome: the built-in body a Completed Triage's reply opens
/// with, rendered from the registration and the current finding. Staff edit
/// the text before Send; the subject stays the origin's with one "Re:".
/// </summary>
/// <remarks>
/// A placeholder with no value renders nothing, and a line whose placeholders
/// are all empty is left out, so a dimension that was not recorded does not
/// appear. <c>{reason}</c> is available but not in the default: the finding
/// reason is internal wording.
/// </remarks>
public static partial class TriageOutcomeReply
{
    public const string Registration = "registration";
    public const string Roadworthiness = "roadworthiness";
    public const string RepairOutcome = "repair outcome";
    public const string Reason = "reason";

    public static readonly IReadOnlyList<string> Placeholders =
        [Registration, Roadworthiness, RepairOutcome, Reason];

    public const string DefaultBody =
        "Thank you for your triage request for {registration}.\n"
        + "\n"
        + "Roadworthiness: {roadworthiness}\n"
        + "Repair outcome: {repair outcome}\n"
        + "\n"
        + "Kind regards\n"
        + "Collision Engineers";

    /// <summary>The default body rendered for one Triage.</summary>
    public static string Render(string registration, TriageFinding? finding) =>
        Render(DefaultBody, Values(registration, finding));

    /// <summary>The placeholder values one Triage supplies, by placeholder name.</summary>
    public static IReadOnlyDictionary<string, string?> Values(string registration, TriageFinding? finding) =>
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [Registration] = registration,
            [Roadworthiness] = finding?.Roadworthiness is { } roadworthiness ? Label(roadworthiness) : null,
            [RepairOutcome] = finding?.Assessment is { } assessment ? Label(assessment) : null,
            [Reason] = finding?.Reason
        };

    /// <summary>
    /// Renders <paramref name="body"/> line by line. A known placeholder is
    /// replaced by its value, or by nothing when the value is empty; a line
    /// whose known placeholders are all empty is left out. A placeholder the
    /// values do not name is left as written.
    /// </summary>
    public static string Render(string body, IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(values);
        var lines = body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var kept = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            var known = 0;
            var filled = 0;
            var rendered = PlaceholderPattern().Replace(line, match =>
            {
                if (!values.TryGetValue(match.Groups["name"].Value, out var value))
                {
                    return match.Value;
                }

                known++;
                if (string.IsNullOrWhiteSpace(value))
                {
                    return string.Empty;
                }

                filled++;
                return value;
            });
            if (known > 0 && filled == 0)
            {
                continue;
            }

            kept.Add(rendered);
        }

        return string.Join("\n", kept);
    }

    public static string Label(RoadworthinessFinding finding) => finding switch
    {
        RoadworthinessFinding.Roadworthy => "Roadworthy",
        RoadworthinessFinding.Unroadworthy => "Unroadworthy",
        _ => throw new InvalidOperationException(
            $"Unknown roadworthiness finding value '{(int)finding}'.")
    };

    public static string Label(AssessmentFinding finding) => finding switch
    {
        AssessmentFinding.Repairable => "Repairable",
        AssessmentFinding.TotalLoss => "Total loss",
        _ => throw new InvalidOperationException(
            $"Unknown assessment finding value '{(int)finding}'.")
    };

    [GeneratedRegex(@"\{(?<name>[^{}]+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderPattern();
}
