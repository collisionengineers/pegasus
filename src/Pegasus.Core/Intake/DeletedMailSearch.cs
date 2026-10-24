using System.Collections.Concurrent;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

public enum DeletedMailSearchState
{
    Available,
    Unavailable
}

public sealed record DeletedMailSearchItem(
    Guid MailboxId,
    string MailboxAddress,
    string ImmutableMessageId,
    string? SenderAddress,
    string? SenderDisplayName,
    string? Subject,
    string? BodyPlainText,
    DateTimeOffset ReceivedAtUtc,
    bool IsRead,
    IReadOnlyList<RetainedMailAttachment> Attachments,
    IReadOnlyList<RetainedMailSearchMatch> Matches);

public sealed record DeletedMailSourceResult(
    IReadOnlyList<DeletedMailSearchItem> Items,
    bool IsTruncated,
    DeletedMailSearchState State = DeletedMailSearchState.Available);

public interface IDeletedMailSearchSource
{
    Task<IReadOnlyList<RetainedMailMailbox>> ListMailboxesAsync(
        CancellationToken cancellationToken);

    Task<DeletedMailSourceResult> SearchAsync(
        Guid? mailboxId,
        string searchTerm,
        int maximumMessages,
        CancellationToken cancellationToken);
}

public sealed record DeletedMailSearchPage(
    IReadOnlyList<DeletedMailSearchItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    bool IsTruncated,
    DeletedMailSearchState State)
{
    public int TotalPages => TotalCount == 0
        ? 1
        : (int)Math.Ceiling((double)TotalCount / PageSize);
}

/// <summary>
/// The parsed result of a Deleted Items search, kept for a few minutes per staff
/// member, mailbox and term so Next and Previous reuse it instead of listing and
/// parsing the same 100 messages again. Only an Available result is kept, and a
/// search, a Refresh or a reload is never answered from it: only a paging request
/// reads it. An expired entry is dropped when its key is read or when another
/// result is kept, so the last entries stay until the next search.
/// </summary>
public sealed class DeletedMailSearchKeep(TimeProvider timeProvider)
{
    internal static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<(string StaffId, Guid? MailboxId, string Term), Kept> kept = new();

    internal bool TryGet(
        string staffId,
        Guid? mailboxId,
        string term,
        out DeletedMailSearchItem[] items,
        out bool isTruncated)
    {
        var key = (staffId, mailboxId, term);
        if (kept.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt > timeProvider.GetUtcNow())
            {
                items = entry.Items;
                isTruncated = entry.IsTruncated;
                return true;
            }

            kept.TryRemove(new KeyValuePair<(string, Guid?, string), Kept>(key, entry));
        }

        items = [];
        isTruncated = false;
        return false;
    }

    internal void Keep(
        string staffId,
        Guid? mailboxId,
        string term,
        DeletedMailSearchItem[] items,
        bool isTruncated)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var expired in kept.Where(pair => pair.Value.ExpiresAt <= now))
        {
            kept.TryRemove(expired);
        }

        kept[(staffId, mailboxId, term)] = new(items, isTruncated, now + Lifetime);
    }

    private sealed record Kept(
        DeletedMailSearchItem[] Items,
        bool IsTruncated,
        DateTimeOffset ExpiresAt);
}

public sealed class SearchDeletedMail(
    IDeletedMailSearchSource source,
    DeletedMailSearchKeep keep)
{
    internal const int MaximumMessages = 100;

    public async Task<IReadOnlyList<RetainedMailMailbox>> ListMailboxesAsync(
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        return await source.ListMailboxesAsync(cancellationToken);
    }

    public async Task<DeletedMailSearchPage> ExecuteAsync(
        ActionActor actor,
        Guid? mailboxId,
        string searchTerm,
        int page,
        int pageSize,
        bool paging = false,
        CancellationToken cancellationToken = default)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        var term = searchTerm?.Trim();
        if (string.IsNullOrWhiteSpace(term) || term.Length > 200)
        {
            throw new ArgumentException(
                "A Deleted Items search term of 1 to 200 characters is required.",
                nameof(searchTerm));
        }
        if (page is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(page));
        }
        if (pageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        // Only Next and Previous reuse the kept result; a new search, a Refresh
        // and a reload read the source, as before.
        if (paging && keep.TryGet(actor.SubjectId, mailboxId, term, out var ordered, out var isTruncated))
        {
            return Page(ordered, page, pageSize, isTruncated, DeletedMailSearchState.Available);
        }

        var result = await source.SearchAsync(
            mailboxId,
            term,
            MaximumMessages,
            cancellationToken);
        ordered = result.Items
            .OrderByDescending(item => item.ReceivedAtUtc)
            .ThenBy(item => item.ImmutableMessageId, StringComparer.Ordinal)
            .ToArray();
        if (result.State == DeletedMailSearchState.Available)
        {
            keep.Keep(actor.SubjectId, mailboxId, term, ordered, result.IsTruncated);
        }

        return Page(ordered, page, pageSize, result.IsTruncated, result.State);
    }

    private static DeletedMailSearchPage Page(
        DeletedMailSearchItem[] ordered,
        int page,
        int pageSize,
        bool isTruncated,
        DeletedMailSearchState state) => new(
            ordered.Skip((page - 1) * pageSize).Take(pageSize).ToArray(),
            page,
            pageSize,
            ordered.Length,
            isTruncated,
            state);
}

public sealed class UnavailableDeletedMailSearchSource : IDeletedMailSearchSource
{
    public Task<IReadOnlyList<RetainedMailMailbox>> ListMailboxesAsync(
        CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<RetainedMailMailbox>>([]);

    public Task<DeletedMailSourceResult> SearchAsync(
        Guid? mailboxId,
        string searchTerm,
        int maximumMessages,
        CancellationToken cancellationToken) =>
        Task.FromResult(new DeletedMailSourceResult(
            [],
            false,
            DeletedMailSearchState.Unavailable));
}
