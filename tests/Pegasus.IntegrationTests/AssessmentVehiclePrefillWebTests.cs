using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Cases;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

/// <summary>
/// CASE-008: the assessment page's vehicle section is one "Mileage" input and
/// a "Source" dropdown, prefilled from the case's DVSA lookup evidence — the
/// estimate lands in the field and the source preselects Online data, with no
/// hint sentences under either control.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class AssessmentVehiclePrefillWebTests
{
    [Fact]
    public async Task VehicleSectionPrefillsMileageAndDetailsFromLookupEvidence()
    {
        var caseId = Guid.NewGuid();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IVehicleEvidenceQueries>();
                services.AddSingleton<IGetCase>(new FakeGetCase(caseId));
                services.AddSingleton<IVehicleEvidenceQueries>(new FakeVehicleEvidence(caseId));
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139"),
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Engineer");

        using var response = await client.GetAsync($"/Cases/{caseId:D}/Assessment?section=vehicle");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains(">Mileage</label>", html, StringComparison.Ordinal);
        Assert.Contains(">Source</label>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Odometer reading", html, StringComparison.Ordinal);
        Assert.DoesNotContain("In miles. Required unless", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Sets the mileage sentence", html, StringComparison.Ordinal);

        Assert.Contains("name=\"vehicle.odometer_miles\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"45123\"", html, StringComparison.Ordinal);
        Assert.Contains(
            "value=\"online_data\" selected", html, StringComparison.Ordinal);
        Assert.Contains("value='VOLKSWAGEN'", html, StringComparison.Ordinal);
        Assert.Contains("value='GOLF'", html, StringComparison.Ordinal);
        Assert.Contains("value='2019'", html, StringComparison.Ordinal);
    }

    private sealed class FakeGetCase(Guid caseId) : IGetCase
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
            CaseDetails details = new(
                summary, workflow, null, [], null, CaseCustodyState.Pending, [], [], []);
            return Task.FromResult<CaseDetails?>(details);
        }
    }

    private sealed class FakeVehicleEvidence(Guid caseId) : IVehicleEvidenceQueries
    {
        public Task<CaseVehicleEvidence?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            if (id != caseId)
            {
                return Task.FromResult<CaseVehicleEvidence?>(null);
            }

            var observation = new VehicleLookupObservation(
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
            return Task.FromResult<CaseVehicleEvidence?>(
                new(caseId, null, observation, [observation], []));
        }
    }
}
