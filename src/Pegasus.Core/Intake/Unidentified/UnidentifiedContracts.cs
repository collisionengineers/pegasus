using System.Globalization;
using System.Text.RegularExpressions;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Triage;

namespace Pegasus.Core.Intake.Unidentified;

/// <summary>One canonical reason taxonomy for safely retained unidentified material.</summary>
public enum UnidentifiedReasonCode
{
    UnreadableOrCorruptContent,
    UnsupportedContent,
    NoUsableIdentification,
    ConflictingIdentification,
    AmbiguousOwnershipOrDestination,
    TechnicalProcessingFailure
}

public enum UnidentifiedState
{
    Open,
    Resolved
}

public enum UnidentifiedOriginKind
{
    Receipt,
    SubmissionGroup
}

public enum UnidentifiedResolutionTargetKind
{
    InstructionCase,
    ImageIntake,
    Triage,
    BlockedIntake,
    ExternalReference
}

/// <summary>
/// What kind of retained material an Unidentified item concerns, for the
/// Queues page's Images/E-mails filter. Not persisted: derived at read time
/// from the origin receipt's source channel and content type by
/// <see cref="UnidentifiedMediaKindPolicy"/>.
/// </summary>
public enum UnidentifiedMediaKind
{
    Image,
    Email,
    Document
}

/// <summary>
/// Classifies retained material by what an operator would call it, from the
/// same channel/media-type vocabulary <see cref="IntakeSourceChannel"/> and
/// <c>IntakeReceipt.MediaType</c> already carry. One rule, so the Unidentified
/// queue row and the Unidentified detail page can never classify the same
/// receipt two different ways.
/// </summary>
public static class UnidentifiedMediaKindPolicy
{
    public static UnidentifiedMediaKind Classify(IntakeSourceChannel channel, string mediaType)
    {
        // A mailbox-channel receipt is a received e-mail, whatever its
        // content type happens to be (the message itself, not an attachment).
        if (channel == IntakeSourceChannel.Mailbox)
        {
            return UnidentifiedMediaKind.Email;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        return mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            ? UnidentifiedMediaKind.Image
            : UnidentifiedMediaKind.Document;
    }

    /// <summary>
    /// As <see cref="Classify(IntakeSourceChannel, string)"/>, for material
    /// with no origin receipt to read a channel or content type from.
    /// INTK-007's grouped-VRM-conflict Unidentified item is the only current
    /// producer of that shape, and it is image material — the fallback lives
    /// here, once, rather than being re-decided at each caller that has no
    /// receipt to classify.
    /// </summary>
    public static UnidentifiedMediaKind Classify(IntakeSourceChannel? channel, string? mediaType) =>
        channel is { } presentChannel
            ? Classify(presentChannel, mediaType ?? string.Empty)
            : UnidentifiedMediaKind.Image;
}

/// <summary>
/// One row of the Unidentified queue tab: enough for an operator to tell what
/// is going on without opening the record. <see cref="FileName"/> is set for
/// an <see cref="UnidentifiedMediaKind.Image"/> or
/// <see cref="UnidentifiedMediaKind.Document"/> row; <see cref="EmailSubject"/>
/// and <see cref="EmailSender"/> are set for an
/// <see cref="UnidentifiedMediaKind.Email"/> row. Never a GUID or an internal
/// origin identifier.
/// </summary>
public sealed record UnidentifiedQueueRow(
    Guid Id,
    string Reference,
    UnidentifiedMediaKind MediaKind,
    string? FileName,
    string? EmailSubject,
    string? EmailSender,
    DateTimeOffset ReceivedAtUtc,
    UnidentifiedReasonCode ReasonCode);

public sealed record UnidentifiedOrigin(UnidentifiedOriginKind Kind, Guid Id)
{
    public static UnidentifiedOrigin Receipt(Guid id) => new(UnidentifiedOriginKind.Receipt, id);

