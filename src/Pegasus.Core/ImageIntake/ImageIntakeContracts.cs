using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.ImageIntake;

public sealed record ImageIntakeOrigin(
    Guid ReceiptId,
    IntakeSourceIdentity SourceIdentity,
    string SourceHash,
    Guid EvaluationRevisionId);

/// <summary>
/// A durable pre-Case record for image-only material with a usable normalised
/// VRM. It is never a Case, carries no case association of its own: whether it
/// is `Associated with Case` derives from its origin receipt's single current
/// case association, so record and receipt can never disagree. The Image
/// Intake Reference is permanent and never reused.
/// </summary>
public sealed record ImageIntakeRecord(
    Guid Id,
    ImageIntakeOrigin Origin,
    string NormalizedVehicleRegistration,
    string ImageIntakeReference,
    ImageInitiatedCaseState State = ImageInitiatedCaseState.AwaitingInstruction,
    Guid? MergedIntoCaseId = null,
    string? MergedIntoCaseReference = null,
    string? ClosureReason = null,
    DateTimeOffset? ClosedAtUtc = null,
    long LifecycleVersion = 0,
    Guid? SubmissionGroupId = null,
    Guid? PendingExternalWorkId = null,
    Guid? PrincipalId = null);

public enum ImageInitiatedCaseState
{
    AwaitingInstruction,
    MergedIntoInstructionCase,
    StaffClosed
}

public enum ImageCustodyState
{
    Pending,
    Confirmed,
    Merged,
    Failed
}

/// <summary>
/// Formats the registration-based identity `{normalised VRM}-{sequence}` with a
/// two-digit minimum sequence that expands past `-99` without reuse.
/// </summary>
public static class ImageIntakeReferenceFormat
{
    public static string Create(string normalizedVehicleRegistration, int sequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedVehicleRegistration);
        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence),
                "An Image Intake Reference sequence starts at 1.");
        }

        return $"{normalizedVehicleRegistration}-{sequence:00}";
    }
}

public sealed class ImageIntakeOperationConflictException(Guid originReceiptId, string operationKey)
    : InvalidOperationException(
        $"Operation '{operationKey}' was already applied to the image intake for receipt '{originReceiptId}' with different inputs.")
{
    public Guid OriginReceiptId { get; } = originReceiptId;

    public string OperationKey { get; } = operationKey;
}

public sealed class ImageIntakeCaseNotEligibleException(Guid caseId)
    : InvalidOperationException(
        $"Case '{caseId}' is not an eligible pre-report instructed case for Image-intake association.")
{
    public Guid CaseId { get; } = caseId;
}

/// <param name="SubmissionGroupId">
/// The <c>IntakeSubmissionGroup</c> this registration covers, when the whole
/// group is the registration unit: exactly one ImageIntake exists per group
/// (enforced by a unique index), <see cref="Origin"/> is the group's primary
/// member (its lowest-ordinal image-only member, so racing siblings compute
/// the same request), and the store moves every image-only member receipt to
/// `ImageIntakeRegistered` against the one reference in the same
/// transaction. Null for the legacy single-receipt path.
/// </param>
public sealed record RegisterImageIntakeRequest(
    ImageIntakeOrigin Origin,
    string NormalizedVehicleRegistration,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    Guid? SubmissionGroupId = null);

public interface IRegisterImageIntake
{
    Task<ImageIntakeRecord> ExecuteAsync(
        RegisterImageIntakeRequest request,
        CancellationToken cancellationToken);
}

public sealed record ImageIntakeSummary(
    Guid Id,
    Guid OriginReceiptId,
    string ImageIntakeReference,
    string NormalizedVehicleRegistration,
    Guid? AssociatedCaseId,
    string? AssociatedCaseReference,
    DateTimeOffset RegisteredAtUtc,
    ImageCustodyState? Custody,
    ImageInitiatedCaseState State = ImageInitiatedCaseState.AwaitingInstruction,
    string? ClosureReason = null,
    int ImageCount = 0,
    IntakeSourceChannel Source = IntakeSourceChannel.ManualUpload,
    string? PrincipalCode = null);

public sealed record ImageIntakeLifecycleEvent(
    Guid Id,
    Guid ImageIntakeId,
    string EventType,
    ActionActor Actor,
    DateTimeOffset OccurredAtUtc,
    string Reason,
    string OperationKey,
    long BeforeVersion,
    long AfterVersion,
    Guid? CaseId = null,
    string? CaseReference = null);

