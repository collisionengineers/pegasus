using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;

namespace Pegasus.Core.Tests.Qdos;

public sealed class EvaBundleContractTests
{
    private static readonly Guid OverviewOccurrenceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DamageOccurrenceId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherOccurrenceId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void SameAcceptedInputsProduceIdenticalBundleBytesAndExactOrder()
    {
        var order = Images();

        var first = EvaBundleSchema.CreateOfflineReplay(Source(), order);
        var replay = EvaBundleSchema.CreateOfflineReplay(Source(), order);

        Assert.Equal(first.Content, replay.Content);
        Assert.Equal(first.Sha256, replay.Sha256);
        Assert.Equal("EVA-QDOS001.zip", first.FileName);
        using var archive = new ZipArchive(new MemoryStream(first.Content), ZipArchiveMode.Read);
        Assert.Equal(
            [
                "EVA-QDOS001.json",
                "Images/002 overview.jpg",
                "Images/003 damage.png",
                "Images/004 other.jpg"
            ],
            archive.Entries.Select(entry => entry.FullName));

        using var eva = JsonDocument.Parse(first.JsonContent);
        Assert.Equal(FieldNames, eva.RootElement.EnumerateObject().Select(property => property.Name));
    }

    [Fact]
    public void RetainedInputSequenceDoesNotChangePersistedEvidenceOrder()
    {
        var first = EvaBundleSchema.CreateOfflineReplay(Source(), Images());
        var changed = EvaBundleSchema.CreateOfflineReplay(
            Source(),
            Images([OtherOccurrenceId, OverviewOccurrenceId, DamageOccurrenceId]));

        Assert.Equal(first.Sha256, changed.Sha256);
        using var archive = new ZipArchive(new MemoryStream(changed.Content), ZipArchiveMode.Read);
        Assert.Equal("Images/002 overview.jpg", archive.Entries[1].FullName);
        Assert.Equal("Images/003 damage.png", archive.Entries[2].FullName);
        Assert.Equal("Images/004 other.jpg", archive.Entries[3].FullName);
    }

    [Fact]
    public void OneRetainedImageIsExportedExactlyOnceWithoutPreviewSelection()
    {
        var bundle = EvaBundleSchema.CreateOfflineReplay(
            Source(),
            new([Images().RetainedImages[0]]));

        using var archive = new ZipArchive(new MemoryStream(bundle.Content), ZipArchiveMode.Read);
        Assert.Equal(
            [
                "EVA-QDOS001.json",
                "Images/002 overview.jpg"
            ],
            archive.Entries.Select(entry => entry.FullName));
    }

    [Fact]
    public void ChangedAcceptedFieldAndProvenanceCreateDifferentDeterministicBundle()
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

        var first = EvaBundleSchema.CreateOfflineReplay(source, Images());
        var changed = EvaBundleSchema.CreateOfflineReplay(changedSource, Images());
        var replay = EvaBundleSchema.CreateOfflineReplay(changedSource, Images());

