using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;

namespace Pegasus.Core.Tests.Operations;

public sealed class StaffMailSendTests
{
    [Fact]
    public async Task AutomationCannotInitiateStaffMail()
    {
        var send = new StaffMailSend(null!, null!, null!, null!, TimeProvider.System, new ExecutionLock());
        var command = Command(ActionActor.Automation("automation"));

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            send.SendAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task GenericSendCannotBypassCaseReportReadiness()
    {
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var command = Command(actor) with { Purpose = StaffMailPurpose.CaseReport };
        var transport = new Transport();
        var send = new StaffMailSend(null!, null!, null!, transport, TimeProvider.System, new ExecutionLock());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            send.SendAsync(command, CancellationToken.None));

        Assert.Equal(0, transport.CreateCount);
        Assert.Equal(0, transport.SendCount);
    }

    [Fact]
    public async Task NewMailCannotBorrowAnOriginalMessageIdentity()
    {
        var send = new StaffMailSend(null!, null!, null!, null!, TimeProvider.System, new ExecutionLock());
        var command = Command(ActionActor.Staff(Guid.NewGuid(), [StaffRole.User])) with
        {
            OriginalMessage = new(
                Guid.NewGuid(), Guid.NewGuid(), "immutable", "<message@example.invalid>", "conversation")
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            send.SendAsync(command, CancellationToken.None));
    }

    [Fact]
    public void SubmittedCannotBePresentedAsSentWithoutObservation()
    {
        Assert.Throws<InvalidOperationException>(() =>
            StaffMailStatePolicy.RequireTransition(
                StaffMailState.Prepared,
                StaffMailState.Sent));
        StaffMailStatePolicy.RequireTransition(
            StaffMailState.Submitted,
            StaffMailState.Sent);
    }

    [Fact]
    public void KnownProviderRejectionCanBecomeFailedButAmbiguousWorkCannotRetry()
    {
        StaffMailStatePolicy.RequireTransition(
            StaffMailState.Sending,
            StaffMailState.Failed);
        Assert.Throws<InvalidOperationException>(() =>
            StaffMailStatePolicy.RequireTransition(
                StaffMailState.Unknown,
                StaffMailState.Sending));
    }

