using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;
using Pegasus.Web.Authentication;
using Pegasus.Web.Mcp;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Caller-equivalent evidence for the AI job ledger (ADR-0035): the store
/// on real LocalDB persistence and the <c>automation.jobs</c> tools over
/// real HTTP against the gated /mcp surface, with the same attribution
/// assertions as the other Automation Actor tranches.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class AutomationAiJobIngressTests
{
    [Fact]
    public async Task JobCursorPagesConcatenateWithoutDuplicatesAndBindTheFilter()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, JobsScope);
        for (var index = 0; index < 3; index++)
        {
            using var create = await PostMcpAsync(client, token, ToolCallPayload(
                70 + index, "pegasus_ai_job_create", new
                {
                    kind = "UnidentifiedQueuePass",
                    instruction = $"Cursor page pass {index}",
                    operationKey = $"mcp:cursor-job-{index}"
                }));
            _ = await ReadStructuredContentAsync(create);
        }

        string continuation;
        Guid[] firstIds;
        using (var first = await PostMcpAsync(client, token, ToolCallPayload(
            74, "pegasus_ai_job_list", new { kind = "UnidentifiedQueuePass", pageSize = 2 })))
        {
            var result = await ReadStructuredContentAsync(first);
            firstIds = result.GetProperty("jobs").EnumerateArray()
                .Select(item => item.GetProperty("jobId").GetGuid()).ToArray();
            Assert.Equal(2, firstIds.Length);
            continuation = result.GetProperty("continuation").GetString()!;
        }
        using (var second = await PostMcpAsync(client, token, ToolCallPayload(
            75, "pegasus_ai_job_list", new { kind = "UnidentifiedQueuePass", pageSize = 2, continuation })))
        {
            var result = await ReadStructuredContentAsync(second);
            var secondIds = result.GetProperty("jobs").EnumerateArray()
                .Select(item => item.GetProperty("jobId").GetGuid()).ToArray();
            Assert.Single(secondIds);
            Assert.Empty(firstIds.Intersect(secondIds));
        }
        using (var foreignFilter = await PostMcpAsync(client, token, ToolCallPayload(
            76, "pegasus_ai_job_list", new { kind = "Estimate", continuation })))
        {
            using var result = await ReadJsonRpcAsync(foreignFilter);
            Assert.True(result.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
            Assert.Contains("The cursor is invalid or no longer applies to this query.",
                result.RootElement.ToString(), StringComparison.Ordinal);
        }
    }

    private const string JobsScope = "automation.jobs";
    private static readonly ActionActor Staff =
        ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);
    private static readonly ActionActor Client = ActionActor.Automation(ClientId);

    [Fact]
    public async Task TheStoreReplaysCreationGuardsVersionsAndExpiresLeasesWithHistory()
    {
        var clock = new CaseDataCompletenessPersistenceTests.MutableTimeProvider(SeedUtcNow);
        using var factory = new IntakeWebApplicationFactory(clock);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IAiJobStore>();
        var queries = scope.ServiceProvider.GetRequiredService<IAiJobQueries>();
        var work = scope.ServiceProvider.GetRequiredService<IWorkAiJob>();

        var job = new NewAiJob(
            AiJobKind.UnidentifiedQueuePass,
            AiJobSubjectKind.Queue,
            null,
            AiJobPolicy.QueueSubjectReference,
            "Pass the queue.",
            null,
            null,
            Staff,
            "create-op",
            AiJobPolicy.DefaultExpiry);
        var created = await store.CreateAsync(job, CancellationToken.None);
        Assert.Equal(AiJobState.Queued, created.State);
        Assert.Equal(0, created.Version);
        var replayed = await store.CreateAsync(job, CancellationToken.None);
        Assert.Equal(created.JobId, replayed.JobId);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.CreateAsync(job with { Instruction = "Different instruction." }, CancellationToken.None));

        var taken = await work.TakeAsync(new(created.JobId, 0, Client, "take-1"), CancellationToken.None);
        Assert.Equal(AiJobState.Taken, taken.State);
        Assert.Equal(ClientId, taken.TakenBy);
        Assert.Equal(SeedUtcNow + AiJobPolicy.LeaseDuration, taken.LeaseExpiresAtUtc);
        Assert.Equal(1, taken.Version);

        // A stale version is a refused, recorded outcome; the same key replays.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            work.ReportProgressAsync(new(created.JobId, 0, Client, "progress-stale", "x"), CancellationToken.None));
        var replay = await work.TakeAsync(new(created.JobId, 0, Client, "take-1"), CancellationToken.None);
        Assert.Equal(1, replay.Version);

        // A held job is renewed through progress, never taken again.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            work.TakeAsync(new(created.JobId, 1, Client, "take-again"), CancellationToken.None));

        // Another client cannot progress a job this client holds.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            work.ReportProgressAsync(
                new(created.JobId, 1, ActionActor.Automation("other-client"), "progress-other", "x"),
                CancellationToken.None));

        // The lease lapses: the job reads as Queued and may be taken again,
        // and the lapsed claim is recorded rather than erased.
        clock.Advance(AiJobPolicy.LeaseDuration + TimeSpan.FromSeconds(1));
        var lapsed = await store.GetAsync(created.JobId, CancellationToken.None);
        Assert.Equal(AiJobState.Queued, lapsed!.State);
        var open = await queries.ListOpenAsync(CancellationToken.None);
        Assert.Equal(AiJobState.Queued, Assert.Single(open, item => item.JobId == created.JobId).State);
        var retaken = await work.TakeAsync(
            new(created.JobId, lapsed.Version, ActionActor.Automation("other-client"), "take-2"),
            CancellationToken.None);
        Assert.Equal(AiJobState.Taken, retaken.State);
        Assert.Equal("other-client", retaken.TakenBy);

        var counts = await queries.GetCountsAsync(CancellationToken.None);
        Assert.Equal(new AiJobCounts(1, 0), counts);

        var history = await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'ai_job'
              AND AggregateId = N'{created.JobId:D}'
              AND EventKind IN (N'ai_job_created', N'ai_job_taken', N'ai_job_expired')
            """);
        Assert.Equal(4, history);
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'ai_job'
              AND AggregateId = N'{created.JobId:D}'
              AND EventKind = N'ai_job_expired'
              AND Reason LIKE N'%{ClientId}%'
            """));
    }

    [Fact]
    public async Task JobToolsEnforceTheJobsScopeAndAppearInTheInventory()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();

        var metadata = await client.GetStringAsync("/.well-known/oauth-protected-resource/mcp");
        Assert.Contains(JobsScope, metadata, StringComparison.Ordinal);

        var casesOnlyToken = await RequestTokenAsync(client, "automation.cases");
        using var response = await PostMcpAsync(
            client,
            casesOnlyToken,
            ToolCallPayload(1, "pegasus_ai_job_list", new { }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonRpcAsync(response);
        Assert.Contains(JobsScope, document.RootElement.ToString(), StringComparison.Ordinal);

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'automation_scope_denied'
              AND Outcome = N'Denied'
              AND SubjectId = N'pegasus-automation'
            """));
    }

    [Fact]
    public async Task MarketResearchCompletionEnforcesTheJobsScope()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();
        var casesOnlyToken = await RequestTokenAsync(client, "automation.cases");

        using var response = await PostMcpAsync(
            client,
            casesOnlyToken,
            MarketResearchCompletionPayload(
                2,
                Guid.NewGuid(),
                0,
                Guid.NewGuid(),
                0,
                "not-a-lease",
                "mcp:market-research-scope-denied"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonRpcAsync(response);
        Assert.True(document.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Contains(JobsScope, document.RootElement.ToString(), StringComparison.Ordinal);

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'automation_scope_denied'
              AND Outcome = N'Denied'
              AND SubjectId = N'pegasus-automation'
            """));
    }

    [Fact]
    public async Task AClientCreatesListsTakesProgressesAndCompletesAQueuePassOverHttp()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, JobsScope);

        // Only the queue pass may be started by the Actor (EPIC-011 D5).
        using (var refused = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                1,
                "pegasus_ai_job_create",
                new { kind = "Estimate", instruction = "Draft it.", operationKey = "mcp:create-estimate" })))
        {
            using var document = await ReadJsonRpcAsync(refused);
            Assert.Contains(
                "UnidentifiedQueuePass",
                document.RootElement.ToString(),
                StringComparison.Ordinal);
        }

        Guid jobId;
        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                2,
                "pegasus_ai_job_create",
                new
                {
                    kind = "UnidentifiedQueuePass",
                    instruction = "Propose a destination for every open item.",
                    operationKey = "mcp:create-pass"
                })))
        {
            var created = await ReadStructuredContentAsync(response);
            Assert.Equal("Queued", created.GetProperty("state").GetString());
            Assert.Equal("UnidentifiedQueuePass", created.GetProperty("kind").GetString());
            Assert.Equal(ClientId, created.GetProperty("createdBy").GetString());
            jobId = created.GetProperty("jobId").GetGuid();
        }

        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(3, "pegasus_ai_job_list", new { kind = "UnidentifiedQueuePass" })))
        {
            var list = await ReadStructuredContentAsync(response);
            var listed = Assert.Single(
                list.GetProperty("jobs").EnumerateArray(),
                item => item.GetProperty("jobId").GetGuid() == jobId);
            Assert.Equal("Queued", listed.GetProperty("state").GetString());
        }

        long version;
        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                4,
                "pegasus_ai_job_take",
                new { jobId, expectedVersion = 0, operationKey = "mcp:take-1" })))
        {
            var taken = await ReadStructuredContentAsync(response);
            Assert.Equal("Taken", taken.GetProperty("state").GetString());
            Assert.Equal(ClientId, taken.GetProperty("takenBy").GetString());
            Assert.True(taken.GetProperty("leaseExpiresAtUtc").GetDateTimeOffset() > DateTimeOffset.UtcNow);
            version = taken.GetProperty("version").GetInt64();
        }

        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                5,
                "pegasus_ai_job_progress",
                new { jobId, expectedVersion = version, progressNote = "Two of five examined.", operationKey = "mcp:progress-1" })))
        {
            var progressed = await ReadStructuredContentAsync(response);
            Assert.Equal("Two of five examined.", progressed.GetProperty("progressNote").GetString());
            version = progressed.GetProperty("version").GetInt64();
        }

        // A result of the wrong kind for the job is refused.
        using (var refused = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                6,
                "pegasus_ai_job_complete",
                new
                {
                    jobId,
                    expectedVersion = version,
                    resultKind = "DraftReply",
                    resultText = "Wrong kind.",
                    operationKey = "mcp:complete-wrong"
                })))
        {
            using var document = await ReadJsonRpcAsync(refused);
            Assert.Contains("ProposedResolution", document.RootElement.ToString(), StringComparison.Ordinal);
        }

        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                7,
                "pegasus_ai_job_complete",
                new
                {
                    jobId,
                    expectedVersion = version,
                    resultKind = "ProposedResolution",
                    resultText = "U17: add to CE-QDOS-31-00001 (registration match).",
                    operationKey = "mcp:complete-1"
                })))
        {
            var ready = await ReadStructuredContentAsync(response);
            Assert.Equal("DraftReady", ready.GetProperty("state").GetString());
            Assert.Equal("ProposedResolution", ready.GetProperty("resultKind").GetString());
            version = ready.GetProperty("version").GetInt64();
        }

        // Draft ready waits for staff: the client can no longer release it.
        using (var refused = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                8,
                "pegasus_ai_job_release",
                new { jobId, expectedVersion = version, operationKey = "mcp:release-1" })))
        {
            using var document = await ReadJsonRpcAsync(refused);
            Assert.Contains("cannot move from DraftReady", document.RootElement.ToString(), StringComparison.Ordinal);
        }

        // Nothing was applied to any record: the ledger row is the only
        // business effect, and every step is attributed Automation history.
        Assert.Equal(4, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'ai_job'
              AND AggregateId = N'{jobId:D}'
              AND ActorKind = N'Automation'
              AND ActorSubjectId = N'{ClientId}'
              AND EventKind IN (N'ai_job_created', N'ai_job_taken', N'ai_job_progress', N'ai_job_draft_ready')
            """));
        Assert.Equal(2, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'automation_mcp'
              AND AggregateId = N'{jobId:D}'
              AND Outcome = N'Succeeded'
              AND EventKind IN (N'pegasus_ai_job_take', N'pegasus_ai_job_complete')
            """));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'automation_mcp'
              AND EventKind = N'pegasus_ai_job_create'
              AND Outcome = N'Succeeded'
            """));
        Assert.Equal(0, await factory.Database.ScalarAsync<int>("SELECT COUNT(*) FROM UnidentifiedItems"));
    }

    [Fact]
    public async Task TheAdministratorSwitchRefusesClaimsAndProgressButNotFinishing()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, JobsScope);

        Guid queued;
        Guid held;
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            var create = scope.ServiceProvider.GetRequiredService<ICreateAiJob>();
            queued = (await create.ExecuteAsync(
                new(AiJobKind.UnidentifiedQueuePass, null, null, "Pass one.", null, Staff, "seed-1"),
                CancellationToken.None)).JobId;
            held = (await create.ExecuteAsync(
                new(AiJobKind.UnidentifiedQueuePass, null, null, "Pass two.", null, Staff, "seed-2"),
                CancellationToken.None)).JobId;
            await scope.ServiceProvider.GetRequiredService<IWorkAiJob>()
                .TakeAsync(new(held, 0, Client, "seed-take"), CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<ISendToAiControl>()
                .SetEnabledAsync(false, Staff, "Integration-test stop", "seed-stop", CancellationToken.None);
        }

        using (var refused = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(1, "pegasus_ai_job_take", new { jobId = queued, expectedVersion = 0, operationKey = "mcp:take-off" })))
        {
            using var document = await ReadJsonRpcAsync(refused);
            Assert.Contains("disabled by an Administrator", document.RootElement.ToString(), StringComparison.Ordinal);
        }

        using (var refused = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                2,
                "pegasus_ai_job_progress",
                new { jobId = held, expectedVersion = 1, progressNote = "Still going.", operationKey = "mcp:progress-off" })))
        {
            using var document = await ReadJsonRpcAsync(refused);
            Assert.Contains("disabled by an Administrator", document.RootElement.ToString(), StringComparison.Ordinal);
        }

        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                3,
                "pegasus_ai_job_fail",
                new { jobId = held, expectedVersion = 1, reason = "Stopped by the Administrator.", operationKey = "mcp:fail-off" })))
        {
            var failed = await ReadStructuredContentAsync(response);
            Assert.Equal("Failed", failed.GetProperty("state").GetString());
        }

        // Every refused claim is recorded against the Automation actor and
        // the queued job is untouched.
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'automation_mcp'
              AND EventKind = N'pegasus_ai_job_take'
              AND AggregateId = N'{queued:D}'
              AND Outcome = N'Failed'
            """));
        Assert.Equal("Queued", await factory.Database.ScalarAsync<string>(
            $"SELECT State FROM AiJobs WHERE JobId = '{queued:D}'"));
    }

    [Fact]
    public async Task MarketResearchCompletesOverHttpWithCaseLeaseDocumentValuationAndActorHistory()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        AiJobRecord taken;
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var created = await services.GetRequiredService<IAiJobStore>().CreateAsync(
                new(
                    AiJobKind.MarketResearch,
                    AiJobSubjectKind.Case,
                    caseId,
                    "fixture-reference",
                    "Research comparable vehicles.",
                    null,
                    null,
                    Staff,
                    "market-research-seed",
                    AiJobPolicy.DefaultExpiry),
                CancellationToken.None);
            taken = await services.GetRequiredService<IWorkAiJob>().TakeAsync(
                new(created.JobId, created.Version, Client, "market-research-take"),
                CancellationToken.None);
        }

        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, $"{JobsScope} automation.cases");
        var lease = await BeginEditAsync(client, token, caseId, 0, rpcId: 20);
        var arguments = new
        {
            jobId = taken.JobId,
            expectedJobVersion = taken.Version,
            caseId,
            expectedCaseVersion = lease.CaseVersion,
            editLeaseToken = lease.LeaseToken,
            operationKey = "mcp:market-research-complete",
            fileName = "market-research.pdf",
            mediaType = "application/pdf",
            contentBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
            recordedDate = "2031-05-06",
            recordedTime = "10:30",
            mileage = 42000,
            retailValue = 12000m,
            tradeValue = 10000m
        };

        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(21, "pegasus_ai_job_complete_market_research", arguments)))
        {
            var result = await ReadStructuredContentAsync(response);
            Assert.Equal("DraftReady", result.GetProperty("job").GetProperty("state").GetString());
            Assert.False(result.GetProperty("isReplay").GetBoolean());
        }
        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(22, "pegasus_ai_job_complete_market_research", arguments)))
        {
            var result = await ReadStructuredContentAsync(response);
            Assert.True(result.GetProperty("isReplay").GetBoolean());
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM DocumentOccurrences WHERE CaseId = '{caseId:D}' AND Source = N'Automation'"));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseValuations WHERE CaseId = '{caseId:D}' AND Source = N'AiMarketResearch'"));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM ActionHistory WHERE AggregateType = N'ai_job' AND AggregateId = N'{taken.JobId:D}' AND EventKind = N'ai_job_draft_ready' AND ActorKind = N'Automation' AND ActorSubjectId = N'{ClientId}'"));
    }

    [Fact]
    public async Task MarketResearchCompletionSucceedsWhileAutomationIsSwitchedOff()
    {
        // Finishing a claimed job is never blocked by the Administrator
        // switch (only new claims and progress are), matching
        // TheAdministratorSwitchRefusesClaimsAndProgressButNotFinishing.
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        AiJobRecord taken;
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var created = await services.GetRequiredService<IAiJobStore>().CreateAsync(
                new(
                    AiJobKind.MarketResearch,
                    AiJobSubjectKind.Case,
                    caseId,
                    "fixture-reference",
                    "Research comparable vehicles.",
                    null,
                    null,
                    Staff,
                    "market-research-switch-off-seed",
                    AiJobPolicy.DefaultExpiry),
                CancellationToken.None);
            taken = await services.GetRequiredService<IWorkAiJob>().TakeAsync(
                new(created.JobId, created.Version, Client, "market-research-switch-off-take"),
                CancellationToken.None);
            await services.GetRequiredService<ISendToAiControl>().SetEnabledAsync(
                false, Staff, "Integration-test stop", "market-research-switch-off-stop", CancellationToken.None);
        }

        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, $"{JobsScope} automation.cases");
        var lease = await BeginEditAsync(client, token, caseId, 0, rpcId: 26);
        var arguments = new
        {
            jobId = taken.JobId,
            expectedJobVersion = taken.Version,
            caseId,
            expectedCaseVersion = lease.CaseVersion,
            editLeaseToken = lease.LeaseToken,
            operationKey = "mcp:market-research-switch-off-complete",
            fileName = "market-research.pdf",
            mediaType = "application/pdf",
            contentBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
            recordedDate = "2031-05-06",
            recordedTime = "10:30",
            mileage = 42000,
            retailValue = 12000m,
            tradeValue = 10000m
        };

        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(27, "pegasus_ai_job_complete_market_research", arguments)))
        {
            var result = await ReadStructuredContentAsync(response);
            Assert.Equal("DraftReady", result.GetProperty("job").GetProperty("state").GetString());
            Assert.False(result.GetProperty("isReplay").GetBoolean());
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM DocumentOccurrences WHERE CaseId = '{caseId:D}' AND Source = N'Automation'"));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseValuations WHERE CaseId = '{caseId:D}' AND Source = N'AiMarketResearch'"));
    }

    [Fact]
    public async Task MarketResearchCompletionReplaySurvivesStaffConfirmation()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        AiJobRecord taken;
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var created = await services.GetRequiredService<IAiJobStore>().CreateAsync(
                new(
                    AiJobKind.MarketResearch,
                    AiJobSubjectKind.Case,
                    caseId,
                    "fixture-reference",
                    "Research comparable vehicles.",
                    null,
                    null,
                    Staff,
                    "market-research-confirmed-replay-seed",
                    AiJobPolicy.DefaultExpiry),
                CancellationToken.None);
            taken = await services.GetRequiredService<IWorkAiJob>().TakeAsync(
                new(created.JobId, created.Version, Client, "market-research-confirmed-replay-take"),
                CancellationToken.None);
        }

        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, $"{JobsScope} automation.cases");
        var lease = await BeginEditAsync(client, token, caseId, 0, rpcId: 23);
        var arguments = new
        {
            jobId = taken.JobId,
            expectedJobVersion = taken.Version,
            caseId,
            expectedCaseVersion = lease.CaseVersion,
            editLeaseToken = lease.LeaseToken,
            operationKey = "mcp:market-research-confirmed-replay-complete",
            fileName = "market-research.pdf",
            mediaType = "application/pdf",
            contentBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
            recordedDate = "2031-05-06",
            recordedTime = "10:30",
            mileage = 42000,
            retailValue = 12000m,
            tradeValue = 10000m
        };

        Guid occurrenceId;
        Guid versionId;
        Guid valuationId;
        long draftReadyVersion;
        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(24, "pegasus_ai_job_complete_market_research", arguments)))
        {
            var result = await ReadStructuredContentAsync(response);
            Assert.False(result.GetProperty("isReplay").GetBoolean());
            occurrenceId = result.GetProperty("documentOccurrenceId").GetGuid();
            versionId = result.GetProperty("documentVersionId").GetGuid();
            valuationId = result.GetProperty("valuationId").GetGuid();
            draftReadyVersion = result.GetProperty("job").GetProperty("version").GetInt64();
        }

        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IConfirmAiJob>().ExecuteAsync(
                new(taken.JobId, draftReadyVersion, Staff, "market-research-confirm"),
                CancellationToken.None);
        }

        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(25, "pegasus_ai_job_complete_market_research", arguments)))
        {
            var result = await ReadStructuredContentAsync(response);
            Assert.True(result.GetProperty("isReplay").GetBoolean());
            Assert.Equal("Completed", result.GetProperty("job").GetProperty("state").GetString());
            Assert.Equal(occurrenceId, result.GetProperty("documentOccurrenceId").GetGuid());
            Assert.Equal(versionId, result.GetProperty("documentVersionId").GetGuid());
            Assert.Equal(valuationId, result.GetProperty("valuationId").GetGuid());
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM DocumentOccurrences WHERE CaseId = '{caseId:D}' AND Source = N'Automation'"));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseValuations WHERE CaseId = '{caseId:D}' AND Source = N'AiMarketResearch'"));
    }

    [Fact]
    public async Task MarketResearchCompletionRefusesAMissingCaseLeaseWithoutChangingTheJob()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        AiJobRecord taken;
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var created = await services.GetRequiredService<IAiJobStore>().CreateAsync(
                new(
                    AiJobKind.MarketResearch,
                    AiJobSubjectKind.Case,
                    caseId,
                    "fixture-reference",
                    "Research comparable vehicles.",
                    null,
                    null,
                    Staff,
                    "market-research-missing-lease-seed",
                    AiJobPolicy.DefaultExpiry),
                CancellationToken.None);
            taken = await services.GetRequiredService<IWorkAiJob>().TakeAsync(
                new(created.JobId, created.Version, Client, "market-research-missing-lease-take"),
                CancellationToken.None);
        }

        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, $"{JobsScope} automation.cases");
        using var response = await PostMcpAsync(
            client,
            token,
            MarketResearchCompletionPayload(
                23,
                taken.JobId,
                taken.Version,
                caseId,
                0,
                "not-a-lease",
                "mcp:market-research-missing-lease"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonRpcAsync(response);
        Assert.True(document.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Contains(
            "no active edit authority",
            document.RootElement.ToString(),
            StringComparison.OrdinalIgnoreCase);

        Assert.Equal("Taken", await factory.Database.ScalarAsync<string>(
            $"SELECT State FROM AiJobs WHERE JobId = '{taken.JobId:D}'"));
        Assert.Equal(0, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM DocumentOccurrences WHERE CaseId = '{caseId:D}' AND Source = N'Automation'"));
        Assert.Equal(0, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseValuations WHERE CaseId = '{caseId:D}' AND Source = N'AiMarketResearch'"));
    }

    private static string MarketResearchCompletionPayload(
        int id,
        Guid jobId,
        long expectedJobVersion,
        Guid caseId,
        long expectedCaseVersion,
        string editLeaseToken,
        string operationKey) =>
        ToolCallPayload(
            id,
            "pegasus_ai_job_complete_market_research",
            new
            {
                jobId,
                expectedJobVersion,
                caseId,
                expectedCaseVersion,
                editLeaseToken,
                operationKey,
                fileName = "market-research.pdf",
                mediaType = "application/pdf",
                contentBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
                recordedDate = "2031-05-06",
                recordedTime = "10:30",
                mileage = 42000,
                retailValue = 12000m,
                tradeValue = 10000m
            });
}