        Assert.NotEqual(first.Sha256, changed.Sha256);
        Assert.Equal(changed.Content, replay.Content);
        Assert.Equal(changed.Sha256, replay.Sha256);
    }

    [Fact]
    public void MissingAcceptedMappingVersionEvidenceIsRejected()
    {
        var source = Source() with { MappingAcceptanceEvidence = " " };

        var exception = Assert.Throws<InvalidOperationException>(
            () => EvaBundleSchema.CreateOfflineReplay(source, Images()));

        Assert.Contains("accepted mapping/config version", exception.Message, StringComparison.Ordinal);
    }

    // ENG-014's regression guard. Before this test the exported layout was
    // pinned only transitively, through two bundles hashing the same -- which
    // holds just as well when both are wrong. The archive shape and the JSON
    // bytes are now asserted directly, because both are what EVA reads.
    [Fact]
    public void TheArchiveIsTheIndentedJsonAndImagesAndNothingElse()
    {
        var bundle = EvaBundleSchema.CreateOfflineReplay(Source(), Images());

        using var archive = new ZipArchive(new MemoryStream(bundle.Content), ZipArchiveMode.Read);
        var names = archive.Entries.Select(entry => entry.FullName).ToArray();

        // No manifest.sha256, no provenance.json: neither was ever an operator
        // requirement, and the three known-good EVA samples carry no companion
        // file at all.
        Assert.Equal(
            [
                "EVA-QDOS001.json",
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

        // The layout every known-good sample uses: a two-space indent, CRLF,
        // an object per line, and no trailing newline after the closing brace.
        Assert.StartsWith("{\r\n  \"Work Provider\": ", json, StringComparison.Ordinal);
        Assert.EndsWith("\r\n}", json, StringComparison.Ordinal);

        // The newline is pinned rather than left to JsonWriterOptions' default
        // of Environment.NewLine: the archive hash is the revision fingerprint,
        // so the bytes must not depend on which OS generated them. Every '\n'
        // in the file therefore belongs to a "\r\n", and a field's own line
        // breaks stay escaped inside its value rather than becoming layout.
        var lines = json.Split("\r\n");
        Assert.Equal(lines.Length - 1, json.Count(character => character == '\n'));
        Assert.Equal(lines.Length - 1, json.Count(character => character == '\r'));

        // One line for '{', one per field, one for '}'.
        Assert.Equal(FieldNames.Length + 2, lines.Length);
        Assert.Equal("{", lines[0]);
        Assert.Equal("}", lines[^1]);

        // Exactly two spaces of indent, and the thirteen keys in their order.
        Assert.Equal(
            FieldNames.Select(name => $"  \"{name}\": "),
            lines[1..^1].Select(line => line[..(line.IndexOf(": ", StringComparison.Ordinal) + 2)]));

        // The whole-archive digest survives ENG-014 and is still the hash of
        // what ships: it is the revision InputFingerprint and the download's
        // Content-Digest, and only the manifest's per-entry hashes are gone.
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bundle.Content)), bundle.Sha256);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bundle.JsonContent)), bundle.JsonSha256);
    }

    [Fact]
    public void NonCurrentOrUnconfirmedImageVersionIsRejected()
    {
        var images = Images();
        var unconfirmed = images.RetainedImages
            .Select(image => image.OccurrenceId == DamageOccurrenceId
                ? image with { CustodyConfirmed = false }
                : image)
            .ToArray();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            EvaBundleSchema.CreateOfflineReplay(
                Source(),
                images with { RetainedImages = unconfirmed }));

        Assert.Contains("custody-confirmed current", exception.Message, StringComparison.Ordinal);
    }

    private static EvaBundleSource Source()
    {
        var fields = new EvaReplayFields(
            "QDOS",
            "AB12 CDE",
            "Example Model",
            "Alex Example",
            "QDOS001",
            "2031-05-01",
            "2031-05-02",
            "2031-05-03",
            CaseEvaMapping.ImageBasedAssessment,
            "Stationary vehicle impact",
            "No",
            "12000",
            "Miles");
        var normalized = CaseEvaMapping.MapOfflineReplay(fields);
        var values = new[]
        {
            normalized.WorkProvider,
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
            FieldNames.Select((name, index) => new EvaFieldProvenance(
                name,
                values[index]!,
                EvaEvidenceStatus.Accepted,
                "accepted-case-data",
                $"case-data/v{index + 1}"))
                .ToArray(),
            CaseEvaMapping.MappingKey,
            CaseEvaMapping.MappingVersion,
            "accepted-evidence:test");
    }

    private static EvaBundleImages Images(IReadOnlyList<Guid>? order = null)
    {
        var images = new[]
        {
            Image(OverviewOccurrenceId, "overview.jpg", "image/jpeg", "overview image"u8.ToArray(), 1),
            Image(DamageOccurrenceId, "damage.png", "image/png", "damage image"u8.ToArray(), 2),
            Image(OtherOccurrenceId, "other.jpg", "image/jpeg", "other image"u8.ToArray(), 3)
        };
        var ordered = order is null
            ? images
            : order.Select(id => images.Single(image => image.OccurrenceId == id)).ToArray();
        return new(ordered);
    }

    private static EvaBundleImage Image(
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
        content,
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
        CustodyConfirmed: true,
        IsCurrent: true,
        Ordinal: sequence + 1);

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
