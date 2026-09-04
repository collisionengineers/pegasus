using Pegasus.Core.Identity;

namespace Pegasus.Core.Eva;

/// <summary>
/// EXT-04: what one EVA API submission is known to have achieved.
///
/// FRD-07 requires these four to stay distinct — "external success, rejection,
/// partial or unknown outcomes must remain distinct when those routes are
/// implemented" — so this is deliberately not a boolean, and no caller may
/// collapse it into one.
/// </summary>
public enum EvaSubmissionOutcome
{
    /// <summary>EVA accepted the instruction and returned its identifiers.</summary>
    Succeeded,

    /// <summary>
    /// EVA refused the instruction and said why. Terminal: the same payload
    /// will be refused again, so it is never retried.
    /// </summary>
    Rejected,

    /// <summary>
    /// EVA accepted the instruction but something it should have returned did
    /// not arrive — an accepted envelope with no identifier. The case reached
    /// EVA; what it became there is not fully known.
    /// </summary>
    Partial,

    /// <summary>
    /// Delivery itself is unknown: a transport failure, a timeout, or an
    /// opaque server error. The instruction may or may not have been created,
    /// which is why a case in this state is never resubmitted automatically —
    /// EVA has no idempotency and a blind retry can duplicate the claim.
    /// </summary>
    Unknown
}

/// <summary>
/// One image travelling to EVA, already read and integrity-checked.
/// <paramref name="Name"/> is the file name without its extension and
/// <paramref name="Extension"/> carries the leading dot, which is the shape
/// EVA's file model documents.
/// </summary>
public sealed record EvaInstructionFile(
    string Name,
    string Extension,
    ReadOnlyMemory<byte> Content);

/// <summary>
/// The EVA-shaped instruction: our case in EVA's own field names.
///
/// Only fields Pegasus can populate honestly appear here. EVA's request model
/// is far wider (repairer block, estimate money, private-hire licensing,
/// salvage); a field the case does not hold is not invented, because
/// fabricated domain data is a stop condition.
///
/// Two mapped values have no EVA instruction field at all — EVA's model
/// carries no inspection date and no mileage — so they travel in
/// <see cref="Notes"/> as labelled lines. So does the work provider, which
/// lost <c>InsName</c> to the claimant name. See
/// <see cref="CaseEvaApiMapping"/> for why each.
/// </summary>
public sealed record EvaInstructionPayload(
    string RequestFrom,

    /// <summary>
    /// The Principal the case belongs to, sent as EVA's <c>Agent</c> code
    /// (operator direction, 2026-08-27). <c>RequestFrom</c> identifies
    /// Collision Engineers to EVA and is the same on every submission; this
    /// says which of our Principals the work arrived for, and is the only
    /// field that varies by Principal.
    ///
    /// Taken from the case's allocated Principal rather than its extracted
    /// work-provider field, because the allocation is immutable and always
    /// present. EVA caps the field at 10 characters where Pegasus allows 20;
    /// a longer code is sent unchanged and refused by EVA, rather than
    /// truncated into a different Principal.
    /// </summary>
    string Agent,
    string ExternalRef,
    string ClaimNumber,

    /// <summary>
    /// Serialised as EVA's <c>InsName</c>. That field is documented as the
    /// insurer name, but the operator's EVA instance carries the claimant
    /// there (2026-08-27), and the property is named for what it holds rather
    /// than for the wire field it lands in.
    /// </summary>
    string ClaimantName,
    string VehicleRegistration,
    string VehicleDescription,
    DateOnly? IncidentDate,
    string Cause,
    string VatStatus,
    string InspectionType,
    string CoverType,
    string VehicleDriveable,
    string InUse,
    string InstructionEmail,
    EvaInspectionLocation Location,
    string Notes,
    IReadOnlyList<EvaInstructionFile> Files);

/// <summary>
/// The inspection-location block. Pegasus stores the inspection address as one
/// collapsed line and exports it as six; EVA wants it split across named
/// fields, so the same six-line resolution feeds both.
/// </summary>
public sealed record EvaInspectionLocation(
    string Name,
    string Address,
    string Town,
    string City,
    string County,
    string Postcode);

/// <summary>
/// What EVA said. Both identifiers are kept: <paramref name="EvaId"/> is the
/// response envelope's own id, and <paramref name="FileReference"/> is the
/// number EVA embeds in its human-readable message. They are different values
/// and operators quote the second one.
/// </summary>
public sealed record EvaSubmissionResult(
    EvaSubmissionOutcome Outcome,
    string? EvaId,
    string? FileReference,
    string? FailureCode,
    string? FailureDetail,
    int ImagesSent)
{
    public bool IsDelivered => Outcome is EvaSubmissionOutcome.Succeeded
        or EvaSubmissionOutcome.Partial;
}

