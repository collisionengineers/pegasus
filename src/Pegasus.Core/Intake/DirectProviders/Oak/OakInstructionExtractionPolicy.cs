using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

/// <summary>
/// Reads Oakwood instructions, including the compound header table whose
/// labels and values must remain aligned before reference or date is trusted.
/// </summary>
public sealed partial class OakInstructionExtractionPolicy
    : IInstructionExtractionPolicy, IInstructionDocumentProfile, IInstructionFieldRoles
{
    public const string Key = "oak_instruction";
    public const int Version = 1;
    public const string SupportedPrincipalCode = "OAK";
    public const string DocumentProfileKeyValue = "oak_instruction_document";
    public const int DocumentProfileVersionValue = 1;

    private const string HeaderAlignmentField = "Aligned header values";
    private const string SourceField = "Source";
    private const string IntroducerField = "Introducer";
    private const string RequestedWorkField = "Requested work";

    private static readonly InstructionFieldEngine.FieldDefinition[] Definitions =
    [
        new(HeaderAlignmentField, ["Our Ref: Your Ref: Date:"], IsRequired: false),
        new("Claimant name", ["Our Client"], PartyRole: "claimant"),
        new("Vehicle registration", ["Client reg"],
            IsValidTyped: InstructionFieldEngine.IsUkRegistration,
            CanonicalValue: InstructionFieldEngine.NormalizeRegistration,
            PartyRole: "claimant"),
        new("Vehicle model", ["Client model"], IsRequired: false,
            AcceptsValue: IsUsableModel,
            PartyRole: "claimant"),
        new("Vehicle mileage", ["Mileage"], IsRequired: false,
            IsValidTyped: value => InstructionFieldEngine.ParseMileage(value) is not null,
            PartyRole: "claimant"),
        new("Incident date", ["Accident"],
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate,
            PartyRole: "claimant"),
        new("Inspection address", ["Inspection address"], IsRequired: false, PartyRole: "inspection-location"),
        new("Inspection date", ["Completed inspection date"], IsRequired: false,
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate,
            PartyRole: "inspection"),
        new("Accident circumstances", ["Accident circumstances"], IsRequired: false, PartyRole: "claimant"),
        new("VAT status", ["VAT status"], IsRequired: false, PartyRole: "claimant"),
        new(SourceField, [SourceField], IsRequired: false, PartyRole: "source"),
        new(IntroducerField, [IntroducerField], IsRequired: false, PartyRole: "introducer"),
        new(RequestedWorkField, [RequestedWorkField], IsRequired: false, PartyRole: "requested-work")
    ];

    private static readonly InstructionFieldEngine.LabelRegexCache Cache = new(Definitions);

    public string PrincipalCode => SupportedPrincipalCode;
    public string DocumentProfileKey => DocumentProfileKeyValue;
    public int DocumentProfileVersion => DocumentProfileVersionValue;
    public InstructionDocumentSignature Signature => new(
        InstructionDocumentSignature.InstructionRole,
        ["Oakwood", "Client reg:", "Client model:"],
        ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"]);

    public IReadOnlyDictionary<string, InstructionFieldRole> FieldRoles { get; } = BuildFieldRoles();

    public InstructionExtractionResult Extract(
        IntakeSourceReadResult readResult,
        DateTimeOffset processedAtUtc,
        EstablishedPrincipalContext principalContext)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        ArgumentNullException.ThrowIfNull(principalContext);
        if (readResult.Status != IntakeSourceReadStatus.Readable || readResult.IsIncomplete)
            throw new ArgumentException("OAK extraction requires complete readable content.", nameof(readResult));
        if (!string.Equals(principalContext.PrincipalCode, SupportedPrincipalCode, StringComparison.Ordinal))
            throw new ArgumentException("The established principal is not OAK.", nameof(principalContext));

        var scoped = readResult.Content.SelectMany(InstructionFields).ToArray();
        var (extracted, engineMissing, fieldEvidence) = InstructionFieldEngine.ExtractFields(
            scoped,
            Definitions,
            Cache,
            processedAtUtc);
        var header = AssertAlignedHeader(readResult.Content);
        var fields = extracted.Where(field => field.Name != HeaderAlignmentField).ToList();
        fields.Add(HeaderField("Claim reference", header.Reference, header.Candidate));
        fields.Add(HeaderField("Instruction date", header.Date, header.Candidate));
        var missing = engineMissing.Where(name => name != HeaderAlignmentField).ToList();
        if (header.Reference is null)
            missing.Add("Claim reference");
        if (header.Date is null)
            missing.Add("Instruction date");

        var values = fields.ToDictionary(field => field.Name, field => field.SuggestedValue, StringComparer.Ordinal);
        var draft = new InstructionDraft(
            SupportedPrincipalCode,
            InstructionFieldEngine.TypedString(values["Claimant name"], 300),
            InstructionFieldEngine.TypedString(values["Claim reference"], 100),
            InstructionFieldEngine.NormalizeRegistration(values["Vehicle registration"]),
            null,
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
        var evidence = new List<IntakeEvidence>(fieldEvidence.Where(item => item.Signal != HeaderAlignmentField));
        AddHeaderEvidence(evidence, "Claim reference", header.Reference, header.Candidate);
        AddHeaderEvidence(evidence, "Instruction date", header.Date, header.Candidate);
        evidence.Add(new(
            IntakeEvidenceSource.Sender,
            IntakeEvidenceStrength.Strong,
            IntakeEvidenceFinding.SupportsPrincipal,
            "established-principal",
            $"Principal OAK was established by {principalContext.PolicyKey} v{principalContext.PolicyVersion}."));

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
        if (fragment.Locator?.Kind == IntakeLocatorKind.TableCell)
        {
            yield return fragment;
            yield break;
        }

        var text = fragment.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var start = text.IndexOf("Dear Sirs", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            yield break;
        var instruction = text[start..];

        foreach (Match match in ClaimantRegex().Matches(instruction))
            yield return Labelled(fragment, "Our Client", match.Groups["value"].Value);
        foreach (Match match in RegistrationRegex().Matches(instruction))
            yield return Labelled(fragment, "Client reg", match.Groups["value"].Value);
        foreach (Match match in ModelRegex().Matches(instruction))
        {
            var model = Clean(match.Groups["value"].Value);
            if (IsUsableModel(model))
                yield return Labelled(fragment, "Client model", model);
        }
        foreach (Match match in IncidentDateRegex().Matches(instruction))
            yield return Labelled(fragment, "Accident", match.Groups["value"].Value);
        foreach (Match match in SourceRegex().Matches(instruction))
            yield return Labelled(fragment, SourceField, match.Groups["value"].Value);
        foreach (Match match in IntroducerRegex().Matches(instruction))
            yield return Labelled(fragment, IntroducerField, match.Groups["value"].Value);
        foreach (Match match in CircumstancesRegex().Matches(instruction))
            yield return Labelled(fragment, "Accident circumstances", match.Groups["value"].Value);
        if (InspectionAddress(instruction) is { } address)
            yield return Labelled(fragment, "Inspection address", address);
        foreach (Match match in MileageRegex().Matches(instruction))
            yield return Labelled(fragment, "Mileage", match.Groups["value"].Value);
        foreach (Match match in ExplicitInspectionDateRegex().Matches(instruction))
            yield return Labelled(fragment, "Completed inspection date", match.Groups["value"].Value);
        foreach (Match match in VatStatusRegex().Matches(instruction))
            yield return Labelled(fragment, "VAT status", match.Groups["value"].Value);
        foreach (Match match in RequestedWorkRegex().Matches(instruction))
            yield return Labelled(fragment, RequestedWorkField, match.Groups["value"].Value);
    }

    private static (string? Reference, string? Date, InstructionFieldCandidate? Candidate) AssertAlignedHeader(
        IReadOnlyList<IntakeContentFragment> fragments)
    {
        var labels = fragments
            .Where(fragment => fragment.Locator is
                { Kind: IntakeLocatorKind.TableCell, Table: { }, Row: { }, Column: { } })
            .Where(fragment => string.Equals(
                HeaderCellText(fragment.Text),
                HeaderCellText("Our Ref: Your Ref: Date:"),
                StringComparison.Ordinal))
            .ToArray();
        if (labels.Length != 1)
            return (null, null, null);

        var label = labels[0];
        var locator = label.Locator!;
        var values = fragments.Where(fragment => fragment.Locator is
            {
                Kind: IntakeLocatorKind.TableCell,
                Table: { } table,
                Row: { } row,
                Column: { } column
            }
            && table == locator.Table
            && row == locator.Row
            && column == locator.Column + 1
            && !string.IsNullOrWhiteSpace(fragment.Text)).ToArray();
        if (values.Length != 1)
            return (null, null, null);

        var value = values[0];
        var normalizedValue = WhitespaceRegex().Replace(value.Text, " ").Trim();
        var candidate = new InstructionFieldCandidate(
            normalizedValue,
            value.Source,
            value.SourceLabel,
            value.Locator,
            string.Equals(normalizedValue, value.Text, StringComparison.Ordinal) ? null : value.Text);
        var match = HeaderValuesRegex().Match(candidate.Value);
        if (!match.Success)
            return (null, null, candidate);
        var reference = Clean(match.Groups["reference"].Value);
        var date = Clean(match.Groups["date"].Value);
        return (
            reference.Length == 0 ? null : reference,
            InstructionFieldEngine.ParseDate(date) is null ? null : date,
            candidate);
    }

    private static string HeaderCellText(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static InstructionReviewField HeaderField(
        string name,
        string? value,
        InstructionFieldCandidate? aligned) =>
        new(
            name,
            value,
            value is null || aligned is null
                ? []
                : [aligned with { Value = value, RawValue = aligned.SourceValue }],
            IsDefaulted: false,
            HasConflict: false);

    private static void AddHeaderEvidence(
        List<IntakeEvidence> evidence,
        string field,
        string? value,
        InstructionFieldCandidate? candidate) =>
        evidence.Add(value is not null && candidate is not null
            ? new(
                candidate.Source,
                IntakeEvidenceStrength.Strong,
                IntakeEvidenceFinding.ExtractedField,
                field,
                $"{field} was read from the aligned Oakwood header value cell.")
            : new(
                IntakeEvidenceSource.SystemDefault,
                IntakeEvidenceStrength.Strong,
                IntakeEvidenceFinding.MissingField,
                field,
                $"No aligned Oakwood {field.ToLowerInvariant()} was available."));

    private static Dictionary<string, InstructionFieldRole> BuildFieldRoles()
    {
        var roles = Definitions.Where(definition => definition.Name != HeaderAlignmentField).ToDictionary(
            definition => definition.Name,
            definition => new InstructionFieldRole(definition.PartyRole, definition.ReferenceRole),
            StringComparer.Ordinal);
        roles.Add("Claim reference", new("principal", "principal"));
        roles.Add("Instruction date", new("instruction", null));
        return roles;
    }

    private static bool IsUsableModel(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Trim(' ', ',').Length > 0
        && InstructionFieldEngine.IsPlausibleVehicleMakeModel(value);

    private static string? InspectionAddress(string instruction)
    {
        var availability = instruction.IndexOf("is available at:", StringComparison.OrdinalIgnoreCase);
        if (availability < 0)
            return null;
        var match = AddressRegex().Match(instruction, availability);
        if (!match.Success)
            return null;
        var lines = match.Groups["value"].Value
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Clean)
            .Where(line => line.Length > 0)
            .ToArray();
        return lines.Length == 0 ? null : string.Join(", ", lines);
    }

    private static IntakeContentFragment Labelled(
        IntakeContentFragment origin,
        string label,
        string value) =>
        origin with { Text = $"{label}: {Clean(value)}" };

    private static string Clean(string value) => WhitespaceRegex().Replace(value, " ").Trim(' ', ':');

    [GeneratedRegex(@"^(?<reference>\S+)(?:\s+\S+)?\s+(?<date>\d{1,2}/\d{1,2}/\d{2,4})\s*$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex HeaderValuesRegex();

    [GeneratedRegex(@"(?im)^\s*Our\s+Client\s*:\s*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ClaimantRegex();

    [GeneratedRegex(@"(?im)^\s*Client\s+reg\s*:\s*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex RegistrationRegex();

    [GeneratedRegex(@"(?im)^\s*Client\s+model\s*:\s*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ModelRegex();

    [GeneratedRegex(@"(?im)^\s*Accident\s*:\s*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex IncidentDateRegex();

    [GeneratedRegex(@"(?im)^\s*Source\s*:\s*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex SourceRegex();

    [GeneratedRegex(@"(?im)^\s*The\s+introducer\s+is\s+called\s+(?<value>[^.\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex IntroducerRegex();

    [GeneratedRegex(@"(?ims)The\s+circumstances\s+of\s+the\s+accident\s+are\s+(?<value>.+?)(?=^\s*Please\s+arrange\b)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex CircumstancesRegex();

    [GeneratedRegex(@"(?ims)^\s*Address\s*:\s*(?<value>.+?)(?=^\s*Mobile\s+Tel\s*:)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex AddressRegex();

    [GeneratedRegex(@"(?im)^\s*Mileage\s*:\s*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex MileageRegex();

    [GeneratedRegex(@"(?im)^\s*(?:Completed|Appointed)\s+Inspection\s+Date\s*:\s*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ExplicitInspectionDateRegex();

    [GeneratedRegex(@"(?im)^\s*VAT\s+(?:status|registered)\s*:\s*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex VatStatusRegex();

    [GeneratedRegex(@"(?im)^\s*(?<value>Please\s+arrange\s+an\s+inspection.+(?:costs?\s+of\s+repair|cost\s+of\s+replacement).*)$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex RequestedWorkRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 100)]
    private static partial Regex WhitespaceRegex();
}
