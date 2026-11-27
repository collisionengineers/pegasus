using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

public sealed partial class MpInstructionExtractionPolicy
    : IInstructionExtractionPolicy, IInstructionDocumentProfile, IInstructionFieldRoles
{
    public const string Key = "mp_instruction";
    public const int Version = 3;
    public const string SupportedPrincipalCode = "MP";
    public const string DocumentProfileKeyValue = "mp_instruction_document";
    private static readonly InstructionFieldEngine.FieldDefinition[] Definitions =
    [
        new("Claimant name", ["Our client"], PartyRole: "claimant"),
        new("Claim reference", ["Our Ref"], PartyRole: "principal", ReferenceRole: "principal"),
        new("Vehicle registration", ["Vehicle Reg"], IsValidTyped: IsSupportedUkRegistration, CanonicalValue: InstructionFieldEngine.NormalizeRegistration, PartyRole: "claimant"),
        new("Vehicle make and model", ["Vehicle description"], PartyRole: "claimant"),
        new("Incident date", ["Date of Accident"], IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null, CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "claimant"),
        new("Inspection address", ["Vehicle inspection address"], IsRequired: false, PartyRole: "inspection-location"),
        new("Accident circumstances", ["Accident circumstances"], IsRequired: false, PartyRole: "claimant"),
        new("Vehicle mileage", ["Mileage"], IsRequired: false, IsValidTyped: value => InstructionFieldEngine.ParseMileage(value) is not null, PartyRole: "claimant"),
        new("Inspection date", ["Completed inspection date"], IsRequired: false, IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null, CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "inspection"),
        new("VAT status", ["VAT status"], IsRequired: false, PartyRole: "claimant")
    ];
    private static readonly InstructionFieldEngine.LabelRegexCache Cache = new(Definitions);
    private static readonly char[] DescriptionEdges = [' ', '\t', ',', '-', '–', '—'];
    public string PrincipalCode => SupportedPrincipalCode;
    public string DocumentProfileKey => DocumentProfileKeyValue;
    public int DocumentProfileVersion => 1;
    public InstructionDocumentSignature Signature => new(InstructionDocumentSignature.InstructionRole,
        ["Please arrange to inspect the above vehicle at your earliest convenience.", "Vehicle Reg:", "Date of Accident:"],
        ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"]);
    public IReadOnlyDictionary<string, InstructionFieldRole> FieldRoles { get; } = Definitions.ToDictionary(item => item.Name, item => new InstructionFieldRole(item.PartyRole, item.ReferenceRole), StringComparer.Ordinal);

public InstructionExtractionResult Extract(IntakeSourceReadResult readResult, InstructionExtractionTiming timing, EstablishedPrincipalContext principalContext)
    {
        ArgumentNullException.ThrowIfNull(readResult); ArgumentNullException.ThrowIfNull(principalContext);
        if (readResult.Status != IntakeSourceReadStatus.Readable || readResult.IsIncomplete) throw new ArgumentException("MP extraction requires complete readable content.", nameof(readResult));
        if (!string.Equals(principalContext.PrincipalCode, SupportedPrincipalCode, StringComparison.Ordinal)) throw new ArgumentException("The established principal is not MP.", nameof(principalContext));
        var (fields, missing, extracted) = InstructionFieldEngine.ExtractFields(readResult.Content.SelectMany(InstructionFields).Concat(SubjectFacts(readResult)).ToArray(), Definitions, Cache, timing);
        var values = fields.ToDictionary(field => field.Name, field => field.SuggestedValue, StringComparer.Ordinal);
        var draft = new InstructionDraft(SupportedPrincipalCode, InstructionFieldEngine.TypedString(values["Claimant name"], 300), InstructionFieldEngine.TypedString(values["Claim reference"], 100),
            InstructionFieldEngine.NormalizeRegistration(values["Vehicle registration"]), InstructionFieldEngine.TypedString(values["Vehicle make and model"], 100), null,
            InstructionFieldEngine.ParseMileage(values["Vehicle mileage"]), InstructionFieldEngine.TypedString(values["Accident circumstances"], 2000), InstructionFieldEngine.ParseDate(values["Incident date"]),
            InstructionFieldEngine.TypedString(values["Inspection address"], 1000), InstructionFieldEngine.ParseDate(values["Inspection date"]),
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
        foreach (Match match in VehicleRegex().Matches(header))
        {
            var (registration, vehicle) = SplitVehicleLine(match.Groups["value"].Value);
            if (registration is not null) yield return Label(fragment, "Vehicle Reg", registration);
            if (vehicle is not null) yield return Label(fragment, "Vehicle description", vehicle);
        }
        foreach (Match match in AccidentRegex().Matches(header)) yield return Label(fragment, "Date of Accident", match.Groups["value"].Value);
        foreach (Match match in LocationRegex().Matches(body)) yield return Label(fragment, "Vehicle inspection address", match.Groups["value"].Value);
        foreach (Match match in CircumstancesRegex().Matches(body)) yield return Label(fragment, "Accident circumstances", match.Groups["value"].Value);
        foreach (Match match in MileageRegex().Matches(body)) yield return Label(fragment, "Mileage", match.Groups["value"].Value);
        foreach (Match match in ExplicitInspectionDateRegex().Matches(body)) yield return Label(fragment, "Completed inspection date", match.Groups["value"].Value);
        foreach (Match match in VatRegex().Matches(body)) yield return Label(fragment, "VAT status", match.Groups["value"].Value);
    }
    // The subject ("... VRM: AB12 CDE   DOA: 03/11/26") is rewritten as labelled
    // lines in one fragment appended after the document's, so a value the document
    // states wins rank-aware conflict resolution. It never selects the policy: the
    // signature reads document text only. MP's own subjects carry no "VRM:" label
    // ("New Instruction - GJ20 XFG - Our Ref RA6459004"), so without one a sole
    // current-format plate in the subject is read instead (R1340).
    private static IEnumerable<IntakeContentFragment> SubjectFacts(IntakeSourceReadResult readResult)
    {
        var subject = readResult.TransportEvidence
            .FirstOrDefault(item => item.Source == IntakeEvidenceSource.Subject && item.SenderIdentityKind == IntakeSenderIdentityKind.Transport)?.Value;
        if (string.IsNullOrWhiteSpace(subject)) yield break;
        var lines = new List<string>();
        var registration = SubjectRegistrationRegex().Match(subject);
        if (registration.Success && IsSupportedUkRegistration(registration.Groups["value"].Value)) lines.Add($"Vehicle Reg: {registration.Groups["value"].Value}");
        else if (SoleCurrentRegistration(subject, SubjectPlateRegex()) is { } plate) lines.Add($"Vehicle Reg: {plate.Value}");
        var accident = SubjectAccidentRegex().Match(subject);
        if (accident.Success)
        {
            var date = $"{accident.Groups["day"].Value}/{accident.Groups["month"].Value}/{accident.Groups["year"].Value}";
            if (InstructionFieldEngine.ParseDate(date) is not null) lines.Add($"Date of Accident: {date}");
        }
        if (lines.Count > 0) yield return new(IntakeEvidenceSource.Subject, "message subject", string.Join('\n', lines));
    }
    // MP's vehicle line puts the registration anywhere: "LV67 UDJ, Jaecoo 4 (Private Hire)",
    // "Toyota Prius Plus – DG70 FFU – Private Hire", "Ford Freedom-SF17 CKP Hackney" or the
    // registration alone. Separated parts are tried first: a sole current-format plate, then
    // the first part in any UK shape (the line's original layout, "NEK 1A , Audi A6"). Otherwise
    // only a sole current-format plate is cut out of running text, because a model such as
    // "BMW 530E" has a suffix plate's shape. The description is MP's words either side of the
    // registration, joined with a comma; with no registration it is the parts after the first,
    // as before (operator, 27 November 2026; R1340).
    private static (string? Registration, string? Vehicle) SplitVehicleLine(string value)
    {
        var parts = PartSeparatorRegex().Split(value).Select(part => part.Trim()).ToArray();
        var current = Enumerable.Range(0, parts.Length).Where(index => InstructionFieldEngine.IsCurrentUkRegistration(parts[index])).ToArray();
        var chosen = current.Length == 1 ? current[0] : IsSupportedUkRegistration(parts[0]) ? 0 : -1;
        if (chosen >= 0) return (parts[chosen], Description(parts.Where((_, index) => index != chosen)));
        if (SoleCurrentRegistration(value, LinePlateRegex()) is { } plate)
            return (plate.Value, Description([value[..plate.Index], value[(plate.Index + plate.Length)..]]));
        return (null, Description(parts.Skip(1)));
    }
    private static string? Description(IEnumerable<string> parts)
    {
        var words = parts.Select(part => part.Trim(DescriptionEdges)).Where(part => part.Length > 0).ToArray();
        return words.Length == 0 ? null : string.Join(", ", words);
    }
    private static Match? SoleCurrentRegistration(string text, Regex plates)
    {
        var matches = plates.Matches(text).Where(match => InstructionFieldEngine.IsCurrentUkRegistration(match.Value)).ToArray();
        return matches.Length > 0 && matches.All(match => string.Equals(InstructionFieldEngine.NormalizeRegistration(match.Value), InstructionFieldEngine.NormalizeRegistration(matches[0].Value), StringComparison.Ordinal))
            ? matches[0]
            : null;
    }
    private static IntakeContentFragment Label(IntakeContentFragment origin, string label, string value) => origin with { Text = $"{label}: {WhitespaceRegex().Replace(value, " ").Trim()}" };
    private static bool IsSupportedUkRegistration(string value) => InstructionFieldEngine.IsUkRegistration(value) && Regex.Replace(value, @"[\s-]", string.Empty, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)).Length <= 7;
    [GeneratedRegex(@"Please\s+arrange\s+to\s+inspect\s+the\s+above\s+vehicle\s+at\s+your\s+earliest\s+convenience\.", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)] private static partial Regex RequestRegex();
    [GeneratedRegex(@"(?im)^\s*Our\s+client\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex ClaimantRegex();
    [GeneratedRegex(@"(?im)^\s*Our\s+Ref\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex ReferenceRegex();
    [GeneratedRegex(@"(?im)^\s*Vehicle\s+Reg\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex VehicleRegex();
    [GeneratedRegex(@",|[–—]|\s-\s", RegexOptions.CultureInvariant, 100)] private static partial Regex PartSeparatorRegex();
    [GeneratedRegex(@"(?<![A-Za-z0-9])[A-Za-z]{2}[0-9]{2} ?[A-Za-z]{3}(?![A-Za-z0-9])", RegexOptions.CultureInvariant, 100)] private static partial Regex LinePlateRegex();
    [GeneratedRegex(@"(?<![A-Za-z0-9])[A-Z]{2}[0-9]{2} ?[A-Z]{3}(?![A-Za-z0-9])", RegexOptions.CultureInvariant, 100)] private static partial Regex SubjectPlateRegex();
    [GeneratedRegex(@"(?im)^\s*Date\s+of\s+Accident\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex AccidentRegex();
    [GeneratedRegex(@"(?ims)The\s+vehicle\s+is\s+to\s+be\s+inspected\s+at\s*:[ \t]*(?<value>.+?)(?=\n\s*Kind\s+regards)", RegexOptions.CultureInvariant, 100)] private static partial Regex LocationRegex();
    [GeneratedRegex(@"(?ims)^\s*(?:Accident\s+circumstances|Circumstances)\s*:[ \t]*(?<value>.+?)(?=\n\s*(?:The\s+vehicle|Kind\s+regards))", RegexOptions.CultureInvariant, 100)] private static partial Regex CircumstancesRegex();
    [GeneratedRegex(@"(?im)^\s*Mileage\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex MileageRegex();
    [GeneratedRegex(@"(?im)^\s*(?:Completed|Appointed)\s+Inspection\s+Date\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex ExplicitInspectionDateRegex();
    [GeneratedRegex(@"(?im)^\s*VAT\s+(?:status|registered)\s*:[ \t]*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)] private static partial Regex VatRegex();
    [GeneratedRegex(@"\bVRM\s{0,10}:\s{0,10}(?<value>[A-Za-z0-9]{1,4}[ -]?[A-Za-z0-9]{1,4})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)] private static partial Regex SubjectRegistrationRegex();
    [GeneratedRegex(@"\bDOA\s{0,10}:\s{0,10}(?<day>\d{1,2})[/.-](?<month>\d{1,2})[/.-](?<year>\d{4}|\d{2})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)] private static partial Regex SubjectAccidentRegex();
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 100)] private static partial Regex WhitespaceRegex();
}
