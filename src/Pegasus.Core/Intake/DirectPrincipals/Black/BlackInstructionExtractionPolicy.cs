using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

/// <summary>
/// Blackstone Legal's instruction: the attached letter "Letter - instructing
/// Collision engineers", read by its labels and a terminal validated vehicle
/// registration. Version 2 reads the live letters as the engineers@ copies of
/// 25 September to 15 October 2026 carry them: the "Our ref:" line follows the
/// signature, where version 1 looked only above the salutation, and the vehicle
/// line may omit the dash before the registration ("Make Model REG"), which
/// version 1 required (operator, 9 November 2026, R797).
/// </summary>
public sealed partial class BlackInstructionExtractionPolicy
    : IInstructionExtractionPolicy, IInstructionDocumentProfile, IInstructionFieldRoles
{
    public const string Key = "black_instruction";
    public const int Version = 2;
    public const string SupportedPrincipalCode = "BLACK";
    public const string DocumentProfileKeyValue = "black_instruction_document";
    public const int DocumentProfileVersionValue = 1;

    private static readonly InstructionFieldEngine.FieldDefinition[] Definitions =
    [
        new("Claimant name", ["Our client"], PartyRole: "claimant"),
        new("Claim reference", ["Our ref"], PartyRole: "principal", ReferenceRole: "principal"),
        new("Vehicle registration", ["Claimant registration"],
            IsValidTyped: InstructionFieldEngine.IsUkRegistration,
            CanonicalValue: InstructionFieldEngine.NormalizeRegistration,
            PartyRole: "claimant"),
        new("Vehicle make", ["Claimant vehicle"],
            AcceptsValue: InstructionFieldEngine.IsPlausibleVehicleMakeModel,
            PartyRole: "claimant"),
        new("Vehicle model", ["Vehicle model"], IsRequired: false,
            AcceptsValue: InstructionFieldEngine.IsPlausibleVehicleMakeModel,
            PartyRole: "claimant"),
        new("Incident date", ["Accident date"],
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate,
            PartyRole: "claimant"),
        new("Vehicle mileage", ["Mileage"], IsRequired: false,
            IsValidTyped: value => InstructionFieldEngine.ParseMileage(value) is not null,
            PartyRole: "claimant"),
        new("Inspection address", ["Inspection address"], IsRequired: false,
            PartyRole: "inspection-location"),
        new("Inspection date", ["Completed inspection date"], IsRequired: false,
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate,
            PartyRole: "inspection"),
        new("Accident circumstances", ["Accident circumstances"], IsRequired: false,
            PartyRole: "claimant"),
        new("VAT status", ["VAT status"], IsRequired: false, PartyRole: "claimant"),
        new("Claimant address", ["Claimant address"], IsRequired: false, PartyRole: "claimant"),
        new("Claimant mobile", ["Claimant mobile"], IsRequired: false, PartyRole: "claimant"),
        new("Vehicle description", ["Raw vehicle"], IsRequired: false, PartyRole: "claimant")
    ];

    private static readonly InstructionFieldEngine.LabelRegexCache Cache = new(Definitions);

    public string PrincipalCode => SupportedPrincipalCode;
    public string DocumentProfileKey => DocumentProfileKeyValue;
    public int DocumentProfileVersion => DocumentProfileVersionValue;
    public InstructionDocumentSignature Signature => new(
        InstructionDocumentSignature.InstructionRole,
        ["Blackstone Legal", "Vehicle:", "Our client:"],
        ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"]);
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
            throw new ArgumentException("BLACK extraction requires complete readable content.", nameof(readResult));
        if (!string.Equals(principalContext.PrincipalCode, SupportedPrincipalCode, StringComparison.Ordinal))
            throw new ArgumentException("The established principal is not BLACK.", nameof(principalContext));

        var scoped = readResult.Content.SelectMany(InstructionFields).ToArray();
        var (fields, missing, fieldEvidence) = InstructionFieldEngine.ExtractFields(
            scoped, Definitions, Cache, timing);
        var values = fields.ToDictionary(field => field.Name, field => field.SuggestedValue, StringComparer.Ordinal);
        var draft = new InstructionDraft(
            SupportedPrincipalCode,
            InstructionFieldEngine.TypedString(values["Claimant name"], 300),
            InstructionFieldEngine.TypedString(values["Claim reference"], 100),
            InstructionFieldEngine.NormalizeRegistration(values["Vehicle registration"]),
            InstructionFieldEngine.TypedString(values["Vehicle make"], 100),
            InstructionFieldEngine.TypedString(values["Vehicle model"], 100),
            InstructionFieldEngine.ParseMileage(values["Vehicle mileage"]),
            InstructionFieldEngine.TypedString(values["Accident circumstances"], 2000),
            InstructionFieldEngine.ParseDate(values["Incident date"]),
            InstructionFieldEngine.TypedString(values["Inspection address"], 1000),
            InstructionFieldEngine.ParseDate(values["Inspection date"]),
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
                $"Principal BLACK was established by {principalContext.PolicyKey} v{principalContext.PolicyVersion}.")
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

    private static IEnumerable<IntakeContentFragment> InstructionFields(IntakeContentFragment fragment)
    {
        var text = fragment.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var dearSirs = DearSirsRegex().Match(text);
        var signature = SignatureRegex().Match(text);
        if (!dearSirs.Success || !signature.Success || signature.Index <= dearSirs.Index)
            yield break;
        var header = text[..dearSirs.Index];
        var instruction = text[dearSirs.Index..signature.Index];
        var footer = text[signature.Index..];

        foreach (Match match in ReferenceRegex().Matches(header).Concat(ReferenceRegex().Matches(footer)))
            yield return Labelled(fragment, "Our ref", match.Groups["value"].Value);
        foreach (Match match in ClaimantRegex().Matches(instruction))
            yield return Labelled(fragment, "Our client", match.Groups["value"].Value);
        foreach (Match match in AccidentDateRegex().Matches(instruction))
            yield return Labelled(fragment, "Accident date", match.Groups["value"].Value);
        foreach (Match match in AddressRegex().Matches(instruction))
            yield return Labelled(fragment, "Claimant address", match.Groups["value"].Value);
        foreach (Match match in MobileRegex().Matches(instruction))
            yield return Labelled(fragment, "Claimant mobile", match.Groups["value"].Value);
        foreach (Match match in VehicleRegex().Matches(instruction))
        {
            var raw = Clean(match.Groups["value"].Value);
            yield return Labelled(fragment, "Raw vehicle", raw);
            var split = SplitTerminalRegistration(raw);
            if (split is null)
                continue;
            yield return Labelled(fragment, "Claimant vehicle", split.Value.Vehicle);
            yield return Labelled(fragment, "Claimant registration", split.Value.Registration);
        }
        foreach (Match match in MileageRegex().Matches(instruction))
            yield return Labelled(fragment, "Mileage", match.Groups["value"].Value);
        foreach (Match match in ExplicitInspectionAddressRegex().Matches(instruction))
            yield return Labelled(fragment, "Inspection address", match.Groups["value"].Value);
        foreach (Match match in ExplicitInspectionDateRegex().Matches(instruction))
            yield return Labelled(fragment, "Completed inspection date", match.Groups["value"].Value);
        foreach (Match match in VatRegex().Matches(instruction))
            yield return Labelled(fragment, "VAT status", match.Groups["value"].Value);
        foreach (Match match in CircumstancesRegex().Matches(instruction))
            yield return Labelled(fragment, "Accident circumstances", match.Groups["value"].Value);
    }

    /// <summary>
    /// The vehicle line is "Make Model - REG" in the recorded letters and, in the
    /// live letter of 9 October 2026, "Make Model REG" with no dash. Without the
    /// dash only a dated registration shape ends the description, so a model that
    /// merely looks like a dateless plate ("320D") stays part of it.
    /// </summary>
    private static (string Vehicle, string Registration)? SplitTerminalRegistration(string raw)
    {
        var dashed = DashedTerminalRegistrationRegex().Match(raw);
        if (dashed.Success)
            return Accept(dashed, InstructionFieldEngine.IsUkRegistration);
        var undashed = UndashedTerminalRegistrationRegex().Match(raw);
        return undashed.Success ? Accept(undashed, InstructionFieldEngine.IsDatedUkRegistration) : null;

        static (string Vehicle, string Registration)? Accept(Match match, Func<string, bool> isRegistration)
        {
            var registration = match.Groups["registration"].Value;
            var vehicle = Clean(match.Groups["vehicle"].Value);
            return isRegistration(registration) && InstructionFieldEngine.IsPlausibleVehicleMakeModel(vehicle)
                ? (vehicle, registration)
                : null;
        }
    }

    private static IntakeContentFragment Labelled(IntakeContentFragment origin, string label, string value) =>
        origin with { Text = $"{label}: {Clean(value)}" };

    private static string Clean(string value) => WhitespaceRegex().Replace(value, " ").Trim(' ', ':');

    [GeneratedRegex(@"(?im)^\s*Dear\s+Sirs,?\s*$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex DearSirsRegex();

    [GeneratedRegex(@"(?im)^\s*Blackstone\s+Legal\s*$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex SignatureRegex();

    [GeneratedRegex(@"(?im)^.*?Our\s+ref\s*:[ \t]*(?<value>\S+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ReferenceRegex();

    [GeneratedRegex(@"(?im)^\s*Our\s+client\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ClaimantRegex();

    [GeneratedRegex(@"(?im)^\s*Accident\s+date\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex AccidentDateRegex();

    [GeneratedRegex(@"(?im)^\s*Address\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex AddressRegex();

    [GeneratedRegex(@"(?im)^\s*Mobile\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex MobileRegex();

    [GeneratedRegex(@"(?im)^\s*Vehicle\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex VehicleRegex();

    [GeneratedRegex(@"^(?<vehicle>.+?)[ \t]+-[ \t]+(?<registration>[A-Z0-9][A-Z0-9 -]{3,18})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)]
    private static partial Regex DashedTerminalRegistrationRegex();

    [GeneratedRegex(@"^(?<vehicle>.+?)[ \t]+(?<registration>[A-Z0-9]{2,4}(?:[ \t]?[A-Z0-9]{3,4})?)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)]
    private static partial Regex UndashedTerminalRegistrationRegex();

    [GeneratedRegex(@"(?im)^\s*Mileage\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex MileageRegex();

    [GeneratedRegex(@"(?im)^\s*(?:Inspection|Vehicle)\s+(?:address|location)\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ExplicitInspectionAddressRegex();

    [GeneratedRegex(@"(?im)^\s*(?:Completed|Appointed)\s+Inspection\s+Date\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ExplicitInspectionDateRegex();

    [GeneratedRegex(@"(?im)^\s*VAT\s+(?:status|registered)\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex VatRegex();

    [GeneratedRegex(@"(?im)^\s*(?:Accident|Incident)\s+Circumstances\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex CircumstancesRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 100)]
    private static partial Regex WhitespaceRegex();
}
