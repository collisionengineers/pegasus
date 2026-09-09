using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

public sealed class ImageIntakePersistenceTests
{
    private const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

    private static ActionActor StaffActor() => ActionActor.Staff(
        DevelopmentOfflineIdentity.AdministratorId,
        [StaffRole.Administrator]);

    [Fact]
    public async Task RegistrationAllocatesSequentialReferencesAndMovesTheReceiptDecision()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var firstReceiptId = await UploadImageAsync(factory, client);
        var secondReceiptId = await UploadImageAsync(factory, client);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var register = services.GetRequiredService<IRegisterImageIntake>();
        var resolver = services.GetRequiredService<IImageIntakeOriginResolver>();
        var queries = services.GetRequiredService<IImageIntakeQueries>();
        var receipts = services.GetRequiredService<IIntakeReceiptQueries>();
        var actor = StaffActor();

        var firstOrigin = await resolver.ResolveOriginAsync(firstReceiptId, CancellationToken.None);
        Assert.NotNull(firstOrigin);
        var firstRequest = new RegisterImageIntakeRequest(
            firstOrigin!,
            "AB12CDE",
            actor,
            "image-intake-register-first",
            "Staff confirmed the registration from the retained image.");
        var first = await register.ExecuteAsync(firstRequest, CancellationToken.None);
        Assert.Equal("AB12CDE-01", first.ImageIntakeReference);

        var secondOrigin = await resolver.ResolveOriginAsync(secondReceiptId, CancellationToken.None);
        var second = await register.ExecuteAsync(
            new(
                secondOrigin!,
                "AB12CDE",
                actor,
                "image-intake-register-second",
                "A second arrival for the same vehicle registration."),
            CancellationToken.None);
        Assert.Equal("AB12CDE-02", second.ImageIntakeReference);

        var replay = await register.ExecuteAsync(firstRequest, CancellationToken.None);
        // The outbox ID is returned only from the commit that created it so
        // Core can publish that exact item. A replay is the same registration,
        // but it intentionally does not publish the old work item again.
        Assert.Equal(first with { PendingExternalWorkId = null }, replay);
        await Assert.ThrowsAsync<ImageIntakeOperationConflictException>(
            () => register.ExecuteAsync(
                firstRequest with { Reason = "Altered request details" },
                CancellationToken.None));
        await Assert.ThrowsAsync<IntakeSourceIdentityConflictException>(
            () => register.ExecuteAsync(
                firstRequest with
                {
                    NormalizedVehicleRegistration = "XY34ZZZ",
                    OperationKey = "image-intake-register-different-vrm"
                },
                CancellationToken.None));

