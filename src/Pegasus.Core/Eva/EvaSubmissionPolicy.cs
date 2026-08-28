using System.Net;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Eva;

/// <summary>
/// A principal's two EVA API settings (EXT-04).
///
/// They are independent by operator decision (2026-08-27), so all four
/// combinations are legal — including automatic without manual, which submits
/// unattended and offers no button. That is a real consequence and not an
/// oversight: recovery for such a principal is the reconciliation sweep
/// re-arming the work, not an operator pressing anything.
/// </summary>
public sealed record EvaSubmissionModes(bool Manual, bool Automatic)
{
    public static EvaSubmissionModes Disabled { get; } = new(false, false);

    public bool IsEnabled => Manual || Automatic;
}

/// <summary>
/// Reads a principal's persisted EVA submission settings, the way
/// <see cref="Cases.IProviderInspectionModeStore"/> reads its inspection mode.
///
/// A principal code that names no active principal returns
/// <see cref="EvaSubmissionModes.Disabled"/> rather than null: an unknown
/// principal has not enabled anything, and failing closed is the same answer
/// as switched off.
/// </summary>
public interface IEvaSubmissionModeStore
{
    Task<EvaSubmissionModes> GetForPrincipalAsync(
        string principalCode,
        CancellationToken cancellationToken);
}

/// <summary>
/// The one owner of EVA API submission decisions: who may submit, when a case
/// may be submitted twice (never), and what EVA's answer means.
///
/// It deliberately holds no transport concern and no persistence concern — it
/// is given facts and returns a decision, which is what lets the same rules be
/// proved by unit test and enforced identically from the page and the worker.
/// </summary>
public static class EvaSubmissionPolicy
{
    public const string PolicyKey = "eva-api-submission-policy";
    public const int PolicyVersion = 1;

    /// <summary>The one wording for "this case has no photographs to send".</summary>
    public const string NoRetainedImagesReason =
        EvaHandoffPolicy.NoRetainedImagesReason;

    /// <summary>The one wording for an already-delivered case.</summary>
    public const string AlreadySubmittedReason =
        "The case has already been submitted to EVA.";

    /// <summary>The one wording for a principal with the route switched off.</summary>
    public const string NotEnabledReason =
        "EVA API submission is not enabled for this principal.";

    /// <summary>
    /// Whether an operator may submit this case by hand. Requires the manual
    /// toggle specifically: a principal set to automatic-only has no manual
    /// act, which is the point of the two settings being independent.
    /// </summary>
    public static bool AllowsManualSubmission(EvaSubmissionModes modes)
    {
        ArgumentNullException.ThrowIfNull(modes);
        return modes.Manual;
    }

    public static bool AllowsAutomaticSubmission(EvaSubmissionModes modes)
    {
        ArgumentNullException.ThrowIfNull(modes);
        return modes.Automatic;
    }

    /// <summary>
    /// Which access right the act requires.
    ///
    /// The two triggers are performed by different kinds of actor, so they
    /// cannot share one right. A manual submission is casework: a member of
    /// staff, or the Automation actor acting for one. An automatic submission
    /// is the Worker running scheduled work, and a SystemWorker actor holds
    /// <see cref="StaffAccessRight.ExecuteSystemWork"/> and deliberately not
    /// <see cref="StaffAccessRight.PerformCasework"/> — granting it casework
    /// to make this compile would widen what every background process may do.
    /// </summary>
    public static StaffAccessRight RequiredRight(EvaSubmissionTrigger trigger) => trigger switch
    {
        EvaSubmissionTrigger.Manual => StaffAccessRight.PerformCasework,
        EvaSubmissionTrigger.Automatic => StaffAccessRight.ExecuteSystemWork,
        _ => throw new ArgumentOutOfRangeException(nameof(trigger))
    };

    /// <summary>
    /// Whether the principal's settings authorise the act being attempted.
    /// Each trigger consults its own setting and only its own — an automatic
    /// principal does not thereby gain a button, and a manual one does not
    /// thereby start submitting on its own.
    /// </summary>
    public static bool Allows(EvaSubmissionModes modes, EvaSubmissionTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(modes);
        return trigger switch
        {
            EvaSubmissionTrigger.Manual => AllowsManualSubmission(modes),
            EvaSubmissionTrigger.Automatic => AllowsAutomaticSubmission(modes),
            _ => throw new ArgumentOutOfRangeException(nameof(trigger))
        };
    }

    /// <summary>
    /// Whether a failed submission may be attempted again.
    ///
    /// Only <see cref="EvaSubmissionOutcome.Unknown"/> is retried, and this is
    /// the load-bearing rule of the whole integration. EVA has no idempotency:
    /// a second instruction for the same case creates a second claim with a
    /// new File Reference. A rejection is terminal because the same payload
    /// will be rejected again. A partial delivery is terminal because the case
    /// *did* reach EVA. An unknown outcome is the only one where retrying is
    /// less bad than not retrying — and even then the attempt cap is what
    /// stops it eventually.
    /// </summary>
    public static bool IsRetryable(EvaSubmissionOutcome outcome) =>
        outcome == EvaSubmissionOutcome.Unknown;

