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
        bool IsRequired = true,
        Func<string, bool>? AcceptsValue = null,
        Func<string, bool>? IsValidTyped = null,
        Func<string, string?>? CanonicalValue = null);

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
            var discovered = fragments
                .SelectMany((fragment, rank) => FindCandidates(fragment, definition, definitions)
                    .Select(candidate => (Candidate: candidate, FragmentRank: rank)))
                .DistinctBy(entry => entry.Candidate.Value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var candidates = discovered
                .Select(entry => entry.Candidate)
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

            if (candidates.Length == 0 && definition.Name == "Vehicle registration")
            {
                var soleRegistration = FindSoleUnlabelledRegistration(fragments);
                if (soleRegistration is not null)
                {
                    fields.Add(new(definition.Name, soleRegistration.Value, [soleRegistration], false, false));
                    evidence.Add(new(
                        soleRegistration.Source,
                        IntakeEvidenceStrength.Strong,
                        IntakeEvidenceFinding.ExtractedField,
                        definition.Name,
                        $"{definition.Name} was suggested from {soleRegistration.SourceLabel} as the document's only registration-shaped value."));
                    continue;
                }
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
                var resolved = ResolveConflictingCandidates(definition, discovered);
                if (resolved is null)
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

                fields.Add(new(definition.Name, resolved.Value, candidates, false, false));
                evidence.Add(new(
                    resolved.Source,
                    IntakeEvidenceStrength.Strong,
                    IntakeEvidenceFinding.ExtractedField,
                    definition.Name,
                    $"{definition.Name} was suggested from {resolved.SourceLabel}."));
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
        // The real correspondence writes typographic apostrophes (\u2019); labels
        // and lookaheads reason in ASCII, so the line text is normalized once here.
        var lines = fragment.Text
            .Replace('\u2019', '\'')
            .Replace('\u2018', '\'')
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.TrimEntries);

        for (var index = 0; index < lines.Length; index++)
        {
            foreach (var label in definition.Labels)
            {
                // A bare label token must sit at a plausible label position (line
                // start or after a clear separator); a label immediately followed by
                // an explicit ':' or '-' is a label wherever it sits on the line.
                // A "TP "-prefixed label is the third party's row, never the
                // claimant field (letters carry "TP Vehicle:"/"TP Registration:").
                var match = Regex.Match(
                    lines[index],
                    $@"(?i)(?:^|[|;\t]\s*|\s{{2,}})(?<!\bTP\s){Regex.Escape(label)}(?!['\w])\s*(?::|-)?\s*(?<value>.*)$",
                    RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(100));
                if (!match.Success)
                {
                    match = Regex.Match(
                        lines[index],
                        $@"(?i)(?:^|\s)(?<!\bTP\s){Regex.Escape(label)}(?!['\w])\s*(?::|-)\s*(?<value>.*)$",
                        RegexOptions.CultureInvariant,
                        TimeSpan.FromMilliseconds(100));
                }

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

                value = TruncateAtFollowingFieldLabel(value, definitions);
                value = TruncateAtColumnBoundary(value);
                value = WhitespaceRegex().Replace(value, " ").Trim();
                if (!string.IsNullOrWhiteSpace(value)
                    && (definition.AcceptsValue is null || definition.AcceptsValue(value)))
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

    /// <summary>
    /// Deterministic resolution of multiple distinct candidates: candidates whose
    /// value satisfies the definition's typed-validity check beat those that do not;
    /// among what remains the earliest fragment (document order — instruction
    /// material precedes appended reports) wins when it is unambiguous. Distinct
    /// values inside the same fragment stay a genuine conflict.
    /// </summary>
    private static InstructionFieldCandidate? ResolveConflictingCandidates(
        FieldDefinition definition,
        IReadOnlyList<(InstructionFieldCandidate Candidate, int FragmentRank)> candidates)
    {
        var pool = candidates;
        if (definition.IsValidTyped is not null)
        {
            var valid = candidates
                .Where(entry => definition.IsValidTyped(entry.Candidate.Value))
                .ToArray();
            if (valid.Length == 1)
            {
                return valid[0].Candidate;
            }

            if (valid.Length > 1)
            {
                pool = valid;
            }
        }

        // Distinct spellings of one typed value ("15 August 2026" beside
        // "15/08/2026", "V2 MTM" beside "V2MTM") are not a conflict: when every
        // remaining candidate canonicalizes to the same value, the first in
        // document order wins.
        if (definition.CanonicalValue is not null)
        {
            var canonicals = pool
                .Select(entry => definition.CanonicalValue(entry.Candidate.Value))
                .ToArray();
            if (canonicals.All(value => value is not null)
                && canonicals.Distinct(StringComparer.Ordinal).Count() == 1)
            {
                return pool[0].Candidate;
            }
        }

        var earliestRank = pool.Min(entry => entry.FragmentRank);
        var earliest = pool
            .Where(entry => entry.FragmentRank == earliestRank)
            .ToArray();
        if (earliest.Length == 1)
        {
            return earliest[0].Candidate;
        }

        // The letters wrap long values across physical lines ("Client's
        // Vehicle: MERCEDES-BENZ E 220" continued on the next line), so the
        // repeated details block yields a truncated prefix of the full value.
        // Within the winning fragment, when every other candidate is a
        // word-boundary prefix of the longest one, the longest is the value,
        // not a conflict.
        var longest = earliest
            .OrderByDescending(entry => entry.Candidate.Value.Length)
            .First();
        if (earliest.All(entry => entry.Candidate == longest.Candidate
                || longest.Candidate.Value.StartsWith(
                    entry.Candidate.Value + " ",
                    StringComparison.OrdinalIgnoreCase)))
        {
            return longest.Candidate;
        }

        return null;
    }

    /// <summary>
    /// Finds the document's only registration-shaped value (current-format UK
    /// registration, uppercase) across every fragment. Returns null when none or
    /// more than one distinct registration appears — an ambiguous read is withheld
    /// rather than guessed.
    /// </summary>
    private static InstructionFieldCandidate? FindSoleUnlabelledRegistration(
        IReadOnlyList<IntakeContentFragment> fragments)
    {
        string? normalized = null;
        InstructionFieldCandidate? candidate = null;
        foreach (var fragment in fragments)
        {
            foreach (Match match in UnlabelledRegistrationRegex().Matches(fragment.Text))
            {
                var value = match.Value.Replace(" ", string.Empty, StringComparison.Ordinal);
                if (normalized is null)
                {
                    normalized = value;
                    candidate = new(match.Value, fragment.Source, fragment.SourceLabel);
                }
                else if (!string.Equals(normalized, value, StringComparison.Ordinal))
                {
                    return null;
                }
            }
        }

        return candidate;
    }

    /// <summary>
    /// Cuts a labelled value where the next known field label (followed by an
    /// explicit ':' or '-') begins, so a flattened line carrying several labelled
    /// fields does not bleed one field's text into another's value.
    /// </summary>
    private static string TruncateAtFollowingFieldLabel(
        string value,
        IReadOnlyList<FieldDefinition> definitions)
    {
        var cut = value.Length;
        foreach (var definition in definitions)
        {
            foreach (var label in definition.Labels)
            {
                var match = Regex.Match(
                    value,
                    $@"(?i)(?:^|\s){Regex.Escape(label)}\s*(?::|-)",
                    RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(100));
                if (match.Success && match.Index < cut)
                {
                    cut = match.Index;
                }
            }
        }

        return value[..cut];
    }

    /// <summary>
    /// Cuts a labelled value at the first column boundary left behind when a tabular
    /// row is flattened into a single line: a tab, a pipe, a run of two or more spaces,
    /// or a whitespace-preceded colon (a genuine label colon is attached to its label
    /// and is consumed by the label match).
    /// </summary>
    private static string TruncateAtColumnBoundary(string value)
    {
        var boundary = ColumnBoundaryRegex().Match(value);
        return boundary.Success ? value[..boundary.Index] : value;
    }

    /// <summary>
    /// A vehicle make/model candidate is implausible when it carries wheel-position
    /// tokens, MOT/brake test-result vocabulary, or characters outside the shapes real
    /// makes and models use — the residue of a flattened test-results table row.
    /// </summary>
    internal static bool IsPlausibleVehicleMakeModel(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && !MotVocabularyRegex().IsMatch(value)
        && MakeModelCharsetRegex().IsMatch(value);

    /// <summary>
    /// Whether a value is a current-format UK registration once spacing and hyphens
    /// are removed. Used to let a well-formed registration candidate beat free text.
    /// </summary>
    internal static bool IsCurrentFormatRegistration(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && CurrentFormatRegistrationRegex().IsMatch(
            Regex.Replace(value, @"[\s-]", string.Empty, RegexOptions.CultureInvariant)
                .ToUpperInvariant());

    /// <summary>
    /// Whether a value is a plausible UK registration in the current
    /// (AB12 CDE), prefix (L100 YDR), or suffix (ABC 123L) format once
    /// spacing and hyphens are removed. Labelled registration fields and the
    /// vehicle-description split accept all three; the unlabelled sole-VRM
    /// fallback stays current-format-only, where a false positive is far
    /// more likely.
    /// </summary>
    internal static bool IsUkRegistration(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && UkRegistrationRegex().IsMatch(
            Regex.Replace(value, @"[\s-]", string.Empty, RegexOptions.CultureInvariant)
                .ToUpperInvariant());

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

    internal static string? CanonicalDate(string value) =>
        ParseDate(value)?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    internal static DateOnly? ParseDate(string? value)
    {
        if (value is not null)
        {
            // "27th April 2026" — the correspondence writes ordinal day
            // suffixes that DateOnly parsing rejects.
            value = OrdinalDaySuffixRegex().Replace(value, string.Empty);
        }

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

    [GeneratedRegex(@"(?<=\d)(?:st|nd|rd|th)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OrdinalDaySuffixRegex();

    [GeneratedRegex(@"^\s*(?:\d+|\d{1,3}(?:,\d{3})+)\s*(?:miles?|mi)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MileageRegex();

    [GeneratedRegex("^[A-Z0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex RegistrationRegex();

    [GeneratedRegex(@"[\t|]|\s{2,}|\s+:", RegexOptions.CultureInvariant)]
    private static partial Regex ColumnBoundaryRegex();

    [GeneratedRegex(
        @"\b(?:NSF|OSF|NSR|OSR|SATISFACTORY|ADVISORY|DANGEROUS|FOOTBRAKE|HANDBRAKE|PASS|FAIL|MOT)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MotVocabularyRegex();

    [GeneratedRegex(@"^[\p{L}\p{N}\s\-.'&/()+]+$", RegexOptions.CultureInvariant)]
    private static partial Regex MakeModelCharsetRegex();

    [GeneratedRegex(@"\b[A-Z]{2}[0-9]{2} ?[A-Z]{3}\b", RegexOptions.CultureInvariant)]
    private static partial Regex UnlabelledRegistrationRegex();

    [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex CurrentFormatRegistrationRegex();

    [GeneratedRegex("^(?:[A-Z]{2}[0-9]{2}[A-Z]{3}|[A-Z][0-9]{1,3}[A-Z]{3}|[A-Z]{3}[0-9]{1,3}[A-Z])$", RegexOptions.CultureInvariant)]
    private static partial Regex UkRegistrationRegex();
}
