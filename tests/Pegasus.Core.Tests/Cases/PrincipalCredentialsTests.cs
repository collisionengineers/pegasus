using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Cases;

public sealed class PrincipalCredentialsTests
{
    private static readonly ActionActor Administrator =
        ActionActor.Staff(Guid.Parse("63f98d69-5368-48b8-b25d-a61ec91f6905"), [StaffRole.Administrator]);
    private static readonly ActionActor Engineer =
        ActionActor.Staff(Guid.Parse("7a1b2c3d-0000-4000-8000-000000000001"), [StaffRole.Engineer]);
    private static readonly DateTimeOffset Now = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private static readonly Guid PrincipalId = Guid.Parse("0f149cac-e1d4-4a57-925f-7c35d33d7f5b");
    private static readonly Principal ActivePrincipal =
        new(PrincipalId, Guid.NewGuid(), "QDOS", Guid.NewGuid(), null, null, true, 0);

    private static PrincipalCredentialCommandRequest Request(long expectedVersion, ActionActor? actor = null) =>
        new(PrincipalId, expectedVersion, actor ?? Administrator, " issue-1 ", " first key ");

    [Fact]
    public void GeneratedSecretsCarryTheKeyIdAndAreWellFormed()
    {
        var keyId = PrincipalCredentialPolicy.GenerateKeyId();
        var secret = PrincipalCredentialPolicy.GenerateSecret(keyId);

        Assert.Equal(PrincipalCredentialPolicy.KeyIdLength, keyId.Length);
        Assert.StartsWith("pgs_" + keyId + "_", secret, StringComparison.Ordinal);
        Assert.Equal(PrincipalCredentialPolicy.SecretLength, secret.Length);
        Assert.True(PrincipalCredentialPolicy.IsWellFormed(keyId, secret));
        Assert.NotEqual(secret, PrincipalCredentialPolicy.GenerateSecret(keyId));
        Assert.False(PrincipalCredentialPolicy.IsWellFormed(PrincipalCredentialPolicy.GenerateKeyId(), secret));
        Assert.False(PrincipalCredentialPolicy.IsWellFormed(keyId, secret[..^1]));
        Assert.False(PrincipalCredentialPolicy.IsWellFormed(keyId, secret + "="));
        Assert.False(PrincipalCredentialPolicy.IsWellFormed(null, null));
    }

    [Fact]
    public void IssueThenResetRotatesInPlaceAndTheResetClearsPauseAndRevocation()
    {
        var issued = PrincipalCredentialPolicy.PlanIssue(null, ActivePrincipal, 0, Key("A"), Now);
        Assert.Equal(PrincipalCredentialState.Active, issued.State);
        Assert.Equal(1, issued.Version);
        Assert.Null(issued.RotatedAtUtc);

        var paused = PrincipalCredentialPolicy.PlanPause(issued, 1, Now.AddMinutes(1));
        var reset = PrincipalCredentialPolicy.PlanIssue(paused, ActivePrincipal, 2, Key("B"), Now.AddMinutes(2));
        Assert.Equal(Key("B"), reset.KeyId);
        Assert.Equal(PrincipalCredentialState.Active, reset.State);
        Assert.Null(reset.PausedAtUtc);
        Assert.Equal(Now.AddMinutes(2), reset.RotatedAtUtc);
        Assert.Equal(Now, reset.IssuedAtUtc);
        Assert.Equal(3, reset.Version);

        var revoked = PrincipalCredentialPolicy.PlanRevoke(reset, 3, Now.AddMinutes(3));
        var reissued = PrincipalCredentialPolicy.PlanIssue(revoked, ActivePrincipal, 4, Key("C"), Now.AddMinutes(4));
        Assert.Null(reissued.RevokedAtUtc);
        Assert.Equal(PrincipalCredentialState.Active, reissued.State);
    }

