using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

/// <summary>Reads Smart Business Link instruction sections without mixing policyholder, repairer, or hire roles.</summary>
public sealed partial class SblInstructionExtractionPolicy
    : IInstructionExtractionPolicy, IInstructionDocumentProfile, IInstructionFieldRoles
{
    public const string Key = "sbl_instruction";
    public const int Version = 1;
    public const string SupportedPrincipalCode = "SBL";
    public const string DocumentProfileKeyValue = "sbl_instruction_document";
    public const int DocumentProfileVersionValue = 1;

    private static readonly InstructionFieldEngine.FieldDefinition[] Definitions =
    [
        new("Claimant name", ["Policyholder"], PartyRole: "claimant"),
        new("Claim reference", ["Claim number"], PartyRole: "principal", ReferenceRole: "principal"),
        new("Vehicle registration", ["Registration"],
            AcceptsValue: IsRegistration, CanonicalValue: CanonicalRegistration, PartyRole: "claimant"),
        new("Vehicle make", ["Vehicle make"],
            AcceptsValue: InstructionFieldEngine.IsPlausibleVehicleMakeModel, PartyRole: "claimant"),
        new("Vehicle model", ["Vehicle model"], IsRequired: false,
            AcceptsValue: InstructionFieldEngine.IsPlausibleVehicleMakeModel, PartyRole: "claimant"),
        new("Incident date", ["Incident date"],
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "claimant"),
        new("Instruction date", ["Instruction date"],
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "instruction"),
        new("Vehicle mileage", ["Mileage"], IsRequired: false,
            IsValidTyped: value => InstructionFieldEngine.ParseMileage(value) is not null, PartyRole: "claimant"),
        new("Inspection address", ["Current vehicle location"], IsRequired: false,
            PartyRole: "inspection-location"),
        new("Inspection date", ["Completed inspection date"], IsRequired: false,
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "inspection"),
        new("Accident circumstances", ["Incident circumstances"], IsRequired: false, PartyRole: "claimant"),
        new("VAT status", ["Policyholder VAT status"], IsRequired: false, PartyRole: "claimant"),
        new("Claimant address", ["Policyholder address"], IsRequired: false, PartyRole: "claimant"),
        new("Driver", ["Driver"], IsRequired: false, PartyRole: "driver"),
        new("Introducer", ["Introducer"], IsRequired: false, PartyRole: "introducer"),
        new("Repairer name", ["Repairer name"], IsRequired: false, PartyRole: "repairer"),
        new("Repairer address", ["Repairer address"], IsRequired: false, PartyRole: "repairer"),
        new("Repairer telephone", ["Repairer telephone"], IsRequired: false, PartyRole: "repairer"),
        new("Repairer email", ["Repairer email"], IsRequired: false, PartyRole: "repairer"),
        new("Agreed labour rate", ["Agreed labour rate"], IsRequired: false, PartyRole: "repairer-rate"),
        new("Hire company", ["Hire company"], IsRequired: false, PartyRole: "hire"),
        new("Hire out date", ["Hire out date"], IsRequired: false,
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "hire")
    ];

    private static readonly InstructionFieldEngine.LabelRegexCache Cache = new(Definitions);

    public string PrincipalCode => SupportedPrincipalCode;
    public string DocumentProfileKey => DocumentProfileKeyValue;
    public int DocumentProfileVersion => DocumentProfileVersionValue;
    public InstructionDocumentSignature Signature => new(
        InstructionDocumentSignature.InstructionRole,
        ["Smart Business Link", "Registration:", "Vehicle Make:"],
        ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"]);
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
            throw new ArgumentException("SBL extraction requires complete readable content.", nameof(readResult));
        if (!string.Equals(principalContext.PrincipalCode, SupportedPrincipalCode, StringComparison.Ordinal))
            throw new ArgumentException("The established principal is not SBL.", nameof(principalContext));

        var scoped = readResult.Content.SelectMany(InstructionFields).ToArray();
        var (fields, missing, fieldEvidence) = InstructionFieldEngine.ExtractFields(
            scoped, Definitions, Cache, processedAtUtc);
        var values = fields.ToDictionary(field => field.Name, field => field.SuggestedValue, StringComparer.Ordinal);
        var draft = new InstructionDraft(
            SupportedPrincipalCode,
            InstructionFieldEngine.TypedString(values["Claimant name"], 300),
            InstructionFieldEngine.TypedString(values["Claim reference"], 100),
            CanonicalRegistration(values["Vehicle registration"] ?? string.Empty),
            InstructionFieldEngine.TypedString(values["Vehicle make"], 100),
            InstructionFieldEngine.TypedString(values["Vehicle model"], 100),
            InstructionFieldEngine.ParseMileage(values["Vehicle mileage"]),
            InstructionFieldEngine.TypedString(values["Accident circumstances"], 2000),
            InstructionFieldEngine.ParseDate(values["Incident date"]),
            InstructionFieldEngine.ParseDate(values["Instruction date"]),
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
                $"Principal SBL was established by {principalContext.PolicyKey} v{principalContext.PolicyVersion}.")
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
        if (!SmartInstructionRegex().IsMatch(text))
            yield break;

        var instruction = Section(text, "Instruction Details", "Claim & Policyholder");
        var claim = Section(text, "Claim & Policyholder", "Vehicle & Damage");
        var vehicle = Section(text, "Vehicle & Damage", "Repairer Details");
        var repairer = Section(text, "Repairer Details", "Insurance & Hire");
        var hire = Section(text, "Insurance & Hire", "Important Notes");

        foreach (var item in ReadLabels(fragment, instruction,
            ("Date", "Instruction date"), ("Introducer", "Introducer")))
            yield return item;
        foreach (var item in ReadLabels(fragment, claim,
            ("Claim Number", "Claim number"), ("Incident Date", "Incident date"),
            ("Driver", "Driver"), ("Policyholder Name", "Policyholder"),
            ("Policyholder VAT Status", "Policyholder VAT status"),
            ("Address", "Policyholder address")))
            yield return item;
        foreach (var item in ReadLabels(fragment, vehicle,
            ("Vehicle Make", "Vehicle make"), ("Model", "Vehicle model"),
            ("Registration", "Registration"), ("Mileage", "Mileage"),
            ("Current Vehicle Location", "Current vehicle location"),
            ("Completed Inspection Date", "Completed inspection date")))
            yield return item;
        if (LabelBlock(vehicle, "Incident Circumstances", "Agreed Value") is { } circumstances)
            yield return Labelled(fragment, "Incident circumstances", circumstances);
        foreach (var item in ReadLabels(fragment, repairer,
            ("Repairer Name", "Repairer name"), ("Repairer Address", "Repairer address"),
            ("Repairer Tel", "Repairer telephone"), ("Repairer Email", "Repairer email"),
            ("Agreed Labour Rate", "Agreed labour rate")))
            yield return item;
        foreach (var item in ReadLabels(fragment, hire,
            ("Hire Company", "Hire company"), ("Hire Out Date", "Hire out date")))
            yield return item;
    }

    private static IEnumerable<IntakeContentFragment> ReadLabels(
        IntakeContentFragment origin,
        string? section,
        params (string Source, string Target)[] labels)
    {
        if (section is null)
            yield break;
        foreach (var (source, target) in labels)
        {
            var readings = new List<string>();
            foreach (Match match in LabelRegex(source).Matches(section))
            {
                var value = Clean(match.Groups["value"].Value);
                if (!IsPlaceholder(value))
                    readings.Add($"{target}: {value}");
            }
            if (readings.Count > 0)
                yield return origin with { Text = string.Join(Environment.NewLine, readings) };
        }
    }

    private static string? Section(string text, string heading, string nextHeading)
    {
        var start = text.IndexOf(heading, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return null;
        var end = text.IndexOf(nextHeading, start + heading.Length, StringComparison.OrdinalIgnoreCase);
        return end < 0 ? null : text[start..end];
    }

    private static string? LabelBlock(string? section, string label, string nextLabel)
    {
        if (section is null)
            return null;
        var match = Regex.Match(
            section,
            $@"(?ims)^\s*{Regex.Escape(label)}\s*:\s*(?<value>.+?)(?=^\s*{Regex.Escape(nextLabel)}\s*:)",
            RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
        var value = match.Success ? Clean(match.Groups["value"].Value) : string.Empty;
        return IsPlaceholder(value) ? null : value;
    }

    private static Regex LabelRegex(string label) => new(
        $@"(?im)^\s*{Regex.Escape(label)}\s*:\s*(?<value>[^\r\n]*)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static IntakeContentFragment Labelled(IntakeContentFragment origin, string label, string value) =>
        origin with { Text = $"{label}: {value}" };

    private static bool IsPlaceholder(string value) =>
        string.IsNullOrWhiteSpace(value) || value is "-" or "N/A" or "n/a";

    private static bool IsRegistration(string value) =>
        value.Length <= 20
        && value.Any(char.IsLetterOrDigit)
        && value.All(character => char.IsLetterOrDigit(character) || character is ' ' or '-');

    private static string? CanonicalRegistration(string value) =>
        IsPlaceholder(value)
            ? null
            : InstructionFieldEngine.IsUkRegistration(value)
            ? InstructionFieldEngine.NormalizeRegistration(value)!
            : Clean(value);

    private static string Clean(string value) => WhitespaceRegex().Replace(value, " ").Trim(' ', ':');

    [GeneratedRegex(@"(?im)^\s*URGENT\s+NEW\s+INSTRUCTION\s*$|^\s*From\s*:\s*Smart\s+Business\s+Link\s*$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex SmartInstructionRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 100)]
    private static partial Regex WhitespaceRegex();
}
