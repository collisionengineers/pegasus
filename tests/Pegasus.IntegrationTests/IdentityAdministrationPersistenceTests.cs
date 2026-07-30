using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Pegasus.Core.Identity;
using Pegasus.Web.Authentication;
using Pegasus.Web.Pages.Connect;

namespace Pegasus.IntegrationTests;

[Collection(LocalDbFixtureDefinition.Name)]
[Trait("Category", "SqlServer")]
public sealed class IdentityAdministrationPersistenceTests
{
    [Fact]
    public async Task StaffCreationForcesPasswordChangeAndProtectsTheLastAdministrator()
    {
        using var factory = new IntakeWebApplicationFactory();
        _ = factory.Services;
        await using var scope = factory.Services.CreateAsyncScope();
        var actor = Administrator();
        var suffix = Guid.NewGuid().ToString("N");
        var command = scope.ServiceProvider.GetRequiredService<ICreateStaffAccount>();

        var created = await command.ExecuteAsync(
            new(actor, $"identity-test-{suffix}", "password", "Approved test account", $"create-{suffix}"),
            default);
        var replay = await command.ExecuteAsync(
            new(actor, $"identity-test-{suffix}", "password", "Approved test account", $"create-{suffix}"),
            default);

        Assert.True(created.Account.IsEnabled);
        Assert.True(created.Account.MustChangePassword);
        Assert.Equal([StaffRole.User], created.Account.Roles);
        Assert.True(replay.WasReplay);
        Assert.Equal(created.Account.Id, replay.Account.Id);

        var roles = scope.ServiceProvider.GetRequiredService<IAssignStaffRoles>();
        var exception = await Assert.ThrowsAsync<StaffAccountAdministrationException>(() =>
            roles.ExecuteAsync(
                new(
                    actor,
                    DevelopmentOfflineIdentity.AdministratorId,
                    [StaffRole.User],
                    "Attempt to remove the final Administrator",
                    $"roles-{suffix}"),
                default));
        Assert.Equal(StaffAccountAdministrationError.LastAdministrator, exception.Error);
    }

    [Fact]
    public async Task PublicClientRegisterReplayConflictAndRevocationPersistFailClosedMetadata()
    {
        using var factory = new IntakeWebApplicationFactory();
        _ = factory.Services;
        await using var scope = factory.Services.CreateAsyncScope();
        var suffix = Guid.NewGuid().ToString("N");
        var clientId = $"identity-client-{suffix}";
        var oauth = scope.ServiceProvider.GetRequiredService<StaffMcpOAuthOptions>();
        var metadata = new PublicMcpClientMetadata(
            clientId,
            "Identity administration integration client",
            [new Uri("http://127.0.0.1:7891/callback")],
            oauth.Resource,
            [StaffMcpClientContract.ReadScope, StaffMcpClientContract.WriteScope]);
        var register = scope.ServiceProvider.GetRequiredService<IRegisterPublicMcpClient>();

        var registered = await register.ExecuteAsync(
            new(Administrator(), metadata, "Approved integration client", $"register-{suffix}"),
            default);
        var replay = await register.ExecuteAsync(
            new(Administrator(), metadata, "Approved integration client", $"register-{suffix}"),
            default);
        var conflict = await Assert.ThrowsAsync<AuthenticationClientAdministrationException>(() =>
            register.ExecuteAsync(
                new(Administrator(), metadata, "Different reason", $"register-{suffix}"),
                default));

        Assert.True(registered.IsPublic);
        Assert.True(registered.RequiresPkceS256);
        Assert.True(replay.WasReplay);
        Assert.Equal(AuthenticationClientAdministrationError.OperationConflict, conflict.Error);

        var revoke = scope.ServiceProvider.GetRequiredService<IRevokePublicMcpClient>();
        var revoked = await revoke.ExecuteAsync(
            new(Administrator(), clientId, "Approved revocation", $"revoke-{suffix}"),
            default);
        var revokedReplay = await revoke.ExecuteAsync(
            new(Administrator(), clientId, "Approved revocation", $"revoke-{suffix}"),
            default);

        Assert.False(revoked.WasReplay);
        Assert.True(revokedReplay.WasReplay);
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await manager.FindByClientIdAsync(clientId);
        Assert.NotNull(application);
        var descriptor = new OpenIddictApplicationDescriptor();
        await manager.PopulateAsync(descriptor, application);
        Assert.Equal(OpenIddictConstants.ClientTypes.Public, descriptor.ClientType);
        Assert.Null(descriptor.ClientSecret);
        Assert.Empty(descriptor.Permissions);
        Assert.Empty(descriptor.RedirectUris);
        Assert.Empty(descriptor.Requirements);
    }

    [Fact]
    public void NormalWebCompositionCannotResolveTheOneShotBootstrapCaller()
    {
        using var factory = new IntakeWebApplicationFactory();
        Assert.Null(factory.Services.GetService<IInitializeApplication>());
    }

    private static ActionActor Administrator() =>
        ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
}
