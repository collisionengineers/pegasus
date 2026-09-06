using System.Security.Cryptography;
using Pegasus.Core.Intake;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Pegasus.IntegrationTests;

public sealed class MultiFormatGenuineCorpusWebTests(ITestOutputHelper output)
{
    private const string PinnedDocHash = "F4FE03C41F7B1B43998A33196FE5EE0F40E94EB7DD199FE12003F929D2229139";
    private const string PinnedMsgHash = "7E4C50D5E0A26A34A813797D1739D17AC661E04D65C2A8A2330A3194FCBEEDC1";
    private const string PinnedJpegHash = "8E066F1AA7CD365274CA77346CCA372E4E3D2A2D359A0158DEA0A0ECAF0BE65D";
    private const string PinnedPngHash = "01039929CBDDFB193D88B155AEE12C9EF144081CA5EFD0BF22371B2813D9B7DA";
    private const string PinnedDocxHash = "05415183569F8B71FCD899E5385B6BE6E24FB5F5AD90D43DAEE107B684D3F7FD";

    [GenuineFormatCorpusFact(".doc", PinnedDocHash)]
    [Trait("Category", "Corpus")]
    public async Task GenuineDocIsRetainedInNeedsSortingWithoutReference()
    {
        var receipt = await UploadSelectedAsync(".doc", PinnedDocHash);

        AssertNeedsSortingWithoutReferenceOrOcr(receipt);
        WriteAggregate("DOC", receipt.Decision);
    }

    [GenuineFormatCorpusFact(".msg", PinnedMsgHash)]
    [Trait("Category", "Corpus")]
    public async Task GenuineMsgIsRetainedInNeedsSortingWithoutReference()
    {
        var receipt = await UploadSelectedAsync(".msg", PinnedMsgHash);

        AssertNeedsSortingWithoutReferenceOrOcr(receipt);
        WriteAggregate("MSG", receipt.Decision);
    }

    [GenuineFormatCorpusFact(".jpg", PinnedJpegHash)]
    [Trait("Category", "Corpus")]
    public async Task GenuineJpegIsRetainedInNeedsSortingWithoutOcrOrReference()
    {
        var receipt = await UploadSelectedAsync(".jpg", PinnedJpegHash);

        AssertNeedsSortingWithoutReferenceOrOcr(receipt);
        WriteAggregate("JPEG", receipt.Decision);
    }

    [GenuineFormatCorpusFact(".png", PinnedPngHash)]
    [Trait("Category", "Corpus")]
    public async Task GenuinePngIsRetainedInNeedsSortingWithoutOcrOrReference()
    {
        var receipt = await UploadSelectedAsync(".png", PinnedPngHash);

        AssertNeedsSortingWithoutReferenceOrOcr(receipt);
        WriteAggregate("PNG", receipt.Decision);
    }

    [GenuineFormatCorpusFact(".docx", PinnedDocxHash)]
    [Trait("Category", "Corpus")]
    public async Task GenuineDocxReachesReaderAndPersistsNonTechnicalOutcome()
    {
        var receipt = await UploadSelectedAsync(".docx", PinnedDocxHash);

        Assert.Contains(
            receipt.Decision,
            new[]
            {
                IntakeDecision.CaseCreated,
                IntakeDecision.NeedsSorting
            });
        Assert.Contains(receipt.Evidence, item => item.Signal == "openxml-engine");
        Assert.NotEqual("source_reader_failure", receipt.FailureCode);
        Assert.NotEqual("artifact_storage_failure", receipt.FailureCode);
        WriteAggregate("DOCX", receipt.Decision);
    }

    private static async Task<IntakeReceipt> UploadSelectedAsync(string extension, string? expectedHash)
    {
        var sample = GenuineMultiFormatCorpus.ReadSelected(extension, expectedHash);
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var upload = await IntakeWebDriver.UploadAndProcessAsync(factory, client, sample);
        var receiptId = IntakeWebDriver.ReceiptId(upload);
        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
        var receipt = Assert.IsType<IntakeReceipt>(
            await queries.GetAsync(receiptId, CancellationToken.None));

        Assert.Equal(sample.Hash, receipt.SourceHash);
        Assert.Equal(sample.UploadName, receipt.SourceFileName);
        Assert.Equal(sample.Bytes.LongLength, receipt.SourceLength);
        return receipt;
    }

