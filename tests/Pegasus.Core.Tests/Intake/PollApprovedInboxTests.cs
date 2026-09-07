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
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "mailbox-a", "a@collisionengineers.co.uk", "inbox-a", NowUtc.AddDays(-1));

    private static readonly ApprovedIntakeMailbox SecondMailbox =
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "mailbox-b", "b@collisionengineers.co.uk", "inbox-b", NowUtc.AddDays(-1));

    private static readonly ApprovedIntakeMailbox ThirdMailbox =
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "mailbox-c", "c@collisionengineers.co.uk", "inbox-c", NowUtc.AddDays(-1));

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
        harness.Source.Enqueue(FirstMailbox.GraphMailboxId, Message("a-1", "cursor-a1"));
        harness.Source.Enqueue(SecondMailbox.GraphMailboxId, Message("b-1", "cursor-b1"));

        var handled = await harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None);

        Assert.Equal(2, handled);
        Assert.Equal(["mailbox-a", "mailbox-b"], harness.PollStore.ClaimedMailboxIds);
        Assert.Equal("cursor-a1", harness.PollStore.Cursors["mailbox-a"]);
        Assert.Equal("cursor-b1", harness.PollStore.Cursors["mailbox-b"]);
        Assert.Empty(harness.PollStore.Releases);
    }

    [Fact]
    public async Task ExactMailboxWakePollsOnlyTheEnabledStableMailbox()
    {
        var harness = new Harness(FirstMailbox, SecondMailbox);
        harness.Source.Enqueue(SecondMailbox.GraphMailboxId, Message("b-1", "cursor-b1"));

        var handled = await harness.Poll().ExecuteMailboxAsync(
            SecondMailbox.ApprovedMailboxId,
            10,
            WorkerActor(),
            CancellationToken.None);

        Assert.Equal(1, handled);
        Assert.Equal(["mailbox-b"], harness.PollStore.ClaimedMailboxIds);
    }

    [Fact]
    public async Task ExactMailboxWakeForUnknownStableIdentityDoesNothing()
    {
        var harness = new Harness(FirstMailbox);

        var handled = await harness.Poll().ExecuteMailboxAsync(
            Guid.NewGuid(),
            10,
            WorkerActor(),
            CancellationToken.None);

        Assert.Equal(0, handled);
        Assert.Empty(harness.PollStore.ClaimedMailboxIds);
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
        harness.Source.Enqueue(FirstMailbox.GraphMailboxId, Message("a-1", "cursor-a1"));

        var handled = await harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None);

        Assert.Equal(1, handled);
        Assert.Equal(
            ("mailbox-b", "mailbox_not_approved"),
            Assert.Single(harness.PollStore.Releases));
        Assert.DoesNotContain(
            harness.Source.Reads,
            read => read.MailboxId == SecondMailbox.GraphMailboxId);
    }

    [Fact]
    public async Task OneFailingMailboxIsReleasedAndTheOthersStillPoll()
    {
        var harness = new Harness(FirstMailbox, SecondMailbox);
        harness.Source.Fail(FirstMailbox.GraphMailboxId, new InvalidDataException("bad page"));
        harness.Source.Enqueue(SecondMailbox.GraphMailboxId, Message("b-1", "cursor-b1"));

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
        harness.Source.Fail(FirstMailbox.GraphMailboxId, new InvalidDataException("bad page"));
        harness.Source.Fail(SecondMailbox.GraphMailboxId, new IntakeArtifactIntegrityException());

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
        harness.Source.Fail(FirstMailbox.GraphMailboxId, new InvalidDataException("bad page"));
        harness.PollStore.FailClaim(
            SecondMailbox.GraphMailboxId,
            new IOException("the poll store is unavailable"));
        harness.Source.Enqueue(ThirdMailbox.GraphMailboxId, Message("c-1", "cursor-c1"));

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
        harness.Source.Fail(FirstMailbox.GraphMailboxId, new InvalidDataException("bad page"));
        harness.PollStore.FailRelease(new IOException("the poll store is unavailable"));
        harness.Source.Enqueue(SecondMailbox.GraphMailboxId, Message("b-1", "cursor-b1"));

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
            FirstMailbox.GraphMailboxId,
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
            new(Guid.NewGuid(), "has space", "c@collisionengineers.co.uk", "inbox-c", NowUtc.AddDays(-1)));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None));

        Assert.Empty(harness.PollStore.ClaimedMailboxIds);
    }

    [Fact]
    public async Task AMailboxThatIsNotDueYieldsNoLeaseAndIsSkippedQuietly()
    {
        var harness = new Harness(FirstMailbox, SecondMailbox);
        harness.PollStore.WithholdLease(FirstMailbox.GraphMailboxId);
        harness.Source.Enqueue(SecondMailbox.GraphMailboxId, Message("b-1", "cursor-b1"));

        var handled = await harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None);

        Assert.Equal(1, handled);
        Assert.Empty(harness.PollStore.Releases);
        Assert.DoesNotContain(
            harness.Source.Reads,
            read => read.MailboxId == FirstMailbox.GraphMailboxId);
    }

    [Fact]
    public async Task AnAcceptedMessageIsRetainedWithTheTokenTheReceiptWasFiledUnder()
    {
        var harness = new Harness(FirstMailbox);
        harness.Source.Enqueue(FirstMailbox.GraphMailboxId, DisplayableMessage("a-1", "cursor-a1"));

        await harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None);

        var retained = Assert.Single(harness.Retained.Retained);
        Assert.Equal(FirstMailbox.ApprovedMailboxId, retained.MailboxId);
        Assert.Equal(FirstMailbox.Address, retained.MailboxAddress);
        Assert.Equal("a-1", retained.ImmutableMessageId);
        // The same token PrepareMessage handed the receipt, which is what joins the
        // retained row to its processing outcome.
        Assert.StartsWith("36:11111111-1111-1111-1111-111111111111rfc:", retained.ExternalReceiptToken, StringComparison.Ordinal);
        Assert.Equal(107, retained.ExternalReceiptToken.Length);
        Assert.Equal("An instruction", retained.Metadata.Subject);
        Assert.Equal(NowUtc, retained.RetainedAtUtc);
    }

    [Fact]
    public async Task ARedeliveredMessageIsOfferedToTheStoreAgainUnchanged()
    {
        var harness = new Harness(FirstMailbox);
        harness.Source.Enqueue(FirstMailbox.GraphMailboxId, DisplayableMessage("a-1", "cursor-a1"));
        await harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None);
        harness.Source.Enqueue(FirstMailbox.GraphMailboxId, DisplayableMessage("a-1", "cursor-a1"));

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
    public async Task EquivalentInternetMessageIdentitiesUseOneCanonicalReceiptToken()
    {
        var harness = new Harness(FirstMailbox);
        harness.Source.Enqueue(
            FirstMailbox.GraphMailboxId,
            DisplayableMessage(
                "provider-one",
                "cursor-a1",
                Metadata(internetMessageIdentity: " <case@K.example> ")));
        await harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None);
        harness.Source.Enqueue(
            FirstMailbox.GraphMailboxId,
            DisplayableMessage(
                "provider-two",
                "cursor-a2",
                Metadata(internetMessageIdentity: "<CASE@K.EXAMPLE>")));

        await harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None);

        Assert.Equal(2, harness.Retained.Retained.Count);
        Assert.Equal(
            harness.Retained.Retained[0].ExternalReceiptToken,
            harness.Retained.Retained[1].ExternalReceiptToken);
    }

    [Fact]
    public async Task CanonicalIdentityExpansionBeyondThePersistenceBoundIsMalformed()
    {
        var harness = new Harness(FirstMailbox);
        var expandingIdentity = $"<{new string('ﬃ', 498)}>";
        Assert.Equal(500, expandingIdentity.Length);
        harness.Source.Enqueue(
            FirstMailbox.GraphMailboxId,
            DisplayableMessage(
                "provider-one",
                "cursor-a1",
                Metadata(internetMessageIdentity: expandingIdentity)));

        await harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None);

        Assert.Empty(harness.Retained.Retained);
    }

    [Fact]
    public async Task AMessageWithoutDisplayMetadataIsAcceptedButNotRetained()
    {
        var harness = new Harness(FirstMailbox);
        harness.Source.Enqueue(FirstMailbox.GraphMailboxId, Message("a-1", "cursor-a1"));

        var handled = await harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None);

        Assert.Equal(1, handled);
        Assert.Empty(harness.Retained.Retained);
    }

    [Fact]
    public async Task MetadataOutsideTheRetainedBoundsQuarantinesRatherThanTruncating()
    {
        var harness = new Harness(FirstMailbox);
        harness.Source.Enqueue(
            FirstMailbox.GraphMailboxId,
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
            FirstMailbox.GraphMailboxId,
            DisplayableMessage(
                "a-1",
                "cursor-a1",
                Metadata(toAddresses: [.. Enumerable.Range(0, 51).Select(index => $"r{index}@example.invalid")])));

        await harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None);

        Assert.Empty(harness.Retained.Retained);
    }

    [Fact]
    public async Task TooManyReplyToAddressesIsRefusedAsMalformedMetadata()
    {
        var harness = new Harness(FirstMailbox);
        harness.Source.Enqueue(
            FirstMailbox.GraphMailboxId,
            DisplayableMessage(
                "a-1",
                "cursor-a1",
                Metadata(replyToAddresses:
                    [.. Enumerable.Range(0, 51).Select(index => $"reply{index}@example.invalid")])));

        await harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None);

        Assert.Empty(harness.Retained.Retained);
    }

    [Fact]
    public async Task MissingInternetMessageIdentityIsRefusedAsMalformedMetadata()
    {
        var harness = new Harness(FirstMailbox);
        harness.Source.Enqueue(
            FirstMailbox.GraphMailboxId,
            DisplayableMessage(
                "a-1",
                "cursor-a1",
                Metadata(internetMessageIdentity: null)));

        await harness.Poll().ExecuteAsync(10, WorkerActor(), CancellationToken.None);

        Assert.Empty(harness.Retained.Retained);
    }

    [Fact]
    public async Task AFailedRetainReleasesTheLeaseAndLeavesTheCursorUnadvanced()
    {
        var harness = new Harness(FirstMailbox);
        harness.Source.Enqueue(FirstMailbox.GraphMailboxId, DisplayableMessage("a-1", "cursor-a1"));
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
        IReadOnlyList<string>? toAddresses = null,
        string? internetMessageIdentity = "<message-1@example.invalid>",
        IReadOnlyList<string>? replyToAddresses = null) =>
        new(
            "inbox-a",
            "conversation-1",
            internetMessageIdentity,
            "sender@example.invalid",
            "A Sender",
            toAddresses ?? ["intake@collisionengineers.co.uk"],
            [],
            replyToAddresses ?? ["sender@example.invalid"],
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
                new ReceiveIntake(
                    artifacts,
                    new WorkStore(),
                    timeProvider,
                    new CommittedWorkPublisherDouble()),
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
        private readonly Dictionary<Guid, string> graphIds = [];
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
            if (claimFailures.TryGetValue(mailbox.GraphMailboxId, out var failure))
            {
                return Task.FromException<ApprovedInboxPollLease?>(failure);
            }

            if (withheld.Contains(mailbox.GraphMailboxId))
            {
                return Task.FromResult<ApprovedInboxPollLease?>(null);
            }

            graphIds[mailbox.ApprovedMailboxId] = mailbox.GraphMailboxId;
            ClaimedMailboxIds.Add(mailbox.GraphMailboxId);
            Cursors.TryGetValue(mailbox.GraphMailboxId, out var cursor);
            return Task.FromResult<ApprovedInboxPollLease?>(new(
                mailbox.ApprovedMailboxId,
                mailbox.GraphMailboxId,
                mailbox.Address,
                mailbox.InboxFolderIdentity,
                mailbox.ActivatedAtUtc,
                cursor,
                $"lease-{mailbox.GraphMailboxId}"));
        }

        public Task AdvanceAsync(
            Guid approvedMailboxId,
            string leaseToken,
            string nextCursor,
            DateTimeOffset advancedAtUtc,
            CancellationToken cancellationToken)
        {
            Cursors[graphIds[approvedMailboxId]] = nextCursor;
            return Task.CompletedTask;
        }

        public Task QuarantineAsync(
            Guid approvedMailboxId,
            string leaseToken,
            ApprovedInboxPoisonMessage message,
            string nextCursor,
            DateTimeOffset quarantinedAtUtc,
            CancellationToken cancellationToken)
        {
            Cursors[graphIds[approvedMailboxId]] = nextCursor;
            return Task.CompletedTask;
        }

        public Task CompleteAsync(
            Guid approvedMailboxId,
            string leaseToken,
            string nextCursor,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken)
        {
            Cursors[graphIds[approvedMailboxId]] = nextCursor;
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(
            Guid approvedMailboxId,
            string leaseToken,
            DateTimeOffset dueAtUtc,
            string failureCode,
            CancellationToken cancellationToken)
        {
            if (releaseFailure is not null)
            {
                return Task.FromException(releaseFailure);
            }

            Releases.Add((graphIds[approvedMailboxId], failureCode));
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
            Reads.Add((lease.GraphMailboxId, lease.InboxFolderIdentity));
            if (failures.TryGetValue(lease.GraphMailboxId, out var failure))
            {
                return Task.FromException<ApprovedInboxPage>(failure);
            }

            if (!queued.TryGetValue(lease.GraphMailboxId, out var messages) || messages.Count == 0)
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

        public Task<IntakeWorkItem?> ClaimDispatchAsync(
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IntakeWorkItem?> ClaimDispatchAsync(
            Guid stagedReceiptId,
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

        public Task<int> RecoverInterruptedWorkAsync(
            DateTimeOffset nowUtc,
            DateTimeOffset staleDispatchedBeforeUtc,
            int maximumItems,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ScheduleReevaluationAsync(
            Guid stagedReceiptId,
            DateTimeOffset dueAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Guid?> FindStagedReceiptIdForReceiptAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
