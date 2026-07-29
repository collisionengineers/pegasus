using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
    Guid AssetId,
    string FileName,
    string MediaType,
    ReadOnlyMemory<byte> Content,
    string Sha256,
    bool CustodyConfirmed);

public sealed record EvaBundle(
    byte[] Content,
    string Sha256,
    byte[] JsonContent,
    string JsonSha256,
    byte[] ManifestContent,
    string FileName);

public sealed record EvaHandoffImageOption(
    Guid AssetId,
    string FileName,
    string MediaType,
    long ContentLength,
    string Sha256,
    bool CustodyConfirmed);

public sealed record EvaHandoffPreparation(
    Guid CaseId,
    long CaseVersion,
    string Reference,
    IReadOnlyList<EvaHandoffImageOption> Images,
    IReadOnlyList<string> BlockingReasons)
{
    public bool CanGenerate => BlockingReasons.Count == 0;
}

public sealed record GenerateEvaHandoffRequest(
    Guid CaseId,
    long ExpectedCaseVersion,
    IReadOnlyList<Guid> SelectedImageIds,
    string Actor,
    string OperationKey);

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

public interface IEvaHandoffStore
{
    Task<EvaHandoffPreparation?> GetPreparationAsync(
        Guid caseId,
        CancellationToken cancellationToken);

    Task<GenerateEvaHandoffResult> GenerateAsync(
        GenerateEvaHandoffRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Produces replay-identical EVA bundles without making an EVA or other network call.
/// The JSON key order is an explicit part of the format.
/// </summary>
public static class EvaBundleSchema
{
    public const string SchemaVersion = "eva-handoff-replay-v1";
    private static readonly DateTimeOffset DeterministicTimestamp =
        new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static EvaBundle CreateOfflineReplay(
        EvaReplayFields fields,
        IReadOnlyList<EvaBundleImage> selectedImages)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(selectedImages);

        var normalized = CaseEvaMapping.MapOfflineReplay(fields);
        var json = WriteOrderedJson(normalized);
        var jsonHash = Hash(json);
        var imageEntries = ValidateAndNameImages(selectedImages);
        var manifest = WriteManifest(jsonHash, imageEntries);
        var archive = WriteArchive(json, imageEntries, manifest);
        var reference = SafeFileComponent(normalized.Reference ?? "unresolved");

        return new(
            archive,
            Hash(archive),
            json,
            jsonHash,
            manifest,
            $"{reference}-eva-handoff.zip");
    }

    private static byte[] WriteOrderedJson(EvaReplayFields fields)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            WriteString(writer, "Work Provider", fields.WorkProvider);
            WriteString(writer, "VRM", fields.Vrm);
            WriteString(writer, "Vehicle Model", fields.VehicleModel);
            WriteString(writer, "Claimant Name", fields.ClaimantName);
            WriteString(writer, "Reference", fields.Reference);
            WriteString(writer, "Incident Date", fields.IncidentDate);
            WriteString(writer, "Instruction Date", fields.InstructionDate);
            WriteString(writer, "Inspection Date", fields.InspectionDate);
            WriteString(writer, "Inspection Address", fields.InspectionAddress);
            WriteString(writer, "Accident Circumstances", fields.AccidentCircumstances);
            WriteString(writer, "VAT Status", fields.VatStatus);
            WriteString(writer, "Mileage", fields.Mileage);
            WriteString(writer, "Mileage Unit", fields.MileageUnit);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static void WriteString(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
        }
        else
        {
            writer.WriteString(name, value);
        }
    }

    private static List<ImageEntry> ValidateAndNameImages(
        IReadOnlyList<EvaBundleImage> selectedImages)
    {
        var ids = new HashSet<Guid>();
        var entries = new List<ImageEntry>(selectedImages.Count);
        for (var index = 0; index < selectedImages.Count; index++)
        {
            var image = selectedImages[index]
                ?? throw new InvalidDataException("A selected EVA image is missing.");
            if (image.AssetId == Guid.Empty || !ids.Add(image.AssetId))
            {
                throw new InvalidDataException("Selected EVA image identities must be non-empty and unique.");
            }

            if (!image.CustodyConfirmed)
            {
                throw new InvalidOperationException("Every selected EVA image must have confirmed custody.");
            }

            if (image.Content.IsEmpty)
            {
                throw new InvalidDataException("A selected EVA image has no retained content.");
            }

            var actualHash = Hash(image.Content.Span);
            if (!string.Equals(actualHash, image.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("A selected EVA image failed SHA-256 integrity validation.");
            }

            var name = $"images/{index + 1:D3}-{image.AssetId:N}-{SafeFileComponent(image.FileName)}";
            entries.Add(new(name, image.Content, actualHash));
        }

        return entries;
    }

    private static byte[] WriteManifest(string jsonHash, IReadOnlyList<ImageEntry> images)
    {
        var builder = new StringBuilder();
        builder.Append(jsonHash).Append("  eva.json\n");
        foreach (var image in images)
        {
            builder.Append(image.Sha256).Append("  ").Append(image.Name).Append('\n');
        }

        return new UTF8Encoding(false).GetBytes(builder.ToString());
    }

    private static byte[] WriteArchive(
        byte[] json,
        IReadOnlyList<ImageEntry> images,
        byte[] manifest)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
        {
            WriteEntry(archive, "eva.json", json);
            foreach (var image in images)
            {
                WriteEntry(archive, image.Name, image.Content.Span);
            }

            WriteEntry(archive, "manifest.sha256", manifest);
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
        Convert.ToHexString(SHA256.HashData(content));

    private sealed record ImageEntry(string Name, ReadOnlyMemory<byte> Content, string Sha256);
}
