using System.Security.Cryptography;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Triage;

namespace Pegasus.Core.Tests.Intake;

public sealed class ReconcileUnidentifiedDestinationsTests
{
    private static readonly byte[] ImageBytes = [1, 2, 3, 4, 5];
    private static readonly string ImageHash = Convert.ToHexString(SHA256.HashData(ImageBytes));
    private static readonly DateTimeOffset Now = new(2031, 8, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PromotedImageReceiptResolvesItsOpenItemToTheImageIntake()
    {
        var harness = new Harness();
        var receipt = Receipt(Guid.NewGuid(), IntakeDecision.ImageIntakeRegistered);
        harness.Receipts.Receipts[receipt.Id] = receipt;
        var item = harness.AddOpenItem(1, UnidentifiedOrigin.Receipt(receipt.Id));
        var intakeId = Guid.NewGuid();
        harness.ImageIntakes.DetailsByOriginReceipt[receipt.Id] = Detail(intakeId, receipt, "AB12CDE-01");

        var result = await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(1, 1, 0, 0), result);
        var resolve = Assert.Single(harness.Resolve.Requests);
        Assert.Equal(item.Id, resolve.UnidentifiedItemId);
        Assert.Equal(UnidentifiedResolutionTargetKind.ImageIntake, resolve.TargetKind);
        Assert.Equal(intakeId.ToString("N"), resolve.TargetId);
        Assert.Equal("AB12CDE-01", resolve.TargetReference);
        Assert.Equal(ActorKind.Automation, resolve.Actor.Kind);
        Assert.Equal(
            ReconcileUnidentifiedDestinations.AutomationActorId,
            resolve.Actor.SubjectId);

        // The key is the ITEM's own version, never the receipt's: a destination
        // change need not mutate the receipt, so a receipt-keyed re-resolve
        // rebuilds a key its own first resolution already took (INTK-048).
        Assert.Equal(
            $"intake-unidentified-reconcile:{item.Id:N}:{item.Version}",
            resolve.OperationKey);
    }

    [Fact]
    public async Task CaseCreatedReceiptResolvesToTheInstructionCase()
    {
        var harness = new Harness();
        var caseId = Guid.NewGuid();
        var receipt = Receipt(
            Guid.NewGuid(),
            IntakeDecision.CaseCreated,
            acceptedCaseId: caseId,
            acceptedCaseReference: "QDOS26009");
        harness.Receipts.Receipts[receipt.Id] = receipt;
        harness.AddOpenItem(2, UnidentifiedOrigin.Receipt(receipt.Id));

        var result = await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(1, result.Resolved);
        var resolve = Assert.Single(harness.Resolve.Requests);
        Assert.Equal(UnidentifiedResolutionTargetKind.InstructionCase, resolve.TargetKind);
        Assert.Equal(caseId.ToString("N"), resolve.TargetId);
        Assert.Equal("QDOS26009", resolve.TargetReference);
    }

