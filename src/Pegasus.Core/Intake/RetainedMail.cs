using Pegasus.Core.Actors;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

/// <summary>
/// The folder a retained message was read from. Only <see cref="Inbox"/> is written
/// today; the other two are declared because the workspace names them as scopes an
/// operator can select, and a scope that cannot be expressed cannot be refused
/// either.
/// </summary>
public enum MailFolderScope
{
    Inbox,
    Sent,
    DeletedItems
}

/// <summary>
/// Which slice of retained mail the operator is looking at. A null
/// <paramref name="MailboxId"/> is the default all-mailboxes view.
/// <paramref name="UnreadOnly"/> is the Unread scope; <paramref name="OldestFirst"/>
/// is the list's sort toggle (newest first by default).
/// </summary>
public sealed record MailWorkspaceScope(
    Guid? MailboxId,
    MailFolderScope Folder,
    string? SearchTerm = null,
    MailOperationalDestination? Destination = null,
    MailCategory? DetailedClassification = null,
    bool UnreadOnly = false,
    bool OldestFirst = false);

public enum MailSearchMatchKind
{
    MessageBody,
    AttachmentFileName,
    AttachmentContent
}

public sealed record RetainedMailSearchMatch(
    MailSearchMatchKind Kind,
    string? AttachmentFileName = null,
    int? AttachmentOrdinal = null);

public sealed record RetainedMailSummary(
    Guid Id,
    Guid MailboxId,
    string MailboxAddress,
    bool MailboxIsPolled,
    string? SenderAddress,
    string? SenderDisplayName,
    string? EffectiveSenderAddress,
    string? Subject,
    string? BodyExcerpt,
    DateTimeOffset ReceivedAtUtc,
    bool IsRead,
    int AttachmentCount,
    IntakeDecision? ProcessingOutcome,
    Guid? IntakeReceiptId,
    Guid? CaseId,
    string? CaseReference,
    IntakeAllocationState? AllocationState = null,
    IReadOnlyList<RetainedMailSearchMatch>? SearchMatches = null,
    MailLogicalFolderType? CurrentFolderType = null,
    MailClassificationResult? Classification = null,
    MailOperationalDestinationResult? OperationalDestination = null)
{
    public IReadOnlyList<RetainedMailSearchMatch> Matches => SearchMatches ?? [];
}

/// <summary>
/// One page of retained mail.
/// </summary>
/// <param name="HasUnretainedHistory">
/// True where a mailbox has completed a poll but the retained read model holds
/// nothing for the selected scope. Message-level retention starts from the tick
/// that first wrote it: everything polled before then produced a receipt and an
/// artifact but no retained row, and no backfill reconstructs it. The workspace
/// says so rather than presenting an empty list as "nothing was ever received".
/// </param>
public sealed record RetainedMailPage(
    IReadOnlyList<RetainedMailSummary> Items,
    int Page,
    int PageSize,
    int TotalCount,
    bool HasUnretainedHistory)
{
    public int TotalPages => TotalCount == 0
        ? 1
        : (int)Math.Ceiling((double)TotalCount / PageSize);
}

public sealed record RetainedMailCursorPage(
    IReadOnlyList<RetainedMailSummary> Items,
    bool HasMore,
    bool HasUnretainedHistory);

public sealed record RetainedMailAttachment(
    string FileName,
    string MediaType,
    long ContentLength,
    bool IsSearchable = false);

public sealed record RetainedMailThreadEntry(
    Guid Id,
    string? SenderDisplayName,
    string? SenderAddress,
    string? Subject,
    DateTimeOffset ReceivedAtUtc);

/// <summary>
/// The current read-only Outlook-folder recommendation for one retained message.
/// A missing <see cref="FolderType"/> is an honest unavailable result, not a
/// destination the caller may fill in. A later move command must re-read the exact
/// current binding rather than carry an opaque identity forward from this view.
/// </summary>
public sealed record RetainedMailFolderRecommendation(
    MailLogicalFolderType? FolderType,
    string PolicyKey,
    int PolicyVersion,
    string Reason,
    int? MailboxVersion = null,
    bool CanMove = false)
{
    public bool IsAvailable => FolderType is not null;
}

