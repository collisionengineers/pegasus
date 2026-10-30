using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

/// <summary>
/// Whether a read-only lookup reached its source. Unavailable is a source
/// that refused, throttled or failed, or one no host has composed; a caller
/// shows it as such and never as "nothing found" (operator, 30 October 2026,
/// R561).
/// </summary>
public enum OldSystemLookupState
{
    Available,
    Unavailable
}

/// <summary>A folder under the old system's Box root, by name and identity only.</summary>
public sealed record OldSystemCaseFolder(
    string FolderId,
    string Name,
    DateTimeOffset? ModifiedAtUtc);

public sealed record OldSystemCaseFolderSearchResult(
    IReadOnlyList<OldSystemCaseFolder> Items,
    bool IsTruncated,
    OldSystemLookupState State = OldSystemLookupState.Available);

/// <summary>
/// The read-only name search under the old system's Box folder. It returns
/// names and identities, never content, and writes nothing.
/// </summary>
public interface IOldSystemCaseFolderSearch
{
    Task<OldSystemCaseFolderSearchResult> SearchAsync(
        string term,
        int maximumItems,
        CancellationToken cancellationToken);
}

/// <summary>One message header an approved mailbox returned for a search term.</summary>
public sealed record MailboxSearchItem(
    Guid MailboxId,
    string MailboxAddress,
    string MessageId,
    string? Subject,
    string? SenderAddress,
    DateTimeOffset? ReceivedAtUtc,
    string? Preview);

/// <summary>An approved mailbox the search asked, and whether it answered.</summary>
public sealed record MailboxSearchMailbox(
    Guid MailboxId,
    string MailboxAddress,
    OldSystemLookupState State);

public sealed record MailboxSearchResult(
    IReadOnlyList<MailboxSearchItem> Items,
    IReadOnlyList<MailboxSearchMailbox> Mailboxes,
    bool IsTruncated)
{
    /// <summary>Available when at least one approved mailbox answered.</summary>
    public OldSystemLookupState State =>
        Mailboxes.Any(mailbox => mailbox.State == OldSystemLookupState.Available)
            ? OldSystemLookupState.Available
            : OldSystemLookupState.Unavailable;
}

/// <summary>
/// The read-only search over the approved mailboxes. It returns message
/// headers only, reads no body or attachment, and changes nothing in any
/// mailbox.
/// </summary>
public interface IApprovedMailboxSearchSource
{
    Task<MailboxSearchResult> SearchAsync(
        Guid? mailboxId,
        string term,
        int maximumItemsPerMailbox,
        CancellationToken cancellationToken);
}

/// <summary>
/// The bounds both lookups share: the term and the cap of the Deleted Items
/// search this replaces (1 to 200 characters, at most 100 items).
/// </summary>
public static class OldSystemLookupBounds
{
    public const int MaximumTermLength = 200;
    public const int MaximumItems = 100;

    public static string RequireTerm(string? term, string parameterName)
    {
        var trimmed = term?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > MaximumTermLength)
        {
            throw new ArgumentException(
                $"A search term of 1 to {MaximumTermLength} characters is required.",
                parameterName);
        }
        return trimmed;
    }

    public static void RequireMaximum(int maximum, string parameterName)
    {
        if (maximum is < 1 or > MaximumItems)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                maximum,
                $"At most {MaximumItems} items may be asked for.");
        }
    }
}

/// <summary>
/// Searches the old system's case folders by name for a caseworker or the
/// Automation actor (R561). Read-only.
/// </summary>
public sealed class SearchOldSystemCaseFolders(IOldSystemCaseFolderSearch source)
{
    public Task<OldSystemCaseFolderSearchResult> ExecuteAsync(
        ActionActor actor,
        string term,
        int maximumItems = OldSystemLookupBounds.MaximumItems,
        CancellationToken cancellationToken = default)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        var trimmed = OldSystemLookupBounds.RequireTerm(term, nameof(term));
        OldSystemLookupBounds.RequireMaximum(maximumItems, nameof(maximumItems));
        return source.SearchAsync(trimmed, maximumItems, cancellationToken);
    }
}

/// <summary>
/// Searches the approved mailboxes for a term, for a caseworker or the
/// Automation actor (R561). Read-only; headers only.
/// </summary>
public sealed class SearchApprovedMailboxes(IApprovedMailboxSearchSource source)
{
    public Task<MailboxSearchResult> ExecuteAsync(
        ActionActor actor,
        Guid? mailboxId,
        string term,
        int maximumItemsPerMailbox = OldSystemLookupBounds.MaximumItems,
        CancellationToken cancellationToken = default)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        var trimmed = OldSystemLookupBounds.RequireTerm(term, nameof(term));
        OldSystemLookupBounds.RequireMaximum(maximumItemsPerMailbox, nameof(maximumItemsPerMailbox));
        return source.SearchAsync(mailboxId, trimmed, maximumItemsPerMailbox, cancellationToken);
    }
}

/// <summary>The lookup a host without a Box adapter composes: always Unavailable.</summary>
public sealed class UnavailableOldSystemCaseFolderSearch : IOldSystemCaseFolderSearch
{
    public Task<OldSystemCaseFolderSearchResult> SearchAsync(
        string term,
        int maximumItems,
        CancellationToken cancellationToken) =>
        Task.FromResult(new OldSystemCaseFolderSearchResult([], false, OldSystemLookupState.Unavailable));
}

/// <summary>The lookup a host without a Graph adapter composes: no mailbox answers.</summary>
public sealed class UnavailableApprovedMailboxSearchSource : IApprovedMailboxSearchSource
{
    public Task<MailboxSearchResult> SearchAsync(
        Guid? mailboxId,
        string term,
        int maximumItemsPerMailbox,
        CancellationToken cancellationToken) =>
        Task.FromResult(new MailboxSearchResult([], [], false));
}
