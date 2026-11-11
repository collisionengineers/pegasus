using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
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

    /// <summary>
    /// The mileage the report states fills the Audit's empty mileage before
    /// its gate is read, so an Audit missing nothing else is created in
    /// Review (R930), sourced to the report.
    /// </summary>
    [Fact]
    public async Task AReportStatingAMileageCreatesTheAuditInReviewWithThatMileage()
    {
        using var factory = new IntakeWebApplicationFactory();
        await AllocationTestData.SeedPrincipalAsync(factory.Services, "PREFILL");
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(factory.Services, CaseType.Audit, "PREFILL");
        var evidenceId = await AllocationTestData.SeedAutomaticAuditEvidenceAsync(factory.Services, receipt.Id);

        var caseId = await AcceptCompleteAuditAsync(
            factory,
            receipt,
            evidenceId,
            new(SeededReportHash, "Connexus Vehicle Assessors", "2026-03-09", null, null, false, 28136, "miles"));

        await using var context = await (await factory.DatabaseAsync).CreateContextAsync();
        Assert.Equal(
            nameof(CaseLifecycleState.Review),
            await context.CaseWorkflows.Where(item => item.CaseId == caseId).Select(item => item.State).SingleAsync());
        var fields = await context.CaseDataFields
            .Where(item => item.WorkId == caseId
                && (item.FieldName == CaseDataFieldNames.VehicleMileage
                    || item.FieldName == CaseDataFieldNames.VehicleMileageUnit))
            .ToDictionaryAsync(item => item.FieldName);
        var reportAssetId = await context.Set<StandaloneAuditEvidenceEntity>()
            .Where(item => item.Id == evidenceId)
            .Select(item => item.OriginalReportAssetId)
            .SingleAsync();
        var mileage = fields[CaseDataFieldNames.VehicleMileage];
        Assert.Equal("28136", mileage.Value);
        Assert.Equal(CaseDataCodes.Fact, mileage.ValueKind);
        Assert.Equal(CaseDataCodes.IntakeEvidence, mileage.SourceKind);
        Assert.Equal(reportAssetId.ToString("D"), mileage.SourceIdentity);
        Assert.Equal("Bodyshopreport-V1.pdf", mileage.SourceLabel);
        Assert.Equal(OriginalReportPrefillPolicy.MileagePolicyKey, mileage.PolicyKey);
        Assert.Equal("miles", fields[CaseDataFieldNames.VehicleMileageUnit].Value);
    }

    [Fact]
    public async Task AReportStatingNoMileageLeavesTheAuditNotReady()
    {
        using var factory = new IntakeWebApplicationFactory();
        await AllocationTestData.SeedPrincipalAsync(factory.Services, "PREFILL");
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(factory.Services, CaseType.Audit, "PREFILL");
        var evidenceId = await AllocationTestData.SeedAutomaticAuditEvidenceAsync(factory.Services, receipt.Id);

        var caseId = await AcceptCompleteAuditAsync(
            factory,
            receipt,
            evidenceId,
            new(SeededReportHash, "Connexus Vehicle Assessors", "2026-03-09", null, null, false));

        await using var context = await (await factory.DatabaseAsync).CreateContextAsync();
        Assert.Equal(
            nameof(CaseLifecycleState.NotReady),
            await context.CaseWorkflows.Where(item => item.CaseId == caseId).Select(item => item.State).SingleAsync());
        Assert.False(await context.CaseDataFields.AnyAsync(item =>
            item.WorkId == caseId && item.FieldName == CaseDataFieldNames.VehicleMileage));
    }

    /// <summary>
    /// A standalone Audit accepted with its instruction and images complete,
    /// seeded from <paramref name="reading"/>, so only its mileage decides
    /// whether it is created in Review.
    /// </summary>
    private static async Task<Guid> AcceptCompleteAuditAsync(
        IntakeWebApplicationFactory factory,
        IntakeReceipt receipt,
        Guid evidenceId,
        OriginalReportReading reading)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var acceptance = ActivatorUtilities.CreateInstance<AcceptIntake>(scope.ServiceProvider, new FixedReader(reading));
        var outcome = await acceptance.ExecuteAsync(
            new(
                receipt.Id,
                receipt.Version,
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
                $"accept-report-mileage:{Guid.NewGuid():N}",
                CaseType.Audit,
                "PREFILL",
                new(true, true),
                StandaloneAuditEvidenceId: evidenceId),
            CancellationToken.None);
        return outcome.Identity.CaseId;
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
                .AttemptAutomaticAsync(services, receiptId, Guid.NewGuid());
            Assert.Equal(IntakeAllocationProjectionStatus.Succeeded, result?.State.Status);
        }

        await using var context = await (await factory.DatabaseAsync).CreateContextAsync();
        var caseId = await context.Cases
            .Where(item => item.OriginIntakeReceiptId == receiptId)
            .Select(item => item.Id)
            .SingleAsync();
        // The Original report cells only: the agreed fee the Case took from
        // its Principal and its Good pre-incident condition (R598) are not
        // the reading's.
        return await context.CaseAssessmentFields
            .Where(item => item.WorkId == caseId
                && item.FieldPath != AssessmentVocabulary.AgreedFee
                && item.FieldPath != AssessmentVocabulary.VehicleCondition)
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

        public Task<OriginalReportRecognition> RecogniseFiledAssetAsync(
            Guid receiptId, IntakeAssetRecord asset, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
