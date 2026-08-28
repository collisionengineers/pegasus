using System.Globalization;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Intake;

namespace Pegasus.Core.ProviderApi;

public sealed record ProviderInstructionParty(
    string? Name = null,
    string? EmailAddress = null,
    string? PhoneNumber = null)
{
    public static readonly ProviderInstructionParty Empty = new();
}

public sealed record ProviderInstructionClaimant(
    string? Name = null,
    string? ContactNumber = null,
    string? Address = null)
{
    public static readonly ProviderInstructionClaimant Empty = new();
}

/// <summary>
/// One retained file, as the submission remembers it. The bytes live in the
/// content-addressed artifact store; this is the manifest entry that names them
/// and says what the provider called the file.
/// </summary>
public sealed record ProviderInstructionAsset(
    int Ordinal,
    string FileName,
    string MediaType,
    DocumentSemanticRole? Role,
    string Sha256,
    string StorageKey);

/// <summary>
/// The instruction a provider declared, normalised. Every value here was stated
/// by the authenticated Principal; none was read out of a document.
/// </summary>
public sealed record ProviderInstruction(
    ProviderInstructionKind Kind,
    AuditAssessment? OriginalReportVerdict = null,
    string? DeclaredPrincipalCode = null,
    string? ClaimNumber = null,
    string? ClaimantName = null,
    string? ClaimantContactNumber = null,
    string? ClaimantAddress = null,
    string? FileHandlerName = null,
    string? FileHandlerEmailAddress = null,
    string? FileHandlerPhoneNumber = null,
    string? VehicleRegistration = null,
    string? VehicleMake = null,
    string? VehicleModel = null,
    long? VehicleMileage = null,
    string? VehicleMileageUnit = null,
    DateOnly? DateOfIncident = null,
    string? AccidentCircumstances = null,
    DateOnly? InspectionDateRequested = null,
    string? InspectionAddress = null,
    DateOnly? InstructionDate = null,
    string? VatStatus = null,
    string? Notes = null);

/// <summary>
/// One declared field the provider got wrong, named by its path in the request
/// body so the refusal can say which field and why. This is deliberately not an
/// <see cref="ArgumentException"/>: the fault is in a submitted document's
/// field, not in a method parameter, and reporting it as the latter both
/// misnames the fault and misleads the caller.
/// </summary>
public sealed class ProviderInstructionValidationException(string field, string message)
    : Exception(message)
{
    public string Field { get; } = field;
}

/// <summary>
/// Normalisation, bounds and the draft projection for a declared instruction.
///
/// The bounds are the case store's own, not tighter ones invented for the wire:
/// a contract that refuses a fifty-character claimant name the database would
/// have stored refuses real work. Which fields must be present is likewise not
/// restated here — <see cref="InstructionDraftCompleteness"/> already owns both
/// the required and the identity-critical lists, and callers ask it.
/// </summary>
public static class ProviderInstructionPolicy
{
    public const string PolicyKey = "provider_api_declared_instruction";
    public const int PolicyVersion = 1;

    /// <summary>
    /// The reader identity a declared instruction is recorded under. No file was
    /// parsed to obtain these values and the receipt must not claim one was.
    /// </summary>
    public const string ReaderKey = "provider_api_declaration";
    public const string ReaderVersion = "1";

    /// <summary>The retained source's own name and type: the request as sent.</summary>
    public const string SourceFileName = "instruction.json";
    public const string SourceMediaType = "application/json";

    /// <summary>
    /// The source label the declared original report is retained under. It is a
    /// fixed name rather than an ordinal so that the Audit evidence can find the
    /// report without a second record of which file it was.
    /// </summary>
    public const string OriginalReportSourceLabel = "provider-original-report";

    /// <summary>Every other submitted file, labelled by its declared ordinal.</summary>
    public static string AssetSourceLabel(int ordinal, DocumentSemanticRole? role) =>
        role == DocumentSemanticRole.AuditReport
            ? OriginalReportSourceLabel
            : $"provider-file:{ordinal}";

