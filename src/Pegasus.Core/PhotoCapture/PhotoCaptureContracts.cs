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

/// <summary>
/// Who a quick link sent from a Case is for (R808): the instructing firm's
/// staff, or a bodyshop's. Each opens the handover app's own start page and
/// form for that kind of firm; a guided link has none.
/// </summary>
public enum PhotoCaptureQuickLinkFor
{
    AccidentManagement,
    Bodyshop
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
/// <see cref="NoKeys"/> is the sender's "Don't have the keys?" skip of the
/// dashboard (R799).
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
    IReadOnlyList<string>? DamageAreas = null,
    bool NoKeys = false)
{
    public static PhotoCaptureDetails Empty { get; } = new();

    public IReadOnlyList<string> MarkedAreas => DamageAreas ?? [];
}

/// <summary>
/// One vehicle's photo link and everything it received: the capture. It is
/// add-only until it expires, so the same link reopened adds to this record.
/// <see cref="QuickLinkFor"/> is who a quick link sent from a Case is for
/// (R808); null for every other link.
/// <see cref="ImagesCompletedAtUtc"/> is when the capture's settle first
/// completed, or found complete, its Case's images: once per capture, so a
/// later batch never completes them again after staff marked them incomplete
/// (operator, 9 November 2026; R792).
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
    PhotoCaptureQuickLinkFor? QuickLinkFor,
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
    long Version,
    DateTimeOffset? ImagesCompletedAtUtc = null)
{
    public bool IsRevoked => RevokedAtUtc is not null;

    /// <summary>The batch the next item joins: one more than the batches already settled.</summary>
    public int CurrentBatch => SettledBatches + 1;

    /// <summary>
    /// Whether the open batch is due to settle, should it hold a photo: the
    /// sender has pressed Send since the last settle, or the link has expired
    /// or was cancelled, whose photos still settle (operator, 9 November
    /// 2026; R794). The store's settle listing reads the same rule in SQL.
    /// </summary>
    public bool IsDueToSettle(DateTimeOffset nowUtc) =>
        IsRevoked
        || nowUtc >= ExpiresAtUtc
        || (SentAtUtc is { } sent && (SettledAtUtc is null || sent > SettledAtUtc));

    /// <summary>
    /// Whether the phone sends full-size originals: only while the capture
    /// files on its Case as each photo arrives. Until there is a Case the app
    /// is told originals are off, and when a Vehicle images record later
    /// reaches a Case the compressed pictures stand (operator, 9 November
    /// 2026; R801).
    /// </summary>
    public bool TakesOriginals =>
        OriginalsInBackground && Destination == PhotoCaptureDestination.Case && SubmissionGroupId is null;

    /// <summary>
    /// The Case an item received now declares to intake: the capture's Case
    /// while its photos file there as they arrive, else none. The store
    /// decides it with the item's batch, under the capture's lock, and the
    /// item keeps it for every retry of its upload.
    /// </summary>
    public Guid? DeclaredCaseId => Destination == PhotoCaptureDestination.Case ? CaseId : null;
}

/// <summary>
/// One photo, document or video a capture received. <see cref="ClientItemId"/>
/// is the id the phone minted when it took the picture, so a retry of the same
/// upload is the same item. <see cref="DeclaredCaseId"/> is the Case it
/// declares to intake (<see cref="PhotoCapture.DeclaredCaseId"/> when it
/// joined its batch).
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
    DateTimeOffset ReceivedAtUtc,
    Guid? DeclaredCaseId = null);

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

/// <summary>One address staff saved on a standing link's Photo links tab, which they may edit or remove (R806).</summary>
public sealed record SavedStandingPhotoLinkAddress(Guid Id, string Name, string Address);

/// <summary>
/// A standing link's saved addresses as its Photo links tab shows them: the
/// ones Pegasus takes from the contact (its own address, or a Principal's
/// inspection locations), read-only, and the ones staff saved on the tab
/// (operator, 9 November 2026; R806, R815).
/// </summary>
public sealed record StandingPhotoLinkAddressBook(
    IReadOnlyList<StandingPhotoLinkAddress> FromContact,
    IReadOnlyList<SavedStandingPhotoLinkAddress> Saved)
{
    public bool IsEmpty => FromContact.Count == 0 && Saved.Count == 0;
}

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
    PhotoCaptureQuickLinkFor? QuickLinkFor,
    bool LiveCameraOnly,
    bool OriginalsInBackground,
    Guid? IssuedByStaffId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? OpenedAtUtc);

