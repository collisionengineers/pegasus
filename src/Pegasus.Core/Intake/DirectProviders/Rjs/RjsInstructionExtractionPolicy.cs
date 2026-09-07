using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

public sealed partial class RjsInstructionExtractionPolicy
    : IInstructionExtractionPolicy, IInstructionDocumentProfile, IInstructionFieldRoles
{
    public const string Key = "rjs_instruction";
    public const int Version = 1;
    public const string SupportedPrincipalCode = "RJS";
    public const string DocumentProfileKeyValue = "rjs_instruction_document";

    private static readonly InstructionFieldEngine.FieldDefinition[] Definitions =
    [
        new("Claimant name", ["Our Client"], PartyRole: "claimant"),
        new("Claim reference", ["Our Reference"], PartyRole: "principal", ReferenceRole: "principal"),
        new("Vehicle registration", ["Client vehicle registration"], IsValidTyped: InstructionFieldEngine.IsUkRegistration, CanonicalValue: InstructionFieldEngine.NormalizeRegistration, PartyRole: "claimant"),
        new("Vehicle make", ["Client vehicle make"], PartyRole: "claimant"),
        new("Vehicle model", ["Client vehicle model"], IsRequired: false, PartyRole: "claimant"),
        new("Incident date", ["Accident"], IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null, CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "claimant"),
        new("Instruction date", ["Header date"], IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null, CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "instruction"),
        new("Inspection address", ["Inspection address"], IsRequired: false, PartyRole: "inspection-location"),
        new("Accident circumstances", ["Accident circumstances"], IsRequired: false, PartyRole: "claimant"),
        new("Claimant address", ["Claimant address"], IsRequired: false, PartyRole: "claimant"),
        new("Claimant work telephone", ["Work Tel"], IsRequired: false, PartyRole: "claimant"),
        new("Claimant mobile", ["Mobile Tel"], IsRequired: false, PartyRole: "claimant"),
        new("Vehicle mileage", ["Mileage"], IsRequired: false, IsValidTyped: value => InstructionFieldEngine.ParseMileage(value) is not null, PartyRole: "claimant"),
        new("Inspection date", ["Completed inspection date"], IsRequired: false, IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null, CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "inspection"),
        new("VAT status", ["VAT status"], IsRequired: false, PartyRole: "claimant")
    ];

    private static readonly InstructionFieldEngine.LabelRegexCache Cache = new(Definitions);
    public string PrincipalCode => SupportedPrincipalCode;
    public string DocumentProfileKey => DocumentProfileKeyValue;
    public int DocumentProfileVersion => 1;
    public InstructionDocumentSignature Signature => new(InstructionDocumentSignature.InstructionRole,
        ["Robert James Solicitors", "Client vehicle registration:", "Client vehicle model:"],
        ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"]);
    public IReadOnlyDictionary<string, InstructionFieldRole> FieldRoles { get; } = Definitions.ToDictionary(
        item => item.Name, item => new InstructionFieldRole(item.PartyRole, item.ReferenceRole), StringComparer.Ordinal);

    public InstructionExtractionResult Extract(IntakeSourceReadResult readResult, DateTimeOffset processedAtUtc, EstablishedPrincipalContext principalContext)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        ArgumentNullException.ThrowIfNull(principalContext);
        if (readResult.Status != IntakeSourceReadStatus.Readable || readResult.IsIncomplete)
            throw new ArgumentException("RJS extraction requires complete readable content.", nameof(readResult));
        if (!string.Equals(principalContext.PrincipalCode, SupportedPrincipalCode, StringComparison.Ordinal))
            throw new ArgumentException("The established principal is not RJS.", nameof(principalContext));

        var (fields, missing, extracted) = InstructionFieldEngine.ExtractFields(
            readResult.Content.SelectMany(InstructionFields).ToArray(), Definitions, Cache, processedAtUtc);
        var values = fields.ToDictionary(field => field.Name, field => field.SuggestedValue, StringComparer.Ordinal);
        var draft = new InstructionDraft(SupportedPrincipalCode,
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
            InstructionFieldEngine.ParseDate(values["Inspection date"]), null,
            InstructionFieldEngine.TypedString(values["VAT status"], 100), null, null);
        var evidence = new List<IntakeEvidence>(extracted)
        {
            new(IntakeEvidenceSource.Sender, IntakeEvidenceStrength.Strong, IntakeEvidenceFinding.SupportsPrincipal,
                "established-principal", $"Principal RJS was established by {principalContext.PolicyKey} v{principalContext.PolicyVersion}.")
        };
        return new(InstructionPolicyApplicability.Applicable, evidence, fields, draft, missing, Key, Version);
    }

    private static IEnumerable<IntakeContentFragment> InstructionFields(IntakeContentFragment fragment)
    {
        var text = fragment.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var dear = DearSirsRegex().Match(text);
        var signature = SignatureRegex().Match(text);
        if (!dear.Success || !signature.Success || signature.Index <= dear.Index) yield break;
        var header = text[..dear.Index];
        var body = text[dear.Index..signature.Index];
        foreach (Match match in ReferenceRegex().Matches(header)) yield return Label(fragment, "Our Reference", match.Groups["value"].Value);
        foreach (Match match in HeaderDateRegex().Matches(header)) yield return Label(fragment, "Header date", match.Groups["value"].Value);
        foreach (var (regex, label) in BodyLabels)
            foreach (Match match in regex.Matches(body)) yield return Label(fragment, label, match.Groups["value"].Value);
        foreach (Match match in AddressRegex().Matches(body)) yield return Label(fragment, "Claimant address", match.Groups["value"].Value);
        foreach (Match match in CircumstancesRegex().Matches(body)) yield return Label(fragment, "Accident circumstances", match.Groups["value"].Value);
    }

    private static readonly (Regex Regex, string Label)[] BodyLabels =
    [
        (ClaimantRegex(), "Our Client"), (AccidentRegex(), "Accident"), (RegistrationRegex(), "Client vehicle registration"),
        (MakeRegex(), "Client vehicle make"), (ModelRegex(), "Client vehicle model"), (WorkRegex(), "Work Tel"),
        (MobileRegex(), "Mobile Tel"), (MileageRegex(), "Mileage"), (InspectionAddressRegex(), "Inspection address"),
        (InspectionDateRegex(), "Completed inspection date"), (VatRegex(), "VAT status")
    ];
    private static IntakeContentFragment Label(IntakeContentFragment origin, string label, string value) => origin with { Text = $"{label}: {Clean(value)}" };
    private static string Clean(string value) => WhitespaceRegex().Replace(value, " ").Trim();

    [GeneratedRegex(@"(?im)^\s*Dear\s+Sirs\s*$", RegexOptions.CultureInvariant, 100)] private static partial Regex DearSirsRegex();
    [GeneratedRegex(@"(?im)^\s*Robert\s+James\s+Solicitors\s*$", RegexOptions.CultureInvariant, 100)] private static partial Regex SignatureRegex();
    [GeneratedRegex(@"(?im)^\s*Our\s+Reference\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex ReferenceRegex();
    [GeneratedRegex(@"(?im)^\s*(?<value>\d{1,2}(?:st|nd|rd|th)?\s+[A-Za-z]+\s+\d{4})\s*$", RegexOptions.CultureInvariant, 100)] private static partial Regex HeaderDateRegex();
    [GeneratedRegex(@"(?im)^\s*Our\s+Client\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex ClaimantRegex();
    [GeneratedRegex(@"(?im)^\s*Accident\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex AccidentRegex();
    [GeneratedRegex(@"(?im)^\s*Client\s+vehicle\s+(?:registration|reg)\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex RegistrationRegex();
    [GeneratedRegex(@"(?im)^\s*Client\s+vehicle\s+make\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex MakeRegex();
    [GeneratedRegex(@"(?im)^\s*Client\s+vehicle\s+model\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex ModelRegex();
    [GeneratedRegex(@"(?im)^\s*Work\s+Tel\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex WorkRegex();
    [GeneratedRegex(@"(?im)^\s*Mobile\s+Tel\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex MobileRegex();
    [GeneratedRegex(@"(?im)^\s*Mileage\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex MileageRegex();
    [GeneratedRegex(@"(?im)^\s*(?:Inspection|Vehicle)\s+(?:address|location)\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex InspectionAddressRegex();
    [GeneratedRegex(@"(?im)^\s*(?:Completed|Appointed)\s+Inspection\s+Date\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex InspectionDateRegex();
    [GeneratedRegex(@"(?im)^\s*VAT\s+(?:status|registered)\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex VatRegex();
    [GeneratedRegex(@"(?ims)^\s*Address\s*:[ \t]*(?<value>.+?)\n\s*(?=(?:Work|Home|Mobile)\s+Tel\s*:)", RegexOptions.CultureInvariant, 100)] private static partial Regex AddressRegex();
    [GeneratedRegex(@"(?ims)^\s*The\s+circumstances\s+of\s+the\s+accident\s+are[ \t]*(?<value>.+?)(?=\n\s*Please\s+arrange)", RegexOptions.CultureInvariant, 100)] private static partial Regex CircumstancesRegex();
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 100)] private static partial Regex WhitespaceRegex();
}
