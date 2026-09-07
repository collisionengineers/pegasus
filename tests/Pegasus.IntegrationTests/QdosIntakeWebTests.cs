using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    public async Task ReadableManualUploadStagesPendingWorkAndOpensItsStatusPage()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            localIntakeEnabled: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        const string receiptToken = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var email = IntakeTestEvidence.CreateEmail(
            "ordinary-correspondence.eml",
            "Please review this ordinary correspondence.",
            "sender@example.test");

        var upload = await IntakeWebDriver.UploadAsync(
            client,
            email.FileName,
            email.MediaType,
            email.Content,
            receiptToken);

        var stagedReceiptId = Assert.IsType<Guid>(IntakeWebDriver.Landing(upload).StagedReceiptId);
        Assert.StartsWith("/Upload/Status/", upload.Location!.OriginalString, StringComparison.OrdinalIgnoreCase);
        await using (var statusScope = factory.Services.CreateAsyncScope())
        {
            var work = Assert.IsType<IntakeWorkItem>(
                await statusScope.ServiceProvider.GetRequiredService<IIntakeWorkStore>()
                    .FindWorkItemAsync(stagedReceiptId, CancellationToken.None));
            Assert.Equal(IntakeWorkState.Pending, work.State);
            Assert.Null(await statusScope.ServiceProvider.GetRequiredService<IIntakeWorkStore>()
                .GetCompletedEvaluationAsync(stagedReceiptId, CancellationToken.None));
            Assert.Throws<InvalidOperationException>(
                () => statusScope.ServiceProvider.GetRequiredService<ProcessQueuedIntake>());
        }
        using var statusPage = await client.GetAsync(upload.Location);
        statusPage.EnsureSuccessStatusCode();
        var html = await statusPage.Content.ReadAsStringAsync();
        Assert.Contains("<h1>Received</h1>", html, StringComparison.Ordinal);
        Assert.Contains("ordinary-correspondence.eml", html, StringComparison.Ordinal);
        Assert.Contains("data-auto-refresh=\"2000\"", html, StringComparison.Ordinal);
        // The state is the heading and the values are the panel: nothing
        // beneath the heading narrates either of them back (PLAT-015).
        Assert.DoesNotContain("class=\"lede\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "safely received and waiting for background processing",
            html,
            StringComparison.Ordinal);

        _ = await IntakeWebDriver.ProcessQueuedAsync(factory, upload);
        using var completedStatusPage = await client.GetAsync(upload.Location);
        completedStatusPage.EnsureSuccessStatusCode();
        var completedHtml = await completedStatusPage.Content.ReadAsStringAsync();
        Assert.Contains("<h1>Complete</h1>", completedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("data-auto-refresh=\"2000\"", completedHtml, StringComparison.Ordinal);
        // No case exists yet and this ordinary correspondence carries no
        // identifiable instruction to become one from, so the confirmation
        // step reports it needing a staff decision rather than offering to
        // create a case from nothing (INTK-010's decision table).
        Assert.Contains("Unidentified", completedHtml, StringComparison.Ordinal);
        Assert.Contains(
            "This could not be matched automatically and needs a staff decision.",
            completedHtml,
            StringComparison.Ordinal);
        Assert.Contains("/Unidentified/", completedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Open case", completedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Create a case", completedHtml, StringComparison.Ordinal);

        var duplicate = await IntakeWebDriver.UploadAsync(
            client,
            email.FileName,
            email.MediaType,
            email.Content,
            receiptToken);
        Assert.Equal(stagedReceiptId, IntakeWebDriver.Landing(duplicate).StagedReceiptId);
        using var duplicateStatusPage = await client.GetAsync(duplicate.Location);
        var duplicateHtml = await duplicateStatusPage.Content.ReadAsStringAsync();
        Assert.Contains(
            "<dt>Duplicate</dt><dd>Already received</dd>",
            duplicateHtml,
            StringComparison.Ordinal);
        Assert.DoesNotContain("No duplicate was created", duplicateHtml, StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        Assert.IsType<ReceiveIntake>(
            scope.ServiceProvider.GetRequiredService<IIntakeSubmission>());
    }

    [Fact]
    public async Task UploadStatusIsStaffOnlyAndUnknownReceiptsReturnNotFound()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);

        using var missing = await client.GetAsync($"/Upload/Status/{Guid.NewGuid():D}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        using var anonymousRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/Upload/Status/{Guid.NewGuid():D}");
        anonymousRequest.Headers.Add("X-Test-Anonymous", "1");
        using var anonymous = await client.SendAsync(anonymousRequest);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);

        using var rolelessRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/Upload/Status/{Guid.NewGuid():D}");
        rolelessRequest.Headers.Add("X-Test-Roleless", "1");
        using var roleless = await client.SendAsync(rolelessRequest);
        Assert.Equal(HttpStatusCode.Forbidden, roleless.StatusCode);
    }

    [Fact]
    public async Task FailedUploadStatusShowsReasonWithoutAResolvedStaffActor()
    {
        var stagedReceiptId = Guid.NewGuid();
        var status = new QueuedIntakeStatus(
            stagedReceiptId,
            "failed-upload.eml",
            new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero),
            QueuedIntakeStatusKind.Failed,
            ProcessedReceiptId: null,
            FailureCode: "unexpected_intake_processing_failure");
        using var baseFactory = new IntakeWebApplicationFactory(
            useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IQueuedIntakeStatusQueries>();
                services.AddSingleton<IQueuedIntakeStatusQueries>(
                    new FixedQueuedIntakeStatusQueries(status));
                services.AddSingleton<IClaimsTransformation,
                    RemoveNameIdentifierClaimsTransformation>();
            }));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/Upload/Status/{stagedReceiptId:D}");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains(
            "<dt>Reason</dt><dd>Processing failed for a technical reason</dd>",
            html,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "unexpected_intake_processing_failure",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompletedAllocatedUploadStatusLinksOnlyToItsCase()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "allocated-status.eml",
            "QDOS instruction\r\nClaimant Name: Status Claimant\r\nClaim Number: STATUS-001\r\nVehicle Registration: AB12 CDE");
        var upload = await IntakeWebDriver.UploadAsync(
            client,
            email.FileName,
            email.MediaType,
            email.Content);

        _ = await IntakeWebDriver.ProcessQueuedAsync(factory, upload);
        var principal = $"S{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        await AllocationTestData.SeedPrincipalAsync(factory.Services, principal);
        var allocatedReceipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.Inspection,
            principal);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var allocation = await scope.ServiceProvider.GetRequiredService<IAllocateIntake>()
                .AttemptAutomaticAsync(allocatedReceipt.Id, Guid.NewGuid());
            Assert.NotNull(allocation?.State.CaseId);
        }
        await AllocationTestData.PointCompletedWorkAtReceiptAsync(
            factory.Services,
            Assert.IsType<Guid>(IntakeWebDriver.Landing(upload).StagedReceiptId),
            allocatedReceipt.Id);
        using var statusPage = await client.GetAsync(upload.Location);
        statusPage.EnsureSuccessStatusCode();
        var html = await statusPage.Content.ReadAsStringAsync();
        Assert.Contains("Open case", html, StringComparison.Ordinal);
        Assert.Contains("/Cases/", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Open receipt", html, StringComparison.Ordinal);
    }

    [GenuineQdosCorpusFact(ForwardedEmailHash)]
    [Trait("Category", "Corpus")]
    public async Task StaffForwardedEmailUsesEstablishedSenderAndPersistsDraft()
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
        var draft = Assert.IsType<InstructionDraft>(receipt.InstructionDraft);
        Assert.Equal(ForwardedEmailHash, receipt.SourceHash);
        Assert.Contains(receipt.Evidence, item =>
            item.Source == IntakeEvidenceSource.Sender
            && item.Strength == IntakeEvidenceStrength.Strong
            && item.Finding == IntakeEvidenceFinding.SupportsPrincipal
            && item.Signal == "established-principal");
        var instructionDate = Assert.Single(receipt.Fields, field => field.Name == "Instruction date");
        Assert.False(instructionDate.IsDefaulted);
        Assert.Equal("10 July 2026", instructionDate.SuggestedValue);
        Assert.Equal(new DateOnly(2026, 7, 10), draft.InstructionDate);
        Assert.Equal("QDOS", draft.SuggestedPrincipalCode);

        var route = Assert.IsType<MailRouteEvaluationResult>(receipt.MailRouteDecision);
        Assert.Equal(MailRouteDisposition.Accepted, route.Disposition);
        Assert.Equal(QdosMailRoutePolicy.Key, route.PolicyKey);
        Assert.Equal(QdosMailRoutePolicy.Version, route.PolicyVersion);
        var selectedRoute = Assert.IsType<MailRouteSelection>(route.SelectedRoute);
        Assert.Equal("QDOS", selectedRoute.RouteOwnerCode);
        Assert.Equal(MailRouteKind.DirectProvider, selectedRoute.Kind);
        Assert.Equal("QDOS", selectedRoute.WorkProviderCode);
        Assert.Contains(route.Predicates, predicate =>
            predicate.Key == "forward.staff-transport" && predicate.Matched);
        Assert.Contains(route.Predicates, predicate =>
            predicate.Key == "forward.original-exactly-one" && predicate.Matched);
        Assert.Contains(route.Predicates, predicate =>
            predicate.Key == "forward.original-external" && predicate.Matched);
        Assert.Contains(route.Predicates, predicate =>
            predicate.Key == "direct.qdos-domain" && predicate.Matched);

        var caseId = Assert.IsType<Guid>(receipt.CurrentCaseId);
        var caseReference = Assert.IsType<string>(receipt.CurrentCaseReference);
        Assert.False(string.IsNullOrWhiteSpace(caseReference));
        Assert.Contains("<h1>Case created</h1>", html, StringComparison.Ordinal);
        Assert.Contains(receipt.SourceFileName, html, StringComparison.Ordinal);
        Assert.Contains($"/Cases/{caseId:D}", html, StringComparison.Ordinal);
        Assert.Contains(caseReference, html, StringComparison.Ordinal);
    }

    [GenuineQdosCorpusFact(LowTextNonScanPdfHash)]
    [Trait("Category", "Corpus")]
    public async Task LowTextPdfWithoutDominantRasterStaysUnidentifiedWithoutOcrOrCaseReference()
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
        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Equal(LowTextNonScanPdfHash, receipt.SourceHash);
        Assert.Null(receipt.FailureCode);
        Assert.Null(receipt.MailClassificationDecision);
        Assert.Null(receipt.InstructionDraft);
        Assert.Null(receipt.CurrentCaseId);
        Assert.Null(receipt.CurrentCaseReference);
        Assert.Empty(receipt.ScannedPdfPages);
        Assert.Contains(receipt.Evidence, evidence => evidence.Signal == "insufficient-embedded-text");
        Assert.Contains("<h1>Unidentified</h1>", reviewHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"triage-title\"", reviewHtml, StringComparison.Ordinal);
        Assert.Contains("not an image-led scanned page", reviewHtml, StringComparison.Ordinal);
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
        Assert.Contains("This file was already received. The existing record is shown.", duplicateHtml, StringComparison.Ordinal);
        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
        Assert.Equal(2, (await queries.ListAsync(null, 1, 100, CancellationToken.None)).TotalCount);
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
            var initialUploads = await Task.WhenAll(samples.Select((sample, index) =>
                IntakeWebDriver.UploadAsync(clients[index], sample)));
            var uploads = new UploadResult[initialUploads.Length];
            for (var index = 0; index < initialUploads.Length; index++)
            {
                var initial = initialUploads[index];
                if (initial.StatusCode == HttpStatusCode.Redirect)
                {
                    uploads[index] = initial;
                    continue;
                }

                // UploadModel returns this exact response only from its recoverable
                // intake-exception branch. Retrying once retains the original token
                // and bytes; every validation, authorization and other 200 response
                // remains a test failure rather than being hidden as a retry.
                Assert.True(
                    initial.StatusCode == HttpStatusCode.OK
                    && initial.Location is null
                    && initial.ResponseBody.Contains(
                        "The file could not be processed. Try again, or contact an administrator if it keeps failing.",
                        StringComparison.Ordinal),
                    $"Unexpected concurrent upload response for {samples[index].Hash[..12]}: "
                    + $"{initial.StatusCode}; {initial.ResponseBody}");
                uploads[index] = await IntakeWebDriver.UploadAsync(
                    clients[index],
                    samples[index],
                    initial.ExternalReceiptToken);
                Assert.True(
                    uploads[index].StatusCode == HttpStatusCode.Redirect,
                    $"The one permitted retry failed for {samples[index].Hash[..12]}. "
                    + $"Initial response: {initial.StatusCode}; {initial.ResponseBody}");
            }
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
        var receipts = await queries.ListAsync(IntakeDecision.CaseCreated, 1, 100, CancellationToken.None);
        Assert.Equal(5, receipts.TotalCount);
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
        var sortingQueue = await queries.ListAsync(IntakeDecision.NeedsSorting, 1, 25, CancellationToken.None);

        Assert.Equal(new IntakeQueueCounts(1, 0), counts);
        Assert.Matches(
            "(?s)data-value=\"unidentified\"(?:(?!</a>).)*?<span class=\"metric-value\">1</span>",
            dashboard);
        Assert.Matches(
            "(?s)data-value=\"blocked\"(?:(?!</a>).)*?<span class=\"metric-value\">0</span>",
            dashboard);
        var sortingItem = Assert.Single(sortingQueue.Items);
        Assert.Equal(IntakeDecision.NeedsSorting, sortingItem.Decision);
        Assert.False(string.IsNullOrWhiteSpace(sortingItem.SourceFileName));
    }

    private static async Task<IntakeReceipt> GetReceiptAsync(
        IntakeWebApplicationFactory factory,
        Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
        return Assert.IsType<IntakeReceipt>(await queries.GetAsync(id, CancellationToken.None));
    }

    private sealed class FixedQueuedIntakeStatusQueries(QueuedIntakeStatus status)
        : IQueuedIntakeStatusQueries
    {
        public Task<QueuedIntakeStatus?> GetAsync(
            Guid stagedReceiptId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<QueuedIntakeStatus?>(
                stagedReceiptId == status.StagedReceiptId ? status : null);
    }

    private sealed class RemoveNameIdentifierClaimsTransformation : IClaimsTransformation
    {
        public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            foreach (var identity in principal.Identities.OfType<ClaimsIdentity>())
            {
                foreach (var claim in identity.FindAll(ClaimTypes.NameIdentifier).ToArray())
                {
                    identity.RemoveClaim(claim);
                }
            }

            return Task.FromResult(principal);
        }
    }
}
