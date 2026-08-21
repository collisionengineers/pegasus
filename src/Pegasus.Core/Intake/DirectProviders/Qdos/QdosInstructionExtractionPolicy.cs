using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

public sealed class QdosInstructionExtractionPolicy(
    IIntakeTriageMatcher? triageMatcher = null) : IInstructionExtractionPolicy
{
    public const string Key = "qdos_instruction";
    public const int Version = 4;
    public const string SupportedPrincipalCode = "QDOS";
    private readonly IIntakeTriageMatcher triageMatcher =
        triageMatcher ?? new NoAcceptedIntakeTriageMatcher();

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
        new("Instruction date", ["Instruction Date", "Date of Instruction"],
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate),
        new("Inspection address", ["Inspection Address", "Vehicle Location", "Inspection Location"]),
        new(
            "Inspection date",
            ["Inspection Date", "Date of Inspection", "Inspection Deadline", "Due By"],
            IsRequired: false,
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate),
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

    private static readonly InstructionFieldEngine.FieldDefinition[] FieldDefinitions =
        [.. BareFieldDefinitions.Select(definition =>
            definition with { GuardedPrefixes = ThirdPartyRowPrefixes })];

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
            processedAtUtc);
        fields = DeriveVehicleFields(fields, out var derivedNames);
        missingFields = missingFields.Where(name => !derivedNames.Contains(name)).ToArray();
        evidence.AddRange(fieldEvidence);
        var draft = CreateInstructionDraft(fields, principalContext.PrincipalCode);
        var triageMatches = triageMatcher.Match(readResult, draft);
        ArgumentNullException.ThrowIfNull(triageMatches);
        foreach (var match in triageMatches)
        {
            ValidateTriageMatch(match);
            evidence.Add(new(
                match.Source,
                IntakeEvidenceStrength.Strong,
                IntakeEvidenceFinding.AcceptedTriageMatch,
                match.Signal.Trim(),
                match.Detail.Trim(),
                match.MatcherKey.Trim(),
                match.MatcherVersion));
        }
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
            if (!IsReportFragment(fragment)
                && CircumstancesParagraph(fragment) is { } circumstances)
            {
                extended.Add(circumstances);
            }
        }
        foreach (var fragment in readResult.Content)
        {
            if (IsReportFragment(fragment))
            {
                extended.AddRange(ReportFacts(fragment));
            }
        }

        return WithSubjectFacts(readResult, extended);
    }

    /// <summary>
    /// Every report document in the corpus names itself a report in its
    /// retained file name (Bodyshopreport…, EngineersReport…); the
    /// instruction letters never do. The fragment's source label carries the
    /// file name.
    /// </summary>
    private static bool IsReportFragment(IntakeContentFragment fragment) =>
        fragment.SourceLabel.Contains("report", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The bodyshop report's own grammar, rewritten as labelled lines the
    /// field definitions already read. The report's "Vehicle:" line is cut at
    /// its neighbouring column labels; a "Speedo:" line contributes only when
    /// it actually carries digits ("Speedo: Miles" carries none). Appended
    /// after all content, so the instruction letter always outranks.
    /// </summary>
    private static IEnumerable<IntakeContentFragment> ReportFacts(
        IntakeContentFragment fragment)
    {
        foreach (var rawLine in SplitLines(fragment.Text))
        {
            var vehicle = Regex.Match(
                rawLine,
                @"(?i)^vehicle\s*:\s*(?<value>.+)$",
                RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100));
            if (vehicle.Success)
            {
                var value = Regex.Replace(
                        vehicle.Groups["value"].Value,
                        @"(?i)\s*(?:colour|speedo|reg\s*no|reg)\s*:.*$",
                        string.Empty,
                        RegexOptions.CultureInvariant)
                    .Trim();
                if (value.Length > 0)
                {
                    yield return new(
                        fragment.Source,
                        fragment.SourceLabel,
                        $"Our Client's Vehicle: {value}");
                }
            }

            var speedo = Regex.Match(
                rawLine,
                @"(?i)^speedo\s*:\s*(?<value>.*\d.*)$",
                RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100));
            if (speedo.Success)
            {
                yield return new(
                    fragment.Source,
                    fragment.SourceLabel,
                    $"Vehicle Mileage: {speedo.Groups["value"].Value.Trim()}");
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
        var prompt = Array.FindIndex(lines, line =>
            Regex.IsMatch(
                line,
                @"(?i)\bcircumstances\s*\?\s*$",
                RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100)));
        if (prompt < 0)
        {
            return null;
        }

        var paragraph = new List<string>();
        foreach (var line in lines.Skip(prompt + 1))
        {
            if (line.Length == 0
                || Regex.IsMatch(
                    line,
                    @"(?i)^(?:damage area|pre-existing damage|tp |if you need)",
                    RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(100)))
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
        var reference = Regex.Match(
            subject, @"\bOur Ref[:.]?\s+([A-Za-z0-9_/-]+)", RegexOptions.IgnoreCase);
        if (reference.Success)
        {
            lines.Add($"Our Ref: {reference.Groups[1].Value.TrimEnd(',', ')', '.')}");
        }

        var incident = Regex.Match(
            subject, @"\bRTA on\s+(\d{1,2})[_/.-](\d{1,2})[_/.-](\d{4})", RegexOptions.IgnoreCase);
        if (incident.Success)
        {
            lines.Add(
                $"Date of Accident: {incident.Groups[1].Value}/{incident.Groups[2].Value}/{incident.Groups[3].Value}");
        }

        var client = Regex.Match(
            subject,
            @"\b(?:Client[:.]?\s+)?((?:Mr|Mrs|Ms|Miss|Dr|Mx)\.?\s+[A-Z][A-Za-z'-]+(?:\s+[A-Z][A-Za-z'-]+){1,3})",
            RegexOptions.None);
        if (client.Success)
        {
            lines.Add($"Our Client: {client.Groups[1].Value.Trim().TrimEnd(',', ')', '.')}");
        }

        var vehicle = Regex.Match(
            subject, @"\bVehicle[:.]?\s+([^,()]+)", RegexOptions.IgnoreCase);
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

    private static void ValidateTriageMatch(IntakeTriageMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentException.ThrowIfNullOrWhiteSpace(match.Signal);
        ArgumentException.ThrowIfNullOrWhiteSpace(match.Detail);
        ArgumentException.ThrowIfNullOrWhiteSpace(match.MatcherKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(match.MatcherVersion);
    }

}
