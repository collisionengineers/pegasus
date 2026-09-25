using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>The QDOS principal code, as seeded. A code, not a gate.</summary>
internal static class QdosPrincipal
{
    public const string Code = "QDOS";
}

internal sealed record SeededPrincipalTestData(
    Guid Id,
    Guid OrganizationId,
    Guid SequenceLineageId);

internal static class SeededPrincipals
{
    public static async Task<SeededPrincipalTestData> QdosAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        return await QdosAsync(context);
    }

    public static async Task<SeededPrincipalTestData> QdosAsync(PegasusDbContext context)
    {
        return await context.Principals
            .AsNoTracking()
            .Where(item => item.Code == QdosPrincipal.Code && item.IsActive)
            .Select(item => new SeededPrincipalTestData(
                item.Id,
                item.OrganizationId,
                item.SequenceLineageId))
            .SingleAsync();
    }
}
