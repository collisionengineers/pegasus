using System.Globalization;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

public sealed partial class CapacitySoakTests
{
    private const int ConcurrentStaff = 8;

    [Fact]
    [Trait("Category", "QdosPressure")]
    public async Task EightConcurrentStaffCompleteBoundedCallerPressureWithoutLostReceipts()
    {
        Assert.Equal(
            "CiPressure",
            Environment.GetEnvironmentVariable("PEGASUS_QDOS_PRESSURE_PROFILE"));

        using var factory = new IntakeWebApplicationFactory();
        using var warmupClient = IntakeWebDriver.CreateClient(factory);
        using (var warmup = await warmupClient.GetAsync("/"))
        {
            warmup.EnsureSuccessStatusCode();
        }

        var readDurations = new ConcurrentBag<TimeSpan>();
        var writeDurations = new ConcurrentBag<TimeSpan>();
        var receiptLocations = new ConcurrentBag<Uri>();
        var queuedUploads = new ConcurrentBag<UploadResult>();
        var unexpectedOutcomes = new ConcurrentBag<string>();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var workers = Enumerable.Range(0, ConcurrentStaff)
            .Select(worker => RunWorkerAsync(
                factory,
                worker,
                start.Task,
                readDurations,
                writeDurations,
                receiptLocations,
                queuedUploads,
                unexpectedOutcomes))
            .ToArray();

        start.SetResult();
        await Task.WhenAll(workers);
        foreach (var queuedUpload in queuedUploads)
        {
            await IntakeWebDriver.ProcessQueuedAsync(factory, queuedUpload);
        }

        Assert.True(
            unexpectedOutcomes.IsEmpty,
            $"Unexpected outcomes under pressure:{Environment.NewLine}"
                + string.Join(Environment.NewLine, unexpectedOutcomes.Order()));
        Assert.Equal(10, receiptLocations.Count);
        Assert.Equal(28, readDurations.Count);
        Assert.Equal(10, writeDurations.Count);
        Assert.True(Percentile95(readDurations) <= TimeSpan.FromSeconds(2),
            $"Warm read p95 was {Percentile95(readDurations).TotalMilliseconds:F0} ms. {Spread(readDurations)}");

        // The Web request only retains the bounded source and stages Pending
        // work. Extraction, evaluation and allocation are drained separately
        // above through the Worker test boundary, so they are deliberately not
        // charged to the request-latency budget.
        Assert.True(Percentile95(writeDurations) <= TimeSpan.FromSeconds(3),
            $"Warm write p95 was {Percentile95(writeDurations).TotalMilliseconds:F0} ms. {Spread(writeDurations)}");

        // Counted from the tables, not from the operator's queue: "no receipt
        // was lost under pressure" is a persistence claim, and the queue
        // deliberately excludes receipts that produced a case — which every
        // definitive instruction here now does, at processing time.
        //
        // The durable thing every upload must have is its staged receipt. A
        // processed receipt only exists once processing finishes, and under this
        // much contention some uploads are pushed onto their retry — which is
        // why the answer they gave the operator is asserted above, and why what
        // must never happen is a work item that gave up.
        await using var scope = factory.Services.CreateAsyncScope();
        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        Assert.Equal(10, await CountAsync(connection, "SELECT COUNT(*) FROM IntakeStagedReceipts"));
        Assert.Equal(
            0,
            await CountAsync(
                connection,
                "SELECT COUNT(*) FROM IntakeWorkItems WHERE State = 'failed'"));
    }

