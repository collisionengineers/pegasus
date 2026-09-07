using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Triage;

public sealed record ListTriageQuery(
    ActionActor Actor,
    TriageState? State,
    int Page = 1,
    int PageSize = 25);

public sealed record TriageListPage(
    IReadOnlyList<TriageSummary> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 1
        : (int)Math.Ceiling((double)TotalCount / PageSize);
}

public sealed record GetTriageQuery(Guid TriageId, ActionActor Actor);

/// <summary>
/// One keyset page of the Triage list. <paramref name="Cursor"/> is null for
/// the first page and is otherwise the opaque value the previous page
/// returned; it is bound to this actor, this filter and this order, so a
/// cursor from another query or another actor is refused rather than
/// silently answered from the wrong list.
/// </summary>
public sealed record ListTriagePageQuery(
    ActionActor Actor,
    TriageState? State,
    string? Cursor = null,
    int? Limit = null);

public interface IListTriage
{
    Task<TriageListPage> ExecuteAsync(
        ListTriageQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The keyset seam for the Triage list. It is separate from
/// <see cref="IListTriage"/> because the offset list is still what the queue
/// tab reads; when that surface moves to continuation this port replaces it
/// rather than joining it.
/// </summary>
public interface IListTriagePage
{
    Task<CursorPage<TriageSummary>> ExecuteAsync(
        ListTriagePageQuery query,
        CancellationToken cancellationToken = default);
}

public interface IGetTriage
{
    Task<TriageDetail?> ExecuteAsync(
        GetTriageQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class ListTriage(ITriageQueries queries) : IListTriage
{
    private readonly ITriageQueries queries =
        queries ?? throw new ArgumentNullException(nameof(queries));

    public async Task<TriageListPage> ExecuteAsync(
        ListTriageQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.Page is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "The requested page is outside the supported range.");
        }
        if (query.PageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "The requested page size is outside the supported range.");
        }
        if (query.State is { } state && !Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "The Triage state is not recognized.");
        }

        var matches = await queries.ListAsync(query.State, cancellationToken);
        var items = matches
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();
        return new(items, query.Page, query.PageSize, matches.Count);
    }
}

/// <summary>
/// The Triage list as a keyset continuation. The opaque cursor is minted here,
/// over the shared <see cref="CursorPaging"/> contract: the store below deals
/// only in a decoded <see cref="TriageListPosition"/> and never sees a token.
/// </summary>
public sealed class ListTriagePage(ITriageQueries queries, ICursorProtector protector)
    : IListTriagePage
{
    private const string QueryName = "triage";
    private const string Order = "created_desc,sequence_desc";

    private readonly ITriageQueries queries =
        queries ?? throw new ArgumentNullException(nameof(queries));

    private readonly ICursorProtector protector =
        protector ?? throw new ArgumentNullException(nameof(protector));

    public async Task<CursorPage<TriageSummary>> ExecuteAsync(
        ListTriagePageQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.State is { } requested && !Enum.IsDefined(requested))
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "The Triage state is not recognized.");
        }

        var limit = CursorPaging.NormalizeLimit(query.Limit);
        // The scope binds the cursor to this actor, this filter and this order.
        // A cursor minted for another one fails to unprotect and is refused.
        var scope = CursorPaging.CreateScope(
            QueryName,
            query.Actor,
            query.State?.ToString(),
            Order);
        TriageListPosition? after = null;
        if (!string.IsNullOrWhiteSpace(query.Cursor))
        {
            var (sortKey, id) = protector.Unprotect(query.Cursor, scope);
            after = new(CursorPaging.DecodeUtcTimestamp(sortKey), id);
        }

        var slice = await queries.ListPageAsync(query.State, after, limit, cancellationToken);
        var next = slice.NextPosition is { } position
            ? protector.Protect(
                scope,
                CursorPaging.EncodeUtcTimestamp(position.CreatedAtUtc),
                position.Id)
            : null;
        return new(slice.Items, next);
    }
}

public sealed class GetTriage(
    ITriageQueries queries,
    ITriageResponseEvidenceCandidateQueries candidateQueries,
    ISentEvidencePollOutcomeQueries pollOutcomeQueries,
    IStaffAccountQueries staffAccountQueries) : IGetTriage
{
    private const int MaximumReplyChainIdentities = 100;
    private const int MaximumResponseEvidenceCandidates = 20;

    private readonly ITriageQueries queries =
        queries ?? throw new ArgumentNullException(nameof(queries));
    private readonly ITriageResponseEvidenceCandidateQueries candidateQueries =
        candidateQueries ?? throw new ArgumentNullException(nameof(candidateQueries));
    private readonly ISentEvidencePollOutcomeQueries pollOutcomeQueries =
        pollOutcomeQueries ?? throw new ArgumentNullException(nameof(pollOutcomeQueries));
    private readonly IStaffAccountQueries staffAccountQueries =
        staffAccountQueries ?? throw new ArgumentNullException(nameof(staffAccountQueries));

    public async Task<TriageDetail?> ExecuteAsync(
        GetTriageQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.TriageId == Guid.Empty)
        {
            throw new ArgumentException(
                "A Triage identifier is required.",
                nameof(query));
        }

        var detail = await queries.GetAsync(query.TriageId, cancellationToken);
        if (detail is null)
        {
            return null;
        }

        var staffIds = detail.History
            .Where(entry => entry.ActorKind == nameof(ActorKind.Staff)
                && Guid.TryParse(entry.Actor, out _))
            .Select(entry => Guid.Parse(entry.Actor));
        var staffNames = await ActorDisplayNames.ResolveStaffNamesAsync(
            staffAccountQueries,
            staffIds,
            cancellationToken);
        detail = detail with
        {
            History = detail.History
                .Select(entry => entry with
                {
                    ActorDisplayName = Enum.TryParse<ActorKind>(entry.ActorKind, out var actorKind)
                        ? ActorDisplayNames.Resolve(actorKind, entry.Actor, staffNames)
                        : ActorDisplayNames.UnknownStaff
                })
                .ToArray()
        };

        var sentEvidence = await candidateQueries.ListSentEvidenceReferencesAsync(
            query.TriageId,
            MaximumReplyChainIdentities,
            cancellationToken);
        if (sentEvidence.Count == 0)
        {
            return detail;
        }

        var replyChainIdentities = sentEvidence
            .Select(item => item.MessageIdentity)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var pollOutcomes = await pollOutcomeQueries.ListUnlinkedReplyCandidatesAsync(
            replyChainIdentities,
            MaximumResponseEvidenceCandidates,
            cancellationToken);
        var candidates = pollOutcomes
            .SelectMany(outcome => sentEvidence
                .Where(evidence => outcome.InReplyToIdentities.Contains(
                    evidence.MessageIdentity,
                    StringComparer.Ordinal))
                .Select(evidence => new TriageResponseEvidenceCandidate(
                    outcome.PollOutcomeId,
                    evidence.SentEvidenceId,
                    outcome.MailboxAddress,
                    outcome.SentFolderIdentity,
                    outcome.ImmutableItemIdentity,
                    outcome.InternetMessageIdentity,
                    outcome.ConversationIdentity,
                    outcome.ReplyChainIdentity,
                    outcome.SentAtUtc,
                    outcome.DiscoveredAtUtc)))
            .Take(MaximumResponseEvidenceCandidates)
            .ToArray();

        return detail with { ResponseEvidenceCandidates = candidates };
    }
}