    public const int MaximumClaimantNameLength = 300;
    public const int MaximumClaimNumberLength = 100;
    public const int MaximumRegistrationLength = 20;
    public const int MaximumVehicleTextLength = 100;
    public const int MaximumMileageUnitLength = 40;
    public const int MaximumCircumstancesLength = 2000;
    public const int MaximumAddressLength = 1000;
    public const int MaximumVatStatusLength = 100;
    public const int MaximumPartyNameLength = 300;
    public const int MaximumEmailLength = 320;
    public const int MaximumPhoneLength = 100;

    /// <summary>The note bound is the case note's own bound, not a second one.</summary>
    public const int MaximumNotesLength = AddCaseNote.MaximumLength;

    /// <summary>
    /// The intake field names the case snapshot looks values up by. They are the
    /// operator's own field labels and must match exactly, so they are named
    /// once here and the review fields are built from this list.
    /// </summary>
    public static class FieldNames
    {
        public const string ClaimantName = "Claimant name";
        public const string ClaimantContactNumber = "Claimant contact number";
        public const string ClaimantAddress = "Claimant address";
        public const string ClaimNumber = "Claim number";
        public const string VehicleRegistration = "Vehicle registration";
        public const string VehicleMake = "Vehicle make";
        public const string VehicleModel = "Vehicle model";
        public const string VehicleMileage = "Vehicle mileage";
        public const string VehicleMileageUnit = "Vehicle mileage unit";
        public const string AccidentCircumstances = "Accident circumstances";
        public const string DateOfIncident = "Date of incident";
        public const string InstructionDate = "Instruction date";
        public const string InspectionDate = "Inspection date";
        public const string InspectionAddress = "Inspection address";
        public const string VatStatus = "VAT status";
        public const string FileHandlerName = "Contact name";
        public const string FileHandlerEmailAddress = "Contact email";
        public const string FileHandlerPhoneNumber = "Contact phone";
    }

    public static ProviderInstruction Normalize(ProviderInstruction instruction)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        if (instruction.VehicleMileage is < 0)
        {
            throw new ProviderInstructionValidationException(
                "vehicle.mileage",
                "The vehicle mileage cannot be negative.");
        }
        if (ProviderInstructionKinds.RequiresOriginalReport(instruction.Kind)
            && instruction.OriginalReportVerdict is null)
        {
            throw new ProviderInstructionValidationException(
                "originalReportVerdict",
                "An Audit instruction must state the original report verdict.");
        }
        if (!ProviderInstructionKinds.RequiresOriginalReport(instruction.Kind)
            && instruction.OriginalReportVerdict is not null)
        {
            throw new ProviderInstructionValidationException(
                "originalReportVerdict",
                "Only an Audit instruction carries an original report verdict.");
        }