    private static async Task<int> CountAsync(System.Data.Common.DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task RunWorkerAsync(
        IntakeWebApplicationFactory factory,
        int worker,
        Task start,
        ConcurrentBag<TimeSpan> readDurations,
        ConcurrentBag<TimeSpan> writeDurations,
        ConcurrentBag<Uri> receiptLocations,
        ConcurrentBag<UploadResult> queuedUploads,
        ConcurrentBag<string> unexpectedOutcomes)
    {
        using var client = IntakeWebDriver.CreateClient(factory);
        await start;

        for (var read = 0; read < 3; read++)
        {
            await MeasureAsync(readDurations, async () =>
            {
                var path = read == 0 ? "/" : "/Operations";
                using var response = await client.GetAsync(path);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    unexpectedOutcomes.Add($"read {path}: {(int)response.StatusCode} {response.StatusCode}");
                }
            });
        }

        var primary = await MeasureAsync(writeDurations, () => UploadAsync(client, worker, 0));
        RecordLocation(primary, receiptLocations, queuedUploads, unexpectedOutcomes);

        if (worker < 4)
        {
            await MeasureAsync(readDurations, async () =>
            {
                using var response = await client.GetAsync(primary.Location);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    unexpectedOutcomes.Add(
                        $"read landing '{primary.Location}': {(int)response.StatusCode} {response.StatusCode}");
                }
            });
        }
        else if (worker < 6)
        {
            var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);
            var denied = await IntakeWebDriver.PostUploadAsync(
                client,
                antiforgeryToken: null,
                uploadName: "denied.eml",
                mediaType: "message/rfc822",
                bytes: CreateMessage(worker, 1),
                externalReceiptToken: form.ExternalReceiptToken);
            if (denied.StatusCode != HttpStatusCode.BadRequest)
            {
                unexpectedOutcomes.Add(Describe("upload without an antiforgery token", denied));
            }
        }
        else
        {
            var secondary = await MeasureAsync(writeDurations, () => UploadAsync(client, worker, 1));
            RecordLocation(secondary, receiptLocations, queuedUploads, unexpectedOutcomes);
        }
    }

    private static async Task<UploadResult> UploadAsync(HttpClient client, int worker, int sequence) =>
        await IntakeWebDriver.UploadAsync(
            client,
            $"pressure-{worker:D2}-{sequence:D2}.eml",
            "message/rfc822",
            CreateMessage(worker, sequence));

    private static byte[] CreateMessage(int worker, int sequence) => Encoding.UTF8.GetBytes(
        $"From: pressure-{worker:D2}@example.test\r\n" +
        "To: intake@example.test\r\n" +
        $"Subject: Controlled pressure {worker:D2}-{sequence:D2}\r\n" +
        "MIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n" +
        $"QDOS instruction\r\nClaimant Name: Controlled {worker:D2}\r\n" +
        $"Claim Number: PRESSURE-{worker:D2}-{sequence:D2}\r\nVehicle Registration: AB12 CDE\r\n");

    private static void RecordLocation(
        UploadResult result,
        ConcurrentBag<Uri> receiptLocations,
        ConcurrentBag<UploadResult> queuedUploads,
        ConcurrentBag<string> unexpectedOutcomes)
    {
        if (result.StatusCode == HttpStatusCode.Redirect && result.Location is not null)
        {
            receiptLocations.Add(result.Location);
            queuedUploads.Add(result);
            return;
        }

        unexpectedOutcomes.Add(Describe("upload", result));
    }

    /// <summary>
    /// What actually happened, not just the status code. A bag of bare status
    /// codes says "OK" for a refused upload and for a page that rendered an
    /// error, which tells whoever reads the failure nothing about which.
    /// </summary>
    private static string Describe(string what, UploadResult result) =>
        $"{what}: {(int)result.StatusCode} {result.StatusCode}, "
        + $"location '{result.Location?.ToString() ?? "(none)"}', "
        + $"message: {ErrorText(result.ResponseBody)}";

    private static string ErrorText(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "(no body)";
        }

        var match = ValidationSummaryRegex().Match(body);
        return match.Success
            ? match.Groups["message"].Value.Trim()
            : "(no validation summary)";
    }

    [GeneratedRegex(
        "validation-summary-errors[^>]*>\\s*<ul>\\s*<li>(?<message>[^<]*)</li>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ValidationSummaryRegex();

    private static async Task MeasureAsync(
        ConcurrentBag<TimeSpan> durations,
        Func<Task> action)
    {
        var stopwatch = Stopwatch.StartNew();
        await action();
        stopwatch.Stop();
        durations.Add(stopwatch.Elapsed);
    }

    private static async Task<T> MeasureAsync<T>(
        ConcurrentBag<TimeSpan> durations,
        Func<Task<T>> action)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await action();
        stopwatch.Stop();
        durations.Add(stopwatch.Elapsed);
        return result;
    }

    /// <summary>
    /// Every sample, in order. A p95 on its own cannot say whether one request
    /// was slow or all of them were, and that is the difference between a stall
    /// and a budget that no longer fits what an upload now does.
    /// </summary>
    private static string Spread(IEnumerable<TimeSpan> samples) =>
        "All (ms): "
        + string.Join(
            ", ",
            samples.Order().Select(sample => sample.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)));

    private static TimeSpan Percentile95(IEnumerable<TimeSpan> samples)
    {
        var ordered = samples.Order().ToArray();
        Assert.NotEmpty(ordered);
        var index = Math.Clamp((int)Math.Ceiling(ordered.Length * 0.95) - 1, 0, ordered.Length - 1);
        return ordered[index];
    }
}
