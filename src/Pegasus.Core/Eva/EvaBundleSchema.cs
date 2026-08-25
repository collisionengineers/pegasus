using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
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
    bool IsCurrent,
    int Ordinal = 0);

public sealed record EvaBundleImages(IReadOnlyList<EvaBundleImage> RetainedImages);

public sealed record EvaBundle(
    byte[] Content,
    string Sha256,
    byte[] JsonContent,
    string JsonSha256,
    string FileName);

/// <summary>
/// CASE-019, ENG-016: the operator's export of a case as the EVA-format
/// archive. Since ENG-016 it is the only act that produces the package, and
/// its first success on a case records the once-per-case
/// <c>First sent to Engineer</c> proxy. Every success updates which workflow
/// version was exported for Assessment access.
///
/// It takes an operation key for exact replay and permanent action history,
/// but no edit lease: the export does not change the case version. What it
/// writes is one history row per distinct successful export and, on the first
/// success only, one <c>EvaFirstHandoffProxies</c> row.
/// </summary>
public sealed record ExportCaseBundleRequest(
    Guid CaseId,
    ActionActor Actor,
    string OperationKey);

public sealed record ExportCaseBundleResult(
    EvaBundle? Bundle,
    IReadOnlyList<string> UnrecordedFields,
    IReadOnlyList<string> BlockingReasons)
{
    public bool IsExported => Bundle is not null;
}

public interface IExportCaseBundle
{
    Task<ExportCaseBundleResult?> ExecuteAsync(
        ExportCaseBundleRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record EvaHandoffImageCandidate(
    Guid OccurrenceId,
    Guid DocumentId,
    Guid VersionId,
    int Version,
    string FileName,
    string MediaType,
    long ContentLength,
    string Sha256,
    DocumentSemanticRole SemanticRole,
    DocumentSource Source,
    string SourceOccurrenceIdentity,
    bool CustodyConfirmed,
    bool IsCurrent,
    bool IsLogicallyRemoved,
    bool IsThirdPartyVehicle,
    int Ordinal);

public static class EvaHandoffPolicy
{
    /// <summary>The one wording for "this case has no photographs to send".</summary>
    public const string NoRetainedImagesReason =
        "At least one stored vehicle image is required.";

    public static IReadOnlyList<EvaHandoffImageCandidate> SelectEligibleImages(
        IEnumerable<EvaHandoffImageCandidate> candidates) => candidates
        .Where(candidate => candidate.SemanticRole == DocumentSemanticRole.Image
            && candidate.CustodyConfirmed
            && candidate.IsCurrent
            && !candidate.IsLogicallyRemoved
            && !candidate.IsThirdPartyVehicle
            && candidate.MediaType is "image/jpeg" or "image/png")
        .OrderBy(candidate => candidate.Ordinal)
        .ToArray();
}

public sealed record EvaHandoffProxyRequest(
    Guid CaseId,
    string BundleSha256,
    ActionActor Actor);

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
/// The archive is the ordered thirteen-key JSON and Images/, and nothing else.
/// JSON keys, archive entries, image order, timestamps, and hashes are explicit.
/// </summary>
public static class EvaBundleSchema
{
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

    /// <summary>
    /// <paramref name="fileNameReference"/> names the archive and the JSON
    /// inside it. It is the Pegasus case reference, which is unique and
    /// already file-safe — deliberately not the <c>Reference</c> field, which
    /// since ENG-015 carries the work provider's own reference. Those can
    /// repeat across cases and contain path separators ("AKH//47743/1"), which
    /// <see cref="SafeFileComponent"/> would reduce to "1". Omitted, the
    /// reference field still names the bundle, which is what an offline replay
    /// with no case in hand wants.
    /// </summary>
    public static EvaBundle CreateOfflineReplay(
        EvaBundleSource source,
        EvaBundleImages images,
        string? fileNameReference = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(images.RetainedImages);

        var fields = ValidateSource(source);
        var reference = SafeFileComponent(
            string.IsNullOrWhiteSpace(fileNameReference)
                ? fields.Reference!
                : fileNameReference);
        var jsonName = $"EVA-{reference}.json";
        var json = WriteOrderedJson(fields);
        var jsonHash = Hash(json);
        var imageEntries = ValidateAndNameImages(images);
        var archive = WriteArchive(jsonName, json, imageEntries);

        return new(
            archive,
            Hash(archive),
            json,
            jsonHash,
            $"EVA-{reference}.zip");
    }

    private static EvaReplayFields ValidateSource(EvaBundleSource source)
    {
        ArgumentNullException.ThrowIfNull(source.Fields);
        ArgumentNullException.ThrowIfNull(source.Provenance);
        if (!string.Equals(source.MappingKey, CaseEvaMapping.MappingKey, StringComparison.Ordinal)
            || source.MappingVersion != CaseEvaMapping.MappingVersion)
        {
            throw new InvalidOperationException(
                "The EVA bundle requires the current mapping version.");
        }

        // What this method guards is the archive FORMAT: the current mapping,
        // the exact ordered field set, provenance that covers it, and values
        // that match that provenance. It never guarded the evidence bar — the
        // and a case with gaps clears it by design.
        //
        // ENG-016 (ENG-014 review finding F3): the loop below throws, and the
        // throws are the whole point. It used to also build a second,
        // normalized copy of the provenance array and return it — dead output,
        // because CreateOfflineReplay reads only the fields. The validation
        // stayed; the copy went, and with it the rebuilt EvaBundleSource, so
        // what comes back is the normalized fields themselves.
        var normalized = CaseEvaMapping.MapOfflineReplay(source.Fields);
        var values = OrderedFields(normalized).ToArray();
        if (source.Provenance.Count != FieldOrder.Length)
        {
            throw new InvalidDataException("EVA field provenance must cover the exact ordered field set.");
        }

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
                || string.IsNullOrWhiteSpace(item.Source)
                || string.IsNullOrWhiteSpace(item.SourceVersion))
            {
                throw new InvalidDataException(
                    "EVA field provenance does not match the accepted ordered field values.");
            }
        }

        return normalized;
    }

