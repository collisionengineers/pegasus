using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

/// <summary>
/// Reads KMR Law's inspection letter: the Our Reference line above its Dear
/// Sirs salutation, and from the salutation to its Yours faithfully closing
/// the Client and Accident lines and the registration its prose names.
/// </summary>
public sealed partial class KmrInstructionExtractionPolicy
    : IInstructionExtractionPolicy, IInstructionDocumentProfile, IInstructionFieldRoles
{
    public const string Key = "kmr_instruction";
    public const int Version = 1;
    public const string SupportedPrincipalCode = "KMR";
    public const string DocumentProfileKeyValue = "kmr_instruction_document";
    private static readonly InstructionFieldEngine.FieldDefinition[] Definitions =
    [
        new("Claimant name", ["Client"], PartyRole: "claimant"),
        new("Claim reference", ["Our Reference"], PartyRole: "principal", ReferenceRole: "principal"),
        new("Vehicle registration", ["registration number"], AcceptsValue: InstructionFieldEngine.IsUkRegistration, CanonicalValue: InstructionFieldEngine.NormalizeRegistration, PartyRole: "claimant"),
        new("Incident date", ["Accident"], IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null, CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "claimant")
    ];
    private static readonly InstructionFieldEngine.LabelRegexCache Cache = new(Definitions);
    public string PrincipalCode => SupportedPrincipalCode;
    public string DocumentProfileKey => DocumentProfileKeyValue;
    public int DocumentProfileVersion => 1;
    public InstructionDocumentSignature Signature => new(InstructionDocumentSignature.InstructionRole,
        ["KMR Law Limited", "Our Reference:", "Client:", "Accident:"],
        ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"]);
    public IReadOnlyDictionary<string, InstructionFieldRole> FieldRoles { get; } = Definitions.ToDictionary(
        item => item.Name, item => new InstructionFieldRole(item.PartyRole, item.ReferenceRole), StringComparer.Ordinal);

    public InstructionExtractionResult Extract(IntakeSourceReadResult readResult, InstructionExtractionTiming timing, EstablishedPrincipalContext principalContext)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        ArgumentNullException.ThrowIfNull(principalContext);
        if (readResult.Status != IntakeSourceReadStatus.Readable || readResult.IsIncomplete) throw new ArgumentException("KMR extraction requires complete readable content.", nameof(readResult));
        if (!string.Equals(principalContext.PrincipalCode, SupportedPrincipalCode, StringComparison.Ordinal)) throw new ArgumentException("The established principal is not KMR.", nameof(principalContext));
        var scoped = readResult.Content.SelectMany(InstructionFields).ToArray();
        var (fields, missing, extracted) = InstructionFieldEngine.ExtractFields(scoped, Definitions, Cache, timing);
        var values = fields.ToDictionary(field => field.Name, field => field.SuggestedValue, StringComparer.Ordinal);
        var draft = new InstructionDraft(SupportedPrincipalCode, InstructionFieldEngine.TypedString(values["Claimant name"], 300),
            InstructionFieldEngine.TypedString(values["Claim reference"], 100), InstructionFieldEngine.NormalizeRegistration(values["Vehicle registration"]),
            null, null, null, null, InstructionFieldEngine.ParseDate(values["Incident date"]), null);
        var evidence = new List<IntakeEvidence>(extracted) { new(IntakeEvidenceSource.Sender, IntakeEvidenceStrength.Strong, IntakeEvidenceFinding.SupportsPrincipal, "established-principal", $"Principal KMR was established by {principalContext.PolicyKey} v{principalContext.PolicyVersion}.") };
        return new(InstructionPolicyApplicability.Applicable, evidence, fields, draft, missing, Key, Version);
    }

    private static IEnumerable<IntakeContentFragment> InstructionFields(IntakeContentFragment fragment)
    {
        var text = fragment.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var salutation = SalutationRegex().Match(text);
        if (!salutation.Success) yield break;
        var closing = ClosingRegex().Match(text, salutation.Index);
        if (!closing.Success) yield break;
        foreach (Match match in ReferenceRegex().Matches(text[..salutation.Index])) yield return Label(fragment, "Our Reference", match.Groups["value"].Value);
        var body = text[salutation.Index..closing.Index];
        foreach (var (regex, label) in BodyFields) foreach (Match match in regex.Matches(body)) yield return Label(fragment, label, match.Groups["value"].Value);
    }
    private static readonly (Regex Regex, string Label)[] BodyFields = [(ClaimantRegex(), "Client"), (RegistrationRegex(), "registration number"), (AccidentRegex(), "Accident")];
    private static IntakeContentFragment Label(IntakeContentFragment origin, string label, string value) => origin with { Text = $"{label}: {Clean(value)}" };
    private static string Clean(string value) => WhitespaceRegex().Replace(value, " ").Trim();
    [GeneratedRegex(@"(?im)^[^\S\n]*Dear[^\S\n]+Sirs,?[^\S\n]*$", RegexOptions.CultureInvariant, 100)] private static partial Regex SalutationRegex();
    [GeneratedRegex(@"(?im)^[^\S\n]*Yours[^\S\n]+faithfully\b", RegexOptions.CultureInvariant, 100)] private static partial Regex ClosingRegex();
    [GeneratedRegex(@"(?im)^[^\S\n]*Our[^\S\n]+Reference[^\S\n]*:(?<value>[^\n]*)", RegexOptions.CultureInvariant, 100)] private static partial Regex ReferenceRegex();
    // "Client:" prints the name, then on some letters a spaced dash and the
    // vehicle's registration.
    [GeneratedRegex(@"(?im)^[^\S\n]*Client[^\S\n]*:(?<value>[^\n]*?)(?:[^\S\n]+[-–—][^\S\n]|[^\S\n]*$)", RegexOptions.CultureInvariant, 100)] private static partial Regex ClaimantRegex();
    // The registration is printed in the letter's prose, not on a labelled line.
    [GeneratedRegex(@"(?i)\bregistration\s+number\s+(?<value>[^\n.]*)", RegexOptions.CultureInvariant, 100)] private static partial Regex RegistrationRegex();
    [GeneratedRegex(@"(?im)^[^\S\n]*Accident[^\S\n]*:(?<value>[^\n]*)", RegexOptions.CultureInvariant, 100)] private static partial Regex AccidentRegex();
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 100)] private static partial Regex WhitespaceRegex();
}
