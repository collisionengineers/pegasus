using System.Collections.Immutable;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;

namespace Pegasus.Core.Tests.Operations;

public sealed class OperationsUseCaseTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(StaffRole.Administrator)]
    [InlineData(StaffRole.Engineer)]
    [InlineData(StaffRole.User)]
    public void EveryStaffRoleCanTakeUnassignedCaseAndTriageWork(StaffRole role)
    {
        var actor = ActionActor.Staff(Guid.NewGuid(), [role]);

        Assert.True(NeedsAttentionPolicy.CanTake(NeedsAttentionKind.UnassignedEngineer, actor));
        Assert.True(NeedsAttentionPolicy.CanTake(NeedsAttentionKind.Triage, actor));
    }

    [Fact]
    public void OwnerTextNamesAPersonOrTheEmptySlot()
    {
        Assert.Equal("Alex", NeedsAttentionPolicy.OwnerText(NeedsAttentionKind.ReviewCase, "Alex"));
        Assert.Equal("Alex", NeedsAttentionPolicy.OwnerText(NeedsAttentionKind.AiDraft, "Alex"));
        Assert.Equal("Unassigned", NeedsAttentionPolicy.OwnerText(NeedsAttentionKind.ReviewCase, null));
        Assert.Equal("Unassigned", NeedsAttentionPolicy.OwnerText(NeedsAttentionKind.Triage, "  "));
        Assert.Equal("No owner", NeedsAttentionPolicy.OwnerText(NeedsAttentionKind.CaseChase, null));
    }

    [Fact]
    public void EveryRowKindDecidesItsEmptyOwnerText()
    {
        foreach (var kind in Enum.GetValues<NeedsAttentionKind>())
        {
            Assert.Contains(
                NeedsAttentionPolicy.OwnerText(kind, null),
                new[] { NeedsAttentionPolicy.UnassignedOwner, NeedsAttentionPolicy.NoPersonOwner });
        }
    }

    [Fact]
    public void PairedVehicleImagesAreNotWorkToTake()
    {
        Assert.False(NeedsAttentionPolicy.CanTake(
            NeedsAttentionKind.VehicleImagesPaired,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator])));
    }

    [Fact]
    public void OpenTasksHaveAPersonSlotAndAreNotWorkToTake()
    {
        Assert.Equal("Unassigned", NeedsAttentionPolicy.OwnerText(NeedsAttentionKind.OpenTasks, null));
        Assert.Equal("Alex", NeedsAttentionPolicy.OwnerText(NeedsAttentionKind.OpenTasks, "Alex"));
        Assert.False(NeedsAttentionPolicy.CanTake(
            NeedsAttentionKind.OpenTasks,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator])));
    }

    [Fact]
    public void NonHumanActorsCannotTakeUnassignedCaseAndTriageWork()
    {
        Assert.False(NeedsAttentionPolicy.CanTake(
            NeedsAttentionKind.UnassignedEngineer,
            ActionActor.Automation("automation")));
        Assert.False(NeedsAttentionPolicy.CanTake(
            NeedsAttentionKind.Triage,
            ActionActor.Principal(Guid.NewGuid())));
        Assert.False(NeedsAttentionPolicy.CanTake(
            NeedsAttentionKind.Triage,
            ActionActor.SystemWorker("worker")));
    }

    [Fact]
    public async Task RequestProjectionIsStaffBounded()
    {
        var projectionStore = new RecordingRequestStore(EmptyRequestProjection());
        var timeProvider = new FixedTimeProvider(FixedUtcNow);
        var query = new GetRequestOperations(projectionStore, timeProvider);

        var projection = await query.ExecuteAsync(StaffActor(), cancellationToken: CancellationToken.None);

        Assert.Empty(projection.Items);
        Assert.Equal(GetRequestOperations.MaximumItems, projectionStore.MaximumItems);
        Assert.Equal(FixedUtcNow, projectionStore.AsOfUtc);
    }

    [Fact]
    public async Task RequestProjectionUsesCallerInstantInsteadOfClockInstant()
    {
        var capturedUtc = FixedUtcNow.AddTicks(-1);
        var projectionStore = new RecordingRequestStore(new(
            ImmutableArray.Create(ExternalWork(capturedUtc)),
            LimitReached: false));
        var query = new GetRequestOperations(
            projectionStore,
            new FixedTimeProvider(FixedUtcNow.AddTicks(1)));

        var result = await query.ExecuteAsync(
            StaffActor(),
            asOfUtc: capturedUtc,
            cancellationToken: CancellationToken.None);

        Assert.Equal(capturedUtc, projectionStore.AsOfUtc);
        Assert.NotEqual(FixedUtcNow.AddTicks(1), projectionStore.AsOfUtc);
        Assert.Equal(capturedUtc, Assert.Single(result.Items).LastActivityAtUtc);
    }

    private static ActionActor StaffActor() =>
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

    private static RequestOperationsProjection EmptyRequestProjection() => new(
        ImmutableArray<RequestOperationProjection>.Empty,
        LimitReached: false);

    private static RequestOperationProjection ExternalWork(DateTimeOffset lastActivityAtUtc) => new(
        Guid.NewGuid(),
        RequestOperationState.Pending,
        Guid.NewGuid(),
        "QDOS31001",
        "QDOS",
        lastActivityAtUtc,
        ExternalKind: "document_custody",
        AttemptCount: 1,
        FailureCode: null,
        FailureReason: null,
        CanRetry: false);

    private sealed class RecordingRequestStore(RequestOperationsProjection result)
        : IRequestOperationsProjectionStore
    {
        public int? MaximumItems { get; private set; }

        public DateTimeOffset? AsOfUtc { get; private set; }

        public Task<RequestOperationsProjection> GetAsync(
            int maximumItems,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken)
        {
            MaximumItems = maximumItems;
            AsOfUtc = nowUtc;
            return Task.FromResult(result);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
