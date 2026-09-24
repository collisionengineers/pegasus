namespace Pegasus.Core.Identity;

/// <summary>
/// A staff member replacing their own password.
/// </summary>
/// <remarks>
/// <paramref name="CurrentPassword"/> is <see langword="null"/> when the account
/// is completing a forced change: the password it holds was issued by an
/// Administrator, so proving it adds nothing and the account only chooses the
/// replacement. The store refuses a missing current password for an account
/// that is not in forced-change state.
/// </remarks>
public sealed record ChangeStaffPasswordRequest(
    ActionActor Actor,
    Guid StaffId,
    string? CurrentPassword,
    string NewPassword,
    string OperationKey);

public sealed record ChangeStaffPasswordResult(
    Guid StaffId,
    long RevokedAuthorizations,
    long RevokedTokens,
    bool WasReplay);

public interface IStaffPasswordChangeStore
{
    Task<ChangeStaffPasswordResult> ChangeAsync(
        ChangeStaffPasswordRequest request,
        CancellationToken cancellationToken);
}

public interface IChangeStaffPassword
{
    Task<ChangeStaffPasswordResult> ExecuteAsync(
        ChangeStaffPasswordRequest request,
        CancellationToken cancellationToken);
}

public sealed class ChangeStaffPassword(IStaffPasswordChangeStore store)
    : IChangeStaffPassword
{
    private readonly IStaffPasswordChangeStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<ChangeStaffPasswordResult> ExecuteAsync(
        ChangeStaffPasswordRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAccountAdministrationPolicy.RequireStaffId(request.StaffId);
        if (request.Actor.Kind != ActorKind.Staff
            || !Guid.TryParse(request.Actor.SubjectId, out var actorStaffId)
            || actorStaffId != request.StaffId)
        {
            throw new StaffAuthorizationException(StaffAccessRight.AccessStaffApplication);
        }

        StaffAuthorization.Require(
            request.Actor,
            StaffAccessRight.AccessStaffApplication);
        StaffAccountAdministrationPolicy.ValidateTemporaryPassword(
            request.NewPassword,
            nameof(request.NewPassword));
        if (request.CurrentPassword is not null)
        {
            StaffAccountAdministrationPolicy.ValidateTemporaryPassword(
                request.CurrentPassword,
                nameof(request.CurrentPassword));
            if (string.Equals(
                    request.CurrentPassword,
                    request.NewPassword,
                    StringComparison.Ordinal))
            {
                throw new StaffPasswordChangeException(
                    StaffPasswordChangeError.PasswordUnchanged);
            }
        }

        return _store.ChangeAsync(
            request with
            {
                OperationKey = StaffAccountAdministrationPolicy.NormalizeRequiredText(
                    request.OperationKey,
                    StaffAccountAdministrationPolicy.MaximumOperationKeyLength,
                    nameof(request.OperationKey))
            },
            cancellationToken);
    }
}

public enum StaffPasswordChangeError
{
    StaffAccountNotFound,
    CurrentPasswordInvalid,
    PasswordUnchanged,
    PasswordRejected,
    OperationConflict
}

public sealed class StaffPasswordChangeException(StaffPasswordChangeError error)
    : InvalidOperationException("The password change could not be completed.")
{
    public StaffPasswordChangeError Error { get; } = error;
}
