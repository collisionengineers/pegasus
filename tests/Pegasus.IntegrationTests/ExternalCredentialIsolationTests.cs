using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class ExternalCredentialIsolationTests
{
    [Fact]
    public async Task CredentialIsProtectedBoundToItsUserAndSurvivesAStoreRestart()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureServices: IdentityPersistenceTestServices.Configure);
        var keyDirectory = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "pegasus-credential-keys-" + Guid.NewGuid().ToString("N")));
        try
        {
            var protection = DataProtectionProvider.Create(keyDirectory);
            var administratorId = Guid.NewGuid();
            var engineerId = Guid.NewGuid();
            var otherEngineerId = Guid.NewGuid();
            await CreateEnabledUsersAsync(database, administratorId, engineerId, otherEngineerId);
            var administrator = ActionActor.Staff(administratorId, [StaffRole.Administrator]);
            var engineer = ActionActor.Staff(engineerId, [StaffRole.Engineer]);
            var otherEngineer = ActionActor.Staff(otherEngineerId, [StaffRole.Engineer]);

            await using (var context = await database.CreateContextAsync())
            {
                var store = new EfPerUserExternalCredentialStore(
                    context,
                    protection,
                    TimeProvider.System);
                var lease = await ClaimStaffAccountAsync(database, engineerId, administrator);
                var status = await store.ReplaceAsync(
                    administrator,
                    engineerId,
                    ExternalCredentialProvider.GlassRepairEstimate,
                    expectedCredentialVersion: 0,
                    expectedStaffAccountVersion: lease.RecordVersion,
                    editLeaseToken: lease.Token,
                    username: "alex.glass",
                    password: "provider-password",
                    enabled: true,
                    default);

                Assert.True(status.Configured);
                Assert.Equal("alex.glass", status.Username);
                Assert.Null(await store.GetEnabledAsync(
                    otherEngineer,
                    ExternalCredentialProvider.GlassRepairEstimate,
                    default));
            }

            await using (var restartedContext = await database.CreateContextAsync())
            {
                var restartedStore = new EfPerUserExternalCredentialStore(
                    restartedContext,
                    protection,
                    TimeProvider.System);
                var material = await restartedStore.GetEnabledAsync(
                    engineer,
                    ExternalCredentialProvider.GlassRepairEstimate,
                    default);

                Assert.NotNull(material);
                Assert.Equal("alex.glass", material.Username);
                Assert.True(SecretEquals("provider-password", material.Password));
                Assert.Equal(
                    nameof(PerUserExternalCredentialMaterial),
                    material.ToString());
            }

            await using var connection = database.CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT ProtectedCredential FROM UserExternalCredentials WHERE UserId = @userId;";
            command.Parameters.AddWithValue("@userId", engineerId);
            var protectedValue = Assert.IsType<string>(await command.ExecuteScalarAsync());
            Assert.False(protectedValue.Contains("alex.glass", StringComparison.Ordinal));
            Assert.False(protectedValue.Contains("provider-password", StringComparison.Ordinal));
        }
        finally
        {
            keyDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ReplacementInvalidatesThePriorGenerationAndClearRemovesReadableMaterial()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureServices: IdentityPersistenceTestServices.Configure);
        await using var context = await database.CreateContextAsync();
        var administratorId = Guid.NewGuid();
        var engineerId = Guid.NewGuid();
        await CreateEnabledUsersAsync(database, administratorId, engineerId);
        var store = new EfPerUserExternalCredentialStore(
            context,
            new EphemeralDataProtectionProvider(),
            TimeProvider.System);
        var administrator = ActionActor.Staff(administratorId, [StaffRole.Administrator]);
        var engineer = ActionActor.Staff(engineerId, [StaffRole.Engineer]);
        var firstLease = await ClaimStaffAccountAsync(database, engineerId, administrator);

        var first = await store.ReplaceAsync(
            administrator,
            engineerId,
            ExternalCredentialProvider.GlassRepairEstimate,
            0,
            firstLease.RecordVersion,
            firstLease.Token,
            "alex.glass",
            "first-password",
            true,
            default);
        var secondLease = await ClaimStaffAccountAsync(database, engineerId, administrator);
        var second = await store.ReplaceAsync(
            administrator,
            engineerId,
            ExternalCredentialProvider.GlassRepairEstimate,
            first.Version,
            secondLease.RecordVersion,
            secondLease.Token,
            "alex.glass",
            "second-password",
            true,
            default);

        Assert.Equal(first.CredentialGeneration + 1, second.CredentialGeneration);
        var replaced = await store.GetEnabledAsync(
            engineer,
            ExternalCredentialProvider.GlassRepairEstimate,
            default);
        Assert.NotNull(replaced);
        Assert.True(SecretEquals("second-password", replaced.Password));

        var clearLease = await ClaimStaffAccountAsync(database, engineerId, administrator);
        await store.ClearAsync(
            administrator,
            engineerId,
            ExternalCredentialProvider.GlassRepairEstimate,
            second.Version,
            clearLease.RecordVersion,
            clearLease.Token,
            default);
        Assert.Null(await store.GetEnabledAsync(
            engineer,
            ExternalCredentialProvider.GlassRepairEstimate,
            default));
        Assert.False((await store.GetAsync(
            administrator,
            engineerId,
            ExternalCredentialProvider.GlassRepairEstimate,
            default)).Configured);
    }

    [Fact]
    public async Task GetManyReturnsConfiguredEmptyAndDisabledStatusesWithoutCredentialSecrets()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureServices: IdentityPersistenceTestServices.Configure);
        await using var context = await database.CreateContextAsync();
        var administratorId = Guid.NewGuid();
        var configuredId = Guid.NewGuid();
        var replacedId = Guid.NewGuid();
        var disabledId = Guid.NewGuid();
        var emptyId = Guid.NewGuid();
        var engineerId = Guid.NewGuid();
        await CreateEnabledUsersAsync(
            database,
            administratorId,
            configuredId,
            replacedId,
            disabledId,
            emptyId,
            engineerId);
        var store = new EfPerUserExternalCredentialStore(
            context,
            new EphemeralDataProtectionProvider(),
            TimeProvider.System);
        var administrator = ActionActor.Staff(administratorId, [StaffRole.Administrator]);

        var configuredLease = await ClaimStaffAccountAsync(database, configuredId, administrator);
        var configured = await store.ReplaceAsync(
            administrator,
            configuredId,
            ExternalCredentialProvider.GlassRepairEstimate,
            0,
            configuredLease.RecordVersion,
            configuredLease.Token,
            "configured-user",
            "configured-secret",
            true,
            default);
        var replacedFirstLease = await ClaimStaffAccountAsync(database, replacedId, administrator);
        var replacedFirst = await store.ReplaceAsync(
            administrator,
            replacedId,
            ExternalCredentialProvider.GlassRepairEstimate,
            0,
            replacedFirstLease.RecordVersion,
            replacedFirstLease.Token,
            "replaced-user",
            "replaced-first-secret",
            true,
            default);
        var replacedSecondLease = await ClaimStaffAccountAsync(database, replacedId, administrator);
        var replaced = await store.ReplaceAsync(
            administrator,
            replacedId,
            ExternalCredentialProvider.GlassRepairEstimate,
            replacedFirst.Version,
            replacedSecondLease.RecordVersion,
            replacedSecondLease.Token,
            "replaced-user",
            "replaced-second-secret",
            true,
            default);
        var disabledLease = await ClaimStaffAccountAsync(database, disabledId, administrator);
        var disabled = await store.ReplaceAsync(
            administrator,
            disabledId,
            ExternalCredentialProvider.GlassRepairEstimate,
            0,
            disabledLease.RecordVersion,
            disabledLease.Token,
            "disabled-user",
            "disabled-secret",
            false,
            default);
        var requestedIds = new[] { configuredId, emptyId, disabledId, replacedId, configuredId };

        var statuses = await store.GetManyAsync(
            administrator,
            requestedIds,
            ExternalCredentialProvider.GlassRepairEstimate,
            default);

        Assert.Equal(4, statuses.Count);
        Assert.Equal(
            (true, true, "configured-user", configured.CredentialGeneration, configured.Version),
            (statuses[configuredId].Configured,
                statuses[configuredId].Enabled,
                statuses[configuredId].Username,
                statuses[configuredId].CredentialGeneration,
                statuses[configuredId].Version));
        Assert.Equal(
            (true, true, "replaced-user", replaced.CredentialGeneration, replaced.Version),
            (statuses[replacedId].Configured,
                statuses[replacedId].Enabled,
                statuses[replacedId].Username,
                statuses[replacedId].CredentialGeneration,
                statuses[replacedId].Version));
        Assert.Equal(replacedFirst.CredentialGeneration + 1, statuses[replacedId].CredentialGeneration);
        Assert.Equal(
            (true, false, "disabled-user", disabled.CredentialGeneration, disabled.Version),
            (statuses[disabledId].Configured,
                statuses[disabledId].Enabled,
                statuses[disabledId].Username,
                statuses[disabledId].CredentialGeneration,
                statuses[disabledId].Version));
        Assert.Equal(
            (false, false, (string?)null, 0L, 0L),
            (statuses[emptyId].Configured,
                statuses[emptyId].Enabled,
                statuses[emptyId].Username,
                statuses[emptyId].CredentialGeneration,
                statuses[emptyId].Version));
        Assert.All(statuses.Values, status => Assert.Equal(
            ExternalCredentialProvider.GlassRepairEstimate,
            status.Provider));
        var serializedStatuses = System.Text.Json.JsonSerializer.Serialize(statuses.Values);
        Assert.DoesNotContain("configured-secret", serializedStatuses, StringComparison.Ordinal);
        Assert.DoesNotContain("replaced-first-secret", serializedStatuses, StringComparison.Ordinal);
        Assert.DoesNotContain("replaced-second-secret", serializedStatuses, StringComparison.Ordinal);
        Assert.DoesNotContain("disabled-secret", serializedStatuses, StringComparison.Ordinal);

        var engineer = ActionActor.Staff(engineerId, [StaffRole.Engineer]);
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => store.GetManyAsync(
            engineer,
            requestedIds,
            ExternalCredentialProvider.GlassRepairEstimate,
            default));
    }

    private static async Task CreateEnabledUsersAsync(
        LocalDbTestDatabase database,
        params Guid[] userIds)
    {
        await using var scope = database.CreateAsyncScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<PegasusIdentityUser>>();
        foreach (var userId in userIds)
        {
            var result = await userManager.CreateAsync(new PegasusIdentityUser
            {
                Id = userId,
                UserName = "user-" + userId.ToString("N"),
                IsEnabled = true,
                MustChangePassword = false,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            });
            Assert.True(result.Succeeded);
        }
    }

    private static async Task<EditScopeLease> ClaimStaffAccountAsync(
        LocalDbTestDatabase database,
        Guid staffId,
        ActionActor actor)
    {
        await using var context = await database.CreateContextAsync();
        var version = await context.Users
            .Where(item => item.Id == staffId)
            .Select(item => item.Version)
            .SingleAsync();
        await using var scope = database.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IEditScopeLeases>().ClaimAsync(
            new(EditScopeKind.StaffAccount, staffId, version, actor, Guid.NewGuid().ToString("N")),
            default);
    }

    private static bool SecretEquals(string expected, string actual) =>
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(expected),
            System.Text.Encoding.UTF8.GetBytes(actual));
}
