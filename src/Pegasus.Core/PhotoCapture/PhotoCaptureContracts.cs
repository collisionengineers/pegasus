using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.PhotoCapture;

/// <summary>
/// How a photo link came to exist (FRD-28). <see cref="CaseLink"/> is the
/// link HQ sends for a Case it already has ("Links we sent"); the other three
/// are photos "Coming in": an AMC's or bodyshop's own staff on their standing
/// link, the customer they forwarded a link to, and a private customer on the
/// website link.
/// </summary>
public enum PhotoCaptureOrigin
{
    CaseLink,
    StandingStaff,
    StandingForwarded,
    Website
}

/// <summary>
/// Where a capture's photos file. A Case link, and a standing capture whose
/// typed registration matches one eligible Case, file straight onto the Case
/// as each photo arrives; a standing capture with no such Case becomes a
/// Vehicle images record when it settles (operator, 9 November 2026:
/// FRD-19 and FRD-22 as they stand).
/// </summary>
public enum PhotoCaptureDestination
{
    Undecided,
    Case,
    VehicleImages
}

/// <summary>Guided: a page per stage, for customers. Quick: guidance inside the camera, for AMC and bodyshop staff.</summary>
public enum PhotoCaptureMode
{
    Guided,
    Quick
}

/// <summary>The kind of thing one item is, which decides its story group, its name and whether it starts In report.</summary>
public enum PhotoCaptureItemKind
{
    Overview,
    Damage,
    NumberPlate,
    Vin,
    Dashboard,
    Scene,
    OtherVehicle,
    Paperwork,
    Video
}

/// <summary>The four stages of one damaged area, in the order the app takes them.</summary>
public enum PhotoCaptureStage
{
    Overview,
    Closer,
    CloseUp,
    Extra
}

/// <summary>What the capture derives its status from; never stored (see <see cref="PhotoCaptureStatus"/>).</summary>
public enum PhotoCaptureState
{
    SentNotOpened,
    Opened,
    PhotosComingIn,
    SomethingMissing,
    NeedsCall,
    Complete,
    Expired,
    Cancelled
}

/// <summary>A standing link belongs to a Contacts entry, or it is the one website link.</summary>
public enum StandingPhotoLinkKind
{
    Contact,
    Website
}

/// <summary>
/// What the sender typed on the link's form. Every value is the sender's own
/// words: nothing here is a Case fact until the settle fills an empty Case
/// field from it with "Photo link" provenance (operator, 9 November 2026).
/// </summary>
public sealed record PhotoCaptureDetails(
    string? Handler = null,
    string? InspectionLocation = null,
    string? InspectionLocationChoice = null,
    string? Roadworthy = null,
    string? Outcome = null,
    string? InHire = null,
    string? RepairView = null,
    string? VehicleRole = null,
    long? Mileage = null,
    string? Airbags = null,
    string? Steering = null,
    IReadOnlyList<string>? DamageAreas = null)
{
    public static PhotoCaptureDetails Empty { get; } = new();

    public IReadOnlyList<string> MarkedAreas => DamageAreas ?? [];
}

/// <summary>
/// One vehicle's photo link and everything it received: the capture. It is
/// add-only until it expires, so the same link reopened adds to this record.
/// </summary>
public sealed record PhotoCapture(
    Guid Id,
    PhotoCaptureOrigin Origin,
    Guid? StandingLinkId,
    Guid? OrganizationId,
    Guid? CaseId,
    string? CaseReference,
    Guid? SubmissionGroupId,
    PhotoCaptureDestination Destination,
    string? RoutingReason,
    string? NormalizedRegistration,
    DateOnly? IncidentDate,
    string? CustomerName,
    string? Mobile,
    string? Email,
    PhotoCaptureDetails Details,
    string? Note,
    PhotoCaptureMode Mode,
    bool LiveCameraOnly,
    bool OriginalsInBackground,
    Guid? IssuedByStaffId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc,
    DateTimeOffset? OpenedAtUtc,
    DateTimeOffset? FirstItemAtUtc,
    DateTimeOffset? LastItemAtUtc,
    DateTimeOffset? SentAtUtc,
    DateTimeOffset? SettledAtUtc,
    int SettledBatches,
    int AcceptedItemCount,
    long AcceptedBytes,
    int ReminderCount,
    DateTimeOffset? LastRemindedAtUtc,
    DateTimeOffset? CallLoggedAtUtc,
    long Version)
{
    public bool IsRevoked => RevokedAtUtc is not null;

    /// <summary>The batch the next item joins: one more than the batches already settled.</summary>
    public int CurrentBatch => SettledBatches + 1;

    /// <summary>Whether the sender has pressed Send since the last settle, or the link has run out with photos on it.</summary>
    public bool HasUnsettledSend(DateTimeOffset nowUtc) =>
        SettledAtUtc is null
            ? SentAtUtc is not null || (nowUtc >= ExpiresAtUtc && AcceptedItemCount > 0)
            : (SentAtUtc is { } sent && sent > SettledAtUtc)
                || (nowUtc >= ExpiresAtUtc && LastItemAtUtc is { } last && last > SettledAtUtc);
}

