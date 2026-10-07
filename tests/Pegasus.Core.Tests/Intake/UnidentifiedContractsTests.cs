using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Core.Tests.Intake;

public sealed class UnidentifiedContractsTests
{
    [Theory]
    [InlineData(1, "U1")]
    [InlineData(99999, "U99999")]
    [InlineData(long.MaxValue, "U9223372036854775807")]
    public void ReferenceFormatIsCanonicalAndUnbounded(long sequence, string expected)
    {
        var reference = UnidentifiedReferenceFormat.Create(sequence);

        Assert.Equal(expected, reference);
        Assert.True(UnidentifiedReferenceFormat.TryParse(reference, out var parsed));
        Assert.Equal(sequence, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("u1")]
    [InlineData("U0")]
    [InlineData("U01")]
    [InlineData("U 1")]
    [InlineData("U9223372036854775808")]
    public void ReferenceParserRejectsNoncanonicalValues(string? value)
    {
        Assert.False(UnidentifiedReferenceFormat.TryParse(value, out _));
    }

    [Fact]
    public void ResolutionRequiresStaffOrAutomationActor()
    {
        var request = new ResolveUnidentifiedRequest(
            Guid.NewGuid(),
            0,
            ActionActor.SystemWorker("worker"),
            "op-1",
            "resolved",
            UnidentifiedResolutionTargetKind.ExternalReference,
            "target",
            null,
            DateTimeOffset.UtcNow);

        Assert.Throws<UnauthorizedAccessException>(() => UnidentifiedValidation.ValidateResolve(request));
    }

    [Fact]
    public void GroupOriginIsExplicitAndNonempty()
    {
        var id = Guid.NewGuid();

        var origin = UnidentifiedOrigin.SubmissionGroup(id);

        Assert.Equal(UnidentifiedOriginKind.SubmissionGroup, origin.Kind);
        Assert.Equal(id, origin.Id);
        Assert.Throws<ArgumentException>(() => UnidentifiedOrigin.Validate(new(UnidentifiedOriginKind.Receipt, Guid.Empty)));
    }

    [Fact]
    public void ReopenRequiresAStaffOrAutomationActorAndACompleteTransition()
    {
        var itemId = Guid.NewGuid();
        var valid = new ReopenUnidentifiedRequest(
            itemId,
            3,
            ActionActor.Automation(ReconcileUnidentifiedDestinations.AutomationActorId),
            "intake-unidentified-reopen:key",
            "The receipt's effective destination no longer matches this resolution.",
            DateTimeOffset.UtcNow);

        UnidentifiedValidation.ValidateReopen(valid);

        // A system worker is authorised to REGISTER retained material but not
        // to withdraw a resolution — the same rule ValidateResolve applies.
        Assert.Throws<UnauthorizedAccessException>(() =>
            UnidentifiedValidation.ValidateReopen(valid with
            {
                Actor = ActionActor.SystemWorker("intake-processing")
            }));
        Assert.Throws<ArgumentException>(() =>
            UnidentifiedValidation.ValidateReopen(valid with { UnidentifiedItemId = Guid.Empty }));
        Assert.Throws<ArgumentException>(() =>
            UnidentifiedValidation.ValidateReopen(valid with { ExpectedVersion = -1 }));
        Assert.Throws<ArgumentException>(() =>
            UnidentifiedValidation.ValidateReopen(valid with { OperationKey = "  " }));
        Assert.Throws<ArgumentException>(() =>
            UnidentifiedValidation.ValidateReopen(valid with { Reason = "" }));
    }

    /// <summary>
    /// A double with no recheck queue keeps no manual-association versions and
    /// no reconciliation watermark, so it can never say which of its rows have
    /// gone stale. An empty page is the honest answer; writing a watermark is
    /// not something it can do at all. The one production implementation
    /// overrides all three.
    /// </summary>
    [Fact]
    public async Task AStoreWithoutARecheckQueueReportsNoneAndRefusesToWriteAWatermark()
    {
        IUnidentifiedStore store = new RecheckFreeStore();

        Assert.Empty(await store.ListResolutionsToRecheckAsync(50));
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            store.MarkResolutionRecheckedAsync(Guid.NewGuid(), 1));
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            store.ReopenAsync(new(
                Guid.NewGuid(),
                0,
                ActionActor.Automation("intake-processing"),
                "op",
                "reason",
                DateTimeOffset.UtcNow)));
    }

    private sealed class RecheckFreeStore : IUnidentifiedStore
    {
        public Task<UnidentifiedRegisterResult> RegisterAsync(
            RegisterUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedRegisterResult?> ProbeRegisterReplayAsync(
            RegisterUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedResolveResult> ResolveAsync(
            ResolveUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedItem?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<UnidentifiedItem?>(null);

        public Task<UnidentifiedItem?> GetByReferenceAsync(
            string reference,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<UnidentifiedItem?>(null);

        public Task<UnidentifiedItem?> GetByOriginAsync(
            UnidentifiedOrigin origin,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<UnidentifiedItem?>(null);

        public Task<IReadOnlyList<UnidentifiedItem>> ListAsync(
            UnidentifiedState? state = UnidentifiedState.Open,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UnidentifiedItem>>([]);

        public Task<IReadOnlyList<UnidentifiedQueueRow>> ListQueueAsync(
            UnidentifiedMediaKind? mediaKind,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UnidentifiedQueueRow>>([]);

        public Task<int> CountOpenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<IReadOnlyList<UnidentifiedHistoryEntry>> HistoryAsync(
            Guid unidentifiedItemId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UnidentifiedHistoryEntry>>([]);
    }

    [Theory]
    [InlineData(IntakeSourceChannel.Mailbox, "application/pdf", UnidentifiedMediaKind.Email)]
    [InlineData(IntakeSourceChannel.Mailbox, "image/jpeg", UnidentifiedMediaKind.Email)]
    [InlineData(IntakeSourceChannel.ManualUpload, "image/jpeg", UnidentifiedMediaKind.Image)]
    [InlineData(IntakeSourceChannel.ManualUpload, "image/png", UnidentifiedMediaKind.Image)]
    [InlineData(IntakeSourceChannel.ManualUpload, "application/pdf", UnidentifiedMediaKind.Document)]
    [InlineData(IntakeSourceChannel.ManualUpload, "message/rfc822", UnidentifiedMediaKind.Document)]
    [InlineData(IntakeSourceChannel.ManualUpload, "video/mp4", UnidentifiedMediaKind.Document)]
    [InlineData(IntakeSourceChannel.Automation, "application/msword", UnidentifiedMediaKind.Document)]
    public void MediaKindPolicyClassifiesByChannelThenContentType(
        IntakeSourceChannel channel,
        string mediaType,
        UnidentifiedMediaKind expected)
    {
        Assert.Equal(expected, UnidentifiedMediaKindPolicy.Classify(channel, mediaType));
    }

    /// <summary>
    /// One table gives a file its broad kind and its name, so the Inbox
    /// preview's attachment count and the Unidentified description can never
    /// disagree about the same media type. A type without a name of its own
    /// is described by its extension, as before.
    /// </summary>
    [Theory]
    [InlineData("image/jpeg", "front.jpg", BroadFileKind.Image, "Image")]
    [InlineData("application/pdf", "estimate.pdf", BroadFileKind.Document, "PDF")]
    [InlineData("application/msword", "letter.doc", BroadFileKind.Document, "Word document")]
    [InlineData("application/vnd.ms-excel", "claim.xls", BroadFileKind.Document, "Spreadsheet")]
    [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "claim.xlsx", BroadFileKind.Document, "Spreadsheet")]
    [InlineData("application/vnd.openxmlformats-officedocument.presentationml.presentation", "deck.pptx", BroadFileKind.Document, "PPTX file")]
    [InlineData("text/plain", "notes.txt", BroadFileKind.Document, "Text file")]
    [InlineData("message/rfc822", "forwarded.eml", BroadFileKind.Email, "E-mail")]
    [InlineData("application/vnd.ms-outlook", "forwarded.msg", BroadFileKind.Email, "E-mail")]
    [InlineData("video/mp4", "walkround.mp4", BroadFileKind.Video, "MP4 file")]
    [InlineData("application/zip", "bundle.zip", BroadFileKind.Other, "ZIP file")]
    [InlineData(null, null, BroadFileKind.Other, "File")]
    public void OneTableGivesAFileItsBroadKindAndItsName(
        string? mediaType,
        string? fileName,
        BroadFileKind expectedKind,
        string expectedName)
    {
        Assert.Equal(expectedKind, UnidentifiedFileKind.Broad(mediaType));
        Assert.Equal(expectedName, UnidentifiedFileKind.Describe(mediaType, fileName));
    }
}

public sealed class CloseUnidentifiedTests
{
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CloseWithReasonIsAResolutionWithNoDestinationThatKeepsTheReference()
    {
        var resolve = new RecordingResolve();
        var sut = new CloseUnidentified(resolve);
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        var result = await sut.ExecuteAsync(new(ItemId, 2, staff, "close-1", "Duplicate of an instruction already on file.", Now));

        var request = Assert.Single(resolve.Requests);
        Assert.Equal(UnidentifiedResolutionTargetKind.Closed, request.TargetKind);
        Assert.Equal(CloseUnidentified.ClosedTargetId, request.TargetId);
        Assert.Null(request.TargetReference);
        Assert.Equal("Duplicate of an instruction already on file.", request.Reason);
        Assert.Equal(2, request.ExpectedVersion);
        Assert.True(result.Item.IsClosed);
        Assert.Equal("U7", result.Item.Reference);
    }

    /// <summary>
    /// Close with reason is casework: the Automation Actor closes in its own
    /// name as staff do (ADR-0064); a system worker or Principal cannot.
    /// </summary>
    [Fact]
    public async Task TheAutomationActorClosesWithAReasonAndOtherActorsCannot()
    {
        var resolve = new RecordingResolve();
        var sut = new CloseUnidentified(resolve);

        var result = await sut.ExecuteAsync(new(ItemId, 2, ActionActor.Automation("client"), "mcp:close-2", "No.", Now));

        Assert.True(result.Item.IsClosed);
        Assert.Equal(ActorKind.Automation, Assert.Single(resolve.Requests).Actor.Kind);
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => sut.ExecuteAsync(new(ItemId, 2, ActionActor.SystemWorker("worker"), "close-3", "No.", Now)));
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => sut.ExecuteAsync(new(ItemId, 2, ActionActor.Principal(Guid.NewGuid()), "close-4", "No.", Now)));
        Assert.Single(resolve.Requests);
    }

    [Fact]
    public async Task TheAutomationActorReopensWithAReasonAndOtherActorsCannot()
    {
        var store = new ClosingStore();
        var sut = new ReopenUnidentified(store);

        await sut.ExecuteAsync(new(ItemId, 3, ActionActor.Automation("client"), "mcp:reopen-1", "Wrong file.", Now));

        Assert.Equal(ActorKind.Automation, Assert.Single(store.Reopens).Actor.Kind);
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => sut.ExecuteAsync(new(ItemId, 3, ActionActor.SystemWorker("worker"), "reopen-2", "Wrong file.", Now)));
        Assert.Single(store.Reopens);
    }

    [Fact]
    public async Task ClosingValidatesLikeAnyResolutionAndNeedsNoDestinationLookup()
    {
        // The real resolver, with no destination queries at all: a closure names none.
        var store = new ClosingStore();
        var sut = new CloseUnidentified(new ResolveUnidentified(store));

        var result = await sut.ExecuteAsync(new(ItemId, 0, ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]), "close-3", "Not ours.", Now));

        Assert.True(result.Item.IsClosed);
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => sut.ExecuteAsync(new(ItemId, 0, ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]), "close-4", " ", Now)));
    }

    private static UnidentifiedItem Closed(ResolveUnidentifiedRequest request) => new(
        ItemId, 7, "U7", UnidentifiedOrigin.Receipt(Guid.NewGuid()), UnidentifiedReasonCode.CouldNotBeRead,
        "Could not be read", UnidentifiedState.Resolved, Now.AddDays(-1), request.ResolvedAtUtc,
        ActionActor.SystemWorker("intake-processing"), request.Actor, request.Reason,
        request.TargetKind, request.TargetId, request.TargetReference, 1)
    {
        FileKind = "PDF"
    };

    private sealed class RecordingResolve : IResolveUnidentified
    {
        public List<ResolveUnidentifiedRequest> Requests { get; } = [];

        public Task<UnidentifiedResolveResult> ExecuteAsync(ResolveUnidentifiedRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var item = Closed(request);
            return Task.FromResult(new UnidentifiedResolveResult(
                item,
                new(Guid.NewGuid(), ItemId, UnidentifiedState.Open, UnidentifiedState.Resolved, request.Actor, request.ResolvedAtUtc, request.Reason, request.OperationKey, request.TargetKind, request.TargetId, request.TargetReference),
                false));
        }
    }

    private sealed class ClosingStore : IUnidentifiedStore
    {
        public Task<UnidentifiedRegisterResult> RegisterAsync(RegisterUnidentifiedRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UnidentifiedRegisterResult?> ProbeRegisterReplayAsync(RegisterUnidentifiedRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedResolveResult> ResolveAsync(ResolveUnidentifiedRequest request, CancellationToken cancellationToken = default)
        {
            var item = Closed(request);
            return Task.FromResult(new UnidentifiedResolveResult(
                item,
                new(Guid.NewGuid(), ItemId, UnidentifiedState.Open, UnidentifiedState.Resolved, request.Actor, request.ResolvedAtUtc, request.Reason, request.OperationKey, request.TargetKind, request.TargetId, request.TargetReference),
                false));
        }

        public Task<UnidentifiedItem?> GetAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UnidentifiedItem?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UnidentifiedItem?> GetByOriginAsync(UnidentifiedOrigin origin, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UnidentifiedItem>> ListAsync(UnidentifiedState? state = UnidentifiedState.Open, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UnidentifiedQueueRow>> ListQueueAsync(UnidentifiedMediaKind? mediaKind, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountOpenAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<UnidentifiedHistoryEntry>> HistoryAsync(Guid unidentifiedItemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public List<ReopenUnidentifiedRequest> Reopens { get; } = [];

        public Task<UnidentifiedReopenResult> ReopenAsync(ReopenUnidentifiedRequest request, CancellationToken cancellationToken = default)
        {
            Reopens.Add(request);
            return Task.FromResult<UnidentifiedReopenResult>(null!);
        }
    }
}