/// <summary>
/// The EVA network boundary, and the only place in Pegasus that talks to EVA.
///
/// It has exactly one method because Pegasus performs exactly one EVA
/// operation. EVA's update endpoints (<c>Claim/Update</c>,
/// <c>Claim/LocationUpdate</c>, <c>Claim/AuthorityStatusUpdate</c>) are not
/// suitable for this product's use case — operator decision, 2026-08-27 — so
/// a submitted case is never updated over the API. Adding a second method here
/// is a scope change, not an implementation detail.
/// </summary>
public interface IEvaApiTransport
{
    Task<EvaSubmissionResult> SubmitInstructionAsync(
        EvaInstructionPayload payload,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Which of a principal's two settings authorises this submission.
///
/// It travels on the request because the settings are independent: a
/// principal may allow automatic submission and no manual one, or the
/// reverse, so "may this proceed?" cannot be answered without knowing which
/// act is being attempted.
/// </summary>
public enum EvaSubmissionTrigger
{
    /// <summary>An operator pressed the button.</summary>
    Manual,

    /// <summary>The case reached Review and the worker picked it up.</summary>
    Automatic
}

/// <summary>
/// One submission of one case to EVA.
///
/// Like the export it takes an operation key for replay-safe action history.
/// A first manual send is also the atomic Review-to-With-Engineer transition.
/// </summary>
public sealed record SubmitCaseToEvaRequest(
    Guid CaseId,
    ActionActor Actor,
    string OperationKey,
    EvaSubmissionTrigger Trigger);

public sealed record SubmitCaseToEvaResult(
    EvaSubmissionResult? Submission,
    IReadOnlyList<string> UnrecordedFields,
    IReadOnlyList<string> BlockingReasons)
{
    public bool IsSubmitted => Submission is not null;
}

public interface ISubmitCaseToEva
{
    Task<SubmitCaseToEvaResult?> ExecuteAsync(
        SubmitCaseToEvaRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// What a case's last EVA submission attempt achieved, for the case surface
/// to show. Deliberately a read model: it carries no bytes, no payload and no
/// way to submit anything.
/// </summary>
public sealed record EvaSubmissionRecord(
    EvaSubmissionOutcome Outcome,
    string? EvaId,
    string? FileReference,
    string? FailureCode,
    DateTimeOffset SubmittedAtUtc)
{
    public bool IsSucceeded => Outcome == EvaSubmissionOutcome.Succeeded;

    /// <summary>
    /// The instruction reached EVA — either completely, or accepted with no
    /// identifier returned. Both are delivered outcomes even though a later
    /// explicit manual operation may record a distinct re-send.
    /// </summary>
    public bool IsDelivered => Outcome is EvaSubmissionOutcome.Succeeded
        or EvaSubmissionOutcome.Partial;
}

public interface IEvaSubmissionQueries
{
    /// <summary>
    /// The most recent attempt for a case, or null when it has never been
    /// submitted. The latest result is what an operator needs to see when
    /// deciding whether another explicit manual handoff is required.
    /// </summary>
    Task<EvaSubmissionRecord?> GetLatestAsync(
        Guid caseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts since <paramref name="sinceUtc"/> that did not reach EVA,
    /// newest first, at most <paramref name="maximumResults"/>. A person
    /// decides what to do with each one; the health surface only shows that
    /// they exist and when.
    /// </summary>
    Task<IReadOnlyList<EvaSubmissionFailure>> GetRecentFailuresAsync(
        DateTimeOffset sinceUtc,
        int maximumResults,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How much EVA work is queued and when EVA was last spoken to at all —
    /// the two facts a health row can state without probing EVA.
    /// </summary>
    Task<EvaSubmissionActivity> GetActivityAsync(CancellationToken cancellationToken = default);
}

public sealed record EvaSubmissionFailure(
    Guid CaseId,
    EvaSubmissionOutcome Outcome,
    string? FailureCode,
    DateTimeOffset SubmittedAtUtc);

/// <summary>
/// <see cref="PendingWorkCount"/> counts queued automatic submissions that have
/// neither completed nor failed; <see cref="LatestSubmittedAtUtc"/> is the
/// newest attempt of any outcome, or null when no case has ever been sent.
/// </summary>
public sealed record EvaSubmissionActivity(
    int PendingWorkCount,
    DateTimeOffset? LatestSubmittedAtUtc);

/// <summary>
/// A case whose principal has not enabled the act that was attempted.
/// </summary>
public sealed class EvaSubmissionNotEnabledException(Guid caseId)
    : InvalidOperationException(
        "The principal has not enabled EVA API submission for this case.")
{
    public Guid CaseId { get; } = caseId;
}