/// <summary>
/// One photo, document or video a capture received. <see cref="ClientItemId"/>
/// is the id the phone minted when it took the picture, so a retry of the same
/// upload is the same item.
/// </summary>
public sealed record PhotoCaptureItem(
    Guid Id,
    Guid CaptureId,
    Guid ClientItemId,
    PhotoCaptureItemKind Kind,
    string? Area,
    PhotoCaptureStage? Stage,
    int Position,
    int Batch,
    int Ordinal,
    int StoryGroup,
    int StorySequence,
    string FileName,
    string MediaType,
    DateTimeOffset? TakenAtUtc,
    bool LiveCamera,
    bool InReportOnArrival,
    string ContentHash,
    long ContentLength,
    Guid? StagedReceiptId,
    Guid? IntakeReceiptId,
    Guid? IntakeAssetId,
    bool OriginalReceived,
    DateTimeOffset ReceivedAtUtc);

/// <summary>
/// A standing link: the URL an AMC or bodyshop keeps on WhatsApp or prints as
/// a QR code, or the one link on the Collision Engineers website. It only ever
/// starts a capture; it never accepts a photo itself.
/// <see cref="ContactActive"/> is whether its contact is active; the website
/// link, which has none, always is.
/// </summary>
public sealed record StandingPhotoLink(
    Guid Id,
    StandingPhotoLinkKind Kind,
    Guid? OrganizationId,
    string? OrganizationName,
    Guid? PrincipalId,
    bool IsRepairer,
    bool ContactActive,
    string Slug,
    PhotoCaptureMode ForwardedMode,
    bool LiveCameraOnly,
    int LinkLifetimeDays,
    bool OriginalsInBackground,
    DateTimeOffset? PausedAtUtc,
    DateTimeOffset? RetiredAtUtc,
    DateTimeOffset CreatedAtUtc,
    long Version)
{
    /// <summary>Neither paused nor retired: what the link's Active or Paused chip shows.</summary>
    public bool IsActive => PausedAtUtc is null && RetiredAtUtc is null;

    /// <summary>
    /// Whether the link starts captures: it is active and its contact is not
    /// deactivated. A deactivated contact's link answers like an unknown one
    /// until the contact is active again (operator, 9 November 2026; R796).
    /// </summary>
    public bool StartsCaptures => IsActive && ContactActive;
}

/// <summary>A saved address a standing link offers as an inspection location on its form.</summary>
public sealed record StandingPhotoLinkAddress(string Name, string Address);

/// <summary>One token issued for a capture: the link in one message, one copy, one hand-off or one phone session.</summary>
public enum PhotoLinkTokenPurpose
{
    Email,
    Copy,
    Handoff,
    Session
}

