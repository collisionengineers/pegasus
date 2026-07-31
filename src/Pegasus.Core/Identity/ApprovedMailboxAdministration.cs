namespace Pegasus.Core.Identity;

public enum ApprovedMailboxRouteScope
{
    InboundIntake,
    SentEvidence
}

public enum ApprovedMailboxState
{
    Approved,
    Disabled
}

public sealed record ApprovedMailbox(
    Guid Id,
    string Address,
    IReadOnlyList<ApprovedMailboxRouteScope> RouteScopes,
    ApprovedMailboxState State,
    int Version);

public sealed record UpdateApprovedMailboxRequest(
    Guid MailboxId,
    string Address,
    IReadOnlyCollection<ApprovedMailboxRouteScope> RouteScopes,
    ApprovedMailboxState State,
    int ExpectedVersion,
    ActionActor Actor,
    string Reason,
    string OperationKey);

public interface IApprovedMailboxPolicy
{
    Task<bool> IsApprovedAsync(
        string mailboxAddress,
        ApprovedMailboxRouteScope routeScope,
        CancellationToken cancellationToken);
}

public interface IApprovedMailboxStore : IApprovedMailboxPolicy
{
    Task<IReadOnlyList<ApprovedMailbox>> ListAsync(CancellationToken cancellationToken);

    Task<ApprovedMailbox> UpdateAsync(
        UpdateApprovedMailboxRequest request,
        CancellationToken cancellationToken);
}

public sealed class ListApprovedMailboxes(IApprovedMailboxStore store)
{
    private readonly IApprovedMailboxStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<IReadOnlyList<ApprovedMailbox>> ExecuteAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ManageApprovedMailboxes);
        return _store.ListAsync(cancellationToken);
    }
}

public sealed class UpdateApprovedMailbox(IApprovedMailboxStore store)
{
    private readonly IApprovedMailboxStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<ApprovedMailbox> ExecuteAsync(
        UpdateApprovedMailboxRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.ManageApprovedMailboxes);
        if (request.MailboxId == Guid.Empty)
        {
            throw new ArgumentException("A mailbox identifier is required.", nameof(request));
        }
        if (request.ExpectedVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "The expected mailbox version cannot be negative.");
        }
        if (!Enum.IsDefined(request.State))
        {
            throw new ArgumentException("Select a supported mailbox state.", nameof(request));
        }

        ArgumentNullException.ThrowIfNull(request.RouteScopes);
        var routeScopes = request.RouteScopes.Distinct().OrderBy(scope => scope).ToArray();
        if (routeScopes.Length == 0 || routeScopes.Any(scope => !Enum.IsDefined(scope)))
        {
            throw new ArgumentException(
                "Select at least one supported mailbox route scope.",
                nameof(request));
        }

        return _store.UpdateAsync(
            request with
            {
                Address = ApprovedMailboxAddress.Normalize(request.Address),
                RouteScopes = routeScopes,
                Reason = RequireText(
                    request.Reason,
                    1000,
                    "A mailbox-policy reason is required."),
                OperationKey = RequireText(
                    request.OperationKey,
                    100,
                    "An operation key is required.")
            },
            cancellationToken);
    }


    private static string RequireText(string value, int maximumLength, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message);
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException($"The value cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }
}

public enum ApprovedMailboxUpdateError
{
    NotFound,
    DuplicateAddress,
    VersionConflict,
    OperationConflict
}

public sealed class ApprovedMailboxUpdateException(
    ApprovedMailboxUpdateError error,
    int? currentVersion = null)
    : InvalidOperationException("The approved-mailbox change could not be completed.")
{
    public ApprovedMailboxUpdateError Error { get; } = error;

    public int? CurrentVersion { get; } = currentVersion;
}
