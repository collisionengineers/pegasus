using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// CASE-047 B04: the Glass's repair-estimate session store against LocalDB,
/// because the rules it keeps are the database's — a filtered unique index for
/// the provider's one live session per account, a unique operation key for
/// replay, and per-row optimistic concurrency. Nothing here is proven against
/// an in-memory substitute that cannot refuse a duplicate.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class GlassRepairEstimatePersistenceTests
{
    private const string EngineerAccount = "a.engineer";
    private const string OtherAccount = "b.engineer";
    private const string ProtectedState = "protected:v1:mE1kZXNpZ25hdGVkLW9wYXF1ZQ";
    private static readonly string CallbackDigest = new('a', 64);
    private static readonly string OtherCallbackDigest = new('b', 64);

    /// <summary>
    /// The provider allows one live ERE calculation per account, so the
    /// database allows one live session per account — and refuses the second
    /// itself rather than trusting a read.
    /// </summary>
    [Fact]
    public async Task OneLiveSessionPerExternalAccountIsEnforcedByTheDatabase()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.Store.CreateAsync(
            harness.Material(EngineerAccount, GlassRepairEstimateSessionState.Active, "launch-1"),
            CancellationToken.None);

        var conflict = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => harness.Store.CreateAsync(
                harness.Material(EngineerAccount, GlassRepairEstimateSessionState.Prepared, "launch-2"),
                CancellationToken.None));

        Assert.Equal(GlassRepairEstimateSessionConflict.ActiveAccount, conflict.Conflict);
        Assert.Equal(1, await harness.SessionCountAsync());
    }

    /// <summary>
    /// The constraint belongs to the provider's account, not to who typed it
    /// in: a second Pegasus user working a second Glass's account is ordinary
    /// parallel work, and so is the same account under a rotated credential.
    /// </summary>
    [Fact]
    public async Task ASecondAccountForTheSameUserIsAllowed()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.Store.CreateAsync(
            harness.Material(EngineerAccount, GlassRepairEstimateSessionState.Active, "launch-1"),
            CancellationToken.None);

        var second = await harness.Store.CreateAsync(
            harness.Material(OtherAccount, GlassRepairEstimateSessionState.Active, "launch-2"),
            CancellationToken.None);

        Assert.Equal(GlassRepairEstimateSessionState.Active, second.Session.State);
        Assert.Equal(2, await harness.SessionCountAsync());
    }

    /// <summary>
    /// The same account under a different Pegasus user is still one live
    /// session: the account is the provider's, not the user's.
    /// </summary>
    [Fact]
    public async Task TheSameAccountUnderAnotherUserOrCredentialGenerationIsStillOneLiveSession()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.Store.CreateAsync(
            harness.Material(EngineerAccount, GlassRepairEstimateSessionState.Active, "launch-1"),
            CancellationToken.None);

        var conflict = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => harness.Store.CreateAsync(
                harness.Material(
                    EngineerAccount,
                    GlassRepairEstimateSessionState.Active,
                    "launch-2",
                    userId: harness.OtherUserId,
                    credentialGeneration: 7),
                CancellationToken.None));

        Assert.Equal(GlassRepairEstimateSessionConflict.ActiveAccount, conflict.Conflict);
    }

    [Theory]
    [InlineData(GlassRepairEstimateSessionState.Completed)]
    [InlineData(GlassRepairEstimateSessionState.Failed)]
    [InlineData(GlassRepairEstimateSessionState.Expired)]
    [InlineData(GlassRepairEstimateSessionState.Cancelled)]
    public async Task TheAccountIsFreeOnceItsSessionIsNoLongerLive(GlassRepairEstimateSessionState settled)
    {
        await using var harness = await Harness.CreateAsync();
        var first = await harness.Store.CreateAsync(
            harness.Material(EngineerAccount, GlassRepairEstimateSessionState.Active, "launch-1"),
            CancellationToken.None);
        await harness.Store.SaveAsync(
            Transition(first, settled), first.Session.Version, CancellationToken.None);

        var second = await harness.Store.CreateAsync(
            harness.Material(EngineerAccount, GlassRepairEstimateSessionState.Active, "launch-2"),
            CancellationToken.None);

        Assert.Equal(GlassRepairEstimateSessionState.Active, second.Session.State);
        Assert.Null(await harness.ActiveAccountKeyAsync(first.Session.Id));
        Assert.NotNull(await harness.ActiveAccountKeyAsync(second.Session.Id));
    }

    /// <summary>
    /// The account key ignores the casing and padding an operator types and is
    /// a one-way digest of the account alone: the password is not a parameter
    /// of it, and the account itself is not recoverable from the row.
    /// </summary>
    [Fact]
    public async Task TheAccountKeyIgnoresCaseAndWhitespaceAndNeverCarriesTheAccountOrItsSecret()
    {
        await using var harness = await Harness.CreateAsync();
        var created = await harness.Store.CreateAsync(
            harness.Material("  A.Engineer  ", GlassRepairEstimateSessionState.Active, "launch-1"),
            CancellationToken.None);

        var conflict = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => harness.Store.CreateAsync(
                harness.Material(EngineerAccount, GlassRepairEstimateSessionState.Active, "launch-2"),
                CancellationToken.None));

        Assert.Equal(GlassRepairEstimateSessionConflict.ActiveAccount, conflict.Conflict);
        var key = created.Session.NormalizedExternalAccountKey;
        Assert.Equal(64, key.Length);
        Assert.True(key.All(Uri.IsHexDigit));
        Assert.DoesNotContain("engineer", key, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(
            key,
            EfGlassRepairEstimateSessionStore.NormalizeAccountKey(OtherAccount));
    }

    [Fact]
    public async Task RelaunchingTheSameOperationReturnsTheSessionItAlreadyCreated()
    {
        await using var harness = await Harness.CreateAsync();
        var request = harness.Material(EngineerAccount, GlassRepairEstimateSessionState.Prepared, "launch-1");
        var first = await harness.Store.CreateAsync(request, CancellationToken.None);

        var replay = await harness.Store.CreateAsync(
            harness.Material(EngineerAccount, GlassRepairEstimateSessionState.Prepared, "launch-1"),
            CancellationToken.None);

        Assert.Equal(first.Session.Id, replay.Session.Id);
        Assert.Equal(1, await harness.SessionCountAsync());
    }

    [Fact]
    public async Task AnOperationKeyHeldByAnotherCaseIsACollisionAndNotAReplay()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.Store.CreateAsync(
            harness.Material(EngineerAccount, GlassRepairEstimateSessionState.Prepared, "launch-1"),
            CancellationToken.None);

        var conflict = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => harness.Store.CreateAsync(
                harness.Material(
                    OtherAccount,
                    GlassRepairEstimateSessionState.Prepared,
                    "launch-1",
                    caseId: harness.OtherCaseId),
                CancellationToken.None));

        Assert.Equal(GlassRepairEstimateSessionConflict.OperationKey, conflict.Conflict);
        Assert.Equal(1, await harness.SessionCountAsync());
    }

    /// <summary>
    /// The callback that lands twice is acted on once. The second identical
    /// write is refused by the version the first one moved past, and the
    /// session still carries the moment the callback was consumed.
    /// </summary>
    [Fact]
    public async Task AConsumedCallbackIsRecordedAndTheIdenticalWriteDoesNotActTwice()
    {
        await using var harness = await Harness.CreateAsync();
        var session = await harness.Store.CreateAsync(
            harness.Material(EngineerAccount, GlassRepairEstimateSessionState.Active, "launch-1"),
            CancellationToken.None);

        await harness.Store.SaveAsync(
            Transition(session, GlassRepairEstimateSessionState.Importing, ereId: "ere-1"),
            session.Session.Version,
            CancellationToken.None);
        var consumedAt = await harness.CallbackConsumedAtAsync(session.Session.Id);

        var conflict = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => harness.Store.SaveAsync(
                Transition(session, GlassRepairEstimateSessionState.Importing, ereId: "ere-1"),
                session.Session.Version,
                CancellationToken.None));

        Assert.Equal(GlassRepairEstimateSessionConflict.Version, conflict.Conflict);
        Assert.Equal(Harness.StartUtc, consumedAt);

        // The moment is stamped once: a later write does not move it.
        var current = await harness.Store.GetAsync(session.Session.Id, CancellationToken.None);
        Assert.NotNull(current);
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(5));
        await harness.Store.SaveAsync(
            Transition(current, GlassRepairEstimateSessionState.Completed, ereId: "ere-1"),
            current.Session.Version,
            CancellationToken.None);
        Assert.Equal(consumedAt, await harness.CallbackConsumedAtAsync(session.Session.Id));
    }

    /// <summary>
    /// A write carrying a different callback is not this session's callback.
    /// It is refused and changes nothing at all.
    /// </summary>
    [Fact]
    public async Task ADifferentCallbackForTheSameSessionIsRefusedAndChangesNothing()
    {
        await using var harness = await Harness.CreateAsync();
        var session = await harness.Store.CreateAsync(
            harness.Material(EngineerAccount, GlassRepairEstimateSessionState.Active, "launch-1"),
            CancellationToken.None);

        var conflict = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => harness.Store.SaveAsync(
                new GlassRepairEstimateSessionMaterial(
                    session.Session with { State = GlassRepairEstimateSessionState.Completed },
                    session.ProtectedProviderState,
                    OtherCallbackDigest),
                session.Session.Version,
                CancellationToken.None));

        Assert.Equal(GlassRepairEstimateSessionConflict.Callback, conflict.Conflict);
        var unchanged = await harness.Store.GetAsync(session.Session.Id, CancellationToken.None);
        Assert.NotNull(unchanged);
        Assert.Equal(GlassRepairEstimateSessionState.Active, unchanged.Session.State);
        Assert.Equal(session.Session.Version, unchanged.Session.Version);
        Assert.NotNull(await harness.ActiveAccountKeyAsync(session.Session.Id));
        Assert.Null(await harness.CallbackConsumedAtAsync(session.Session.Id));
    }

    /// <summary>
    /// The store never sees the provider material in clear: it takes the
    /// protected string as given and hands back the same one.
    /// </summary>
    [Fact]
    public async Task ProtectedProviderMaterialRoundTripsAsTheSameOpaqueString()
    {
        await using var harness = await Harness.CreateAsync();
        var created = await harness.Store.CreateAsync(
            harness.Material(EngineerAccount, GlassRepairEstimateSessionState.Prepared, "launch-1"),
            CancellationToken.None);

        var read = await harness.Store.GetAsync(created.Session.Id, CancellationToken.None);

        Assert.NotNull(read);
        Assert.Equal(ProtectedState, read.ProtectedProviderState);
        Assert.Equal(ProtectedState, await harness.ProtectedSessionAsync(created.Session.Id));
        Assert.Equal(CallbackDigest, read.CallbackDigest);
    }

    [Fact]
    public async Task AStaleVersionIsRefused()
    {
        await using var harness = await Harness.CreateAsync();
        var session = await harness.Store.CreateAsync(
            harness.Material(EngineerAccount, GlassRepairEstimateSessionState.Prepared, "launch-1"),
            CancellationToken.None);
        await harness.Store.SaveAsync(
            Transition(session, GlassRepairEstimateSessionState.Launching),
            session.Session.Version,
            CancellationToken.None);

        var conflict = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => harness.Store.SaveAsync(
                Transition(session, GlassRepairEstimateSessionState.Active),
                session.Session.Version,
                CancellationToken.None));

        Assert.Equal(GlassRepairEstimateSessionConflict.Version, conflict.Conflict);
        var current = await harness.Store.GetAsync(session.Session.Id, CancellationToken.None);
        Assert.Equal(GlassRepairEstimateSessionState.Launching, current?.Session.State);
    }

    /// <summary>
    /// Every persisted stage survives a restart: each read below goes through
    /// a new store over a new connection, never the instance that wrote it.
    /// </summary>
    [Fact]
    public async Task EveryStageIsReadableAfterARestartAndTheAccountSlotFollowsTheState()
    {
        await using var harness = await Harness.CreateAsync();
        var session = await harness.NewStore().CreateAsync(
            harness.Material(EngineerAccount, GlassRepairEstimateSessionState.Prepared, "launch-1"),
            CancellationToken.None);
        Assert.NotNull(await harness.ActiveAccountKeyAsync(session.Session.Id));

        GlassRepairEstimateSessionState[] stages =
        [
            GlassRepairEstimateSessionState.Launching,
            GlassRepairEstimateSessionState.Active,
            GlassRepairEstimateSessionState.AwaitingImport,
            GlassRepairEstimateSessionState.Importing,
            GlassRepairEstimateSessionState.Completed,
        ];
        var expectedVersion = session.Session.Version;
        foreach (var stage in stages)
        {
            harness.TimeProvider.Advance(TimeSpan.FromMinutes(1));
            var read = await harness.NewStore().GetAsync(session.Session.Id, CancellationToken.None);
            Assert.NotNull(read);
            Assert.Equal(expectedVersion, read.Session.Version);
            Assert.Equal(ProtectedState, read.ProtectedProviderState);

            await harness.NewStore().SaveAsync(
                Transition(read, stage, ereId: "ere-1", vehicleId: "vehicle-1"),
                expectedVersion,
                CancellationToken.None);
            expectedVersion++;

            var persisted = await harness.NewStore().GetAsync(session.Session.Id, CancellationToken.None);
            Assert.NotNull(persisted);
            Assert.Equal(stage, persisted.Session.State);
            Assert.Equal("ere-1", persisted.Session.ProviderEstimateId);
            Assert.Equal("vehicle-1", persisted.Session.ProviderVehicleId);
            Assert.Equal(harness.TimeProvider.GetUtcNow(), await harness.UpdatedAtAsync(session.Session.Id));
            Assert.Equal(
                stage is GlassRepairEstimateSessionState.Launching or GlassRepairEstimateSessionState.Active,
                await harness.ActiveAccountKeyAsync(session.Session.Id) is not null);
        }
    }

    [Fact]
    public async Task AFailureIsRecordedAgainstTheSessionThatFailed()
    {
        await using var harness = await Harness.CreateAsync();
        var session = await harness.Store.CreateAsync(
            harness.Material(EngineerAccount, GlassRepairEstimateSessionState.Active, "launch-1"),
            CancellationToken.None);

        await harness.Store.SaveAsync(
            Transition(session, GlassRepairEstimateSessionState.Failed, failureCode: "provider_rejected_the_session"),
            session.Session.Version,
            CancellationToken.None);

        var failed = await harness.Store.GetAsync(session.Session.Id, CancellationToken.None);
        Assert.Equal("provider_rejected_the_session", failed?.Session.FailureCode);
        Assert.Null(await harness.ActiveAccountKeyAsync(session.Session.Id));
    }

    [Fact]
    public async Task AnUnknownSessionReadsAsNothing()
    {
        await using var harness = await Harness.CreateAsync();

        Assert.Null(await harness.Store.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task SavingASessionThatWasNeverCreatedIsRefused()
    {
        await using var harness = await Harness.CreateAsync();
        var unknown = harness.Material(EngineerAccount, GlassRepairEstimateSessionState.Active, "launch-1");

        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Store.SaveAsync(unknown, 0, CancellationToken.None));
        Assert.Equal(0, await harness.SessionCountAsync());
    }

    [Fact]
    public async Task AWriteThatNamesAnotherCaseIsRefused()
    {
        await using var harness = await Harness.CreateAsync();
        var session = await harness.Store.CreateAsync(
            harness.Material(EngineerAccount, GlassRepairEstimateSessionState.Active, "launch-1"),
            CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Store.SaveAsync(
                new GlassRepairEstimateSessionMaterial(
                    session.Session with
                    {
                        CaseId = harness.OtherCaseId,
                        State = GlassRepairEstimateSessionState.Completed,
                    },
                    session.ProtectedProviderState,
                    session.CallbackDigest),
                session.Session.Version,
                CancellationToken.None));

        var unchanged = await harness.Store.GetAsync(session.Session.Id, CancellationToken.None);
        Assert.Equal(GlassRepairEstimateSessionState.Active, unchanged?.Session.State);
    }

    /// <summary>
    /// The session a caller read back carries the stored key, and saving it
    /// again is the same account rather than a second one.
    /// </summary>
    [Fact]
    public async Task ASessionReadBackSavesUnderTheSameAccount()
    {
        await using var harness = await Harness.CreateAsync();
        var created = await harness.Store.CreateAsync(
            harness.Material(EngineerAccount, GlassRepairEstimateSessionState.Active, "launch-1"),
            CancellationToken.None);
        var read = await harness.Store.GetAsync(created.Session.Id, CancellationToken.None);
        Assert.NotNull(read);

        await harness.Store.SaveAsync(
            Transition(read, GlassRepairEstimateSessionState.Importing),
            read.Session.Version,
            CancellationToken.None);

        var persisted = await harness.Store.GetAsync(created.Session.Id, CancellationToken.None);
        Assert.Equal(GlassRepairEstimateSessionState.Importing, persisted?.Session.State);
        Assert.Equal(read.Session.NormalizedExternalAccountKey, persisted?.Session.NormalizedExternalAccountKey);
    }

    private static GlassRepairEstimateSessionMaterial Transition(
        GlassRepairEstimateSessionMaterial material,
        GlassRepairEstimateSessionState state,
        string? ereId = null,
        string? vehicleId = null,
        string? failureCode = null) =>
        new(
            material.Session with
            {
                State = state,
                ProviderEstimateId = ereId ?? material.Session.ProviderEstimateId,
                ProviderVehicleId = vehicleId ?? material.Session.ProviderVehicleId,
                FailureCode = failureCode,
            },
            material.ProtectedProviderState,
            material.CallbackDigest);

    private sealed class Harness : IAsyncDisposable
    {
        private Harness(
            LocalDbTestDatabase database,
            PooledDbContextFactory<PegasusDbContext> factory,
            CaseDataCompletenessPersistenceTests.MutableTimeProvider timeProvider,
            Guid caseId,
            Guid otherCaseId,
            Guid userId,
            Guid otherUserId)
        {
            Database = database;
            Factory = factory;
            TimeProvider = timeProvider;
            CaseId = caseId;
            OtherCaseId = otherCaseId;
            UserId = userId;
            OtherUserId = otherUserId;
            Store = NewStore();
        }

        public static DateTimeOffset StartUtc { get; } = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

        private LocalDbTestDatabase Database { get; }

        private PooledDbContextFactory<PegasusDbContext> Factory { get; }

        public CaseDataCompletenessPersistenceTests.MutableTimeProvider TimeProvider { get; }

        public Guid CaseId { get; }

        public Guid OtherCaseId { get; }

        public Guid UserId { get; }

        public Guid OtherUserId { get; }

        public EfGlassRepairEstimateSessionStore Store { get; }

        public static async Task<Harness> CreateAsync()
        {
            var database = await LocalDbTestDatabase.CreateAsync();
            try
            {
                var options = new DbContextOptionsBuilder<PegasusDbContext>()
                    .UseSqlServer(database.ConnectionString)
                    .Options;
                var factory = new PooledDbContextFactory<PegasusDbContext>(options);
                var userId = await SeedUserAsync(factory, "a.engineer");
                var otherUserId = await SeedUserAsync(factory, "b.engineer");
                var caseId = await SeedCaseAsync(factory, "GLAS31001", 1, StartUtc);
                var otherCaseId = await SeedCaseAsync(factory, "GLAS31002", 2, StartUtc);
                return new(
                    database,
                    factory,
                    new CaseDataCompletenessPersistenceTests.MutableTimeProvider(StartUtc),
                    caseId,
                    otherCaseId,
                    userId,
                    otherUserId);
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        /// <summary>A store over its own connection, as a restarted process would build.</summary>
        public EfGlassRepairEstimateSessionStore NewStore() => new(Factory, TimeProvider);

        public GlassRepairEstimateSessionMaterial Material(
            string account,
            GlassRepairEstimateSessionState state,
            string operationKey,
            Guid? caseId = null,
            Guid? userId = null,
            long credentialGeneration = 1) =>
            new(
                new GlassRepairEstimateSession(
                    Guid.NewGuid(),
                    caseId ?? CaseId,
                    userId ?? UserId,
                    credentialGeneration,
                    account,
                    state,
                    Version: 0,
                    operationKey,
                    StartUtc,
                    StartUtc.AddHours(2),
                    ProviderVehicleId: null,
                    ProviderEstimateId: null,
                    FailureCode: null),
                ProtectedState,
                CallbackDigest);

        public async Task<int> SessionCountAsync()
        {
            await using var context = await Factory.CreateDbContextAsync();
            return await context.Set<GlassRepairEstimateSessionEntity>().CountAsync();
        }

        public Task<string?> ActiveAccountKeyAsync(Guid sessionId) =>
            ColumnAsync(sessionId, entity => entity.ActiveAccountKey);

        public Task<string> ProtectedSessionAsync(Guid sessionId) =>
            ColumnAsync(sessionId, entity => entity.ProtectedSession);

        public Task<DateTimeOffset?> CallbackConsumedAtAsync(Guid sessionId) =>
            ColumnAsync(sessionId, entity => entity.CallbackConsumedAtUtc);

        public Task<DateTimeOffset?> UpdatedAtAsync(Guid sessionId) =>
            ColumnAsync(sessionId, entity => (DateTimeOffset?)entity.UpdatedAtUtc);

        public async ValueTask DisposeAsync() => await Database.DisposeAsync();

        private async Task<T> ColumnAsync<T>(
            Guid sessionId, System.Linq.Expressions.Expression<Func<GlassRepairEstimateSessionEntity, T>> column)
        {
            await using var context = await Factory.CreateDbContextAsync();
            return await context.Set<GlassRepairEstimateSessionEntity>()
                .Where(entity => entity.Id == sessionId)
                .Select(column)
                .SingleAsync();
        }

        private static async Task<Guid> SeedUserAsync(
            PooledDbContextFactory<PegasusDbContext> factory, string userName)
        {
            await using var context = await factory.CreateDbContextAsync();
            var userId = Guid.NewGuid();
            context.Users.Add(new PegasusIdentityUser
            {
                Id = userId,
                UserName = userName,
                NormalizedUserName = userName.ToUpperInvariant(),
                IsEnabled = true,
                MustChangePassword = false,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            });
            await context.SaveChangesAsync();
            return userId;
        }

        private static async Task<Guid> SeedCaseAsync(
            PooledDbContextFactory<PegasusDbContext> factory,
            string reference,
            int sequence,
            DateTimeOffset occurredAtUtc)
        {
            await using var context = await factory.CreateDbContextAsync();
            var organizationId = Guid.NewGuid();
            var lineageId = Guid.NewGuid();
            var principalId = Guid.NewGuid();
            var receiptId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            context.AddRange(
                new OrganizationEntity { Id = organizationId, Name = $"Glass's session test {reference}", Version = 0 },
                new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = occurredAtUtc },
                new PrincipalEntity
                {
                    Id = principalId,
                    OrganizationId = organizationId,
                    SequenceLineageId = lineageId,
                    Code = reference,
                    IsActive = true,
                    Version = 0
                },
                new IntakeReceiptEntity
                {
                    Id = receiptId,
                    SourceFileName = "glass-origin.pdf",
                    MediaType = "application/pdf",
                    SourceLength = 1,
                    SourceHash = new string('0', 64),
                    SourceChannel = "manual_upload",
                    ExternalReceiptToken = $"glass:{receiptId:N}",
                    ReceivedAtUtc = occurredAtUtc,
                    ProcessedAtUtc = occurredAtUtc,
                    SourceReaderKey = "glass-test",
                    SourceReaderVersion = "1",
                    Version = 0,
                    Decision = "case_created",
                    DecisionReason = "Glass's session test",
                    EvidenceJson = "[]",
                    FieldsJson = "[]",
                    OcrCandidatesJson = "[]"
                },
                new CaseEntity
                {
                    Id = caseId,
                    PrincipalId = principalId,
                    SequenceLineageId = lineageId,
                    Year = 2031,
                    Sequence = sequence,
                    Reference = reference,
                    Type = "Inspection",
                    InitialState = "NotReady",
                    CustodyState = "confirmed",
                    OriginIntakeReceiptId = receiptId,
                    CreatedAtUtc = occurredAtUtc,
                    Version = 1,
                    ConcurrencyToken = Guid.NewGuid()
                },
                new CaseWorkflowEntity
                {
                    CaseId = caseId,
                    State = "Review",
                    Version = 1,
                    ConcurrencyToken = Guid.NewGuid()
                });
            await context.SaveChangesAsync();
            return caseId;
        }

    }
}
