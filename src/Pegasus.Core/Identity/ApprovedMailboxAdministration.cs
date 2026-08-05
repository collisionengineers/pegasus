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
    string? MailboxIdentity,
    string? InboxFolderIdentity,
    string? SentFolderIdentity,
    bool IdentityIsBound,
    int Version);

public sealed record UpdateApprovedMailboxRequest(
    Guid MailboxId,
    string Address,
    IReadOnlyCollection<ApprovedMailboxRouteScope> RouteScopes,
    ApprovedMailboxState State,
    int ExpectedVersion,
    ActionActor Actor,
    string Reason,
    string OperationKey,
    string? MailboxIdentity = null,
    string? InboxFolderIdentity = null,
    string? SentFolderIdentity = null);

/// <summary>
/// One mailbox the approved estate says inbound-intake polling may read, with the
/// exact tenant identity the read needs. Only fully identified, Approved,
/// inbound-intake rows are ever offered.
/// </summary>
public sealed record ApprovedIntakeMailbox(
    string MailboxId,
    string Address,
    string InboxFolderIdentity);

public interface IApprovedIntakeMailboxes
{
    Task<IReadOnlyList<ApprovedIntakeMailbox>> ListPollableAsync(
        CancellationToken cancellationToken);
}

/// <summary>
/// What inbound-intake polling has actually managed for one mailbox. Read-only: the
/// administration surface reports it, and nothing writes it from there.
/// </summary>
public sealed record ApprovedMailboxPollStatus(
    string MailboxIdentity,
    string MailboxAddress,
    DateTimeOffset DueAtUtc,
    DateTimeOffset? LastCompletedAtUtc,
    string? LastFailureCode);

public interface IApprovedMailboxPollStatusQueries
{
    Task<IReadOnlyList<ApprovedMailboxPollStatus>> ListAsync(CancellationToken cancellationToken);
}

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

        var address = ApprovedMailboxAddress.Normalize(request.Address);

        // The tenant identities are what make a row pollable. Their shape is business
        // policy, so Core owns it; the page only reports what Core refused.
        var mailboxIdentity = NormalizeIdentity(request.MailboxIdentity, MaximumMailboxIdentityLength);
        var inboxFolderIdentity = NormalizeIdentity(request.InboxFolderIdentity, MaximumFolderIdentityLength);
        var sentFolderIdentity = NormalizeIdentity(request.SentFolderIdentity, MaximumFolderIdentityLength);
        if (request.State == ApprovedMailboxState.Approved)
        {
            // Fail closed: an approved row that cannot be read is a silent no-op.
            // An administrator awaiting the tenant grant saves the row Disabled.
            var missing = mailboxIdentity is null
                || (routeScopes.Contains(ApprovedMailboxRouteScope.InboundIntake)
                    && inboxFolderIdentity is null)
                || (routeScopes.Contains(ApprovedMailboxRouteScope.SentEvidence)
                    && sentFolderIdentity is null);
            if (missing)
            {
                throw new ApprovedMailboxUpdateException(
                    ApprovedMailboxUpdateError.MissingMailboxIdentity);
            }
        }

        return _store.UpdateAsync(
            request with
            {
                Address = address,
                RouteScopes = routeScopes,
                MailboxIdentity = mailboxIdentity,
                InboxFolderIdentity = inboxFolderIdentity,
                SentFolderIdentity = sentFolderIdentity,
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

    /// <summary>
    /// A mailbox identity becomes the <c>ApprovedInboxPollStates</c> primary key, which
    /// is 100 characters; the folder identities mirror the 200-character Sent-state
    /// column. Both are exact tenant identifiers, so no whitespace and no control
    /// characters are admitted. Blank means "not supplied yet".
    /// </summary>
    private const int MaximumMailboxIdentityLength = 100;
    private const int MaximumFolderIdentityLength = 200;

    private static string? NormalizeIdentity(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength
            || normalized.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
        {
            throw new ApprovedMailboxUpdateException(
                ApprovedMailboxUpdateError.InvalidMailboxIdentity);
        }

        return normalized;
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
    OperationConflict,
    MissingMailboxIdentity,
    InvalidMailboxIdentity,
    MailboxIdentityImmutable,
    DuplicateMailboxIdentity
}

public sealed class ApprovedMailboxUpdateException(
    ApprovedMailboxUpdateError error,
    int? currentVersion = null)
    : InvalidOperationException("The approved-mailbox change could not be completed.")
{
    public ApprovedMailboxUpdateError Error { get; } = error;

    public int? CurrentVersion { get; } = currentVersion;
}