        var firstReceipt = await receipts.GetAsync(firstReceiptId, CancellationToken.None);
        Assert.Equal(IntakeDecision.ImageIntakeRegistered, firstReceipt!.Decision);
        var detail = await queries.GetByOriginReceiptAsync(firstReceiptId, CancellationToken.None);
        Assert.Equal("AB12CDE-01", detail!.Record.ImageIntakeReference);
        Assert.Null(detail.AssociatedCaseId);
        var byReference = await queries.GetByReferenceAsync("ab12cde-02 ", CancellationToken.None);
        Assert.Equal(second.Id, byReference!.Record.Id);
        var byVrm = await queries.SearchByRegistrationAsync("AB12CDE", CancellationToken.None);
        Assert.Equal(2, byVrm.Count);
    }

    [Fact]
    public async Task InstructionBearingReceiptCannotRegister()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "instruction.eml",
            "QDOS instruction\r\nClaim Number: IMG-REG-001\r\nVehicle Registration: AB12 CDE");
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            email.FileName,
            email.MediaType,
            email.Content);
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var resolver = services.GetRequiredService<IImageIntakeOriginResolver>();
        var register = services.GetRequiredService<IRegisterImageIntake>();
        var origin = await resolver.ResolveOriginAsync(receiptId, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => register.ExecuteAsync(
                new(
                    origin!,
                    "AB12CDE",
                    StaffActor(),
                    "image-intake-register-instruction",
                    "Attempted registration of instruction-bearing material."),
                CancellationToken.None));
    }

    [Fact]
    public async Task NonImageNeedsSortingMaterialCannotRegister()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "loose-notes.eml",
            "Please review this ordinary correspondence; no instruction, no image.",
            "sender@example.test");
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            email.FileName,
            email.MediaType,
            email.Content);
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receipts = services.GetRequiredService<IIntakeReceiptQueries>();
        var receipt = await receipts.GetAsync(receiptId, CancellationToken.None);
        // The guard under test is the material shape, not the queue decision:
        // this receipt sits in Needs sorting exactly like image-only material.
        Assert.Equal(IntakeDecision.NeedsSorting, receipt!.Decision);

        var resolver = services.GetRequiredService<IImageIntakeOriginResolver>();
        var register = services.GetRequiredService<IRegisterImageIntake>();
        var origin = await resolver.ResolveOriginAsync(receiptId, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => register.ExecuteAsync(
                new(
                    origin!,
                    "AB12CDE",
                    StaffActor(),
                    "image-intake-register-non-image",
                    "Attempted registration of non-image material."),
                CancellationToken.None));
    }

    [Fact]
    public async Task ConcurrentSameVrmRegistrationsAllocateDistinctSequentialReferences()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var firstReceiptId = await UploadImageAsync(factory, client);
        var secondReceiptId = await UploadImageAsync(factory, client);

        // Both registrations race the same per-VRM sequence row under
        // serializable isolation. The winner commits; a loser may deadlock or
        // hit a serialization failure, must surface that failure rather than
        // reuse or skip a reference, and must succeed cleanly when retried.
        var outcomes = await Task.WhenAll(
            Task.Run(() => TryRegisterAsync(
                factory.Services, firstReceiptId, "concurrent-register-first")),
            Task.Run(() => TryRegisterAsync(
                factory.Services, secondReceiptId, "concurrent-register-second")));

        Assert.Contains(outcomes, outcome => outcome is null);
        foreach (var (receiptId, operationKey) in new[]
        {
            (firstReceiptId, "concurrent-register-first"),
            (secondReceiptId, "concurrent-register-second")
        })
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                if (await TryRegisterAsync(factory.Services, receiptId, operationKey) is null)
                {
                    break;
                }
            }
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IImageIntakeQueries>();
        var registered = await queries.SearchByRegistrationAsync("AB12CDE", CancellationToken.None);
        Assert.Equal(2, registered.Count);
        Assert.Collection(
            registered
                .Select(intake => intake.ImageIntakeReference)
                .OrderBy(reference => reference, StringComparer.Ordinal),
            reference => Assert.Equal("AB12CDE-01", reference),
            reference => Assert.Equal("AB12CDE-02", reference));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task GroupRegistrationAndInterruptedPairingPreserveEveryMember(bool reverseSibling, bool staffOverride)
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var submissionToken = $"mailbox-image-group:{Guid.NewGuid():N}";
        Guid groupId;
        await using (var submissionScope = factory.Services.CreateAsyncScope())
        {
            var submissionServices = submissionScope.ServiceProvider;
            var receivedAtUtc = submissionServices.GetRequiredService<TimeProvider>().GetUtcNow();
            var submittedGroup = await submissionServices.GetRequiredService<IGroupedIntakeSubmission>()
                .ExecuteAsync(
                    new(
                        submissionToken,
                        "system-worker:approved-inbox-poller",
                        receivedAtUtc,
                        [
                            new(0, new("overview.png", "image/png", Convert.FromBase64String(TinyPngBase64),
                                receivedAtUtc, "system-worker:approved-inbox-poller",
                                new(IntakeSourceChannel.Mailbox, GroupedIntakeMemberToken.Create(submissionToken, 0)))),
                            new(1, new("close-up.png", "image/png", Convert.FromBase64String(TinyPngBase64),
                                receivedAtUtc, "system-worker:approved-inbox-poller",
                                new(IntakeSourceChannel.Mailbox, GroupedIntakeMemberToken.Create(submissionToken, 1))))
                        ],
                        IntakeSourceChannel.Mailbox),
                    CancellationToken.None);
            groupId = submittedGroup.Group.Id;
        }

        Guid[] stagedReceiptIds;
        await using (var lookupScope = factory.Services.CreateAsyncScope())
        {
            var groups = lookupScope.ServiceProvider.GetRequiredService<IIntakeSubmissionGroupStore>();
            var group = await groups.GetAsync(groupId);
            stagedReceiptIds = group!.Members
                .OrderBy(member => member.Ordinal)
                .Select(member => member.StagedReceiptId)
                .ToArray();
        }

        var memberReceiptIds = new Guid[stagedReceiptIds.Length];
        for (var index = 0; index < stagedReceiptIds.Length; index++)
        {
            await using var drainScope = factory.Services.CreateAsyncScope();
            var evaluation = await IntakeWebDriver.DrainStagedAsync(
                drainScope.ServiceProvider,
                stagedReceiptIds[index]);
            memberReceiptIds[index] = evaluation.ProcessedReceiptId;
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var resolver = services.GetRequiredService<IImageIntakeOriginResolver>();
        var register = services.GetRequiredService<IRegisterImageIntake>();
        var origin = await resolver.ResolveOriginAsync(memberReceiptIds[0], CancellationToken.None);
        var record = await register.ExecuteAsync(
            new(
                origin!,
                "AB12CDE",
                StaffActor(),
                $"image-intake-register:group:{groupId:N}",
                "Staff registered the whole submission group.",
                SubmissionGroupId: groupId),
            CancellationToken.None);
        Assert.Equal("AB12CDE-01", record.ImageIntakeReference);
        Assert.Equal(groupId, record.SubmissionGroupId);

        // Every member receipt moved to the registered decision against the
        // ONE reference, in the registration transaction itself.
        var receipts = services.GetRequiredService<IIntakeReceiptQueries>();
        foreach (var memberReceiptId in memberReceiptIds)
        {
            var receipt = await receipts.GetAsync(memberReceiptId, CancellationToken.None);
            Assert.Equal(IntakeDecision.ImageIntakeRegistered, receipt!.Decision);
            Assert.Contains("AB12CDE-01", receipt.DecisionReason);
        }

        // The group-aware lookup reaches the one registration from a
        // non-origin member's receipt.
        var queries = services.GetRequiredService<IImageIntakeQueries>();
        var fromSibling = await queries.GetByOriginReceiptAsync(
            memberReceiptIds[1],
            CancellationToken.None);
        Assert.Equal(record.Id, fromSibling!.Record.Id);

        // A second registration for the same group — different operation key,
        // different member origin, exactly a racing sibling's attempt —
        // returns the one existing row instead of allocating a second
        // reference.
        var siblingOrigin = await resolver.ResolveOriginAsync(
            memberReceiptIds[1],
            CancellationToken.None);
        var replayed = await register.ExecuteAsync(
            new(
                siblingOrigin!,
                "AB12CDE",
                StaffActor(),
                "image-intake-register-group-second-attempt",
                "A sibling's racing registration attempt.",
                SubmissionGroupId: groupId),
            CancellationToken.None);
        Assert.Equal(record.Id, replayed.Id);
        var registered = await queries.SearchByRegistrationAsync("AB12CDE", CancellationToken.None);
        Assert.Single(registered);

        var caseOrigin = await UploadCaseOriginAsync(factory, client, "AUTO-LINK-01");
        var caseId = await SeedCaseAsync(services, caseOrigin, "IMG26011",
            nameof(CaseLifecycleState.Review), staffOverride ? "XY34ZZZ" : "AB12CDE");
        var mutations = services.GetRequiredService<IIntakeMutationStore>();
        var store = services.GetRequiredService<IImageIntakeStore>();
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var systemActor = ActionActor.SystemWorker(ImageIntakeAutomation.ActorId);
        if (staffOverride)
        {
            var originLease = await ClaimLeaseAsync(services, caseId, StaffActor(), "staff-group-origin-lease");
            var originReceipt = await receipts.GetAsync(memberReceiptIds[0], CancellationToken.None);
            await mutations.LinkAsync(new(memberReceiptIds[0], caseId, originReceipt!.Version, 0,
                originLease.Token, StaffActor(), "staff-group-origin", "Staff confirmed this group belongs to the instruction Case."),
                DateTimeOffset.UtcNow, CancellationToken.None);
            var observed = await store.GetAsync(record.Id, CancellationToken.None);
            Assert.Equal(1, observed!.AssociatedCaseVersion);
            var reverseLease = await ClaimLeaseAsync(services, caseId, StaffActor(), "staff-group-revise-lease");
            originReceipt = await receipts.GetAsync(memberReceiptIds[0], CancellationToken.None);
            await mutations.ReverseLinkAsync(new(memberReceiptIds[0], caseId, originReceipt!.Version, 1,
                reverseLease.Token, StaffActor(), "staff-group-revise", "Reconsider this group decision."),
                DateTimeOffset.UtcNow, CancellationToken.None);
            var relinkLease = await ClaimLeaseAsync(services, caseId, StaffActor(), "staff-group-relink-lease");
            originReceipt = await receipts.GetAsync(memberReceiptIds[0], CancellationToken.None);
            await mutations.LinkAsync(new(memberReceiptIds[0], caseId, originReceipt!.Version, 2,
                relinkLease.Token, StaffActor(), "staff-group-relink", "Staff confirmed this group belongs to the instruction Case."),
                DateTimeOffset.UtcNow, CancellationToken.None);
            // The target ID is unchanged and the supplied Case version is
            // current. The stale ORIGIN decision version must still refuse.
            await Assert.ThrowsAsync<IntakeAssociationConflictException>(() => mutations.AutoLinkAsync(
                new(memberReceiptIds[1], caseId, 3, systemActor, "stale-staff-group-completion",
                    "Complete the current reasoned staff group association.", ExpectedStaffOriginAssociationVersion: 0),
                DateTimeOffset.UtcNow, CancellationToken.None));
            Assert.Null((await receipts.GetAsync(memberReceiptIds[1], CancellationToken.None))!.CurrentCaseId);
        }
        else
        {
            await mutations.AutoLinkAsync(new(memberReceiptIds[0], caseId, 0, systemActor,
                $"image-intake-associate:{memberReceiptIds[0]:N}",
                "Automatic association: unambiguous registration match."),
                DateTimeOffset.UtcNow, CancellationToken.None);
        }

        // Simulate interruption after the first link: merging now must fail,
        // leaving the second member and the lifecycle available for recovery.
        await Assert.ThrowsAsync<IntakeAssociationConflictException>(() => store.MergeAsync(
            new(record.Id, caseId, systemActor, "interrupted-group-merge", "Resume the linked group.", 0),
            CancellationToken.None));
        if (reverseSibling)
        {
            var completionRequest = new AutomaticIntakeLinkRequest(memberReceiptIds[1], caseId, staffOverride ? 3 : 1, systemActor,
                $"image-intake-associate:{memberReceiptIds[1]:N}",
                "Automatic association: unambiguous registration match.",
                ExpectedStaffOriginAssociationVersion: staffOverride ? 2 : null);
            await mutations.AutoLinkAsync(completionRequest, DateTimeOffset.UtcNow, CancellationToken.None);
            var afterCompletion = await receipts.GetAsync(memberReceiptIds[1], CancellationToken.None);
            await mutations.AutoLinkAsync(completionRequest, DateTimeOffset.UtcNow, CancellationToken.None);
            await Assert.ThrowsAsync<IntakeOperationConflictException>(() => mutations.AutoLinkAsync(
                completionRequest with { ExpectedStaffOriginAssociationVersion = staffOverride ? 3 : 0 },
                DateTimeOffset.UtcNow, CancellationToken.None));
            var afterReplay = await receipts.GetAsync(memberReceiptIds[1], CancellationToken.None);
            Assert.Equal(afterCompletion!.Version, afterReplay!.Version);
            Assert.Equal(afterCompletion.ManualAssociationVersion, afterReplay.ManualAssociationVersion);
            Assert.Equal(caseId, afterReplay.CurrentCaseId);
            await using (var replayContext = await contextFactory.CreateDbContextAsync())
            {
                Assert.Equal(staffOverride ? 4 : 2, await replayContext.CaseWorkflows
                    .Where(item => item.CaseId == caseId).Select(item => item.Version).SingleAsync());
                Assert.Equal(1, await replayContext.IntakeMutationHistory.CountAsync(item =>
                    item.OperationKey == completionRequest.OperationKey));
            }
            if (staffOverride)
            {
                await Assert.ThrowsAsync<IntakeAssociationConflictException>(() => store.MergeAsync(
                    new(record.Id, caseId, systemActor, "stale-staff-group-merge", "Resume the linked group.", 0,
                        ExpectedStaffOriginAssociationVersion: 0), CancellationToken.None));
            }
            var lease = await ClaimLeaseAsync(services, caseId, StaffActor(), "reverse-group-lease");
            var sibling = await receipts.GetAsync(memberReceiptIds[1], CancellationToken.None);
            await mutations.ReverseLinkAsync(new(memberReceiptIds[1], caseId, sibling!.Version, staffOverride ? 4 : 2,
                lease.Token, StaffActor(), "reverse-group-member", "This member belongs elsewhere."),
                DateTimeOffset.UtcNow, CancellationToken.None);
            await Assert.ThrowsAsync<IntakeAssociationConflictException>(() => mutations.AutoLinkAsync(
                new(memberReceiptIds[1], caseId, staffOverride ? 5 : 3, systemActor, "do-not-revive-group-member",
                    "Complete the registered group.", ExpectedStaffOriginAssociationVersion: staffOverride ? 2 : null),
                DateTimeOffset.UtcNow, CancellationToken.None));
            // A changed queue decision cannot hide the reversed group member.
            await using var context = await contextFactory.CreateDbContextAsync();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE IntakeReceipts SET Decision = {"needs_sorting"} WHERE Id = {memberReceiptIds[1]}");
            await Assert.ThrowsAsync<IntakeAssociationConflictException>(() => store.MergeAsync(
                new(record.Id, caseId, systemActor, "reversed-group-merge", "Resume the linked group.", 0),
                CancellationToken.None));
        }

        var pairing = services.GetRequiredService<IImageIntakeCasePairing>();
        await StagedArtifactReconciliationFunctionIntegrationTests.RunPairingTimerAsync(pairing, contextFactory);
        await StagedArtifactReconciliationFunctionIntegrationTests.RunPairingTimerAsync(pairing, contextFactory);
        var final = await queries.GetAsync(record.Id, CancellationToken.None);
        Assert.Equal(reverseSibling ? ImageInitiatedCaseState.AwaitingInstruction
            : ImageInitiatedCaseState.MergedIntoInstructionCase, final!.State);
        Assert.Equal(reverseSibling ? 0 : 1, final.LifecycleVersion);
        foreach (var memberId in memberReceiptIds)
        {
            var member = await receipts.GetAsync(memberId, CancellationToken.None);
            Assert.Equal(reverseSibling && memberId == memberReceiptIds[1] ? (Guid?)null : caseId,
                member!.CurrentCaseId);
        }
        await using var assertions = await contextFactory.CreateDbContextAsync();
        Assert.Equal(reverseSibling ? 0 : 1, await assertions.ExternalWorkItems.CountAsync(item =>
            item.ImageIntakeId == record.Id && item.Kind == ExternalWorkKinds.MergeImageCaseCustody));
        Assert.Equal(reverseSibling ? 0 : 1, await assertions.ImageIntakeLifecycleEvents.CountAsync(item =>
            item.ImageIntakeId == record.Id));
        if (staffOverride)
        {
            var completion = await assertions.IntakeMutationHistory.SingleAsync(item =>
                item.IntakeReceiptId == memberReceiptIds[1] && item.EventType == "intake_case_auto_linked");
            Assert.Equal(nameof(ActorKind.SystemWorker), completion.ActorKind);
            using var evidence = JsonDocument.Parse(Assert.IsType<string>(completion.AfterJson));
            var decision = evidence.RootElement.GetProperty("StaffGroupDecision");
            Assert.Equal(memberReceiptIds[0], decision.GetProperty("OriginReceiptId").GetGuid());
            Assert.Equal(2, decision.GetProperty("Version").GetInt64());
            Assert.Equal(nameof(ActorKind.Staff), decision.GetProperty("ActorKind").GetString());
            Assert.Equal(StaffActor().SubjectId, decision.GetProperty("ActorSubjectId").GetString());
            Assert.Equal("staff-group-relink", decision.GetProperty("LastOperationKey").GetString());
            Assert.Equal("Staff confirmed this group belongs to the instruction Case.", decision.GetProperty("Reason").GetString());
        }
    }

    [Fact]
    public async Task TimerRecoversLinkedImageAfterOlderNonmatchWithoutAnotherAcceptance()
    {
        using var factory = new IntakeWebApplicationFactory("Development", true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var older = await UploadImageAsync(factory, client);
        var matching = await UploadImageAsync(factory, client);
        var origin = await UploadCaseOriginAsync(factory, client, "AUTO-LINK-01");
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await RegisterAsync(services, older, "XY34ZZZ", "older-nonmatching-image");
        await RegisterAsync(services, matching, "AB12CDE", "recover-linked-image");
        var caseId = await SeedCaseAsync(services, origin, "IMG26011",
            nameof(CaseLifecycleState.Review), "AB12CDE");
        var store = services.GetRequiredService<IImageIntakeStore>();
        var mutations = services.GetRequiredService<IIntakeMutationStore>();
        await mutations.AutoLinkAsync(new(matching, caseId, 0,
            ActionActor.SystemWorker(ImageIntakeAutomation.ActorId),
            $"image-intake-associate:{matching:N}", "Automatic association: unambiguous registration match."),
            DateTimeOffset.UtcNow, CancellationToken.None);
        var pending = Assert.Single(await store.ListPendingPairingAsync(1, null, CancellationToken.None));
        Assert.Equal(matching, pending.OriginReceiptId);
        Assert.Equal(ImageInitiatedCaseState.AwaitingInstruction, pending.State);

        var pairing = services.GetRequiredService<IImageIntakeCasePairing>();
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await StagedArtifactReconciliationFunctionIntegrationTests.RunPairingTimerAsync(pairing, contextFactory);
        await StagedArtifactReconciliationFunctionIntegrationTests.RunPairingTimerAsync(pairing, contextFactory);
        var merged = await store.GetByOriginReceiptAsync(matching, CancellationToken.None);
        Assert.Equal(ImageInitiatedCaseState.MergedIntoInstructionCase, merged!.State);
        Assert.Equal(caseId, merged.MergedIntoCaseId);
        Assert.Equal(1, merged.LifecycleVersion);
        Assert.Equal(ImageInitiatedCaseState.AwaitingInstruction,
            (await store.GetByOriginReceiptAsync(older, CancellationToken.None))!.State);
        await using var context = await contextFactory.CreateDbContextAsync();
        Assert.Equal(1, await context.IntakeManualAssociations.CountAsync(item => item.IntakeReceiptId == matching));
        Assert.Equal(1, await context.ImageIntakeLifecycleEvents.CountAsync(item => item.ImageIntakeId == pending.Id));
        Assert.Equal(1, await context.ExternalWorkItems.CountAsync(item =>
            item.ImageIntakeId == pending.Id && item.Kind == ExternalWorkKinds.MergeImageCaseCustody));
    }

    [Fact]
    public async Task AutomaticWriteRechecksCurrentIdentityAndPrincipalBeforeAssociation()
    {
        using var factory = new IntakeWebApplicationFactory("Development", true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var imageReceipt = await UploadImageAsync(factory, client);
        var origin = await UploadCaseOriginAsync(factory, client, "AUTO-LINK-01");
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await RegisterAsync(services, imageReceipt, "AB12CDE", "current-identity-image");
        var caseId = await SeedCaseAsync(services, origin, "IMG26011",
            nameof(CaseLifecycleState.Review), "AB12CDE");
        var candidates = services.GetRequiredService<IImageIntakeCaseCandidates>();
        var selected = Assert.Single(await candidates.FindEligibleByRegistrationAsync("AB12CDE", CancellationToken.None));
        var mutations = services.GetRequiredService<IIntakeMutationStore>();
        var request = new AutomaticIntakeLinkRequest(imageReceipt, selected.CaseId, selected.CaseVersion,
            ActionActor.SystemWorker(ImageIntakeAutomation.ActorId), "current-identity-link",
            "Automatic association: unambiguous registration match.");
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        // A pre-query is not authority for the later automatic write.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE CaseMatchIndex SET NormalizedVrm = {"XY34ZZZ"} WHERE CaseId = {caseId}");
        await Assert.ThrowsAsync<IntakeAssociationConflictException>(() => mutations.AutoLinkAsync(
            request, DateTimeOffset.UtcNow, CancellationToken.None));
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE CaseMatchIndex SET NormalizedVrm = {"AB12CDE"} WHERE CaseId = {caseId}");
        var secondOrigin = await UploadCaseOriginAsync(factory, client, "AUTO-LINK-02");
        var secondCase = await SeedCaseAsync(services, secondOrigin, "IMG26012",
            nameof(CaseLifecycleState.PostReport), "AB12CDE");
        var otherPrincipal = await context.Cases.Where(item => item.Id == secondCase)
            .Select(item => item.PrincipalId).SingleAsync();
        var store = services.GetRequiredService<IImageIntakeStore>();
        var image = await store.GetByOriginReceiptAsync(imageReceipt, CancellationToken.None);
        await store.SetPrincipalAsync(new(image!.Record.Id, otherPrincipal, StaffActor(), 0), CancellationToken.None);
        await Assert.ThrowsAsync<IntakeAssociationConflictException>(() => mutations.AutoLinkAsync(
            request, DateTimeOffset.UtcNow, CancellationToken.None));
        await store.SetPrincipalAsync(new(image.Record.Id, selected.PrincipalId, StaffActor(), 1), CancellationToken.None);
        await Assert.ThrowsAsync<CaseVersionConflictException>(() => mutations.AutoLinkAsync(
            request with { ExpectedCaseVersion = 1 }, DateTimeOffset.UtcNow, CancellationToken.None));
        await ClaimLeaseAsync(services, caseId, StaffActor(), "image-current-lease");
        await Assert.ThrowsAsync<IntakeAssociationConflictException>(() => mutations.AutoLinkAsync(
            request, DateTimeOffset.UtcNow, CancellationToken.None));
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE CaseWorkflows SET EditLeaseExpiresAtUtc = {DateTimeOffset.UtcNow.AddMinutes(-1)}, State = {nameof(CaseLifecycleState.PostReport)} WHERE CaseId = {caseId}");
        await Assert.ThrowsAsync<IntakeAssociationConflictException>(() => mutations.AutoLinkAsync(
            request, DateTimeOffset.UtcNow, CancellationToken.None));
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE CaseWorkflows SET State = {nameof(CaseLifecycleState.Review)} WHERE CaseId = {caseId}");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE CaseWorkflows SET State = {nameof(CaseLifecycleState.Review)} WHERE CaseId = {secondCase}");
        await Assert.ThrowsAsync<IntakeAssociationConflictException>(() => mutations.AutoLinkAsync(
            request, DateTimeOffset.UtcNow, CancellationToken.None));
        Assert.False(await context.IntakeManualAssociations.AnyAsync(item => item.IntakeReceiptId == imageReceipt));
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE CaseWorkflows SET State = {nameof(CaseLifecycleState.PostReport)} WHERE CaseId = {secondCase}");
        await mutations.AutoLinkAsync(request, DateTimeOffset.UtcNow, CancellationToken.None);
        Assert.Equal(caseId, (await services.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(imageReceipt, CancellationToken.None))!.CurrentCaseId);
    }

    [Fact]
    public async Task MergeRechecksCurrentStaffDestinationAndPreservesItsReasonedOverride()
    {
        using var factory = new IntakeWebApplicationFactory("Development", true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var imageReceipt = await UploadImageAsync(factory, client);
        var firstOrigin = await UploadCaseOriginAsync(factory, client, "AUTO-LINK-01");
        var secondOrigin = await UploadCaseOriginAsync(factory, client, "AUTO-LINK-02");
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await RegisterAsync(services, imageReceipt, "AB12CDE", "staff-override-image");
        var firstCase = await SeedCaseAsync(services, firstOrigin, "IMG26011",
            nameof(CaseLifecycleState.Review), "AB12CDE");
        var secondCase = await SeedCaseAsync(services, secondOrigin, "IMG26012",
            nameof(CaseLifecycleState.Review), "XY34ZZZ");
        var store = services.GetRequiredService<IImageIntakeStore>();
        var mutations = services.GetRequiredService<IIntakeMutationStore>();
        var receipts = services.GetRequiredService<IIntakeReceiptQueries>();
        var image = await store.GetByOriginReceiptAsync(imageReceipt, CancellationToken.None);
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var firstPrincipal = await context.Cases.Where(item => item.Id == firstCase)
            .Select(item => item.PrincipalId).SingleAsync();
        await store.SetPrincipalAsync(new(image!.Record.Id, firstPrincipal, StaffActor(), 0), CancellationToken.None);
        await mutations.AutoLinkAsync(new(imageReceipt, firstCase, 0,
            ActionActor.SystemWorker(ImageIntakeAutomation.ActorId), "before-staff-reversal",
            "Automatic association: unambiguous registration match."), DateTimeOffset.UtcNow, CancellationToken.None);
        var oldMerge = new MergeImageInitiatedCaseRequest(image.Record.Id, firstCase,
            ActionActor.SystemWorker(ImageIntakeAutomation.ActorId), "stale-destination-merge",
            "Resume the linked image.", 1);
        var lease = await ClaimLeaseAsync(services, firstCase, StaffActor(), "unlink-before-merge-lease");
        var receipt = await receipts.GetAsync(imageReceipt, CancellationToken.None);
        await mutations.ReverseLinkAsync(new(imageReceipt, firstCase, receipt!.Version, 1, lease.Token,
            StaffActor(), "unlink-before-merge", "The image belongs to another Case."),
            DateTimeOffset.UtcNow, CancellationToken.None);
        await Assert.ThrowsAsync<IntakeAssociationConflictException>(() => store.MergeAsync(oldMerge, CancellationToken.None));
        Assert.Empty(await store.ListPendingPairingAsync(1, null, CancellationToken.None));
        var secondLease = await ClaimLeaseAsync(services, secondCase, StaffActor(), "relink-before-merge-lease");
        receipt = await receipts.GetAsync(imageReceipt, CancellationToken.None);
        await mutations.LinkAsync(new(imageReceipt, secondCase, receipt!.Version, 0, secondLease.Token,
            StaffActor(), "relink-before-merge", "Staff confirmed this source belongs with this instruction."),
            DateTimeOffset.UtcNow, CancellationToken.None);
        await Assert.ThrowsAsync<IntakeAssociationConflictException>(() => store.MergeAsync(oldMerge, CancellationToken.None));
        Assert.Equal(1, (await store.GetAsync(image.Record.Id, CancellationToken.None))!.LifecycleVersion);

        var currentMerge = oldMerge with { CaseId = secondCase, OperationKey = "staff-destination-merge",
            ExpectedStaffOriginAssociationVersion = 2 };
        await ClaimLeaseAsync(services, secondCase, StaffActor(), "edit-during-image-merge");
        await Assert.ThrowsAsync<IntakeAssociationConflictException>(() => store.MergeAsync(currentMerge, CancellationToken.None));
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE CaseWorkflows SET EditLeaseExpiresAtUtc = {DateTimeOffset.UtcNow.AddMinutes(-1)}, State = {nameof(CaseLifecycleState.PostReport)} WHERE CaseId = {secondCase}");
        await Assert.ThrowsAsync<ImageIntakeCaseNotEligibleException>(() => store.MergeAsync(currentMerge, CancellationToken.None));
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE CaseWorkflows SET State = {nameof(CaseLifecycleState.Review)} WHERE CaseId = {secondCase}");

        // The timer is a SystemWorker, but the CURRENT recorded decision is
        // Staff's: its intentional VRM/principal override remains authoritative.
        var pairing = services.GetRequiredService<IImageIntakeCasePairing>();
        Assert.Equal(new ImageIntakePairingResult(1, 1, 0), await pairing.ReconcileAsync(1, CancellationToken.None));
        Assert.Equal(new ImageIntakePairingResult(0, 0, 0), await pairing.ReconcileAsync(1, CancellationToken.None));
        var merged = await store.GetAsync(image.Record.Id, CancellationToken.None);
        Assert.Equal(secondCase, merged!.MergedIntoCaseId);
        Assert.Equal(firstPrincipal, merged.Record.PrincipalId);
        Assert.Equal("AB12CDE", merged.Record.NormalizedVehicleRegistration);
        var association = await context.IntakeManualAssociations.AsNoTracking()
            .SingleAsync(item => item.IntakeReceiptId == imageReceipt);
        Assert.Equal(nameof(ActorKind.Staff), association.ActorKind);
        Assert.Equal("Staff confirmed this source belongs with this instruction.", association.Reason);
        var work = Assert.Single(await context.ExternalWorkItems.Where(item =>
            item.ImageIntakeId == image.Record.Id && item.Kind == ExternalWorkKinds.MergeImageCaseCustody).ToArrayAsync());
        Assert.Equal(secondCase, work.CaseId);
    }

    private static async Task<Exception?> TryRegisterAsync(
        IServiceProvider services,
        Guid receiptId,
        string operationKey)
    {
        try
        {
            await using var scope = services.CreateAsyncScope();
            await RegisterAsync(scope.ServiceProvider, receiptId, "AB12CDE", operationKey);
            return null;
        }
        catch (Exception exception)
        {
            // Never a replay conflict: the race loses on the sequence row,
            // not on the operation key.
            Assert.IsNotType<ImageIntakeOperationConflictException>(exception);
            return exception;
        }
    }

    [Fact]
    public async Task ReceiptLinkEnforcesEligibilityOnceAnImageIntakeExists()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var imageReceiptId = await UploadImageAsync(factory, client);
        var caseOriginReceiptId = await UploadCaseOriginAsync(factory, client, "CASE-LINK-01");
        var eligibleCaseId = await SeedCaseAsync(
            factory.Services,
            caseOriginReceiptId,
            "IMG26001",
            nameof(CaseLifecycleState.Review),
            "AB12CDE");
        var postReportReceiptId = await UploadCaseOriginAsync(factory, client, "CASE-LINK-02");
        var postReportCaseId = await SeedCaseAsync(
            factory.Services,
            postReportReceiptId,
            "IMG26002",
            nameof(CaseLifecycleState.PostReport),
            "AB12CDE");
        var actor = StaffActor();

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await RegisterAsync(services, imageReceiptId, "AB12CDE", "link-eligibility-register");
        var link = services.GetRequiredService<ILinkIntake>();
        var reverse = services.GetRequiredService<IReverseIntakeLink>();
        var receipts = services.GetRequiredService<IIntakeReceiptQueries>();
        var queries = services.GetRequiredService<IImageIntakeQueries>();

        var ineligibleLease = await ClaimLeaseAsync(
            factory.Services,
            postReportCaseId,
            actor,
            "claim-post-report-lease");
        var receipt = await receipts.GetAsync(imageReceiptId, CancellationToken.None);
        await Assert.ThrowsAsync<IntakeAssociationConflictException>(
            () => link.ExecuteAsync(
                new(
                    imageReceiptId,
                    postReportCaseId,
                    receipt!.Version,
                    0,
                    ineligibleLease.Token,
                    actor,
                    "link-post-report-case",
                    "A post-report case must be rejected."),
                CancellationToken.None));
        var rejected = await queries.GetByOriginReceiptAsync(imageReceiptId, CancellationToken.None);
        Assert.Null(rejected!.AssociatedCaseId);
        Assert.Equal(ImageInitiatedCaseState.AwaitingInstruction, rejected.State);
        Assert.Null(rejected.MergedIntoCaseId);

        var lease = await ClaimLeaseAsync(
            factory.Services,
            eligibleCaseId,
            actor,
            "claim-eligible-lease");
        receipt = await receipts.GetAsync(imageReceiptId, CancellationToken.None);
        await link.ExecuteAsync(
            new(
                imageReceiptId,
                eligibleCaseId,
                receipt!.Version,
                0,
                lease.Token,
                actor,
                "link-eligible-case",
                "The registration matches this pre-report case."),
            CancellationToken.None);

        var associated = await queries.GetByOriginReceiptAsync(imageReceiptId, CancellationToken.None);
        Assert.Equal(eligibleCaseId, associated!.AssociatedCaseId);
        Assert.Equal("IMG26001", associated.AssociatedCaseReference);
        // The manual link path shares the one lifecycle transition owner with
        // the automatic pairing paths: a staff-linked record must not stay
        // AwaitingInstruction.
        Assert.Equal(ImageInitiatedCaseState.MergedIntoInstructionCase, associated.State);
        Assert.Equal(eligibleCaseId, associated.MergedIntoCaseId);
        Assert.Equal("IMG26001", associated.MergedIntoCaseReference);
        var forCase = await queries.ListForCaseAsync(eligibleCaseId, CancellationToken.None);
        Assert.Single(forCase);

        var unlinkLease = await ClaimLeaseAsync(
            factory.Services,
            eligibleCaseId,
            actor,
            "claim-unlink-lease");
        receipt = await receipts.GetAsync(imageReceiptId, CancellationToken.None);
        await reverse.ExecuteAsync(
            new(
                imageReceiptId,
                eligibleCaseId,
                receipt!.Version,
                1,
                unlinkLease.Token,
                actor,
                "unlink-eligible-case",
                "Reasoned reversal of the association."),
            CancellationToken.None);

        var afterUnlink = await queries.GetByOriginReceiptAsync(imageReceiptId, CancellationToken.None);
        Assert.Null(afterUnlink!.AssociatedCaseId);
        Assert.Equal("AB12CDE-01", afterUnlink.Record.ImageIntakeReference);
    }

    [Fact]
    public async Task AutomaticAssociationWritesTheSameAssociationWithSystemAttribution()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var imageReceiptId = await UploadImageAsync(factory, client);
        var caseOriginReceiptId = await UploadCaseOriginAsync(factory, client, "AUTO-LINK-01");
        var caseId = await SeedCaseAsync(
            factory.Services,
            caseOriginReceiptId,
            "IMG26011",
            nameof(CaseLifecycleState.Review),
            "AB12CDE");

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await RegisterAsync(services, imageReceiptId, "AB12CDE", "auto-link-register");
        var mutationStore = services.GetRequiredService<IIntakeMutationStore>();
        var systemActor = ActionActor.SystemWorker("image-intake-automation");
        var request = new AutomaticIntakeLinkRequest(
            imageReceiptId,
            caseId,
            0,
            systemActor,
            "image-intake-associate-test",
            "Automatic association: unambiguous registration match.");

        await mutationStore.AutoLinkAsync(request, DateTimeOffset.UtcNow, CancellationToken.None);
        await mutationStore.AutoLinkAsync(request, DateTimeOffset.UtcNow, CancellationToken.None);
        await Assert.ThrowsAsync<IntakeOperationConflictException>(
            () => mutationStore.AutoLinkAsync(
                request with { Reason = "Altered automatic request" },
                DateTimeOffset.UtcNow,
                CancellationToken.None));

        var queries = services.GetRequiredService<IImageIntakeQueries>();
        var detail = await queries.GetByOriginReceiptAsync(imageReceiptId, CancellationToken.None);
        Assert.Equal(caseId, detail!.AssociatedCaseId);

        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var workflowEvent = await context.Database
            .SqlQuery<string>(
                $"SELECT EventType AS Value FROM CaseWorkflowEvents WHERE CaseId = {caseId}")
            .ToListAsync();
        Assert.Contains("intake_case_auto_linked", workflowEvent);
        var actorKind = await context.Database
            .SqlQuery<string>(
                $"SELECT ActorKind AS Value FROM IntakeMutationHistory WHERE OperationKey = {"image-intake-associate-test"}")
            .SingleAsync();
        Assert.Equal(nameof(ActorKind.SystemWorker), actorKind);
    }

    [Fact]
    public async Task CloseValidatesBeforePersistingAndReplayRejectsAMismatchedCommand()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var imageReceiptId = await UploadImageAsync(factory, client);
        var actor = StaffActor();

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await RegisterAsync(services, imageReceiptId, "AB12CDE", "close-validate-register");
        var store = services.GetRequiredService<IImageIntakeStore>();
        var queries = services.GetRequiredService<IImageIntakeQueries>();
        var detail = await queries.GetByOriginReceiptAsync(imageReceiptId, CancellationToken.None);
        Assert.NotNull(detail);

        // Registering an Image intake with no matching Case never inserts a
        // formal Cases row — the Image-initiated Case is a projection, not a
        // second allocator.
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            Assert.Equal(0, await context.Database
                .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM Cases")
                .SingleAsync());
        }

        // A validator failure — never a raw SQL truncation error — for a
        // reason over the 500-character bound the Core policy enforces.
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.CloseAsync(
            new(
                detail.Record.Id,
                actor,
                "close-over-length",
                new string('r', 501),
                detail.LifecycleVersion),
            CancellationToken.None));

        var reason = "Instructions never arrived for this vehicle.";
        var operationKey = $"close-replay:{imageReceiptId:N}";
        var closed = await store.CloseAsync(
            new(detail.Record.Id, actor, operationKey, reason, detail.LifecycleVersion),
            CancellationToken.None);
        Assert.Equal(ImageInitiatedCaseState.StaffClosed, closed.State);

        // The exact same command replays (idempotent retry) ...
        var replayed = await store.CloseAsync(
            new(detail.Record.Id, actor, operationKey, reason, detail.LifecycleVersion),
            CancellationToken.None);
        Assert.Equal(ImageInitiatedCaseState.StaffClosed, replayed.State);

        // ... but the same operation key with a different reason is a
        // conflicting reuse, not a silent second success.
        await Assert.ThrowsAsync<ImageIntakeOperationConflictException>(() => store.CloseAsync(
            new(detail.Record.Id, actor, operationKey, "A different reason.", detail.LifecycleVersion),
            CancellationToken.None));
    }

    /// <summary>
    /// The known principal end to end through the store: absent, set,
    /// replaced, re-submitted, cleared — and never a lifecycle event, because
    /// recording who the work is for is not a lifecycle transition and nothing
    /// about it is inferred from a registration match or a linked Case.
    /// </summary>
    [Fact]
    public async Task PrincipalAssignmentRoundTripsWithoutLifecycleHistoryOrInference()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var imageReceiptId = await UploadImageAsync(factory, client);
        var actor = StaffActor();
        var alpha = await ImageIntakeTestData.SeedPrincipalAsync(factory.Services, "ALPHA");
        var beta = await ImageIntakeTestData.SeedPrincipalAsync(factory.Services, "BETA");
        var retired = await ImageIntakeTestData.SeedPrincipalAsync(
            factory.Services,
            "GAMMA",
            isActive: false);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await RegisterAsync(services, imageReceiptId, "AB12CDE", "principal-register");
        var store = services.GetRequiredService<IImageIntakeStore>();
        var queries = services.GetRequiredService<IImageIntakeQueries>();
        var detail = await queries.GetByOriginReceiptAsync(imageReceiptId, CancellationToken.None);
        Assert.NotNull(detail);
        var imageIntakeId = detail.Record.Id;
        var historyCount = (await store.ListHistoryAsync(imageIntakeId, CancellationToken.None)).Count;

        // Absent to begin with, and the options list offers only the active
        // principals, ordered by code.
        Assert.Null(detail.Record.PrincipalId);
        Assert.Null(detail.PrincipalCode);
        var options = await queries.ListActivePrincipalsAsync(CancellationToken.None);
        Assert.Contains(options, principal => principal.Id == alpha);
        Assert.Contains(options, principal => principal.Id == beta);
        Assert.DoesNotContain(options, principal => principal.Id == retired);
        Assert.Equal(
            options.Select(principal => principal.Code).OrderBy(code => code, StringComparer.Ordinal),
            options.Select(principal => principal.Code));

        // Set.
        var set = await store.SetPrincipalAsync(
            new(imageIntakeId, alpha, actor, detail.LifecycleVersion),
            CancellationToken.None);
        Assert.Equal(alpha, set.PrincipalId);
        Assert.Equal(detail.LifecycleVersion + 1, set.LifecycleVersion);
        var afterSet = Assert.IsType<ImageIntakeDetail>(
            await queries.GetAsync(imageIntakeId, CancellationToken.None));
        Assert.Equal("ALPHA", afterSet.PrincipalCode);
        Assert.Equal(
            "ALPHA",
            Assert.Single(
                await queries.SearchByRegistrationAsync("AB12CDE", CancellationToken.None))
                .PrincipalCode);

        // Re-submitting the same value is a no-op that leaves the version
        // alone, so it can never invalidate an open form by itself.
        var repeated = await store.SetPrincipalAsync(
            new(imageIntakeId, alpha, actor, set.LifecycleVersion),
            CancellationToken.None);
        Assert.Equal(set.LifecycleVersion, repeated.LifecycleVersion);

        // A stale expected version is refused and overwrites nothing.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => store.SetPrincipalAsync(
            new(imageIntakeId, beta, actor, detail.LifecycleVersion),
            CancellationToken.None));
        Assert.Equal(
            alpha,
            Assert.IsType<ImageIntakeDetail>(
                await queries.GetAsync(imageIntakeId, CancellationToken.None)).Record.PrincipalId);

        // A principal that is not active cannot be recorded.
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SetPrincipalAsync(
            new(imageIntakeId, retired, actor, set.LifecycleVersion),
            CancellationToken.None));

        // Replace, then clear back to `Not known`.
        var replaced = await store.SetPrincipalAsync(
            new(imageIntakeId, beta, actor, set.LifecycleVersion),
            CancellationToken.None);
        Assert.Equal(beta, replaced.PrincipalId);
        Assert.Equal(
            "BETA",
            Assert.IsType<ImageIntakeDetail>(
                await queries.GetAsync(imageIntakeId, CancellationToken.None)).PrincipalCode);

        var cleared = await store.SetPrincipalAsync(
            new(imageIntakeId, null, actor, replaced.LifecycleVersion),
            CancellationToken.None);
        Assert.Null(cleared.PrincipalId);
        var afterClear = Assert.IsType<ImageIntakeDetail>(
            await queries.GetAsync(imageIntakeId, CancellationToken.None));
        Assert.Null(afterClear.PrincipalCode);
        Assert.Null(afterClear.Record.PrincipalId);

        // Not one lifecycle event was written by any of it.
        Assert.Equal(
            historyCount,
            (await store.ListHistoryAsync(imageIntakeId, CancellationToken.None)).Count);
        Assert.Equal(ImageInitiatedCaseState.AwaitingInstruction, afterClear.State);
    }

    private static async Task<Guid> UploadImageAsync(
        IntakeWebApplicationFactory factory,
        HttpClient client)
    {
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "vehicle.png",
            "image/png",
            Convert.FromBase64String(TinyPngBase64),
            Guid.NewGuid().ToString("N"));
        return IntakeWebDriver.ReceiptId(upload);
    }

    private static async Task<Guid> UploadCaseOriginAsync(
        IntakeWebApplicationFactory factory,
        HttpClient client,
        string claimNumber)
    {
        var email = IntakeTestEvidence.CreateEmail(
            $"{claimNumber}.eml",
            $"QDOS instruction\r\nClaim Number: {claimNumber}\r\nVehicle Registration: AB12 CDE");
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            email.FileName,
            email.MediaType,
            email.Content);
        return IntakeWebDriver.ReceiptId(upload);
    }

    private static async Task RegisterAsync(
        IServiceProvider services,
        Guid receiptId,
        string registration,
        string operationKey)
    {
        var resolver = services.GetRequiredService<IImageIntakeOriginResolver>();
        var register = services.GetRequiredService<IRegisterImageIntake>();
        var origin = await resolver.ResolveOriginAsync(receiptId, CancellationToken.None);
        await register.ExecuteAsync(
            new(
                origin!,
                registration,
                StaffActor(),
                operationKey,
                "Staff confirmed the registration from the retained image."),
            CancellationToken.None);
    }

    private static async Task<CaseEditLease> ClaimLeaseAsync(
        IServiceProvider services,
        Guid caseId,
        ActionActor actor,
        string operationKey)
    {
        await using var scope = services.CreateAsyncScope();
        var workflows = scope.ServiceProvider.GetRequiredService<ICaseWorkflowQueries>();
        var workflow = await workflows.GetAsync(caseId, CancellationToken.None);
        return await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>().ClaimAsync(
            new ClaimCaseEditLeaseRequest(
                caseId,
                workflow!.Version,
                actor,
                operationKey),
            CancellationToken.None);
    }

    private static async Task<Guid> SeedCaseAsync(
        IServiceProvider services,
        Guid originReceiptId,
        string reference,
        string workflowState,
        string draftRegistration)
    {
        await using var scope = services.CreateAsyncScope();
        var contextFactory =
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var now = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {$"Image intake provider {reference}"}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {now})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version) VALUES ({principalId}, {organizationId}, {reference}, {lineageId}, {true}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2031}, {1}, {reference}, {"inspection"}, {"not_ready"}, {"pending"}, {originReceiptId}, {true}, {true}, {now}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({caseId}, {workflowState}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseDataSnapshots (CaseId, OriginIntakeReceiptId, OriginSourceChannel, OriginExternalReceiptToken, OriginSourceHash, OriginReceivedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, CompletenessPolicyKey, CompletenessPolicyVersion, CompletenessPolicySatisfied, AcceptedAtUtc) VALUES ({caseId}, {originReceiptId}, {"manual_upload"}, {reference}, {1.ToString("X64", CultureInfo.InvariantCulture)}, {now}, {"image-intake-test-reader"}, {"1"}, {"image-intake-fixture"}, {1}, {reference}, {1}, {true}, {now})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseMatchIndex (CaseId, WorkProviderCode, NormalizedVrm, MatchPolicyKey, MatchPolicyVersion, UpdatedAtUtc) VALUES ({caseId}, {reference}, {draftRegistration}, {"image-intake-fixture"}, {1}, {now})");
        return caseId;
    }
}
