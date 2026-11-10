namespace Pegasus.Core.Intake;

/// <summary>
/// Davison Flynn Duke Solicitors' instruction: the attached PDF form titled
/// "Engineer instruction request", read by its form-field names. Version 2
/// reads the live template as the receipts of 6 and 9 November 2026 carry it:
/// the accident date is form field <c>Text16</c> and the date instructed
/// <c>Text17</c>, which version 1 gave to Additional information while
/// reading the incident date from a <c>Text7</c> the form no longer has; and
/// the printed line "(Below is optional)" beneath the blank contact field is
/// a boundary, not the claimant's contact (operator, 9 November 2026, R774).
/// <c>Text7</c> stays for the earlier template the original mapping described.
/// </summary>
public sealed class DfdInstructionExtractionPolicy
    : IInstructionExtractionPolicy, IInstructionDocumentProfile, IInstructionFieldRoles
{
    public const string Key = "dfd_instruction";
    public const int Version = 2;
    public const string SupportedPrincipalCode = "DFD";
    public const string DocumentProfileKeyValue = "dfd_instruction_document";
    private const string DateInstructedBoundary = "DFD date instructed boundary";
    private const string OptionalSectionBoundary = "DFD optional section boundary";
    private static readonly string[] Boundaries = [DateInstructedBoundary, OptionalSectionBoundary];

    private static readonly InstructionFieldEngine.FieldDefinition[] Definitions =
    [
        new("Claim reference", ["Your Reference"], FormFields: ["Text4"], PartyRole: "principal", ReferenceRole: "principal"),
        new(DateInstructedBoundary, ["Date instructed"], IsRequired: false, AcceptsValue: _ => false, FormFields: ["Text17"]),
        new("Claimant name", ["Client name or vehicle owner"], FormFields: ["Text6"], PartyRole: "claimant"),
        new("Incident date", ["Accident date"], FormFields: ["Text16", "Text7"], IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null, CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "claimant"),
        new("Vehicle registration", ["Registration number"], FormFields: ["Text8"], IsValidTyped: InstructionFieldEngine.IsUkRegistration, CanonicalValue: InstructionFieldEngine.NormalizeRegistration, PartyRole: "claimant"),
        new("Claimant contact", ["Client contact number"], FormFields: ["Text9"], IsRequired: false, PartyRole: "claimant"),
        new(OptionalSectionBoundary, ["(Below is optional)"], IsRequired: false, AcceptsValue: _ => false),
        new("Inspection address", ["Vehicle location"], FormFields: ["Text10"], IsRequired: false, PartyRole: "inspection-location"),
        new("Accident circumstances", ["Accident circumstances"], FormFields: ["Text11"], IsRequired: false, PartyRole: "claimant"),
        new("Area of damage", ["Area of damage"], FormFields: ["Text12"], IsRequired: false, PartyRole: "claimant"),
        new("Claim source", ["Claim source"], FormFields: ["Text13"], IsRequired: false, PartyRole: "introducer"),
        new("Contact details if not client", ["Contact details if not client"], FormFields: ["Text14"], IsRequired: false, PartyRole: "contact"),
        new("Additional information", ["Additional information"], FormFields: ["Text15"], IsRequired: false, PartyRole: "instruction"),
        new("Vehicle make", ["Vehicle make"], FormFields: ["Vehicle make"], IsRequired: false, PartyRole: "claimant"),
        new("Vehicle model", ["Vehicle model"], FormFields: ["Vehicle model"], IsRequired: false, PartyRole: "claimant"),
        new("Vehicle mileage", ["Mileage"], FormFields: ["Mileage"], IsRequired: false, IsValidTyped: value => InstructionFieldEngine.ParseMileage(value) is not null, PartyRole: "claimant"),
        new("Inspection date", ["Completed inspection date"], FormFields: ["Completed inspection date", "Appointed inspection date"], IsRequired: false, IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null, CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "inspection"),
        new("VAT status", ["VAT status"], FormFields: ["VAT status"], IsRequired: false, PartyRole: "claimant")
    ];
    private static readonly InstructionFieldEngine.LabelRegexCache Cache = new(Definitions);

    public string PrincipalCode => SupportedPrincipalCode;
    public string DocumentProfileKey => DocumentProfileKeyValue;
    public int DocumentProfileVersion => 1;
    public InstructionDocumentSignature Signature => new(InstructionDocumentSignature.InstructionRole,
        ["Davison Flynn Duke Solicitors", "Registration number", "Client name or vehicle owner"],
        ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"]);
    public IReadOnlyDictionary<string, InstructionFieldRole> FieldRoles { get; } = Definitions.Where(item => !IsBoundary(item.Name)).ToDictionary(
        item => item.Name, item => new InstructionFieldRole(item.PartyRole, item.ReferenceRole), StringComparer.Ordinal);

    public InstructionExtractionResult Extract(IntakeSourceReadResult readResult, InstructionExtractionTiming timing, EstablishedPrincipalContext principalContext)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        ArgumentNullException.ThrowIfNull(principalContext);
        if (readResult.Status != IntakeSourceReadStatus.Readable || readResult.IsIncomplete)
            throw new ArgumentException("DFD extraction requires complete readable content.", nameof(readResult));
        if (!string.Equals(principalContext.PrincipalCode, SupportedPrincipalCode, StringComparison.Ordinal))
            throw new ArgumentException("The established principal is not DFD.", nameof(principalContext));
        var (fields, missing, extracted) = InstructionFieldEngine.ExtractFields(readResult.Content, Definitions, Cache, timing);
        fields = [.. fields.Where(field => !IsBoundary(field.Name))];
        var values = fields.ToDictionary(field => field.Name, field => field.SuggestedValue, StringComparer.Ordinal);
        var draft = new InstructionDraft(SupportedPrincipalCode,
            InstructionFieldEngine.TypedString(values["Claimant name"], 300), InstructionFieldEngine.TypedString(values["Claim reference"], 100),
            InstructionFieldEngine.NormalizeRegistration(values["Vehicle registration"]), InstructionFieldEngine.TypedString(values["Vehicle make"], 100),
            InstructionFieldEngine.TypedString(values["Vehicle model"], 100), InstructionFieldEngine.ParseMileage(values["Vehicle mileage"]),
            InstructionFieldEngine.TypedString(values["Accident circumstances"], 2000), InstructionFieldEngine.ParseDate(values["Incident date"]),
            InstructionFieldEngine.TypedString(values["Inspection address"], 1000),
            InstructionFieldEngine.ParseDate(values["Inspection date"]), null, InstructionFieldEngine.TypedString(values["VAT status"], 100), null, null);
        var evidence = new List<IntakeEvidence>(extracted)
        {
            new(IntakeEvidenceSource.Sender, IntakeEvidenceStrength.Strong, IntakeEvidenceFinding.SupportsPrincipal,
                "established-principal", $"Principal DFD was established by {principalContext.PolicyKey} v{principalContext.PolicyVersion}.")
        };
        return new(InstructionPolicyApplicability.Applicable, evidence, fields, draft, missing, Key, Version);
    }

    /// <summary>
    /// A boundary definition exists so the engine stops a neighbouring value at
    /// the line it names; it is never a field of the instruction.
    /// </summary>
    private static bool IsBoundary(string name) => Boundaries.Contains(name, StringComparer.Ordinal);
}
