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

        using (var noEngineer = await PostMcpAsync(client, token, ToolCallPayload(10, "pegasus_case_action",
            new { caseId, expectedVersion = version, operationKey = "mcp:assign-none", action = "assign_engineer" })))
        {
            Assert.Contains("needs engineerId", await ReadErrorTextAsync(noEngineer), StringComparison.Ordinal);
        }

        using var assigned = await PostMcpAsync(client, token, ToolCallPayload(11, "pegasus_case_action", new
        {
            caseId,
            expectedVersion = version,
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
