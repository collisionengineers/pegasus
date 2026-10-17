using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

/// <summary>
/// Reads Ten Legal's instruction e-mail: the Our Ref line above the labelled
/// block in the message body, and the block from its Client line to its Kind
/// Regards closing.
/// </summary>
public sealed partial class TenInstructionExtractionPolicy
    : IInstructionExtractionPolicy, IInstructionDocumentProfile, IInstructionFieldRoles
{
    public const string Key = "ten_instruction";
    public const int Version = 1;
    public const string SupportedPrincipalCode = "TEN";
    public const string DocumentProfileKeyValue = "ten_instruction_document";
    private static readonly InstructionFieldEngine.FieldDefinition[] Definitions =
    [
        new("Claimant name", ["Client"], PartyRole: "claimant"),
        new("Claim reference", ["Our Ref"], PartyRole: "principal", ReferenceRole: "principal"),
        new("Vehicle registration", ["Vehicle Reg"], IsValidTyped: InstructionFieldEngine.IsUkRegistration, CanonicalValue: InstructionFieldEngine.NormalizeRegistration, PartyRole: "claimant"),
        new("Incident date", ["Accident Date"], IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null, CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "claimant")
    ];
    private static readonly InstructionFieldEngine.LabelRegexCache Cache = new(Definitions);
    public string PrincipalCode => SupportedPrincipalCode;
    public string DocumentProfileKey => DocumentProfileKeyValue;
    public int DocumentProfileVersion => 1;
    public InstructionDocumentSignature Signature => new(InstructionDocumentSignature.InstructionRole,
        ["tenlegal.co.uk", "Client:", "Vehicle Reg:", "Accident Date:"],
        ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"]);
    public IReadOnlyDictionary<string, InstructionFieldRole> FieldRoles { get; } = Definitions.ToDictionary(
        item => item.Name, item => new InstructionFieldRole(item.PartyRole, item.ReferenceRole), StringComparer.Ordinal);

    public InstructionExtractionResult Extract(IntakeSourceReadResult readResult, InstructionExtractionTiming timing, EstablishedPrincipalContext principalContext)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        ArgumentNullException.ThrowIfNull(principalContext);
        if (readResult.Status != IntakeSourceReadStatus.Readable || readResult.IsIncomplete) throw new ArgumentException("TEN extraction requires complete readable content.", nameof(readResult));
        if (!string.Equals(principalContext.PrincipalCode, SupportedPrincipalCode, StringComparison.Ordinal)) throw new ArgumentException("The established principal is not TEN.", nameof(principalContext));
        var scoped = readResult.Content.SelectMany(InstructionFields).ToArray();
        var (fields, missing, extracted) = InstructionFieldEngine.ExtractFields(scoped, Definitions, Cache, timing);
        var values = fields.ToDictionary(field => field.Name, field => field.SuggestedValue, StringComparer.Ordinal);
        var draft = new InstructionDraft(SupportedPrincipalCode, InstructionFieldEngine.TypedString(values["Claimant name"], 300),
            InstructionFieldEngine.TypedString(values["Claim reference"], 100), InstructionFieldEngine.NormalizeRegistration(values["Vehicle registration"]),
            null, null, null, null, InstructionFieldEngine.ParseDate(values["Incident date"]), null);
        var evidence = new List<IntakeEvidence>(extracted) { new(IntakeEvidenceSource.Sender, IntakeEvidenceStrength.Strong, IntakeEvidenceFinding.SupportsPrincipal, "established-principal", $"Principal TEN was established by {principalContext.PolicyKey} v{principalContext.PolicyVersion}.") };
        return new(InstructionPolicyApplicability.Applicable, evidence, fields, draft, missing, Key, Version);
    }

    private static IEnumerable<IntakeContentFragment> InstructionFields(IntakeContentFragment fragment)
    {
        var text = fragment.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var client = ClaimantRegex().Match(text);
        if (!client.Success) yield break;
        var closing = ClosingRegex().Match(text, client.Index);
        if (!closing.Success) yield break;
        foreach (Match match in ReferenceRegex().Matches(text[..client.Index])) yield return Label(fragment, "Our Ref", match.Groups["value"].Value);
        var body = text[client.Index..closing.Index];
        foreach (var (regex, label) in LineFields) foreach (Match match in regex.Matches(body)) yield return Label(fragment, label, match.Groups["value"].Value);
    }
    private static readonly (Regex Regex, string Label)[] LineFields = [(ClaimantRegex(), "Client"), (RegistrationRegex(), "Vehicle Reg"), (AccidentRegex(), "Accident Date")];
    private static IntakeContentFragment Label(IntakeContentFragment origin, string label, string value) => origin with { Text = $"{label}: {Clean(value)}" };
    private static string Clean(string value) => WhitespaceRegex().Replace(value, " ").Trim();
    // The HTML body can break the line inside "Our Ref", so the two words may
    // stand on separate lines.
    [GeneratedRegex(@"(?im)^[^\S\n]*Our\s+Ref[^\S\n]*:(?<value>[^\n]*)", RegexOptions.CultureInvariant, 100)] private static partial Regex ReferenceRegex();
    [GeneratedRegex(@"(?im)^[^\S\n]*Client[^\S\n]*:(?<value>[^\n]*)", RegexOptions.CultureInvariant, 100)] private static partial Regex ClaimantRegex();
    [GeneratedRegex(@"(?im)^[^\S\n]*Vehicle[^\S\n]+Reg[^\S\n]*:(?<value>[^\n]*)", RegexOptions.CultureInvariant, 100)] private static partial Regex RegistrationRegex();
    [GeneratedRegex(@"(?im)^[^\S\n]*Accident[^\S\n]+Date[^\S\n]*:(?<value>[^\n]*)", RegexOptions.CultureInvariant, 100)] private static partial Regex AccidentRegex();
    [GeneratedRegex(@"(?im)^[^\S\n]*Kind[^\S\n]+Regards\b", RegexOptions.CultureInvariant, 100)] private static partial Regex ClosingRegex();
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 100)] private static partial Regex WhitespaceRegex();
}