/// <summary>
/// The optional advisory to start the separate confirmed folder-move workflow.
/// It carries no command, transport identity or durable operation state.
/// </summary>
public sealed record RetainedMailSuggestedMove(
    MailLogicalFolderType FolderType,
    string Reason);

public sealed record RetainedMailDetail(
    RetainedMailSummary Summary,
    IReadOnlyList<string> ToAddresses,
    IReadOnlyList<string> CcAddresses,
    string? BodyPlainText,
    IReadOnlyList<RetainedMailAttachment> Attachments,
    IReadOnlyList<RetainedMailThreadEntry> Thread,
    MailFolderScope Folder,
    MailClassificationOutcome? ClassificationOutcome,
    MailRouteDisposition? RouteDisposition,
    string ImmutableMessageId,
    string? InternetMessageId,
    string? ConversationId,
    MailClassificationDossier? Classification = null,
    RetainedMailFolderRecommendation? FolderRecommendation = null,
    RetainedMailFolderMoveResult? LatestFolderMove = null,
    RetainedMailSuggestedMove? SuggestedMove = null);

public sealed record MailClassificationHistoryEntry(
    int Version,
    MailClassificationResult Before,
    MailClassificationResult After,
    string Actor,
    string Reason,
    DateTimeOffset CorrectedAtUtc)
{
    /// <summary>
    /// The operator-facing name for <see cref="Actor"/> — a persisted
    /// <c>"{kind}:{subjectId}"</c> pair, see <see cref="MailClassificationActor"/> —
    /// resolved by <c>GetRetainedMail</c>. Defaults to the same honest fallback an
    /// unresolvable actor gets, so a caller that forgets to populate it never
    /// renders the raw subject id.
    /// </summary>
    public string ActorDisplayName { get; init; } = ActorDisplayNames.UnknownStaff;
}

public sealed record MailClassificationDossier(
    int Version,
    MailClassificationResult Current,
    string CurrentActor,
    DateTimeOffset CurrentDecidedAtUtc,
    IReadOnlyList<MailClassificationHistoryEntry> History)
{
    /// <summary>The operator-facing name for <see cref="CurrentActor"/>.</summary>
    public string CurrentActorDisplayName { get; init; } = ActorDisplayNames.UnknownStaff;
}

public sealed record CorrectMailClassificationRequest(
    Guid MessageId,
    int ExpectedVersion,
    MailCategory Category,
    string Reason);

public interface IRetainedMailClassificationStore
{
    Task<MailClassificationDossier?> GetClassificationAsync(
        Guid messageId,
        CancellationToken cancellationToken);

    Task<MailClassificationDossier> AppendCorrectionAsync(
        Guid messageId,
        int expectedVersion,
        MailClassificationResult before,
        MailClassificationResult after,
        string actor,
        string reason,
        DateTimeOffset correctedAtUtc,
        CancellationToken cancellationToken);
}

public sealed class MailClassificationConcurrencyException()
    : InvalidOperationException("The classification changed after this message was opened. Reload it before correcting it.");

/// <summary>
/// The single format for the actor persisted alongside a mail classification
/// correction: <c>"{kind}:{subjectId}"</c>, lowercase kind. There is no dedicated
/// actor column for this history (unlike <c>CaseWorkflowEvents</c> or Triage
/// history), so the pair is packed into the one <c>Actor</c> string the store
/// writes; this is the sole place that packs and unpacks it.
/// </summary>
public static class MailClassificationActor
{
    /// <summary>
    /// The kind prefixes as the rest of the codebase already writes them
    /// (<c>"staff:"</c> in <c>Pages/Upload.cshtml.cs</c>, <c>"automation:"</c> in
    /// <c>Mcp/IntakeMcpTools.cs</c>, <c>"system-worker:"</c> throughout Intake and
    /// Triage — including the pre-PLAT-011 rows this migrates,
    /// e.g. <c>"system-worker:legacy-intake"</c>). <see cref="ActorKind"/>'s own
    /// <c>ToString()</c> does not hyphenate <c>SystemWorker</c>, so this map, not
    /// the enum name, is the source of truth for the prefix.
    /// </summary>
    private static readonly Dictionary<ActorKind, string> Prefixes = new()
    {
        [ActorKind.Staff] = "staff",
        [ActorKind.SystemWorker] = "system-worker",
        [ActorKind.Automation] = "automation",
        [ActorKind.RequestLink] = "request-link",
        [ActorKind.Provider] = "provider"
    };

