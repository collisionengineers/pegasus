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
            $"intake-unidentified-reconcile:{receipt.Id:N}:{receipt.Version}",
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

    [Fact]
    public async Task ManuallyLinkedUnidentifiedReceiptResolvesToTheInstructionCase()
    {
        var harness = new Harness();
        var caseId = Guid.NewGuid();
        var receipt = Receipt(Guid.NewGuid(), IntakeDecision.NeedsSorting) with
        {
            ManualLinkedCaseId = caseId,
            ManualAssociationVersion = 0,
            ManualLinkedCaseReference = "QDOS26030"
        };
        harness.Receipts.Receipts[receipt.Id] = receipt;
        harness.AddOpenItem(3, UnidentifiedOrigin.Receipt(receipt.Id));

        var result = await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(1, result.Resolved);
        var resolve = Assert.Single(harness.Resolve.Requests);
        Assert.Equal(UnidentifiedResolutionTargetKind.InstructionCase, resolve.TargetKind);
        Assert.Equal(caseId.ToString("N"), resolve.TargetId);
        Assert.Equal("QDOS26030", resolve.TargetReference);
    }

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
        harness.AddOpenItem(6, UnidentifiedOrigin.Receipt(failing.Id));
        harness.AddOpenItem(7, UnidentifiedOrigin.Receipt(succeeding.Id));
        harness.ImageIntakes.DetailsByOriginReceipt[failing.Id] = Detail(Guid.NewGuid(), failing, "AB12CDE-01");
        harness.ImageIntakes.DetailsByOriginReceipt[succeeding.Id] = Detail(Guid.NewGuid(), succeeding, "AB12CDE-02");
        harness.Resolve.FailForReceiptOperationKeys.Add(
            $"intake-unidentified-reconcile:{failing.Id:N}:{failing.Version}");

        var result = await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(2, 1, 0, 1), result);
    }

    [Fact]
    public async Task AnAlreadyStaffResolvedItemIsANoOp()
    {
        var harness = new Harness();
        var receipt = Receipt(Guid.NewGuid(), IntakeDecision.ImageIntakeRegistered);
        harness.ImageIntakes.DetailsByOriginReceipt[receipt.Id] = Detail(Guid.NewGuid(), receipt, "AB12CDE-01");
        var item = harness.AddResolvedItem(8, UnidentifiedOrigin.Receipt(receipt.Id)) with
        {
            ResolvedBy = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator])
        };
        harness.Store.Replace(item);

        var resolved = await harness.Reconciler.SynchronizeForReceiptAsync(receipt, CancellationToken.None);

        Assert.False(resolved);
        Assert.Empty(harness.Resolve.Requests);
    }

    [Fact]
    public async Task ChangedManualAssociationReopensAnAutomationResolvedItemWhenTheLinkIsRemoved()
    {
        var harness = new Harness();
        var receipt = Receipt(Guid.NewGuid(), IntakeDecision.NeedsSorting) with
        {
            ManualAssociationVersion = 1
        };
        harness.Receipts.Receipts[receipt.Id] = receipt;
        var item = harness.AddAutomationResolvedCaseItem(
            9, UnidentifiedOrigin.Receipt(receipt.Id), Guid.NewGuid(), "QDOS26030");
        harness.Store.RecheckItems.Add(item);

        var result = await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(0, 0, 1, 0), result);
        var reopened = Assert.Single(harness.Store.ReopenRequests);
        Assert.Equal(item.Id, reopened.UnidentifiedItemId);
        var current = Assert.Single(harness.Store.Items);
        Assert.Equal(UnidentifiedState.Open, current.State);
        Assert.Null(current.ResolvedAtUtc);
        Assert.Null(current.ResolvedBy);
        Assert.Null(current.ResolutionReason);
        Assert.Null(current.ResolutionTargetKind);
        Assert.Null(current.ResolutionTargetId);
        Assert.Null(current.ResolutionTargetReference);
        Assert.Empty(harness.Resolve.Requests);
    }

    [Fact]
    public async Task ChangedManualAssociationReopensAndRetargetsAnAutomationResolvedItem()
    {
        var harness = new Harness();
        var caseA = Guid.NewGuid();
        var caseB = Guid.NewGuid();
        var receipt = Receipt(Guid.NewGuid(), IntakeDecision.NeedsSorting) with
        {
            ManualLinkedCaseId = caseB,
            ManualAssociationVersion = 1,
            ManualLinkedCaseReference = "QDOS26031"
        };
        harness.Receipts.Receipts[receipt.Id] = receipt;
        var item = harness.AddAutomationResolvedCaseItem(
            10, UnidentifiedOrigin.Receipt(receipt.Id), caseA, "QDOS26030");
        harness.Store.RecheckItems.Add(item);

        var result = await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(0, 0, 1, 0), result);
        Assert.Single(harness.Store.ReopenRequests);
        var resolve = Assert.Single(harness.Resolve.Requests);
        Assert.Equal(caseB.ToString("N"), resolve.TargetId);
        Assert.Equal("QDOS26031", resolve.TargetReference);
        var current = Assert.Single(harness.Store.Items);
        Assert.Equal(UnidentifiedState.Resolved, current.State);
        Assert.Equal(caseB.ToString("N"), current.ResolutionTargetId);
        Assert.Equal("QDOS26031", current.ResolutionTargetReference);
    }

    [Fact]
    public async Task ChangedManualAssociationNeverReopensAStaffResolution()
    {
        var harness = new Harness();
        var receipt = Receipt(Guid.NewGuid(), IntakeDecision.NeedsSorting) with
        {
            ManualAssociationVersion = 1
        };
        harness.Receipts.Receipts[receipt.Id] = receipt;
        var item = harness.AddAutomationResolvedCaseItem(
            11, UnidentifiedOrigin.Receipt(receipt.Id), Guid.NewGuid(), "QDOS26030") with
        {
            ResolvedBy = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator])
        };
        harness.Store.Replace(item);
        harness.Store.RecheckItems.Add(item);

        var result = await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(0, 0, 0, 0), result);
        Assert.Empty(harness.Store.ReopenRequests);
        Assert.Empty(harness.Resolve.Requests);
    }

    [Fact]
    public async Task UnchangedAutomationResolutionIsANoOp()
    {
        var harness = new Harness();
        var caseId = Guid.NewGuid();
        var receipt = Receipt(Guid.NewGuid(), IntakeDecision.NeedsSorting) with
        {
            ManualLinkedCaseId = caseId,
            ManualAssociationVersion = 1,
            ManualLinkedCaseReference = "QDOS26030"
        };
        harness.Receipts.Receipts[receipt.Id] = receipt;
        var item = harness.AddAutomationResolvedCaseItem(
            12, UnidentifiedOrigin.Receipt(receipt.Id), caseId, "QDOS26030");
        harness.Store.RecheckItems.Add(item);

        var result = await harness.Reconciler.ExecuteAsync(50);

        Assert.Equal(new ReconcileUnidentifiedDestinationsResult(0, 0, 0, 0), result);
        Assert.Empty(harness.Store.ReopenRequests);
        Assert.Empty(harness.Resolve.Requests);
    }

    [Fact]
    public async Task ManuallyLinkedImageIntakeReceiptKeepsImageIntakePrecedence()
    {
        var harness = new Harness();
        var receipt = Receipt(Guid.NewGuid(), IntakeDecision.ImageIntakeRegistered) with
        {
            ManualLinkedCaseId = Guid.NewGuid(),
            ManualAssociationVersion = 1,
            ManualLinkedCaseReference = "QDOS26030"
        };
        harness.Receipts.Receipts[receipt.Id] = receipt;
        harness.AddOpenItem(13, UnidentifiedOrigin.Receipt(receipt.Id));
        var imageIntakeId = Guid.NewGuid();
        harness.ImageIntakes.DetailsByOriginReceipt[receipt.Id] =
            Detail(imageIntakeId, receipt, "AB12CDE-01");

        await harness.Reconciler.ExecuteAsync(50);

        var resolve = Assert.Single(harness.Resolve.Requests);
        Assert.Equal(UnidentifiedResolutionTargetKind.ImageIntake, resolve.TargetKind);
        Assert.Equal(imageIntakeId.ToString("N"), resolve.TargetId);
    }

    [Fact]
    public async Task ManuallyLinkedTriageRequestKeepsTriagePrecedence()
    {
        var harness = new Harness();
        var receipt = TriageRequestReceipt(Guid.NewGuid()) with
        {
            ManualLinkedCaseId = Guid.NewGuid(),
            ManualAssociationVersion = 1,
            ManualLinkedCaseReference = "QDOS26030"
        };
        harness.Receipts.Receipts[receipt.Id] = receipt;
        harness.AddOpenItem(14, UnidentifiedOrigin.Receipt(receipt.Id));
        var triageId = Guid.NewGuid();
        harness.Triages.SummariesByOriginReceipt[receipt.Id] =
            new(triageId, "VO75DFJ", TriageState.Open, null, null, Now, 0);

        await harness.Reconciler.ExecuteAsync(50);

        var resolve = Assert.Single(harness.Resolve.Requests);
        Assert.Equal(UnidentifiedResolutionTargetKind.Triage, resolve.TargetKind);
        Assert.Equal(triageId.ToString("N"), resolve.TargetId);
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
        harness.Triages.SummariesByOriginReceipt[receipt.Id] =
            new(triageId, "VO75DFJ", TriageState.Open, null, null, Now, 0);

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

        public UnidentifiedItem AddResolvedItem(long sequence, UnidentifiedOrigin origin)
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
                ActionActor.Automation("intake-processing"),
                "Previously resolved.",
                UnidentifiedResolutionTargetKind.ExternalReference,
                "earlier-target",
                null,
                1);
            Store.Items.Add(item);
            return item;
        }

        public UnidentifiedItem AddAutomationResolvedCaseItem(
            long sequence,
            UnidentifiedOrigin origin,
            Guid caseId,
            string reference)
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
                ActionActor.Automation("intake-processing"),
                "Previously resolved.",
                UnidentifiedResolutionTargetKind.InstructionCase,
                caseId.ToString("N"),
                reference,
                1);
            Store.Items.Add(item);
            return item;
        }
    }

    private sealed class FakeUnidentifiedStore : IUnidentifiedStore
    {
        public List<UnidentifiedItem> Items { get; } = [];

        public List<UnidentifiedItem> RecheckItems { get; } = [];

        public List<ReopenUnidentifiedRequest> ReopenRequests { get; } = [];

        public Task<UnidentifiedRegisterResult> RegisterAsync(
            RegisterUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedRegisterResult?> ProbeRegisterReplayAsync(
            RegisterUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedResolveResult> ResolveAsync(
            ResolveUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedReopenResult> ReopenAsync(
            ReopenUnidentifiedRequest request,
            CancellationToken cancellationToken = default)
        {
            ReopenRequests.Add(request);
            var current = Items.Single(item => item.Id == request.UnidentifiedItemId);
            var reopened = current with
            {
                State = UnidentifiedState.Open,
                ResolvedAtUtc = null,
                ResolvedBy = null,
                ResolutionReason = null,
                ResolutionTargetKind = null,
                ResolutionTargetId = null,
                ResolutionTargetReference = null,
                Version = current.Version + 1
            };
            Replace(reopened);
            return Task.FromResult(new UnidentifiedReopenResult(
                reopened,
                new(
                    Guid.NewGuid(),
                    reopened.Id,
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

        public Task<UnidentifiedResolveResult?> ProbeResolveReplayAsync(
            ResolveUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

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

        public Task<IReadOnlyList<UnidentifiedItem>> ListResolutionsToRecheckAsync(
            int maximum,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UnidentifiedItem>>(RecheckItems.Take(maximum).ToArray());

        public Task<IReadOnlyList<UnidentifiedQueueRow>> ListQueueAsync(
            UnidentifiedMediaKind? mediaKind,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<UnidentifiedHistoryEntry>> HistoryAsync(
            Guid unidentifiedItemId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Replace(UnidentifiedItem item)
        {
            var index = Items.FindIndex(existing => existing.Id == item.Id);
            Items[index] = item;
        }
    }

    private sealed class FakeResolveUnidentified(FakeUnidentifiedStore store) : IResolveUnidentified
    {
        public List<ResolveUnidentifiedRequest> Requests { get; } = [];

        public HashSet<string> FailForReceiptOperationKeys { get; } = [];

        public Task<UnidentifiedResolveResult> ExecuteAsync(
            ResolveUnidentifiedRequest request,
            CancellationToken cancellationToken = default)
        {
            if (FailForReceiptOperationKeys.Contains(request.OperationKey))
            {
                throw new InvalidOperationException("Simulated transient resolution failure.");
            }

            Requests.Add(request);
            var current = store.Items.Single(item => item.Id == request.UnidentifiedItemId);
            var resolved = current with
            {
                State = UnidentifiedState.Resolved,
                ResolvedAtUtc = request.ResolvedAtUtc,
                ResolvedBy = request.Actor,
                ResolutionReason = request.Reason,
                ResolutionTargetKind = request.TargetKind,
                ResolutionTargetId = request.TargetId,
                ResolutionTargetReference = request.TargetReference,
                Version = current.Version + 1
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
