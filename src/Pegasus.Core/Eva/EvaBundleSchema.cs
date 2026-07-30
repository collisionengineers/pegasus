using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Eva;

public sealed record EvaReplayFields(
    string? WorkProvider,
    string? Vrm,
    string? VehicleModel,
    string? ClaimantName,
    string? Reference,
    string? IncidentDate,
    string? InstructionDate,
    string? InspectionDate,
    string? InspectionAddress,
    string? AccidentCircumstances,
    string? VatStatus,
    string? Mileage,
    string? MileageUnit);

public sealed record EvaBundleImage(
    Guid OccurrenceId,
    Guid DocumentId,
    Guid VersionId,
    int Version,
    string FileName,
    string MediaType,
    DocumentSemanticRole SemanticRole,
    DocumentSource Source,
    string SourceOccurrenceIdentity,
    ReadOnlyMemory<byte> Content,
    string Sha256,
    bool CustodyConfirmed,
    bool IsCurrent);

public sealed record EvaBundleImageOrder(
    Guid OverviewImageOccurrenceId,
    Guid MainDamageImageOccurrenceId,
    IReadOnlyList<EvaBundleImage> OrderedImages);

public sealed record EvaBundle(
    byte[] Content,
    string Sha256,
    byte[] JsonContent,
    string JsonSha256,
    byte[] ProvenanceContent,
    string ProvenanceSha256,
    byte[] ManifestContent,
    string FileName);

public sealed record EvaHandoffImageOption(
    Guid OccurrenceId,
    Guid DocumentId,
    Guid VersionId,
    int Version,
    string FileName,
    string MediaType,
    long ContentLength,
    string Sha256,
    DocumentSource Source,
    string SourceOccurrenceIdentity);

public sealed record EvaHandoffRevisionSummary(
    int Revision,
    string FileName,
    string BundleSha256,
    string JsonSha256,
    DateTimeOffset GeneratedAtUtc,
    string GeneratedBy,
    bool EstablishedFirstSentToEngineerProxy);

public sealed record EvaHandoffRevisionArtifact(
    int Revision,
    string FileName,
    byte[] Content,
    string BundleSha256)
{
    public const string MediaType = "application/zip";

    public long ContentLength => Content.LongLength;
}

public sealed record EvaHandoffPreparation(
    Guid CaseId,
    long CaseVersion,
    string Reference,
    IReadOnlyList<EvaHandoffImageOption> Images,
    IReadOnlyList<EvaHandoffRevisionSummary> Revisions,
    DateTimeOffset? FirstSentToEngineerAtUtc,
    IReadOnlyList<string> BlockingReasons)
{
    public bool CanGenerate => BlockingReasons.Count == 0;
}

public sealed record GenerateEvaHandoffRequest(
    Guid CaseId,
    long ExpectedCaseVersion,
    Guid OverviewImageOccurrenceId,
    Guid MainDamageImageOccurrenceId,
    IReadOnlyList<Guid> OrderedImageOccurrenceIds,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken);

public enum GenerateEvaHandoffOutcome
{
    Generated,
    Blocked,
    Conflict,
    NotFound
}

public sealed record GenerateEvaHandoffResult(
    GenerateEvaHandoffOutcome Outcome,
    EvaBundle? Bundle,
    IReadOnlyList<string> Reasons,
    int? Revision = null,
    bool FirstSentToEngineerRecorded = false);

public interface IEvaHandoffQueries
{
    Task<EvaHandoffPreparation?> GetPreparationAsync(
        Guid caseId,
        CancellationToken cancellationToken = default);

    Task<EvaHandoffRevisionArtifact?> GetRevisionAsync(
        Guid caseId,
        int revision,
        ActionActor actor,
        CancellationToken cancellationToken = default);
}

