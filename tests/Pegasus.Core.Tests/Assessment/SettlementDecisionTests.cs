using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Assessment;

/// <summary>
/// What the Decisions section derives rather than asks for (v28 P30) and the
/// firm's unroadworthy reason bank (v28 P15).
/// </summary>
public sealed class SettlementDecisionTests
{
    private static readonly ActionActor Engineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
    private static readonly ActionActor User = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
    private static readonly ActionActor Automation = ActionActor.Automation("pegasus-automation");

    [Theory]
    [InlineData("repairable", 916.00, 950.00)]
    [InlineData("repairable", 950.00, 950.00)]
    [InlineData("repairable", 950.01, 1000.00)]
    public void TheComputedReserveIsTheRepairCostRoundedUpToTheNextFifty(string outcome, double cost, double expected) =>
        Assert.Equal((decimal)expected, SettlementPolicy.ComputedRepairReserve((decimal)cost, outcome));

    [Theory]
    [InlineData("total_loss")]
    [InlineData("cash_in_lieu")]
    [InlineData("contract_repair")]
    [InlineData(null)]
    public void OnlyARepairableOutcomeImpliesARepairReserve(string? outcome) =>
        Assert.Null(SettlementPolicy.ComputedRepairReserve(916m, outcome));

    [Fact]
    public void NoRepairCostImpliesNoReserve()
    {
        Assert.Null(SettlementPolicy.ComputedRepairReserve(null, "repairable"));
        Assert.Null(SettlementPolicy.ComputedRepairReserve(0m, "repairable"));
        Assert.Equal(50m, SettlementPolicy.ReserveStep);
    }

    [Fact]
    public void ABankWordingIsTrimmedLowerCasedAndStrippedOfItsStop()
    {
        Assert.Equal(
            "the headlamp assemblies are inoperative",
            UnroadworthyReasonBank.Normalize("  The headlamp   assemblies are inoperative.  "));
        Assert.Throws<ArgumentException>(() => UnroadworthyReasonBank.Normalize("   "));
        Assert.Throws<ArgumentException>(() => UnroadworthyReasonBank.Normalize(new string('x', 301)));
        Assert.Equal(7, UnroadworthyReasonBank.Standard.Count);
        Assert.All(UnroadworthyReasonBank.Standard, wording =>
            Assert.Equal(wording, UnroadworthyReasonBank.Normalize(wording)));
    }

    [Fact]
    public void InsertingAWordingStartsTheSentenceThenJoinsWithAnd()
    {
        var first = UnroadworthyReasonBank.Insert(null, "the rear lamp assemblies are inoperative");
        Assert.Equal("The rear lamp assemblies are inoperative", first);

        var second = UnroadworthyReasonBank.Insert(first, "there is a loss of essential fluids");
        Assert.Equal(
            "The rear lamp assemblies are inoperative and there is a loss of essential fluids",
            second);

        // A wording already in the reason is not repeated.
        Assert.Equal(second, UnroadworthyReasonBank.Insert(second, "There is a loss of essential fluids."));
    }

    /// <summary>
    /// PR 792 took the Engineer account type out of the authority rules, so saving
    /// to a firm's reason bank is a staff act. An actor who is not staff is still
    /// refused, and a User saves the same as an Engineer.
    /// </summary>
    [Fact]
    public async Task OnlyStaffSaveAWordingAndTheBankNeverHoldsItTwice()
    {
        var store = new RecordingBank();
        var save = new SaveUnroadworthyReason(store);

        await Assert.ThrowsAsync<InvalidOperationException>(() => save.ExecuteAsync(
            new("QDOS", "the wheels are missing", Automation), CancellationToken.None));

        var saved = await save.ExecuteAsync(new("QDOS", "The wheels are missing.", User), CancellationToken.None);
        Assert.NotNull(saved);
        Assert.Equal("the wheels are missing", saved.Text);
        Assert.Equal("QDOS", saved.PrincipalCode);

        // The same wording again, and one the standard list already offers, add nothing.
        Assert.Null(await save.ExecuteAsync(new("QDOS", "the wheels are missing", Engineer), CancellationToken.None));
        Assert.Null(await save.ExecuteAsync(
            new("QDOS", "There is a loss of essential fluids.", Engineer), CancellationToken.None));
        Assert.Single(store.Rows);
    }

    [Fact]
    public async Task AStaleListReadMakesTheLosingAddReturnNoSavedWording()
    {
        var store = new StaleListBank();
        var save = new SaveUnroadworthyReason(store);
        var request = new SaveUnroadworthyReasonRequest(
            "QDOS", "The brake line is severed.", Engineer);

        var results = await Task.WhenAll(
            save.ExecuteAsync(request, CancellationToken.None),
            save.ExecuteAsync(request, CancellationToken.None));

        Assert.Equal(1, results.Count(result => result is not null));
        Assert.Single(store.Rows);
    }

    private sealed class RecordingBank : IUnroadworthyReasonBankStore
    {
        public List<UnroadworthyReason> Rows { get; } = [];

        public Task<IReadOnlyList<UnroadworthyReason>> ListAsync(string principalCode, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<UnroadworthyReason>>(
                Rows.Where(row => row.PrincipalCode == principalCode).ToArray());

        public Task<UnroadworthyReason?> AddAsync(
            SaveUnroadworthyReasonRequest request, string normalized, CancellationToken cancellationToken)
        {
            var row = new UnroadworthyReason(
                Guid.NewGuid(), request.PrincipalCode, normalized, request.Actor.SubjectId, DateTimeOffset.UtcNow);
            Rows.Add(row);
            return Task.FromResult<UnroadworthyReason?>(row);
        }
    }

    private sealed class StaleListBank : IUnroadworthyReasonBankStore
    {
        private readonly object sync = new();
        private readonly TaskCompletionSource<bool> listReads =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> addCalls =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int listReadCount;
        private int addCallCount;

        public List<UnroadworthyReason> Rows { get; } = [];

        public async Task<IReadOnlyList<UnroadworthyReason>> ListAsync(
            string principalCode, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref listReadCount) == 2)
            {
                listReads.TrySetResult(true);
            }
            await listReads.Task.WaitAsync(cancellationToken);
            return [];
        }

        public async Task<UnroadworthyReason?> AddAsync(
            SaveUnroadworthyReasonRequest request, string normalized, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref addCallCount) == 2)
            {
                addCalls.TrySetResult(true);
            }
            await addCalls.Task.WaitAsync(cancellationToken);
            lock (sync)
            {
                if (Rows.Any(row => row.PrincipalCode == request.PrincipalCode
                    && string.Equals(row.Text, normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    return null;
                }

                var row = new UnroadworthyReason(
                    Guid.NewGuid(), request.PrincipalCode, normalized, request.Actor.SubjectId,
                    DateTimeOffset.UtcNow);
                Rows.Add(row);
                return row;
            }
        }
    }
}
