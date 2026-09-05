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
/// ENG-034: vehicle evidence remains on the Case record when the Assessment
/// page is retired. The existing Vehicle section renders lookup observations
/// and gives confirmed case facts precedence in its primary fields.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class AssessmentVehiclePrefillWebTests
{
    [Fact]
    public async Task CaseVehicleSectionShowsLookupEvidence()
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

        using var response = await client.GetAsync($"/Cases/{caseId:D}?section=vehicle");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Odometer reading", html, StringComparison.Ordinal);
        Assert.DoesNotContain("In miles. Required unless", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Sets the mileage sentence", html, StringComparison.Ordinal);

        Assert.Contains("id=\"case-vehicle-title\"", html, StringComparison.Ordinal);
        Assert.Contains("AB12CDE", html, StringComparison.Ordinal);
        Assert.Contains("45,123 miles", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmedVehicleFactsTakePrecedenceOverLookupObservation()
    {
        var caseId = Guid.NewGuid();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IGetAssessmentAccess>();
                services.RemoveAll<IGetAssessmentWorkspace>();
                var source = new FakeGetCase(caseId, includeConfirmedFacts: true);
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

        using var response = await client.GetAsync($"/Cases/{caseId:D}?section=vehicle");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("FORD", html, StringComparison.Ordinal);
        Assert.Contains("FOCUS", html, StringComparison.Ordinal);
        Assert.Contains("40,000 miles", html, StringComparison.Ordinal);
        Assert.DoesNotContain("VOLKSWAGEN", html, StringComparison.Ordinal);
        Assert.DoesNotContain("GOLF", html, StringComparison.Ordinal);
    }

    private sealed class FakeGetCase(Guid caseId, bool includeConfirmedFacts = false)
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
                Data = includeConfirmedFacts ? Data(identity, workflow) : null,
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
        CaseField<T> Confirmed<T>(T value) where T : notnull => new(
            null,
            null,
            new(value, CaseDataValueKind.Confirmed, source, "engineer-1", DateTimeOffset.UtcNow));
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
            new(Confirmed("AB12CDE"), Confirmed("FORD"), Confirmed("FOCUS"), Confirmed(40000L), Confirmed("miles")),
            new(Empty<DateOnly>(), Empty<string>()),
            new(Empty<string>(), Empty<string>(), Empty<string>()),
            new(Empty<DateOnly>(), Empty<string>()),
            new(Empty<DateOnly>(), Empty<DateOnly>(), Empty<string>(), Empty<CaseInspectionMode>()));
    }
}
