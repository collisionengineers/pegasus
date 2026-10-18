using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Core.CaseExport;

public sealed record CaseExportFields(
    string? WorkPrincipal,
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

public sealed record CaseExportImage(
    Guid OccurrenceId,
    Guid DocumentId,
    Guid VersionId,
    int Version,
    string FileName,
    string MediaType,
    DocumentSemanticRole SemanticRole,
    DocumentSource Source,
    string SourceOccurrenceIdentity,
    long ContentLength,
    string Sha256,
    bool CustodyConfirmed,
    bool IsCurrent,
    int Ordinal = 0);

public sealed record CaseExportImages(IReadOnlyList<CaseExportImage> RetainedImages);

/// <summary>
/// One retained image's bytes, asked for when the archive reaches it. The
/// archive checks the bytes against the image's <c>Sha256</c> and lets go of
/// them before it asks for the next, so a caller supplies one image at a
/// time and never holds the set.
/// </summary>
public delegate ValueTask<ReadOnlyMemory<byte>> CaseExportImageContentReader(
    CaseExportImage image,
    CancellationToken cancellationToken);

public sealed record CaseExportBundle(
    ReadOnlyMemory<byte> Content,
    string Sha256,
    byte[] JsonContent,
    string JsonSha256,
    string FileName);

/// <summary>
/// The operator's export of a case: the thirteen-field JSON and the eligible
/// photographs as one archive. It is a plain download.
///
/// It takes an operation key for exact replay and permanent action history,
/// but no edit lease: the export changes no case state or version. What it
/// writes is one history row per distinct successful export.
/// </summary>
public sealed record ExportCaseBundleRequest(
    Guid CaseId,
    ActionActor Actor,
    string OperationKey);

public sealed record ExportCaseBundleResult(
    CaseExportBundle? Bundle,
    IReadOnlyList<string> UnrecordedFields,
    IReadOnlyList<string> BlockingReasons);

public interface IExportCaseBundle
{
    Task<ExportCaseBundleResult?> ExecuteAsync(
        ExportCaseBundleRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CaseExportImageCandidate(
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
    IReadOnlyList<Guid> TagIds,
    int Ordinal);

public static class CaseExportPolicy
{
    /// <summary>
    /// The history event a successful bundle export writes. The one spelling,
    /// so a surface asking "has this case been exported?" reads the same word
    /// the export wrote.
    /// </summary>
    public const string BundleExportedHistoryEventKind = "case_exported";

    public static IReadOnlyList<CaseExportImageCandidate> SelectEligibleImages(
        IEnumerable<CaseExportImageCandidate> candidates) => candidates
        .Where(candidate => candidate.SemanticRole == DocumentSemanticRole.Image
            && candidate.CustodyConfirmed
            && candidate.IsCurrent
            && !candidate.IsLogicallyRemoved
            && !candidate.TagIds.Contains(ImageTagVocabulary.ThirdPartyId)
            && candidate.MediaType is "image/jpeg" or "image/png")
        .OrderBy(candidate => candidate.Ordinal)
        .ToArray();
}

/// <summary>
/// Produces replay-identical case export archives. It makes no call of its own:
/// each image's bytes come from the reader the caller supplies, one image at a time.
/// The archive is the ordered thirteen-key JSON and Images/, and nothing else.
/// JSON keys, archive entries, image order, timestamps, and hashes are explicit.
/// </summary>
public static class CaseExportArchive
{
    private static readonly DateTimeOffset DeterministicTimestamp =
        new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private const string MissingContentOrProvenance =
        "A retained export image is missing content or source provenance.";

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
    /// now carries the Principal's own reference. Those can
    /// repeat across cases and contain path separators ("AKH//47743/1"), which
    /// <see cref="SafeFileComponent"/> would reduce to "1". Omitted, the
    /// reference field still names the bundle, which is what an offline replay
    /// with no case in hand wants.
    ///
    /// <paramref name="images"/> is metadata only. Every check that needs no
    /// bytes runs before <paramref name="readContent"/> is called once; the
    /// archive then asks for one image at a time, in ascending ordinal,
    /// checks its bytes, writes its entry and drops the bytes before it asks
    /// for the next.
    /// </summary>
    public static async Task<CaseExportBundle> CreateAsync(
        CaseExportSource source,
        CaseExportImages images,
        CaseExportImageContentReader readContent,
        string? fileNameReference = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(images.RetainedImages);
        ArgumentNullException.ThrowIfNull(readContent);

        var fields = ValidateSource(source);
        var reference = SafeFileComponent(
            string.IsNullOrWhiteSpace(fileNameReference)
                ? fields.Reference!
                : fileNameReference);
        var jsonName = $"{reference}.json";
        var json = WriteOrderedJson(fields);
        var jsonHash = Hash(json);
        var imageEntries = ValidateAndNameImages(images);
        var archive = await WriteArchiveAsync(
            jsonName, json, imageEntries, readContent, cancellationToken);

        return new(
            archive,
            Hash(archive.Span),
            json,
            jsonHash,
            $"{reference}.zip");
    }

    private static CaseExportFields ValidateSource(CaseExportSource source)
    {
        ArgumentNullException.ThrowIfNull(source.Fields);
        ArgumentNullException.ThrowIfNull(source.Provenance);
        if (!string.Equals(source.MappingKey, CaseExportMapping.MappingKey, StringComparison.Ordinal)
            || source.MappingVersion != CaseExportMapping.MappingVersion)
        {
            throw new InvalidOperationException(
                "The case export requires the current mapping version.");
        }

        // What this method guards is the archive FORMAT: the current mapping,
        // the exact ordered field set, provenance that covers it, and values
        // that match that provenance. It never guarded the evidence bar — the
        // and a case with gaps clears it by design.
        //
        // The loop below throws, and the
        // throws are the whole point. It used to also build a second,
        // normalized copy of the provenance array and return it — dead output,
        // because Create reads only the fields. The validation
        // stayed; the copy went, and with it the rebuilt CaseExportSource, so
        // what comes back is the normalized fields themselves.
        var normalized = CaseExportMapping.NormalizeFields(source.Fields);
        var values = OrderedFields(normalized).ToArray();
        if (source.Provenance.Count != FieldOrder.Length)
        {
            throw new InvalidDataException("Export field provenance must cover the exact ordered field set.");
        }

        for (var index = 0; index < FieldOrder.Length; index++)
        {
            var item = source.Provenance[index]
                ?? throw new InvalidDataException("An export field provenance entry is missing.");
            var field = values[index];
            if (!string.Equals(item.Name, FieldOrder[index], StringComparison.Ordinal)
                || !string.Equals(
                    CaseExportMapping.NormalizeFields(FieldWithValue(item.Name, item.Value))
                        .GetValue(item.Name),
                    field.Value,
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(item.Source)
                || string.IsNullOrWhiteSpace(item.SourceVersion))
            {
                throw new InvalidDataException(
                    "Export field provenance does not match the accepted ordered field values.");
            }
        }

        return normalized;
    }

    private static List<ImageEntry> ValidateAndNameImages(CaseExportImages images)
    {
        var ids = new HashSet<Guid>();
        var retained = new List<CaseExportImage>(images.RetainedImages.Count);
        foreach (var image in images.RetainedImages)
        {
            ValidateImage(image);
            if (!ids.Add(image.OccurrenceId))
            {
                throw new InvalidDataException(
                    "Retained export image occurrence identities must be unique.");
            }

            retained.Add(image);
        }

        if (retained.Select(item => item.Ordinal).Any(value => value <= 0)
            || retained.Select(item => item.Ordinal).Distinct().Count() != retained.Count)
        {
            throw new InvalidDataException("Retained export images require distinct persisted evidence ordinals.");
        }

        return retained
            .OrderBy(item => item.Ordinal)
            .Select(CreateImageEntry)
            .ToList();
    }

    private static void ValidateImage(CaseExportImage? image)
    {
        if (image is null)
        {
            throw new InvalidDataException("A retained export image is missing.");
        }
        if (image.OccurrenceId == Guid.Empty
            || image.DocumentId == Guid.Empty
            || image.VersionId == Guid.Empty
            || image.Version <= 0)
        {
            throw new InvalidDataException(
                "Retained export images require occurrence, document, and version identities.");
        }
        if (!image.CustodyConfirmed || !image.IsCurrent)
        {
            throw new InvalidOperationException(
                "Every retained export image must be the custody-confirmed current document version.");
        }
        if (image.SemanticRole != DocumentSemanticRole.Image
            || !IsSupportedImageMediaType(image.MediaType))
        {
            throw new InvalidDataException("Only retained JPEG or PNG image documents may enter the export.");
        }
        if (string.IsNullOrWhiteSpace(image.FileName)
            || string.IsNullOrWhiteSpace(image.SourceOccurrenceIdentity)
            || image.ContentLength <= 0)
        {
            throw new InvalidDataException(MissingContentOrProvenance);
        }
    }

    // The checks that need the bytes, made on the one image just read.
    private static void ValidateContent(CaseExportImage image, ReadOnlyMemory<byte> content)
    {
        if (content.IsEmpty)
        {
            throw new InvalidDataException(MissingContentOrProvenance);
        }
        if (!string.Equals(Hash(content.Span), image.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A retained export image failed SHA-256 integrity validation.");
        }
    }

    private static ImageEntry CreateImageEntry(CaseExportImage image) => new(
        $"Images/{image.Ordinal:000} {SafeFileComponent(image.FileName)}",
        image);

    private static byte[] WriteOrderedJson(CaseExportFields fields)
    {
        using var stream = new MemoryStream();
        // Three explicit choices, all of them parity with the known-good samples.
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
        // one whose output the known-good samples come from -- dumps with
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

    private static async Task<ReadOnlyMemory<byte>> WriteArchiveAsync(
        string jsonName,
        byte[] json,
        IReadOnlyList<ImageEntry> images,
        CaseExportImageContentReader readContent,
        CancellationToken cancellationToken)
    {
        // Sized up front so the stream does not grow by doubling and copy the archive each
        // time. Only the capacity is chosen here; the bytes written do not depend on it.
        using var stream = new MemoryStream(ArchiveCapacity(jsonName, json, images));
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
        {
            WriteEntry(archive, jsonName, json);
            foreach (var image in images)
            {
                // One read at a time: the next image is not asked for until this
                // one's entry is written.
                await WriteImageEntryAsync(archive, image, readContent, cancellationToken);
            }
        }

        // The buffer is the archive's own; the stream is not used again.
        return stream.GetBuffer().AsMemory(0, (int)stream.Length);
    }

    // The image's bytes live in this method alone, so they are gone before the
    // caller asks for the next image.
    private static async Task WriteImageEntryAsync(
        ZipArchive archive,
        ImageEntry image,
        CaseExportImageContentReader readContent,
        CancellationToken cancellationToken)
    {
        var content = await readContent(image.Image, cancellationToken);
        ValidateContent(image.Image, content);
        WriteEntry(archive, image.Name, content.Span);
    }

    // The archive stores every entry uncompressed, so its size is the entries' content plus
    // a fixed cost per entry: a 30-byte local header and a 46-byte central-directory record,
    // each of which carries the entry name, and the 22-byte end record. The margin covers
    // any extra field the writer adds. A short estimate only means the stream grows once.
    private static int ArchiveCapacity(string jsonName, byte[] json, IReadOnlyList<ImageEntry> images)
    {
        const int PerEntry = 30 + 46 + 32;
        const int EndRecord = 22 + 32;
        var total = EndRecord + PerEntry + json.Length + 2L * Encoding.UTF8.GetByteCount(jsonName);
        foreach (var image in images)
        {
            total += PerEntry + image.Image.ContentLength + 2L * Encoding.UTF8.GetByteCount(image.Name);
        }

        return (int)Math.Min(total, Array.MaxLength);
    }

    private static void WriteEntry(ZipArchive archive, string name, ReadOnlySpan<byte> content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
        entry.LastWriteTime = DeterministicTimestamp;
        entry.ExternalAttributes = 0;
        using var entryStream = entry.Open();
        entryStream.Write(content);
    }

    private static IEnumerable<(string Name, string? Value)> OrderedFields(CaseExportFields fields)
    {
        yield return ("Work Provider", fields.WorkPrincipal);
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

    private static CaseExportFields FieldWithValue(string name, string value) => name switch
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
        _ => throw new InvalidDataException($"Unknown export field '{name}'.")
    };

    private static string? GetValue(this CaseExportFields fields, string name) => name switch
    {
        "Work Provider" => fields.WorkPrincipal,
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
        _ => throw new InvalidDataException($"Unknown export field '{name}'.")
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

    private sealed record ImageEntry(string Name, CaseExportImage Image);
}