    public static string Format(ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return $"{Prefixes[actor.Kind]}:{actor.SubjectId}";
    }

    public static bool TryParse(string value, out ActorKind kind, out string subjectId)
    {
        kind = default;
        subjectId = string.Empty;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var separator = value.IndexOf(':');
        if (separator <= 0 || separator == value.Length - 1)
        {
            return false;
        }

        var prefix = value[..separator];
        foreach (var (candidateKind, candidatePrefix) in Prefixes)
        {
            if (!string.Equals(prefix, candidatePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            kind = candidateKind;
            subjectId = value[(separator + 1)..];
            return true;
        }

        return false;
    }
}

/// <summary>
/// The sole business operation for correcting one retained message. Persistence owns
/// the transaction; this use case owns authorization, validation and the decision that
/// a correction preserves the policy/evidence which produced the prior result.
/// </summary>
public sealed class CorrectRetainedMailClassification(
    IRetainedMailClassificationStore store,
    TimeProvider timeProvider)
{
    private readonly IRetainedMailClassificationStore store =
        store ?? throw new ArgumentNullException(nameof(store));
    private readonly TimeProvider timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public async Task<MailClassificationDossier?> ExecuteAsync(
        ActionActor actor,
        CorrectMailClassificationRequest request,
        CancellationToken cancellationToken = default)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Category);
        request.Category.ValidateCanonical();
        if (request.MessageId == Guid.Empty)
        {
            throw new ArgumentException("A retained message identifier is required.", nameof(request));
        }
        if (request.ExpectedVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "A positive classification version is required.");
        }
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
        {
            throw new ArgumentException("A correction reason of 1 to 500 characters is required.", nameof(request));
        }

        var current = await store.GetClassificationAsync(request.MessageId, cancellationToken);
        if (current is null)
        {
            return null;
        }
        if (current.Version != request.ExpectedVersion)
        {
            throw new MailClassificationConcurrencyException();
        }

        var after = MailClassificationResult.Classified(
            request.Category,
            current.Current.Predicates,
            reason,
            current.Current.PolicyKey,
            current.Current.PolicyVersion,
            current.Current.Category == request.Category ? current.Current.CaseType : null,
            current.Current.Category == request.Category ? current.Current.StandaloneAuditReport : null);
        return await store.AppendCorrectionAsync(
            request.MessageId,
            request.ExpectedVersion,
            current.Current,
            after,
            MailClassificationActor.Format(actor),
            reason,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }
}

/// <summary>
/// One mailbox the workspace can scope to, and whether the estate still polls it.
/// </summary>
/// <remarks>
/// Deliberately not read through <c>ListApprovedMailboxes</c>: that use case
/// requires <see cref="StaffAccessRight.ManageApprovedMailboxes"/>, which a
/// caseworker does not hold, and the workspace is a casework surface. What the
/// tabs need is the set of mailboxes that actually have retained mail, which the
/// read model already knows.
/// </remarks>
public sealed record RetainedMailMailbox(
    Guid MailboxId,
    string MailboxAddress,
    bool IsPolled);

public enum MailFreshnessState
{
    Current,
    Stale,
    Unavailable
}

public sealed record MailFreshness(
    MailFreshnessState State,
    DateTimeOffset? LastSuccessfulUpdateAtUtc);

/// <summary>
/// What inbound polling has managed for one mailbox, as the read model sees it.
/// Raw facts only: turning them into a freshness state is policy and belongs to
/// <see cref="GetRetainedMailFreshness"/>.
/// </summary>
public sealed record MailPollHealth(
    Guid MailboxId,
    DateTimeOffset? LastCompletedAtUtc,
    string? LastFailureCode,
    DateTimeOffset DueAtUtc);

