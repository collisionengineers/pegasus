using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfStaffPasswordChange(
    PegasusDbContext context,
    UserManager<PegasusIdentityUser> userManager,
    TimeProvider timeProvider) : IStaffPasswordChangeStore
{
    public async Task<ChangeStaffPasswordResult> ChangeAsync(
        ChangeStaffPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var replay = await context.SecurityEvents.SingleOrDefaultAsync(
            item => item.CorrelationId == request.OperationKey,
            cancellationToken);
        if (replay is not null)
        {
            if (replay.Type != SecurityEventType.PasswordChanged.ToString()
                || replay.SubjectId != request.StaffId.ToString("D")
                || replay.ReasonCode != "password_changed")
            {
                throw new StaffPasswordChangeException(
                    StaffPasswordChangeError.OperationConflict);
            }

            await transaction.CommitAsync(cancellationToken);
            return new(request.StaffId, 0, 0, WasReplay: true);
        }

        var user = await context.Users.SingleOrDefaultAsync(
            item => item.Id == request.StaffId,
            cancellationToken)
            ?? throw new StaffPasswordChangeException(
                StaffPasswordChangeError.StaffAccountNotFound);
        var change = await userManager.ChangePasswordAsync(
            user,
            request.CurrentPassword,
            request.NewPassword);
        if (!change.Succeeded)
        {
            var passwordMismatchCode = new IdentityErrorDescriber().PasswordMismatch().Code;
            throw new StaffPasswordChangeException(
                change.Errors.Any(error => error.Code == passwordMismatchCode)
                    ? StaffPasswordChangeError.CurrentPasswordInvalid
                    : StaffPasswordChangeError.PasswordRejected);
        }

        user.MustChangePassword = false;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            throw new StaffPasswordChangeException(
                StaffPasswordChangeError.PasswordRejected);
        }

        var subject = request.StaffId.ToString("D");
        var authorizations = await context.Set<OpenIddictEntityFrameworkCoreAuthorization>()
            .Where(item => item.Subject == subject
                && item.Status != OpenIddictConstants.Statuses.Revoked)
            .ToListAsync(cancellationToken);
        var tokens = await context.Set<OpenIddictEntityFrameworkCoreToken>()
            .Where(item => item.Subject == subject
                && item.Status != OpenIddictConstants.Statuses.Revoked)
            .ToListAsync(cancellationToken);
        foreach (var authorization in authorizations)
        {
            authorization.Status = OpenIddictConstants.Statuses.Revoked;
            authorization.ConcurrencyToken = Guid.NewGuid().ToString("N");
        }

        foreach (var token in tokens)
        {
            token.Status = OpenIddictConstants.Statuses.Revoked;
            token.ConcurrencyToken = Guid.NewGuid().ToString("N");
        }

        context.SecurityEvents.Add(new SecurityEventEntity
        {
            Id = Guid.NewGuid(),
            Type = SecurityEventType.PasswordChanged.ToString(),
            Outcome = SecurityEventOutcome.Succeeded.ToString(),
            SubjectId = subject,
            OccurredAtUtc = timeProvider.GetUtcNow(),
            CorrelationId = request.OperationKey,
            ReasonCode = "password_changed"
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            request.StaffId,
            RevokedAuthorizations: authorizations.Count,
            RevokedTokens: tokens.Count,
            WasReplay: false);
    }
}
