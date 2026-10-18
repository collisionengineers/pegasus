using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Documents;
using Pegasus.Core.CaseExport;

namespace Pegasus.Core.Tests.Qdos;

public sealed class CaseExportArchiveTests
{
    private static readonly Guid OverviewOccurrenceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DamageOccurrenceId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherOccurrenceId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task SameAcceptedInputsProduceIdenticalBundleBytesAndExactOrder()
    {
        var order = Images();

        var first = await CreateAsync(Source(), order);
        var replay = await CreateAsync(Source(), order);

        Assert.Equal(first.Content.ToArray(), replay.Content.ToArray());
        Assert.Equal(first.Sha256, replay.Sha256);
        Assert.Equal("QDOS001.zip", first.FileName);
        using var archive = new ZipArchive(new MemoryStream(first.Content.ToArray()), ZipArchiveMode.Read);
        Assert.Equal(
            [
                "QDOS001.json",
                "Images/002 overview.jpg",
                "Images/003 damage.png",
                "Images/004 other.jpg"
            ],
            archive.Entries.Select(entry => entry.FullName));

        using var json = JsonDocument.Parse(first.JsonContent);
        Assert.Equal(FieldNames, json.RootElement.EnumerateObject().Select(property => property.Name));
    }

    [Fact]
    public async Task RetainedInputSequenceDoesNotChangePersistedEvidenceOrder()
    {
        var first = await CreateAsync(Source(), Images());
        var changed = await CreateAsync(
            Source(),
            Images([OtherOccurrenceId, OverviewOccurrenceId, DamageOccurrenceId]));

        Assert.Equal(first.Sha256, changed.Sha256);
        using var archive = new ZipArchive(new MemoryStream(changed.Content.ToArray()), ZipArchiveMode.Read);
        Assert.Equal("Images/002 overview.jpg", archive.Entries[1].FullName);
        Assert.Equal("Images/003 damage.png", archive.Entries[2].FullName);
        Assert.Equal("Images/004 other.jpg", archive.Entries[3].FullName);
    }

    // Whatever order the metadata arrives in, the reader is asked for each
    // image once, in ascending ordinal, and never for two at the same time.
    [Fact]
    public async Task EachImageIsReadOnceInOrdinalOrderAndNeverAlongsideAnother()
    {
        var photographs = Images([OtherOccurrenceId, OverviewOccurrenceId, DamageOccurrenceId]);

        await CreateAsync(Source(), photographs);

        Assert.Equal<int>([2, 3, 4], photographs.ReadOrdinals);
        Assert.Equal(1, photographs.MostInFlight);
    }

    [Fact]
    public async Task OneRetainedImageIsExportedExactlyOnceWithoutPreviewSelection()
    {
        var photographs = new Photographs([DefaultPhotographs()[0]]);

        var bundle = await CreateAsync(Source(), photographs);

        using var archive = new ZipArchive(new MemoryStream(bundle.Content.ToArray()), ZipArchiveMode.Read);
        Assert.Equal(
            [
                "QDOS001.json",
                "Images/002 overview.jpg"
            ],
            archive.Entries.Select(entry => entry.FullName));
        Assert.Equal<int>([2], photographs.ReadOrdinals);
    }

    [Fact]
    public async Task ACaseWithNoPhotographExportsTheDataFileAlone()
    {
        var photographs = new Photographs([]);

        var bundle = await CreateAsync(Source(), photographs);

        Assert.Equal("QDOS001.zip", bundle.FileName);
        using var archive = new ZipArchive(new MemoryStream(bundle.Content.ToArray()), ZipArchiveMode.Read);
        Assert.Equal(["QDOS001.json"], archive.Entries.Select(entry => entry.FullName));
        Assert.Empty(photographs.ReadOrdinals);
    }

