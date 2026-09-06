using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class CaseEngineerChoicesPersistenceTests
{
    [Fact]
    public async Task EngineerChoicesReturnOnlyEnabledEngineersInStableOrder()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PegasusDbContext>();
        var role = await context.Roles.SingleAsync(item =>
            item.Name == StaffRoleNames.Engineer);
        var first = User("a.engineer", enabled: true);
        var second = User("b.engineer", enabled: true);
        var disabled = User("disabled.engineer", enabled: false);
        var ordinary = User("ordinary.user", enabled: true);
        context.Users.AddRange(second, disabled, ordinary, first);
        context.UserRoles.AddRange(
            Link(first, role),
            Link(second, role),
            Link(disabled, role));
        await context.SaveChangesAsync();

        var choices = scope.ServiceProvider.GetRequiredService<ICaseEngineerChoices>();
        Assert.Same(
            scope.ServiceProvider.GetRequiredService<IStaffAccountQueries>(),
            choices);

        var result = await choices.GetAsync(
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
            default);

        Assert.Collection(
            result,
            item => Assert.Equal(new(first.Id, "a.engineer"), item),
            item => Assert.Equal(new(second.Id, "b.engineer"), item));
    }

    [Fact]
    public async Task EngineerChoicesRequireCaseworkAuthority()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();
        var choices = scope.ServiceProvider.GetRequiredService<ICaseEngineerChoices>();

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => choices.GetAsync(
            ActionActor.SystemWorker("worker"),
            default));
    }

    private static PegasusIdentityUser User(string name, bool enabled) => new()
    {
        Id = Guid.NewGuid(),
        UserName = name,
        NormalizedUserName = name.ToUpperInvariant(),
        IsEnabled = enabled,
        SecurityStamp = Guid.NewGuid().ToString("N"),
        ConcurrencyStamp = Guid.NewGuid().ToString("N")
    };

    private static IdentityUserRole<Guid> Link(
        PegasusIdentityUser user,
        IdentityRole<Guid> role) => new()
        {
            UserId = user.Id,
            RoleId = role.Id
        };
}
