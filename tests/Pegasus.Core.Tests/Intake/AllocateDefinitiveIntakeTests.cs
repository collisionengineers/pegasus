using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

public sealed class AllocateDefinitiveIntakeTests
{
    [Fact]
    public async Task AutomaticAllocationUsesPersistedTypedCaseType()
    {
        var receipt = Receipt(CaseType.InspectionAndAudit, "QDOS");
        var store = new RecordingAllocationStore();
        var accept = new RecordingAcceptance();
        var sut = new AllocateIntake(new ReceiptQueries(receipt), store, accept, TimeProvider.System);

        var result = await sut.AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());

        Assert.Equal(IntakeAllocationProjectionStatus.Succeeded, result?.State.Status);
        Assert.Equal(CaseType.InspectionAndAudit, Assert.Single(accept.Requests).CaseType);
        Assert.Equal(IntakeAllocationAttemptKind.Automatic, store.Current?.Kind);
    }

    [Fact]
    public async Task ManualUploadDoesNotAllocateBeforeStaffAcceptsTheProposal()
    {
        var receipt = Receipt(CaseType.Inspection, "QDOS") with
        {
            SourceIdentity = new(IntakeSourceChannel.ManualUpload, "manual-proposal")
        };
        var store = new RecordingAllocationStore();
        var accept = new RecordingAcceptance();
        var sut = new AllocateIntake(new ReceiptQueries(receipt), store, accept, TimeProvider.System);

        var result = await sut.AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());

        Assert.Null(result);
        Assert.Null(store.Current);
        Assert.Empty(accept.Requests);
    }

    [Fact]
    public async Task FailedAutomaticAttemptIsDurableAndIsNotRetriedInBackground()
    {
        var receipt = Receipt(CaseType.Inspection, "MISSING");
        var store = new RecordingAllocationStore();
        var accept = new RecordingAcceptance(new PrincipalUnavailableException("MISSING"));
        var sut = new AllocateIntake(new ReceiptQueries(receipt), store, accept, TimeProvider.System);

        var first = await sut.AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());
        var replay = await sut.AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());

        Assert.Equal(IntakeAllocationProjectionStatus.FailedRecoverable, first?.State.Status);
        Assert.Equal(IntakeAllocationFailureKind.PrincipalUnavailable, first?.State.FailureKind);
        Assert.True(replay?.IsSuppressed);
        Assert.Single(accept.Requests);
    }

    /// <summary>
    /// A staff create or retry is casework (ADR-0064): a member of staff with
    /// a casework role or the Automation Actor, which holds casework without
    /// staff roles. A system worker and a Principal are refused before
    /// anything is attempted.
    /// </summary>
    [Fact]
    public async Task OnlyCaseworkActorsInvokeStaffCreateAndRetry()
    {
        var receipt = Receipt(CaseType.Inspection, "QDOS");
        var store = new RecordingAllocationStore();
        var sut = new AllocateIntake(
            new ReceiptQueries(receipt),
            store,
            new RecordingAcceptance(),
            TimeProvider.System);

        foreach (var refused in new[]
        {
            ActionActor.SystemWorker("worker"),
            ActionActor.Principal(Guid.NewGuid())
        })
        {
            await Assert.ThrowsAsync<StaffAuthorizationException>(() => sut.RetryAsync(new(
                receipt.Id,
                receipt.Version,
                Guid.NewGuid(),
                refused,
                "retry:test",
                "Controlled retry.")));
            await Assert.ThrowsAsync<StaffAuthorizationException>(() => sut.AttemptStaffCreateAsync(new(
                receipt.Id,
                receipt.Version,
                refused,
                "create:test",
                CaseType.Inspection,
                "QDOS",
                new(InstructionComplete: true, ImagesComplete: false))));
        }

        Assert.Null(store.Current);
    }

    [Fact]
    public async Task TheAutomationActorCreatesACaseFromAReceivedItemInItsOwnName()
    {
        var receipt = Receipt(CaseType.Inspection, "QDOS");
        var accept = new RecordingAcceptance();
        var sut = new AllocateIntake(
            new ReceiptQueries(receipt),
            new RecordingAllocationStore(),
            accept,
            TimeProvider.System);

        var created = await sut.AttemptStaffCreateAsync(new(
            receipt.Id,
            receipt.Version,
            ActionActor.Automation("grant-1"),
            "mcp:accept-1",
            CaseType.Inspection,
            "qdos",
            new(InstructionComplete: true, ImagesComplete: false)));

        Assert.Equal(IntakeAllocationProjectionStatus.Succeeded, created.State.Status);
        var request = Assert.Single(accept.Requests);
        Assert.Equal(ActorKind.Automation, request.Actor.Kind);
        Assert.Equal("grant-1", request.Actor.SubjectId);
        Assert.Equal("QDOS", request.PrincipalCode);
    }

    [Fact]
    public async Task TheAutomationActorRetriesAFailedAllocationWithTheSameCommand()
    {
        var receipt = Receipt(CaseType.Audit, "QDOS");
        var evidenceId = Guid.NewGuid();
        var accept = new RecordingAcceptance(new InvalidOperationException("transient fault"));
        var sut = new AllocateIntake(
            new ReceiptQueries(receipt),
            new RecordingAllocationStore(),
            accept,
            TimeProvider.System,
            new EvidenceQueries(receipt.Id, evidenceId));

        var failed = await sut.AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());
        accept.Failure = null;
        var retried = await sut.RetryAsync(new(
            receipt.Id,
            receipt.Version,
            failed!.State.AttemptId,
            ActionActor.Automation("grant-1"),
            "mcp:retry-1",
            "Retry after the fault cleared."));

        Assert.Equal(IntakeAllocationProjectionStatus.Succeeded, retried.State.Status);
        Assert.Equal(ActorKind.Automation, accept.Requests[1].Actor.Kind);
        Assert.Equal(evidenceId, accept.Requests[1].StandaloneAuditEvidenceId);
    }

    [Fact]
    public async Task UnexpectedFailureIsSafeAndRecoverable()
    {
        var receipt = Receipt(CaseType.Inspection, "QDOS");
        var unexpectedSut = new AllocateIntake(
            new ReceiptQueries(receipt),
            new RecordingAllocationStore(),
            new RecordingAcceptance(new InvalidDataException("private failure detail")),
            TimeProvider.System);

        var unexpected = await unexpectedSut.AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());

        Assert.Equal(IntakeAllocationFailureKind.Unexpected, unexpected?.State.FailureKind);
        Assert.Equal(IntakeAllocationRecoveryDisposition.ReloadThenRetry, unexpected?.State.RecoveryDisposition);
        Assert.Equal("The case could not be created. No reference was allocated.", unexpected?.State.SafeReason);
        Assert.DoesNotContain("private", unexpected?.State.SafeReason, StringComparison.OrdinalIgnoreCase);
    }

    // A standalone Audit that failed automatic allocation for an
    // unclassified reason has no manual creation route, so the failure must
    // be staff-retryable, and the retry must hand acceptance the identical
    // command — same receipt version, same retained evidence — rather than
    // anything the staff member could have reshaped.
    [Fact]
    public async Task UnexpectedAutomaticAuditFailureIsRetriedWithTheSameCommand()
    {
        var receipt = Receipt(CaseType.Audit, "QDOS");
        var evidenceId = Guid.NewGuid();
        var store = new RecordingAllocationStore();
        var accept = new RecordingAcceptance(new InvalidOperationException("transient fault"));
        var sut = new AllocateIntake(
            new ReceiptQueries(receipt),
            store,
            accept,
            TimeProvider.System,
            new EvidenceQueries(receipt.Id, evidenceId));

        var failed = await sut.AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());
        Assert.Equal(IntakeAllocationProjectionStatus.FailedRecoverable, failed?.State.Status);
        Assert.True(failed?.State.CanRetry);

        accept.Failure = null;
        var retried = await sut.RetryAsync(new(
            receipt.Id,
            receipt.Version,
            failed!.State.AttemptId,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
            "retry:unclassified-fault",
            "Retry after the fault cleared."));

        Assert.Equal(IntakeAllocationProjectionStatus.Succeeded, retried.State.Status);
        Assert.Equal(2, accept.Requests.Count);
        Assert.Equal(CaseType.Audit, accept.Requests[1].CaseType);
        Assert.Equal(evidenceId, accept.Requests[1].StandaloneAuditEvidenceId);
        Assert.Equal(accept.Requests[0].ExpectedVersion, accept.Requests[1].ExpectedVersion);
        Assert.Equal(accept.Requests[0].Completeness, accept.Requests[1].Completeness);
    }

    [Fact]
    public async Task CancellationIsRethrownAndDoesNotLeaveAnAttempt()
    {
        var receipt = Receipt(CaseType.Inspection, "QDOS");
        var store = new RecordingAllocationStore();
        var sut = new AllocateIntake(
            new ReceiptQueries(receipt),
            store,
            new RecordingAcceptance(new OperationCanceledException()),
            TimeProvider.System);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            sut.AttemptAutomaticAsync(receipt.Id, Guid.NewGuid()));

        Assert.Null(store.Current);
    }

    [Fact]
    public async Task AutomaticAllocationRejectsMissingAcceptedRoute()
    {
        var receipt = Receipt(CaseType.Inspection, "QDOS") with
        {
            MailRouteDecision = null
        };
        var sut = new AllocateIntake(
            new ReceiptQueries(receipt),
            new RecordingAllocationStore(),
            new RecordingAcceptance(),
            TimeProvider.System);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.AttemptAutomaticAsync(receipt.Id, Guid.NewGuid()));

        Assert.Contains("accepted principal route", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AutomaticAllocationRejectsRouteDraftPrincipalMismatch()
    {
        var receipt = Receipt(CaseType.Inspection, "QDOS") with
        {
            InstructionDraft = new("OTHER", null, null, null, null, null, null, null, null, null)
        };
        var sut = new AllocateIntake(
            new ReceiptQueries(receipt),
            new RecordingAllocationStore(),
            new RecordingAcceptance(),
            TimeProvider.System);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.AttemptAutomaticAsync(receipt.Id, Guid.NewGuid()));

        Assert.Contains("does not match", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Automatic allocation used to assert ImagesComplete: true as a
    // constant, so an audit with an instruction, a report and no photographs
    // was born Review-ready. These drive AttemptAutomaticAsync and assert on what
    // acceptance actually received, so they prove the wiring rather than
    // re-implementing it.
    [Fact]
    public async Task AnInstructionCarryingNoPhotographsIsNotImageComplete()
    {
        var receipt = Receipt(
            CaseType.Audit,
            "QDOS",
            Asset("49378_1_LtrtoAuditEngin.pdf", "application/pdf", IntakeAssetKind.Attachment, 82_000),
            Asset("Bodyshopreport119508-V1.pdf", "application/pdf", IntakeAssetKind.Attachment, 240_000));
        var accept = new RecordingAcceptance();
        var sut = new AllocateIntake(
            new ReceiptQueries(receipt), new RecordingAllocationStore(), accept, TimeProvider.System);

        await sut.AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());

        var completeness = Assert.Single(accept.Requests).Completeness;
        Assert.True(completeness.InstructionComplete);
        Assert.False(completeness.ImagesComplete);
    }

    [Fact]
    public async Task AnInstructionCarryingAGenuinePhotographIsImageComplete()
    {
        var receipt = Receipt(
            CaseType.Inspection,
            "QDOS",
            Asset("damage-1.jpg", "image/jpeg", IntakeAssetKind.Attachment, 1_400_000, 4032, 3024));
        var accept = new RecordingAcceptance();
        var sut = new AllocateIntake(
            new ReceiptQueries(receipt), new RecordingAllocationStore(), accept, TimeProvider.System);

        await sut.AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());

        Assert.True(Assert.Single(accept.Requests).Completeness.ImagesComplete);
    }

    [Fact]
    public async Task ALetterheadBannerIsNotAPhotograph()
    {
        // The corpus letterhead shape: 1990x437, comfortably over any byte
        // floor, with a JPEG sibling at 2214x248. Only the side ratio catches
        // them, and this pins the readiness gate to the same definition of an
        // image the gallery and custody already use.
        var receipt = Receipt(
            CaseType.Audit,
            "QDOS",
            Asset("letterhead.png", "image/png", IntakeAssetKind.EmbeddedImage, 110_783, 1990, 437));
        var accept = new RecordingAcceptance();
        var sut = new AllocateIntake(
            new ReceiptQueries(receipt), new RecordingAllocationStore(), accept, TimeProvider.System);

        await sut.AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());

        Assert.False(Assert.Single(accept.Requests).Completeness.ImagesComplete);
    }

    [Fact]
    public async Task PhotographsEmbeddedInTheBodyRatherThanAttachedAreNotImageComplete()
    {
        // A known and accepted consequence, pinned so it is a decision rather
        // than a surprise: InstructionEvidenceImages counts attachments and
        // embedded images, never inline ones. A sender who puts the damage
        // photographs in the HTML body leaves the case Not ready until staff
        // confirm it.
        var receipt = Receipt(
            CaseType.Inspection,
            "QDOS",
            Asset("inline-damage.jpg", "image/jpeg", IntakeAssetKind.InlineImage, 900_000, 3000, 2000));
        var accept = new RecordingAcceptance();
        var sut = new AllocateIntake(
            new ReceiptQueries(receipt), new RecordingAllocationStore(), accept, TimeProvider.System);

        await sut.AttemptAutomaticAsync(receipt.Id, Guid.NewGuid());

        Assert.False(Assert.Single(accept.Requests).Completeness.ImagesComplete);
    }

    private static IntakeAssetRecord Asset(
        string fileName,
        string mediaType,
        IntakeAssetKind kind,
        long contentLength,
        int? width = null,
        int? height = null) =>
        new(
            Guid.NewGuid(),
            $"outer message, attachment {fileName}",
            fileName,
            mediaType,
            kind,
            kind == IntakeAssetKind.Attachment
                ? IntakeAssetDisposition.Attachment
                : IntakeAssetDisposition.Embedded,
            contentLength,
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(fileName))),
            $"storage/{fileName}",
            null,
            null,
            width,
            height);

    private static IntakeReceipt Receipt(
        CaseType caseType,
        string principalCode,
        params IntakeAssetRecord[] assets) => new(
        Guid.NewGuid(),
        "retained-instruction.pdf",
        "application/pdf",
        100,
        new string('a', 64),
        new(IntakeSourceChannel.Mailbox, "source-token"),
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow,
        IntakeDecision.CaseCreated,
        "Eligible for case allocation.",
        [],
        [],
        new(principalCode, null, null, "AB12CDE", null, null, null, null, null, null),
        [],
        null,
        null,
        false,
        "test-reader",
        "1",
        "test-policy",
        1,
        assets,
        MailRouteDecision: new(
            MailRouteDisposition.Accepted,
            new(principalCode, MailRouteKind.DirectPrincipal, principalCode),
            [],
            "Accepted test route.",
            "test-route",
            1,
            [new($"instructions@{principalCode.ToLowerInvariant()}.example", "outer message")],
            [],
            new($"instructions@{principalCode.ToLowerInvariant()}.example", "outer message")),
        MailClassificationDecision: MailClassificationResult.Classified(
            MailCategory.Received(ReceivedMailFamily.NewInstructionReceived, "inspection"),
            [],
            "Definitive instruction.",
            "test-classifier",
            1,
            caseType));

    private sealed class ReceiptQueries(IntakeReceipt receipt) : IIntakeReceiptQueries
    {
        public Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<IntakeReceipt?>(id == receipt.Id ? receipt : null);

    }

    private sealed class EvidenceQueries(Guid receiptId, Guid evidenceId) : IStandaloneAuditEvidenceQueries
    {
        public Task<StandaloneAuditEvidence?> GetForReceiptAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken) =>
            Task.FromResult<StandaloneAuditEvidence?>(intakeReceiptId == receiptId
                ? new(
                    evidenceId,
                    receiptId,
                    Guid.NewGuid(),
                    AuditAssessment.Repairable,
                    Guid.Empty,
                    DateTimeOffset.UtcNow,
                    "The retained original report states Repairable.",
                    0,
                    false)
                : null);
    }

    private sealed class RecordingAcceptance(Exception? failure = null) : IAcceptIntake
    {
        public List<AcceptIntakeRequest> Requests { get; } = [];

        public Exception? Failure { get; set; } = failure;

        public Task<CaseAcceptanceOutcome> ExecuteAsync(
            AcceptIntakeRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (Failure is not null)
            {
                return Task.FromException<CaseAcceptanceOutcome>(Failure);
            }

            return Task.FromResult(new CaseAcceptanceOutcome(
                new(Guid.NewGuid(), request.PrincipalCode, 2031, 1, "QDOS/2031/0001"),
                CaseInitialState.Review,
                CaseCustodyState.Pending,
                Guid.NewGuid(),
                false));
        }
    }

    private sealed class RecordingAllocationStore : IIntakeAllocationStore
    {
        public IntakeAllocationAttempt? Current { get; private set; }

        public Task<IntakeAllocationAttempt?> GetCurrentAsync(
            Guid receiptId,
            CancellationToken cancellationToken) => Task.FromResult(Current);

        public Task<BeginIntakeAllocationResult> BeginAsync(
            BeginIntakeAllocationAttempt request,
            CancellationToken cancellationToken)
        {
            if (Current is not null && request.Kind == IntakeAllocationAttemptKind.Automatic)
            {
                return Task.FromResult(new BeginIntakeAllocationResult(Current, true, true));
            }

            Current = new(
                Guid.NewGuid(),
                request.Command.ReceiptId,
                request.Kind,
                IntakeAllocationAttemptStatus.Pending,
                request.Command,
                request.Actor,
                request.OperationKey,
                request.CommandHash,
                request.Reason,
                request.StartedAtUtc,
                null,
                null,
                null,
                null,
                null,
                null,
                null);
            return Task.FromResult(new BeginIntakeAllocationResult(Current, false, false));
        }

        public Task<IntakeAllocationAttempt> CompleteFailureAsync(
            Guid attemptId,
            IntakeAllocationFailureKind failureKind,
            IntakeAllocationRecoveryDisposition recoveryDisposition,
            string safeReason,
            DateTimeOffset completedAtUtc,
            Exception exception,
            CancellationToken cancellationToken)
        {
            Current = Current! with
            {
                Status = IntakeAllocationAttemptStatus.Failed,
                CompletedAtUtc = completedAtUtc,
                FailureKind = failureKind,
                RecoveryDisposition = recoveryDisposition,
                SafeReason = safeReason
            };
            return Task.FromResult(Current);
        }

        public Task CancelPendingAsync(Guid attemptId, CancellationToken cancellationToken)
        {
            Current = null;
            return Task.CompletedTask;
        }
    }
}
