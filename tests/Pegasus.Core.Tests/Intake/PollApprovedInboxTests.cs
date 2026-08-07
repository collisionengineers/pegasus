using System.Security.Cryptography;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// The estate, not deployment configuration, decides which mailboxes a tick reads.
/// These cover that decision and the isolation it promises: one lease and one cursor
/// per mailbox, and one mailbox's failure confined to that mailbox.
/// </summary>
public sealed class PollApprovedInboxTests
{
    private static readonly DateTimeOffset NowUtc = new(2031, 9, 1, 8, 0, 0, TimeSpan.Zero);

    private static readonly ApprovedIntakeMailbox FirstMailbox =
        new("mailbox-a", "a@collisionengineers.co.uk", "inbox-a");

    private static readonly ApprovedIntakeMailbox SecondMailbox =
        new("mailbox-b", "b@collisionengineers.co.uk", "inbox-b");

    private static readonly ApprovedIntakeMailbox ThirdMailbox =
        new("mailbox-c", "c@collisionengineers.co.uk", "inbox-c");

    [Fact]
    public async Task PollRequiresASystemWorkerActor()
    {
        var harness = new Harness(FirstMailbox);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            harness.Poll().ExecuteAsync(
                10,
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                CancellationToken.None));

        Assert.Empty(harness.PollStore.ClaimedMailboxIds);
    }

    [Fact]
    public async Task PollRefusesASystemWorkerIdentityLongerThanTheActorColumn()
    {
        var harness = new Harness(FirstMailbox);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            harness.Poll().ExecuteAsync(
                10,
                ActionActor.SystemWorker(new string('w', 200)),
                CancellationToken.None));

        Assert.Empty(harness.PollStore.ClaimedMailboxIds);
    }

    [Fact]
    public async Task EveryApprovedInboundMailboxIsPolledUnderItsOwnLeaseAndCursor()
    {
        var harness = new Harness(FirstMailbox, SecondMailbox);
        harness.Source.Enqueue(FirstMailbox.MailboxId, Message("a-1", "cursor-a1"));
        harness.Source.Enqueue(SecondMailbox.MailboxId, Message("b-1", "cursor-b1"));

        var handled = await harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None);

        Assert.Equal(2, handled);
        Assert.Equal(["mailbox-a", "mailbox-b"], harness.PollStore.ClaimedMailboxIds);
        Assert.Equal("cursor-a1", harness.PollStore.Cursors["mailbox-a"]);
        Assert.Equal("cursor-b1", harness.PollStore.Cursors["mailbox-b"]);
        Assert.Empty(harness.PollStore.Releases);
    }

    [Fact]
    public async Task EachMailboxReadsUnderItsOwnInboxFolderIdentity()
    {
        var harness = new Harness(FirstMailbox, SecondMailbox);

        await harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None);

        Assert.Equal(
            [("mailbox-a", "inbox-a"), ("mailbox-b", "inbox-b")],
            harness.Source.Reads);
    }

    [Fact]
    public async Task AMailboxTheEstateDoesNotOfferIsNeverClaimed()
    {
        // A Disabled row simply does not appear in the pollable estate.
        var harness = new Harness(FirstMailbox);

        await harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None);

        Assert.Equal(["mailbox-a"], harness.PollStore.ClaimedMailboxIds);
    }

    [Fact]
    public async Task AMailboxDisabledBetweenListingAndClaimingIsReleasedWithoutBeingRead()
    {
        var harness = new Harness(FirstMailbox, SecondMailbox);
        harness.Policy.Withdraw(SecondMailbox.Address);
        harness.Source.Enqueue(FirstMailbox.MailboxId, Message("a-1", "cursor-a1"));

        var handled = await harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None);

        Assert.Equal(1, handled);
        Assert.Equal(
            ("mailbox-b", "mailbox_not_approved"),
            Assert.Single(harness.PollStore.Releases));
        Assert.DoesNotContain(
            harness.Source.Reads,
            read => read.MailboxId == SecondMailbox.MailboxId);
    }

    [Fact]
    public async Task OneFailingMailboxIsReleasedAndTheOthersStillPoll()
    {
        var harness = new Harness(FirstMailbox, SecondMailbox);
        harness.Source.Fail(FirstMailbox.MailboxId, new InvalidDataException("bad page"));
        harness.Source.Enqueue(SecondMailbox.MailboxId, Message("b-1", "cursor-b1"));

        // The single-failure path preserves the original exception type, which existing
        // callers and tests depend on.
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None));

        Assert.Equal(
            ("mailbox-a", "invalid_mailbox_source"),
            Assert.Single(harness.PollStore.Releases));
        Assert.Equal("cursor-b1", harness.PollStore.Cursors["mailbox-b"]);
    }

    [Fact]
    public async Task TwoFailingMailboxesAreBothReportedTogether()
    {
        var harness = new Harness(FirstMailbox, SecondMailbox);
        harness.Source.Fail(FirstMailbox.MailboxId, new InvalidDataException("bad page"));
        harness.Source.Fail(SecondMailbox.MailboxId, new IntakeArtifactIntegrityException());

        var exception = await Assert.ThrowsAsync<AggregateException>(() =>
            harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None));

        Assert.Equal(2, exception.InnerExceptions.Count);
        Assert.Equal(
            [
                ("mailbox-a", "invalid_mailbox_source"),
                ("mailbox-b", "mailbox_poll_failure")
            ],
            harness.PollStore.Releases);
    }

    [Fact]
    public async Task AMailboxWhoseClaimFailsCostsNeitherTheEarlierFailureNorTheLaterMailbox()
    {
        var harness = new Harness(FirstMailbox, SecondMailbox, ThirdMailbox);
        harness.Source.Fail(FirstMailbox.MailboxId, new InvalidDataException("bad page"));
        harness.PollStore.FailClaim(
            SecondMailbox.MailboxId,
            new IOException("the poll store is unavailable"));
        harness.Source.Enqueue(ThirdMailbox.MailboxId, Message("c-1", "cursor-c1"));

        var exception = await Assert.ThrowsAsync<AggregateException>(() =>
            harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None));

        // Claiming is inside the per-mailbox boundary, so its failure belongs to
        // that mailbox alone. Outside it, this exception escaped the loop over the
        // estate: every mailbox after it went unpolled and every failure already
        // collected before it was thrown away with the aggregation that never ran.
        Assert.Equal(2, exception.InnerExceptions.Count);
        Assert.IsType<InvalidDataException>(exception.InnerExceptions[0]);
        Assert.IsType<IOException>(exception.InnerExceptions[1]);
        Assert.Equal("cursor-c1", harness.PollStore.Cursors["mailbox-c"]);
        // No lease was ever handed out for mailbox-b, so nothing is released for it.
        Assert.Equal(
            ("mailbox-a", "invalid_mailbox_source"),
            Assert.Single(harness.PollStore.Releases));
    }

    [Fact]
    public async Task AReleaseThatFailsDoesNotReplaceTheFailureThatCausedIt()
    {
        var harness = new Harness(FirstMailbox, SecondMailbox);
        harness.Source.Fail(FirstMailbox.MailboxId, new InvalidDataException("bad page"));
        harness.PollStore.FailRelease(new IOException("the poll store is unavailable"));
        harness.Source.Enqueue(SecondMailbox.MailboxId, Message("b-1", "cursor-b1"));

        // The lease lapses on its own. What the estate must be told is why the
        // mailbox failed, not that tidying up after it also failed.
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None));

        Assert.Equal("cursor-b1", harness.PollStore.Cursors["mailbox-b"]);
    }

    [Fact]
    public async Task AMailboxThatTheTenantRefusesReportsAccessDenied()
    {
        var harness = new Harness(FirstMailbox);
        harness.Source.Fail(
            FirstMailbox.MailboxId,
            new ApprovedMailboxAccessDeniedException("Graph refused the mailbox."));

        await Assert.ThrowsAsync<ApprovedMailboxAccessDeniedException>(() =>
            harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None));

        Assert.Equal(
            ("mailbox-a", "mailbox_access_denied"),
            Assert.Single(harness.PollStore.Releases));
    }

    [Fact]
    public async Task AMalformedMailboxIdentityIsRefusedBeforeAnyMailboxIsClaimed()
    {
        var harness = new Harness(
            FirstMailbox,
            new("has space", "c@collisionengineers.co.uk", "inbox-c"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None));

        Assert.Empty(harness.PollStore.ClaimedMailboxIds);
    }

    [Fact]
    public async Task AMailboxThatIsNotDueYieldsNoLeaseAndIsSkippedQuietly()
    {
        var harness = new Harness(FirstMailbox, SecondMailbox);
        harness.PollStore.WithholdLease(FirstMailbox.MailboxId);
        harness.Source.Enqueue(SecondMailbox.MailboxId, Message("b-1", "cursor-b1"));

        var handled = await harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None);

        Assert.Equal(1, handled);
        Assert.Empty(harness.PollStore.Releases);
        Assert.DoesNotContain(
            harness.Source.Reads,
            read => read.MailboxId == FirstMailbox.MailboxId);
    }

    [Fact]
    public async Task AnAcceptedMessageIsRetainedWithTheTokenTheReceiptWasFiledUnder()
    {
        var harness = new Harness(FirstMailbox);
        harness.Source.Enqueue(FirstMailbox.MailboxId, DisplayableMessage("a-1", "cursor-a1"));

        await harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None);

        var retained = Assert.Single(harness.Retained.Retained);
        Assert.Equal("mailbox-a", retained.MailboxId);
        Assert.Equal(FirstMailbox.Address, retained.MailboxAddress);
        Assert.Equal("a-1", retained.ImmutableMessageId);
        // The same token PrepareMessage handed the receipt, which is what joins the
        // retained row to its processing outcome.
        Assert.Equal("9:mailbox-aa-1", retained.ExternalReceiptToken);
        Assert.Equal("An instruction", retained.Metadata.Subject);
        Assert.Equal(NowUtc, retained.RetainedAtUtc);
    }

    [Fact]
    public async Task ARedeliveredMessageIsOfferedToTheStoreAgainUnchanged()
    {
        var harness = new Harness(FirstMailbox);
        harness.Source.Enqueue(FirstMailbox.MailboxId, DisplayableMessage("a-1", "cursor-a1"));
        await harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None);
        harness.Source.Enqueue(FirstMailbox.MailboxId, DisplayableMessage("a-1", "cursor-a1"));

        await harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None);

        // Idempotency is the store's promise, not the poll's: the poll offers the
        // same row twice and the unique index decides.
        Assert.Equal(2, harness.Retained.Retained.Count);
        var first = harness.Retained.Retained[0];
        var second = harness.Retained.Retained[1];
        Assert.Equal(first.MailboxId, second.MailboxId);
        Assert.Equal(first.ImmutableMessageId, second.ImmutableMessageId);
        Assert.Equal(first.ExternalReceiptToken, second.ExternalReceiptToken);
        Assert.Equal(first.SourceSha256, second.SourceSha256);
        Assert.Equal(first.ReceivedAtUtc, second.ReceivedAtUtc);
        Assert.Equal(first.Metadata.Subject, second.Metadata.Subject);
        Assert.Equal(first.Metadata.BodyPlainText, second.Metadata.BodyPlainText);
    }

    [Fact]
    public async Task AMessageWithoutDisplayMetadataIsAcceptedButNotRetained()
    {
        var harness = new Harness(FirstMailbox);
        harness.Source.Enqueue(FirstMailbox.MailboxId, Message("a-1", "cursor-a1"));

        var handled = await harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None);

        Assert.Equal(1, handled);
        Assert.Empty(harness.Retained.Retained);
    }

    [Fact]
    public async Task MetadataOutsideTheRetainedBoundsQuarantinesRatherThanTruncating()
    {
        var harness = new Harness(FirstMailbox);
        harness.Source.Enqueue(
            FirstMailbox.MailboxId,
            DisplayableMessage(
                "a-1",
                "cursor-a1",
                Metadata(subject: new string('s', 1001))));

        var handled = await harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None);

        Assert.Equal(1, handled);
        Assert.Empty(harness.Retained.Retained);
    }

    [Fact]
    public async Task TooManyRecipientsIsRefusedAsMalformedMetadata()
    {
        var harness = new Harness(FirstMailbox);
        harness.Source.Enqueue(
            FirstMailbox.MailboxId,
            DisplayableMessage(
                "a-1",
                "cursor-a1",
                Metadata(toAddresses: [.. Enumerable.Range(0, 51).Select(index => $"r{index}@example.invalid")])));

        await harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None);

        Assert.Empty(harness.Retained.Retained);
    }

    [Fact]
    public async Task AFailedRetainReleasesTheLeaseAndLeavesTheCursorUnadvanced()
    {
        var harness = new Harness(FirstMailbox);
        harness.Source.Enqueue(FirstMailbox.MailboxId, DisplayableMessage("a-1", "cursor-a1"));
        harness.Retained.Fail(new IOException("the read model is unavailable"));

        await Assert.ThrowsAsync<IOException>(() =>
            harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None));

        Assert.Equal(
            ("mailbox-a", "mailbox_poll_failure"),
            Assert.Single(harness.PollStore.Releases));
        Assert.False(harness.PollStore.Cursors.ContainsKey("mailbox-a"));
    }

    private static ActionActor WorkerActor() =>
        ActionActor.SystemWorker("approved-inbox-poller");

    private static ApprovedInboxMessage Message(string identity, string nextCursor) =>
        new(
            identity,
            $"{identity}.eml",
            new ReadOnlyMemory<byte>("From: sender@example.invalid\r\n\r\nBody"u8.ToArray()),
            NowUtc,
            nextCursor);

    private static ApprovedInboxMessage DisplayableMessage(
        string identity,
        string nextCursor,
        RetainedMailboxMessageMetadata? metadata = null) =>
        Message(identity, nextCursor) with
        {
            RetainedMetadata = metadata ?? Metadata()
        };

    private static RetainedMailboxMessageMetadata Metadata(
        string? subject = "An instruction",
        IReadOnlyList<string>? toAddresses = null) =>
        new(
            "inbox-a",
            "conversation-1",
            "<message-1@example.invalid>",
            "sender@example.invalid",
            "A Sender",
            toAddresses ?? ["intake@collisionengineers.co.uk"],
            [],
            subject,
            "Body",
            [new("estimate.pdf", "application/pdf", 2048)],
            IsRead: false);

    private sealed class Harness
    {
        internal Harness(params ApprovedIntakeMailbox[] mailboxes)
        {
            Mailboxes = new([.. mailboxes]);
            Policy = new([.. mailboxes.Select(mailbox => mailbox.Address)]);
            PollStore = new();
            Source = new();
        }

        internal MailboxEstate Mailboxes { get; }

        internal MailboxPolicy Policy { get; }

        internal PollStore PollStore { get; }

        internal InboxSource Source { get; }

        internal RetainedStore Retained { get; } = new();

        internal PollApprovedInbox Poll()
        {
            var artifacts = new ArtifactStore();
            var timeProvider = new FixedTimeProvider(NowUtc);
            return new(
                Mailboxes,
                Policy,
                PollStore,
                Source,
                artifacts,
                artifacts,
                new ReceiveIntake(artifacts, new WorkStore(), timeProvider),
                Retained,
                timeProvider);
        }
    }

    private sealed class RetainedStore : IRetainedMailboxMessageStore
    {
        private Exception? failure;

        internal List<RetainedMailboxMessage> Retained { get; } = [];

        internal void Fail(Exception exception) => failure = exception;

        public Task RetainAsync(
            RetainedMailboxMessage message,
            CancellationToken cancellationToken)
        {
            if (failure is not null)
            {
                return Task.FromException(failure);
            }

            Retained.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class MailboxEstate(List<ApprovedIntakeMailbox> mailboxes) : IApprovedIntakeMailboxes
    {
        public Task<IReadOnlyList<ApprovedIntakeMailbox>> ListPollableAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ApprovedIntakeMailbox>>(mailboxes);
    }

    private sealed class MailboxPolicy(HashSet<string> approvedAddresses) : IApprovedMailboxPolicy
    {
        internal void Withdraw(string address) => approvedAddresses.Remove(address);

        public Task<bool> IsApprovedAsync(
            string mailboxAddress,
            ApprovedMailboxRouteScope routeScope,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                routeScope == ApprovedMailboxRouteScope.InboundIntake
                && approvedAddresses.Contains(mailboxAddress));
    }

    private sealed class PollStore : IApprovedInboxPollStore
    {
        private readonly HashSet<string> withheld = new(StringComparer.Ordinal);

        private readonly Dictionary<string, Exception> claimFailures = new(StringComparer.Ordinal);

        private Exception? releaseFailure;

        internal List<string> ClaimedMailboxIds { get; } = [];

        internal Dictionary<string, string> Cursors { get; } = new(StringComparer.Ordinal);

        internal List<(string MailboxId, string FailureCode)> Releases { get; } = [];

        internal void WithholdLease(string mailboxId) => withheld.Add(mailboxId);

        internal void FailClaim(string mailboxId, Exception exception) =>
            claimFailures[mailboxId] = exception;

        internal void FailRelease(Exception exception) => releaseFailure = exception;

        public Task<ApprovedInboxPollLease?> ClaimAsync(
            ApprovedIntakeMailbox mailbox,
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken)
        {
            if (claimFailures.TryGetValue(mailbox.MailboxId, out var failure))
            {
                return Task.FromException<ApprovedInboxPollLease?>(failure);
            }

            if (withheld.Contains(mailbox.MailboxId))
            {
                return Task.FromResult<ApprovedInboxPollLease?>(null);
            }

            ClaimedMailboxIds.Add(mailbox.MailboxId);
            Cursors.TryGetValue(mailbox.MailboxId, out var cursor);
            return Task.FromResult<ApprovedInboxPollLease?>(new(
                mailbox.MailboxId,
                mailbox.Address,
                mailbox.InboxFolderIdentity,
                cursor,
                $"lease-{mailbox.MailboxId}"));
        }

        public Task AdvanceAsync(
            string mailboxId,
            string leaseToken,
            string nextCursor,
            DateTimeOffset advancedAtUtc,
            CancellationToken cancellationToken)
        {
            Cursors[mailboxId] = nextCursor;
            return Task.CompletedTask;
        }

        public Task QuarantineAsync(
            string mailboxId,
            string leaseToken,
            ApprovedInboxPoisonMessage message,
            string nextCursor,
            DateTimeOffset quarantinedAtUtc,
            CancellationToken cancellationToken)
        {
            Cursors[mailboxId] = nextCursor;
            return Task.CompletedTask;
        }

        public Task CompleteAsync(
            string mailboxId,
            string leaseToken,
            string nextCursor,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken)
        {
            Cursors[mailboxId] = nextCursor;
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(
            string mailboxId,
            string leaseToken,
            DateTimeOffset dueAtUtc,
            string failureCode,
            CancellationToken cancellationToken)
        {
            if (releaseFailure is not null)
            {
                return Task.FromException(releaseFailure);
            }

            Releases.Add((mailboxId, failureCode));
            return Task.CompletedTask;
        }
    }

    private sealed class InboxSource : IApprovedInboxSource
    {
        private readonly Dictionary<string, List<ApprovedInboxMessage>> queued =
            new(StringComparer.Ordinal);

        private readonly Dictionary<string, Exception> failures = new(StringComparer.Ordinal);

        internal List<(string MailboxId, string InboxFolderIdentity)> Reads { get; } = [];

        internal void Enqueue(string mailboxId, ApprovedInboxMessage message)
        {
            if (!queued.TryGetValue(mailboxId, out var messages))
            {
                messages = [];
                queued[mailboxId] = messages;
            }

            messages.Add(message);
        }

        internal void Fail(string mailboxId, Exception exception) =>
            failures[mailboxId] = exception;

        public Task<ApprovedInboxPage> ReadAsync(
            ApprovedInboxPollLease lease,
            int maximumMessages,
            CancellationToken cancellationToken)
        {
            Reads.Add((lease.MailboxId, lease.InboxFolderIdentity));
            if (failures.TryGetValue(lease.MailboxId, out var failure))
            {
                return Task.FromException<ApprovedInboxPage>(failure);
            }

            if (!queued.TryGetValue(lease.MailboxId, out var messages) || messages.Count == 0)
            {
                return Task.FromResult(new ApprovedInboxPage([], lease.Cursor ?? "empty"));
            }

            var page = messages.Take(maximumMessages).ToArray();
            messages.RemoveRange(0, page.Length);
            return Task.FromResult(new ApprovedInboxPage(page, page[^1].NextCursor));
        }
    }

    private sealed class ArtifactStore : IIntakeArtifactStore, IIntakeQuarantineArtifactStore
    {
        private readonly Dictionary<string, byte[]> stored = new(StringComparer.Ordinal);

        public Task<string> StoreAsync(
            string contentHash,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken)
        {
            var key = $"sha256/{contentHash[..2]}/{contentHash}";
            stored[key] = content.ToArray();
            return Task.FromResult(key);
        }

        public Task<ReadOnlyMemory<byte>?> ReadAsync(
            string storageKey,
            CancellationToken cancellationToken) =>
            Task.FromResult<ReadOnlyMemory<byte>?>(
                stored.TryGetValue(storageKey, out var content) ? content : null);

        public Task<IntakeQuarantineArtifact> StoreStreamAsync(
            Stream content,
            long contentLength,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task VerifyAsync(
            IntakeQuarantineArtifact artifact,
            CancellationToken cancellationToken)
        {
            if (!stored.TryGetValue(artifact.StorageKey, out var content)
                || content.LongLength != artifact.ContentLength
                || !string.Equals(
                    Convert.ToHexString(SHA256.HashData(content)),
                    artifact.ContentHash,
                    StringComparison.Ordinal))
            {
                throw new IntakeArtifactIntegrityException();
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Only the two members the poll reaches are implemented; everything else would be a
    /// silent change of what this test exercises, so it refuses instead.
    /// </summary>
    private sealed class WorkStore : IIntakeWorkStore
    {
        private readonly Dictionary<string, IntakeStagedReceipt> received =
            new(StringComparer.Ordinal);

        public Task<IntakeStagedReceipt?> FindBySourceIdentityAsync(
            IntakeSourceIdentity sourceIdentity,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                received.TryGetValue(sourceIdentity.ExternalReceiptToken, out var receipt)
                    ? receipt
                    : null);

        public Task<IntakeWorkItem?> FindWorkItemAsync(
            Guid stagedReceiptId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The mailbox poll does not read work items.");

        public Task<ReceivedIntake> ReceiveAsync(
            IntakeStagedReceipt receipt,
            string operationKey,
            CancellationToken cancellationToken)
        {
            var duplicate = !received.TryAdd(
                receipt.SourceIdentity.ExternalReceiptToken,
                receipt);
            return Task.FromResult(new ReceivedIntake(receipt.Id, duplicate));
        }

        public Task<ReceivedIntake> ReceiveForProcessingAsync(
            IntakeStagedReceipt receipt,
            string operationKey,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IntakeWorkItem?> ClaimDispatchAsync(
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task MarkDispatchedAsync(
            Guid workItemId,
            string leaseToken,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ReleaseDispatchAsync(
            Guid workItemId,
            string leaseToken,
            DateTimeOffset dueAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<(IntakeWorkItem WorkItem, IntakeStagedReceipt Receipt)?> ClaimProcessingAsync(
            Guid stagedReceiptId,
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IntakeEvaluationRevision> CompleteProcessingAsync(
            Guid workItemId,
            string leaseToken,
            Guid processedReceiptId,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IntakeEvaluationRevision?> GetCompletedEvaluationAsync(
            Guid stagedReceiptId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task RetryProcessingAsync(
            Guid workItemId,
            string leaseToken,
            DateTimeOffset dueAtUtc,
            string failureCode,
            bool terminal,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task MarkPoisonedAsync(
            Guid stagedReceiptId,
            DateTimeOffset failedAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<int> RecoverExpiredLeasesAsync(
            DateTimeOffset nowUtc,
            int maximumItems,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ScheduleReevaluationAsync(
            Guid stagedReceiptId,
            DateTimeOffset dueAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
