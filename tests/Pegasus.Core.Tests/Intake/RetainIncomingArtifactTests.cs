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

    [Fact]
    public async Task AConfirmedRetentionRecordsItsLogicalDocumentAndRemoteIdentities()
    {
        var custody = new RecordingCustody(Confirmed());
        var store = new RecordingStore();
        var retained = await new RetainIncomingArtifact(custody, store).ExecuteAsync(
            PublicActor(),
            Occurrence(),
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
            Occurrence(),
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
        var occurrence = Occurrence();
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
            Occurrence("occurrence-1", "estimate.pdf", "aaaa"),
            new MemoryStream([1]));
        var second = await command.ExecuteAsync(
            PublicActor(),
            Occurrence("occurrence-2", "estimate.pdf", "bbbb"),
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
        var occurrence = Occurrence();

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

    [Fact]
    public async Task AnUncertainHandOverWithNothingToAskAboutStaysUncertain()
    {
        var custody = new RecordingCustody(new(
            CaseArtifactCustodyDisposition.Unknown,
            null, null, null, null, null, null, null, "timeout", null));
        var store = new RecordingStore();
        var status = new RecordingCustodyStatus(Confirmed());
        var command = new RetainIncomingArtifact(custody, store, status);
        var occurrence = Occurrence();

        _ = await command.ExecuteAsync(PublicActor(), occurrence, new MemoryStream([1]));
        var second = await command.ExecuteAsync(PublicActor(), occurrence, new MemoryStream([1]));

        Assert.Equal(IncomingArtifactCustodyState.Unknown, second.State);
        Assert.Equal(0, status.Calls);
        Assert.Equal(1, custody.Calls);
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
    /// A hand-over that throws mid-call is exactly what
    /// <see cref="IncomingArtifactCustodyState.Unknown"/> exists for: custody
    /// may already hold the bytes. It has to be recorded as uncertain rather
    /// than left in the state it was offered from, so the next attempt asks
    /// about it instead of offering the same bytes a second time.
    /// </summary>
    [Fact]
    public async Task AThrownHandOverIsRecordedUncertainAndTheBytesAreNeverOfferedAgain()
    {
        var custody = new ThrowingCustody(new TimeoutException("the custody call timed out"));
        var store = new RecordingStore();
        var status = new RecordingCustodyStatus(Confirmed());
        var command = new RetainIncomingArtifact(custody, store, status);
        var occurrence = Occurrence();

        var uncertain = await command.ExecuteAsync(PublicActor(), occurrence, new MemoryStream([1]));

        Assert.Equal(IncomingArtifactCustodyState.Unknown, uncertain.State);
        Assert.Equal(occurrence.OccurrenceId, uncertain.OccurrenceId);
        Assert.Equal(occurrence.CaseId, uncertain.CaseId);
        Assert.Null(uncertain.DocumentId);
        Assert.Null(uncertain.BoxFileId);
        Assert.Equal(uncertain, Assert.Single(store.Recorded));

        // The retry asks rather than repeats. There is nothing to ask with -
        // custody never named a document - so it honestly stays uncertain, and
        // the bytes are still offered exactly once.
        var retry = await command.ExecuteAsync(PublicActor(), occurrence, new MemoryStream([1]));

        Assert.Equal(IncomingArtifactCustodyState.Unknown, retry.State);
        Assert.Equal(1, custody.Calls);
        Assert.Equal(0, status.Calls);
    }

    /// <summary>
    /// A refusal is not an uncertainty. An authorization failure inside custody
    /// never offered the bytes, so it surfaces instead of being buried as an
    /// arrival nothing can reconcile.
    /// </summary>
    [Fact]
    public async Task ARefusalInsideCustodySurfacesInsteadOfBecomingAnUncertainRetention()
    {
        var custody = new ThrowingCustody(
            new StaffAuthorizationException(StaffAccessRight.SubmitRequestUpload));
        var store = new RecordingStore();
        var command = new RetainIncomingArtifact(custody, store);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => command.ExecuteAsync(
            PublicActor(),
            Occurrence(),
            new MemoryStream([1])));

        Assert.Empty(store.Recorded);
    }

    private static CaseArtifactCustodyResult Confirmed() => new(
        CaseArtifactCustodyDisposition.Confirmed,
        DocumentId,
        VersionId,
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
    private sealed class ThrowingCustody(Exception exception) : ICaseArtifactCustody
    {
        public int Calls { get; private set; }

        public Task<CaseArtifactCustodyResult> RetainAsync(
            CaseArtifactCustodyRequest request,
            CancellationToken cancellationToken)
        {
            Calls++;
            request.Content.CopyTo(Stream.Null);
            return Task.FromException<CaseArtifactCustodyResult>(exception);
        }
    }

    private sealed class RecordingCustodyStatus(CaseArtifactCustodyResult result)
        : ICaseArtifactCustodyStatus
    {
        public int Calls { get; private set; }

        public Task<CaseArtifactCustodyResult> GetAsync(
            ActionActor actor,
            Guid caseId,
            Guid documentId,
            Guid versionId,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingStore : IIncomingArtifactRetentionStore
    {
        private readonly Dictionary<string, RetainedIncomingArtifact> byOperationKey = [];

        public List<RetainedIncomingArtifact> Recorded { get; } = [];

        public Task<RetainedIncomingArtifact?> FindAsync(
            string operationKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(byOperationKey.GetValueOrDefault(operationKey));

        public Task RecordAsync(
            RetainedIncomingArtifact artifact,
            CancellationToken cancellationToken)
        {
            byOperationKey[artifact.OperationKey] = artifact;
            Recorded.Add(artifact);
            return Task.CompletedTask;
        }
    }
}