    [Fact]
    public async Task ChangedAcceptedFieldAndProvenanceCreateDifferentDeterministicBundle()
    {
        var source = Source();
        var changedSource = source with
        {
            Fields = source.Fields with { Mileage = "12001" },
            Provenance = source.Provenance
                .Select(item => item.Name == "Mileage"
                    ? item with { Value = "12001", SourceVersion = "case-data/v14" }
                    : item)
                .ToArray()
        };

        var first = await CreateAsync(source, Images());
        var changed = await CreateAsync(changedSource, Images());
        var replay = await CreateAsync(changedSource, Images());

        Assert.NotEqual(first.Sha256, changed.Sha256);
        Assert.Equal(changed.Content.ToArray(), replay.Content.ToArray());
        Assert.Equal(changed.Sha256, replay.Sha256);
    }

    [Fact]
    public async Task AStaleMappingVersionIsRejected()
    {
        var source = Source() with { MappingVersion = CaseExportMapping.MappingVersion - 1 };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateAsync(source, Images()));

        Assert.Contains("current mapping version", exception.Message, StringComparison.Ordinal);
    }

    // The archive-layout regression guard. Before this test the exported layout was
    // pinned only transitively, through two bundles hashing the same -- which
    // holds just as well when both are wrong. The archive shape and the JSON
    // bytes are now asserted directly, because both are what the importer reads.
    [Fact]
    public async Task TheArchiveIsTheIndentedJsonAndImagesAndNothingElse()
    {
        var bundle = await CreateAsync(Source(), Images());

        using var archive = new ZipArchive(new MemoryStream(bundle.Content.ToArray()), ZipArchiveMode.Read);
        var names = archive.Entries.Select(entry => entry.FullName).ToArray();

        // No manifest.sha256, no provenance.json: neither was ever an operator
        // requirement, and the known-good samples carry no companion
        // file at all.
        Assert.Equal(
            [
                "QDOS001.json",
                "Images/002 overview.jpg",
                "Images/003 damage.png",
                "Images/004 other.jpg"
            ],
            names);
        Assert.All(names, name =>
        {
            Assert.DoesNotContain(OverviewOccurrenceId.ToString("N"), name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(DamageOccurrenceId.ToString("N"), name, StringComparison.OrdinalIgnoreCase);
        });

        var json = Encoding.UTF8.GetString(bundle.JsonContent);

        // The layout every known-good sample uses: a two-space indent, LF, an
        // object per line, and no trailing newline after the closing brace.
        Assert.StartsWith("{\n  \"Work Provider\": ", json, StringComparison.Ordinal);
        Assert.EndsWith("\n}", json, StringComparison.Ordinal);

        // The newline is pinned rather than left to JsonWriterOptions' default
        // of Environment.NewLine: the archive hash is the revision fingerprint,
        // so the bytes must not depend on which OS generated them. No CR byte
        // appears anywhere, and a field's own line breaks stay escaped inside
        // its value rather than becoming layout.
        var lines = json.Split('\n');
        Assert.Equal(lines.Length - 1, json.Count(character => character == '\n'));
        Assert.DoesNotContain('\r', json);

        // One line for '{', one per field, one for '}'.
        Assert.Equal(FieldNames.Length + 2, lines.Length);
        Assert.Equal("{", lines[0]);
        Assert.Equal("}", lines[^1]);

        // Exactly two spaces of indent, and the thirteen keys in their order.
        Assert.Equal(
            FieldNames.Select(name => $"  \"{name}\": "),
            lines[1..^1].Select(line => line[..(line.IndexOf(": ", StringComparison.Ordinal) + 2)]));

        // The whole-archive digest survives the manifest's removal and is still the hash of
        // what ships: it is the revision InputFingerprint and the download's
        // Content-Digest, and only the manifest's per-entry hashes are gone.
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bundle.Content.Span)), bundle.Sha256);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bundle.JsonContent)), bundle.JsonSha256);
    }

    // The layout claim is about two files that are checked in, so it is
    // checkable. It was previously only asserted -- in a comment here and
    // another beside the writer, both of which said the samples use CRLF. They
    // do not, and nothing failed when that was wrong, because no test ever
    // opened them.
    [Fact]
    public void TheRetainedSamplesAreTheSourceOfTheNewlineConvention()
    {
        string[] samples =
        [
            Path.Combine("reference", "case_export", "Final Format Example 02.json"),
            Path.Combine("reference", "case_export", "AX_SP58WVO.json")
        ];

        foreach (var sample in samples)
        {
            var text = File.ReadAllText(Path.Combine(RepositoryRoot(), sample));
            Assert.DoesNotContain('\r', text);
            Assert.Contains('\n', text);
            // The same two-space, object-per-line, no-trailing-newline layout
            // the writer produces.
            Assert.StartsWith("{\n  \"", text, StringComparison.Ordinal);
            Assert.EndsWith("\n}", text, StringComparison.Ordinal);
        }
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Pegasus.slnx")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    // The predecessor extractor whose output the samples come from dumps with
    // ensure_ascii=False, and both retained samples carry raw non-ASCII bytes
    // and zero \u escapes. Utf8JsonWriter's DEFAULT encoder would escape every
    // one of them -- and & < > + ' besides -- so without this the parity
    // silently reverts the moment a real claimant name carries an accent or
    // circumstances text carries the en-dash QDOS letters use.
    [Fact]
    public async Task NonAsciiTravelsAsLiteralUtf8RatherThanEscapes()
    {
        var source = Source();
        const string awkward = "Mr André O’Sullivan-–Björk & Co <Ltd> + 'x'";
        var bundle = await CreateAsync(
            source with
            {
                Fields = source.Fields with { ClaimantName = awkward },
                Provenance = source.Provenance
                    .Select(item => item.Name == "Claimant Name"
                        ? item with { Value = awkward }
                        : item)
                    .ToArray()
            },
            Images());

        var json = Encoding.UTF8.GetString(bundle.JsonContent);

        Assert.Contains($"\"Claimant Name\": \"{awkward}\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u", json, StringComparison.OrdinalIgnoreCase);

        // Literal UTF-8 on the wire, not an escape sequence: the curly
        // apostrophe and the en-dash are their own bytes in the file.
        Assert.True(bundle.JsonContent.AsSpan().IndexOf("’"u8) >= 0);
        Assert.True(bundle.JsonContent.AsSpan().IndexOf("–"u8) >= 0);

        // Still valid JSON that round-trips to the value we put in.
        using var parsed = JsonDocument.Parse(bundle.JsonContent);
        Assert.Equal(awkward, parsed.RootElement.GetProperty("Claimant Name").GetString());
    }

    [Fact]
    public async Task NonCurrentOrUnconfirmedImageVersionIsRejected()
    {
        var photographs = Images();
        var unconfirmed = photographs.Images.RetainedImages
            .Select(image => image.OccurrenceId == DamageOccurrenceId
                ? image with { CustodyConfirmed = false }
                : image)
            .ToArray();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateAsync(Source(), photographs, new(unconfirmed)));

        Assert.Contains("custody-confirmed current", exception.Message, StringComparison.Ordinal);
        Assert.Empty(photographs.ReadOrdinals);
    }

    [Fact]
    public async Task ARepeatedOrdinalIsRejectedBeforeAnyImageIsRead()
    {
        var photographs = Images();
        var repeated = photographs.Images.RetainedImages
            .Select(image => image.OccurrenceId == DamageOccurrenceId
                ? image with { Ordinal = 2 }
                : image)
            .ToArray();

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateAsync(Source(), photographs, new(repeated)));

        Assert.Contains("distinct persisted evidence ordinals", exception.Message, StringComparison.Ordinal);
        Assert.Empty(photographs.ReadOrdinals);
    }

    [Fact]
    public async Task AnImageRecordedWithNoContentIsRejectedBeforeAnyImageIsRead()
    {
        var photographs = Images();
        var empty = photographs.Images.RetainedImages
            .Select(image => image.OccurrenceId == DamageOccurrenceId
                ? image with { ContentLength = 0 }
                : image)
            .ToArray();

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateAsync(Source(), photographs, new(empty)));

        Assert.Contains("missing content or source provenance", exception.Message, StringComparison.Ordinal);
        Assert.Empty(photographs.ReadOrdinals);
    }

    [Fact]
    public async Task BytesThatDoNotHashToTheRecordedSha256AreRejected()
    {
        var photographs = new Photographs(
        [
            (Image(OverviewOccurrenceId, "overview.jpg", "image/jpeg", "overview image"u8.ToArray(), 1),
                "tampered image"u8.ToArray())
        ]);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateAsync(Source(), photographs));

        Assert.Contains("failed SHA-256 integrity validation", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnImageThatIsReadBackEmptyIsRejected()
    {
        var photographs = new Photographs(
        [
            (Image(OverviewOccurrenceId, "overview.jpg", "image/jpeg", "overview image"u8.ToArray(), 1),
                Array.Empty<byte>())
        ]);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateAsync(Source(), photographs));

        Assert.Contains("missing content or source provenance", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheArchiveIsNamedByTheCaseNotByThePrincipalsOwnReference()
    {
        // The Reference field now carries the work provider's own
        // reference, which repeats across cases and can contain path
        // separators — "AKH//47743/1" would reduce to "1" and collide. The
        // archive is named by the Pegasus case reference instead.
        const string principalReference = "AKH//47743/1";
        var source = Source();
        var bundle = await CreateAsync(
            source with
            {
                Fields = source.Fields with { Reference = principalReference },
                Provenance = [.. source.Provenance.Select(field => field.Name == "Reference"
                    ? field with { Value = principalReference }
                    : field)]
            },
            Images(),
            fileNameReference: "QDOS26015");

        Assert.Equal("QDOS26015.zip", bundle.FileName);
        using var parsed = JsonDocument.Parse(bundle.JsonContent);
        Assert.Equal(principalReference, parsed.RootElement.GetProperty("Reference").GetString());
    }

    // The archive is written into a buffer sized up front, and the bundle's SHA-256 is the
    // revision fingerprint. This writes the same entries the way the archive was always
    // written, into a stream that grows by doubling, and requires the same bytes and the
    // same fingerprint. The large image is bigger than any margin the estimate carries.
    [Fact]
    public async Task ThePreSizedArchiveHasTheSameBytesAndFingerprintAsAnUnsizedOne()
    {
        var large = new byte[300_000];
        new Random(20260930).NextBytes(large);
        var small = "damage image"u8.ToArray();
        var bundle = await CreateAsync(
            Source(),
            new Photographs(
            [
                Photograph(OverviewOccurrenceId, "overview.jpg", "image/jpeg", large, 1),
                Photograph(DamageOccurrenceId, "damage.png", "image/png", small, 2)
            ]));

        using var unsized = new MemoryStream();
        using (var archive = new ZipArchive(unsized, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
        {
            AddReferenceEntry(archive, "QDOS001.json", bundle.JsonContent);
            AddReferenceEntry(archive, "Images/002 overview.jpg", large);
            AddReferenceEntry(archive, "Images/003 damage.png", small);
        }
        var expected = unsized.ToArray();

        Assert.Equal(expected, bundle.Content.ToArray());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(expected)), bundle.Sha256);
        using var read = new ZipArchive(new MemoryStream(bundle.Content.ToArray()), ZipArchiveMode.Read);
        Assert.Equal(large.Length, read.GetEntry("Images/002 overview.jpg")!.Length);
    }

    private static void AddReferenceEntry(ZipArchive archive, string name, byte[] content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
        entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        entry.ExternalAttributes = 0;
        using var entryStream = entry.Open();
        entryStream.Write(content);
    }

    private static CaseExportSource Source()
    {
        var fields = new CaseExportFields(
            "QDOS",
            "AB12 CDE",
            "Example Model",
            "Alex Example",
            "QDOS001",
            "2031-05-01",
            "2031-05-02",
            "2031-05-03",
            CaseExportMapping.ImageBasedAssessment,
            "Stationary vehicle impact",
            "No",
            "12000",
            "Miles");
        var normalized = CaseExportMapping.NormalizeFields(fields);
        var values = new[]
        {
            normalized.WorkPrincipal,
            normalized.Vrm,
            normalized.VehicleModel,
            normalized.ClaimantName,
            normalized.Reference,
            normalized.IncidentDate,
            normalized.InstructionDate,
            normalized.InspectionDate,
            normalized.InspectionAddress,
            normalized.AccidentCircumstances,
            normalized.VatStatus,
            normalized.Mileage,
            normalized.MileageUnit
        };
        return new(
            normalized,
            FieldNames.Select((name, index) => new CaseExportFieldProvenance(
                name,
                values[index]!,
                CaseExportEvidenceStatus.Accepted,
                "accepted-case-data",
                $"case-data/v{index + 1}"))
                .ToArray(),
            CaseExportMapping.MappingKey,
            CaseExportMapping.MappingVersion);
    }

    private static Task<CaseExportBundle> CreateAsync(
        CaseExportSource source,
        Photographs photographs,
        CaseExportImages? images = null,
        string? fileNameReference = null) => CaseExportArchive.CreateAsync(
        source,
        images ?? photographs.Images,
        photographs.ReadAsync,
        fileNameReference);

    private static Photographs Images(IReadOnlyList<Guid>? order = null)
    {
        var photographs = DefaultPhotographs();
        return new(order is null
            ? photographs
            : order.Select(id => photographs.Single(item => item.Image.OccurrenceId == id)).ToArray());
    }

    private static (CaseExportImage Image, byte[] Content)[] DefaultPhotographs() =>
    [
        Photograph(OverviewOccurrenceId, "overview.jpg", "image/jpeg", "overview image"u8.ToArray(), 1),
        Photograph(DamageOccurrenceId, "damage.png", "image/png", "damage image"u8.ToArray(), 2),
        Photograph(OtherOccurrenceId, "other.jpg", "image/jpeg", "other image"u8.ToArray(), 3)
    ];

    private static (CaseExportImage Image, byte[] Content) Photograph(
        Guid occurrenceId,
        string fileName,
        string mediaType,
        byte[] content,
        int sequence) => (Image(occurrenceId, fileName, mediaType, content, sequence), content);

    private static CaseExportImage Image(
        Guid occurrenceId,
        string fileName,
        string mediaType,
        byte[] content,
        int sequence) => new(
        occurrenceId,
        Guid.Parse($"aaaaaaaa-aaaa-aaaa-aaaa-{sequence:D12}"),
        Guid.Parse($"bbbbbbbb-bbbb-bbbb-bbbb-{sequence:D12}"),
        1,
        fileName,
        mediaType,
        DocumentSemanticRole.Image,
        DocumentSource.StaffUpload,
        $"staff-upload:{occurrenceId:N}",
        content.Length,
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
        CustodyConfirmed: true,
        IsCurrent: true,
        Ordinal: sequence + 1);

    /// <summary>
    /// The photographs a case holds, standing in for the custody read: the
    /// metadata the archive is given, and a reader that answers for one image
    /// at a time and notes how it was asked.
    /// </summary>
    private sealed class Photographs
    {
        private readonly Dictionary<Guid, byte[]> contents;
        private int inFlight;

        public Photographs(IReadOnlyList<(CaseExportImage Image, byte[] Content)> photographs)
        {
            Images = new([.. photographs.Select(item => item.Image)]);
            contents = photographs.ToDictionary(item => item.Image.OccurrenceId, item => item.Content);
        }

        public CaseExportImages Images { get; }

        /// <summary>The ordinal of each image asked for, in the order asked.</summary>
        public List<int> ReadOrdinals { get; } = [];

        /// <summary>The most reads that were open at the same moment.</summary>
        public int MostInFlight { get; private set; }

        public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(
            CaseExportImage image,
            CancellationToken cancellationToken)
        {
            ReadOrdinals.Add(image.Ordinal);
            MostInFlight = Math.Max(MostInFlight, ++inFlight);
            try
            {
                // A read that yields would be overlapped by an archive that
                // asked for the next image before this one was answered.
                await Task.Yield();
                return contents[image.OccurrenceId];
            }
            finally
            {
                inFlight--;
            }
        }
    }

    private static readonly string[] FieldNames =
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
}
