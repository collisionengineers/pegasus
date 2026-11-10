using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.PhotoCapture;
using Pegasus.Core.Triage;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The settle and the Intake log of a link sent from a Case (FRD-28;
/// operator, 10 November 2026): a Triage Case takes the customer's note as
/// its own Triage note (R845), and a Case link's photo names the link sent
/// from the Case as its decision (R805).
/// </summary>
public sealed partial class PhotoCaptureWebTests
{
    /// <summary>
    /// A link sent from a Triage Case: once its photo has filed and the
    /// sender has pressed Send, the settle writes the customer's note as one
    /// of the Triage Case's own notes, with the words a Case note gets, once,
    /// and leaves the Triage's version alone (R845).
    /// </summary>
    [Fact]
    public async Task ALinkSentFromATriageCaseWritesTheCustomersNoteAsATriageNote()
    {
        const string note = "Please call before 5pm.";
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await MailboxIntakeTestData.SeedTriageCaseAsync(factory.Services, originReceiptId: null, "t.QDOS31801");
        await using (var db = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            // The Triage Case's evidence folder, as a seeded Case has one.
            Assert.Equal(1, await db.Cases.Where(value => value.Id == caseId).ExecuteUpdateAsync(update => update
                .SetProperty(value => value.CustodyRootRemoteId, "case-root")
                .SetProperty(value => value.CustodyState, "confirmed")));
        }

        var link = await IssueLinkAsync(factory, caseId);
        await SendAndDrainAsync(factory, client, link, FirstOverview, seed: 1);
        await StandingCaptureDriver.CompleteAsync(client, link.Token.Value, note);
        var versionBefore = await TriageVersionAsync(factory, caseId);

        await StandingCaptureDriver.SettleAsync(factory);
        await StandingCaptureDriver.SettleAsync(factory);

        Assert.Equal(1, (await StandingCaptureDriver.CaptureAsync(factory, link.Capture.Id)).SettledBatches);
        await using (var db = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            var written = Assert.Single(await db.TriageHistory.AsNoTracking()
                .Where(entry => entry.TriageCaseId == caseId && entry.EventType == TriageNotes.EventType)
                .Select(entry => new { entry.Reason, entry.ActorKind, entry.Actor })
                .ToListAsync());
            Assert.Equal("Customer's note (photo link): " + note, written.Reason);
            Assert.Equal(nameof(ActorKind.SystemWorker), written.ActorKind);
            Assert.Equal(PhotoCaptureIntakeRules.SettleActorId, written.Actor);
        }

        Assert.Empty(await StandingCaptureDriver.CaseEventReasonsAsync(factory, caseId, AddCaseNoteEventType));
        Assert.Equal(versionBefore, await TriageVersionAsync(factory, caseId));
    }

    /// <summary>
    /// A Case link's photo gives the Intake log the sentence R805 fixed for a
    /// link sent from the Case, not a staff upload's.
    /// </summary>
    [Fact]
    public async Task ACaseLinksPhotoNamesTheLinkSentFromTheCaseAsItsDecision()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);

        var item = await SendAndDrainAsync(factory, client, link, FirstOverview, seed: 1);

        await using var scope = factory.Services.CreateAsyncScope();
        var receipt = await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>().GetAsync(
                item.IntakeReceiptId ?? throw new InvalidOperationException("The item did not learn its processed receipt."),
                CancellationToken.None)
            ?? throw new InvalidOperationException("The photo's receipt was not recorded.");
        Assert.Equal(PhotoCaptureIntakeRules.CaseLinkDestinationReason, receipt.DecisionReason);
    }

    private static async Task<long> TriageVersionAsync(IntakeWebApplicationFactory factory, Guid caseId)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        return await db.Triage.Where(value => value.CaseId == caseId).Select(value => value.Version).SingleAsync();
    }
}