    private static List<ImageEntry> ValidateAndNameImages(EvaBundleImages images)
    {
        var ids = new HashSet<Guid>();
        var retained = new List<ValidatedImage>(images.RetainedImages.Count);
        foreach (var image in images.RetainedImages)
        {
            var validated = ValidateImage(image);
            if (!ids.Add(validated.Image.OccurrenceId))
            {
                throw new InvalidDataException(
                    "Retained EVA image occurrence identities must be unique.");
            }

            retained.Add(validated);
        }

        if (retained.Count == 0)
        {
            throw new InvalidDataException("At least one retained EVA image is required.");
        }

        if (retained.Select(item => item.Image.Ordinal).Any(value => value <= 0)
            || retained.Select(item => item.Image.Ordinal).Distinct().Count() != retained.Count)
        {
            throw new InvalidDataException("Retained EVA images require distinct persisted evidence ordinals.");
        }

        return retained
            .OrderBy(item => item.Image.Ordinal)
            .Select(CreateImageEntry)
            .ToList();
    }

    private static ValidatedImage ValidateImage(EvaBundleImage? image)
    {
        if (image is null)
        {
            throw new InvalidDataException("A retained EVA image is missing.");
        }
        if (image.OccurrenceId == Guid.Empty
            || image.DocumentId == Guid.Empty
            || image.VersionId == Guid.Empty
            || image.Version <= 0)
        {
            throw new InvalidDataException(
                "Retained EVA images require occurrence, document, and version identities.");
        }
        if (!image.CustodyConfirmed || !image.IsCurrent)
        {
            throw new InvalidOperationException(
                "Every retained EVA image must be the custody-confirmed current document version.");
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
                "A retained EVA image is missing content or source provenance.");
        }

        var actualHash = Hash(image.Content.Span);
        if (!string.Equals(actualHash, image.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A retained EVA image failed SHA-256 integrity validation.");
        }

        return new(image, actualHash);
    }

    private static ImageEntry CreateImageEntry(ValidatedImage image) => new(
        $"Images/{image.Image.Ordinal:000} {SafeFileComponent(image.Image.FileName)}",
        image.Image,
        image.Sha256);

    private static byte[] WriteOrderedJson(EvaReplayFields fields)
    {
        using var stream = new MemoryStream();
        // Three explicit choices, all of them parity with what EVA accepts.
        //
        // Indented, two spaces, which is what every known-good sample uses.
        //
        // NewLine pinned rather than left to JsonWriterOptions' default of
        // Environment.NewLine: the archive's SHA-256 is the revision
        // InputFingerprint, so a writer whose bytes depend on the host OS is
        // not the replay-identical bundle this type promises. LF is also
        // what all three known-good samples use.
        //
        // UnsafeRelaxedJsonEscaping because the predecessor extractor -- the
        // one whose output EVA actually accepts -- dumps with
        // ensure_ascii=False, so non-ASCII travels as literal UTF-8. The
        // default JavaScriptEncoder would escape it, and & < > + ' besides,
        // as \uXXXX. The name is about HTML/JS embedding: this is a file
        // written to disk and dragged into a desktop application, never
        // interpolated into markup, so that escaping buys nothing here and
        // costs the parity. A claimant name with an accent, or the en-dash
        // QDOS letters demonstrably use, would otherwise diverge.
        using (var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions
            {
                Indented = true,
                NewLine = "\n",
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }))
        {
            writer.WriteStartObject();
            foreach (var field in OrderedFields(fields))
            {
                // Every key is always present and always a string: an importer
                // reads the same thirteen keys whether or not the case knew
                // the answer. A field the case does not hold is empty, never
                // null and never absent.
                writer.WriteString(field.Name, field.Value ?? string.Empty);
            }
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static byte[] WriteArchive(
        string jsonName,
        byte[] json,
        IReadOnlyList<ImageEntry> images)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
        {
            WriteEntry(archive, jsonName, json);
            foreach (var image in images)
            {
                WriteEntry(archive, image.Name, image.Image.Content.Span);
            }
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
        EvaBundleImage Image,
        string Sha256);
}
