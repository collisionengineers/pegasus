using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Locks the supplied QDOS incident evidence through the real mailbox intake,
/// allocation, Case-data persistence, and the Case Details vehicle panel.
/// </summary>
[Trait("Category", "SqlServer")]
[Trait("Category", "Corpus")]
public sealed class IncidentVehicleCompositionTests
{
    [IncidentVehicleSourceFact]
    public async Task QdosIncidentVehicleDescriptionStaysSourceOnlyThroughCaseDetails()
    {
        const string description = "SEAT LEON SPORT TDI 105";
        var path = IncidentVehicleSourceFixture.Path;
        var bytes = await File.ReadAllBytesAsync(path);
        Assert.Equal(IncidentVehicleSourceFixture.Sha256, Convert.ToHexString(SHA256.HashData(bytes)));

        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            useIntegrationTestAuthentication: true,
            initializeDevelopmentOffline: false);
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var clock = services.GetRequiredService<TimeProvider>();
        var workStore = services.GetRequiredService<IIntakeWorkStore>();
        var receiver = new ReceiveIntake(
            services.GetRequiredService<IIntakeArtifactStore>(),
            workStore,
            clock,
            new CommittedWorkPublisherDouble());
        var source = new IntakeSource(
            Path.GetFileName(path),
            Top15InstructionCorpusTests.MediaType(path),
            bytes,
            clock.GetUtcNow(),
            "system-worker:approved-inbox-poller",
            new(IntakeSourceChannel.Mailbox, "incident-qdos-vehicle-description"));

        var received = await receiver.ExecuteAsync(source, "incident-qdos-vehicle-description");
        var dispatch = Assert.IsType<IntakeWorkItem>(await workStore.ClaimDispatchAsync(
            received.StagedReceiptId,
            clock.GetUtcNow(),
            TimeSpan.FromMinutes(1),
            CancellationToken.None));
        await workStore.MarkDispatchedAsync(
            dispatch.Id,
            dispatch.LeaseToken!,
            clock.GetUtcNow(),
            CancellationToken.None);
        await IntakeWebDriver.CreateProcessor(services).ExecuteAsync(received.StagedReceiptId);

        var evaluation = Assert.IsType<IntakeEvaluationRevision>(await workStore.GetCompletedEvaluationAsync(
            received.StagedReceiptId,
            CancellationToken.None));
        var receipt = Assert.IsType<IntakeReceipt>(await services
            .GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(evaluation.ProcessedReceiptId, CancellationToken.None));
        var caseId = receipt.CurrentCaseId ?? throw new InvalidOperationException("The incident intake was not allocated to a Case.");
        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);
        Assert.Equal(QdosInstructionExtractionPolicy.Key, receipt.ExtractionPolicyKey);
        Assert.Equal(QdosInstructionExtractionPolicy.Version, receipt.ExtractionPolicyVersion);
        Assert.Equal("QDOS", receipt.InstructionDraft?.SuggestedPrincipalCode);
        Assert.Equal(description, Assert.Single(receipt.Fields,
            field => field.Name == "Vehicle description").SuggestedValue);
        Assert.Null(receipt.InstructionDraft?.VehicleMake);
        Assert.Null(receipt.InstructionDraft?.VehicleModel);
        Assert.Null(receipt.InstructionDraft?.VehicleMileage);

        await using (var context = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync())
        {
            var snapshot = await context.CaseDataSnapshots
                .Include(item => item.Fields)
                .SingleAsync(item => item.CaseId == caseId);
            Assert.Equal(receipt.Id, snapshot.OriginIntakeReceiptId);
            Assert.Equal(IncidentVehicleSourceFixture.Sha256, snapshot.OriginSourceHash, ignoreCase: true);

            var fact = Assert.Single(snapshot.Fields, item =>
                item.FieldName == CaseDataFieldNames.VehicleDescription
                && item.ValueKind == CaseDataCodes.Fact);
            Assert.Equal(description, fact.Value);
            Assert.Equal(CaseDataCodes.IntakeEvidence, fact.SourceKind);
            Assert.Equal(receipt.Id.ToString("D"), fact.SourceIdentity);
            Assert.Equal(receipt.ExtractionPolicyKey, fact.PolicyKey);
            Assert.Equal(receipt.ExtractionPolicyVersion, fact.PolicyVersion);
            Assert.Contains("attachment 6", fact.SourceLabel, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(snapshot.Fields, item => item.FieldName is
                CaseDataFieldNames.VehicleMake or
                CaseDataFieldNames.VehicleModel or
                CaseDataFieldNames.VehicleMileage);
        }

        using var page = await client.GetAsync($"/Cases/{caseId:D}?section=vehicle");
        page.EnsureSuccessStatusCode();
        var html = await page.Content.ReadAsStringAsync();
        // The description stays a source field on the record (asserted above),
        // but the section states the vehicle's own facts: it draws no row for a
        // value the operator cannot act on.
        Assert.DoesNotContain(description, html, StringComparison.Ordinal);
        Assert.Contains("<dt>Make</dt><dd>Not recorded</dd>", html, StringComparison.Ordinal);
        Assert.Contains("<dt>Model</dt><dd>Not recorded</dd>", html, StringComparison.Ordinal);
        Assert.Contains("<dt>Mileage</dt><dd>Not recorded</dd>", html, StringComparison.Ordinal);
    }
}
