namespace Pegasus.Core.Identity;

public sealed record ChangeStaffPasswordRequest(
    ActionActor Actor,
    Guid StaffId,
    string CurrentPassword,
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
            request.CurrentPassword,
            nameof(request.CurrentPassword));
        StaffAccountAdministrationPolicy.ValidateTemporaryPassword(
            request.NewPassword,
            nameof(request.NewPassword));
        if (string.Equals(
                request.CurrentPassword,
                request.NewPassword,
                StringComparison.Ordinal))
        {
            throw new StaffPasswordChangeException(
                StaffPasswordChangeError.PasswordUnchanged);
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
