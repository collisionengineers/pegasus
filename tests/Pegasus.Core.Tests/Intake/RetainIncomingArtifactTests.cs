using System.Diagnostics;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

public sealed class RetainIncomingArtifactTests
{
    private static readonly Guid CaseId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DocumentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid VersionId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static ActionActor PublicActor() => ActionActor.RequestLink(Guid.NewGuid());

    private static IncomingArtifactOccurrence Occurrence(
        string operationKey = "occurrence-1",
        string fileName = "estimate.pdf",
        string sha256 = "aaaa") => new(
        Guid.NewGuid(),
        CaseId,
        null,
        operationKey,
        fileName,
        "application/pdf",
        1024,
        sha256);

    /// <summary>
    /// One arrival, committed before anything is offered. Every caller of the
    /// command has to do this - the command refuses a hand-over that names no
    /// committed arrival - so every test below hands over from a staged
    /// arrival exactly as the public upload path does.
    /// </summary>
    private static IncomingArtifactOccurrence Staged(
        RecordingStore store,
        IncomingArtifactOccurrence? occurrence = null)
    {
        var staged = occurrence ?? Occurrence();
        store.Arrive(staged);
        return staged;
    }

    [Fact]
    public async Task AConfirmedRetentionRecordsItsLogicalDocumentAndRemoteIdentities()
    {
        var custody = new RecordingCustody(Confirmed());
        var store = new RecordingStore();
        var retained = await new RetainIncomingArtifact(custody, store).ExecuteAsync(
            PublicActor(),
            Staged(store),
            new MemoryStream([1, 2, 3]));

        Assert.True(retained.IsConfirmed);
        Assert.Equal(DocumentId, retained.DocumentId);
        Assert.Equal(VersionId, retained.DocumentVersionId);
        Assert.Equal("box-file", retained.BoxFileId);
        Assert.Equal("box-version", retained.BoxVersionId);
        Assert.Equal(retained, Assert.Single(store.Recorded));
    }

    [Theory]
    [InlineData(CaseArtifactCustodyDisposition.Pending, IncomingArtifactCustodyState.Pending)]
    [InlineData(CaseArtifactCustodyDisposition.Failed, IncomingArtifactCustodyState.Failed)]
    [InlineData(CaseArtifactCustodyDisposition.Unknown, IncomingArtifactCustodyState.Unknown)]
    public async Task NothingButAConfirmedDispositionIsSuccessAndNoneOfThemInventRemoteIdentities(
        CaseArtifactCustodyDisposition disposition,
        IncomingArtifactCustodyState expected)
    {
        var custody = new RecordingCustody(
            Confirmed() with { Disposition = disposition, FailureCode = "custody_unavailable" });
        var store = new RecordingStore();
        var retained = await new RetainIncomingArtifact(custody, store).ExecuteAsync(
            PublicActor(),
            Staged(store),
            new MemoryStream([1]));

        Assert.Equal(expected, retained.State);
        Assert.False(retained.IsConfirmed);
        // The remote identities would say custody holds the bytes. It has not
        // said so, so they are not carried.
        Assert.Null(retained.BoxFileId);
        Assert.Null(retained.BoxVersionId);
        Assert.Equal("custody_unavailable", retained.FailureCode);
        // A non-confirmed retention is still durable — it never disappears and
        // never ages out on its own.
        Assert.Single(store.Recorded);
    }

    [Fact]
    public async Task AConfirmedReplayReturnsTheSameDocumentWithoutOfferingTheBytesAgain()
    {
        var custody = new RecordingCustody(Confirmed());
        var store = new RecordingStore();
        var command = new RetainIncomingArtifact(custody, store);
        var occurrence = Staged(store);
        var first = await command.ExecuteAsync(PublicActor(), occurrence, new MemoryStream([1]));
        var replay = await command.ExecuteAsync(PublicActor(), occurrence, new MemoryStream([1]));

        Assert.Equal(first, replay);
        Assert.Equal(1, custody.Calls);
    }

