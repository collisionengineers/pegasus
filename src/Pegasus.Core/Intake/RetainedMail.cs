using Pegasus.Core.Actors;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Notifications;

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
    DeletedItems,

    /// <summary>
    /// An email a member of staff uploaded rather than one a mailbox poll
    /// brought in. It has no mailbox folder; the mailbox workspace leaves the
    /// scope out and the Case's Correspondence tab is where it is read.
    /// </summary>
    Upload
}

/// <summary>
/// Which slice of retained mail the operator is looking at. A null
/// <paramref name="MailboxId"/> is the default all-mailboxes view.
/// <paramref name="UnreadOnly"/> is the Unread scope; <paramref name="OldestFirst"/>
/// is the list's sort toggle (newest first by default). <paramref name="DismissedOnly"/>
/// is the Dismissed scope: dismissed messages appear there and nowhere else.
/// </summary>
public sealed record MailWorkspaceScope(
    Guid? MailboxId,
    MailFolderScope Folder,
    string? SearchTerm = null,
    MailOperationalDestination? Destination = null,
    ReceivedMailFamily? Family = null,
    bool UnreadOnly = false,
    bool OldestFirst = false,
    bool DismissedOnly = false);

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
    MailClassificationResult? Classification = null,
    MailOperationalDestinationResult? OperationalDestination = null)
{
    public IReadOnlyList<RetainedMailSearchMatch> Matches => SearchMatches ?? [];

    /// <summary>When the message was dismissed from the incoming scopes; null while it is not.</summary>
    public DateTimeOffset? DismissedAtUtc { get; init; }

    /// <summary>True once the receipt's Unidentified item has resolved; the message has left the Inbox Unidentified scope.</summary>
    public bool UnidentifiedResolved { get; init; }

    /// <summary>
    /// True when <see cref="CaseId"/> is a Triage Case by its own type, whether
    /// the message opened it or staff linked the message to it. Then
    /// <see cref="CaseReference"/> is its <c>t.</c> reference and the workspace
    /// offers Open Triage rather than Open Case.
    /// </summary>
    public bool IsTriageCase { get; init; }
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
    bool IsSearchable = false,
    Guid? IntakeAssetId = null);

public sealed record RetainedMailThreadEntry(
    Guid Id,
    string? SenderDisplayName,
    string? SenderAddress,
    string? Subject,
    DateTimeOffset ReceivedAtUtc);

public sealed record RetainedMailDetail(
    RetainedMailSummary Summary,
    IReadOnlyList<string> ToAddresses,
    IReadOnlyList<string> CcAddresses,
    IReadOnlyList<string>? ReplyToAddresses,
    string? BodyPlainText,
    IReadOnlyList<RetainedMailAttachment> Attachments,
    IReadOnlyList<RetainedMailThreadEntry> Thread,
    MailFolderScope Folder,
    MailClassificationOutcome? ClassificationOutcome,
    MailRouteDisposition? RouteDisposition,
    string ImmutableMessageId,
    string? InternetMessageId,
    string? ConversationId,
    MailClassificationDossier? Classification = null);

/// <summary>
/// What the Inbox preview pane renders of one message: its summary, the
/// attachments it counts by kind, the classification decision it names and the
/// folder scope it was retained under. The thread, body and recipients belong to
/// the full message and are not read.
/// </summary>
public sealed record RetainedMailPreview(
    RetainedMailSummary Summary,
    IReadOnlyList<RetainedMailAttachment> Attachments,
    MailClassificationResult? Classification,
    MailFolderScope Folder);

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
    string Reason,
    CaseType? CaseType = null);

/// <summary>
/// What one correction did: the dossier it produced, the Case the message is
/// currently linked to (null when none, or a Triage Case), and whether the
/// correction moved that Case into Query or withdrew its query.
/// </summary>
public sealed record MailClassificationCorrectionResult(
    MailClassificationDossier Dossier,
    Guid? LinkedCaseId,
    PostReportQueryEntry QueryEntry,
    bool QueryWithdrawn);

