using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// INTK-018: the production U7 shape — an Unidentified item whose origin
/// receipt is later promoted to a real destination must be resolved by the
/// product's own reconciliation, never manual SQL — and the terminal-only
/// creation contract: a group member whose group is still pending never
/// gains an Unidentified row.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class UnidentifiedReconciliationTests
{
    private static readonly byte[] TinyPngBytes = Convert.FromBase64String(MultiFormatFixture.TinyPngBase64);

    [Fact]
    public async Task SweepResolvesAnOpenItemWhoseReceiptWasPromotedToAnImageIntake()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);

        // A single image with no readable plate: processing parks it at
        // needs_sorting and registers the Unidentified item.
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "vehicle.png",
            "image/png",
            TinyPngBytes,
            Guid.NewGuid().ToString("N"));
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var unidentifiedStore = services.GetRequiredService<IUnidentifiedStore>();
        var open = await unidentifiedStore.GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId));
        Assert.NotNull(open);
        Assert.Equal(UnidentifiedState.Open, open!.State);

        // The receipt is promoted outside any processing pass of its own —
        // exactly how production's U7 receipt became AU17SEO-01 while U7
        // stayed open.
        var resolver = services.GetRequiredService<IImageIntakeOriginResolver>();
        var register = services.GetRequiredService<IRegisterImageIntake>();
        var origin = await resolver.ResolveOriginAsync(receiptId, CancellationToken.None);
        var record = await register.ExecuteAsync(
            new(
                origin!,
                "AB12CDE",
                ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]),
                $"unidentified-reconcile-register:{receiptId:N}",
                "Staff confirmed the registration from the retained image."),
            CancellationToken.None);
        var stillOpen = await unidentifiedStore.GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId));
        Assert.Equal(UnidentifiedState.Open, stillOpen!.State);

        // The sweep is the product's own recovery.
        var reconciler = services.GetRequiredService<ReconcileUnidentifiedDestinations>();
        var result = await reconciler.ExecuteAsync(50);
        Assert.Equal(1, result.Resolved);
        Assert.Equal(0, result.Failures);

        var resolved = await unidentifiedStore.GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId));
        Assert.Equal(UnidentifiedState.Resolved, resolved!.State);
        Assert.Equal(UnidentifiedResolutionTargetKind.ImageIntake, resolved.ResolutionTargetKind);
        Assert.Equal(record.Id.ToString("N"), resolved.ResolutionTargetId);
        Assert.Equal(record.ImageIntakeReference, resolved.ResolutionTargetReference);

        // The destination is recorded permanently in the item's history.
        var history = await unidentifiedStore.HistoryAsync(resolved.Id);
        Assert.Contains(
            history,
            entry => entry.NewState == UnidentifiedState.Resolved
                && entry.TargetKind == UnidentifiedResolutionTargetKind.ImageIntake
                && entry.TargetReference == record.ImageIntakeReference);

        // Replay-safe: a second sweep finds nothing left to resolve.
        var second = await reconciler.ExecuteAsync(50);
        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(0, 0, 0, 0), second);
    }

    [Fact]
    public async Task SweepFollowsAManuallyLinkedReceiptThroughUnlinkAndRelink()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseA = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "XY34 ZZZ", "UNIDENTIFIED-MANUAL-LINK-01");
        var caseB = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "XY35 ZZZ", "UNIDENTIFIED-MANUAL-LINK-02");
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "vehicle.png",
            "image/png",
            TinyPngBytes,
            Guid.NewGuid().ToString("N"));
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var unidentifiedStore = services.GetRequiredService<IUnidentifiedStore>();
        var open = await unidentifiedStore.GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId));
        Assert.NotNull(open);
        Assert.Equal(UnidentifiedState.Open, open!.State);

        var actor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
        var attach = await services.GetRequiredService<IUploadCaseDecision>().AttachAsync(
            receiptId,
            caseA,
            null,
            "Staff matched the retained material to the instructed case.",
            actor);
        Assert.True(attach.Succeeded, attach.Message);

        var receipt = await services.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        Assert.NotNull(receipt);
        Assert.Equal(IntakeDecision.NeedsSorting, receipt!.Decision);
        Assert.Equal(caseA, receipt.CurrentCaseId);

        var stillOpen = await unidentifiedStore.GetByOriginAsync(
            UnidentifiedOrigin.Receipt(receiptId));
        Assert.Equal(UnidentifiedState.Open, stillOpen!.State);

        var reconciler = services.GetRequiredService<ReconcileUnidentifiedDestinations>();
        var result = await reconciler.ExecuteAsync(50);
        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(1, 1, 0, 0), result);

        var resolved = await unidentifiedStore.GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId));
        Assert.Equal(UnidentifiedState.Resolved, resolved!.State);
        Assert.Equal(UnidentifiedResolutionTargetKind.InstructionCase, resolved.ResolutionTargetKind);
        Assert.Equal(caseA.ToString("N"), resolved.ResolutionTargetId);
        Assert.Equal(receipt.CurrentCaseReference, resolved.ResolutionTargetReference);
        Assert.Contains(
            await unidentifiedStore.HistoryAsync(resolved.Id),
            entry => entry.NewState == UnidentifiedState.Resolved
                && entry.TargetKind == UnidentifiedResolutionTargetKind.InstructionCase
                && entry.TargetReference == receipt.CurrentCaseReference);

        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var association = await context.IntakeManualAssociations
                .SingleAsync(item => item.IntakeReceiptId == receiptId);
            Assert.True(association.IsActive);
            Assert.Equal(caseA, association.CaseId);
            var linkedCase = await context.Cases.SingleAsync(item => item.Id == caseA);
            Assert.NotEqual(receiptId, linkedCase.OriginIntakeReceiptId);
            Assert.True(await context.CaseWorkflowEvents.AnyAsync(
                item => item.CaseId == caseA
                    && item.EventType == "intake_case_linked"
                    && item.OperationKey == $"upload-attach:{receiptId:N}:{caseA:N}"));
        }

        var caseDetails = await services.GetRequiredService<IGetCase>().ExecuteAsync(
            new(caseA, actor),
            CancellationToken.None);
        var lease = await services.GetRequiredService<IAcquireCaseEditLease>().ExecuteAsync(
            new(caseA, caseDetails!.Workflow.Version, actor, "unidentified-manual-unlink-lease"),
            CancellationToken.None);
        await services.GetRequiredService<IReverseIntakeLink>().ExecuteAsync(
            new(
                receiptId,
                caseA,
                receipt.Version,
                lease.Version,
                lease.Token,
                actor,
                "unidentified-manual-unlink",
                "The retained material was linked to the wrong instructed case."),
            CancellationToken.None);

        var unlinkedReceipt = await services.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        Assert.NotNull(unlinkedReceipt);
        Assert.Null(unlinkedReceipt!.CurrentCaseId);
        var reopenedResult = await reconciler.ExecuteAsync(50);
        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(0, 0, 1, 0), reopenedResult);
        var reopened = await unidentifiedStore.GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId));
        Assert.Equal(UnidentifiedState.Open, reopened!.State);
        Assert.Null(reopened.ResolvedAtUtc);
        Assert.Null(reopened.ResolvedBy);
        Assert.Null(reopened.ResolutionReason);
        Assert.Null(reopened.ResolutionTargetKind);
        Assert.Null(reopened.ResolutionTargetId);
        Assert.Null(reopened.ResolutionTargetReference);
        Assert.Contains(
            await unidentifiedStore.HistoryAsync(reopened.Id),
            entry => entry.PreviousState == UnidentifiedState.Resolved
                && entry.NewState == UnidentifiedState.Open);

        var relink = await services.GetRequiredService<IUploadCaseDecision>().AttachAsync(
            receiptId,
            caseB,
            null,
            "Staff corrected the retained material's instructed case.",
            actor);
        Assert.True(relink.Succeeded, relink.Message);
        var relinkedReceipt = await services.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        Assert.NotNull(relinkedReceipt);
        var retargetedResult = await reconciler.ExecuteAsync(50);
        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(1, 1, 0, 0), retargetedResult);
        var retargeted = await unidentifiedStore.GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId));
        Assert.Equal(UnidentifiedState.Resolved, retargeted!.State);
        Assert.Equal(caseB.ToString("N"), retargeted.ResolutionTargetId);
        Assert.Equal(relinkedReceipt!.CurrentCaseReference, retargeted.ResolutionTargetReference);
        var history = await unidentifiedStore.HistoryAsync(retargeted.Id);
        Assert.Contains(history, entry =>
            entry.NewState == UnidentifiedState.Resolved
            && entry.TargetId == caseA.ToString("N"));
        Assert.Contains(history, entry =>
            entry.NewState == UnidentifiedState.Resolved
            && entry.TargetId == caseB.ToString("N"));

        var final = await reconciler.ExecuteAsync(50);
        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(0, 0, 0, 0), final);
    }

    [Fact]
    public async Task StaffOpeningTheTriageOfALinkedReceiptRetargetsTheRecordedDestination()
    {
        // INTK-048 regression. Correcting a destination does not mutate the
        // origin receipt, so a reopen/re-resolve pair keyed on the receipt
        // rebuilt the key the first resolution had already used: the store
        // rejected it, the advisory catch swallowed the conflict, and the item
        // was left Open with no destination beside a live Triage — the two
        // queues INTK-033 closed — with every later sweep failing on the same
        // taken key forever.
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "XY36 ZZZ", "UNIDENTIFIED-TRIAGE-LINK-01");
        var email = IntakeTestEvidence.CreateEmail(
            "triage-linked-then-opened.eml",
            "Triage Only Request" + Environment.NewLine + Environment.NewLine
                + "Please find attached our client's images.");
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory, client, email.FileName, email.MediaType, email.Content);
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        long receiptVersionBeforeTriage;
        await using (var before = factory.Services.CreateAsyncScope())
        {
            var services = before.ServiceProvider;
            var unidentifiedStore = services.GetRequiredService<IUnidentifiedStore>();
            var open = Assert.IsType<UnidentifiedItem>(
                await unidentifiedStore.GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId)));
            Assert.Equal(UnidentifiedState.Open, open.State);

            var attach = await services.GetRequiredService<IUploadCaseDecision>().AttachAsync(
                receiptId,
                caseId,
                null,
                "Staff matched the retained Triage request to the instructed case.",
                ActionActor.Staff(
                    DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]));
            Assert.True(attach.Succeeded, attach.Message);

            // The sweep records the only destination the receipt has so far.
            var reconciler = services.GetRequiredService<ReconcileUnidentifiedDestinations>();
            Assert.Equal(
                new ReconcileUnidentifiedDestinationsResult(1, 1, 0, 0),
                await reconciler.ExecuteAsync(50));
            var linked = Assert.IsType<UnidentifiedItem>(
                await unidentifiedStore.GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId)));
            Assert.Equal(UnidentifiedState.Resolved, linked.State);
            Assert.Equal(UnidentifiedResolutionTargetKind.InstructionCase, linked.ResolutionTargetKind);
            Assert.Equal(caseId.ToString("N"), linked.ResolutionTargetId);

            receiptVersionBeforeTriage = Assert.IsType<IntakeReceipt>(
                await services.GetRequiredService<IIntakeReceiptQueries>()
                    .GetAsync(receiptId, CancellationToken.None)).Version;
        }

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await IntakeWebDriver.GetAntiforgeryTokenAsync(client),
            ["vehicleRegistration"] = "vn64 wng",
            ["operationKey"] = Guid.NewGuid().ToString("N")
        });
        using var opened = await client.PostAsync($"/Received/{receiptId}?handler=OpenTriage", form);
        Assert.Equal(HttpStatusCode.Redirect, opened.StatusCode);

        await using var after = factory.Services.CreateAsyncScope();
        var afterServices = after.ServiceProvider;
        var triage = Assert.Single(
            await afterServices.GetRequiredService<ITriageQueries>()
                .ListAsync(null, CancellationToken.None));

        // The precondition the defect needed: opening the Triage leaves the
        // receipt untouched, so nothing about it discriminates the correction
        // from the resolution that preceded it.
        var receiptAfterTriage = Assert.IsType<IntakeReceipt>(
            await afterServices.GetRequiredService<IIntakeReceiptQueries>()
                .GetAsync(receiptId, CancellationToken.None));
        Assert.Equal(receiptVersionBeforeTriage, receiptAfterTriage.Version);

        var store = afterServices.GetRequiredService<IUnidentifiedStore>();
        var retargeted = Assert.IsType<UnidentifiedItem>(
            await store.GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId)));
        Assert.Equal(UnidentifiedState.Resolved, retargeted.State);
        Assert.Equal(UnidentifiedResolutionTargetKind.Triage, retargeted.ResolutionTargetKind);
        Assert.Equal(triage.Id.ToString("N"), retargeted.ResolutionTargetId);
        Assert.Equal("VN64WNG", retargeted.ResolutionTargetReference);

        // Both destinations are on the record, and the reopen between them.
        var history = await store.HistoryAsync(retargeted.Id);
        Assert.Contains(history, entry =>
            entry.NewState == UnidentifiedState.Resolved
            && entry.TargetKind == UnidentifiedResolutionTargetKind.InstructionCase);
        Assert.Contains(history, entry =>
            entry.PreviousState == UnidentifiedState.Resolved
            && entry.NewState == UnidentifiedState.Open);
        Assert.Contains(history, entry =>
            entry.NewState == UnidentifiedState.Resolved
            && entry.TargetKind == UnidentifiedResolutionTargetKind.Triage);
        Assert.Equal(
            history.Select(entry => entry.OperationKey).Distinct(StringComparer.Ordinal).Count(),
            history.Count);

        // Nothing is left for the sweep to fail on, pass after pass.
        var sweep = afterServices.GetRequiredService<ReconcileUnidentifiedDestinations>();
        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(0, 0, 0, 0), await sweep.ExecuteAsync(50));
        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(0, 0, 0, 0), await sweep.ExecuteAsync(50));
    }

    [Fact]
    public async Task APendingGroupMemberNeverGainsAnUnidentifiedRow()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine("AB12CDE"));
        using var client = IntakeWebDriver.CreateClient(factory);
        var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);
        var upload = await IntakeWebDriver.PostUploadManyAsync(
            client,
            form.AntiforgeryToken,
            form.ExternalReceiptToken,
            [
                ("overview.png", "image/png", TinyPngBytes),
                ("close-up.png", "image/png", TinyPngBytes)
            ]);
        var groupId = Guid.Parse(upload.Location!.OriginalString.Split('/').Last());

        Guid[] stagedReceiptIds;
        await using (var lookupScope = factory.Services.CreateAsyncScope())
        {
            var groups = lookupScope.ServiceProvider.GetRequiredService<IIntakeSubmissionGroupStore>();
            var group = await groups.GetAsync(groupId)
                ?? throw new InvalidOperationException("The submission group was not persisted.");
            stagedReceiptIds = group.Members
                .OrderBy(member => member.Ordinal)
                .Select(member => member.StagedReceiptId)
                .ToArray();
        }

        // Move both members past pending->dispatched without processing, so
        // each ProcessQueuedIntake call below is one specific member's pass.
        await using (var dispatchScope = factory.Services.CreateAsyncScope())
        {
            var dispatcher = new DispatchPendingIntakeWork(
                dispatchScope.ServiceProvider.GetRequiredService<IIntakeWorkStore>(),
                new IntakeWebDriver.NoOpIntakeWorkEnqueuer(),
                dispatchScope.ServiceProvider.GetRequiredService<TimeProvider>());
            await dispatcher.ExecuteAsync(10);
        }

        // Process only the second member: its sibling's receipt does not
        // exist yet, so the group defers (GroupPending). The pending window
        // must not surface anything as Unidentified.
        await using (var firstScope = factory.Services.CreateAsyncScope())
        {
            var processor = IntakeWebDriver.CreateProcessor(firstScope.ServiceProvider);
            await processor.ExecuteAsync(stagedReceiptIds[1]);
            var openDuringPendingWindow = await firstScope.ServiceProvider
                .GetRequiredService<IUnidentifiedStore>()
                .ListAsync(UnidentifiedState.Open);
            Assert.Empty(openDuringPendingWindow);
        }

        // Complete the group: the sibling processes and the deferred member
        // is re-driven through the safe replay branch. The group resolves to
        // registration, so no member ever appears as Unidentified.
        await using (var secondScope = factory.Services.CreateAsyncScope())
        {
            var processor = IntakeWebDriver.CreateProcessor(secondScope.ServiceProvider);
            await processor.ExecuteAsync(stagedReceiptIds[0]);
            await processor.ExecuteAsync(stagedReceiptIds[1]);
        }

        await using var assertScope = factory.Services.CreateAsyncScope();
        var openAfterResolution = await assertScope.ServiceProvider
            .GetRequiredService<IUnidentifiedStore>()
            .ListAsync(UnidentifiedState.Open);
        Assert.Empty(openAfterResolution);
    }
}
