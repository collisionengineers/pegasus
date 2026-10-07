using System.Text.Json;
using System.Text.Json.Serialization;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;

namespace Pegasus.Core.PrincipalApi;

/// <summary>
/// The Principal API's wire schema and its one parser (API-01).
///
/// It lives in Core, and there is exactly one of it, because two owners read
/// the same bytes: the endpoint parses the incoming request, and intake parses
/// the retained body again to recover the files as attachments. A second copy
/// of this shape would let those two disagree about what a submission said.
/// </summary>
public static class PrincipalInstructionJson
{
    /// <summary>
    /// The request, the stored declaration and the responses. Enums are
    /// written by name, so a retained declaration still says what it said
    /// after an enum gains or reorders a member.
    /// </summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(PrincipalInstruction instruction) =>
        JsonSerializer.Serialize(instruction, Options);

    public static PrincipalInstruction? Deserialize(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<PrincipalInstruction>(json, Options);

    /// <summary>
    /// The declared instruction and its files, or a
    /// <see cref="PrincipalInstructionValidationException"/> naming the field at
    /// fault. Text is normalised and bounded exactly as the case store does it.
    /// </summary>
    public static (PrincipalInstruction Instruction, IReadOnlyList<PrincipalSubmissionFile> Files) Parse(
        ReadOnlyMemory<byte> body)
    {
        var parsed = Read(body);
        return (Instruction(parsed), Files(parsed));
    }

    /// <summary>
    /// Only the files, for intake recovering them from a retained body. The
    /// instruction was checked when the body was received and is read from
    /// the stored declaration, not from here.
    /// </summary>
    public static IReadOnlyList<PrincipalSubmissionFile> ParseFiles(ReadOnlyMemory<byte> body) =>
        Files(Read(body));

    private static PrincipalSubmissionBody Read(ReadOnlyMemory<byte> body)
    {
        try
        {
            return JsonSerializer.Deserialize<PrincipalSubmissionBody>(body.Span, Options)
                ?? throw new PrincipalInstructionValidationException("body", "The submission body is empty.");
        }
        catch (JsonException exception)
        {
            throw new PrincipalInstructionValidationException(
                "body",
                $"The submission is not valid JSON: {exception.Message}");
        }
    }

    private static PrincipalInstruction Instruction(PrincipalSubmissionBody body)
    {
        var caseType = PrincipalInstructionVocabulary.CaseTypes.TryGetValue(body.CaseType?.Trim() ?? string.Empty, out var type)
            ? type
            : throw new PrincipalInstructionValidationException(
                "caseType",
                $"The case type must be one of: {string.Join(", ", PrincipalInstructionVocabulary.CaseTypes.Keys)}.");
        AuditAssessment? verdict = null;
        if (!string.IsNullOrWhiteSpace(body.OriginalReportVerdict))
        {
            verdict = PrincipalInstructionVocabulary.ReportVerdicts.TryGetValue(body.OriginalReportVerdict.Trim(), out var value)
                ? value
                : throw new PrincipalInstructionValidationException(
                    "originalReportVerdict",
                    $"The original report verdict must be one of: {string.Join(", ", PrincipalInstructionVocabulary.ReportVerdicts.Keys)}.");
        }

        // Only a standalone Audit audits another firm's report, so only it
        // states a verdict on one. The report file itself is optional and may
        // arrive later (operator, 2026-09-28).
        if (caseType == CaseType.Audit && verdict is null)
        {
            throw new PrincipalInstructionValidationException(
                "originalReportVerdict",
                "An Audit instruction must state the original report verdict.");
        }
        if (caseType != CaseType.Audit && verdict is not null)
        {
            throw new PrincipalInstructionValidationException(
                "originalReportVerdict",
                "Only an Audit instruction carries an original report verdict.");
        }

        var claimant = body.Claimant ?? new();
        var handler = body.FileHandler ?? new();
        var vehicle = body.Vehicle ?? new();
        var incident = body.Incident ?? new();
        var inspection = body.Inspection ?? new();
        if (vehicle.Mileage < 0)
        {
            throw new PrincipalInstructionValidationException(
                "vehicle.mileage",
                "The vehicle mileage cannot be negative.");
        }

        // The case store's own normalisation and bounds, reporting the field by
        // its path in the request body.
        try
        {
            var draft = new InstructionDraft(
                SuggestedPrincipalCode: null,
                ClaimantName: CaseDataPolicy.Text(claimant.Name, CaseDataLimits.PersonName, "claimant.name"),
                ClaimNumber: CaseDataPolicy.Text(body.ClaimNumber, CaseDataLimits.ClaimNumber, "claimNumber"),
                VehicleRegistration: CaseDataPolicy.Registration(vehicle.Registration, "vehicle.registration"),
                VehicleMake: CaseDataPolicy.Text(vehicle.Make, CaseDataLimits.VehicleText, "vehicle.make"),
                VehicleModel: CaseDataPolicy.Text(vehicle.Model, CaseDataLimits.VehicleText, "vehicle.model"),
                VehicleMileage: vehicle.Mileage,
                AccidentCircumstances: CaseDataPolicy.Paragraphs(
                    incident.Circumstances, CaseDataLimits.AccidentCircumstances, "incident.circumstances"),
                DateOfIncident: incident.DateOfIncident,
                InspectionAddress: CaseDataPolicy.Text(inspection.Location, CaseDataLimits.Address, "inspection.location"),
                InspectionDate: inspection.DateRequested,
                VehicleMileageUnit: CaseDataPolicy.Text(vehicle.MileageUnit, CaseDataLimits.MileageUnit, "vehicle.mileageUnit"),
                VatStatus: CaseDataPolicy.Text(body.VatStatus, CaseDataLimits.VatStatus, "vatStatus"),
                ClaimantAddress: CaseDataPolicy.Paragraphs(claimant.Address, CaseDataLimits.Address, "claimant.address"),
                ClaimantContactNumber: CaseDataPolicy.Text(claimant.ContactNumber, CaseDataLimits.Telephone, "claimant.contactNumber"),
                FileHandlerName: CaseDataPolicy.Text(handler.Name, CaseDataLimits.PersonName, "fileHandler.name"),
                FileHandlerEmailAddress: CaseDataPolicy.Text(handler.EmailAddress, CaseDataLimits.EmailAddress, "fileHandler.emailAddress"),
                FileHandlerPhoneNumber: CaseDataPolicy.Text(handler.PhoneNumber, CaseDataLimits.Telephone, "fileHandler.phoneNumber"),
                Notes: CaseDataPolicy.Paragraphs(body.Notes, AddCaseNote.MaximumLength, "notes"));
            return new(caseType, verdict, draft);
        }
        catch (ArgumentException exception) when (exception.ParamName is { } field)
        {
            throw new PrincipalInstructionValidationException(field, exception.Message);
        }
    }

    /// <summary>
    /// The files in the order sent, then the original report. A Principal may
    /// declare an instruction with no files at all (operator, 2026-09-28).
    /// </summary>
    private static PrincipalSubmissionFile[] Files(PrincipalSubmissionBody body)
    {
        var files = (body.Files ?? [])
            .Select((file, index) => File(file, $"files[{index}]", PrincipalInstructionPolicy.AssetSourceLabel(index)));
        return body.OriginalReport is { } report
            ? [.. files, File(report, "originalReport", PrincipalInstructionPolicy.OriginalReportSourceLabel)]
            : [.. files];
    }

    /// <summary>
    /// One file, typed by its extension as every other intake route types a
    /// file it is given by name.
    /// </summary>
    private static PrincipalSubmissionFile File(PrincipalSubmissionFileBody file, string field, string sourceLabel)
    {
        if (string.IsNullOrWhiteSpace(file.FileName)
            || file.FileName.Length > PrincipalSubmissionPolicy.MaximumFileNameLength
            || !string.Equals(Path.GetFileName(file.FileName), file.FileName, StringComparison.Ordinal))
        {
            throw new PrincipalInstructionValidationException(
                $"{field}.fileName",
                $"A file name of at most {PrincipalSubmissionPolicy.MaximumFileNameLength} characters, with no directory part, is required.");
        }

        var mediaType = IntakeUploadFilePolicy.MediaTypeFor(file.FileName)
            ?? throw new PrincipalInstructionValidationException(
                $"{field}.fileName",
                "The file type is not supported.");
        if (string.IsNullOrWhiteSpace(file.ContentBase64))
        {
            throw new PrincipalInstructionValidationException($"{field}.contentBase64", "File content is required.");
        }

        try
        {
            return new(field, sourceLabel, file.FileName, mediaType, Convert.FromBase64String(file.ContentBase64));
        }
        catch (FormatException)
        {
            throw new PrincipalInstructionValidationException(
                $"{field}.contentBase64",
                "The file content is not valid base64.");
        }
    }
}

public sealed record PrincipalSubmissionFileBody(
    string? FileName = null,
    string? ContentBase64 = null);

public sealed record PrincipalInstructionClaimantBody(
    string? Name = null,
    string? ContactNumber = null,
    string? Address = null);

public sealed record PrincipalInstructionPartyBody(
    string? Name = null,
    string? EmailAddress = null,
    string? PhoneNumber = null);

public sealed record PrincipalInstructionVehicleBody(
    string? Registration = null,
    string? Make = null,
    string? Model = null,
    long? Mileage = null,
    string? MileageUnit = null);

public sealed record PrincipalInstructionIncidentBody(
    DateOnly? DateOfIncident = null,
    string? Circumstances = null);

public sealed record PrincipalInstructionInspectionBody(
    DateOnly? DateRequested = null,
    string? Location = null);

/// <summary>
/// API-01's request body. It has no instruction date: the time Pegasus received
/// the submission is the Case's Received date, which is its instruction date
/// (operator, 24 September 2026). A member this contract does not name is
/// ignored rather than refused (System.Text.Json's default unmapped-member
/// handling).
/// </summary>
public sealed record PrincipalSubmissionBody(
    string? ClaimNumber = null,
    string? CaseType = null,
    string? OriginalReportVerdict = null,
    PrincipalInstructionClaimantBody? Claimant = null,
    PrincipalInstructionPartyBody? FileHandler = null,
    PrincipalInstructionVehicleBody? Vehicle = null,
    PrincipalInstructionIncidentBody? Incident = null,
    PrincipalInstructionInspectionBody? Inspection = null,
    string? VatStatus = null,
    string? Notes = null,
    IReadOnlyList<PrincipalSubmissionFileBody>? Files = null,
    PrincipalSubmissionFileBody? OriginalReport = null);
