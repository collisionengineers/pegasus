using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

// `partial` because this policy owns generated regexes; the triage-matcher
// constructor parameter is gone -- INTK-033 replaced that matcher with
// classification-derived evidence, and nothing here reads it.
public sealed partial class QdosInstructionExtractionPolicy : IInstructionExtractionPolicy
{
    public const string Key = "qdos_instruction";
    // ENG-015 changed three extraction rules -- the bare `Date` label, the
    // labelled damage-area synthesis, and inspection-date fragment precedence.
    // The version is persisted as each extracted fact's provenance, so facts
    // read before and after must stay distinguishable for audit and
    // re-evaluation. Bumped for the same reason as v3 (letter shapes),
    // v4 (INTK-025), v5 (INTK-028) and v6 (INTK-033).
    public const int Version = 7;
    public const string SupportedPrincipalCode = "QDOS";

    public string PrincipalCode => SupportedPrincipalCode;

    // The letters' third-party rows ("TP Vehicle:", "TP Registration:",
    // "TP Representative Name:") must never feed the claimant's fields.
    // QDOS grammar, supplied to the neutral engine per definition.
    private static readonly string[] ThirdPartyRowPrefixes = ["TP"];

    private static readonly InstructionFieldEngine.FieldDefinition[] BareFieldDefinitions =
    [
        new("Claimant name", ["Claimant Name", "Claimant", "Our Client", "Client Name"]),
        new(
            "Claim number",
            ["Claim Number", "Claim No", "Claim Reference", "Claim Ref", "Our Reference", "Our Ref"]),
        new(
            "Vehicle registration",
            [
                "Vehicle Registration", "Registration Number", "Registration No",
                "Vehicle Reg No", "Vehicle Reg", "Registration", "Reg No", "VRM", "VRN"
            ],
            IsValidTyped: InstructionFieldEngine.IsUkRegistration,
            CanonicalValue: InstructionFieldEngine.NormalizeRegistration),
        new("Vehicle make", ["Vehicle Make", "Make"],
            AcceptsValue: InstructionFieldEngine.IsPlausibleVehicleMakeModel),
        new("Vehicle model", ["Vehicle Model", "Model"],
            AcceptsValue: InstructionFieldEngine.IsPlausibleVehicleMakeModel),
        new("Vehicle mileage", ["Vehicle Mileage", "Mileage"],
            IsValidTyped: value => InstructionFieldEngine.ParseMileage(value) is not null),
        new("Accident circumstances", ["Accident Circumstances", "Circumstances"]),
        new(
            "Date of incident",
            ["Date of Incident", "Incident Date", "Accident Date", "Date of Accident", "Accident on"],
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate),
        // The letters date themselves with a bare "Date:" row, so without it
        // every QDOS case silently fell back to its receipt date (ENG-015).
        // The bare label is deliberately last: a line that says "Instruction
        // Date" is matched by the specific label first.
        //
        // A bare "Date" also matches other date rows, in two shapes, and both
        // are shut off here rather than left to conflict resolution:
        //   "Date of Accident: 14/08/2026" yields "of Accident: 14/08/2026",
        //   which AcceptsValue rejects at discovery because it is not a date;
        //   "Accident Date: 14/08/2026" yields a perfectly valid date, so only
        //   the guarded prefixes can reject it — the value cannot.
        new(
            "Instruction date",
            ["Instruction Date", "Date of Instruction", "Date"],
            AcceptsValue: value => InstructionFieldEngine.ParseDate(value) is not null,
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate,
            GuardedPrefixes: ["Accident", "Incident", "Inspection", "Issue", "Report", "Due"]),
        new("Inspection address", ["Inspection Address", "Vehicle Location", "Inspection Location"]),
        // An appended engineer's report states when the vehicle was actually
        // seen; the instruction can only propose a date. So when both carry
        // one, the later fragment wins — the reverse of every other field
        // (ENG-015).
        new(
            "Inspection date",
            ["Inspection Date", "Date of Inspection", "Inspection Deadline", "Due By"],
            IsRequired: false,
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate,
            PrefersLatestFragment: true),
        // The real correspondence writes the vehicle as one description line
        // ("Our Client's Vehicle: PEUGEOT RCZ GT THP 156"); the split into
        // make/model/registration happens after extraction. The bare word
        // "Vehicle" is deliberately not a label here — it collides with the
        // registration and location labels.
        new(
            "Vehicle description",
            [
                "Our Client's Vehicle", "Client's Vehicle", "Claimant's Vehicle",
                "Client Vehicle", "Vehicle Description"
            ],
            IsRequired: false,
            AcceptsValue: InstructionFieldEngine.IsPlausibleVehicleMakeModel)
    ];