public interface IRetainedMailQueries
{
    Task<RetainedMailPage> ListAsync(
        MailWorkspaceScope scope,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<RetainedMailCursorPage> ListByCursorAsync(
        MailWorkspaceScope scope,
        DateTimeOffset? beforeReceivedAtUtc,
        Guid? beforeId,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>How many retained messages the scope holds — the scope list's count wells.</summary>
    Task<int> CountAsync(
        MailWorkspaceScope scope,
        CancellationToken cancellationToken);

    Task<RetainedMailDetail?> GetAsync(
        Guid id,
        CancellationToken cancellationToken,
        string? searchTerm = null);

    Task<IReadOnlyList<RetainedMailMailbox>> ListMailboxesAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MailPollHealth>> ListPollHealthAsync(
        CancellationToken cancellationToken);
}

public sealed class ListRetainedMail(IRetainedMailQueries queries)
{
    private readonly IRetainedMailQueries queries =
        queries ?? throw new ArgumentNullException(nameof(queries));

    public async Task<RetainedMailPage> ExecuteAsync(
        ActionActor actor,
        MailWorkspaceScope scope,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (page is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(page),
                "The requested page is outside the supported range.");
        }
        if (pageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                "The requested page size is outside the supported range.");
        }
        return await queries.ListAsync(Normalize(scope), page, pageSize, cancellationToken);
    }

    public async Task<int> CountAsync(
        ActionActor actor,
        MailWorkspaceScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        return await queries.CountAsync(Normalize(scope), cancellationToken);
    }

    public async Task<RetainedMailCursorPage> ExecuteCursorAsync(
        ActionActor actor,
        MailWorkspaceScope scope,
        DateTimeOffset? beforeReceivedAtUtc,
        Guid? beforeId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if ((beforeReceivedAtUtc is null) != (beforeId is null))
        {
            throw new ArgumentException("Both retained-mail cursor values are required together.");
        }
        if (limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }
        return await queries.ListByCursorAsync(
            Normalize(scope), beforeReceivedAtUtc, beforeId, limit, cancellationToken);
    }

    private static MailWorkspaceScope Normalize(MailWorkspaceScope scope)
    {
        if (!Enum.IsDefined(scope.Folder))
        {
            throw new ArgumentOutOfRangeException(
                nameof(scope),
                "The mail folder scope is not recognized.");
        }
        if (scope.Destination is not null && scope.DetailedClassification is not null)
        {
            throw new ArgumentException(
                "Choose either an operational destination or one detailed classification.",
                nameof(scope));
        }
        if (scope.Destination is { } destination)
        {
            _ = MailOperationalDestinationPolicy.Query(destination);
        }
        if (scope.DetailedClassification is { } detailedClassification)
        {
            detailedClassification.ValidateCanonical();
            if (MailOperationalDestinationPolicy.Map(detailedClassification).Destination
                != MailOperationalDestination.DetailedClassification)
            {
                throw new ArgumentException(
                    "The selected classification does not have its own detailed mail view.",
                    nameof(scope));
            }
        }
        var searchTerm = NormalizeSearchTerm(scope.SearchTerm, nameof(scope));
        if (scope.MailboxId == Guid.Empty)
        {
            throw new ArgumentException("The mailbox identity is required.", nameof(scope));
        }

        return scope with
        {
            SearchTerm = searchTerm
        };
    }

    internal static string? NormalizeSearchTerm(string? value, string parameterName)
    {
        if (value is null)
        {
            return null;
        }
        var term = value.Trim();
        if (term.Length is 0 or > 200)
        {
            throw new ArgumentException(
                "A mail search term must contain 1 to 200 characters.",
                parameterName);
        }
        return term;
    }

    public Task<IReadOnlyList<RetainedMailMailbox>> ListMailboxesAsync(
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        return queries.ListMailboxesAsync(cancellationToken);
    }
}

