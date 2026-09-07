using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
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
    private const string ProtectedState = "protected:v1:mE1kZXNpZ25hdGVkLW9wYXF1ZQ";

    /// <summary>
    /// Opaque to the store, so it is stored and read as the exact characters
    /// given — nothing here asks the store to understand it.
    /// </summary>
    private const string ResultArtifacts =
        """{"ere":"ere-1","documents":[{"kind":"pdf","href":"glass://ere-1/report.pdf"}]}""";

    private const string OtherResultArtifacts =
        """{"ere":"ere-1","documents":[{"kind":"xml","href":"glass://ere-1/export.xml"}]}""";

    /// <summary>Every outcome of a concurrent launch, as <see cref="Describe"/> spells it.</summary>
    private static readonly string[] BothLaunchesCreated = ["created", "created"];

    private static readonly string[] OneLaunchCreatedAndOneRefused = ["conflict:ActiveAccount", "created"];

    private static readonly string CallbackDigest = new('a', 64);
    private static readonly string OtherCallbackDigest = new('b', 64);

    /// <summary>
    /// The canonical account key a launch carries. It is the lower-hex SHA-256
    /// of the provider and the normalized username, mirroring Stream A's
    /// <c>EfPerUserExternalCredentialStore.NormalizeAccountKey</c>: a fixture
    /// that mints a key of the real shape, not a second owner of the rule. The
    /// store under test neither mints nor re-derives it — it is handed the
    /// canonical key and must keep it exactly.
    /// </summary>
    private static string AccountKey(string username) =>
        Convert.ToHexStringLower(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    $"{ExternalCredentialProvider.GlassRepairEstimate}\n{username.Trim().ToLowerInvariant()}")));

    private static readonly string EngineerAccountKey = AccountKey("a.engineer");

    private static readonly string OtherAccountKey = AccountKey("b.engineer");

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
            harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Active, "launch-1"),
            CancellationToken.None);

        var conflict = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => harness.Store.CreateAsync(
                harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Prepared, "launch-2"),
                CancellationToken.None));

        Assert.Equal(GlassRepairEstimateSessionConflict.ActiveAccount, conflict.Conflict);
        Assert.Equal(1, await harness.SessionCountAsync());
    }

    /// <summary>
    /// The constraint belongs to the provider's account, not to who typed it
    /// in: a second Glass's account is a second calculation and so is ordinary
    /// parallel work. The same account under a rotated credential is not, as
    /// the next test proves.
    /// </summary>
    [Fact]
    public async Task ASecondAccountForTheSameUserIsAllowed()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.Store.CreateAsync(
            harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Active, "launch-1"),
            CancellationToken.None);

        var second = await harness.Store.CreateAsync(
            harness.Material(OtherAccountKey, GlassRepairEstimateSessionState.Active, "launch-2"),
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
            harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Active, "launch-1"),
            CancellationToken.None);

        var conflict = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => harness.Store.CreateAsync(
                harness.Material(
                    EngineerAccountKey,
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
            harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Active, "launch-1"),
            CancellationToken.None);
        await harness.Store.SaveAsync(
            Transition(first, settled), first.Session.Version, CancellationToken.None);

        var second = await harness.Store.CreateAsync(
            harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Active, "launch-2"),
            CancellationToken.None);

        Assert.Equal(GlassRepairEstimateSessionState.Active, second.Session.State);
        Assert.Null(await harness.ActiveAccountKeyAsync(first.Session.Id));
        Assert.NotNull(await harness.ActiveAccountKeyAsync(second.Session.Id));
    }

    /// <summary>
    /// The canonical account key is minted once, by the credential store that
    /// owns the external account, and this store is not a second owner of it:
    /// the key the launch carries is the key the row holds, the key the
    /// account slot holds and the key a read hands back — byte for byte, with
    /// no salting, re-hashing or case folding of its own.
    /// </summary>
    [Fact]
    public async Task TheStoreNeverTransformsTheAccountKey()
    {
        await using var harness = await Harness.CreateAsync();
        var created = await harness.Store.CreateAsync(
            harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Active, "launch-1"),
            CancellationToken.None);

        var read = await harness.Store.GetAsync(created.Session.Id, CancellationToken.None);

        Assert.NotNull(read);
        Assert.Equal(EngineerAccountKey, created.Session.NormalizedExternalAccountKey);
        Assert.Equal(EngineerAccountKey, read.Session.NormalizedExternalAccountKey);
        Assert.Equal(EngineerAccountKey, await harness.ActiveAccountKeyAsync(created.Session.Id));
        // The key it was handed is already one-way: it carries neither the
        // account an operator typed nor anything derived from the secret.
        Assert.Equal(64, EngineerAccountKey.Length);
        Assert.True(EngineerAccountKey.All(Uri.IsHexDigit));
        Assert.DoesNotContain("engineer", EngineerAccountKey, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(EngineerAccountKey, OtherAccountKey);
    }

    [Fact]
    public async Task RelaunchingTheSameOperationReturnsTheSessionItAlreadyCreated()
    {
        await using var harness = await Harness.CreateAsync();
        var request = harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Prepared, "launch-1");
        var first = await harness.Store.CreateAsync(request, CancellationToken.None);

        var replay = await harness.Store.CreateAsync(
            harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Prepared, "launch-1"),
            CancellationToken.None);

        Assert.Equal(first.Session.Id, replay.Session.Id);
        Assert.Equal(1, await harness.SessionCountAsync());
    }

    [Fact]
    public async Task AnOperationKeyHeldByAnotherCaseIsACollisionAndNotAReplay()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.Store.CreateAsync(
            harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Prepared, "launch-1"),
            CancellationToken.None);

        var conflict = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => harness.Store.CreateAsync(
                harness.Material(
                    OtherAccountKey,
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
            harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Active, "launch-1"),
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
            harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Active, "launch-1"),
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
            harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Prepared, "launch-1"),
            CancellationToken.None);

        var read = await harness.Store.GetAsync(created.Session.Id, CancellationToken.None);

        Assert.NotNull(read);
        Assert.Equal(ProtectedState, read.ProtectedProviderState);
        Assert.Equal(ProtectedState, await harness.ProtectedSessionAsync(created.Session.Id));
        Assert.Equal(CallbackDigest, read.CallbackDigest);
    }

    /// <summary>
    /// The provider's results are durable and opaque: the store writes the
    /// exact characters it was given, hands the same ones back with the
    /// session, and never parses them.
    /// </summary>
    [Fact]
    public async Task TheProvidersResultsRoundTripAsTheSameOpaqueJson()
    {
        await using var harness = await Harness.CreateAsync();
        var created = await harness.Store.CreateAsync(
            harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Active, "launch-1"),
            CancellationToken.None);
        Assert.Null(created.ResultArtifactsJson);

        await harness.Store.SaveAsync(
            Transition(
                created, GlassRepairEstimateSessionState.Importing, ereId: "ere-1", results: ResultArtifacts),
            created.Session.Version,
            CancellationToken.None);

        var read = await harness.NewStore().GetAsync(created.Session.Id, CancellationToken.None);
        Assert.NotNull(read);
        Assert.Equal(ResultArtifacts, read.ResultArtifactsJson);
        Assert.Equal(ResultArtifacts, await harness.ResultArtifactsJsonAsync(created.Session.Id));

        // A caller that reads and saves keeps them without restating them.
        await harness.NewStore().SaveAsync(
            Transition(read, GlassRepairEstimateSessionState.Completed),
            read.Session.Version,
            CancellationToken.None);
        var completed = await harness.NewStore().GetAsync(created.Session.Id, CancellationToken.None);
        Assert.Equal(ResultArtifacts, completed?.ResultArtifactsJson);

        // And replacing them replaces them whole.
        await harness.NewStore().SaveAsync(
            Transition(completed!, GlassRepairEstimateSessionState.Completed, results: OtherResultArtifacts),
            completed!.Session.Version,
            CancellationToken.None);
        Assert.Equal(OtherResultArtifacts, await harness.ResultArtifactsJsonAsync(created.Session.Id));
    }

    /// <summary>
    /// The material is the row's whole mutable state, so a save carrying no
    /// results writes none: null is a value here, never "leave what is there".
    /// It is the rule the failure code already followed.
    /// </summary>
    [Fact]
    public async Task ASaveCarryingNoResultsWritesNullRatherThanKeepingTheOldOnes()
    {
        await using var harness = await Harness.CreateAsync();
        var created = await harness.Store.CreateAsync(
            harness.Material(
                EngineerAccountKey,
                GlassRepairEstimateSessionState.Active,
                "launch-1",
                results: ResultArtifacts),
            CancellationToken.None);
        Assert.Equal(ResultArtifacts, await harness.ResultArtifactsJsonAsync(created.Session.Id));

        await harness.Store.SaveAsync(
            new GlassRepairEstimateSessionMaterial(
                created.Session with
                {
                    State = GlassRepairEstimateSessionState.Failed,
                    FailureCode = "provider_returned_nothing",
                },
                created.ProtectedProviderState,
                created.CallbackDigest,
                resultArtifactsJson: null),
            created.Session.Version,
            CancellationToken.None);

        var cleared = await harness.NewStore().GetAsync(created.Session.Id, CancellationToken.None);
        Assert.NotNull(cleared);
        Assert.Null(cleared.ResultArtifactsJson);
        Assert.Null(await harness.ResultArtifactsJsonAsync(created.Session.Id));
        Assert.Equal("provider_returned_nothing", cleared.Session.FailureCode);
    }

    /// <summary>
    /// A replay is the same launch relaunching, so it legitimately carries
    /// fresh cookies: the opaque provider state is not part of what makes a
    /// replay the same launch, and the recorded material is what comes back.
    /// </summary>
    [Fact]
    public async Task AReplayCarryingFreshProviderStateIsStillTheSameLaunch()
    {
        await using var harness = await Harness.CreateAsync();
        var first = await harness.Store.CreateAsync(
            harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Prepared, "launch-1"),
            CancellationToken.None);

        var replay = await harness.Store.CreateAsync(
            harness.Material(
                EngineerAccountKey,
                GlassRepairEstimateSessionState.Prepared,
                "launch-1",
                protectedState: "protected:v1:ZnJlc2gtY29va2llcw"),
            CancellationToken.None);

        Assert.Equal(first.Session.Id, replay.Session.Id);
        Assert.Equal(ProtectedState, replay.ProtectedProviderState);
        Assert.Equal(1, await harness.SessionCountAsync());
    }

    /// <summary>
    /// A rotated credential is a different launch even for the same Case and
    /// user, so the operation key it reuses is a collision. Handing back the
    /// earlier session would hand over provider material the new generation
    /// never established.
    /// </summary>
    [Fact]
    public async Task AnotherCredentialGenerationUnderTheSameOperationKeyIsACollision()
    {
        await using var harness = await Harness.CreateAsync();
        var first = await harness.Store.CreateAsync(
            harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Prepared, "launch-1"),
            CancellationToken.None);

        var conflict = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => harness.Store.CreateAsync(
                harness.Material(
                    EngineerAccountKey,
                    GlassRepairEstimateSessionState.Prepared,
                    "launch-1",
                    credentialGeneration: 2),
                CancellationToken.None));

        Assert.Equal(GlassRepairEstimateSessionConflict.OperationKey, conflict.Conflict);
        Assert.Equal(first.Session.Id, conflict.SessionId);
        Assert.Equal(1, await harness.SessionCountAsync());
    }

    /// <summary>
    /// The account is part of the launch's identity too: the same Case, user
    /// and generation against a different Glass's account under one operation
    /// key is a collision, not a replay.
    /// </summary>
    [Fact]
    public async Task AnotherExternalAccountUnderTheSameOperationKeyIsACollision()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.Store.CreateAsync(
            harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Prepared, "launch-1"),
            CancellationToken.None);

        var conflict = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => harness.Store.CreateAsync(
                harness.Material(OtherAccountKey, GlassRepairEstimateSessionState.Prepared, "launch-1"),
                CancellationToken.None));

        Assert.Equal(GlassRepairEstimateSessionConflict.OperationKey, conflict.Conflict);
        Assert.Equal(1, await harness.SessionCountAsync());
    }

    /// <summary>
    /// An uncertain provider outcome may still be holding that account's one
    /// calculation open, so the account stays occupied and a second launch for
    /// it is refused rather than raced against the provider.
    /// </summary>
    [Fact]
    public async Task AnUncertainOutcomeKeepsTheAccountOccupied()
    {
        await using var harness = await Harness.CreateAsync();
        var first = await harness.Store.CreateAsync(
            harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Active, "launch-1"),
            CancellationToken.None);
        await harness.Store.SaveAsync(
            Transition(first, GlassRepairEstimateSessionState.Unknown),
            first.Session.Version,
            CancellationToken.None);

        var conflict = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => harness.Store.CreateAsync(
                harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Prepared, "launch-2"),
                CancellationToken.None));

        Assert.Equal(GlassRepairEstimateSessionConflict.ActiveAccount, conflict.Conflict);
        Assert.NotNull(await harness.ActiveAccountKeyAsync(first.Session.Id));
        // An uncertain outcome is not an answer, so the callback is still
        // unconsumed and the session can still be resolved by one.
        Assert.Null(await harness.CallbackConsumedAtAsync(first.Session.Id));
        Assert.Equal(1, await harness.SessionCountAsync());
    }

    /// <summary>
    /// Pegasus importing a result says nothing about whether the operator's
    /// interactive provider session closed, and the shared contract carries no
    /// statement that it did, so the account stays occupied through the import.
    /// </summary>
    [Theory]
    [InlineData(GlassRepairEstimateSessionState.AwaitingImport)]
    [InlineData(GlassRepairEstimateSessionState.Importing)]
    public async Task ImportingKeepsTheAccountOccupied(GlassRepairEstimateSessionState importing)
    {
        await using var harness = await Harness.CreateAsync();
        var first = await harness.Store.CreateAsync(
            harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Active, "launch-1"),
            CancellationToken.None);
        await harness.Store.SaveAsync(
            Transition(first, importing, ereId: "ere-1"),
            first.Session.Version,
            CancellationToken.None);

        var conflict = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => harness.Store.CreateAsync(
                harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Prepared, "launch-2"),
                CancellationToken.None));

        Assert.Equal(GlassRepairEstimateSessionConflict.ActiveAccount, conflict.Conflict);
        Assert.NotNull(await harness.ActiveAccountKeyAsync(first.Session.Id));
        // The import is the provider's answer, so the callback is consumed by
        // it even though the account is not yet free.
        Assert.Equal(Harness.StartUtc, await harness.CallbackConsumedAtAsync(first.Session.Id));
    }

    /// <summary>
    /// The account is released once the session settles, and the very next
    /// launch for it succeeds.
    /// </summary>
    [Fact]
    public async Task ACompletedSessionReleasesTheAccountForTheNextLaunch()
    {
        await using var harness = await Harness.CreateAsync();
        var first = await harness.Store.CreateAsync(
            harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Active, "launch-1"),
            CancellationToken.None);
        await harness.Store.SaveAsync(
            Transition(first, GlassRepairEstimateSessionState.Completed, results: ResultArtifacts),
            first.Session.Version,
            CancellationToken.None);

        var second = await harness.Store.CreateAsync(
            harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Prepared, "launch-2"),
            CancellationToken.None);

        Assert.Equal(GlassRepairEstimateSessionState.Prepared, second.Session.State);
        Assert.Null(await harness.ActiveAccountKeyAsync(first.Session.Id));
        Assert.NotNull(await harness.ActiveAccountKeyAsync(second.Session.Id));
        // The settled session keeps its results and its consumed callback.
        Assert.Equal(ResultArtifacts, await harness.ResultArtifactsJsonAsync(first.Session.Id));
        Assert.Equal(Harness.StartUtc, await harness.CallbackConsumedAtAsync(first.Session.Id));
    }

    /// <summary>
    /// Two Glass's accounts are two calculations, so two Pegasus processes
    /// launching them at the same instant both get a session. Each runs on its
    /// own context factory, connection and serializable transaction.
    /// </summary>
    [Fact]
    public async Task TwoAccountsLaunchingAtOnceBothGetASession()
    {
        await using var harness = await Harness.CreateAsync();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var launches = LaunchTogether(
            gate.Task,
            (harness.IndependentStore(),
                harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Active, "launch-1")),
            (harness.IndependentStore(),
                harness.Material(OtherAccountKey, GlassRepairEstimateSessionState.Active, "launch-2")));

        gate.SetResult();
        var outcomes = await Task.WhenAll(launches);

        Assert.Equal(
            BothLaunchesCreated,
            outcomes.Select(outcome => Describe(outcome.Error)).ToArray());
        Assert.Equal(2, outcomes.Select(outcome => outcome.Created!.Session.Id).Distinct().Count());
        Assert.Equal(2, await harness.SessionCountAsync());
    }

    /// <summary>
    /// One account is one live calculation even when two processes ask at the
    /// same instant: exactly one session exists afterwards and the loser is
    /// told the account is taken. The database decides, so the losing side is
    /// never a lost write.
    /// </summary>
    [Fact]
    public async Task TheSameAccountLaunchingAtOnceYieldsOneSessionAndOneConflict()
    {
        await using var harness = await Harness.CreateAsync();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var launches = LaunchTogether(
            gate.Task,
            (harness.IndependentStore(),
                harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Active, "launch-1")),
            (harness.IndependentStore(),
                harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Active, "launch-2")));

        gate.SetResult();
        var outcomes = await Task.WhenAll(launches);

        // Order is the race's, so compare the set. Describe spells out any
        // other outcome — a deadlock arrives as its own SQL error.
        Assert.Equal(
            OneLaunchCreatedAndOneRefused,
            outcomes.Select(outcome => Describe(outcome.Error)).Order().ToArray());
        Assert.Equal(1, await harness.SessionCountAsync());
        var created = Assert.Single(outcomes, outcome => outcome.Created is not null);
        Assert.NotNull(await harness.ActiveAccountKeyAsync(created.Created!.Session.Id));
    }

    /// <summary>
    /// The same operation launched twice at the same instant is one launch:
    /// both callers get the same session back rather than one of them being
    /// told the account it just took is taken.
    /// </summary>
    [Fact]
    public async Task TheSameOperationLaunchingAtOnceIsOneSessionForBothCallers()
    {
        await using var harness = await Harness.CreateAsync();
        var replayed = harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Active, "launch-1");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var launches = LaunchTogether(
            gate.Task,
            (harness.IndependentStore(), replayed),
            (harness.IndependentStore(),
                harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Active, "launch-1")));

        gate.SetResult();
        var outcomes = await Task.WhenAll(launches);

        Assert.Equal(
            BothLaunchesCreated,
            outcomes.Select(outcome => Describe(outcome.Error)).ToArray());
        Assert.Single(outcomes.Select(outcome => outcome.Created!.Session.Id).Distinct());
        Assert.Equal(1, await harness.SessionCountAsync());
    }

    [Fact]
    public async Task AStaleVersionIsRefused()
    {
        await using var harness = await Harness.CreateAsync();
        var session = await harness.Store.CreateAsync(
            harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Prepared, "launch-1"),
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
            harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Prepared, "launch-1"),
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
        DateTimeOffset? consumedAt = null;
        foreach (var stage in stages)
        {
            harness.TimeProvider.Advance(TimeSpan.FromMinutes(1));
            var read = await harness.NewStore().GetAsync(session.Session.Id, CancellationToken.None);
            Assert.NotNull(read);
            Assert.Equal(expectedVersion, read.Session.Version);
            Assert.Equal(ProtectedState, read.ProtectedProviderState);
            Assert.Equal(consumedAt, read.Session.CallbackConsumedAtUtc);

            await harness.NewStore().SaveAsync(
                Transition(read, stage, ereId: "ere-1", vehicleId: "vehicle-1", results: ResultArtifacts),
                expectedVersion,
                CancellationToken.None);
            expectedVersion++;

            // The first stage that is no longer waiting on the provider stamps
            // the callback, and every later stage carries that same moment.
            consumedAt ??= stage is GlassRepairEstimateSessionState.AwaitingImport
                or GlassRepairEstimateSessionState.Importing or GlassRepairEstimateSessionState.Completed
                ? harness.TimeProvider.GetUtcNow()
                : null;

            var persisted = await harness.NewStore().GetAsync(session.Session.Id, CancellationToken.None);
            Assert.NotNull(persisted);
            Assert.Equal(stage, persisted.Session.State);
            Assert.Equal("ere-1", persisted.Session.ProviderEstimateId);
            Assert.Equal("vehicle-1", persisted.Session.ProviderVehicleId);
            Assert.Equal(ResultArtifacts, persisted.ResultArtifactsJson);
            Assert.Equal(ResultArtifacts, await harness.ResultArtifactsJsonAsync(session.Session.Id));
            Assert.Equal(harness.TimeProvider.GetUtcNow(), await harness.UpdatedAtAsync(session.Session.Id));
            Assert.Equal(consumedAt, persisted.Session.CallbackConsumedAtUtc);
            Assert.Equal(consumedAt, await harness.CallbackConsumedAtAsync(session.Session.Id));

            // The account is held until the session settles, so only Completed
            // releases it here.
            Assert.Equal(
                stage is not GlassRepairEstimateSessionState.Completed,
                await harness.ActiveAccountKeyAsync(session.Session.Id) is not null);
        }

        Assert.NotNull(consumedAt);
    }

    [Fact]
    public async Task AFailureIsRecordedAgainstTheSessionThatFailed()
    {
        await using var harness = await Harness.CreateAsync();
        var session = await harness.Store.CreateAsync(
            harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Active, "launch-1"),
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
        var unknown = harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Active, "launch-1");

        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Store.SaveAsync(unknown, 0, CancellationToken.None));
        Assert.Equal(0, await harness.SessionCountAsync());
    }

    [Fact]
    public async Task AWriteThatNamesAnotherCaseIsRefused()
    {
        await using var harness = await Harness.CreateAsync();
        var session = await harness.Store.CreateAsync(
            harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Active, "launch-1"),
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
            harness.Material(EngineerAccountKey, GlassRepairEstimateSessionState.Active, "launch-1"),
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

    /// <summary>
    /// The read-change-save a caller performs: everything it does not mean to
    /// change is carried across from the material the store handed it, results
    /// included.
    /// </summary>
    private static GlassRepairEstimateSessionMaterial Transition(
        GlassRepairEstimateSessionMaterial material,
        GlassRepairEstimateSessionState state,
        string? ereId = null,
        string? vehicleId = null,
        string? failureCode = null,
        string? results = null) =>
        new(
            material.Session with
            {
                State = state,
                ProviderEstimateId = ereId ?? material.Session.ProviderEstimateId,
                ProviderVehicleId = vehicleId ?? material.Session.ProviderVehicleId,
                FailureCode = failureCode,
            },
            material.ProtectedProviderState,
            material.CallbackDigest,
            results ?? material.ResultArtifactsJson);

    /// <summary>
    /// What actually came back from a concurrent launch, spelled out, so a
    /// Serializable range-lock deadlock is read as its own SQL error rather
    /// than as some unnamed wrong exception.
    /// </summary>
    private static string Describe(Exception? error) => error switch
    {
        null => "created",
        GlassRepairEstimateSessionConflictException conflict => $"conflict:{conflict.Conflict}",
        _ => error.GetBaseException() is SqlException sql
            ? $"sql:{sql.Number}:{sql.Message}"
            : $"{error.GetType().Name}:{error.GetBaseException().Message}",
    };

    /// <summary>
    /// Both launches wait on one gate, so they are inside the store's
    /// serializable transactions at the same time rather than merely started
    /// from the same statement.
    /// </summary>
    private static Task<LaunchOutcome>[] LaunchTogether(
        Task gate,
        params (EfGlassRepairEstimateSessionStore Store, GlassRepairEstimateSessionMaterial Material)[] launches) =>
        [.. launches.Select(launch => Task.Run(async () =>
        {
            await gate;
            try
            {
                return new LaunchOutcome(
                    await launch.Store.CreateAsync(launch.Material, CancellationToken.None), null);
            }
            catch (Exception exception)
            {
                // Kept, not swallowed: every outcome below is asserted on, and
                // an unexpected one is reported with its SQL error verbatim.
                return new LaunchOutcome(null, exception);
            }
        }))];

    private sealed record LaunchOutcome(
        GlassRepairEstimateSessionMaterial? Created, Exception? Error);

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

        /// <summary>
        /// A store on its own context factory and therefore its own connection
        /// pool, as a second Pegasus process launching at the same moment has.
        /// </summary>
        public EfGlassRepairEstimateSessionStore IndependentStore() =>
            new(
                new PooledDbContextFactory<PegasusDbContext>(
                    new DbContextOptionsBuilder<PegasusDbContext>()
                        .UseSqlServer(Database.ConnectionString)
                        .Options),
                TimeProvider);

        public GlassRepairEstimateSessionMaterial Material(
            string accountKey,
            GlassRepairEstimateSessionState state,
            string operationKey,
            Guid? caseId = null,
            Guid? userId = null,
            long credentialGeneration = 1,
            string? protectedState = null,
            string? results = null) =>
            new(
                new GlassRepairEstimateSession(
                    Guid.NewGuid(),
                    caseId ?? CaseId,
                    userId ?? UserId,
                    credentialGeneration,
                    accountKey,
                    state,
                    Version: 0,
                    operationKey,
                    StartUtc,
                    StartUtc.AddHours(2),
                    ProviderVehicleId: null,
                    ProviderEstimateId: null,
                    FailureCode: null),
                protectedState ?? ProtectedState,
                CallbackDigest,
                results);

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

        public Task<string?> ResultArtifactsJsonAsync(Guid sessionId) =>
            ColumnAsync(sessionId, entity => entity.ResultArtifactsJson);

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