    /// <summary>
    /// Every definition is guarded against the third-party rows. A definition
    /// that named its own guarded prefixes keeps them — the third-party guard
    /// is added to that list rather than replacing it, so a per-field guard is
    /// not silently dropped here.
    /// </summary>
    private static readonly InstructionFieldEngine.FieldDefinition[] FieldDefinitions =
        [.. BareFieldDefinitions.Select(definition => definition with
        {
            GuardedPrefixes = [.. ThirdPartyRowPrefixes.Concat(definition.GuardedPrefixes ?? [])]
        })];

    private static readonly InstructionFieldEngine.LabelRegexCache FieldRegexCache =
        new(FieldDefinitions);

    /// <summary>
    /// Makes written as two words, so a combined vehicle description splits
    /// on the right boundary. Deterministic and deliberately small.
    /// </summary>
    private static readonly string[] TwoWordMakes =
    [
        "LAND ROVER", "ALFA ROMEO", "ASTON MARTIN", "MERCEDES BENZ", "ROLLS ROYCE"
    ];

    public InstructionExtractionResult Extract(
        IntakeSourceReadResult readResult,
        DateTimeOffset processedAtUtc,
        EstablishedPrincipalContext principalContext)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        ArgumentNullException.ThrowIfNull(principalContext);
        if (readResult.Status != IntakeSourceReadStatus.Readable || readResult.IsIncomplete)
        {
            throw new ArgumentException(
                "The QDOS extraction policy accepts only fully readable, complete reader results.",
                nameof(readResult));
        }
        if (!string.Equals(
                principalContext.PrincipalCode,
                SupportedPrincipalCode,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The established principal is not supported by the QDOS extraction policy.",
                nameof(principalContext));
        }

        var evidence = new List<IntakeEvidence>
        {
            new(
                IntakeEvidenceSource.Sender,
                IntakeEvidenceStrength.Strong,
                IntakeEvidenceFinding.SupportsPrincipal,
                "established-principal",
                $"Principal QDOS was established by {principalContext.PolicyKey} v{principalContext.PolicyVersion}.")
        };
        var (fields, missingFields, fieldEvidence) = InstructionFieldEngine.ExtractFields(
            WithDerivedFacts(readResult),
            FieldDefinitions,
            FieldRegexCache,
            processedAtUtc);
        fields = DeriveVehicleFields(fields, out var derivedNames);
        fields = WithLabelledDamageArea(fields, readResult.Content);
        missingFields = missingFields.Where(name => !derivedNames.Contains(name)).ToArray();
        evidence.AddRange(fieldEvidence);
        var draft = CreateInstructionDraft(fields, principalContext.PrincipalCode);
        if (readResult.RequiresOcr)
        {
            evidence.Add(new(
                IntakeEvidenceSource.PdfContent,
                IntakeEvidenceStrength.Strong,
                IntakeEvidenceFinding.Information,
                "additional-scanned-content",
                "A QDOS draft was extracted from readable content; additional scanned PDF content still requires review."));
        }