    public static UnidentifiedOrigin SubmissionGroup(Guid id) =>
        new(UnidentifiedOriginKind.SubmissionGroup, id);

    public static void Validate(UnidentifiedOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (origin.Id == Guid.Empty)
        {
            throw new ArgumentException("An Unidentified origin requires a non-empty identifier.", nameof(origin));
        }
        if (!Enum.IsDefined(origin.Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(origin), "The Unidentified origin kind is not recognised.");
        }
    }
}

public static class UnidentifiedReferenceFormat
{
    private static readonly Regex Canonical = new("^U[1-9][0-9]*$", RegexOptions.CultureInvariant);

    public static string Create(long sequence)
    {
        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "An Unidentified sequence must be positive.");
        }

        return $"U{sequence.ToString(CultureInfo.InvariantCulture)}";
    }

    public static bool TryParse(string? value, out long sequence)
    {
        sequence = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        if (!Canonical.IsMatch(candidate)
            || !long.TryParse(candidate.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out sequence)
            || sequence <= 0)
        {
            sequence = 0;
            return false;
        }

        return string.Equals(Create(sequence), candidate, StringComparison.Ordinal);
    }

    public static long Parse(string value) =>
        TryParse(value, out var sequence)
            ? sequence
            : throw new FormatException("The value is not a canonical Unidentified reference.");
}

public sealed record UnidentifiedItem(
    Guid Id,
    long Sequence,
    string Reference,
    UnidentifiedOrigin Origin,
    UnidentifiedReasonCode ReasonCode,
    string SafeDetail,
    UnidentifiedState State,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ResolvedAtUtc,
    ActionActor CreatedBy,
    ActionActor? ResolvedBy,
    string? ResolutionReason,
    UnidentifiedResolutionTargetKind? ResolutionTargetKind,
    string? ResolutionTargetId,
    string? ResolutionTargetReference,
    long Version)
{
    public static void Validate(UnidentifiedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        UnidentifiedOrigin.Validate(item.Origin);
        if (item.Id == Guid.Empty || item.Sequence <= 0 || !string.Equals(
                item.Reference,
                UnidentifiedReferenceFormat.Create(item.Sequence),
                StringComparison.Ordinal))
        {
            throw new ArgumentException("The Unidentified identity is invalid.", nameof(item));
        }
        if (!Enum.IsDefined(item.ReasonCode) || !Enum.IsDefined(item.State))
        {
            throw new ArgumentOutOfRangeException(nameof(item), "The Unidentified state or reason is not recognised.");
        }
        UnidentifiedValidation.RequireDetail(item.SafeDetail);
        if (item.Version < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(item), "The Unidentified version cannot be negative.");
        }
        if (item.State == UnidentifiedState.Open
            && (item.ResolvedAtUtc is not null
                || item.ResolvedBy is not null
                || item.ResolutionReason is not null
                || item.ResolutionTargetKind is not null
                || item.ResolutionTargetId is not null
                || item.ResolutionTargetReference is not null))
        {
            throw new ArgumentException("An open Unidentified item cannot contain resolution fields.", nameof(item));
        }
    }
}

public sealed record UnidentifiedHistoryEntry(
    Guid Id,
    Guid UnidentifiedItemId,
    UnidentifiedState PreviousState,
    UnidentifiedState NewState,
    ActionActor Actor,
    DateTimeOffset OccurredAtUtc,
    string Reason,
    string OperationKey,
    UnidentifiedResolutionTargetKind? TargetKind,
    string? TargetId,
    string? TargetReference);

public sealed record RegisterUnidentifiedRequest(
    UnidentifiedOrigin Origin,
    UnidentifiedReasonCode ReasonCode,
    string SafeDetail,
    ActionActor Actor,
    string OperationKey,
    DateTimeOffset CreatedAtUtc);

public sealed record ResolveUnidentifiedRequest(
    Guid UnidentifiedItemId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    UnidentifiedResolutionTargetKind TargetKind,
    string TargetId,
    string? TargetReference,
    DateTimeOffset ResolvedAtUtc);

