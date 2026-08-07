using System.Globalization;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;

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
        Assert.Equal(0, await CountReceiptsAsync(factory));

        var replay = await IntakeWebDriver.PostUploadAsync(
            client,
            form.AntiforgeryToken,
            "cancelled.eml",
            "message/rfc822",
            bytes,
            form.ExternalReceiptToken);

        Assert.Equal(HttpStatusCode.Redirect, replay.StatusCode);
        await IntakeWebDriver.ProcessQueuedAsync(factory, replay);
        Assert.Equal(1, await CountReceiptsAsync(factory));
        // The replay first re-stages the source, then the worker retains the
        // verified content for durable processing. Together with the
        // cancelled original attempt, that is three store calls.
        Assert.Equal(3, store.Attempts);
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

            // Every request answers, and answers the same thing. The upload is
            // processed while the operator waits now, so the landing is the case
            // the instruction became rather than a queued receipt — but one
            // upload replayed eight times is still one piece of work, and eight
            // identical landings is what says so. Losing the processing race is
            // not a failure: a request that lost it waits for the winner.
            Assert.All(
                results,
                result => Assert.True(
                    result.StatusCode == HttpStatusCode.Redirect,
                    $"A replayed upload answered {(int)result.StatusCode} {result.StatusCode} "
                        + $"instead of redirecting. Body: {result.ResponseBody}"));
            // Not "every landing is identical": requests that read the receipt
            // before the case allocation committed land on the create screen for
            // that same receipt, which then sends them to the case. Both are
            // right about the same work. What must not vary is what was written.
            Assert.All(
                results,
                result => Assert.True(
                    result.Location is not null
                        && result.Location.ToString().StartsWith("/Cases", StringComparison.Ordinal),
                    $"A replayed upload landed on '{result.Location}'."));

            // The claim the name makes, counted from the tables: one receipt,
            // and — because a definitive instruction now allocates while the
            // operator waits — exactly one case, not eight.
            Assert.Equal(1, await CountReceiptsAsync(factory));
            Assert.Equal(1, await CountCasesAsync(factory));
        }
        finally
        {
            foreach (var client in clients)
            {
                client.Dispose();
            }
        }
    }

    /// <summary>
    /// How many receipts are persisted, counted from the table rather than
    /// from the operator's queue.
    /// </summary>
    /// <remarks>
    /// These tests assert that no receipt is lost under pressure, which is a
    /// persistence claim. The queue projection is the wrong instrument for it:
    /// it deliberately excludes receipts that produced a case, so a definitive
    /// instruction — which now allocates its case at processing time — is
    /// correctly absent from it and would read as a lost receipt.
    /// </remarks>
    private static Task<int> CountReceiptsAsync(IntakeWebApplicationFactory factory) =>
        CountAsync(factory, "SELECT COUNT(*) FROM IntakeReceipts");

    /// <summary>
    /// How many cases exist. A replayed upload that allocated a second reference
    /// would be the worst outcome this test can catch, and a reference is never
    /// reused, so it is counted rather than inferred.
    /// </summary>
    private static Task<int> CountCasesAsync(IntakeWebApplicationFactory factory) =>
        CountAsync(factory, "SELECT COUNT(*) FROM Cases");

    private static async Task<int> CountAsync(IntakeWebApplicationFactory factory, string sql)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
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
        private readonly ConcurrentDictionary<string, byte[]> artifacts = new(StringComparer.Ordinal);
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

            var storageKey = $"sha256/{contentHash[..2]}/{contentHash}";
            artifacts.TryAdd(storageKey, content.ToArray());
            return storageKey;
        }

        public Task<ReadOnlyMemory<byte>?> ReadAsync(
            string storageKey,
            CancellationToken cancellationToken) =>
            Task.FromResult<ReadOnlyMemory<byte>?>(
                artifacts.TryGetValue(storageKey, out var content) ? content : null);
    }
}
