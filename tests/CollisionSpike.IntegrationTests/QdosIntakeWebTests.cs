using System.Net;
using CollisionSpike.Core.Intake.Qdos;
using Microsoft.Extensions.DependencyInjection;

namespace CollisionSpike.IntegrationTests;

public sealed class QdosIntakeWebTests
{
    private const string ForwardedEmailHash = "B91F5BBC622041B088D6F55E7A949CAEC945F476BDB18C489D0756D797552FB0";
    private const string ConfirmedInputTwoHash = "01165467CE0233F5452AA20AA7A016B25402F25026E0957B8A4E13EB34E6EC5B";
    private const string ConfirmedInputThreeHash = "A53C23F1B1E1372E0F0E8751FE712E110580AD7E1985B7094B88BB98A50AA56B";
    private const string ConfirmedInputFourHash = "E4A512B31F8964E5AC16AD6D7FA85A62B5D301B813AF72A6A147D956308AF9BC";
    private const string ConfirmedInputFiveHash = "AA1314773D9B632F7AC4CA78AEA54410A49B280ACBC93BC6F787053423CA14A9";
    private const string OcrRequiredPdfHash = "A9225D67A3FCD208B8EE00F9F6A1814E9FBEF0C693976BE2E2003612F56560CE";
    private const string NeedsSortingEmailHash = "28F896A1A20ACBE869570B78A2A5722B7AA514A5216150A8B86EEF5AFC47B65B";

    [GenuineQdosCorpusFact]
    public async Task StaffForwardedEmailStrongContentBeatsSenderAndRendersPersistedCase()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = QdosWebDriver.CreateClient(factory);

        var upload = await QdosWebDriver.UploadAsync(client, GenuineQdosCorpus.Read(ForwardedEmailHash));
        var receiptId = QdosWebDriver.ReceiptId(upload);
        using var review = await client.GetAsync(upload.Location);
        review.EnsureSuccessStatusCode();
        var html = await review.Content.ReadAsStringAsync();
        var receipt = await GetReceiptAsync(factory, receiptId);