    [Fact]
    public async Task TwoArrivalsWithTheSameFileNameAreTwoOccurrencesAndNeitherOverwritesTheOther()
    {
        var custody = new RecordingCustody(Confirmed());
        var store = new RecordingStore();
        var command = new RetainIncomingArtifact(custody, store);
        var first = await command.ExecuteAsync(
            PublicActor(),
            Staged(store, Occurrence("occurrence-1", "estimate.pdf", "aaaa")),
            new MemoryStream([1]));
        var second = await command.ExecuteAsync(
            PublicActor(),
            Staged(store, Occurrence("occurrence-2", "estimate.pdf", "bbbb")),
            new MemoryStream([2]));

        Assert.NotEqual(first.OccurrenceId, second.OccurrenceId);
        Assert.Equal(2, custody.Calls);
        Assert.Equal(2, store.Recorded.Count);
        Assert.Equal(
            ["estimate.pdf", "estimate.pdf"],
            custody.Requests.Select(request => request.FileName));
    }

    [Fact]
    public async Task AnUncertainHandOverIsReconciledUnderTheSameOperationKeyAndNeverResubmitted()
    {
        var custody = new RecordingCustody(Confirmed() with
        {
            Disposition = CaseArtifactCustodyDisposition.Unknown
        });
        var store = new RecordingStore();
        var status = new RecordingCustodyStatus(Confirmed());
        var command = new RetainIncomingArtifact(custody, store, status);
        var occurrence = Staged(store);

        var uncertain = await command.ExecuteAsync(PublicActor(), occurrence, new MemoryStream([1]));
        Assert.Equal(IncomingArtifactCustodyState.Unknown, uncertain.State);

        var reconciled = await command.ExecuteAsync(PublicActor(), occurrence, new MemoryStream([1]));

        // Asked, not repeated: custody saw the bytes exactly once.
        Assert.Equal(1, custody.Calls);
        Assert.Equal(1, status.Calls);
        Assert.Equal(IncomingArtifactCustodyState.Confirmed, reconciled.State);
        Assert.Equal("box-file", reconciled.BoxFileId);
        Assert.Equal(occurrence.OperationKey, reconciled.OperationKey);
    }

    /// <summary>
    /// An uncertain hand-over custody owns up to nothing for is offered again
    /// under the same key, which is the only way a claim taken by a process
    /// that then died is ever resolved. It is asked about first, every time,
    /// and never under a new key: what custody converges is one repeated key,
    /// so a fresh one would be the second file (Stream A, PR 673 comment
    /// 5561151076).
    /// </summary>
    [Fact]
    public async Task AnUncertainHandOverNothingIsObservedForIsReOfferedUnderTheSameKey()
    {
        var custody = new RecordingCustody(new(
            CaseArtifactCustodyDisposition.Unknown,
            null, null, null, null, null, null, null, null, "timeout", null));
        var store = new RecordingStore();
        var status = new RecordingCustodyStatus(Confirmed());
        var command = new RetainIncomingArtifact(custody, store, status);
        var occurrence = Staged(store);

        _ = await command.ExecuteAsync(PublicActor(), occurrence, new MemoryStream([1]));
        var second = await command.ExecuteAsync(PublicActor(), occurrence, new MemoryStream([1]));

        Assert.Equal(IncomingArtifactCustodyState.Unknown, second.State);

        // Nothing precise to ask about, so it is asked for by its operation
        // key - and only then, custody owning up to nothing, offered again
        // under that same key.
        Assert.Equal(0, status.Calls);
        Assert.Equal(1, status.LookupCalls);
        Assert.Equal(2, custody.Calls);
        Assert.Equal(
            [occurrence.OperationKey, occurrence.OperationKey],
            custody.Requests.Select(request => request.OperationKey));
    }