/// <summary>
/// The record plus the context that is not part of it: when it was registered
/// and the origin receipt's current Case association. The Image-initiated
/// lifecycle is not restated here — <see cref="Record"/> is its one owner and
/// these forward to it, so the two can never disagree. Lifecycle history is a
/// separate read (<see cref="IImageIntakeStore.ListHistoryAsync"/>): only the
/// Image-initiated Case page renders it.
/// </summary>
public sealed record ImageIntakeDetail(
    ImageIntakeRecord Record,
    DateTimeOffset RegisteredAtUtc,
    Guid? AssociatedCaseId,
    string? AssociatedCaseReference,
    ImageCustodyState? Custody = null,
    string? PrincipalCode = null)
{
    public ImageInitiatedCaseState State => Record.State;

    public Guid? MergedIntoCaseId => Record.MergedIntoCaseId;

    public string? MergedIntoCaseReference => Record.MergedIntoCaseReference;

    public string? ClosureReason => Record.ClosureReason;

    public DateTimeOffset? ClosedAtUtc => Record.ClosedAtUtc;

    public long LifecycleVersion => Record.LifecycleVersion;
}

/// <summary>
/// The formal Case reference is deliberately not carried on this request: the
/// store resolves it from the persisted Case by <see cref="CaseId"/> inside the
/// same transaction, so a caller can never record a stale or mistyped
/// reference on the merge event or on the Image intake row.
/// </summary>
public sealed record MergeImageInitiatedCaseRequest(
    Guid ImageIntakeId,
    Guid CaseId,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    long ExpectedVersion);

public sealed record CloseImageInitiatedCaseRequest(
    Guid ImageIntakeId,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    long ExpectedVersion);

/// <summary>
/// Records, replaces or clears the optional known principal on an Image
/// Intake. A null <see cref="PrincipalId"/> is the `Not known` state — a
/// legitimate value staff may return to, not an error. There is no operation
/// key: the value is replaceable and clearable, so a replay probe returning
/// the current record (which is only correct for a terminal transition) would
/// be wrong here; <see cref="ExpectedVersion"/> alone guards the write.
/// </summary>
public sealed record SetImageIntakePrincipalRequest(
    Guid ImageIntakeId,
    Guid? PrincipalId,
    ActionActor Actor,
    long ExpectedVersion);

/// <summary>
/// One retained image of an Image-initiated Case, in stored group order. The
/// receipt id addresses the bytes through the authorised intake image
/// endpoint; the file name is the operator-facing identity (and alt text).
/// </summary>
public sealed record ImageIntakeImage(
    Guid ReceiptId,
    string FileName,
    string MediaType);

public interface IImageIntakeQueries
{
    Task<IReadOnlyList<ImageIntakeSummary>> ListAsync(
        bool? associated,
        CancellationToken cancellationToken);

