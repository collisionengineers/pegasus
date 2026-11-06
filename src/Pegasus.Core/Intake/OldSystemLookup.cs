using System.Text.RegularExpressions;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;

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
/// The old-system folders are named by the firm's own Case reference
/// (operator, 2 November 2026, R592). The source's search is a full-text
/// match, so when the term is itself reference-shaped only the folders that
/// name that reference as a whole token are hits; any other term keeps the
/// source's hits as they are.
/// </summary>
public static partial class OldSystemFolderNameMatch
{
    private static readonly char[] TokenSeparators = [' ', '\t', '_', '-', ',', ';', '(', ')', '[', ']', '/'];

    /// <summary>
    /// A Case/PO (<c>QDOS26112</c>), with or without its <c>a.</c> or
    /// <c>t.</c> prefix, or a one-off's registration with an optional repeat
    /// (<c>AB12CDE</c>, <c>AB12CDE.2</c>, <c>a.AB12CDE</c>).
    /// </summary>
    public static bool IsReferenceShaped(string term)
    {
        var bare = StripTypePrefix(term.Trim());
        if (CasePoRegex().IsMatch(bare))
        {
            return true;
        }

        var separator = bare.IndexOf(CaseReferenceFormat.RepeatSeparator, StringComparison.Ordinal);
        var registration = separator < 0 ? bare : bare[..separator];
        var repeat = separator < 0 ? null : bare[(separator + 1)..];
        if (repeat is not null && !RepeatRegex().IsMatch(repeat))
        {
            return false;
        }

        return registration.Length > 0
            && registration.All(char.IsAsciiLetterOrDigit)
            && UkRegistrationShapes.IsAnyShape(registration.ToUpperInvariant());
    }

    /// <summary>
    /// The folder's name holds the term as a whole token, prefix-insensitive:
    /// <c>QDOS26112</c> matches <c>a.QDOS26112 Smith</c> and not
    /// <c>QDOS261125</c>.
    /// </summary>
    public static bool Matches(string folderName, string term)
    {
        var wanted = StripTypePrefix(term.Trim());
        return folderName
            .Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Any(token => string.Equals(StripTypePrefix(token), wanted, StringComparison.OrdinalIgnoreCase));
    }

    private static string StripTypePrefix(string value) =>
        value.StartsWith(CaseReferenceFormat.AuditPrefix, StringComparison.OrdinalIgnoreCase)
        || value.StartsWith(CaseReferenceFormat.TriagePrefix, StringComparison.OrdinalIgnoreCase)
            ? value[CaseReferenceFormat.AuditPrefix.Length..]
            : value;

    // The Case/PO shape CaseReferenceFormat.Base writes: principal code,
    // two-digit year, at least three digits of sequence.
    [GeneratedRegex(@"^[A-Za-z]{2,10}[0-9]{2}[0-9]{3,}$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex CasePoRegex();

    [GeneratedRegex(@"^[1-9][0-9]{0,2}$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex RepeatRegex();
}

/// <summary>
/// Searches the old system's case folders by name for a caseworker or the
/// Automation actor (R561). Read-only. A reference-shaped term keeps only the
/// folders named by that reference (R592).
/// </summary>
public sealed class SearchOldSystemCaseFolders(IOldSystemCaseFolderSearch source)
{
    public async Task<OldSystemCaseFolderSearchResult> ExecuteAsync(
        ActionActor actor,
        string term,
        int maximumItems = OldSystemLookupBounds.MaximumItems,
        CancellationToken cancellationToken = default)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        var trimmed = OldSystemLookupBounds.RequireTerm(term, nameof(term));
        OldSystemLookupBounds.RequireMaximum(maximumItems, nameof(maximumItems));
        if (!OldSystemFolderNameMatch.IsReferenceShaped(trimmed))
        {
            return await source.SearchAsync(trimmed, maximumItems, cancellationToken);
        }

        // The filter runs before the caller's cap: the source's first hits
        // may all be near-misses, so it is asked for the full cap.
        var result = await source.SearchAsync(trimmed, OldSystemLookupBounds.MaximumItems, cancellationToken);
        var matches = result.Items.Where(folder => OldSystemFolderNameMatch.Matches(folder.Name, trimmed)).ToArray();
        return result with
        {
            Items = matches.Take(maximumItems).ToArray(),
            IsTruncated = matches.Length > maximumItems || result.IsTruncated,
        };
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
