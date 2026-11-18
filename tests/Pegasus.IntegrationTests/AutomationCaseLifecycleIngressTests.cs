using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Tier-4 caller-equivalent evidence for the Automation Actor's Case
/// lifecycle, report and document acts (ADR-0064, phase 2): real HTTP against
/// the gated /mcp surface and real LocalDB persistence, each act attributed to
/// the Automation actor and read back through the staff readers.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class AutomationCaseLifecycleIngressTests
{
    [Fact]
    public async Task HoldAndReleaseThroughCaseActionAreAttributedToTheAutomationActor()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);
        var version = await GetWorkflowVersionAsync(mcpFactory, caseId);
        var before = await StateAsync(mcpFactory, caseId);

        // A malformed call is refused before any lease is claimed.
        using (var noReason = await PostMcpAsync(client, token, ToolCallPayload(1, "pegasus_case_action",
            new { caseId, expectedVersion = version, operationKey = "mcp:hold-no-reason", action = "hold" })))
        {
            Assert.Contains("needs a reason", await ReadErrorTextAsync(noReason), StringComparison.Ordinal);
        }
        using (var unknown = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_case_action",
            new { caseId, expectedVersion = version, operationKey = "mcp:delete", action = "delete", reason = "No." })))
        {
            Assert.Contains("The action must be one of", await ReadErrorTextAsync(unknown), StringComparison.Ordinal);
        }
        Assert.Equal(version, await GetWorkflowVersionAsync(mcpFactory, caseId));

        using var held = await PostMcpAsync(client, token, ToolCallPayload(3, "pegasus_case_action", new
        {
            caseId,
            expectedVersion = version,
            operationKey = "mcp:hold",
            action = "hold",
            reason = "Awaiting the repairer's images.",
        }));
        var heldResult = await ReadStructuredContentAsync(held);
        Assert.Equal("hold", heldResult.GetProperty("action").GetString());
        Assert.Equal(nameof(CaseLifecycleState.Held), heldResult.GetProperty("state").GetString());
        var heldVersion = heldResult.GetProperty("caseVersion").GetInt64();
        Assert.True(heldVersion > version);
        Assert.Equal(CaseLifecycleState.Held, await StateAsync(mcpFactory, caseId));

        using var released = await PostMcpAsync(client, token, ToolCallPayload(4, "pegasus_case_action", new
        {
            caseId,
            expectedVersion = heldVersion,
            operationKey = "mcp:release-hold",
            action = "release_hold",
            reason = "The images arrived.",
        }));
        var releasedResult = await ReadStructuredContentAsync(released);
        Assert.Equal(before.ToString(), releasedResult.GetProperty("state").GetString());
        Assert.True(releasedResult.GetProperty("caseVersion").GetInt64() > heldVersion);

        await using var scope = mcpFactory.Services.CreateAsyncScope();
        await using var db = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
        var events = await db.CaseWorkflowEvents.AsNoTracking()
            .Where(row => row.CaseId == caseId
                && (row.EventType == "case_held" || row.EventType == "case_hold_released"))
            .ToListAsync();
        Assert.Equal(2, events.Count);
        Assert.All(events, row =>
        {
            Assert.Equal(nameof(ActorKind.Automation), row.ActorKind);
            Assert.Equal(ClientId, row.ActorSubjectId);
        });
        // The one-command lease is released after the act; nothing is left on the Case.
        var workflow = await db.CaseWorkflows.AsNoTracking().SingleAsync(row => row.CaseId == caseId);
        Assert.Null(workflow.EditLeaseHolder);
    }

    /// <summary>
    /// close (operator, 15 October 2026): a closure without a reason changes
    /// nothing; with one, the history records the reason and the disposition
    /// under the Automation actor.
    /// </summary>
    [Fact]
    public async Task CloseThroughCaseActionNeedsAReasonAndRecordsItWithTheOutcome()
    {
        const string reason = "The Principal withdrew the instruction.";
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);
        var version = await GetWorkflowVersionAsync(mcpFactory, caseId);

        using (var noReason = await PostMcpAsync(client, token, ToolCallPayload(40, "pegasus_case_action", new
        {
            caseId,
            expectedVersion = version,
            operationKey = "mcp:close-no-reason",
            action = "close",
            outcome = nameof(CaseClosureOutcome.PrincipalCancelled),
        })))
        {
            Assert.Contains("The close action needs a reason.", await ReadErrorTextAsync(noReason), StringComparison.Ordinal);
        }
        Assert.Equal(version, await GetWorkflowVersionAsync(mcpFactory, caseId));

        using var closed = await PostMcpAsync(client, token, ToolCallPayload(41, "pegasus_case_action", new
        {
            caseId,
            expectedVersion = version,
            operationKey = "mcp:close",
            action = "close",
            outcome = nameof(CaseClosureOutcome.PrincipalCancelled),
            reason,
        }));
        var closedResult = await ReadStructuredContentAsync(closed);
        Assert.Equal(nameof(CaseLifecycleState.PrincipalCancelled), closedResult.GetProperty("state").GetString());

        await using var scope = mcpFactory.Services.CreateAsyncScope();
        await using var db = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
        var closure = await db.CaseWorkflowEvents.AsNoTracking()
            .SingleAsync(row => row.CaseId == caseId && row.EventType == "case_closed_PrincipalCancelled");
        Assert.Equal(reason, closure.Reason);
        Assert.Equal(nameof(ActorKind.Automation), closure.ActorKind);
        Assert.Equal(ClientId, closure.ActorSubjectId);
        var workflow = await db.CaseWorkflows.AsNoTracking().SingleAsync(row => row.CaseId == caseId);
        Assert.Equal(nameof(CaseClosureOutcome.PrincipalCancelled), workflow.ClosureOutcome);
    }

    /// <summary>
    /// pegasus_case_query (operator, 27 October 2026): Automation opens a query
    /// on a Report sent Case with the channel it came by and its details, and
    /// marks that query done, each through the same Core command as staff and
    /// attributed to the Automation actor. The result carries whether a query
    /// is still open.
    /// </summary>
    [Fact]
    public async Task LogAndResolveQueryThroughCaseActionAreAttributedToTheAutomationActor()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        await using (var seedScope = mcpFactory.Services.CreateAsyncScope())
        {
            await using var seedDb = await seedScope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
            await seedDb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE CaseWorkflows SET State = {nameof(CaseLifecycleState.ReportSent)} WHERE CaseId = {caseId}");
        }
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);
        var version = await GetWorkflowVersionAsync(mcpFactory, caseId);

        // A malformed log_query claims nothing.
        using (var noChannel = await PostMcpAsync(client, token, ToolCallPayload(31, "pegasus_case_query",
            new { caseId, operationKey = "mcp:log-no-channel", action = "open" })))
        {
            Assert.Contains("open needs channel", await ReadErrorTextAsync(noChannel), StringComparison.Ordinal);
        }
        Assert.Equal(version, await GetWorkflowVersionAsync(mcpFactory, caseId));

        using var logged = await PostMcpAsync(client, token, ToolCallPayload(32, "pegasus_case_query", new
        {
            caseId,
            operationKey = "mcp:log-query",
            action = "open",
            channel = "Phone",
            details = "The handler rang about the labour rate.",
        }));
        var loggedResult = await ReadStructuredContentAsync(logged);
        Assert.Equal("open", loggedResult.GetProperty("action").GetString());
        Assert.True(loggedResult.GetProperty("hasOpenQuery").GetBoolean());
        var loggedVersion = loggedResult.GetProperty("caseVersion").GetInt64();
        Assert.True(loggedVersion > version);

        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            await using var db = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
            var query = await db.CaseQueries.AsNoTracking().SingleAsync(row => row.CaseId == caseId);
            Assert.Equal(nameof(CaseQuerySource.Staff), query.Source);
            Assert.Equal(nameof(CaseQueryChannel.Phone), query.Channel);
            Assert.Equal("The handler rang about the labour rate.", query.Details);
            Assert.Equal(nameof(ActorKind.Automation), query.RaisedByKind);
            Assert.Equal(ClientId, query.RaisedBySubjectId);
            Assert.Null(query.ClosedAtUtc);
            Assert.True((await db.CaseWorkflows.AsNoTracking().SingleAsync(row => row.CaseId == caseId)).HasOpenQuery);
        }

        using var resolved = await PostMcpAsync(client, token, ToolCallPayload(33, "pegasus_case_query", new
        {
            queryId = loggedResult.GetProperty("queryId").GetGuid(),
            operationKey = "mcp:mark-query-done",
            action = "mark_done",
        }));
        var resolvedResult = await ReadStructuredContentAsync(resolved);
        Assert.Equal("mark_done", resolvedResult.GetProperty("action").GetString());
        Assert.False(resolvedResult.GetProperty("hasOpenQuery").GetBoolean());
        Assert.True(resolvedResult.GetProperty("caseVersion").GetInt64() > loggedVersion);

        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            await using var db = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
            var query = await db.CaseQueries.AsNoTracking().SingleAsync(row => row.CaseId == caseId);
            Assert.Equal(nameof(CaseQueryClosure.Resolved), query.ClosedAs);
            Assert.Equal(nameof(ActorKind.Automation), query.ClosedByKind);
            Assert.Equal(ClientId, query.ClosedBySubjectId);
            var workflow = await db.CaseWorkflows.AsNoTracking().SingleAsync(row => row.CaseId == caseId);
            Assert.False(workflow.HasOpenQuery);
            Assert.Null(workflow.EditLeaseHolder);
            var events = await db.CaseWorkflowEvents.AsNoTracking()
                .Where(row => row.CaseId == caseId
                    && (row.EventType == "case_query_logged" || row.EventType == "case_query_resolved"))
                .ToListAsync();
            Assert.Equal(2, events.Count);
            Assert.All(events, row => Assert.Equal(nameof(ActorKind.Automation), row.ActorKind));
        }
    }

    /// <summary>
    /// pegasus_case_query reopen: a Done query opens again. A withdrawn query is refused.
    /// </summary>
    [Fact]
    public async Task ReopenThroughCaseQueryOpensADoneQueryAndRefusesAWithdrawnOne()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        await using (var seedScope = mcpFactory.Services.CreateAsyncScope())
        {
            await using var seedDb = await seedScope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
            await seedDb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE CaseWorkflows SET State = {nameof(CaseLifecycleState.ReportSent)} WHERE CaseId = {caseId}");
        }
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);

        using var logged = await PostMcpAsync(client, token, ToolCallPayload(51, "pegasus_case_query", new
        {
            caseId,
            operationKey = "mcp:reopen-open",
            action = "open",
            channel = "Email",
            details = "Asked for the labour rate."
        }));
        var queryId = (await ReadStructuredContentAsync(logged)).GetProperty("queryId").GetGuid();
        using var done = await PostMcpAsync(client, token, ToolCallPayload(52, "pegasus_case_query", new
        {
            queryId,
            operationKey = "mcp:reopen-done",
            action = "mark_done"
        }));
        Assert.False((await ReadStructuredContentAsync(done)).GetProperty("hasOpenQuery").GetBoolean());

        using var reopened = await PostMcpAsync(client, token, ToolCallPayload(53, "pegasus_case_query", new
        {
            queryId,
            operationKey = "mcp:reopen",
            action = "reopen"
        }));
        var reopenedResult = await ReadStructuredContentAsync(reopened);
        Assert.Equal("reopen", reopenedResult.GetProperty("action").GetString());
        Assert.True(reopenedResult.GetProperty("hasOpenQuery").GetBoolean());

        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            await using var db = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
            var query = await db.CaseQueries.AsNoTracking().SingleAsync(row => row.Id == queryId);
            Assert.Null(query.ClosedAtUtc);
            Assert.Null(query.ClosedAs);
            Assert.Contains(
                "case_query_reopened",
                await db.CaseWorkflowEvents.AsNoTracking()
                    .Where(row => row.CaseId == caseId)
                    .Select(row => row.EventType)
                    .ToListAsync());
        }

        var withdrawnId = Guid.NewGuid();
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            await using var db = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
            db.CaseQueries.Add(new()
            {
                Id = withdrawnId,
                CaseId = caseId,
                Source = nameof(CaseQuerySource.Staff),
                Channel = nameof(CaseQueryChannel.Phone),
                Details = "Withdrawn.",
                RaisedAtUtc = DateTimeOffset.UtcNow,
                RaisedByKind = nameof(ActorKind.Staff),
                RaisedBySubjectId = Guid.NewGuid().ToString("D"),
                RaisedOperationKey = $"withdrawn:{withdrawnId:N}",
                ClosedAs = nameof(CaseQueryClosure.Withdrawn),
                ClosedAtUtc = DateTimeOffset.UtcNow,
                ClosedByKind = nameof(ActorKind.Staff),
                ClosedBySubjectId = Guid.NewGuid().ToString("D")
            });
            await db.SaveChangesAsync();
        }

        using var refused = await PostMcpAsync(client, token, ToolCallPayload(54, "pegasus_case_query", new
        {
            queryId = withdrawnId,
            operationKey = "mcp:reopen-withdrawn",
            action = "reopen"
        }));
        Assert.Contains("cannot be reopened", await ReadErrorTextAsync(refused), StringComparison.Ordinal);
    }

    /// <summary>
    /// pegasus_case_query assign sets a staff member, then Unassigned when the assignee is omitted.
    /// </summary>
    [Fact]
    public async Task AssignThroughCaseQuerySetsAStaffMemberOrUnassigned()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        var engineerId = await SeedEngineerAsync(mcpFactory, "query-assignee");
        await using (var seedScope = mcpFactory.Services.CreateAsyncScope())
        {
            await using var seedDb = await seedScope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
            await seedDb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE CaseWorkflows SET State = {nameof(CaseLifecycleState.ReportSent)} WHERE CaseId = {caseId}");
        }
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);

        using var logged = await PostMcpAsync(client, token, ToolCallPayload(61, "pegasus_case_query", new
        {
            caseId,
            operationKey = "mcp:assign-open",
            action = "open",
            channel = "WhatsApp",
            details = "Who holds this?"
        }));
        var queryId = (await ReadStructuredContentAsync(logged)).GetProperty("queryId").GetGuid();

        using var assigned = await PostMcpAsync(client, token, ToolCallPayload(62, "pegasus_case_query", new
        {
            queryId,
            operationKey = "mcp:assign",
            action = "assign",
            assigneeStaffId = engineerId
        }));
        Assert.Equal("assign", (await ReadStructuredContentAsync(assigned)).GetProperty("action").GetString());

        string? userName;
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            await using var db = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
            var query = await db.CaseQueries.AsNoTracking().SingleAsync(row => row.Id == queryId);
            Assert.Equal(engineerId, query.AssigneeStaffId);
            Assert.NotNull(query.AssignedAtUtc);
            userName = await db.Users.AsNoTracking().Where(user => user.Id == engineerId).Select(user => user.UserName).SingleAsync();
            Assert.Equal(
                $"Assigned to {userName}",
                (await db.CaseWorkflowEvents.AsNoTracking()
                    .SingleAsync(row => row.CaseId == caseId && row.EventType == "case_query_assigned")).Reason);
        }

        using var cleared = await PostMcpAsync(client, token, ToolCallPayload(63, "pegasus_case_query", new
        {
            queryId,
            operationKey = "mcp:assign-unassigned",
            action = "assign"
        }));
        Assert.Equal("assign", (await ReadStructuredContentAsync(cleared)).GetProperty("action").GetString());

        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            await using var db = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
            var query = await db.CaseQueries.AsNoTracking().SingleAsync(row => row.Id == queryId);
            Assert.Null(query.AssigneeStaffId);
            Assert.Null(query.AssignedAtUtc);
            Assert.Equal(
                "Set to Unassigned",
                (await db.CaseWorkflowEvents.AsNoTracking()
                    .SingleAsync(row => row.CaseId == caseId && row.OperationKey == "mcp:assign-unassigned")).Reason);
        }
    }

    /// <summary>
    /// #1213 (operator, 15 October 2026): an omitted engineerId hands the Case
    /// to Unassigned, With Engineer, and a named one then assigns it there; no
    /// reason is asked, and Core records who it went to.
    /// </summary>
    [Fact]
    public async Task AssignEngineerThroughCaseActionMovesTheCaseToWithEngineer()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);
        await EnsureInReviewAsync(mcpFactory, client, token, caseId);
        var engineerId = await SeedEngineerAsync(mcpFactory, "lifecycle-engineer");
        var version = await GetWorkflowVersionAsync(mcpFactory, caseId);

        using var unassigned = await PostMcpAsync(client, token, ToolCallPayload(10, "pegasus_case_action",
            new { caseId, expectedVersion = version, operationKey = "mcp:assign-none", action = "assign_engineer" }));
        var unassignedResult = await ReadStructuredContentAsync(unassigned);
        Assert.Equal(nameof(CaseLifecycleState.ReportPreparation), unassignedResult.GetProperty("state").GetString());

        using var assigned = await PostMcpAsync(client, token, ToolCallPayload(11, "pegasus_case_action", new
        {
            caseId,
            expectedVersion = unassignedResult.GetProperty("caseVersion").GetInt64(),
            operationKey = "mcp:assign",
            action = "assign_engineer",
            engineerId,
        }));
        var result = await ReadStructuredContentAsync(assigned);
        Assert.Equal(nameof(CaseLifecycleState.ReportPreparation), result.GetProperty("state").GetString());

        await using var scope = mcpFactory.Services.CreateAsyncScope();
        var workflow = await scope.ServiceProvider.GetRequiredService<ICaseWorkflowQueries>()
            .GetAsync(caseId, CancellationToken.None);
        Assert.NotNull(workflow);
        Assert.Equal(engineerId, workflow.AssignedEngineerId);
        Assert.Equal(result.GetProperty("caseVersion").GetInt64(), workflow.Version);
        await using var db = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
        var reasons = await db.CaseWorkflowEvents.AsNoTracking()
            .Where(row => row.CaseId == caseId && row.EventType == "state_ReportPreparation")
            .OrderBy(row => row.AfterVersion)
            .Select(row => row.Reason)
            .ToListAsync();
        Assert.Equal(["Set to Unassigned", $"Assigned to lifecycle-engineer-{engineerId:N}"], reasons);
    }

    /// <summary>
    /// Defect F: a report approval the Automation Actor records reads back
    /// through the workflow store, the Case query store and the report list.
    /// The readers once accepted only a staff identity and threw on it.
    /// </summary>
    [Fact]
    public async Task ReportApprovalRecordedByTheAutomationActorReadsBackAsAutomation()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);
        await EnsureInReviewAsync(mcpFactory, client, token, caseId);
        var engineerId = await SeedEngineerAsync(mcpFactory, "approval-engineer");
        using (var assigned = await PostMcpAsync(client, token, ToolCallPayload(20, "pegasus_case_action", new
        {
            caseId,
            expectedVersion = await GetWorkflowVersionAsync(mcpFactory, caseId),
            operationKey = "mcp:approval-assign",
            action = "assign_engineer",
            engineerId,
        })))
        {
            await ReadStructuredContentAsync(assigned);
        }

        var version = await GetWorkflowVersionAsync(mcpFactory, caseId);
        using (var unknownArtifact = await PostMcpAsync(client, token, ToolCallPayload(21, "pegasus_report_action", new
        {
            caseId,
            expectedVersion = version,
            operationKey = "mcp:approve-unknown",
            action = "record_approval",
            artifactId = Guid.NewGuid(),
            reason = "Reviewed the report.",
        })))
        {
            Assert.Contains("not a report artifact of this work", await ReadErrorTextAsync(unknownArtifact),
                StringComparison.Ordinal);
        }

        var lease = await BeginEditAsync(client, token, caseId, version, rpcId: 22);
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("automation approved report")));
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRecordCaseReportApproval>().ExecuteAsync(
                new(caseId, lease.CaseVersion, ActionActor.Automation(ClientId), "mcp:approve",
                    "Reviewed the report.", lease.LeaseToken,
                    new(Guid.NewGuid(), "automation-report.pdf", sha256)),
                CancellationToken.None);

            var workflow = await scope.ServiceProvider.GetRequiredService<ICaseWorkflowQueries>()
                .GetAsync(caseId, CancellationToken.None);
            Assert.NotNull(workflow);
            var recorded = workflow.ReportApproval;
            Assert.NotNull(recorded);
            Assert.Equal(ActorKind.Automation, recorded.ApprovedBy.Kind);
            Assert.Equal(ClientId, recorded.ApprovedBy.SubjectId);

            var header = await scope.ServiceProvider.GetRequiredService<IGetCaseHeader>()
                .ExecuteAsync(new(caseId, ActionActor.Automation(ClientId)), CancellationToken.None);
            Assert.NotNull(header);
            var headerApproval = header.Workflow.ReportApproval;
            Assert.NotNull(headerApproval);
            Assert.Equal(ActorKind.Automation, headerApproval.ApprovedBy.Kind);
        }

        using var listed = await PostMcpAsync(client, token, ToolCallPayload(23, "pegasus_report_list", new { caseId }));
        var list = await ReadStructuredContentAsync(listed);
        var approval = list.GetProperty("approval");
        Assert.Equal(JsonValueKind.Object, approval.ValueKind);
        Assert.Equal(nameof(ActorKind.Automation), approval.GetProperty("approvedByKind").GetString());
        Assert.Equal(ClientId, approval.GetProperty("approvedBy").GetString());
        Assert.Equal(sha256, approval.GetProperty("artifactSha256").GetString());
    }

    [Fact]
    public async Task ImageTagThroughDocumentActionIsAppliedAndRemovedAsTheAutomationActor()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        var occurrenceId = await SeedImageAsync(mcpFactory, caseId);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);

        using (var vocabulary = await PostMcpAsync(client, token, ToolCallPayload(30, "pegasus_vocabulary_get", new { })))
        {
            var tags = (await ReadStructuredContentAsync(vocabulary)).GetProperty("imageTags");
            Assert.Contains(
                tags.EnumerateArray(),
                tag => tag.GetProperty("tagId").GetGuid() == ImageTagVocabulary.OverviewId);
        }

        var version = await GetWorkflowVersionAsync(mcpFactory, caseId);
        using (var noTag = await PostMcpAsync(client, token, ToolCallPayload(31, "pegasus_document_action",
            new { caseId, expectedVersion = version, operationKey = "mcp:tag-none", action = "tag", occurrenceId })))
        {
            Assert.Contains("needs tagId", await ReadErrorTextAsync(noTag), StringComparison.Ordinal);
        }

        using var tagged = await PostMcpAsync(client, token, ToolCallPayload(32, "pegasus_document_action", new
        {
            caseId,
            expectedVersion = version,
            operationKey = "mcp:tag-overview",
            action = "tag",
            occurrenceId,
            tagId = ImageTagVocabulary.OverviewId,
        }));
        var taggedResult = await ReadStructuredContentAsync(tagged);
        var taggedVersion = taggedResult.GetProperty("caseVersion").GetInt64();
        Assert.True(taggedVersion > version);
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            await using var db = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
            var assignment = await db.Set<DocumentOccurrenceTagEntity>().AsNoTracking()
                .SingleAsync(row => row.OccurrenceId == occurrenceId);
            Assert.Equal(ImageTagVocabulary.OverviewId, assignment.TagId);
            Assert.Equal(nameof(ActorKind.Automation), assignment.AppliedByKind);
            Assert.Equal(ClientId, assignment.AppliedBySubjectId);
        }

        using var untagged = await PostMcpAsync(client, token, ToolCallPayload(33, "pegasus_document_action", new
        {
            caseId,
            expectedVersion = taggedVersion,
            operationKey = "mcp:untag-overview",
            action = "untag",
            occurrenceId,
            tagId = ImageTagVocabulary.OverviewId,
        }));
        await ReadStructuredContentAsync(untagged);
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            await using var db = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
            Assert.False(await db.Set<DocumentOccurrenceTagEntity>().AnyAsync(row => row.OccurrenceId == occurrenceId));
        }
    }

    /// <summary>
    /// set_type (operator, 22 October 2026; R204): the tool names the types the
    /// Core lets staff choose, a malformed call claims nothing, and the change
    /// is recorded on the timeline as the Automation actor's under the
    /// one-command lease, which is released.
    /// </summary>
    [Fact]
    public async Task DocumentTypeThroughDocumentActionIsSetAsTheAutomationActor()
    {
        const string reason = "A photograph of the claim form, not of the vehicle.";
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        var occurrenceId = await SeedImageAsync(mcpFactory, caseId);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);

        var choosable = DocumentTypePolicy.StaffMayChoose.Select(role => role.ToString()).ToArray();
        var listed = string.Join(", ", choosable[..^1]) + " or " + choosable[^1];
        using (var toolsResponse = await PostMcpAsync(client, token, ToolsListPayload(50)))
        {
            using var tools = await ReadJsonRpcAsync(toolsResponse);
            var documentAction = tools.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
                .Single(tool => tool.GetProperty("name").GetString() == "pegasus_document_action");
            Assert.Contains(listed, documentAction.GetProperty("description").GetString(), StringComparison.Ordinal);
            Assert.Contains(
                listed,
                documentAction.GetProperty("inputSchema").GetProperty("properties").GetProperty("semanticRole")
                    .GetProperty("description").GetString(),
                StringComparison.Ordinal);
        }

        var version = await GetWorkflowVersionAsync(mcpFactory, caseId);
        var versionId = await VersionIdAsync(mcpFactory, occurrenceId);
        foreach (var (rpcId, arguments, refusal) in new (int, object, string)[]
        {
            (51, new { caseId, expectedVersion = version, operationKey = "mcp:type-no-reason", action = "set_type",
                occurrenceId, versionId, semanticRole = "Other" }, "The set_type action needs a reason."),
            (52, new { caseId, expectedVersion = version, operationKey = "mcp:type-digit", action = "set_type",
                occurrenceId, versionId, semanticRole = "8", reason }, "The set_type action needs semanticRole"),
            (53, new { caseId, expectedVersion = version, operationKey = "mcp:type-original-source", action = "set_type",
                occurrenceId, versionId, semanticRole = "OriginalSource", reason }, "The set_type action needs semanticRole"),
            (54, new { caseId, expectedVersion = version, operationKey = "mcp:type-no-version", action = "set_type",
                occurrenceId, semanticRole = "Other", reason }, "The set_type action needs versionId."),
        })
        {
            using var refused = await PostMcpAsync(client, token, ToolCallPayload(rpcId, "pegasus_document_action", arguments));
            Assert.Contains(refusal, await ReadErrorTextAsync(refused), StringComparison.Ordinal);
        }
        Assert.Equal(version, await GetWorkflowVersionAsync(mcpFactory, caseId));

        using var set = await PostMcpAsync(client, token, ToolCallPayload(55, "pegasus_document_action", new
        {
            caseId,
            expectedVersion = version,
            operationKey = "mcp:type-other",
            action = "set_type",
            occurrenceId,
            versionId,
            semanticRole = "Other",
            reason,
        }));
        var result = await ReadStructuredContentAsync(set);
        Assert.Equal("set_type", result.GetProperty("action").GetString());
        Assert.True(result.GetProperty("caseVersion").GetInt64() > version);

        await using var scope = mcpFactory.Services.CreateAsyncScope();
        await using var db = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
        Assert.Equal(
            DocumentSemanticRole.Other,
            (await db.Set<DocumentOccurrenceEntity>().AsNoTracking().SingleAsync(row => row.Id == occurrenceId)).SemanticRole);
        var change = await db.CaseWorkflowEvents.AsNoTracking()
            .SingleAsync(row => row.CaseId == caseId && row.EventType == "case_document_type_set");
        Assert.Equal(reason, change.Reason);
        Assert.Equal(nameof(ActorKind.Automation), change.ActorKind);
        Assert.Equal(ClientId, change.ActorSubjectId);
        Assert.Null((await db.CaseWorkflows.AsNoTracking().SingleAsync(row => row.CaseId == caseId)).EditLeaseHolder);
    }

    /// <summary>
    /// The garage authorisation milestone of a Garage authorisation and
    /// invoice Audit, end to end (operator, 17 November 2026; R1141 to R1144,
    /// R1159, R1161): the Case is Not ready until a confirmed External
    /// estimate is filed, the Engineer marks the garage authorised with no
    /// reason, the Case then waits Not ready with its Engineer for the repair
    /// invoice, and a confirmed Repair invoice returns it to With Engineer.
    /// Undo takes a reason and returns a waiting Case to With Engineer.
    /// </summary>
    [Fact]
    public async Task AGarageAuditWaitsForItsEstimateItsAuthorisationAndItsRepairInvoice()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(
            mcpFactory,
            caseType: CaseType.Audit,
            principalAuditWorkflowCode: PrincipalAuditWorkflowPolicy.GarageAuthorisationAndInvoiceCode);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);
        var engineerId = await SeedEngineerAsync(mcpFactory, "garage-engineer");

        // With its registration and mileage recorded, only the garage's
        // estimate keeps the Case Not ready (R1141).
        using (var details = await PostMcpAsync(client, token, ToolCallPayload(1, "pegasus_case_update_details", new
        {
            caseId,
            expectedVersion = await GetWorkflowVersionAsync(mcpFactory, caseId),
            operationKey = "mcp:garage-mileage",
            reason = "Recorded the vehicle mileage.",
            vehicleRegistration = "AB12CDE",
            vehicleMileage = 44_200,
            vehicleMileageUnit = "miles",
        })))
        {
            _ = await ReadStructuredContentAsync(details);
        }
        Assert.Equal(0, await ReconcileGarageWaitsAsync(mcpFactory));
        Assert.Equal(CaseLifecycleState.NotReady, await StateAsync(mcpFactory, caseId));
        var created = await GetCaseAsync(client, token, caseId, 2);
        Assert.Equal(
            nameof(AuditWorkflowKind.GarageAuthorisationAndInvoice),
            created.GetProperty("auditWorkflow").GetString());
        Assert.Equal([CaseWorkflowConfiguration.ExternalEstimateRequirement], MissingRequirements(created));
        Assert.False(created.GetProperty("holds").GetProperty("originalReportMissing").GetBoolean());

        // Authorise garage is the Engineer's act once the Case is With Engineer.
        using (var early = await PostMcpAsync(client, token, ToolCallPayload(3, "pegasus_case_action", new
        {
            caseId,
            expectedVersion = await GetWorkflowVersionAsync(mcpFactory, caseId),
            operationKey = "mcp:garage-authorise-early",
            action = "authorise_garage",
        })))
        {
            Assert.Contains(
                GarageAuthorisationPolicy.AuthoriseNotOffered,
                await ReadErrorTextAsync(early),
                StringComparison.Ordinal);
        }

        await AddGarageDocumentAsync(mcpFactory, client, token, caseId, 4, "ExternalEstimate", "garage-estimate.pdf");
        Assert.Equal(1, await ReconcileGarageWaitsAsync(mcpFactory));
        Assert.Equal(CaseLifecycleState.Review, await StateAsync(mcpFactory, caseId));
        Assert.Equal(0, await ReconcileGarageWaitsAsync(mcpFactory));

        using (var assigned = await PostMcpAsync(client, token, ToolCallPayload(5, "pegasus_case_action", new
        {
            caseId,
            expectedVersion = await GetWorkflowVersionAsync(mcpFactory, caseId),
            operationKey = "mcp:garage-assign",
            action = "assign_engineer",
            engineerId,
        })))
        {
            Assert.Equal(
                nameof(CaseLifecycleState.ReportPreparation),
                (await ReadStructuredContentAsync(assigned)).GetProperty("state").GetString());
        }

        // Mark done: no reason from the caller, and the Case waits Not ready,
        // keeping its Engineer, chased for the repair invoice (R1142, R1144).
        Assert.Equal(
            nameof(CaseLifecycleState.NotReady),
            await GarageActionAsync(mcpFactory, client, token, caseId, 6, "authorise_garage", "mcp:garage-authorise"));
        var waiting = await GetCaseAsync(client, token, caseId, 7);
        Assert.Equal([CaseWorkflowConfiguration.RepairInvoiceRequirement], MissingRequirements(waiting));
        Assert.Equal(JsonValueKind.String, waiting.GetProperty("garageAuthorisedAtUtc").ValueKind);
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            await using var db = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
            var workflow = await db.CaseWorkflows.AsNoTracking().SingleAsync(row => row.CaseId == caseId);
            Assert.Equal(engineerId, workflow.AssignedEngineerId);
            Assert.NotNull(workflow.GarageAuthorisedAtUtc);
            Assert.Null(workflow.EditLeaseHolder);
            var due = await db.CaseDueWork.AsNoTracking().SingleAsync(row => row.CaseId == caseId);
            Assert.Equal(GarageAuthorisationPolicy.AwaitingRepairInvoice, due.MissingMaterialReason);
            Assert.Equal(nameof(Pegasus.Core.Tasks.CaseDueWorkState.Scheduled), due.State);
            Assert.NotNull(due.NextChaseAtUtc);
            var authorised = await db.CaseWorkflowEvents.AsNoTracking()
                .SingleAsync(row => row.CaseId == caseId
                    && row.EventType == GarageAuthorisationPolicy.AuthorisedEventType);
            Assert.Equal("Garage authorised.", authorised.Reason);
            Assert.Equal(nameof(ActorKind.Automation), authorised.ActorKind);
        }
        Assert.Equal(0, await ReconcileGarageWaitsAsync(mcpFactory));

        // Undo takes a reason and returns the waiting Case to With Engineer,
        // keeping an Engineer whose account is enabled (R1142, R1159).
        using (var noReason = await PostMcpAsync(client, token, ToolCallPayload(8, "pegasus_case_action", new
        {
            caseId,
            expectedVersion = await GetWorkflowVersionAsync(mcpFactory, caseId),
            operationKey = "mcp:garage-undo-no-reason",
            action = "undo_garage_authorisation",
        })))
        {
            Assert.Contains("needs a reason", await ReadErrorTextAsync(noReason), StringComparison.Ordinal);
        }
        Assert.Equal(
            nameof(CaseLifecycleState.ReportPreparation),
            await GarageActionAsync(
                mcpFactory, client, token, caseId, 9, "undo_garage_authorisation", "mcp:garage-undo",
                "Marked before the garage answered."));
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            await using var db = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
            var workflow = await db.CaseWorkflows.AsNoTracking().SingleAsync(row => row.CaseId == caseId);
            Assert.Equal(engineerId, workflow.AssignedEngineerId);
            Assert.Null(workflow.GarageAuthorisedAtUtc);
        }

        // Authorised again, the Case waits until a confirmed Repair invoice
        // is filed, then returns to With Engineer with its Engineer (R1144).
        Assert.Equal(
            nameof(CaseLifecycleState.NotReady),
            await GarageActionAsync(
                mcpFactory, client, token, caseId, 10, "authorise_garage", "mcp:garage-authorise-again"));
        await AddGarageDocumentAsync(mcpFactory, client, token, caseId, 11, "RepairInvoice", "repair-invoice.pdf");
        Assert.Equal(1, await ReconcileGarageWaitsAsync(mcpFactory));
        Assert.Equal(CaseLifecycleState.ReportPreparation, await StateAsync(mcpFactory, caseId));
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            await using var db = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
            var workflow = await db.CaseWorkflows.AsNoTracking().SingleAsync(row => row.CaseId == caseId);
            Assert.Equal(engineerId, workflow.AssignedEngineerId);
            Assert.NotNull(workflow.GarageAuthorisedAtUtc);
            var returned = await db.CaseWorkflowEvents.AsNoTracking()
                .SingleAsync(row => row.CaseId == caseId
                    && row.EventType == GarageAuthorisationPolicy.ReturnedEventType);
            Assert.Equal(GarageAuthorisationPolicy.RepairInvoiceFiledReason, returned.Reason);
            Assert.Equal(workflow.Version, returned.AfterVersion);
            var due = await db.CaseDueWork.AsNoTracking().SingleAsync(row => row.CaseId == caseId);
            Assert.Equal(nameof(Pegasus.Core.Tasks.CaseDueWorkState.Stopped), due.State);
        }
        Assert.Equal(0, await ReconcileGarageWaitsAsync(mcpFactory));
        Assert.Empty(MissingRequirements(await GetCaseAsync(client, token, caseId, 12)));

        // The garage is authorised once.
        using var twice = await PostMcpAsync(client, token, ToolCallPayload(13, "pegasus_case_action", new
        {
            caseId,
            expectedVersion = await GetWorkflowVersionAsync(mcpFactory, caseId),
            operationKey = "mcp:garage-authorise-twice",
            action = "authorise_garage",
        }));
        Assert.Contains(
            GarageAuthorisationPolicy.AuthoriseNotOffered,
            await ReadErrorTextAsync(twice),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A Case that is not a Garage authorisation and invoice Audit has no
    /// garage to authorise, and files of those types do not move it.
    /// </summary>
    [Fact]
    public async Task AuthoriseGarageIsRefusedOnACaseWithoutTheMilestone()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);
        await EnsureInReviewAsync(mcpFactory, client, token, caseId);
        var engineerId = await SeedEngineerAsync(mcpFactory, "inspection-engineer");
        using (var assigned = await PostMcpAsync(client, token, ToolCallPayload(1, "pegasus_case_action", new
        {
            caseId,
            expectedVersion = await GetWorkflowVersionAsync(mcpFactory, caseId),
            operationKey = "mcp:no-garage-assign",
            action = "assign_engineer",
            engineerId,
        })))
        {
            _ = await ReadStructuredContentAsync(assigned);
        }

        using (var refused = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_case_action", new
        {
            caseId,
            expectedVersion = await GetWorkflowVersionAsync(mcpFactory, caseId),
            operationKey = "mcp:no-garage-authorise",
            action = "authorise_garage",
        })))
        {
            Assert.Contains(
                GarageAuthorisationPolicy.AuthoriseNotOffered,
                await ReadErrorTextAsync(refused),
                StringComparison.Ordinal);
        }
        using (var refused = await PostMcpAsync(client, token, ToolCallPayload(3, "pegasus_case_action", new
        {
            caseId,
            expectedVersion = await GetWorkflowVersionAsync(mcpFactory, caseId),
            operationKey = "mcp:no-garage-undo",
            action = "undo_garage_authorisation",
            reason = "There is nothing to undo.",
        })))
        {
            Assert.Contains(
                GarageAuthorisationPolicy.UndoNotOffered,
                await ReadErrorTextAsync(refused),
                StringComparison.Ordinal);
        }

        Assert.Equal(0, await ReconcileGarageWaitsAsync(mcpFactory));
        Assert.Equal(CaseLifecycleState.ReportPreparation, await StateAsync(mcpFactory, caseId));
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            await using var db = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
            Assert.Null((await db.CaseWorkflows.AsNoTracking().SingleAsync(row => row.CaseId == caseId))
                .GarageAuthorisedAtUtc);
            Assert.False(await db.CaseWorkflowEvents.AsNoTracking().AnyAsync(row =>
                row.CaseId == caseId
                && (row.EventType == GarageAuthorisationPolicy.AuthorisedEventType
                    || row.EventType == GarageAuthorisationPolicy.UndoneEventType)));
        }
        var read = await GetCaseAsync(client, token, caseId, 4);
        Assert.Equal(
            nameof(AuditWorkflowKind.OriginalEngineerReport),
            read.GetProperty("auditWorkflow").GetString());
    }

    /// <summary>
    /// The Case page's half of the milestone (R1142): the Next action is
    /// Authorise garage with one Mark done button that posts with no reason,
    /// the waiting Case names its Repair invoice, and the Actions menu's Undo
    /// garage authorisation posts its reason.
    /// </summary>
    [Fact]
    public async Task TheCasePageMarksTheGarageAuthorisedInOnePostAndUndoesItWithAReason()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        var caseId = await SeedAcceptedCaseAsync(
            factory,
            caseType: CaseType.Audit,
            principalAuditWorkflowCode: PrincipalAuditWorkflowPolicy.GarageAuthorisationAndInvoiceCode);
        var engineerId = await SeedEngineerAsync(factory, "garage-page-engineer");
        // The fixture puts the Case With Engineer with its Engineer; the way
        // there is the test above's.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await using var db = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE CaseWorkflows SET State = {nameof(CaseLifecycleState.ReportPreparation)}, AssignedEngineerId = {engineerId} WHERE CaseId = {caseId}");
        }
        using var staff = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var withEngineer = await CaseWebTestSupport.GetHtmlAsync(staff, $"/Cases/{caseId:D}");
        var markDone = System.Text.RegularExpressions.Regex.Match(
            withEngineer,
            "<form[^>]*data-authorise-garage[^>]*>.*?</form>",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(markDone.Success, "The Next action must draw Authorise garage's Mark done form.");
        Assert.Contains(
            $"/Cases/{caseId:D}/Workflow?handler=AuthoriseGarage",
            System.Net.WebUtility.HtmlDecode(markDone.Value),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">Mark done</button>", markDone.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"reason\"", markDone.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<strong data-next-label>Authorise garage</strong>", withEngineer, StringComparison.Ordinal);
        Assert.DoesNotContain("case-undo-garage-authorisation-dialog", withEngineer, StringComparison.Ordinal);

        using (var posted = await staff.PostAsync(
            $"/Cases/{caseId:D}/Workflow?handler=AuthoriseGarage",
            CaseWebTestSupport.Form(
                CaseWebTestSupport.AntiforgeryValue(withEngineer),
                ("id", caseId.ToString("D")),
                ("expectedVersion", CaseWebTestSupport.InputValue(markDone.Value, "expectedVersion")),
                ("operationKey", CaseWebTestSupport.InputValue(markDone.Value, "operationKey")))))
        {
            Assert.Equal(System.Net.HttpStatusCode.Redirect, posted.StatusCode);
        }
        Assert.Equal(CaseLifecycleState.NotReady, await StateAsync(factory, caseId));

        var waiting = await CaseWebTestSupport.GetHtmlAsync(staff, $"/Cases/{caseId:D}");
        Assert.DoesNotContain("data-authorise-garage", waiting, StringComparison.Ordinal);
        Assert.Contains("Repair invoice incomplete", waiting, StringComparison.Ordinal);
        Assert.Contains(
            "data-dialog-open=\"case-undo-garage-authorisation-dialog\"", waiting, StringComparison.Ordinal);
        var undo = System.Text.RegularExpressions.Regex.Match(
            waiting,
            "<div id=\"case-undo-garage-authorisation-dialog\".*?</form>",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(undo.Success, "The Case page must draw the Undo garage authorisation dialog.");
        Assert.Contains(
            $"/Cases/{caseId:D}/Workflow?handler=UndoGarageAuthorisation",
            System.Net.WebUtility.HtmlDecode(undo.Value),
            StringComparison.OrdinalIgnoreCase);

        using (var posted = await staff.PostAsync(
            $"/Cases/{caseId:D}/Workflow?handler=UndoGarageAuthorisation",
            CaseWebTestSupport.Form(
                CaseWebTestSupport.AntiforgeryValue(waiting),
                ("id", caseId.ToString("D")),
                ("expectedVersion", CaseWebTestSupport.InputValue(undo.Value, "expectedVersion")),
                ("operationKey", CaseWebTestSupport.InputValue(undo.Value, "operationKey")),
                ("Reason", "Marked on the wrong Case."))))
        {
            Assert.Equal(System.Net.HttpStatusCode.Redirect, posted.StatusCode);
        }
        Assert.Equal(CaseLifecycleState.ReportPreparation, await StateAsync(factory, caseId));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await using var db = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
            var workflow = await db.CaseWorkflows.AsNoTracking().SingleAsync(row => row.CaseId == caseId);
            Assert.Null(workflow.GarageAuthorisedAtUtc);
            Assert.Equal(engineerId, workflow.AssignedEngineerId);
            var undone = await db.CaseWorkflowEvents.AsNoTracking()
                .SingleAsync(row => row.CaseId == caseId
                    && row.EventType == GarageAuthorisationPolicy.UndoneEventType);
            Assert.Equal("Marked on the wrong Case.", undone.Reason);
            Assert.Equal(nameof(ActorKind.Staff), undone.ActorKind);
        }
    }

    private static async Task<int> ReconcileGarageWaitsAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ReconcileGarageWaits>()
            .ExecuteAsync(25, CancellationToken.None);
    }

    private static async Task<JsonElement> GetCaseAsync(HttpClient client, string token, Guid caseId, int rpcId)
    {
        using var response = await PostMcpAsync(client, token, ToolCallPayload(rpcId,
            "pegasus_case_get", new { caseId, limit = 1 }));
        return await ReadStructuredContentAsync(response);
    }

    private static string[] MissingRequirements(JsonElement read) =>
        read.GetProperty("holds").GetProperty("missingRequirements")
            .EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();

    /// <summary>One garage act under its own one-command lease; returns the Case's state after it.</summary>
    private static async Task<string?> GarageActionAsync(
        WebApplicationFactory<Program> factory,
        HttpClient client,
        string token,
        Guid caseId,
        int rpcId,
        string action,
        string operationKey,
        string? reason = null)
    {
        using var response = await PostMcpAsync(client, token, ToolCallPayload(rpcId, "pegasus_case_action", new
        {
            caseId,
            expectedVersion = await GetWorkflowVersionAsync(factory, caseId),
            operationKey,
            action,
            reason,
        }));
        var result = await ReadStructuredContentAsync(response);
        Assert.Equal(action, result.GetProperty("action").GetString());
        return result.GetProperty("state").GetString();
    }

    /// <summary>Files one document of <paramref name="semanticRole"/> under a one-command lease.</summary>
    private static async Task AddGarageDocumentAsync(
        WebApplicationFactory<Program> factory,
        HttpClient client,
        string token,
        Guid caseId,
        int rpcId,
        string semanticRole,
        string fileName)
    {
        using var response = await PostMcpAsync(client, token, ToolCallPayload(rpcId, "pegasus_document_add", new
        {
            caseId,
            fileName,
            mediaType = "application/pdf",
            contentBase64 = Convert.ToBase64String(Encoding.ASCII.GetBytes($"%PDF-1.4 {fileName}")),
            semanticRole,
            expectedCaseVersion = await GetWorkflowVersionAsync(factory, caseId),
            operationKey = $"mcp:garage-file-{rpcId}",
        }));
        _ = await ReadStructuredContentAsync(response);
    }

    private static async Task<CaseLifecycleState> StateAsync(WebApplicationFactory<Program> factory, Guid caseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var workflow = await scope.ServiceProvider.GetRequiredService<ICaseWorkflowQueries>()
            .GetAsync(caseId, CancellationToken.None)
            ?? throw new InvalidOperationException("The seeded case has no workflow.");
        return workflow.State;
    }

    private static async Task<Guid> SeedEngineerAsync(WebApplicationFactory<Program> factory, string userName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await using var db = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
        var engineerId = Guid.NewGuid();
        var role = await db.Roles.SingleOrDefaultAsync(row => row.NormalizedName == "ENGINEER");
        if (role is null)
        {
            role = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Engineer", NormalizedName = "ENGINEER" };
            db.Roles.Add(role);
        }
        var unique = $"{userName}-{engineerId:N}";
        db.Users.Add(new PegasusIdentityUser
        {
            Id = engineerId,
            UserName = unique,
            NormalizedUserName = unique.ToUpperInvariant(),
            IsEnabled = true,
            MustChangePassword = false,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
        });
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = engineerId, RoleId = role.Id });
        await db.SaveChangesAsync();
        return engineerId;
    }

    private static async Task<Guid> VersionIdAsync(WebApplicationFactory<Program> factory, Guid occurrenceId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await using var db = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
        return await db.Set<DocumentOccurrenceEntity>().AsNoTracking()
            .Where(row => row.Id == occurrenceId)
            .Select(row => row.VersionId)
            .SingleAsync();
    }

    private static async Task<Guid> SeedImageAsync(WebApplicationFactory<Program> factory, Guid caseId)
    {
        var bytes = Encoding.UTF8.GetBytes("automation image tag fixture");
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        await using var db = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
        db.AddRange(
            new CaseDocumentEntity
            {
                Id = documentId, CaseId = caseId, Ordinal = 1, SourceOccurrenceIdentity = "automation:image-tag-fixture",
            },
            new DocumentVersionEntity
            {
                Id = versionId, DocumentId = documentId, Version = 1, FileName = "front.jpg", MediaType = "image/jpeg",
                ContentLength = bytes.Length, Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
                CustodyStatus = DocumentCustodyStatus.Confirmed, CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = ClientId, IsCurrent = true,
            },
            new DocumentOccurrenceEntity
            {
                Id = occurrenceId, CaseId = caseId, DocumentId = documentId, VersionId = versionId, Ordinal = 1,
                SourceOccurrenceIdentity = "automation:image-tag-fixture", OperationKey = "mcp-image-fixture",
                SemanticRole = DocumentSemanticRole.Image, Source = DocumentSource.Automation,
                RecordedAtUtc = DateTimeOffset.UtcNow,
            });
        await db.SaveChangesAsync();
        return occurrenceId;
    }
}