    private static void AssertNeedsSortingWithoutReferenceOrOcr(IntakeReceipt receipt)
    {
        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Empty(receipt.ScannedPdfPages);
        Assert.DoesNotContain(
            receipt.Evidence,
            item => item.Signal.Contains("ocr", StringComparison.OrdinalIgnoreCase));
    }

    private void WriteAggregate(string format, IntakeDecision decision)
    {
        var decisions = Enum.GetValues<IntakeDecision>()
            .Select(candidate => $"{candidate}={(candidate == decision ? 1 : 0)}");
        output.WriteLine($"{format}: samples=1; {string.Join("; ", decisions)}");
    }
}

internal static class GenuineMultiFormatCorpus
{
    private const long MaximumUploadLength = 10L * 1024 * 1024;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CorpusCandidate[]>
        CandidatesByExtension = new(StringComparer.OrdinalIgnoreCase);

    public static bool HasCohort(string extension)
    {
        if (CorpusLocator.CorpusRoot is null)
        {
            return false;
        }

        try
        {
            return Directory.EnumerateFiles(CorpusRoot, "*", SearchOption.AllDirectories)
                .Any(path => Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase)
                             && new FileInfo(path).Length is > 0 and <= MaximumUploadLength);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool HasPinned(string extension, string pinnedHash)
    {
        if (CorpusLocator.CorpusRoot is null)
        {
            return false;
        }

        try
        {
            return Candidates(extension).Any(candidate => candidate.Hash == pinnedHash);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static GenuineCorpusSample ReadSelected(string extension, string? expectedHash)
    {
        CorpusCandidate[] candidates;
        try
        {
            candidates = Candidates(extension);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "The immutable local corpus cohort could not be read; no source path or content was emitted.");
        }

        Assert.NotEmpty(candidates);
        var selected = string.IsNullOrWhiteSpace(expectedHash)
            ? candidates[0]
            : candidates.FirstOrDefault(candidate => candidate.Hash == expectedHash);
        Assert.True(selected is not null, $"The pinned {extension} corpus hash is absent.");
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(selected!.Path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "The selected immutable corpus source could not be read; no source path or content was emitted.");
        }

        var actualHash = Convert.ToHexString(SHA256.HashData(bytes));
        Assert.Equal(selected!.Hash, actualHash);

        return new(
            actualHash,
            $"{actualHash[..12].ToLowerInvariant()}{extension.ToLowerInvariant()}",
            MediaType(extension),
            bytes);
    }

    private static CorpusCandidate[] Candidates(string extension) =>
        CandidatesByExtension.GetOrAdd(extension, key =>
            Directory.EnumerateFiles(CorpusRoot, "*", SearchOption.AllDirectories)
                .Where(path => Path.GetExtension(path).Equals(key, StringComparison.OrdinalIgnoreCase))
                .Select(path => new FileInfo(path))
                .Where(file => file.Length is > 0 and <= MaximumUploadLength)
                .Select(file => new CorpusCandidate(file.FullName, HashFile(file.FullName)))
                .OrderBy(candidate => candidate.Hash, StringComparer.Ordinal)
                .ToArray());

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string MediaType(string extension) => extension.ToLowerInvariant() switch
    {
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".msg" => "application/vnd.ms-outlook",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        _ => "application/octet-stream"
    };

    private static string CorpusRoot =>
        CorpusLocator.CorpusRoot
        ?? throw new InvalidOperationException(
            "The immutable local corpus is absent: set PEGASUS_CORPUS_ROOT to its location, "
            + "or restore the repository's corpus/ directory.");

    private sealed record CorpusCandidate(string Path, string Hash);
}

internal sealed class GenuineFormatCorpusFactAttribute : FactAttribute
{
    public GenuineFormatCorpusFactAttribute(string extension, string pinnedHash)
    {
        if (!GenuineMultiFormatCorpus.HasCohort(extension))
        {
            Skip = $"The ignored local genuine corpus has no {extension} source at or below the 10 MB Web limit.";
        }
        else if (!GenuineMultiFormatCorpus.HasPinned(extension, pinnedHash))
        {
            Skip = $"This machine's local corpus lacks the pinned {extension} sample; corpora differ per system.";
        }
    }
}