public sealed record ReopenUnidentifiedRequest(
    Guid UnidentifiedItemId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    DateTimeOffset ReopenedAtUtc);

public sealed record UnidentifiedRegisterResult(UnidentifiedItem Item, bool IsReplay);

public sealed record UnidentifiedResolveResult(UnidentifiedItem Item, UnidentifiedHistoryEntry History, bool IsReplay);

public sealed record UnidentifiedReopenResult(UnidentifiedItem Item, UnidentifiedHistoryEntry History, bool IsReplay);

public interface IUnidentifiedStore
{
    Task<UnidentifiedRegisterResult> RegisterAsync(
        RegisterUnidentifiedRequest request,
        CancellationToken cancellationToken = default);

    Task<UnidentifiedRegisterResult?> ProbeRegisterReplayAsync(
        RegisterUnidentifiedRequest request,
        CancellationToken cancellationToken = default);

    Task<UnidentifiedResolveResult> ResolveAsync(
        ResolveUnidentifiedRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraws a resolution and returns the item to <c>Open</c>, appending
    /// the <c>Resolved to Open</c> history row; the withdrawn destination stays
    /// on the record. Not a new state and not a deletion.
    ///
    /// Default: in-memory doubles that never reopen anything. The one
    /// production implementation is
    /// <c>Pegasus.Infrastructure.Persistence.EfUnidentifiedStore</c>, which
    /// overrides this and both recheck members.
    /// </summary>
    Task<UnidentifiedReopenResult> ReopenAsync(
        ReopenUnidentifiedRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromException<UnidentifiedReopenResult>(
            new NotSupportedException("Reopening an Unidentified item is not available."));

    Task<UnidentifiedResolveResult?> ProbeResolveReplayAsync(
        ResolveUnidentifiedRequest request,
        CancellationToken cancellationToken = default);

    Task<UnidentifiedItem?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UnidentifiedItem?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>
    /// The item registered for this origin, if one exists, regardless of
    /// state. Backed by the unique OriginKind/OriginId index; used to
    /// reconcile a stale open item once its source receipt reaches a
    /// different, non-Unidentified outcome.
    /// </summary>
    Task<UnidentifiedItem?> GetByOriginAsync(
        UnidentifiedOrigin origin,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UnidentifiedItem>> ListAsync(
        UnidentifiedState? state = UnidentifiedState.Open,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Items this reconciliation itself resolved whose origin receipt's manual
    /// case association has moved on from the version the recorded destination
    /// was last reconciled against: the only rows whose destination can have
    /// gone stale. This is a freshness filter, not a destination decision;
    /// <see cref="ReconcileUnidentifiedDestinations"/> still owns what the
    /// effective destination is.
    ///
    /// Default: an empty page. An in-memory double has no recheck queue - it
    /// keeps no manual-association versions and no reconciliation watermark, so
    /// it can never say which of its rows have gone stale, and an empty page is
    /// the honest answer rather than a fabricated one. The sweep therefore
    /// still runs its open loop against such a double. The one production
    /// implementation,
    /// <c>Pegasus.Infrastructure.Persistence.EfUnidentifiedStore</c>, overrides
    /// this, <see cref="MarkResolutionRecheckedAsync"/> and
    /// <see cref="ReopenAsync"/>.
    /// </summary>
    Task<IReadOnlyList<UnidentifiedItem>> ListResolutionsToRecheckAsync(
        int maximum,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UnidentifiedItem>>([]);

    /// <summary>
    /// Records that this reconciliation examined the item's recorded
    /// destination against <paramref name="associationVersion"/>, completing a
    /// recheck. A recheck that finds the destination unchanged writes nothing
    /// else, so without this the row satisfies
    /// <see cref="ListResolutionsToRecheckAsync"/> on every pass; holding the
    /// head of that bounded, oldest-first page, enough such rows starve every
    /// later stale resolution of a recheck entirely. The version recorded is
    /// the one the caller observed, never "now", so an association that moves
    /// during the pass is picked up next time rather than marked reconciled
    /// unseen.
    ///
    /// Default: unsupported, for the same reason
    /// <see cref="ListResolutionsToRecheckAsync"/> defaults to empty - a double
    /// with no recheck queue has no watermark to write.
    /// </summary>
    Task MarkResolutionRecheckedAsync(
        Guid unidentifiedItemId,
        long associationVersion,
        CancellationToken cancellationToken = default) =>
        Task.FromException(
            new NotSupportedException("Recording an Unidentified resolution recheck is not available."));

    /// <summary>
    /// One keyset page of the Unidentified queue: strictly after
    /// <paramref name="after"/> in (CreatedAtUtc, Id) order, or from the head
    /// when it is null. Oldest-first, as the queue itself is.
    ///
    /// The queue moves constantly — every processing pass can register a row
    /// and every sweep can resolve one — so an offset page is exactly where a
    /// connector loses rows without knowing it. The sort key plus the id names
    /// a row rather than a position.
    ///
    /// Default: unsupported. An in-memory double has no stable ordering to
    /// continue from, and inventing one would make a test pass over a
    /// continuation the real store might not produce.
    /// </summary>
    Task<KeysetPage<UnidentifiedQueueRow>> ListQueueByCursorAsync(
        UnidentifiedMediaKind? mediaKind,
        KeysetPosition? after,
        int limit,
        CancellationToken cancellationToken = default) =>
        Task.FromException<KeysetPage<UnidentifiedQueueRow>>(
            new NotSupportedException(
                "This Unidentified store does not support keyset continuation."));

    /// <summary>
    /// The Queues page's Unidentified tab: open items oldest-first, each
    /// carrying enough of the origin receipt to answer what it is without a
    /// second lookup. <paramref name="mediaKind"/> narrows to one of the
    /// Images/E-mails filter values; <see langword="null"/> returns every open
    /// item (the "All" filter).
    /// </summary>
    Task<IReadOnlyList<UnidentifiedQueueRow>> ListQueueAsync(
        UnidentifiedMediaKind? mediaKind,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UnidentifiedHistoryEntry>> HistoryAsync(
        Guid unidentifiedItemId,
        CancellationToken cancellationToken = default);
}

public interface IRegisterUnidentified
{
    Task<UnidentifiedRegisterResult> ExecuteAsync(
        RegisterUnidentifiedRequest request,
        CancellationToken cancellationToken = default);
}

public interface IResolveUnidentified
{
    Task<UnidentifiedResolveResult> ExecuteAsync(
        ResolveUnidentifiedRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class RegisterUnidentified(IUnidentifiedStore store) : IRegisterUnidentified
{
    public async Task<UnidentifiedRegisterResult> ExecuteAsync(
        RegisterUnidentifiedRequest request,
        CancellationToken cancellationToken = default)
    {
        UnidentifiedValidation.ValidateRegister(request);
        return await store.RegisterAsync(request, cancellationToken);
    }
}

/// <summary>
/// Resolving an Unidentified item requires the selected destination to
/// actually exist: a typo or fabricated <c>TargetId</c> must not be able to
/// permanently remove work from the open queue with no supported destination
/// behind it. Each destination port is optional so a deployment lacking one
/// of these capabilities still fails closed for that target kind rather than
/// throwing a missing-service error; <see cref="UnidentifiedResolutionTargetKind.ExternalReference"/>
/// is free-form by design and has no Core-owned port to validate against.
/// </summary>
public sealed class ResolveUnidentified(
    IUnidentifiedStore store,
    ICaseQueryStore? caseQueries = null,
    IImageIntakeQueries? imageIntakeQueries = null,
    ITriageQueries? triageQueries = null,
    IIntakeReceiptQueries? intakeReceiptQueries = null) : IResolveUnidentified
{
    public async Task<UnidentifiedResolveResult> ExecuteAsync(
        ResolveUnidentifiedRequest request,
        CancellationToken cancellationToken = default)
    {
        UnidentifiedValidation.ValidateResolve(request);
        await EnsureDestinationExistsAsync(request, cancellationToken);
        return await store.ResolveAsync(request, cancellationToken);
    }

    private async Task EnsureDestinationExistsAsync(
        ResolveUnidentifiedRequest request,
        CancellationToken cancellationToken)
    {
        var targetId = request.TargetId.Trim();
        var exists = request.TargetKind switch
        {
            UnidentifiedResolutionTargetKind.InstructionCase => Guid.TryParse(targetId, out var caseId)
                && caseQueries is not null
                && await caseQueries.GetAsync(new(caseId, request.Actor), cancellationToken) is not null,
            UnidentifiedResolutionTargetKind.ImageIntake => Guid.TryParse(targetId, out var imageIntakeId)
                && imageIntakeQueries is not null
                && await imageIntakeQueries.GetAsync(imageIntakeId, cancellationToken) is not null,
            UnidentifiedResolutionTargetKind.Triage => Guid.TryParse(targetId, out var triageId)
                && triageQueries is not null
                && await triageQueries.GetAsync(triageId, cancellationToken) is not null,
            UnidentifiedResolutionTargetKind.BlockedIntake => Guid.TryParse(targetId, out var receiptId)
                && intakeReceiptQueries is not null
                && await intakeReceiptQueries.GetAsync(receiptId, cancellationToken) is
                    { Decision: IntakeDecision.BlockedIntake },
            // Free-form external reference; no Core-owned destination to validate.
            UnidentifiedResolutionTargetKind.ExternalReference => true,
            _ => throw new ArgumentOutOfRangeException(
                nameof(request), "The resolution target is not recognised.")
        };
        if (!exists)
        {
            throw new UnidentifiedResolutionTargetNotFoundException(request.TargetKind, targetId);
        }
    }
}

public static class UnidentifiedValidation
{
    public const int MaximumDetailLength = 1000;
    public const int MaximumReasonLength = 500;
    public const int MaximumOperationKeyLength = 200;
    public const int MaximumTargetIdLength = 200;
    public const int MaximumTargetReferenceLength = 200;

    public static void ValidateRegister(RegisterUnidentifiedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        UnidentifiedOrigin.Validate(request.Origin);
        if (!Enum.IsDefined(request.ReasonCode))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The Unidentified reason is not recognised.");
        }
        RequireDetail(request.SafeDetail);
        RequireOperation(request.OperationKey);
        RequireActorForRegistration(request.Actor);
        RequireUtc(request.CreatedAtUtc, nameof(request.CreatedAtUtc));
    }

    public static void ValidateResolve(ResolveUnidentifiedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.UnidentifiedItemId == Guid.Empty || request.ExpectedVersion < 0)
        {
            throw new ArgumentException("A resolution requires a valid item and expected version.", nameof(request));
        }
        RequireStaffOrAutomation(request.Actor);
        RequireOperation(request.OperationKey);
        RequireText(request.Reason, MaximumReasonLength, nameof(request.Reason));
        RequireText(request.TargetId, MaximumTargetIdLength, nameof(request.TargetId));
        if (request.TargetReference is not null)
        {
            RequireText(request.TargetReference, MaximumTargetReferenceLength, nameof(request.TargetReference));
        }
        if (!Enum.IsDefined(request.TargetKind))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The resolution target is not recognised.");
        }
        RequireUtc(request.ResolvedAtUtc, nameof(request.ResolvedAtUtc));
    }

    public static void ValidateReopen(ReopenUnidentifiedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.UnidentifiedItemId == Guid.Empty || request.ExpectedVersion < 0)
        {
            throw new ArgumentException("A reopen requires a valid item and expected version.", nameof(request));
        }
        RequireStaffOrAutomation(request.Actor);
        RequireOperation(request.OperationKey);
        RequireText(request.Reason, MaximumReasonLength, nameof(request.Reason));
        RequireUtc(request.ReopenedAtUtc, nameof(request.ReopenedAtUtc));
    }

    public static void RequireDetail(string value) => RequireText(value, MaximumDetailLength, nameof(value));

    private static void RequireOperation(string value) => RequireText(value, MaximumOperationKeyLength, nameof(value));

    private static void RequireActorForRegistration(ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Kind is not (ActorKind.Staff or ActorKind.SystemWorker or ActorKind.Automation))
        {
            throw new UnauthorizedAccessException("This actor cannot register Unidentified material.");
        }
    }

    private static void RequireStaffOrAutomation(ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Kind is not (ActorKind.Staff or ActorKind.Automation))
        {
            throw new UnauthorizedAccessException("Only staff or authorised automation can resolve Unidentified material.");
        }
    }

    private static void RequireText(string value, int maximum, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Trim().Length > maximum)
        {
            throw new ArgumentException($"{parameterName} exceeds its maximum length of {maximum}.", parameterName);
        }
    }

    private static void RequireUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamps must be UTC.", parameterName);
        }
    }
}

