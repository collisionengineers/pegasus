using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

/// <summary>
/// Reads Fairway Legal's current instruction block while keeping insured,
/// third-party and location facts in their printed roles.
/// </summary>
public sealed partial class FwInstructionExtractionPolicy
    : IInstructionExtractionPolicy, IInstructionDocumentProfile, IInstructionFieldRoles
{
    public const string Key = "fw_instruction";
    public const int Version = 1;
    public const string SupportedPrincipalCode = "FW";
    public const string DocumentProfileKeyValue = "fw_instruction_document";
    public const int DocumentProfileVersionValue = 1;

    private const string AccidentLocationField = "Accident location";
    private const string ThirdPartyNameField = "Third party name";
    private const string ThirdPartyRegistrationField = "Third party registration";
    private const string ThirdPartyVehicleField = "Third party vehicle";

    private static readonly InstructionFieldEngine.FieldDefinition[] Definitions =
    [
        new("Claimant name", ["Our insured name"], PartyRole: "claimant"),
        new("Claim reference", ["Our reference"], PartyRole: "principal", ReferenceRole: "principal"),
        new("Vehicle registration", ["Insured registration"],
            IsValidTyped: InstructionFieldEngine.IsUkRegistration,
            CanonicalValue: InstructionFieldEngine.NormalizeRegistration,
            PartyRole: "claimant"),
        new("Vehicle make", ["Insured vehicle"],
            AcceptsValue: InstructionFieldEngine.IsPlausibleVehicleMakeModel,
            PartyRole: "claimant"),
        new("Vehicle model", ["Insured model"], IsRequired: false, PartyRole: "claimant"),
        new("Vehicle mileage", ["Insured mileage"], IsRequired: false,
            IsValidTyped: value => InstructionFieldEngine.ParseMileage(value) is not null,
            PartyRole: "claimant"),
        new("Incident date", ["Accident date"],
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate,
            PartyRole: "claimant"),
        new("Instruction date", ["Current instruction date"], IsRequired: false,
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate,
            PartyRole: "instruction"),
        new("Inspection address", ["Inspection location"], IsRequired: false, PartyRole: "inspection-location"),
        new(AccidentLocationField, [AccidentLocationField], IsRequired: false, PartyRole: "incident-location"),
        new("Accident circumstances", ["Current circumstances"], IsRequired: false, PartyRole: "claimant"),
        new("VAT status", ["VAT status"], IsRequired: false, PartyRole: "claimant"),
        new(ThirdPartyNameField, [ThirdPartyNameField], IsRequired: false, PartyRole: "third-party"),
        new(ThirdPartyRegistrationField, [ThirdPartyRegistrationField], IsRequired: false,
            IsValidTyped: InstructionFieldEngine.IsUkRegistration,
            CanonicalValue: InstructionFieldEngine.NormalizeRegistration,
            PartyRole: "third-party"),
        new(ThirdPartyVehicleField, [ThirdPartyVehicleField], IsRequired: false,
            AcceptsValue: InstructionFieldEngine.IsPlausibleVehicleMakeModel,
            PartyRole: "third-party")
    ];

    private static readonly InstructionFieldEngine.LabelRegexCache Cache = new(Definitions);
    private static readonly Dictionary<string, InstructionFieldEngine.FieldDefinition> DefinitionByName =
        Definitions.ToDictionary(definition => definition.Name, StringComparer.Ordinal);

    public string PrincipalCode => SupportedPrincipalCode;
    public string DocumentProfileKey => DocumentProfileKeyValue;
    public int DocumentProfileVersion => DocumentProfileVersionValue;
    public InstructionDocumentSignature Signature => new(
        InstructionDocumentSignature.InstructionRole,
        ["fairwaylegal", "Vehicle Registration Number:", "Make/Model:"],
        ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"]);

    public IReadOnlyList<InstructionTemplateVariant> Variants { get; } =
    [
        new("collision-profile-fw-garage", new(
            InstructionDocumentSignature.InstructionRole,
            ["fairwaylegal", "Inspection Location:", "Vehicle Registration Number:", "Make/Model:"],
            ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"])),
        new("collision-profile-fw-solicitor", new(
            InstructionDocumentSignature.InstructionRole,
            ["fairwaylegal", "Vehicle Registration Number:", "Make/Model:"],
            ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"]))
    ];

    public IReadOnlyDictionary<string, InstructionFieldRole> FieldRoles { get; } = Definitions.ToDictionary(
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
            throw new ArgumentException("FW extraction requires complete readable content.", nameof(readResult));
        if (!string.Equals(principalContext.PrincipalCode, SupportedPrincipalCode, StringComparison.Ordinal))
            throw new ArgumentException("The established principal is not FW.", nameof(principalContext));

        var scoped = readResult.Content.SelectMany(CurrentInstructionFields).ToArray();
        var (extracted, missing, fieldEvidence) = InstructionFieldEngine.ExtractFields(
            scoped,
            Definitions,
            Cache,
            processedAtUtc);
        var fields = extracted.Select(WithCurrentConflict).ToArray();
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
            InstructionFieldEngine.ParseDate(values["Instruction date"]),
            InstructionFieldEngine.TypedString(values["Inspection address"], 1000),
            null,
            null,
            InstructionFieldEngine.TypedString(values["VAT status"], 100),
            null,
            null);
        var conflicts = fields.Where(field => field.HasConflict).Select(field => field.Name)
            .ToHashSet(StringComparer.Ordinal);
        var evidence = new List<IntakeEvidence>(fieldEvidence.Where(item => !conflicts.Contains(item.Signal)))
        {
            new(
                IntakeEvidenceSource.Sender,
                IntakeEvidenceStrength.Strong,
                IntakeEvidenceFinding.SupportsPrincipal,
                "established-principal",
                $"Principal FW was established by {principalContext.PolicyKey} v{principalContext.PolicyVersion}.")
        };
        evidence.AddRange(fields.Where(field => field.HasConflict).Select(field => new IntakeEvidence(
            field.Candidates[0].Source,
            IntakeEvidenceStrength.Strong,
            IntakeEvidenceFinding.ConflictingField,
            field.Name,
            $"Conflicting current {field.Name.ToLowerInvariant()} candidates require operator review.")));

        return new(
            InstructionPolicyApplicability.Applicable,
            evidence,
            fields,
            draft,
            missing,
            Key,
            Version);
    }

    private static IEnumerable<IntakeContentFragment> CurrentInstructionFields(IntakeContentFragment fragment)
    {
        var current = CurrentInstruction(fragment.Text);
        if (current is null)
            yield break;

        foreach (Match match in OurReferenceRegex().Matches(current))
            yield return Labelled(fragment, "Our reference", match.Groups["value"].Value);
        foreach (Match match in InsuredNameRegex().Matches(current))
            yield return Labelled(fragment, "Our insured name", match.Groups["value"].Value);
        foreach (Match match in RegistrationRegex().Matches(current))
            yield return Labelled(fragment, "Insured registration", match.Groups["value"].Value);
        foreach (Match match in VehicleRegex().Matches(current))
            yield return Labelled(fragment, "Insured vehicle", match.Groups["value"].Value);
        foreach (Match match in AccidentDateRegex().Matches(current))
            yield return Labelled(fragment, "Accident date", match.Groups["value"].Value);
        foreach (Match match in InstructionDateRegex().Matches(current))
            yield return Labelled(fragment, "Current instruction date", match.Groups["value"].Value);
        foreach (Match match in MileageRegex().Matches(current))
            yield return Labelled(fragment, "Insured mileage", match.Groups["value"].Value);
        foreach (Match match in AccidentLocationRegex().Matches(current))
            yield return Labelled(fragment, AccidentLocationField, match.Groups["value"].Value);
        foreach (Match match in CircumstancesRegex().Matches(current))
            yield return Labelled(fragment, "Current circumstances", match.Groups["value"].Value);
        foreach (Match match in InspectionLocationRegex().Matches(current))
        {
            var value = Clean(match.Groups["value"].Value);
            if (value.Length > 0)
                yield return Labelled(fragment, "Inspection location", value);
        }
        foreach (Match match in ThirdPartyNameRegex().Matches(current))
        {
            var value = Clean(match.Groups["value"].Value);
            if (value.Length > 0)
                yield return Labelled(fragment, ThirdPartyNameField, value);
        }
        foreach (Match match in ThirdPartyVehicleRegex().Matches(current))
        {
            yield return Labelled(fragment, ThirdPartyRegistrationField, match.Groups["registration"].Value);
            var vehicle = Clean(match.Groups["vehicle"].Value);
            if (vehicle.Length > 0)
                yield return Labelled(fragment, ThirdPartyVehicleField, vehicle);
        }
    }

    private static string? CurrentInstruction(string text)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var start = normalized.IndexOf("New INSTRUCTIONS:", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return null;
        var current = normalized[start..];
        var quote = QuotedMessageBoundaryRegex().Match(current);
        return quote.Success ? current[..quote.Index] : current;
    }

    private static InstructionReviewField WithCurrentConflict(InstructionReviewField field)
    {
        if (field.Candidates.Count < 2)
            return field;

        var definition = DefinitionByName[field.Name];
        var distinct = field.Candidates.Select(candidate =>
                definition.CanonicalValue?.Invoke(candidate.Value) ?? Clean(candidate.Value))
            .Where(value => value is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .Count();
        return distinct > 1
            ? field with { SuggestedValue = null, HasConflict = true }
            : field;
    }

    private static IntakeContentFragment Labelled(
        IntakeContentFragment origin,
        string label,
        string value) =>
        origin with { Text = $"{label}: {Clean(value)}" };

    private static string Clean(string value) => WhitespaceRegex().Replace(value, " ").Trim(' ', ':');

    [GeneratedRegex(@"(?im)^\s*Our\s+Ref\s*:\s*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex OurReferenceRegex();

    [GeneratedRegex(@"(?ims)^\s*Our\s+Insured\s*:\s*Name\s*:\s*(?<value>.+?)(?=^\s*Address\s*:)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex InsuredNameRegex();

    [GeneratedRegex(@"(?im)^\s*Vehicle\s+Registration\s+Number\s*:\s*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex RegistrationRegex();

    [GeneratedRegex(@"(?ims)^\s*Make/Model\s*:\s*(?<value>.+?)(?=^\s*Damage\s*:|^\s*Accident\s+Location\s*:)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex VehicleRegex();

    [GeneratedRegex(@"(?im)^\s*Accident\s+Date\s*:\s*(?<value>.+?)(?=\s+Time\s*:|$)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex AccidentDateRegex();

    [GeneratedRegex(@"(?im)^\s*Date\s*:\s*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex InstructionDateRegex();

    [GeneratedRegex(@"(?im)^\s*M(?:i|e)l(?:e)?age\s*(?:[-:]\s*)+(?<value>[\d,]+(?:\s*(?:miles?|mi))?)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex MileageRegex();

    [GeneratedRegex(@"(?ims)^\s*Accident\s+Location\s*:\s*(?<value>.+?)(?=^\s*Circumstances?\s*:)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex AccidentLocationRegex();

    [GeneratedRegex(@"(?ims)^\s*Circumstances?\s*:\s*(?<value>.+?)(?=^\s*Third\s+Party\s+Name\b)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex CircumstancesRegex();

    [GeneratedRegex(@"(?ims)^\s*Inspection\s+Location[ \t]*:[ \t]*(?<value>.*?)(?=^\s*Should\s+you\s+have\b|^\s*Kind\s+Regards\b|\z)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex InspectionLocationRegex();

    [GeneratedRegex(@"(?ims)^\s*Third\s+Party\s+Name\s*(?<value>.*?)(?=^\s*Third\s+Party\s+Reg\s*:)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ThirdPartyNameRegex();

    [GeneratedRegex(@"(?im)^\s*Third\s+Party\s+Reg\s*:\s*(?<registration>[A-Z]{2}\s?\d{2}\s?[A-Z]{3})\s*(?<vehicle>[^\r\n]*)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ThirdPartyVehicleRegex();

    [GeneratedRegex(@"(?im)^\s*(?:-{2,}\s*)?Original Message(?:\s*-{2,})?\s*$|^\s*From\s*:.+\n\s*Sent\s*:", RegexOptions.CultureInvariant, 100)]
    private static partial Regex QuotedMessageBoundaryRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 100)]
    private static partial Regex WhitespaceRegex();
}