    [Theory]
    [InlineData("Body")]
    [InlineData("")]
    [InlineData(" ")]
    public async Task FreshOperationCreatesOnceAttachesExactBytesAndBecomesSubmitted(string body)
    {
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var bytes = new byte[] { 1, 2, 3, 4 };
        var attachment = new StaffMailAttachment(
            Guid.NewGuid(), Guid.NewGuid(),
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)),
            bytes.Length, "report.pdf", "application/pdf");
        var command = Command(actor) with { Body = body, Attachments = [attachment] };
        var store = new Store();
        var transport = new Transport();
        var send = new StaffMailSend(
            store,
            new Mailboxes(command.ApprovedMailboxId, command.ExpectedMailboxGeneration),
            new Reader(bytes, attachment),
            transport,
            TimeProvider.System, new ExecutionLock());

        var result = await send.SendAsync(command, CancellationToken.None);

        Assert.Equal(StaffMailState.Submitted, result.State);
        Assert.Equal(1, transport.CreateCount);
        Assert.Equal(1, transport.SendCount);
        Assert.Equal(bytes, Assert.Single(transport.Attached));
        Assert.Equal(4, store.CurrentStaffChecks);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task EmptyMailWithoutAttachmentsRefusesBeforePersistenceOrTransport(string body)
    {
        var command = Command(ActionActor.Staff(Guid.NewGuid(), [StaffRole.User])) with { Body = body };
        var store = new Store();
        var transport = new Transport();
        var send = new StaffMailSend(store, null!, null!, transport, TimeProvider.System, new ExecutionLock());

        await Assert.ThrowsAsync<ArgumentException>(() => send.SendAsync(command, CancellationToken.None));

        Assert.Null(store.Operation);
        Assert.Equal(0, transport.CreateCount);
        Assert.Equal(0, transport.SendCount);
    }

    [Fact]
    public async Task DisabledStaffCannotPrepareOrCallProvider()
    {
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var command = Command(actor);
        var store = new Store { RejectCurrentStaff = true };
        var transport = new Transport();
        var send = new StaffMailSend(
            store,
            new Mailboxes(command.ApprovedMailboxId, command.ExpectedMailboxGeneration),
            new Reader([], new(Guid.NewGuid(), Guid.NewGuid(), new string('A', 64), 1,
                "unused", "application/octet-stream")),
            transport,
            TimeProvider.System, new ExecutionLock());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            send.SendAsync(command, CancellationToken.None));

        Assert.Equal(0, transport.SendCount);
        Assert.Null(store.Operation);
    }

    [Fact]
    public async Task FinalReadinessFailureOccursBeforeSendingAndIsRecordedFailed()
    {
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var command = Command(actor);
        var store = new Store();
        var transport = new Transport();
        var send = new StaffMailSend(store,
            new Mailboxes(command.ApprovedMailboxId, command.ExpectedMailboxGeneration),
            new Reader([], new(Guid.NewGuid(), Guid.NewGuid(), new string('A', 64), 1,
                "unused", "application/octet-stream")), transport, TimeProvider.System,
            new ExecutionLock());

        await Assert.ThrowsAsync<InvalidDataException>(() => send.SendValidatedAsync(
            command, _ => throw new InvalidDataException("report changed"), CancellationToken.None));

        Assert.Equal(0, transport.SendCount);
        Assert.Equal(StaffMailState.Failed, store.Operation!.State);
        Assert.Equal(StaffMailAttemptStage.Attach, store.Operation.AttemptStage);
    }

    [Fact]
    public async Task ConcurrentLoserCannotMutateWinnerWhileExecutionLockIsHeld()
    {
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var command = Command(actor);
        var store = new Store();
        var transport = new Transport { BlockSend = true };
        var gate = new ContendedExecutionLock();
        var send = new StaffMailSend(store,
            new Mailboxes(command.ApprovedMailboxId, command.ExpectedMailboxGeneration),
            new Reader([], new(Guid.NewGuid(), Guid.NewGuid(), new string('A', 64), 1,
                "unused", "application/octet-stream")), transport, TimeProvider.System, gate);

        var winner = send.SendAsync(command, CancellationToken.None);
        await transport.SendEntered.Task;
        var versionWhileWinnerHeld = store.Operation!.Version;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            send.SendAsync(command, CancellationToken.None));
        Assert.Equal(versionWhileWinnerHeld, store.Operation!.Version);
        Assert.Equal(StaffMailState.Sending, store.Operation.State);

        transport.ReleaseSend.SetResult();
        Assert.Equal(StaffMailState.Submitted, (await winner).State);
    }

    private static StaffMailSendCommand Command(ActionActor actor) => new(
        actor,
        Guid.NewGuid(),
        1,
        StaffMailPurpose.GeneralCorrespondence,
        Guid.NewGuid(),
        1,
        StaffMailComposeMode.New,
        null,
        [new("recipient@example.invalid", null)],
        [],
        "Subject",
        "Body",
        [],
        "operation-key");

    private sealed class Mailboxes(Guid id, long generation) : IApprovedStaffSendMailboxQueries
    {
        public Task<ApprovedStaffSendMailbox?> GetAsync(Guid mailboxId, CancellationToken cancellationToken) =>
            Task.FromResult<ApprovedStaffSendMailbox?>(mailboxId == id
                ? new(id, "graph-mailbox", generation, 25_000_000)
                : null);
    }

    private sealed class Reader(byte[] bytes, StaffMailAttachment attachment) : IReadLogicalDocumentVersion
    {
        public Task<LogicalDocumentContent> OpenAsync(
            ReadLogicalDocumentVersionRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new LogicalDocumentContent(
                new MemoryStream(bytes, writable: false), attachment.DocumentId,
                attachment.VersionId, null, attachment.Sha256, bytes.Length,
                attachment.FileName, attachment.MediaType));
    }

    private sealed class Transport : IStaffMailTransport
    {
        public int CreateCount { get; private set; }
        public int SendCount { get; private set; }
        public bool BlockSend { get; init; }
        public TaskCompletionSource SendEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseSend { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<byte[]> Attached { get; } = [];
        public Task ValidateEncodedSizeAsync(ApprovedStaffSendMailbox mailbox, StaffMailOperation operation, StaffMailSendCommand command, IReadOnlyList<StaffMailAttachmentContent> attachments, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<StaffMailDraftLookupResult> FindDraftAsync(ApprovedStaffSendMailbox mailbox, StaffMailOperation operation, CancellationToken cancellationToken) =>
            Task.FromResult(new StaffMailDraftLookupResult(null, null, true));
        public Task<StaffMailDraftResult> CreateDraftAsync(ApprovedStaffSendMailbox mailbox, Guid operationId, string payloadHash, StaffMailSendCommand command, CancellationToken cancellationToken)
        {
            CreateCount++;
            return Task.FromResult(new StaffMailDraftResult("draft"));
        }
        public async Task AttachAsync(ApprovedStaffSendMailbox mailbox, Guid operationId, string immutableDraftId, StaffMailAttachment attachment, Stream content, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            Attached.Add(buffer.ToArray());
        }
        public async Task<StaffMailSubmitResult> SendDraftAsync(ApprovedStaffSendMailbox mailbox, string immutableDraftId, CancellationToken cancellationToken)
        {
            SendCount++;
            SendEntered.SetResult();
            if (BlockSend) await ReleaseSend.Task.WaitAsync(cancellationToken);
            return new StaffMailSubmitResult(DateTimeOffset.UtcNow);
        }
    }

    private sealed class ExecutionLock : IStaffMailExecutionLock
    {
        public Task<IAsyncDisposable> AcquireAsync(Guid operationId, CancellationToken cancellationToken) =>
            Task.FromResult<IAsyncDisposable>(new Lease());
        private sealed class Lease : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class ContendedExecutionLock : IStaffMailExecutionLock
    {
        private int held;
        public Task<IAsyncDisposable> AcquireAsync(Guid operationId, CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref held, 1, 0) != 0)
                throw new InvalidOperationException("busy");
            return Task.FromResult<IAsyncDisposable>(new Lease(this));
        }
        private sealed class Lease(ContendedExecutionLock owner) : IAsyncDisposable
        {
            public ValueTask DisposeAsync()
            {
                Interlocked.Exchange(ref owner.held, 0);
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class Store : IStaffMailSendStore
    {
        private StaffMailOperation? operation;
        public StaffMailOperation? Operation => operation;
        public bool RejectCurrentStaff { get; init; }
        public int CurrentStaffChecks { get; private set; }
        public Task<StaffMailOperation> PrepareAsync(StaffMailSendCommand command, string payloadHash, DateTimeOffset nowUtc, CancellationToken cancellationToken)
        {
            operation ??= new(Guid.NewGuid(), StaffMailState.Prepared, null, 1, nowUtc,
                null, null, null, command.ApprovedMailboxId, command.ExpectedMailboxGeneration,
                payloadHash, null, null);
            return Task.FromResult(operation);
        }
        public Task<StaffMailOperation?> GetAsync(string actorSubjectId, Guid operationId, CancellationToken cancellationToken) => Task.FromResult(operation);
        public Task<StaffMailExecution?> GetExecutionAsync(string actorSubjectId, Guid operationId, CancellationToken cancellationToken) =>
            Task.FromResult<StaffMailExecution?>(operation is null ? null : new(
                actorSubjectId, operation, operation.State is StaffMailState.DraftReady or StaffMailState.Sending ? "draft" : null,
                [], StaffMailPurpose.GeneralCorrespondence, Guid.NewGuid(), 1, Guid.NewGuid()));
        public Task<StaffMailExecution?> GetExecutionForObservationAsync(ActionActor systemActor, Guid operationId, CancellationToken cancellationToken) => Task.FromResult<StaffMailExecution?>(null);
        public Task RequireCurrentStaffAsync(string actorSubjectId, CancellationToken cancellationToken) { CurrentStaffChecks++; return RejectCurrentStaff ? Task.FromException(new UnauthorizedAccessException()) : Task.CompletedTask; }
        public Task<StaffMailOperation> TransitionAsync(string actorSubjectId, Guid operationId, long expectedVersion, StaffMailState state, StaffMailAttemptStage? stage, string? draftImmutableId, DateTimeOffset? submittedAtUtc, DateTimeOffset? observedSentAtUtc, string? failureCode, CancellationToken cancellationToken)
        {
            var current = operation!;
            StaffMailStatePolicy.RequireTransition(current.State, state);
            operation = current with { State = state, AttemptStage = stage, Version = current.Version + 1, SubmittedAtUtc = submittedAtUtc ?? current.SubmittedAtUtc, ObservedSentAtUtc = observedSentAtUtc ?? current.ObservedSentAtUtc, FailureCode = failureCode };
            return Task.FromResult(operation);
        }
        public Task<StaffMailOperation> SetReconciliationContinuationAsync(string actorSubjectId, Guid operationId, long expectedVersion, string? continuation, CancellationToken cancellationToken)
        {
            operation = operation! with
            {
                ReconciliationContinuation = continuation,
                Version = operation.Version + 1
            };
            return Task.FromResult(operation);
        }
        public Task TransitionObservedSentAsync(ActionActor systemActor, Guid operationId, long expectedVersion, string immutableMessageId, DateTimeOffset providerSentAtUtc, DateTimeOffset observedAtUtc, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
