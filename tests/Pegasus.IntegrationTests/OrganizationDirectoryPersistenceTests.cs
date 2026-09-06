using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// EXT-18/S05: <see cref="EfOrganizationDirectory"/>'s read-only search over
/// the Administrator-maintained directory — active only, role-filterable,
/// exact-before-prefix ordered, and never more than the shared internal
/// 20-row cap.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class OrganizationDirectoryPersistenceTests
{
    private static readonly ActionActor Staff = ActionActor.Staff(
        Guid.Parse("9c3a1b2c-4d5e-46f7-8a9b-0c1d2e3f4a5b"),
        [StaffRole.User]);

    [Fact]
    public async Task SearchMatchesByNamePrefixAndExcludesInactiveEntries()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await SeedAsync(factory, "Northgate Repairs", "NORTHGATE REPAIRS", "storage", "N1 1AA", "N11AA", active: true);
        await SeedAsync(factory, "Northgate Storage Depot", "NORTHGATE STORAGE DEPOT", "storage", "N2 2BB", "N22BB", active: false);

        await using var scope = factory.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<IOrganizationDirectoryQueries>();
        var results = await directory.SearchAsync(
            new(Staff, "Northgate", Role: null),
            CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("Northgate Repairs", results[0].Name);
        Assert.True(results[0].Active);
    }

    [Fact]
    public async Task SearchMatchesByPostcodePrefix()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await SeedAsync(factory, "Eastfield Bodyshop", "EASTFIELD BODYSHOP", "repairer", "EF4 5GH", "EF45GH", active: true);

        await using var scope = factory.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<IOrganizationDirectoryQueries>();
        var results = await directory.SearchAsync(
            new(Staff, "EF4", Role: null),
            CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("Eastfield Bodyshop", results[0].Name);
    }

    [Fact]
    public async Task SearchFiltersByRole()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await SeedAsync(factory, "Combined Site Alpha", "COMBINED SITE ALPHA", "repairer", null, null, active: true);
        await SeedAsync(factory, "Combined Site Beta", "COMBINED SITE BETA", "storage", null, null, active: true);

        await using var scope = factory.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<IOrganizationDirectoryQueries>();
        var results = await directory.SearchAsync(
            new(Staff, "Combined", OrganizationDirectoryRole.Storage),
            CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("Combined Site Beta", results[0].Name);
    }

    [Fact]
    public async Task SearchOrdersAnExactNormalizedMatchBeforeAPrefixMatch()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await SeedAsync(factory, "Fenwick", "FENWICK", "storage", null, null, active: true);
        await SeedAsync(factory, "Fenwick Extended Yard", "FENWICK EXTENDED YARD", "storage", null, null, active: true);

        await using var scope = factory.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<IOrganizationDirectoryQueries>();
        var results = await directory.SearchAsync(
            new(Staff, "Fenwick", Role: null),
            CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.Equal("Fenwick", results[0].Name);
    }

    [Fact]
    public async Task SearchDeniesAnActorWithoutCaseworkAccess()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await using var scope = factory.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<IOrganizationDirectoryQueries>();
        var externalActor = ActionActor.RequestLink(Guid.NewGuid());

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            directory.SearchAsync(new(externalActor, "Fenwick", Role: null), CancellationToken.None));
    }

    private static async Task SeedAsync(
        WebApplicationFactory<Program> factory,
        string name,
        string normalizedName,
        string role,
        string? postcode,
        string? normalizedPostcode,
        bool active)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        context.Set<OrganizationDirectoryEntryEntity>().Add(new()
        {
            Id = Guid.NewGuid(),
            Role = role,
            Name = name,
            NormalizedName = normalizedName,
            Address = "1 Test Way",
            Postcode = postcode,
            NormalizedPostcode = normalizedPostcode,
            SourceKind = "manual",
            SourceRecordId = null,
            SourceVersion = 1,
            UpdatedBy = Staff.SubjectId,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Active = active,
            Version = 0
        });
        await context.SaveChangesAsync();
    }
}
