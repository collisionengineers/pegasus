using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Operations;

/// <summary>
/// The Intake log's outcome column (Administration › Logs, 13 September): the
/// receipt's decision and what became of it, in the words the page uses. Not
/// the decision vocabulary itself, which the record keeps for automation (D6).
/// </summary>
public enum IntakeLogOutcome
{
    CaseCreated,
    Unidentified,
    Triage,
    VehicleImages,
    CouldNotBeRead,
    ProcessingFailed,
    Closed,

    /// <summary>The last allocation attempt failed and can be retried (Retry allocation).</summary>
    AllocationFailed,

    /// <summary>The last OCR attempt failed (Retry OCR).</summary>
    OcrFailed
}

/// <summary>What the receipt became: the Case, Unidentified item, Triage or Image intake it produced.</summary>
public enum IntakeLogBecameKind
{
    Case,
    Unidentified,
    Triage,
    ImageIntake
}

public sealed record IntakeLogBecame(IntakeLogBecameKind Kind, Guid Id, string Reference);

/// <summary>
/// Where a received item came from: the mailbox and sender, the upload and who
/// made it, or the Provider API and the principal. <see cref="Detail"/> is the
/// second half of that pair; null when the record did not keep it.
/// </summary>
public sealed record IntakeLogSource(IntakeSourceChannel Channel, string? Address, string? Detail);

/// <summary>
/// One row per receipt. <see cref="AssetId"/> opens the original in the viewer;
/// <see cref="ProcessingAttempts"/> and <see cref="AllocationAttempts"/> show
/// only when more than one (the page's rule), but the counts are always here.
/// </summary>
public sealed record IntakeLogRow(
    Guid ReceiptId,
    DateTimeOffset ReceivedAtUtc,
    IntakeLogSource Source,
    string Item,
    Guid? AssetId,
    IntakeLogOutcome Outcome,
    string? OutcomeReason,
    IntakeLogBecame? Became,
    int ProcessingAttempts,
    int AllocationAttempts)
{
    /// <summary>The retained message the item came in, so the drawer offers Open message beside Open file.</summary>
    public Guid? MessageId { get; init; }
}

public sealed record IntakeLogFilter(
    IntakeLogOutcome? Outcome = null,
    IntakeSourceChannel? SourceChannel = null,
    string? PrincipalCode = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    string? Text = null);

public sealed record IntakeLogPage(
    IReadOnlyList<IntakeLogRow> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling((double)TotalCount / PageSize);
}

/// <summary>
/// The tab's head-line counts. <see cref="FailedIntake"/> counts every receipt
/// whose outcome is a retryable failure (<see cref="IntakeLogPolicy.RetryableFailures"/>):
/// the same rows Operations lists under Attention required, each with its own
/// retry.
/// </summary>
public sealed record IntakeLogCounts(int FailedIntake, DateTimeOffset? OldestPendingIntakeDueAtUtc);

/// <summary>
/// The technical actions the row drawer offers, each true only when it applies:
/// Retry allocation when the last allocation attempt failed, Retry OCR when OCR
/// failed, Re-evaluate with current policy on any processed receipt.
/// </summary>
public sealed record IntakeLogActions(bool CanReevaluate, bool CanRetryAllocation, bool CanRetryOcr);

/// <summary>
/// The drawer: the retained original and the processing evidence in a plain
/// list — registration readings, suggested fields, decision evidence, the
/// allocation attempts — and which technical actions apply.
/// </summary>
public sealed record IntakeLogDetail(
    IntakeLogRow Row,
    IntakeReceipt Receipt,
    IReadOnlyList<ImageVrmSuggestion> RegistrationReadings,
    IReadOnlyList<IntakeAllocationState> AllocationAttempts,
    IntakeLogActions Actions);

public interface IIntakeLogQueries
{
    Task<IntakeLogPage> ListAsync(IntakeLogFilter filter, int page, int pageSize, CancellationToken cancellationToken);

    Task<IntakeLogCounts> GetCountsAsync(CancellationToken cancellationToken);

    Task<IntakeLogDetail?> GetAsync(Guid receiptId, CancellationToken cancellationToken);
}

public static class IntakeLogPolicy
{
    public const int PageSize = 50;
    public const int MaximumTextLength = 200;

