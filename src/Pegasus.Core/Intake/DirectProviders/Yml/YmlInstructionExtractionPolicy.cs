using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

public sealed partial class YmlInstructionExtractionPolicy : IInstructionExtractionPolicy, IInstructionDocumentProfile, IInstructionFieldRoles
{
    public const string Key = "yml_hduk_instruction"; public const int Version = 1; public const string SupportedPrincipalCode = "YML"; public const string DocumentProfileKeyValue = "yml_hduk_instruction_document";
    private static readonly InstructionFieldEngine.FieldDefinition[] Definitions =
    [
        new("Document issuer", ["Document issuer"], PartyRole: "issuer"), new("Claimant name", ["Our Client"], PartyRole: "claimant"),
        new("Claim reference", ["Our Ref"], PartyRole: "principal", ReferenceRole: "principal"),
        new("Vehicle registration", ["Registration"], IsValidTyped: InstructionFieldEngine.IsUkRegistration, CanonicalValue: InstructionFieldEngine.NormalizeRegistration, PartyRole: "claimant"),
        new("Vehicle make and model", ["Our Client's Vehicle"], PartyRole: "claimant"),
        new("Incident date", ["Date of Accident"], IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null, CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "claimant"),
        new("Instruction date", ["Header date"], IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null, CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "instruction"),
        new("Inspection address", ["Vehicle location"], IsRequired: false, PartyRole: "inspection-location"), new("Accident circumstances", ["Accident circumstances"], IsRequired: false, PartyRole: "claimant"),
        new("Inspection contact", ["Inspection contact"], IsRequired: false, PartyRole: "inspection-contact"), new("Vehicle mileage", ["Mileage"], IsRequired: false, IsValidTyped: value => InstructionFieldEngine.ParseMileage(value) is not null, PartyRole: "claimant"),
        new("Inspection date", ["Completed inspection date"], IsRequired: false, IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null, CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "inspection"), new("VAT status", ["VAT status"], IsRequired: false, PartyRole: "claimant")
    ];
    private static readonly InstructionFieldEngine.LabelRegexCache Cache = new(Definitions);
    public string PrincipalCode => SupportedPrincipalCode; public string DocumentProfileKey => DocumentProfileKeyValue; public int DocumentProfileVersion => 1;
    public InstructionDocumentSignature Signature => new(InstructionDocumentSignature.InstructionRole, ["HD UK NETWORK", "Registration", "Our Client’s Vehicle"], ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"]);
    public IReadOnlyDictionary<string, InstructionFieldRole> FieldRoles { get; } = Definitions.ToDictionary(x => x.Name, x => new InstructionFieldRole(x.PartyRole, x.ReferenceRole), StringComparer.Ordinal);
    public InstructionExtractionResult Extract(IntakeSourceReadResult readResult, DateTimeOffset processedAtUtc, EstablishedPrincipalContext principalContext)
    {
        ArgumentNullException.ThrowIfNull(readResult); ArgumentNullException.ThrowIfNull(principalContext);
        if (readResult.Status != IntakeSourceReadStatus.Readable || readResult.IsIncomplete) throw new ArgumentException("YML extraction requires complete readable content.", nameof(readResult));
        if (!string.Equals(principalContext.PrincipalCode, SupportedPrincipalCode, StringComparison.Ordinal)) throw new ArgumentException("The established principal is not YML.", nameof(principalContext));
        var (fields, missing, extracted) = InstructionFieldEngine.ExtractFields(readResult.Content.SelectMany(Fields).ToArray(), Definitions, Cache, processedAtUtc);
        var v = fields.ToDictionary(x => x.Name, x => x.SuggestedValue, StringComparer.Ordinal);
        var draft = new InstructionDraft(SupportedPrincipalCode, InstructionFieldEngine.TypedString(v["Claimant name"], 300), InstructionFieldEngine.TypedString(v["Claim reference"], 100), InstructionFieldEngine.NormalizeRegistration(v["Vehicle registration"]), InstructionFieldEngine.TypedString(v["Vehicle make and model"], 100), null, InstructionFieldEngine.ParseMileage(v["Vehicle mileage"]), InstructionFieldEngine.TypedString(v["Accident circumstances"], 2000), InstructionFieldEngine.ParseDate(v["Incident date"]), InstructionFieldEngine.ParseDate(v["Instruction date"]), InstructionFieldEngine.TypedString(v["Inspection address"], 1000), InstructionFieldEngine.ParseDate(v["Inspection date"]), null, InstructionFieldEngine.TypedString(v["VAT status"], 100), null, null);
        var evidence = new List<IntakeEvidence>(extracted) { new(IntakeEvidenceSource.Sender, IntakeEvidenceStrength.Strong, IntakeEvidenceFinding.SupportsPrincipal, "established-principal", $"Principal YML was established by {principalContext.PolicyKey} v{principalContext.PolicyVersion}; the document issuer remains HD UK Network.") };
        return new(InstructionPolicyApplicability.Applicable, evidence, fields, draft, missing, Key, Version);
    }
    private static IEnumerable<IntakeContentFragment> Fields(IntakeContentFragment f)
    {
        var t=f.Text.Replace("\r\n","\n",StringComparison.Ordinal).Replace('\r','\n'); var dear=DearRegex().Match(t); var sign=SignatureRegex().Match(t); if(!dear.Success||!sign.Success||sign.Index<=dear.Index) yield break; var h=t[..dear.Index]; var b=t[dear.Index..sign.Index];
        yield return L(f,"Document issuer","HD UK Network");
        foreach(Match m in RefRegex().Matches(h)) yield return L(f,"Our Ref",m.Groups["value"].Value); foreach(Match m in DateRegex().Matches(h)) yield return L(f,"Header date",m.Groups["value"].Value);
        foreach(var (r,l) in Lines) foreach(Match m in r.Matches(b)) yield return L(f,l,m.Groups["value"].Value);
        foreach(Match m in CircRegex().Matches(b)) yield return L(f,"Accident circumstances",m.Groups["value"].Value.Trim(' ','\'', '’'));
        foreach(Match m in LocRegex().Matches(b)) yield return L(f,"Vehicle location",m.Groups["value"].Value); foreach(Match m in ContactRegex().Matches(b)) yield return L(f,"Inspection contact",m.Groups["value"].Value);
    }
    private static readonly (Regex,string)[] Lines=[(ClientRegex(),"Our Client"),(VehicleRegex(),"Our Client's Vehicle"),(RegistrationRegex(),"Registration"),(AccidentRegex(),"Date of Accident")];
    private static IntakeContentFragment L(IntakeContentFragment f,string l,string v)=>f with{Text=$"{l}: {WsRegex().Replace(v," ").Trim()}"};
    [GeneratedRegex(@"(?im)^\s*Dear\s+Sirs,?\s*$",RegexOptions.CultureInvariant,100)]private static partial Regex DearRegex(); [GeneratedRegex(@"(?im)^\s*HD\s+UK\s+Network\s*$",RegexOptions.CultureInvariant,100)]private static partial Regex SignatureRegex();
    [GeneratedRegex(@"(?im)^\s*Our\s+Ref\s*:[ \t]*(?<value>[^\r\n]+)",RegexOptions.CultureInvariant,100)]private static partial Regex RefRegex(); [GeneratedRegex(@"(?im)^\s*(?<value>\d{1,2}\s+[A-Za-z]+\s+\d{4})\s*$",RegexOptions.CultureInvariant,100)]private static partial Regex DateRegex();
    [GeneratedRegex(@"(?im)^\s*Our\s+Client\s*:[ \t]*(?<value>[^\r\n]+)",RegexOptions.CultureInvariant,100)]private static partial Regex ClientRegex(); [GeneratedRegex(@"(?im)^\s*Our\s+Client['’]s\s+Vehicle\s*:[ \t]*(?<value>[^\r\n]+)",RegexOptions.CultureInvariant,100)]private static partial Regex VehicleRegex();
    [GeneratedRegex(@"(?im)^\s*Registration\s*:[ \t]*(?<value>[^\r\n]+)",RegexOptions.CultureInvariant,100)]private static partial Regex RegistrationRegex(); [GeneratedRegex(@"(?im)^\s*Date\s+of\s+Accident\s*:[ \t]*(?<value>[^\r\n]+)",RegexOptions.CultureInvariant,100)]private static partial Regex AccidentRegex();
    [GeneratedRegex(@"(?ims)The\s+circumstances\s+of\s+the\s+accident\s+are\s+that[ \t]*(?<value>.+?)(?=\s+As\s+a\s+result\s+of\s+the\s+collision)",RegexOptions.CultureInvariant,100)]private static partial Regex CircRegex(); [GeneratedRegex(@"(?im)The\s+vehicle\s+is\s+currently\s+located\s+at\s*:[ \t]*(?<value>[^\r\n]+)",RegexOptions.CultureInvariant,100)]private static partial Regex LocRegex(); [GeneratedRegex(@"(?im)Please\s+contact\s+the\s+following\s+number\s+to\s+arrange\s+inspection\s*:[ \t]*(?<value>[^\r\n]+)",RegexOptions.CultureInvariant,100)]private static partial Regex ContactRegex(); [GeneratedRegex(@"\s+",RegexOptions.CultureInvariant,100)]private static partial Regex WsRegex();
}