        Assert.Equal(QdosIntakeDecision.ConfirmedQdos, receipt.Decision);
        Assert.Equal("QDOS31001", receipt.CaseReference);
        Assert.Equal(ForwardedEmailHash, receipt.SourceHash);
        Assert.Contains(receipt.Evidence, item =>
            item.Source == QdosEvidenceSource.Sender
            && item.Finding == QdosEvidenceFinding.ContradictsTransport);
        Assert.Contains(receipt.Evidence, item =>
            item.Strength == QdosEvidenceStrength.Strong
            && item.Finding == QdosEvidenceFinding.SupportsQdos
            && item.Source is QdosEvidenceSource.EmailBody or QdosEvidenceSource.PdfContent);
        var instructionDate = Assert.Single(receipt.Fields, field => field.Name == "Instruction date");
        Assert.True(instructionDate.IsDefaulted);
        Assert.Equal("2031-05-06", instructionDate.SuggestedValue);
        Assert.Contains("Confirmed QDOS", html, StringComparison.Ordinal);
        Assert.Contains("QDOS31001", html, StringComparison.Ordinal);
    }

    [GenuineQdosCorpusFact]
    public async Task InsufficientEmbeddedTextPdfIsOcrRequiredVisibleAndUnreferenced()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = QdosWebDriver.CreateClient(factory);

        var upload = await QdosWebDriver.UploadAsync(client, GenuineQdosCorpus.Read(OcrRequiredPdfHash));
        var receiptId = QdosWebDriver.ReceiptId(upload);
        var receipt = await GetReceiptAsync(factory, receiptId);
        using var review = await client.GetAsync(upload.Location);
        var reviewHtml = await review.Content.ReadAsStringAsync();
        using var queue = await client.GetAsync("/Intake/Queue");
        var queueHtml = await queue.Content.ReadAsStringAsync();

        Assert.Equal(QdosIntakeDecision.OcrRequired, receipt.Decision);
        Assert.Null(receipt.CaseId);
        Assert.Null(receipt.CaseReference);
        Assert.Equal("ocr_required", receipt.FailureCode);
        Assert.Contains("Document text required", reviewHtml, StringComparison.Ordinal);
        Assert.Contains("Not allocated", reviewHtml, StringComparison.Ordinal);
        Assert.Contains("Document text required", queueHtml, StringComparison.Ordinal);
        Assert.Null(factory.Services.GetService<IHttpClientFactory>());
    }

    [GenuineQdosCorpusFact]
    public async Task RepeatUploadReturnsSameReceiptAndReferenceAndConsumesOneSequence()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = QdosWebDriver.CreateClient(factory);
        var repeated = GenuineQdosCorpus.Read(ForwardedEmailHash);

        var first = await QdosWebDriver.UploadAsync(client, repeated);
        var duplicate = await QdosWebDriver.UploadAsync(client, repeated);
        var distinct = await QdosWebDriver.UploadAsync(client, GenuineQdosCorpus.Read(ConfirmedInputTwoHash));
        var firstId = QdosWebDriver.ReceiptId(first);
        var duplicateId = QdosWebDriver.ReceiptId(duplicate);
        var distinctId = QdosWebDriver.ReceiptId(distinct);
        var firstReceipt = await GetReceiptAsync(factory, firstId);
        var distinctReceipt = await GetReceiptAsync(factory, distinctId);
        using var duplicateReview = await client.GetAsync(duplicate.Location);
        var duplicateHtml = await duplicateReview.Content.ReadAsStringAsync();

        Assert.Equal(firstId, duplicateId);
        Assert.Equal("QDOS31001", firstReceipt.CaseReference);
        Assert.Equal("QDOS31002", distinctReceipt.CaseReference);
        Assert.Contains("already received", duplicateHtml, StringComparison.OrdinalIgnoreCase);
        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IQdosIntakeQueries>();
        Assert.Equal(2, (await queries.ListAsync(null, CancellationToken.None)).Count);
    }

    [GenuineQdosCorpusFact]
    public async Task UncheckedWebSubmissionIsRejectedWithoutPersistence()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = QdosWebDriver.CreateClient(factory);

        var result = await QdosWebDriver.UploadAsync(
            client,
            GenuineQdosCorpus.Read(ForwardedEmailHash),
            caseCreationAuthorized: false);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Contains("authorised to create its case and reference", result.ResponseBody, StringComparison.Ordinal);
        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IQdosIntakeQueries>();
        Assert.Empty(await queries.ListAsync(null, CancellationToken.None));
    }

    [GenuineQdosCorpusFact]
    public async Task ConfirmedUnauthorizedCoreCallPersistsWithoutCaseOrSequenceConsumption()
    {
        using var factory = new QdosWebApplicationFactory();
        var unauthorizedSample = GenuineQdosCorpus.Read(ForwardedEmailHash);
        var authorizedSample = GenuineQdosCorpus.Read(ConfirmedInputTwoHash);
        await using var scope = factory.Services.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<ProcessQdosIntake>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var unauthorized = await processor.ExecuteAsync(new(
            unauthorizedSample.UploadName,
            unauthorizedSample.MediaType,
            unauthorizedSample.Bytes,
            timeProvider.GetUtcNow(),
            "Genuine corpus integration test",
            false));
        var authorized = await processor.ExecuteAsync(new(
            authorizedSample.UploadName,
            authorizedSample.MediaType,
            authorizedSample.Bytes,
            timeProvider.GetUtcNow(),
            "Genuine corpus integration test",
            true));

        Assert.Equal(QdosIntakeDecision.ConfirmedQdos, unauthorized.Decision);
        Assert.Null(unauthorized.CaseId);
        Assert.Null(unauthorized.CaseReference);
        Assert.Equal("QDOS31001", authorized.CaseReference);
    }

    [GenuineQdosCorpusFact]
    public async Task ParallelDistinctConfirmedInputsAllocateUniqueConsecutiveReferencesInFileBackedSqlite()
    {
        using var factory = new QdosWebApplicationFactory();
        var samples = new[]
        {
            ForwardedEmailHash, ConfirmedInputTwoHash, ConfirmedInputThreeHash,
            ConfirmedInputFourHash, ConfirmedInputFiveHash
        }.Select(GenuineQdosCorpus.Read).ToArray();
        var clients = samples.Select(_ => QdosWebDriver.CreateClient(factory)).ToArray();

        try
        {
            var uploads = await Task.WhenAll(samples.Select((sample, index) =>
                QdosWebDriver.UploadAsync(clients[index], sample)));
            Assert.All(uploads, upload => Assert.Equal(HttpStatusCode.Redirect, upload.StatusCode));
        }
        finally
        {
            foreach (var client in clients)
            {
                client.Dispose();
            }
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IQdosIntakeQueries>();
        var references = (await queries.ListAsync(QdosIntakeDecision.ConfirmedQdos, CancellationToken.None))
            .Select(item => item.CaseReference!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            ["QDOS31001", "QDOS31002", "QDOS31003", "QDOS31004", "QDOS31005"],
            references);
    }

    [GenuineQdosCorpusFact]
    public async Task DashboardAndQueueCountsAreBackedByPersistedDecisions()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = QdosWebDriver.CreateClient(factory);
        await QdosWebDriver.UploadAsync(client, GenuineQdosCorpus.Read(ForwardedEmailHash));
        await QdosWebDriver.UploadAsync(client, GenuineQdosCorpus.Read(NeedsSortingEmailHash));

        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IQdosIntakeQueries>();
        var counts = await queries.GetCountsAsync(CancellationToken.None);
        var dashboard = await client.GetStringAsync("/");
        var sortingQueue = await client.GetStringAsync("/Intake/Queue?decision=NeedsSorting");

        Assert.Equal(new QdosQueueCounts(1, 1), counts);
        Assert.Contains("<strong>1</strong><span>Review</span>", dashboard, StringComparison.Ordinal);
        Assert.Contains("<strong>1</strong><small>Needs sorting</small>", dashboard, StringComparison.Ordinal);
        Assert.Contains("Needs sorting", sortingQueue, StringComparison.Ordinal);
        Assert.Contains(NeedsSortingEmailHash[..12], sortingQueue, StringComparison.Ordinal);
    }

    private static async Task<QdosIntakeRecord> GetReceiptAsync(
        QdosWebApplicationFactory factory,
        Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IQdosIntakeQueries>();
        return Assert.IsType<QdosIntakeRecord>(await queries.GetAsync(id, CancellationToken.None));
    }
}
