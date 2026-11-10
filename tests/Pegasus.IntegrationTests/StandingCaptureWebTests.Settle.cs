using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Documents;
using Pegasus.Core.PhotoCapture;

namespace Pegasus.IntegrationTests;

/// <summary>
/// A capture's Vehicle images records reaching a Case (FRD-28; operator,
/// 10 November 2026): the kept paperwork follows the record's merge wherever
/// the photos went (R844), and a capture completes the Case's images once,
/// so a later record of the same capture never completes them again (R841).
/// </summary>
public sealed partial class StandingCaptureWebTests
{
    private const string MergedPhotographsCompletedEventType = "case_images_completed_from_merged_photographs";

    /// <summary>
    /// A record Pegasus paired with a Case whose report has since been sent:
    /// the batch's kept paperwork files there all the same, as the work that
    /// follows the record's merge (R844).
    /// </summary>
    [Fact]
    public async Task APegasusPairingFilesTheKeptPaperworkOnAReportSentCase()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var started = await StandingCaptureDriver.StartAsync(factory, client, link, UnheldRegistration);
        var (_, _, paperwork) = await SendPhotographsAndPaperworkAsync(factory, client, started);
        var paperworkReceipt = await ReceiptOfAsync(factory, paperwork);
        await StandingCaptureDriver.CompleteAsync(client, started.Token);
        await SettleAndReconcileAsync(factory);
        var groupId = PhotoCaptureIntakeRules.BatchGroupId(started.Capture.Id, 1);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        await StandingCaptureDriver.SetCaseRegistrationAsync(factory, caseId, "ZZ99QQQ");
        await CreateCaseCustodyRootAsync(factory, caseId);
        await ImageIntakeTestData.ProcessImageCaseCustodyAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            // The pairing a newly accepted Case runs.
            await scope.ServiceProvider.GetRequiredService<Pegasus.Core.ImageIntake.IImageIntakeCasePairing>()
                .PairAcceptedCaseAsync(caseId, CancellationToken.None);
        }

        Assert.Equal(caseId, (await StandingCaptureDriver.RecordForGroupAsync(factory, groupId)).MergedIntoCaseId);
        await ImageIntakeTestData.ProcessCustodyAsync(factory, Pegasus.Core.Custody.ExternalWorkKinds.MergeImageCaseCustody);
        await PhotoCaptureWebTests.MarkReportSentAsync(factory, caseId);

        await StandingCaptureDriver.SettleAsync(factory);
        await StandingCaptureDriver.SettleAsync(factory);

        var association = await StandingCaptureDriver.AssociationAsync(factory, paperworkReceipt);
        Assert.Equal(caseId, association.CaseId);
        Assert.Equal(PhotoCaptureIntakeRules.ReachActorId, association.ActorSubjectId);
        Assert.True(await StandingCaptureDriver.FiledOnAsync(factory, paperworkReceipt, caseId));
        Assert.Equal(DocumentSemanticRole.Other, await KeptFileRoleAsync(factory, caseId, paperwork.FileName));
        Assert.NotNull(Assert.Single(await StandingCaptureDriver.BatchesAsync(factory, started.Capture.Id)).ReachedAtUtc);
    }

    /// <summary>
    /// A record that merged into a Triage Case with no member of staff's
    /// decision: the batch's kept paperwork files on the Triage Case, where
    /// the photos went (R844). The merge is recorded on the record as the
    /// pairing records it.
    /// </summary>
    [Fact]
    public async Task ARecordMergedIntoATriageCaseFilesItsKeptPaperworkThere()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var started = await StandingCaptureDriver.StartAsync(factory, client, link, UnheldRegistration);
        var (_, _, paperwork) = await SendPhotographsAndPaperworkAsync(factory, client, started);
        var paperworkReceipt = await ReceiptOfAsync(factory, paperwork);
        await StandingCaptureDriver.CompleteAsync(client, started.Token);
        await SettleAndReconcileAsync(factory);
        var record = await StandingCaptureDriver.RecordForGroupAsync(
            factory, PhotoCaptureIntakeRules.BatchGroupId(started.Capture.Id, 1));
        var triageCaseId = await MailboxIntakeTestData.SeedTriageCaseAsync(factory.Services, originReceiptId: null, "t.QDOS31802");
        await CreateCaseCustodyRootAsync(factory, triageCaseId);
        await using (var db = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            Assert.Equal(1, await db.ImageIntakes.Where(value => value.Id == record.Id).ExecuteUpdateAsync(update => update
                .SetProperty(value => value.MergedIntoCaseId, (Guid?)triageCaseId)));
        }

        await StandingCaptureDriver.SettleAsync(factory);
        await StandingCaptureDriver.SettleAsync(factory);

        var association = await StandingCaptureDriver.AssociationAsync(factory, paperworkReceipt);
        Assert.Equal(triageCaseId, association.CaseId);
        Assert.Equal(nameof(Pegasus.Core.Identity.ActorKind.SystemWorker), association.ActorKind);
        Assert.Equal(PhotoCaptureIntakeRules.ReachActorId, association.ActorSubjectId);
        Assert.True(await StandingCaptureDriver.FiledOnAsync(factory, paperworkReceipt, triageCaseId));
        Assert.Equal(DocumentSemanticRole.Other, await KeptFileRoleAsync(factory, triageCaseId, paperwork.FileName));
        Assert.NotNull(Assert.Single(await StandingCaptureDriver.BatchesAsync(factory, started.Capture.Id)).ReachedAtUtc);
    }

    /// <summary>
    /// Two batches of one capture make two records. The first record's merge
    /// completes the Case's images and spends the capture's one
    /// images-complete; staff then mark them incomplete, and the second
    /// record's merge leaves them so (R792, R841).
    /// </summary>
    [Fact]
    public async Task ALaterRecordOfTheSameCaptureNeverCompletesTheImagesAgain()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        var started = await StandingCaptureDriver.StartAsync(factory, client, link, UnheldRegistration);
        var first = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, FirstOverview, MultiFormatFixture.Photograph(1), "image/jpeg");
        var firstReceipt = await StandingCaptureDriver.DrainAsync(factory, first);
        await StandingCaptureDriver.CompleteAsync(client, started.Token);
        await SettleAndReconcileAsync(factory);
        await StandingCaptureDriver.MoveLastSettleBackAsync(factory, started.Capture.Id);
        var further = await StandingCaptureDriver.SendItemAsync(
            factory, client, started, SecondOverview, MultiFormatFixture.Photograph(2), "image/jpeg");
        Assert.Equal(2, further.Batch);
        var furtherReceipt = await StandingCaptureDriver.DrainAsync(factory, further);
        await StandingCaptureDriver.CompleteAsync(client, started.Token);
        await SettleAndReconcileAsync(factory);
        var firstRecord = await StandingCaptureDriver.RecordForGroupAsync(
            factory, PhotoCaptureIntakeRules.BatchGroupId(started.Capture.Id, 1));
        var secondRecord = await StandingCaptureDriver.RecordForGroupAsync(
            factory, PhotoCaptureIntakeRules.BatchGroupId(started.Capture.Id, 2));
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        await CreateCaseCustodyRootAsync(factory, caseId);
        await ImageIntakeTestData.ProcessImageCaseCustodyAsync(factory);

        await MergeRecordIntoCaseAsync(factory, firstRecord.Id, [firstReceipt], caseId);
        await ImageIntakeTestData.ProcessCustodyAsync(factory, Pegasus.Core.Custody.ExternalWorkKinds.MergeImageCaseCustody);

        Assert.True(await StandingCaptureDriver.ImagesCompleteAsync(factory, caseId));
        Assert.True((await StandingCaptureDriver.CaptureAsync(factory, started.Capture.Id)).ImagesCompletedAtUtc.HasValue);
        await using (var db = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            // Staff mark the images incomplete; nothing on the capture changes.
            await db.Cases.Where(value => value.Id == caseId).ExecuteUpdateAsync(update => update
                .SetProperty(value => value.ImagesComplete, false));
        }

        await MergeRecordIntoCaseAsync(factory, secondRecord.Id, [furtherReceipt], caseId);
        await ImageIntakeTestData.ProcessCustodyAsync(factory, Pegasus.Core.Custody.ExternalWorkKinds.MergeImageCaseCustody);

        Assert.Equal(caseId, (await StandingCaptureDriver.RecordForGroupAsync(
            factory, PhotoCaptureIntakeRules.BatchGroupId(started.Capture.Id, 2))).MergedIntoCaseId);
        Assert.False(await StandingCaptureDriver.ImagesCompleteAsync(factory, caseId));
        Assert.Single(await StandingCaptureDriver.CaseEventReasonsAsync(factory, caseId, MergedPhotographsCompletedEventType));
    }
}
