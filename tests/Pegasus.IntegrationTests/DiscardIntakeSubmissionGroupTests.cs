using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>Durable terminal-discard safeguards, exercised against SQL Server.</summary>
[Trait("Category", "SqlServer")]
public sealed class DiscardIntakeSubmissionGroupTests
{
    [Fact]
    public async Task UnlinkedCompletedGroupDiscardsOnceReplaysAndRetainsTheSource()
    {
        await using var harness = await Harness.CreateAsync();
        var result = await harness.DiscardAsync();
        var replay = await harness.DiscardAsync();

        Assert.True(result.Succeeded);
        Assert.False(result.IsReplay);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);

        await using var context = await harness.Database.CreateContextAsync();
        var group = await context.IntakeSubmissionGroups.SingleAsync(item => item.Id == harness.GroupId);
        Assert.NotNull(group.DiscardedAtUtc);
        Assert.Equal(harness.Actor.SubjectId, group.DiscardedByActorSubjectId);
        Assert.Equal(2, group.Version);
        Assert.Single(await context.IntakeSubmissionGroupHistory.ToListAsync());
        Assert.Single(await context.IntakeStagedReceipts.Where(item => item.Id == harness.StagedReceiptId).ToListAsync());
        Assert.Single(await context.IntakeReceipts.Where(item => item.Id == harness.ReceiptId).ToListAsync());
    }

    [Fact]
    public async Task StaleGroupVersionAndChangedMembershipAreRefused()
    {
        await using var harness = await Harness.CreateAsync();
        var stale = await harness.DiscardAsync(expectedGroupVersion: 0);
        Assert.False(stale.Succeeded);

        await using (var context = await harness.Database.CreateContextAsync())
        {
            var group = await context.IntakeSubmissionGroups.SingleAsync(item => item.Id == harness.GroupId);
            var stagedReceiptId = Guid.NewGuid();
            context.IntakeStagedReceipts.Add(new()
            {
                Id = stagedReceiptId, SourceFileName = "later-member.jpg", MediaType = "image/jpeg",
                SourceLength = 3, SourceHash = new string('b', 64), SourceChannel = "manual_upload",
                ExternalReceiptToken = "discard-test:1", ReceivedAtUtc = DateTimeOffset.UtcNow,
                Actor = "staff:test", StorageKey = "manual/later-member", StagedAtUtc = DateTimeOffset.UtcNow
            });
            group.Version++;
            group.ExpectedMemberCount = 2;
            context.IntakeSubmissionGroupMembers.Add(new()
            {
                Id = Guid.NewGuid(), GroupId = group.Id, Ordinal = 1,
                StagedReceiptId = stagedReceiptId, SourceFileName = "later-member.jpg",
                SourceHash = new string('b', 64), AddedAtUtc = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();
        }

        var changed = await harness.DiscardAsync();
        Assert.False(changed.Succeeded);
        await harness.AssertActionableAsync();
    }

    [Fact]
    public async Task ActiveManualAssociationRefusesDiscard()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.AddActiveAssociationAsync();

        var result = await harness.DiscardAsync();

        Assert.False(result.Succeeded);
        await harness.AssertActionableAsync();
    }
    [Theory]
    [InlineData("allocation")]
    [InlineData("image")]
    public async Task AllocationOrImageRegistrationRefusesDiscard(string existingOutcome)
    {
        await using var harness = await Harness.CreateAsync();
        await harness.AddExistingOutcomeAsync(existingOutcome);

        var result = await harness.DiscardAsync();

        Assert.False(result.Succeeded);
        await harness.AssertActionableAsync();
    }

    [Fact]
    public async Task UnauthorizedActorCannotReachTheSqlStore()
    {
        await using var harness = await Harness.CreateAsync();
        var request = harness.Request(ActionActor.SystemWorker("test-worker"));

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new DiscardIntakeSubmissionGroup(harness.Store, TimeProvider.System).ExecuteAsync(request));

        await harness.AssertActionableAsync();
    }

    [Fact]
    public async Task ProcessingRaceHasOneValidOutcomeAndNeverCommitsAnInvalidDiscard()
    {
        await using var harness = await Harness.CreateAsync();
        var processing = Task.Run(async () =>
        {
            await using var context = await harness.Database.CreateContextAsync();
            var work = await context.IntakeWorkItems.SingleAsync(item => item.StagedReceiptId == harness.StagedReceiptId);
            work.State = "processing";
            await context.SaveChangesAsync();
        });
        var discard = harness.DiscardAsync();
        await processing;
        var result = await discard;

        await using var verify = await harness.Database.CreateContextAsync();
        var group = await verify.IntakeSubmissionGroups.SingleAsync(item => item.Id == harness.GroupId);
        if (result.Succeeded)
        {
            Assert.NotNull(group.DiscardedAtUtc);
        }
        else
        {
            Assert.Null(group.DiscardedAtUtc);
        }
    }

    [Fact]
    public async Task TransactionFailureRollsBackTheTerminalDecision()
    {
        var interceptor = new FailDiscardSaveInterceptor();
        await using var harness = await Harness.CreateAsync(interceptor);

        await Assert.ThrowsAsync<DbUpdateException>(() => harness.DiscardAsync());

        await harness.AssertActionableAsync();
    }

    private sealed class Harness : IAsyncDisposable
    {
        private Harness(LocalDbTestDatabase database, IDbContextFactory<PegasusDbContext> factory)
        {
            Database = database;
            Factory = factory;
            Store = new EfIntakeSubmissionGroupStore(factory);
        }

        public LocalDbTestDatabase Database { get; }
        public IDbContextFactory<PegasusDbContext> Factory { get; }
        public IIntakeSubmissionGroupStore Store { get; }
        public Guid GroupId { get; } = Guid.NewGuid();
        public Guid StagedReceiptId { get; } = Guid.NewGuid();
        public Guid ReceiptId { get; } = Guid.NewGuid();
        public Guid OperationId { get; } = Guid.NewGuid();
        public ActionActor Actor { get; } = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

        public static async Task<Harness> CreateAsync(SaveChangesInterceptor? interceptor = null)
        {
            var database = await LocalDbTestDatabase.CreateAsync();
            var options = new DbContextOptionsBuilder<PegasusDbContext>()
                .UseSqlServer(database.ConnectionString);
            if (interceptor is not null)
            {
                options.AddInterceptors(interceptor);
            }
            var harness = new Harness(database, new PooledDbContextFactory<PegasusDbContext>(options.Options));
            await harness.SeedAsync();
            return harness;
        }

        public DiscardIntakeSubmissionGroupRequest Request(ActionActor? actor = null, long? expectedGroupVersion = null) =>
            new(GroupId, expectedGroupVersion ?? 1, new Dictionary<Guid, long> { [ReceiptId] = 4 }, actor ?? Actor, OperationId);

        public Task<DiscardIntakeSubmissionGroupResult> DiscardAsync(long? expectedGroupVersion = null) =>
            new DiscardIntakeSubmissionGroup(Store, TimeProvider.System).ExecuteAsync(Request(expectedGroupVersion: expectedGroupVersion));

        public async Task AddActiveAssociationAsync()
        {
            await using var context = await Database.CreateContextAsync();
            var organizationId = Guid.NewGuid();
            var lineageId = Guid.NewGuid();
            var principalId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            context.AddRange(
                new OrganizationEntity { Id = organizationId, Name = "Discard association test", Version = 0 },
                new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = DateTimeOffset.UtcNow },
                new PrincipalEntity
                {
                    Id = principalId, OrganizationId = organizationId, SequenceLineageId = lineageId,
                    Code = "DISC", IsActive = true, Version = 0
                },
                new CaseEntity
                {
                    Id = caseId, PrincipalId = principalId, SequenceLineageId = lineageId,
                    Year = 2031, Sequence = 1, Reference = "DISC31001", Type = "Audit",
                    InitialState = "NotReady", CustodyState = "Pending", CreatedAtUtc = DateTimeOffset.UtcNow,
                    Version = 1, ConcurrencyToken = Guid.NewGuid()
                },
                new IntakeManualAssociationEntity
                {
                    IntakeReceiptId = ReceiptId, CaseId = caseId, IsActive = true, Version = 1,
                    LinkedAtUtc = DateTimeOffset.UtcNow, ActorKind = "Staff", ActorSubjectId = Actor.SubjectId,
                    ActorRolesJson = "[\"Engineer\"]", LastOperationKey = $"link:{Guid.NewGuid():N}"
                });
            await context.SaveChangesAsync();
        }
        public async Task AddExistingOutcomeAsync(string kind)
        {
            await using var context = await Database.CreateContextAsync();
            if (kind == "allocation")
            {
                context.IntakeAllocationAttempts.Add(new()
                {
                    Id = Guid.NewGuid(), IntakeReceiptId = ReceiptId, AttemptNumber = 1,
                    Kind = "automatic", Status = "succeeded", ExpectedReceiptVersion = 4,
                    PrincipalCode = "TEST", ActorKind = "Staff", ActorSubjectId = Actor.SubjectId,
                    ActorRolesJson = "[\"Engineer\"]", OperationKey = $"allocation:{Guid.NewGuid():N}",
                    CommandHash = new string('a', 64), Reason = "seed", StartedAtUtc = DateTimeOffset.UtcNow,
                    CompletedAtUtc = DateTimeOffset.UtcNow
                });
            }
            else
            {
                context.ImageIntakes.Add(new()
                {
                    Id = Guid.NewGuid(), OriginReceiptId = ReceiptId, SourceChannel = "manual_upload",
                    ExternalReceiptToken = "discard-test", SourceHash = new string('a', 64),
                    EvaluationRevisionId = Guid.NewGuid(), SubmissionGroupId = GroupId,
                    NormalizedVehicleRegistration = "AB12CDE", ImageIntakeReference = "IMG-TEST-1",
                    CreatedAtUtc = DateTimeOffset.UtcNow, CreatedByActorKind = "Staff",
                    CreatedByActorSubjectId = Actor.SubjectId, Reason = "seed",
                    CreationOperationKey = $"image:{Guid.NewGuid():N}", RequestFingerprint = new string('a', 64),
                    LifecycleState = "registered", LifecycleVersion = 1
                });
            }
            await context.SaveChangesAsync();
        }

        public async Task AssertActionableAsync()
        {
            await using var context = await Database.CreateContextAsync();
            var group = await context.IntakeSubmissionGroups.SingleAsync(item => item.Id == GroupId);
            Assert.Null(group.DiscardedAtUtc);
            Assert.Empty(await context.IntakeSubmissionGroupHistory.ToListAsync());
        }

        private async Task SeedAsync()
        {
            await using var context = await Database.CreateContextAsync();
            context.AddRange(
                new IntakeStagedReceiptEntity
                {
                    Id = StagedReceiptId, SourceFileName = "discard-test.jpg", MediaType = "image/jpeg",
                    SourceLength = 3, SourceHash = new string('a', 64), SourceChannel = "manual_upload",
                    ExternalReceiptToken = "discard-test", ReceivedAtUtc = DateTimeOffset.UtcNow,
                    Actor = "staff:test", StorageKey = "manual/discard-test", StagedAtUtc = DateTimeOffset.UtcNow
                },
                new IntakeReceiptEntity
                {
                    Id = ReceiptId, SourceFileName = "discard-test.jpg", MediaType = "image/jpeg", SourceLength = 3,
                    SourceHash = new string('a', 64), SourceChannel = "manual_upload", ExternalReceiptToken = "discard-test",
                    ReceivedAtUtc = DateTimeOffset.UtcNow, ProcessedAtUtc = DateTimeOffset.UtcNow,
                    SourceReaderKey = "test", SourceReaderVersion = "1", Version = 4,
                    Decision = "needs_sorting", DecisionReason = "test", EvidenceJson = "[]", FieldsJson = "[]",
                    OcrCandidatesJson = "[]"
                },
                new IntakeSubmissionGroupEntity
                {
                    Id = GroupId, SourceChannel = "manual_upload", SubmissionToken = "discard-test",
                    ExpectedMemberCount = 1, Actor = "staff:test", ReceivedAtUtc = DateTimeOffset.UtcNow, Version = 1
                },
                new IntakeSubmissionGroupMemberEntity
                {
                    Id = Guid.NewGuid(), GroupId = GroupId, Ordinal = 0, StagedReceiptId = StagedReceiptId,
                    SourceFileName = "discard-test.jpg", SourceHash = new string('a', 64), AddedAtUtc = DateTimeOffset.UtcNow
                },
                new IntakeWorkItemEntity
                {
                    Id = Guid.NewGuid(), StagedReceiptId = StagedReceiptId, OperationKey = "manual-upload:discard-test:0",
                    State = "completed", AttemptCount = 1, DueAtUtc = DateTimeOffset.UtcNow,
                    ProcessedReceiptId = ReceiptId, CompletedAtUtc = DateTimeOffset.UtcNow
                });
            await context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync() => await Database.DisposeAsync();
    }

    private sealed class FailDiscardSaveInterceptor : SaveChangesInterceptor
    {
        private int fail = 1;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<IntakeSubmissionGroupHistoryEntity>()
                    .Any(entry => entry.State == EntityState.Added) == true
                && Interlocked.Exchange(ref fail, 0) == 1)
            {
                throw new DbUpdateException("Injected discard transaction failure.");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