    /// <summary>
    /// The registered image receipts this Image intake covers — its origin
    /// plus, for a group registration, every registered image-only member —
    /// ordered by the submission ordinal and restricted to image media.
    /// </summary>
    Task<IReadOnlyList<ImageIntakeImage>> ListImagesAsync(
        Guid imageIntakeId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ImageIntakeImage>>([]);

    Task<ImageIntakeDetail?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<ImageIntakeDetail?> GetByReferenceAsync(
        string imageIntakeReference,
        CancellationToken cancellationToken);

    Task<ImageIntakeDetail?> GetByOriginReceiptAsync(
        Guid intakeReceiptId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ImageIntakeSummary>> ListByOriginReceiptsAsync(
        IReadOnlyCollection<Guid> intakeReceiptIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ImageIntakeSummary>> ListForCaseAsync(
        Guid caseId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ImageIntakeSummary>> SearchByRegistrationAsync(
        string normalizedVehicleRegistration,
        CancellationToken cancellationToken);

    /// <summary>
    /// The active principals a staff member may record against an Image
    /// Intake, ordered by code. The organisation administration query is
    /// paginated and gated behind `ManageOrganizationsAndPrincipals`, so it
    /// cannot serve this page. The default fails closed rather than returning
    /// an empty option list that would silently look like `no principals
    /// exist` when an implementation is missing.
    /// </summary>
    Task<IReadOnlyList<Principal>> ListActivePrincipalsAsync(
        CancellationToken cancellationToken) =>
        Task.FromException<IReadOnlyList<Principal>>(
            new NotSupportedException("Active principal options are not available."));
}

/// <summary>
/// The historical result for an exact committed registration. A replay probe
/// returns <see langword="null"/> only for an unseen operation key; a committed
/// key with a different request fingerprint throws
/// <see cref="ImageIntakeOperationConflictException"/>.
/// </summary>
public sealed record ImageIntakeOperationReplay(ImageIntakeRecord Result);

/// <summary>
/// Persists Image-intake registrations. Registration allocates the next
/// per-VRM Image Intake Reference atomically (a reference is never reused),
/// verifies the origin against the persisted receipt and evaluation revision,
/// and moves the receipt's decision to `ImageIntakeRegistered` in the same
/// transaction. Registration identity is immutable after creation — only the
/// Image-initiated lifecycle columns change, and only through
/// <see cref="MergeAsync"/>/<see cref="CloseAsync"/>; case association lives
/// exclusively on the origin receipt. The optional known principal is the one
/// exception: it is not registration identity and is recorded, replaced or
/// cleared through <see cref="SetPrincipalAsync"/> alone.
/// </summary>
public interface IImageIntakeStore : IImageIntakeQueries
{
    Task<ImageIntakeOperationReplay?> ProbeRegisterReplayAsync(
        RegisterImageIntakeRequest request,
        CancellationToken cancellationToken);

    Task<ImageIntakeRecord> RegisterAsync(
        RegisterImageIntakeRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Re-asserts `ImageIntakeRegistered` on a receipt that carries a
    /// registered Image intake but has fallen back to `Needs sorting` (a
    /// policy re-evaluation recomputes the decision without knowledge of the
    /// registration). Any other decision — including a reasoned staff block —
    /// stands untouched.
    /// </summary>
    Task EnsureRegisteredReceiptDecisionAsync(
        Guid intakeReceiptId,
        CancellationToken cancellationToken);

    Task<ImageIntakeRecord> MergeAsync(
        MergeImageInitiatedCaseRequest request,
        CancellationToken cancellationToken) =>
        Task.FromException<ImageIntakeRecord>(new NotSupportedException("Image-initiated lifecycle is not available."));

    Task<ImageIntakeRecord> CloseAsync(
        CloseImageInitiatedCaseRequest request,
        CancellationToken cancellationToken) =>
        Task.FromException<ImageIntakeRecord>(new NotSupportedException("Image-initiated lifecycle is not available."));

    /// <summary>
    /// Records, replaces or clears the optional known principal. This is not a
    /// lifecycle transition: it writes no lifecycle event, infers nothing from
    /// a registration match or a linked Case, and a same-value re-submission
    /// is a no-op that leaves the version alone.
    /// </summary>
    Task<ImageIntakeRecord> SetPrincipalAsync(
        SetImageIntakePrincipalRequest request,
        CancellationToken cancellationToken) =>
        Task.FromException<ImageIntakeRecord>(
            new NotSupportedException("Image Intake principal assignment is not available."));

    Task<IReadOnlyList<ImageIntakeLifecycleEvent>> ListHistoryAsync(
        Guid imageIntakeId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ImageIntakeLifecycleEvent>>([]);
}

/// <summary>
/// Resolves the registration origin for a processed intake receipt: its source
/// identity, source hash, and latest completed evaluation revision (the
/// web/pipeline-facing receipt record does not expose the revision).
/// </summary>
public interface IImageIntakeOriginResolver
{
    Task<ImageIntakeOrigin?> ResolveOriginAsync(
        Guid intakeReceiptId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Eligible pre-report instructed Cases whose confirmed vehicle registration
/// matches a normalised read under <see cref="VrmRegistrationMatching"/>
/// (exact, or the read missing exactly one character of the confirmed value) —
/// candidates for a reasoned staff pick and for the automatic unambiguous
/// match. Eligibility (editable pre-report state, no report-sent evidence,
/// not archived) is enforced by the query.
/// </summary>
public interface IImageIntakeCaseCandidates
{
    Task<IReadOnlyList<ImageIntakeCaseCandidate>> FindEligibleByRegistrationAsync(
        string normalizedVehicleRegistration,
        CancellationToken cancellationToken);
}

public sealed record ImageIntakeCaseCandidate(
    Guid CaseId,
    string CaseReference,
    long CaseVersion,
    string ConfirmedRegistration);
