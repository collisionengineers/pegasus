namespace Pegasus.Core.Identity;

public sealed record StaffAccountSummary(
    Guid Id,
    string UserName,
    bool IsEnabled,
    bool MustChangePassword,
    IReadOnlyList<StaffRole> Roles,
    DateTimeOffset? LastAccessReviewAtUtc);

public interface IStaffAccountAdministration
{
    Task<IReadOnlyList<StaffAccountSummary>> ListAsync(
        ActionActor actor,
        CancellationToken cancellationToken);

    Task<StaffAccountSummary> CreateAsync(
        ActionActor actor,
        string userName,
        string temporaryPassword,
        string operationKey,
        CancellationToken cancellationToken);

    Task SetEnabledAsync(
        ActionActor actor,
        Guid staffId,
        bool enabled,
        string reason,
        string operationKey,
        CancellationToken cancellationToken);

    Task SetRolesAsync(
        ActionActor actor,
        Guid staffId,
        IReadOnlyCollection<StaffRole> roles,
        string reason,
        string operationKey,
        CancellationToken cancellationToken);

    Task ReviewAccessAsync(
        ActionActor actor,
        Guid staffId,
        string reason,
        string operationKey,
        CancellationToken cancellationToken);
}

public enum StaffAccountAdministrationError
{
    DuplicateUserName,
    InvalidAccount,
    StaffAccountNotFound,
    LastAdministrator,
    OperationConflict
}

public sealed class StaffAccountAdministrationException(
    StaffAccountAdministrationError error)
    : Exception("The staff account request could not be completed.")
{
    public StaffAccountAdministrationError Error { get; } = error;
}
