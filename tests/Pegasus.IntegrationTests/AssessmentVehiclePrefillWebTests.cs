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
/// Vehicle evidence remains on the Case record when the Assessment
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
                services.RemoveAll<IGetCasePageFrame>();
                services.RemoveAll<IGetCaseVehicleSection>();
                services.RemoveAll<IGetAssessmentWorkspace>();
                var source = new FakeGetCase(caseId);
                services.AddSingleton<IGetCaseEditBasis>(source);
                services.AddSingleton<IGetCasePageFrame>(source);
                services.AddSingleton<IGetCaseVehicleSection>(source);
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

        // v26: the Vehicle section is the record-section host itself, and the
        // lookup line sits in its head as the section meta.
        Assert.Contains("id=\"section-vehicle-title\"", html, StringComparison.Ordinal);
        Assert.Contains("data-vehicle-lookup-line", html, StringComparison.Ordinal);
        Assert.Contains("AB12CDE", html, StringComparison.Ordinal);
        // The lookup fills the case record rather than displaying its own reading
        // beside it: the observation is stated by the outcome line only.
        Assert.DoesNotContain("45,123", html, StringComparison.Ordinal);
        Assert.Contains("Looked up ", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmedVehicleFactsTakePrecedenceOverLookupObservation()
    {
        var caseId = Guid.NewGuid();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCasePageFrame>();
                services.RemoveAll<IGetCaseVehicleSection>();
                services.RemoveAll<IGetAssessmentWorkspace>();
                var source = new FakeGetCase(caseId, includeConfirmedFacts: true);
                services.AddSingleton<IGetCaseEditBasis>(source);
                services.AddSingleton<IGetCasePageFrame>(source);
                services.AddSingleton<IGetCaseVehicleSection>(source);
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
        // v25 decision 6: one mileage box reads the figure with its unit word.
        Assert.Contains("40,000 mi", html, StringComparison.Ordinal);
        Assert.Contains("data-vehicle-mileage-read", html, StringComparison.Ordinal);
        Assert.DoesNotContain("VOLKSWAGEN", html, StringComparison.Ordinal);
        Assert.DoesNotContain("GOLF", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PartialConfirmedVehicleEvidenceKeepsTheAcceptedRegistrationVisible()
    {
        var caseId = Guid.NewGuid();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCasePageFrame>();
                services.RemoveAll<IGetCaseVehicleSection>();
                services.RemoveAll<IGetAssessmentWorkspace>();
                var source = new FakeGetCase(caseId, includePartialConfirmedFacts: true);
                services.AddSingleton<IGetCaseEditBasis>(source);
                services.AddSingleton<IGetCasePageFrame>(source);
                services.AddSingleton<IGetCaseVehicleSection>(source);
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
        Assert.Contains("AB12CDE", html, StringComparison.Ordinal);
        Assert.Contains("FORD", html, StringComparison.Ordinal);
    }

    private sealed class FakeGetCase(
        Guid caseId,
        bool includeConfirmedFacts = false,
        bool includePartialConfirmedFacts = false)
        : IGetCaseEditBasis, IGetCasePageFrame, IGetCaseVehicleSection, IGetAssessmentWorkspace
    {
        /// <summary>The Case's frame as a focused read returns it; null for any other Case.</summary>
        private CaseSectionFrame? Frame(Guid requestedCaseId)
        {
            if (requestedCaseId != caseId)
            {
                return null;
            }

            var identity = new CaseIdentity(caseId, "QDOS", 2026, 42, "QDOS-2026-00042");
            var workflow = new CaseWorkflowRecord(
                caseId, identity, CaseLifecycleState.Review, null, null,
                null, null, null, null, null, 7);
            var summary = new CaseSearchItem(
                caseId, identity.Reference, null, CaseType.Inspection, "Approved Principal",
                workflow.State, null, "AB12CDE", "Alex Example", "P-100",
                DateTimeOffset.UtcNow, "Email", DateTimeOffset.UtcNow);
            return new(summary, workflow, null);
        }

        private CaseDataProjection CaseData(CaseSectionFrame frame) =>
            Data(frame.Workflow.Identity, frame.Workflow, includeConfirmedFacts, includePartialConfirmedFacts);

        Task<CaseEditBasis?> IGetCaseEditBasis.ExecuteAsync(
            GetCaseQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<CaseEditBasis?>(Frame(query.CaseId) is { } frame
                ? new(frame, CaseData(frame))
                : null);

        Task<CasePageFrame?> IGetCasePageFrame.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult<CasePageFrame?>(Frame(query.CaseId) is { } frame
                ? new(frame, [], [], CaseRecordNotes.None, CaseData(frame))
                : null);

        Task<CaseVehicleSection?> IGetCaseVehicleSection.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken)
        {
            if (Frame(query.CaseId) is not { } frame)
            {
                return Task.FromResult<CaseVehicleSection?>(null);
            }

            var workspace = query.AssessmentWorkspace ?? Workspace(frame);
            return Task.FromResult<CaseVehicleSection?>(new(
                frame,
                workspace.Data,
                workspace.LatestVehicleObservation,
                workspace.Assessment));
        }

        public Task<AssessmentWorkspace?> ExecuteAsync(
            GetAssessmentWorkspaceQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Frame(query.CaseId) is { } frame ? Workspace(frame) : null);

        private AssessmentWorkspace Workspace(CaseSectionFrame frame)
        {
            var assessment = new CaseAssessmentProjection(
                caseId,
                frame.Summary.Reference,
                frame.Workflow.Version,
                frame.Workflow.State,
                null,
                [],
                [],
                new(null, null, null, null, null, null, "tbc", null, new DateOnly(2026, 8, 2), null, null, null, null, null));
            return AssessmentWorkspaceTestData.Create(frame, assessment, CaseData(frame), Observation(caseId));
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

    private static CaseDataProjection Data(
        CaseIdentity identity,
        CaseWorkflowRecord workflow,
        bool includeConfirmedFacts = false,
        bool partialConfirmedVehicleEvidence = false)
    {
        var source = new CaseDataSource(CaseDataSourceKind.IntakeEvidence, "instruction", "Instruction", "test", 1);
        CaseField<T> Empty<T>() where T : notnull => new(null, null, null);
        CaseField<T> Confirmed<T>(T value) where T : notnull => new(
            null,
            null,
            new(value, CaseDataValueKind.Confirmed, source, "engineer-1", DateTimeOffset.UtcNow));
        CaseField<T> Fact<T>(T value) where T : notnull => new(
            new(value, CaseDataValueKind.Fact, source, null, null),
            null,
            null);
        return new(
            identity,
            new(Guid.NewGuid(), IntakeSourceChannel.Mailbox, "mail", "hash", DateTimeOffset.UtcNow, "reader", "1", null, null),
            DateTimeOffset.UtcNow,
            workflow.Version,
            workflow.State,
            new(new(true, true), new(true, "test", 1)),
            new(Empty<string>()),
            new(Empty<string>(), Empty<string>(), Empty<string>()),
            new(Empty<string>()),
            new(
                includeConfirmedFacts && !partialConfirmedVehicleEvidence ? Confirmed("AB12CDE") : Fact("AB12CDE"),
                (includeConfirmedFacts || partialConfirmedVehicleEvidence) ? Confirmed("FORD") : Empty<string>(),
                includeConfirmedFacts && !partialConfirmedVehicleEvidence ? Confirmed("FOCUS") : Empty<string>(),
                includeConfirmedFacts && !partialConfirmedVehicleEvidence ? Confirmed("2019") : Empty<string>(),
                includeConfirmedFacts && !partialConfirmedVehicleEvidence ? Confirmed(40000L) : Empty<long>(),
                includeConfirmedFacts && !partialConfirmedVehicleEvidence ? Confirmed("miles") : Empty<string>()),
            new(Empty<DateOnly>(), Empty<string>()),
            new(Empty<string>(), Empty<string>(), Empty<string>()),
            new(new DateOnly(2026, 8, 1), Empty<string>()),
            new(Empty<DateOnly>(), Empty<DateOnly>(), Empty<string>(), Empty<CaseInspectionMode>()));
    }
}
