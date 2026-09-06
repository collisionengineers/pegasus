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

        public Task<UnidentifiedResolveResult?> ProbeResolveReplayAsync(
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
    [InlineData(IntakeSourceChannel.Automation, "application/msword", UnidentifiedMediaKind.Document)]
    public void MediaKindPolicyClassifiesByChannelThenContentType(
        IntakeSourceChannel channel,
        string mediaType,
        UnidentifiedMediaKind expected)
    {
        Assert.Equal(expected, UnidentifiedMediaKindPolicy.Classify(channel, mediaType));
    }
}
