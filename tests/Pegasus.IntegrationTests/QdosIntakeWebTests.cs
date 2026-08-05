using System.Net;
using Pegasus.Core.Intake;
using Microsoft.Extensions.DependencyInjection;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class QdosIntakeWebTests
{
    private const string ForwardedEmailHash = "B91F5BBC622041B088D6F55E7A949CAEC945F476BDB18C489D0756D797552FB0";
    private const string ConfirmedInputTwoHash = "01165467CE0233F5452AA20AA7A016B25402F25026E0957B8A4E13EB34E6EC5B";
    private const string ConfirmedInputThreeHash = "A53C23F1B1E1372E0F0E8751FE712E110580AD7E1985B7094B88BB98A50AA56B";
    private const string ConfirmedInputFourHash = "E4A512B31F8964E5AC16AD6D7FA85A62B5D301B813AF72A6A147D956308AF9BC";
    private const string ConfirmedInputFiveHash = "AA1314773D9B632F7AC4CA78AEA54410A49B280ACBC93BC6F787053423CA14A9";
    private const string LowTextNonScanPdfHash = "A9225D67A3FCD208B8EE00F9F6A1814E9FBEF0C693976BE2E2003612F56560CE";
    private const string NeedsSortingEmailHash = "28F896A1A20ACBE869570B78A2A5722B7AA514A5216150A8B86EEF5AFC47B65B";

    [Fact]
    public async Task AuthenticatedManualUploadQueuesThroughReceiveIntakeAndPrgsToTheList()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            localIntakeEnabled: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        const string receiptToken = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        var upload = await IntakeWebDriver.UploadAsync(
            client,
            "queued-intake.txt",
            "text/plain",
            [49],
            receiptToken);

        Assert.Equal(HttpStatusCode.Redirect, upload.StatusCode);
        Assert.NotNull(upload.Location);
        Assert.Equal(
            "/Intake",
            upload.Location!.OriginalString.Split('?', 2)[0]);
        var queuedReceiptId = IntakeWebDriver.QueuedReceiptId(upload);
        using var queue = await client.GetAsync(upload.Location);
        queue.EnsureSuccessStatusCode();
        var html = await queue.Content.ReadAsStringAsync();
        Assert.Contains(
            "The instruction has been retained and queued for processing.",
            html,
            StringComparison.Ordinal);

        var duplicate = await IntakeWebDriver.UploadAsync(
            client,
            "queued-intake.txt",
            "text/plain",
            [49],
            receiptToken);
        Assert.Equal(HttpStatusCode.Redirect, duplicate.StatusCode);
        Assert.NotNull(duplicate.Location);
        Assert.Equal(
            "/Intake",
            duplicate.Location!.OriginalString.Split('?', 2)[0]);
        Assert.Equal(queuedReceiptId, IntakeWebDriver.QueuedReceiptId(duplicate));
        using var duplicateQueue = await client.GetAsync(duplicate.Location);
        duplicateQueue.EnsureSuccessStatusCode();
        var duplicateHtml = await duplicateQueue.Content.ReadAsStringAsync();
        Assert.Contains(
            "This instruction was already queued; no duplicate work was created.",
            duplicateHtml,
            StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        Assert.IsType<ReceiveIntake>(
            scope.ServiceProvider.GetRequiredService<IIntakeSubmission>());
    }

    [GenuineQdosCorpusFact(ForwardedEmailHash)]
    [Trait("Category", "Corpus")]
    public async Task StaffForwardedEmailStrongContentBeatsSenderAndRendersPersistedDraft()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            GenuineQdosCorpus.Read(ForwardedEmailHash));
        var receiptId = IntakeWebDriver.ReceiptId(upload);
        using var review = await client.GetAsync(upload.Location);
        review.EnsureSuccessStatusCode();
        var html = await review.Content.ReadAsStringAsync();
        var receipt = await GetReceiptAsync(factory, receiptId);

        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);
        Assert.NotNull(receipt.InstructionDraft);
        Assert.Equal(ForwardedEmailHash, receipt.SourceHash);
        Assert.Contains(receipt.Evidence, item =>
            item.Source == IntakeEvidenceSource.Sender
            && item.Finding == IntakeEvidenceFinding.ContradictsTransport);
        Assert.Contains(receipt.Evidence, item =>
            item.Strength == IntakeEvidenceStrength.Strong
            && item.Finding == IntakeEvidenceFinding.SupportsPrincipal
            && item.Source is IntakeEvidenceSource.EmailBody or IntakeEvidenceSource.PdfContent);
        var instructionDate = Assert.Single(receipt.Fields, field => field.Name == "Instruction date");
        Assert.True(instructionDate.IsDefaulted);
        Assert.Equal("2031-05-06", instructionDate.SuggestedValue);
        Assert.Contains("Instruction draft", html, StringComparison.Ordinal);
        Assert.Contains("Typed review draft", html, StringComparison.Ordinal);
    }

    [GenuineQdosCorpusFact(LowTextNonScanPdfHash)]
    [Trait("Category", "Corpus")]
    public async Task LowTextPdfWithoutDominantRasterNeedsSortingWithoutOcrOrReference()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            GenuineQdosCorpus.Read(LowTextNonScanPdfHash));
        var receiptId = IntakeWebDriver.ReceiptId(upload);
        var receipt = await GetReceiptAsync(factory, receiptId);
        using var review = await client.GetAsync(upload.Location);
        var reviewHtml = await review.Content.ReadAsStringAsync();
        using var queue = await client.GetAsync("/Intake");
        var queueHtml = await queue.Content.ReadAsStringAsync();

        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Null(receipt.FailureCode);
        Assert.Empty(receipt.ScannedPdfPages);
        Assert.Contains(receipt.Evidence, evidence => evidence.Signal == "insufficient-embedded-text");
        Assert.Contains("Needs sorting", reviewHtml, StringComparison.Ordinal);
        Assert.Contains("not an image-led scanned page", reviewHtml, StringComparison.Ordinal);
        Assert.Contains("Needs sorting", queueHtml, StringComparison.Ordinal);
    }

    [GenuineQdosCorpusFact(ForwardedEmailHash, ConfirmedInputTwoHash)]
    [Trait("Category", "Corpus")]
    public async Task RepeatExternalReceiptTokenReturnsSamePreCaseReceipt()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var repeated = GenuineQdosCorpus.Read(ForwardedEmailHash);
        const string replayToken = "44444444444444444444444444444444";

        var first = await IntakeWebDriver.UploadAndProcessAsync(factory, client, repeated, replayToken);
        var duplicate = await IntakeWebDriver.UploadAndProcessAsync(factory, client, repeated, replayToken);
        var distinct = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            GenuineQdosCorpus.Read(ConfirmedInputTwoHash));
        var firstId = IntakeWebDriver.ReceiptId(first);
        var duplicateId = IntakeWebDriver.ReceiptId(duplicate);
        var distinctId = IntakeWebDriver.ReceiptId(distinct);
        var firstReceipt = await GetReceiptAsync(factory, firstId);
        var distinctReceipt = await GetReceiptAsync(factory, distinctId);
        using var duplicateReview = await client.GetAsync(duplicate.Location);
        var duplicateHtml = await duplicateReview.Content.ReadAsStringAsync();

        Assert.Equal(firstId, duplicateId);
        Assert.Equal(replayToken, firstReceipt.SourceIdentity.ExternalReceiptToken);
        Assert.NotEqual(
            firstReceipt.SourceIdentity.ExternalReceiptToken,
            distinctReceipt.SourceIdentity.ExternalReceiptToken);
        Assert.Contains("already processed", duplicateHtml, StringComparison.OrdinalIgnoreCase);
        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
        Assert.Equal(2, (await queries.ListAsync(null, CancellationToken.None)).Count);
    }

    [GenuineQdosCorpusFact(ForwardedEmailHash, ConfirmedInputTwoHash)]
    [Trait("Category", "Corpus")]
    public async Task ConfirmedCoreCallsPersistDistinctPreCaseDraftsWithoutSequenceConsumption()
    {
        using var factory = new IntakeWebApplicationFactory();
        var unauthorizedSample = GenuineQdosCorpus.Read(ForwardedEmailHash);
        var authorizedSample = GenuineQdosCorpus.Read(ConfirmedInputTwoHash);
        await using var scope = factory.Services.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<ProcessIntake>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var unauthorized = await processor.ExecuteAsync(new(
            unauthorizedSample.UploadName,
            unauthorizedSample.MediaType,
            unauthorizedSample.Bytes,
            timeProvider.GetUtcNow(),
            "Genuine corpus integration test",
            new(IntakeSourceChannel.ManualUpload, "55555555555555555555555555555555")));
        var authorized = await processor.ExecuteAsync(new(
            authorizedSample.UploadName,
            authorizedSample.MediaType,
            authorizedSample.Bytes,
            timeProvider.GetUtcNow(),
            "Genuine corpus integration test",
            new(IntakeSourceChannel.ManualUpload, "66666666666666666666666666666666")));

        Assert.Equal(IntakeDecision.CaseCreated, unauthorized.Decision);
        Assert.Equal(IntakeDecision.CaseCreated, authorized.Decision);
    }

    [GenuineQdosCorpusFact(
        ForwardedEmailHash,
        ConfirmedInputTwoHash,
        ConfirmedInputThreeHash,
        ConfirmedInputFourHash,
        ConfirmedInputFiveHash)]
    [Trait("Category", "Corpus")]
    public async Task ParallelDistinctConfirmedInputsPersistUniquePreCaseReceiptsInLocalDb()
    {
        using var factory = new IntakeWebApplicationFactory();
        var samples = new[]
        {
            ForwardedEmailHash, ConfirmedInputTwoHash, ConfirmedInputThreeHash,
            ConfirmedInputFourHash, ConfirmedInputFiveHash
        }.Select(GenuineQdosCorpus.Read).ToArray();
        var clients = samples.Select(_ => IntakeWebDriver.CreateClient(factory)).ToArray();

        try
        {
            var uploads = await Task.WhenAll(samples.Select((sample, index) =>
                IntakeWebDriver.UploadAsync(clients[index], sample)));
            Assert.All(uploads, upload => Assert.Equal(HttpStatusCode.Redirect, upload.StatusCode));
            foreach (var upload in uploads)
            {
                _ = await IntakeWebDriver.ProcessQueuedAsync(factory, upload);
            }
        }
        finally
        {
            foreach (var client in clients)
            {
                client.Dispose();
            }
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
        var receipts = await queries.ListAsync(IntakeDecision.CaseCreated, CancellationToken.None);
        Assert.Equal(5, receipts.Count);
    }

    [GenuineQdosCorpusFact(ForwardedEmailHash, NeedsSortingEmailHash)]
    [Trait("Category", "Corpus")]
    public async Task DashboardAndQueueCountsAreBackedByPersistedDecisions()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var forwarded = await IntakeWebDriver.UploadAsync(
            client,
            GenuineQdosCorpus.Read(ForwardedEmailHash));
        _ = await IntakeWebDriver.ProcessQueuedAsync(factory, forwarded);
        var needsSorting = await IntakeWebDriver.UploadAsync(
            client,
            GenuineQdosCorpus.Read(NeedsSortingEmailHash));
        _ = await IntakeWebDriver.ProcessQueuedAsync(factory, needsSorting);

        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
        var counts = await queries.GetCountsAsync(CancellationToken.None);
        var dashboard = await client.GetStringAsync("/");
        var sortingQueue = await client.GetStringAsync("/Intake?decision=needs_sorting");

        Assert.Equal(new IntakeQueueCounts(1, 1), counts);
        Assert.Contains(
            "<strong>1</strong><span>Review</span><small>Current intake drafts</small>",
            dashboard,
            StringComparison.Ordinal);
        Assert.Contains(
            "<strong>1</strong><span>Needs sorting</span><small>Current intake receipts</small>",
            dashboard,
            StringComparison.Ordinal);
        Assert.Contains("Needs sorting", sortingQueue, StringComparison.Ordinal);
        Assert.Contains(NeedsSortingEmailHash[..12], sortingQueue, StringComparison.Ordinal);
    }

    private static async Task<IntakeReceipt> GetReceiptAsync(
        IntakeWebApplicationFactory factory,
        Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
        return Assert.IsType<IntakeReceipt>(await queries.GetAsync(id, CancellationToken.None));
    }
}