public interface IRetainedMailClassificationStore
{
    Task<MailClassificationDossier?> GetClassificationAsync(
        Guid messageId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Appends the correction and, in the same transaction, applies the Query
    /// rule to the linked Case when the correction enters or leaves Post-report.
    /// </summary>
    Task<MailClassificationCorrectionResult> AppendCorrectionAsync(
        Guid messageId,
        int expectedVersion,
        MailClassificationResult before,
        MailClassificationResult after,
        ActionActor actor,
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
    /// Triage — including the rows written before actor names were resolved,
    /// e.g. <c>"system-worker:legacy-intake"</c>). <see cref="ActorKind"/>'s own
    /// <c>ToString()</c> does not hyphenate <c>SystemWorker</c>, so this map, not
    /// the enum name, is the source of truth for the prefix.
    /// </summary>
    private static readonly Dictionary<ActorKind, string> Prefixes = new()
    {
        [ActorKind.Staff] = "staff",
        [ActorKind.SystemWorker] = "system-worker",
        [ActorKind.Automation] = "automation",
        [ActorKind.Principal] = "principal"
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
    TimeProvider timeProvider,
    ICaseStaffNotifier? notifier = null)
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

        var caseType = WorkTypeFor(request);

        var current = await store.GetClassificationAsync(request.MessageId, cancellationToken);
        if (current is null)
        {
            return null;
        }
        if (current.Version != request.ExpectedVersion)
        {
            throw new MailClassificationConcurrencyException();
        }

        var sameCategory = current.Current.Category == request.Category;
        var after = MailClassificationResult.Classified(
            request.Category,
            current.Current.Predicates,
            reason,
            current.Current.PolicyKey,
            current.Current.PolicyVersion,
            caseType,
            sameCategory ? current.Current.StandaloneAuditReport : null);
        var result = await store.AppendCorrectionAsync(
            request.MessageId,
            request.ExpectedVersion,
            current.Current,
            after,
            actor,
            reason,
            timeProvider.GetUtcNow(),
            cancellationToken);
        await NotifyAsync(result, current.Current, after, actor, cancellationToken);
        return result.Dossier;
    }

    // Raised after the correction committed (FRD-12): the Case's engineer
    // learns a query entered their Case, or that a message on it is now a
    // cancellation, as they would had it been linked that way.
    private async Task NotifyAsync(
        MailClassificationCorrectionResult result,
        MailClassificationResult before,
        MailClassificationResult after,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        if (notifier is null || result.LinkedCaseId is not { } caseId)
        {
            return;
        }
        if (result.QueryEntry == PostReportQueryEntry.EnterQuery)
        {
            await notifier.NotifyAsync(
                StaffNotificationCause.QueryReceived, caseId, actor, "correspondence", null, cancellationToken);
        }
        if (after.Category is { IsCancellation: true } && before.Category is not { IsCancellation: true })
        {
            await notifier.NotifyCancellationReceivedAsync(caseId, actor, cancellationToken);
        }
    }

    // A New instruction names the work it instructs; nothing else carries one.
    private static CaseType? WorkTypeFor(CorrectMailClassificationRequest request)
    {
        if (!request.Category.IsNewInstruction)
        {
            return request.CaseType is null
                ? null
                : throw new ArgumentException("Only a New instruction classification carries a case type.", nameof(request));
        }
        return request.CaseType switch
        {
            CaseType.Inspection or CaseType.Audit or CaseType.InspectionAndAudit => request.CaseType,
            null => throw new ArgumentException(
                "A New instruction classification requires a case type: Inspection, Audit or Inspection and Audit.",
                nameof(request)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(request), "A Triage is not a case type a New instruction can carry.")
        };
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

    Task<IReadOnlyList<int>> CountManyAsync(
        IReadOnlyList<MailWorkspaceScope> scopes,
        CancellationToken cancellationToken);

    Task<RetainedMailDetail?> GetAsync(
        Guid id,
        CancellationToken cancellationToken,
        string? searchTerm = null);

    /// <summary>
    /// The preview pane's read: the attachments, the classification decision and
    /// the folder scope of one message. <paramref name="summary"/> is the row the
    /// caller already holds from a list read, which keeps its search matches; without
    /// one the store reads the message's own summary.
    /// </summary>
    Task<RetainedMailPreview?> GetPreviewAsync(
        Guid id,
        RetainedMailSummary? summary,
        CancellationToken cancellationToken);

    Task<RetainedMailDetail?> GetByOriginReceiptAsync(
        Guid originReceiptId,
        CancellationToken cancellationToken);

    /// <summary>
    /// The identifier of the retained message an origin receipt came from, or
    /// null when it has none. A store that can answer without loading the
    /// message overrides this; the default reads the whole detail.
    /// </summary>
    async Task<Guid?> FindIdByOriginReceiptAsync(
        Guid originReceiptId,
        CancellationToken cancellationToken) =>
        (await GetByOriginReceiptAsync(originReceiptId, cancellationToken))?.Summary.Id;

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

    public async Task<IReadOnlyList<int>> CountManyAsync(
        ActionActor actor,
        IReadOnlyList<MailWorkspaceScope> scopes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        return await queries.CountManyAsync(
            scopes.Select(scope => Normalize(scope)).ToArray(),
            cancellationToken);
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
        if (scope.Destination is not null && scope.Family is not null)
        {
            throw new ArgumentException(
                "Choose either an operational destination or one classification family.",
                nameof(scope));
        }
        if (scope.Destination is { } destination)
        {
            _ = MailOperationalDestinationPolicy.Query(destination);
        }
        if (scope.Family is { } family && !Enum.IsDefined(family))
        {
            throw new ArgumentOutOfRangeException(
                nameof(scope),
                "The classification family is not recognized.");
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
    IStaffAccountQueries staffAccountQueries)
{
    private readonly IRetainedMailQueries queries =
        queries ?? throw new ArgumentNullException(nameof(queries));
    private readonly IStaffAccountQueries staffAccountQueries =
        staffAccountQueries ?? throw new ArgumentNullException(nameof(staffAccountQueries));

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
        return await CompleteAsync(detail, cancellationToken);
    }

    public async Task<RetainedMailDetail?> ExecuteByOriginReceiptAsync(
        ActionActor actor,
        Guid originReceiptId,
        CancellationToken cancellationToken = default)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (originReceiptId == Guid.Empty)
        {
            throw new ArgumentException(
                "An origin receipt identifier is required.",
                nameof(originReceiptId));
        }

        var detail = await queries.GetByOriginReceiptAsync(originReceiptId, cancellationToken);
        return await CompleteAsync(detail, cancellationToken);
    }

    /// <summary>
    /// Only the identifier of the message an origin receipt came from, for a
    /// caller that links to it and needs nothing else of the detail.
    /// </summary>
    public Task<Guid?> FindIdByOriginReceiptAsync(
        ActionActor actor,
        Guid originReceiptId,
        CancellationToken cancellationToken = default)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (originReceiptId == Guid.Empty)
        {
            throw new ArgumentException(
                "An origin receipt identifier is required.",
                nameof(originReceiptId));
        }

        return queries.FindIdByOriginReceiptAsync(originReceiptId, cancellationToken);
    }

    private async Task<RetainedMailDetail?> CompleteAsync(
        RetainedMailDetail? detail,
        CancellationToken cancellationToken)
    {
        if (detail is null)
        {
            return null;
        }

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

/// <summary>
/// The Inbox preview pane's read, with the same actor boundary and identifier
/// validation as <see cref="GetRetainedMail"/> and none of what only the full
/// message page renders.
/// </summary>
public sealed class GetRetainedMailPreview(IRetainedMailQueries queries)
{
    private readonly IRetainedMailQueries queries =
        queries ?? throw new ArgumentNullException(nameof(queries));

    public Task<RetainedMailPreview?> ExecuteAsync(
        ActionActor actor,
        Guid messageId,
        RetainedMailSummary? summary = null,
        CancellationToken cancellationToken = default)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (messageId == Guid.Empty)
        {
            throw new ArgumentException(
                "A retained message identifier is required.",
                nameof(messageId));
        }
        if (summary is not null && summary.Id != messageId)
        {
            throw new ArgumentException(
                "The summary belongs to another retained message.",
                nameof(summary));
        }

        return queries.GetPreviewAsync(messageId, summary, cancellationToken);
    }
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
    /// poll (run by <c>PendingWorkRecoveryFunction</c> on every fifth minute) runs
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
