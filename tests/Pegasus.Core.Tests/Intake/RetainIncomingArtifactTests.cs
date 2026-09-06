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
