using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Documents;
using Pegasus.Infrastructure.Persistence;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Caller evidence for the Automation Actor's report preparation (ADR-0064):
/// the report's wording blocks and each image's crop, rotation, order and
/// page of its own, read as the Case page reads them and written through the
/// staff Case save over the gated /mcp host.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class AutomationReportPreparationIngressTests
{
    [Fact]
    public async Task TheActorRotatesCropsAndPrintsAnImageOnItsOwnPageThroughTheCaseSave()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        var occurrenceId = await SeedImageAsync(mcpFactory, caseId);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);

        long preparationVersion;
        long caseVersion;
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(1, "pegasus_image_preparation_get", new { caseId })))
        {
            var read = await ReadStructuredContentAsync(response);
            caseVersion = read.GetProperty("caseVersion").GetInt64();
            var image = Assert.Single(read.GetProperty("images").EnumerateArray());
            Assert.Equal(occurrenceId, image.GetProperty("occurrenceId").GetGuid());
            Assert.True(image.GetProperty("inReport").GetBoolean());
            Assert.Equal(0, image.GetProperty("rotation").GetInt32());
            preparationVersion = image.GetProperty("preparationVersion").GetInt64();
        }

        // Malformed calls are refused before any lease is claimed.
        using (var refused = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_image_prepare", new
        {
            caseId,
            expectedVersion = caseVersion,
            operationKey = "mcp:prepare-rotation",
            images = new[] { new { occurrenceId, expectedPreparationVersion = preparationVersion, rotation = 45 } },
        })))
        {
            Assert.Contains("0, 90, 180 or 270", await ReadErrorTextAsync(refused), StringComparison.Ordinal);
        }
        using (var refused = await PostMcpAsync(client, token, ToolCallPayload(3, "pegasus_image_prepare", new
        {
            caseId,
            expectedVersion = caseVersion,
            operationKey = "mcp:prepare-stranger",
            images = new[] { new { occurrenceId = Guid.NewGuid(), expectedPreparationVersion = 0 } },
        })))
        {
            Assert.Contains("is not an image of this Case", await ReadErrorTextAsync(refused), StringComparison.Ordinal);
        }
        Assert.Equal(caseVersion, await GetWorkflowVersionAsync(mcpFactory, caseId));

        using (var response = await PostMcpAsync(client, token, ToolCallPayload(4, "pegasus_image_prepare", new
        {
            caseId,
            expectedVersion = caseVersion,
            operationKey = "mcp:prepare",
            reason = "Straighten the front image.",
            images = new[]
            {
                new
                {
                    occurrenceId,
                    expectedPreparationVersion = preparationVersion,
                    rotation = 90,
                    crop = new { left = 0.1m, top = 0.2m, width = 0.5m, height = 0.6m },
                    fullPage = true,
                },
            },
        })))
        {
            var saved = await ReadStructuredContentAsync(response);
            Assert.True(saved.GetProperty("caseVersion").GetInt64() > caseVersion);
            var image = Assert.Single(saved.GetProperty("images").EnumerateArray());
            Assert.Equal(90, image.GetProperty("rotation").GetInt32());
            Assert.Equal(0.5m, image.GetProperty("crop").GetProperty("width").GetDecimal());
            Assert.True(image.GetProperty("fullPage").GetBoolean());
            Assert.Equal(ClientId, image.GetProperty("preparedBy").GetString());
        }

        // The preparation version moved, so a call that read the old one is stale.
        using (var stale = await PostMcpAsync(client, token, ToolCallPayload(5, "pegasus_image_prepare", new
        {
            caseId,
            expectedVersion = await GetWorkflowVersionAsync(mcpFactory, caseId),
            operationKey = "mcp:prepare-stale",
            images = new[] { new { occurrenceId, expectedPreparationVersion = preparationVersion, rotation = 180 } },
        })))
        {
            _ = await ReadErrorTextAsync(stale);
        }

        await using var scope = mcpFactory.Services.CreateAsyncScope();
        await using var db = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
        var occurrence = await db.Set<DocumentOccurrenceEntity>().AsNoTracking().SingleAsync(row => row.Id == occurrenceId);
        Assert.Equal((short)90, occurrence.RotationDegrees);
        Assert.True(occurrence.PreparationFullPage);
        Assert.Equal(ClientId, occurrence.PreparedBy);
        // The one-command lease is released after the save.
        var workflow = await db.CaseWorkflows.AsNoTracking().SingleAsync(row => row.CaseId == caseId);
        Assert.Null(workflow.EditLeaseHolder);
    }

    [Fact]
    public async Task ReportWordingIsReadAndRefusedWhileTheReportCannotBeProjected()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);
        var version = await GetWorkflowVersionAsync(mcpFactory, caseId);

        using (var response = await PostMcpAsync(client, token, ToolCallPayload(1, "pegasus_report_wording_get", new { caseId })))
        {
            var read = await ReadStructuredContentAsync(response);
            Assert.False(read.GetProperty("available").GetBoolean());
            Assert.Equal(0, read.GetProperty("blocks").GetArrayLength());
            Assert.Contains(
                read.GetProperty("standardBlocks").EnumerateArray(),
                block => block.GetProperty("code").GetString() == "nature");
        }

        // An unknown key and a key named twice are refused before any lease.
        using (var refused = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_report_wording_save", new
        {
            caseId,
            expectedVersion = version,
            operationKey = "mcp:wording-unknown",
            blocks = new[] { new { key = "summary", text = "Not a block." } },
        })))
        {
            Assert.Contains("is not a report wording block", await ReadErrorTextAsync(refused), StringComparison.Ordinal);
        }
        using (var refused = await PostMcpAsync(client, token, ToolCallPayload(3, "pegasus_report_wording_save", new
        {
            caseId,
            expectedVersion = version,
            operationKey = "mcp:wording-twice",
            blocks = new[] { new { key = "nature", text = "One." }, new { key = "nature", text = "Two." } },
        })))
        {
            Assert.Contains("named more than once", await ReadErrorTextAsync(refused), StringComparison.Ordinal);
        }

        // With no projected report there is no composed sentence to compare
        // with, so nothing is saved, as on the Case page.
        using (var refused = await PostMcpAsync(client, token, ToolCallPayload(4, "pegasus_report_wording_save", new
        {
            caseId,
            expectedVersion = version,
            operationKey = "mcp:wording-unprojected",
            blocks = new[] { new { key = "nature", text = "The vehicle was struck from behind while stationary." } },
        })))
        {
            Assert.Contains("report wording is unavailable", await ReadErrorTextAsync(refused), StringComparison.Ordinal);
        }
        Assert.Equal(0, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseReportWordings AS r
            INNER JOIN CaseWorks AS w ON w.Id = r.WorkId
            WHERE w.CaseId = '{caseId:D}'
            """));
    }

    [Fact]
    public async Task ReportPreparationNeedsTheDocumentsScope()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();
        var casesOnly = await RequestTokenAsync(client, "automation.cases");
        var caseId = Guid.NewGuid();

        var payloads = new[]
        {
            ToolCallPayload(1, "pegasus_report_wording_get", new { caseId }),
            ToolCallPayload(2, "pegasus_report_wording_save", new
            {
                caseId,
                expectedVersion = 0,
                operationKey = "mcp:wording-scope",
                blocks = new[] { new { key = "nature", text = "Scope." } },
            }),
            ToolCallPayload(3, "pegasus_image_preparation_get", new { caseId }),
            ToolCallPayload(4, "pegasus_image_prepare", new
            {
                caseId,
                expectedVersion = 0,
                operationKey = "mcp:prepare-scope",
                images = new[] { new { occurrenceId = Guid.NewGuid(), expectedPreparationVersion = 0 } },
            }),
        };
        foreach (var payload in payloads)
        {
            using var response = await PostMcpAsync(client, casesOnly, payload);
            Assert.Contains("automation.documents", await ReadErrorTextAsync(response), StringComparison.Ordinal);
        }
    }

    private static async Task<Guid> SeedImageAsync(WebApplicationFactory<Program> factory, Guid caseId)
    {
        var bytes = Encoding.UTF8.GetBytes("automation image preparation fixture");
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        await using var db = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
        db.AddRange(
            new CaseDocumentEntity
            {
                Id = documentId, CaseId = caseId, Ordinal = 1, SourceOccurrenceIdentity = "automation:image-preparation-fixture",
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
                SourceOccurrenceIdentity = "automation:image-preparation-fixture", OperationKey = "mcp-image-preparation-fixture",
                SemanticRole = DocumentSemanticRole.Image, Source = DocumentSource.Automation,
                RecordedAtUtc = DateTimeOffset.UtcNow,
            });
        await db.SaveChangesAsync();
        return occurrenceId;
    }
}
