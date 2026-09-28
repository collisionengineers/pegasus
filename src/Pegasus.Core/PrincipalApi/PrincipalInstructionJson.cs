using System.Text.Json;
using System.Text.Json.Serialization;
using Pegasus.Core.Documents;

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
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false
    };

    /// <summary>
    /// How the declaration is stored alongside its submission. Enums are written
    /// by name: a retained submission must still say what it said after an enum
    /// gains or reorders a member.
    /// </summary>
    public static readonly JsonSerializerOptions StorageOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(PrincipalInstruction instruction) =>
        JsonSerializer.Serialize(instruction, StorageOptions);

    public static PrincipalInstruction? Deserialize(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<PrincipalInstruction>(json, StorageOptions);

    /// <summary>
    /// The declared instruction and its files, or a
    /// <see cref="PrincipalInstructionValidationException"/> naming the field at
    /// fault. Malformed JSON is reported as the body being unreadable rather
    /// than as a field.
    /// </summary>
    public static (PrincipalInstruction Instruction, IReadOnlyList<PrincipalSubmissionFile> Files) Parse(
        ReadOnlyMemory<byte> body)
    {
        PrincipalSubmissionBody? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<PrincipalSubmissionBody>(body.Span, Options);
        }
        catch (JsonException exception)
        {
            throw new PrincipalInstructionValidationException(
                "body",
                $"The submission is not valid JSON: {exception.Message}");
        }

        if (parsed is null)
        {
            throw new PrincipalInstructionValidationException("body", "The submission body is empty.");
        }

        var claimant = parsed.Claimant ?? new();
        var handler = parsed.FileHandler ?? new();
        var vehicle = parsed.Vehicle ?? new();
        var incident = parsed.Incident ?? new();
        var inspection = parsed.Inspection ?? new();
        var instruction = new PrincipalInstruction(
            PrincipalInstructionKinds.Parse(parsed.CaseType),
            PrincipalReportVerdicts.Parse(parsed.OriginalReportVerdict),
            parsed.Principal,
            parsed.ClaimNumber,
            claimant.Name,
            claimant.ContactNumber,
            claimant.Address,
            handler.Name,
            handler.EmailAddress,
            handler.PhoneNumber,
            vehicle.Registration,
            vehicle.Make,
            vehicle.Model,
            vehicle.Mileage,
            vehicle.MileageUnit,
            incident.DateOfIncident,
            incident.Circumstances,
            inspection.DateRequested,
            inspection.Location,
            parsed.VatStatus,
            parsed.Notes);
        return (instruction, Files(parsed.Files));
    }

    private static PrincipalSubmissionFile[] Files(IReadOnlyList<PrincipalSubmissionFileBody>? files)
    {
        // A Principal may declare an instruction with no files at all
        // (operator, 2026-09-28): the files are optional for every kind.
        if (files is null)
        {
            return [];
        }

        return files
            .Select((file, index) =>
            {
                var ordinal = file.Ordinal ?? index;
                var field = $"files[{ordinal}]";
                if (string.IsNullOrWhiteSpace(file.FileName))
                {
                    throw new PrincipalInstructionValidationException($"{field}.fileName", "A file name is required.");
                }
                if (string.IsNullOrWhiteSpace(file.MediaType))
                {
                    throw new PrincipalInstructionValidationException($"{field}.mediaType", "A media type is required.");
                }
                if (string.IsNullOrWhiteSpace(file.ContentBase64))
                {
                    throw new PrincipalInstructionValidationException($"{field}.contentBase64", "File content is required.");
                }

                byte[] content;
                try
                {
                    content = Convert.FromBase64String(file.ContentBase64);
                }
                catch (FormatException)
                {
                    throw new PrincipalInstructionValidationException(
                        $"{field}.contentBase64",
                        "The file content is not valid base64.");
                }

                DocumentSemanticRole? role;
                try
                {
                    role = PrincipalFileRoles.Parse(file.Role);
                }
                catch (ArgumentException exception)
                {
                    throw new PrincipalInstructionValidationException($"{field}.role", exception.Message);
                }

                return new PrincipalSubmissionFile(ordinal, file.FileName, file.MediaType, content, role);
            })
            .ToArray();
    }
}

public sealed record PrincipalSubmissionFileBody(
    int? Ordinal = null,
    string? FileName = null,
    string? MediaType = null,
    string? Role = null,
    [property: JsonPropertyName("contentBase64")] string? ContentBase64 = null);

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
/// (operator, 24 September 2026). A member this contract does not name,
/// including the retired <c>instructionDate</c>, is ignored rather than
/// refused: <see cref="PrincipalInstructionJson.Options"/> and
/// <see cref="PrincipalInstructionJson.StorageOptions"/> keep System.Text.Json's
/// default unmapped-member handling, so retained request bodies (re-read by
/// PrincipalApiIntakeSourceReader) and stored declarations keep parsing.
/// </summary>
public sealed record PrincipalSubmissionBody(
    string? Principal = null,
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
    IReadOnlyList<PrincipalSubmissionFileBody>? Files = null);
