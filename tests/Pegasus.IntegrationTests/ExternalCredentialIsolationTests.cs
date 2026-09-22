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
            var engineer = ActionActor.Staff(engineerId, [StaffRole.User]);
            var otherEngineer = ActionActor.Staff(otherEngineerId, [StaffRole.User]);

            await using (var context = await database.CreateContextAsync())
            {
                var store = new EfPerUserExternalCredentialStore(
                    context,
                    protection,
                    TimeProvider.System);
                var status = await store.ReplaceAsync(
                    administrator,
                    engineerId,
                    ExternalCredentialProvider.GlassRepairEstimate,
                    expectedCredentialVersion: 0,
                    expectedStaffAccountVersion: await StaffAccountVersionAsync(database, engineerId),
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
        var engineer = ActionActor.Staff(engineerId, [StaffRole.User]);
        var first = await store.ReplaceAsync(
            administrator,
            engineerId,
            ExternalCredentialProvider.GlassRepairEstimate,
            0,
            await StaffAccountVersionAsync(database, engineerId),
            "alex.glass",
            "first-password",
            true,
            default);
        var second = await store.ReplaceAsync(
            administrator,
            engineerId,
            ExternalCredentialProvider.GlassRepairEstimate,
            first.Version,
            await StaffAccountVersionAsync(database, engineerId),
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

        await store.ClearAsync(
            administrator,
            engineerId,
            ExternalCredentialProvider.GlassRepairEstimate,
            second.Version,
            await StaffAccountVersionAsync(database, engineerId),
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

        var configured = await store.ReplaceAsync(
            administrator,
            configuredId,
            ExternalCredentialProvider.GlassRepairEstimate,
            0,
            await StaffAccountVersionAsync(database, configuredId),
            "configured-user",
            "configured-secret",
            true,
            default);
        var replacedFirst = await store.ReplaceAsync(
            administrator,
            replacedId,
            ExternalCredentialProvider.GlassRepairEstimate,
            0,
            await StaffAccountVersionAsync(database, replacedId),
            "replaced-user",
            "replaced-first-secret",
            true,
            default);
        var replaced = await store.ReplaceAsync(
            administrator,
            replacedId,
            ExternalCredentialProvider.GlassRepairEstimate,
            replacedFirst.Version,
            await StaffAccountVersionAsync(database, replacedId),
            "replaced-user",
            "replaced-second-secret",
            true,
            default);
        var disabled = await store.ReplaceAsync(
            administrator,
            disabledId,
            ExternalCredentialProvider.GlassRepairEstimate,
            0,
            await StaffAccountVersionAsync(database, disabledId),
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

        var engineer = ActionActor.Staff(engineerId, [StaffRole.User]);
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

    private static async Task<long> StaffAccountVersionAsync(
        LocalDbTestDatabase database,
        Guid staffId)
    {
        await using var context = await database.CreateContextAsync();
        return await context.Users
            .Where(item => item.Id == staffId)
            .Select(item => item.Version)
            .SingleAsync();
    }

    private static bool SecretEquals(string expected, string actual) =>
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(expected),
            System.Text.Encoding.UTF8.GetBytes(actual));
}
