using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace Pegasus.Infrastructure.Vision;

/// <summary>
/// The vendored ADR-0018 model set: embedded bytes verified against the
/// hash-pinned manifest before any session is created. A hash mismatch makes
/// the engine `Unavailable`; nothing is ever downloaded at runtime.
/// </summary>
internal sealed class VisionModelSet
{
    private const string ResourcePrefix = "Pegasus.Infrastructure.Vision.Models.";
    private const string ManifestResource = ResourcePrefix + "vision-models-manifest.json";
    private const string DetectionRole = "plate-detection";
    private const string RecognitionRole = "plate-recognition";

    private VisionModelSet(
        string engineKey,
        string engineVersion,
        byte[] detectionModel,
        byte[] recognitionModel,
        string modelHashes)
    {
        EngineKey = engineKey;
        EngineVersion = engineVersion;
        DetectionModel = detectionModel;
        RecognitionModel = recognitionModel;
        ModelHashes = modelHashes;
    }

    public string EngineKey { get; }

    public string EngineVersion { get; }

    public byte[] DetectionModel { get; }

    public byte[] RecognitionModel { get; }

    /// <summary>Compact pinned-hash summary recorded with every suggestion.</summary>
    public string ModelHashes { get; }

    public static VisionModelSet LoadVerified()
    {
        var assembly = typeof(VisionModelSet).Assembly;
        var manifest = ReadManifest(assembly);
        byte[]? detection = null;
        byte[]? recognition = null;
        var hashSummaries = new List<string>(manifest.Models.Count);
        foreach (var model in manifest.Models)
        {
            var bytes = ReadResource(assembly, ResourcePrefix + model.Name);
            var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.Equals(actual, model.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new VisionModelIntegrityException(
                    $"Embedded model '{model.Name}' does not match its pinned SHA-256.");
            }

            hashSummaries.Add($"{model.Role}={model.Sha256}");
            switch (model.Role)
            {
                case DetectionRole:
                    detection = bytes;
                    break;
                case RecognitionRole:
                    recognition = bytes;
                    break;
            }
        }

        if (detection is null || recognition is null)
        {
            throw new VisionModelIntegrityException(
                "The vision model manifest does not name both a detection and a recognition model.");
        }

        return new VisionModelSet(
            manifest.EngineKey,
            manifest.EngineVersion,
            detection,
            recognition,
            string.Join(';', hashSummaries));
    }

    private static readonly JsonSerializerOptions ManifestJsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    private static VisionModelManifest ReadManifest(Assembly assembly)
    {
        var bytes = ReadResource(assembly, ManifestResource);
        return JsonSerializer.Deserialize<VisionModelManifest>(bytes, ManifestJsonOptions)
            ?? throw new VisionModelIntegrityException("The vision model manifest is empty.");
    }

    private static byte[] ReadResource(Assembly assembly, string logicalName)
    {
        using var stream = assembly.GetManifestResourceStream(logicalName)
            ?? throw new VisionModelIntegrityException(
                $"Embedded vision resource '{logicalName}' is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private sealed record VisionModelManifest(
        string EngineKey,
        string EngineVersion,
        IReadOnlyList<VisionModelManifestEntry> Models);

    private sealed record VisionModelManifestEntry(
        string Name,
        string Role,
        string OriginUrl,
        string Sha256);
}

public sealed class VisionModelIntegrityException(string message) : Exception(message);
