using System.Net;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;

namespace Pegasus.IntegrationTests;

public sealed class IntakeWebNegativeTests
{
    private static readonly string[] BusinessTables =
    [
        "IntakeReceipts",
        "IntakeAssets",
        "InstructionDrafts",
        "IntakeReceiptEvents",
        "IntakeStagedReceipts",
        "IntakeWorkItems",
        "IntakeEvaluations",
        "Organizations",
        "OrganizationRoles",
        "PrincipalSequenceLineages",
        "Principals",
        "CaseSequences",
        "Cases",
        "CaseHistory",
        "ExternalWorkItems",
        "CaseIntakeLinks",
        "BoxFileRequests",
        "CaseDocuments",
        "RequestUploadLinks",
        "DocumentVersions",
        "DocumentOccurrences",
        "RequestUploadReceipts"
    ];

    [Fact]
    public async Task ArtifactFailureShowsRetryMessageCreatesNoReceiptAndSameTokenCanRetry()
    {
        var artifactStore = new FailOnceArtifactStore();
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            artifactStore: artifactStore);
        using var client = IntakeWebDriver.CreateClient(factory);
        var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);
        byte[] bytes = [0x01, 0x02];

        var failed = await IntakeWebDriver.PostUploadAsync(
            client,
            form.AntiforgeryToken,
            "unknown.bin",
            "application/octet-stream",
            bytes,
            form.ExternalReceiptToken);

        Assert.Equal(HttpStatusCode.OK, failed.StatusCode);
        Assert.Contains(
            "could not be retained. Retry using the same upload receipt.",
            failed.ResponseBody,
            StringComparison.Ordinal);
        Assert.Empty(await ListAllAsync(factory));

        var retried = await IntakeWebDriver.PostUploadAsync(
            client,
            form.AntiforgeryToken,
            "unknown.bin",
            "application/octet-stream",
            bytes,
            form.ExternalReceiptToken);

        Assert.Equal(HttpStatusCode.Redirect, retried.StatusCode);
        Assert.Single(await ListAllAsync(factory));
        Assert.Equal(2, artifactStore.Attempts);
    }

    [Fact]
    public async Task QdosFilenameAndSenderWithoutConfirmingBodyNeedSortingThroughUploadCaller()
    {
        var message = new MimeMessage
        {
            Subject = "General forwarded correspondence",
            Body = new TextPart("plain") { Text = "Please review this ordinary correspondence." }
        };
        message.From.Add(MailboxAddress.Parse("qdos-forwarder@example.test"));
        message.To.Add(MailboxAddress.Parse("intake@example.test"));
        using var stream = new MemoryStream();
        message.WriteTo(stream);
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var upload = await IntakeWebDriver.UploadAsync(
            client,
            "QDOS-forward.eml",
            "message/rfc822",
            stream.ToArray());
        var receipt = await GetAsync(factory, IntakeWebDriver.ReceiptId(upload));

        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Null(receipt.InstructionDraft);
        Assert.DoesNotContain(receipt.Fields, field => field.SuggestedValue == "QDOS");
    }

    private const int TenMiB = 10 * 1024 * 1024;

    [Fact]
    public async Task MissingAntiforgeryTokenIsRejectedBeforePersistence()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var result = await IntakeWebDriver.PostUploadAsync(
            client,
            null,
            "payload.pdf",
            "application/octet-stream",
            [0x01],
            "cccccccccccccccccccccccccccccccc");

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        await AssertNoBusinessPersistenceAsync(factory);
    }

    [Fact]
    public async Task TamperedAntiforgeryTokenIsRejectedBeforePersistence()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);

        var result = await IntakeWebDriver.PostUploadAsync(
            client, "tampered", "payload.pdf", "application/octet-stream", [0x01], form.ExternalReceiptToken);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        await AssertNoBusinessPersistenceAsync(factory);
    }

    [Fact]
    public async Task MissingUploadReturnsValidationAndDoesNotPersist()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);

        var result = await IntakeWebDriver.PostUploadAsync(
            client, form.AntiforgeryToken, null, "application/octet-stream", null, form.ExternalReceiptToken);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Contains(
            "Choose an email, document, PDF or image to upload.",
            result.ResponseBody,
            StringComparison.Ordinal);
        await AssertNoBusinessPersistenceAsync(factory);
    }

    [Fact]
    public async Task UnknownExtensionReachesReaderAndPersistsUnsupportedReceipt()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);

        var result = await IntakeWebDriver.PostUploadAsync(
            client, form.AntiforgeryToken, "payload.bin", "application/octet-stream", [0x00, 0x01], form.ExternalReceiptToken);

        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        var receipt = await GetAsync(factory, IntakeWebDriver.ReceiptId(result));
        Assert.Equal(IntakeDecision.Unsupported, receipt.Decision);
        Assert.Null(receipt.InstructionDraft);
        Assert.Single(receipt.AssetRecords);
    }

    [Fact]
    public async Task EmptyUploadReturnsValidationAndDoesNotPersist()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);

        var result = await IntakeWebDriver.PostUploadAsync(
            client, form.AntiforgeryToken, "payload.pdf", "application/octet-stream", [], form.ExternalReceiptToken);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Contains("The selected file is empty.", result.ResponseBody, StringComparison.Ordinal);
        await AssertNoBusinessPersistenceAsync(factory);
    }

    [Fact]
    public async Task ExactTenMiBUploadPassesTransportValidation()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);

        var result = await IntakeWebDriver.PostUploadAsync(
            client,
            form.AntiforgeryToken,
            "boundary.pdf",
            "application/octet-stream",
            new byte[TenMiB],
            form.ExternalReceiptToken);

        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        var receipt = Assert.Single(await ListAllAsync(factory));
        Assert.Equal(TenMiB, (await GetAsync(factory, receipt.Id)).SourceLength);
    }

    [Fact]
    public async Task TenMiBPlusOneReturnsValidationAndDoesNotPersist()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);

        var result = await IntakeWebDriver.PostUploadAsync(
            client,
            form.AntiforgeryToken,
            "boundary.pdf",
            "application/octet-stream",
            new byte[TenMiB + 1],
            form.ExternalReceiptToken);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Contains("The selected file must be 10 MB or smaller.", result.ResponseBody, StringComparison.Ordinal);
        await AssertNoBusinessPersistenceAsync(factory);
    }

    [Fact]
    public async Task InvalidExternalReceiptTokenReturnsValidationAndDoesNotPersist()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);

        var result = await IntakeWebDriver.PostUploadAsync(
            client,
            form.AntiforgeryToken,
            "payload.pdf",
            "application/octet-stream",
            [0x00, 0x01],
            externalReceiptToken: "not-a-valid-n-format-guid");

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Contains(
            "upload receipt is invalid",
            result.ResponseBody,
            StringComparison.Ordinal);
        await AssertNoBusinessPersistenceAsync(factory);
    }

    [Fact]
    public async Task ExternalReceiptTokenCaseVariantsReplayOneReceiptThroughUploadCaller()
    {
        const string canonicalToken = "abcdefabcdefabcdefabcdefabcdefab";
        byte[] bytes = [0x01, 0x02, 0x03];
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var first = await IntakeWebDriver.UploadAsync(
            client,
            "unknown.bin",
            "application/octet-stream",
            bytes,
            canonicalToken.ToUpperInvariant());
        var replay = await IntakeWebDriver.UploadAsync(
            client,
            "unknown.bin",
            "application/octet-stream",
            bytes,
            canonicalToken);

        var firstId = IntakeWebDriver.ReceiptId(first);
        Assert.Equal(firstId, IntakeWebDriver.ReceiptId(replay));
        Assert.Equal(canonicalToken, (await GetAsync(factory, firstId)).SourceIdentity.ExternalReceiptToken);
        Assert.Single(await ListAllAsync(factory));

        using var replayReview = await client.GetAsync(replay.Location);
        var replayHtml = await replayReview.Content.ReadAsStringAsync();
        Assert.Contains("already processed", replayHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingReviewReceiptReturnsNotFound()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync($"/Intake/Review/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PersistedDraftValuesAreHtmlEncodedByReviewPage()
    {
        const string hostile = "<script>alert(1)</script>";
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>();
        var time = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
        var record = await store.StoreAsync(new(
            hostile + ".bin",
            "application/octet-stream",
            1,
            new string('A', 64),
            new(IntakeSourceChannel.ManualUpload, "33333333333333333333333333333333"),
            time,
            time,
            "Direct persisted draft",
            IntakeDecision.TechnicalFailure,
            hostile,
            [new(IntakeEvidenceSource.SystemDefault, IntakeEvidenceStrength.Weak,
                IntakeEvidenceFinding.Information, hostile, hostile)],
            [new(hostile, hostile, [new(hostile, IntakeEvidenceSource.SystemDefault, hostile)], false, false)],
            null,
            [hostile],
            hostile,
            hostile,
            "controlled_test_reader",
            "1",
            null,
            null), CancellationToken.None);

        using var response = await client.GetAsync($"/Intake/Review/{record.Id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(hostile, html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html, StringComparison.Ordinal);
    }

    private static async Task<IReadOnlyList<IntakeReceiptSummary>> ListAllAsync(IntakeWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
            .ListAsync(null, CancellationToken.None);
    }

    private static async Task<IntakeReceipt> GetAsync(IntakeWebApplicationFactory factory, Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return Assert.IsType<IntakeReceipt>(await scope.ServiceProvider
            .GetRequiredService<IIntakeReceiptQueries>().GetAsync(id, CancellationToken.None));
    }

    private static async Task AssertNoBusinessPersistenceAsync(IntakeWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.OpenConnectionAsync();
        try
        {
            foreach (var table in BusinessTables)
            {
                await using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = $"SELECT COUNT(*) FROM [{table}]";
                Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync(),
                    System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private sealed class FailOnceArtifactStore : IIntakeArtifactStore
    {
        private ReadOnlyMemory<byte>? retainedContent;
        private string? retainedStorageKey;

        public int Attempts { get; private set; }

        public Task<string> StoreAsync(
            string contentHash,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken)
        {
            Attempts++;
            if (Attempts == 1)
            {
                throw new IOException("controlled first retention failure");
            }

            retainedContent = content;
            retainedStorageKey = $"sha256/{contentHash[..2]}/{contentHash}";
            return Task.FromResult(retainedStorageKey);
        }

        public Task<ReadOnlyMemory<byte>?> ReadAsync(
            string storageKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                string.Equals(storageKey, retainedStorageKey, StringComparison.Ordinal)
                    ? retainedContent
                    : null);
    }
}
