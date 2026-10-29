using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The Automation kill switch's one transaction (FRD-17). It runs on the
/// request's scoped context, the one OpenIddict's application manager writes
/// through, so the caller's grant change, Stop's lease release and the history
/// row commit once. The registration row is locked first, as the Send to AI
/// switch locks its own (<see cref="EfSendToAiControlStore"/>), and the
/// enabled state the outcome names is read under that lock.
/// </summary>
public sealed class EfAutomationSwitchStore(
    PegasusDbContext context,
    TimeProvider timeProvider) : IAutomationSwitchStore
{
    public async Task<AutomationSwitchOutcome> SetAsync(
        AutomationSwitchCommand command,
        Func<CancellationToken, Task> changeGrant,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(changeGrant);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ClientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.OperationKey);
        StaffAuthorization.Require(command.Actor, StaffAccessRight.ManageAutomationClients);

        try
        {
            await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
            await context.Database.ExecuteSqlAsync(
                $"SELECT 1 FROM [OpenIddictApplications] WITH (UPDLOCK, HOLDLOCK) WHERE [ClientId] = {command.ClientId}",
                cancellationToken);
            var permissions = await context.Set<OpenIddictEntityFrameworkCoreApplication>()
                .AsNoTracking()
                .Where(item => item.ClientId == command.ClientId)
                .Select(item => item.Permissions)
                .ToListAsync(cancellationToken);
            if (permissions.Count != 1)
            {
                throw new InvalidOperationException("The Automation client registration is unavailable.");
            }

            // Enabled is the client-credentials grant on the registration,
            // the state AutomationClientRegistry reads and writes.
            var wasEnabled = (JsonSerializer.Deserialize<string[]>(permissions[0] ?? "[]") ?? [])
                .Contains(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials, StringComparer.Ordinal);
            var changed = wasEnabled != command.Enabled;
            if (changed)
            {
                await changeGrant(cancellationToken);
            }

            var released = command.Enabled
                ? null
                : await EfCaseWorkflowStore.ReleaseAutomationLeasesAsync(context, cancellationToken);
            context.ActionHistory.Add(EfIdentityAuditStore.ToEntity(new ActionHistoryEntry(
                Guid.NewGuid(),
                "automation_client",
                command.ClientId,
                command.Enabled ? "automation_client_enabled" : "automation_client_disabled",
                command.Actor,
                timeProvider.GetUtcNow(),
                changed ? "Succeeded" : "Unchanged",
                command.OperationKey.Trim(),
                string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason.Trim(),
                AfterJson: released is null ? null : JsonSerializer.Serialize(released))));
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(changed, released);
        }
        catch
        {
            // The transaction rolled back, so nothing this request tracked
            // (the grant, the cleared leases, the history row) is what the
            // database holds; a later save on this context must not replay it.
            context.ChangeTracker.Clear();
            throw;
        }
    }
}
