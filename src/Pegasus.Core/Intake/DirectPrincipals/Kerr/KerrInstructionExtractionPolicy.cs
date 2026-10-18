using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

/// <summary>
/// Reads Kerr Brown's instruction e-mail: the labelled block in the message
/// body, from its Client line to its closing, and the file reference, which
/// only the subject prints.
/// </summary>
public sealed partial class KerrInstructionExtractionPolicy
    : IInstructionExtractionPolicy, IInstructionDocumentProfile, IInstructionFieldRoles
{
    public const string Key = "kerr_instruction";
    public const int Version = 1;
    public const string SupportedPrincipalCode = "KERR";
    public const string DocumentProfileKeyValue = "kerr_instruction_document";
    private static readonly InstructionFieldEngine.FieldDefinition[] Definitions =
    [
        new("Claimant name", ["Client"], PartyRole: "claimant"),
        new("Claim reference", ["Our ref"], PartyRole: "principal", ReferenceRole: "principal"),
        new("Vehicle registration", ["Reg"], IsValidTyped: InstructionFieldEngine.IsUkRegistration, CanonicalValue: InstructionFieldEngine.NormalizeRegistration, PartyRole: "claimant"),
        new("Vehicle make and model", ["Vehicle"], PartyRole: "claimant"),
        new("Incident date", ["RTA"], IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null, CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "claimant")
    ];
    private static readonly InstructionFieldEngine.LabelRegexCache Cache = new(Definitions);
    public string PrincipalCode => SupportedPrincipalCode;
    public string DocumentProfileKey => DocumentProfileKeyValue;
    public int DocumentProfileVersion => 1;
    public InstructionDocumentSignature Signature => new(InstructionDocumentSignature.InstructionRole,
        ["kerrbrown.co.uk", "Client:", "Reg:", "RTA:"],
        ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"]);
    public IReadOnlyDictionary<string, InstructionFieldRole> FieldRoles { get; } = Definitions.ToDictionary(
        item => item.Name, item => new InstructionFieldRole(item.PartyRole, item.ReferenceRole), StringComparer.Ordinal);

    public InstructionExtractionResult Extract(IntakeSourceReadResult readResult, InstructionExtractionTiming timing, EstablishedPrincipalContext principalContext)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        ArgumentNullException.ThrowIfNull(principalContext);
        if (readResult.Status != IntakeSourceReadStatus.Readable || readResult.IsIncomplete) throw new ArgumentException("KERR extraction requires complete readable content.", nameof(readResult));
        if (!string.Equals(principalContext.PrincipalCode, SupportedPrincipalCode, StringComparison.Ordinal)) throw new ArgumentException("The established principal is not KERR.", nameof(principalContext));
        var block = readResult.Content.SelectMany(InstructionFields).ToArray();
        // The subject's reference belongs to the instruction the body states;
        // without that block there is no instruction to give it to.
        IntakeContentFragment[] scoped = block.Length == 0 ? [] : [.. block, .. SubjectReferenceLines(readResult)];
        var (fields, missing, extracted) = InstructionFieldEngine.ExtractFields(scoped, Definitions, Cache, timing);
        var values = fields.ToDictionary(field => field.Name, field => field.SuggestedValue, StringComparer.Ordinal);
        var draft = new InstructionDraft(SupportedPrincipalCode, InstructionFieldEngine.TypedString(values["Claimant name"], 300),
            InstructionFieldEngine.TypedString(values["Claim reference"], 100), InstructionFieldEngine.NormalizeRegistration(values["Vehicle registration"]),
            InstructionFieldEngine.TypedString(values["Vehicle make and model"], 100), null, null, null,
            InstructionFieldEngine.ParseDate(values["Incident date"]), null);
        var evidence = new List<IntakeEvidence>(extracted) { new(IntakeEvidenceSource.Sender, IntakeEvidenceStrength.Strong, IntakeEvidenceFinding.SupportsPrincipal, "established-principal", $"Principal KERR was established by {principalContext.PolicyKey} v{principalContext.PolicyVersion}.") };
        return new(InstructionPolicyApplicability.Applicable, evidence, fields, draft, missing, Key, Version);
    }

    /// <summary>Every file reference the subject prints, in Kerr Brown's initials-then-numbers shape.</summary>
    internal static IEnumerable<string> SubjectReferences(string subject) =>
        ReferenceRegex().Matches(subject).Select(match => match.Groups["value"].Value);

    private static IEnumerable<IntakeContentFragment> SubjectReferenceLines(IntakeSourceReadResult readResult) =>
        readResult.TransportEvidence
            .Where(item => item.Source == IntakeEvidenceSource.Subject && item.SenderIdentityKind == IntakeSenderIdentityKind.Transport)
            .Take(1)
            .SelectMany(item => SubjectReferences(item.Value))
            .Select(reference => new IntakeContentFragment(IntakeEvidenceSource.Subject, "message subject", $"Our ref: {reference}"));

    private static IEnumerable<IntakeContentFragment> InstructionFields(IntakeContentFragment fragment)
    {
        var text = fragment.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var client = ClaimantRegex().Match(text);
        if (!client.Success) yield break;
        var closing = ClosingRegex().Match(text, client.Index);
        if (!closing.Success) yield break;
        var body = text[client.Index..closing.Index];
        foreach (var (regex, label) in LineFields) foreach (Match match in regex.Matches(body)) yield return Label(fragment, label, match.Groups["value"].Value);
    }
    private static readonly (Regex Regex, string Label)[] LineFields = [(ClaimantRegex(), "Client"), (VehicleRegex(), "Vehicle"), (RegistrationRegex(), "Reg"), (AccidentRegex(), "RTA")];
    // The HTML body renders the date's superscript day suffix as its own word
    // ("21 st March 2026"), which date parsing does not read as an ordinal.
    private static IntakeContentFragment Label(IntakeContentFragment origin, string label, string value) =>
        origin with { Text = $"{label}: {Clean(label == "RTA" ? OrdinalGapRegex().Replace(value, string.Empty) : value)}" };
    private static string Clean(string value) => WhitespaceRegex().Replace(value, " ").Trim();
    [GeneratedRegex(@"(?im)^[^\S\n]*Client[^\S\n]*:(?<value>[^\n]*)", RegexOptions.CultureInvariant, 100)] private static partial Regex ClaimantRegex();
    [GeneratedRegex(@"(?im)^[^\S\n]*Vehicle[^\S\n]*:(?<value>[^\n]*)", RegexOptions.CultureInvariant, 100)] private static partial Regex VehicleRegex();
    [GeneratedRegex(@"(?im)^[^\S\n]*Reg[^\S\n]*:(?<value>[^\n]*)", RegexOptions.CultureInvariant, 100)] private static partial Regex RegistrationRegex();
    [GeneratedRegex(@"(?im)^[^\S\n]*RTA[^\S\n]*:(?<value>[^\n]*)", RegexOptions.CultureInvariant, 100)] private static partial Regex AccidentRegex();
    [GeneratedRegex(@"(?im)^[^\S\n]*Kind[^\S\n]+Regards\b", RegexOptions.CultureInvariant, 100)] private static partial Regex ClosingRegex();
    [GeneratedRegex(@"(?<![A-Za-z0-9/])(?<value>[A-Z]{2,5}(?:/[A-Z]{2,5}){0,4}/\d{1,3}/\d{1,8})(?![A-Za-z0-9/])", RegexOptions.CultureInvariant, 100)] private static partial Regex ReferenceRegex();
    [GeneratedRegex(@"(?<=\d)\s+(?=(?:st|nd|rd|th)\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)] private static partial Regex OrdinalGapRegex();
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 100)] private static partial Regex WhitespaceRegex();
}
