using Pegasus.Core.Intake;

namespace Pegasus.Core.Identity;

public enum ApprovedMailboxRouteScope
{
    InboundIntake,
    SentEvidence,
    StaffSend
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
    DateTimeOffset? ActivatedAtUtc,
    int Version,
    IReadOnlyList<ApprovedMailboxFolderBinding> FolderBindings,
    long Generation = 0,
    long? VerifiedEncodedMessageSizeLimit = null);

public sealed record ApprovedMailboxFolderBinding(
    MailLogicalFolderType FolderType,
    string FolderIdentity);

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
    string? SentFolderIdentity = null,
    IReadOnlyCollection<ApprovedMailboxFolderBinding>? FolderBindings = null,
    long? VerifiedEncodedMessageSizeLimit = null);

/// <summary>
/// One mailbox the approved estate says inbound-intake polling may read, with the
/// exact tenant identity the read needs. Only fully identified, Approved,
/// inbound-intake rows are ever offered.
/// </summary>
public sealed record ApprovedIntakeMailbox(
    Guid ApprovedMailboxId,
    string GraphMailboxId,
    string Address,
    string InboxFolderIdentity,
    DateTimeOffset ActivatedAtUtc,
    long Generation = 1);

public interface IApprovedIntakeMailboxes
{
    Task<IReadOnlyList<ApprovedIntakeMailbox>> ListPollableAsync(
        CancellationToken cancellationToken);

    async Task<ApprovedIntakeMailbox?> GetPollableAsync(
        Guid approvedMailboxId,
        CancellationToken cancellationToken) =>
        (await ListPollableAsync(cancellationToken))
            .SingleOrDefault(item => item.ApprovedMailboxId == approvedMailboxId);
}

/// <summary>
/// What inbound-intake polling has actually managed for one mailbox. Read-only: the
/// administration surface reports it, and nothing writes it from there.
/// </summary>
public sealed record ApprovedMailboxPollStatus(
    Guid ApprovedMailboxId,
    string MailboxAddress,
    DateTimeOffset DueAtUtc,
    DateTimeOffset? LastCompletedAtUtc,
    string? LastFailureCode,
    DateTimeOffset StartBoundaryUtc = default,
    long Generation = 0,
    DateTimeOffset? SubscriptionExpiresAtUtc = null,
    ApprovedMailboxSubscriptionLifecycleState? SubscriptionState = null,
    IReadOnlyList<ApprovedMailboxRouteScope>? Capabilities = null)
{
    public bool IsFresh(DateTimeOffset nowUtc) =>
        LastCompletedAtUtc is { } completed
        && nowUtc - completed <= GetRetainedMailFreshness.StaleAfter;
}

public interface IApprovedMailboxPollStatusQueries
{
    Task<IReadOnlyList<ApprovedMailboxPollStatus>> ListAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The exact Graph identities behind one mailbox address: the tenant mailbox id and its
/// well-known Inbox and Sent-items folder ids. Administration never asks an operator for
/// these — see <see cref="IResolveApprovedMailboxIdentity"/>.
/// </summary>
public sealed record ApprovedMailboxIdentityResolution(
    string MailboxIdentity,
    string InboxFolderIdentity,
    string SentFolderIdentity,
    IReadOnlyList<ApprovedMailboxFolderBinding>? FolderBindings = null);

/// <summary>
/// Resolves an approved-mailbox address to its exact Graph mailbox and well-known folder
/// identities, so the administration surface can add a mailbox from an address alone.
/// Returns null when the address cannot be resolved — not found in the tenant, or the
/// resolution transport itself failed — and the caller fails closed: no row is created,
/// and the operator sees only that the address could not be resolved, never why.
/// </summary>
public interface IResolveApprovedMailboxIdentity
{
    Task<ApprovedMailboxIdentityResolution?> ResolveAsync(
        string address,
        CancellationToken cancellationToken);
}

public interface ICheckApprovedMailboxAccess
{
    Task<bool> CanReadInboxAsync(
        ApprovedMailboxIdentityResolution mailbox,
        CancellationToken cancellationToken);
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
        var folderBindings = NormalizeFolderBindings(request.FolderBindings);
        if (request.VerifiedEncodedMessageSizeLimit is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request), "The verified encoded-message size limit must be positive.");
        }
        if (request.State == ApprovedMailboxState.Approved
            && routeScopes.Contains(ApprovedMailboxRouteScope.StaffSend)
            && request.VerifiedEncodedMessageSizeLimit is null)
        {
            throw new ApprovedMailboxUpdateException(
                ApprovedMailboxUpdateError.MissingVerifiedSendLimit);
        }
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
                FolderBindings = folderBindings,
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

    private static ApprovedMailboxFolderBinding[]? NormalizeFolderBindings(
        IReadOnlyCollection<ApprovedMailboxFolderBinding>? bindings)
    {
        if (bindings is null)
        {
            return null;
        }

        var normalized = new List<ApprovedMailboxFolderBinding>(bindings.Count);
        var folderTypes = new HashSet<MailLogicalFolderType>();
        foreach (var binding in bindings)
        {
            ArgumentNullException.ThrowIfNull(binding);
            if (!Enum.IsDefined(binding.FolderType) || !folderTypes.Add(binding.FolderType))
            {
                throw new ArgumentException(
                    "Each supported logical folder type may be bound at most once.",
                    nameof(bindings));
            }

            var identity = NormalizeIdentity(binding.FolderIdentity, MaximumFolderIdentityLength)
                ?? throw new ApprovedMailboxUpdateException(
                    ApprovedMailboxUpdateError.InvalidMailboxIdentity);
            normalized.Add(new(binding.FolderType, identity));
        }

        return normalized.OrderBy(item => item.FolderType).ToArray();
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
    DuplicateMailboxIdentity,
    MissingVerifiedSendLimit
}

public sealed class ApprovedMailboxUpdateException(
    ApprovedMailboxUpdateError error,
    int? currentVersion = null)
    : InvalidOperationException("The approved-mailbox change could not be completed.")
{
    public ApprovedMailboxUpdateError Error { get; } = error;

    public int? CurrentVersion { get; } = currentVersion;
}