    // Statement 1 (PR 639 preservation table §4) — the rule INTK-048 exists for.
    [Fact]
    public async Task ManuallyLinkedUnidentifiedReceiptResolvesToTheInstructionCase()
    {
        var harness = new Harness();
        var caseId = Guid.NewGuid();
        // A manual link never rewrites the immutable processing decision, so
        // the receipt is still NeedsSorting and still unidentified-eligible.
        var receipt = ManuallyLinked(
            Receipt(Guid.NewGuid(), IntakeDecision.NeedsSorting),
            caseId,
            "QDOS26030",
            associationVersion: 1);
        harness.Receipts.Receipts[receipt.Id] = receipt;
        var item = harness.AddOpenItem(1, UnidentifiedOrigin.Receipt(receipt.Id));

        var result = await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(1, 1, 0, 0), result);
        var resolve = Assert.Single(harness.Resolve.Requests);
        Assert.Equal(item.Id, resolve.UnidentifiedItemId);
        Assert.Equal(UnidentifiedResolutionTargetKind.InstructionCase, resolve.TargetKind);
        Assert.Equal(caseId.ToString("N"), resolve.TargetId);
        Assert.Equal("QDOS26030", resolve.TargetReference);
    }

    // Statement 2 — established precedence beats the trailing manual-link branch.
    [Fact]
    public async Task AManuallyLinkedImageReceiptStillResolvesToTheImageIntake()
    {
        var harness = new Harness();
        var receipt = ManuallyLinked(
            Receipt(Guid.NewGuid(), IntakeDecision.ImageIntakeRegistered),
            Guid.NewGuid(),
            "QDOS26031",
            associationVersion: 1);
        harness.Receipts.Receipts[receipt.Id] = receipt;
        harness.AddOpenItem(1, UnidentifiedOrigin.Receipt(receipt.Id));
        var intakeId = Guid.NewGuid();
        harness.ImageIntakes.DetailsByOriginReceipt[receipt.Id] = Detail(intakeId, receipt, "AB12CDE-01");

        await harness.Reconciler.ExecuteAsync(50);

        var resolve = Assert.Single(harness.Resolve.Requests);
        Assert.Equal(UnidentifiedResolutionTargetKind.ImageIntake, resolve.TargetKind);
        Assert.Equal(intakeId.ToString("N"), resolve.TargetId);
    }

    // Statement 3 — a Triage also outranks the trailing manual-link branch.
    [Fact]
    public async Task AManuallyLinkedTriageRequestStillResolvesToTheTriage()
    {
        var harness = new Harness();
        var receipt = ManuallyLinked(
            TriageRequestReceipt(Guid.NewGuid()),
            Guid.NewGuid(),
            "QDOS26032",
            associationVersion: 1);
        harness.Receipts.Receipts[receipt.Id] = receipt;
        harness.AddOpenItem(1, UnidentifiedOrigin.Receipt(receipt.Id));
        var triageId = Guid.NewGuid();
        harness.Triages.SummariesByOriginReceipt[receipt.Id] = Triage(triageId, "VO75DFJ");

        await harness.Reconciler.ExecuteAsync(50);

        var resolve = Assert.Single(harness.Resolve.Requests);
        Assert.Equal(UnidentifiedResolutionTargetKind.Triage, resolve.TargetKind);
        Assert.Equal(triageId.ToString("N"), resolve.TargetId);
        Assert.Equal("VO75DFJ", resolve.TargetReference);
    }

    // Statement 4.
    [Fact]
    public async Task StillUnidentifiedReceiptsAreNeverForceClosed()
    {
        var harness = new Harness();
        // Image-only material still awaiting sorting, and a terminal
        // unsupported receipt: both remain legitimately Unidentified.
        var pendingImage = Receipt(Guid.NewGuid(), IntakeDecision.NeedsSorting);
        var unsupported = Receipt(Guid.NewGuid(), IntakeDecision.Unsupported, mediaType: "application/zip");
        harness.Receipts.Receipts[pendingImage.Id] = pendingImage;
        harness.Receipts.Receipts[unsupported.Id] = unsupported;
        harness.AddOpenItem(3, UnidentifiedOrigin.Receipt(pendingImage.Id));
        harness.AddOpenItem(4, UnidentifiedOrigin.Receipt(unsupported.Id));

        var result = await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(2, 0, 0, 0), result);
        Assert.Empty(harness.Resolve.Requests);
    }

    [Fact]
    public async Task GroupOriginItemsAreSkipped()
    {
        var harness = new Harness();
        harness.AddOpenItem(5, UnidentifiedOrigin.SubmissionGroup(Guid.NewGuid()));

        var result = await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(0, 0, 0, 0), result);
        Assert.Empty(harness.Resolve.Requests);
    }

    [Fact]
    public async Task AResolveFailureIsCountedAndNeverStopsTheSweep()
    {
        var harness = new Harness();
        var failing = Receipt(Guid.NewGuid(), IntakeDecision.ImageIntakeRegistered);
        var succeeding = Receipt(Guid.NewGuid(), IntakeDecision.ImageIntakeRegistered);
        harness.Receipts.Receipts[failing.Id] = failing;
        harness.Receipts.Receipts[succeeding.Id] = succeeding;
        var failingItem = harness.AddOpenItem(6, UnidentifiedOrigin.Receipt(failing.Id));
        harness.AddOpenItem(7, UnidentifiedOrigin.Receipt(succeeding.Id));
        harness.ImageIntakes.DetailsByOriginReceipt[failing.Id] = Detail(Guid.NewGuid(), failing, "AB12CDE-01");
        harness.ImageIntakes.DetailsByOriginReceipt[succeeding.Id] = Detail(Guid.NewGuid(), succeeding, "AB12CDE-02");
        harness.Resolve.FailForOperationKeys.Add(
            $"intake-unidentified-reconcile:{failingItem.Id:N}:{failingItem.Version}");

        var result = await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(2, 1, 0, 1), result);
    }

    [Fact]
    public async Task AnAlreadyResolvedItemWithAnUnchangedDestinationIsANoOp()
    {
        var harness = new Harness();
        var receipt = Receipt(Guid.NewGuid(), IntakeDecision.ImageIntakeRegistered);
        var intakeId = Guid.NewGuid();
        harness.ImageIntakes.DetailsByOriginReceipt[receipt.Id] = Detail(intakeId, receipt, "AB12CDE-01");
        harness.AddAutomationResolvedItem(
            8,
            UnidentifiedOrigin.Receipt(receipt.Id),
            UnidentifiedResolutionTargetKind.ImageIntake,
            intakeId.ToString("N"),
            "AB12CDE-01");

        var written = await harness.Reconciler.SynchronizeForReceiptAsync(receipt, CancellationToken.None);

        Assert.False(written);
        Assert.Empty(harness.Resolve.Requests);
        Assert.Empty(harness.Store.ReopenRequests);
    }

    [Fact]
    public async Task ATriageRequestWhoseTriageNowExistsResolvesItsStaleOpenItem()
    {
        // The operator's own transition: material waits in Unidentified until a
        // registration is known, "then open the Triage". A staff re-evaluation
        // reaches it — the second pass reads the registration and opens the
        // Triage, and the U-reference minted by the first pass is then stale.
        // Without a Triage destination it stayed open beside the Triage
        // forever, and the same material sat in two queues (INTK-033).
        var harness = new Harness();
        var receipt = TriageRequestReceipt(Guid.NewGuid());
        harness.Receipts.Receipts[receipt.Id] = receipt;
        var item = harness.AddOpenItem(1, UnidentifiedOrigin.Receipt(receipt.Id));
        var triageId = Guid.NewGuid();
        harness.Triages.SummariesByOriginReceipt[receipt.Id] = Triage(triageId, "VO75DFJ");

        var result = await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(1, 1, 0, 0), result);
        var resolve = Assert.Single(harness.Resolve.Requests);
        Assert.Equal(item.Id, resolve.UnidentifiedItemId);
        Assert.Equal(UnidentifiedResolutionTargetKind.Triage, resolve.TargetKind);
        Assert.Equal(triageId.ToString("N"), resolve.TargetId);
        Assert.Equal("VO75DFJ", resolve.TargetReference);
    }

    [Fact]
    public async Task ATriageRequestStillWaitingForItsRegistrationKeepsItsOpenItem()
    {
        // The other half of the same rule: no Triage exists yet, so the
        // Unidentified item is not stale and must not be force-closed.
        var harness = new Harness();
        var receipt = TriageRequestReceipt(Guid.NewGuid());
        harness.Receipts.Receipts[receipt.Id] = receipt;
        harness.AddOpenItem(1, UnidentifiedOrigin.Receipt(receipt.Id));

        var result = await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(1, 0, 0, 0), result);
        Assert.Empty(harness.Resolve.Requests);
    }

    // Statement 5.
    [Fact]
    public async Task AStaffResolutionIsNeverReopenedOrReTargeted()
    {
        var harness = new Harness();
        var receipt = ManuallyLinked(
            Receipt(Guid.NewGuid(), IntakeDecision.NeedsSorting),
            Guid.NewGuid(),
            "QDOS26040",
            associationVersion: 7);
        harness.Receipts.Receipts[receipt.Id] = receipt;
        var item = harness.AddResolvedItem(
            9,
            UnidentifiedOrigin.Receipt(receipt.Id),
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
            UnidentifiedResolutionTargetKind.ExternalReference,
            "a-staff-decision",
            null);
        harness.Store.RecheckItems.Add(item.Id);

        var result = await harness.Reconciler.ExecuteAsync(50);

        // The recheck row is still examined — and therefore counted — but the
        // sweep writes nothing at all for a resolution it does not own.
        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(1, 0, 0, 0), result);
        Assert.Empty(harness.Store.ReopenRequests);
        Assert.Empty(harness.Resolve.Requests);
        Assert.Empty(harness.Store.RecheckMarks);

        // And directly, past the queue: still no write.
        Assert.False(await harness.Reconciler.SynchronizeForReceiptAsync(receipt, CancellationToken.None));
    }

    // Statement 6.
    [Fact]
    public async Task AWithdrawnDestinationReopensTheItemAndClearsEveryResolutionField()
    {
        var harness = new Harness();
        var caseId = Guid.NewGuid();
        var receipt = ManuallyLinked(
            Receipt(Guid.NewGuid(), IntakeDecision.NeedsSorting),
            caseId,
            "QDOS26041",
            associationVersion: 1);
        harness.Receipts.Receipts[receipt.Id] = receipt;
        var item = harness.AddAutomationResolvedItem(
            10,
            UnidentifiedOrigin.Receipt(receipt.Id),
            UnidentifiedResolutionTargetKind.InstructionCase,
            caseId.ToString("N"),
            "QDOS26041");
        harness.Store.RecheckItems.Add(item.Id);

        // Unlink: the association moves on and names no case any more.
        harness.Receipts.Receipts[receipt.Id] = Unlinked(receipt, associationVersion: 2);

        var result = await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(1, 0, 1, 0), result);
        var reopen = Assert.Single(harness.Store.ReopenRequests);
        Assert.Equal(item.Id, reopen.UnidentifiedItemId);
        Assert.Equal(item.Version, reopen.ExpectedVersion);
        Assert.Equal(ActorKind.Automation, reopen.Actor.Kind);
        Assert.Empty(harness.Resolve.Requests);

        var reopened = Assert.Single(harness.Store.Items);
        Assert.Equal(UnidentifiedState.Open, reopened.State);
        Assert.Null(reopened.ResolvedAtUtc);
        Assert.Null(reopened.ResolvedBy);
        Assert.Null(reopened.ResolutionReason);
        Assert.Null(reopened.ResolutionTargetKind);
        Assert.Null(reopened.ResolutionTargetId);
        Assert.Null(reopened.ResolutionTargetReference);
        Assert.Equal(item.Version + 1, reopened.Version);
    }

    // Statement 7.
    [Fact]
    public async Task AChangedDestinationReopensAndReResolvesInTheSamePass()
    {
        var harness = new Harness();
        var caseA = Guid.NewGuid();
        var caseB = Guid.NewGuid();
        var receipt = ManuallyLinked(
            Receipt(Guid.NewGuid(), IntakeDecision.NeedsSorting),
            caseA,
            "QDOS26050",
            associationVersion: 1);
        harness.Receipts.Receipts[receipt.Id] = receipt;
        var item = harness.AddAutomationResolvedItem(
            11,
            UnidentifiedOrigin.Receipt(receipt.Id),
            UnidentifiedResolutionTargetKind.InstructionCase,
            caseA.ToString("N"),
            "QDOS26050");
        harness.Store.RecheckItems.Add(item.Id);

        harness.Receipts.Receipts[receipt.Id] = ManuallyLinked(receipt, caseB, "QDOS26051", associationVersion: 3);

        var result = await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(1, 0, 1, 0), result);
        Assert.Single(harness.Store.ReopenRequests);
        var resolve = Assert.Single(harness.Resolve.Requests);
        Assert.Equal(UnidentifiedResolutionTargetKind.InstructionCase, resolve.TargetKind);
        Assert.Equal(caseB.ToString("N"), resolve.TargetId);
        Assert.Equal("QDOS26051", resolve.TargetReference);

        // The re-resolve is applied at the version the reopen produced.
        Assert.Equal(item.Version + 1, resolve.ExpectedVersion);
    }

    // Statement 8 — operation-key uniqueness per transition, at a receipt whose
    // own Version never moves.
    [Fact]
    public async Task TwoSuccessiveCorrectionsProduceFourDistinctOperationKeys()
    {
        var harness = new Harness();
        var caseId = Guid.NewGuid();
        var receipt = ManuallyLinked(
            TriageRequestReceipt(Guid.NewGuid()),
            caseId,
            "QDOS26060",
            associationVersion: 1);
        harness.Receipts.Receipts[receipt.Id] = receipt;
        var item = harness.AddAutomationResolvedItem(
            12,
            UnidentifiedOrigin.Receipt(receipt.Id),
            UnidentifiedResolutionTargetKind.InstructionCase,
            caseId.ToString("N"),
            "QDOS26060");
        harness.Store.RecheckItems.Add(item.Id);

        // Correction one: a Triage is opened for the already-linked receipt.
        // Opening a Triage leaves the receipt itself untouched.
        var triageId = Guid.NewGuid();
        harness.Triages.SummariesByOriginReceipt[receipt.Id] = Triage(triageId, "VO75DFJ");
        await harness.Reconciler.ExecuteAsync(50);

        // Correction two: the Triage's registration is corrected. Again the
        // receipt's own Version does not move.
        harness.Triages.SummariesByOriginReceipt[receipt.Id] = Triage(triageId, "VO75DFK");
        harness.Store.RecheckItems.Clear();
        harness.Store.RecheckItems.Add(item.Id);
        await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(receipt.Version, harness.Receipts.Receipts[receipt.Id].Version);
        string[] keys =
        [
            .. harness.Store.ReopenRequests.Select(request => request.OperationKey),
            .. harness.Resolve.Requests.Select(request => request.OperationKey)
        ];
        Assert.Equal(4, keys.Length);
        Assert.Equal(4, keys.Distinct(StringComparer.Ordinal).Count());
    }

    // Statement 9 — replay stability: a reopen that commits and a re-resolve
    // that fails must present the SAME resolve key on the retry.
    [Fact]
    public async Task ARetriedReResolvePresentsTheSameOperationKey()
    {
        var harness = new Harness();
        var caseA = Guid.NewGuid();
        var caseB = Guid.NewGuid();
        var receipt = ManuallyLinked(
            Receipt(Guid.NewGuid(), IntakeDecision.NeedsSorting),
            caseA,
            "QDOS26070",
            associationVersion: 1);
        harness.Receipts.Receipts[receipt.Id] = receipt;
        var item = harness.AddAutomationResolvedItem(
            13,
            UnidentifiedOrigin.Receipt(receipt.Id),
            UnidentifiedResolutionTargetKind.InstructionCase,
            caseA.ToString("N"),
            "QDOS26070");
        harness.Store.RecheckItems.Add(item.Id);
        harness.Receipts.Receipts[receipt.Id] = ManuallyLinked(receipt, caseB, "QDOS26071", associationVersion: 3);

        var expectedResolveKey = $"intake-unidentified-reconcile:{item.Id:N}:{item.Version + 1}";
        harness.Resolve.FailForOperationKeys.Add(expectedResolveKey);

        var first = await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(1, 0, 0, 1), first);
        Assert.Single(harness.Store.ReopenRequests);
        Assert.Empty(harness.Resolve.Requests);
        // The failed correction left the recheck incomplete (statement 12).
        Assert.Empty(harness.Store.RecheckMarks);

        // Next sweep: the item is Open again, so the open loop picks it up and
        // rebuilds the identical key — the store replays rather than rejecting.
        harness.Resolve.FailForOperationKeys.Clear();
        harness.Store.RecheckItems.Clear();

        var second = await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(1, 1, 0, 0), second);
        var resolve = Assert.Single(harness.Resolve.Requests);
        Assert.Equal(expectedResolveKey, resolve.OperationKey);
    }

    // Statements 10 and 11.
    [Fact]
    public async Task ACompletedNoChangeRecheckRecordsTheAssociationVersionThisPassRead()
    {
        var harness = new Harness();
        var caseId = Guid.NewGuid();
        var receipt = ManuallyLinked(
            Receipt(Guid.NewGuid(), IntakeDecision.NeedsSorting),
            caseId,
            "QDOS26080",
            associationVersion: 5);
        harness.Receipts.Receipts[receipt.Id] = receipt;
        var item = harness.AddAutomationResolvedItem(
            14,
            UnidentifiedOrigin.Receipt(receipt.Id),
            UnidentifiedResolutionTargetKind.InstructionCase,
            caseId.ToString("N"),
            "QDOS26080");
        harness.Store.RecheckItems.Add(item.Id);

        var result = await harness.Reconciler.ExecuteAsync(50);

        // Nothing changed, so no resolution is written — yet the recheck is
        // completed, or the row would hold the head of the bounded page for
        // ever and starve every later stale resolution (INTK-048).
        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(1, 0, 0, 0), result);
        Assert.Empty(harness.Resolve.Requests);
        Assert.Empty(harness.Store.ReopenRequests);
        var mark = Assert.Single(harness.Store.RecheckMarks);
        Assert.Equal(item.Id, mark.ItemId);
        // Statement 11: the version recorded is the one THIS pass read, never
        // "now" — an association that moves mid-pass is picked up next pass.
        Assert.Equal(5, mark.AssociationVersion);
    }

    // Statement 12.
    [Fact]
    public async Task AFailedCorrectionCountsAFailureAndLeavesTheRecheckIncomplete()
    {
        var harness = new Harness();
        var caseA = Guid.NewGuid();
        var receipt = ManuallyLinked(
            Receipt(Guid.NewGuid(), IntakeDecision.NeedsSorting),
            caseA,
            "QDOS26090",
            associationVersion: 1);
        harness.Receipts.Receipts[receipt.Id] = receipt;
        var item = harness.AddAutomationResolvedItem(
            15,
            UnidentifiedOrigin.Receipt(receipt.Id),
            UnidentifiedResolutionTargetKind.InstructionCase,
            caseA.ToString("N"),
            "QDOS26090");
        harness.Store.RecheckItems.Add(item.Id);
        harness.Receipts.Receipts[receipt.Id] = Unlinked(receipt, associationVersion: 2);
        harness.Store.FailReopenForItems.Add(item.Id);

        var result = await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(1, 0, 0, 1), result);
        Assert.Empty(harness.Store.RecheckMarks);
        Assert.Equal(UnidentifiedState.Resolved, Assert.Single(harness.Store.Items).State);
    }

    private static TriageSummary Triage(Guid id, string registration) =>
        new(
            id,
            registration,
            TriageState.Open,
            AssigneeId: null,
            LinkedCaseId: null,
            Now,
            Version: 0,
            Reference: null,
            Provider: null);

    private static IntakeReceipt ManuallyLinked(
        IntakeReceipt receipt,
        Guid caseId,
        string caseReference,
        long associationVersion) =>
        receipt with
        {
            ManualLinkedCaseId = caseId,
            ManualLinkedCaseReference = caseReference,
            ManualAssociationVersion = associationVersion,
            ManualAssociationActorKind = ActorKind.Staff
        };

    private static IntakeReceipt Unlinked(IntakeReceipt receipt, long associationVersion) =>
        receipt with
        {
            ManualLinkedCaseId = null,
            ManualLinkedCaseReference = null,
            ManualAssociationVersion = associationVersion,
            ManualAssociationActorKind = ActorKind.Staff
        };

    private static IntakeReceipt TriageRequestReceipt(Guid id) =>
        Receipt(id, IntakeDecision.NeedsSorting, "message/rfc822") with
        {
            MailClassificationDecision = MailClassificationResult.Classified(
                MailCategory.Received(
                    ReceivedMailFamily.PreInstructionEmails,
                    MailCategory.TriageRequestSubtype),
                [new("subject.engineer-triage", true, "The subject opens with the generated Triage line.")],
                "Exactly one accepted classification predicate family matched.",
                QdosMailClassificationPolicy.Key,
                QdosMailClassificationPolicy.Version)
        };

    private static IntakeReceipt Receipt(
        Guid id,
        IntakeDecision decision,
        string mediaType = "image/jpeg",
        Guid? acceptedCaseId = null,
        string? acceptedCaseReference = null) =>
        new(
            id,
            "vehicle.jpg",
            mediaType,
            ImageBytes.Length,
            ImageHash,
            new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, id.ToString("N")),
            Now,
            Now,
            decision,
            "Recorded by the pipeline.",
            [],
            [],
            null,
            [],
            null,
            null,
            false,
            "intake_source_reader",
            "1",
            null,
            null,
            [
                new IntakeAssetRecord(
                    Guid.NewGuid(),
                    "uploaded source",
                    "vehicle.jpg",
                    mediaType,
                    IntakeAssetKind.Source,
                    IntakeAssetDisposition.Source,
                    ImageBytes.Length,
                    ImageHash,
                    "storage/0",
                    null,
                    null,
                    null,
                    null)
            ],
            Version: 3,
            AcceptedCaseId: acceptedCaseId,
            AcceptedCaseReference: acceptedCaseReference);

    private static ImageIntakeDetail Detail(Guid intakeId, IntakeReceipt receipt, string reference) =>
        new(
            new ImageIntakeRecord(
                intakeId,
                new ImageIntakeOrigin(
                    receipt.Id,
                    receipt.SourceIdentity,
                    receipt.SourceHash.ToLowerInvariant(),
                    Guid.NewGuid()),
                "AB12CDE",
                reference),
            Now,
            null,
            null);

    private sealed class Harness
    {
        public Harness()
        {
            Resolve = new FakeResolveUnidentified(Store);
            Reconciler = new ReconcileUnidentifiedDestinations(
                Store,
                Resolve,
                Receipts,
                ImageIntakes,
                Triages,
                TimeProvider.System);
        }

        public FakeUnidentifiedStore Store { get; } = new();

        public FakeResolveUnidentified Resolve { get; }

        public FakeReceiptQueries Receipts { get; } = new();

        public FakeImageIntakeQueries ImageIntakes { get; } = new();

        public FakeTriageQueries Triages { get; } = new();

        public ReconcileUnidentifiedDestinations Reconciler { get; }

        public UnidentifiedItem AddOpenItem(long sequence, UnidentifiedOrigin origin)
        {
            var item = new UnidentifiedItem(
                Guid.NewGuid(),
                sequence,
                UnidentifiedReferenceFormat.Create(sequence),
                origin,
                UnidentifiedReasonCode.NoUsableIdentification,
                "Recorded safe detail.",
                UnidentifiedState.Open,
                Now,
                null,
                ActionActor.SystemWorker("intake-processing"),
                null,
                null,
                null,
                null,
                null,
                0);
            Store.Items.Add(item);
            return item;
        }

        /// <summary>
        /// An item this reconciliation itself resolved — the only shape the
        /// recheck sweep may ever write to.
        /// </summary>
        public UnidentifiedItem AddAutomationResolvedItem(
            long sequence,
            UnidentifiedOrigin origin,
            UnidentifiedResolutionTargetKind targetKind,
            string targetId,
            string? targetReference) =>
            AddResolvedItem(
                sequence,
                origin,
                ActionActor.Automation(ReconcileUnidentifiedDestinations.AutomationActorId),
                targetKind,
                targetId,
                targetReference);

        public UnidentifiedItem AddResolvedItem(
            long sequence,
            UnidentifiedOrigin origin,
            ActionActor resolvedBy,
            UnidentifiedResolutionTargetKind targetKind,
            string targetId,
            string? targetReference)
        {
            var item = new UnidentifiedItem(
                Guid.NewGuid(),
                sequence,
                UnidentifiedReferenceFormat.Create(sequence),
                origin,
                UnidentifiedReasonCode.NoUsableIdentification,
                "Recorded safe detail.",
                UnidentifiedState.Resolved,
                Now,
                Now,
                ActionActor.SystemWorker("intake-processing"),
                resolvedBy,
                "Previously resolved.",
                targetKind,
                targetId,
                targetReference,
                1);
            Store.Items.Add(item);
            return item;
        }
    }

    private sealed class FakeUnidentifiedStore : IUnidentifiedStore
    {
        public List<UnidentifiedItem> Items { get; } = [];

        /// <summary>Item ids the recheck queue hands back this pass.</summary>
        public HashSet<Guid> RecheckItems { get; } = [];

        public HashSet<Guid> FailReopenForItems { get; } = [];

        public List<ReopenUnidentifiedRequest> ReopenRequests { get; } = [];

        public List<(Guid ItemId, long AssociationVersion)> RecheckMarks { get; } = [];

        public void Replace(UnidentifiedItem item)
        {
            var index = Items.FindIndex(candidate => candidate.Id == item.Id);
            Items[index] = item;
        }

        public Task<UnidentifiedRegisterResult> RegisterAsync(
            RegisterUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedRegisterResult?> ProbeRegisterReplayAsync(
            RegisterUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedResolveResult> ResolveAsync(
            ResolveUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedResolveResult?> ProbeResolveReplayAsync(
            ResolveUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedReopenResult> ReopenAsync(
            ReopenUnidentifiedRequest request,
            CancellationToken cancellationToken = default)
        {
            UnidentifiedValidation.ValidateReopen(request);
            if (FailReopenForItems.Contains(request.UnidentifiedItemId))
            {
                throw new UnidentifiedVersionConflictException();
            }

            var existing = Items.Single(item => item.Id == request.UnidentifiedItemId);
            if (existing.Version != request.ExpectedVersion
                || existing.State != UnidentifiedState.Resolved)
            {
                throw new UnidentifiedVersionConflictException();
            }

            ReopenRequests.Add(request);
            var reopened = existing with
            {
                State = UnidentifiedState.Open,
                ResolvedAtUtc = null,
                ResolvedBy = null,
                ResolutionReason = null,
                ResolutionTargetKind = null,
                ResolutionTargetId = null,
                ResolutionTargetReference = null,
                Version = existing.Version + 1
            };
            Replace(reopened);
            return Task.FromResult(new UnidentifiedReopenResult(
                reopened,
                new UnidentifiedHistoryEntry(
                    Guid.NewGuid(),
                    request.UnidentifiedItemId,
                    UnidentifiedState.Resolved,
                    UnidentifiedState.Open,
                    request.Actor,
                    request.ReopenedAtUtc,
                    request.Reason,
                    request.OperationKey,
                    null,
                    null,
                    null),
                false));
        }

        public Task<IReadOnlyList<UnidentifiedItem>> ListResolutionsToRecheckAsync(
            int maximum,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UnidentifiedItem>>(
                Items
                    .Where(item => RecheckItems.Contains(item.Id)
                        && item.State == UnidentifiedState.Resolved)
                    .OrderBy(item => item.Sequence)
                    .Take(maximum)
                    .ToArray());

        public Task MarkResolutionRecheckedAsync(
            Guid unidentifiedItemId,
            long associationVersion,
            CancellationToken cancellationToken = default)
        {
            // Mirrors EfUnidentifiedStore: the update is scoped to the
            // still-resolved item, so a watermark is never resurrected onto a
            // resolution the same pass reopened and left open.
            var item = Items.SingleOrDefault(candidate => candidate.Id == unidentifiedItemId);
            if (item is not { State: UnidentifiedState.Resolved })
            {
                return Task.CompletedTask;
            }

            RecheckMarks.Add((unidentifiedItemId, associationVersion));
            RecheckItems.Remove(unidentifiedItemId);
            return Task.CompletedTask;
        }

        public Task<UnidentifiedItem?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(item => item.Id == id));

        public Task<UnidentifiedItem?> GetByReferenceAsync(
            string reference,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(item => item.Reference == reference));

        public Task<UnidentifiedItem?> GetByOriginAsync(
            UnidentifiedOrigin origin,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(item => item.Origin == origin));

        public Task<IReadOnlyList<UnidentifiedItem>> ListAsync(
            UnidentifiedState? state = UnidentifiedState.Open,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UnidentifiedItem>>(
                Items.Where(item => state is null || item.State == state).ToArray());

        public Task<IReadOnlyList<UnidentifiedQueueRow>> ListQueueAsync(
            UnidentifiedMediaKind? mediaKind,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<UnidentifiedHistoryEntry>> HistoryAsync(
            Guid unidentifiedItemId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>
    /// Advances the stored item's version exactly as the real resolve does, so
    /// each transition in a pass builds its key from a version of its own.
    /// </summary>
    private sealed class FakeResolveUnidentified(FakeUnidentifiedStore store) : IResolveUnidentified
    {
        public List<ResolveUnidentifiedRequest> Requests { get; } = [];

        public HashSet<string> FailForOperationKeys { get; } = [];

        public Task<UnidentifiedResolveResult> ExecuteAsync(
            ResolveUnidentifiedRequest request,
            CancellationToken cancellationToken = default)
        {
            if (FailForOperationKeys.Contains(request.OperationKey))
            {
                throw new InvalidOperationException("Simulated transient resolution failure.");
            }

            Requests.Add(request);
            var existing = store.Items.Single(item => item.Id == request.UnidentifiedItemId);
            var resolved = existing with
            {
                State = UnidentifiedState.Resolved,
                ResolvedAtUtc = request.ResolvedAtUtc,
                ResolvedBy = request.Actor,
                ResolutionReason = request.Reason,
                ResolutionTargetKind = request.TargetKind,
                ResolutionTargetId = request.TargetId,
                ResolutionTargetReference = request.TargetReference,
                Version = existing.Version + 1
            };
            store.Replace(resolved);
            return Task.FromResult(new UnidentifiedResolveResult(
                resolved,
                new UnidentifiedHistoryEntry(
                    Guid.NewGuid(),
                    request.UnidentifiedItemId,
                    UnidentifiedState.Open,
                    UnidentifiedState.Resolved,
                    request.Actor,
                    request.ResolvedAtUtc,
                    request.Reason,
                    request.OperationKey,
                    request.TargetKind,
                    request.TargetId,
                    request.TargetReference),
                false));
        }
    }

    private sealed class FakeReceiptQueries : IIntakeReceiptQueries
    {
        public Dictionary<Guid, IntakeReceipt> Receipts { get; } = [];

        public Task<IntakeQueueCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new IntakeQueueCounts(0, 0));

        public Task<IntakeListPage> ListAsync(
            IntakeDecision? decision,
            int page,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new IntakeListPage([], page, pageSize, 0));

        public Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Receipts.TryGetValue(id, out var receipt) ? receipt : null);

        public Task<IntakeAssetRecord?> GetAssetAsync(
            Guid receiptId,
            Guid assetId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IntakeAssetRecord?>(null);
    }

    private sealed class FakeTriageQueries : ITriageQueries
    {
        public Dictionary<Guid, TriageSummary> SummariesByOriginReceipt { get; } = [];

        public Task<IReadOnlyList<TriageSummary>> ListAsync(
            TriageState? state,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<TriageDetail?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<TriageSummary?> GetByOriginReceiptAsync(
            Guid originReceiptId,
            CancellationToken cancellationToken) =>
            Task.FromResult(SummariesByOriginReceipt.TryGetValue(originReceiptId, out var summary)
                ? summary
                : null);
    }

    private sealed class FakeImageIntakeQueries : IImageIntakeQueries
    {
        public Dictionary<Guid, ImageIntakeDetail> DetailsByOriginReceipt { get; } = [];

        public Task<IReadOnlyList<ImageIntakeSummary>> ListAsync(
            bool? associated,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageIntakeSummary>>([]);

        public Task<ImageIntakeDetail?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<ImageIntakeDetail?>(null);

        public Task<ImageIntakeDetail?> GetByReferenceAsync(
            string imageIntakeReference,
            CancellationToken cancellationToken) =>
            Task.FromResult<ImageIntakeDetail?>(null);

        public Task<ImageIntakeDetail?> GetByOriginReceiptAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                DetailsByOriginReceipt.TryGetValue(intakeReceiptId, out var detail) ? detail : null);

        public Task<IReadOnlyList<ImageIntakeSummary>> ListByOriginReceiptsAsync(
            IReadOnlyCollection<Guid> intakeReceiptIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageIntakeSummary>>([]);

        public Task<IReadOnlyList<ImageIntakeSummary>> ListForCaseAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageIntakeSummary>>([]);

        public Task<IReadOnlyList<ImageIntakeSummary>> SearchByRegistrationAsync(
            string normalizedVehicleRegistration,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageIntakeSummary>>([]);
    }
}
