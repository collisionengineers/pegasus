using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Intake;

public interface IIntakeEvaluationReportStore
{
    Task<string> StoreReportAsync(
        ReadOnlyMemory<byte> report,
        CancellationToken cancellationToken);

    Task<ReadOnlyMemory<byte>?> ReadReportAsync(
        string reportKey,
        CancellationToken cancellationToken);
}

internal sealed partial class FileSystemIntakeArtifactStore(string rootPath)
    : IIntakeArtifactStore, IIntakeEvaluationReportStore
{
    private readonly string rootPath = Path.GetFullPath(rootPath);

    public async Task<string> StoreAsync(
        string contentHash,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        var normalisedHash = NormaliseHash(contentHash);
        var actualHash = Convert.ToHexString(SHA256.HashData(content.Span));
        if (!actualHash.Equals(normalisedHash, StringComparison.Ordinal))
        {
            throw new IntakeArtifactIntegrityException();
        }

        var storageKey = $"sha256/{normalisedHash[..2]}/{normalisedHash}";
        await StoreImmutableAsync(
            Resolve(storageKey),
            normalisedHash,
            content,
            cancellationToken);
        return storageKey;
    }

    public async Task<ReadOnlyMemory<byte>?> ReadAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        var path = Resolve(storageKey);
        if (!File.Exists(path))
        {
            return null;
        }

        var content = await File.ReadAllBytesAsync(path, cancellationToken);
        var expectedHash = Path.GetFileName(path);
        var actualHash = Convert.ToHexString(SHA256.HashData(content));
        if (!actualHash.Equals(expectedHash, StringComparison.Ordinal))
        {
            throw new IntakeArtifactIntegrityException();
        }

        return content;
    }
    public async Task<string> StoreReportAsync(
        ReadOnlyMemory<byte> report,
        CancellationToken cancellationToken)
    {
        if (report.IsEmpty)
        {
            throw new ArgumentException("An evaluation report is required.", nameof(report));
        }

        var reportHash = Convert.ToHexString(SHA256.HashData(report.Span));
        var reportKey = $"evaluation-reports/sha256/{reportHash[..2]}/{reportHash}.json";
        await StoreImmutableAsync(
            ResolveReport(reportKey),
            reportHash,
            report,
            cancellationToken);
        return reportKey;
    }

    public async Task<ReadOnlyMemory<byte>?> ReadReportAsync(
        string reportKey,
        CancellationToken cancellationToken)
    {
        var path = ResolveReport(reportKey);
        if (!File.Exists(path))
        {
            return null;
        }

        var content = await File.ReadAllBytesAsync(path, cancellationToken);
        var expectedHash = Path.GetFileNameWithoutExtension(path);
        var actualHash = Convert.ToHexString(SHA256.HashData(content));
        if (!actualHash.Equals(expectedHash, StringComparison.Ordinal))
        {
            throw new IntakeArtifactIntegrityException();
        }

        return content;
    }

    private static async Task StoreImmutableAsync(
        string destination,
        string expectedHash,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(destination))
        {
            await VerifyFileAsync(destination, expectedHash, content.Length, cancellationToken);
            return;
        }

        var temporary = Path.Combine(
            Path.GetDirectoryName(destination)!,
            $".{expectedHash}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                             temporary,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(content, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            try
            {
                File.Move(temporary, destination, overwrite: false);
            }
            catch (IOException) when (File.Exists(destination))
            {
                await VerifyFileAsync(destination, expectedHash, content.Length, cancellationToken);
            }
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static async Task VerifyFileAsync(
        string path,
        string expectedHash,
        long? expectedLength,
        CancellationToken cancellationToken)
    {
        var file = new FileInfo(path);
        if (expectedLength is not null && file.Length != expectedLength.Value)
        {
            throw new IntakeArtifactIntegrityException();
        }

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
        if (!actualHash.Equals(expectedHash, StringComparison.Ordinal))
        {
            throw new IntakeArtifactIntegrityException();
        }
    }

    private string Resolve(string storageKey)
    {
        var segments = storageKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 3
            || !segments[0].Equals("sha256", StringComparison.Ordinal)
            || segments[1].Length != 2
            || !HashRegex().IsMatch(segments[2])
            || !segments[2].StartsWith(segments[1], StringComparison.Ordinal))
        {
            throw new ArgumentException("The artifact storage key is invalid.", nameof(storageKey));
        }

        var path = Path.GetFullPath(Path.Combine(rootPath, segments[0], segments[1], segments[2]));
        var requiredPrefix = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;
        if (!path.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The artifact storage key is outside the configured root.", nameof(storageKey));
        }

        return path;
    }

    private string ResolveReport(string reportKey)
    {
        var segments = reportKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 4
            || !segments[0].Equals("evaluation-reports", StringComparison.Ordinal)
            || !segments[1].Equals("sha256", StringComparison.Ordinal)
            || segments[2].Length != 2
            || !Path.GetFileName(segments[3]).Equals(segments[3], StringComparison.Ordinal)
            || !segments[3].EndsWith(".json", StringComparison.Ordinal)
            || !HashRegex().IsMatch(Path.GetFileNameWithoutExtension(segments[3]))
            || !segments[3].StartsWith(segments[2], StringComparison.Ordinal))
        {
            throw new ArgumentException("The evaluation report key is invalid.", nameof(reportKey));
        }

        var path = Path.GetFullPath(Path.Combine(rootPath, segments[0], segments[1], segments[2], segments[3]));
        var reportRoot = Path.GetFullPath(Path.Combine(rootPath, "evaluation-reports"));
        var requiredPrefix = reportRoot.EndsWith(Path.DirectorySeparatorChar)
            ? reportRoot
            : reportRoot + Path.DirectorySeparatorChar;
        if (!path.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The evaluation report key is outside the configured root.",
                nameof(reportKey));
        }

        return path;
    }

    private static string NormaliseHash(string contentHash)
    {
        var value = contentHash.ToUpperInvariant();
        return HashRegex().IsMatch(value)
            ? value
            : throw new ArgumentException("A SHA-256 content hash is required.", nameof(contentHash));
    }

    [GeneratedRegex("^[0-9A-F]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex HashRegex();
}
