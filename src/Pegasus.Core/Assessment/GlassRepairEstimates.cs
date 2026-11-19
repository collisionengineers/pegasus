using System.Buffers;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Assessment;

public enum GlassRepairEstimateSessionState
{
    Prepared, Launching, Active, Importing, AwaitingImport, Completed, Failed, Unknown, Expired, Cancelled
}

public static class GlassRepairEstimateSessionPolicy
{
    /// <summary>
    /// Whether two registrations are the same, ignoring case and any spacing
    /// in either. Each is compacted into a stack buffer (a rented one past
    /// 1,024 characters) and the two are compared there, so no string is built.
    /// </summary>
    public static bool SameRegistration(string? left, string? right)
    {
        var leftText = left.AsSpan();
        var rightText = right.AsSpan();
        char[]? leftRented = null;
        char[]? rightRented = null;
        Span<char> leftBuffer = leftText.Length <= StackBufferLength
            ? stackalloc char[leftText.Length]
            : (leftRented = ArrayPool<char>.Shared.Rent(leftText.Length));
        Span<char> rightBuffer = rightText.Length <= StackBufferLength
            ? stackalloc char[rightText.Length]
            : (rightRented = ArrayPool<char>.Shared.Rent(rightText.Length));
        try
        {
            ReadOnlySpan<char> leftCompact = leftBuffer[..CompactInto(leftText, leftBuffer)];
            ReadOnlySpan<char> rightCompact = rightBuffer[..CompactInto(rightText, rightBuffer)];
            return MemoryExtensions.Equals(leftCompact, rightCompact, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (leftRented is not null)
            {
                ArrayPool<char>.Shared.Return(leftRented);
            }

            if (rightRented is not null)
            {
                ArrayPool<char>.Shared.Return(rightRented);
            }
        }
    }

    /// <summary>
    /// The registration with its whitespace left out: compacted once, however
    /// many texts <see cref="NamesCompactedRegistration"/> then tests. One
    /// pass into a stack buffer and one new string, or the registration itself
    /// when it has no whitespace.
    /// </summary>
    public static string CompactRegistration(string? registration)
    {
        if (string.IsNullOrEmpty(registration))
        {
            return string.Empty;
        }

        Span<char> compact = registration.Length <= StackBufferLength
            ? stackalloc char[registration.Length]
            : new char[registration.Length];
        var length = CompactInto(registration, compact);

        return length == registration.Length ? registration : new string(compact[..length]);
    }

    /// <summary>
    /// Whether printed text names the registration, ignoring case and any
    /// spacing in either. The text is compacted into a stack buffer sized for
    /// it (a rented one for a long page), so no string is built for any page.
    /// </summary>
    public static bool NamesCompactedRegistration(string? text, string compactRegistration)
    {
        if (compactRegistration.Length == 0 || string.IsNullOrEmpty(text))
        {
            return false;
        }

        char[]? rented = null;
        Span<char> compact = text.Length <= StackBufferLength
            ? stackalloc char[text.Length]
            : (rented = ArrayPool<char>.Shared.Rent(text.Length));
        try
        {
            ReadOnlySpan<char> compacted = compact[..CompactInto(text, compact)];
            return compacted.Contains(compactRegistration, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    /// <summary>The characters of the text that are not whitespace, written to the start of the buffer; how many.</summary>
    private static int CompactInto(ReadOnlySpan<char> text, Span<char> destination)
    {
        var length = 0;
        foreach (var character in text)
        {
            if (!char.IsWhiteSpace(character))
            {
                destination[length++] = character;
            }
        }

        return length;
    }

    private const int StackBufferLength = 1024;

    /// <summary>
    /// The registration and mileage a staff member's own session continues
    /// on. A stock vehicle the session recorded is held at Glass's under the
    /// registration and mileage it was made with: the Case must still record
    /// that registration, and a corrected mileage continues on the vehicle's
    /// own. A placeholder holds neither at Glass's, and a session that has
    /// recorded no vehicle yet holds nothing there, so each follows what the
    /// Case records now (operator, 9 October 2026; issue 1070).
    /// </summary>
    public static (string Registration, long MileageMiles) RequireUnchangedVehicle(
        string recordedRegistration, long recordedMileage, bool placeholder, bool vehicleRecorded,
        string registration, long mileage) =>
        ContinuingVehicle(
            recordedRegistration, recordedMileage, followsCase: placeholder || !vehicleRecorded,
            registration, mileage,
            "The Case registration has changed since this Glass's session started. "
            + "The session still holds the account. Restore the original vehicle details to resume, "
            + "or close the external session and confirm its closure before launching again.");

    /// <summary>
    /// The registration and mileage a repair spec's Glass's estimate is
    /// reopened on, under the same rule as a live session
    /// (<see cref="RequireUnchangedVehicle"/>); nothing is held at Glass's
    /// while it is refused.
    /// </summary>
    public static (string Registration, long MileageMiles) RequireUnchangedEstimateVehicle(
        GlassEstimateLink link, string registration, long mileage) =>
        ContinuingVehicle(
            link.Registration, link.MileageMiles, followsCase: link.Placeholder,
            registration, mileage,
            "The Case registration has changed since this Glass's estimate was started. "
            + "Restore the original vehicle details to reopen it.");

    private static (string Registration, long MileageMiles) ContinuingVehicle(
        string recordedRegistration, long recordedMileage, bool followsCase,
        string registration, long mileage, string refusal)
    {
        if (!followsCase && !SameRegistration(recordedRegistration, registration))
        {
            throw new GlassRepairEstimateRefusalException(refusal);
        }

        return followsCase ? (registration, mileage) : (recordedRegistration, recordedMileage);
    }

    /// <summary>
    /// The operation key of the reopen a build's own work makes of the spec it
    /// landed (operator, 18 November 2026; R1171): the build's own key with a
    /// suffix. Operation keys are unique, so it names that one reopen and no
    /// other session, and a second run of it is a replay that sends nothing.
    /// </summary>
    public static string BuildReopenOperationKey(GlassRepairEstimateSession build)
    {
        ArgumentNullException.ThrowIfNull(build);
        return build.OperationKey + ":reopen";
    }

    /// <summary>
    /// The build whose list the member of staff sees on the Case (R1173):
    /// their newest session's, or, while their newest session is the reopen
    /// that build's own work made, the build's, because that reopen is part
    /// of the same press. Their next press of Glass's makes a newer session
    /// and ends it. <paramref name="previous"/> is the session made directly
    /// before <paramref name="newest"/>.
    /// </summary>
    public static GlassEstimateBuild? ListedBuild(
        GlassRepairEstimateSession? newest, GlassRepairEstimateSession? previous)
    {
        if (newest is null)
        {
            return null;
        }
        if (newest.Build is not null)
        {
            return newest.Build;
        }

        return previous is { Build.BuiltSpecificationId: not null }
            && string.Equals(newest.OperationKey, BuildReopenOperationKey(previous), StringComparison.Ordinal)
                ? previous.Build
                : null;
    }

    public static bool OccupiesAccount(GlassRepairEstimateSessionState state) => state is
        GlassRepairEstimateSessionState.Prepared or GlassRepairEstimateSessionState.Launching
        or GlassRepairEstimateSessionState.Active or GlassRepairEstimateSessionState.Importing
        or GlassRepairEstimateSessionState.AwaitingImport or GlassRepairEstimateSessionState.Unknown;

    /// <summary>
    /// The states a session holds only while provider work is running for it.
    /// A session found in one of them with nothing running was interrupted and
    /// is settled: Prepared stays resumable, Launching and Importing become
    /// Unknown.
    /// </summary>
    public static bool AwaitsProviderWork(GlassRepairEstimateSessionState state) => state is
        GlassRepairEstimateSessionState.Prepared or GlassRepairEstimateSessionState.Launching
        or GlassRepairEstimateSessionState.Importing;

    /// <summary>
    /// The failure code of a session whose export Pegasus's own reader
    /// refused: Glass's answered, and the document is the one thing wrong.
    /// </summary>
    public const string ExportUnreadableFailureCode = "glass.export.unreadable";

    /// <summary>
    /// The failure codes of an export that could not be fetched after the relay
    /// succeeded: Glass's request for the export grid, a grid that offers more
    /// than one export or one off Glass's own origin, and the download itself.
    /// </summary>
    public const string ExportRequestFailureCode = "glass.export.request";

    public const string ExportAmbiguousFailureCode = "glass.export.ambiguous";

    public const string ExportOffOriginFailureCode = "glass.export.off_origin";

    public const string DownloadRequestFailureCode = "glass.download.request";

    public const string DownloadOversizeFailureCode = "glass.download.oversize";

    private static readonly HashSet<string> RefetchableFailureCodes = new(StringComparer.Ordinal)
    {
        ExportUnreadableFailureCode,
        ExportRequestFailureCode,
        ExportAmbiguousFailureCode,
        ExportOffOriginFailureCode,
        DownloadRequestFailureCode,
        DownloadOversizeFailureCode,
    };

    /// <summary>
    /// Whether the owner may fetch the export again for the same estimate. A
    /// session that failed after the relay succeeded, because the reader
    /// refused the document or the export could not be fetched, has nothing
    /// wrong at Glass's: the estimate is saved there and can be read again. An
    /// identity or empty refusal is not offered, because the export it kept is
    /// not this session's vehicle. No vehicle is made and no estimate is
    /// started.
    /// </summary>
    public static bool CanRefetchExport(GlassRepairEstimateSessionState state, string? failureCode) =>
        state == GlassRepairEstimateSessionState.Failed
        && failureCode is not null
        && RefetchableFailureCodes.Contains(failureCode);

    /// <summary>
    /// Which sessions the owning staff member may close: every one that still
    /// holds the account except one mid-import, whose claim is acting on the
    /// provider's return and must be allowed to settle.
    /// </summary>
    public static bool CanClose(GlassRepairEstimateSessionState state) =>
        OccupiesAccount(state) && state != GlassRepairEstimateSessionState.Importing;

    /// <summary>
    /// The failure code of a finish refused because the Automation's estimate
    /// holds no position: nothing is saved, and the session stays open so it
    /// can be built on or closed (design 07 section 3.2).
    /// </summary>
    public const string AutomationEmptyFailureCode = "glass.automation.empty";

    /// <summary>
    /// Who may run a Glass's session at all: a member of staff, under their
    /// own Glass's login, or the Automation actor, under the login the
    /// deployment holds for it (operator, 18 November 2026; R1177). Never the
    /// system worker or a Principal. Whose session it is is
    /// <see cref="RequireActor"/>'s.
    /// </summary>
    public static void RequireSessionActor(ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Kind is not (ActorKind.Staff or ActorKind.Automation))
        {
            throw new InvalidOperationException("Only staff or the Automation can run a Glass's session.");
        }
    }

    /// <summary>
    /// Whether the actor is the session's owner: the member of staff who
    /// launched a staff session, or, for a session the Automation owns, any
    /// Automation actor whatever its grant, because the grant is renewed
    /// every fortnight and the session outlives it.
    /// </summary>
    public static bool IsOwner(ActionActor actor, GlassRepairEstimateSession session)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(session);
        return session.OwnerKind == ActorKind.Automation
            ? actor.Kind == ActorKind.Automation
            : actor.Kind == ActorKind.Staff
                && Guid.TryParse(actor.SubjectId, out var staffId)
                && staffId == session.PegasusUserId;
    }

    /// <summary>The session's owner, or the refusal every act on another's session gets.</summary>
    public static void RequireActor(ActionActor actor, GlassRepairEstimateSession session)
    {
        if (!IsOwner(actor, session))
        {
            throw new GlassRepairEstimateSessionConflictException(
                GlassRepairEstimateSessionConflict.Callback,
                session.Id,
                session.OwnerKind == ActorKind.Automation
                    ? "This Glass's session belongs to the Automation."
                    : "This Glass's session belongs to another staff member.");
        }
    }

    /// <summary>
    /// Who may close a session: its owner, or, for a session the Automation
    /// owns, any member of staff, because no person holds it and it has no
    /// lifetime (operator, 18 November 2026; R1180). The Case page asks first
    /// that they can open the Case's assessment. One predicate, so the rule
    /// can be narrowed in one place.
    /// </summary>
    public static bool MayClose(ActionActor actor, GlassRepairEstimateSession session) =>
        IsOwner(actor, session)
        || (actor.Kind == ActorKind.Staff && session.OwnerKind == ActorKind.Automation);

    public static void ValidateClosure(
        GlassRepairEstimateCloseRequest request, GlassRepairEstimateSession session)
    {
        ArgumentNullException.ThrowIfNull(request.Actor);
        if (!MayClose(request.Actor, session))
        {
            throw new GlassRepairEstimateRefusalException("This Glass's session belongs to another staff member.");
        }
        if (!CanClose(session.State)
            || !request.ExternalSessionClosed || string.IsNullOrWhiteSpace(request.Reason)
            || request.Reason.Trim().Length > 2000)
        {
            throw new GlassRepairEstimateRefusalException(
                "Closing a Glass's session that holds the account requires confirmation of external closure and a reason.");
        }
    }
}

/// <summary>Which invariant a Glass's session write ran into.</summary>
public enum GlassRepairEstimateSessionConflict
{
    ActiveAccount,
    Version,
    Callback,
    OperationKey,

    /// <summary>A colleague's session on the same estimate is landing its return.</summary>
    Importing
}

/// <summary>A Glass's session write refused because it would break an invariant.</summary>
public sealed class GlassRepairEstimateSessionConflictException(
    GlassRepairEstimateSessionConflict conflict, Guid sessionId, string message)
    : InvalidOperationException(message)
{
    public GlassRepairEstimateSessionConflict Conflict { get; } = conflict;
    public Guid SessionId { get; } = sessionId;
}

/// <summary>An expected Glass's action refusal whose reason may be shown to staff.</summary>
public sealed class GlassRepairEstimateRefusalException(string message)
    : InvalidOperationException(message);

/// <summary>
/// The Glass's estimate a repair spec belongs to (operator, 6 October 2026).
/// The portal keeps one estimate on each stock vehicle and reopens it from the
/// vehicle, so the vehicle is the identity: the estimate id is the one that
/// vehicle last answered, and the type number, placeholder flag, registration
/// and mileage are what the vehicle was proved against when the estimate was
/// started. Glass's on that spec reopens this estimate, and its saved return
/// updates that spec.
/// </summary>
public sealed record GlassEstimateLink(
    string VehicleId, string EstimateId, string NatCode, bool Placeholder,
    string Registration, long MileageMiles);

public sealed record GlassRepairEstimateSession(
    Guid Id, Guid CaseId, Guid PegasusUserId, long CredentialGeneration,
    string NormalizedExternalAccountKey, GlassRepairEstimateSessionState State,
    long Version, string OperationKey, DateTimeOffset CreatedAtUtc,
    string? ProviderVehicleId, string? ProviderEstimateId, string? FailureCode,
    DateTimeOffset? CallbackConsumedAtUtc = null)
{
    /// <summary>
    /// The rebuild of a repair spec made outside Pegasus that this session
    /// runs or ran, or null for an ordinary launch: the plan, each act's
    /// outcome as it is reached, and the Glass's repair spec it landed as.
    /// </summary>
    public GlassEstimateBuild? Build { get; init; }

    /// <summary>
    /// Who runs the session: a member of staff (<see cref="PegasusUserId"/>),
    /// or the Automation actor, whose session names no member of staff
    /// (<see cref="Guid.Empty"/>) and signs in with the login the deployment
    /// holds for it (operator, 18 November 2026; R1177).
    /// </summary>
    public ActorKind OwnerKind { get; init; } = ActorKind.Staff;

    /// <summary>
    /// What an Automation session has done that the Automation and the Case
    /// page read without the protected state; null for a staff session.
    /// </summary>
    public GlassAutomationWork? Automation { get; init; }

    /// <summary>
    /// When the member of staff who owns the session let the Automation help
    /// in it from the Case page; null while help is off, as it is for every
    /// new session (R1170). Only <see cref="IGlassSessionHelpStore.SetHelpAsync"/>
    /// writes it, and whether help is on is
    /// <see cref="GlassSessionHelpPolicy.HelpOn"/>'s.
    /// </summary>
    public DateTimeOffset? HelpOnAtUtc { get; init; }
}
/// <summary>
/// A launch. <paramref name="SessionId"/> is the id a new session takes, chosen
/// by the caller so it can hold the session busy before it exists; a replay of
/// the operation key answers the session it already created instead.
/// </summary>
public sealed record GlassRepairEstimateLaunchRequest(
    ActionActor Actor, Guid CaseId, long ExpectedCaseVersion, string LeaseToken,
    string OperationKey, Guid SessionId)
{
    /// <summary>
    /// The repair spec on the screen. One that belongs to a Glass's estimate
    /// reopens it; any other, or none, starts a new estimate.
    /// </summary>
    public Guid? SpecificationId { get; init; }

    /// <summary>
    /// The member of staff chose Import and open: a spec with lines that
    /// belongs to no Glass's estimate is rebuilt in Glass's before the
    /// estimator opens. A spec that belongs to one, or has no lines, ignores it.
    /// </summary>
    public bool Build { get; init; }
}
public sealed record GlassRepairEstimateResumeRequest(
    ActionActor Actor, Guid SessionId, long ExpectedVersion,
    long ExpectedCaseVersion, string LeaseToken);
public sealed record GlassRepairEstimateCloseRequest(
    ActionActor Actor, Guid SessionId, long ExpectedVersion, bool ExternalSessionClosed,
    string Reason);
public sealed record GlassRepairEstimateCallback(
    ActionActor Actor, Guid SessionId, long ExpectedVersion, string Correlation,
    string RawQuery)
{
    /// <summary>The provider query exactly as received, without normalization or re-encoding.</summary>
    public override string ToString() => nameof(GlassRepairEstimateCallback);
}
/// <summary>The provider work a step leaves owed to its session.</summary>
public enum GlassRepairEstimateContinuation
{
    None,
    Launch,
    Import,

    /// <summary>A launch that rebuilds the spec on the screen in Glass's, then brings it back.</summary>
    Build,

    /// <summary>The Automation's start: the launch, or its reopen, then the editor opened and kept.</summary>
    AutomationStart,

    /// <summary>The Automation's finish: Save and Exit in the editor, then the return and the import.</summary>
    AutomationFinish
}

/// <summary>
/// A session as a launch, a resume or an accepted return leaves it once its
/// checks and its durable claim are done, and the provider work still owed.
/// </summary>
public sealed record GlassRepairEstimateStep(
    GlassRepairEstimateSession Session, GlassRepairEstimateContinuation Continuation);

/// <summary>
/// The Automation's finish of its own session: the session version it read,
/// and the Case version and edit lease the Case's one-command lease proved,
/// which replace the protected import authority (FRD-25). The operation key
/// is the finish's own: the same key answers the session as it stands.
/// </summary>
public sealed record GlassAutomationFinishRequest(
    ActionActor Actor, Guid SessionId, long ExpectedVersion,
    long ExpectedCaseVersion, string LeaseToken, string OperationKey);

/// <summary>
/// The provider work a step left owed, run for the staff member who asked for
/// it. Everything else it needs, the accepted return's own message included,
/// is read back from the session.
/// </summary>
public sealed record GlassRepairEstimateContinueRequest(ActionActor Actor, Guid SessionId);

/// <summary>
/// Every Glass's act is two halves. The prepare half proves authority,
/// ownership and the one-use token and writes the durable claim; the staff
/// member's request waits for it. The continue half does the provider work
/// and may run after that request has answered.
/// </summary>
public interface IGlassRepairEstimateGateway
{
    Task<GlassRepairEstimateStep> PrepareLaunchAsync(
        GlassRepairEstimateLaunchRequest request, CancellationToken cancellationToken);
    Task<GlassRepairEstimateSession> ContinueLaunchAsync(
        GlassRepairEstimateContinueRequest request, CancellationToken cancellationToken);
    /// <summary>
    /// A build's provider work: the launch stages, then the session's
    /// <see cref="GlassRepairEstimateSession.Build"/> in Glass's editor, then
    /// its save and its return through the import, all in one piece of work.
    /// A session no build is owed to continues as
    /// <see cref="ContinueLaunchAsync"/> continues it.
    /// </summary>
    Task<GlassRepairEstimateSession> ContinueBuildAsync(
        GlassRepairEstimateContinueRequest request, CancellationToken cancellationToken);
    Task<GlassRepairEstimateStep> PrepareResumeAsync(
        GlassRepairEstimateResumeRequest request, CancellationToken cancellationToken);
    Task<GlassRepairEstimateStep> AcceptCallbackAsync(
        GlassRepairEstimateCallback callback, CancellationToken cancellationToken);
    /// <summary>
    /// <see cref="AcceptCallbackAsync(GlassRepairEstimateCallback, CancellationToken)"/>
    /// for a session the caller has just read, so it is not read again. The
    /// correlation and the version are still proved against what was read.
    /// </summary>
    Task<GlassRepairEstimateStep> AcceptCallbackAsync(
        GlassRepairEstimateCallback callback, GlassRepairEstimateSessionMaterial material,
        CancellationToken cancellationToken);
    Task<GlassRepairEstimateSession> ContinueImportAsync(
        GlassRepairEstimateContinueRequest request, CancellationToken cancellationToken);
    /// <summary>
    /// Settles a session left in a state that only running provider work
    /// holds (<see cref="GlassRepairEstimateSessionPolicy.AwaitsProviderWork"/>)
    /// once nothing runs for it. Any other session is returned as it stands.
    /// </summary>
    Task<GlassRepairEstimateSession> SettleInterruptedAsync(
        ActionActor actor, Guid sessionId, CancellationToken cancellationToken);
    /// <summary>
    /// <see cref="SettleInterruptedAsync(ActionActor, Guid, CancellationToken)"/>
    /// for a session the caller has just read, so it is not read again.
    /// </summary>
    Task<GlassRepairEstimateSession> SettleInterruptedAsync(
        ActionActor actor, GlassRepairEstimateSessionMaterial material, CancellationToken cancellationToken);
    Task<GlassRepairEstimateSession> CloseAsync(
        GlassRepairEstimateCloseRequest request, CancellationToken cancellationToken);
    Task<Uri?> GetEstimatorUrlAsync(
        ActionActor actor, Guid sessionId, CancellationToken cancellationToken);
    /// <summary>
    /// <see cref="GetEstimatorUrlAsync(ActionActor, Guid, CancellationToken)"/>
    /// for a session the caller has just read, so it is not read again.
    /// </summary>
    Task<Uri?> GetEstimatorUrlAsync(
        ActionActor actor, GlassRepairEstimateSessionMaterial material, CancellationToken cancellationToken);

    /// <summary>
    /// The Automation's start, prepare half (design 07 section 3.2): the Case
    /// authority, the unchanged vehicle, the Automation's login and the
    /// account's one live slot are proved, and a new session is recorded
    /// before anything reaches Glass's. It never reopens a spec already on
    /// the Case. The Automation's own live session on the same Case is
    /// continued instead; a replay of the operation key owes nothing.
    /// </summary>
    Task<GlassRepairEstimateStep> PrepareAutomationLaunchAsync(
        GlassRepairEstimateLaunchRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// The Automation's start, provider work: the launch stages, or the
    /// reopen of an estimate already started, then the editor opened and kept
    /// in the protected state so it stays open between the Automation's calls.
    /// </summary>
    Task<GlassRepairEstimateSession> ContinueAutomationStartAsync(
        GlassRepairEstimateContinueRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// The Automation's finish, prepare half: an open session with its
    /// editor is claimed for its save under the proved Case authority; a held
    /// estimate is claimed for its import.
    /// </summary>
    Task<GlassRepairEstimateStep> PrepareAutomationFinishAsync(
        GlassAutomationFinishRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// The Automation's finish, provider work: the estimate read, nothing
    /// saved when it holds no position, otherwise Save and Exit and the
    /// return imported as a Draft.
    /// </summary>
    Task<GlassRepairEstimateSession> ContinueAutomationFinishAsync(
        GlassRepairEstimateContinueRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// One read of the Automation's open estimate, asked of Glass's with one
    /// request (R1206): its positions and Glass's running totals, one
    /// position's figures, or Glass's calculation. Null when its editor is
    /// not open. Nothing changes at Glass's; the editor's cookies and pace
    /// are kept, so the session's version moves.
    /// </summary>
    Task<GlassAutomationEstimate?> ReadAutomationEstimateAsync(
        ActionActor actor, Guid sessionId, GlassAutomationRead read, CancellationToken cancellationToken);

    /// <summary>
    /// The Automation's catalogue search of its open estimate (design 07
    /// section 3.3): the special list and the part list, read once each, no
    /// cache; nothing changes at Glass's. Null when its editor is not open.
    /// </summary>
    Task<GlassAutomationCatalogue?> SearchAutomationCatalogueAsync(
        GlassAutomationCatalogueQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// A position act's prepare half (design 07 section 3.4): the act claimed
    /// under the session version before Glass's is asked, or the act already
    /// recorded under its key. Null when the session's editor is not open.
    /// </summary>
    Task<GlassAutomationPositionStep?> PrepareAutomationPositionAsync(
        GlassAutomationPositionRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// A claimed position act's provider work: the estimate read and earlier
    /// acts settled, the row chosen or refused, the post or the removal, and
    /// the outcome recorded.
    /// </summary>
    Task<GlassAutomationPositionStep> ContinueAutomationPositionAsync(
        GlassAutomationPositionContinueRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// What one read of an open Glass's estimate answered (R1206), with the
/// session as the read left it. A positions read answers each position's id
/// and the codes on the estimate, as the editor's <c>selected.php</c> lists
/// them, and Glass's running totals from the same page; a position read
/// answers <see cref="Position"/>; a calculation read answers
/// <see cref="Calculation"/>. When Glass's did not answer, nothing read is
/// answered and <see cref="Code"/> is the <c>glass.*</c> code, as a
/// catalogue search answers it.
/// </summary>
public sealed record GlassAutomationEstimate(
    GlassRepairEstimateSession Session, IReadOnlyList<string>? PositionIds, IReadOnlyList<string>? Codes)
{
    /// <summary>Glass's running totals in the page's order; null when the page holds none it can be read by.</summary>
    public IReadOnlyList<GlassEstimateTotal>? Totals { get; init; }

    public GlassPositionFigures? Position { get; init; }

    public GlassCalculation? Calculation { get; init; }

    /// <summary>The <c>glass.*</c> code of a read Glass's did not answer.</summary>
    public string? Code { get; init; }
}

/// <summary>
/// Whether this host composes Glass's at all. A host without the provider's
/// configuration composes no gateway and says so here, so the Case record
/// offers no Glass's control and a command that reaches it is refused rather
/// than failing on a missing setting. Shaped like
/// <see cref="Vehicle.VehicleLookupAvailability"/>.
/// </summary>
public sealed record GlassRepairEstimateAvailability(bool Enabled, string Mode)
{
    public static GlassRepairEstimateAvailability Unavailable { get; } =
        new(false, "unavailable");

    public static GlassRepairEstimateAvailability Configured { get; } =
        new(true, "configured");
}
/// <summary>Durable provider session material stays server-side and protected at rest.</summary>
public sealed class GlassRepairEstimateSessionMaterial(
    GlassRepairEstimateSession session, string protectedProviderState, string callbackDigest,
    string? resultArtifactsJson = null)
{
    public GlassRepairEstimateSession Session { get; } = session;
    public string ProtectedProviderState { get; } = protectedProviderState;
    public string CallbackDigest { get; } = callbackDigest;
    public string? ResultArtifactsJson { get; } = resultArtifactsJson;
    public override string ToString() => nameof(GlassRepairEstimateSessionMaterial);
}
/// <summary>
/// A create's answer: the stored session, and whether this call created it or
/// a replay of its operation key returned the session already recorded.
/// </summary>
public sealed record GlassRepairEstimateSessionCreation(
    GlassRepairEstimateSessionMaterial Material, bool Created);
public interface IGlassRepairEstimateSessionStore
{
    Task<GlassRepairEstimateSessionMaterial?> GetAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<GlassRepairEstimateSessionCreation> CreateAsync(
        GlassRepairEstimateSessionMaterial material, CancellationToken cancellationToken);
    Task SaveAsync(GlassRepairEstimateSessionMaterial material, long expectedVersion,
        CancellationToken cancellationToken);
    Task<GlassRepairEstimateSession> CloseAsync(
        GlassRepairEstimateCloseRequest request, CancellationToken cancellationToken);
    /// <summary>The session that holds the external account now, or null when none does.</summary>
    Task<GlassRepairEstimateSession?> FindLiveForAccountAsync(
        string normalizedExternalAccountKey, CancellationToken cancellationToken);

    /// <summary>
    /// The newest of the Case's Automation sessions whose acts hold one
    /// recorded under the operation key, or null: a replayed key answers what
    /// it recorded whichever session recorded it, and never sends twice.
    /// </summary>
    Task<GlassRepairEstimateSession?> FindAutomationActSessionAsync(
        Guid caseId, string operationKey, CancellationToken cancellationToken);

    /// <summary>
    /// Ends every other staff member's live session on a stock vehicle of
    /// this Case and frees their accounts, so the staff member who now holds
    /// the Case edit can reopen its estimate (operator, 6 October 2026). One
    /// that is landing its return is left to settle: the call is refused with
    /// <see cref="GlassRepairEstimateSessionConflict.Importing"/> instead.
    /// </summary>
    Task SupersedeAsync(
        Guid caseId, string providerVehicleId, ActionActor actor, CancellationToken cancellationToken);
}