public sealed class GetRetainedMail(
    IRetainedMailQueries queries,
    IStaffAccountQueries staffAccountQueries,
    IApprovedMailboxStore approvedMailboxStore,
    IRetainedMailFolderMoveStore? folderMoveStore = null,
    IRetainedMailFolderMover? folderMover = null)
{
    private readonly IRetainedMailQueries queries =
        queries ?? throw new ArgumentNullException(nameof(queries));
    private readonly IStaffAccountQueries staffAccountQueries =
        staffAccountQueries ?? throw new ArgumentNullException(nameof(staffAccountQueries));
    private readonly IApprovedMailboxStore approvedMailboxStore =
        approvedMailboxStore ?? throw new ArgumentNullException(nameof(approvedMailboxStore));
    private readonly IRetainedMailFolderMoveStore folderMoveStore =
        folderMoveStore ?? EmptyRetainedMailFolderMoveStore.Instance;
    private readonly IRetainedMailFolderMover? folderMover = folderMover;

    public async Task<RetainedMailDetail?> ExecuteAsync(
        ActionActor actor,
        Guid messageId,
        CancellationToken cancellationToken = default)
        => await ExecuteAsync(actor, messageId, searchTerm: null, cancellationToken);

    public async Task<RetainedMailDetail?> ExecuteAsync(
        ActionActor actor,
        Guid messageId,
        string? searchTerm,
        CancellationToken cancellationToken = default)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (messageId == Guid.Empty)
        {
            throw new ArgumentException(
                "A retained message identifier is required.",
                nameof(messageId));
        }

        var normalizedSearchTerm = ListRetainedMail.NormalizeSearchTerm(
            searchTerm,
            nameof(searchTerm));
        var detail = await queries.GetAsync(
            messageId,
            cancellationToken,
            normalizedSearchTerm);
        if (detail is null)
        {
            return null;
        }

        var recommendation = await RecommendFolderAsync(detail, cancellationToken);
        var latestMove = await folderMoveStore.GetLatestAsync(messageId, cancellationToken);
        detail = detail with
        {
            FolderRecommendation = recommendation,
            LatestFolderMove = latestMove,
            SuggestedMove = recommendation is { CanMove: true, FolderType: { } folderType }
                && latestMove?.Outcome is not RetainedMailFolderMoveOutcome.Uncertain
                ? new(folderType, recommendation.Reason)
                : null
        };
        if (detail.Classification is not { } dossier)
        {
            return detail;
        }

        var packedActors = new[] { dossier.CurrentActor }
            .Concat(dossier.History.Select(entry => entry.Actor));
        var staffIds = packedActors.Select(TryParseStaffId).OfType<Guid>();
        var staffNames = await ActorDisplayNames.ResolveStaffNamesAsync(
            staffAccountQueries,
            staffIds,
            cancellationToken);

        return detail with
        {
            Classification = dossier with
            {
                CurrentActorDisplayName = ResolveActorLabel(dossier.CurrentActor, staffNames),
                History = dossier.History
                    .Select(entry => entry with
                    {
                        ActorDisplayName = ResolveActorLabel(entry.Actor, staffNames)
                    })
                    .ToArray()
            }
        };
    }

    private async Task<RetainedMailFolderRecommendation> RecommendFolderAsync(
        RetainedMailDetail detail,
        CancellationToken cancellationToken)
    {
        if (detail.Classification is not { } dossier)
        {
            return Unavailable(
                null,
                "This message has no current classification decision, so no Outlook folder can be recommended.");
        }

        var policy = MailLogicalFolderPolicy.Map(dossier.Current);
        if (policy.FolderType is not { } folderType)
        {
            return Unavailable(policy, policy.Reason);
        }

        var mailboxes = await approvedMailboxStore.ListAsync(cancellationToken);
        var mailbox = mailboxes.SingleOrDefault(item => item.Id == detail.Summary.MailboxId);
        if (mailbox is null || mailbox.State != ApprovedMailboxState.Approved)
        {
            return Unavailable(
                policy,
                "This message's mailbox is not currently approved, so its designated Outlook folder is unavailable.");
        }

        var binding = mailbox.FolderBindings.SingleOrDefault(item => item.FolderType == folderType);
        if (binding is null)
        {
            var label = MailLogicalFolders.Definition(folderType).Label;
            return Unavailable(
                policy,
                $"The designated {label} folder is not configured for this mailbox.");
        }

        var isCurrentLocation = await folderMoveStore.IsCurrentLocationAsync(
            detail.Summary.Id,
            binding.FolderIdentity,
            cancellationToken);
        return new(
            folderType,
            policy.PolicyKey,
            policy.PolicyVersion,
            policy.Reason,
            mailbox.Version,
            folderMover?.IsAvailable == true && !isCurrentLocation);
    }

    private static RetainedMailFolderRecommendation Unavailable(
        MailLogicalFolderResult? policy,
        string reason) => new(
            null,
            policy?.PolicyKey ?? MailLogicalFolderPolicy.Key,
            policy?.PolicyVersion ?? MailLogicalFolderPolicy.Version,
            reason);

    private static string ResolveActorLabel(
        string packedActor,
        IReadOnlyDictionary<Guid, string> staffNames) =>
        MailClassificationActor.TryParse(packedActor, out var kind, out var subjectId)
            ? ActorDisplayNames.Resolve(kind, subjectId, staffNames)
            : ActorDisplayNames.UnknownStaff;

    private static Guid? TryParseStaffId(string packedActor) =>
        MailClassificationActor.TryParse(packedActor, out var kind, out var subjectId)
            && kind == ActorKind.Staff
            && Guid.TryParse(subjectId, out var staffId)
                ? staffId
                : null;
}

