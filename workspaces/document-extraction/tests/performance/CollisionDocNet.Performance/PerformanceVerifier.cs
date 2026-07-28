using System.Diagnostics;
using System.Text.Json;
using CollisionDocNet.Extraction;
using CollisionDocNet.Model;

namespace CollisionDocNet.Performance;

internal static class PerformanceVerifier
{
    private static readonly JsonSerializerOptions ReportJsonOptions = new() { WriteIndented = true };

    internal static async Task<int> RunAsync(TextWriter output, CancellationToken cancellationToken)
    {
        SyntheticInputSet inputs = SyntheticInputSet.Create();
        InputCase[] cases = inputs.Cases;
        var references = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (InputCase input in cases)
        {
            ExtractionResult result = await Extract(input, CancellationToken.None).ConfigureAwait(false);
            if (!string.Equals(result.DetectedFormat.ToString(), input.ExpectedFormat, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"{input.Name} dispatched as {result.DetectedFormat}, not {input.ExpectedFormat}.");
            }
            references.Add(input.Name, Fingerprint(result));
        }

        var cancelled = new List<string>();
        foreach (InputCase input in cases)
        {
            using var source = new CancellationTokenSource();
            source.Cancel();
            ExtractionResult result = await Extract(input, source.Token).ConfigureAwait(false);
            if (result.Outcome != ExtractionOutcome.Cancelled)
            {
                throw new InvalidOperationException($"Pre-cancelled {input.Name} extraction returned {result.Outcome}.");
            }

            cancelled.Add(input.Name);
        }

        long workingSetBefore = Process.GetCurrentProcess().WorkingSet64;
        TimeSpan cpuBefore = Process.GetCurrentProcess().TotalProcessorTime;
        var stopwatch = Stopwatch.StartNew();
        const int repetitions = 4;
        var work = Enumerable.Range(0, repetitions)
            .SelectMany(_ => cases)
            .ToArray();
        await Parallel.ForEachAsync(
            work,
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken },
            async (input, token) =>
            {
                ExtractionResult result = await Extract(input, token).ConfigureAwait(false);
                if (!string.Equals(Fingerprint(result), references[input.Name], StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Concurrent {input.Name} extraction differed from its reference result.");
                }
            }).ConfigureAwait(false);
        stopwatch.Stop();

        using var blockedCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        await using var blocked = new BlockingReadStream();
        var blockedRequest = new ExtractionRequest(
            ExtractionInput.FromStream(blocked),
            "synthetic-blocked-stream",
            "blocked.eml",
            "message/rfc822",
            ExtractionPolicy.CreateDefault());
        var cancellationStopwatch = Stopwatch.StartNew();
        ExtractionResult blockedResult = await DocumentExtractor.ExtractAsync(blockedRequest, blockedCancellation.Token).ConfigureAwait(false);
        cancellationStopwatch.Stop();
        if (blockedResult.Outcome != ExtractionOutcome.Cancelled)
        {
            throw new InvalidOperationException($"Blocked stream cancellation returned {blockedResult.Outcome}.");
        }

        Process process = Process.GetCurrentProcess();
        var report = new
        {
            schema = "collisiondocnet-performance-verification/1",
            utc = DateTimeOffset.UtcNow,
            host = new
            {
                os = Environment.OSVersion.VersionString,
                architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                processors = Environment.ProcessorCount,
            },
            inputClasses = cases.Select(static item => new { item.Name, item.ExpectedFormat, bytes = item.Bytes.Length }),
            referenceOutcomes = await BuildReferenceEvidenceAsync(cases, references).ConfigureAwait(false),
            preCancelledFormats = cancelled,
            blockedStreamCancellationMilliseconds = cancellationStopwatch.Elapsed.TotalMilliseconds,
            concurrency = new
            {
                degree = 4,
                operations = work.Length,
                elapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                cpuMilliseconds = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds,
                workingSetBefore,
                workingSetAfter = process.WorkingSet64,
            },
        };
        await output.WriteLineAsync(JsonSerializer.Serialize(report, ReportJsonOptions)).ConfigureAwait(false);
        return 0;
    }

    private static ValueTask<ExtractionResult> Extract(InputCase input, CancellationToken cancellationToken) =>
        DocumentExtractor.ExtractAsync(input.Bytes, "synthetic-performance-input", input.FileName, input.MediaType,
            cancellationToken: cancellationToken);

    private static string Fingerprint(ExtractionResult result) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(ExtractionResultJson.SerializeToUtf8Bytes(result)));

    private static async Task<object[]> BuildReferenceEvidenceAsync(InputCase[] cases, Dictionary<string, string> references)
    {
        var evidence = new object[cases.Length];
        for (int index = 0; index < cases.Length; index++)
        {
            InputCase input = cases[index];
            ExtractionResult result = await Extract(input, CancellationToken.None).ConfigureAwait(false);
            evidence[index] = new
            {
                input.Name,
                detectedFormat = result.DetectedFormat.ToString(),
                outcome = result.Outcome.ToString(),
                fingerprint = references[input.Name],
                result.Measurements.InputBytes,
                result.Measurements.DecodedBytes,
                result.Measurements.Objects,
                result.Measurements.TextCharacters,
                result.Measurements.Assets,
                result.Measurements.AssetBytes,
                result.Measurements.MaximumNestingDepth,
            };
        }

        return evidence;
    }

    private sealed class BlockingReadStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return 0;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
