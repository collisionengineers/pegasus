using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
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
        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(0, 0, 0), second);
    }

    [Fact]
    public async Task SweepResolvesAnOpenItemWhoseReceiptWasManuallyLinkedToACase()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "XY34 ZZZ", "UNIDENTIFIED-MANUAL-LINK-01");
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
            caseId,
            null,
            "Staff matched the retained material to the instructed case.",
            actor);
        Assert.True(attach.Succeeded, attach.Message);

        var receipt = await services.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        Assert.NotNull(receipt);
        Assert.Equal(IntakeDecision.NeedsSorting, receipt!.Decision);
        Assert.Equal(caseId, receipt.CurrentCaseId);

        var stillOpen = await unidentifiedStore.GetByOriginAsync(
            UnidentifiedOrigin.Receipt(receiptId));
        Assert.Equal(UnidentifiedState.Open, stillOpen!.State);

        var reconciler = services.GetRequiredService<ReconcileUnidentifiedDestinations>();
        var result = await reconciler.ExecuteAsync(50);
        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(1, 1, 0), result);

        var resolved = await unidentifiedStore.GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId));
        Assert.Equal(UnidentifiedState.Resolved, resolved!.State);
        Assert.Equal(UnidentifiedResolutionTargetKind.InstructionCase, resolved.ResolutionTargetKind);
        Assert.Equal(caseId.ToString("N"), resolved.ResolutionTargetId);
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
            Assert.Equal(caseId, association.CaseId);
            Assert.True(await context.CaseWorkflowEvents.AnyAsync(
                item => item.CaseId == caseId
                    && item.EventType == "intake_case_linked"
                    && item.OperationKey == $"upload-attach:{receiptId:N}:{caseId:N}"));
        }

        var second = await reconciler.ExecuteAsync(50);
        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(0, 0, 0), second);
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