public sealed class GetRetainedMailFreshness(
    IRetainedMailQueries queries,
    TimeProvider timeProvider)
{
    private readonly IRetainedMailQueries queries =
        queries ?? throw new ArgumentNullException(nameof(queries));
    private readonly TimeProvider timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>
    /// How long after the last successful poll the workspace stops calling its data
    /// current.
    /// </summary>
    /// <remarks>
    /// PROVISIONAL. Graph change notifications are the primary wake; the recovery
    /// poll (<c>InboxRecoveryFunction</c>, <c>ApprovedInboxPollSchedule</c>) runs
    /// every five minutes, so fifteen minutes is three consecutive missed recovery
    /// ticks — long enough that a single slow or skipped run never shows a chip,
    /// short enough that a stopped Worker is visible within a quarter of an hour.
    /// No operator statement fixes this number; it is recorded
    /// as open in docs/open-decisions.md and moves when observed behaviour, not
    /// taste, says it should.
    /// </remarks>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);

    public async Task<MailFreshness> ExecuteAsync(
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        var health = await queries.ListPollHealthAsync(cancellationToken);
        var nowUtc = timeProvider.GetUtcNow();
        return Evaluate(health, nowUtc);
    }

    /// <summary>
    /// Unavailable means the workspace cannot say anything true about how current it
    /// is: either nothing has ever polled, or every mailbox is sitting on a recorded
    /// failure and backing off. Anything else reports the newest successful poll and
    /// is stale once that is older than <see cref="StaleAfter"/>.
    /// </summary>
    public static MailFreshness Evaluate(
        IReadOnlyList<MailPollHealth> health,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(health);
        if (health.Count == 0)
        {
            return new(MailFreshnessState.Unavailable, null);
        }

        var lastCompleted = health
            .Select(item => item.LastCompletedAtUtc)
            .Where(item => item is not null)
            .DefaultIfEmpty(null)
            .Max();
        if (health.All(item => item.LastFailureCode is not null && item.DueAtUtc > nowUtc))
        {
            return new(MailFreshnessState.Unavailable, lastCompleted);
        }

        if (lastCompleted is not { } completedAtUtc)
        {
            return new(MailFreshnessState.Unavailable, null);
        }

        return new(
            nowUtc - completedAtUtc > StaleAfter
                ? MailFreshnessState.Stale
                : MailFreshnessState.Current,
            completedAtUtc);
    }
}
