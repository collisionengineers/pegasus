using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Intake;

namespace Pegasus.IntegrationTests;

public sealed class CapacitySoakTests
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
        var unexpectedStatuses = new ConcurrentBag<HttpStatusCode>();
        using var start = new ManualResetEventSlim(false);

        var workers = Enumerable.Range(0, ConcurrentStaff)
            .Select(worker => RunWorkerAsync(
                factory,
                worker,
                start,
                readDurations,
                writeDurations,
                receiptLocations,
                unexpectedStatuses))
            .ToArray();

        start.Set();
        await Task.WhenAll(workers);

        Assert.Empty(unexpectedStatuses);
        Assert.Equal(10, receiptLocations.Count);
        Assert.Equal(28, readDurations.Count);
        Assert.Equal(10, writeDurations.Count);
        Assert.True(Percentile95(readDurations) <= TimeSpan.FromSeconds(2),
            $"Warm read p95 was {Percentile95(readDurations).TotalMilliseconds:F0} ms.");
        Assert.True(Percentile95(writeDurations) <= TimeSpan.FromSeconds(3),
            $"Warm write p95 was {Percentile95(writeDurations).TotalMilliseconds:F0} ms.");

        await using var scope = factory.Services.CreateAsyncScope();
        var receipts = await scope.ServiceProvider
            .GetRequiredService<IIntakeReceiptQueries>()
            .ListAsync(null, CancellationToken.None);
        Assert.Equal(10, receipts.Count);
    }

    private static async Task RunWorkerAsync(
        IntakeWebApplicationFactory factory,
        int worker,
        ManualResetEventSlim start,
        ConcurrentBag<TimeSpan> readDurations,
        ConcurrentBag<TimeSpan> writeDurations,
        ConcurrentBag<Uri> receiptLocations,
        ConcurrentBag<HttpStatusCode> unexpectedStatuses)
    {
        using var client = IntakeWebDriver.CreateClient(factory);
        start.Wait();

        for (var read = 0; read < 3; read++)
        {
            await MeasureAsync(readDurations, async () =>
            {
                using var response = await client.GetAsync(read == 0 ? "/" : "/Intake");
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    unexpectedStatuses.Add(response.StatusCode);
                }
            });
        }

        var primary = await MeasureAsync(writeDurations, () => UploadAsync(client, worker, 0));
        RecordLocation(primary, receiptLocations, unexpectedStatuses);

        if (worker < 4)
        {
            await MeasureAsync(readDurations, async () =>
            {
                using var response = await client.GetAsync(primary.Location);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    unexpectedStatuses.Add(response.StatusCode);
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
                unexpectedStatuses.Add(denied.StatusCode);
            }
        }
        else
        {
            var secondary = await MeasureAsync(writeDurations, () => UploadAsync(client, worker, 1));
            RecordLocation(secondary, receiptLocations, unexpectedStatuses);
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
        ConcurrentBag<HttpStatusCode> unexpectedStatuses)
    {
        if (result.StatusCode == HttpStatusCode.Redirect && result.Location is not null)
        {
            receiptLocations.Add(result.Location);
            return;
        }

        unexpectedStatuses.Add(result.StatusCode);
    }

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

    private static TimeSpan Percentile95(IEnumerable<TimeSpan> samples)
    {
        var ordered = samples.Order().ToArray();
        Assert.NotEmpty(ordered);
        var index = Math.Clamp((int)Math.Ceiling(ordered.Length * 0.95) - 1, 0, ordered.Length - 1);
        return ordered[index];
    }
}