        return new(
            InstructionPolicyApplicability.Applicable,
            evidence,
            fields,
            draft,
            missingFields,
            Key,
            Version);
    }

    /// <summary>
    /// Everything the policy derives beyond the raw fragments, in rank order:
    /// the raw content first (the letter always outranks), then the
    /// circumstances paragraph synthesized from the letter's prompt, then the
    /// report-sourced vehicle facts, then the subject facts last.
    /// </summary>
    private static IReadOnlyList<IntakeContentFragment> WithDerivedFacts(
        IntakeSourceReadResult readResult)
    {
        var extended = new List<IntakeContentFragment>(readResult.Content);
        foreach (var fragment in readResult.Content)
        {
            // The circumstances prompt is its own test: only the letter asks
            // the question, so a report yields nothing here anyway. Gating
            // this on the report test as well meant broadening that test
            // could silently cost a letter its circumstances (INTK-028).
            if (CircumstancesParagraph(fragment) is { } circumstances)
            {
                extended.Add(circumstances);
            }
        }
        foreach (var fragment in readResult.Content)
        {
            extended.AddRange(ReportFacts(fragment));
        }

        return WithSubjectFacts(readResult, extended);
    }

    /// <summary>
    /// The report's own column labels, held in one place because two rules
    /// need the same list: the Vehicle rule cuts its value where the next
    /// column begins, and the Speedo rule must cut at exactly the same
    /// points. Written separately, the two drifted and the Speedo rule
    /// silently missed every multi-column line (INTK-028).
    /// </summary>
    private const string ReportColumnCutPattern =
        @"(?i)\s*(?:colour|color|speedo(?:meter)?|registered|reg\s*no|reg"
        + @"|vin|mileage|type|trans|body|derivative|fuel)\s*:.*$";

    /// <summary>
    /// There is deliberately no "is this fragment a report" test.
    ///
    /// The report accompanying an instruction is written by a third-party
    /// engineer — a different firm each time, named however that firm's
    /// system named it (operator, 2026-08-21). Identifying it by file name
    /// only ever worked for the firms whose name happened to contain
    /// "report", and any structural test would be one more thing to get
    /// wrong. Instead the report grammar runs over every fragment and is
    /// written so that only a report can satisfy it: the letters address the
    /// vehicle as "Our Client's Vehicle:" or "TP Vehicle:", never as a bare
    /// "Vehicle:" opening a line, and carry no "Speedo:" column at all.
    /// Its facts are appended after all content, so the letter still
    /// outranks wherever both speak (INTK-028).
    /// </summary>
    /// Trims a column value where the line's next column label begins, so a
    /// value never carries its neighbours.
    /// </summary>
    private static string CutAtNextColumnLabel(string value) => ReportColumnCutRegex()
        .Replace(value, string.Empty)
        .Trim();

    /// <summary>
    /// The bodyshop report's own grammar, rewritten as labelled lines the
    /// field definitions already read. Both the "Vehicle:" and "Speedo:"
    /// values are cut at their neighbouring column labels, because the real
    /// reports write them as columns of one physical line
    /// ("Vehicle: … Colour: … Speedo: … Reg No: …"). A "Speedo:" line
    /// contributes only when it actually carries digits ("Speedo: Miles"
    /// carries none). Appended after all content, so the letter outranks.
    /// </summary>
    private static IEnumerable<IntakeContentFragment> ReportFacts(
        IntakeContentFragment fragment)
    {
        foreach (var rawLine in SplitLines(fragment.Text))
        {
            // The third-party rows are the claimant's fields' one real
            // hazard here, and these rules read labels mid-line, so the
            // guard is applied once to the whole line rather than being
            // repeated — and forgotten — per rule.
            if (ThirdPartyRowRegex().IsMatch(rawLine))
            {
                continue;
            }

            var vehicle = VehicleReportRowRegex().Match(rawLine);
            if (vehicle.Success)
            {
                var value = CutAtNextColumnLabel(vehicle.Groups["value"].Value);
                if (value.Length > 0)
                {
                    yield return new(
                        fragment.Source,
                        fragment.SourceLabel,
                        $"Our Client's Vehicle: {value}");
                }
            }

            // Anchored to the label, not to the start of the line: the
            // Speedo column is almost never first (INTK-028).
            var speedo = SpeedoColumnRegex().Match(rawLine);
            if (speedo.Success)
            {
                var value = CutAtNextColumnLabel(speedo.Groups["value"].Value);
                if (value.Any(char.IsDigit))
                {
                    yield return new(
                        fragment.Source,
                        fragment.SourceLabel,
                        $"Vehicle Mileage: {value}");
                }
            }

            // The registration column has the same problem the mileage one
            // did: it is followed on the same line by "Registered:",
            // "Type:", "Trans:", so the raw value never reads as a
            // registration and the report's copy was silently unusable.
            var registration = ReportRegistrationColumnRegex().Match(rawLine);
            if (registration.Success)
            {
                var value = CutAtNextColumnLabel(registration.Groups["value"].Value);
                if (value.Length > 0)
                {
                    yield return new(
                        fragment.Source,
                        fragment.SourceLabel,
                        $"Vehicle Registration: {value}");
                }
            }
        }
    }

    /// <summary>
    /// The letter asks "…check the damage for consistency with the following
    /// accident circumstances?" and the paragraph after that line is the
    /// circumstances. It ends where the letter's next block begins.
    /// </summary>
    private static IntakeContentFragment? CircumstancesParagraph(
        IntakeContentFragment fragment)
    {
        var lines = SplitLines(fragment.Text);
        // The reader sometimes wraps the prompt across physical lines, so the
        // anchor is the phrase's final word closing the question.
        var prompt = Array.FindIndex(lines, CircumstancesPromptRegex().IsMatch);
        if (prompt < 0)
        {
            return null;
        }

        var paragraph = new List<string>();
        foreach (var line in lines.Skip(prompt + 1))
        {
            if (line.Length == 0 || CircumstancesStopRegex().IsMatch(line))
            {
                break;
            }
            paragraph.Add(line);
        }

        return paragraph.Count == 0
            ? null
            : new(
                fragment.Source,
                fragment.SourceLabel,
                $"Accident Circumstances: {string.Join(' ', paragraph)}");
    }

    private static string[] SplitLines(string text) => text
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace('\r', '\n')
        .Split('\n', StringSplitOptions.TrimEntries);

    /// <summary>
    /// The message subject carries settled facts in the principal's own
    /// grammar ("Client Mr X", "Vehicle ... AB12CDE", "Our Ref 46805_1",
    /// "RTA on 03_07_2026"). They are rewritten as labelled lines and
    /// appended as the LAST fragment, so anything the document body states
    /// wins rank-aware conflict resolution.
    /// </summary>
    private static IReadOnlyList<IntakeContentFragment> WithSubjectFacts(
        IntakeSourceReadResult readResult,
        IReadOnlyList<IntakeContentFragment> content)
    {
        var subject = readResult.TransportEvidence
            .FirstOrDefault(item =>
                item.Source == IntakeEvidenceSource.Subject
                && item.SenderIdentityKind == IntakeSenderIdentityKind.Transport)
            ?.Value;
        if (string.IsNullOrWhiteSpace(subject))
        {
            return content;
        }

        var lines = SubjectFactLines(subject);
        if (lines.Length == 0)
        {
            return content;
        }

        return
        [
            .. content,
            new(IntakeEvidenceSource.Subject, "message subject", string.Join('\n', lines))
        ];
    }

    internal static string[] SubjectFactLines(string subject)
    {
        var lines = new List<string>();
        var reference = SubjectReferenceRegex().Match(subject);
        if (reference.Success)
        {
            lines.Add($"Our Ref: {reference.Groups[1].Value.TrimEnd(',', ')', '.')}");
        }

        var incident = SubjectIncidentRegex().Match(subject);
        if (incident.Success)
        {
            lines.Add(
                $"Date of Accident: {incident.Groups[1].Value}/{incident.Groups[2].Value}/{incident.Groups[3].Value}");
        }

        var client = SubjectClientRegex().Match(subject);
        if (client.Success)
        {
            lines.Add($"Our Client: {client.Groups[1].Value.Trim().TrimEnd(',', ')', '.')}");
        }

        // The Triage subject template writes the registration as its own
        // labelled field, in two recorded spacings ("Vehicle Registration
        // YD14VGJ" and "Vehicle Registration : VO75DFJ"). It is the only
        // place that template states a registration at all — its body is
        // free prose — so without this rule every subject-template Triage
        // request falls to Unidentified (INTK-033).
        //
        // The separator is ONE bounded class, not `\s*[:.]?\s*`. Two
        // unbounded whitespace runs either side of an optional character are
        // ambiguous, and a long whitespace run then costs O(k²) to fail —
        // measured at 6.9 s for 16,000 spaces, four times worse per
        // doubling, on a subject header an approved sender controls. This
        // form is 1.1 ms at the same width. The value is two short bounded
        // runs, and the shape is validated outside the pattern.
        var subjectRegistration = SubjectRegistrationRegex().Match(subject);
        if (subjectRegistration.Success
            && InstructionFieldEngine.IsUkRegistration(subjectRegistration.Groups["value"].Value))
        {
            lines.Add($"Vehicle Registration: {subjectRegistration.Groups["value"].Value}");
        }

        // The lookahead keeps this rule off the registration label above.
        // Without it "Vehicle Registration : VO75DFJ" read as the vehicle
        // description "Registration : VO75DFJ".
        var vehicle = SubjectVehicleRegex().Match(subject);
        if (vehicle.Success)
        {
            lines.Add($"Our Client's Vehicle: {vehicle.Groups[1].Value.Trim().TrimEnd(',', '.')}");
        }

        return [.. lines];
    }

    /// <summary>
    /// Fills empty make/model/registration fields from a combined vehicle
    /// description ("PEUGEOT RCZ GT THP 156", possibly ending in the
    /// registration), carrying the description candidate's own provenance so
    /// the acceptance write still names a real source.
    /// </summary>
    /// <summary>
    /// Appends the letter's damage area to the accident circumstances, under
    /// its own label and below a blank line (ENG-015, operator direction):
    ///
    /// <code>
    /// &lt;circumstances prose, when the letter has any&gt;
    ///
    /// Damage Area: &lt;damage area&gt;
    /// </code>
    ///
    /// The QDOS audit letters carry no prose, so the value is usually the
    /// labelled damage area alone, with no leading blank line.
    ///
    /// This runs after the neutral engine rather than inside it because the
    /// engine collapses every whitespace run in a value — a deliberate rule
    /// for single-line fields that a two-part value cannot pass through.
    /// <see cref="DeriveVehicleFields"/> adjusts a field after extraction the
    /// same way.
    /// </summary>
    private static IReadOnlyList<InstructionReviewField> WithLabelledDamageArea(
        IReadOnlyList<InstructionReviewField> fields,
        IReadOnlyList<IntakeContentFragment> content)
    {
        var damageArea = content
            .Select(DamageArea)
            .FirstOrDefault(value => value is not null);
        if (damageArea is null)
        {
            return fields;
        }

        var labelled = $"{DamageAreaLabel}{damageArea}";
        return fields
            .Select(field =>
            {
                if (field.Name != "Accident circumstances" || field.HasConflict)
                {
                    return field;
                }

                var prose = field.SuggestedValue;
                var combined = string.IsNullOrWhiteSpace(prose)
                    ? labelled
                    : $"{prose}\n\n{labelled}";
                return field with
                {
                    SuggestedValue = combined,
                    Candidates = field.Candidates.Count == 0
                        ? [new(combined, IntakeEvidenceSource.PdfContent, DamageAreaSourceLabel)]
                        : [.. field.Candidates.Select((candidate, index) => index == 0
                            ? candidate with { Value = combined }
                            : candidate)]
                };
            })
            .ToArray();
    }

    /// <summary>
    /// The letter writes the damage area as one row — "Damage Area - Rear:
    /// Moderate" — and the block ends at the third-party rows. Returns the text
    /// after the label, or null when the fragment has no damage-area row.
    /// </summary>
    private static string? DamageArea(IntakeContentFragment fragment)
    {
        var lines = SplitLines(fragment.Text);
        var index = Array.FindIndex(lines, line =>
            DamageAreaRowRegex().IsMatch(line));
        if (index < 0)
        {
            return null;
        }

        // The description is a block, not a line: the letters wrap it across
        // physical rows mid-sentence ("...rear wheel arch is / damaged."), so
        // taking only the first row cuts the sentence in half. Read on until the
        // next block starts.
        var block = new List<string>();
        var inline = DamageAreaRowRegex().Replace(lines[index], string.Empty).Trim();
        if (inline.Length > 0)
        {
            block.Add(inline);
        }

        // A wrapped description continues on the rows immediately beneath the
        // label. When the label sat alone, its value starts after the blank rows.
        var rest = lines.Skip(index + 1);
        if (block.Count == 0)
        {
            rest = rest.SkipWhile(line => line.Length == 0);
        }

        foreach (var line in rest)
        {
            if (line.Length == 0 || DamageAreaStopRegex().IsMatch(line))
            {
                break;
            }

            block.Add(line);
        }

        return block.Count == 0 ? null : string.Join('\n', block);
    }

    private const string DamageAreaLabel = "Damage Area: ";
    private const string DamageAreaSourceLabel = "damage area";

    [GeneratedRegex(ReportColumnCutPattern, RegexOptions.CultureInvariant, 100)]
    private static partial Regex ReportColumnCutRegex();

    [GeneratedRegex(@"(?i)^TP\b", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ThirdPartyRowRegex();

    [GeneratedRegex(@"(?i)^vehicle\s*:\s*(?<value>.+)$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex VehicleReportRowRegex();

    [GeneratedRegex(@"(?i)\bspeedo(?:meter)?\s*:\s*(?<value>.*)$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex SpeedoColumnRegex();

    [GeneratedRegex(@"(?i)\breg(?:istration)?\s*(?:no|number)?\s*:\s*(?<value>.*)$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ReportRegistrationColumnRegex();

    [GeneratedRegex(@"(?i)\bcircumstances\s*\?\s*$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex CircumstancesPromptRegex();

    [GeneratedRegex(@"(?i)^(?:damage area|pre-existing damage|tp |if you need)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex CircumstancesStopRegex();

    [GeneratedRegex(@"\bOur Ref[:.]?\s+([A-Za-z0-9_/-]+)", RegexOptions.IgnoreCase, 100)]
    private static partial Regex SubjectReferenceRegex();

    [GeneratedRegex(@"\bRTA on\s+(\d{1,2})[_/.-](\d{1,2})[_/.-](\d{4})", RegexOptions.IgnoreCase, 100)]
    private static partial Regex SubjectIncidentRegex();

    [GeneratedRegex(@"\b(?:Client[:.]?\s+)?((?:Mr|Mrs|Ms|Miss|Dr|Mx)\.?\s+[A-Z][A-Za-z'-]+(?:\s+[A-Z][A-Za-z'-]+){1,3})", RegexOptions.None, 100)]
    private static partial Regex SubjectClientRegex();

    [GeneratedRegex(@"\bVehicle\s+Registration\b[\s:.-]{1,10}(?<value>[A-Za-z0-9]{1,4}[ -]?[A-Za-z0-9]{1,4})\b", RegexOptions.IgnoreCase, 100)]
    private static partial Regex SubjectRegistrationRegex();

    [GeneratedRegex(@"\bVehicle(?!\s+Registration\b)[:.]?\s+([^,()]+)", RegexOptions.IgnoreCase, 100)]
    private static partial Regex SubjectVehicleRegex();

    [GeneratedRegex(
        @"^\s*damage\s+area\s*[-:]?\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        100)]
    private static partial Regex DamageAreaRowRegex();

    [GeneratedRegex(
        @"^(?:pre-existing damage|tp |if you need)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        100)]
    private static partial Regex DamageAreaStopRegex();

    private static IReadOnlyList<InstructionReviewField> DeriveVehicleFields(
        IReadOnlyList<InstructionReviewField> fields,
        out HashSet<string> derivedNames)
    {
        derivedNames = new(StringComparer.Ordinal);
        var description = fields.FirstOrDefault(field =>
            field.Name == "Vehicle description"
            && !field.HasConflict
            && !string.IsNullOrWhiteSpace(field.SuggestedValue));
        if (description is null || description.Candidates.Count == 0)
        {
            return fields;
        }

        var origin = description.Candidates[0];
        var tokens = description.SuggestedValue!
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        string? registration = null;
        if (tokens.Count >= 2
            && InstructionFieldEngine.IsUkRegistration(
                string.Concat(tokens[^2], tokens[^1])))
        {
            registration = $"{tokens[^2]} {tokens[^1]}";
            tokens.RemoveRange(tokens.Count - 2, 2);
        }
        else if (tokens.Count >= 1
            && InstructionFieldEngine.IsUkRegistration(tokens[^1]))
        {
            registration = tokens[^1];
            tokens.RemoveAt(tokens.Count - 1);
        }

        string? make = null;
        string? model = null;
        if (tokens.Count > 0)
        {
            var upper = string.Join(' ', tokens).ToUpperInvariant();
            var twoWord = TwoWordMakes.FirstOrDefault(candidate =>
                upper.StartsWith(candidate + " ", StringComparison.Ordinal)
                || string.Equals(upper, candidate, StringComparison.Ordinal));
            var makeWordCount = twoWord is null ? 1 : 2;
            make = string.Join(' ', tokens.Take(makeWordCount));
            model = tokens.Count > makeWordCount
                ? string.Join(' ', tokens.Skip(makeWordCount))
                : null;
        }

        var updated = fields.ToList();
        Fill(updated, derivedNames, "Vehicle make", make, origin);
        Fill(updated, derivedNames, "Vehicle model", model, origin);
        Fill(updated, derivedNames, "Vehicle registration", registration, origin);
        return updated;

        static void Fill(
            List<InstructionReviewField> fields,
            HashSet<string> derivedNames,
            string name,
            string? value,
            InstructionFieldCandidate origin)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }
            var index = fields.FindIndex(field => field.Name == name);
            if (index < 0
                || fields[index].HasConflict
                || !string.IsNullOrWhiteSpace(fields[index].SuggestedValue))
            {
                return;
            }
            fields[index] = fields[index] with
            {
                SuggestedValue = value,
                Candidates = [origin with { Value = value }]
            };
            derivedNames.Add(name);
        }
    }

    private static InstructionDraft CreateInstructionDraft(
        IReadOnlyList<InstructionReviewField> fields,
        string principalCode)
    {
        var values = fields.ToDictionary(
            field => field.Name,
            field => field.SuggestedValue,
            StringComparer.Ordinal);
        return new(
            principalCode,
            InstructionFieldEngine.TypedString(values["Claimant name"], 300),
            InstructionFieldEngine.TypedString(values["Claim number"], 100),
            InstructionFieldEngine.NormalizeRegistration(values["Vehicle registration"]),
            InstructionFieldEngine.TypedString(values["Vehicle make"], 100),
            InstructionFieldEngine.TypedString(values["Vehicle model"], 100),
            InstructionFieldEngine.ParseMileage(values["Vehicle mileage"]),
            InstructionFieldEngine.TypedString(values["Accident circumstances"], 2000),
            InstructionFieldEngine.ParseDate(values["Date of incident"]),
            InstructionFieldEngine.ParseDate(values["Instruction date"]),
            InstructionFieldEngine.TypedString(values["Inspection address"], 1000),
            InstructionFieldEngine.ParseDate(values["Inspection date"]));
    }
}
