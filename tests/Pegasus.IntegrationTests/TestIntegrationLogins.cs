using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Integration logins as the store hands them to a provider (R1288), built
/// for provider tests that need no database.
/// </summary>
internal static class TestIntegrationLogins
{
    /// <summary>
    /// What the login store needs to be composed and resolved, over a
    /// database that does not exist: a test that resolves a provider proves
    /// it reads no login until it values.
    /// </summary>
    public static void AddCompositionOnlyStore(IServiceCollection services)
    {
        services.AddDataProtection();
        services.AddPooledDbContextFactory<PegasusDbContext>(options =>
            options.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=PegasusCompositionOnly;"));
    }

    public static IntegrationLoginMaterial Glass(string username, string password, long generation = 1) =>
        IntegrationLoginMaterial.Create(
            IntegrationLogin.GlassValuation,
            generation,
            new Dictionary<IntegrationLoginField, string>
            {
                [IntegrationLoginField.Username] = username,
                [IntegrationLoginField.Password] = password
            });

    public static IntegrationLoginMaterial Cazana(string email, string password, long generation = 1) =>
        IntegrationLoginMaterial.Create(
            IntegrationLogin.Cazana,
            generation,
            new Dictionary<IntegrationLoginField, string>
            {
                [IntegrationLoginField.Email] = email,
                [IntegrationLoginField.Password] = password
            });

    public static IntegrationLoginMaterial Brego(
        string email,
        string? refreshToken,
        string? sessionCookie,
        long generation = 1,
        string? password = null)
    {
        var fields = new Dictionary<IntegrationLoginField, string> { [IntegrationLoginField.Email] = email };
        if (password is not null)
        {
            fields[IntegrationLoginField.Password] = password;
        }

        return IntegrationLoginMaterial.Create(
            IntegrationLogin.Brego,
            generation,
            fields,
            refreshToken,
            sessionCookie);
    }

    public static IntegrationLoginMaterial Experian(
        IntegrationLogin login,
        string clientId,
        string clientSecret,
        string username,
        string password,
        long generation = 1) =>
        IntegrationLoginMaterial.Create(
            login,
            generation,
            new Dictionary<IntegrationLoginField, string>
            {
                [IntegrationLoginField.ClientId] = clientId,
                [IntegrationLoginField.ClientSecret] = clientSecret,
                [IntegrationLoginField.Username] = username,
                [IntegrationLoginField.Password] = password
            });
}
