using Pegasus.Core.Identity;

namespace Pegasus.Core.Assessment;

public enum GlassRepairEstimateSessionState
{
    Prepared, Launching, Active, Importing, AwaitingImport, Completed, Failed, Unknown, Expired, Cancelled
}

public static class GlassRepairEstimateSessionPolicy
{
    public static bool SameRegistration(string? left, string? right) =>
        string.Equals(Compact(left), Compact(right), StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether printed text names the registration, ignoring case and any spacing in either.</summary>
    public static bool NamesRegistration(string? text, string? registration)
    {
        var plate = Compact(registration);
        return plate.Length > 0 && Compact(text).Contains(plate, StringComparison.OrdinalIgnoreCase);
    }

    private static string Compact(string? value) =>
        new((value ?? string.Empty).Where(character => !char.IsWhiteSpace(character)).ToArray());

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

    public static void ValidateClosure(
        GlassRepairEstimateCloseRequest request, GlassRepairEstimateSession session)
    {
        ArgumentNullException.ThrowIfNull(request.Actor);
        if (request.Actor.Kind != ActorKind.Staff
            || !Guid.TryParse(request.Actor.SubjectId, out var staffId)
            || staffId != session.PegasusUserId)
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
    DateTimeOffset? CallbackConsumedAtUtc = null);
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
    Import
}

/// <summary>
/// A session as a launch, a resume or an accepted return leaves it once its
/// checks and its durable claim are done, and the provider work still owed.
/// </summary>
public sealed record GlassRepairEstimateStep(
    GlassRepairEstimateSession Session, GlassRepairEstimateContinuation Continuation);

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
    Task<GlassRepairEstimateStep> PrepareResumeAsync(
        GlassRepairEstimateResumeRequest request, CancellationToken cancellationToken);
    Task<GlassRepairEstimateStep> AcceptCallbackAsync(
        GlassRepairEstimateCallback callback, CancellationToken cancellationToken);
    Task<GlassRepairEstimateSession> ContinueImportAsync(
        GlassRepairEstimateContinueRequest request, CancellationToken cancellationToken);
    /// <summary>
    /// Settles a session left in a state that only running provider work
    /// holds (<see cref="GlassRepairEstimateSessionPolicy.AwaitsProviderWork"/>)
    /// once nothing runs for it. Any other session is returned as it stands.
    /// </summary>
    Task<GlassRepairEstimateSession> SettleInterruptedAsync(
        ActionActor actor, Guid sessionId, CancellationToken cancellationToken);
    Task<GlassRepairEstimateSession> CloseAsync(
        GlassRepairEstimateCloseRequest request, CancellationToken cancellationToken);
    Task<Uri?> GetEstimatorUrlAsync(
        ActionActor actor, Guid sessionId, CancellationToken cancellationToken);
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
    /// Ends every other staff member's live session on a stock vehicle of
    /// this Case and frees their accounts, so the staff member who now holds
    /// the Case edit can reopen its estimate (operator, 6 October 2026). One
    /// that is landing its return is left to settle: the call is refused with
    /// <see cref="GlassRepairEstimateSessionConflict.Importing"/> instead.
    /// </summary>
    Task SupersedeAsync(
        Guid caseId, string providerVehicleId, ActionActor actor, CancellationToken cancellationToken);
}
