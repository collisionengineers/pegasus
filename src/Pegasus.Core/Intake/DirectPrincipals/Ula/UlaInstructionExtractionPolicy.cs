using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

/// <summary>
/// Reads United Legal Assistance's instruction: the Word 97 letter attached to
/// an e-mail from united-legal.co.uk, every one of which is a standalone Audit
/// (operator, 17 November 2026; R1138, R1145, R1155). The policy proposes
/// candidates and allocates nothing; which work the letter asks for is the
/// classification policy's answer, not this one's.
///
/// The letter is addressed to whichever assessor ULA instructs, so the
/// addressee block above the salutation is read for nothing: neither the
/// signature nor any field depends on it. The letter's date and its contact
/// person are not Case facts and are not read either.
/// </summary>
public sealed partial class UlaInstructionExtractionPolicy
    : IInstructionExtractionPolicy, IInstructionDocumentProfile, IInstructionFieldRoles
{
    public const string Key = "ula_instruction";
    public const int Version = 1;
    public const string SupportedPrincipalCode = "ULA";
    public const string DocumentProfileKeyValue = "ula_instruction_document";
    public const int DocumentProfileVersionValue = 1;

    /// <summary>
    /// The ULA letter signature (R1155): the sender domain the letter prints in
    /// its own contact block and the four labels of its claim block. The
    /// negative signals are the assessor firms whose letters share such labels,
    /// as PCH and OAK carry them.
    /// </summary>
    public static readonly InstructionDocumentSignature DocumentSignature = new(
        InstructionDocumentSignature.InstructionRole,
        ["united-legal.co.uk", "Our Reference:", "Our Client", "Vehicle:", "Accident:"],
        ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"]);

    // The synthesized labels below are written only into the rewritten rows
    // this policy appends, so none of them competes with a printed label.
    private const string ReferenceLabel = "Our Reference";
    private const string ClaimantLabel = "Our Client";
    private const string RegistrationLabel = "Client vehicle registration";
    private const string ModelLabel = "Client vehicle model";
    private const string IncidentDateLabel = "Accident date";
    private const string CircumstancesLabel = "Accident circumstances";
    private const string VatLabel = "VAT Registered";
    private const string RepairerNameField = "Repairer name";
    private const string RepairerAddressField = "Repairer address";

    private static readonly InstructionFieldEngine.FieldDefinition[] Definitions =
    [
        new("Claimant name", [ClaimantLabel], PartyRole: "claimant"),
        new("Claim reference", [ReferenceLabel], PartyRole: "principal", ReferenceRole: "principal"),
        new("Vehicle registration", [RegistrationLabel],
            IsValidTyped: InstructionFieldEngine.IsUkRegistration,
            CanonicalValue: InstructionFieldEngine.NormalizeRegistration,
            PartyRole: "claimant"),
        new("Vehicle model", [ModelLabel], IsRequired: false,
            AcceptsValue: InstructionFieldEngine.IsPlausibleVehicleMakeModel,
            PartyRole: "claimant"),
        new("Incident date", [IncidentDateLabel],
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate,
            PartyRole: "claimant"),
        new("Accident circumstances", [CircumstancesLabel], IsRequired: false, PartyRole: "claimant"),
        new("VAT status", [VatLabel], IsRequired: false, PartyRole: "claimant"),
        // The repairer ULA has instructed, which EstablishedIntakeRepairer
        // links to an active Repairer by name (R1027).
        new(RepairerNameField, [RepairerNameField], IsRequired: false, PartyRole: "repairer"),
        new(RepairerAddressField, [RepairerAddressField], IsRequired: false, PartyRole: "repairer")
    ];

    private static readonly InstructionFieldEngine.LabelRegexCache Cache = new(Definitions);

    public string PrincipalCode => SupportedPrincipalCode;
    public string DocumentProfileKey => DocumentProfileKeyValue;
    public int DocumentProfileVersion => DocumentProfileVersionValue;
    public InstructionDocumentSignature Signature => DocumentSignature;

    public IReadOnlyDictionary<string, InstructionFieldRole> FieldRoles { get; } = Definitions.ToDictionary(
        definition => definition.Name,
        definition => new InstructionFieldRole(definition.PartyRole, definition.ReferenceRole),
        StringComparer.Ordinal);

    public InstructionExtractionResult Extract(
        IntakeSourceReadResult readResult,
        InstructionExtractionTiming timing,
        EstablishedPrincipalContext principalContext)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        ArgumentNullException.ThrowIfNull(principalContext);
        if (readResult.Status != IntakeSourceReadStatus.Readable || readResult.IsIncomplete)
            throw new ArgumentException("ULA extraction requires complete readable content.", nameof(readResult));
        if (!string.Equals(principalContext.PrincipalCode, SupportedPrincipalCode, StringComparison.Ordinal))
            throw new ArgumentException("The established principal is not ULA.", nameof(principalContext));

        var scoped = readResult.Content.SelectMany(InstructionFields).ToArray();
        var (fields, missing, fieldEvidence) = InstructionFieldEngine.ExtractFields(
            scoped,
            Definitions,
            Cache,
            timing);
        var values = fields.ToDictionary(field => field.Name, field => field.SuggestedValue, StringComparer.Ordinal);
        var draft = new InstructionDraft(
            SupportedPrincipalCode,
            InstructionFieldEngine.TypedString(values["Claimant name"], 300),
            InstructionFieldEngine.TypedString(values["Claim reference"], 100),
            InstructionFieldEngine.NormalizeRegistration(values["Vehicle registration"]),
            null,
            InstructionFieldEngine.TypedString(values["Vehicle model"], 100),
            null,
            InstructionFieldEngine.TypedString(values["Accident circumstances"], 2000),
            InstructionFieldEngine.ParseDate(values["Incident date"]),
            null,
            null,
            null,
            InstructionFieldEngine.TypedString(values["VAT status"], 100),
            null,
            null);
        var evidence = new List<IntakeEvidence>(fieldEvidence)
        {
            new(
                IntakeEvidenceSource.Sender,
                IntakeEvidenceStrength.Strong,
                IntakeEvidenceFinding.SupportsPrincipal,
                "established-principal",
                $"Principal ULA was established by {principalContext.PolicyKey} v{principalContext.PolicyVersion}.")
        };
        return new(
            InstructionPolicyApplicability.Applicable,
            evidence,
            fields,
            draft,
            missing,
            Key,
            Version);
    }

    /// <summary>
    /// The letter's own rows, rewritten as the labelled rows the definitions
    /// read. A table cell or form field is left out: the Word main story
    /// already carries its text, and a cell is never line-scanned.
    /// </summary>
    private static IEnumerable<IntakeContentFragment> InstructionFields(IntakeContentFragment fragment)
    {
        if (fragment.Locator?.Kind is IntakeLocatorKind.TableCell or IntakeLocatorKind.FormField)
            yield break;

        var text = fragment.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        foreach (var (regex, label) in LineFields)
        {
            foreach (Match match in regex.Matches(text))
            {
                if (Labelled(fragment, label, match.Groups["value"].Value) is { } labelled)
                    yield return labelled;
            }
        }

        foreach (Match match in VehicleRegex().Matches(text))
        {
            if (SplitLeadingRegistration(Clean(match.Groups["value"].Value)) is not { } vehicle)
                continue;
            if (Labelled(fragment, RegistrationLabel, vehicle.Registration) is { } registration)
                yield return registration;
            if (vehicle.Model is not null && Labelled(fragment, ModelLabel, vehicle.Model) is { } model)
                yield return model;
        }

        foreach (var repairer in RepairerFields(fragment, text.Split('\n')))
            yield return repairer;
    }

    private static readonly (Regex Regex, string Label)[] LineFields =
    [
        (ReferenceRegex(), ReferenceLabel),
        (ClaimantRegex(), ClaimantLabel),
        (AccidentRegex(), IncidentDateLabel),
        (VatRegex(), VatLabel),
        (CircumstancesRegex(), CircumstancesLabel)
    ];

    /// <summary>
    /// The "Vehicle:" row prints the registration first and the vehicle after
    /// it ("FV25JOJ FORD TRANSIT COURIER TREND ECOBLUE"). A leading token, or
    /// a leading pair printed with a space, is the registration only when it
    /// has a current, prefix or suffix shape, because a dateless shape is as
    /// likely to be part of a model; the rest of the row is kept whole as the
    /// model text and is never split into make and model on token position.
    /// A row of one token is the registration alone. Anything else reads as
    /// neither.
    /// </summary>
    private static (string Registration, string? Model)? SplitLeadingRegistration(string vehicle)
    {
        var tokens = vehicle.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
            return null;
        if (tokens.Length == 1)
        {
            if (InstructionFieldEngine.IsUkRegistration(tokens[0]))
                return (tokens[0], null);
            return null;
        }
        if (!InstructionFieldEngine.IsDatedUkRegistration(tokens[0])
            && InstructionFieldEngine.IsDatedUkRegistration(string.Concat(tokens[0], tokens[1])))
            return ($"{tokens[0]} {tokens[1]}", Rest(tokens, 2));
        return InstructionFieldEngine.IsDatedUkRegistration(tokens[0]) ? (tokens[0], Rest(tokens, 1)) : null;

        static string? Rest(string[] parts, int start) =>
            parts.Length > start ? string.Join(' ', parts[start..]) : null;
    }

    /// <summary>
    /// The repairer ULA names after the paragraph saying it has "instructed
    /// the following Repairer": the next printed row is its name and the row
    /// after that its address. The telephone and e-mail row that follows is
    /// not read.
    /// </summary>
    private static IEnumerable<IntakeContentFragment> RepairerFields(IntakeContentFragment fragment, string[] lines)
    {
        var heading = Array.FindIndex(lines, line => RepairerHeadingRegex().IsMatch(line));
        if (heading < 0)
            yield break;

        var block = lines.Skip(heading + 1).Where(line => !string.IsNullOrWhiteSpace(line)).Take(2).ToArray();
        if (block.Length > 0 && Labelled(fragment, RepairerNameField, block[0]) is { } name)
            yield return name;
        if (block.Length > 1 && Labelled(fragment, RepairerAddressField, block[1]) is { } address)
            yield return address;
    }

    private static IntakeContentFragment? Labelled(IntakeContentFragment origin, string label, string value)
    {
        var cleaned = Clean(value);
        return cleaned.Length == 0 ? null : origin with { Text = $"{label}: {cleaned}" };
    }

    private static string Clean(string value) => WhitespaceRegex().Replace(value, " ").Trim();

    [GeneratedRegex(@"(?im)^[ \t]*Our[ \t]+Reference[ \t]*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ReferenceRegex();

    // The letter prints a tab, not a colon, between the label and the client.
    [GeneratedRegex(@"(?im)^[ \t]*Our[ \t]+Client(?:[ \t]*:|\t| {2,})[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ClaimantRegex();

    [GeneratedRegex(@"(?im)^[ \t]*Vehicle[ \t]*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex VehicleRegex();

    [GeneratedRegex(@"(?im)^[ \t]*Accident[ \t]*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex AccidentRegex();

    [GeneratedRegex(@"(?im)^[ \t]*VAT[ \t]+Registered[ \t]*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex VatRegex();

    [GeneratedRegex(@"(?im)^[ \t]*The[ \t]+circumstances[ \t]+of[ \t]+the[ \t]+accident[ \t]+are[ \t]*:?[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex CircumstancesRegex();

    [GeneratedRegex(@"\binstructed\s+the\s+following\s+Repairer\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)]
    private static partial Regex RepairerHeadingRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 100)]
    private static partial Regex WhitespaceRegex();
}
