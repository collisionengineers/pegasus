using System.Globalization;
using System.Net;
using System.Text;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Eva;

/// <summary>
/// The selected report-generation route for a Principal.
/// </summary>
public sealed record EvaSubmissionModes(PrincipalReportGenerationPolicy Policy)
{
    public static EvaSubmissionModes Disabled { get; } = new(PrincipalReportGenerationPolicy.Pegasus);

    public bool IsEnabled => PrincipalReportGenerationPolicyRules.IsEva(Policy);
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
/// The one owner of EVA API submission decisions: who may submit, which case
/// states permit a send or re-send, and what EVA's answer means.
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

    public const string InvalidClaimantAddressReason =
        "EVA requires an accepted claimant address of at most 40 characters without control or format characters.";

    /// <summary>
    /// The current accepted claimant address, unchanged, or no usable API value.
    /// A rejected current value never falls back to an older fact or suggestion.
    /// </summary>
    public static string? AcceptedClaimantAddress(CaseField<string> field)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (field.Current is not { IsAccepted: true } current
            || string.IsNullOrWhiteSpace(current.Value)
            || current.Value.Length > 40)
        {
            return null;
        }

        foreach (var rune in current.Value.EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format)
            {
                return null;
            }
        }

        return current.Value;
    }

    /// <summary>
    /// Whether an operator may submit this case by hand. ZIP export is a
    /// separate export route and automatic API work is Worker-owned.
    /// </summary>
    public static bool AllowsManualSubmission(EvaSubmissionModes modes)
    {
        ArgumentNullException.ThrowIfNull(modes);
        return PrincipalReportGenerationPolicyRules.AllowsManualApi(modes.Policy);
    }

    public static bool AllowsAutomaticSubmission(EvaSubmissionModes modes)
    {
        ArgumentNullException.ThrowIfNull(modes);
        return PrincipalReportGenerationPolicyRules.RequiresAutomaticApiOnReview(modes.Policy);
    }

    /// <summary>
    /// Which access right the act requires.
    ///
    /// A manual submission is casework: a member of staff, or the Automation
    /// actor acting for one.
    /// </summary>
    public static StaffAccessRight RequiredRight => StaffAccessRight.PerformCasework;

    /// <summary>
    /// Stable actor authority for a request. It is intentionally separate
    /// from the Principal's current mode so an exact completed replay remains
    /// readable when settings changed after the original handoff.
    /// </summary>
    public static void RequireAuthorizedActor(
        ActionActor actor,
        EvaSubmissionInitiator initiator)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (initiator == EvaSubmissionInitiator.Manual)
        {
            StaffAuthorization.Require(actor, RequiredRight);
            return;
        }
        if (initiator != EvaSubmissionInitiator.AutomaticReview
            || actor.Kind != ActorKind.SystemWorker)
        {
            throw new InvalidOperationException("The EVA submission initiator is not authorised.");
        }
    }

    public static void RequireAuthorizedInitiator(
        EvaSubmissionModes modes,
        ActionActor actor,
        EvaSubmissionInitiator initiator)
    {
        ArgumentNullException.ThrowIfNull(modes);
        RequireAuthorizedActor(actor, initiator);
        if (initiator == EvaSubmissionInitiator.Manual)
        {
            if (!AllowsManualSubmission(modes) && !AllowsAutomaticSubmission(modes))
            {
                throw new InvalidOperationException("The Principal has not selected manual EVA API submission.");
            }
            return;
        }

        if (!AllowsAutomaticSubmission(modes))
        {
            throw new InvalidOperationException("The EVA submission initiator is not authorised for this Principal.");
        }
    }

    /// <summary>
    /// Whether the principal's settings authorise the act being attempted.
    /// The explicit manual action consults the principal's manual setting.
    /// </summary>
    public static bool Allows(EvaSubmissionModes modes)
    {
        ArgumentNullException.ThrowIfNull(modes);
        return AllowsManualSubmission(modes);
    }

    /// <summary>
    /// The state a send leaves the case in.
    ///
    /// <paramref name="isDelivered"/> defaults to true so a pre-flight check
    /// made before the transport call — deciding whether the D47 start-work
    /// preconditions need checking at all — can still ask "what state would
    /// this leave the case in if it succeeds?" without knowing the outcome
    /// yet. The actual post-transport commit
    /// (<see cref="EvaSubmissionResult.IsDelivered"/>, CASE-040 review) must
    /// pass the real outcome: a Rejected or Unknown manual send never reached
    /// EVA, so it is not a handoff and the case stays exactly where it was.
    /// </summary>
    public static CaseLifecycleState StateAfterSend(
        CaseLifecycleState state,
        bool isDelivered = true) =>
        EvaHandoffPolicy.StateAfterManualSend(state, isDelivered);

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
