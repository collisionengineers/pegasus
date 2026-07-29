using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

public sealed class CaseWorkflowPersistenceTests
{
    [Fact]
    public async Task AllowedTransitionPersistsAndReplaysIdempotently()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var lease = await harness.Store.ClaimAsync(new(harness.CaseId, 0, actor, "claim-1"), default);
        var request = new ChangeCaseStateRequest(harness.CaseId, 0, actor, "start-1", "Work started", lease.Token);
        var command = new StartCaseWork(harness.Store);

        var first = await command.ExecuteAsync(request, default);
        var replay = await command.ExecuteAsync(request, default);
        var persisted = await harness.Store.GetAsync(harness.CaseId, default);

        Assert.Equal(CaseLifecycleState.Active, first.State);
        Assert.Equal(first, replay);
        Assert.Equal(first, persisted);
    }

    [Fact]
    public async Task StaleVersionAndCompetingLeaseAreRejected()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
        var firstActor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var secondActor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        _ = await harness.Store.ClaimAsync(new(harness.CaseId, 0, firstActor, "claim-1"), default);

        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.Store.ClaimAsync(new(harness.CaseId, 0, secondActor, "claim-2"), default));

        var lease = await harness.Store.ClaimAsync(new(harness.SecondCaseId, 0, secondActor, "claim-3"), default);
        var hold = new PutCaseOnHold(harness.Store);
        _ = await hold.ExecuteAsync(new(harness.SecondCaseId, 0, secondActor, "hold-1", "Waiting", lease.Token, DateTimeOffset.UtcNow), default);
        await Assert.ThrowsAsync<CaseVersionConflictException>(() =>
            harness.Store.ClaimAsync(new(harness.SecondCaseId, 0, secondActor, "claim-stale"), default));
    }

    [Fact]
    public async Task CreatedInErrorRequiresReplacementAndNeverReopens()
    {
        await using var harness = await WorkflowHarness.CreateAsync();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var close = new CloseCase(harness.Store);
        var lease = await harness.Store.ClaimAsync(new(harness.CaseId, 0, actor, "claim-close"), default);

        await Assert.ThrowsAsync<ArgumentException>(() => close.ExecuteAsync(
            new(harness.CaseId, 0, actor, "close-invalid", "Wrong principal", lease.Token, CaseClosureOutcome.CreatedInError), default));

        var closed = await close.ExecuteAsync(
            new(harness.CaseId, 0, actor, "close-valid", "Wrong principal", lease.Token, CaseClosureOutcome.CreatedInError, harness.SecondCaseId), default);
        Assert.Equal(CaseLifecycleState.CreatedInError, closed.State);

        var reopenLease = await harness.Store.ClaimAsync(new(harness.CaseId, 1, actor, "claim-reopen"), default);
        var reopen = new ReopenCase(harness.Store, new DefaultCaseWorkflowConfiguration());
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopen.ExecuteAsync(
            new(harness.CaseId, 1, actor, "reopen-invalid", "Reopen", reopenLease.Token, CaseReopenDestination.Review,
                new(true, true, true, true, "reviewed")), default));
    }

    private sealed class WorkflowHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private WorkflowHarness(SqliteConnection connection, PooledDbContextFactory<PegasusDbContext> factory, Guid caseId, Guid secondCaseId)
        {
            _connection = connection;
            CaseId = caseId;
            SecondCaseId = secondCaseId;
            Store = new EfCaseWorkflowStore(factory, TimeProvider.System);
        }

        public Guid CaseId { get; }
        public Guid SecondCaseId { get; }
        public EfCaseWorkflowStore Store { get; }

        public static async Task<WorkflowHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<PegasusDbContext>().UseSqlite(connection).Options;
            var factory = new PooledDbContextFactory<PegasusDbContext>(options);
            await using var context = await factory.CreateDbContextAsync();
            await context.Database.EnsureCreatedAsync();
            await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");

            var principalId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            var secondCaseId = Guid.NewGuid();
            await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version) VALUES ({principalId}, {Guid.NewGuid()}, {"TST"}, {Guid.NewGuid()}, {true}, {0L})");
            await InsertCaseAsync(context, caseId, principalId, "TST26001");
            await InsertCaseAsync(context, secondCaseId, principalId, "TST26002");
            await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO CaseWorkflows (CaseId, State, AssignedEngineerId, Version, ConcurrencyToken) VALUES ({caseId}, {nameof(CaseLifecycleState.Review)}, {Guid.NewGuid()}, {0L}, {Guid.NewGuid()})");
            await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO CaseWorkflows (CaseId, State, AssignedEngineerId, Version, ConcurrencyToken) VALUES ({secondCaseId}, {nameof(CaseLifecycleState.Review)}, {Guid.NewGuid()}, {0L}, {Guid.NewGuid()})");
            return new(connection, factory, caseId, secondCaseId);
        }

        private static Task<int> InsertCaseAsync(PegasusDbContext context, Guid caseId, Guid principalId, string reference) =>
            context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, InstructionConfirmedByStaff, ImagesConfirmedByStaff, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {Guid.NewGuid()}, {2026}, {(reference.EndsWith('1') ? 1 : 2)}, {reference}, {"inspection"}, {"review"}, {"pending"}, {Guid.NewGuid()}, {true}, {true}, {true}, {true}, {DateTimeOffset.UtcNow}, {0L}, {Guid.NewGuid()})");

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
        }
    }
}