    /// <summary>
    /// The operation key one attempt of a queued submission runs under.
    ///
    /// The durable work row's own key is stable per case, so two sweeps racing
    /// each other produce the same row rather than two. An attempt is not the
    /// same thing as the row: the replay guard answers a repeated operation
    /// with the outcome already recorded for it, so if every attempt shared the
    /// row's key then the second attempt would replay the first attempt's
    /// unknown outcome instead of reaching EVA, and the retry ladder would
    /// spend every attempt without sending anything.
    ///
    /// Derived rather than generated, so a queue message delivered twice for
    /// the same attempt still replays instead of submitting twice. The mix is
    /// exclusive-or, which is injective in the attempt number, so two attempts
    /// of one case can never collide.
    /// </summary>
    public static string AttemptOperationKey(string operationKey, int attemptCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attemptCount, 1);
        if (!Guid.TryParseExact(operationKey, "N", out var key))
        {
            throw new ArgumentException("The operation key is invalid.", nameof(operationKey));
        }

        var bytes = key.ToByteArray();
        var attempt = BitConverter.GetBytes(attemptCount);
        for (var index = 0; index < attempt.Length; index++)
        {
            bytes[^(index + 1)] ^= attempt[index];
        }

        return new Guid(bytes).ToString("N");
    }

    /// <summary>
    /// What EVA's answer means, in the four terms FRD-07 requires stay
    /// distinct.
    ///
    /// EVA's answers are not shaped the way the documentation says. Three
    /// observed behaviours drive every branch below:
    ///
    /// 1. A rejection can arrive inside an HTTP 200 — the body carries its own
    ///    <c>statusCode</c>, and 400 in that envelope is a refusal however
    ///    healthy the HTTP status looks.
    /// 2. A 500 arrives as <c>text/plain</c>, not JSON, so there is no
    ///    envelope to read at all and no way to know whether the claim was
    ///    created.
    /// 3. A success can arrive with no identifier, which is not a failure —
    ///    the instruction landed — but is not a complete success either.
    /// </summary>
    public static EvaSubmissionOutcome Classify(
        HttpStatusCode? httpStatus,
        int? envelopeStatusCode,
        bool hasIdentifier)
    {
        // No status at all means the request never produced a response:
        // connect failure, timeout, cancellation. Delivery is genuinely
        // unknown — EVA may have created the claim before the connection died.
        if (httpStatus is not { } status)
        {
            return EvaSubmissionOutcome.Unknown;
        }

        // The envelope outranks the HTTP status when it disagrees, because the
        // envelope is what EVA's application actually decided.
        if (envelopeStatusCode is { } envelope)
        {
            return envelope switch
            {
                >= 200 and <= 299 => hasIdentifier
                    ? EvaSubmissionOutcome.Succeeded
                    : EvaSubmissionOutcome.Partial,
                >= 400 and <= 499 => EvaSubmissionOutcome.Rejected,
                _ => EvaSubmissionOutcome.Unknown
            };
        }

        // No readable envelope. A 2xx with nothing in it means the instruction
        // was accepted and we learned nothing else about it.
        var code = (int)status;
        return code switch
        {
            >= 200 and <= 299 => EvaSubmissionOutcome.Partial,
            >= 400 and <= 499 => EvaSubmissionOutcome.Rejected,
            _ => EvaSubmissionOutcome.Unknown
        };
    }

    /// <summary>
    /// A stable failure code for an outcome that is not a success, used for
    /// operator display and for the retry decision. Kept short and
    /// machine-shaped, in the style the vehicle-lookup failures already use.
    /// </summary>
    public static string? FailureCode(
        EvaSubmissionOutcome outcome,
        HttpStatusCode? httpStatus) => outcome switch
        {
            EvaSubmissionOutcome.Succeeded => null,
            EvaSubmissionOutcome.Partial => "eva_accepted_without_identifier",
            EvaSubmissionOutcome.Rejected => httpStatus is { } status
                ? $"eva_rejected_{(int)status}"
                : "eva_rejected",
            _ => httpStatus is { } unknown
                ? $"eva_unavailable_{(int)unknown}"
                : "eva_unreachable"
        };
}

/// <summary>
/// When a failed EVA submission is tried again, and how many times.
///
/// Shaped like <see cref="Custody.ImageCustodyRetryPolicy"/> deliberately —
/// same idea, same delays, so an operator reading two queues sees one
/// behaviour. What differs is the gate: custody retries on named failure
/// codes, and this retries only on an outcome that leaves delivery unknown,
/// because every other outcome is one we already know the answer to.
/// </summary>
public static class EvaSubmissionRetryPolicy
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(6)
    ];

    public static int MaximumAttempts => RetryDelays.Length + 1;

    public static TimeSpan? NextAttemptDelay(
        int attemptCount,
        EvaSubmissionOutcome outcome) =>
        attemptCount < 1
        || attemptCount >= MaximumAttempts
        || !EvaSubmissionPolicy.IsRetryable(outcome)
            ? null
            : RetryDelays[attemptCount - 1];
}
