using Pegasus.Core.Identity;

namespace Pegasus.Core.Workflow;

/// <summary>
/// How a request to edit a Case was answered. Null on the request itself means it has not been
/// answered yet.
/// </summary>
public enum CaseEditRequestAnswer
{
    /// <summary>The holder handed the lease to the requester.</summary>
    Accepted,

    /// <summary>The holder declined; the requester is offered nothing further until a new request.</summary>
    Declined,

    /// <summary>The requester withdrew, by leaving the Case or by a call that was cut off.</summary>
    Withdrawn
}

/// <summary>
/// A request to edit a Case a member of staff holds (FRD-14 §Take over, R550–R558, 30 October
/// 2026), as every reader sees it. A request is addressed to the holder it was made against:
/// <see cref="Holder"/> is that holder's subject, and the request is void once a different holder
/// has the lease. The subjects are internal identities; presentation resolves them to names.
/// </summary>
public sealed record CaseEditRequestSnapshot(
    Guid Id,
    ActorKind? RequesterKind,
    string Requester,
    string Holder,
    DateTimeOffset RequestedAtUtc,
    CaseEditRequestAnswer? Answer);

/// <summary>
/// Asks the member of staff who holds the Case's lease to hand it over. Within a case, the
/// normalized operation key identifies this exact request; sending it again while the request
/// stands answers the request's current state rather than raising a second one.
/// </summary>
public sealed record RaiseCaseEditRequestRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey);

/// <summary>What raising a request found.</summary>
public enum CaseEditRequestOutcome
{
    /// <summary>The request stands; the holder is being asked.</summary>
    Pending,

    /// <summary>Nobody holds a live lease: the caller claims the lease as usual.</summary>
    Free,

    /// <summary>The caller already holds the lease; <see cref="CaseEditRequestRaised.Lease"/> is it.</summary>
    Held,

    /// <summary>
    /// The Automation Actor holds the lease. Nobody can be asked: staff take it over at once
    /// (ADR-0075), and another Automation session is refused.
    /// </summary>
    AutomationHeld,

    /// <summary>
    /// The request was accepted and the caller now holds the lease; <see cref="CaseEditRequestRaised.Lease"/>
    /// is it. The answer to a call sent again after the holder accepted.
    /// </summary>
    Accepted
}

public sealed record CaseEditRequestRaised(
    CaseEditRequestOutcome Outcome,
    CaseEditRequestSnapshot? Request,
    CaseEditLease? Lease);

/// <summary>
/// The holder's answer to a request, proven by the holder's own lease token from any of their
/// windows; a withdrawal is the requester's and carries no token.
/// </summary>
public sealed record AnswerCaseEditRequestRequest(
    Guid CaseId,
    ActionActor Actor,
    Guid RequestId,
    CaseEditRequestAnswer Answer,
    string? LeaseToken);

/// <summary>
/// The Case's lease and request facts as the long poll answers them. <see cref="Stamp"/> changes
/// whenever anything a waiting page acts on changes: the holder, whether the lease is live, the
/// request, its answer, and whether it is still inside its answer window. <see cref="OwnLease"/>
/// is the caller's own live lease when the caller is the holder, so a requester whose request was
/// accepted learns its token from the same read; it is never another holder's.
/// </summary>
public sealed record CaseEditRequestView(
    string Stamp,
    long Version,
    ActorKind? HolderKind,
    string? Holder,
    bool HolderIsLive,
    CaseEditRequestSnapshot? Request,
    bool RequestIsPending,
    CaseEditLease? OwnLease);

/// <summary>Why a request to edit was refused.</summary>
public enum CaseEditRequestRefusal
{
    /// <summary>Another editor has already asked and their request still stands.</summary>
    AlreadyAsked,

    /// <summary>The holder declined.</summary>
    Declined,

    /// <summary>The holder did not answer within the window.</summary>
    NoAnswer
}

public sealed class CaseEditRequestRefusedException(Guid caseId, long caseVersion, CaseEditRequestRefusal refusal)
    : InvalidOperationException(refusal switch
    {
        CaseEditRequestRefusal.AlreadyAsked => $"Another editor has already asked to edit case '{caseId}'.",
        CaseEditRequestRefusal.Declined => $"The request to edit case '{caseId}' was declined.",
        _ => $"The request to edit case '{caseId}' was not answered."
    })
{
    public Guid CaseId { get; } = caseId;

    public long CaseVersion { get; } = caseVersion;

    public CaseEditRequestRefusal Refusal { get; } = refusal;
}

/// <summary>
/// Wakes whoever is waiting on a Case's lease or request. The store raises it after every commit
/// that changes either; a page's long poll and the Automation Actor's waiting call take the next
/// wake-up before they read, so no change between the read and the wait is missed. In-process:
/// exact on the one Web instance, and still correct across instances because every wait reads
/// the Case first and again when the hold ends.
/// </summary>
public interface ICaseEditSignal
{
    /// <summary>A task that completes at the next <see cref="Raise"/> for the Case.</summary>
    Task NextAsync(Guid caseId, CancellationToken cancellationToken);

    void Raise(Guid caseId);
}

/// <summary>
/// The long poll behind the Case page and the Automation Actor's waiting call (R551, 30 October
/// 2026). It answers at once when the Case's lease or request differs from what the caller last
/// saw, and otherwise holds until the next change, until a pending request's answer window
/// closes, or until <paramref name="hold"/> runs out, then answers the Case as it stands.
/// </summary>
public sealed class CaseEditRequestWatch(
    ILeaseCaseForEdit leases,
    ICaseEditSignal signal,
    TimeProvider timeProvider)
{
    private readonly ILeaseCaseForEdit _leases = leases ?? throw new ArgumentNullException(nameof(leases));
    private readonly ICaseEditSignal _signal = signal ?? throw new ArgumentNullException(nameof(signal));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>The longest one answer is held for; a page sends the next ask as this one returns.</summary>
    public static readonly TimeSpan MaximumHold = TimeSpan.FromSeconds(25);

    public async Task<CaseEditRequestView> WaitAsync(
        Guid caseId,
        ActionActor actor,
        string? knownStamp,
        TimeSpan hold,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }
        ArgumentNullException.ThrowIfNull(actor);
        if (hold < TimeSpan.Zero || hold > MaximumHold)
        {
            throw new ArgumentOutOfRangeException(nameof(hold), $"The hold must be between zero and {MaximumHold}.");
        }

        // The wake-up is taken before the read, so a change that lands between them is not slept through.
        var wake = _signal.NextAsync(caseId, cancellationToken);
        var view = await _leases.ReadRequestAsync(caseId, actor, cancellationToken);
        if (knownStamp is null || !string.Equals(view.Stamp, knownStamp, StringComparison.Ordinal))
        {
            return view;
        }

        var wait = hold;
        if (view.Request is { Answer: null } request && view.RequestIsPending)
        {
            var untilWindowCloses = request.RequestedAtUtc + CaseEditAuthority.RequestAnswerWindow - _timeProvider.GetUtcNow();
            if (untilWindowCloses < wait)
            {
                wait = untilWindowCloses < TimeSpan.Zero ? TimeSpan.Zero : untilWindowCloses;
            }
        }

        if (wait > TimeSpan.Zero)
        {
            await Task.WhenAny(wake, Task.Delay(wait, _timeProvider, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
        }

        return await _leases.ReadRequestAsync(caseId, actor, cancellationToken);
    }
}
