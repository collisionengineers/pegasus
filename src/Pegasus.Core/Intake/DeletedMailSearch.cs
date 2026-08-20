using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

public enum DeletedMailSearchState
{
    Available,
    Unavailable
}

public sealed record DeletedMailSearchItem(
    string MailboxId,
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
        string? mailboxId,
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

public sealed class SearchDeletedMail(IDeletedMailSearchSource source)
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
        string? mailboxId,
        string searchTerm,
        int page,
        int pageSize,
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

        var result = await source.SearchAsync(
            string.IsNullOrWhiteSpace(mailboxId) ? null : mailboxId.Trim(),
            term,
            MaximumMessages,
            cancellationToken);
        var ordered = result.Items
            .OrderByDescending(item => item.ReceivedAtUtc)
            .ThenBy(item => item.ImmutableMessageId, StringComparer.Ordinal)
            .ToArray();
        return new(
            ordered.Skip((page - 1) * pageSize).Take(pageSize).ToArray(),
            page,
            pageSize,
            ordered.Length,
            result.IsTruncated,
            result.State);
    }
}

public sealed class UnavailableDeletedMailSearchSource : IDeletedMailSearchSource
{
    public Task<IReadOnlyList<RetainedMailMailbox>> ListMailboxesAsync(
        CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<RetainedMailMailbox>>([]);

    public Task<DeletedMailSourceResult> SearchAsync(
        string? mailboxId,
        string searchTerm,
        int maximumMessages,
        CancellationToken cancellationToken) =>
        Task.FromResult(new DeletedMailSourceResult(
            [],
            false,
            DeletedMailSearchState.Unavailable));
}
