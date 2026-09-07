using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

/// <summary>
/// Provider-neutral instruction field reading: label-anchored candidate discovery and
/// value parsing shared by provider extraction policies. Which fields a policy expects
/// remains that policy's own definition list.
/// </summary>
internal static partial class InstructionFieldEngine
{
    /// <param name="FormFields">
    /// PDF form-field names that carry this field, when the provider's template
    /// is a form. A form field's identity is its own; it is never inferred from
    /// a neighbouring label.
    /// </param>
    /// <param name="ColumnHeader">
    /// For a value read from a table row, the header of the column the value
    /// must sit under. A provider printing two parties in paired columns states
    /// which column is whose in its header row, and this is how a definition
    /// says which party it is asking about. Without it every viable column is
    /// returned and the field is ambiguous rather than guessed.
    /// </param>
    /// <param name="PartyRole">
    /// Which separate role this field's value belongs to — claimant, driver,
    /// repairer, third party, principal, instruction. The roles the intake
    /// invariants keep apart are kept apart HERE, on the definition, so a
    /// policy cannot declare a field without saying whose fact it is and a
    /// second role map cannot drift from the definition list.
    /// </param>
    /// <param name="ReferenceRole">
    /// For a reference or claim number, whose reference it is: the principal's
    /// own, an insurer's policy or claim number, a solicitor's file. Two
    /// numbers printed on one instruction are two roles, never two spellings
    /// of one field.
    /// </param>
    /// <param name="DefaultsToProcessedDate">
    /// Whether an absent value is filled from the injected clock. Only a field
    /// a profile has explicitly opted in carries this: today's date is not an
    /// extracted fact, so a profile that does not ask for the default records
    /// the absence instead (INTK-060 C03).
    /// </param>
    /// <param name="AllowsSoleUnlabelledRegistration">
    /// Whether the document's single unlabelled registration-shaped value may
    /// stand in when no labelled one was found. Opt-in per definition rather
    /// than keyed on a field's name, so the rule belongs to the profile that
    /// wants it.
    /// </param>
    internal sealed record FieldDefinition(
        string Name,
        string[] Labels,
        bool IsRequired = true,
        Func<string, bool>? AcceptsValue = null,
        Func<string, bool>? IsValidTyped = null,
        Func<string, string?>? CanonicalValue = null,
        string[]? GuardedPrefixes = null,
        bool PrefersLatestFragment = false,
        string[]? FormFields = null,
        string? ColumnHeader = null,
        string? PartyRole = null,
        string? ReferenceRole = null,
        bool DefaultsToProcessedDate = false,
        bool AllowsSoleUnlabelledRegistration = false);

    /// <summary>
    /// Regexes whose patterns depend on a field definition's labels. The QDOS
    /// definition set is fixed, so construct these once with the definitions
    /// and reuse the instances for every fragment and line.
    /// </summary>
    internal sealed class LabelRegexCache
    {
        private sealed class DefinitionPatterns(
            Regex[] candidate,
            Regex[] explicitCandidate,
            Regex[] startsWith,
            Regex[] followingLabel)
        {
            internal Regex Candidate(int index, bool requiresExplicitSeparator) =>
                (requiresExplicitSeparator ? explicitCandidate : candidate)[index];

            internal Regex StartsWith(int index) => startsWith[index];

            internal Regex FollowingLabel(int index) => followingLabel[index];
        }

        private readonly Dictionary<FieldDefinition, DefinitionPatterns> patterns = [];

