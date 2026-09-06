using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
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
                var status = await store.ReplaceAsync(
                    administrator,
                    engineerId,
                    ExternalCredentialProvider.GlassRepairEstimate,
                    expectedVersion: 0,
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

        var first = await store.ReplaceAsync(
            administrator,
            engineerId,
            ExternalCredentialProvider.GlassRepairEstimate,
            0,
            "alex.glass",
            "first-password",
            true,
            default);
        var second = await store.ReplaceAsync(
            administrator,
            engineerId,
            ExternalCredentialProvider.GlassRepairEstimate,
            first.Version,
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

    private static bool SecretEquals(string expected, string actual) =>
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(expected),
            System.Text.Encoding.UTF8.GetBytes(actual));
}
