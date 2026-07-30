using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfApplicationInitialization(
    PegasusDbContext context,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOpenIddictApplicationManager applicationManager,
    EfStaffAccountAdministration staffAccounts,
    OpenIddictMcpClientAdministration mcpClients,
    TimeProvider timeProvider) : IApplicationInitializationStore
{
    private const string SingletonInitializationId = "application";

    public async Task<InitializeApplicationResult> InitializeAsync(
        InitializeApplicationStoreRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        await RequireExpectedMigrationAsync(request.ExpectedMigrationId, cancellationToken);
        if (await context.ApplicationInitializations.AnyAsync(cancellationToken))
        {
            throw new ApplicationInitializationException(
                ApplicationInitializationError.AlreadyInitialized);
        }

        if (await context.Users.AnyAsync(cancellationToken)
            || await applicationManager.CountAsync(cancellationToken) != 0)
        {
            throw new ApplicationInitializationException(
                ApplicationInitializationError.NonEmptyTarget);
        }

        var completedAtUtc = timeProvider.GetUtcNow();
        context.ApplicationInitializations.Add(new ApplicationInitializationEntity
        {
            Id = SingletonInitializationId,
            ManifestSha256 = request.ManifestSha256,
            MigrationId = request.ExpectedMigrationId,
            TargetIdentity = request.TargetIdentity,
            CompletedAtUtc = completedAtUtc
        });
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ApplicationInitializationException(
                ApplicationInitializationError.AlreadyInitialized);
        }

        await EnsureRolesAsync(cancellationToken);
        var administrators = new List<StaffAccountSummary>(
            InitializeApplication.InitialAdministratorCount);
        for (var index = 0; index < request.InitialAdministrators.Count; index++)
        {
            administrators.Add(await staffAccounts.CreateInitialAdministratorAsync(
                request.Actor,
                request.InitialAdministrators[index],
                BootstrapOperationKey(request.ManifestSha256, $"staff-{index + 1}"),
                cancellationToken));
        }

        var publicClient = await mcpClients.RegisterInitialClientAsync(
            request.Actor,
            request.PublicMcpClient,
            BootstrapOperationKey(request.ManifestSha256, "public-client"),
            cancellationToken);
        context.ActionHistory.Add(new ActionHistoryEntity
        {
            Id = Guid.NewGuid(),
            AggregateType = "application_initialization",
            AggregateId = request.TargetIdentity,
            EventKind = "application_initialized",
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = "[]",
            OccurredAtUtc = completedAtUtc,
            Outcome = "succeeded",
            CorrelationId = request.CorrelationId,
            Reason = "Approved one-shot application initialization.",
            BeforeJson = null,
            AfterJson = JsonSerializer.Serialize(new
            {
                request.ManifestSha256,
                MigrationId = request.ExpectedMigrationId,
                request.TargetIdentity,
                AdministratorManifestIdentities = request.InitialAdministrators
                    .Select(administrator => administrator.ManifestIdentity),
                PublicClientId = request.PublicMcpClient.ClientId
            })
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            request.ManifestSha256,
            request.ExpectedMigrationId,
            request.TargetIdentity,
            completedAtUtc,
            administrators.ToArray(),
            publicClient);
    }

    private async Task RequireExpectedMigrationAsync(
        string expectedMigrationId,
        CancellationToken cancellationToken)
    {
        var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync(
            cancellationToken)).ToArray();
        var pendingMigrations = (await context.Database.GetPendingMigrationsAsync(
            cancellationToken)).ToArray();
        if (pendingMigrations.Length != 0
            || appliedMigrations.Length == 0
            || !string.Equals(
                appliedMigrations[^1],
                expectedMigrationId,
                StringComparison.Ordinal))
        {
            throw new ApplicationInitializationException(
                ApplicationInitializationError.MigrationMismatch);
        }
    }

    private async Task EnsureRolesAsync(CancellationToken cancellationToken)
    {
        foreach (var roleName in StaffRoleNames.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                var result = await roleManager.CreateAsync(new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                    NormalizedName = roleName.ToUpperInvariant(),
                    ConcurrencyStamp = Guid.NewGuid().ToString("N")
                });
                if (!result.Succeeded)
                {
                    throw new ApplicationInitializationException(
                        ApplicationInitializationError.InvalidInitialAccount);
                }
            }
            else if (!string.Equals(role.Name, roleName, StringComparison.Ordinal))
            {
                throw new ApplicationInitializationException(
                    ApplicationInitializationError.InvalidInitialAccount);
            }
        }
    }

    private static string BootstrapOperationKey(string manifestSha256, string suffix) =>
        $"bootstrap:{manifestSha256[..32]}:{suffix}";
}