/// <summary>
/// An item to insert. Its batch, its ordinal in the batch and the Case it
/// declares are not the caller's: the store decides them from the capture as
/// it is under its lock (<see cref="IPhotoCaptureStore.AddItemAsync"/>).
/// </summary>
public sealed record NewPhotoCaptureItem(
    Guid Id,
    Guid CaptureId,
    Guid ClientItemId,
    PhotoCaptureItemKind Kind,
    string? Area,
    PhotoCaptureStage? Stage,
    int Position,
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

/// <summary>A capture and its items in story order.</summary>
public sealed record PhotoCaptureWithItems(PhotoCapture Capture, IReadOnlyList<PhotoCaptureItem> Items);

/// <summary>Where a settle listing stopped: the last capture it returned, in (created, id) order.</summary>
public sealed record PhotoCaptureSettleCursor(DateTimeOffset CreatedAtUtc, Guid Id);

/// <summary>
/// One capture the settle works on, with its items in story order and the
/// Case it works against: the capture's own Case, or for a capture whose
/// Vehicle images record has reached a Case, that Case.
/// <see cref="CaseArchived"/> is whether that Case is archived (R793);
/// <see cref="CaseIsTriage"/> whether it is a Triage Case, which keeps no
/// Notes.
/// </summary>
public sealed record PhotoCaptureSettleCandidate(
    PhotoCapture Capture,
    IReadOnlyList<PhotoCaptureItem> Items,
    Guid? CaseId,
    bool CaseArchived,
    bool CaseIsTriage);

/// <summary>
/// The submission group a Vehicle images batch becomes when it closes: the
/// registration and the accident date its sender typed, so the record never
/// pairs with a Case whose date contradicts it (operator, 9 November 2026;
/// R791).
/// </summary>
public sealed record PhotoCaptureDeclaredGroup(string Registration, DateOnly? IncidentDate);

/// <summary>
/// What closing a batch did: the batch had closed already, an upload into it
/// was still in flight (<see cref="SettlePhotoCaptures.UploadInFlightGrace"/>),
/// or it closed now with the items it holds.
/// </summary>
public enum PhotoCaptureBatchClose
{
    AlreadyClosed,
    Waiting,
    Closed
}

/// <summary>
/// The close's outcome, the capture as the close left it and, when it
/// <see cref="PhotoCaptureBatchClose.Closed"/>, the batch's received items in
/// story order: nothing joins the batch after it.
/// </summary>
public sealed record PhotoCaptureBatchClosed(
    PhotoCaptureBatchClose Outcome,
    PhotoCapture Capture,
    IReadOnlyList<PhotoCaptureItem> Items);

/// <summary>The capture after <see cref="IPhotoCaptureStore.RecordCaseAsync"/>, and whether it recorded the Case now.</summary>
public sealed record PhotoCaptureCaseRecorded(PhotoCapture Capture, bool Changed);

/// <summary>
/// One list's filter. <see cref="NoCaseYet"/> keeps only the captures with no
/// Case, Coming in's "No case yet" (R806).
/// </summary>
public sealed record PhotoCaptureListFilter(
    bool SentByUs,
    string? Search,
    PhotoCaptureState? State,
    PhotoCaptureOrigin? Origin,
    bool NoCaseYet,
    int PageSize,
    int Page);

/// <summary>How many captures each tab holds, counted together (R806).</summary>
public sealed record PhotoCaptureTabCounts(int ComingIn, int LinksWeSent);

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
/// <see cref="CaseArchived"/> is whether the capture's Case is archived: its
/// link is then dead and reads Expired (R793). <see cref="Photographs"/> is
/// how many of its items are photographs, not paperwork or video: what the
/// Photos column counts and what decides Opened or Photos coming in.
/// <see cref="SentOnToCustomer"/> is whether a quick Case link's firm sent it
/// on to their customer (R798), read only for the rows shown.
/// </summary>
public sealed record PhotoCaptureListRow(
    PhotoCapture Capture,
    string? OrganizationName,
    string? ImageIntakeReference,
    Guid? ImageIntakeId,
    IReadOnlyList<string> MissingItems,
    string? IssuedByName,
    PhotoCapturePairing Pairing,
    bool CaseArchived,
    int Photographs,
    bool SentOnToCustomer);

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

/// <summary>
/// Where a capture's items sit on a Case, for the Files → Images story
/// (FRD-16). <see cref="StandingLinkIsRepairer"/> is what the required items
/// need to know (a repairer's own staff owe the VIN), so the Case page derives
/// the link's status in the lists' own way.
/// </summary>
public sealed record CasePhotoCaptureStory(
    PhotoCapture Capture,
    IReadOnlyList<PhotoCaptureItem> Items,
    string? OrganizationName,
    bool StandingLinkIsRepairer);

/// <summary>The capture store: writes are replay-safe by operation key where one is taken.</summary>
public interface IPhotoCaptureStore
{
    /// <summary>
    /// Creates the capture with its first token and, when
    /// <paramref name="sessionTokenDigest"/> is given, a Session token for
    /// the phone that made it (a hand-off's staff phone), in one save.
    /// </summary>
    Task<PhotoCapture> CreateAsync(
        NewPhotoCapture capture,
        string tokenDigest,
        PhotoLinkTokenPurpose tokenPurpose,
        string? sessionTokenDigest,
        CancellationToken cancellationToken);

    Task<PhotoCapture?> GetAsync(Guid captureId, CancellationToken cancellationToken);

    /// <summary>
    /// The capture one presented token names, with whether that token is
    /// itself revoked, whether the capture's Case is archived (R793), and the
    /// token's purpose.
    /// </summary>
    Task<(PhotoCapture Capture, bool TokenRevoked, bool CaseArchived, PhotoLinkTokenPurpose Purpose)?> FindByTokenDigestAsync(
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

    /// <summary>
    /// Records where a standing capture's photos go. Under the capture row's
    /// lock: once it holds an item row its routing stands, so a different one
    /// is refused with <see cref="PhotoCaptureRefusals.RegistrationLocked"/>
    /// and the same one changes nothing.
    /// </summary>
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
    /// conflict. Under the capture row's lock it is the one owner of an
    /// item's batch, its ordinal and the Case it declares, all taken from the
    /// capture as it is then (<see cref="PhotoCapture.CurrentBatch"/>,
    /// <see cref="PhotoCapture.DeclaredCaseId"/>). A replayed item that never
    /// staged a receipt, whose batch has settled since, joins the open batch
    /// with the capture's Case as it is now.
    /// </summary>
    Task<PhotoCaptureItemWriteResult> AddItemAsync(
        NewPhotoCaptureItem item,
        CancellationToken cancellationToken);

    Task RecordItemReceiptAsync(
        Guid itemId,
        Guid stagedReceiptId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Counts one full-size original of the item against its capture's caps,
    /// under the capture row's lock: the originals per link and the bytes per
    /// link <see cref="CaptureLimits"/> names. False, counting nothing, when
    /// the original would pass either.
    /// </summary>
    Task<bool> ReserveOriginalAsync(Guid itemId, long contentLength, CancellationToken cancellationToken);

    /// <summary>Gives back what <see cref="ReserveOriginalAsync"/> counted for an original that did not file.</summary>
    Task ReleaseOriginalAsync(Guid itemId, long contentLength, CancellationToken cancellationToken);

    /// <summary>Marks the item's original received; what it counted was reserved before it filed.</summary>
    Task RecordItemOriginalAsync(Guid itemId, CancellationToken cancellationToken);

    Task<PhotoCapture> RecordSentAsync(
        Guid captureId,
        string? note,
        DateTimeOffset sentAtUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Closes the capture's open batch <paramref name="batch"/>, under the
    /// capture row's lock, the one an item add takes, so no item joins it
    /// after: <see cref="PhotoCaptureBatchClose.AlreadyClosed"/> when a settle
    /// closed it before; <see cref="PhotoCaptureBatchClose.Waiting"/>, writing
    /// nothing, while an item in it has no receipt yet and arrived within
    /// <see cref="SettlePhotoCaptures.UploadInFlightGrace"/>; otherwise
    /// <see cref="PhotoCaptureBatchClose.Closed"/>, recording the settle. With
    /// <paramref name="group"/> the batch's received photographs, and only
    /// they, become one submission group in the same transaction, its
    /// expected count the number of them; paperwork and video wait outside it
    /// (operator, 9 November 2026; R803).
    /// </summary>
    Task<PhotoCaptureBatchClosed> CloseBatchAsync(
        Guid captureId,
        int batch,
        DateTimeOffset nowUtc,
        PhotoCaptureDeclaredGroup? group,
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

    /// <summary>
    /// One page, oldest first after <paramref name="after"/>, of the captures
    /// whose open batch holds a received photograph and whose sender has
    /// pressed Send since the last settle, or whose link has expired or was
    /// cancelled (operator, 9 November 2026; R794). Paperwork or video alone
    /// lists nothing (R792).
    /// </summary>
    Task<IReadOnlyList<PhotoCaptureSettleCandidate>> ListAwaitingSettleAsync(
        DateTimeOffset nowUtc,
        PhotoCaptureSettleCursor? after,
        int maximumItems,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PhotoCaptureItem>> ListItemsAsync(Guid captureId, CancellationToken cancellationToken);

    Task<PhotoCaptureItem?> FindItemAsync(Guid captureId, Guid clientItemId, CancellationToken cancellationToken);

    /// <summary>
    /// Settled standing captures with no Case that have since reached one:
    /// their Vehicle images record merged into one, or their group's members
    /// were filed on one. Each carries the Case it reached as
    /// <see cref="PhotoCaptureSettleCandidate.CaseId"/>; the sweep finishes
    /// the Case-side work and then records the Case on the capture.
    /// </summary>
    Task<IReadOnlyList<PhotoCaptureSettleCandidate>> ListSettledReachingCaseAsync(
        int maximumItems,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the Case on a capture that has none, reading its reference,
    /// with the routing reason when one is given and
    /// <paramref name="imagesCompletedAtUtc"/> as its images marker when it
    /// has none. A capture that already has a Case is left as it is and
    /// <see cref="PhotoCaptureCaseRecorded.Changed"/> is false.
    /// </summary>
    Task<PhotoCaptureCaseRecorded> RecordCaseAsync(
        Guid captureId,
        Guid caseId,
        string? routingReason,
        DateTimeOffset? imagesCompletedAtUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether every received photograph in the batch has settled: its
    /// receipt evaluated, or its work failed for good (F-R12); and with
    /// <paramref name="caseId"/>, also filed on that Case or its association
    /// since moved elsewhere. Paperwork and video are not waited for.
    /// </summary>
    Task<bool> AllItemsSettledAsync(Guid captureId, int batch, Guid? caseId, CancellationToken cancellationToken);
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

    /// <summary>How many captures Coming in and Links we sent each hold, from one count by origin.</summary>
    Task<PhotoCaptureTabCounts> CountByTabAsync(CancellationToken cancellationToken);

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

    /// <summary>
    /// The saved addresses the link offers on its form: the contact's own
    /// address or a Principal's inspection locations first, then the ones
    /// saved on its Photo links tab (R815).
    /// </summary>
    Task<IReadOnlyList<StandingPhotoLinkAddress>> ListAddressesAsync(Guid standingLinkId, CancellationToken cancellationToken);

    /// <summary>The same addresses split as the Photo links tab shows them, the saved ones with their ids.</summary>
    Task<StandingPhotoLinkAddressBook> GetAddressBookAsync(Guid standingLinkId, CancellationToken cancellationToken);

    // A saved address is added, changed and removed under the link's version,
    // which each of them moves on, so a change over a stale read is refused as
    // StaleVersion, as the link's other changes are.

    Task<StandingPhotoLink> AddAddressAsync(
        Guid id,
        long expectedVersion,
        string name,
        string address,
        DateTimeOffset addedAtUtc,
        CancellationToken cancellationToken);

    Task<StandingPhotoLink> ChangeAddressAsync(
        Guid id,
        long expectedVersion,
        Guid addressId,
        string name,
        string address,
        CancellationToken cancellationToken);

    Task<StandingPhotoLink> RemoveAddressAsync(
        Guid id,
        long expectedVersion,
        Guid addressId,
        CancellationToken cancellationToken);

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
    /// <summary>
    /// Marks the Case's images complete as system work, once per capture: in
    /// the same transaction, under the Case's workflow lock, it records the
    /// capture's <see cref="PhotoCapture.ImagesCompletedAtUtc"/>, and a capture
    /// that already has one changes nothing (operator, 9 November 2026;
    /// R792). A Case that is closed, archived, a Triage or already complete
    /// records only the capture's marker.
    /// </summary>
    Task CompleteImagesAsync(Guid caseId, Guid captureId, string operationKey, string reason, CancellationToken cancellationToken);

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
