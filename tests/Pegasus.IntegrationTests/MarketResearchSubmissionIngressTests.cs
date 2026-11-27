using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Custody;
using Pegasus.Web.Mcp;
using Pegasus.Web.Presentation;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;
using static Pegasus.IntegrationTests.DesktopSignInTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Market research submitted from Pegasus Desktop (issue 2166; R1238, R1242
/// to R1244, R1309 to R1315): one call files the record, the screenshots,
/// the Advert Evidence Pack and the report under the member of staff's name,
/// writes the one research card and completes a waiting request. Each fact
/// is driven over real HTTP with a token from the real sign-in.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class MarketResearchSubmissionIngressTests
{
    private const string Tool = "pegasus_market_research_submit";
    private const string SubfolderRoot = "case-root/market-research";
    private static readonly string GuideMonth =
        DateTimeOffset.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    private static readonly ActionActor AutomationClient = ActionActor.Automation(ClientId);
    private static readonly object[] NoFiles = [];

    /// <summary>
    /// One act files everything, a replay returns the same submission, and
    /// another payload under the key is refused. The report is a document of
    /// the Case marked as the AI's; the pack and the screenshots are in the
    /// subfolder, read back through it and absent from the Documents list.
    /// The card is retail only and the Engineer's Value is not written.
    /// </summary>
    [Fact]
    public async Task OneActFilesTheRecordTheScreenshotsThePackAndTheReportAndAReplayReturnsTheSameSubmission()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var host = WithDesktopSignIn(factory);
        var caseId = await SeedCaseWithFolderAsync(factory, host);
        using var desktop = CreateConnector(host);
        var (staffId, token) = await SignedInStaffAsync(host, desktop, "desktop-research", StaffRole.Engineer);
        var database = await factory.DatabaseAsync;
        var versionBefore = await GetWorkflowVersionAsync(host, caseId);

        var report = "market valuation evidence report"u8.ToArray();
        var pack = "advert evidence pack"u8.ToArray();
        var first = "first advert screenshot"u8.ToArray();
        var second = "second advert screenshot"u8.ToArray();
        object[] files =
        [
            StagedFile(await StageUploadAsync(desktop, token, first, "mcp:research-u-1"), "advert-1.jpg", "image/jpeg", "Screenshot"),
            StagedFile(await StageUploadAsync(desktop, token, second, "mcp:research-u-2"), "advert-2.jpg", "image/jpeg", "Screenshot"),
            StagedFile(await StageUploadAsync(desktop, token, pack, "mcp:research-u-3"), "evidence-pack.pdf", "application/pdf", "EvidencePack"),
            StagedFile(await StageUploadAsync(desktop, token, report, "mcp:research-u-4"), "market-valuation-evidence.pdf", "application/pdf", "Report")
        ];
        var record = Record(9900, "evidence-pack.pdf", (1, "advert-1.jpg"), (2, "advert-2.jpg"));

        // The Automation Actor files its research through the AI job list, never here.
        var automationToken = await RequestTokenAsync(desktop, AllScopes);
        using (var refused = await PostMcpAsync(
            desktop, automationToken, Submission(1, caseId, "mcp:research-automation", "Accepted", record, files)))
        {
            Assert.Contains(
                "The Automation actor is not authorized for this action.",
                await ReadErrorTextAsync(refused),
                StringComparison.Ordinal);
        }
        Assert.Equal(0, await database.ScalarAsync<int>("SELECT COUNT(*) FROM MarketResearchSubmissions"));

        Guid submissionId;
        Guid screenshotOccurrenceId;
        Guid screenshotVersionId;
        using (var submitted = await PostMcpAsync(
            desktop, token, Submission(2, caseId, "mcp:research-1", "Accepted", record, files)))
        {
            var result = await ReadStructuredContentAsync(submitted);
            submissionId = result.GetProperty("submissionId").GetGuid();
            Assert.Equal(caseId, result.GetProperty("caseId").GetGuid());
            Assert.Equal(staffId.ToString("D"), result.GetProperty("submittedBy").GetString());
            Assert.True(result.GetProperty("submittedAtUtc").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddMinutes(-5));
            Assert.False(result.GetProperty("isReplay").GetBoolean());
            var filed = result.GetProperty("files").EnumerateArray().ToArray();
            Assert.Equal(
                "advert-1.jpg|Screenshot advert-2.jpg|Screenshot evidence-pack.pdf|EvidencePack market-valuation-evidence.pdf|Report",
                string.Join(' ', filed.Select(file =>
                    $"{file.GetProperty("fileName").GetString()}|{file.GetProperty("kind").GetString()}")));
            screenshotOccurrenceId = filed[0].GetProperty("occurrenceId").GetGuid();
            screenshotVersionId = filed[0].GetProperty("versionId").GetGuid();
        }

        // The submission, its files and which advert each screenshot shows.
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM MarketResearchSubmissions
            WHERE Id = '{submissionId:D}' AND CaseId = '{caseId:D}'
              AND OperationKey = N'mcp:research-1' AND SubmittedBy = N'{staffId:D}'
              AND Review = N'Accepted' AND ProposedValue = 9900
              AND GuideMonth = '{GuideMonth}-01' AND ClosedAiJobId IS NULL
            """));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM MarketResearchSubmissions WHERE Id = '{submissionId:D}' AND RecordJson LIKE N'%\"comment\":\"Close match.\"%'"));
        Assert.Equal(
            "Screenshot:1,Screenshot:2,EvidencePack:,Report:",
            await database.ScalarAsync<string>(
                $"""
                SELECT STRING_AGG(CONCAT(Kind, N':', AdvertNumber), N',') WITHIN GROUP (ORDER BY Position)
                FROM MarketResearchSubmissionFiles WHERE SubmissionId = '{submissionId:D}'
                """));

        // The report is the Case's own document, marked as the AI's and filed by the person.
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM DocumentOccurrences occurrence
            JOIN CaseDocuments document ON document.Id = occurrence.DocumentId
            JOIN DocumentVersions version ON version.Id = occurrence.VersionId
            WHERE occurrence.CaseId = '{caseId:D}'
              AND occurrence.SemanticRole = N'{nameof(DocumentSemanticRole.ValuationEvidence)}'
              AND occurrence.Source = N'{nameof(DocumentSource.Automation)}'
              AND document.CustodySubfolder IS NULL
              AND version.FileName = N'market-valuation-evidence.pdf'
              AND version.CreatedBy = N'Staff:{staffId:D}'
            """));
        // The pack and the screenshots are the person's uploads, in the subfolder.
        Assert.Equal(3, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM DocumentOccurrences occurrence
            JOIN CaseDocuments document ON document.Id = occurrence.DocumentId
            WHERE occurrence.CaseId = '{caseId:D}'
              AND occurrence.Source = N'{nameof(DocumentSource.StaffUpload)}'
              AND document.CustodySubfolder = N'market-research'
            """));
        Assert.Equal(SubfolderRoot, await database.ScalarAsync<string>(
            $"SELECT MarketResearchCustodyRemoteId FROM Cases WHERE Id = '{caseId:D}'"));

        // One card for the month: retail only, recorded by the person. No Engineer's Value.
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseValuations
            WHERE WorkId = '{caseId:D}' AND Source = N'{nameof(ValuationSource.AiMarketResearch)}'
              AND RetailValue = 9900 AND TradeValue IS NULL AND Mileage = 42000
              AND GuideMonth = '{GuideMonth}-01' AND RecordedBy = N'{staffId:D}'
            """));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseValuations WHERE WorkId = '{caseId:D}'"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseAssessmentFields WHERE WorkId = '{caseId:D}' AND FieldPath = N'{AssessmentVocabulary.ValueEngineer}'"));

        // The history line names the member of staff, and the Case did not move.
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseWorkflowEvents
            WHERE CaseId = '{caseId:D}'
              AND EventType = N'{MarketResearchSubmissionPolicy.SubmittedEventType}'
              AND ActorKind = N'Staff' AND ActorSubjectId = N'{staffId:D}'
              AND Reason = N'{MarketResearchSubmissionPolicy.HistoryReason}'
            """));
        Assert.Equal(
            MarketResearchSubmissionPolicy.HistoryReason,
            OperatorLabels.HistoryEvent(MarketResearchSubmissionPolicy.SubmittedEventType));
        Assert.Equal(versionBefore, await GetWorkflowVersionAsync(host, caseId));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'{AutomationClientRegistry.DesktopAggregateType}'
              AND EventKind = N'{Tool}' AND AggregateId = N'{caseId:D}'
              AND ActorKind = N'Staff' AND ActorSubjectId = N'{staffId:D}'
              AND Outcome = N'Succeeded' AND CorrelationId = N'mcp:research-1'
            """));

        // The Documents list holds the report and neither the pack nor a screenshot.
        using (var found = await PostMcpAsync(desktop, token, ToolCallPayload(3, "pegasus_case_get", new { caseId })))
        {
            var documents = (await ReadStructuredContentAsync(found)).GetProperty("documents").EnumerateArray()
                .Select(document => document.GetProperty("fileName").GetString())
                .ToArray();
            Assert.Contains("market-valuation-evidence.pdf", documents);
            Assert.DoesNotContain("evidence-pack.pdf", documents);
            Assert.DoesNotContain("advert-1.jpg", documents);
            Assert.DoesNotContain("advert-2.jpg", documents);
        }
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var listed = await scope.ServiceProvider.GetRequiredService<ICaseDocumentQueries>()
                .ListAsync(caseId, CancellationToken.None);
            Assert.DoesNotContain(
                listed.SelectMany(document => document.Occurrences),
                occurrence => occurrence.Id == screenshotOccurrenceId);
        }

        // A screenshot is read back through the subfolder it is kept in.
        using (var read = await GetWithBearerAsync(
            desktop,
            token,
            $"{AutomationMcp.DocumentContentPath}/{screenshotOccurrenceId:D}/versions/{screenshotVersionId:D}?caseId={caseId:D}"))
        {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            Assert.Equal(first, await read.Content.ReadAsByteArrayAsync());
        }

        // The same call again is the first submission, and files nothing more.
        using (var replayed = await PostMcpAsync(
            desktop, token, Submission(4, caseId, "mcp:research-1", "Accepted", record, files)))
        {
            var result = await ReadStructuredContentAsync(replayed);
            Assert.Equal(submissionId, result.GetProperty("submissionId").GetGuid());
            Assert.True(result.GetProperty("isReplay").GetBoolean());
            Assert.Equal(4, result.GetProperty("files").GetArrayLength());
        }
        Assert.Equal(1, await database.ScalarAsync<int>("SELECT COUNT(*) FROM MarketResearchSubmissions"));
        Assert.Equal(4, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM DocumentOccurrences WHERE CaseId = '{caseId:D}' AND OperationKey LIKE N'mcp:research-1:%'"));

        // Other inputs under the key are refused.
        using (var changed = await PostMcpAsync(
            desktop, token, Submission(5, caseId, "mcp:research-1", "NotReviewed", record, files)))
        {
            Assert.Contains(
                "The market research operation key was reused with different inputs.",
                await ReadErrorTextAsync(changed),
                StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Research with no report still writes the card, and it completes the
    /// request waiting on the Case, queued or taken, with the reason the AI
    /// job list shows (R1311, R1312). Research for the same month replaces
    /// the card.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResearchWithNoReportWritesTheCardAndCompletesTheWaitingRequest(bool taken)
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var host = WithDesktopSignIn(factory);
        var caseId = await SeedCaseWithFolderAsync(factory, host);
        using var desktop = CreateConnector(host);
        var (staffId, token) = await SignedInStaffAsync(host, desktop, "desktop-no-report", StaffRole.User);
        var database = await factory.DatabaseAsync;
        Guid jobId;
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var created = await services.GetRequiredService<IAiJobStore>().CreateAsync(
                new(
                    AiJobKind.MarketResearch,
                    AiJobSubjectKind.Case,
                    caseId,
                    "fixture-reference",
                    MarketResearchPolicy.Instruction(DateOnly.ParseExact($"{GuideMonth}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    null,
                    null,
                    ActionActor.Staff(staffId, [StaffRole.User]),
                    "market-research-request",
                    AiJobPolicy.DefaultExpiry),
                CancellationToken.None);
            jobId = created.JobId;
            if (taken)
            {
                await services.GetRequiredService<IWorkAiJob>().TakeAsync(
                    new(created.JobId, created.Version, AutomationClient, "market-research-request-take"),
                    CancellationToken.None);
            }
        }

        var screenshot = "advert screenshot"u8.ToArray();
        object[] files =
        [
            StagedFile(await StageUploadAsync(desktop, token, screenshot, "mcp:no-report-u-1"), "advert-1.jpg", "image/jpeg", "Screenshot")
        ];
        Guid submissionId;
        using (var submitted = await PostMcpAsync(
            desktop,
            token,
            Submission(1, caseId, "mcp:no-report-1", "NotReviewed", Record(8750, null, (1, "advert-1.jpg")), files)))
        {
            submissionId = (await ReadStructuredContentAsync(submitted)).GetProperty("submissionId").GetGuid();
        }

        Assert.Equal(0, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM DocumentOccurrences WHERE CaseId = '{caseId:D}' AND Source = N'{nameof(DocumentSource.Automation)}'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseValuations
            WHERE WorkId = '{caseId:D}' AND Source = N'{nameof(ValuationSource.AiMarketResearch)}'
              AND RetailValue = 8750 AND TradeValue IS NULL
            """));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM AiJobs
            WHERE JobId = '{jobId:D}' AND State = N'{nameof(AiJobState.Completed)}'
              AND ClosureReason = N'{MarketResearchSubmissionPolicy.RequestAnsweredReason}'
              AND ResultKind IS NULL AND ClosedAtUtc IS NOT NULL
            """));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'ai_job' AND AggregateId = N'{jobId:D}'
              AND EventKind = N'ai_job_completed' AND ActorKind = N'Staff' AND ActorSubjectId = N'{staffId:D}'
              AND Reason = N'{MarketResearchSubmissionPolicy.RequestAnsweredReason}'
            """));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM MarketResearchSubmissions WHERE Id = '{submissionId:D}' AND Review = N'NotReviewed' AND ClosedAiJobId = '{jobId:D}'"));
        await using (var scope = host.Services.CreateAsyncScope())
        {
            Assert.Null(await scope.ServiceProvider.GetRequiredService<MarketResearchQueries>()
                .GetPendingAsync(caseId, CancellationToken.None));
        }

        // Research for the same month, with no files at all, replaces the card.
        using (var again = await PostMcpAsync(
            desktop, token, Submission(2, caseId, "mcp:no-report-2", "NotReviewed", Record(9100, null), NoFiles)))
        {
            Assert.Empty((await ReadStructuredContentAsync(again)).GetProperty("files").EnumerateArray());
        }
        Assert.Equal(9100m, await database.ScalarAsync<decimal>(
            $"SELECT RetailValue FROM CaseValuations WHERE WorkId = '{caseId:D}' AND Source = N'{nameof(ValuationSource.AiMarketResearch)}'"));
        Assert.Equal(2, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM MarketResearchSubmissions WHERE CaseId = '{caseId:D}'"));
    }

    /// <summary>
    /// A submission is refused on a closed Case, whatever its disposition,
    /// and allowed in every other state, Report sent included (R1242, R1244).
    /// A Case whose Box folder is not there yet is refused its files in the
    /// Case page's own words.
    /// </summary>
    [Fact]
    public async Task ASubmissionIsRefusedOnEveryClosedCaseAndAllowedOnReportSent()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var host = WithDesktopSignIn(factory);
        var caseId = await SeedAcceptedCaseAsync(host);
        using var desktop = CreateConnector(host);
        var (_, token) = await SignedInStaffAsync(host, desktop, "desktop-states", StaffRole.User);
        var database = await factory.DatabaseAsync;
        CaseLifecycleState[] closed =
        [
            CaseLifecycleState.PrincipalCancelled,
            CaseLifecycleState.CollisionEngineersRejected,
            CaseLifecycleState.CreatedInError,
            CaseLifecycleState.SourceEmailUnlinked
        ];
        for (var index = 0; index < closed.Length; index++)
        {
            await database.ExecuteAsync(
                $"UPDATE CaseWorkflows SET State = N'{closed[index]}' WHERE CaseId = '{caseId:D}'");
            using var refused = await PostMcpAsync(
                desktop, token, Submission(10 + index, caseId, $"mcp:closed-{index}", "NotReviewed", Record(9000, null), NoFiles));
            // The closed-Case refusal every write gives; its quotation marks are escapes in the answer.
            Assert.Contains(
                "is application read-only until an authorized reopen.",
                await ReadErrorTextAsync(refused),
                StringComparison.Ordinal);
        }
        Assert.Equal(0, await database.ScalarAsync<int>("SELECT COUNT(*) FROM MarketResearchSubmissions"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseValuations WHERE WorkId = '{caseId:D}'"));

        await database.ExecuteAsync(
            $"UPDATE CaseWorkflows SET State = N'{nameof(CaseLifecycleState.ReportSent)}' WHERE CaseId = '{caseId:D}'");

        // The Case has no Box folder yet: research with a file is refused, in the Case page's words.
        await database.ExecuteAsync(
            $"UPDATE Cases SET CustodyRootRemoteId = NULL, CustodyState = N'pending' WHERE Id = '{caseId:D}'");
        object[] files =
        [
            StagedFile(await StageUploadAsync(desktop, token, "advert screenshot"u8.ToArray(), "mcp:states-u-1"), "advert-1.jpg", "image/jpeg", "Screenshot")
        ];
        using (var noFolder = await PostMcpAsync(
            desktop,
            token,
            Submission(20, caseId, "mcp:report-sent-files", "NotReviewed", Record(9000, null, (1, "advert-1.jpg")), files)))
        {
            Assert.Contains(
                OperatorLabels.CustodyFolderState(CaseCustodyState.Pending),
                await ReadErrorTextAsync(noFolder),
                StringComparison.Ordinal);
        }
        Assert.Equal(0, await database.ScalarAsync<int>("SELECT COUNT(*) FROM MarketResearchSubmissions"));

        using (var allowed = await PostMcpAsync(
            desktop, token, Submission(21, caseId, "mcp:report-sent", "NotReviewed", Record(9000, null), NoFiles)))
        {
            Assert.False((await ReadStructuredContentAsync(allowed)).GetProperty("isReplay").GetBoolean());
        }
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM MarketResearchSubmissions WHERE CaseId = '{caseId:D}'"));
        Assert.Equal(nameof(CaseLifecycleState.ReportSent), await database.ScalarAsync<string>(
            $"SELECT State FROM CaseWorkflows WHERE CaseId = '{caseId:D}'"));
    }

    /// <summary>
    /// A file that cannot be written fails the whole act: nothing is recorded,
    /// every file already written is removed from custody, and the same call
    /// then files it all.
    /// </summary>
    [Fact]
    public async Task AFailureMidwayRecordsNothingAndRemovesTheFilesAlreadyWritten()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var host = WithDesktopSignIn(factory).WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDocumentContentStore>();
                services.AddSingleton(provider =>
                    new FailingDocumentContentStore(provider.GetRequiredService<LocalDocumentContentStore>()));
                services.AddSingleton<IDocumentContentStore>(provider =>
                    provider.GetRequiredService<FailingDocumentContentStore>());
            }));
        var caseId = await SeedCaseWithFolderAsync(factory, host);
        using var desktop = CreateConnector(host);
        var (staffId, token) = await SignedInStaffAsync(host, desktop, "desktop-failure", StaffRole.Engineer);
        var database = await factory.DatabaseAsync;
        var store = host.Services.GetRequiredService<FailingDocumentContentStore>();
        object[] files =
        [
            StagedFile(await StageUploadAsync(desktop, token, "report"u8.ToArray(), "mcp:failure-u-1"), "report.pdf", "application/pdf", "Report"),
            StagedFile(await StageUploadAsync(desktop, token, "pack"u8.ToArray(), "mcp:failure-u-2"), "pack.pdf", "application/pdf", "EvidencePack"),
            StagedFile(await StageUploadAsync(desktop, token, "first"u8.ToArray(), "mcp:failure-u-3"), "advert-1.jpg", "image/jpeg", "Screenshot"),
            StagedFile(await StageUploadAsync(desktop, token, "second"u8.ToArray(), "mcp:failure-u-4"), "advert-2.jpg", "image/jpeg", "Screenshot")
        ];
        var record = Record(9900, "pack.pdf", (1, "advert-1.jpg"), (2, "advert-2.jpg"));

        store.FailFileName = "advert-2.jpg";
        using (var failed = await PostMcpAsync(
            desktop, token, Submission(1, caseId, "mcp:failure-1", "Accepted", record, files)))
        {
            Assert.Contains("The automation action failed.", await ReadErrorTextAsync(failed), StringComparison.Ordinal);
        }

        Assert.Equal(3, store.Stored.Count);
        Assert.Equal(
            store.Stored.Order().ToArray(),
            store.Deleted.Order().ToArray());
        Assert.Equal(0, await database.ScalarAsync<int>("SELECT COUNT(*) FROM MarketResearchSubmissions"));
        Assert.Equal(0, await database.ScalarAsync<int>("SELECT COUNT(*) FROM MarketResearchSubmissionFiles"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM DocumentOccurrences WHERE CaseId = '{caseId:D}' AND OperationKey LIKE N'mcp:failure-1:%'"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseValuations WHERE WorkId = '{caseId:D}'"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseWorkflowEvents WHERE CaseId = '{caseId:D}' AND EventType = N'{MarketResearchSubmissionPolicy.SubmittedEventType}'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE EventKind = N'{Tool}' AND ActorKind = N'Staff' AND ActorSubjectId = N'{staffId:D}' AND Outcome = N'Failed'
            """));

        store.FailFileName = null;
        using (var retried = await PostMcpAsync(
            desktop, token, Submission(2, caseId, "mcp:failure-1", "Accepted", record, files)))
        {
            var result = await ReadStructuredContentAsync(retried);
            Assert.False(result.GetProperty("isReplay").GetBoolean());
            Assert.Equal(4, result.GetProperty("files").GetArrayLength());
        }
        Assert.Equal(4, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM DocumentOccurrences WHERE CaseId = '{caseId:D}' AND OperationKey LIKE N'mcp:failure-1:%'"));
    }

    // A Case whose Box folder is confirmed, as the Worker leaves it once it is made.
    private static async Task<Guid> SeedCaseWithFolderAsync(
        IntakeWebApplicationFactory factory,
        WebApplicationFactory<Program> host)
    {
        var caseId = await SeedAcceptedCaseAsync(host);
        await (await factory.DatabaseAsync).ExecuteAsync(
            $"UPDATE Cases SET CustodyRootRemoteId = N'case-root', CustodyState = N'confirmed' WHERE Id = '{caseId:D}'");
        return caseId;
    }

    private static object StagedFile(Guid uploadId, string fileName, string mediaType, string kind) =>
        new { uploadId, fileName, mediaType, kind };

    // The research record as Pegasus Desktop writes it, schema version 1.
    private static object Record(
        int proposed,
        string? evidencePack,
        params (int Number, string Screenshot)[] adverts) =>
        new
        {
            schemaVersion = 1,
            app = new { name = "Pegasus Desktop", version = "0.13.0" },
            submittedAt = "2031-05-06T10:30:00.000Z",
            registration = "DK12TOP",
            vehicle = "Ford Focus 1.0 Zetec",
            mileage = 42000,
            mileageBasis = "odometer",
            guide = new { amount = 9500 },
            search = new
            {
                at = "2031-05-06T10:00:00.000Z",
                sites = new[] { new { label = "Auto Trader", status = "done", count = 14 } },
                filters = new { make = "Ford", model = "Focus", radius = 50 }
            },
            adverts = adverts.Select(advert => new
            {
                number = advert.Number,
                origin = "search",
                source = "Auto Trader",
                url = $"https://adverts.example/{advert.Number}",
                vehicle = "Ford Focus 1.0 Zetec",
                year = 2019,
                mileage = 40000,
                price = 9800 + (advert.Number * 100),
                seller = "Trade, Leeds",
                comment = "Close match.",
                inAverage = true,
                matchScore = 90,
                capturedAt = "2031-05-06T10:05:00.000Z",
                screenshots = new[] { advert.Screenshot },
                sha256 = (string?)null
            }).ToArray(),
            value = new
            {
                count = adverts.Length,
                mean = (int?)proposed,
                median = (int?)proposed,
                min = (int?)proposed,
                max = (int?)proposed,
                rounded = (int?)proposed,
                proposed = (int?)proposed,
                basis = "average",
                overrideReason = (string?)null
            },
            evidencePack
        };

    private static string Submission(
        int id,
        Guid caseId,
        string operationKey,
        string review,
        object record,
        object[] files) =>
        ToolCallPayload(id, Tool, new
        {
            caseId,
            operationKey,
            review,
            guideMonth = GuideMonth,
            record,
            files
        });

    private static async Task<HttpResponseMessage> GetWithBearerAsync(HttpClient client, string token, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    /// <summary>
    /// The local content store, failing the write of one named file once the
    /// submission's other three files are in custody, and remembering what
    /// was written and what was taken back.
    /// </summary>
    private sealed class FailingDocumentContentStore(IDocumentContentStore inner) : IDocumentContentStore
    {
        private const int OtherFiles = 3;

        public string? FailFileName { get; set; }

        public ConcurrentBag<Guid> Stored { get; } = [];

        public ConcurrentBag<Guid> Deleted { get; } = [];

        public async Task<DocumentContentWriteResult> StoreVersionAsync(
            ManagedDocumentContentAddress address,
            ReadOnlyMemory<byte> content,
            string expectedSha256,
            CancellationToken cancellationToken)
        {
            if (string.Equals(address.FileName, FailFileName, StringComparison.Ordinal))
            {
                for (var waited = 0; Stored.Count < OtherFiles && waited < 500; waited++)
                {
                    await Task.Delay(10, cancellationToken);
                }

                throw new IOException("The document store is unavailable.");
            }

            var written = await inner.StoreVersionAsync(address, content, expectedSha256, cancellationToken);
            Stored.Add(address.VersionId);
            return written;
        }

        public Task<DocumentContentWriteResult> StoreVersionAsync(
            ManagedDocumentContentAddress address,
            Stream content,
            long contentLength,
            string expectedSha256,
            CancellationToken cancellationToken) =>
            inner.StoreVersionAsync(address, content, contentLength, expectedSha256, cancellationToken);

        public Task StoreAsync(
            Guid caseId,
            string caseReference,
            Guid versionId,
            ReadOnlyMemory<byte> content,
            string expectedSha256,
            CancellationToken cancellationToken) =>
            inner.StoreAsync(caseId, caseReference, versionId, content, expectedSha256, cancellationToken);

        public Task<Stream> OpenReadAsync(
            Guid caseId,
            string caseReference,
            Guid versionId,
            string expectedSha256,
            long expectedLength,
            CancellationToken cancellationToken) =>
            inner.OpenReadAsync(caseId, caseReference, versionId, expectedSha256, expectedLength, cancellationToken);

        public Task<Stream> OpenReadVersionAsync(
            ManagedDocumentContentAddress address,
            string expectedSha256,
            long expectedLength,
            CancellationToken cancellationToken) =>
            inner.OpenReadVersionAsync(address, expectedSha256, expectedLength, cancellationToken);

        public async Task DeleteAsync(
            Guid caseId,
            string caseReference,
            Guid versionId,
            CancellationToken cancellationToken)
        {
            await inner.DeleteAsync(caseId, caseReference, versionId, cancellationToken);
            Deleted.Add(versionId);
        }
    }
}