        return instruction with
        {
            DeclaredPrincipalCode = Trim(instruction.DeclaredPrincipalCode, CasePrincipalCode.MaximumLength, "principal"),
            ClaimNumber = Trim(instruction.ClaimNumber, MaximumClaimNumberLength, "claimNumber"),
            ClaimantName = Collapse(instruction.ClaimantName, MaximumClaimantNameLength, "claimant.name"),
            ClaimantContactNumber = Trim(instruction.ClaimantContactNumber, MaximumPhoneLength, "claimant.contactNumber"),
            ClaimantAddress = Paragraph(instruction.ClaimantAddress, MaximumAddressLength, "claimant.address"),
            FileHandlerName = Collapse(instruction.FileHandlerName, MaximumPartyNameLength, "fileHandler.name"),
            FileHandlerEmailAddress = Trim(instruction.FileHandlerEmailAddress, MaximumEmailLength, "fileHandler.emailAddress"),
            FileHandlerPhoneNumber = Trim(instruction.FileHandlerPhoneNumber, MaximumPhoneLength, "fileHandler.phoneNumber"),
            VehicleRegistration = Registration(instruction.VehicleRegistration),
            VehicleMake = Collapse(instruction.VehicleMake, MaximumVehicleTextLength, "vehicle.make"),
            VehicleModel = Collapse(instruction.VehicleModel, MaximumVehicleTextLength, "vehicle.model"),
            VehicleMileageUnit = Trim(instruction.VehicleMileageUnit, MaximumMileageUnitLength, "vehicle.mileageUnit"),
            AccidentCircumstances = Paragraph(instruction.AccidentCircumstances, MaximumCircumstancesLength, "incident.circumstances"),
            InspectionAddress = Paragraph(instruction.InspectionAddress, MaximumAddressLength, "inspection.location"),
            VatStatus = Trim(instruction.VatStatus, MaximumVatStatusLength, "vatStatus"),
            Notes = Paragraph(instruction.Notes, MaximumNotesLength, "notes")
        };
    }

    /// <summary>
    /// The Principal the credential established, checked against the one the
    /// body claims. FRD-09 is explicit that content never selects a Principal,
    /// so a mismatch is refused rather than honoured — the field exists to catch
    /// a provider posting to the wrong account, not to choose an account.
    /// </summary>
    public static bool DeclaredPrincipalMatches(ProviderInstruction instruction, string principalCode)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        ArgumentException.ThrowIfNullOrWhiteSpace(principalCode);
        return string.IsNullOrWhiteSpace(instruction.DeclaredPrincipalCode)
            || string.Equals(
                CasePrincipalCode.Normalize(instruction.DeclaredPrincipalCode),
                CasePrincipalCode.Normalize(principalCode),
                StringComparison.Ordinal);
    }

    /// <summary>
    /// The declared instruction as the draft every downstream owner already
    /// reads. <paramref name="receivedOn"/> is the submission's own date, which
    /// stands in for an instruction date the provider did not state: an
    /// instruction dates from when it was given, and for an API submission that
    /// instant is when it arrived.
    /// </summary>
    public static InstructionDraft ToDraft(
        ProviderInstruction instruction,
        string principalCode,
        DateOnly receivedOn)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        ArgumentException.ThrowIfNullOrWhiteSpace(principalCode);
        return new(
            CasePrincipalCode.Normalize(principalCode),
            instruction.ClaimantName,
            instruction.ClaimNumber,
            instruction.VehicleRegistration,
            instruction.VehicleMake,
            instruction.VehicleModel,
            instruction.VehicleMileage,
            instruction.AccidentCircumstances,
            instruction.DateOfIncident,
            instruction.InstructionDate ?? receivedOn,
            instruction.InspectionAddress,
            instruction.InspectionDateRequested,
            instruction.VehicleMileageUnit,
            instruction.VatStatus,
            instruction.ClaimantAddress,
            instruction.ClaimantContactNumber,
            instruction.FileHandlerName,
            instruction.FileHandlerEmailAddress,
            instruction.FileHandlerPhoneNumber,
            instruction.Notes);
    }

    /// <summary>
    /// One review field per declared value, each with exactly one candidate
    /// naming the declaration as its source. The case snapshot refuses a draft
    /// value with no unambiguous provenance, and this is that provenance: the
    /// provider said so.
    /// </summary>
    public static IReadOnlyList<InstructionReviewField> ReviewFields(InstructionDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var fields = new List<InstructionReviewField>(18);
        Add(fields, FieldNames.ClaimantName, draft.ClaimantName);
        Add(fields, FieldNames.ClaimantContactNumber, draft.ClaimantContactNumber);
        Add(fields, FieldNames.ClaimantAddress, draft.ClaimantAddress);
        Add(fields, FieldNames.ClaimNumber, draft.ClaimNumber);
        Add(fields, FieldNames.VehicleRegistration, draft.VehicleRegistration);
        Add(fields, FieldNames.VehicleMake, draft.VehicleMake);
        Add(fields, FieldNames.VehicleModel, draft.VehicleModel);
        Add(fields, FieldNames.VehicleMileage, draft.VehicleMileage?.ToString(CultureInfo.InvariantCulture));
        Add(fields, FieldNames.VehicleMileageUnit, draft.VehicleMileageUnit);
        Add(fields, FieldNames.AccidentCircumstances, draft.AccidentCircumstances);
        Add(fields, FieldNames.DateOfIncident, Date(draft.DateOfIncident));
        Add(fields, FieldNames.InstructionDate, Date(draft.InstructionDate));
        Add(fields, FieldNames.InspectionDate, Date(draft.InspectionDate));
        Add(fields, FieldNames.InspectionAddress, draft.InspectionAddress);
        Add(fields, FieldNames.VatStatus, draft.VatStatus);
        Add(fields, FieldNames.FileHandlerName, draft.FileHandlerName);
        Add(fields, FieldNames.FileHandlerEmailAddress, draft.FileHandlerEmailAddress);
        Add(fields, FieldNames.FileHandlerPhoneNumber, draft.FileHandlerPhoneNumber);
        return fields;
    }

    /// <summary>
    /// The evidence a declared Triage carries. It is the same shape the accepted
    /// route classification produces, because the gate downstream reads exactly
    /// one Strong AcceptedTriageMatch with a matcher key and version — a
    /// declaration satisfies it as an e-mail tell does.
    /// </summary>
    public static IntakeEvidence TriageEvidence() =>
        new(
            IntakeEvidenceSource.ProviderDeclaration,
            IntakeEvidenceStrength.Strong,
            IntakeEvidenceFinding.AcceptedTriageMatch,
            ProviderInstructionKinds.Triage,
            "The authenticated Principal declared this submission a Triage request.",
            PolicyKey,
            PolicyVersion);

    public static IntakeEvidence DeclarationEvidence(ProviderInstructionKind kind) =>
        new(
            IntakeEvidenceSource.ProviderDeclaration,
            IntakeEvidenceStrength.Strong,
            IntakeEvidenceFinding.Information,
            ProviderInstructionKinds.Format(kind),
            "The authenticated Principal declared this instruction over the Provider API.",
            PolicyKey,
            PolicyVersion);

    private static void Add(List<InstructionReviewField> fields, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        fields.Add(new(
            name,
            value,
            [new(value, IntakeEvidenceSource.ProviderDeclaration, PolicyKey)],
            IsDefaulted: false,
            HasConflict: false));
    }

    private static string? Date(DateOnly? value) =>
        value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string? Registration(string? value)
    {
        var normalized = Trim(value, MaximumRegistrationLength, "vehicle.registration")
            ?.Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
        if (normalized is not null
            && normalized.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new ProviderInstructionValidationException(
                "vehicle.registration",
                "The vehicle registration can contain only letters, digits and spaces.");
        }

        return normalized;
    }

    private static string? Trim(string? value, int maximumLength, string field)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }
        if (normalized.Length > maximumLength)
        {
            throw new ProviderInstructionValidationException(
                field,
                $"The value of '{field}' is at most {maximumLength} characters.");
        }

        return normalized;
    }

    /// <summary>Whitespace collapsed to single spaces, as the case store does.</summary>
    private static string? Collapse(string? value, int maximumLength, string field)
    {
        var normalized = value is null
            ? null
            : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return Trim(normalized, maximumLength, field);
    }

    /// <summary>
    /// Line breaks kept, surrounding whitespace dropped. An address and the
    /// accident circumstances are written as several lines and mean less as one.
    /// </summary>
    private static string? Paragraph(string? value, int maximumLength, string field)
    {
        if (value is null)
        {
            return null;
        }

        var lines = value
            .ReplaceLineEndings("\n")
            .Split('\n')
            .Select(line => line.Trim());
        return Trim(string.Join('\n', lines).Trim('\n'), maximumLength, field);
    }
}
