using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

public sealed partial class MpInstructionExtractionPolicy
    : IInstructionExtractionPolicy, IInstructionDocumentProfile, IInstructionFieldRoles
{
    public const string Key = "mp_instruction";
    public const int Version = 1;
    public const string SupportedPrincipalCode = "MP";
    public const string DocumentProfileKeyValue = "mp_instruction_document";
    private static readonly InstructionFieldEngine.FieldDefinition[] Definitions =
    [
        new("Claimant name", ["Our client"], PartyRole: "claimant"),
        new("Claim reference", ["Our Ref"], PartyRole: "principal", ReferenceRole: "principal"),
        new("Vehicle registration", ["Vehicle Reg"], IsValidTyped: InstructionFieldEngine.IsUkRegistration, CanonicalValue: InstructionFieldEngine.NormalizeRegistration, PartyRole: "claimant"),
        new("Vehicle make and model", ["Vehicle description"], PartyRole: "claimant"),
        new("Incident date", ["Date of Accident"], IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null, CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "claimant"),
        new("Instruction date", ["Header date"], IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null, CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "instruction"),
        new("Inspection address", ["Vehicle inspection address"], IsRequired: false, PartyRole: "inspection-location"),
        new("Accident circumstances", ["Accident circumstances"], IsRequired: false, PartyRole: "claimant"),
        new("Vehicle mileage", ["Mileage"], IsRequired: false, IsValidTyped: value => InstructionFieldEngine.ParseMileage(value) is not null, PartyRole: "claimant"),
        new("Inspection date", ["Completed inspection date"], IsRequired: false, IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null, CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "inspection"),
        new("VAT status", ["VAT status"], IsRequired: false, PartyRole: "claimant")
    ];
    private static readonly InstructionFieldEngine.LabelRegexCache Cache = new(Definitions);
    public string PrincipalCode => SupportedPrincipalCode;
    public string DocumentProfileKey => DocumentProfileKeyValue;
    public int DocumentProfileVersion => 1;
    public InstructionDocumentSignature Signature => new(InstructionDocumentSignature.InstructionRole,
        ["Please arrange to inspect the above vehicle at your earliest convenience.", "Vehicle Reg:", "Date of Accident:"],
        ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"]);
    public IReadOnlyDictionary<string, InstructionFieldRole> FieldRoles { get; } = Definitions.ToDictionary(item => item.Name, item => new InstructionFieldRole(item.PartyRole, item.ReferenceRole), StringComparer.Ordinal);

    public InstructionExtractionResult Extract(IntakeSourceReadResult readResult, DateTimeOffset processedAtUtc, EstablishedPrincipalContext principalContext)
    {
        ArgumentNullException.ThrowIfNull(readResult); ArgumentNullException.ThrowIfNull(principalContext);
        if (readResult.Status != IntakeSourceReadStatus.Readable || readResult.IsIncomplete) throw new ArgumentException("MP extraction requires complete readable content.", nameof(readResult));
        if (!string.Equals(principalContext.PrincipalCode, SupportedPrincipalCode, StringComparison.Ordinal)) throw new ArgumentException("The established principal is not MP.", nameof(principalContext));
        var (fields, missing, extracted) = InstructionFieldEngine.ExtractFields(readResult.Content.SelectMany(InstructionFields).ToArray(), Definitions, Cache, processedAtUtc);
        var values = fields.ToDictionary(field => field.Name, field => field.SuggestedValue, StringComparer.Ordinal);
        var draft = new InstructionDraft(SupportedPrincipalCode, InstructionFieldEngine.TypedString(values["Claimant name"], 300), InstructionFieldEngine.TypedString(values["Claim reference"], 100),
            InstructionFieldEngine.NormalizeRegistration(values["Vehicle registration"]), InstructionFieldEngine.TypedString(values["Vehicle make and model"], 100), null,
            InstructionFieldEngine.ParseMileage(values["Vehicle mileage"]), InstructionFieldEngine.TypedString(values["Accident circumstances"], 2000), InstructionFieldEngine.ParseDate(values["Incident date"]),
            InstructionFieldEngine.ParseDate(values["Instruction date"]), InstructionFieldEngine.TypedString(values["Inspection address"], 1000), InstructionFieldEngine.ParseDate(values["Inspection date"]),
            null, InstructionFieldEngine.TypedString(values["VAT status"], 100), null, null);
        var evidence = new List<IntakeEvidence>(extracted) { new(IntakeEvidenceSource.Sender, IntakeEvidenceStrength.Strong, IntakeEvidenceFinding.SupportsPrincipal, "established-principal", $"Principal MP was established by {principalContext.PolicyKey} v{principalContext.PolicyVersion}.") };
        return new(InstructionPolicyApplicability.Applicable, evidence, fields, draft, missing, Key, Version);
    }
    private static IEnumerable<IntakeContentFragment> InstructionFields(IntakeContentFragment fragment)
    {
        var text = fragment.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var request = RequestRegex().Match(text); if (!request.Success) yield break;
        var header = text[..request.Index]; var body = text[request.Index..];
        foreach (Match match in ClaimantRegex().Matches(header)) yield return Label(fragment, "Our client", match.Groups["value"].Value);
        foreach (Match match in ReferenceRegex().Matches(header)) yield return Label(fragment, "Our Ref", match.Groups["value"].Value);
        foreach (Match match in VehicleRegex().Matches(header)) { yield return Label(fragment, "Vehicle Reg", match.Groups["registration"].Value); yield return Label(fragment, "Vehicle description", match.Groups["vehicle"].Value); }
        foreach (Match match in AccidentRegex().Matches(header)) yield return Label(fragment, "Date of Accident", match.Groups["value"].Value);
        foreach (Match match in HeaderDateRegex().Matches(header)) yield return Label(fragment, "Header date", match.Groups["value"].Value);
        foreach (Match match in LocationRegex().Matches(body)) yield return Label(fragment, "Vehicle inspection address", match.Groups["value"].Value);
        foreach (Match match in CircumstancesRegex().Matches(body)) yield return Label(fragment, "Accident circumstances", match.Groups["value"].Value);
        foreach (Match match in MileageRegex().Matches(body)) yield return Label(fragment, "Mileage", match.Groups["value"].Value);
        foreach (Match match in ExplicitInspectionDateRegex().Matches(body)) yield return Label(fragment, "Completed inspection date", match.Groups["value"].Value);
        foreach (Match match in VatRegex().Matches(body)) yield return Label(fragment, "VAT status", match.Groups["value"].Value);
    }
    private static IntakeContentFragment Label(IntakeContentFragment origin, string label, string value) => origin with { Text = $"{label}: {WhitespaceRegex().Replace(value, " ").Trim()}" };
    [GeneratedRegex(@"Please\s+arrange\s+to\s+inspect\s+the\s+above\s+vehicle\s+at\s+your\s+earliest\s+convenience\.", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)] private static partial Regex RequestRegex();
    [GeneratedRegex(@"(?im)^\s*Our\s+client\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex ClaimantRegex();
    [GeneratedRegex(@"(?im)^\s*Our\s+Ref\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex ReferenceRegex();
    [GeneratedRegex(@"(?im)^\s*Vehicle\s+Reg\s*:[ \t]*(?<registration>[^,\r\n]+)[ \t]*,[ \t]*(?<vehicle>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex VehicleRegex();
    [GeneratedRegex(@"(?im)^\s*Date\s+of\s+Accident\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex AccidentRegex();
    [GeneratedRegex(@"(?m)^\s*(?<value>\d{1,2}/\d{1,2}/\d{4})\s*$", RegexOptions.CultureInvariant, 100)] private static partial Regex HeaderDateRegex();
    [GeneratedRegex(@"(?ims)The\s+vehicle\s+is\s+to\s+be\s+inspected\s+at\s*:[ \t]*(?<value>.+?)(?=\n\s*Kind\s+regards)", RegexOptions.CultureInvariant, 100)] private static partial Regex LocationRegex();
    [GeneratedRegex(@"(?ims)^\s*(?:Accident\s+circumstances|Circumstances)\s*:[ \t]*(?<value>.+?)(?=\n\s*(?:The\s+vehicle|Kind\s+regards))", RegexOptions.CultureInvariant, 100)] private static partial Regex CircumstancesRegex();
    [GeneratedRegex(@"(?im)^\s*Mileage\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex MileageRegex();
    [GeneratedRegex(@"(?im)^\s*(?:Completed|Appointed)\s+Inspection\s+Date\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex ExplicitInspectionDateRegex();
    [GeneratedRegex(@"(?im)^\s*VAT\s+(?:status|registered)\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex VatRegex();
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 100)] private static partial Regex WhitespaceRegex();
}
