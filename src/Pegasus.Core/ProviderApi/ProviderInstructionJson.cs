using System.Text.Json;
using System.Text.Json.Serialization;
using Pegasus.Core.Documents;

namespace Pegasus.Core.ProviderApi;

/// <summary>
/// The Provider API's wire schema and its one parser (API-01).
///
/// It lives in Core, and there is exactly one of it, because two owners read
/// the same bytes: the endpoint parses the incoming request, and intake parses
/// the retained body again to recover the files as attachments. A second copy
/// of this shape would let those two disagree about what a submission said.
/// </summary>
public static class ProviderInstructionJson
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

    public static string Serialize(ProviderInstruction instruction) =>
        JsonSerializer.Serialize(instruction, StorageOptions);

    public static ProviderInstruction? Deserialize(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<ProviderInstruction>(json, StorageOptions);

    /// <summary>
    /// The declared instruction and its files, or a
    /// <see cref="ProviderInstructionValidationException"/> naming the field at
    /// fault. Malformed JSON is reported as the body being unreadable rather
    /// than as a field.
    /// </summary>
    public static (ProviderInstruction Instruction, IReadOnlyList<ProviderSubmissionFile> Files) Parse(
        ReadOnlyMemory<byte> body)
    {
        ProviderSubmissionBody? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ProviderSubmissionBody>(body.Span, Options);
        }
        catch (JsonException exception)
        {
            throw new ProviderInstructionValidationException(
                "body",
                $"The submission is not valid JSON: {exception.Message}");
        }

        if (parsed is null)
        {
            throw new ProviderInstructionValidationException("body", "The submission body is empty.");
        }

        var claimant = parsed.Claimant ?? new();
        var handler = parsed.FileHandler ?? new();
        var vehicle = parsed.Vehicle ?? new();
        var incident = parsed.Incident ?? new();
        var inspection = parsed.Inspection ?? new();
        var instruction = new ProviderInstruction(
            ProviderInstructionKinds.Parse(parsed.CaseType),
            ProviderReportVerdicts.Parse(parsed.OriginalReportVerdict),
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
            parsed.InstructionDate,
            parsed.VatStatus,
            parsed.Notes);
        return (instruction, Files(parsed.Files));
    }

    private static ProviderSubmissionFile[] Files(IReadOnlyList<ProviderSubmissionFileBody>? files)
    {
        if (files is null || files.Count == 0)
        {
            throw new ProviderInstructionValidationException(
                "files",
                "At least one file is required.");
        }

        return files
            .Select((file, index) =>
            {
                var ordinal = file.Ordinal ?? index;
                var field = $"files[{ordinal}]";
                if (string.IsNullOrWhiteSpace(file.FileName))
                {
                    throw new ProviderInstructionValidationException($"{field}.fileName", "A file name is required.");
                }
                if (string.IsNullOrWhiteSpace(file.MediaType))
                {
                    throw new ProviderInstructionValidationException($"{field}.mediaType", "A media type is required.");
                }
                if (string.IsNullOrWhiteSpace(file.ContentBase64))
                {
                    throw new ProviderInstructionValidationException($"{field}.contentBase64", "File content is required.");
                }

                byte[] content;
                try
                {
                    content = Convert.FromBase64String(file.ContentBase64);
                }
                catch (FormatException)
                {
                    throw new ProviderInstructionValidationException(
                        $"{field}.contentBase64",
                        "The file content is not valid base64.");
                }

                DocumentSemanticRole? role;
                try
                {
                    role = ProviderFileRoles.Parse(file.Role);
                }
                catch (ArgumentException exception)
                {
                    throw new ProviderInstructionValidationException($"{field}.role", exception.Message);
                }

                return new ProviderSubmissionFile(ordinal, file.FileName, file.MediaType, content, role);
            })
            .ToArray();
    }
}

public sealed record ProviderSubmissionFileBody(
    int? Ordinal = null,
    string? FileName = null,
    string? MediaType = null,
    string? Role = null,
    [property: JsonPropertyName("contentBase64")] string? ContentBase64 = null);

public sealed record ProviderInstructionClaimantBody(
    string? Name = null,
    string? ContactNumber = null,
    string? Address = null);

public sealed record ProviderInstructionPartyBody(
    string? Name = null,
    string? EmailAddress = null,
    string? PhoneNumber = null);

public sealed record ProviderInstructionVehicleBody(
    string? Registration = null,
    string? Make = null,
    string? Model = null,
    long? Mileage = null,
    string? MileageUnit = null);

public sealed record ProviderInstructionIncidentBody(
    DateOnly? DateOfIncident = null,
    string? Circumstances = null);

public sealed record ProviderInstructionInspectionBody(
    DateOnly? DateRequested = null,
    string? Location = null);

public sealed record ProviderSubmissionBody(
    string? Principal = null,
    string? ClaimNumber = null,
    string? CaseType = null,
    string? OriginalReportVerdict = null,
    ProviderInstructionClaimantBody? Claimant = null,
    ProviderInstructionPartyBody? FileHandler = null,
    ProviderInstructionVehicleBody? Vehicle = null,
    ProviderInstructionIncidentBody? Incident = null,
    ProviderInstructionInspectionBody? Inspection = null,
    DateOnly? InstructionDate = null,
    string? VatStatus = null,
    string? Notes = null,
    IReadOnlyList<ProviderSubmissionFileBody>? Files = null);
