using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Administration › Logs (13 September): the Action logs and Intake log tabs, the
/// Intake log's head-line counts, filters and row drawer with Re-evaluate, Retry
/// allocation and Retry OCR, and the same actions on Operations' failed intake
/// processing rows — one owner (the Logs handlers), Administrators only.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class LogsWebTests
{
    private static readonly DateTimeOffset ReceivedAtUtc = new(2031, 5, 6, 9, 31, 0, TimeSpan.Zero);

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
        // The intake metrics moved to the Intake log's head-line counts.
        Assert.DoesNotContain(">Failed intake</dt>", actionLogs, StringComparison.Ordinal);

        var intake = await GetHtmlAsync(client, "/Administration/Logs?tab=intake");
        Assert.Matches("aria-current=\"page\" data-logs-tab=\"intake\">Intake log</a>", intake);
        Assert.Contains("data-intake-counts", intake, StringComparison.Ordinal);
        Assert.Contains("data-failed-intake", intake, StringComparison.Ordinal);
        Assert.Contains(">All outcomes</option>", intake, StringComparison.Ordinal);
        Assert.Contains(">Could not be read</option>", intake, StringComparison.Ordinal);
        Assert.Contains(">All sources</option>", intake, StringComparison.Ordinal);
        Assert.Contains("name=\"IntakePrincipal\"", intake, StringComparison.Ordinal);
        Assert.Contains("name=\"IntakeFrom\"", intake, StringComparison.Ordinal);
        Assert.DoesNotContain("compact-log-filter", intake, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheIntakeLogListsRowsOpensTheDrawerAndRunsItsThreeActions()
    {
        var log = new RecordingIntakeLog();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = Configure(baseFactory, log);
        using var client = CreateClient(factory);

        var list = await GetHtmlAsync(client, "/Administration/Logs?tab=intake&IntakeOutcome=ProcessingFailed");
        Assert.Equal(IntakeLogOutcome.ProcessingFailed, log.LastFilter?.Outcome);
        Assert.Contains($"data-intake-row=\"{log.ReceiptId:D}\"", list, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<span class=\"value\" data-failed-intake>3</span>", list, StringComparison.Ordinal);
        Assert.Contains("Processing failed", list, StringComparison.Ordinal);
        Assert.Contains("desk@collisionengineers.co.uk", list, StringComparison.Ordinal);
        // Razor encodes the middle dot in an expression.
        Assert.Contains("2 processing &#xB7; 3 allocation", list, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Received/{log.ReceiptId:D}/Source\"", list, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-intake-drawer", list, StringComparison.Ordinal);

        var drawerRoute = $"/Administration/Logs?tab=intake&Receipt={log.ReceiptId:D}";
        var drawer = await GetHtmlAsync(client, drawerRoute);
        Assert.Contains($"data-intake-drawer=\"{log.ReceiptId:D}\"", drawer, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public async Task OperationsListsFailedIntakeForAdministratorsWithActionsPostingToLogs()
    {
        var log = new RecordingIntakeLog();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = Configure(baseFactory, log);
        using var client = CreateClient(factory);

        var operations = await GetHtmlAsync(client, "/Operations");
        // Three failure kinds, each read by its own Intake log outcome, each row
        // offering only its own action.
        Assert.Equal(
            [IntakeLogOutcome.AllocationFailed, IntakeLogOutcome.OcrFailed, IntakeLogOutcome.ProcessingFailed],
            log.Filters.Select(filter => filter.Outcome!.Value).ToArray());
        var allocationRow = FailedRow(operations, "allocation");
        Assert.Contains("action=\"/Administration/Logs?handler=RetryIntakeAllocation\"", allocationRow, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=RetryIntakeOcr", allocationRow, StringComparison.Ordinal);
        var ocrRow = FailedRow(operations, "ocr");
        Assert.Contains("action=\"/Administration/Logs?handler=RetryIntakeOcr\"", ocrRow, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=ReevaluateIntake", ocrRow, StringComparison.Ordinal);
        var processingRow = FailedRow(operations, "processing");
        Assert.Contains("action=\"/Administration/Logs?handler=ReevaluateIntake\"", processingRow, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=RetryIntakeAllocation", processingRow, StringComparison.Ordinal);
        Assert.Contains("name=\"returnUrl\" value=\"/Operations\"", operations, StringComparison.Ordinal);

        var form = Regex.Match(ocrRow, "<form[^>]*action=\"/Administration/Logs\\?handler=RetryIntakeOcr\"[^>]*>[\\s\\S]*?</form>").Value;
        using (var response = await client.PostAsync(
            "/Administration/Logs?handler=RetryIntakeOcr",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Input(form, "__RequestVerificationToken"),
                ["receiptId"] = log.ReceiptId.ToString("D"),
                ["expectedVersion"] = Input(form, "expectedVersion"),
                ["operationKey"] = Input(form, "operationKey"),
                ["reason"] = "The provider is back.",
                ["returnUrl"] = "/Operations"
            })))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Operations", response.Headers.Location?.OriginalString);
        }
        Assert.Single(log.OcrRetries);
        Assert.Empty(log.Reevaluations);
    }

    private static string FailedRow(string html, string kind)
    {
        var row = Regex.Match(html, $"<tr[^>]*data-failed-intake-kind=\"{kind}\"[^>]*>[\\s\\S]*?</tr>");
        Assert.True(row.Success, $"The {kind} failure row was not rendered.");
        return row.Value;
    }

    [Theory]
    [InlineData("Engineer")]
    [InlineData("User")]
    public async Task OperationsOmitsFailedIntakeForNonAdministrators(string role)
    {
        var log = new RecordingIntakeLog();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Configure(baseFactory, log);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);

        var operations = await GetHtmlAsync(client, "/Operations");

        Assert.DoesNotContain("data-failed-intake", operations, StringComparison.Ordinal);
        Assert.Null(log.LastFilter);
    }

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

    private static async Task<string> GetHtmlAsync(HttpClient client, string route)
    {
        using var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

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

        public Task<IntakeAllocationResult?> AttemptAutomaticAsync(Guid receiptId, Guid evaluationId, CancellationToken cancellationToken = default) =>
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
