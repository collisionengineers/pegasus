using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

// `partial` because this policy owns generated regexes; the triage-matcher
// constructor parameter is gone -- INTK-033 replaced that matcher with
// classification-derived evidence, and nothing here reads it.
public sealed partial class QdosInstructionExtractionPolicy
    : IInstructionExtractionPolicy, IInstructionDocumentProfile, IInstructionFieldRoles
{
    public const string Key = "qdos_instruction";
    // ENG-015 changed three extraction rules -- the bare `Date` label, the
    // labelled damage-area synthesis, and inspection-date fragment precedence.
    // The version is persisted as each extracted fact's provenance, so facts
    // read before and after must stay distinguishable for audit and
    // re-evaluation. Bumped for the same reason as v3 (letter shapes),
    // v4 (INTK-025), v5 (INTK-028) and v6 (INTK-033).
    //
    // v8 (INTK-060 C03) changed three more: the combined vehicle description
    // is no longer SPLIT into make and model, the labelled damage area is no
    // longer appended to the accident circumstances, and the letter's own
    // party, damage, third-party, repairer and requested-work blocks are read
    // as their own role-bearing fields.
    public const int Version = 8;
    public const string SupportedPrincipalCode = "QDOS";

    public string PrincipalCode => SupportedPrincipalCode;

    /// <summary>
    /// The document-side profile key, kept separate from <see cref="Key"/>: the
    /// extraction grammar and the "is this a QDOS instruction at all" signature
    /// change for different reasons and are versioned apart.
    /// </summary>
    public const string DocumentProfileKeyValue = "qdos_instruction_document";

    public const int DocumentProfileVersionValue = 1;

    public string DocumentProfileKey => DocumentProfileKeyValue;

    public int DocumentProfileVersion => DocumentProfileVersionValue;

    /// <summary>
    /// Transcribed from the `collision-profile-qdos` fingerprint in
    /// <c>reference/workproviders-and-repairers/principal-identification-corpus.v1.json</c>
    /// (register: Closed; the runtime never loads that file, so the signals are
    /// carried here as source). Only the required and negative signals take
    /// part — the fingerprint's optional signals exist to describe a template,
    /// not to rank one profile above another, and the selector has no ranking
    /// to give them. The two negative signals are the assessor firms whose
    /// letters share QDOS's labels but are not QDOS instructions.
    /// </summary>
    public static readonly InstructionDocumentSignature DocumentSignature = new(
        InstructionDocumentSignature.InstructionRole,
        ["QDOS", "Registration:", "Our Client’s Vehicle:"],
        ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"]);

    public InstructionDocumentSignature Signature => DocumentSignature;

    // The letters' third-party rows ("TP Vehicle:", "TP Registration:",
    // "TP Representative Name:") must never feed the claimant's fields.
    // QDOS grammar, supplied to the neutral engine per definition.
    private static readonly string[] ThirdPartyRowPrefixes = ["TP"];

    /// <summary>
    /// The whole-line row-skip guard, built once from the same prefix list
    /// every definition is guarded against. A prefix added to
    /// <see cref="ThirdPartyRowPrefixes"/> therefore extends both the
    /// per-field guard and this skip; a pattern hardcoded here would silently
    /// extend only the first.
    /// </summary>
    private static readonly Regex[] ThirdPartyRowRegexes =
        [.. ThirdPartyRowPrefixes.Select(prefix => new Regex(
            $@"(?i)^{Regex.Escape(prefix)}\b",
            RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100)))];

    /// <summary>
    /// The roles the letters keep apart, spelled once. The claimant, the
    /// third party, the repairer, the principal's own reference and the
    /// instruction itself are separate roles, and a value read under one of
    /// them never becomes a fact about another.
    /// </summary>
    private const string ClaimantRole = "claimant";

    private const string PrincipalRole = "principal";

    private const string InstructionRole = "instruction";

    private const string RepairerRole = "repairer";

    private const string ThirdPartyRole = "third-party";

    private static readonly InstructionFieldEngine.FieldDefinition[] BareFieldDefinitions =
    [
        new("Claimant name", ["Claimant Name", "Claimant", "Our Client", "Client Name"],
            PartyRole: ClaimantRole),
        new(
            "Claim number",
            ["Claim Number", "Claim No", "Claim Reference", "Claim Ref", "Our Reference", "Our Ref"],
            PartyRole: PrincipalRole,
            ReferenceRole: PrincipalRole),
        new(
            "Vehicle registration",
            [
                "Vehicle Registration", "Registration Number", "Registration No",
                "Vehicle Reg No", "Vehicle Reg", "Registration", "Reg No", "VRM", "VRN"
            ],
            IsValidTyped: InstructionFieldEngine.IsUkRegistration,
            CanonicalValue: InstructionFieldEngine.NormalizeRegistration,
            PartyRole: ClaimantRole,
            AllowsSoleUnlabelledRegistration: true),
        // Make and model are read ONLY from their own labels. The letters
        // print one combined description instead, and splitting that on token
        // position or a short make list is the guess the extraction invariants
        // forbid; the whole description survives as its own field below.
        new("Vehicle make", ["Vehicle Make", "Make"],
            AcceptsValue: InstructionFieldEngine.IsPlausibleVehicleMakeModel,
            PartyRole: ClaimantRole),
        new("Vehicle model", ["Vehicle Model", "Model"],
            AcceptsValue: InstructionFieldEngine.IsPlausibleVehicleMakeModel,
            PartyRole: ClaimantRole),
        new("Vehicle mileage", ["Vehicle Mileage", "Mileage"],
            IsValidTyped: value => InstructionFieldEngine.ParseMileage(value) is not null,
            PartyRole: ClaimantRole),
        new("Accident circumstances", ["Accident Circumstances", "Circumstances"],
            PartyRole: ClaimantRole),
        new(
            "Date of incident",
            ["Date of Incident", "Incident Date", "Accident Date", "Date of Accident", "Accident on"],
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate,
            PartyRole: ClaimantRole),
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
        //   the guarded prefixes can reject it - the value cannot.
        new(
            "Instruction date",
            ["Instruction Date", "Date of Instruction", "Date"],
            AcceptsValue: value => InstructionFieldEngine.ParseDate(value) is not null,
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate,
            GuardedPrefixes: ["Accident", "Incident", "Inspection", "Issue", "Report", "Due"],
            PartyRole: InstructionRole,
            DefaultsToProcessedDate: true),
        new("Inspection address", ["Inspection Address", "Vehicle Location", "Inspection Location"],
            PartyRole: InstructionRole),
        // An appended engineer's report states when the vehicle was actually
        // seen; the instruction can only propose a date. So when both carry
        // one, the later fragment wins - the reverse of every other field
        // (ENG-015).
        new(
            "Inspection date",
            ["Inspection Date", "Date of Inspection", "Inspection Deadline", "Due By"],
            IsRequired: false,
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate,
            PrefersLatestFragment: true,
            PartyRole: InstructionRole),
        // The real correspondence writes the vehicle as one description line
        // ("Our Client's Vehicle: PEUGEOT RCZ GT THP 156"). The bare word
        // "Vehicle" is deliberately not a label here - it collides with the
        // registration and location labels.
        new(
            "Vehicle description",
            [
                "Our Client's Vehicle", "Client's Vehicle", "Claimant's Vehicle",
                "Client Vehicle", "Vehicle Description"
            ],
            IsRequired: false,
            AcceptsValue: InstructionFieldEngine.IsPlausibleVehicleMakeModel,
            PartyRole: ClaimantRole),
        // The letter's own damage, pre-existing damage and driveability rows.
        // Separate fields, separate roles, and never part of the accident
        // circumstances: what a vehicle looks like now is not how it came to
        // look that way, and the two were being concatenated (INTK-060 C03).
        // The block reader below rewrites the whole wrapped damage block as
        // one row and appends it after the raw content, so the LATEST fragment
        // wins here: the line scan sees only the block's first physical row,
        // and half a sentence is not the damage description.
        new(DamageAreaField, [DamageAreaField],
            IsRequired: false,
            PrefersLatestFragment: true,
            PartyRole: ClaimantRole),
        new("Pre-existing damage", ["Pre-existing Damage", "Pre Existing Damage"],
            IsRequired: false,
            PartyRole: ClaimantRole),
        new("Vehicle status", ["Vehicle Status", "Driveable", "Vehicle Driveable"],
            IsRequired: false,
            PartyRole: ClaimantRole),
        // The third party's own rows. Read, so staff can see them; kept under
        // the third-party role, so nothing downstream can read one as the
        // claimant's vehicle, registration or representative.
        new("Third-party vehicle", ["TP Vehicle", "Third Party Vehicle"],
            IsRequired: false,
            PartyRole: ThirdPartyRole),
        new("Third-party registration", ["TP Registration", "Third Party Registration"],
            IsRequired: false,
            PartyRole: ThirdPartyRole),
        new(
            "Third-party representative",
            ["TP Representative Name", "TP Representative", "Third Party Representative Name"],
            IsRequired: false,
            PartyRole: ThirdPartyRole),
        // The letter's party blocks, synthesized into labelled rows below
        // because the originals print them as columns of a flattened page.
        new(ClaimantAddressField, [ClaimantAddressField],
            IsRequired: false,
            PrefersLatestFragment: true,
            PartyRole: ClaimantRole),
        new(RepairerDetailsField, [RepairerDetailsField],
            IsRequired: false,
            PrefersLatestFragment: true,
            PartyRole: RepairerRole),
        new(RequestedWorkField, [RequestedWorkField],
            IsRequired: false,
            PartyRole: InstructionRole),
        // The claimant's own numbers, one field each, because the letters
        // print three rows and a case that shows one number cannot say which.
        // Each is guarded against the repairer block's bare "Tel:" row.
        new("Claimant home telephone", ["Home Tel"],
            IsRequired: false,
            PartyRole: ClaimantRole),
        new("Claimant work telephone", ["Work Tel"],
            IsRequired: false,
            PartyRole: ClaimantRole),
        new("Claimant mobile telephone", ["Mobile"],
            IsRequired: false,
            PartyRole: ClaimantRole),
        // The repairer's own contact rows. "Tel" is guarded against the
        // claimant's rows: "Home Tel: 07738011335" ends in the same three
        // letters, and without the guard the repairer field silently took the
        // claimant's number.
        new("Repairer telephone", ["Tel", "Telephone"],
            IsRequired: false,
            GuardedPrefixes: ["Home", "Work", "Mobile", "Repairer"],
            PartyRole: RepairerRole),
        // An address, or nothing. The originals mislabel this row - one of
        // them prints a telephone number under "Email:" - and a value that is
        // not an address is not this field's value.
        new("Repairer email", ["Email"],
            IsRequired: false,
            AcceptsValue: value => value.Contains('@', StringComparison.Ordinal),
            PartyRole: RepairerRole)
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
    /// Each field's role, taken from the definition that declares it rather
    /// than from a second list beside it: a role added to a definition is the
    /// role recorded with its candidates, and the two cannot drift.
    /// </summary>
    public IReadOnlyDictionary<string, InstructionFieldRole> FieldRoles { get; } =
        FieldDefinitions.ToDictionary(
            definition => definition.Name,
            definition => new InstructionFieldRole(definition.PartyRole, definition.ReferenceRole),
            StringComparer.Ordinal);

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
        fields = DeriveVehicleRegistration(fields, out var derivedNames);
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
            extended.AddRange(LetterBlocks(fragment));
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
            if (ThirdPartyRowRegexes.Any(regex => regex.IsMatch(rawLine)))
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
    /// The letter's own blocks, rewritten as the labelled rows the field
    /// definitions already read:
    ///
    /// <list type="bullet">
    /// <item>the labelled damage area, as its OWN field. It used to be
    /// appended to the accident circumstances under a second label
    /// (ENG-015); the extraction invariants keep damage, pre-existing damage
    /// and driveability separate from how the accident happened, and a
    /// reviewer reading one concatenated value cannot tell which half the
    /// document actually stated.</item>
    /// <item>the CLIENT DETAILS and REPAIRER DETAILS address blocks, which
    /// the originals print as columns of a flattened page.</item>
    /// <item>the notification heading, which is what the letter is asking
    /// for.</item>
    /// </list>
    ///
    /// Each carries its origin fragment's source, label and locator, so the
    /// page a value came from survives into the recorded candidate.
    /// </summary>
    private static IEnumerable<IntakeContentFragment> LetterBlocks(
        IntakeContentFragment fragment)
    {
        if (DamageArea(fragment) is { } damageArea)
        {
            yield return Labelled(fragment, DamageAreaField, damageArea.Replace('\n', ' '));
        }

        var lines = SplitLines(fragment.Text);
        var requestedWork = lines.FirstOrDefault(RequestedWorkHeadingRegex().IsMatch);
        if (requestedWork is not null)
        {
            yield return Labelled(fragment, RequestedWorkField, requestedWork.Trim());
        }

        if (PartyBlock(lines, ClientDetailsHeadingRegex()) is { } clientDetails)
        {
            yield return Labelled(fragment, ClaimantAddressField, clientDetails);
        }

        if (PartyBlock(lines, RepairerDetailsHeadingRegex()) is { } repairerDetails)
        {
            yield return Labelled(fragment, RepairerDetailsField, repairerDetails);
        }
    }

    private static IntakeContentFragment Labelled(
        IntakeContentFragment origin,
        string label,
        string value) =>
        new(origin.Source, origin.SourceLabel, $"{label}: {value}", origin.Locator);

    /// <summary>
    /// A party block: the rows printed beneath one of the letter's party
    /// headings, each cut where the page's next column begins, up to the row
    /// that starts the next block. The heading's own row carries nothing.
    ///
    /// The originals print the client's name as the block's first row and the
    /// address beneath it. The name is already its own field, so a first row
    /// that is only a person's name is left to it rather than repeated inside
    /// the address - and a row that is not a name is kept, because a company
    /// claimant's address genuinely begins with its name.
    /// </summary>
    private static string? PartyBlock(string[] lines, Regex heading)
    {
        var index = Array.FindIndex(lines, line => heading.IsMatch(line));
        if (index < 0)
        {
            return null;
        }

        var block = new List<string>();
        foreach (var line in lines.Skip(index + 1))
        {
            if (line.Length == 0 || PartyBlockStopRegex().IsMatch(line))
            {
                break;
            }

            var cut = ColumnCutRegex().Split(line, 2)[0].Trim();
            // A row that is only the next column's label, left behind by the
            // cut, states nothing about this party and is skipped rather than
            // ending the block: the address rows continue beneath it.
            if (cut.Length > 0 && !cut.EndsWith(':'))
            {
                block.Add(cut);
            }
        }

        if (block.Count > 0 && PersonalNameRegex().IsMatch(block[0]))
        {
            block.RemoveAt(0);
        }

        return block.Count == 0 ? null : string.Join(", ", block);
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

    /// <summary>
    /// The synthesized labels. Each is both the label written into the
    /// rewritten row and the field definition's own label, so the two can
    /// never be spelled differently.
    /// </summary>
    private const string DamageAreaField = "Damage area";

    private const string ClaimantAddressField = "Claimant address";

    private const string RepairerDetailsField = "Repairer details";

    private const string RequestedWorkField = "Requested work";

    [GeneratedRegex(ReportColumnCutPattern, RegexOptions.CultureInvariant, 100)]
    private static partial Regex ReportColumnCutRegex();

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

    /// <summary>
    /// The two notification headings the letters carry. This is what the
    /// principal is asking for; it is requested work, never a finding, an
    /// accepted outcome or an inspection that happened.
    /// </summary>
    [GeneratedRegex(
        @"^\s*(?:audit report notification|engineer notification\b.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        100)]
    private static partial Regex RequestedWorkHeadingRegex();

    [GeneratedRegex(
        @"^\s*client details\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        100)]
    private static partial Regex ClientDetailsHeadingRegex();

    [GeneratedRegex(
        @"^\s*repairer details\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        100)]
    private static partial Regex RepairerDetailsHeadingRegex();

    /// <summary>
    /// Where a party block ends: the next heading, the contact rows the
    /// engine reads as their own fields, or the estimate row.
    /// </summary>
    [GeneratedRegex(
        @"(?i)^(?:client details|repairer details|vehicle details|home tel|work tel|mobile"
        + @"|tel|fax|email|engineer to estimate|if you need|yours\b)",
        RegexOptions.CultureInvariant,
        100)]
    private static partial Regex PartyBlockStopRegex();

    /// <summary>
    /// The column boundary a flattened page leaves behind: a run of two or
    /// more spaces, a tab or a pipe. A block row is cut here so the next
    /// column's text never joins this party's address.
    /// </summary>
    [GeneratedRegex(@"[\t|]|\s{2,}", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ColumnCutRegex();

    /// <summary>
    /// A row that is only a person's name, with a title: the shape the party
    /// blocks print above the address. Deliberately narrow - anything else,
    /// a company name included, is kept as part of the address.
    /// </summary>
    [GeneratedRegex(
        @"^(?:Mr|Mrs|Ms|Miss|Dr|Mx)\.?\s+[A-Z][A-Za-z'-]+(?:\s+[A-Z][A-Za-z'-]+){0,3}$",
        RegexOptions.CultureInvariant,
        100)]
    private static partial Regex PersonalNameRegex();

    /// <summary>
    /// Fills an empty registration field from a combined vehicle description
    /// that ENDS in a valid registration ("PEUGEOT RCZ GT THP 156 LK17 NHT"),
    /// carrying the description candidate's own provenance so the acceptance
    /// write still names a real source.
    ///
    /// Make and model are deliberately NOT derived here any more. Splitting a
    /// description on token position and a short list of two-word makes is a
    /// guess, the extraction invariants forbid exactly that guess, and the
    /// independently labelled corpus records the description as one value in
    /// every sample. The whole description survives as its own field; a make
    /// or model appears only where the letter labels one (INTK-060 C03).
    /// </summary>
    private static IReadOnlyList<InstructionReviewField> DeriveVehicleRegistration(
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
        }
        else if (tokens.Count >= 1
            && InstructionFieldEngine.IsUkRegistration(tokens[^1]))
        {
            registration = tokens[^1];
        }

        if (registration is null)
        {
            return fields;
        }

        var updated = fields.ToList();
        var index = updated.FindIndex(field => field.Name == "Vehicle registration");
        if (index < 0
            || updated[index].HasConflict
            || !string.IsNullOrWhiteSpace(updated[index].SuggestedValue))
        {
            return fields;
        }

        updated[index] = updated[index] with
        {
            SuggestedValue = registration,
            Candidates = [origin with { Value = registration }]
        };
        derivedNames.Add("Vehicle registration");
        return updated;
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
