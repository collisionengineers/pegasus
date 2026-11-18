using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

/// <summary>
/// Reads Parkhouse Assist's instruction: the Word letter
/// "Instructing_Repairer.docx" attached to an e-mail from parkhouseassist.com,
/// addressed to the repairer and copied to Collision Engineers (operator, 18
/// November 2026; R1191, R1195). The policy proposes candidates and allocates
/// nothing; which work the letter asks for is the classification policy's
/// answer, not this one's (R1193).
///
/// The letter gives no inspection address, and its date and damage paragraphs
/// are not Case facts this profile reads.
/// </summary>
public sealed partial class PhouseInstructionExtractionPolicy
    : IInstructionExtractionPolicy, IInstructionDocumentProfile, IInstructionFieldRoles
{
    public const string Key = "phouse_instruction";
    public const int Version = 1;
    public const string SupportedPrincipalCode = "PHOUSE";
    public const string DocumentProfileKeyValue = "phouse_instruction_document";
    public const int DocumentProfileVersionValue = 1;

    /// <summary>
    /// The letter's heading over the repairer's block, the four labels of its
    /// client block and the name it signs with. Smart Business Link's letters
    /// name Parkhouse Assist as an introducer and carry no such heading. The
    /// negative signals are the assessor firms whose letters share such
    /// labels, as PCH and ULA carry them.
    /// </summary>
    public static readonly InstructionDocumentSignature DocumentSignature = new(
        InstructionDocumentSignature.InstructionRole,
        ["Instructing repairer:", "Our Ref:", "Our Client:", "Vehicle:", "Accident Date:", "Parkhouse Assist"],
        ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"]);

    // The synthesized labels below are written only into the rewritten rows
    // this policy appends, so none of them competes with a printed label.
    private const string ReferenceLabel = "Our Ref";
    private const string ClaimantLabel = "Our Client";
    private const string AddressLabel = "Client address";
    private const string TelephoneLabel = "Client telephone";
    private const string RegistrationLabel = "Client vehicle registration";
    private const string ModelLabel = "Client vehicle model";
    private const string IncidentDateLabel = "Accident Date";
    private const string VatLabel = "Vat Registered";
    private const string RepairerNameField = "Repairer name";

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
        new("VAT status", [VatLabel], IsRequired: false, PartyRole: "claimant"),
        new("Claimant address", [AddressLabel], IsRequired: false, PartyRole: "claimant"),
        new("Claimant contact number", [TelephoneLabel], IsRequired: false, PartyRole: "claimant"),
        // The repairer Parkhouse Assist is instructing, which
        // EstablishedIntakeRepairer links to an active Repairer by name
        // (R1027, R1195).
        new(RepairerNameField, [RepairerNameField], IsRequired: false, PartyRole: "repairer")
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
            throw new ArgumentException("Parkhouse extraction requires complete readable content.", nameof(readResult));
        if (!string.Equals(principalContext.PrincipalCode, SupportedPrincipalCode, StringComparison.Ordinal))
            throw new ArgumentException("The established principal is not PHOUSE.", nameof(principalContext));

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
            null,
            InstructionFieldEngine.ParseDate(values["Incident date"]),
            null,
            null,
            null,
            InstructionFieldEngine.TypedString(values["VAT status"], 100),
            InstructionFieldEngine.TypedString(values["Claimant address"], 1000),
            InstructionFieldEngine.TypedString(values["Claimant contact number"], 100));
        var evidence = new List<IntakeEvidence>(fieldEvidence)
        {
            new(
                IntakeEvidenceSource.Sender,
                IntakeEvidenceStrength.Strong,
                IntakeEvidenceFinding.SupportsPrincipal,
                "established-principal",
                $"Principal PHOUSE was established by {principalContext.PolicyKey} v{principalContext.PolicyVersion}.")
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
            var (registration, model) = SplitVehicle(Clean(match.Groups["value"].Value));
            if (Labelled(fragment, RegistrationLabel, registration) is { } registrationRow)
                yield return registrationRow;
            if (model is not null && Labelled(fragment, ModelLabel, model) is { } modelRow)
                yield return modelRow;
        }

        // The row under the heading is the repairer's name; the address rows
        // beneath it are not read.
        var lines = text.Split('\n');
        var heading = Array.FindIndex(lines, line => RepairerHeadingRegex().IsMatch(line));
        if (heading >= 0
            && lines.Skip(heading + 1).FirstOrDefault(line => !string.IsNullOrWhiteSpace(line)) is { } name
            && Labelled(fragment, RepairerNameField, name) is { } repairer)
        {
            yield return repairer;
        }
    }

    private static readonly (Regex Regex, string Label)[] LineFields =
    [
        (ReferenceRegex(), ReferenceLabel),
        (ClaimantRegex(), ClaimantLabel),
        (AddressRegex(), AddressLabel),
        (TelephoneRegex(), TelephoneLabel),
        (AccidentDateRegex(), IncidentDateLabel),
        (VatRegex(), VatLabel)
    ];

    /// <summary>
    /// The "Vehicle:" row prints the registration, a dash and the vehicle
    /// ("VK63 SSZ - BMW 320d", "ED13 BEN – Audi A7"). The dash is the letter's
    /// own separator, so what stands before it is the registration whatever
    /// its shape, and the rest is kept whole as the model text, never split
    /// into make and model on token position. A row with no dash is offered
    /// whole as the registration, which the field's own check then judges.
    /// </summary>
    private static readonly char[] VehicleDashes = ['-', '–', '—'];

    private static (string Registration, string? Model) SplitVehicle(string vehicle)
    {
        var dash = vehicle.IndexOfAny(VehicleDashes);
        if (dash < 0)
            return (vehicle, null);

        var model = vehicle[(dash + 1)..].Trim();
        return (vehicle[..dash].Trim(), model.Length == 0 ? null : model);
    }

    private static IntakeContentFragment? Labelled(IntakeContentFragment origin, string label, string value)
    {
        var cleaned = Clean(value);
        return cleaned.Length == 0 ? null : origin with { Text = $"{label}: {cleaned}" };
    }

    private static string Clean(string value) => WhitespaceRegex().Replace(value, " ").Trim();

    [GeneratedRegex(@"(?im)^[ \t]*Our[ \t]+Ref[ \t]*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ReferenceRegex();

    [GeneratedRegex(@"(?im)^[ \t]*Our[ \t]+Client[ \t]*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ClaimantRegex();

    [GeneratedRegex(@"(?im)^[ \t]*Address[ \t]*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex AddressRegex();

    [GeneratedRegex(@"(?im)^[ \t]*Telephone[ \t]*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex TelephoneRegex();

    [GeneratedRegex(@"(?im)^[ \t]*Vehicle[ \t]*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex VehicleRegex();

    [GeneratedRegex(@"(?im)^[ \t]*Accident[ \t]+Date[ \t]*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex AccidentDateRegex();

    [GeneratedRegex(@"(?im)^[ \t]*Vat[ \t]+Registered[ \t]*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex VatRegex();

    [GeneratedRegex(@"^[ \t]*Instructing[ \t]+repairer[ \t]*:[ \t]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)]
    private static partial Regex RepairerHeadingRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 100)]
    private static partial Regex WhitespaceRegex();
}
