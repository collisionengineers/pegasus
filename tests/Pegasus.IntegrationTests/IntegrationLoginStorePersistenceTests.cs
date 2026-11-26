using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Brego;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The integrations' logins in the database (R1288, ADR-0091): protected
/// with the key ring and never stored or written to history in plain text,
/// replaced by an Administrator under optimistic concurrency, Brego's stored
/// sign-in renewed in place without counting as a change, and a login another
/// key ring protected read as unreadable.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class IntegrationLoginStorePersistenceTests
{
    private const string GlassUsername = "valuation-user";
    private const string GlassPassword = "synthetic-glass-password-7731";
    private const string BregoEmail = "valuations@collisionengineers.test";
    private const string RefreshToken = "synthetic-refresh-token-5521";
    private const string SessionCookie = "synthetic-session-cookie-9044";

    private static readonly ActionActor Administrator =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    [Fact]
    public async Task AReplacedLoginShowsItsAccountNameAndIsReadBackButNeverStoredInPlainText()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var store = Store(database, new EphemeralDataProtectionProvider());

        var status = await store.ReplaceAsync(
            Administrator, IntegrationLogin.GlassValuation, 0, GlassValues(GlassUsername, GlassPassword), "replace-glass", default);

        Assert.Equal(GlassUsername, status.AccountName);
        Assert.True(status.Readable);
        Assert.Equal(1, status.Version);
        var listed = await store.ListAsync(Administrator, default);
        Assert.Equal(Enum.GetValues<IntegrationLogin>(), listed.Select(item => item.Login));
        var glass = listed.Single(item => item.Login == IntegrationLogin.GlassValuation);
        Assert.Equal(GlassUsername, glass.AccountName);
        Assert.True(glass.Readable);
        Assert.NotNull(glass.UpdatedAtUtc);
        Assert.All(
            listed.Where(item => item.Login != IntegrationLogin.GlassValuation),
            item =>
            {
                Assert.Null(item.AccountName);
                Assert.Equal(0, item.Version);
                Assert.True(item.Readable);
            });

        var material = await store.ReadAsync(IntegrationLogin.GlassValuation, default);
        Assert.NotNull(material);
        Assert.Equal(GlassUsername, material.Value(IntegrationLoginField.Username));
        Assert.Equal(GlassPassword, material.Value(IntegrationLoginField.Password));
        Assert.Equal(1, material.Generation);
        Assert.Null(await store.ReadAsync(IntegrationLogin.Cazana, default));

        var stored = await ScalarAsync(
            database, "SELECT ProtectedLogin FROM IntegrationLogins WHERE Id = N'glass-valuation';");
        Assert.DoesNotContain(GlassUsername, stored, StringComparison.Ordinal);
        Assert.DoesNotContain(GlassPassword, stored, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStaleVersionAnotherRoleOrAnotherLoginsFieldsAreRefused()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var store = Store(database, new EphemeralDataProtectionProvider());
        await store.ReplaceAsync(
            Administrator, IntegrationLogin.GlassValuation, 0, GlassValues(GlassUsername, GlassPassword), "first", default);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => store.ReplaceAsync(
            Administrator, IntegrationLogin.GlassValuation, 0, GlassValues("someone-else", GlassPassword), "stale", default));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => store.ReplaceAsync(
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
            IntegrationLogin.GlassValuation,
            1,
            GlassValues(GlassUsername, GlassPassword),
            "engineer",
            default));
        await Assert.ThrowsAsync<ArgumentException>(() => store.ReplaceAsync(
            Administrator, IntegrationLogin.Cazana, 0, GlassValues(GlassUsername, GlassPassword), "wrong-fields", default));
        await Assert.ThrowsAsync<ArgumentException>(() => store.ReplaceAsync(
            Administrator, IntegrationLogin.GlassValuation, 1, GlassValues(" ", GlassPassword), "blank", default));

        var material = await store.ReadAsync(IntegrationLogin.GlassValuation, default);
        Assert.Equal(GlassUsername, material!.Value(IntegrationLoginField.Username));
    }

    [Fact]
    public async Task EachReplacementIsOneHistoryRowThatCarriesNoSubmittedValue()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var store = Store(database, new EphemeralDataProtectionProvider());
        var first = await store.ReplaceAsync(
            Administrator, IntegrationLogin.GlassValuation, 0, GlassValues(GlassUsername, GlassPassword), "first", default);
        await store.ReplaceAsync(
            Administrator, IntegrationLogin.GlassValuation, first.Version, GlassValues(GlassUsername, GlassPassword + "-2"), "second", default);

        var rows = await RowsAsync(
            database,
            "SELECT EventKind + N'|' + ActorSubjectId + N'|' + ISNULL(BeforeJson, N'') + N'|' + ISNULL(AfterJson, N'') "
            + "FROM ActionHistory WHERE AggregateType = N'integration_login' ORDER BY OccurredAtUtc, CorrelationId;");

        Assert.Equal(2, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.StartsWith("integration_login_replaced|" + Administrator.SubjectId + "|", row, StringComparison.Ordinal);
            Assert.Contains("glass-valuation", row, StringComparison.Ordinal);
            Assert.DoesNotContain(GlassPassword, row, StringComparison.Ordinal);
            Assert.DoesNotContain(GlassUsername, row, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task ABregoRenewalKeepsTheGenerationAndVersionAndAnOlderGenerationWritesNothing()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var store = Store(database, new EphemeralDataProtectionProvider());
        var linked = await store.ReplaceAsync(
            Administrator, IntegrationLogin.Brego, 0, BregoValues(BregoEmail), "brego", default);

        Assert.True(await store.TrySaveAsync(1, new BregoStoredSignIn(SessionCookie, RefreshToken), default));

        var renewed = await store.ReadAsync(IntegrationLogin.Brego, default);
        Assert.NotNull(renewed);
        Assert.Equal(1, renewed.Generation);
        Assert.Equal(BregoEmail, renewed.Value(IntegrationLoginField.Email));
        Assert.Equal(RefreshToken, renewed.RefreshToken);
        Assert.Equal(SessionCookie, renewed.SessionCookie);
        var listed = (await store.ListAsync(Administrator, default))
            .Single(item => item.Login == IntegrationLogin.Brego);
        Assert.Equal(linked.Version, listed.Version);

        var replaced = await store.ReplaceAsync(
            Administrator, IntegrationLogin.Brego, linked.Version, BregoValues(BregoEmail), "brego-again", default);
        Assert.False(await store.TrySaveAsync(1, new BregoStoredSignIn("old-cookie", "old-refresh-token"), default));

        var after = await store.ReadAsync(IntegrationLogin.Brego, default);
        Assert.NotNull(after);
        Assert.Equal(2, after.Generation);
        Assert.Null(after.RefreshToken);
        Assert.Null(after.SessionCookie);
        Assert.Equal(2, replaced.Version);
        Assert.Equal(
            "2",
            await ScalarAsync(
                database, "SELECT CONVERT(nvarchar(10), COUNT(*)) FROM ActionHistory WHERE AggregateType = N'integration_login';"));
    }

    [Fact]
    public async Task ALoginAnotherKeyRingProtectedReadsAsNoneAndShowsUnreadable()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var original = Store(database, new EphemeralDataProtectionProvider());
        await original.ReplaceAsync(
            Administrator, IntegrationLogin.GlassValuation, 0, GlassValues(GlassUsername, GlassPassword), "glass", default);
        var otherHost = Store(database, new EphemeralDataProtectionProvider());

        Assert.Null(await otherHost.ReadAsync(IntegrationLogin.GlassValuation, default));
        var status = (await otherHost.ListAsync(Administrator, default))
            .Single(item => item.Login == IntegrationLogin.GlassValuation);
        Assert.False(status.Readable);
        Assert.Null(status.AccountName);
        Assert.Equal(1, status.Version);
    }

    private static EfIntegrationLoginStore Store(LocalDbTestDatabase database, IDataProtectionProvider protection)
    {
        using var scope = database.CreateAsyncScope();
        return new EfIntegrationLoginStore(
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
            protection,
            TimeProvider.System);
    }

    private static Dictionary<IntegrationLoginField, string> GlassValues(string username, string password) => new()
    {
        [IntegrationLoginField.Username] = username,
        [IntegrationLoginField.Password] = password
    };

    private static Dictionary<IntegrationLoginField, string> BregoValues(string email) => new()
    {
        [IntegrationLoginField.Email] = email
    };

    private static async Task<string> ScalarAsync(LocalDbTestDatabase database, string commandText)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        return Assert.IsType<string>(await command.ExecuteScalarAsync());
    }

    private static async Task<List<string>> RowsAsync(LocalDbTestDatabase database, string commandText)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync())
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }
}
