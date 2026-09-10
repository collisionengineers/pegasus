using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests.Reports;

/// <summary>
/// DOCS-014: the report-draft preview GET route records a Case-history
/// "viewed" event distinct from a completed download, at most once per Case,
/// staff member and London day. This reuses
/// <see cref="AssessmentReportDraftWebTests"/>'s real-pipeline fixtures (a
/// fast fake renderer, the real readiness projection and the real page
/// handler) against a genuinely persisted Case, so the event is written
/// through the real <c>EfCaseReportGenerationStore</c> and read back from
/// <c>CaseWorkflowEvents</c> — the table the Case's own history panel reads —
/// not a substituted store. The download side of DOCS-014 (reopening a
/// confirmed generated artifact) is exercised at the persistence level in
/// <c>Reports/CaseReportGenerationPersistenceTests.cs</c>, because the
/// download route's handler (<c>OnGetGeneratedArtifactAsync</c>) is
/// unchanged — the recording lives entirely inside the store it already
/// calls.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class AssessmentReportDraftWebTests
{
    private static readonly DateTimeOffset ViewDownloadFixtureAtUtc =
        new(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PreviewingTheReportDraftRecordsAViewedCaseHistoryEventOnce()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = Guid.NewGuid();
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId)),
            new FakeRenderer([1, 2, 3, 4]));
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await SeedCaseWorkflowAsync(contextFactory, caseId);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        using var first = await client.GetAsync($"/Cases/{caseId:D}?handler=PreviewReportDraft&section=report");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        // A second preview minutes later, same staff member, same London
        // day: a silent no-op, never a second Case-history row.
        using var second = await client.GetAsync($"/Cases/{caseId:D}?handler=PreviewReportDraft&section=report");
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var viewed = Assert.Single(await CaseHistoryEventsAsync(contextFactory, caseId, "case_report_draft_previewed"));
        Assert.Equal("Report draft previewed", viewed.Reason);
        Assert.Equal(nameof(ActorKind.Staff), viewed.ActorKind);
        Assert.False(string.IsNullOrWhiteSpace(viewed.ActorSubjectId));
        // A preview is never a Case mutation.
        Assert.Equal(viewed.BeforeVersion, viewed.AfterVersion);
        // Distinct from a completed download: a preview never records one.
        Assert.Empty(await CaseHistoryEventsAsync(contextFactory, caseId, "case_report_artifact_downloaded"));
    }

    /// <summary>
    /// The preview handler's guard runs before rendering; a Case that fails
    /// readiness never reaches the draft, so it never records a view either.
    /// </summary>
    [Fact]
    public async Task IncompleteCaseDraftPreviewNeverRecordsAViewedEvent()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = Guid.NewGuid();
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId) with { CurrentEstimate = null }),
            new FakeRenderer([1]));
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await SeedCaseWorkflowAsync(contextFactory, caseId);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.GetAsync($"/Cases/{caseId:D}?handler=PreviewReportDraft&section=report");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(await CaseHistoryEventsAsync(contextFactory, caseId, "case_report_draft_previewed"));
    }

    private static async Task<IReadOnlyList<CaseWorkflowEventEntity>> CaseHistoryEventsAsync(
        IDbContextFactory<PegasusDbContext> contextFactory, Guid caseId, string eventType)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Set<CaseWorkflowEventEntity>().AsNoTracking()
            .Where(item => item.CaseId == caseId && item.EventType == eventType)
            .ToArrayAsync();
    }

    /// <summary>
    /// The minimal real Case + Workflow row DOCS-014's presentation events
    /// need to attach to: <see cref="Compose"/> fakes every read the page
    /// makes, but the store that records a view or a download reloads the
    /// real <c>CaseWorkflows</c> row directly, so one must actually exist.
    /// <c>OriginIntakeReceiptId</c> is nullable, so no receipt fixture is
    /// needed — only the Organization/sequence/Principal a Case's required
    /// foreign keys demand.
    /// </summary>
    private static async Task SeedCaseWorkflowAsync(
        IDbContextFactory<PegasusDbContext> contextFactory, Guid caseId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        context.AddRange(
            new OrganizationEntity { Id = organizationId, Name = "DOCS-014 view/download test", Version = 0 },
            new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = ViewDownloadFixtureAtUtc },
            new PrincipalEntity
            {
                Id = principalId,
                OrganizationId = organizationId,
                SequenceLineageId = lineageId,
                Code = "DOCS014",
                IsActive = true,
                Version = 0
            },
            new CaseEntity
            {
                Id = caseId,
                PrincipalId = principalId,
                SequenceLineageId = lineageId,
                Year = 2026,
                Sequence = 1,
                Reference = "DOCS014-1",
                Type = "Inspection",
                InitialState = "NotReady",
                CustodyState = "confirmed",
                CreatedAtUtc = ViewDownloadFixtureAtUtc,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            },
            new CaseWorkflowEntity
            {
                CaseId = caseId,
                State = "ReportPreparation",
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            });
        await context.SaveChangesAsync();
    }
}