        internal LabelRegexCache(IReadOnlyList<FieldDefinition> definitions)
        {
            foreach (var definition in definitions)
            {
                var guard = definition.GuardedPrefixes is { Length: > 0 } prefixes
                    ? $@"(?<!\b(?:{string.Join('|', prefixes.Select(Regex.Escape))})\s)"
                    : string.Empty;
                var candidate = new Regex[definition.Labels.Length];
                var explicitCandidate = new Regex[definition.Labels.Length];
                var startsWith = new Regex[definition.Labels.Length];
                var followingLabel = new Regex[definition.Labels.Length];

                for (var index = 0; index < definition.Labels.Length; index++)
                {
                    var label = Regex.Escape(definition.Labels[index]);
                    candidate[index] = new(
                        $@"(?i)(?:^|[|;\t]\s*|\s{{2,}}){guard}{label}(?!['\w])\s*(?::|-)?\s*(?<value>.*)$",
                        RegexOptions.CultureInvariant,
                        TimeSpan.FromMilliseconds(100));
                    explicitCandidate[index] = new(
                        $@"(?i)(?:^|\s){guard}{label}(?!['\w])\s*(?::|-)\s*(?<value>.*)$",
                        RegexOptions.CultureInvariant,
                        TimeSpan.FromMilliseconds(100));
                    startsWith[index] = new(
                        $@"(?i)^{label}(?:\s*(?::|-|\|)\s*|\s+|$)",
                        RegexOptions.CultureInvariant,
                        TimeSpan.FromMilliseconds(100));
                    followingLabel[index] = new(
                        $@"(?i)(?:^|\s){label}\s*(?::|-)",
                        RegexOptions.CultureInvariant,
                        TimeSpan.FromMilliseconds(100));
                }

                patterns.Add(definition, new(candidate, explicitCandidate, startsWith, followingLabel));
            }
        }

        internal Regex Candidate(
            FieldDefinition definition,
            int labelIndex,
            bool requiresExplicitSeparator) =>
            patterns[definition].Candidate(labelIndex, requiresExplicitSeparator);

        internal Regex StartsWith(FieldDefinition definition, int labelIndex) =>
            patterns[definition].StartsWith(labelIndex);

        internal Regex FollowingLabel(FieldDefinition definition, int labelIndex) =>
            patterns[definition].FollowingLabel(labelIndex);
    }

    /// <summary>
    /// The document's own structure, as the reader reported it: its PDF form
    /// fields and its table cells, indexed so a label can find the value bound
    /// to it.
    ///
    /// Two bounded layout rules, and no others. A label printed in a table's
    /// header row labels the cells BENEATH it in its column; a label printed in
    /// the body of a table labels the cells BESIDE it on its own row, and only
    /// when there is nothing beside it does it label the cell beneath. A form
    /// field's identity is its own name. There is no score, no priority and no
    /// first-match winner: where a label admits more than one value cell, every
    /// one of them is returned and the field comes back ambiguous.
    /// </summary>
    internal sealed class SourceStructure
    {
        private readonly List<(IntakeContentFragment Fragment, int Rank)> formFields = [];
        private readonly List<TableIndex> tables = [];

