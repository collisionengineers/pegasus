using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Intake;

namespace Pegasus.IntegrationTests;

public sealed class FailureInjectionTests
{
    [Fact]
    [Trait("Category", "QdosPressure")]
    public async Task CancelledRetentionLeavesNoReceiptAndSameTokenReplaysSuccessfully()
    {
        var store = new CancelFirstArtifactStore();
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            artifactStore: store);
        using var client = IntakeWebDriver.CreateClient(factory);
        var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);
        var bytes = CreateMessage("CANCEL-001");
        using var cancellation = new CancellationTokenSource();

        var interrupted = IntakeWebDriver.PostUploadAsync(
            client,
            form.AntiforgeryToken,
            "cancelled.eml",
            "message/rfc822",
            bytes,
            form.ExternalReceiptToken,
            cancellation.Token);
        await store.FirstAttemptEntered;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await interrupted);
        Assert.Empty(await ListReceiptsAsync(factory));

        var replay = await IntakeWebDriver.PostUploadAsync(
            client,
            form.AntiforgeryToken,
            "cancelled.eml",
            "message/rfc822",
            bytes,
            form.ExternalReceiptToken);

        Assert.Equal(HttpStatusCode.Redirect, replay.StatusCode);
        Assert.Single(await ListReceiptsAsync(factory));
        Assert.Equal(2, store.Attempts);
    }

    [Fact]
    [Trait("Category", "QdosPressure")]
    public async Task ConcurrentReplayPressureProducesOneDurableReceipt()
    {
        using var factory = new IntakeWebApplicationFactory();
        const string replayToken = "99999999999999999999999999999999";
        var clients = Enumerable.Range(0, 8)
            .Select(_ => IntakeWebDriver.CreateClient(factory))
            .ToArray();

        try
        {
            var forms = await Task.WhenAll(clients.Select(
                client => IntakeWebDriver.GetUploadFormTokensAsync(client)));
            var requests = clients.Select((client, index) => IntakeWebDriver.PostUploadAsync(
                client,
                forms[index].AntiforgeryToken,
                "replay.eml",
                "message/rfc822",
                CreateMessage("REPLAY-001"),
                replayToken));

            var results = await Task.WhenAll(requests);

            Assert.All(results, result => Assert.Equal(HttpStatusCode.Redirect, result.StatusCode));
            Assert.Single(results.Select(result => result.Location).Distinct());
            Assert.Single(await ListReceiptsAsync(factory));
        }
        finally
        {
            foreach (var client in clients)
            {
                client.Dispose();
            }
        }
    }

    private static async Task<IReadOnlyList<IntakeReceiptSummary>> ListReceiptsAsync(
        IntakeWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<IIntakeReceiptQueries>()
            .ListAsync(null, CancellationToken.None);
    }

    private static byte[] CreateMessage(string claimNumber) => Encoding.UTF8.GetBytes(
        "From: controlled-pressure@example.test\r\n" +
        "To: intake@example.test\r\n" +
        "Subject: Controlled failure pressure\r\n" +
        "MIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n" +
        "QDOS instruction\r\nClaimant Name: Controlled Failure\r\n" +
        $"Claim Number: {claimNumber}\r\nVehicle Registration: AB12 CDE\r\n");

    private sealed class CancelFirstArtifactStore : IIntakeArtifactStore
    {
        private readonly TaskCompletionSource firstAttemptEntered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int attempts;

        public Task FirstAttemptEntered => firstAttemptEntered.Task;

        public int Attempts => Volatile.Read(ref attempts);

        public async Task<string> StoreAsync(
            string contentHash,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                firstAttemptEntered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return $"sha256/{contentHash[..2]}/{contentHash}";
        }

        public Task<ReadOnlyMemory<byte>?> ReadAsync(
            string storageKey,
            CancellationToken cancellationToken) =>
            Task.FromResult<ReadOnlyMemory<byte>?>(null);
    }
}
