using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

/// <summary>
/// Reads QC Law instructions whose compact detail block may lose whitespace
/// between a label and its value without borrowing dates or references from
/// neighbouring document metadata.
/// </summary>
public sealed partial class QclInstructionExtractionPolicy
    : IInstructionExtractionPolicy, IInstructionDocumentProfile, IInstructionFieldRoles
{
    public const string Key = "qcl_instruction";
    public const int Version = 1;
    public const string SupportedPrincipalCode = "QCL";
    public const string DocumentProfileKeyValue = "qcl_instruction_document";
    public const int DocumentProfileVersionValue = 1;

    private const string BoxReferenceField = "Box reference";
    private const string ReportDeadlineField = "Report deadline";
    private const string IssuerField = "Document issuer";
    private const string IntermediaryField = "Intermediary";

    private static readonly InstructionFieldEngine.FieldDefinition[] Definitions =
    [
        new("Claimant name", ["Claimant"], PartyRole: "claimant"),
        new("Claim reference", ["Principal reference"], PartyRole: "principal", ReferenceRole: "principal"),
        new("Vehicle registration", ["Claimant registration"],
            IsValidTyped: InstructionFieldEngine.IsUkRegistration,
            CanonicalValue: InstructionFieldEngine.NormalizeRegistration,
            PartyRole: "claimant"),
        new("Vehicle make", ["Claimant vehicle"],
            AcceptsValue: InstructionFieldEngine.IsPlausibleVehicleMakeModel,
            PartyRole: "claimant"),
        new("Vehicle model", ["Claimant model"], IsRequired: false,
            AcceptsValue: InstructionFieldEngine.IsPlausibleVehicleMakeModel,
            PartyRole: "claimant"),
        new("Vehicle mileage", ["Mileage"], IsRequired: false,
            IsValidTyped: value => InstructionFieldEngine.ParseMileage(value) is not null,
            PartyRole: "claimant"),
        new("Incident date", ["Accident date"],
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate,
            PartyRole: "claimant"),
        new("Instruction date", ["Header date"], IsRequired: false,
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate,
            PartyRole: "instruction"),
        new("Inspection address", ["Inspection location"], IsRequired: false, PartyRole: "inspection-location"),
        new("Inspection date", ["Completed inspection date"], IsRequired: false,
            IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null,
            CanonicalValue: InstructionFieldEngine.CanonicalDate,
            PartyRole: "inspection"),
        new("Accident circumstances", ["Accident circumstances"], IsRequired: false, PartyRole: "claimant"),
        new("VAT status", ["VAT status"], IsRequired: false, PartyRole: "claimant"),
        new(BoxReferenceField, [BoxReferenceField], IsRequired: false,
            PartyRole: "box", ReferenceRole: "box"),
        new(ReportDeadlineField, [ReportDeadlineField], IsRequired: false, PartyRole: "deadline"),
        new(IssuerField, [IssuerField], IsRequired: false, PartyRole: "issuer"),
        new(IntermediaryField, [IntermediaryField], IsRequired: false, PartyRole: "intermediary")
    ];

    private static readonly InstructionFieldEngine.LabelRegexCache Cache = new(Definitions);

    public string PrincipalCode => SupportedPrincipalCode;
    public string DocumentProfileKey => DocumentProfileKeyValue;
    public int DocumentProfileVersion => DocumentProfileVersionValue;
    public InstructionDocumentSignature Signature => new(
        InstructionDocumentSignature.InstructionRole,
        ["qc-law", "Vehicle reg", "Make"],
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
            throw new ArgumentException("QCL extraction requires complete readable content.", nameof(readResult));
        if (!string.Equals(principalContext.PrincipalCode, SupportedPrincipalCode, StringComparison.Ordinal))
            throw new ArgumentException("The established principal is not QCL.", nameof(principalContext));

        var scoped = readResult.Content.SelectMany(InstructionFields).ToArray();
        var (fields, missing, fieldEvidence) = InstructionFieldEngine.ExtractFields(
            scoped,
            Definitions,
            Cache,
            processedAtUtc);
        var values = fields.ToDictionary(field => field.Name, field => field.SuggestedValue, StringComparer.Ordinal);
        var draft = new InstructionDraft(
            SupportedPrincipalCode,
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
                $"Principal QCL was established by {principalContext.PolicyKey} v{principalContext.PolicyVersion}.")
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
        var instruction = InstructionBlock(text);
        if (instruction is null)
            yield break;

        foreach (Match match in ClaimantRegex().Matches(instruction.Value.Text))
            yield return Labelled(fragment, "Claimant", match.Groups["value"].Value);
        foreach (Match match in ReferenceRegex().Matches(text[..instruction.Value.Start]))
            yield return Labelled(fragment, "Principal reference", match.Groups["value"].Value);
        foreach (Match match in RegistrationRegex().Matches(instruction.Value.Text))
            yield return Labelled(fragment, "Claimant registration", match.Groups["value"].Value);
        foreach (Match match in MakeRegex().Matches(instruction.Value.Text))
            yield return Labelled(fragment, "Claimant vehicle", match.Groups["value"].Value);
        foreach (Match match in ModelRegex().Matches(instruction.Value.Text))
            yield return Labelled(fragment, "Claimant model", match.Groups["value"].Value);
        foreach (Match match in AccidentDateRegex().Matches(instruction.Value.Text))
            yield return Labelled(fragment, "Accident date", match.Groups["value"].Value);
        foreach (Match match in HeaderDateRegex().Matches(text[..instruction.Value.Start]))
            yield return Labelled(fragment, "Header date", match.Groups["value"].Value);
        foreach (Match match in LocationRegex().Matches(instruction.Value.Text))
            yield return Labelled(fragment, "Inspection location", match.Groups["value"].Value);
        foreach (Match match in MileageRegex().Matches(instruction.Value.Text))
            yield return Labelled(fragment, "Mileage", match.Groups["value"].Value);
        foreach (Match match in CircumstancesRegex().Matches(instruction.Value.Text))
            yield return Labelled(fragment, "Accident circumstances", match.Groups["value"].Value);
        foreach (Match match in ExplicitInspectionDateRegex().Matches(instruction.Value.Text))
            yield return Labelled(fragment, "Completed inspection date", match.Groups["value"].Value);
        foreach (Match match in BoxReferenceRegex().Matches(text))
            yield return Labelled(fragment, BoxReferenceField, match.Groups["value"].Value);
        foreach (Match match in ReportDeadlineRegex().Matches(text))
            yield return Labelled(fragment, ReportDeadlineField, match.Groups["value"].Value);
        if (IssuerRegex().IsMatch(text))
            yield return Labelled(fragment, IssuerField, "QC Law");
        if (IntermediaryRegex().IsMatch(text[..instruction.Value.Start]))
            yield return Labelled(fragment, IntermediaryField, "Complex Reports");
    }

    private static (int Start, string Text)? InstructionBlock(string text)
    {
        var start = DearSirsRegex().Match(text);
        if (!start.Success)
            return null;
        var end = InstructionEndRegex().Match(text, start.Index);
        return (start.Index, end.Success ? text[start.Index..end.Index] : text[start.Index..]);
    }

    private static IntakeContentFragment Labelled(
        IntakeContentFragment origin,
        string label,
        string value) =>
        origin with { Text = $"{label}: {Clean(value)}" };

    private static string Clean(string value) => WhitespaceRegex().Replace(value, " ").Trim(' ', ':');

    [GeneratedRegex(@"(?im)^\s*Dear\s+Sirs\b", RegexOptions.CultureInvariant, 100)]
    private static partial Regex DearSirsRegex();

    [GeneratedRegex(@"(?im)^\s*Yours\s+Faithfully\b", RegexOptions.CultureInvariant, 100)]
    private static partial Regex InstructionEndRegex();

    [GeneratedRegex(@"(?im)^\s*Re\s*:\s*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ClaimantRegex();

    [GeneratedRegex(@"(?im)^\s*Our\s+Ref\s*:\s*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ReferenceRegex();

    [GeneratedRegex(@"(?im)^\s*Vehicle\s+reg\s*:?[ \t]*(?<value>[A-Z]{2}\s?\d{2}\s?[A-Z]{3})", RegexOptions.CultureInvariant, 100)]
    private static partial Regex RegistrationRegex();

    [GeneratedRegex(@"(?im)^\s*Make\s*:?[ \t]*(?<value>.+?)(?=\s+Model\s*:|\s+Location\s*:|\s+Contact\s+no\s*:|$)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex MakeRegex();

    [GeneratedRegex(@"(?im)^\s*Model\s*:?[ \t]*(?<value>.+?)(?=\s+Location\s*:|\s+Contact\s+no\s*:|$)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ModelRegex();

    [GeneratedRegex(@"(?im)^\s*Acc\s+date\s*:?[ \t]*(?<value>\d{1,2}[-/]\p{L}{3,9}[-/]\d{4}|\d{1,2}/\d{1,2}/\d{4})", RegexOptions.CultureInvariant, 100)]
    private static partial Regex AccidentDateRegex();

    [GeneratedRegex(@"(?im)^\s*Date\s*:\s*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex HeaderDateRegex();

    [GeneratedRegex(@"(?im)^\s*Location\s*:?[ \t]*(?<value>.+?)(?=\s+Contact\s+no\s*:|$)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex LocationRegex();

    [GeneratedRegex(@"(?im)^\s*Mileage\s*:?[ \t]*(?<value>[\d,]+(?:\s*(?:miles?|mi))?)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex MileageRegex();

    [GeneratedRegex(@"(?im)^\s*(?:Accident\s+)?Circumstances?\s*:\s*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex CircumstancesRegex();

    [GeneratedRegex(@"(?im)^\s*(?:Completed|Appointed)\s+Inspection\s+Date\s*:\s*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ExplicitInspectionDateRegex();

    [GeneratedRegex(@"(?im)^\s*Box\s+(?:Ref|Reference)\s*:\s*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex BoxReferenceRegex();

    [GeneratedRegex(@"(?im)^\s*Report\s+Due\s+on\s*:\s*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ReportDeadlineRegex();

    [GeneratedRegex(@"(?im)^\s*(?:Yours\s+Faithfully\s*)?QC\s+Law\s*$|qc-law\.co\.uk", RegexOptions.CultureInvariant, 100)]
    private static partial Regex IssuerRegex();

    [GeneratedRegex(@"(?im)^\s*Complex\s+Reports\s*$|Address\s*:\s*Complex\s+Reports", RegexOptions.CultureInvariant, 100)]
    private static partial Regex IntermediaryRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 100)]
    private static partial Regex WhitespaceRegex();
}
