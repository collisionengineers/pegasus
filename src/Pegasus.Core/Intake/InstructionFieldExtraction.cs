using System.Globalization;
using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

/// <summary>
/// Provider-neutral instruction field reading: label-anchored candidate discovery and
/// value parsing shared by provider extraction policies. Which fields a policy expects
/// remains that policy's own definition list.
/// </summary>
internal static partial class InstructionFieldEngine
{
    internal sealed record FieldDefinition(
        string Name,
        string[] Labels,
        bool IsRequired = true);

    internal static (IReadOnlyList<InstructionReviewField> Fields, IReadOnlyList<string> Missing, IReadOnlyList<IntakeEvidence> Evidence)
        ExtractFields(
            IReadOnlyList<IntakeContentFragment> fragments,
            IReadOnlyList<FieldDefinition> definitions,
            DateTimeOffset processedAtUtc)
    {
        var fields = new List<InstructionReviewField>();
        var missing = new List<string>();
        var evidence = new List<IntakeEvidence>();

        foreach (var definition in definitions)
        {
            var candidates = fragments
                .SelectMany(fragment => FindCandidates(fragment, definition, definitions))
                .DistinctBy(candidate => candidate.Value, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (candidates.Length == 0 && definition.Name == "Instruction date")
            {
                var defaultValue = DateOnly.FromDateTime(processedAtUtc.UtcDateTime)
                    .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var defaultCandidate = new InstructionFieldCandidate(
                    defaultValue,
                    IntakeEvidenceSource.SystemDefault,
                    "Receipt date");
                fields.Add(new(definition.Name, defaultValue, [defaultCandidate], true, false));
                evidence.Add(new(
                    IntakeEvidenceSource.SystemDefault,
                    IntakeEvidenceStrength.Strong,
                    IntakeEvidenceFinding.ExtractedField,
                    "instruction-date-defaulted",
                    "Instruction date was absent and was defaulted from the injected clock."));
                continue;
            }

            if (candidates.Length == 0)
            {
                fields.Add(new(definition.Name, null, [], false, false));
                if (definition.IsRequired)
                {
                    missing.Add(definition.Name);
                    evidence.Add(new(
                        IntakeEvidenceSource.SystemDefault,
                        IntakeEvidenceStrength.Strong,
                        IntakeEvidenceFinding.MissingField,
                        definition.Name,
                        $"No {definition.Name.ToLowerInvariant()} suggestion was found."));
                }
                continue;
            }

            if (candidates.Length > 1)
            {
                fields.Add(new(definition.Name, null, candidates, false, true));
                evidence.Add(new(
                    candidates[0].Source,
                    IntakeEvidenceStrength.Strong,
                    IntakeEvidenceFinding.ConflictingField,
                    definition.Name,
                    $"Conflicting {definition.Name.ToLowerInvariant()} candidates require operator review."));
                continue;
            }

            fields.Add(new(definition.Name, candidates[0].Value, candidates, false, false));
            evidence.Add(new(
                candidates[0].Source,
                IntakeEvidenceStrength.Strong,
                IntakeEvidenceFinding.ExtractedField,
                definition.Name,
                $"{definition.Name} was suggested from {candidates[0].SourceLabel}."));
        }

        return (fields, missing, evidence);
    }

    private static IEnumerable<InstructionFieldCandidate> FindCandidates(
        IntakeContentFragment fragment,
        FieldDefinition definition,
        IReadOnlyList<FieldDefinition> definitions)
    {
        var lines = fragment.Text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.TrimEntries);

        for (var index = 0; index < lines.Length; index++)
        {
            foreach (var label in definition.Labels)
            {
                var match = Regex.Match(
                    lines[index],
                    $@"(?i)(?:^|\s){Regex.Escape(label)}\s*(?::|-)?\s*(?<value>.*)$",
                    RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(100));
                if (!match.Success)
                {
                    continue;
                }

                var value = match.Groups["value"].Value.Trim(' ', ':', '-', '|');
                if (string.IsNullOrWhiteSpace(value))
                {
                    var nextLine = lines
                        .Skip(index + 1)
                        .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));
                    value = nextLine is not null && !StartsWithKnownFieldLabel(nextLine, definitions)
                        ? nextLine
                        : string.Empty;
                }

                value = WhitespaceRegex().Replace(value, " ").Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return new(value, fragment.Source, fragment.SourceLabel);
                }

                break;
            }
        }
    }

    private static bool StartsWithKnownFieldLabel(
        string line,
        IReadOnlyList<FieldDefinition> definitions) =>
        definitions.Any(definition => definition.Labels.Any(label =>
            Regex.IsMatch(
                line,
                $@"(?i)^{Regex.Escape(label)}(?:\s*(?::|-|\|)\s*|\s+|$)",
                RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100))));

    internal static bool ContainsLabel(string text, string label) =>
        Regex.IsMatch(
            text,
            $@"(?i)\b{Regex.Escape(label)}\b",
            RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

    internal static string? TypedString(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength ? value : null;

    internal static string? NormalizeRegistration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = Regex.Replace(value, @"[\s-]", string.Empty, RegexOptions.CultureInvariant)
            .ToUpperInvariant();
        return normalized.Length <= 20 && RegistrationRegex().IsMatch(normalized) ? normalized : null;
    }

    internal static DateOnly? ParseDate(string? value)
    {
        if (DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var exactDate))
        {
            return exactDate;
        }

        return DateOnly.TryParse(
            value,
            CultureInfo.GetCultureInfo("en-GB"),
            DateTimeStyles.AllowWhiteSpaces,
            out var date)
            ? date
            : null;
    }

    internal static long? ParseMileage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !MileageRegex().IsMatch(value))
        {
            return null;
        }

        var normalized = Regex.Replace(
            value,
            @"(?i)\s*(?:miles?|mi)\s*$",
            string.Empty,
            RegexOptions.CultureInvariant);
        return long.TryParse(
            normalized,
            NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out var mileage)
            ? mileage
            : null;
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^\s*(?:\d+|\d{1,3}(?:,\d{3})+)\s*(?:miles?|mi)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MileageRegex();

    [GeneratedRegex("^[A-Z0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex RegistrationRegex();
}
