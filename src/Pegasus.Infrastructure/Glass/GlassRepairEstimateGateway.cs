using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Assessment;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// Runs one Glass's Repair Estimate session for a Case: launch, resume, and the
/// operator's Save &amp; Exit through to a source-labelled Draft.
///
/// <para>
/// <b>Identities are persisted before side effects.</b> The session exists,
/// with its account and its one-use callback fingerprint, before a single
/// request reaches Glass's; it moves to
/// <see cref="GlassRepairEstimateSessionState.Launching"/> before the stage that
/// first creates state inside the provider's account, and records the vehicle
/// and estimate it created as soon as they are known. A lost response remains
/// explicitly uncertain; the session holds the account until reconciled.
/// </para>
///
/// <para>
/// <b>A provider failure is data, not an exception.</b> Every outcome the
/// provider can produce lands on the session as a state and a failure code, so
/// the operator sees where it stopped. An outcome Pegasus cannot determine —
/// a lost answer to vehicle creation, to inserting a placeholder or to
/// starting the estimate — is
/// <see cref="GlassRepairEstimateSessionState.Unknown"/>, keeps the account's
/// one live slot, and waits for a person. It is never replaced by a fresh
/// launch. Programming errors still throw.
/// </para>
///
/// <para>
/// <b>A callback is claimed before it is acted on.</b> The delivery's
/// fingerprint and the move to
/// <see cref="GlassRepairEstimateSessionState.Importing"/> are written through
/// the store's version check before the provider hears anything, so two
/// deliveries racing for one session meet there: one is recorded and acts,
/// the other reads the record — the same message gets the session as it
/// stands, a different one is refused. The claim keeps the message itself, so
/// a relay that never began can still be made. Once the relay has begun, what
/// is lost stays <see cref="GlassRepairEstimateSessionState.Unknown"/> on the
/// record, and a later resume looks the export up again rather than relaying
/// again.
/// </para>
///
/// <para>
/// <b>What is protected.</b> The session's cookie jar, the prepared estimate
/// (<c>MvaVehicleId</c>, <c>NatCode</c>, <c>EreId</c>, the provider's own
/// callback — which carries its <c>ere_session</c> — and the rewritten
/// estimator URL), the accepted return's own query and the launch's own Case
/// authority are one protected blob at rest. The CSRF token is not: it is
/// single-use and belongs to one login. The <c>ere_session</c> never appears in
/// a log, an exception, a failure code or the session read model.
/// </para>
///
/// <para>
/// <b>The launch's Case authority is carried, not re-asked.</b> The staff
/// member proved version and lease when they launched; the callback arrives on an
/// anonymous page minutes later and has no lease of its own. Replaying the
/// launch's version and lease into the import is what makes the completion the
/// same authorised act — and when the Case has moved on since, the import
/// refuses, the artifacts stay retained, and the session waits in
/// <see cref="GlassRepairEstimateSessionState.AwaitingImport"/> for the staff member
/// to regain edit authority.
/// </para>
///
/// <para>
/// <b>Prepare, then continue.</b> Each act is split where the provider work
/// begins. The prepare half proves and claims; the continue half reads the
/// session back by id and does the provider work, so it may run after the
/// staff member's request has answered. A continuation claims the version it
/// read before it contacts the provider, and one that is interrupted settles
/// the session as an interrupted request always has: Prepared stays
/// resumable, anything past it is Unknown.
/// </para>
/// </summary>
public sealed partial class GlassRepairEstimateGateway(
    IGlassRepairEstimateSessionStore store,
    IGlassRepairEstimateCaseAuthority caseAuthority,
    IPerUserExternalCredentialReader credentials,
    GlassAutomationAccountSource automationAccount,
    ICaseArtifactCustody custody,
    ICaseArtifactCustodyStatus custodyStatus,
    IImportRawEstimate import,
    IHttpClientFactory httpClientFactory,
    IDataProtectionProvider dataProtection,
    GlassRepairEstimateOptions options,
    TimeProvider timeProvider,
    ILogger<GlassRepairEstimateGateway> logger) : IGlassRepairEstimateGateway
{
    /// <summary>
    /// Versioned on purpose: changing it makes every session in flight
    /// unreadable rather than silently mis-read, which is the right failure for
    /// protected provider state.
    /// </summary>
    public const string ProtectionPurpose = "Pegasus.Glass.Session.v1";

    /// <summary>
    /// The fingerprint a launch records for the callback it will accept. The
    /// correlation token itself is protected at rest, so a caller holding one — the
    /// callback page, reading it out of its own route — finds the session it
    /// names through this and nothing else.
    /// </summary>
    public static string CallbackDigestOf(string correlation) => Sha256Hex(correlation);

    /// <summary>The XML export's custody occurrence on the Case.</summary>
    public static string XmlOccurrenceIdentity(Guid sessionId) => $"glass-estimate:{sessionId:D}:xml";

    /// <summary>
    /// The custody occurrence of an export Pegasus's reader refused. It is kept
    /// on the Case as a rejected Glass's export, not as a Draft's source.
    /// </summary>
    public static string RejectedXmlOccurrenceIdentity(Guid sessionId) => $"glass-estimate:{sessionId:D}:rejected-xml";

    /// <summary>The embedded calculation sheet's custody occurrence on the Case.</summary>
    public static string PdfOccurrenceIdentity(Guid sessionId) => $"glass-estimate:{sessionId:D}:pdf";

    /// <summary>
    /// The series one kind of file of one Glass's estimate belongs to. A later
    /// session on the same estimate files its file as the next Box version of
    /// the earlier one (operator, 4 November 2026).
    /// </summary>
    public static string SeriesIdentity(string ereId, string part) => $"glass-estimate:{ereId}:{part}";

    /// <summary>How long a just-created vehicle waits before its identity is read once more.</summary>
    private static readonly TimeSpan VehicleReread = TimeSpan.FromMilliseconds(500);

    /// <summary>The special list's group a build chooses special operations from.</summary>
    private const string SpecialOperationGroup = GlassEditorPositionWork.SpecialOperationGroup;

    /// <summary>The group list the part list is asked for with.</summary>
    internal const string PartListGroups = GlassEditorPositionWork.PartListGroups;

    /// <summary>
    /// The editor's lists, a position's add and remove, and every stage's
    /// log line, shared with the Automation's help in a member of staff's
    /// session (<see cref="GlassSessionHelp"/>).
    /// </summary>
    private GlassEditorPositionWork Positions => positionWork ??= new(logger);

    private GlassEditorPositionWork? positionWork;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// A launch's prepare half: the Case authority, the credential and the
    /// account's one live slot are proved, and the session is recorded with its
    /// account and one-use callback fingerprint, before anything reaches
    /// Glass's. A replay of the same operation key answers the session the
    /// first one created and owes no provider work of its own.
    /// </summary>
    public async Task<GlassRepairEstimateStep> PrepareLaunchAsync(
        GlassRepairEstimateLaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        if (request.SessionId == Guid.Empty)
        {
            throw new ArgumentException("A launch names the id its new session takes.", nameof(request));
        }
        RequireStaff(request.Actor);

        var facts = await caseAuthority.RequireEditAuthorityAsync(
            request.Actor, request.CaseId, request.ExpectedCaseVersion, request.LeaseToken, cancellationToken);
        var credential = await AccountForAsync(request.Actor, null, cancellationToken);

        // The spec on the screen may belong to a Glass's estimate already: the
        // launch then reopens that estimate on its stock vehicle instead of
        // making another (operator, 6 October 2026). Whoever holds the Case
        // edit owns Glass's for it, so a colleague's session still live on the
        // same vehicle ends here, before the account is asked: staff who share
        // one Glass's login share its one live slot, and that session may be
        // the one holding it.
        var link = request.SpecificationId is { } specificationId
            ? await caseAuthority.FindEstimateAsync(facts.WorkId, specificationId, cancellationToken)
            : null;
        var vehicle = (facts.Registration, facts.MileageMiles);
        if (link is not null)
        {
            vehicle = GlassRepairEstimateSessionPolicy.RequireUnchangedEstimateVehicle(
                link, facts.Registration, facts.MileageMiles);
            await store.SupersedeAsync(request.CaseId, link.VehicleId, request.Actor, cancellationToken);
        }

        // Import and open (operator, 18 November 2026; R1171): a spec with
        // lines that belongs to no Glass's estimate is planned for a build.
        // A spec that belongs to one reopens it, and a spec with no lines
        // starts a new estimate, whatever was chosen. The outside estimate's
        // vehicle is not checked against the Case: Glass's starts on the
        // Case's vehicle as any launch does.
        var build = request.Build && link is null && request.SpecificationId is { } source
            && await caseAuthority.FindBuildSourceAsync(facts.WorkId, source, cancellationToken) is { } outside
                ? GlassEstimateBuildPolicy.Plan(outside)
                : null;

        // The account holds one live session. Asking first makes the ordinary
        // refusal a read that names the session in the way; the store's index
        // still decides a genuine race. A replay of the same operation key is
        // not a second launch and is answered by the store as before.
        if (await store.FindLiveForAccountAsync(credential.AccountKey, cancellationToken) is { } live
            && !string.Equals(live.OperationKey, request.OperationKey.Trim(), StringComparison.Ordinal))
        {
            throw Conflict(
                GlassRepairEstimateSessionConflict.ActiveAccount,
                live.Id,
                "The Glass's account already holds a live session.");
        }

        var now = timeProvider.GetUtcNow();
        var correlation = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var digest = Sha256Hex(correlation);
        var provider = new ProviderState
        {
            Registration = vehicle.Registration,
            MileageMiles = vehicle.MileageMiles,
            CaseVersion = request.ExpectedCaseVersion,
            LeaseToken = request.LeaseToken,
            PegasusCallback = options.CallbackFor(correlation).AbsoluteUri,
        };
        if (link is not null)
        {
            provider.Linked = true;
            provider.NatCode = link.NatCode;
            provider.Placeholder = link.Placeholder;
            provider.MvaVehicleId = link.VehicleId;
            provider.EreId = link.EstimateId;
            provider.EstimateIds.Add(link.EstimateId);
        }
        var prepared = new GlassRepairEstimateSession(
            request.SessionId,
            request.CaseId,
            credential.UserId,
            credential.Generation,
            // The canonical account key is minted by the credential store and
            // travels to the session store unchanged; the account name itself
            // is never recorded on the session.
            credential.AccountKey,
            GlassRepairEstimateSessionState.Prepared,
            Version: 0,
            request.OperationKey.Trim(),
            now,
            ProviderVehicleId: link?.VehicleId,
            ProviderEstimateId: link?.EstimateId,
            FailureCode: null)
        {
            Build = build,
        };

        var creation = await store.CreateAsync(
            new(prepared, Protect(provider), digest, null), cancellationToken);
        // The store says whether this call created the session. A replay of the
        // same operation key names the launch that already happened, whatever
        // id it carries: running the provider stages again would start a second
        // estimate for one operator action.
        return new(
            creation.Material.Session,
            !creation.Created
                ? GlassRepairEstimateContinuation.None
                : creation.Material.Session.Build is { Running: true }
                    ? GlassRepairEstimateContinuation.Build
                    : GlassRepairEstimateContinuation.Launch);
    }

    /// <summary>
    /// A launch's or a resume's provider work, read back from the session's own
    /// protected state: a session that has not started an estimate runs the
    /// launch stages from where it stopped, and one that has reopens it under
    /// the callback it already minted. A session no launch is owed to is
    /// returned as it stands.
    /// </summary>
    public async Task<GlassRepairEstimateSession> ContinueLaunchAsync(
        GlassRepairEstimateContinueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        GlassRepairEstimateSessionPolicy.RequireSessionActor(request.Actor);
        var material = await store.GetAsync(request.SessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"There is no Glass's session {request.SessionId}.");
        var session = material.Session;
        GlassRepairEstimateSessionPolicy.RequireActor(request.Actor, session);
        var results = Deserialize(material.ResultArtifactsJson);
        if (session.State is not (GlassRepairEstimateSessionState.Prepared
                or GlassRepairEstimateSessionState.Launching
                or GlassRepairEstimateSessionState.Active
                or GlassRepairEstimateSessionState.Unknown)
            || results.CallbackQueryDigest is not null
            || results.Xml is not null)
        {
            return session;
        }

        // A build the host stopped before it saved is not taken up again:
        // continuing it is a launch's continuation, so no post is ever sent
        // twice, and the window and the Case say no build ran. The first
        // write below records it.
        if (session.Build is { Running: true } unfinished)
        {
            session = session with { Build = unfinished with { StoppedAt = GlassEstimateBuildPolicy.InterruptedCode } };
        }

        var provider = Unprotect(material.ProtectedProviderState);
        var credential = await AccountForAsync(request.Actor, session, cancellationToken);
        if (IsUncertain(session, provider))
        {
            return await WriteAsync(session, GlassRepairEstimateSessionState.Unknown,
                GlassFailure.TransportUnknown, provider, material.CallbackDigest, results, cancellationToken);
        }

        return provider.EreId is null && provider.PegasusCallback is not null
            ? await LaunchStagesAsync(session, provider, credential, material.CallbackDigest, cancellationToken)
            : await ReopenAsync(session, provider, credential, material.CallbackDigest, results, cancellationToken);
    }

    /// <summary>
    /// A build's provider work, in one piece (operator, 18 November 2026;
    /// R1171 to R1173): the launch stages, the session's build in Glass's
    /// editor with <see cref="GlassEditorClient"/>, Save and Exit, and then
    /// the build's own return through the accepted-return path and the
    /// import, so the result lands as a new Glass's repair spec. A session no
    /// build is owed to continues as a launch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every act's outcome is written as it is reached, so the Glass's window
    /// can say how far the build has gone and a host that stops leaves the
    /// record of what was sent. The session stays Launching throughout, so a
    /// build stopped part-way settles as interrupted work and is never taken
    /// up again: a post is never sent twice.
    /// </para>
    /// <para>
    /// Nothing is typed (R1168): a special operation is posted as its dialog
    /// serves it, and only when exactly one row of the special list has
    /// exactly the line's name (R1169). A new part is posted as its dialog
    /// serves it, pressed Part, only when <see cref="GlassEditorRows"/> is
    /// certain of its row from the part list and the row's equipment map, and
    /// only when the dialog serves the part code and side the line was found
    /// by (R1172). A repair or remove-and-refit line is posted the same way,
    /// with the dialog's operation changed to Repair or to Uninstall and
    /// install and whatever price and hours the dialog serves for it (R1172):
    /// where it serves none, the dialog's question before sending a part
    /// with no figures is answered OK (R1188). A dialog the editor refuses before
    /// anything is posted leaves that line off and the build goes on; a post
    /// whose answer is lost stops the adding, and the build saves what it has.
    /// When the editor cannot be opened, or nothing was placed, nothing is
    /// saved: the estimate is started again under the same callback, and the
    /// member of staff gets the empty estimate a launch gives.
    /// </para>
    /// </remarks>
    public async Task<GlassRepairEstimateSession> ContinueBuildAsync(
        GlassRepairEstimateContinueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        GlassRepairEstimateSessionPolicy.RequireSessionActor(request.Actor);
        var material = await store.GetAsync(request.SessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"There is no Glass's session {request.SessionId}.");
        var session = material.Session;
        GlassRepairEstimateSessionPolicy.RequireActor(request.Actor, session);
        var provider = Unprotect(material.ProtectedProviderState);
        if (session.Build is not { Running: true } build
            || session.State != GlassRepairEstimateSessionState.Prepared
            || provider.EreId is not null
            || provider.PegasusCallback is null)
        {
            return await ContinueLaunchAsync(request, cancellationToken);
        }

        var credential = await AccountForAsync(request.Actor, session, cancellationToken);
        session = await LaunchStagesAsync(
            session, provider, credential, material.CallbackDigest, cancellationToken, build: true);
        return session.State == GlassRepairEstimateSessionState.Launching
            ? await BuildAsync(request.Actor, session, provider, credential, material.CallbackDigest, build, cancellationToken)
            : session;
    }

    /// <summary>
    /// The build itself, from an estimate the launch stages have just started
    /// and left Launching: open the editor, add the build's acts, save and
    /// exit, and hand the editor's own way home to the return path.
    /// </summary>
    private async Task<GlassRepairEstimateSession> BuildAsync(
        ActionActor actor,
        GlassRepairEstimateSession session,
        ProviderState provider,
        GlassLogin credential,
        string digest,
        GlassEstimateBuild build,
        CancellationToken cancellationToken)
    {
        var editor = NewEditorClient();
        string? stoppedAt = null;
        var returned = string.Empty;
        try
        {
            var (read, unopened) = await OpenEditorAsync(session, editor, provider, cancellationToken);
            if (read is null)
            {
                stoppedAt = unopened ?? GlassFailure.EditorLaunch;
            }
            else
            {
                var opened = read.Session;
                var estimate = read.Value;
                List<GlassSpecialOperation>? specials = null;
                string? unread = null;
                if (build.Acts.Any(act => act.Sends && act.Kind == GlassEstimateBuildKind.SpecialOperation))
                {
                    try
                    {
                        var list = await StageAsync(session, "EditorSpecialList", () => editor.ReadSpecialOperationsAsync(
                            opened, SpecialOperationGroup, cancellationToken));
                        opened = list.Session;
                        specials = SpecialOperationsOf(list.Value);
                    }
                    catch (Exception failure)
                        when (failure is GlassMvaStageException || IsTransportFailure(failure, cancellationToken))
                    {
                        // Reading the list changes nothing: each special
                        // operation is left off with the reason.
                        unread = CodeOf(failure);
                    }
                }

                IReadOnlyList<GlassEditorPartRow>? parts = null;
                string? unreadParts = null;
                if (build.Acts.Any(act => act.Sends && OperationOf(act.Kind) is not null))
                {
                    try
                    {
                        var list = await StageAsync(session, "EditorPartList", () => editor.ReadPartListAsync(
                            opened, PartListGroups, cancellationToken));
                        opened = list.Session;
                        parts = list.Value.Rows;
                    }
                    catch (Exception failure)
                        when (failure is GlassMvaStageException || IsTransportFailure(failure, cancellationToken))
                    {
                        // Reading the list changes nothing: each part is left
                        // off with the reason.
                        unreadParts = CodeOf(failure);
                    }
                }

                for (var index = 0; index < build.Acts.Count; index++)
                {
                    var act = build.Acts[index];
                    if (act.Outcome != GlassEstimateBuildOutcome.Pending)
                    {
                        continue;
                    }

                    var stopped = false;
                    GlassEditorAddRequest? request = null;
                    string? reason;
                    if (act.Kind == GlassEstimateBuildKind.SpecialOperation)
                    {
                        GlassSpecialOperation? row = null;
                        if (specials is null)
                        {
                            reason = unread ?? GlassEstimateBuildPolicy.NoRowCode;
                        }
                        else
                        {
                            (row, reason) = GlassEstimateBuildPolicy.ChooseSpecialOperation(specials, act.Key);
                        }
                        if (row is not null)
                        {
                            request = new GlassEditorAddRequest(
                                GlassEditorDialog.SpecialOperation, row.Code, PartCodeOf(row.Code));
                        }
                    }
                    else if (OperationOf(act.Kind) is { } operation)
                    {
                        if (parts is null)
                        {
                            reason = unreadParts ?? GlassEstimateBuildPolicy.NoRowCode;
                        }
                        else
                        {
                            (request, reason, opened) = await Positions.ChoosePartAsync(
                                session, editor, opened, parts, act.KeyKind, act.Key, act.Side, operation, vehicle: null,
                                cancellationToken);
                        }
                    }
                    else
                    {
                        reason = GlassEstimateBuildPolicy.NotBuiltCode;
                    }

                    if (request is not { } adding)
                    {
                        act = act with
                        {
                            Outcome = GlassEstimateBuildOutcome.LeftOff,
                            ReasonCode = reason ?? GlassEstimateBuildPolicy.NoRowCode,
                        };
                    }
                    else
                    {
                        try
                        {
                            var added = await StageAsync(session, "EditorAdd", () => editor.AddAsync(
                                opened, estimate, adding, cancellationToken));
                            opened = added.Session;
                            estimate = added.Value.Estimate;
                            act = act with
                            {
                                Outcome = GlassEstimateBuildOutcome.Added,
                                ReasonCode = null,
                                Served = FiguresOf(added.Value.Summary),
                            };
                        }
                        catch (GlassMvaStageException refused)
                            when (refused.FailureCode is GlassFailure.EditorDialog or GlassFailure.EditorRefused)
                        {
                            // Refused before anything was posted.
                            LogBuildAct(logger, session.Id, session.CaseId, act.SourcePositions[0],
                                GlassEstimateBuildOutcome.LeftOff, refused.FailureCode, refused.Detail ?? string.Empty);
                            act = act with
                            {
                                Outcome = GlassEstimateBuildOutcome.LeftOff,
                                ReasonCode = refused.FailureCode,
                            };
                        }
                        catch (Exception lost)
                            when (lost is GlassMvaStageException || IsTransportFailure(lost, cancellationToken))
                        {
                            // Posted, or perhaps posted: what the estimate
                            // holds is now Glass's to say, and nothing more
                            // is added.
                            var failure = AsFailure(lost);
                            LogBuildAct(logger, session.Id, session.CaseId, act.SourcePositions[0],
                                GlassEstimateBuildOutcome.NotConfirmed, failure.FailureCode, failure.Detail ?? string.Empty);
                            act = act with
                            {
                                Outcome = GlassEstimateBuildOutcome.NotConfirmed,
                                ReasonCode = failure.FailureCode,
                            };
                            stopped = true;
                        }
                    }

                    build = GlassEstimateBuildPolicy.Record(build, index, act);
                    if (stopped)
                    {
                        build = GlassEstimateBuildPolicy.LeaveOffPending(build, GlassEstimateBuildPolicy.StoppedCode);
                    }

                    session = await WriteAsync(session with { Build = build },
                        GlassRepairEstimateSessionState.Launching, null, provider, digest, null, cancellationToken);
                    if (stopped)
                    {
                        break;
                    }
                }

                if (!build.Placed)
                {
                    stoppedAt = GlassEstimateBuildPolicy.NothingPlacedCode;
                }
                else
                {
                    try
                    {
                        returned = await StageAsync(session, "EditorSave", () => editor.SaveAndExitAsync(
                            opened, cancellationToken));
                    }
                    catch (Exception failure)
                        when (failure is GlassMvaStageException || IsTransportFailure(failure, cancellationToken))
                    {
                        return await SettleAsync(
                            session, AsFailure(failure), provider, digest, null, CancellationToken.None);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await WriteAsync(session with { Build = build }, GlassRepairEstimateSessionState.Unknown,
                GlassFailure.Interrupted, provider, digest, null, CancellationToken.None);
            throw;
        }

        if (stoppedAt is not null)
        {
            // Nothing was saved at Glass's. The estimate is started again
            // under this session's own callback, which the build never
            // handed back, so the member of staff opens it empty.
            return await ReopenAsync(
                session with { Build = build with { StoppedAt = stoppedAt } },
                provider, credential, digest, new Results(), cancellationToken);
        }

        return await ReturnBuiltAsync(actor, session, provider, digest, returned, cancellationToken);
    }

    /// <summary>
    /// The editor opened from the address the launch stages recorded, and the
    /// estimate as it opened; or why it could not be, when nothing was sent.
    /// </summary>
    private async Task<(GlassEditorReply<GlassEditorEstimate>? Read, string? Failure)> OpenEditorAsync(
        GlassRepairEstimateSession session, GlassEditorClient editor, ProviderState provider,
        CancellationToken cancellationToken)
    {
        var launchAddress = provider.EstimatorUrl
            ?? throw new InvalidOperationException("A started Glass's estimate must carry its estimator address.");
        try
        {
            var opened = await StageAsync(session, "EditorOpen", () => editor.OpenAsync(launchAddress, cancellationToken));
            return (await StageAsync(session, "EditorRead", () => editor.ReadEstimateAsync(opened, cancellationToken)), null);
        }
        catch (Exception failure)
            when (failure is GlassMvaStageException || IsTransportFailure(failure, cancellationToken))
        {
            return (null, CodeOf(failure));
        }
    }

    /// <summary>
    /// The operation a line of this kind is placed with in a part's dialog:
    /// Replace for a new part, Repair for a repair line, Uninstall and
    /// install for a remove-and-refit line; null for any other kind.
    /// </summary>
    private static string? OperationOf(GlassEstimateBuildKind kind) => kind switch
    {
        GlassEstimateBuildKind.Part => GlassEditorRows.Replace,
        GlassEstimateBuildKind.Repair => GlassEditorRows.Repair,
        GlassEstimateBuildKind.RemoveAndRefit => GlassEditorRows.RemoveAndRefit,
        _ => null,
    };

    /// <summary>
    /// The editor's way home, taken as the provider's return to this session:
    /// it must be this session's own callback, with the query exactly as the
    /// editor gave it, as the callback page passes it. The session is opened
    /// for the return only now, and the return is accepted and imported in the
    /// same work, with no request to Pegasus.
    /// </summary>
    private async Task<GlassRepairEstimateSession> ReturnBuiltAsync(
        ActionActor actor,
        GlassRepairEstimateSession session,
        ProviderState provider,
        string digest,
        string returned,
        CancellationToken cancellationToken)
    {
        var own = provider.PegasusCallback
            ?? throw new InvalidOperationException("A Glass's build must carry the callback it was launched under.");
        if (!returned.StartsWith(own + "?", StringComparison.Ordinal))
        {
            // Saved at Glass's, but not as this session's: what it holds is
            // for a person to look at.
            return await SettleAsync(
                session,
                new GlassMvaStageException(GlassFailure.EditorReturn, outcomeUnknown: true, detail: "address=not-this-session"),
                provider, digest, null, CancellationToken.None);
        }

        session = await WriteAsync(
            session, GlassRepairEstimateSessionState.Active, null, provider, digest, null, CancellationToken.None);
        // The claim is made whatever the time cap does: a session left Active
        // here would open an editor the build has already closed.
        var accepted = await AcceptReadAsync(
            new GlassRepairEstimateCallback(
                actor, session.Id, session.Version, new Uri(own, UriKind.Absolute).Segments[^1], returned[own.Length..]),
            await store.GetAsync(session.Id, CancellationToken.None),
            CancellationToken.None);
        return accepted.Continuation == GlassRepairEstimateContinuation.Import
            ? await ContinueImportAsync(new(actor, session.Id), cancellationToken)
            : accepted.Session;
    }

    private static List<GlassSpecialOperation> SpecialOperationsOf(IReadOnlyList<GlassEditorSpecialGroup> groups) =>
        GlassEditorPositionWork.SpecialOperationsOf(groups);

    private static string PartCodeOf(string code) => GlassEditorPositionWork.PartCodeOf(code);

    private static GlassEstimateBuildFigures FiguresOf(GlassEditorSummary summary) =>
        GlassEditorPositionWork.FiguresOf(summary);

    private static string CodeOf(Exception failure) => GlassEditorPositionWork.CodeOf(failure);

    private async Task<GlassRepairEstimateSession> LaunchStagesAsync(
        GlassRepairEstimateSession session, ProviderState provider,
        GlassLogin credential, string digest, CancellationToken cancellationToken,
        bool build = false)
    {
        // Claim this version before any external work, including a resumed
        // Prepared session. A concurrent Resume cannot also create a vehicle.
        session = await WriteAsync(session, session.State, null, provider, digest, null, cancellationToken);
        // An earlier start on this vehicle went unanswered. The vehicle is
        // known, and the portal answers a start on a vehicle with the estimate
        // it holds, so starting again makes no second vehicle and no second
        // estimate (captures of 6 October 2026).
        var restarted = provider.EstimateStartAttempted;
        provider.Cookies.Clear();
        var client = NewClient(provider.Cookies);
        try
        {
            await StageAsync(session, "SignIn", () => client.SignInAsync(credential.Username, credential.Password, cancellationToken));
            if (provider.NatCode is null && !provider.Placeholder)
            {
                try
                {
                    var lookup = await StageAsync(session, "Lookup", () => client.LookupAsync(provider.Registration, provider.MileageMiles, cancellationToken));
                    provider.NatCode = lookup.NatCode;
                }
                catch (GlassMvaStageException notFound) when (notFound.FailureCode == GlassFailure.LookupNotFound)
                {
                    // The portal's own "vehicle details have not been found".
                    // The estimate is started on a placeholder vehicle and the
                    // Engineer identifies the real one inside the estimator
                    // (operator, 2 October 2026).
                    provider.Placeholder = true;
                    LogPlaceholderLaunch(logger, session.Id, session.CaseId, notFound.Detail ?? string.Empty);
                }
            }
            if (provider.MvaVehicleId is null)
            {
                session = await WriteAsync(session, GlassRepairEstimateSessionState.Launching,
                    null, provider, digest, null, cancellationToken);
                provider.MvaVehicleId = provider.Placeholder
                    ? await StageAsync(session, "InsertPlaceholder", () => client.InsertPlaceholderAsync(
                        provider.Registration, cancellationToken))
                    : await StageAsync(session, "CreateVehicle", () => client.CreateVehicleAsync(
                        provider.Registration, provider.MileageMiles, cancellationToken));
                // Keep a successful answer even if the work was interrupted.
                session = await WriteAsync(session with { ProviderVehicleId = provider.MvaVehicleId },
                    GlassRepairEstimateSessionState.Launching, null, provider, digest, null, CancellationToken.None);
            }
            if (provider.Placeholder)
            {
                provider.NatCode = await StageAsync(session, "RequirePlaceholder", () => restarted
                    ? EitherWayAsync(started => client.RequirePlaceholderAsync(
                        provider.MvaVehicleId, provider.NatCode, started, cancellationToken))
                    : client.RequirePlaceholderAsync(
                        provider.MvaVehicleId, provider.NatCode, estimateStarted: false, cancellationToken));
            }
            else if (restarted)
            {
                await StageAsync(session, "RequireVehicle", () => EitherWayAsync(async started =>
                {
                    await client.RequireVehicleAsync(
                        provider.MvaVehicleId, provider.NatCode!, provider.Registration, provider.MileageMiles,
                        started, cancellationToken);
                    return started;
                }));
            }
            else
            {
                await StageAsync(session, "RequireVehicle", () => RequireNewVehicleAsync(
                    client, provider.MvaVehicleId, provider.NatCode!, provider.Registration, provider.MileageMiles,
                    cancellationToken));
            }
            await StageAsync(session, "SelectOnly", () => client.SelectOnlyAsync(provider.MvaVehicleId, cancellationToken));
            provider.EstimateStartAttempted = true;
            session = await WriteAsync(session, GlassRepairEstimateSessionState.Launching,
                null, provider, digest, null, cancellationToken);
            var launch = await StageAsync(session, "EstimatorUrlIssued", () => client.StartEstimateAsync(
                "0", new Uri(provider.PegasusCallback!, UriKind.Absolute), expectedEstimateIds: null, cancellationToken));
            Record(provider, launch);
            // A build keeps the editor to itself: the session stays Launching,
            // the state for work that may have acted at Glass's, until its
            // own return is handed over.
            return await WriteAsync(session with { ProviderEstimateId = launch.EreId },
                build ? GlassRepairEstimateSessionState.Launching : GlassRepairEstimateSessionState.Active,
                null, provider, digest, null, CancellationToken.None);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await WriteAsync(session,
                session.State == GlassRepairEstimateSessionState.Prepared
                    ? GlassRepairEstimateSessionState.Prepared : GlassRepairEstimateSessionState.Unknown,
                GlassFailure.Interrupted, provider, digest, null, CancellationToken.None);
            throw;
        }
        catch (GlassMvaStageException failure)
        {
            return await SettleAsync(session, failure, provider, digest, null, CancellationToken.None);
        }
        catch (Exception transport) when (IsTransportFailure(transport, cancellationToken))
        {
            var uncertain = session.State != GlassRepairEstimateSessionState.Prepared;
            return await SettleAsync(session,
                new GlassMvaStageException(uncertain ? GlassFailure.TransportUnknown : GlassFailure.TransportFailed, uncertain),
                provider, digest, null, CancellationToken.None);
        }
    }

    /// <summary>
    /// Proves a vehicle created moments ago. Glass's answered a just-created
    /// vehicle's detail fragments wrongly twice in 28 launches on 2 October
    /// 2026 and rightly moments later (issue 1030), so a refused identity is
    /// read once more after <see cref="VehicleReread"/>; the second refusal
    /// is the one settled, flagged <c>reread=1</c>. Only a launch reads again:
    /// a resumed or placeholder vehicle is not new.
    /// </summary>
    private async Task RequireNewVehicleAsync(
        GlassMvaClient client, string vehicleId, string natCode, string registration, long mileageMiles,
        CancellationToken cancellationToken)
    {
        try
        {
            await client.RequireVehicleAsync(
                vehicleId, natCode, registration, mileageMiles, estimateStarted: false, cancellationToken);
        }
        catch (GlassMvaStageException first) when (first.FailureCode == GlassFailure.DetailsIdentity)
        {
            await Task.Delay(VehicleReread, timeProvider, cancellationToken);
            try
            {
                await client.RequireVehicleAsync(
                    vehicleId, natCode, registration, mileageMiles, estimateStarted: false, cancellationToken);
            }
            catch (GlassMvaStageException second) when (second.FailureCode == GlassFailure.DetailsIdentity)
            {
                throw new GlassMvaStageException(
                    second.FailureCode,
                    second.OutcomeUnknown,
                    second.Detail is null ? "reread=1" : $"{second.Detail} reread=1");
            }
        }
    }

    /// <summary>
    /// A vehicle whose first start went unanswered may or may not hold an
    /// estimate, and the portal locks the repair profile only once it does:
    /// the vehicle is proved as not started and, when its profile is locked,
    /// as started. Any other profile refusal is the vehicle's as it stands,
    /// so it is the one settled.
    /// </summary>
    private static async Task<T> EitherWayAsync<T>(Func<bool, Task<T>> prove)
    {
        try
        {
            return await prove(false);
        }
        catch (GlassMvaStageException locked) when (locked.ProfileLocked)
        {
            return await prove(true);
        }
    }

    /// <summary>
    /// Reopens an estimate at the provider: fresh cookies, the grid
    /// selection re-asserted because it is server-side session state, and the
    /// calculation restarted as the portal restarts one — <c>ere_id</c> 0 on
    /// the vehicle — under the callback this session minted. Only an answer
    /// naming one of the session's own estimates is opened. A session reopening
    /// the estimate its repair spec belongs to made nothing at the provider,
    /// so whatever stops it is a plain failure and the next launch reopens it
    /// again; only an answer naming another estimate stays for reconciliation.
    /// </summary>
    private async Task<GlassRepairEstimateSession> ReopenAsync(
        GlassRepairEstimateSession session, ProviderState provider,
        GlassLogin credential, string digest, Results results,
        CancellationToken cancellationToken)
    {
        var (vehicleId, ereId) = RequireReopenable(provider);
        // The callback this session accepts never changes — the store refuses a
        // write that carries a different one — so the resumed launch reuses the
        // address the first one minted rather than trying to mint a second. A
        // session that has not opened its estimator yet still holds the one it
        // was prepared with.
        var callback = provider.EstimatorUrl is { } estimatorUrl
            ? PegasusCallbackOf(estimatorUrl)
            : new Uri(
                provider.PegasusCallback
                    ?? throw new InvalidOperationException("The Glass's session names no callback to reopen under."),
                UriKind.Absolute);
        session = await WriteAsync(session, session.State, null, provider, digest, results, cancellationToken);
        provider.Cookies.Clear();
        var client = NewClient(provider.Cookies);
        try
        {
            await StageAsync(session, "SignIn", () => client.SignInAsync(credential.Username, credential.Password, cancellationToken));
            await RequireSessionVehicleAsync(session, provider, client, vehicleId, cancellationToken);
            await StageAsync(session, "SelectOnly", () => client.SelectOnlyAsync(vehicleId, cancellationToken));
            // The portal reopens an estimate by starting with id 0 on the
            // vehicle, never with the id itself (issue 1026, operator 5 October
            // 2026); the answer must be one of this session's own estimates.
            var launch = await StageAsync(session, "EstimatorUrlIssued", () => client.StartEstimateAsync(
                "0", callback, EstimateIdsOf(provider, ereId), cancellationToken));
            Record(provider, launch);
            return await WriteAsync(
                session with { ProviderEstimateId = launch.EreId },
                GlassRepairEstimateSessionState.Active,
                null,
                provider,
                digest,
                results,
                CancellationToken.None);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await WriteAsync(session,
                provider.Linked ? GlassRepairEstimateSessionState.Failed : GlassRepairEstimateSessionState.Unknown,
                GlassFailure.Interrupted, provider, digest, results, CancellationToken.None);
            throw;
        }
        catch (Exception failure)
            when (failure is GlassMvaStageException || IsTransportFailure(failure, cancellationToken))
        {
            var stopped = AsFailure(failure);
            return provider.Linked && stopped.FailureCode != GlassFailure.StartEreId
                ? await SettleAsync(
                    session,
                    new GlassMvaStageException(stopped.FailureCode, outcomeUnknown: false, stopped.Detail),
                    provider, digest, results, CancellationToken.None)
                : await SettleUnknownAsync(session, stopped, provider, digest, results, CancellationToken.None);
        }
    }

    /// <summary>
    /// A resume's or a lookup's refusal: whatever stopped it, the session is
    /// Unknown and keeps the account, because the provider may already have
    /// acted. The stage's own numbers and flags are logged first, as
    /// <see cref="SettleAsync"/> does, so the host log says why.
    /// </summary>
    private Task<GlassRepairEstimateSession> SettleUnknownAsync(
        GlassRepairEstimateSession session,
        GlassMvaStageException failure,
        ProviderState provider,
        string callbackDigest,
        Results? results,
        CancellationToken cancellationToken)
    {
        LogSettled(logger, session.Id, session.CaseId, GlassRepairEstimateSessionState.Unknown,
            failure.FailureCode, failure.Detail ?? string.Empty);
        return WriteAsync(
            session,
            GlassRepairEstimateSessionState.Unknown,
            failure.FailureCode,
            provider,
            callbackDigest,
            results,
            cancellationToken);
    }

    public Task<GlassRepairEstimateSession> CloseAsync(
        GlassRepairEstimateCloseRequest request, CancellationToken cancellationToken) =>
        store.CloseAsync(request, cancellationToken);

    /// <summary>
    /// The Automation's start, prepare half (design 07 section 3.2). A new
    /// session always starts a new estimate on a new stock vehicle: a spec
    /// already on the Case is never reopened. The Automation's own session
    /// still live on the same Case is continued instead, as a member of
    /// staff's press continues theirs; the account held by anyone else is
    /// refused before Glass's is asked, naming the session in the way.
    /// </summary>
    public async Task<GlassRepairEstimateStep> PrepareAutomationLaunchAsync(
        GlassRepairEstimateLaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        if (request.SessionId == Guid.Empty)
        {
            throw new ArgumentException("A launch names the id its new session takes.", nameof(request));
        }
        RequireAutomation(request.Actor);

        var facts = await caseAuthority.RequireEditAuthorityAsync(
            request.Actor, request.CaseId, request.ExpectedCaseVersion, request.LeaseToken, cancellationToken);
        var login = await AccountForAsync(request.Actor, null, cancellationToken);
        var operationKey = request.OperationKey.Trim();
        if (await store.FindLiveForAccountAsync(login.AccountKey, cancellationToken) is { } live
            && !string.Equals(live.OperationKey, operationKey, StringComparison.Ordinal))
        {
            if (live.OwnerKind == ActorKind.Automation && live.CaseId == request.CaseId)
            {
                return await PrepareAutomationContinueAsync(request, facts, live.Id, cancellationToken);
            }

            throw Conflict(
                GlassRepairEstimateSessionConflict.ActiveAccount,
                live.Id,
                "The Glass's account already holds a live session.");
        }

        var correlation = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var provider = new ProviderState
        {
            Registration = facts.Registration,
            MileageMiles = facts.MileageMiles,
            CaseVersion = request.ExpectedCaseVersion,
            LeaseToken = request.LeaseToken,
            PegasusCallback = options.CallbackFor(correlation).AbsoluteUri,
        };
        var prepared = new GlassRepairEstimateSession(
            request.SessionId,
            request.CaseId,
            // An Automation session names no member of staff.
            Guid.Empty,
            CredentialGeneration: 0,
            login.AccountKey,
            GlassRepairEstimateSessionState.Prepared,
            Version: 0,
            operationKey,
            timeProvider.GetUtcNow(),
            ProviderVehicleId: null,
            ProviderEstimateId: null,
            FailureCode: null)
        {
            OwnerKind = ActorKind.Automation,
        };

        var creation = await store.CreateAsync(
            new(prepared, Protect(provider), Sha256Hex(correlation), null), cancellationToken);
        return new(
            creation.Material.Session,
            creation.Created ? GlassRepairEstimateContinuation.AutomationStart : GlassRepairEstimateContinuation.None);
    }

    /// <summary>
    /// The Automation's own live session on the Case, taken up again by a
    /// new start: the proved Case authority replaces the protected import
    /// authority and the vehicle must be unchanged, as a resume's are. A
    /// session not yet started runs its launch; an open or uncertain one is
    /// reopened. One whose work is in hand, or whose estimate is held, is
    /// answered as it stands.
    /// </summary>
    private async Task<GlassRepairEstimateStep> PrepareAutomationContinueAsync(
        GlassRepairEstimateLaunchRequest request,
        GlassRepairEstimateCaseFacts facts,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var material = await store.GetAsync(sessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"There is no Glass's session {sessionId}.");
        var session = material.Session;
        GlassRepairEstimateSessionPolicy.RequireActor(request.Actor, session);
        var results = Deserialize(material.ResultArtifactsJson);
        if (session.State is not (GlassRepairEstimateSessionState.Prepared
                or GlassRepairEstimateSessionState.Active
                or GlassRepairEstimateSessionState.Unknown)
            || results.CallbackQueryDigest is not null
            || results.Xml is not null)
        {
            return new(session, GlassRepairEstimateContinuation.None);
        }

        var provider = Unprotect(material.ProtectedProviderState);
        (provider.Registration, provider.MileageMiles) = GlassRepairEstimateSessionPolicy.RequireUnchangedVehicle(
            provider.Registration, provider.MileageMiles, provider.Placeholder,
            vehicleRecorded: provider.MvaVehicleId is not null, facts.Registration, facts.MileageMiles);
        await AccountForAsync(request.Actor, session, cancellationToken);
        provider.CaseVersion = request.ExpectedCaseVersion;
        provider.LeaseToken = request.LeaseToken;
        if (IsUncertain(session, provider))
        {
            return new(
                await WriteAsync(session, GlassRepairEstimateSessionState.Unknown,
                    GlassFailure.TransportUnknown, provider, material.CallbackDigest, Kept(material), cancellationToken),
                GlassRepairEstimateContinuation.None);
        }
        if (!Launching(provider))
        {
            RequireReopenable(provider);
        }

        session = await WriteAsync(session, session.State, null, provider, material.CallbackDigest,
            Kept(material), cancellationToken);
        return new(session, GlassRepairEstimateContinuation.AutomationStart);
    }

    /// <summary>
    /// The Automation's start, provider work: the launch stages on a new
    /// stock vehicle, or the reopen of the estimate the session already
    /// started under its own callback, then the editor opened from the launch
    /// address and kept in the protected state with the pace of its last
    /// request. The session is Active once the editor is open. An editor that
    /// does not open leaves the estimate started at Glass's and the session
    /// Unknown, holding the account, with the editor's code; a start again
    /// reopens it.
    /// </summary>
    public async Task<GlassRepairEstimateSession> ContinueAutomationStartAsync(
        GlassRepairEstimateContinueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireAutomation(request.Actor);
        var material = await store.GetAsync(request.SessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"There is no Glass's session {request.SessionId}.");
        var session = material.Session;
        GlassRepairEstimateSessionPolicy.RequireActor(request.Actor, session);
        var results = Deserialize(material.ResultArtifactsJson);
        if (session.State is not (GlassRepairEstimateSessionState.Prepared
                or GlassRepairEstimateSessionState.Active
                or GlassRepairEstimateSessionState.Unknown)
            || results.CallbackQueryDigest is not null
            || results.Xml is not null)
        {
            return session;
        }

        var provider = Unprotect(material.ProtectedProviderState);
        var digest = material.CallbackDigest;
        var kept = Kept(material);
        var login = await AccountForAsync(request.Actor, session, cancellationToken);
        if (IsUncertain(session, provider))
        {
            return await WriteAsync(session, GlassRepairEstimateSessionState.Unknown,
                GlassFailure.TransportUnknown, provider, digest, kept, cancellationToken);
        }

        // An editor a previous start opened is not taken up again: the
        // estimate is reopened and a new editor opened from its launch.
        provider.Editor = null;
        var launching = Launching(provider);
        session = launching
            ? await LaunchStagesAsync(session, provider, login, digest, cancellationToken, build: true)
            : await ReopenAsync(session, provider, login, digest, results, cancellationToken);
        if (session.State is not (GlassRepairEstimateSessionState.Launching or GlassRepairEstimateSessionState.Active))
        {
            return session;
        }

        // What the stages just wrote: none after a launch, the results after a reopen.
        kept = launching ? null : results;
        var editor = NewEditorClient(provider.EditorLastRequestAtUtc);
        var (read, unopened) = await OpenEditorAsync(session, editor, provider, cancellationToken);
        provider.EditorLastRequestAtUtc = editor.LastRequestAtUtc;
        if (read is null)
        {
            return await SettleUnknownAsync(
                session,
                new GlassMvaStageException(unopened ?? GlassFailure.EditorLaunch, outcomeUnknown: true),
                provider, digest, kept, CancellationToken.None);
        }

        provider.Editor = EditorState.Of(read.Session);
        return await WriteAsync(session, GlassRepairEstimateSessionState.Active, null, provider, digest, kept,
            CancellationToken.None);
    }

    /// <summary>
    /// The Automation's finish, prepare half. A held estimate (the Case
    /// moved on, or a member of staff held it, when the return landed) is
    /// claimed for its import under the authority just proved, as a resume
    /// claims it. Otherwise the session must be open with its editor: the
    /// proved Case version and lease replace the protected import authority
    /// (FRD-25), and the session is written Launching, the state for work
    /// that may act at Glass's, before Glass's is asked anything. The same
    /// operation key again answers the session as it stands.
    /// </summary>
    public async Task<GlassRepairEstimateStep> PrepareAutomationFinishAsync(
        GlassAutomationFinishRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.LeaseToken);
        RequireAutomation(request.Actor);
        var operationKey = request.OperationKey.Trim();
        var material = await store.GetAsync(request.SessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"There is no Glass's session {request.SessionId}.");
        var session = material.Session;
        GlassRepairEstimateSessionPolicy.RequireActor(request.Actor, session);
        if (string.Equals(session.Automation?.FinishOperationKey, operationKey, StringComparison.Ordinal)
            && session.State != GlassRepairEstimateSessionState.AwaitingImport)
        {
            return new(session, GlassRepairEstimateContinuation.None);
        }
        if (session.Version != request.ExpectedVersion)
        {
            throw Conflict(
                GlassRepairEstimateSessionConflict.Version,
                session.Id,
                $"The Glass's session is at version {session.Version} and not {request.ExpectedVersion}.");
        }

        var provider = Unprotect(material.ProtectedProviderState);
        var results = Deserialize(material.ResultArtifactsJson);
        var held = session.State == GlassRepairEstimateSessionState.AwaitingImport
            || (session.State == GlassRepairEstimateSessionState.Unknown && results.Xml is not null);
        if (!held && (session.State != GlassRepairEstimateSessionState.Active || provider.Editor is null))
        {
            throw new GlassRepairEstimateRefusalException(
                "The Automation's Glass's session has no open estimate to finish.");
        }

        var facts = await caseAuthority.RequireEditAuthorityAsync(
            request.Actor, session.CaseId, request.ExpectedCaseVersion, request.LeaseToken, cancellationToken);
        (provider.Registration, provider.MileageMiles) = GlassRepairEstimateSessionPolicy.RequireUnchangedVehicle(
            provider.Registration, provider.MileageMiles, provider.Placeholder,
            vehicleRecorded: provider.MvaVehicleId is not null, facts.Registration, facts.MileageMiles);
        await AccountForAsync(request.Actor, session, cancellationToken);
        provider.CaseVersion = request.ExpectedCaseVersion;
        provider.LeaseToken = request.LeaseToken;
        session = session with
        {
            Automation = (session.Automation ?? new GlassAutomationWork()) with { FinishOperationKey = operationKey },
        };
        if (held)
        {
            // Claim the import before touching custody, the provider or the
            // Case, as a resume does. Close is unavailable while it runs.
            return new(
                await WriteAsync(session, GlassRepairEstimateSessionState.Importing, session.FailureCode,
                    provider, material.CallbackDigest, results, cancellationToken),
                GlassRepairEstimateContinuation.Import);
        }

        return new(
            await WriteAsync(session, GlassRepairEstimateSessionState.Launching, null, provider,
                material.CallbackDigest, Kept(material), cancellationToken),
            GlassRepairEstimateContinuation.AutomationFinish);
    }

    /// <summary>
    /// The Automation's finish, provider work (design 07 section 3.2): the
    /// estimate is read first, and with no position nothing is saved: the
    /// session is open again with <see cref="GlassRepairEstimateSessionPolicy.AutomationEmptyFailureCode"/>,
    /// to be built on or closed. A read Glass's does not answer leaves it open
    /// with the code. Otherwise Save and Exit, and the editor's own way home
    /// is taken as the session's return and imported in the same work, as a
    /// build's is: the Automation's import lands as a Draft.
    /// </summary>
    public async Task<GlassRepairEstimateSession> ContinueAutomationFinishAsync(
        GlassRepairEstimateContinueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireAutomation(request.Actor);
        var material = await store.GetAsync(request.SessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"There is no Glass's session {request.SessionId}.");
        var session = material.Session;
        GlassRepairEstimateSessionPolicy.RequireActor(request.Actor, session);
        var provider = Unprotect(material.ProtectedProviderState);
        if (session.State != GlassRepairEstimateSessionState.Launching || provider.Editor is not { } open)
        {
            return session;
        }

        var digest = material.CallbackDigest;
        var kept = Kept(material);
        var editor = NewEditorClient(provider.EditorLastRequestAtUtc);
        string returned;
        try
        {
            GlassEditorReply<GlassEditorEstimate> read;
            try
            {
                read = await StageAsync(session, "EditorRead", () => editor.ReadEstimateAsync(
                    open.Session(), cancellationToken));
            }
            catch (Exception failure)
                when (failure is GlassMvaStageException || IsTransportFailure(failure, cancellationToken))
            {
                // Reading changes nothing: the session is open as it was.
                provider.EditorLastRequestAtUtc = editor.LastRequestAtUtc;
                return await WriteAsync(session, GlassRepairEstimateSessionState.Active, CodeOf(failure),
                    provider, digest, kept, CancellationToken.None);
            }

            provider.Editor = EditorState.Of(read.Session);
            provider.EditorLastRequestAtUtc = editor.LastRequestAtUtc;
            // An act whose answer was lost is settled by what the estimate holds.
            session = session with
            {
                Automation = GlassAutomationPositionPolicy.Settle(session.Automation, read.Value.Positions),
            };
            if (read.Value.Positions.Count == 0)
            {
                return await WriteAsync(session, GlassRepairEstimateSessionState.Active,
                    GlassRepairEstimateSessionPolicy.AutomationEmptyFailureCode, provider, digest, kept,
                    CancellationToken.None);
            }

            returned = await StageAsync(session, "EditorSave", () => editor.SaveAndExitAsync(
                read.Session, cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            provider.EditorLastRequestAtUtc = editor.LastRequestAtUtc;
            await WriteAsync(session, GlassRepairEstimateSessionState.Unknown, GlassFailure.Interrupted,
                provider, digest, kept, CancellationToken.None);
            throw;
        }
        catch (Exception failure)
            when (failure is GlassMvaStageException || IsTransportFailure(failure, cancellationToken))
        {
            // The save was asked for: what Glass's holds is Glass's to say.
            provider.Editor = null;
            provider.EditorLastRequestAtUtc = editor.LastRequestAtUtc;
            return await SettleAsync(session, AsFailure(failure), provider, digest, kept, CancellationToken.None);
        }

        // The editor has closed; nothing more is sent to it.
        provider.Editor = null;
        provider.EditorLastRequestAtUtc = editor.LastRequestAtUtc;
        return await ReturnBuiltAsync(request.Actor, session, provider, digest, returned, cancellationToken);
    }

    /// <summary>
    /// One read of the Automation's open estimate with one request (design
    /// 07 section 3.1; R1206): its positions and Glass's running totals, one
    /// position's figures, or Glass's calculation. Null when the session's
    /// editor is not open. Nothing changes at Glass's; the editor's cookies
    /// and the time of the request are kept, so the session's version moves.
    /// A positions read settles an act whose answer was lost. A read Glass's
    /// does not answer answers nothing read and its code, as a catalogue
    /// search does, and leaves the session as it stands.
    /// </summary>
    public async Task<GlassAutomationEstimate?> ReadAutomationEstimateAsync(
        ActionActor actor, Guid sessionId, GlassAutomationRead read, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(read);
        RequireAutomation(actor);
        var material = await store.GetAsync(sessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"There is no Glass's session {sessionId}.");
        var session = material.Session;
        GlassRepairEstimateSessionPolicy.RequireActor(actor, session);
        var provider = Unprotect(material.ProtectedProviderState);
        if (session.State != GlassRepairEstimateSessionState.Active || provider.Editor is not { } open)
        {
            return null;
        }

        var editor = NewEditorClient(provider.EditorLastRequestAtUtc);
        var done = await Positions.ReadAsync(session, editor, open.Session(), read, cancellationToken);
        provider.Editor = EditorState.Of(done.Opened);
        provider.EditorLastRequestAtUtc = editor.LastRequestAtUtc;
        // An act whose answer was lost is settled by what the estimate holds (design 07 section 3.4).
        session = await WriteAsync(
            done.Estimate is { } estimate
                ? session with
                {
                    Automation = GlassAutomationPositionPolicy.Settle(session.Automation, estimate.Positions),
                }
                : session,
            GlassRepairEstimateSessionState.Active, session.FailureCode,
            provider, material.CallbackDigest, Kept(material), cancellationToken);
        return done.For(session);
    }

    /// <summary>
    /// The Automation's catalogue search of its open estimate (design 07
    /// section 3.3): the special list's "Free positions parts" group, the
    /// part list, or both, asked for once each and matched against each
    /// row's description, part code or part number, ignoring case. A part is
    /// answered by its list's head row, the row an add takes. A side asked
    /// for keeps the parts of that side and leaves the special list unread,
    /// as a special operation has none. Nothing changes at Glass's; the
    /// editor's cookies and pace are kept, so the session's version moves. A
    /// list Glass's does not answer answers no rows and its code, and leaves
    /// the session as it stands. Null when the session's editor is not open.
    /// </summary>
    public async Task<GlassAutomationCatalogue?> SearchAutomationCatalogueAsync(
        GlassAutomationCatalogueQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (string.IsNullOrWhiteSpace(query.Row))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(query.Term);
        }

        RequireAutomation(query.Actor);
        var material = await store.GetAsync(query.SessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"There is no Glass's session {query.SessionId}.");
        var session = material.Session;
        GlassRepairEstimateSessionPolicy.RequireActor(query.Actor, session);
        var provider = Unprotect(material.ProtectedProviderState);
        if (session.State != GlassRepairEstimateSessionState.Active || provider.Editor is not { } open)
        {
            return null;
        }

        var editor = NewEditorClient(provider.EditorLastRequestAtUtc);
        var (rows, code, opened) = await Positions.SearchAsync(
            session, editor, open.Session(), query.Term.Trim(), query.Kind, query.Side,
            string.IsNullOrWhiteSpace(query.Row) ? null : query.Row.Trim(), cancellationToken);
        provider.Editor = EditorState.Of(opened);
        provider.EditorLastRequestAtUtc = editor.LastRequestAtUtc;
        session = await WriteAsync(session, GlassRepairEstimateSessionState.Active, session.FailureCode,
            provider, material.CallbackDigest, Kept(material), cancellationToken);
        return new(session, rows, code);
    }

    /// <summary>
    /// A preview in the Automation's open estimate (R1206): the dialog an add
    /// of the request's row and inputs would post, chosen as the add chooses
    /// it, or a position on the estimate's dialog, read as a form. Nothing is
    /// posted, no equipment answer is stored and no act is recorded; the
    /// editor's cookies and pace are kept, so the session's version moves. A
    /// page Glass's does not answer answers no dialog and its code, and leaves
    /// the session as it stands. Null when the session's editor is not open.
    /// </summary>
    public async Task<GlassAutomationPreview?> PreviewAutomationPositionAsync(
        GlassAutomationPositionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Action != GlassAutomationActAction.Preview)
        {
            throw new ArgumentException("The request is not a preview.", nameof(request));
        }

        var draft = GlassAutomationPositionPolicy.Draft(request, timeProvider.GetUtcNow());
        RequireAutomation(request.Actor);
        var material = await store.GetAsync(request.SessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"There is no Glass's session {request.SessionId}.");
        var session = material.Session;
        GlassRepairEstimateSessionPolicy.RequireActor(request.Actor, session);
        var provider = Unprotect(material.ProtectedProviderState);
        if (session.State != GlassRepairEstimateSessionState.Active || provider.Editor is not { } open)
        {
            return null;
        }

        var editor = NewEditorClient(provider.EditorLastRequestAtUtc);
        var (dialog, code, opened, equipment) = await Positions.PreviewAsync(
            session, editor, open.Session(), draft, cancellationToken);
        provider.Editor = EditorState.Of(opened);
        provider.EditorLastRequestAtUtc = editor.LastRequestAtUtc;
        session = await WriteAsync(session, GlassRepairEstimateSessionState.Active, session.FailureCode,
            provider, material.CallbackDigest, Kept(material), cancellationToken);
        return new(session, dialog, code) { Equipment = equipment };
    }

    /// <summary>
    /// A position act's prepare half (design 07 section 3.4): the act is
    /// claimed, Pending, under the session version the Automation read and
    /// before Glass's is asked anything. The same operation key again
    /// answers the act as recorded and owes nothing, whatever the version
    /// and whichever of the Case's Automation sessions recorded it.
    /// Every kind and operation is claimed and owed (R1206), with the inputs
    /// <see cref="GlassAutomationPositionPolicy.InputRefused"/> allows. Null
    /// when the session's editor is not open. A remove or a change may name
    /// any position of the estimate (R1185, R1206).
    /// </summary>
    public async Task<GlassAutomationPositionStep?> PrepareAutomationPositionAsync(
        GlassAutomationPositionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        if (request.Action == GlassAutomationActAction.Preview)
        {
            throw new ArgumentException("A preview is not an act.", nameof(request));
        }

        RequireAutomation(request.Actor);
        var operationKey = request.OperationKey.Trim();
        var material = await store.GetAsync(request.SessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"There is no Glass's session {request.SessionId}.");
        var session = material.Session;
        GlassRepairEstimateSessionPolicy.RequireActor(request.Actor, session);
        if (GlassAutomationPositionPolicy.Find(session.Automation, operationKey) is { } recorded)
        {
            return new(session, recorded, Owed: false);
        }
        // A key an earlier session of the Case recorded answers that act.
        if (await store.FindAutomationActSessionAsync(session.CaseId, operationKey, cancellationToken) is { } earlier
            && GlassAutomationPositionPolicy.Find(earlier.Automation, operationKey) is { } recordedEarlier)
        {
            return new(earlier, recordedEarlier, Owed: false);
        }

        var provider = Unprotect(material.ProtectedProviderState);
        if (session.State != GlassRepairEstimateSessionState.Active || provider.Editor is null)
        {
            return null;
        }
        if (session.Version != request.ExpectedVersion)
        {
            throw Conflict(
                GlassRepairEstimateSessionConflict.Version,
                session.Id,
                $"The Glass's session is at version {session.Version} and not {request.ExpectedVersion}.");
        }

        // Every kind and operation is sent, with the inputs the policy allows (R1206).
        var act = GlassAutomationPositionPolicy.Draft(request, timeProvider.GetUtcNow());

        session = await WriteAsync(
            session with { Automation = GlassAutomationPositionPolicy.Record(session.Automation, act) },
            GlassRepairEstimateSessionState.Active, session.FailureCode, provider, material.CallbackDigest,
            Kept(material), cancellationToken);
        return new(session, act, Owed: act.Outcome == GlassAutomationActOutcome.Pending);
    }

    /// <summary>
    /// A claimed position act's provider work (design 07 section 3.4). The
    /// estimate is read first: it is the add's <c>before</c>, and it settles
    /// every earlier act still open. An add then chooses its row as a build
    /// does, from the session's own lists, and is refused with the reason and
    /// nothing sent when the row is not certain (R1169); a remove must name a
    /// position the estimate holds. Before an add is posted, the code its
    /// position will carry is recorded, so a lost answer is settled by the
    /// next read. The dialog is posted as Glass's serves it, with the button,
    /// operation and answers the Automation gave (R1206), nothing typed
    /// (R1168). The outcome, the served figures and the editor's cookies and
    /// pace are recorded. An act no longer Pending, or a session whose editor
    /// is closed, is answered as it stands.
    /// </summary>
    public async Task<GlassAutomationPositionStep> ContinueAutomationPositionAsync(
        GlassAutomationPositionContinueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireAutomation(request.Actor);
        var material = await store.GetAsync(request.SessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"There is no Glass's session {request.SessionId}.");
        var session = material.Session;
        GlassRepairEstimateSessionPolicy.RequireActor(request.Actor, session);
        var act = GlassAutomationPositionPolicy.Find(session.Automation, request.OperationKey)
            ?? throw new KeyNotFoundException($"The Glass's session {request.SessionId} has no such act.");
        var provider = Unprotect(material.ProtectedProviderState);
        if (act.Outcome != GlassAutomationActOutcome.Pending
            || session.State != GlassRepairEstimateSessionState.Active
            || provider.Editor is not { } open)
        {
            return new(session, act, Owed: false);
        }

        var position = new PositionWork(
            session, provider, NewEditorClient(provider.EditorLastRequestAtUtc), open.Session(),
            material.CallbackDigest, Kept(material));
        GlassEditorEstimate before;
        try
        {
            var read = await StageAsync(session, "EditorRead", () => position.Editor.ReadEstimateAsync(
                position.Opened, cancellationToken));
            position.Opened = read.Session;
            before = read.Value;
        }
        catch (Exception failure)
            when (failure is GlassMvaStageException || IsTransportFailure(failure, cancellationToken))
        {
            // Reading changes nothing: nothing was sent for the act.
            return await RecordActAsync(
                position, act with { Outcome = GlassAutomationActOutcome.Refused, Code = CodeOf(failure) });
        }

        position.Session = position.Session with
        {
            Automation = GlassAutomationPositionPolicy.Settle(
                position.Session.Automation, before.Positions, act.OperationKey),
        };

        // The act is written before its post and once it has an outcome,
        // with the editor's cookies and pace as they stand.
        Task Record(GlassAutomationAct recorded, GlassEditorSession opened)
        {
            position.Opened = opened;
            return WriteActAsync(position, recorded);
        }

        var done = act.Action switch
        {
            GlassAutomationActAction.Remove => await Positions.RemovePositionAsync(
                position.Session, position.Editor, position.Opened, act, before, Record, cancellationToken),
            GlassAutomationActAction.Change => await Positions.ChangePositionAsync(
                position.Session, position.Editor, position.Opened, act, before, Record, cancellationToken),
            _ => await Positions.AddPositionAsync(
                position.Session, position.Editor, position.Opened, act, before, Record, cancellationToken),
        };
        return new(position.Session, done.Act, Owed: false);
    }

    /// <summary>
    /// Writes the act as it now stands, with the editor's cookies and pace.
    /// The write is made whatever the time cap does, so what was sent is
    /// never forgotten. A position added, removed or changed clears the
    /// session's last code; any other outcome leaves it.
    /// </summary>
    private async Task WriteActAsync(PositionWork position, GlassAutomationAct act)
    {
        position.Provider.Editor = EditorState.Of(position.Opened);
        position.Provider.EditorLastRequestAtUtc = position.Editor.LastRequestAtUtc;
        var session = position.Session;
        position.Session = await WriteAsync(
            session with { Automation = GlassAutomationPositionPolicy.Record(session.Automation, act) },
            GlassRepairEstimateSessionState.Active,
            act.Outcome is GlassAutomationActOutcome.Added or GlassAutomationActOutcome.Removed
                or GlassAutomationActOutcome.Changed ? null : session.FailureCode,
            position.Provider, position.Digest, position.Kept, CancellationToken.None);
    }

    private async Task<GlassAutomationPositionStep> RecordActAsync(PositionWork position, GlassAutomationAct act)
    {
        await WriteActAsync(position, act);
        return new(position.Session, act, Owed: false);
    }

    /// <summary>One position act's work in hand: the session as last written, its protected state, the editor and its session.</summary>
    private sealed class PositionWork(
        GlassRepairEstimateSession session,
        ProviderState provider,
        GlassEditorClient editor,
        GlassEditorSession opened,
        string digest,
        Results? kept)
    {
        public GlassRepairEstimateSession Session { get; set; } = session;

        public ProviderState Provider { get; } = provider;

        public GlassEditorClient Editor { get; } = editor;

        public GlassEditorSession Opened { get; set; } = opened;

        public string Digest { get; } = digest;

        public Results? Kept { get; } = kept;
    }

    /// <summary>A session that has not started its estimate yet runs the launch stages; any other is reopened.</summary>
    private static bool Launching(ProviderState provider) =>
        provider.EreId is null && provider.PegasusCallback is not null;

    /// <summary>A session's results as stored: none stays none, so a write keeps the column as it was.</summary>
    private static Results? Kept(GlassRepairEstimateSessionMaterial material) =>
        string.IsNullOrWhiteSpace(material.ResultArtifactsJson) ? null : Deserialize(material.ResultArtifactsJson);

    /// <summary>
    /// The address the operator's browser opens for a live session, or null
    /// when there is nothing to open.
    /// </summary>
    /// <remarks>
    /// <see cref="IGlassRepairEstimateGateway"/> answers a
    /// <see cref="GlassRepairEstimateSession"/>, which records the identities of
    /// a launch but not the URL it produced — and that URL carries the one-use
    /// callback token, so it cannot be a field on a read model the Case page
    /// projects. It is read back here, from the protected state, by the
    /// staff member who launched it.
    /// </remarks>
    public async Task<Uri?> GetEstimatorUrlAsync(
        ActionActor actor, Guid sessionId, CancellationToken cancellationToken)
    {
        GlassRepairEstimateSessionPolicy.RequireSessionActor(actor);
        return await store.GetAsync(sessionId, cancellationToken) is { } material
            ? await GetEstimatorUrlAsync(actor, material, cancellationToken)
            : null;
    }

    public Task<Uri?> GetEstimatorUrlAsync(
        ActionActor actor, GlassRepairEstimateSessionMaterial material, CancellationToken cancellationToken)
    {
        GlassRepairEstimateSessionPolicy.RequireSessionActor(actor);
        ArgumentNullException.ThrowIfNull(material);
        GlassRepairEstimateSessionPolicy.RequireActor(actor, material.Session);
        var provider = Unprotect(material.ProtectedProviderState);
        return Task.FromResult<Uri?>(
            material.Session.State == GlassRepairEstimateSessionState.Active
            && provider.EstimatorUrl is { } url
                ? new Uri(url, UriKind.Absolute)
                : null);
    }

    /// <summary>
    /// A resume's prepare half. Current Case authority, unchanged vehicle
    /// facts and the launching credential are proved before anything is
    /// written, and the proved authority replaces the protected import
    /// authority. A session waiting to be imported is claimed for the import;
    /// a live one is recorded for its launch to continue, however long ago it
    /// started; one whose outcome cannot be known is settled here and owes
    /// nothing more.
    /// </summary>
    public async Task<GlassRepairEstimateStep> PrepareResumeAsync(
        GlassRepairEstimateResumeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        GlassRepairEstimateSessionPolicy.RequireSessionActor(request.Actor);
        var material = await RequireSessionAsync(request.SessionId, request.ExpectedVersion, cancellationToken);
        var session = material.Session;
        GlassRepairEstimateSessionPolicy.RequireActor(request.Actor, session);
        var provider = Unprotect(material.ProtectedProviderState);
        var results = Deserialize(material.ResultArtifactsJson);

        // A session Failed after the relay because the reader refused the
        // export, or because the export could not be fetched, is taken up
        // again the way a claimed return is: the export is fetched again for
        // the estimate it already has.
        var refetch = GlassRepairEstimateSessionPolicy.CanRefetchExport(session.State, session.FailureCode);
        if (!GlassRepairEstimateSessionPolicy.OccupiesAccount(session.State) && !refetch)
        {
            throw new GlassRepairEstimateRefusalException(
                $"A Glass's session in {session.State} cannot be resumed.");
        }
        if (request.ExpectedCaseVersion <= 0 || string.IsNullOrWhiteSpace(request.LeaseToken))
        {
            throw new GlassRepairEstimateRefusalException(
                "Resuming a Glass's session requires the current Case version and edit lease.");
        }
        var facts = await caseAuthority.RequireEditAuthorityAsync(
            request.Actor, session.CaseId, request.ExpectedCaseVersion, request.LeaseToken, cancellationToken);
        (provider.Registration, provider.MileageMiles) = GlassRepairEstimateSessionPolicy.RequireUnchangedVehicle(
            provider.Registration, provider.MileageMiles, provider.Placeholder,
            vehicleRecorded: provider.MvaVehicleId is not null, facts.Registration, facts.MileageMiles);
        await AccountForAsync(request.Actor, session, cancellationToken);
        provider.CaseVersion = request.ExpectedCaseVersion;
        provider.LeaseToken = request.LeaseToken;

        var held = session.State == GlassRepairEstimateSessionState.AwaitingImport
            || (session.State is GlassRepairEstimateSessionState.Importing or GlassRepairEstimateSessionState.Unknown
                && results.Xml is not null);
        var claimed = (refetch
                || session.State is GlassRepairEstimateSessionState.Importing or GlassRepairEstimateSessionState.Unknown)
            && results.CallbackQueryDigest is not null;
        if (held || claimed)
        {
            if (!held && (provider.MvaVehicleId is null || provider.EreId is null))
            {
                throw new InvalidOperationException(
                    "The Glass's session has no vehicle or estimate to look up, so its outcome stays for reconciliation.");
            }

            // Claim the import before touching custody, the provider or the
            // Case. Close is unavailable while this claim is running.
            session = await WriteAsync(session, GlassRepairEstimateSessionState.Importing,
                refetch ? null : session.FailureCode, provider, material.CallbackDigest, results, cancellationToken);
            return new(session, GlassRepairEstimateContinuation.Import);
        }

        if (refetch)
        {
            throw new GlassRepairEstimateRefusalException(
                "This Glass's session carries no accepted return to fetch the export for again.");
        }

        // A host may have stopped after the provider acted but before the ID
        // was recorded. A retry cannot prove it closed.
        if (IsUncertain(session, provider))
        {
            return new(
                await WriteAsync(session, GlassRepairEstimateSessionState.Unknown,
                    GlassFailure.TransportUnknown, provider, material.CallbackDigest, results, cancellationToken),
                GlassRepairEstimateContinuation.None);
        }
        var launching = provider.EreId is null && provider.PegasusCallback is not null;
        if (!launching)
        {
            RequireReopenable(provider);
        }

        // The proved authority is recorded before the launch continues, so the
        // import this launch leads to lands under it.
        session = await WriteAsync(session, session.State, null, provider, material.CallbackDigest,
            launching ? null : results, cancellationToken);
        return new(session, GlassRepairEstimateContinuation.Launch);
    }

    /// <summary>
    /// A return's prepare half: prove the correlation and the owner, and claim
    /// the delivery. Everything after the claim that needs no provider — a
    /// replaced credential, a calculation that was not saved — is settled
    /// here; a saved one owes its import, however long after the launch it
    /// arrives (operator, 9 October 2026).
    /// </summary>
    public async Task<GlassRepairEstimateStep> AcceptCallbackAsync(
        GlassRepairEstimateCallback callback, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(callback.RawQuery);
        GlassRepairEstimateSessionPolicy.RequireSessionActor(callback.Actor);
        return await AcceptReadAsync(
            callback, await store.GetAsync(callback.SessionId, cancellationToken), cancellationToken);
    }

    public async Task<GlassRepairEstimateStep> AcceptCallbackAsync(
        GlassRepairEstimateCallback callback, GlassRepairEstimateSessionMaterial material,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(callback.RawQuery);
        ArgumentNullException.ThrowIfNull(material);
        GlassRepairEstimateSessionPolicy.RequireSessionActor(callback.Actor);
        return await AcceptReadAsync(callback, material, cancellationToken);
    }

    private async Task<GlassRepairEstimateStep> AcceptReadAsync(
        GlassRepairEstimateCallback callback, GlassRepairEstimateSessionMaterial? read,
        CancellationToken cancellationToken)
    {
        var material = RequireCorrelated(callback, read);
        var session = material.Session;
        var provider = Unprotect(material.ProtectedProviderState);
        var results = Deserialize(material.ResultArtifactsJson);
        var queryDigest = Sha256Hex(callback.RawQuery);

        if (session.CallbackConsumedAtUtc is not null)
        {
            // Already acted on. The same delivery reads back what it produced;
            // a different one is a second, contradictory message and changes
            // nothing.
            return string.Equals(results.CallbackQueryDigest, queryDigest, StringComparison.Ordinal)
                ? new GlassRepairEstimateStep(session, GlassRepairEstimateContinuation.None)
                : throw Conflict(
                    GlassRepairEstimateSessionConflict.Callback,
                    session.Id,
                    "A different Glass's callback has already been acted on for this session.");
        }
        if (session.State != GlassRepairEstimateSessionState.Active)
        {
            throw Conflict(
                GlassRepairEstimateSessionConflict.Callback,
                session.Id,
                "This Glass's session is not waiting for a callback.");
        }
        GlassRepairEstimateSessionPolicy.RequireActor(callback.Actor, session);
        if (provider.EreId is null || provider.OriginalCallback is null)
        {
            throw new InvalidOperationException(
                "An active Glass's session must carry the estimate it started.");
        }

        // The claim. This delivery is the one acted on: its fingerprint and the
        // move to Importing go through the store's version check before the
        // provider hears anything, so two deliveries racing for the same
        // version meet here — one is recorded, the other reads the record.
        // The message itself is kept with the protected provider state, so the
        // relay can still be made when the work that owes it did not run.
        results.CallbackQueryDigest = queryDigest;
        provider.ReturnQuery = callback.RawQuery;
        try
        {
            session = await WriteAsync(
                session,
                GlassRepairEstimateSessionState.Importing,
                null,
                provider,
                material.CallbackDigest,
                results,
                cancellationToken);
        }
        catch (GlassRepairEstimateSessionConflictException lost)
            when (lost.Conflict == GlassRepairEstimateSessionConflict.Version)
        {
            return new(
                await ReadClaimAsync(callback, queryDigest, lost, cancellationToken),
                GlassRepairEstimateContinuation.None);
        }

        if (!await HoldsLaunchAccountAsync(callback.Actor, session, cancellationToken))
        {
            // The credential that launched this has been replaced or turned
            // off; the session it opened is no longer this staff member's to finish.
            return new(
                await WriteAsync(
                    session,
                    GlassRepairEstimateSessionState.Expired,
                    GlassFailure.CallbackExpired,
                    provider,
                    material.CallbackDigest,
                    results,
                    cancellationToken),
                GlassRepairEstimateContinuation.None);
        }
        if (Query(callback.RawQuery, "DoSave") != "1")
        {
            return new(
                await SettleAsync(
                    session,
                    new GlassMvaStageException(GlassFailure.CallbackNotSaved),
                    provider,
                    material.CallbackDigest,
                    results,
                    cancellationToken),
                GlassRepairEstimateContinuation.None);
        }

        return new(session, GlassRepairEstimateContinuation.Import);
    }

    /// <summary>
    /// An import's provider work, from where its claim left it: a retained
    /// result is resolved through custody and landed; an accepted return whose
    /// relay has not begun relays the provider's own message, once, then
    /// exports; a relay whose answer was lost is looked up again and never
    /// relayed again. A session no import is owed to is returned as it stands.
    /// </summary>
    public async Task<GlassRepairEstimateSession> ContinueImportAsync(
        GlassRepairEstimateContinueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        GlassRepairEstimateSessionPolicy.RequireSessionActor(request.Actor);
        var material = await store.GetAsync(request.SessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"There is no Glass's session {request.SessionId}.");
        var session = material.Session;
        GlassRepairEstimateSessionPolicy.RequireActor(request.Actor, session);
        if (session.State != GlassRepairEstimateSessionState.Importing)
        {
            return session;
        }

        var provider = Unprotect(material.ProtectedProviderState);
        var results = Deserialize(material.ResultArtifactsJson);
        var digest = material.CallbackDigest;
        // The accepted return is relayed once. The mark goes into the same
        // claim that precedes the relay, so a claim whose relay never began is
        // told apart from one whose answer was lost.
        var relay = results.Xml is null && !results.RelayStarted && provider.ReturnQuery is not null;
        if (relay)
        {
            if (!string.Equals(Sha256Hex(provider.ReturnQuery!), results.CallbackQueryDigest, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The retained Glass's return is not the one this session accepted, so its outcome stays for reconciliation.");
            }
            results.RelayStarted = true;
        }

        // Claim this version before any external work, so a Resume that read
        // the session earlier cannot also act on it.
        session = await WriteAsync(session, GlassRepairEstimateSessionState.Importing, session.FailureCode,
            provider, digest, results, cancellationToken);
        try
        {
            if (results.Xml is not null)
            {
                // A retention whose answer was lost already has its identities,
                // so this asks custody what became of it instead of offering the
                // same bytes a second time.
                results.Xml = await ResolveAsync(request.Actor, session.CaseId, results.Xml, cancellationToken);
                results.Pdf = await ResolveAsync(request.Actor, session.CaseId, results.Pdf, cancellationToken);
                return await FinishAsync(request.Actor, session, provider, digest, results, cancellationToken);
            }

            return relay
                ? await RelayAsync(request.Actor, session, provider, digest, results, provider.ReturnQuery!, cancellationToken)
                : await LookUpExportAsync(request.Actor, session, provider, digest, results, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await WriteAsync(session, GlassRepairEstimateSessionState.Unknown,
                GlassFailure.Interrupted, provider, digest, results, CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// The operator's Save &amp; Exit relayed back to the provider exactly as it
    /// arrived, then the export it produced.
    /// </summary>
    private async Task<GlassRepairEstimateSession> RelayAsync(
        ActionActor actor,
        GlassRepairEstimateSession session,
        ProviderState provider,
        string callbackDigest,
        Results results,
        string rawQuery,
        CancellationToken cancellationToken)
    {
        if (provider.EreId is not { } ereId || provider.OriginalCallback is not { } originalCallback)
        {
            throw new InvalidOperationException(
                "An accepted Glass's return must carry the estimate it started.");
        }

        var client = NewClient(provider.Cookies);
        try
        {
            await StageAsync(session, "RelayCallback", () => client.RelayCallbackAsync(
                new Uri(originalCallback, UriKind.Absolute),
                EstimateIdsOf(provider, ereId),
                rawQuery,
                cancellationToken));
        }
        catch (Exception failure)
            when (failure is GlassMvaStageException || IsTransportFailure(failure, cancellationToken))
        {
            return await SettleAsync(
                session, AsFailure(failure), provider, callbackDigest, results, cancellationToken);
        }

        return await ExportAsync(
            actor, session, provider, callbackDigest, results, client, ereId, cancellationToken);
    }

    /// <summary>
    /// The operator's Save &amp; Exit was claimed and its answer was lost: a
    /// transport failure after the relay, or a host that stopped between the
    /// claim and the record. The relay is never repeated; the export it
    /// produced is looked up again, which is the one safe retry.
    /// </summary>
    private async Task<GlassRepairEstimateSession> LookUpExportAsync(
        ActionActor actor,
        GlassRepairEstimateSession session,
        ProviderState provider,
        string callbackDigest,
        Results results,
        CancellationToken cancellationToken)
    {
        if (results.CallbackQueryDigest is null
            || provider.MvaVehicleId is not { } lookupVehicle
            || provider.EreId is not { } lookupEre)
        {
            throw new InvalidOperationException(
                "The Glass's session has no vehicle or estimate to look up, so its outcome stays for reconciliation.");
        }

        var credential = await AccountForAsync(actor, session, cancellationToken);
        provider.Cookies.Clear();
        var lookup = NewClient(provider.Cookies);
        try
        {
            await StageAsync(session, "SignIn", () => lookup.SignInAsync(credential.Username, credential.Password, cancellationToken));
            await RequireSessionVehicleAsync(session, provider, lookup, lookupVehicle, cancellationToken);
            await StageAsync(session, "SelectOnly", () => lookup.SelectOnlyAsync(lookupVehicle, cancellationToken));
        }
        catch (Exception failure)
            when (failure is GlassMvaStageException || IsTransportFailure(failure, cancellationToken))
        {
            return await SettleUnknownAsync(
                session, AsFailure(failure), provider, callbackDigest, results, cancellationToken);
        }

        return await ExportAsync(
            actor, session, provider, callbackDigest, results, lookup, lookupEre, cancellationToken);
    }

    /// <summary>
    /// A session left in a state only running provider work holds, with nothing
    /// running for it: a host that stopped, or work that ran past its time.
    /// Prepared never reached the provider and stays resumable; Launching and
    /// Importing may have acted there, so they become Unknown and keep the
    /// account.
    /// </summary>
    public async Task<GlassRepairEstimateSession> SettleInterruptedAsync(
        ActionActor actor, Guid sessionId, CancellationToken cancellationToken)
    {
        GlassRepairEstimateSessionPolicy.RequireSessionActor(actor);
        return await SettleInterruptedAsync(
            actor,
            await store.GetAsync(sessionId, cancellationToken)
                ?? throw new KeyNotFoundException($"There is no Glass's session {sessionId}."),
            cancellationToken);
    }

    public async Task<GlassRepairEstimateSession> SettleInterruptedAsync(
        ActionActor actor, GlassRepairEstimateSessionMaterial material, CancellationToken cancellationToken)
    {
        GlassRepairEstimateSessionPolicy.RequireSessionActor(actor);
        ArgumentNullException.ThrowIfNull(material);
        var sessionId = material.Session.Id;
        var session = material.Session;
        GlassRepairEstimateSessionPolicy.RequireActor(actor, session);
        if (!GlassRepairEstimateSessionPolicy.AwaitsProviderWork(session.State)
            || (session.State == GlassRepairEstimateSessionState.Prepared
                && session.FailureCode == GlassFailure.Interrupted))
        {
            return session;
        }

        var state = session.State == GlassRepairEstimateSessionState.Prepared
            ? GlassRepairEstimateSessionState.Prepared
            : GlassRepairEstimateSessionState.Unknown;
        LogSettled(logger, session.Id, session.CaseId, state, GlassFailure.Interrupted, string.Empty);
        try
        {
            return await WriteAsync(
                session,
                state,
                GlassFailure.Interrupted,
                Unprotect(material.ProtectedProviderState),
                material.CallbackDigest,
                string.IsNullOrWhiteSpace(material.ResultArtifactsJson) ? null : Deserialize(material.ResultArtifactsJson),
                cancellationToken);
        }
        catch (GlassRepairEstimateSessionConflictException moved)
            when (moved.Conflict == GlassRepairEstimateSessionConflict.Version)
        {
            // Something else moved the session on first; that is its answer.
            return (await store.GetAsync(sessionId, cancellationToken))?.Session ?? session;
        }
    }

    /// <summary>
    /// Re-proves the vehicle a session already recorded before it is selected
    /// again: a placeholder by the placeholder rule, under the type number the
    /// launch recorded; any other vehicle by the launch's registration,
    /// mileage and type number. A session that has an estimate is proved as
    /// the portal shows a vehicle that has one: its repair-profile control
    /// locked, with the configured profile selected.
    /// </summary>
    private Task RequireSessionVehicleAsync(
        GlassRepairEstimateSession session, ProviderState provider, GlassMvaClient client,
        string vehicleId, CancellationToken cancellationToken)
    {
        var estimateStarted = provider.EreId is not null;
        return provider.Placeholder
            ? StageAsync(session, "RequirePlaceholder", () => client.RequirePlaceholderAsync(
                vehicleId, provider.NatCode, estimateStarted, cancellationToken))
            : StageAsync(session, "RequireVehicle", () => client.RequireVehicleAsync(vehicleId, provider.NatCode!,
                provider.Registration, provider.MileageMiles, estimateStarted, cancellationToken));
    }

    /// <summary>
    /// A session past Prepared whose vehicle was never recorded: the provider
    /// may have made one, and a retry cannot prove it
    /// did not. A start that went unanswered on a recorded vehicle is not
    /// uncertain in this way: starting again on that vehicle answers the
    /// estimate it holds, so the launch stages take it up where they stopped.
    /// </summary>
    private static bool IsUncertain(GlassRepairEstimateSession session, ProviderState provider) =>
        session.State != GlassRepairEstimateSessionState.Prepared
        && provider.MvaVehicleId is null;

    private static (string VehicleId, string EreId) RequireReopenable(ProviderState provider) =>
        provider is { MvaVehicleId: { } vehicleId, EreId: { } ereId }
            ? (vehicleId, ereId)
            : throw new InvalidOperationException(
                "The Glass's session has no vehicle or estimate to resume, so its outcome stays for reconciliation.");

    /// <summary>
    /// What a delivery that lost the claim reads: the record the winner made.
    /// The same message finds its own fingerprint there and gets the session
    /// as it stands; a different one is a second, contradictory message.
    /// </summary>
    private async Task<GlassRepairEstimateSession> ReadClaimAsync(
        GlassRepairEstimateCallback callback,
        string queryDigest,
        GlassRepairEstimateSessionConflictException lost,
        CancellationToken cancellationToken)
    {
        var material = await store.GetAsync(callback.SessionId, cancellationToken) ?? throw lost;
        var recorded = Deserialize(material.ResultArtifactsJson).CallbackQueryDigest;
        if (recorded is null)
        {
            // Something other than a callback moved the session on.
            throw lost;
        }

        return string.Equals(recorded, queryDigest, StringComparison.Ordinal)
            ? material.Session
            : throw Conflict(
                GlassRepairEstimateSessionConflict.Callback,
                material.Session.Id,
                "A different Glass's callback has already been acted on for this session.");
    }

    /// <summary>
    /// From the provider's export to the Draft: wait for the export the relay
    /// produced, download it, read it, reconcile it against the vehicle this
    /// session launched for, retain both artifacts and land the estimate.
    /// Nothing here writes at the provider, which is what lets a lost answer
    /// be looked up again instead of relayed again.
    /// </summary>
    private async Task<GlassRepairEstimateSession> ExportAsync(
        ActionActor actor,
        GlassRepairEstimateSession session,
        ProviderState provider,
        string callbackDigest,
        Results results,
        GlassMvaClient client,
        string ereId,
        CancellationToken cancellationToken)
    {
        byte[] exported;
        try
        {
            var link = await StageAsync(session, "WaitForExport", () => client.WaitForExportAsync(cancellationToken));
            exported = await StageAsync(session, "DownloadExport", () => client.DownloadExportAsync(link, cancellationToken));
        }
        catch (Exception failure)
            when (failure is GlassMvaStageException || IsTransportFailure(failure, cancellationToken))
        {
            return await SettleAsync(
                session, AsFailure(failure), provider, callbackDigest, results, cancellationToken);
        }

        GlassEstimateExport export;
        try
        {
            export = GlassEstimateXmlParser.Read(exported);
            RequireSameVehicle(export, provider);
            if (provider.Placeholder)
            {
                LogPlaceholderReturn(
                    logger,
                    session.Id,
                    session.CaseId,
                    export.Identity.TypeNumber ?? "absent",
                    string.IsNullOrWhiteSpace(export.Identity.RegistrationPlate) ? "absent" : "case",
                    export.Identity.Mileage is null or 0 ? "absent" : "case");
            }
        }
        catch (EstimateParseRejectedException rejection)
        {
            // The reader's refusal is a fact about this document, and the
            // document exists only here and at Glass's. Keep it on the Case
            // before the session settles, so it can be read again once the
            // reader accepts it.
            results.RejectedXml = await KeepRejectedExportAsync(
                actor, session, ereId, exported, cancellationToken);
            return await SettleAsync(
                session, UnreadableExport(rejection, provider), provider, callbackDigest, results, cancellationToken);
        }
        catch (GlassMvaStageException failure)
        {
            if (failure.FailureCode is GlassFailure.IdentityRegistration or GlassFailure.IdentityMileage
                or GlassFailure.IdentityNatCode or GlassFailure.ExportEmpty)
            {
                // The refusal is a fact about this export, which exists only
                // here and at Glass's: an Engineer who corrected the vehicle
                // inside Glass's has saved work that must not be lost. Keep it
                // as a reader refusal does; nothing is imported.
                results.RejectedXml = await KeepRejectedExportAsync(
                    actor, session, ereId, exported, cancellationToken);
            }

            return await SettleAsync(session, failure, provider, callbackDigest, results, cancellationToken);
        }

        try
        {
            results.Xml = await StageAsync(session, "RetainArtifact", () => RetainAsync(
                actor,
                session,
                XmlOccurrenceIdentity(session.Id),
                $"{session.OperationKey}:xml",
                $"glass-estimate-{ereId}.xml",
                "application/xml",
                exported,
                SeriesIdentity(ereId, "xml"),
                cancellationToken));
            if (export.CalculationSheet is { } sheet)
            {
                results.Pdf = await StageAsync(session, "RetainArtifact", () => RetainAsync(
                    actor,
                    session,
                    PdfOccurrenceIdentity(session.Id),
                    $"{session.OperationKey}:pdf",
                    sheet.FileName,
                    "application/pdf",
                    sheet.Content.ToArray(),
                    SeriesIdentity(ereId, "pdf"),
                    cancellationToken));
            }
        }
        catch (Exception failure) when (!cancellationToken.IsCancellationRequested)
        {
            // Custody refused or could not be reached. Glass's already has the
            // export and the relay is not made again, so the outcome is the
            // uncertain one a lookup resolves: Resume signs in again and
            // fetches the export, and custody answers the same identities.
            // Nothing is reported as interrupted: something was wrong.
            LogCustodyFailed(logger, session.Id, session.CaseId, failure);
            results.Xml = null;
            results.Pdf = null;
            return await SettleAsync(
                session,
                new GlassMvaStageException(GlassFailure.CustodyFailed, outcomeUnknown: true, failure.GetType().Name),
                provider,
                callbackDigest,
                results,
                cancellationToken);
        }

        return await FinishAsync(actor, session, provider, callbackDigest, results, cancellationToken);
    }

    /// <summary>
    /// Keeps an export the reader refused as a rejected Glass's export on the
    /// Case, through the same custody as every other retained artifact. The
    /// export was already fetched, so a custody failure here is logged and
    /// never hides the rejection it accompanies.
    /// </summary>
    private async Task<Artifact?> KeepRejectedExportAsync(
        ActionActor actor,
        GlassRepairEstimateSession session,
        string ereId,
        byte[] exported,
        CancellationToken cancellationToken)
    {
        try
        {
            return await StageAsync(session, "RetainRejectedExport", () => RetainAsync(
                actor,
                session,
                RejectedXmlOccurrenceIdentity(session.Id),
                $"{session.OperationKey}:rejected-xml",
                $"glass-estimate-{ereId}-rejected.xml",
                "application/xml",
                exported,
                SeriesIdentity(ereId, "rejected-xml"),
                cancellationToken));
        }
        catch (Exception failure) when (!cancellationToken.IsCancellationRequested)
        {
            LogCustodyFailed(logger, session.Id, session.CaseId, failure);
            return null;
        }
    }

    /// <summary>
    /// The unreadable-export failure with the reader's own reason as its
    /// detail, so the host log says which position or field was refused.
    /// </summary>
    private static GlassMvaStageException UnreadableExport(
        EstimateParseRejectedException rejection, ProviderState provider) =>
        new(GlassFailure.ExportUnreadable, detail: GlassReaderReason.Of(rejection.Message, provider.Registration));

    /// <summary>
    /// The last step, shared by a completing callback and a resumed import:
    /// read what every retained artifact came to, then land the estimate. A
    /// retention that is not yet confirmed keeps its identities and waits; one
    /// that failed stops the session at Failed with nothing imported.
    /// </summary>
    private async Task<GlassRepairEstimateSession> FinishAsync(
        ActionActor actor,
        GlassRepairEstimateSession session,
        ProviderState provider,
        string callbackDigest,
        Results results,
        CancellationToken cancellationToken)
    {
        if (results.Xml is null)
        {
            throw new InvalidOperationException(
                "A Glass's session cannot be imported before its export has been retained.");
        }
        if (Failed(results.Xml) || Failed(results.Pdf))
        {
            return await SettleAsync(
                session,
                new GlassMvaStageException(GlassFailure.CustodyFailed),
                provider,
                callbackDigest,
                results,
                cancellationToken);
        }
        if (!Confirmed(results.Xml) || (results.Pdf is not null && !Confirmed(results.Pdf)))
        {
            // The artifacts are recorded with the identities custody gave them,
            // so a later resume asks custody what happened instead of offering
            // the same bytes again.
            return await WriteAsync(
                session,
                GlassRepairEstimateSessionState.AwaitingImport,
                null,
                provider,
                callbackDigest,
                results,
                cancellationToken);
        }

        // The estimate belongs to its stock vehicle, and the repair spec to
        // the estimate: a return for a vehicle a live spec already stands on
        // updates that spec, and any other return makes the spec and records
        // the vehicle on it (operator, 6 October 2026).
        var link = provider is { MvaVehicleId: { } linkedVehicle, EreId: { } linkedEstimate, NatCode: { } linkedType }
            ? new GlassEstimateLink(
                linkedVehicle, linkedEstimate, linkedType, provider.Placeholder,
                provider.Registration, provider.MileageMiles)
            : null;
        var specification = link is null
            ? null
            : await caseAuthority.FindSpecificationAsync(session.CaseId, link.VehicleId, cancellationToken);
        try
        {
            var imported = await StageAsync(session, "ImportDraft", () => import.ExecuteAsync(
                new ImportRawEstimateRequest(
                    actor,
                    session.CaseId,
                    provider.CaseVersion,
                    provider.LeaseToken,
                    results.Xml.OccurrenceId
                        ?? throw new InvalidOperationException(
                            "A retained Glass's export names no Case occurrence, so it cannot be imported."),
                    results.Xml.VersionId!.Value,
                    results.Xml.Sha256!,
                    $"{session.OperationKey}:import",
                    Name: string.Empty)
                {
                    // The estimate lands under the staff member's session and
                    // never ends it: their page catches up on the import as on
                    // any system write (operator, 6 October 2026).
                    KeepsLease = true,
                    GlassEstimate = link,
                    EstimateId = specification,
                },
                cancellationToken));
            results.ImportedEstimateId = imported.EstimateId;
            // A build's own return lands as a new Glass's repair spec beside
            // the outside one (operator, 18 November 2026), and the build
            // names it so its list is shown on that spec (R1173).
            if (session.Build is { Running: true } built)
            {
                session = session with { Build = built with { BuiltSpecificationId = imported.EstimateId } };
            }
            // The Automation's finish lands as a Draft (an Automation import is
            // never put in use), and its session names it for the Automation
            // and the Case page.
            if (session.OwnerKind == ActorKind.Automation)
            {
                session = session with
                {
                    Automation = (session.Automation ?? new GlassAutomationWork()) with
                    {
                        DraftSpecificationId = imported.EstimateId,
                    },
                };
            }
        }
        catch (Exception stale)
            when (stale is CaseVersionConflictException
                or CaseEditLeaseExpiredException
                or CaseEditLeaseConflictException)
        {
            // The Case moved on while the operator was in Glass's. Everything
            // the provider produced is already retained; the estimate lands
            // under the staff member's current session, or when they take the
            // Case back. The reason is said here, once: the session records no
            // code, as nothing failed at Glass's.
            LogImportHeld(logger, session.Id, session.CaseId, stale.GetType().Name, stale.Message);
            return await WriteAsync(
                session,
                GlassRepairEstimateSessionState.AwaitingImport,
                null,
                provider,
                callbackDigest,
                results,
                cancellationToken);
        }
        catch (EstimateParseRejectedException rejection)
        {
            return await SettleAsync(
                session, UnreadableExport(rejection, provider), provider, callbackDigest, results, cancellationToken);
        }

        return await WriteAsync(
            session,
            GlassRepairEstimateSessionState.Completed,
            null,
            provider,
            callbackDigest,
            results,
            cancellationToken);
    }

    /// <summary>
    /// What the export says about itself must be the vehicle this session
    /// launched for, and it must actually cost something. A zero-position
    /// calculation is a real Glass's document, but it is not an estimate to
    /// import, and it says so rather than landing as an empty Draft.
    /// </summary>
    /// <remarks>
    /// A placeholder session launched on a vehicle with no registration, no
    /// mileage and no real type number, and the Engineer identified the
    /// vehicle inside the estimator. Its export may therefore name no plate
    /// and no mileage, or the Case's own; any other vehicle's is refused. The
    /// type number it names is the Engineer's choice and is recorded, not
    /// compared (operator, 2 October 2026; to be read against the first live
    /// return).
    /// </remarks>
    private static void RequireSameVehicle(GlassEstimateExport export, ProviderState provider)
    {
        var identity = export.Identity;
        if (provider.Placeholder)
        {
            if (!string.IsNullOrWhiteSpace(identity.RegistrationPlate)
                && !GlassRepairEstimateSessionPolicy.SameRegistration(identity.RegistrationPlate, provider.Registration))
            {
                throw new GlassMvaStageException(GlassFailure.IdentityRegistration);
            }
            if (identity.Mileage is { } mileage && mileage != 0 && mileage != provider.MileageMiles)
            {
                throw new GlassMvaStageException(GlassFailure.IdentityMileage);
            }
            provider.ReturnedTypeNumber = identity.TypeNumber;
        }
        else
        {
            if (!GlassRepairEstimateSessionPolicy.SameRegistration(identity.RegistrationPlate, provider.Registration))
            {
                throw new GlassMvaStageException(GlassFailure.IdentityRegistration);
            }
            if (identity.Mileage != provider.MileageMiles)
            {
                throw new GlassMvaStageException(GlassFailure.IdentityMileage);
            }
            if (!string.Equals(identity.TypeNumber, provider.NatCode, StringComparison.Ordinal))
            {
                throw new GlassMvaStageException(GlassFailure.IdentityNatCode);
            }
        }
        if (export.Estimate.Lines.Count == 0 || export.Estimate.SourceTotals?.Gross is not > 0m)
        {
            throw new GlassMvaStageException(GlassFailure.ExportEmpty);
        }
    }

    private async Task<Artifact> RetainAsync(
        ActionActor actor,
        GlassRepairEstimateSession session,
        string occurrenceIdentity,
        string operationKey,
        string fileName,
        string mediaType,
        byte[] content,
        string seriesIdentity,
        CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(content, writable: false);
        var retained = await custody.RetainAsync(
            new CaseArtifactCustodyRequest(
                actor,
                session.CaseId,
                IntakeReceiptId: null,
                occurrenceIdentity,
                operationKey,
                fileName,
                mediaType,
                content.LongLength,
                Convert.ToHexStringLower(SHA256.HashData(content)),
                stream,
                SemanticRole: DocumentSemanticRole.CeEstimate,
                SeriesIdentity: seriesIdentity),
            cancellationToken);
        return Artifact.From(retained, fileName, mediaType, content.LongLength);
    }

    /// <summary>
    /// Asks custody what actually became of a retention whose answer was lost.
    /// Nothing is offered again: the artifact already has its identities, so
    /// this reads them.
    /// </summary>
    private async Task<Artifact?> ResolveAsync(
        ActionActor actor, Guid caseId, Artifact? artifact, CancellationToken cancellationToken)
    {
        if (artifact is null
            || Confirmed(artifact)
            || artifact.DocumentId is not { } documentId
            || artifact.VersionId is not { } versionId
            || artifact.OccurrenceId is not { } occurrenceId)
        {
            return artifact;
        }

        var status = await custodyStatus.GetAsync(
            actor, caseId, documentId, versionId, occurrenceId, cancellationToken);
        return status.Disposition == CaseArtifactCustodyDisposition.Confirmed
            ? Artifact.From(status, artifact.FileName, artifact.MediaType, artifact.ContentLength ?? 0)
            : artifact;
    }

    private GlassMvaClient NewClient(IDictionary<string, string> cookies) =>
        new(httpClientFactory.CreateClient(GlassRepairEstimateOptions.HttpClientName),
            options,
            cookies,
            timeProvider);

    /// <summary>
    /// An editor client. One that continues an editor a previous call
    /// opened takes that call's last request time, so the pace between
    /// requests holds across clients (design 07 section 6).
    /// </summary>
    private GlassEditorClient NewEditorClient(DateTimeOffset? lastRequestAtUtc = null) =>
        new(httpClientFactory.CreateClient(GlassRepairEstimateOptions.HttpClientName),
            options,
            timeProvider,
            options.EditorPause)
        {
            LastRequestAtUtc = lastRequestAtUtc,
        };

    /// <summary>
    /// The one seam a session's Glass's login is read through (design 07
    /// section 1): a member of staff's own enabled credential, or, for the
    /// Automation, the login the deployment holds for it. Pegasus never reads
    /// a staff member's stored login for the Automation. With a session, the
    /// login must be the one it was launched with: the credential generation
    /// for staff, the account key for the Automation, whose deployment login
    /// has no generation.
    /// </summary>
    private async Task<GlassLogin> AccountForAsync(
        ActionActor actor, GlassRepairEstimateSession? session, CancellationToken cancellationToken)
    {
        GlassLogin login;
        if (actor.Kind == ActorKind.Automation)
        {
            var account = automationAccount.Read();
            login = new(Guid.Empty, account.Username, account.Password, account.AccountKey, Generation: 0);
        }
        else
        {
            var credential = await credentials.GetEnabledAsync(
                actor, ExternalCredentialProvider.GlassRepairEstimate, cancellationToken)
                ?? throw new GlassRepairEstimateRefusalException(
                    "The signed-in staff member has no enabled Glass's account, so no estimate can be started.");
            login = new(
                credential.Reference.PegasusUserId,
                credential.Username,
                credential.Password,
                credential.Reference.NormalizedExternalAccountKey,
                credential.Reference.CredentialGeneration);
        }

        if (session is null)
        {
            return login;
        }

        var launchedWith = session.OwnerKind == ActorKind.Automation
            ? string.Equals(login.AccountKey, session.NormalizedExternalAccountKey, StringComparison.Ordinal)
            : login.Generation == session.CredentialGeneration;
        return launchedWith
            ? login
            : throw new GlassRepairEstimateRefusalException(
                "This Glass's session requires the account credentials it was launched with.");
    }

    /// <summary>
    /// Whether the actor still holds the login the session was launched with;
    /// a credential replaced or turned off, or a deployment login changed,
    /// does not.
    /// </summary>
    private async Task<bool> HoldsLaunchAccountAsync(
        ActionActor actor, GlassRepairEstimateSession session, CancellationToken cancellationToken)
    {
        try
        {
            await AccountForAsync(actor, session, cancellationToken);
            return true;
        }
        catch (GlassRepairEstimateRefusalException)
        {
            return false;
        }
    }

    private static void RequireStaff(ActionActor actor)
    {
        GlassRepairEstimateSessionPolicy.RequireSessionActor(actor);
        if (actor.Kind != ActorKind.Staff)
        {
            throw new InvalidOperationException("The Automation starts its own Glass's sessions with its own start.");
        }
    }

    private static void RequireAutomation(ActionActor actor)
    {
        GlassRepairEstimateSessionPolicy.RequireSessionActor(actor);
        if (actor.Kind != ActorKind.Automation)
        {
            throw new InvalidOperationException("Only the Automation acts on its own Glass's session here.");
        }
    }

    private async Task<GlassRepairEstimateSessionMaterial> RequireSessionAsync(
        Guid sessionId, long expectedVersion, CancellationToken cancellationToken)
    {
        var material = await store.GetAsync(sessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"There is no Glass's session {sessionId}.");
        return material.Session.Version == expectedVersion
            ? material
            : throw Conflict(
                GlassRepairEstimateSessionConflict.Version,
                sessionId,
                $"The Glass's session is at version {material.Session.Version} and not {expectedVersion}.");
    }

    /// <summary>
    /// Proves the callback names a session Pegasus is waiting for. An unknown
    /// session and a token that does not fingerprint to the recorded digest are
    /// one refusal, so a caller learns nothing by trying either.
    /// </summary>
    private static GlassRepairEstimateSessionMaterial RequireCorrelated(
        GlassRepairEstimateCallback callback, GlassRepairEstimateSessionMaterial? material)
    {
        if (material is null
            || string.IsNullOrWhiteSpace(callback.Correlation)
            || !CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(callback.Correlation)),
                Convert.FromHexString(material.CallbackDigest)))
        {
            throw Conflict(
                GlassRepairEstimateSessionConflict.Callback,
                callback.SessionId,
                "The callback does not name a Glass's session this Pegasus is waiting for.");
        }

        return material.Session.Version == callback.ExpectedVersion
            ? material
            : throw Conflict(
                GlassRepairEstimateSessionConflict.Version,
                callback.SessionId,
                $"The Glass's session is at version {material.Session.Version} and not {callback.ExpectedVersion}.");
    }

    /// <summary>
    /// Records where a session stopped and says so once in the host log,
    /// with the stage's own numbers and flags: the log is the only place
    /// the provider's answer can be read after the fact, and the code alone
    /// cannot tell a provider that refused from one that answered nothing.
    /// </summary>
    private Task<GlassRepairEstimateSession> SettleAsync(
        GlassRepairEstimateSession session,
        GlassMvaStageException failure,
        ProviderState provider,
        string callbackDigest,
        Results? results,
        CancellationToken cancellationToken)
    {
        var state = failure.OutcomeUnknown
            ? GlassRepairEstimateSessionState.Unknown
            : GlassRepairEstimateSessionState.Failed;
        LogSettled(logger, session.Id, session.CaseId, state, failure.FailureCode, failure.Detail ?? string.Empty);
        return WriteAsync(
            session,
            state,
            failure.FailureCode,
            provider,
            callbackDigest,
            results,
            cancellationToken);
    }

    private Task StageAsync(GlassRepairEstimateSession session, string stage, Func<Task> action) =>
        Positions.StageAsync(session, stage, action);

    private Task<T> StageAsync<T>(GlassRepairEstimateSession session, string stage, Func<Task<T>> action) =>
        Positions.StageAsync(session, stage, action);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Glass's session {SessionId}/{Version} case {CaseId} state {State} code {FailureCode}")]
    private static partial void LogState(ILogger logger, Guid sessionId, Guid caseId, long version,
        GlassRepairEstimateSessionState state, string? failureCode);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Glass's session {SessionId} for case {CaseId} could not retain an export in custody")]
    private static partial void LogCustodyFailed(
        ILogger logger, Guid sessionId, Guid caseId, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Glass's session {SessionId} for case {CaseId} holds its estimate for landing: {Reason} {Detail}")]
    private static partial void LogImportHeld(
        ILogger logger, Guid sessionId, Guid caseId, string reason, string detail);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Glass's session {SessionId} for case {CaseId} settled {State} at {FailureCode} {Detail}")]
    private static partial void LogSettled(
        ILogger logger, Guid sessionId, Guid caseId, GlassRepairEstimateSessionState state, string failureCode, string detail);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Glass's session {SessionId} for case {CaseId} build line {Position} {Outcome} at {ReasonCode} {Detail}")]
    private static partial void LogBuildAct(
        ILogger logger, Guid sessionId, Guid caseId, int position, GlassEstimateBuildOutcome outcome,
        string reasonCode, string detail);


    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Glass's session {SessionId} for case {CaseId} launches on a placeholder vehicle after lookup {Detail}")]
    private static partial void LogPlaceholderLaunch(ILogger logger, Guid sessionId, Guid caseId, string detail);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Glass's session {SessionId} for case {CaseId} returned a placeholder estimate identified as type number {TypeNumber} with plate {Plate} and mileage {Mileage}")]
    private static partial void LogPlaceholderReturn(
        ILogger logger, Guid sessionId, Guid caseId, string typeNumber, string plate, string mileage);

    private async Task<GlassRepairEstimateSession> WriteAsync(
        GlassRepairEstimateSession session,
        GlassRepairEstimateSessionState state,
        string? failureCode,
        ProviderState provider,
        string callbackDigest,
        Results? results,
        CancellationToken cancellationToken)
    {
        var next = session with { State = state, FailureCode = failureCode };
        await store.SaveAsync(
            new(next, Protect(provider), callbackDigest, Serialize(results)),
            session.Version,
            cancellationToken);
        LogState(logger, session.Id, session.CaseId, session.Version + 1, state, failureCode);
        return next with { Version = session.Version + 1 };
    }

    /// <summary>
    /// Every estimate id this session has been launched under, and the only
    /// ones a reopen or a return may name: a reopen's answer outside this set
    /// is refused (<see cref="GlassFailure.StartEreId"/>), so the set only
    /// grows when a launch's own answer is recorded and none is forgotten.
    /// </summary>
    private static HashSet<string> EstimateIdsOf(ProviderState provider, string current) =>
        new(provider.EstimateIds, StringComparer.Ordinal) { current };

    private static void Record(ProviderState provider, GlassEstimateLaunch launch)
    {
        if (provider.EreId is { } previous && !provider.EstimateIds.Contains(previous))
        {
            provider.EstimateIds.Add(previous);
        }
        if (!provider.EstimateIds.Contains(launch.EreId))
        {
            provider.EstimateIds.Add(launch.EreId);
        }
        provider.EreId = launch.EreId;
        provider.OriginalCallback = launch.OriginalCallback.AbsoluteUri;
        provider.EstimatorUrl = launch.EstimatorUrl.AbsoluteUri;
    }

    /// <summary>The Pegasus callback a launch already minted, read back from its estimator URL.</summary>
    private static Uri PegasusCallbackOf(string estimatorUrl) =>
        Query(new Uri(estimatorUrl, UriKind.Absolute).Query, "caller") is { } caller
            ? new Uri(caller, UriKind.Absolute)
            : throw new InvalidOperationException("The retained Glass's launch URL names no callback.");

    /// <summary>
    /// A single parameter of the provider's raw callback query. The query is
    /// never re-encoded for the relay; this reads it only to decide what the
    /// operator did.
    /// </summary>
    private static string? Query(string rawQuery, string name)
    {
        foreach (var part in rawQuery.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0 && Uri.UnescapeDataString(part[..separator]) == name)
            {
                return Uri.UnescapeDataString(part[(separator + 1)..]);
            }
        }

        return null;
    }

    private static bool Confirmed(Artifact artifact) =>
        artifact.Status == nameof(CaseArtifactCustodyDisposition.Confirmed)
        && artifact is { DocumentId: not null, VersionId: not null, Sha256: not null };

    private static bool Failed(Artifact? artifact) =>
        artifact?.Status == nameof(CaseArtifactCustodyDisposition.Failed);

    private static bool IsTransportFailure(Exception exception, CancellationToken cancellationToken) =>
        GlassEditorPositionWork.IsTransportFailure(exception, cancellationToken);

    /// <summary>
    /// A stage's own refusal as it was thrown; a transport failure after the
    /// provider may have acted becomes the outcome-unknown transport code.
    /// </summary>
    private static GlassMvaStageException AsFailure(Exception exception) =>
        GlassEditorPositionWork.AsFailure(exception);

    private static GlassRepairEstimateSessionConflictException Conflict(
        GlassRepairEstimateSessionConflict conflict, Guid sessionId, string message) =>
        new(conflict, sessionId, message);

    private static string Sha256Hex(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private string Protect(ProviderState provider) =>
        dataProtection.CreateProtector(ProtectionPurpose)
            .Protect(JsonSerializer.Serialize(provider, Json));

    private ProviderState Unprotect(string protectedState)
    {
        string plain;
        try
        {
            plain = dataProtection.CreateProtector(ProtectionPurpose).Unprotect(protectedState);
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException(
                "The retained Glass's session state could not be read, so the session stays for reconciliation.");
        }

        return JsonSerializer.Deserialize<ProviderState>(plain, Json)
            ?? throw new InvalidOperationException("The retained Glass's session state is empty.");
    }

    private static string? Serialize(Results? results) =>
        results is null ? null : JsonSerializer.Serialize(results, Json);

    private static Results Deserialize(string? json) =>
        string.IsNullOrWhiteSpace(json) ? new() : JsonSerializer.Deserialize<Results>(json, Json) ?? new();

    /// <summary>
    /// The whole of a session's provider material. Protected at rest and never
    /// projected: it carries the cookie jar that authenticates the session, the
    /// <c>ere_session</c> the relay is made with, and the Case edit lease the
    /// launch was authorised by.
    /// </summary>
    private sealed class ProviderState
    {
        public Dictionary<string, string> Cookies { get; init; } = new(StringComparer.Ordinal);

        public string Registration { get; set; } = string.Empty;

        public long MileageMiles { get; set; }

        public long CaseVersion { get; set; }

        public string LeaseToken { get; set; } = string.Empty;

        public string? NatCode { get; set; }

        /// <summary>
        /// The launch's lookup found no vehicle for the registration, so the
        /// estimate was started on an unqualified placeholder vehicle and the
        /// Engineer identified the real one inside the estimator. Absent from
        /// state protected before it existed, which reads as false.
        /// </summary>
        public bool Placeholder { get; set; }

        /// <summary>
        /// The type number a placeholder session's export named: the
        /// Engineer's choice inside the estimator. Kept apart from
        /// <see cref="NatCode"/>, which stays the placeholder's own so the
        /// stock vehicle can be re-proved by it.
        /// </summary>
        public string? ReturnedTypeNumber { get; set; }

        public string? PegasusCallback { get; set; }

        public bool EstimateStartAttempted { get; set; }

        /// <summary>
        /// The session reopens the estimate a repair spec belongs to, so its
        /// vehicle and estimate were recorded before it began and it makes
        /// neither. Absent from state protected before it existed, which reads
        /// as false.
        /// </summary>
        public bool Linked { get; set; }

        public string? MvaVehicleId { get; set; }

        public string? EreId { get; set; }

        /// <summary>
        /// Every estimate id a launch or a resume of this session recorded, in
        /// order. Absent from state protected before it existed, which reads as
        /// empty; <see cref="EreId"/> alone then names the estimate.
        /// </summary>
        public List<string> EstimateIds { get; init; } = [];

        /// <summary>
        /// The provider's own callback, whole. It names the estimate and the
        /// provider session the relay is made under, so keeping it is what lets
        /// a completion relay to exactly the address Glass's issued instead of
        /// rebuilding one from its parts.
        /// </summary>
        public string? OriginalCallback { get; set; }

        public string? EstimatorUrl { get; set; }

        /// <summary>
        /// The provider's return exactly as it was accepted, kept so the relay
        /// can be made by whichever work runs it, after a restart included.
        /// Its fingerprint is <see cref="Results.CallbackQueryDigest"/>.
        /// </summary>
        public string? ReturnQuery { get; set; }

        /// <summary>
        /// The Automation's open editor, kept between its calls (design 07
        /// section 2): the editor's address, <c>url0</c>, session key and
        /// cookies. Absent from state protected before it existed, and from
        /// every staff session, which reads as none.
        /// </summary>
        public EditorState? Editor { get; set; }

        /// <summary>When the last request to the editor was sent, so the next call keeps the pace.</summary>
        public DateTimeOffset? EditorLastRequestAtUtc { get; set; }
    }

    /// <summary>
    /// An open editor as the protected state keeps it. Its own shape rather
    /// than <see cref="GlassEditorSession"/>, so what is stored does not
    /// depend on how that record serializes.
    /// </summary>
    private sealed class EditorState
    {
        public string BaseAddress { get; set; } = string.Empty;

        public string Url0 { get; set; } = string.Empty;

        public string Key { get; set; } = string.Empty;

        public Dictionary<string, string> Cookies { get; init; } = new(StringComparer.Ordinal);

        public static EditorState Of(GlassEditorSession session) => new()
        {
            BaseAddress = session.BaseAddress,
            Url0 = session.Url0,
            Key = session.Key,
            Cookies = new Dictionary<string, string>(session.Cookies, StringComparer.Ordinal),
        };

        public GlassEditorSession Session() =>
            new(BaseAddress, Url0, Key, new Dictionary<string, string>(Cookies, StringComparer.Ordinal).AsReadOnly());
    }

    /// <summary>
    /// The Glass's login a session signs in with, read through
    /// <see cref="AccountForAsync"/>: the member of staff it belongs to
    /// (none for the Automation), the username and password, the account key
    /// and the credential generation (none for the Automation). Prints nothing.
    /// </summary>
    private sealed record GlassLogin(Guid UserId, string Username, string Password, string AccountKey, long Generation)
    {
        public override string ToString() => nameof(GlassLogin);
    }

    /// <summary>
    /// What a completed session produced, as the session's own
    /// <c>ResultArtifactsJson</c>. It holds no content and no secret: the
    /// fingerprint of the callback that was acted on, each retained artifact's
    /// custody identities, and the Draft the import landed.
    /// </summary>
    private sealed class Results
    {
        public string? CallbackQueryDigest { get; set; }

        /// <summary>
        /// Recorded in the claim that precedes the relay. Once set the relay is
        /// never made again; a lost answer is looked up instead.
        /// </summary>
        public bool RelayStarted { get; set; }

        public Artifact? Xml { get; set; }

        public Artifact? Pdf { get; set; }

        /// <summary>
        /// The export Pegasus's reader refused, kept on the Case as a rejected
        /// Glass's export. It is never a Draft's source.
        /// </summary>
        public Artifact? RejectedXml { get; set; }

        public Guid? ImportedEstimateId { get; set; }
    }

    private sealed class Artifact
    {
        public string Status { get; set; } = nameof(CaseArtifactCustodyDisposition.Unknown);

        public string FileName { get; set; } = string.Empty;

        public string MediaType { get; set; } = string.Empty;

        public Guid? DocumentId { get; set; }

        public Guid? VersionId { get; set; }

        /// <summary>The Case occurrence custody minted or reused for it (G23).</summary>
        public Guid? OccurrenceId { get; set; }

        public string? Sha256 { get; set; }

        public long? ContentLength { get; set; }

        public static Artifact From(
            CaseArtifactCustodyResult result, string fileName, string mediaType, long contentLength) =>
            new()
            {
                Status = result.Disposition.ToString(),
                FileName = fileName,
                MediaType = result.MediaType ?? mediaType,
                DocumentId = result.DocumentId,
                VersionId = result.VersionId,
                OccurrenceId = result.OccurrenceId,
                Sha256 = result.Sha256,
                ContentLength = result.ContentLength ?? contentLength,
            };
    }
}