    /// <summary>
    /// The intake failures a person can retry, in the order Operations lists
    /// them: failed allocation (Retry allocation), failed OCR (Retry OCR) and
    /// any other processing failure (Re-evaluate). The Intake log's Failed
    /// intake count is the number of receipts in these outcomes.
    /// </summary>
    public static readonly IReadOnlyList<IntakeLogOutcome> RetryableFailures =
        [IntakeLogOutcome.AllocationFailed, IntakeLogOutcome.OcrFailed, IntakeLogOutcome.ProcessingFailed];

    public static bool IsRetryableFailure(IntakeLogOutcome outcome) => RetryableFailures.Contains(outcome);

    /// <summary>
    /// The outcome a receipt reads as. A receipt whose Unidentified item was
    /// closed with a reason reads Closed; an unreadable file reads Could not be
    /// read; a processing failure that never produced a decision reads
    /// Processing failed. The two actionable failures read as themselves, each
    /// with its own retry: a retryable failed last allocation reads Allocation
    /// failed, and a failed last OCR attempt reads OCR failed.
    /// </summary>
    public static IntakeLogOutcome Outcome(
        IntakeDecision decision,
        bool triageOpened,
        bool unidentifiedClosed,
        bool processingFailed,
        bool allocationFailed = false,
        bool ocrFailed = false) =>
        processingFailed && decision == IntakeDecision.TechnicalFailure
            ? IntakeLogOutcome.ProcessingFailed
            : allocationFailed
            ? IntakeLogOutcome.AllocationFailed
            : ocrFailed
            ? IntakeLogOutcome.OcrFailed
            : decision switch
            {
                IntakeDecision.CaseCreated => IntakeLogOutcome.CaseCreated,
                IntakeDecision.ImageIntakeRegistered => IntakeLogOutcome.VehicleImages,
                IntakeDecision.Unsupported or IntakeDecision.OcrRequired or IntakeDecision.TechnicalFailure =>
                    unidentifiedClosed ? IntakeLogOutcome.Closed : IntakeLogOutcome.CouldNotBeRead,
                IntakeDecision.BlockedIntake => IntakeLogOutcome.Closed,
                _ when triageOpened => IntakeLogOutcome.Triage,
                _ when unidentifiedClosed => IntakeLogOutcome.Closed,
                _ => IntakeLogOutcome.Unidentified
            };

    public static IntakeLogFilter Normalize(IntakeLogFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (filter.FromUtc is { } from && filter.ToUtc is { } to && from > to)
        {
            throw new ArgumentException("The date range is reversed.", nameof(filter));
        }

        var text = filter.Text?.Trim();
        if (text is { Length: > MaximumTextLength })
        {
            throw new ArgumentException("The search text is too long.", nameof(filter));
        }

        return filter with
        {
            Text = string.IsNullOrEmpty(text) ? null : text,
            PrincipalCode = string.IsNullOrWhiteSpace(filter.PrincipalCode) ? null : filter.PrincipalCode.Trim().ToUpperInvariant()
        };
    }
}

/// <summary>Administrators only (decided 13 September).</summary>
public interface IListIntakeLog
{
    Task<IntakeLogPage> ExecuteAsync(ActionActor actor, IntakeLogFilter filter, int page, CancellationToken cancellationToken);

    Task<IntakeLogCounts> CountsAsync(ActionActor actor, CancellationToken cancellationToken);

    Task<IntakeLogDetail?> GetAsync(ActionActor actor, Guid receiptId, CancellationToken cancellationToken);
}

public sealed class ListIntakeLog(IIntakeLogQueries queries) : IListIntakeLog
{
    private readonly IIntakeLogQueries _queries = queries ?? throw new ArgumentNullException(nameof(queries));

    public Task<IntakeLogPage> ExecuteAsync(ActionActor actor, IntakeLogFilter filter, int page, CancellationToken cancellationToken)
    {
        RequireAdministrator(actor);
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "The page must be positive.");
        }

        return _queries.ListAsync(IntakeLogPolicy.Normalize(filter), page, IntakeLogPolicy.PageSize, cancellationToken);
    }

    public Task<IntakeLogCounts> CountsAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        RequireAdministrator(actor);
        return _queries.GetCountsAsync(cancellationToken);
    }

    public Task<IntakeLogDetail?> GetAsync(ActionActor actor, Guid receiptId, CancellationToken cancellationToken)
    {
        RequireAdministrator(actor);
        if (receiptId == Guid.Empty)
        {
            throw new ArgumentException("A receipt identifier is required.", nameof(receiptId));
        }

        return _queries.GetAsync(receiptId, cancellationToken);
    }

    private static void RequireAdministrator(ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.ViewOperationalReports);
    }
}
