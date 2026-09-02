using System.Security.Cryptography;
using System.Text.Json;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Intake;
using Xunit.Abstractions;

namespace Pegasus.IntegrationTests;

[Trait("Category", "Corpus")]
public sealed class PrincipalIdentificationCorpusEvidenceTests(ITestOutputHelper output)
{
    private static readonly DateTimeOffset ReceivedAtUtc =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [PrincipalIdentificationCorpusFact]
    public async Task EveryLocallyPresentOriginalKeepsItsHashAndReachesTheRealReader()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(CorpusPackage.PackagePath));
        var reader = new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System);
        var statuses = new Dictionary<IntakeSourceReadStatus, int>();
        var processed = 0;

        foreach (var item in document.RootElement.GetProperty("evidenceItems").EnumerateArray())
        {
            var path = CorpusPackage.ResolveExistingLocation(item);
            Assert.True(path is not null, $"No local original resolves for {item.GetProperty("id").GetString()}.");
            var bytes = await File.ReadAllBytesAsync(path!);
            var actualHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            Assert.Equal(item.GetProperty("sha256").GetString(), actualHash);

            var readResult = await reader.ReadAsync(
                new IntakeSource(
                    Path.GetFileName(path),
                    MediaType(path),
                    bytes,
                    ReceivedAtUtc,
                    "principal-identification-corpus",
                    new(
                        Path.GetExtension(path).Equals(".eml", StringComparison.OrdinalIgnoreCase)
                            ? IntakeSourceChannel.Mailbox
                            : IntakeSourceChannel.ManualUpload,
                        $"principal-evidence-{processed:00000}")),
                CancellationToken.None);
            statuses[readResult.Status] = statuses.GetValueOrDefault(readResult.Status) + 1;
            Assert.NotEqual("unspecified_reader", readResult.ReaderKey);
            processed++;
        }

        output.WriteLine(
            $"principal evidence: processed={processed}; "
            + string.Join(
                "; ",
                statuses.OrderBy(item => item.Key).Select(item => $"{item.Key}={item.Value}")));
        Assert.Equal(
            document.RootElement.GetProperty("evidenceItems").GetArrayLength(),
            processed);
        Assert.True(statuses.GetValueOrDefault(IntakeSourceReadStatus.Readable) > 0);
    }

    private static string MediaType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".eml" => "message/rfc822",
        ".msg" => "application/vnd.ms-outlook",
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".rtf" => "application/rtf",
        _ => "application/octet-stream"
    };
}

internal static class CorpusPackage
{
    public static string RepositoryRoot => FindRepositoryRoot();

    public static string PackagePath => Path.Combine(
        RepositoryRoot,
        "reference",
        "workproviders-and-repairers",
        "principal-identification-corpus.v1.json");

    public static string? CollisionSpikeRoot =>
        Environment.GetEnvironmentVariable("COLLISIONSPIKE_ROOT")
        ?? FindCollisionSpikeRoot();

    public static bool SourcesArePresent =>
        File.Exists(PackagePath)
        && Directory.Exists(QdosCorpus.Root)
        && Directory.Exists(CollisionSpikeRoot);

    public static string? ResolveExistingLocation(JsonElement evidence)
    {
        foreach (var location in evidence.GetProperty("sourceLocations").EnumerateArray())
        {
            var repository = location.GetProperty("repository").GetString();
            var root = repository switch
            {
                "pegasus-local-corpus" => QdosCorpus.Root,
                "collisionspike" => CollisionSpikeRoot,
                _ => null
            };
            if (root is null)
            {
                continue;
            }

            var relativePath = location.GetProperty("relativePath").GetString()!;
            var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static string? FindCollisionSpikeRoot()
    {
        var directory = new DirectoryInfo(RepositoryRoot);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "collisionsuite", "active", "collisionspike");
            if (File.Exists(Path.Combine(candidate, "services", "engine", "cedocumentmapper_v2", "providers.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}

internal sealed class PrincipalIdentificationCorpusFactAttribute : FactAttribute
{
    public PrincipalIdentificationCorpusFactAttribute()
    {
        if (!CorpusPackage.SourcesArePresent)
        {
            Skip = "The immutable Pegasus corpus or read-only CollisionSpike checkout is not present on this machine.";
        }
    }
}