    [Fact]
    public void LifecycleGuardsFailClosed()
    {
        var issued = PrincipalCredentialPolicy.PlanIssue(null, ActivePrincipal, 0, Key("A"), Now);

        Assert.Equal(
            PrincipalCredentialError.StaleVersion,
            Assert.Throws<PrincipalCredentialException>(
                () => PrincipalCredentialPolicy.PlanIssue(issued, ActivePrincipal, 0, Key("B"), Now)).Error);
        Assert.Equal(
            PrincipalCredentialError.PrincipalInactive,
            Assert.Throws<PrincipalCredentialException>(
                () => PrincipalCredentialPolicy.PlanIssue(null, ActivePrincipal with { IsActive = false }, 0, Key("B"), Now)).Error);
        Assert.Equal(
            PrincipalCredentialError.CredentialNotFound,
            Assert.Throws<PrincipalCredentialException>(
                () => PrincipalCredentialPolicy.PlanPause(null, 0, Now)).Error);
        Assert.Equal(
            PrincipalCredentialError.CredentialNotPaused,
            Assert.Throws<PrincipalCredentialException>(
                () => PrincipalCredentialPolicy.PlanResume(issued, 1)).Error);

        var paused = PrincipalCredentialPolicy.PlanPause(issued, 1, Now);
        Assert.Equal(
            PrincipalCredentialError.CredentialAlreadyPaused,
            Assert.Throws<PrincipalCredentialException>(
                () => PrincipalCredentialPolicy.PlanPause(paused, 2, Now)).Error);
        Assert.Equal(
            PrincipalCredentialError.StaleVersion,
            Assert.Throws<PrincipalCredentialException>(
                () => PrincipalCredentialPolicy.PlanResume(paused, 1)).Error);

        var revoked = PrincipalCredentialPolicy.PlanRevoke(paused, 2, Now);
        Assert.Null(revoked.PausedAtUtc);
        foreach (var attempt in new Action[]
                 {
                     () => PrincipalCredentialPolicy.PlanPause(revoked, 3, Now),
                     () => PrincipalCredentialPolicy.PlanResume(revoked, 3),
                     () => PrincipalCredentialPolicy.PlanRevoke(revoked, 3, Now)
                 })
        {
            Assert.Equal(
                PrincipalCredentialError.CredentialRevoked,
                Assert.Throws<PrincipalCredentialException>(attempt).Error);
        }
    }

    [Fact]
    public void AuthenticationRefusesUnknownRevokedAndInactiveAndBlocksPausedSubmissions()
    {
        var active = PrincipalCredentialPolicy.PlanIssue(null, ActivePrincipal, 0, Key("A"), Now);
        var paused = PrincipalCredentialPolicy.PlanPause(active, 1, Now);
        var revoked = PrincipalCredentialPolicy.PlanRevoke(active, 1, Now);

        Assert.Null(PrincipalCredentialPolicy.Authenticate(null));
        Assert.Null(PrincipalCredentialPolicy.Authenticate(new(revoked, true)));
        Assert.Null(PrincipalCredentialPolicy.Authenticate(new(active, false)));

        var authenticated = PrincipalCredentialPolicy.Authenticate(new(active, true));
        Assert.NotNull(authenticated);
        Assert.Equal(PrincipalId, authenticated.PrincipalId);
        Assert.True(authenticated.MaySubmit);

        var blocked = PrincipalCredentialPolicy.Authenticate(new(paused, true));
        Assert.NotNull(blocked);
        Assert.Equal(PrincipalCredentialState.Paused, blocked.State);
        Assert.False(blocked.MaySubmit);
    }

    [Fact]
    public async Task IssueReturnsTheSecretOnceAndNeverOnReplay()
    {
        var store = new RecordingStore();
        var command = new IssuePrincipalCredential(store);

        var first = await command.ExecuteAsync(Request(0), default);
        store.ReplayNext = true;
        var replay = await command.ExecuteAsync(Request(0), default);

        Assert.NotNull(first.Secret);
        Assert.Equal(2, store.Issued.Count);
        Assert.Equal(first.Credential.KeyId, store.Issued[0].KeyId);
        Assert.True(PrincipalCredentialPolicy.IsWellFormed(first.Credential.KeyId, first.Secret));
        Assert.Null(replay.Secret);
        Assert.Equal("issue-1", store.Issued[0].Request.OperationKey);
        Assert.Equal("first key", store.Issued[0].Request.Reason);
    }