public interface IGenerateEvaHandoff
{
    Task<GenerateEvaHandoffResult> ExecuteAsync(
        GenerateEvaHandoffRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record EvaHandoffProxyRequest(
    Guid CaseId,
    int Revision,
    string BundleSha256,
    ActionActor Actor,
    string OperationKey);

public sealed record EvaHandoffProxyReceipt(
    string AdapterKey,
    string AdapterVersion,
    DateTimeOffset RecordedAtUtc,
    bool ClaimsExternalDelivery,
    bool ClaimsEngineerAssignment);

public interface IEvaHandoffProxy
{
    Task<EvaHandoffProxyReceipt> RecordFirstGenerationAsync(
        EvaHandoffProxyRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Produces replay-identical manual EVA bundles without making an EVA or other network call.
/// JSON keys, archive entries, provenance, image order, timestamps, and hashes are explicit.
/// </summary>
public static class EvaBundleSchema
{
    public const string SchemaVersion = "eva-handoff-v2";
    private const string ProvenanceFileName = "provenance.json";
    private const string ManifestFileName = "manifest.sha256";
    private static readonly DateTimeOffset DeterministicTimestamp =
        new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly string[] FieldOrder =
    [
        "Work Provider",
        "VRM",
        "Vehicle Model",
        "Claimant Name",
        "Reference",
        "Incident Date",
        "Instruction Date",
        "Inspection Date",
        "Inspection Address",
        "Accident Circumstances",
        "VAT Status",
        "Mileage",
        "Mileage Unit"
    ];

    public static EvaBundle CreateOfflineReplay(
        EvaBundleSource source,
        EvaBundleImageOrder imageOrder)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(imageOrder);
        ArgumentNullException.ThrowIfNull(imageOrder.OrderedImages);

        var normalizedSource = ValidateSource(source);
        var reference = SafeFileComponent(normalizedSource.Fields.Reference!);
        var jsonName = $"EVA-{reference}.json";
        var json = WriteOrderedJson(normalizedSource.Fields);
        var jsonHash = Hash(json);
        var imageEntries = ValidateAndNameImages(imageOrder);
        var provenance = WriteProvenance(normalizedSource, imageEntries);
        var provenanceHash = Hash(provenance);
        var manifest = WriteManifest(jsonName, jsonHash, imageEntries, provenanceHash);
        var archive = WriteArchive(jsonName, json, imageEntries, provenance, manifest);

        return new(
            archive,
            Hash(archive),
            json,
            jsonHash,
            provenance,
            provenanceHash,
            manifest,
            $"EVA-{reference}.zip");
    }

    private static EvaBundleSource ValidateSource(EvaBundleSource source)
    {
        ArgumentNullException.ThrowIfNull(source.Fields);
        ArgumentNullException.ThrowIfNull(source.Provenance);
        if (!string.Equals(source.MappingKey, CaseEvaMapping.MappingKey, StringComparison.Ordinal)
            || source.MappingVersion != CaseEvaMapping.MappingVersion
            || string.IsNullOrWhiteSpace(source.MappingAcceptanceEvidence))
        {
            throw new InvalidOperationException(
                "The EVA bundle requires an explicitly accepted mapping/config version.");
        }

        var normalized = CaseEvaMapping.MapOfflineReplay(source.Fields);
        var values = OrderedFields(normalized).ToArray();
        if (values.Any(field => string.IsNullOrWhiteSpace(field.Value)))
        {
            throw new InvalidDataException("Every EVA field requires an accepted non-empty value.");
        }
        if (source.Provenance.Count != FieldOrder.Length)
        {
            throw new InvalidDataException("EVA field provenance must cover the exact ordered field set.");
        }

        var provenance = new EvaFieldProvenance[FieldOrder.Length];
        for (var index = 0; index < FieldOrder.Length; index++)
        {
            var item = source.Provenance[index]
                ?? throw new InvalidDataException("An EVA field provenance entry is missing.");
            var field = values[index];
            if (!string.Equals(item.Name, FieldOrder[index], StringComparison.Ordinal)
                || !string.Equals(
                    CaseEvaMapping.MapOfflineReplay(FieldWithValue(item.Name, item.Value))
                        .GetValue(item.Name),
                    field.Value,
                    StringComparison.Ordinal)
                || item.Status is not (EvaEvidenceStatus.Accepted or EvaEvidenceStatus.Corrected)
                || string.IsNullOrWhiteSpace(item.Source)
                || string.IsNullOrWhiteSpace(item.SourceVersion))
            {
                throw new InvalidDataException(
                    "EVA field provenance does not match the accepted ordered field values.");
            }

            provenance[index] = item with
            {
                Value = field.Value!,
                Source = item.Source.Trim(),
                SourceVersion = item.SourceVersion.Trim()
            };
        }

        return new(
            normalized,
            provenance,
            CaseEvaMapping.MappingKey,
            CaseEvaMapping.MappingVersion,
            source.MappingAcceptanceEvidence.Trim());
    }

    private static List<ImageEntry> ValidateAndNameImages(EvaBundleImageOrder imageOrder)
    {
        if (imageOrder.OverviewImageOccurrenceId == Guid.Empty
            || imageOrder.MainDamageImageOccurrenceId == Guid.Empty
            || imageOrder.OverviewImageOccurrenceId == imageOrder.MainDamageImageOccurrenceId)
        {
            throw new InvalidDataException(
                "Distinct overview and main-damage preview image occurrences are required.");
        }

        var ids = new HashSet<Guid>();
        var accepted = new List<ValidatedImage>(imageOrder.OrderedImages.Count);
        foreach (var image in imageOrder.OrderedImages)
        {
            var validated = ValidateImage(image);
            if (!ids.Add(validated.Image.OccurrenceId))
            {
                throw new InvalidDataException(
                    "Accepted EVA image occurrence identities must be unique.");
            }

            accepted.Add(validated);
        }

        if (accepted.Count < 2)
        {
            throw new InvalidDataException("At least two accepted EVA images are required.");
        }

        var overview = accepted.SingleOrDefault(
            item => item.Image.OccurrenceId == imageOrder.OverviewImageOccurrenceId)
            ?? throw new InvalidDataException(
                "The overview preview must also be present in the accepted image order.");
        var mainDamage = accepted.SingleOrDefault(
            item => item.Image.OccurrenceId == imageOrder.MainDamageImageOccurrenceId)
            ?? throw new InvalidDataException(
                "The main-damage preview must also be present in the accepted image order.");

        var entries = new List<ImageEntry>(accepted.Count + 2)
        {
            CreateImageEntry(overview, 1, "overview-preview"),
            CreateImageEntry(mainDamage, 2, "main-damage-preview")
        };
        for (var index = 0; index < accepted.Count; index++)
        {
            entries.Add(CreateImageEntry(accepted[index], index + 3, "accepted"));
        }

        return entries;
    }

    private static ValidatedImage ValidateImage(EvaBundleImage? image)
    {
        if (image is null)
        {
            throw new InvalidDataException("A selected EVA image is missing.");
        }
        if (image.OccurrenceId == Guid.Empty
            || image.DocumentId == Guid.Empty
            || image.VersionId == Guid.Empty
            || image.Version <= 0)
        {
            throw new InvalidDataException(
                "Selected EVA images require occurrence, document, and version identities.");
        }
        if (!image.CustodyConfirmed || !image.IsCurrent)
        {
            throw new InvalidOperationException(
                "Every selected EVA image must be the custody-confirmed current document version.");
        }
        if (image.SemanticRole != DocumentSemanticRole.Image
            || !IsSupportedImageMediaType(image.MediaType))
        {
            throw new InvalidDataException("Only retained JPEG or PNG image documents may enter EVA.");
        }
        if (string.IsNullOrWhiteSpace(image.FileName)
            || string.IsNullOrWhiteSpace(image.SourceOccurrenceIdentity)
            || image.Content.IsEmpty)
        {
            throw new InvalidDataException(
                "A selected EVA image is missing retained content or source provenance.");
        }

        var actualHash = Hash(image.Content.Span);
        if (!string.Equals(actualHash, image.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A selected EVA image failed SHA-256 integrity validation.");
        }

        return new(image, actualHash);
    }

    private static ImageEntry CreateImageEntry(
        ValidatedImage image,
        int sequence,
        string slot) => new(
        $"{sequence:D3}-{SafeFileComponent(image.Image.FileName)}",
        slot,
        image.Image,
        image.Sha256);

    private static byte[] WriteOrderedJson(EvaReplayFields fields)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            foreach (var field in OrderedFields(fields))
            {
                writer.WriteString(field.Name, field.Value);
            }
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static byte[] WriteProvenance(
        EvaBundleSource source,
        IReadOnlyList<ImageEntry> images)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", SchemaVersion);
            writer.WritePropertyName("mapping");
            writer.WriteStartObject();
            writer.WriteString("key", source.MappingKey);
            writer.WriteNumber("version", source.MappingVersion);
            writer.WriteString("acceptanceEvidence", source.MappingAcceptanceEvidence);
            writer.WriteEndObject();
            writer.WritePropertyName("fields");
            writer.WriteStartArray();
            foreach (var field in source.Provenance)
            {
                writer.WriteStartObject();
                writer.WriteString("name", field.Name);
                writer.WriteString("value", field.Value);
                writer.WriteString(
                    "status",
                    field.Status == EvaEvidenceStatus.Accepted ? "accepted" : "corrected");
                writer.WriteString("source", field.Source);
                writer.WriteString("sourceVersion", field.SourceVersion);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WritePropertyName("images");
            writer.WriteStartArray();
            foreach (var entry in images)
            {
                writer.WriteStartObject();
                writer.WriteString("entryName", entry.Name);
                writer.WriteString("slot", entry.Slot);
                writer.WriteString("occurrenceId", entry.Image.OccurrenceId);
                writer.WriteString("documentId", entry.Image.DocumentId);
                writer.WriteString("versionId", entry.Image.VersionId);
                writer.WriteNumber("version", entry.Image.Version);
                writer.WriteString("source", entry.Image.Source.ToString());
                writer.WriteString("sourceOccurrenceIdentity", entry.Image.SourceOccurrenceIdentity);
                writer.WriteString("fileName", entry.Image.FileName);
                writer.WriteString("mediaType", entry.Image.MediaType);
                writer.WriteNumber("contentLength", entry.Image.Content.Length);
                writer.WriteString("semanticRole", entry.Image.SemanticRole.ToString());
                writer.WriteString("sha256", entry.Sha256);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static byte[] WriteManifest(
        string jsonName,
        string jsonHash,
        IReadOnlyList<ImageEntry> images,
        string provenanceHash)
    {
        var builder = new StringBuilder();
        builder.Append(jsonHash).Append("  ").Append(jsonName).Append('\n');
        foreach (var image in images)
        {
            builder.Append(image.Sha256).Append("  ").Append(image.Name).Append('\n');
        }
        builder.Append(provenanceHash).Append("  ").Append(ProvenanceFileName).Append('\n');
        return new UTF8Encoding(false).GetBytes(builder.ToString());
    }

    private static byte[] WriteArchive(
        string jsonName,
        byte[] json,
        IReadOnlyList<ImageEntry> images,
        byte[] provenance,
        byte[] manifest)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
        {
            WriteEntry(archive, jsonName, json);
            foreach (var image in images)
            {
                WriteEntry(archive, image.Name, image.Image.Content.Span);
            }
            WriteEntry(archive, ProvenanceFileName, provenance);
            WriteEntry(archive, ManifestFileName, manifest);
        }

        return stream.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string name, ReadOnlySpan<byte> content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
        entry.LastWriteTime = DeterministicTimestamp;
        entry.ExternalAttributes = 0;
        using var entryStream = entry.Open();
        entryStream.Write(content);
    }

    private static IEnumerable<(string Name, string? Value)> OrderedFields(EvaReplayFields fields)
    {
        yield return ("Work Provider", fields.WorkProvider);
        yield return ("VRM", fields.Vrm);
        yield return ("Vehicle Model", fields.VehicleModel);
        yield return ("Claimant Name", fields.ClaimantName);
        yield return ("Reference", fields.Reference);
        yield return ("Incident Date", fields.IncidentDate);
        yield return ("Instruction Date", fields.InstructionDate);
        yield return ("Inspection Date", fields.InspectionDate);
        yield return ("Inspection Address", fields.InspectionAddress);
        yield return ("Accident Circumstances", fields.AccidentCircumstances);
        yield return ("VAT Status", fields.VatStatus);
        yield return ("Mileage", fields.Mileage);
        yield return ("Mileage Unit", fields.MileageUnit);
    }

    private static EvaReplayFields FieldWithValue(string name, string value) => name switch
    {
        "Work Provider" => new(value, null, null, null, null, null, null, null, null, null, null, null, null),
        "VRM" => new(null, value, null, null, null, null, null, null, null, null, null, null, null),
        "Vehicle Model" => new(null, null, value, null, null, null, null, null, null, null, null, null, null),
        "Claimant Name" => new(null, null, null, value, null, null, null, null, null, null, null, null, null),
        "Reference" => new(null, null, null, null, value, null, null, null, null, null, null, null, null),
        "Incident Date" => new(null, null, null, null, null, value, null, null, null, null, null, null, null),
        "Instruction Date" => new(null, null, null, null, null, null, value, null, null, null, null, null, null),
        "Inspection Date" => new(null, null, null, null, null, null, null, value, null, null, null, null, null),
        "Inspection Address" => new(null, null, null, null, null, null, null, null, value, null, null, null, null),
        "Accident Circumstances" => new(null, null, null, null, null, null, null, null, null, value, null, null, null),
        "VAT Status" => new(null, null, null, null, null, null, null, null, null, null, value, null, null),
        "Mileage" => new(null, null, null, null, null, null, null, null, null, null, null, value, null),
        "Mileage Unit" => new(null, null, null, null, null, null, null, null, null, null, null, null, value),
        _ => throw new InvalidDataException($"Unknown EVA field '{name}'.")
    };

    private static string? GetValue(this EvaReplayFields fields, string name) => name switch
    {
        "Work Provider" => fields.WorkProvider,
        "VRM" => fields.Vrm,
        "Vehicle Model" => fields.VehicleModel,
        "Claimant Name" => fields.ClaimantName,
        "Reference" => fields.Reference,
        "Incident Date" => fields.IncidentDate,
        "Instruction Date" => fields.InstructionDate,
        "Inspection Date" => fields.InspectionDate,
        "Inspection Address" => fields.InspectionAddress,
        "Accident Circumstances" => fields.AccidentCircumstances,
        "VAT Status" => fields.VatStatus,
        "Mileage" => fields.Mileage,
        "Mileage Unit" => fields.MileageUnit,
        _ => throw new InvalidDataException($"Unknown EVA field '{name}'.")
    };

    private static bool IsSupportedImageMediaType(string mediaType) =>
        string.Equals(mediaType, "image/jpeg", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mediaType, "image/png", StringComparison.OrdinalIgnoreCase);

    private static string SafeFileComponent(string value)
    {
        var trimmed = value.Trim();
        var separator = Math.Max(trimmed.LastIndexOf('/'), trimmed.LastIndexOf('\\'));
        var fileName = separator < 0 ? trimmed : trimmed[(separator + 1)..];
        var builder = new StringBuilder(fileName.Length);
        foreach (var character in fileName)
        {
            builder.Append(
                char.IsControl(character) || "<>:\"/\\|?*".Contains(character, StringComparison.Ordinal)
                    ? '_'
                    : character);
        }

        var result = builder.ToString().Trim().TrimEnd('.');
        return string.IsNullOrEmpty(result) ? "unnamed" : result;
    }

    private static string Hash(ReadOnlySpan<byte> content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private sealed record ValidatedImage(EvaBundleImage Image, string Sha256);

    private sealed record ImageEntry(
        string Name,
        string Slot,
        EvaBundleImage Image,
        string Sha256);
}
