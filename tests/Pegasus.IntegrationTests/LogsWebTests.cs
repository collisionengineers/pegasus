using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Actors;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using OperatorLabels = Pegasus.Web.Presentation.OperatorLabels;
using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Administration › Logs (13 September): the Action logs and Intake log tabs, the
/// Intake log's head-line counts, filters and row drawer with Re-evaluate, Retry
/// allocation and Retry OCR — one owner (the Logs handlers), Administrators only.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class LogsWebTests
{
    private static readonly DateTimeOffset ReceivedAtUtc = new(2031, 5, 6, 9, 31, 0, TimeSpan.Zero);

    /// <summary>Logs reads in words: a stored action code becomes wording, AI and API stay initialisms, and a result takes a capital (v41 A13).</summary>
    [Theory]
    [InlineData("ai_job_failed", "AI job failed")]
    [InlineData("principal_api_credential_issued", "Principal API credential issued")]
    [InlineData("SignInFailed", "Sign in failed")]
    public void AnActionCodeReadsAsWords(string code, string expected) =>
        Assert.Equal(expected, OperatorLabels.ActionLogAction(code));

    [Theory]
    [InlineData("succeeded", "Succeeded")]
    [InlineData("Denied", "Denied")]
    public void AResultReadsWithACapitalWhateverItsStoredSpelling(string stored, string expected) =>
        Assert.Equal(expected, OperatorLabels.ActionLogResult(stored));

    [Fact]
    public async Task TheLogsPageHasBothTabsAndTheIntakeLogReadsTheRealStore()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var actionLogs = await GetHtmlAsync(client, "/Administration/Logs");
        Assert.Contains("<h1>Logs</h1>", actionLogs, StringComparison.Ordinal);
        Assert.Matches("<a class=\"tab\" href=\"/Administration/Logs\" aria-current=\"page\">Action logs</a>", actionLogs);
        Assert.Contains("href=\"/Administration/Logs?tab=intake\"", actionLogs, StringComparison.Ordinal);
        Assert.Contains("compact-log-filter", actionLogs, StringComparison.Ordinal);
        // The Action logs filter names its fields as its columns are headed (v41 A24).
        foreach (var label in new[] { "Search", "Action", "Reference", "Result" })
        {
            Assert.Contains($">{label}</label>", actionLogs, StringComparison.Ordinal);
        }

        var intake = await GetHtmlAsync(client, "/Administration/Logs?tab=intake");
        Assert.Matches("aria-current=\"page\" data-logs-tab=\"intake\">Intake log</a>", intake);
        Assert.Contains("data-intake-counts", intake, StringComparison.Ordinal);
        Assert.Contains("data-failed-intake", intake, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.IntakeLog.FailedFiles}</span>", intake, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.IntakeLog.OldestFileWaiting}</span>", intake, StringComparison.Ordinal);
        Assert.Contains(">All outcomes</option>", intake, StringComparison.Ordinal);
        Assert.Contains(">Could not be read</option>", intake, StringComparison.Ordinal);
        Assert.Contains(">All sources</option>", intake, StringComparison.Ordinal);
        Assert.Contains("name=\"IntakePrincipal\"", intake, StringComparison.Ordinal);
        Assert.Contains("name=\"IntakeFrom\"", intake, StringComparison.Ordinal);
        // The Intake log's filter takes the Action logs structure (v41 A24).
        Assert.Contains("compact-log-filter", intake, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheIntakeLogListsRowsOpensTheDrawerAndRunsItsThreeActions()
    {
        var log = new RecordingIntakeLog();
        using var baseFactory = IntakeWebApplicationFactory.WithoutDatabase();
        using var factory = Configure(baseFactory, log);
        using var client = CreateClient(factory);

        var list = await GetHtmlAsync(client, "/Administration/Logs?tab=intake&IntakeOutcome=ProcessingFailed");
        Assert.Equal(IntakeLogOutcome.ProcessingFailed, log.LastFilter?.Outcome);
        Assert.Contains($"data-intake-row=\"{log.ReceiptId:D}\"", list, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<span class=\"metric-value\" data-failed-intake>3</span>", list, StringComparison.Ordinal);
        Assert.Contains("Processing failed", list, StringComparison.Ordinal);
        Assert.Contains("desk@collisionengineers.co.uk", list, StringComparison.Ordinal);
        // Razor encodes the middle dot in an expression.
        Assert.Contains("2 processing &#xB7; 3 allocation", list, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Received/{log.ReceiptId:D}/Source\"", list, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-intake-drawer", list, StringComparison.Ordinal);

        var drawerRoute = $"/Administration/Logs?tab=intake&Receipt={log.ReceiptId:D}";
        var drawer = await GetHtmlAsync(client, drawerRoute);
        Assert.Contains($"data-intake-drawer=\"{log.ReceiptId:D}\"", drawer, StringComparison.OrdinalIgnoreCase);
        // The row opens in the shared dialog frame with the one close (v41 A24).
        Assert.Contains("data-dialog=\"intake-drawer\" data-dialog-open-on-load=\"true\"", drawer, StringComparison.Ordinal);
        Assert.Contains("class=\"dialog-close\"", drawer, StringComparison.Ordinal);
        Assert.Contains("Retained original", drawer, StringComparison.Ordinal);
        Assert.Contains("Processing evidence", drawer, StringComparison.Ordinal);
        Assert.Contains("AB12CDE", drawer, StringComparison.Ordinal);
        Assert.Contains("The PDF text layer was empty.", drawer, StringComparison.Ordinal);
        Assert.Contains("data-intake-action=\"reevaluate\"", drawer, StringComparison.Ordinal);
        Assert.Contains("data-intake-action=\"retry-allocation\"", drawer, StringComparison.Ordinal);
        Assert.Contains("data-intake-action=\"retry-ocr\"", drawer, StringComparison.Ordinal);

        var token = Input(drawer, "__RequestVerificationToken");
        var reevaluate = ActionForm(drawer, "reevaluate");
        using (var response = await client.PostAsync(
            "/Administration/Logs?handler=ReevaluateIntake",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["receiptId"] = log.ReceiptId.ToString("D"),
                ["expectedVersion"] = Input(reevaluate, "expectedVersion"),
                ["operationKey"] = Input(reevaluate, "operationKey"),
                ["reason"] = "The sender re-sent a readable copy.",
                ["returnUrl"] = Input(reevaluate, "returnUrl")
            })))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(drawerRoute, response.Headers.Location?.OriginalString, ignoreCase: true);
        }
        var reevaluation = Assert.Single(log.Reevaluations);
        Assert.Equal(log.ReceiptId, reevaluation.ReceiptId);
        Assert.Equal(7, reevaluation.ExpectedVersion);
        Assert.Equal("The sender re-sent a readable copy.", reevaluation.Reason);
        Assert.Equal(ActorKind.Staff, reevaluation.Actor.Kind);

        // Open message sits beside Open file when the row carries its message.
        Assert.Contains($"href=\"/Inbox/{log.MessageId:D}\" data-intake-open-message", drawer, StringComparison.OrdinalIgnoreCase);

        // Retry OCR is its own Core command with a reason, not a re-evaluation.
        var ocr = ActionForm(drawer, "retry-ocr");
        Assert.Contains("name=\"reason\"", ocr, StringComparison.Ordinal);
        using (var response = await client.PostAsync(
            "/Administration/Logs?handler=RetryIntakeOcr",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["receiptId"] = log.ReceiptId.ToString("D"),
                ["expectedVersion"] = Input(ocr, "expectedVersion"),
                ["operationKey"] = Input(ocr, "operationKey"),
                ["reason"] = "The OCR provider was unavailable.",
                ["returnUrl"] = Input(ocr, "returnUrl")
            })))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }
        var ocrRetry = Assert.Single(log.OcrRetries);
        Assert.Equal("The OCR provider was unavailable.", ocrRetry.Reason);
        Assert.Equal(7, ocrRetry.ExpectedVersion);
        Assert.Single(log.Reevaluations);

        var allocation = ActionForm(drawer, "retry-allocation");
        using (var response = await client.PostAsync(
            "/Administration/Logs?handler=RetryIntakeAllocation",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["receiptId"] = log.ReceiptId.ToString("D"),
                ["expectedVersion"] = Input(allocation, "expectedVersion"),
                ["expectedAttemptId"] = Input(allocation, "expectedAttemptId"),
                ["operationKey"] = Input(allocation, "operationKey"),
                ["reason"] = "The principal code was corrected.",
                ["returnUrl"] = Input(allocation, "returnUrl")
            })))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }
        var retry = Assert.Single(log.AllocationRetries);
        Assert.Equal(log.AttemptId, retry.ExpectedCurrentAttemptId);
        Assert.Equal("The principal code was corrected.", retry.Reason);

        // A reason is required; nothing is sent without one.
        using (var refused = await client.PostAsync(
            "/Administration/Logs?handler=ReevaluateIntake",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["receiptId"] = log.ReceiptId.ToString("D"),
                ["expectedVersion"] = "7",
                ["operationKey"] = Guid.NewGuid().ToString("N"),
                ["reason"] = " "
            })))
        {
            Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        }
        Assert.Single(log.Reevaluations);

        // Retry OCR without a reason sends nothing either.
        using (var refusedOcr = await client.PostAsync(
            "/Administration/Logs?handler=RetryIntakeOcr",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["receiptId"] = log.ReceiptId.ToString("D"),
                ["expectedVersion"] = "7",
                ["operationKey"] = Guid.NewGuid().ToString("N")
            })))
        {
            Assert.Equal(HttpStatusCode.Redirect, refusedOcr.StatusCode);
        }
        Assert.Single(log.OcrRetries);
    }

    /// <summary>
    /// Retry OCR and Re-evaluate posted from the drawer run through the real
    /// Core and store (FRD-17 §Acceptance evidence): each records one action
    /// with its reason and the Administrator, the drawer stops offering a
    /// queued OCR retry, and a refusal shows the store's or the page's words.
    /// </summary>
    [Fact]
    public async Task TheIntakeLogActionsRunThroughTheRealCoreAndStore()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "scan.png",
            "image/png",
            Convert.FromBase64String(MultiFormatFixture.TinyPngBase64),
            Guid.NewGuid().ToString("N"));
        var receiptId = IntakeWebDriver.ReceiptId(upload);
        Guid operationId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            operationId = await IntakeOcrRetryPersistenceTests.SeedFailedOcrAttemptAsync(
                scope.ServiceProvider, receiptId, DateTimeOffset.UtcNow.AddMinutes(-5));
        }

        var drawer = await GetHtmlAsync(client, $"/Administration/Logs?tab=intake&Receipt={receiptId:D}");
        drawer = await PostIntakeActionAsync(
            client, drawer, "RetryIntakeOcr", ActionForm(drawer, "retry-ocr"), "The OCR provider was unavailable.");
        Assert.Contains("OCR will be retried.", drawer, StringComparison.Ordinal);
        Assert.DoesNotContain("data-intake-action=\"retry-ocr\"", drawer, StringComparison.Ordinal);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await using var context = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
                .CreateDbContextAsync();
            var work = await context.Set<ExternalWorkItemEntity>().AsNoTracking().SingleAsync(item => item.Id == operationId);
            Assert.Equal(ExternalWorkStatePersistence.Pending, work.State);
            Assert.Equal(0, work.AttemptCount);
            var recorded = Assert.Single(await HistoryAsync(context, receiptId, "intake_ocr_retry_queued"));
            Assert.Equal("The OCR provider was unavailable.", recorded.Reason);
            Assert.Equal(DevelopmentOfflineIdentity.AdministratorId.ToString("D"), recorded.ActorSubjectId);
        }

        // Retry OCR again, at the drawer's current version with a new key: the
        // store refuses it in its own words.
        var current = ActionForm(drawer, "reevaluate");
        drawer = await PostIntakeActionAsync(
            client, drawer, "RetryIntakeOcr", current, "Again.",
            operationKey: Guid.NewGuid().ToString("N"));
        Assert.Contains(
            "<div class=\"notice notice--danger\" role=\"alert\"><span>OCR can be retried only when its last attempt failed.</span></div>",
            drawer,
            StringComparison.Ordinal);

        var reevaluate = ActionForm(drawer, "reevaluate");
        var earlierVersion = Input(reevaluate, "expectedVersion");
        drawer = await PostIntakeActionAsync(
            client, drawer, "ReevaluateIntake", reevaluate, "The sender re-sent a readable copy.");
        Assert.Contains("Re-evaluation with current policy was queued.", drawer, StringComparison.Ordinal);

        // A repeat with a new key and the version it was read at is refused.
        drawer = await PostIntakeActionAsync(
            client, drawer, "ReevaluateIntake", ActionForm(drawer, "reevaluate"), "Once more.",
            expectedVersion: earlierVersion,
            operationKey: Guid.NewGuid().ToString("N"));
        Assert.Contains(
            "<div class=\"notice notice--danger\" role=\"alert\"><span>The received item changed. Reload it before trying again.</span></div>",
            drawer,
            StringComparison.Ordinal);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await using var context = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
                .CreateDbContextAsync();
            var recorded = Assert.Single(await HistoryAsync(context, receiptId, "intake_reevaluation_queued"));
            Assert.Equal("The sender re-sent a readable copy.", recorded.Reason);
            Assert.Equal(DevelopmentOfflineIdentity.AdministratorId.ToString("D"), recorded.ActorSubjectId);
            Assert.Single(await HistoryAsync(context, receiptId, "intake_ocr_retry_queued"));
        }
    }

    /// <summary>
    /// Retry allocation posted from the drawer on the real Core and store: with
    /// the Principal absent it shows why and creates nothing; once the
    /// Principal exists it creates the Case once.
    /// </summary>
    [Fact]
    public async Task RetryAllocationFromTheIntakeLogCreatesTheCaseOnceAndAFailedRetryShowsItsReason()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.Inspection,
            "RECOVER");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var first = await scope.ServiceProvider.GetRequiredService<IAllocateIntake>()
                .AttemptAutomaticAsync(scope.ServiceProvider, receipt.Id, Guid.NewGuid());
            Assert.Equal(IntakeAllocationProjectionStatus.FailedRecoverable, first?.State.Status);
        }

        var drawerRoute = $"/Administration/Logs?tab=intake&Receipt={receipt.Id:D}";
        var drawer = await GetHtmlAsync(client, drawerRoute);
        drawer = await PostIntakeActionAsync(
            client, drawer, "RetryIntakeAllocation", ActionForm(drawer, "retry-allocation"), "Retry before correction.");
        Assert.Contains("<div class=\"notice notice--danger\" role=\"alert\">", drawer, StringComparison.Ordinal);
        Assert.Equal(0, await AllocationTestData.CountAsync(factory.Services, "Cases"));

        await AllocationTestData.SeedPrincipalAsync(factory.Services, "RECOVER");
        drawer = await GetHtmlAsync(client, drawerRoute);
        drawer = await PostIntakeActionAsync(
            client, drawer, "RetryIntakeAllocation", ActionForm(drawer, "retry-allocation"), "The principal was added.");
        Assert.Contains("Allocation was retried and the case was created.", drawer, StringComparison.Ordinal);
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "Cases"));
        Assert.Equal(1, await AllocationTestData.CountAsync(factory.Services, "CaseIntakeLinks"));
    }

    /// <summary>Posts one drawer action with the drawer's own values, then reads the page it returns to.</summary>
    private static async Task<string> PostIntakeActionAsync(
        HttpClient client,
        string drawer,
        string handler,
        string form,
        string reason,
        string? expectedVersion = null,
        string? operationKey = null)
    {
        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Input(drawer, "__RequestVerificationToken"),
            ["receiptId"] = Input(form, "receiptId"),
            ["expectedVersion"] = expectedVersion ?? Input(form, "expectedVersion"),
            ["operationKey"] = operationKey ?? Input(form, "operationKey"),
            ["reason"] = reason,
            ["returnUrl"] = Input(form, "returnUrl")
        };
        if (form.Contains("name=\"expectedAttemptId\"", StringComparison.Ordinal))
        {
            fields["expectedAttemptId"] = Input(form, "expectedAttemptId");
        }

        using var response = await client.PostAsync(
            $"/Administration/Logs?handler={handler}", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return await GetHtmlAsync(client, response.Headers.Location!.OriginalString);
    }

    private static async Task<List<IntakeMutationHistoryEntity>> HistoryAsync(
        PegasusDbContext context, Guid receiptId, string eventType) =>
        await context.IntakeMutationHistory.AsNoTracking()
            .Where(item => item.IntakeReceiptId == receiptId && item.EventType == eventType)
            .ToListAsync();

    private static WebApplicationFactory<Program> Configure(IntakeWebApplicationFactory baseFactory, RecordingIntakeLog log) =>
        baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IListIntakeLog>();
            services.RemoveAll<IReevaluateIntake>();
            services.RemoveAll<IRetryIntakeOcr>();
            services.RemoveAll<IAllocateIntake>();
            services.AddSingleton<IListIntakeLog>(log);
            services.AddSingleton<IReevaluateIntake>(log);
            services.AddSingleton<IRetryIntakeOcr>(log);
            services.AddSingleton<IAllocateIntake>(log);
        }));

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private static string ActionForm(string html, string action)
    {
        var form = Regex.Match(html, $"<form[^>]*data-intake-action=\"{action}\"[^>]*>[\\s\\S]*?</form>");
        Assert.True(form.Success, $"The {action} form was not rendered.");
        return form.Value;
    }

    private static string Input(string html, string name)
    {
        var tag = Regex.Match(html, $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*>");
        Assert.True(tag.Success, $"The input '{name}' was not rendered.");
        return WebUtility.HtmlDecode(Regex.Match(tag.Value, "value=\"(?<value>[^\"]*)\"").Groups["value"].Value);
    }

    private sealed class RecordingIntakeLog : IListIntakeLog, IReevaluateIntake, IRetryIntakeOcr, IAllocateIntake
    {
        public Guid ReceiptId { get; } = Guid.NewGuid();
        public Guid AttemptId { get; } = Guid.NewGuid();
        public Guid MessageId { get; } = Guid.NewGuid();
        public IntakeLogFilter? LastFilter { get; private set; }
        public List<IntakeLogFilter> Filters { get; } = [];
        public List<ReevaluateIntakeRequest> Reevaluations { get; } = [];
        public List<RetryIntakeOcrRequest> OcrRetries { get; } = [];
        public List<RetryIntakeAllocationRequest> AllocationRetries { get; } = [];

        private IntakeLogRow RowFor(IntakeLogOutcome outcome) => new(
            ReceiptId,
            ReceivedAtUtc,
            new IntakeLogSource(IntakeSourceChannel.Mailbox, "desk@collisionengineers.co.uk", "nduncombe@example.invalid"),
            "57709_1_LtrtoEngineerIn.pdf",
            Guid.NewGuid(),
            outcome,
            "The PDF text layer was empty.",
            null,
            2,
            3)
        {
            MessageId = MessageId
        };

        private IntakeLogRow Row => RowFor(LastFilter?.Outcome ?? IntakeLogOutcome.ProcessingFailed);

        private IntakeReceipt Receipt => new(
            ReceiptId,
            "57709_1_LtrtoEngineerIn.pdf",
            "application/pdf",
            102_400,
            new string('A', 64),
            new IntakeSourceIdentity(IntakeSourceChannel.Mailbox, "desk-token"),
            ReceivedAtUtc,
            ReceivedAtUtc.AddMinutes(1),
            IntakeDecision.TechnicalFailure,
            "Processing could not complete.",
            [],
            [],
            null,
            [],
            "unreadable_pdf",
            "The PDF text layer was empty.",
            false,
            "pdf",
            "1",
            null,
            null,
            Version: 7);

        public Task<IntakeLogPage> ExecuteAsync(ActionActor actor, IntakeLogFilter filter, int page, CancellationToken cancellationToken)
        {
            LastFilter = filter;
            Filters.Add(filter);
            return Task.FromResult(new IntakeLogPage([Row], page, IntakeLogPolicy.PageSize, 1));
        }

        public Task<IntakeReceipt> ExecuteAsync(RetryIntakeOcrRequest request, CancellationToken cancellationToken = default)
        {
            OcrRetries.Add(request);
            return Task.FromResult(Receipt);
        }

        public Task<IntakeLogCounts> CountsAsync(ActionActor actor, CancellationToken cancellationToken) =>
            Task.FromResult(new IntakeLogCounts(3, ReceivedAtUtc));

        public Task<IntakeLogDetail?> GetAsync(ActionActor actor, Guid receiptId, CancellationToken cancellationToken) =>
            Task.FromResult<IntakeLogDetail?>(receiptId != ReceiptId ? null : new IntakeLogDetail(
                Row,
                Receipt,
                [new ImageVrmSuggestion(
                    Guid.NewGuid(), ReceiptId, Guid.NewGuid(), "key", new string('B', 64), "engine", "1", "hash",
                    VrmRecognitionOutcomeKind.Suggested, "AB12CDE", 0.93, null, null, ReceivedAtUtc,
                    ImageVrmSuggestionDisposition.Pending, null, null, null)],
                [new IntakeAllocationState(
                    AttemptId,
                    IntakeAllocationProjectionStatus.FailedRecoverable,
                    null,
                    ReceivedAtUtc,
                    SafeReason: "The principal code was not recognised.")],
                new IntakeLogActions(CanReevaluate: true, CanRetryAllocation: true, CanRetryOcr: true)));

        public Task<IntakeReceipt> ExecuteAsync(ReevaluateIntakeRequest request, CancellationToken cancellationToken = default)
        {
            Reevaluations.Add(request);
            return Task.FromResult(Receipt);
        }

        public Task<IntakeAllocationResult?> AttemptAutomaticAsync(IntakeReceipt receipt, Guid evaluationId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IntakeAllocationResult> AttemptStaffCreateAsync(AcceptIntakeRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IntakeAllocationResult> RetryAsync(RetryIntakeAllocationRequest request, CancellationToken cancellationToken = default)
        {
            AllocationRetries.Add(request);
            return Task.FromResult(new IntakeAllocationResult(
                new IntakeAllocationState(Guid.NewGuid(), IntakeAllocationProjectionStatus.Succeeded, null, ReceivedAtUtc, CaseId: Guid.NewGuid(), CaseReference: "QDOS31001"),
                IsReplay: false,
                IsSuppressed: false));
        }
    }
}