public sealed record NewPhotoCapture(
    Guid Id,
    PhotoCaptureOrigin Origin,
    Guid? StandingLinkId,
    Guid? OrganizationId,
    Guid? CaseId,
    PhotoCaptureDestination Destination,
    string? NormalizedRegistration,
    DateOnly? IncidentDate,
    string? CustomerName,
    string? Mobile,
    string? Email,
    PhotoCaptureMode Mode,
    bool LiveCameraOnly,
    bool OriginalsInBackground,
    Guid? IssuedByStaffId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed record NewPhotoCaptureItem(
    Guid Id,
    Guid CaptureId,
    Guid ClientItemId,
    PhotoCaptureItemKind Kind,
    string? Area,
    PhotoCaptureStage? Stage,
    int Position,
    int Batch,
    int Ordinal,
    int StoryGroup,
    int StorySequence,
    string FileName,
    string MediaType,
    DateTimeOffset? TakenAtUtc,
    bool LiveCamera,
    bool InReportOnArrival,
    string ContentHash,
    long ContentLength,
    DateTimeOffset ReceivedAtUtc);

/// <summary>What one item insert did: a new item, the same item again, or a different file under the same client id.</summary>
public enum PhotoCaptureItemWrite
{
    Created,
    Replayed,
    Conflict,
    CapReached
}

public sealed record PhotoCaptureItemWriteResult(PhotoCaptureItemWrite Write, PhotoCaptureItem? Item);

/// <summary>What the settle sweep reads for one capture: the capture and its items in story order.</summary>
public sealed record PhotoCaptureWithItems(PhotoCapture Capture, IReadOnlyList<PhotoCaptureItem> Items);

public sealed record PhotoCaptureListFilter(
    bool SentByUs,
    string? Search,
    PhotoCaptureState? State,
    PhotoCaptureOrigin? Origin,
    int PageSize,
    int Page);

/// <summary>
/// How a Coming in capture reached its Case: it filed there as its photos
/// arrived, or its Vehicle images record was paired with the Case, by Pegasus
/// or by a member of staff. <see cref="None"/> for a capture with no Case and
/// for a Case link, which was sent from its Case.
/// </summary>
public enum PhotoCapturePairing
{
    None,
    FiledAsItArrived,
    MatchedAutomatically,
    ChosenByStaff
}

/// <summary>
/// One list row. <see cref="OrganizationName"/> is the From: the standing
/// link's contact, or for a Case link the Case's Principal (R805), read when
/// the row is shown. <see cref="Pairing"/> is read only for the rows shown.
/// </summary>
public sealed record PhotoCaptureListRow(
    PhotoCapture Capture,
    string? OrganizationName,
    string? ImageIntakeReference,
    Guid? ImageIntakeId,
    IReadOnlyList<string> MissingItems,
    string? IssuedByName,
    PhotoCapturePairing Pairing);

public sealed record PhotoCaptureListPage(IReadOnlyList<PhotoCaptureListRow> Rows, int TotalCount);

/// <summary>The staff acts on a link that name who did them in its history (FRD-28 Staff pages; R805).</summary>
public static class PhotoLinkActs
{
    public const string Cancelled = "photo_link_cancelled";
    public const string Extended = "photo_link_extended";
    public const string CallLogged = "photo_link_call_logged";
    public const string Reminded = "photo_link_reminded";
}

/// <summary>One staff act on a link: which act (<see cref="PhotoLinkActs"/>), when, and who did it, null when no name is known.</summary>
public sealed record PhotoLinkAct(string Kind, DateTimeOffset AtUtc, string? ActorName);

/// <summary>Where a capture's items sit on a Case, for the Files → Images story (FRD-16).</summary>
public sealed record CasePhotoCaptureStory(
    PhotoCapture Capture,
    IReadOnlyList<PhotoCaptureItem> Items,
    string? OrganizationName);

/// <summary>The capture store: writes are replay-safe by operation key where one is taken.</summary>
public interface IPhotoCaptureStore
{
    Task<PhotoCapture> CreateAsync(
        NewPhotoCapture capture,
        string tokenDigest,
        PhotoLinkTokenPurpose tokenPurpose,
        CancellationToken cancellationToken);

    Task<PhotoCapture?> GetAsync(Guid captureId, CancellationToken cancellationToken);

    /// <summary>The capture one presented token names, with whether that token is itself revoked.</summary>
    Task<(PhotoCapture Capture, bool TokenRevoked)?> FindByTokenDigestAsync(
        string tokenDigest,
        CancellationToken cancellationToken);

    Task AddTokenAsync(
        Guid captureId,
        string tokenDigest,
        PhotoLinkTokenPurpose purpose,
        Guid? issuedByStaffId,
        DateTimeOffset issuedAtUtc,
        CancellationToken cancellationToken);

    Task<PhotoCapture> RecordOpenedAsync(Guid captureId, DateTimeOffset openedAtUtc, CancellationToken cancellationToken);

    Task<PhotoCapture> RecordDetailsAsync(
        Guid captureId,
        long expectedVersion,
        string? normalizedRegistration,
        DateOnly? incidentDate,
        string? customerName,
        string? mobile,
        string? email,
        PhotoCaptureDetails details,
        CancellationToken cancellationToken);

    Task<PhotoCapture> RecordRoutingAsync(
        Guid captureId,
        long expectedVersion,
        PhotoCaptureDestination destination,
        Guid? caseId,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// Inserts the item inside the capture's caps: the item count, the bytes
    /// and the per-kind totals <see cref="CaptureLimits"/> names. The same
    /// client id with the same hash is a replay; with a different hash a
    /// conflict. Also stamps the capture's first and last item times.
    /// </summary>
    Task<PhotoCaptureItemWriteResult> AddItemAsync(
        NewPhotoCaptureItem item,
        CancellationToken cancellationToken);

    Task RecordItemReceiptAsync(
        Guid itemId,
        Guid stagedReceiptId,
        CancellationToken cancellationToken);

    Task RecordItemProcessedAsync(
        Guid itemId,
        Guid intakeReceiptId,
        Guid? intakeAssetId,
        CancellationToken cancellationToken);

    Task RecordItemOriginalAsync(Guid itemId, CancellationToken cancellationToken);

    Task<PhotoCapture> RecordSentAsync(
        Guid captureId,
        string? note,
        DateTimeOffset sentAtUtc,
        CancellationToken cancellationToken);

    Task<PhotoCapture> RecordSettledAsync(
        Guid captureId,
        long expectedVersion,
        Guid? submissionGroupId,
        DateTimeOffset settledAtUtc,
        CancellationToken cancellationToken);

    // The four staff acts each record who did it (PhotoLinkActs) in the same
    // write. A replayed operation key changes nothing and answers the capture
    // as it is, so a double click acts once.

    Task<PhotoCapture> RevokeAsync(
        Guid captureId,
        ActionActor actor,
        string operationKey,
        DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken);

    Task<PhotoCapture> ExtendAsync(
        Guid captureId,
        ActionActor actor,
        string operationKey,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken);

    Task<PhotoCapture> RecordReminderAsync(
        Guid captureId,
        ActionActor actor,
        string operationKey,
        DateTimeOffset remindedAtUtc,
        CancellationToken cancellationToken);

    Task<PhotoCapture> RecordCallAsync(
        Guid captureId,
        ActionActor actor,
        string operationKey,
        DateTimeOffset calledAtUtc,
        CancellationToken cancellationToken);

    /// <summary>Captures with a Send, or an expiry with photos, that no settle has recorded yet; oldest first.</summary>
    Task<IReadOnlyList<PhotoCaptureWithItems>> ListAwaitingSettleAsync(
        DateTimeOffset nowUtc,
        int maximumItems,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PhotoCaptureItem>> ListItemsAsync(Guid captureId, CancellationToken cancellationToken);

    Task<PhotoCaptureItem?> FindItemAsync(Guid captureId, Guid clientItemId, CancellationToken cancellationToken);

    /// <summary>
    /// Settled standing captures that have since reached a Case: their Vehicle
    /// images record merged into one, or their group's members were filed on
    /// one. Each carries the Case it reached; the sweep then records the Case
    /// on the capture and finishes the Case-side work.
    /// </summary>
    Task<IReadOnlyList<(PhotoCaptureWithItems Capture, Guid CaseId, string CaseReference)>> ListSettledReachingCaseAsync(
        int maximumItems,
        CancellationToken cancellationToken);

    Task<PhotoCapture> RecordCaseAsync(Guid captureId, Guid caseId, string caseReference, CancellationToken cancellationToken);

    /// <summary>Whether every member of the capture's current batch has a processed receipt, so the batch can settle.</summary>
    Task<bool> AllItemsProcessedAsync(Guid captureId, int batch, CancellationToken cancellationToken);
}

/// <summary>Reads for the staff surfaces.</summary>
public interface IPhotoCaptureQueries
{
    Task<PhotoCaptureListPage> ListAsync(PhotoCaptureListFilter filter, CancellationToken cancellationToken);

    Task<PhotoCaptureListRow?> GetRowAsync(Guid captureId, CancellationToken cancellationToken);

    /// <summary>Every capture whose items are on the Case, with the items, for the Files → Images story.</summary>
    Task<IReadOnlyList<CasePhotoCaptureStory>> ListForCaseAsync(Guid caseId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CasePhotoCaptureStory>> ListForImageIntakeAsync(Guid imageIntakeId, CancellationToken cancellationToken);

    /// <summary>How many captures each of the five Links we sent and Coming in headline states holds.</summary>
    Task<IReadOnlyDictionary<PhotoCaptureState, int>> CountByStateAsync(bool sentByUs, CancellationToken cancellationToken);

    /// <summary>The staff acts on the link, oldest first, each with the name of who did it.</summary>
    Task<IReadOnlyList<PhotoLinkAct>> ListHistoryAsync(Guid captureId, CancellationToken cancellationToken);
}

public interface IStandingPhotoLinkStore
{
    Task<StandingPhotoLink?> FindBySlugAsync(string slug, CancellationToken cancellationToken);

    Task<StandingPhotoLink?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<StandingPhotoLink?> FindForOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<StandingPhotoLink?> FindWebsiteAsync(CancellationToken cancellationToken);

    Task<StandingPhotoLink> CreateAsync(
        StandingPhotoLinkKind kind,
        Guid? organizationId,
        string slug,
        PhotoCaptureMode forwardedMode,
        bool liveCameraOnly,
        int linkLifetimeDays,
        bool originalsInBackground,
        ActionActor actor,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken);

    Task<StandingPhotoLink> UpdateDefaultsAsync(
        Guid id,
        long expectedVersion,
        PhotoCaptureMode forwardedMode,
        bool liveCameraOnly,
        int linkLifetimeDays,
        bool originalsInBackground,
        CancellationToken cancellationToken);

    Task<StandingPhotoLink> SetPausedAsync(Guid id, long expectedVersion, DateTimeOffset? pausedAtUtc, CancellationToken cancellationToken);

    /// <summary>Ends the link for good: its slug answers like an unknown one and the lookups skip it (R796).</summary>
    Task<StandingPhotoLink> SetRetiredAsync(Guid id, long expectedVersion, DateTimeOffset retiredAtUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Retires the slug and issues a new one; the old slug answers like an
    /// unknown one from then on. A slug another link holds is refused as
    /// <see cref="StandingPhotoLinkError.SlugTaken"/>.
    /// </summary>
    Task<StandingPhotoLink> ReplaceSlugAsync(Guid id, long expectedVersion, string newSlug, DateTimeOffset replacedAtUtc, CancellationToken cancellationToken);

    /// <summary>The saved addresses the link offers on its form: the contact's inspection locations.</summary>
    Task<IReadOnlyList<StandingPhotoLinkAddress>> ListAddressesAsync(Guid standingLinkId, CancellationToken cancellationToken);

    /// <summary>How many captures the standing link started since <paramref name="sinceUtc"/>, for the daily mint cap.</summary>
    Task<int> CountMintedSinceAsync(Guid standingLinkId, DateTimeOffset sinceUtc, CancellationToken cancellationToken);
}

/// <summary>
/// The Case facts a capture's settle needs and writes: the Case's registration
/// and whether it is still open to system work, the images-complete
/// transition, and the fill of its empty fields from the sender's details.
/// </summary>
public interface IPhotoCaptureCaseWriter
{
    /// <summary>Marks the Case's images complete as system work; a Case that is closed, archived or a Triage records nothing.</summary>
    Task CompleteImagesAsync(Guid caseId, string operationKey, string reason, CancellationToken cancellationToken);

    /// <summary>
    /// Fills each named Case field that holds neither an extracted fact nor a
    /// confirmed value, as a Fact with "Photo link" provenance; returns the
    /// field names it filled. Nothing already on the Case is replaced.
    /// </summary>
    Task<IReadOnlyList<string>> FillEmptyFieldsAsync(
        Guid caseId,
        IReadOnlyDictionary<string, string> values,
        Guid captureId,
        string sourceLabel,
        CancellationToken cancellationToken);

    /// <summary>The Case's assigned Engineer, or null when it has none or no longer exists.</summary>
    Task<Guid?> AssignedEngineerAsync(Guid caseId, CancellationToken cancellationToken);
}

/// <summary>
/// Files a full-size original the phone sent after its compressed picture as
/// the next version of the same Case image, so the Case keeps one image with
/// the original as its current content.
/// </summary>
public interface IFilePhotoCaptureOriginal
{
    Task<bool> FileAsync(
        PhotoCaptureItem item,
        string fileName,
        string mediaType,
        long contentLength,
        string sha256,
        Func<CancellationToken, ValueTask<Stream>> openContentAsync,
        CancellationToken cancellationToken);
}
