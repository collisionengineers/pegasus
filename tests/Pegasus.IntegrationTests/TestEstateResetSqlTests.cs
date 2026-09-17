using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class TestEstateResetSqlTests
{
    [Fact]
    public async Task ResetRetainsAlexRemovesOtherAccountTracesAndRestartsQdos()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureServices: IdentityPersistenceTestServices.Configure);
        await using var scope = database.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<PegasusIdentityUser>>();
        var alex = await CreateUserAsync(userManager, "alex", "Administrator");
        var removed = await CreateUserAsync(userManager, "remove-me", "Engineer");
        var context = services.GetRequiredService<PegasusDbContext>();
        var qdosLineageId = await context.Principals
            .Where(item => item.Code == "QDOS")
            .Select(item => item.SequenceLineageId)
            .SingleAsync();
        const int sequenceYear = 2031;
        context.CaseSequences.Add(new CaseSequenceEntity
        {
            SequenceLineageId = qdosLineageId,
            Year = sequenceYear,
            LastAllocatedSequence = 9
        });
        context.SecurityEvents.AddRange(
            SecurityEvent(removed.Id.ToString("D"), alex.Id.ToString("D"), "removed-subject"),
            SecurityEvent(alex.Id.ToString("D"), removed.Id.ToString("D"), "removed-actor"),
            SecurityEvent(alex.Id.ToString("D"), alex.Id.ToString("D"), "retained"));
        context.Set<OpenIddictEntityFrameworkCoreAuthorization>().Add(new()
        {
            Id = "removed-authorization",
            Subject = removed.Id.ToString("D"),
            Status = "valid",
            Type = "ad_hoc"
        });
        context.Set<OpenIddictEntityFrameworkCoreToken>().Add(new()
        {
            Id = "removed-token",
            Subject = removed.Id.ToString("D"),
            Status = "valid",
            Type = "access_token"
        });
        await context.SaveChangesAsync();

        var sql = await File.ReadAllTextAsync(Path.Combine(
            CorpusPackage.RepositoryRoot, "scripts", "Reset-TestEstate.sql"));
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.Database.ExecuteSqlRawAsync(
            sql,
            new SqlParameter("@QdosSequenceLineageId", qdosLineageId),
            new SqlParameter("@QdosSequenceYear", sequenceYear));
        await transaction.CommitAsync();
        context.ChangeTracker.Clear();

        var retained = Assert.Single(await context.Users.AsNoTracking().ToListAsync());
        Assert.Equal(alex.Id, retained.Id);
        Assert.Equal("ALEX", retained.NormalizedUserName);
        Assert.Single(await context.UserRoles.AsNoTracking().ToListAsync());
        Assert.Empty(await context.Set<OpenIddictEntityFrameworkCoreAuthorization>()
            .AsNoTracking().ToListAsync());
        Assert.Empty(await context.Set<OpenIddictEntityFrameworkCoreToken>()
            .AsNoTracking().ToListAsync());
        var securityEvent = Assert.Single(await context.SecurityEvents.AsNoTracking().ToListAsync());
        Assert.Equal("retained", securityEvent.CorrelationId);
        Assert.Equal(
            0,
            await context.CaseSequences
                .Where(item => item.SequenceLineageId == qdosLineageId && item.Year == sequenceYear)
                .Select(item => item.LastAllocatedSequence)
                .SingleAsync());
    }

    [Fact]
    public async Task ResetRefusesWhenAlexIsNotAnAdministrator()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureServices: IdentityPersistenceTestServices.Configure);
        await using var scope = database.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<PegasusIdentityUser>>();
        _ = await CreateUserAsync(userManager, "alex", "User");
        var context = services.GetRequiredService<PegasusDbContext>();
        var qdosLineageId = await context.Principals
            .Where(item => item.Code == "QDOS")
            .Select(item => item.SequenceLineageId)
            .SingleAsync();
        var sql = await File.ReadAllTextAsync(Path.Combine(
            CorpusPackage.RepositoryRoot, "scripts", "Reset-TestEstate.sql"));

        await using var transaction = await context.Database.BeginTransactionAsync();
        var exception = await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(
            sql,
            new SqlParameter("@QdosSequenceLineageId", qdosLineageId),
            new SqlParameter("@QdosSequenceYear", 2031)));

        Assert.Contains("alex account must be an Administrator", exception.Message);
        await transaction.RollbackAsync();
        Assert.Single(await context.Users.AsNoTracking().ToListAsync());
    }

    private static async Task<PegasusIdentityUser> CreateUserAsync(
        UserManager<PegasusIdentityUser> userManager,
        string userName,
        string role)
    {
        var user = new PegasusIdentityUser { Id = Guid.NewGuid(), UserName = userName };
        Assert.True((await userManager.CreateAsync(user, "Password-1")).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, role)).Succeeded);
        return user;
    }

    private static SecurityEventEntity SecurityEvent(
        string subjectId,
        string actorSubjectId,
        string correlationId) =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = "test",
            Outcome = "succeeded",
            SubjectId = subjectId,
            ActorKind = "Staff",
            ActorSubjectId = actorSubjectId,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = correlationId
        };
}
