using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class DueChaserSweepPersistenceTests
{
    private static readonly DateTimeOffset StartUtc =
        new(2026, 7, 30, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SweepAdvancesDueWorkAndPersistsCopyableDraftWithActionHistory()
    {
        await using var harness = await DueChaserHarness.CreateAsync(StartUtc);

        var result = await harness.Runner.ExecuteAsync(50, default);

        Assert.Equal(new RunDueChasersResult(1, 1, 0, 0), result);
        var generated = await harness.ChaserStore.GetLatestAsync(harness.CaseId, default);
        Assert.NotNull(generated);
        Assert.Equal(harness.CaseId, generated.CaseId);
        Assert.Equal(StartUtc, generated.ScheduledAtUtc);
        Assert.Equal(StartUtc, generated.GeneratedAtUtc);
        Assert.Equal(CaseChaseSchedule.NextChaseAt(StartUtc), generated.NextChaseAtUtc);
        Assert.Equal(
            "Please provide the outstanding material for case QDOS26001: Vehicle images.",
            generated.CopyableText);
        Assert.Equal(harness.RequestLinkId, generated.RequestLinkReference);
        Assert.Equal(
            RunDueChasers.MissingMaterialRequestLinkPurpose,
            generated.RequestLinkPurpose);
        Assert.Equal(1, generated.DueWorkVersion);

        var workflow = await harness.WorkflowStore.GetAsync(harness.CaseId, default);
        Assert.Equal(1, workflow?.DueWork?.Version);
        Assert.Equal(generated.NextChaseAtUtc, workflow?.DueWork?.NextChaseAtUtc);
        Assert.Null(workflow?.DueWork?.MostRecentOutcome);
        Assert.Null(workflow?.DueWork?.MostRecentChannel);

        var history = await harness.ReadActionHistoryAsync();
        Assert.Equal("case_due_work", history.AggregateType);
        Assert.Equal(harness.CaseId.ToString("D"), history.AggregateId);
        Assert.Equal("due_chaser_generated", history.EventKind);
        Assert.Equal(nameof(ActorKind.SystemWorker), history.ActorKind);
        Assert.Equal(RunDueChasers.WorkerSubjectId, history.ActorSubjectId);
        Assert.Equal("Succeeded", history.Outcome);
        Assert.Equal(StartUtc, history.OccurredAtUtc);
        Assert.Equal(CaseChaseSchedule.PolicyIdentity, history.PolicyVersion);
        Assert.StartsWith("due-chaser:", history.CorrelationId, StringComparison.Ordinal);
        Assert.Contains("\"dueWorkVersion\":0", history.BeforeJson, StringComparison.Ordinal);
        Assert.Contains("\"dueWorkVersion\":1", history.AfterJson, StringComparison.Ordinal);
        Assert.Contains(generated.Id.ToString("D"), history.AfterJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(generated.CopyableText, history.AfterJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExpiredRequestLinkIsNotAttachedToGeneratedDraft()
    {
        var dueAtUtc = StartUtc.AddDays(31);
        await using var harness = await DueChaserHarness.CreateAsync(dueAtUtc);
        harness.TimeProvider.SetUtcNow(dueAtUtc);

        var result = await harness.Runner.ExecuteAsync(50, default);
        var generated = await harness.ChaserStore.GetLatestAsync(harness.CaseId, default);

        Assert.Equal(1, result.GeneratedCount);
        Assert.NotNull(generated);
        Assert.Null(generated.RequestLinkReference);
        Assert.Null(generated.RequestLinkPurpose);
    }

    [Fact]
    public async Task ExactOccurrenceReplayReturnsOriginalWithoutAdvancingOrDuplicatingHistory()
    {
        await using var harness = await DueChaserHarness.CreateAsync(StartUtc);
        var capturingStore = new CapturingDueChaserStore(harness.ChaserStore);
        var runner = new RunDueChasers(harness.ChaserStore, capturingStore, harness.TimeProvider);

        var first = await runner.ExecuteAsync(1, default);
        var transition = Assert.IsType<DueChaserTransition>(capturingStore.LastTransition);
        var replay = await harness.ChaserStore.TryClaimAndRecordAsync(
            transition with
            {
                Id = Guid.NewGuid(),
                GeneratedAtUtc = transition.GeneratedAtUtc.AddMinutes(1)
            },
            default);

        Assert.Equal(1, first.GeneratedCount);
        Assert.Equal(DueChaserClaimOutcome.Replay, replay.Outcome);
        Assert.Equal(transition.Id, replay.Chaser?.Id);
        Assert.Equal(1L, await harness.CountAsync("CaseDueChasers"));
        Assert.Equal(1L, await harness.CountDueChaserHistoryAsync());
        var workflow = await harness.WorkflowStore.GetAsync(harness.CaseId, default);
        Assert.Equal(1, workflow?.DueWork?.Version);
        Assert.Equal(transition.NextChaseAtUtc, workflow?.DueWork?.NextChaseAtUtc);
    }

    [Fact]
    public async Task ConcurrentSweepsClaimOneOccurrenceAndAdvanceOnce()
    {
        await using var harness = await DueChaserHarness.CreateAsync(StartUtc);
        var synchronizedQueries = new SynchronizedDueChaserQueries(harness.ChaserStore);
        var firstRunner = new RunDueChasers(
            synchronizedQueries,
            new EfCaseDueChaserStore(harness.ContextFactory),
            harness.TimeProvider);
        var secondRunner = new RunDueChasers(
            synchronizedQueries,
            new EfCaseDueChaserStore(harness.ContextFactory),
            harness.TimeProvider);

        var results = await Task.WhenAll(
            firstRunner.ExecuteAsync(1, default),
            secondRunner.ExecuteAsync(1, default));

        Assert.Equal(1, results.Sum(item => item.GeneratedCount));
        Assert.Equal(1, results.Sum(item => item.ReplayCount + item.SupersededCount));
        Assert.Equal(1L, await harness.CountAsync("CaseDueChasers"));
        Assert.Equal(1L, await harness.CountDueChaserHistoryAsync());
        var workflow = await harness.WorkflowStore.GetAsync(harness.CaseId, default);
        Assert.Equal(1, workflow?.DueWork?.Version);
        Assert.Equal(CaseChaseSchedule.NextChaseAt(StartUtc), workflow?.DueWork?.NextChaseAtUtc);
    }

    [Fact]
    public async Task HoldPausesAndReleaseToNotReadyResumesRemainderBeforeReviewStopsIt()
    {
        var firstDueAtUtc = StartUtc.AddDays(3);
        await using var harness = await DueChaserHarness.CreateAsync(firstDueAtUtc);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var holdLease = await harness.WorkflowStore.ClaimAsync(
            new(harness.CaseId, 0, actor, "claim-due-hold"),
            default);
        var held = await new PutCaseOnHold(harness.WorkflowStore).ExecuteAsync(
            new(
                harness.CaseId,
                0,
                actor,
                "hold-due-work",
                "Material is temporarily unavailable",
                holdLease.Token),
            default);

        harness.TimeProvider.Advance(TimeSpan.FromDays(5));
        Assert.Equal(0, (await harness.Runner.ExecuteAsync(50, default)).GeneratedCount);
        Assert.Equal(CaseDueWorkState.Held, held.DueWork?.State);
        Assert.Equal(TimeSpan.FromDays(3), held.DueWork?.RemainingChaseInterval);

        var releaseLease = await harness.WorkflowStore.ClaimAsync(
            new(harness.CaseId, held.Version, actor, "claim-due-release"),
            default);
        var released = await new ReleaseCaseHold(harness.WorkflowStore).ExecuteAsync(
            new ChangeCaseStateRequest(
                harness.CaseId,
                held.Version,
                actor,
                "release-due-work",
                "Material can be requested again",
                releaseLease.Token),
            default);
        var resumedAtUtc = harness.TimeProvider.GetUtcNow().AddDays(3);
        Assert.Equal(CaseLifecycleState.NotReady, released.State);
        Assert.Equal(CaseDueWorkState.Scheduled, released.DueWork?.State);
        Assert.Equal(resumedAtUtc, released.DueWork?.NextChaseAtUtc);

        harness.TimeProvider.SetUtcNow(resumedAtUtc);
        Assert.Equal(1, (await harness.Runner.ExecuteAsync(50, default)).GeneratedCount);
        await using (var context = await harness.ContextFactory.CreateDbContextAsync())
        {
            Assert.Equal(1, await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Cases SET InstructionComplete = {true}, ImagesComplete = {true} WHERE Id = {harness.CaseId}"));
        }

        var current = await harness.WorkflowStore.GetAsync(harness.CaseId, default);
        var reviewLease = await harness.WorkflowStore.ClaimAsync(
            new(harness.CaseId, current!.Version, actor, "claim-review-material"),
            default);
        var review = await new ReturnCaseToReview(harness.WorkflowStore).ExecuteAsync(
            new(
                harness.CaseId,
                current.Version,
                actor,
                "material-arrived-review",
                "The requested images arrived",
                reviewLease.Token,
                new(true, true, "retained-material-occurrence")),
            default);

        Assert.Equal(CaseLifecycleState.Review, review.State);
        Assert.Equal(CaseDueWorkState.Stopped, review.DueWork?.State);
        Assert.Null(review.DueWork?.NextChaseAtUtc);
        harness.TimeProvider.Advance(TimeSpan.FromDays(21));
        Assert.Equal(0, (await harness.Runner.ExecuteAsync(50, default)).GeneratedCount);
        Assert.Equal(1L, await harness.CountAsync("CaseDueChasers"));
    }

    [Fact]
    public async Task TerminalClosureStopsFutureChasers()
    {
        await using var harness = await DueChaserHarness.CreateAsync(StartUtc.AddDays(1));
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var lease = await harness.WorkflowStore.ClaimAsync(
            new(harness.CaseId, 0, actor, "claim-provider-cancel"),
            default);
        var closed = await new CloseCase(harness.WorkflowStore).ExecuteAsync(
            new(
                harness.CaseId,
                0,
                actor,
                "provider-cancel-before-chase",
                "The provider cancelled the instruction",
                lease.Token,
                CaseClosureOutcome.ProviderCancelled),
            default);

        harness.TimeProvider.Advance(TimeSpan.FromDays(14));
        var result = await harness.Runner.ExecuteAsync(50, default);

        Assert.Equal(CaseLifecycleState.ProviderCancelled, closed.State);
        Assert.Equal(CaseDueWorkState.Stopped, closed.DueWork?.State);
        Assert.Equal(0, result.GeneratedCount);
        Assert.Equal(0L, await harness.CountAsync("CaseDueChasers"));
    }

    private sealed class CapturingDueChaserStore(ICaseDueChaserStore inner)
        : ICaseDueChaserStore
    {
        public DueChaserTransition? LastTransition { get; private set; }

        public Task<DueChaserClaimResult> TryClaimAndRecordAsync(
            DueChaserTransition transition,
            CancellationToken cancellationToken)
        {
            LastTransition = transition;
            return inner.TryClaimAndRecordAsync(transition, cancellationToken);
        }
    }

    private sealed class SynchronizedDueChaserQueries(ICaseDueChaserQueries inner)
        : ICaseDueChaserQueries
    {
        private readonly TaskCompletionSource bothQueried =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int queryCount;

        public async Task<IReadOnlyList<DueCaseChaser>> GetDueAsync(
            DateTimeOffset asOfUtc,
            int maximumResults,
            CancellationToken cancellationToken)
        {
            var candidates = await inner.GetDueAsync(asOfUtc, maximumResults, cancellationToken);
            if (Interlocked.Increment(ref queryCount) == 2)
            {
                bothQueried.TrySetResult();
            }
            await bothQueried.Task.WaitAsync(cancellationToken);
            return candidates;
        }

        public Task<GeneratedCaseChaser?> GetLatestAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            inner.GetLatestAsync(caseId, cancellationToken);
    }

    private sealed class DueChaserHarness : IAsyncDisposable
    {
        private readonly LocalDbTestDatabase database;
        private readonly AsyncServiceScope scope;

        private DueChaserHarness(
            LocalDbTestDatabase database,
            AsyncServiceScope scope,
            IDbContextFactory<PegasusDbContext> contextFactory,
            Guid caseId,
            Guid requestLinkId,
            MutableTimeProvider timeProvider)
        {
            this.database = database;
            this.scope = scope;
            ContextFactory = contextFactory;
            CaseId = caseId;
            RequestLinkId = requestLinkId;
            TimeProvider = timeProvider;
            ChaserStore = new EfCaseDueChaserStore(contextFactory);
            WorkflowStore = new EfCaseWorkflowStore(contextFactory, timeProvider);
            Runner = new RunDueChasers(ChaserStore, ChaserStore, timeProvider);
        }

        public IDbContextFactory<PegasusDbContext> ContextFactory { get; }
        public Guid CaseId { get; }
        public Guid RequestLinkId { get; }
        public MutableTimeProvider TimeProvider { get; }
        public EfCaseDueChaserStore ChaserStore { get; }
        public EfCaseWorkflowStore WorkflowStore { get; }
        public RunDueChasers Runner { get; }

        public static async Task<DueChaserHarness> CreateAsync(DateTimeOffset firstDueAtUtc)
        {
            var timeProvider = new MutableTimeProvider(StartUtc);
            var database = await LocalDbTestDatabase.CreateAsync(
                configureServices: services => services.AddSingleton<TimeProvider>(timeProvider));
            var scope = database.CreateAsyncScope();
            try
            {
                var contextFactory = scope.ServiceProvider
                    .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
                var caseId = Guid.NewGuid();
                var requestLinkId = Guid.NewGuid();
                await SeedAsync(contextFactory, caseId, requestLinkId, firstDueAtUtc);
                return new(
                    database,
                    scope,
                    contextFactory,
                    caseId,
                    requestLinkId,
                    timeProvider);
            }
            catch
            {
                await scope.DisposeAsync();
                await database.DisposeAsync();
                throw;
            }
        }

        public Task<long> CountAsync(string tableName)
        {
            var allowed = tableName == "CaseDueChasers"
                ? tableName
                : throw new ArgumentOutOfRangeException(nameof(tableName));
            return database.ScalarAsync<long>($"SELECT COUNT_BIG(*) FROM [{allowed}]");
        }

        public Task<long> CountDueChaserHistoryAsync() => database.ScalarAsync<long>(
            "SELECT COUNT_BIG(*) FROM ActionHistory WHERE AggregateType = 'case_due_work' AND EventKind = 'due_chaser_generated'");

        public async Task<PersistedActionHistory> ReadActionHistoryAsync()
        {
            await using var connection = new SqlConnection(database.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT AggregateType, AggregateId, EventKind, ActorKind, ActorSubjectId, " +
                "OccurredAtUtc, Outcome, CorrelationId, BeforeJson, AfterJson, PolicyVersion " +
                "FROM ActionHistory WHERE AggregateType = 'case_due_work'";
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            var result = new PersistedActionHistory(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetFieldValue<DateTimeOffset>(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.GetString(10));
            Assert.False(await reader.ReadAsync());
            return result;
        }

        public async ValueTask DisposeAsync()
        {
            await scope.DisposeAsync();
            await database.DisposeAsync();
        }

        private static async Task SeedAsync(
            IDbContextFactory<PegasusDbContext> contextFactory,
            Guid caseId,
            Guid requestLinkId,
            DateTimeOffset firstDueAtUtc)
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var principal = await SeededPrincipals.QdosAsync(context);
            var organizationId = principal.OrganizationId;
            var lineageId = principal.SequenceLineageId;
            var principalId = principal.Id;
            var receiptId = Guid.NewGuid();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeReceipts (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, Version, Decision, DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson) VALUES ({receiptId}, {"due-chaser.eml"}, {"message/rfc822"}, {1L}, {new string('a', 64)}, {"manual_upload"}, {$"due-chaser-{caseId:N}"}, {StartUtc}, {StartUtc}, {"due-chaser-test-reader"}, {"1"}, {0L}, {"needs_sorting"}, {"Due chaser fixture"}, {"{\"version\":1,\"data\":[]}"}, {"{\"version\":1,\"data\":[]}"}, {"{\"version\":1,\"data\":[]}"})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, InstructionConfirmedByStaff, ImagesConfirmedByStaff, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2026}, {1}, {"QDOS26001"}, {"inspection"}, {"not_ready"}, {"pending"}, {receiptId}, {false}, {false}, {false}, {false}, {StartUtc}, {0L}, {Guid.NewGuid()})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({caseId}, {nameof(CaseLifecycleState.NotReady)}, {0L}, {Guid.NewGuid()})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO CaseDueWork (CaseId, MissingMaterialReason, State, NextChaseAtUtc, NextChaseAtUtcTicks, Version, ConcurrencyToken) VALUES ({caseId}, {"Vehicle images"}, {nameof(CaseDueWorkState.Scheduled)}, {firstDueAtUtc}, {firstDueAtUtc.UtcDateTime.Ticks}, {0L}, {Guid.NewGuid()})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO RequestUploadLinks (Id, CaseId, TokenDigest, Status, CreatedAtUtc, ExpiresAtUtc, AcceptedFileCount, AcceptedByteCount, LimitsVersion, Version, CreateOperationKey) VALUES ({requestLinkId}, {caseId}, {new string('b', 64)}, {"Active"}, {StartUtc.AddMinutes(-1)}, {StartUtc.AddDays(30)}, {0}, {0L}, {"due-chaser-test-limits"}, {0L}, {$"due-chaser-link-{caseId:N}"})");
        }
    }

    private sealed record PersistedActionHistory(
        string AggregateType,
        string AggregateId,
        string EventKind,
        string ActorKind,
        string ActorSubjectId,
        DateTimeOffset OccurredAtUtc,
        string Outcome,
        string CorrelationId,
        string BeforeJson,
        string AfterJson,
        string PolicyVersion);

    public sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset currentUtc = utcNow;

        public override DateTimeOffset GetUtcNow() => currentUtc;

        public void Advance(TimeSpan interval) => currentUtc += interval;

        public void SetUtcNow(DateTimeOffset value) => currentUtc = value;
    }
}
