using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// A standalone Audit is created with its Original report cells filled from
/// its retained original report, in the acceptance transaction (v28 P51,
/// #840): the report's own reading where it can be read, the intake verdict
/// for Repairable status where the report printed none, and neither where the
/// two disagree.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class OriginalReportPrefillAcceptanceTests
{
    // The hash AllocationTestData gives the synthetic original report asset.
    private static readonly string SeededReportHash = new('c', 64);

    [Fact]
    public async Task AnUnreadableReportLeavesTheIntakeVerdictAloneToFillRepairableStatus()
    {
        using var factory = new IntakeWebApplicationFactory();
        await AllocationTestData.SeedPrincipalAsync(factory.Services, "PREFILL");
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(factory.Services, CaseType.Audit, "PREFILL");
        await AllocationTestData.SeedAutomaticAuditEvidenceAsync(factory.Services, receipt.Id);

        // The composed reader finds no retained bytes behind the synthetic asset.
        var cells = await AllocateAsync(factory, receipt.Id, reader: null);

        var outcome = Assert.Single(cells);
        Assert.Equal(AssessmentVocabulary.OriginalReportOutcome, outcome.FieldPath);
        Assert.Equal("repairable", outcome.Value);
        Assert.Equal(nameof(ActorKind.Automation), outcome.RecordedByKind);
        Assert.Equal(OriginalReportPrefillPolicy.RecorderId, outcome.RecordedBy);
    }

    [Fact]
    public async Task TheReportsReadingFillsItsCellsAndADisagreeingOutcomeIsLeftBlank()
    {
        using var factory = new IntakeWebApplicationFactory();
        await AllocationTestData.SeedPrincipalAsync(factory.Services, "PREFILL");
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(factory.Services, CaseType.Audit, "PREFILL");
        await AllocationTestData.SeedAutomaticAuditEvidenceAsync(factory.Services, receipt.Id);

        // The seeded evidence's verdict is Repairable; the report says total loss.
        var cells = await AllocateAsync(
            factory,
            receipt.Id,
            new FixedReader(new(SeededReportHash, "Montgomery Assessors", "2026-08-14", "unroadworthy", "total_loss", false)));

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["original_report.assessor"] = "Montgomery Assessors",
                ["original_report.report_date"] = "2026-08-14",
                ["original_report.roadworthiness"] = "unroadworthy"
            },
            cells.ToDictionary(item => item.FieldPath, item => item.Value));
        Assert.All(cells, cell => Assert.Equal(OriginalReportPrefillPolicy.RecorderId, cell.RecordedBy));
    }

    [Fact]
    public async Task AReadingOfOtherBytesIsIgnored()
    {
        using var factory = new IntakeWebApplicationFactory();
        await AllocationTestData.SeedPrincipalAsync(factory.Services, "PREFILL");
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(factory.Services, CaseType.Audit, "PREFILL");
        await AllocationTestData.SeedAutomaticAuditEvidenceAsync(factory.Services, receipt.Id);

        var cells = await AllocateAsync(
            factory,
            receipt.Id,
            new FixedReader(new(new string('d', 64), "Montgomery Assessors", "2026-08-14", "unroadworthy", null, false)));

        Assert.Equal(AssessmentVocabulary.OriginalReportOutcome, Assert.Single(cells).FieldPath);
    }

    private static async Task<List<CaseAssessmentFieldEntity>> AllocateAsync(
        IntakeWebApplicationFactory factory,
        Guid receiptId,
        IReadOriginalReport? reader)
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var acceptance = reader is null
                ? services.GetRequiredService<IAcceptIntake>()
                : ActivatorUtilities.CreateInstance<AcceptIntake>(services, reader);
            var result = await new AllocateIntake(
                    services.GetRequiredService<IIntakeReceiptQueries>(),
                    services.GetRequiredService<IIntakeAllocationStore>(),
                    acceptance,
                    services.GetRequiredService<TimeProvider>(),
                    services.GetRequiredService<IStandaloneAuditEvidenceQueries>())
                .AttemptAutomaticAsync(receiptId, Guid.NewGuid());
            Assert.Equal(IntakeAllocationProjectionStatus.Succeeded, result?.State.Status);
        }

        await using var context = await factory.Database.CreateContextAsync();
        var caseId = await context.Cases
            .Where(item => item.OriginIntakeReceiptId == receiptId)
            .Select(item => item.Id)
            .SingleAsync();
        return await context.CaseAssessmentFields
            .Where(item => item.WorkId == caseId)
            .ToListAsync();
    }

    private sealed class FixedReader(OriginalReportReading reading) : IReadOriginalReport
    {
        public Task<OriginalReportReading?> ForIntakeAsync(
            Guid receiptId, Guid standaloneAuditEvidenceId, CancellationToken cancellationToken) =>
            Task.FromResult<OriginalReportReading?>(reading);

        public Task<OriginalReportReading?> ForDocumentAsync(
            ActionActor actor, Guid caseId, Guid occurrenceId, Guid versionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