public sealed class UnidentifiedOperationConflictException() : InvalidOperationException(
    "The operation key was already used for a different Unidentified request.");

public sealed class UnidentifiedVersionConflictException() : InvalidOperationException(
    "The Unidentified item changed; reload it before resolving.");

/// <summary>
/// An <see cref="ArgumentException"/> so the existing Web resolve-form error
/// handling (which already surfaces any <c>ArgumentException</c> as a model
/// error) reports it without a separate catch clause.
/// </summary>
public sealed class UnidentifiedResolutionTargetNotFoundException(
    UnidentifiedResolutionTargetKind targetKind,
    string targetId)
    : ArgumentException(
        $"No {targetKind} destination exists for target '{targetId}'.",
        nameof(ResolveUnidentifiedRequest.TargetId));

public sealed record ListUnidentifiedQueueByCursorQuery(
    ActionActor Actor,
    UnidentifiedMediaKind? MediaKind,
    string? Cursor,
    int? Limit);

public interface IListUnidentifiedQueueByCursor
{
    Task<CursorPage<UnidentifiedQueueRow>> ExecuteAsync(
        ListUnidentifiedQueueByCursorQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Stable continuation over the Unidentified queue, on the shared cursor
/// contract. The scope binds the cursor to this query, this actor, this media
/// filter and this order, so a cursor cannot be replayed against the received
/// list, another operator's view, or a different tab of the same page.
/// </summary>
public sealed class ListUnidentifiedQueueByCursor(
    IUnidentifiedStore store,
    ICursorProtector cursorProtector) : IListUnidentifiedQueueByCursor
{
    public const string QueryName = "intake.unidentified.queue";

    public const string OrderName = "created-asc,id-asc";

    public async Task<CursorPage<UnidentifiedQueueRow>> ExecuteAsync(
        ListUnidentifiedQueueByCursorQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.MediaKind is { } mediaKind && !Enum.IsDefined(mediaKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "The media kind is not recognized.");
        }

        var limit = CursorPaging.NormalizeLimit(query.Limit);
        var scope = CursorPaging.CreateScope(
            QueryName,
            query.Actor,
            query.MediaKind?.ToString(),
            OrderName);

        KeysetPosition? after = null;
        if (!string.IsNullOrWhiteSpace(query.Cursor))
        {
            var (sortKey, id) = cursorProtector.Unprotect(query.Cursor, scope);
            after = new(CursorPaging.DecodeUtcTimestamp(sortKey), id);
        }

        var page = await store.ListQueueByCursorAsync(
            query.MediaKind, after, limit, cancellationToken);
        return new(
            page.Items,
            page.Next is { } next
                ? cursorProtector.Protect(
                    scope,
                    CursorPaging.EncodeUtcTimestamp(next.SortKey),
                    next.Id)
                : null);
    }
}
