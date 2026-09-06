namespace Pegasus.Core.Intake;

public sealed class DfdInstructionExtractionPolicy
    : IInstructionExtractionPolicy, IInstructionDocumentProfile, IInstructionFieldRoles
{
    public const string Key = "dfd_instruction";
    public const int Version = 1;
    public const string SupportedPrincipalCode = "DFD";
    public const string DocumentProfileKeyValue = "dfd_instruction_document";

    private static readonly InstructionFieldEngine.FieldDefinition[] Definitions =
    [
        new("Claim reference", ["Your Reference"], FormFields: ["Text4"], PartyRole: "principal", ReferenceRole: "principal"),
        new("Instruction date", ["Date instructed"], FormFields: ["Text5"], IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null, CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "instruction"),
        new("Claimant name", ["Client name or vehicle owner"], FormFields: ["Text6"], PartyRole: "claimant"),
        new("Incident date", ["Accident date"], FormFields: ["Text7"], IsValidTyped: value => InstructionFieldEngine.ParseDate(value) is not null, CanonicalValue: InstructionFieldEngine.CanonicalDate, PartyRole: "claimant"),
        new("Vehicle registration", ["Registration number"], FormFields: ["Text8"], IsValidTyped: InstructionFieldEngine.IsUkRegistration, CanonicalValue: InstructionFieldEngine.NormalizeRegistration, PartyRole: "claimant"),
        new("Claimant contact", ["Client contact number"], FormFields: ["Text9"], IsRequired: false, PartyRole: "claimant"),
        new("Inspection address", ["Vehicle location"], FormFields: ["Text10"], IsRequired: false, PartyRole: "inspection-location"),
        new("Accident circumstances", ["Accident circumstances"], FormFields: ["Text11"], IsRequired: false, PartyRole: "claimant"),
        new("Area of damage", ["Area of damage"], FormFields: ["Text12"], IsRequired: false, PartyRole: "claimant"),
        new("Claim source", ["Claim source"], FormFields: ["Text13"], IsRequired: false, PartyRole: "introducer"),
        new("Contact details if not client", ["Contact details if not client"], FormFields: ["Text14"], IsRequired: false, PartyRole: "contact"),
        new("Additional information", ["Additional information"], FormFields: ["Text15", "Text16", "Text17"], IsRequired: false, PartyRole: "instruction"),
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
    public IReadOnlyDictionary<string, InstructionFieldRole> FieldRoles { get; } = Definitions.ToDictionary(
        item => item.Name, item => new InstructionFieldRole(item.PartyRole, item.ReferenceRole), StringComparer.Ordinal);

    public InstructionExtractionResult Extract(IntakeSourceReadResult readResult, DateTimeOffset processedAtUtc, EstablishedPrincipalContext principalContext)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        ArgumentNullException.ThrowIfNull(principalContext);
        if (readResult.Status != IntakeSourceReadStatus.Readable || readResult.IsIncomplete)
            throw new ArgumentException("DFD extraction requires complete readable content.", nameof(readResult));
        if (!string.Equals(principalContext.PrincipalCode, SupportedPrincipalCode, StringComparison.Ordinal))
            throw new ArgumentException("The established principal is not DFD.", nameof(principalContext));
        var (fields, missing, extracted) = InstructionFieldEngine.ExtractFields(readResult.Content, Definitions, Cache, processedAtUtc);
        var values = fields.ToDictionary(field => field.Name, field => field.SuggestedValue, StringComparer.Ordinal);
        var draft = new InstructionDraft(SupportedPrincipalCode,
            InstructionFieldEngine.TypedString(values["Claimant name"], 300), InstructionFieldEngine.TypedString(values["Claim reference"], 100),
            InstructionFieldEngine.NormalizeRegistration(values["Vehicle registration"]), InstructionFieldEngine.TypedString(values["Vehicle make"], 100),
            InstructionFieldEngine.TypedString(values["Vehicle model"], 100), InstructionFieldEngine.ParseMileage(values["Vehicle mileage"]),
            InstructionFieldEngine.TypedString(values["Accident circumstances"], 2000), InstructionFieldEngine.ParseDate(values["Incident date"]),
            InstructionFieldEngine.ParseDate(values["Instruction date"]), InstructionFieldEngine.TypedString(values["Inspection address"], 1000),
            InstructionFieldEngine.ParseDate(values["Inspection date"]), null, InstructionFieldEngine.TypedString(values["VAT status"], 100), null, null);
        var evidence = new List<IntakeEvidence>(extracted)
        {
            new(IntakeEvidenceSource.Sender, IntakeEvidenceStrength.Strong, IntakeEvidenceFinding.SupportsPrincipal,
                "established-principal", $"Principal DFD was established by {principalContext.PolicyKey} v{principalContext.PolicyVersion}.")
        };
        return new(InstructionPolicyApplicability.Applicable, evidence, fields, draft, missing, Key, Version);
    }
}
