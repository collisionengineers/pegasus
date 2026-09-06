using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

public sealed partial class KbsInstructionExtractionPolicy
    : IInstructionExtractionPolicy, IInstructionDocumentProfile, IInstructionFieldRoles
{
    public const string Key = "kbs_instruction";
    public const int Version = 1;
    public const string SupportedPrincipalCode = "KBS";
    public const string DocumentProfileKeyValue = "kbs_instruction_document";
    private static readonly InstructionFieldEngine.FieldDefinition[] Definitions =
    [
        new("Claimant name", ["Our Client"], PartyRole: "claimant"),
        new("Claim reference", ["Our Ref"], CanonicalValue: value => value.Replace('‐', '-'), PartyRole: "principal", ReferenceRole: "principal"),
        new("Vehicle registration", ["Registration"], IsValidTyped: InstructionFieldEngine.IsUkRegistration, CanonicalValue: InstructionFieldEngine.NormalizeRegistration, PartyRole: "claimant"),
        new("Vehicle make", ["Our Client's Vehicle"], PartyRole: "claimant"),
        new("Vehicle model", ["Vehicle model"], IsRequired: false, PartyRole: "claimant"),
        new("Incident date", ["Date of Accident"], IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null, CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "claimant"),
        new("Instruction date", ["Header date"], IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null, CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "instruction"),
        new("Inspection address", ["Vehicle location"], IsRequired: false, PartyRole: "inspection-location"),
        new("Accident circumstances", ["Accident circumstances"], IsRequired: false, PartyRole: "claimant"),
        new("Inspection contact", ["Inspection contact"], IsRequired: false, PartyRole: "inspection-contact"),
        new("Inspection email", ["Inspection email"], IsRequired: false, PartyRole: "inspection-contact"),
        new("Vehicle mileage", ["Mileage"], IsRequired: false, IsValidTyped: value => InstructionFieldEngine.ParseMileage(value) is not null, PartyRole: "claimant"),
        new("Inspection date", ["Completed inspection date"], IsRequired: false, IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null, CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "inspection"),
        new("VAT status", ["VAT status"], IsRequired: false, PartyRole: "claimant")
    ];
    private static readonly InstructionFieldEngine.LabelRegexCache Cache = new(Definitions);
    public string PrincipalCode => SupportedPrincipalCode;
    public string DocumentProfileKey => DocumentProfileKeyValue;
    public int DocumentProfileVersion => 1;
    public InstructionDocumentSignature Signature => new(InstructionDocumentSignature.InstructionRole,
        ["KNIGHTSBRIDGE", "Registration", "Our Client’s Vehicle"],
        ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"]);
    public IReadOnlyDictionary<string, InstructionFieldRole> FieldRoles { get; } = Definitions.ToDictionary(
        item => item.Name, item => new InstructionFieldRole(item.PartyRole, item.ReferenceRole), StringComparer.Ordinal);

    public InstructionExtractionResult Extract(IntakeSourceReadResult readResult, DateTimeOffset processedAtUtc, EstablishedPrincipalContext principalContext)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        ArgumentNullException.ThrowIfNull(principalContext);
        if (readResult.Status != IntakeSourceReadStatus.Readable || readResult.IsIncomplete) throw new ArgumentException("KBS extraction requires complete readable content.", nameof(readResult));
        if (!string.Equals(principalContext.PrincipalCode, SupportedPrincipalCode, StringComparison.Ordinal)) throw new ArgumentException("The established principal is not KBS.", nameof(principalContext));
        var scoped = readResult.Content.SelectMany(InstructionFields).ToArray();
        var (fields, missing, extracted) = InstructionFieldEngine.ExtractFields(scoped, Definitions, Cache, processedAtUtc);
        var values = fields.ToDictionary(field => field.Name, field => field.SuggestedValue, StringComparer.Ordinal);
        var draft = new InstructionDraft(SupportedPrincipalCode, InstructionFieldEngine.TypedString(values["Claimant name"], 300),
            InstructionFieldEngine.TypedString(values["Claim reference"], 100), InstructionFieldEngine.NormalizeRegistration(values["Vehicle registration"]),
            InstructionFieldEngine.TypedString(values["Vehicle make"], 100), InstructionFieldEngine.TypedString(values["Vehicle model"], 100),
            InstructionFieldEngine.ParseMileage(values["Vehicle mileage"]), InstructionFieldEngine.TypedString(values["Accident circumstances"], 2000),
            InstructionFieldEngine.ParseDate(values["Incident date"]), InstructionFieldEngine.ParseDate(values["Instruction date"]),
            InstructionFieldEngine.TypedString(values["Inspection address"], 1000), InstructionFieldEngine.ParseDate(values["Inspection date"]), null,
            InstructionFieldEngine.TypedString(values["VAT status"], 100), null, null);
        var evidence = new List<IntakeEvidence>(extracted) { new(IntakeEvidenceSource.Sender, IntakeEvidenceStrength.Strong, IntakeEvidenceFinding.SupportsPrincipal, "established-principal", $"Principal KBS was established by {principalContext.PolicyKey} v{principalContext.PolicyVersion}.") };
        return new(InstructionPolicyApplicability.Applicable, evidence, fields, draft, missing, Key, Version);
    }

    private static IEnumerable<IntakeContentFragment> InstructionFields(IntakeContentFragment fragment)
    {
        var text = fragment.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var dear = DearRegex().Match(text); var signature = SignatureRegex().Match(text);
        if (!dear.Success || !signature.Success || signature.Index <= dear.Index) yield break;
        var header = text[..dear.Index]; var body = text[dear.Index..signature.Index];
        foreach (Match match in ReferenceRegex().Matches(header)) yield return Label(fragment, "Our Ref", ProtectLeadingHyphen(match.Groups["value"].Value));
        foreach (Match match in HeaderDateRegex().Matches(header)) yield return Label(fragment, "Header date", match.Groups["value"].Value);
        foreach (var (regex, label) in LineFields) foreach (Match match in regex.Matches(body)) yield return Label(fragment, label, match.Groups["value"].Value);
        foreach (Match match in CircumstancesRegex().Matches(body)) yield return Label(fragment, "Accident circumstances", match.Groups["value"].Value);
        foreach (Match match in LocationRegex().Matches(body)) yield return Label(fragment, "Vehicle location", match.Groups["value"].Value.Trim(' ', '(', ')'));
        foreach (Match match in ContactRegex().Matches(body)) yield return Label(fragment, "Inspection contact", match.Groups["value"].Value.Trim(' ', '(', ')'));
        foreach (Match match in EmailRegex().Matches(body)) yield return Label(fragment, "Inspection email", match.Groups["value"].Value);
    }
    private static readonly (Regex Regex, string Label)[] LineFields = [(ClaimantRegex(), "Our Client"), (VehicleRegex(), "Our Client's Vehicle"), (RegistrationRegex(), "Registration"), (AccidentRegex(), "Date of Accident"), (MileageRegex(), "Mileage"), (InspectionDateRegex(), "Completed inspection date"), (VatRegex(), "VAT status")];
    private static IntakeContentFragment Label(IntakeContentFragment origin, string label, string value) => origin with { Text = $"{label}: {Clean(value)}" };
    private static string Clean(string value) => WhitespaceRegex().Replace(value, " ").Trim();
    private static string ProtectLeadingHyphen(string value) => value.TrimStart() is { Length: > 0 } trimmed && trimmed[0] == '-' ? $"‐{trimmed[1..]}" : value;
    [GeneratedRegex(@"(?im)^\s*Dear\s+Sirs,?\s*$", RegexOptions.CultureInvariant, 100)] private static partial Regex DearRegex();
    [GeneratedRegex(@"(?im)^\s*KNIGHTSBRIDGE\s+SOLICITORS\s*$", RegexOptions.CultureInvariant, 100)] private static partial Regex SignatureRegex();
    [GeneratedRegex(@"(?im)^\s*Our\s+Ref\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex ReferenceRegex();
    [GeneratedRegex(@"(?im)^\s*(?<value>\d{1,2}(?:st|nd|rd|th)?\s+[A-Za-z]+\s+\d{4})\s*$", RegexOptions.CultureInvariant, 100)] private static partial Regex HeaderDateRegex();
    [GeneratedRegex(@"(?im)^\s*Our\s+Client\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex ClaimantRegex();
    [GeneratedRegex(@"(?im)^\s*Our\s+Client['’]s\s+Vehicle\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex VehicleRegex();
    [GeneratedRegex(@"(?im)^\s*Registration\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex RegistrationRegex();
    [GeneratedRegex(@"(?im)^\s*Date\s+of\s+Accident\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex AccidentRegex();
    [GeneratedRegex(@"(?ims)The\s+circumstances\s+of\s+the\s+accident\s+are\s+that\s*:?[ \t]*(?<value>.+?)(?=\s+As\s+a\s+result\s+of\s+the\s+collision)", RegexOptions.CultureInvariant, 100)] private static partial Regex CircumstancesRegex();
    [GeneratedRegex(@"(?ims)The\s+vehicle\s+is\s+currently\s+located\s+at\s*:[ \t]*(?<value>.+?)(?=\n\s*Please\s+contact)", RegexOptions.CultureInvariant, 100)] private static partial Regex LocationRegex();
    [GeneratedRegex(@"(?ims)Please\s+contact\s+the\s+following\s+number\s+to\s+arrange\s+inspection\s*:[ \t]*(?:\n\s*)?(?<value>\(?[+\d][+\d ()-]+\)?)", RegexOptions.CultureInvariant, 100)] private static partial Regex ContactRegex();
    [GeneratedRegex(@"(?im)^\s*Email\s*:[ \t]*(?<value>\S+)", RegexOptions.CultureInvariant, 100)] private static partial Regex EmailRegex();
    [GeneratedRegex(@"(?im)^\s*Mileage\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex MileageRegex();
    [GeneratedRegex(@"(?im)^\s*(?:Completed|Appointed)\s+Inspection\s+Date\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex InspectionDateRegex();
    [GeneratedRegex(@"(?im)^\s*VAT\s+(?:status|registered)\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex VatRegex();
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 100)] private static partial Regex WhitespaceRegex();
}