    [Fact]
    public async Task EveryCommandAndTheStatusQueryRequireAnAdministrator()
    {
        var store = new RecordingStore();
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => new IssuePrincipalCredential(store).ExecuteAsync(Request(0, Engineer), default));
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => new PausePrincipalCredential(store).ExecuteAsync(Request(1, Engineer), default));
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => new ResumePrincipalCredential(store).ExecuteAsync(Request(1, Engineer), default));
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => new RevokePrincipalCredential(store).ExecuteAsync(Request(1, Engineer), default));
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => new GetPrincipalCredential(store).ExecuteAsync(Engineer, PrincipalId, default));
        await Assert.ThrowsAsync<ArgumentException>(
            () => new PausePrincipalCredential(store).ExecuteAsync(Request(1) with { Reason = " " }, default));
        Assert.Empty(store.Issued);
        Assert.Empty(store.Transitions);
    }

    [Fact]
    public async Task MalformedPresentedCredentialsNeverReachTheStore()
    {
        var store = new RecordingStore();
        var authenticate = new AuthenticatePrincipalCredential(store);

        Assert.Null(await authenticate.ExecuteAsync("", "", default));
        Assert.Null(await authenticate.ExecuteAsync(Key("A"), "pgs_" + Key("B") + "_" + new string('x', 43), default));
        Assert.Equal(0, store.Verifications);

        Assert.Null(await authenticate.ExecuteAsync(Key("A"), "pgs_" + Key("A") + "_" + new string('x', 43), default));
        Assert.Equal(1, store.Verifications);
    }

    private static string Key(string seed) => seed.PadRight(PrincipalCredentialPolicy.KeyIdLength, '0');

    private sealed class RecordingStore : IPrincipalCredentialStore, IPrincipalCredentialQueries
    {
        public List<(PrincipalCredentialCommandRequest Request, string KeyId, string Secret)> Issued { get; } = [];
        public List<PrincipalCredentialCommandRequest> Transitions { get; } = [];
        public bool ReplayNext { get; set; }
        public int Verifications { get; private set; }

        public Task<PrincipalCredentialIssueResult> IssueAsync(
            PrincipalCredentialCommandRequest request,
            string keyId,
            string secret,
            CancellationToken cancellationToken)
        {
            Issued.Add((request, keyId, secret));
            var record = new PrincipalCredentialRecord(
                request.PrincipalId, keyId, PrincipalCredentialState.Active, Now, null, null, null, 1);
            return Task.FromResult(new PrincipalCredentialIssueResult(record, ReplayNext));
        }

        public Task<PrincipalCredentialRecord> PauseAsync(
            PrincipalCredentialCommandRequest request,
            CancellationToken cancellationToken) => Transition(request);

        public Task<PrincipalCredentialRecord> ResumeAsync(
            PrincipalCredentialCommandRequest request,
            CancellationToken cancellationToken) => Transition(request);

        public Task<PrincipalCredentialRecord> RevokeAsync(
            PrincipalCredentialCommandRequest request,
            CancellationToken cancellationToken) => Transition(request);

        public Task<PrincipalCredentialVerification?> VerifySecretAsync(
            string keyId,
            string secret,
            CancellationToken cancellationToken)
        {
            Verifications++;
            return Task.FromResult<PrincipalCredentialVerification?>(null);
        }

        public Task<PrincipalCredentialRecord?> GetAsync(Guid principalId, CancellationToken cancellationToken) =>
            Task.FromResult<PrincipalCredentialRecord?>(null);

        private Task<PrincipalCredentialRecord> Transition(PrincipalCredentialCommandRequest request)
        {
            Transitions.Add(request);
            throw new InvalidOperationException("Transitions are not expected in this test.");
        }
    }
}
