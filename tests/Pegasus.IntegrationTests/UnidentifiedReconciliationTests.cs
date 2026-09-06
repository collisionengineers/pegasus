using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

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

    /// <summary>
    /// Statements 13 and 14 of the INTK-048 preservation table, against the
    /// real query and the real association writer: an automation resolution
    /// follows the receipt's effective destination through link, unlink and
    /// relink, and the sweep returns to all-zero steady state afterwards.
    /// </summary>
    [Fact]
    public async Task AnAutomationResolutionFollowsTheCaseLinkThroughUnlinkAndRelink()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "vehicle.png",
            "image/png",
            TinyPngBytes,
            Guid.NewGuid().ToString("N"));
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        var caseA = await SeedCaseAsync(factory.Services, receiptId, "RECON26001");
        var caseB = await SeedCaseAsync(factory.Services, receiptId, "RECON26002");
        var actor = StaffActor();

        await LinkAsync(factory.Services, receiptId, caseA, actor, "recon-link-a");

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var store = services.GetRequiredService<IUnidentifiedStore>();
        var reconciler = services.GetRequiredService<ReconcileUnidentifiedDestinations>();

        // Forward direction: a manual link on a still-NeedsSorting receipt.
        var linked = await reconciler.ExecuteAsync(50);
        Assert.Equal(1, linked.Resolved);
        var resolvedToA = await store.GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId));
        Assert.Equal(UnidentifiedState.Resolved, resolvedToA!.State);
        Assert.Equal(UnidentifiedResolutionTargetKind.InstructionCase, resolvedToA.ResolutionTargetKind);
        Assert.Equal(caseA.ToString("N"), resolvedToA.ResolutionTargetId);
        Assert.Equal("RECON26001", resolvedToA.ResolutionTargetReference);

        // Statement 14, first half: the resolution is complete, so the queue
        // does not hand the row back and the sweep is quiet.
        Assert.Equal(
            new ReconcileUnidentifiedDestinationsResult(0, 0, 0, 0),
            await reconciler.ExecuteAsync(50));

        // Inverse direction: the association is reversed under a real lease.
        await ReverseAsync(factory.Services, receiptId, caseA, actor, "recon-unlink-a");

        // Statement 16/17's predicate exercised against the REAL query.
        Assert.Single(await store.ListResolutionsToRecheckAsync(50));

        var reopenSweep = await reconciler.ExecuteAsync(50);
        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(1, 0, 1, 0), reopenSweep);
        var reopened = await store.GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId));
        Assert.Equal(UnidentifiedState.Open, reopened!.State);
        Assert.Null(reopened.ResolutionTargetKind);
        Assert.Null(reopened.ResolutionTargetId);
        Assert.Null(reopened.ResolutionTargetReference);
        Assert.Null(reopened.ResolvedBy);
        Assert.Null(reopened.ResolvedAtUtc);

        var afterReopen = await store.HistoryAsync(reopened.Id);
        Assert.Contains(
            afterReopen,
            entry => entry.PreviousState == UnidentifiedState.Resolved
                && entry.NewState == UnidentifiedState.Open);

        // Relink to a different case: the open item follows it.
        await LinkAsync(factory.Services, receiptId, caseB, actor, "recon-link-b");
        var relinked = await reconciler.ExecuteAsync(50);
        Assert.Equal(1, relinked.Resolved);
        var resolvedToB = await store.GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId));
        Assert.Equal(UnidentifiedState.Resolved, resolvedToB!.State);
        Assert.Equal(caseB.ToString("N"), resolvedToB.ResolutionTargetId);
        Assert.Equal("RECON26002", resolvedToB.ResolutionTargetReference);

        // The withdrawn destination stays on the record permanently.
        var history = await store.HistoryAsync(resolvedToB.Id);
        Assert.Contains(history, entry => entry.TargetReference == "RECON26001");
        Assert.Contains(history, entry => entry.TargetReference == "RECON26002");
        Assert.Contains(
            history,
            entry => entry.PreviousState == UnidentifiedState.Resolved
                && entry.NewState == UnidentifiedState.Open);

        // Every transition carries an operation key of its own.
        Assert.Equal(
            history.Count,
            history.Select(entry => entry.OperationKey).Distinct(StringComparer.Ordinal).Count());

        // Statement 14: steady state is all zeros, and stays there.
        Assert.Equal(
            new ReconcileUnidentifiedDestinationsResult(0, 0, 0, 0),
            await reconciler.ExecuteAsync(50));
        Assert.Equal(
            new ReconcileUnidentifiedDestinationsResult(0, 0, 0, 0),
            await reconciler.ExecuteAsync(50));
    }

    /// <summary>
    /// Statements 16 and 17: a completed no-change recheck leaves the bounded,
    /// oldest-first page — without that watermark it holds the head for ever
    /// and every later stale resolution is silently never rechecked.
    /// </summary>
    [Fact]
    public async Task ACompletedRecheckReleasesTheHeadOfTheBoundedPage()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var actor = StaffActor();

        // First receipt: registered as an Image intake, then manually linked to
        // a case. The Image intake keeps precedence, so the recheck concludes
        // "unchanged" — the exact shape that used to re-select for ever.
        var firstUpload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "first.png",
            "image/png",
            TinyPngBytes,
            Guid.NewGuid().ToString("N"));
        var firstReceiptId = IntakeWebDriver.ReceiptId(firstUpload);
        await RegisterImageIntakeAsync(factory.Services, firstReceiptId, "AB12CDE");

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var store = services.GetRequiredService<IUnidentifiedStore>();
        var reconciler = services.GetRequiredService<ReconcileUnidentifiedDestinations>();
        Assert.Equal(1, (await reconciler.ExecuteAsync(50)).Resolved);

        var firstCase = await SeedCaseAsync(factory.Services, firstReceiptId, "RECON26010");
        await LinkAsync(factory.Services, firstReceiptId, firstCase, actor, "recheck-link-first");

        // The association moved, so the row qualifies for a recheck exactly once.
        Assert.Single(await store.ListResolutionsToRecheckAsync(50));

        var unchanged = await reconciler.ExecuteAsync(50);
        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(1, 0, 0, 0), unchanged);
        var stillImageIntake = await store.GetByOriginAsync(UnidentifiedOrigin.Receipt(firstReceiptId));
        Assert.Equal(UnidentifiedResolutionTargetKind.ImageIntake, stillImageIntake!.ResolutionTargetKind);

        // Statement 16: the completed row is gone from the queue.
        Assert.Empty(await store.ListResolutionsToRecheckAsync(50));

        // Statement 17: a second, later resolution goes stale; with a page size
        // of one it is now the head, which the completed row would otherwise
        // have held for ever.
        var secondUpload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "second.png",
            "image/png",
            TinyPngBytes,
            Guid.NewGuid().ToString("N"));
        var secondReceiptId = IntakeWebDriver.ReceiptId(secondUpload);
        var secondCase = await SeedCaseAsync(factory.Services, secondReceiptId, "RECON26011");
        await LinkAsync(factory.Services, secondReceiptId, secondCase, actor, "recheck-link-second");
        Assert.Equal(1, (await reconciler.ExecuteAsync(50)).Resolved);

        var thirdCase = await SeedCaseAsync(factory.Services, secondReceiptId, "RECON26012");
        await ReverseAsync(factory.Services, secondReceiptId, secondCase, actor, "recheck-unlink-second");
        await LinkAsync(factory.Services, secondReceiptId, thirdCase, actor, "recheck-relink-second");

        var head = Assert.Single(await store.ListResolutionsToRecheckAsync(1));
        Assert.Equal(UnidentifiedOrigin.Receipt(secondReceiptId), head.Origin);
    }

    private static ActionActor StaffActor() => ActionActor.Staff(
        DevelopmentOfflineIdentity.AdministratorId,
        [StaffRole.Administrator]);

    private static async Task RegisterImageIntakeAsync(
        IServiceProvider rootServices,
        Guid receiptId,
        string registration)
    {
        await using var scope = rootServices.CreateAsyncScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IImageIntakeOriginResolver>();
        var origin = await resolver.ResolveOriginAsync(receiptId, CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<IRegisterImageIntake>().ExecuteAsync(
            new(
                origin!,
                registration,
                StaffActor(),
                $"unidentified-recheck-register:{receiptId:N}",
                "Staff confirmed the registration from the retained image."),
            CancellationToken.None);
    }

    private static async Task LinkAsync(
        IServiceProvider rootServices,
        Guid receiptId,
        Guid caseId,
        ActionActor actor,
        string operationKey)
    {
        await using var scope = rootServices.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receipt = await services.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        var (caseVersion, lease) = await ClaimLeaseAsync(services, caseId, actor, $"{operationKey}-lease");
        await services.GetRequiredService<ILinkIntake>().ExecuteAsync(
            new(
                receiptId,
                caseId,
                receipt!.Version,
                caseVersion,
                lease.Token,
                actor,
                operationKey,
                "Staff linked the retained material to the case."),
            CancellationToken.None);
    }

    private static async Task ReverseAsync(
        IServiceProvider rootServices,
        Guid receiptId,
        Guid caseId,
        ActionActor actor,
        string operationKey)
    {
        await using var scope = rootServices.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receipt = await services.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        var (caseVersion, lease) = await ClaimLeaseAsync(services, caseId, actor, $"{operationKey}-lease");
        await services.GetRequiredService<IReverseIntakeLink>().ExecuteAsync(
            new(
                receiptId,
                caseId,
                receipt!.Version,
                caseVersion,
                lease.Token,
                actor,
                operationKey,
                "Staff reversed the link to the case."),
            CancellationToken.None);
    }

    private static async Task<(long Version, CaseEditLease Lease)> ClaimLeaseAsync(
        IServiceProvider services,
        Guid caseId,
        ActionActor actor,
        string operationKey)
    {
        var workflow = await services.GetRequiredService<ICaseWorkflowQueries>()
            .GetAsync(caseId, CancellationToken.None);
        var lease = await services.GetRequiredService<ILeaseCaseForEdit>().ClaimAsync(
            new ClaimCaseEditLeaseRequest(caseId, workflow!.Version, actor, operationKey),
            CancellationToken.None);
        return (workflow.Version, lease);
    }

    private static async Task<Guid> SeedCaseAsync(
        IServiceProvider rootServices,
        Guid originReceiptId,
        string reference)
    {
        await using var scope = rootServices.CreateAsyncScope();
        var contextFactory =
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var now = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {$"Reconciliation provider {reference}"}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {now})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version) VALUES ({principalId}, {organizationId}, {reference}, {lineageId}, {true}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, InstructionConfirmedByStaff, ImagesConfirmedByStaff, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2031}, {1}, {reference}, {"inspection"}, {"not_ready"}, {"pending"}, {originReceiptId}, {true}, {true}, {true}, {true}, {now}, {0L}, {Guid.NewGuid()})");
        // CaseWorkflows.State is the CaseLifecycleState ENUM NAME - the store
        // reads it with Enum.Parse. Cases.InitialState is a different, snake_case
        // code vocabulary; writing that spelling here made every read of the case
        // throw "Requested value 'not_ready' was not found".
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({caseId}, {nameof(CaseLifecycleState.NotReady)}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseDataSnapshots (CaseId, OriginIntakeReceiptId, OriginSourceChannel, OriginExternalReceiptToken, OriginSourceHash, OriginReceivedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, CompletenessPolicyKey, CompletenessPolicyVersion, CompletenessPolicySatisfied, AcceptedAtUtc) VALUES ({caseId}, {originReceiptId}, {"manual_upload"}, {reference}, {1.ToString("X64", CultureInfo.InvariantCulture)}, {now}, {"unidentified-reconcile-reader"}, {"1"}, {"unidentified-reconcile-fixture"}, {1}, {reference}, {1}, {true}, {now})");
        return caseId;
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
