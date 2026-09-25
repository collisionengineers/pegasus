using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Actors;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Core.Documents;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

/// <summary>The Engineer workbench is part of every Case render.</summary>
[Trait("Category", "SqlServer")]
public sealed class CaseEngineerSectionsWebTests
{
    public static TheoryData<CaseLifecycleState> LifecycleStates =>
        new(Enum.GetValues<CaseLifecycleState>());

    [Theory]
    [MemberData(nameof(LifecycleStates))]
    public async Task EveryLifecycleStateRendersAllEngineerSectionsAndRecordedValues(
        CaseLifecycleState state)
    {
        var source = new EngineerSectionSource(state);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IGetCasePageFrame>();
                services.RemoveAll<IGetAssessmentAccess>();
                services.RemoveAll<IGetAssessmentWorkspace>();
                services.RemoveAll<ICaseReportSnapshotSource>();
                services.RemoveAll<IListCaseEstimates>();
                services.RemoveAll<ISendToAiControl>();
                services.AddSingleton<IGetCase>(source);
                services.AddSingleton<IGetCasePageFrame>(source);
                services.AddSingleton<IGetAssessmentAccess>(source);
                services.AddSingleton<IGetAssessmentWorkspace>(source);
                services.AddSingleton<ICaseReportSnapshotSource>(source);
                services.AddSingleton<IListCaseEstimates>(source);
                services.AddSingleton<ISendToAiControl>(new EnabledSendToAiControl());
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", "User");

        using var response = await client.GetAsync($"/Cases/{source.CaseId:D}?section=estimate");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        foreach (var section in new[] { "damage", "valuation", "estimate", "settlement", "report" })
        {
            Assert.Contains($"id=\"section-{section}\"", html, StringComparison.Ordinal);
        }
        Assert.Contains("right_rear", html, StringComparison.Ordinal);
        Assert.Contains("repairable", html, StringComparison.Ordinal);
        Assert.Contains("Engineer comments recorded", html, StringComparison.Ordinal);
        Assert.Contains("Estimate 1", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-salvage-range", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-salvage-snap", html, StringComparison.Ordinal);
        Assert.DoesNotContain("staff-reviewed", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reviewed by staff", html, StringComparison.OrdinalIgnoreCase);

        if (!AssessmentPolicy.IsWritableState(state))
        {
            Assert.DoesNotContain("New repair spec", html, StringComparison.Ordinal);
            Assert.DoesNotContain("Import estimate", html, StringComparison.Ordinal);
            Assert.DoesNotContain("Save repair spec", html, StringComparison.Ordinal);
            Assert.DoesNotContain("Send to Claude", html, StringComparison.Ordinal);
            Assert.DoesNotContain("Generate report draft", html, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("id=\"case-estimate-import-form\"", html, StringComparison.Ordinal);
            Assert.Contains("Import estimate", html, StringComparison.Ordinal);
            Assert.Contains("handler=ImportEstimate", html, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// GET ?estimate=new must not depend on the actor's role or the assessment
    /// being open to render the (read-only) editor panel; a Held Case offers
    /// no import to anyone.
    /// </summary>
    [Theory]
    [InlineData("User", CaseLifecycleState.Held)]
    [InlineData("Engineer", CaseLifecycleState.Held)]
    public async Task NewEstimateGetRendersReadOnlyEditorWhenNotEditable(string role, CaseLifecycleState state)
    {
        var source = new EngineerSectionSource(state);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IGetCasePageFrame>();
                services.RemoveAll<IGetAssessmentAccess>();
                services.RemoveAll<IGetAssessmentWorkspace>();
                services.RemoveAll<ICaseReportSnapshotSource>();
                services.RemoveAll<IListCaseEstimates>();
                services.RemoveAll<ISendToAiControl>();
                services.AddSingleton<IGetCase>(source);
                services.AddSingleton<IGetCasePageFrame>(source);
                services.AddSingleton<IGetAssessmentAccess>(source);
                services.AddSingleton<IGetAssessmentWorkspace>(source);
                services.AddSingleton<ICaseReportSnapshotSource>(source);
                services.AddSingleton<IListCaseEstimates>(source);
                services.AddSingleton<ISendToAiControl>(new EnabledSendToAiControl());
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);

        using var response = await client.GetAsync($"/Cases/{source.CaseId:D}?section=estimate&estimate=new");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("id=\"section-estimate\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"case-estimate-import-form\"", html, StringComparison.Ordinal);
    }

    private sealed class EngineerSectionSource :
        IGetCase,
        IGetCasePageFrame,
        IGetAssessmentAccess,
        IGetAssessmentWorkspace,
        ICaseReportSnapshotSource,
        IListCaseEstimates
    {
        private readonly CaseDetails details;
        private readonly AssessmentWorkspace workspace;
        private readonly RepairSpecificationVersion estimate;
        public EngineerSectionSource(CaseLifecycleState state)
        {
            CaseId = Guid.NewGuid();
            var identity = new CaseIdentity(CaseId, "QDOS", 2026, 42, "QDOS-2026-00042");
            var workflow = new CaseWorkflowRecord(
                CaseId,
                identity,
                state,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                7);
            var summary = new CaseSearchItem(
                CaseId,
                identity.Reference,
                null,
                CaseType.Inspection,
                "Approved Principal",
                state,
                null,
                "AB12CDE",
                "Alex Example",
                "P-100",
                DateTimeOffset.UtcNow,
                new DateOnly(2026, 8, 1),
                "Email",
                DateTimeOffset.UtcNow);
            var assessment = new CaseAssessmentProjection(
                CaseId,
                identity.Reference,
                workflow.Version,
                state,
                null,
                Fields(),
                [],
                new("AB12CDE", null, null, null, null, null, "tbc", null, new DateOnly(2026, 8, 2), null, null,
                    null, "Alex Example", "P-100"));
            workspace = AssessmentWorkspaceTestData.Create(assessment);
            details = new(
                summary,
                workflow,
                null,
                [],
                null,
                CaseCustodyState.Pending,
                [],
                [])
            {
                Data = workspace.Data
            };
            estimate = Estimate(CaseId);
            estimate = estimate with { RecordedTotals = EstimateTotals.Compute(estimate) };
            workspace = workspace with
            {
                Header = workspace.Header with { State = state },
                AcceptedSpecification = estimate
            };
        }

        public Guid CaseId { get; }

        public Task<CaseDetails?> ExecuteAsync(GetCaseQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<CaseDetails?>(query.CaseId == CaseId ? details : null);

        Task<CasePageFrame?> IGetCasePageFrame.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult<CasePageFrame?>(query.CaseId == CaseId
                ? new(
                    new(details.Summary, details.Workflow, details.ActiveEditLease),
                    details.Documents,
                    details.AvailableReportSentEvidence,
                    details.RecordNotes,
                    details.Data!)
                : null);

        public Task<AssessmentAccessState?> ExecuteAsync(
            GetAssessmentAccessQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AssessmentAccessState?>(
                query.CaseId == CaseId
                    ? new(details.Workflow.State)
                    : null);

        public Task<AssessmentWorkspace?> ExecuteAsync(
            GetAssessmentWorkspaceQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AssessmentWorkspace?>(query.CaseId == CaseId ? workspace : null);

        public Task<IReadOnlyList<RepairSpecificationVersion>> ExecuteAsync(
            Guid caseId,
            CaseWorkSelector work,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RepairSpecificationVersion>>(
                caseId == CaseId ? [estimate] : []);

        public Task<CaseReportFreezeInputs?> GetAsync(Guid caseId, ActionActor actor, CaseWorkSelector work, CancellationToken cancellationToken) =>
            Task.FromResult<CaseReportFreezeInputs?>(caseId != CaseId ? null : new(
                new(workspace.Assessment, workspace.Assessment.Reference, [], null, [], [], estimate),
                new(workspace.Assessment, null, null, [], estimate, null, [], new Dictionary<Guid, DocumentVersion>()),
                workspace.Assessment.Reference, details.Workflow.Version));

        private static IReadOnlyList<AssessmentFieldValue> Fields()
        {
            AssessmentFieldValue Field(string path, string value) => new(
                path,
                value,
                ActorKind.Staff,
                "engineer-1",
                DateTimeOffset.UtcNow,
                "engineer-1",
                DateTimeOffset.UtcNow);
            return
            [
                Field(AssessmentVocabulary.ImpactLocation, "right_rear"),
                Field(AssessmentVocabulary.ImpactSeverity, "moderate"),
                Field(AssessmentVocabulary.NatureOfIncident, "Rear impact"),
                Field(AssessmentVocabulary.Outcome, "repairable"),
                Field(AssessmentVocabulary.SalvageCategory, "S"),
                Field(AssessmentVocabulary.SalvageValue, "750.00"),
                Field(AssessmentVocabulary.CostRecoveryCharge, "120.00"),
                Field(AssessmentVocabulary.CostStorageCharge, "80.00"),
                Field(AssessmentVocabulary.EngineersComments, "Engineer comments recorded"),
                Field(AssessmentVocabulary.HistoryCheck, "History clear"),
                Field(AssessmentVocabulary.AgreedFee, "120.00"),
                Field(AssessmentVocabulary.FeeDescriptionLines, "Engineering assessment"),
                Field(AssessmentVocabulary.StatementOfTruth, "I confirm this report is true"),
                Field(AssessmentVocabulary.ValueEngineer, "5000.00")
            ];
        }

        private static RepairSpecificationVersion Estimate(Guid caseId) => new(
            Guid.NewGuid(),
            caseId,
            1,
            RepairSpecificationState.Accepted,
            new(RepairSpecificationSourceRoute.Manual, null, null, null),
            [
                new(
                    Guid.NewGuid(), 1, "new_part", null, "FRONT BUMPER", null, 620.20m, false,
                    "51 11 8 067", null, null, null, null,
                    ActorKind.Staff, "engineer-1", DateTimeOffset.UtcNow, "engineer-1", DateTimeOffset.UtcNow),
            ],
            null,
            "engineer-1",
            DateTimeOffset.UtcNow,
            "engineer-1",
            DateTimeOffset.UtcNow,
            null,
            null,
            new("Estimate 1", 50m, 25m, 20m),
            true);
    }

    private sealed class EnabledSendToAiControl : ISendToAiControl
    {
        public Task<bool> IsEnabledAsync(CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> SetEnabledAsync(
            bool enabled,
            ActionActor actor,
            string? reason,
            string operationKey,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
