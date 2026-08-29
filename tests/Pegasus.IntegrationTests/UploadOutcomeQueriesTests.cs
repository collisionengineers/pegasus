using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Web.Authentication;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The confirmation-step decision table (INTK-010 research.md/plan.md, step
/// 5) exercised directly against <see cref="UploadOutcomeQueries"/> with
/// hand-built fakes for its three read ports — fast and precise, since every
/// branch is a pure function of what those ports return. The Web-hosted
/// end-to-end path (a real upload reaching a real Complete/Failed status) is
/// covered separately in <c>QdosIntakeWebTests</c> and the Browser suite.
/// </summary>
public sealed class UploadOutcomeQueriesTests
{
    private static readonly ActionActor StaffActor = ActionActor.Staff(
        DevelopmentOfflineIdentity.AdministratorId,
        [StaffRole.Administrator]);

    [Fact]
    public async Task ReceivedIsStillWorkingWithNoOffer()
    {
        var result = await BuildAsync(StatusOf(QueuedIntakeStatusKind.Received));

        Assert.Equal(UploadOutcomeKind.Working, result.Kind);
        Assert.True(result.IsStillWorking);
        Assert.Null(result.ChipLabel);
        Assert.Null(result.PrimaryAction);
    }

    [Fact]
    public async Task ProcessingIsStillWorkingWithNoOffer()
    {
        var result = await BuildAsync(StatusOf(QueuedIntakeStatusKind.Processing));

        Assert.Equal(UploadOutcomeKind.Working, result.Kind);
        Assert.True(result.IsStillWorking);
    }

    [Fact]
    public async Task FailedFileStatesItsFailureWithNoOffer()
    {
        var status = StatusOf(QueuedIntakeStatusKind.Failed, failureCode: "unreadable_pdf");

        var result = await BuildAsync(status);

        Assert.Equal(UploadOutcomeKind.Failed, result.Kind);
        Assert.Contains("PDF could not be read", result.Message, StringComparison.Ordinal);
        Assert.Null(result.PrimaryAction);
    }

    [Fact]
    public async Task CompleteWithACaseAlreadyAttachedIsReportedNotReOffered()
    {
        var caseId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var status = StatusOf(QueuedIntakeStatusKind.Complete, receiptId: receiptId);
        var receipt = MakeReceipt(receiptId, IntakeDecision.CaseCreated, acceptedCaseId: caseId, acceptedCaseReference: "AB12CDE-01");

        var result = await BuildAsync(status, receipt);

        Assert.Equal(UploadOutcomeKind.Attached, result.Kind);
        Assert.Equal("Success", result.ChipLabel);
        Assert.Contains("AB12CDE-01", result.Message, StringComparison.Ordinal);
        Assert.NotNull(result.PrimaryAction);
        Assert.Equal($"/Cases/Details/{caseId:D}", result.PrimaryAction!.Url);
        Assert.NotNull(result.SecondaryAction);
        Assert.Equal($"/Received/{receiptId:D}", result.SecondaryAction!.Url);
    }

    [Fact]
    public async Task NoCaseImageGroupReportsTheAutomaticallyRegisteredImageInitiatedCase()
    {
        var receiptId = Guid.NewGuid();
        var status = StatusOf(QueuedIntakeStatusKind.Complete, receiptId: receiptId);
        var receipt = MakeReceipt(receiptId, IntakeDecision.ImageIntakeRegistered);
        var imageIntakeId = Guid.NewGuid();
        var detail = new ImageIntakeDetail(
            new ImageIntakeRecord(
                imageIntakeId,
                new ImageIntakeOrigin(receiptId, new(IntakeSourceChannel.ManualUpload, "token"), "hash", Guid.NewGuid()),
                "AB12CDE",
                "AB12CDE-01"),
            DateTimeOffset.UtcNow,
            null,
            null);

        var result = await BuildAsync(status, receipt, imageIntakeDetail: detail);

        Assert.Equal(UploadOutcomeKind.ImageCaseRegistered, result.Kind);
        Assert.Contains("AB12CDE-01", result.Message, StringComparison.Ordinal);
        Assert.NotNull(result.PrimaryAction);
        Assert.Equal($"/VehicleImages/{imageIntakeId:D}", result.PrimaryAction!.Url);
    }

    [Fact]
    public async Task NoCaseInstructionDocumentOffersToCreateOne()
    {
        var receiptId = Guid.NewGuid();
        var status = StatusOf(QueuedIntakeStatusKind.Complete, receiptId: receiptId);
        var receipt = MakeReceipt(receiptId, IntakeDecision.OcrRequired);

        var result = await BuildAsync(status, receipt);

        Assert.Equal(UploadOutcomeKind.ReadyToCreate, result.Kind);
        Assert.NotNull(result.PrimaryAction);
        Assert.Equal("Create a case", result.PrimaryAction!.Label);
        Assert.Equal($"/Cases/Create?receiptId={receiptId:D}", result.PrimaryAction!.Url);
    }

    [Fact]
    public async Task AmbiguousCandidateMatchOffersToReviewAndAttachWithOverride()
    {
        var receiptId = Guid.NewGuid();
        var status = StatusOf(QueuedIntakeStatusKind.Complete, receiptId: receiptId);
        var caseMatch = new CaseMatchEvaluationResult(
            CaseMatchOutcome.Ambiguous,
            null,
            null,
            new(null, null, "smith", "j", null),
            [
                new(Guid.NewGuid(), ["surname"], []),
                new(Guid.NewGuid(), ["surname"], [])
            ],
            "More than one case matched.",
            "test-policy",
            1);
        var receipt = MakeReceipt(receiptId, IntakeDecision.NeedsSorting, caseMatchDecision: caseMatch);

        var result = await BuildAsync(status, receipt);

        Assert.Equal(UploadOutcomeKind.PossibleMatch, result.Kind);
        Assert.NotNull(result.PrimaryAction);
        Assert.Equal("Review and attach", result.PrimaryAction!.Label);
        Assert.Equal($"/Received/{receiptId:D}", result.PrimaryAction!.Url);
    }

    [Fact]
    public async Task NoUsableVrmImageGroupRoutedToUnidentifiedIsReportedForReview()
    {
        var receiptId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var status = StatusOf(QueuedIntakeStatusKind.Complete, receiptId: receiptId);
        var receipt = MakeReceipt(receiptId, IntakeDecision.NeedsSorting);
        var unidentifiedId = Guid.NewGuid();
        var byGroup = new UnidentifiedItem(
            unidentifiedId, 1, "U1", UnidentifiedOrigin.SubmissionGroup(groupId),
            UnidentifiedReasonCode.ConflictingIdentification, "Conflicting registrations across the group.",
            UnidentifiedState.Open, DateTimeOffset.UtcNow, null,
            ActionActor.Automation("vrm"), null, null, null, null, null, 0);

        // Nothing registered against the receipt itself (a split/grouped
        // image case) — only the group-level origin, which is exactly
        // INTK-006/007's "kept intact as one group" routing. The builder
        // must fall back to it rather than reporting this member as if
        // nothing happened.
        var result = await BuildAsync(status, receipt, submissionGroupId: groupId, unidentifiedByGroup: byGroup);

        Assert.Equal(UploadOutcomeKind.NeedsReview, result.Kind);
        Assert.NotNull(result.PrimaryAction);
        Assert.Equal($"/Unidentified/{unidentifiedId:D}", result.PrimaryAction!.Url);
    }

    [Fact]
    public async Task CompletedGroupedImageWithoutASettledDestinationIsStillProcessing()
    {
        var receiptId = Guid.NewGuid();
        var status = StatusOf(QueuedIntakeStatusKind.Complete, receiptId: receiptId);
        var receipt = MakeReceipt(
            receiptId,
            IntakeDecision.NeedsSorting,
            mediaType: "image/jpeg");

        var result = await BuildAsync(
            status,
            receipt,
            submissionGroupId: Guid.NewGuid());

        Assert.Equal(UploadOutcomeKind.Working, result.Kind);
        Assert.True(result.IsStillWorking);
        Assert.False(result.IsOpenDecision);
        Assert.Null(result.PrimaryAction);
        Assert.Null(result.Attach);
    }

    [Fact]
    public async Task ResolvedGroupedUnidentifiedItemIsReportedWithoutPollingOrAnotherDecision()
    {
        var receiptId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var unidentifiedId = Guid.NewGuid();
        var status = StatusOf(QueuedIntakeStatusKind.Complete, receiptId: receiptId);
        var receipt = MakeReceipt(
            receiptId,
            IntakeDecision.NeedsSorting,
            mediaType: "image/jpeg");
        var resolved = new UnidentifiedItem(
            unidentifiedId,
            1,
            "U1",
            UnidentifiedOrigin.SubmissionGroup(groupId),
            UnidentifiedReasonCode.NoUsableIdentification,
            "No usable registration was found.",
            UnidentifiedState.Resolved,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow,
            ActionActor.Automation("vrm"),
            StaffActor,
            "Matched outside Pegasus.",
            UnidentifiedResolutionTargetKind.ExternalReference,
            "external-1",
            "EXTERNAL-1",
            1);

        var result = await BuildAsync(
            status,
            receipt,
            submissionGroupId: groupId,
            unidentifiedByGroup: resolved);

        Assert.Equal(UploadOutcomeKind.Resolved, result.Kind);
        Assert.False(result.IsStillWorking);
        Assert.False(result.IsOpenDecision);
        Assert.Contains("EXTERNAL-1", result.Message, StringComparison.Ordinal);
        Assert.Equal($"/Unidentified/{unidentifiedId:D}", result.PrimaryAction?.Url);
        Assert.Null(result.Attach);
    }

    [Fact]
    public async Task BlockedIntakeCannotBecomeACaseAndIsNotOfferedOne()
    {
        var receiptId = Guid.NewGuid();
        var status = StatusOf(QueuedIntakeStatusKind.Complete, receiptId: receiptId);
        var receipt = MakeReceipt(receiptId, IntakeDecision.BlockedIntake);

        var result = await BuildAsync(status, receipt);

        Assert.Equal(UploadOutcomeKind.CannotBecomeCase, result.Kind);
        Assert.Contains("blocked", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BlockedGroupedImageRemainsTerminal()
    {
        var receiptId = Guid.NewGuid();
        var status = StatusOf(QueuedIntakeStatusKind.Complete, receiptId: receiptId);
        var receipt = MakeReceipt(
            receiptId,
            IntakeDecision.BlockedIntake,
            mediaType: "image/jpeg");

        var result = await BuildAsync(
            status,
            receipt,
            submissionGroupId: Guid.NewGuid());

        Assert.Equal(UploadOutcomeKind.CannotBecomeCase, result.Kind);
        Assert.False(result.IsStillWorking);
        Assert.False(result.IsOpenDecision);
        Assert.Null(result.Attach);
    }

    [Fact]
    public async Task OpenStaffDecisionsCarryTheAddToExistingCaseOffer()
    {
        var receiptId = Guid.NewGuid();
        var status = StatusOf(QueuedIntakeStatusKind.Complete, receiptId: receiptId);

        var readyToCreate = await BuildAsync(status, MakeReceipt(receiptId, IntakeDecision.OcrRequired));
        Assert.Equal(UploadOutcomeKind.ReadyToCreate, readyToCreate.Kind);
        Assert.NotNull(readyToCreate.Attach);
        Assert.Equal(receiptId, readyToCreate.Attach!.ReceiptId);

        var attached = await BuildAsync(
            StatusOf(QueuedIntakeStatusKind.Complete, receiptId: receiptId),
            MakeReceipt(receiptId, IntakeDecision.CaseCreated, acceptedCaseId: Guid.NewGuid()));
        Assert.Equal(UploadOutcomeKind.Attached, attached.Kind);
        Assert.Null(attached.Attach);
    }

    [Fact]
    public async Task AwaitingImageRegistrationOffersAttachAgainstItsOriginReceipt()
    {
        var receiptId = Guid.NewGuid();
        var originReceiptId = Guid.NewGuid();
        var status = StatusOf(QueuedIntakeStatusKind.Complete, receiptId: receiptId);
        var receipt = MakeReceipt(receiptId, IntakeDecision.ImageIntakeRegistered);
        var detail = new ImageIntakeDetail(
            new ImageIntakeRecord(
                Guid.NewGuid(),
                new ImageIntakeOrigin(originReceiptId, new(IntakeSourceChannel.ManualUpload, "token"), "hash", Guid.NewGuid()),
                "AB12CDE",
                "AB12CDE-01"),
            DateTimeOffset.UtcNow,
            null,
            null);

        var result = await BuildAsync(status, receipt, imageIntakeDetail: detail);

        Assert.Equal(UploadOutcomeKind.ImageCaseRegistered, result.Kind);
        Assert.NotNull(result.Attach);
        // The staff decision links the registration's origin receipt so the
        // whole registered group merges, whichever member row offered it.
        Assert.Equal(originReceiptId, result.Attach!.ReceiptId);
    }

    [Fact]
    public async Task MergedImageRegistrationReportsItsCaseInsteadOfTheRegistration()
    {
        var receiptId = Guid.NewGuid();
        var mergedCaseId = Guid.NewGuid();
        var status = StatusOf(QueuedIntakeStatusKind.Complete, receiptId: receiptId);
        var receipt = MakeReceipt(receiptId, IntakeDecision.ImageIntakeRegistered);
        var detail = new ImageIntakeDetail(
            new ImageIntakeRecord(
                Guid.NewGuid(),
                new ImageIntakeOrigin(Guid.NewGuid(), new(IntakeSourceChannel.ManualUpload, "token"), "hash", Guid.NewGuid()),
                "AB12CDE",
                "AB12CDE-01",
                ImageInitiatedCaseState.MergedIntoInstructionCase,
                MergedIntoCaseId: mergedCaseId,
                MergedIntoCaseReference: "QDO31001"),
            DateTimeOffset.UtcNow,
            mergedCaseId,
            "QDO31001");

        var result = await BuildAsync(status, receipt, imageIntakeDetail: detail);

        Assert.Equal(UploadOutcomeKind.Attached, result.Kind);
        Assert.Contains("QDO31001", result.Message, StringComparison.Ordinal);
        Assert.Equal($"/Cases/Details/{mergedCaseId:D}", result.PrimaryAction!.Url);
        Assert.Null(result.Attach);
    }

    [Fact]
    public async Task StaffLinkedCaseIsNotReportedAsAutomatic()
    {
        var caseId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var status = StatusOf(QueuedIntakeStatusKind.Complete, receiptId: receiptId);
        var receipt = MakeReceipt(
            receiptId,
            IntakeDecision.CaseCreated,
            manualLinkedCaseId: caseId,
            manualLinkedCaseReference: "QDO31002",
            manualAssociationVersion: 3,
            manualAssociationActorKind: ActorKind.Staff);

        var result = await BuildAsync(status, receipt);

        Assert.Equal(UploadOutcomeKind.Attached, result.Kind);
        Assert.Contains("added to case QDO31002", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("automatically", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.Attach);
    }

    private static QueuedIntakeStatus StatusOf(
        QueuedIntakeStatusKind kind,
        Guid? receiptId = null,
        string? failureCode = null)
    {
        var id = receiptId ?? Guid.NewGuid();
        return new(id, "example.pdf", DateTimeOffset.UtcNow, kind, id, failureCode);
    }

    private static IntakeReceipt MakeReceipt(
        Guid id,
        IntakeDecision decision,
        Guid? acceptedCaseId = null,
        string? acceptedCaseReference = null,
        CaseMatchEvaluationResult? caseMatchDecision = null,
        Guid? manualLinkedCaseId = null,
        string? manualLinkedCaseReference = null,
        long? manualAssociationVersion = null,
        ActorKind? manualAssociationActorKind = null,
        string mediaType = "application/pdf") =>
        new(
            id,
            "example.pdf",
            mediaType,
            1024,
            "hash",
            new(IntakeSourceChannel.ManualUpload, "token"),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            decision,
            "reason",
            [],
            [],
            null,
            [],
            null,
            null,
            false,
            "reader",
            "1",
            null,
            null,
            Assets: mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                ? [
                    new(
                        Guid.NewGuid(),
                        "uploaded source",
                        "example.jpg",
                        mediaType,
                        IntakeAssetKind.Source,
                        IntakeAssetDisposition.Source,
                        1024,
                        new string('A', 64),
                        "test-storage-key",
                        null,
                        null,
                        null,
                        null)
                  ]
                : null,
            AcceptedCaseId: acceptedCaseId,
            AcceptedCaseReference: acceptedCaseReference,
            CaseMatchDecision: caseMatchDecision,
            ManualLinkedCaseId: manualLinkedCaseId,
            ManualLinkedCaseReference: manualLinkedCaseReference,
            ManualAssociationVersion: manualAssociationVersion,
            ManualAssociationActorKind: manualAssociationActorKind);

    private static Task<UploadOutcomeView> BuildAsync(
        QueuedIntakeStatus status,
        IntakeReceipt? receipt = null,
        Guid? submissionGroupId = null,
        ImageIntakeDetail? imageIntakeDetail = null,
        UnidentifiedItem? unidentifiedByReceipt = null,
        UnidentifiedItem? unidentifiedByGroup = null)
    {
        var queries = new UploadOutcomeQueries(
            new FakeGetIntake(receipt),
            new FakeImageIntakeQueries(imageIntakeDetail),
            new FakeUnidentifiedStore(unidentifiedByReceipt, unidentifiedByGroup));
        return queries.BuildAsync(status, submissionGroupId, StaffActor);
    }

    private sealed class FakeGetIntake(IntakeReceipt? receipt) : IGetIntake
    {
        public Task<IntakeReceipt?> ExecuteAsync(
            GetIntakeQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(receipt);
    }

    private sealed class FakeImageIntakeQueries(ImageIntakeDetail? detail) : IImageIntakeQueries
    {
        public Task<IReadOnlyList<ImageIntakeSummary>> ListAsync(
            bool? associated, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageIntakeSummary>>([]);

        public Task<ImageIntakeDetail?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(detail);

        public Task<ImageIntakeDetail?> GetByReferenceAsync(
            string imageIntakeReference, CancellationToken cancellationToken) =>
            Task.FromResult(detail);

        public Task<ImageIntakeDetail?> GetByOriginReceiptAsync(
            Guid intakeReceiptId, CancellationToken cancellationToken) =>
            Task.FromResult(detail);

        public Task<IReadOnlyList<ImageIntakeSummary>> ListByOriginReceiptsAsync(
            IReadOnlyCollection<Guid> intakeReceiptIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageIntakeSummary>>([]);

        public Task<IReadOnlyList<ImageIntakeSummary>> ListForCaseAsync(
            Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageIntakeSummary>>([]);

        public Task<IReadOnlyList<ImageIntakeSummary>> SearchByRegistrationAsync(
            string normalizedVehicleRegistration, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageIntakeSummary>>([]);
    }

    private sealed class FakeUnidentifiedStore(
        UnidentifiedItem? byReceipt,
        UnidentifiedItem? byGroup) : IUnidentifiedStore
    {
        public Task<UnidentifiedRegisterResult> RegisterAsync(
            RegisterUnidentifiedRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UnidentifiedRegisterResult?> ProbeRegisterReplayAsync(
            RegisterUnidentifiedRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UnidentifiedResolveResult> ResolveAsync(
            ResolveUnidentifiedRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UnidentifiedReopenResult> ReopenAsync(
            ReopenUnidentifiedRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UnidentifiedResolveResult?> ProbeResolveReplayAsync(
            ResolveUnidentifiedRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UnidentifiedItem?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UnidentifiedItem?> GetByReferenceAsync(
            string reference, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<UnidentifiedQueueRow>> ListQueueAsync(
            UnidentifiedMediaKind? mediaKind, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UnidentifiedItem?> GetByOriginAsync(
            UnidentifiedOrigin origin, CancellationToken cancellationToken = default) =>
            Task.FromResult(origin.Kind == UnidentifiedOriginKind.Receipt ? byReceipt : byGroup);

        public Task<IReadOnlyList<UnidentifiedItem>> ListAsync(
            UnidentifiedState? state = UnidentifiedState.Open, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<UnidentifiedItem>> ListResolutionsToRecheckAsync(
            int maximum, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<UnidentifiedHistoryEntry>> HistoryAsync(
            Guid unidentifiedItemId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
