using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

/// <summary>
/// CASE-008: the assessment identity ribbon's Mileage and Vehicle figures
/// come from the same evidence cascade the old vehicle section prefilled
/// from — the saved assessment value, else confirmed case facts, else the
/// DVSA lookup observation — with no hint sentences anywhere on the page
/// (ENG-025 ported the section away; the cascade is unchanged).
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class AssessmentVehiclePrefillWebTests
{
    [Fact]
    public async Task RibbonShowsMileageAndVehicleFromLookupEvidence()
    {
        var caseId = Guid.NewGuid();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IGetAssessmentAccess>();
                services.RemoveAll<IGetAssessmentWorkspace>();
                var source = new FakeGetCase(caseId);
                services.AddSingleton<IGetCase>(source);
                services.AddSingleton<IGetAssessmentAccess>(new FakeGetAssessmentAccess());
                services.AddSingleton<IGetAssessmentWorkspace>(source);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139"),
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Engineer");

        using var response = await client.GetAsync($"/Cases/{caseId:D}/Assessment");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Odometer reading", html, StringComparison.Ordinal);
        Assert.DoesNotContain("In miles. Required unless", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Sets the mileage sentence", html, StringComparison.Ordinal);

        Assert.Contains("<div class=\"ribbon-label\">Mileage</div>", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"ribbon-value\">45123</div>", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"ribbon-value\">VOLKSWAGEN GOLF</div>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtractedVehicleFactsTakePrecedenceOverLookupObservation()
    {
        var caseId = Guid.NewGuid();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IGetAssessmentAccess>();
                services.RemoveAll<IGetAssessmentWorkspace>();
                var source = new FakeGetCase(caseId, includeExtractedFacts: true);
                services.AddSingleton<IGetCase>(source);
                services.AddSingleton<IGetAssessmentAccess>(new FakeGetAssessmentAccess());
                services.AddSingleton<IGetAssessmentWorkspace>(source);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139"),
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Engineer");

        using var response = await client.GetAsync($"/Cases/{caseId:D}/Assessment");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<div class=\"ribbon-value\">FORD FOCUS</div>", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"ribbon-value\">40000</div>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<div class=\"ribbon-value\">VOLKSWAGEN GOLF</div>", html, StringComparison.Ordinal);
    }

    private sealed class FakeGetCase(Guid caseId, bool includeExtractedFacts = false)
        : IGetCase, IGetAssessmentWorkspace
    {
        public Task<CaseDetails?> ExecuteAsync(GetCaseQuery query, CancellationToken cancellationToken)
        {
            if (query.CaseId != caseId)
            {
                return Task.FromResult<CaseDetails?>(null);
            }

            var identity = new CaseIdentity(caseId, "QDOS", 2026, 42, "QDOS-2026-00042");
            var workflow = new CaseWorkflowRecord(
                caseId, identity, CaseLifecycleState.Review, null, null,
                null, null, null, null, null, 7);
            var summary = new CaseSearchItem(
                caseId, identity.Reference, null, CaseType.Inspection, "Approved Principal",
                workflow.State, null, "AB12CDE", "Alex Example", "P-100",
                DateTimeOffset.UtcNow, new DateOnly(2026, 8, 1), "Email", DateTimeOffset.UtcNow);
            var observation = Observation(caseId);
            CaseDetails details = new(
                summary, workflow, null, [], null, CaseCustodyState.Pending, [], [], [])
            {
                Data = includeExtractedFacts ? Data(identity, workflow) : null,
                VehicleEvidence = new(caseId, null, observation, [observation], []),
            };
            return Task.FromResult<CaseDetails?>(details);
        }

        public async Task<AssessmentWorkspace?> ExecuteAsync(
            GetAssessmentWorkspaceQuery query,
            CancellationToken cancellationToken = default)
        {
            var details = await ExecuteAsync(new GetCaseQuery(query.CaseId, query.Actor), cancellationToken);
            if (details is null)
            {
                return null;
            }
            var assessment = new CaseAssessmentProjection(
                caseId,
                details.Summary.Reference,
                details.Workflow.Version,
                details.Workflow.State,
                null,
                [],
                [],
                new(null, null, null, null, null, null, null, null, null));
            return AssessmentWorkspaceTestData.Create(details, assessment);
        }
    }

    private static VehicleLookupObservation Observation(Guid caseId) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            caseId,
            1,
            VehicleLookupOutcome.Current,
            "AB12CDE",
            new("dvla-dvsa", "1", "response-1", DateTimeOffset.UtcNow, null, null),
            new("VOLKSWAGEN", "GOLF", 2019, 1968, "Diesel"),
            [new(new DateOnly(2026, 3, 4), "PASSED", new DateOnly(2027, 3, 3), 45123, VehicleMileageUnit.Miles)],
            new(45123, VehicleMileageUnit.Miles, new DateOnly(2026, 3, 4), VehicleMileagePolicy.MethodKey, VehicleMileagePolicy.MethodVersion, 1),
            null,
            DateTimeOffset.UtcNow);

    private static CaseDataProjection Data(CaseIdentity identity, CaseWorkflowRecord workflow)
    {
        var source = new CaseDataSource(CaseDataSourceKind.IntakeEvidence, "instruction", "Instruction", "test", 1);
        CaseField<T> Empty<T>() where T : notnull => new(null, null, null);
        CaseField<T> Fact<T>(T value) where T : notnull => new(new(value, CaseDataValueKind.Fact, source), null, null);
        return new(
            identity,
            new(Guid.NewGuid(), IntakeSourceChannel.Mailbox, "mail", "hash", DateTimeOffset.UtcNow, "reader", "1", null, null),
            DateTimeOffset.UtcNow,
            workflow.Version,
            workflow.State,
            new(new(true, true, true, true), new(true, "test", 1)),
            new(Empty<string>()),
            new(Empty<string>(), Empty<string>(), Empty<string>()),
            new(Empty<string>()),
            new(Fact("AB12CDE"), Fact("FORD"), Fact("FOCUS"), Fact(40000L), Fact("miles")),
            new(Empty<DateOnly>(), Empty<string>()),
            new(Empty<string>(), Empty<string>(), Empty<string>()),
            new(Empty<DateOnly>(), Empty<string>()),
            new(Empty<DateOnly>(), Empty<DateOnly>(), Empty<string>(), Empty<CaseInspectionMode>()));
    }
}