    [Fact]
    public async Task AnUnauthorizedActorAndAMalformedOccurrenceAreRefusedBeforeCustodySeesAnything()
    {
        var custody = new RecordingCustody(Confirmed());
        var command = new RetainIncomingArtifact(custody, new RecordingStore());

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => command.ExecuteAsync(
            ActionActor.Provider(Guid.NewGuid()),
            Occurrence(),
            new MemoryStream([1])));
        await Assert.ThrowsAsync<ArgumentException>(() => command.ExecuteAsync(
            PublicActor(),
            Occurrence() with { OccurrenceId = Guid.Empty },
            new MemoryStream([1])));
        await Assert.ThrowsAsync<ArgumentException>(() => command.ExecuteAsync(
            PublicActor(),
            Occurrence() with { CaseId = null, IntakeReceiptId = null },
            new MemoryStream([1])));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => command.ExecuteAsync(
            PublicActor(),
            Occurrence() with { ContentLength = 0 },
            new MemoryStream([1])));

        Assert.Equal(0, custody.Calls);
    }

    /// <summary>
    /// A Pending hand-over is asked about under the same operation key, exactly
    /// as an uncertain one is. It is the only way a Pending ever moves —
    /// re-offering the bytes would duplicate a file custody already holds — and
    /// only a confirmation carries a remote identity back.
    /// </summary>
    [Theory]
    [InlineData(CaseArtifactCustodyDisposition.Confirmed, IncomingArtifactCustodyState.Confirmed)]
    [InlineData(CaseArtifactCustodyDisposition.Failed, IncomingArtifactCustodyState.Failed)]
    [InlineData(CaseArtifactCustodyDisposition.Pending, IncomingArtifactCustodyState.Pending)]
    public async Task APendingHandOverIsReconciledUnderTheSameOperationKeyAndNeverResubmitted(
        CaseArtifactCustodyDisposition reconciledAs,
        IncomingArtifactCustodyState expected)
    {
        var custody = new RecordingCustody(Confirmed() with
        {
            Disposition = CaseArtifactCustodyDisposition.Pending
        });
        var store = new RecordingStore();
        var status = new RecordingCustodyStatus(Confirmed() with { Disposition = reconciledAs });
        var command = new RetainIncomingArtifact(custody, store, status);
        var occurrence = Staged(store);

        var pending = await command.ExecuteAsync(PublicActor(), occurrence, new MemoryStream([1]));
        Assert.Equal(IncomingArtifactCustodyState.Pending, pending.State);

        var reconciled = await command.ExecuteAsync(PublicActor(), occurrence, new MemoryStream([1]));

        Assert.Equal(expected, reconciled.State);
        Assert.Equal(occurrence.OperationKey, reconciled.OperationKey);

        // Asked, not repeated: custody saw the bytes exactly once.
        Assert.Equal(1, custody.Calls);
        Assert.Equal(1, status.Calls);

        // Only a confirmed reconciliation says where custody holds them.
        Assert.Equal(
            expected == IncomingArtifactCustodyState.Confirmed ? "box-file" : null,
            reconciled.BoxFileId);
    }

    /// <summary>
    /// A hand-over that throws mid-call is exactly what
    /// <see cref="IncomingArtifactCustodyState.Unknown"/> exists for: custody
    /// may already hold the bytes. It has to be recorded as uncertain rather
    /// than left in the state it was offered from, so the next attempt asks
    /// about it before it does anything else - and offers the same bytes under
    /// the same key only because custody owned up to nothing.
    /// </summary>
    [Fact]
    public async Task AThrownHandOverIsRecordedUncertainAndAskedAboutBeforeItIsOfferedAgain()
    {
        var custody = new ThrowingCustody(new TimeoutException("the custody call timed out"));
        var store = new RecordingStore();
        var status = new RecordingCustodyStatus(Confirmed());
        var command = new RetainIncomingArtifact(custody, store, status);
        var occurrence = Staged(store);

        var uncertain = await command.ExecuteAsync(PublicActor(), occurrence, new MemoryStream([1]));

        Assert.Equal(IncomingArtifactCustodyState.Unknown, uncertain.State);
        Assert.Equal(occurrence.OccurrenceId, uncertain.OccurrenceId);
        Assert.Equal(occurrence.CaseId, uncertain.CaseId);
        Assert.Null(uncertain.DocumentId);
        Assert.Null(uncertain.BoxFileId);
        Assert.Equal(uncertain, Assert.Single(store.Recorded));

        // The retry asks first. There is nothing precise to ask with - custody
        // never named a document - so it asks by the key, and only a lookup
        // that observes nothing lets the same bytes go under the same key.
        var retry = await command.ExecuteAsync(PublicActor(), occurrence, new MemoryStream([1]));

        Assert.Equal(IncomingArtifactCustodyState.Unknown, retry.State);
        Assert.Equal(2, custody.Calls);
        Assert.Equal(0, status.Calls);
        Assert.Equal(1, status.LookupCalls);
        Assert.Equal(occurrence.OperationKey, Assert.Single(status.Lookups).OperationKey);
    }

    /// <summary>
    /// A refusal is not an uncertainty. Custody declining the authority is the
    /// one thing it states as a refusal of the acceptance it was attempting,
    /// so it surfaces rather than being buried as an arrival to reconcile -
    /// and where an arrival was staged, that arrival is closed as refused.
    /// </summary>
    [Fact]
    public async Task ARefusedHandOverSurfacesAndClosesTheArrivalItWasClaimedFrom()
    {
        var store = new RecordingStore();
        var custody = new ThrowingCustody(
            new StaffAuthorizationException(StaffAccessRight.SubmitRequestUpload));
        var command = new RetainIncomingArtifact(custody, store);
        var occurrence = Staged(store);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            command.ExecuteAsync(PublicActor(), occurrence, new MemoryStream([1])));

        // The claim this attempt held is closed as the refusal it was, not
        // left uncertain: that is what lets the sender make a new deliberate
        // submission instead of retrying into a slot nothing can resolve.
        var refused = Assert.Single(store.Recorded);
        Assert.Equal(IncomingArtifactCustodyState.Failed, refused.State);
        Assert.Equal(occurrence.OccurrenceId, refused.OccurrenceId);

        // And the same key is not offered again: the refusal answers it.
        var replay = await command.ExecuteAsync(
            PublicActor(),
            occurrence,
            new MemoryStream([1]));

        Assert.Equal(IncomingArtifactCustodyState.Failed, replay.State);
        Assert.Equal(1, custody.Calls);
    }

    /// <summary>
    /// A caller that staged nothing never reaches custody at all. There would
    /// be no claim to win, nothing to record an answer on and nothing for a
    /// retry to reconcile against, so the invariant is enforced here rather
    /// than left to each caller's own discipline - which matters for the
    /// holding-receipt destination this command already carries and no
    /// production caller stages yet.
    /// </summary>
    [Fact]
    public async Task AHandOverWithNoCommittedArrivalIsRefusedBeforeCustodyIsAsked()
    {
        var custody = new RecordingCustody(Confirmed());
        var store = new RecordingStore();

        await Assert.ThrowsAsync<UnclaimedHandOverException>(() =>
            new RetainIncomingArtifact(custody, store).ExecuteAsync(
                PublicActor(),
                Occurrence(),
                new MemoryStream([1])));

        // Not offered, not recorded, and emphatically not an uncertainty a
        // retry would have to reconcile: nothing happened at all.
        Assert.Equal(0, custody.Calls);
        Assert.Empty(store.Recorded);
    }

    /// <summary>
    /// Only this command's own validation refuses malformed input, and it does
    /// so before anything is claimed or offered. An
    /// <see cref="ArgumentException"/> out of the adapter is not that: an
    /// adapter can raise one after it has committed as easily as before, so it
    /// is uncertain like every other mid-call fault.
    /// </summary>
    [Fact]
    public async Task AnAdapterArgumentExceptionIsUncertainAndNotARefusal()
    {
        var custody = new ThrowingCustody(new ArgumentException("the request is malformed"));
        var store = new RecordingStore();
        var status = new RecordingCustodyStatus(Confirmed());
        var command = new RetainIncomingArtifact(custody, store, status);
        var occurrence = Staged(store);

        var uncertain = await command.ExecuteAsync(
            PublicActor(),
            occurrence,
            new MemoryStream([1]));

        Assert.Equal(IncomingArtifactCustodyState.Unknown, uncertain.State);

        // The retry asks under the original key rather than offering again.
        var retry = await command.ExecuteAsync(PublicActor(), occurrence, new MemoryStream([1]));

        Assert.Equal(IncomingArtifactCustodyState.Unknown, retry.State);

        // Asked first, then offered again under the same key: an uncertainty
        // is not a refusal, and a refusal is the only thing that closes a key.
        Assert.Equal(2, custody.Calls);
        Assert.Equal(1, status.LookupCalls);
    }

    /// <summary>
    /// A caller that does not hold the claim asks before it offers anything,
    /// and what it may offer is bounded to the one thing that cannot make a
    /// second file: the same bytes under the same operation key. A committed
    /// intent it finds is reconciled and never re-offered.
    /// </summary>
    [Fact]
    public async Task ACallerThatDoesNotWinTheClaimAsksBeforeItOffersAnything()
    {
        var custody = new RecordingCustody(Confirmed());
        var store = new RecordingStore();
        var status = new RecordingCustodyStatus(Confirmed());
        var command = new RetainIncomingArtifact(custody, store, status);
        var occurrence = Staged(store);

        // The winner takes the claim and is still inside its hand-over.
        Assert.True(await store.TryClaimHandOverAsync(occurrence.OccurrenceId, CancellationToken.None));
        status.Committed[occurrence.OperationKey] = Confirmed();

        var lost = await command.ExecuteAsync(PublicActor(), occurrence, new MemoryStream([1]));

        // It asked by the original key and custody owned up to the intent, so
        // the bytes were never offered a second time.
        Assert.Equal(0, custody.Calls);
        Assert.Equal(1, status.LookupCalls);
        Assert.Equal(occurrence.OperationKey, Assert.Single(status.Lookups).OperationKey);
        Assert.Equal(IncomingArtifactCustodyState.Confirmed, lost.State);
        Assert.Equal(DocumentId, lost.DocumentId);
    }

    /// <summary>
    /// The claim that was taken and never used: a process wins it and dies
    /// before it makes the call, so no intent exists and nothing ever will
    /// unless someone offers the bytes again. The claim is not reopened - the
    /// arrival stays claimed - and the offer is the same bytes under the same
    /// key, which is what custody converges on one intent (Stream A, PR 673
    /// comment 5561151076).
    /// </summary>
    [Fact]
    public async Task AClaimNothingWasEverOfferedUnderIsResolvedByReOfferingTheSameBytes()
    {
        var custody = new RecordingCustody(Confirmed());
        var store = new RecordingStore();
        var status = new RecordingCustodyStatus(Confirmed());
        var command = new RetainIncomingArtifact(custody, store, status);
        var occurrence = Staged(store);

        // The claim is taken and the call is never made.
        Assert.True(await store.TryClaimHandOverAsync(occurrence.OccurrenceId, CancellationToken.None));

        var resolved = await command.ExecuteAsync(PublicActor(), occurrence, new MemoryStream([1]));

        // Asked first, observed nothing, then offered - under the original key
        // and only it.
        Assert.Equal(1, status.LookupCalls);
        Assert.Equal(occurrence.OperationKey, Assert.Single(status.Lookups).OperationKey);
        Assert.Equal(
            occurrence.OperationKey,
            Assert.Single(custody.Requests).OperationKey);
        Assert.Equal(IncomingArtifactCustodyState.Confirmed, resolved.State);
        Assert.Equal(DocumentId, resolved.DocumentId);
        Assert.Equal(resolved, Assert.Single(store.Recorded));
    }

    /// <summary>
    /// An operation key names one file. Bytes that are not the ones its
    /// arrival was committed with are a different submission, and offering
    /// them under this key would make custody's own same-key convergence
    /// return the wrong document - so they are refused before a claim is
    /// touched and before custody is asked anything.
    /// </summary>
    [Fact]
    public async Task BytesThatAreNotTheArrivalsAreRefusedBeforeCustodyIsAsked()
    {
        var custody = new RecordingCustody(Confirmed());
        var store = new RecordingStore();
        var status = new RecordingCustodyStatus(Confirmed());
        var command = new RetainIncomingArtifact(custody, store, status);
        var occurrence = Staged(store, Occurrence(sha256: "aaaa"));

        await Assert.ThrowsAsync<HandOverContentMismatchException>(() =>
            command.ExecuteAsync(
                PublicActor(),
                occurrence with { Sha256 = "bbbb" },
                new MemoryStream([2])));
        await Assert.ThrowsAsync<HandOverContentMismatchException>(() =>
            command.ExecuteAsync(
                PublicActor(),
                occurrence with { ContentLength = 2048 },
                new MemoryStream([1])));

        Assert.Equal(0, custody.Calls);
        Assert.Equal(0, status.LookupCalls);
        Assert.Empty(store.Recorded);

        // The arrival is untouched, so the file it does name can still be
        // offered under it.
        var retained = await command.ExecuteAsync(
            PublicActor(),
            occurrence,
            new MemoryStream([1]));

        Assert.True(retained.IsConfirmed);
        Assert.Equal(1, custody.Calls);
    }

    /// <summary>
    /// The hand-over whose response was lost before its identities could be
    /// written down. There is nothing precise to ask about, so it is asked for
    /// by the operation key it was accepted under - the one identity both
    /// sides still share - and the recovered identities are copied onto the
    /// record without the bytes being offered a second time.
    /// </summary>
    [Fact]
    public async Task AnIdentitylessUncertainHandOverIsRecoveredByItsOriginalOperationKey()
    {
        var custody = new ThrowingCustody(new TimeoutException("the custody call timed out"));
        var store = new RecordingStore();
        var status = new RecordingCustodyStatus(Confirmed());
        var command = new RetainIncomingArtifact(custody, store, status);
        var occurrence = Staged(store);

        var uncertain = await command.ExecuteAsync(PublicActor(), occurrence, new MemoryStream([1]));
        Assert.Equal(IncomingArtifactCustodyState.Unknown, uncertain.State);
        Assert.Null(uncertain.DocumentId);

        // Custody had in fact committed the acceptance before the response was
        // lost, and owns up to it under the same key.
        status.Committed[occurrence.OperationKey] = Confirmed();

        var recovered = await command.ExecuteAsync(PublicActor(), occurrence, new MemoryStream([1]));

        Assert.Equal(IncomingArtifactCustodyState.Confirmed, recovered.State);
        Assert.Equal(DocumentId, recovered.DocumentId);
        Assert.Equal(VersionId, recovered.DocumentVersionId);
        Assert.Equal("box-file", recovered.BoxFileId);

        // Asked, not repeated, and asked by the original key: no second
        // hand-over and no new identity was invented for it.
        Assert.Equal(1, custody.Calls);
        Assert.Equal(0, status.Calls);
        Assert.Equal(
            (CaseId, occurrence.OperationKey),
            Assert.Single(status.Lookups));
    }

    /// <summary>
    /// Any other thrown hand-over is uncertain, whatever its type. The type of
    /// a fault raised mid-call is not evidence about what custody kept, and
    /// this command must never depend on knowing what transport an adapter
    /// speaks - naming one would put an infrastructure dependency in Core.
    /// </summary>
    [Fact]
    public async Task AnyOtherThrownHandOverIsUncertainWhateverItsType()
    {
        Exception[] faults =
        [
            // What an adapter translates a transport fault to. Core names
            // this and never the transport exception itself.
            new IntakeDependencyUnavailableException("custody is unreachable"),
            new TimeoutException("the custody call timed out"),
            new IOException("the connection was reset"),
            // A type this command has never heard of, and two an adapter can
            // raise after the bytes were written as easily as before them.
            new InvalidOperationException("a second operation was started"),
            new ArgumentException("the adapter did not like the request"),
            new NotSupportedException("some adapter's own idea of a fault")
        ];

        foreach (var fault in faults)
        {
            var custody = new ThrowingCustody(fault);
            var store = new RecordingStore();

            var uncertain = await new RetainIncomingArtifact(custody, store).ExecuteAsync(
                PublicActor(),
                Staged(store),
                new MemoryStream([1]));

            Assert.Equal(IncomingArtifactCustodyState.Unknown, uncertain.State);
            Assert.Equal(uncertain, Assert.Single(store.Recorded));
        }
    }

    /// <summary>
    /// The narrow case a transport-typed classifier missed: the sender
    /// disconnects and the request's token is cancelled after custody already
    /// has the bytes. That is an uncertain hand-over like any other, and the
    /// record of it is written on a fresh token, because the cancelled one
    /// would refuse the write and leave the arrival re-offerable.
    /// </summary>
    [Fact]
    public async Task AHandOverCancelledAfterTheBytesWereReadIsUncertainAndIsStillRecorded()
    {
        using var aborted = new CancellationTokenSource();
        var custody = new ThrowingCustody(
            new TaskCanceledException("the sender disconnected"),
            aborted);
        var store = new RecordingStore();
        var command = new RetainIncomingArtifact(custody, store);

        var uncertain = await command.ExecuteAsync(
            PublicActor(),
            Staged(store),
            new MemoryStream([1]),
            aborted.Token);

        Assert.True(aborted.IsCancellationRequested);
        Assert.Equal(IncomingArtifactCustodyState.Unknown, uncertain.State);
        Assert.Equal(uncertain, Assert.Single(store.Recorded));
    }

    /// <summary>
    /// A reconciliation custody's fence turns away - the link was revoked or
    /// expired, or it names another Case - leaves the retention exactly where
    /// it was: never success, never re-offered, and the refusal never reaches
    /// the sender as a fault. What this proves is the command's behaviour on a
    /// refused read, not that senders cannot read: Stream A's published fence
    /// admits the exact active link that arrival came through, which
    /// <c>PublicUploadRetentionWebTests</c> exercises against a real link row.
    /// </summary>
    [Fact]
    public async Task AReconciliationTheActorMayNotReadLeavesTheRetentionWhereItWas()
    {
        var custody = new RecordingCustody(Confirmed() with
        {
            Disposition = CaseArtifactCustodyDisposition.Pending
        });
        var store = new RecordingStore();
        var status = new RefusingCustodyStatus();
        var command = new RetainIncomingArtifact(custody, store, status);
        var occurrence = Staged(store);

        var pending = await command.ExecuteAsync(PublicActor(), occurrence, new MemoryStream([1]));
        var retry = await command.ExecuteAsync(PublicActor(), occurrence, new MemoryStream([1]));

        Assert.Equal(IncomingArtifactCustodyState.Pending, pending.State);
        Assert.Equal(IncomingArtifactCustodyState.Pending, retry.State);

        // Asked and refused, not repeated: one hand-over, one attempted read,
        // and no second record claiming something changed.
        Assert.Equal(1, custody.Calls);
        Assert.Equal(1, status.Calls);
        Assert.Single(store.Recorded);
    }

    private static CaseArtifactCustodyResult Confirmed() => new(
        CaseArtifactCustodyDisposition.Confirmed,
        DocumentId,
        VersionId,
        Guid.NewGuid(),
        "box-file",
        "box-version",
        "aaaa",
        1024,
        "application/pdf",
        null,
        null);

    private sealed class RecordingCustody(CaseArtifactCustodyResult result) : ICaseArtifactCustody
    {
        public int Calls { get; private set; }

        public List<CaseArtifactCustodyRequest> Requests { get; } = [];

        public Task<CaseArtifactCustodyResult> RetainAsync(
            CaseArtifactCustodyRequest request,
            CancellationToken cancellationToken)
        {
            Calls++;
            Requests.Add(request);
            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Custody that reads the bytes and then fails - the shape of a lost
    /// connection or a timeout, where the caller cannot know what was kept.
    /// </summary>
    private sealed class ThrowingCustody(
        Exception exception,
        CancellationTokenSource? abortBeforeThrowing = null) : ICaseArtifactCustody
    {
        public int Calls { get; private set; }

        public Task<CaseArtifactCustodyResult> RetainAsync(
            CaseArtifactCustodyRequest request,
            CancellationToken cancellationToken)
        {
            Calls++;
            request.Content.CopyTo(Stream.Null);

            // A sender that disconnects mid-hand-over: the request's token is
            // cancelled after custody has already read the bytes.
            abortBeforeThrowing?.Cancel();
            return Task.FromException<CaseArtifactCustodyResult>(exception);
        }
    }

    private sealed class RecordingCustodyStatus(CaseArtifactCustodyResult result)
        : ICaseArtifactCustodyStatus
    {
        public int Calls { get; private set; }

        /// <summary>
        /// The committed intents this double will own up to, keyed by the
        /// operation key each was accepted under. A key absent here has no
        /// committed intent to report, which is not permission to start a new
        /// one.
        /// </summary>
        public Dictionary<string, CaseArtifactCustodyResult> Committed { get; } = [];

        public int LookupCalls { get; private set; }

        public List<(Guid CaseId, string OperationKey)> Lookups { get; } = [];

        public Task<CaseArtifactCustodyResult> GetAsync(
            ActionActor actor,
            Guid caseId,
            Guid documentId,
            Guid versionId,
            Guid occurrenceId,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(result);
        }

        // The lookup answers under the same actor rule this double applies to
        // GetAsync — it does not gate on the actor — so the only thing that
        // separates an answer from null is what was recorded, never who asked.
        public Task<CaseArtifactCustodyResult?> FindByOperationKeyAsync(
            ActionActor actor,
            Guid caseId,
            string operationKey,
            CancellationToken cancellationToken)
        {
            LookupCalls++;
            Lookups.Add((caseId, operationKey));
            return Task.FromResult(Committed.GetValueOrDefault(operationKey));
        }
    }

    /// <summary>
    /// Custody's status port refusing this actor. Stream A's published fence
    /// (PR 673 comment 5560737585) is the same one for both status reads -
    /// staff casework, or the exact active, unrevoked and unexpired request
    /// link naming its own Case - so what this double stands for is an actor
    /// that fence turns away: a link that was revoked, expired, or names
    /// another Case. Handing bytes over is not itself permission to read what
    /// became of them; being the link that arrival came through is.
    /// </summary>
    private sealed class RefusingCustodyStatus : ICaseArtifactCustodyStatus
    {
        public int Calls { get; private set; }

        public int LookupCalls { get; private set; }

        public Task<CaseArtifactCustodyResult> GetAsync(
            ActionActor actor,
            Guid caseId,
            Guid documentId,
            Guid versionId,
            Guid occurrenceId,
            CancellationToken cancellationToken)
        {
            Calls++;
            StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
            throw new UnreachableException("The staff-only rule refuses above.");
        }

        /// <summary>
        /// The lookup carries the same fence this double puts on
        /// <see cref="GetAsync"/>, so an actor it refuses is refused whichever
        /// read the command reaches for. Implemented explicitly rather than
        /// left to a default, because a status port that silently answered
        /// would prove nothing about the refusal.
        /// </summary>
        public Task<CaseArtifactCustodyResult?> FindByOperationKeyAsync(
            ActionActor actor,
            Guid caseId,
            string operationKey,
            CancellationToken cancellationToken)
        {
            LookupCalls++;
            StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
            throw new UnreachableException("The staff-only rule refuses above.");
        }
    }

    private sealed class RecordingStore : IIncomingArtifactRetentionStore
    {
        private readonly Dictionary<string, RetainedIncomingArtifact> byOperationKey = [];
        private readonly HashSet<Guid> unclaimed = [];

        /// <summary>
        /// Every record the command asked for, in order, refused or not. The
        /// durable state is <see cref="FindAsync"/>; this is the log of what
        /// was attempted.
        /// </summary>
        public List<RetainedIncomingArtifact> Recorded { get; } = [];

        /// <summary>
        /// Commits the pre-custody arrival a staging caller writes before it
        /// hands anything over, exactly as the public upload path does. It
        /// reads as Unknown - custody has said nothing - and it can be claimed
        /// once.
        /// </summary>
        public void Arrive(IncomingArtifactOccurrence occurrence)
        {
            byOperationKey[occurrence.OperationKey] = new(
                occurrence.OccurrenceId,
                occurrence.OperationKey,
                IncomingArtifactCustodyState.Unknown,
                occurrence.CaseId,
                // The validated bytes, as a real store commits them: what a
                // retry under this key has to offer again to be a retry.
                Sha256: occurrence.Sha256,
                ContentLength: occurrence.ContentLength);
            unclaimed.Add(occurrence.OccurrenceId);
        }

        public Task<RetainedIncomingArtifact?> FindAsync(
            string operationKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(byOperationKey.GetValueOrDefault(operationKey));

        /// <summary>
        /// One arrival, one claim. The set is emptied by the winner, so a
        /// second caller of the same occurrence is told no exactly as the
        /// conditional update tells it no.
        /// </summary>
        public Task<bool> TryClaimHandOverAsync(
            Guid occurrenceId,
            CancellationToken cancellationToken) =>
            Task.FromResult(unclaimed.Remove(occurrenceId));

        /// <summary>
        /// Refuses a cancelled token, exactly as a database-backed store does.
        /// That is what makes the uncertain-hand-over proof meaningful: if the
        /// command recorded on the token the hand-over was cancelled on,
        /// nothing would be written down at all. The write is forward-only and
        /// keeps identities, like the real one.
        /// </summary>
        public Task RecordAsync(
            RetainedIncomingArtifact artifact,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Recorded.Add(artifact);
            byOperationKey[artifact.OperationKey] =
                byOperationKey.TryGetValue(artifact.OperationKey, out var stored)
                    ? Merge(stored, artifact)
                    : artifact;
            return Task.CompletedTask;
        }

        private static RetainedIncomingArtifact Merge(
            RetainedIncomingArtifact stored,
            RetainedIncomingArtifact recorded) =>
            IncomingArtifactCustodyProgress.MovesForward(stored.State, recorded.State)
                ? recorded with
                {
                    DocumentId = recorded.DocumentId ?? stored.DocumentId,
                    DocumentVersionId = recorded.DocumentVersionId ?? stored.DocumentVersionId
                }
                : stored with
                {
                    DocumentId = stored.DocumentId ?? recorded.DocumentId,
                    DocumentVersionId = stored.DocumentVersionId ?? recorded.DocumentVersionId
                };
    }
}