        internal SourceStructure(IReadOnlyList<IntakeContentFragment> fragments)
        {
            ArgumentNullException.ThrowIfNull(fragments);
            var byTable = new Dictionary<int, TableIndex>();
            for (var rank = 0; rank < fragments.Count; rank++)
            {
                var fragment = fragments[rank];
                switch (fragment.Locator)
                {
                    case { Kind: IntakeLocatorKind.FormField, FormField: not null }:
                        this.formFields.Add((fragment, rank));
                        break;
                    case { Kind: IntakeLocatorKind.TableCell, Table: { } table, Row: { } row, Column: { } column }:
                        if (!byTable.TryGetValue(table, out var index))
                        {
                            index = new();
                            byTable.Add(table, index);
                            this.tables.Add(index);
                        }

                        index.Add(row, column, fragment, rank);
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// Every candidate the document's structure binds to one definition, in
        /// the order it was read. An empty result means the document states this
        /// field in no cell and no form field, and the caller falls back to the
        /// flattened line scan.
        /// </summary>
        internal IEnumerable<(InstructionFieldCandidate Candidate, int FragmentRank)> Bind(
            FieldDefinition definition,
            IReadOnlyList<FieldDefinition> definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(definitions);
            var occurrence = 0;
            foreach (var bound in BindFormFields(definition).Concat(BindCells(definition, definitions)))
            {
                // Occurrence counts the repetitions of one field within one
                // source, so two printings of the same value stay two readings.
                var candidate = bound.Candidate.Locator is { } locator
                    ? bound.Candidate with { Locator = locator with { Occurrence = occurrence } }
                    : bound.Candidate;
                occurrence++;
                // Every structured reading of one field is a PEER of every
                // other, so they all carry the same rank. Document order is a
                // rule about a document's parts — an instruction before an
                // appended report — and it says nothing about two cells of one
                // row: letting it choose between them would be exactly the
                // first-match winner the invariants forbid. Two cells the
                // document supports equally stay ambiguous.
                yield return (candidate, 0);
            }
        }

        private IEnumerable<(InstructionFieldCandidate Candidate, int FragmentRank)> BindFormFields(
            FieldDefinition definition)
        {
            foreach (var (fragment, rank) in this.formFields)
            {
                if (!MatchesFormField(definition, fragment.Locator!.FormField!))
                {
                    continue;
                }

                if (Bound(fragment, definition) is { } candidate)
                {
                    yield return (candidate, rank);
                }
            }
        }

        private IEnumerable<(InstructionFieldCandidate Candidate, int FragmentRank)> BindCells(
            FieldDefinition definition,
            IReadOnlyList<FieldDefinition> definitions)
        {
            foreach (var table in this.tables)
            {
                foreach (var label in table.LabelCells(definition))
                {
                    foreach (var value in table.ValueCellsFor(label, definition, definitions))
                    {
                        if (Bound(value.Fragment, definition) is { } candidate)
                        {
                            yield return (candidate, value.Rank);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// A definition that names its form fields is bound by those names and
        /// nothing else — the point of a form field is that its identity is
        /// stated rather than inferred. A definition that names none falls back
        /// to its printed labels, which is how a form whose fields are named
        /// after the labels beside them still reads.
        /// </summary>
        private static bool MatchesFormField(FieldDefinition definition, string formField)
        {
            var normalized = Normalize(formField);
            if (normalized.Length == 0)
            {
                return false;
            }

            return definition.FormFields is { Length: > 0 } names
                ? names.Any(name => string.Equals(Normalize(name), normalized, StringComparison.Ordinal))
                : definition.Labels.Any(
                    label => string.Equals(Normalize(label), normalized, StringComparison.Ordinal));
        }

        /// <summary>
        /// A candidate from one already-bounded cell or form field. Whitespace is
        /// collapsed for the value the pipeline reads; the printed text is kept
        /// beside it whenever the two differ, because normalization never
        /// destroys the source value.
        /// </summary>
        private static InstructionFieldCandidate? Bound(
            IntakeContentFragment fragment,
            FieldDefinition definition)
        {
            var raw = fragment.Text;
            var value = WhitespaceRegex().Replace(raw, " ").Trim();
            return string.IsNullOrWhiteSpace(value)
                || (definition.AcceptsValue is not null && !definition.AcceptsValue(value))
                    ? null
                    : new(
                        value,
                        fragment.Source,
                        fragment.SourceLabel,
                        fragment.Locator,
                        string.Equals(raw, value, StringComparison.Ordinal) ? null : raw);
        }

        /// <summary>
        /// Punctuation-and-whitespace-insensitive comparison of one printed
        /// label to one definition label. Bounded label rules may normalize
        /// punctuation and whitespace; they may not match a substring, which is
        /// what would let a neighbouring column's header be read as this label.
        /// </summary>
        private static string Normalize(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
            }

            return builder.ToString();
        }

        private static bool IsLabelOf(string text, FieldDefinition definition)
        {
            var normalized = Normalize(text);
            return normalized.Length > 0
                && definition.Labels.Any(
                    label => string.Equals(Normalize(label), normalized, StringComparison.Ordinal));
        }

        /// <summary>
        /// A cell can carry a value when it says something and that something is
        /// not itself one of the document's known labels — a label cell is never
        /// another label's value.
        /// </summary>
        private static bool IsValueCell(string text, IReadOnlyList<FieldDefinition> definitions)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var normalized = Normalize(text);
            return !definitions.Any(definition => definition.Labels.Any(
                label => string.Equals(Normalize(label), normalized, StringComparison.Ordinal)));
        }

        private sealed class TableIndex
        {
            private readonly Dictionary<(int Row, int Column), (IntakeContentFragment Fragment, int Rank)> cells = [];
            private int headerRow = int.MaxValue;

            internal void Add(int row, int column, IntakeContentFragment fragment, int rank)
            {
                this.cells[(row, column)] = (fragment, rank);
                this.headerRow = Math.Min(this.headerRow, row);
            }

            internal IEnumerable<(int Row, int Column)> LabelCells(FieldDefinition definition) =>
                this.cells
                    .Where(entry => IsLabelOf(entry.Value.Fragment.Text, definition))
                    .Select(entry => entry.Key)
                    .OrderBy(key => key.Row)
                    .ThenBy(key => key.Column);

            internal (IntakeContentFragment Fragment, int Rank)[] ValueCellsFor(
                (int Row, int Column) label,
                FieldDefinition definition,
                IReadOnlyList<FieldDefinition> definitions)
            {
                // A header labels its own column: its values are aligned beneath
                // it, never beside it.
                if (label.Row == this.headerRow)
                {
                    return
                    [
                        .. this.cells
                            .Where(entry => entry.Key.Column == label.Column
                                && entry.Key.Row > label.Row
                                && IsValueCell(entry.Value.Fragment.Text, definitions))
                            .OrderBy(entry => entry.Key.Row)
                            .Select(entry => entry.Value)
                    ];
                }

                // A label in the body of the table labels what is beside it. Where
                // the definition names the column header it is asking about — a
                // provider printing two parties in paired columns — only that
                // column's cell is viable; without one, every viable column is
                // returned and the field is ambiguous rather than guessed at.
                var siblings = this.cells
                    .Where(entry => entry.Key.Row == label.Row
                        && entry.Key.Column > label.Column
                        && IsValueCell(entry.Value.Fragment.Text, definitions)
                        && (definition.ColumnHeader is null
                            || HeaderMatches(entry.Key.Column, definition.ColumnHeader)))
                    .OrderBy(entry => entry.Key.Column)
                    .Select(entry => entry.Value)
                    .ToArray();
                if (siblings.Length > 0)
                {
                    return siblings;
                }

                // Nothing beside it: a label alone on its row labels the cell
                // beneath. A definition that named a column header is not
                // satisfied by that, so it stays unbound.
                return definition.ColumnHeader is null
                    && this.cells.TryGetValue((label.Row + 1, label.Column), out var below)
                    && IsValueCell(below.Fragment.Text, definitions)
                        ? [below]
                        : [];
            }

            private bool HeaderMatches(int column, string columnHeader) =>
                this.cells.TryGetValue((this.headerRow, column), out var header)
                && string.Equals(
                    Normalize(header.Fragment.Text),
                    Normalize(columnHeader),
                    StringComparison.Ordinal);
        }
    }

    internal static (IReadOnlyList<InstructionReviewField> Fields, IReadOnlyList<string> Missing, IReadOnlyList<IntakeEvidence> Evidence)
        ExtractFields(
            IReadOnlyList<IntakeContentFragment> fragments,
            IReadOnlyList<FieldDefinition> definitions,
            LabelRegexCache regexCache,
            DateTimeOffset processedAtUtc)
    {
        var fields = new List<InstructionReviewField>();
        var missing = new List<string>();
        var evidence = new List<IntakeEvidence>();
        // Built once for the whole document: which fragments are table cells and
        // which are PDF form fields, indexed so a label can find the cell beside
        // or beneath it without re-scanning the document for every field.
        var structure = new SourceStructure(fragments);

        foreach (var definition in definitions)
        {
            // Structure beats flattened text, and does so by construction rather
            // than by score: where the document states a field in a cell or a
            // form field, that binding IS the reading, and the line scan's guess
            // at the same flattened row is not offered beside it. This is what
            // stops a neighbouring column's value being read as this field's.
            var structured = structure
                .Bind(definition, definitions)
                .DistinctBy(entry => entry.Candidate.Value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var discovered = structured.Length > 0
                ? structured
                : fragments
                    .SelectMany((fragment, rank) => FindCandidates(fragment, definition, definitions, regexCache)
                        .Select(candidate => (Candidate: candidate, FragmentRank: rank)))
                    .DistinctBy(entry => entry.Candidate.Value, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            var candidates = discovered
                .Select(entry => entry.Candidate)
                .ToArray();

            if (candidates.Length == 0 && definition.DefaultsToProcessedDate)
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

            if (candidates.Length == 0 && definition.AllowsSoleUnlabelledRegistration)
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
        IReadOnlyList<FieldDefinition> definitions,
        LabelRegexCache regexCache)
    {
        // A cell or a form field is already bounded by the document's own
        // structure. Line-scanning it again would re-create exactly the
        // flattening this path exists to work around, so structured fragments
        // are left to SourceStructure.
        if (fragment.Locator?.Kind is IntakeLocatorKind.TableCell or IntakeLocatorKind.FormField)
        {
            yield break;
        }

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
            for (var labelIndex = 0; labelIndex < definition.Labels.Length; labelIndex++)
            {
                // A bare label token must sit at a plausible label position (line
                // start or after a clear separator); a label immediately followed by
                // an explicit ':' or '-' is a label wherever it sits on the line.
                // A definition's guarded prefixes reject a label that is really
                // another party's row — the provider policy supplies the words
                // (this engine carries no provider grammar).
                var match = regexCache.Candidate(definition, labelIndex, false).Match(lines[index]);
                if (!match.Success)
                {
                    match = regexCache.Candidate(definition, labelIndex, true).Match(lines[index]);
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
                    value = nextLine is not null && !StartsWithKnownFieldLabel(nextLine, definitions, regexCache)
                        ? nextLine
                        : string.Empty;
                }

                // What the line actually printed, kept before the engine bounds
                // it. The bounded value is what downstream reads; the printed one
                // is what an operator is shown when two readings compete.
                var rawValue = value;
                value = TruncateAtFollowingFieldLabel(value, definitions, regexCache);
                value = TruncateAtColumnBoundary(value);
                value = WhitespaceRegex().Replace(value, " ").Trim();
                if (!string.IsNullOrWhiteSpace(value)
                    && (definition.AcceptsValue is null || definition.AcceptsValue(value)))
                {
                    yield return new(
                        value,
                        fragment.Source,
                        fragment.SourceLabel,
                        fragment.Locator,
                        string.Equals(rawValue, value, StringComparison.Ordinal) ? null : rawValue);
                }

                break;
            }
        }
    }

    private static bool StartsWithKnownFieldLabel(
        string line,
        IReadOnlyList<FieldDefinition> definitions,
        LabelRegexCache regexCache)
    {
        foreach (var definition in definitions)
        {
            for (var labelIndex = 0; labelIndex < definition.Labels.Length; labelIndex++)
            {
                if (regexCache.StartsWith(definition, labelIndex).IsMatch(line))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Deterministic resolution of multiple distinct candidates: candidates whose
    /// value satisfies the definition's typed-validity check beat those that do not;
    /// among what remains one fragment wins by document order when it is
    /// unambiguous. Distinct values inside the same fragment stay a genuine conflict.
    ///
    /// Document order normally favours the <em>earliest</em> fragment, because
    /// instruction material precedes appended reports and the instruction is the
    /// base. A definition may set <see cref="FieldDefinition.PrefersLatestFragment"/>
    /// to reverse that for itself — the inspection date does, because an appended
    /// engineer's report states when the vehicle was actually seen and overrides
    /// whatever the instruction proposed (ENG-015). The reversal is per field, not
    /// global.
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

        var winningRank = definition.PrefersLatestFragment
            ? pool.Max(entry => entry.FragmentRank)
            : pool.Min(entry => entry.FragmentRank);
        var winning = pool
            .Where(entry => entry.FragmentRank == winningRank)
            .ToArray();
        if (winning.Length == 1)
        {
            return winning[0].Candidate;
        }

        // The letters wrap long values across physical lines ("Client's
        // Vehicle: MERCEDES-BENZ E 220" continued on the next line), so the
        // repeated details block yields a truncated prefix of the full value.
        // Within the winning fragment, when every other candidate is a
        // word-boundary prefix of the longest one, the longest is the value,
        // not a conflict.
        var longest = winning
            .OrderByDescending(entry => entry.Candidate.Value.Length)
            .First();
        if (winning.All(entry => entry.Candidate == longest.Candidate
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
        IReadOnlyList<FieldDefinition> definitions,
        LabelRegexCache regexCache)
    {
        var cut = value.Length;
        foreach (var definition in definitions)
        {
            for (var labelIndex = 0; labelIndex < definition.Labels.Length; labelIndex++)
            {
                var match = regexCache.FollowingLabel(definition, labelIndex).Match(value);
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
            WhitespaceHyphenRegex().Replace(value, string.Empty)
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
            WhitespaceHyphenRegex().Replace(value, string.Empty)
                .ToUpperInvariant());

    internal static string? TypedString(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength ? value : null;

    internal static string? NormalizeRegistration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = WhitespaceHyphenRegex().Replace(value, string.Empty)
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

        var normalized = MileageSuffixRegex().Replace(value, string.Empty);
        return long.TryParse(
            normalized,
            NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out var mileage)
            ? mileage
            : null;
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 100)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"(?<=\d)(?:st|nd|rd|th)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)]
    private static partial Regex OrdinalDaySuffixRegex();

    [GeneratedRegex(@"^\s*(?:\d+|\d{1,3}(?:,\d{3})+)\s*(?:miles?|mi)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)]
    private static partial Regex MileageRegex();

    [GeneratedRegex("^[A-Z0-9]+$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex RegistrationRegex();

    [GeneratedRegex(@"[\t|]|\s{2,}|\s+:", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ColumnBoundaryRegex();

    [GeneratedRegex(
        @"\b(?:NSF|OSF|NSR|OSR|SATISFACTORY|ADVISORY|DANGEROUS|FOOTBRAKE|HANDBRAKE|PASS|FAIL|MOT)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        100)]
    private static partial Regex MotVocabularyRegex();

    [GeneratedRegex(@"^[\p{L}\p{N}\s\-.'&/()+]+$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex MakeModelCharsetRegex();

    [GeneratedRegex(@"\b[A-Z]{2}[0-9]{2} ?[A-Z]{3}\b", RegexOptions.CultureInvariant, 100)]
    private static partial Regex UnlabelledRegistrationRegex();

    [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z]{3}$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex CurrentFormatRegistrationRegex();

    [GeneratedRegex("^(?:[A-Z]{2}[0-9]{2}[A-Z]{3}|[A-Z][0-9]{1,3}[A-Z]{3}|[A-Z]{3}[0-9]{1,3}[A-Z])$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex UkRegistrationRegex();

    [GeneratedRegex(@"[\s-]", RegexOptions.CultureInvariant, 100)]
    private static partial Regex WhitespaceHyphenRegex();

    [GeneratedRegex(@"(?i)\s*(?:miles?|mi)\s*$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex MileageSuffixRegex();
}
