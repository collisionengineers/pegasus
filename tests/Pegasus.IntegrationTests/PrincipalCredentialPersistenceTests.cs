using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class PrincipalCredentialPersistenceTests
{
    private static readonly ActionActor Administrator = ActionActor.Staff(
        Guid.Parse("0f149cac-e1d4-4a57-925f-7c35d33d7f5b"),
        [StaffRole.Administrator]);

    [Fact]
    public async Task IssueResetPauseResumeRevokeAreHashOnlyReplaySafeAndFailClosed()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var issue = services.GetRequiredService<IIssuePrincipalCredential>();
        var pause = services.GetRequiredService<IPausePrincipalCredential>();
        var resume = services.GetRequiredService<IResumePrincipalCredential>();
        var revoke = services.GetRequiredService<IRevokePrincipalCredential>();
        var get = services.GetRequiredService<IGetPrincipalCredential>();
        var authenticate = services.GetRequiredService<IAuthenticatePrincipalCredential>();
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();

        var organization = await services.GetRequiredService<ICreateOrganization>().ExecuteAsync(
            new("Alpha Provider", [OrganizationRole.WorkProvider], Administrator, "credential:org:alpha"),
            default);
        var principalId = (await SeededPrincipals.QdosAsync(services)).Id;
        var other = await services.GetRequiredService<ICreatePrincipal>().ExecuteAsync(
            new(organization.Id, "OTHER", Administrator, "credential:principal:other"),
            default);

        Assert.Null(await get.ExecuteAsync(Administrator, principalId, default));

        // Issue: the secret comes back once; the replay of the same operation
        // key returns the record and no secret.
        var issueRequest = Request(principalId, 0, "credential:issue:1", "first key");
        var issued = await issue.ExecuteAsync(issueRequest, default);
        var issuedReplay = await issue.ExecuteAsync(issueRequest, default);
        Assert.NotNull(issued.Secret);
        Assert.Null(issuedReplay.Secret);
        Assert.Equal(issued.Credential, issuedReplay.Credential);
        Assert.Equal(1, issued.Credential.Version);
        Assert.Equal(
            PrincipalCredentialError.OperationConflict,
            (await Assert.ThrowsAsync<PrincipalCredentialException>(
                () => issue.ExecuteAsync(issueRequest with { Reason = "different" }, default))).Error);

        var firstSecret = issued.Secret!;
        var firstKeyId = issued.Credential.KeyId;
        var authenticated = await authenticate.ExecuteAsync(firstKeyId, firstSecret, default);
        Assert.NotNull(authenticated);
        Assert.Equal(principalId, authenticated.PrincipalId);
        Assert.True(authenticated.MaySubmit);
        // Flip the last character to one it is not (DELIV-034): appending a
        // fixed "A" silently reproduced the *same* secret whenever the issued
        // one already ended in "A", so authentication correctly succeeded and
        // this assertion failed for no reason a reader could see. The secret's
        // 32 random bytes base64url-encode to 43 characters whose last one
        // carries only 4 bits, so it is drawn from 16 values that include 'A'
        // — one run in sixteen. Assert the tamper actually changed the secret
        // so a future no-op mutation fails loudly instead of passing.
        var tamperedSecret = firstSecret[..^1] + (firstSecret[^1] == 'A' ? 'B' : 'A');
        Assert.NotEqual(firstSecret, tamperedSecret);
        Assert.Null(await authenticate.ExecuteAsync(firstKeyId, tamperedSecret, default));

        // Only the PBKDF2 verifier is stored; the clear secret appears in no
        // column, receipt, or history row.
        await using (var context = await contextFactory.CreateDbContextAsync(default))
        {
            var stored = await context.PrincipalApiCredentials.AsNoTracking().SingleAsync();
            Assert.NotEqual(firstSecret, stored.SecretHash);
            Assert.DoesNotContain(firstSecret, stored.SecretHash, StringComparison.Ordinal);
            Assert.Equal(
                0,
                await context.Database.SqlQuery<int>(
                        $"SELECT COUNT(*) AS [Value] FROM [OrganizationAdministrationOperations] WHERE [ResultJson] LIKE {"%" + firstSecret + "%"}")
                    .SingleAsync());
            Assert.Equal(
                0,
                await context.Database.SqlQuery<int>(
                        $"SELECT COUNT(*) AS [Value] FROM [ActionHistory] WHERE [AfterJson] LIKE {"%" + firstSecret + "%"} OR [AfterJson] LIKE {"%" + stored.SecretHash + "%"}")
                    .SingleAsync());
        }

        // Reset: the previous secret stops verifying the moment the new one exists.
        var reset = await issue.ExecuteAsync(Request(principalId, 1, "credential:issue:2", "rotate"), default);
        Assert.NotNull(reset.Secret);
        Assert.NotEqual(firstKeyId, reset.Credential.KeyId);
        Assert.NotNull(reset.Credential.RotatedAtUtc);
        Assert.Equal(2, reset.Credential.Version);
        Assert.Null(await authenticate.ExecuteAsync(firstKeyId, firstSecret, default));
        Assert.NotNull(await authenticate.ExecuteAsync(reset.Credential.KeyId, reset.Secret!, default));

        // Pause: authenticated, submissions blocked. Resume restores them.
        var paused = await pause.ExecuteAsync(Request(principalId, 2, "credential:pause:1", "provider on hold"), default);
        Assert.Equal(PrincipalCredentialState.Paused, paused.State);
        var blocked = await authenticate.ExecuteAsync(reset.Credential.KeyId, reset.Secret!, default);
        Assert.NotNull(blocked);
        Assert.False(blocked.MaySubmit);
        Assert.Equal(
            PrincipalCredentialError.StaleVersion,
            (await Assert.ThrowsAsync<PrincipalCredentialException>(
                () => resume.ExecuteAsync(Request(principalId, 2, "credential:resume:stale", "stale"), default))).Error);
        var resumed = await resume.ExecuteAsync(Request(principalId, 3, "credential:resume:1", "provider back"), default);
        Assert.Equal(PrincipalCredentialState.Active, resumed.State);
        Assert.Null(resumed.PausedAtUtc);
        Assert.True((await authenticate.ExecuteAsync(reset.Credential.KeyId, reset.Secret!, default))!.MaySubmit);

        // Revoke: authentication refused; the lifecycle stops until a reissue.
        var revoked = await revoke.ExecuteAsync(Request(principalId, 4, "credential:revoke:1", "compromised"), default);
        Assert.Equal(PrincipalCredentialState.Revoked, revoked.State);
        Assert.Null(await authenticate.ExecuteAsync(reset.Credential.KeyId, reset.Secret!, default));
        Assert.Equal(
            PrincipalCredentialError.CredentialRevoked,
            (await Assert.ThrowsAsync<PrincipalCredentialException>(
                () => pause.ExecuteAsync(Request(principalId, 5, "credential:pause:revoked", "no"), default))).Error);
        var reissued = await issue.ExecuteAsync(Request(principalId, 5, "credential:issue:3", "new key"), default);
        Assert.Equal(PrincipalCredentialState.Active, reissued.Credential.State);
        Assert.NotNull(await authenticate.ExecuteAsync(reissued.Credential.KeyId, reissued.Secret!, default));

        // Isolation: the other Principal has nothing, and its absence is not
        // confused with this Principal's credential.
        Assert.Null(await get.ExecuteAsync(Administrator, other.Id, default));
        var status = await get.ExecuteAsync(Administrator, principalId, default);
        Assert.Equal(reissued.Credential, status);
        Assert.Equal(
            PrincipalCredentialError.CredentialNotFound,
            (await Assert.ThrowsAsync<PrincipalCredentialException>(
                () => revoke.ExecuteAsync(Request(other.Id, 0, "credential:revoke:other", "none"), default))).Error);

        await using (var context = await contextFactory.CreateDbContextAsync(default))
        {
            var events = await context.ActionHistory.AsNoTracking()
                .Where(item => item.AggregateType == "principal_api_credential")
                .Select(item => item.EventKind)
                .ToListAsync();
            Assert.Equal(
                [
                    "principal_credential_issued",
                    "principal_credential_paused",
                    "principal_credential_reset",
                    "principal_credential_reset",
                    "principal_credential_resumed",
                    "principal_credential_revoked"
                ],
                events.Order(StringComparer.Ordinal));
            Assert.Equal(1, await context.PrincipalApiCredentials.CountAsync());
        }
    }

    private static PrincipalCredentialCommandRequest Request(
        Guid principalId,
        long expectedVersion,
        string operationKey,
        string reason) =>
        new(principalId, expectedVersion, Administrator, operationKey, reason);
}
