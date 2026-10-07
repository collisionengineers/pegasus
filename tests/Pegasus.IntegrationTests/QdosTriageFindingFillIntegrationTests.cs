using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

public sealed partial class QdosTriageIntegrationTests
{
    /// <summary>
    /// QDOS26080: a linked Triage's first finding fills the Case's empty
    /// Roadworthiness and repair outcome once (operator, 7 October 2026). A
    /// value staff entered is kept, and a superseding finding changes nothing
    /// on the Case.
    /// </summary>
    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task ALinkedTriagesFirstFindingFillsOnlyTheCasesEmptyFindings()
    {
        using var factory = new IntakeWebApplicationFactory();
        var email = IntakeTestEvidence.CreateEngineerTriageRequest("triage-finding-fill.eml");
        _ = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, email);
        var triage = (await GetOnlyTriageAsync(factory.Services)).Record;
        var caseId = await SeedMatchingFormalCaseAsync(factory.Services, triage.Origin!.ReceiptId);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var staff = ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);
        Assert.Equal(
            new TriageCasePairingResult(1, 1, 0),
            await services.GetRequiredService<ITriageCasePairing>().ReconcileAsync(1, CancellationToken.None));
        await using var context = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
        // Linked with no finding: nothing to fill.
        Assert.False(await context.CaseAssessmentFields.AnyAsync(item => item.WorkId == caseId));
        Assert.Equal(1, (await context.CaseWorkflows.AsNoTracking().SingleAsync(item => item.CaseId == caseId)).Version);

        context.CaseAssessmentFields.Add(new()
        {
            WorkId = caseId,
            FieldPath = AssessmentVocabulary.Outcome,
            Value = "total_loss",
            RecordedByKind = nameof(ActorKind.Staff),
            RecordedBy = DevelopmentOfflineIdentity.AdministratorId.ToString("D"),
            RecordedAtUtc = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        async Task<string> ClaimEditAsync(long expectedVersion, string operationKey) =>
            (await services.GetRequiredService<IEditScopeLeases>().ClaimAsync(
                new(EditScopeKind.Triage, triage.CaseId, expectedVersion, staff, operationKey),
                CancellationToken.None)).Token;

        var linkedVersion = (await GetTriageAsync(factory.Services, triage.CaseId)).Record.Version;
        await services.GetRequiredService<IRecordTriageFinding>().ExecuteAsync(
            new RecordTriageFindingRequest(
                triage.CaseId, linkedVersion, staff, "fill-first-finding", "Seen in the images",
                RoadworthinessFinding.Roadworthy, AssessmentFinding.Repairable, null)
            {
                EditLeaseToken = await ClaimEditAsync(linkedVersion, "fill-first-finding-edit")
            },
            CancellationToken.None);

        async Task<Dictionary<string, (string Value, string Kind, string By)>> ReadFieldsAsync()
        {
            await using var read = await services
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
            return await read.CaseAssessmentFields.AsNoTracking()
                .Where(item => item.WorkId == caseId)
                .ToDictionaryAsync(
                    item => item.FieldPath,
                    item => (item.Value, item.RecordedByKind, item.RecordedBy));
        }

        var afterFirst = await ReadFieldsAsync();
        Assert.Equal(
            ("roadworthy", nameof(ActorKind.Automation), TriageFindingFill.RecorderId),
            afterFirst[AssessmentVocabulary.LegalStatus]);
        // Staff's outcome is never replaced by the finding's.
        Assert.Equal("total_loss", afterFirst[AssessmentVocabulary.Outcome].Value);
        Assert.Equal(nameof(ActorKind.Staff), afterFirst[AssessmentVocabulary.Outcome].Kind);
        var fill = await context.CaseWorkflowEvents.AsNoTracking()
            .SingleAsync(item => item.CaseId == caseId && item.EventType == "triage_finding_filled");
        Assert.Equal(1, fill.BeforeVersion);
        Assert.Equal(2, fill.AfterVersion);

        var detail = await GetTriageAsync(factory.Services, triage.CaseId);
        await services.GetRequiredService<ISupersedeTriageFinding>().ExecuteAsync(
            new RecordTriageFindingRequest(
                triage.CaseId, detail.Record.Version, staff, "fill-superseding-finding", "Corrected",
                RoadworthinessFinding.Unroadworthy, AssessmentFinding.TotalLoss, Assert.Single(detail.Findings).Id)
            {
                EditLeaseToken = await ClaimEditAsync(detail.Record.Version, "fill-superseding-finding-edit")
            },
            CancellationToken.None);

        Assert.Equal(afterFirst, await ReadFieldsAsync());
        Assert.Equal(2, (await context.CaseWorkflows.AsNoTracking().SingleAsync(item => item.CaseId == caseId)).Version);
        Assert.Equal(1, await context.CaseWorkflowEvents.CountAsync(
            item => item.CaseId == caseId && item.EventType == "triage_finding_filled"));
    }
}
