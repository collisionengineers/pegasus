using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Assessment;

/// <summary>
/// The unroadworthy reason bank (v28 P15): one list for every Case, kept from
/// Administration (operator, 11 November 2026; R952 to R954).
/// </summary>
public sealed class SettlementDecisionTests
{
    private static readonly ActionActor Engineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
    private static readonly ActionActor User = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
    private static readonly ActionActor Automation = ActionActor.Automation("pegasus-automation");
    private static readonly ActionActor Administrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    [Fact]
    public void ABankWordingIsTrimmedLowerCasedAndStrippedOfItsStop()
    {
        Assert.Equal(
            "the headlamp assemblies are inoperative",
            UnroadworthyReasonBank.Normalize("  The headlamp   assemblies are inoperative.  "));
        Assert.Throws<ArgumentException>(() => UnroadworthyReasonBank.Normalize("   "));
        Assert.Throws<ArgumentException>(() => UnroadworthyReasonBank.Normalize(new string('x', 301)));
    }

    /// <summary>
    /// PR 792 took the Engineer account type out of the authority rules, and
    /// saving to the reason bank is casework: a User saves the same as an
    /// Engineer, and the Automation actor as staff do (operator, 7 October
    /// 2026). An actor without the casework right is refused.
    /// </summary>
    [Fact]
    public async Task EveryCaseworkActorSavesAWordingAndTheBankNeverHoldsItTwice()
    {
        var store = new RecordingBank();
        var save = new SaveUnroadworthyReason(store);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => save.ExecuteAsync(
            new("the wheels are missing", ActionActor.SystemWorker("case-worker")), CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => save.ExecuteAsync(
            new("the wheels are missing", ActionActor.Principal(Guid.NewGuid())), CancellationToken.None));
        Assert.Empty(store.Rows);

        var saved = await save.ExecuteAsync(new("The wheels are missing.", User), CancellationToken.None);
        Assert.NotNull(saved);
        Assert.Equal("the wheels are missing", saved.Text);

        // The same wording again, however it is cased or stopped, adds nothing.
        Assert.Null(await save.ExecuteAsync(new("the wheels are missing", Engineer), CancellationToken.None));
        Assert.Null(await save.ExecuteAsync(new("The Wheels are missing.", Engineer), CancellationToken.None));
        Assert.Single(store.Rows);

        var automated = await save.ExecuteAsync(
            new("The steering is unsafe.", Automation), CancellationToken.None);
        Assert.NotNull(automated);
        Assert.Equal(Automation.SubjectId, automated.CreatedBy);
        Assert.Equal(2, store.Rows.Count);
    }

    [Fact]
    public async Task AStaleListReadMakesTheLosingAddReturnNoSavedWording()
    {
        var store = new StaleListBank();
        var save = new SaveUnroadworthyReason(store);
        var request = new SaveUnroadworthyReasonRequest("The brake line is severed.", Engineer);

        var results = await Task.WhenAll(
            save.ExecuteAsync(request, CancellationToken.None),
            save.ExecuteAsync(request, CancellationToken.None));

        Assert.Equal(1, results.Count(result => result is not null));
        Assert.Single(store.Rows);
    }

    /// <summary>
    /// Rewording and removing are an Administrator's (operator, 11 November
    /// 2026; R953): casework actors and Automation are refused, a reworded
    /// wording takes the bank's one shape, and both carry the wording the
    /// page rendered to the store.
    /// </summary>
    [Fact]
    public async Task OnlyAnAdministratorRewordsOrRemovesAWording()
    {
        var store = new RecordingBank();
        var reword = new RewordUnroadworthyReason(store);
        var remove = new RemoveUnroadworthyReason(store);
        var id = Guid.NewGuid();

        foreach (var actor in new[] { Engineer, User, Automation })
        {
            await Assert.ThrowsAsync<StaffAuthorizationException>(() => reword.ExecuteAsync(
                new(id, "the wheels are missing", "the wheels are gone", actor), CancellationToken.None));
            await Assert.ThrowsAsync<StaffAuthorizationException>(() => remove.ExecuteAsync(
                new(id, "the wheels are missing", actor), CancellationToken.None));
        }
        await Assert.ThrowsAsync<ArgumentException>(() => reword.ExecuteAsync(
            new(id, "the wheels are missing", "   ", Administrator), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => reword.ExecuteAsync(
            new(Guid.Empty, "the wheels are missing", "the wheels are gone", Administrator), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => remove.ExecuteAsync(
            new(id, " ", Administrator), CancellationToken.None));
        Assert.Empty(store.Changes);

        Assert.Equal(UnroadworthyReasonChange.Done, await reword.ExecuteAsync(
            new(id, "the wheels are missing", "  The wheels   are gone. ", Administrator), CancellationToken.None));
        Assert.Equal(UnroadworthyReasonChange.Done, await remove.ExecuteAsync(
            new(id, "the wheels are gone", Administrator), CancellationToken.None));
        (Guid, string, string?)[] expected =
            [(id, "the wheels are missing", "the wheels are gone"), (id, "the wheels are gone", null)];
        Assert.Equal(expected, store.Changes);
    }

    private sealed class RecordingBank : IUnroadworthyReasonBankStore
    {
        public List<UnroadworthyReason> Rows { get; } = [];

        public List<(Guid Id, string RenderedText, string? Normalized)> Changes { get; } = [];

        public Task<IReadOnlyList<UnroadworthyReason>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<UnroadworthyReason>>(Rows.ToArray());

        public Task<UnroadworthyReason?> AddAsync(
            SaveUnroadworthyReasonRequest request, string normalized, CancellationToken cancellationToken)
        {
            var row = new UnroadworthyReason(
                Guid.NewGuid(), normalized, request.Actor.SubjectId, DateTimeOffset.UtcNow);
            Rows.Add(row);
            return Task.FromResult<UnroadworthyReason?>(row);
        }

        public Task<UnroadworthyReasonChange> RewordAsync(
            Guid id, string renderedText, string normalized, CancellationToken cancellationToken)
        {
            Changes.Add((id, renderedText, normalized));
            return Task.FromResult(UnroadworthyReasonChange.Done);
        }

        public Task<UnroadworthyReasonChange> RemoveAsync(
            Guid id, string renderedText, CancellationToken cancellationToken)
        {
            Changes.Add((id, renderedText, null));
            return Task.FromResult(UnroadworthyReasonChange.Done);
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

        public async Task<IReadOnlyList<UnroadworthyReason>> ListAsync(CancellationToken cancellationToken)
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
                if (Rows.Any(row => string.Equals(row.Text, normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    return null;
                }

                var row = new UnroadworthyReason(
                    Guid.NewGuid(), normalized, request.Actor.SubjectId, DateTimeOffset.UtcNow);
                Rows.Add(row);
                return row;
            }
        }

        public Task<UnroadworthyReasonChange> RewordAsync(
            Guid id, string renderedText, string normalized, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<UnroadworthyReasonChange> RemoveAsync(
            Guid id, string renderedText, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
