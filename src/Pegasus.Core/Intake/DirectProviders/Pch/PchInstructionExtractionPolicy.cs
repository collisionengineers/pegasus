using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

/// <summary>
/// Reads a Performance Car Hire / Parkhouse (PCH) instruction: rank 2 by
/// observed volume, and review-only — this policy proposes candidates and
/// allocates nothing. `partial` because it owns generated regexes.
///
/// PCH prints two documents that read alike and mean different things: an
/// audit request against an engineer's report already in hand, and a
/// credit-repair instruction. It also prints roles that are routinely the same
/// person and must never be assumed to be: the policyholder and the driver
/// (sample 03 names two different people who share a surname), its own claim
/// number and the claimant's insurer policy number, and the hire company that
/// supplied a replacement vehicle — Connexus in three of the five recorded
/// originals — which is neither the principal nor the repairer.
///
/// Nothing here activates a route. The pch-ltd.com sender evidence in the
/// corpus is route identity, and a document profile is not permission to
/// allocate: `ProcessIntake` keeps automatic allocation for QDOS alone.
/// </summary>
public sealed partial class PchInstructionExtractionPolicy
    : IInstructionExtractionPolicy, IInstructionDocumentProfile, IInstructionFieldRoles
{
    public const string Key = "pch_instruction";

    public const int Version = 1;

    public const string SupportedPrincipalCode = "PCH";

    public string PrincipalCode => SupportedPrincipalCode;

    /// <summary>
    /// The document-side profile key, kept separate from <see cref="Key"/> for
    /// the same reason QDOS keeps them apart: the extraction grammar and the
    /// "is this a PCH instruction at all" signature change for different
    /// reasons and are versioned apart.
    /// </summary>
    public const string DocumentProfileKeyValue = "pch_instruction_document";

    public const int DocumentProfileVersionValue = 1;

    public string DocumentProfileKey => DocumentProfileKeyValue;

    public int DocumentProfileVersion => DocumentProfileVersionValue;

    /// <summary>
    /// What every accepted PCH template shares, and nothing more. The two
    /// fingerprints the corpus records for this principal —
    /// `collision-profile-pch-performance` and `collision-profile-pch-lawshield`
    /// in <c>reference/workproviders-and-repairers/principal-identification-corpus.v1.json</c>
    /// — differ only in their brand signal and agree on these two labels and
    /// these two negative signals. So the labels are the profile's signature
    /// and the brand signals are its <see cref="Variants"/>: no signal is
    /// invented here, and the specificity comes from a recorded variant rather
    /// than from a phrase nobody accepted.
    ///
    /// The negative signals are the assessor firms whose letters share these
    /// labels. Note that they are the FULL firm names: four of the five
    /// recorded originals carry the heading "URGENT NEW INSTRUCTION (Connexus
    /// Audit Report)", and a negative check on the word "Connexus" alone would
    /// reject every one of them.
    /// </summary>
    public static readonly InstructionDocumentSignature DocumentSignature = new(
        InstructionDocumentSignature.InstructionRole,
        ["Registration No:", "Vehicle Make:"],
        ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"]);

    public InstructionDocumentSignature Signature => DocumentSignature;

    public const string PerformanceVariantKey = "collision-profile-pch-performance";

    public const string LawshieldVariantKey = "collision-profile-pch-lawshield";

    /// <summary>
    /// The two accepted template signatures, transcribed from the corpus
    /// fingerprints. Both are recorded evidence about a template; neither is
    /// an identity to merge.
    ///
    /// They are NOT mutually exclusive in the recorded originals: sample 01
    /// carries only the Performance footer, and samples 02 to 05 carry both
    /// ("Performance Car Hire Ltd is an appointed representative of Lawshield
    /// UK Ltd" prints both firm names in one sentence). Which template was
    /// used is therefore genuinely ambiguous for four of the five, and it is
    /// recorded as ambiguous. WHO the principal is never was in doubt, which
    /// is why these are variants of one profile and not two profiles.
    ///
    /// The Everywhen variant the method file names has no accepted signature
    /// and no local original, so it is deliberately absent: an unproved
    /// variant matches nothing rather than borrowing this one's grammar.
    /// </summary>
    public static readonly IReadOnlyList<InstructionTemplateVariant> TemplateVariants =
    [
        new(
            PerformanceVariantKey,
            new(
                InstructionDocumentSignature.InstructionRole,
                ["Performance Car Hire", "Registration No:", "Vehicle Make:"],
                ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"])),
        new(
            LawshieldVariantKey,
            new(
                InstructionDocumentSignature.InstructionRole,
                ["Lawshield", "Registration No:", "Vehicle Make:"],
                ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"]))
    ];

    public IReadOnlyList<InstructionTemplateVariant> Variants => TemplateVariants;

    private const string ClaimantRole = "claimant";

    private const string DriverRole = "driver";

    private const string PrincipalRole = "principal";

    private const string InsurerRole = "insurer";

    private const string InstructionRole = "instruction";

    private const string RepairerRole = "repairer";

    private const string HireProviderRole = "hire-provider";

    private const string DeadlineRole = "deadline";

    /// <summary>
    /// The synthesized labels: both the label written into a rewritten row and
    /// the definition's own label, so the two cannot be spelled differently.
    /// None of them is a phrase the originals print, which is what keeps the
    /// block reading below from competing with the line scan over one row.
    /// </summary>
    private const string ClaimantAddressField = "Claimant address";

    private const string RequestedWorkField = "Requested work";

    private const string ReportDeadlineField = "Report deadline";

    private static readonly InstructionFieldEngine.FieldDefinition[] FieldDefinitions =
    [
        // The policyholder is the claimant. The driver is a separate labelled
        // role that happens to be the same person in three of the five
        // originals and a different one in the fourth, so it is read as itself
        // and never offered as the claimant's name.
        new("Claimant name", ["Policyholder Name"],
            PartyRole: ClaimantRole),
        new("Driver name", ["Driver"],
            IsRequired: false,
            PartyRole: DriverRole),
        // PCH's own claim number. The claimant's insurer policy and claim
        // numbers are printed a few rows below and are a different party's
        // reference; the legacy mapping that treated them as spellings of one
        // field is exactly what these three definitions prevent.
        new("Claim number", ["Claim Number"],
            PartyRole: PrincipalRole,
            ReferenceRole: PrincipalRole),
        new("Insurer policy number", ["Insurer Policy No", "Insurer Policy Number"],
            IsRequired: false,
            PartyRole: InsurerRole,
            ReferenceRole: "insurer-policy"),
        new("Insurer claim number", ["Insurer Claim No", "Insurer Claim Number"],
            IsRequired: false,
            PartyRole: InsurerRole,
            ReferenceRole: "insurer-claim"),
        new(
            "Vehicle registration",
            ["Registration No", "Registration Number", "Registration", "VRM"],
            IsValidTyped: InstructionFieldEngine.IsUkRegistration,
            CanonicalValue: InstructionFieldEngine.NormalizeRegistration,
            PartyRole: ClaimantRole),
        // The template labels one combined string "Vehicle Make:" ("VOLVO
        // Xc90 r-design t8 phev awd") and carries no Model label at all. The
        // printed value is kept whole under the label the document used:
        // splitting it on token position is the guess the invariants forbid,
        // and a model appears only where a template labels one.
        new("Vehicle make", ["Vehicle Make", "Make"],
            AcceptsValue: InstructionFieldEngine.IsPlausibleVehicleMakeModel,
            PartyRole: ClaimantRole),
        new("Vehicle model", ["Vehicle Model", "Model"],
            IsRequired: false,
            AcceptsValue: InstructionFieldEngine.IsPlausibleVehicleMakeModel,
            PartyRole: ClaimantRole),
        new("Vehicle mileage", ["Mileage"],
            IsRequired: false,
            IsValidTyped: value => InstructionFieldEngine.ParseMileage(value) is not null,
            PartyRole: ClaimantRole),
        new("Date of incident", ["Incident date", "Incident Date", "Date of Incident"],
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate,
            PartyRole: ClaimantRole),
        // The instruction message's own "Date:" header, and nothing else. It
        // is guarded against the two other date rows the template prints: the
        // incident date above it and the replacement-vehicle "Hire Out Date:"
        // below it, which is when a hire car was supplied and is neither an
        // incident nor an instruction.
        //
        // There is deliberately no clock default: a PCH instruction that
        // states no date has no instruction date, and today is not an
        // extracted fact.
        new("Instruction date", ["Date"],
            AcceptsValue: value => InstructionFieldEngine.ParseDate(value) is not null,
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate,
            GuardedPrefixes: ["Incident", "Out", "Hire", "Inspection", "Report", "Due", "Issue"],
            PartyRole: InstructionRole),
        new("Accident circumstances", ["Incident Circumstances", "Accident Circumstances"],
            PartyRole: ClaimantRole),
        // Damage, driveability and pre-existing damage are four separate rows
        // in this template and stay four separate fields: what the vehicle
        // looks like is not how it came to look that way.
        new("Damage area", ["Area of damage"],
            IsRequired: false,
            PartyRole: ClaimantRole),
        new("Damage description", ["Description of damage"],
            IsRequired: false,
            PartyRole: ClaimantRole),
        new("Damage nature", ["Nature of Damage"],
            IsRequired: false,
            PartyRole: ClaimantRole),
        new("Pre-existing damage", ["Pre Existing Damage", "Pre-existing Damage"],
            IsRequired: false,
            PartyRole: ClaimantRole),
        new("Vehicle status", ["Vehicle Driveable", "Driveable"],
            IsRequired: false,
            PartyRole: ClaimantRole),
        // Explicit, and only explicit: "is VAT registered" and "isn't VAT
        // registered" are both printed by this template, and an absent row is
        // unavailable rather than the legacy hard-coded No.
        new("VAT status", ["Policyholder VAT Status", "VAT Status"],
            IsRequired: false,
            PartyRole: ClaimantRole),
        // "Current Vehicle Location:" is where the vehicle is, and only an
        // address will do. One original prints "in use" there — a statement
        // about whether the car is being driven, not a place — and the
        // supplier footer a few lines below is a real address that is not this
        // one. An inspection address is withheld unless the row carries a
        // postcode.
        new("Inspection address", ["Current Vehicle Location", "Inspection Address"],
            IsRequired: false,
            AcceptsValue: ContainsPostcode,
            PartyRole: InstructionRole),
        // No recorded original carries a repairer, storage or hire-rates
        // block, so these labels are the ones the template would print and
        // the fields stay unavailable until an original carries one. An
        // unavailable repairer is not the hire company below and is never the
        // footer address.
        new("Repairer name", ["Repairer", "Repairer Name", "Bodyshop"],
            IsRequired: false,
            PartyRole: RepairerRole),
        new("Storage location", ["Storage Location", "Storage Address"],
            IsRequired: false,
            PartyRole: RepairerRole),
        // The replacement-hire rows. Their own role: the company that supplied
        // a hire car is not the principal, not the repairer and not the
        // claimant's insurer, and the hire-out date is not the incident date.
        new("Hire supplied", ["Hire Supplied"],
            IsRequired: false,
            PartyRole: HireProviderRole),
        new("Hire company", ["Hire Company"],
            IsRequired: false,
            PartyRole: HireProviderRole),
        new("Hire out date", ["Hire Out Date"],
            IsRequired: false,
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate,
            PartyRole: HireProviderRole),
        new("Claimant home telephone", ["Tel Home"],
            IsRequired: false,
            PartyRole: ClaimantRole),
        new("Claimant mobile telephone", ["Tel Mobile"],
            IsRequired: false,
            PartyRole: ClaimantRole),
        // Synthesized below from the template's own blocks.
        new(ClaimantAddressField, [ClaimantAddressField],
            IsRequired: false,
            PartyRole: ClaimantRole),
        new(RequestedWorkField, [RequestedWorkField],
            IsRequired: false,
            PartyRole: InstructionRole),
        // A turnaround the principal is asking for. It is a deadline under its
        // own role, and it never becomes an inspection date.
        new(ReportDeadlineField, [ReportDeadlineField],
            IsRequired: false,
            PartyRole: DeadlineRole)
    ];

    private static readonly InstructionFieldEngine.LabelRegexCache FieldRegexCache =
        new(FieldDefinitions);

    public IReadOnlyDictionary<string, InstructionFieldRole> FieldRoles { get; } =
        FieldDefinitions.ToDictionary(
            definition => definition.Name,
            definition => new InstructionFieldRole(definition.PartyRole, definition.ReferenceRole),
            StringComparer.Ordinal);

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
                "The PCH extraction policy accepts only fully readable, complete reader results.",
                nameof(readResult));
        }
        if (!string.Equals(
                principalContext.PrincipalCode,
                SupportedPrincipalCode,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The established principal is not supported by the PCH extraction policy.",
                nameof(principalContext));
        }

        var evidence = new List<IntakeEvidence>
        {
            new(
                IntakeEvidenceSource.Sender,
                IntakeEvidenceStrength.Strong,
                IntakeEvidenceFinding.SupportsPrincipal,
                "established-principal",
                $"Principal PCH was established by {principalContext.PolicyKey} v{principalContext.PolicyVersion}.")
        };
        var (fields, missingFields, fieldEvidence) = InstructionFieldEngine.ExtractFields(
            WithSynthesizedBlocks(readResult.Content),
            FieldDefinitions,
            FieldRegexCache,
            processedAtUtc);
        evidence.AddRange(fieldEvidence);
        if (readResult.RequiresOcr)
        {
            evidence.Add(new(
                IntakeEvidenceSource.DocumentContent,
                IntakeEvidenceStrength.Strong,
                IntakeEvidenceFinding.Information,
                "additional-scanned-content",
                "A PCH draft was extracted from readable content; additional scanned content still requires review."));
        }

        return new(
            InstructionPolicyApplicability.Applicable,
            evidence,
            fields,
            CreateInstructionDraft(fields),
            missingFields,
            Key,
            Version);
    }

    /// <summary>
    /// The template's own multi-row and heading blocks, rewritten as the
    /// labelled rows the field definitions read, and appended after the
    /// original content so nothing the document states is displaced.
    ///
    /// Each carries its origin fragment's source, label and locator, so the
    /// page or message part a value came from survives into the recorded
    /// candidate.
    /// </summary>
    private static List<IntakeContentFragment> WithSynthesizedBlocks(
        IReadOnlyList<IntakeContentFragment> content)
    {
        var extended = new List<IntakeContentFragment>(content);
        foreach (var fragment in content)
        {
            var lines = SplitLines(fragment.Text);
            if (AddressBlock(lines) is { } address)
            {
                extended.Add(Labelled(fragment, ClaimantAddressField, address));
            }

            var requestedWork = lines.FirstOrDefault(line =>
                RequestedWorkHeadingRegex().IsMatch(line));
            if (requestedWork is not null)
            {
                extended.Add(Labelled(fragment, RequestedWorkField, requestedWork.Trim()));
            }

            var deadline = lines.FirstOrDefault(line => ReportDeadlineRegex().IsMatch(line));
            if (deadline is not null)
            {
                extended.Add(Labelled(fragment, ReportDeadlineField, deadline.Trim()));
            }
        }

        return extended;
    }

    private static IntakeContentFragment Labelled(
        IntakeContentFragment origin,
        string label,
        string value) =>
        new(origin.Source, origin.SourceLabel, $"{label}: {value}", origin.Locator);

    /// <summary>
    /// The claimant's address: the value beside the "Address:" label plus the
    /// indented rows beneath it, which is how this template prints a
    /// multi-line address. The block ends at the first row that is not
    /// indented or that carries a label of its own — four rows in one original
    /// and five or six in the others, so a fixed row count would misread it.
    ///
    /// The rows are joined with ", " for the value the pipeline reads. The
    /// printed rows survive as the raw value, because normalization never
    /// destroys the source.
    /// </summary>
    private static string? AddressBlock(string[] lines)
    {
        var index = Array.FindIndex(lines, line => AddressLabelRegex().IsMatch(line));
        if (index < 0)
        {
            return null;
        }

        var block = new List<string>();
        var first = AddressLabelRegex().Replace(lines[index], string.Empty).Trim();
        if (first.Length > 0)
        {
            block.Add(first);
        }

        foreach (var line in lines.Skip(index + 1))
        {
            if (!IndentedContinuationRegex().IsMatch(line))
            {
                break;
            }

            block.Add(line.Trim());
        }

        return block.Count == 0 ? null : string.Join(", ", block);
    }

    /// <summary>
    /// Whether a value carries a UK postcode. This is the whole test for
    /// "is this row an address": the template prints a status phrase and a
    /// blank in that row far more often than it prints a place, and returning
    /// no inspection address is better than returning a plausible wrong one.
    /// </summary>
    private static bool ContainsPostcode(string value) => PostcodeRegex().IsMatch(value);

    private static string[] SplitLines(string text) => text
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace('\r', '\n')
        .Split('\n');

    private static InstructionDraft CreateInstructionDraft(
        IReadOnlyList<InstructionReviewField> fields)
    {
        var values = fields.ToDictionary(
            field => field.Name,
            field => field.SuggestedValue,
            StringComparer.Ordinal);
        return new(
            SupportedPrincipalCode,
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
            // Deliberately null. No recorded original states an appointed or
            // completed inspection, and the report deadline this template does
            // print is not one.
            null,
            null,
            InstructionFieldEngine.TypedString(values["VAT status"], 100),
            InstructionFieldEngine.TypedString(values[ClaimantAddressField], 1000),
            InstructionFieldEngine.TypedString(values["Claimant mobile telephone"], 100)
                ?? InstructionFieldEngine.TypedString(values["Claimant home telephone"], 100));
    }

    [GeneratedRegex(
        @"^\s*Address\s*[:]\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        100)]
    private static partial Regex AddressLabelRegex();

    /// <summary>
    /// A continuation row of an indented block: it starts with a tab or two or
    /// more spaces and then says something that is not itself a label.
    /// </summary>
    [GeneratedRegex(
        @"^(?:\t| {2,})\s*(?![A-Za-z][A-Za-z '/]{0,40}:)\S",
        RegexOptions.CultureInvariant,
        100)]
    private static partial Regex IndentedContinuationRegex();

    /// <summary>
    /// The instruction heading, which is what this principal is asking for.
    /// The recorded originals all print "URGENT NEW INSTRUCTION (Connexus
    /// Audit Report)"; the whole printed heading is kept rather than the
    /// parenthesis alone, so the audit request and a credit-repair request
    /// stay distinguishable by what the document said.
    /// </summary>
    [GeneratedRegex(
        @"^\s*(?:URGENT\s+)?NEW\s+INSTRUCTION\b.*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        100)]
    private static partial Regex RequestedWorkHeadingRegex();

    [GeneratedRegex(
        @"provide\s+full\s+report\s+within",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        100)]
    private static partial Regex ReportDeadlineRegex();

    /// <summary>
    /// A UK postcode in either spacing. Bounded deliberately: this decides
    /// whether a row is an address at all.
    /// </summary>
    [GeneratedRegex(
        @"\b[A-Z]{1,2}[0-9][A-Z0-9]?\s?[0-9][A-Z]{2}\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        100)]
    private static partial Regex PostcodeRegex();
}
