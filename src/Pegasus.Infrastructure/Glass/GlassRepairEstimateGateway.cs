using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Assessment;
using Pegasus.Core.Custody;
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

    /// <summary>How long a just-created vehicle waits before its identity is read once more.</summary>
    private static readonly TimeSpan VehicleReread = TimeSpan.FromMilliseconds(500);

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
        RepairSpecificationPolicy.RequireStaffAuthor(request.Actor);

        var facts = await caseAuthority.RequireEditAuthorityAsync(
            request.Actor, request.CaseId, request.ExpectedCaseVersion, request.LeaseToken, cancellationToken);
        var credential = await RequireCredentialAsync(request.Actor, cancellationToken);
        // The account holds one live session. Asking first makes the ordinary
        // refusal a read that names the session in the way; the store's index
        // still decides a genuine race. A replay of the same operation key is
        // not a second launch and is answered by the store as before.
        if (await store.FindLiveForAccountAsync(
                credential.Reference.NormalizedExternalAccountKey, cancellationToken) is { } live
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
            Registration = facts.Registration,
            MileageMiles = facts.MileageMiles,
            CaseVersion = request.ExpectedCaseVersion,
            LeaseToken = request.LeaseToken,
            PegasusCallback = options.CallbackFor(correlation).AbsoluteUri,
        };
        var prepared = new GlassRepairEstimateSession(
            request.SessionId,
            request.CaseId,
            credential.Reference.PegasusUserId,
            credential.Reference.CredentialGeneration,
            // The canonical account key is minted by the credential store and
            // travels to the session store unchanged; the account name itself
            // is never recorded on the session.
            credential.Reference.NormalizedExternalAccountKey,
            GlassRepairEstimateSessionState.Prepared,
            Version: 0,
            request.OperationKey.Trim(),
            now,
            now + options.SessionLifetime,
            ProviderVehicleId: null,
            ProviderEstimateId: null,
            FailureCode: null);

        var creation = await store.CreateAsync(
            new(prepared, Protect(provider), digest, null), cancellationToken);
        // The store says whether this call created the session. A replay of the
        // same operation key names the launch that already happened, whatever
        // id it carries: running the provider stages again would start a second
        // estimate for one operator action.
        return new(
            creation.Material.Session,
            creation.Created
                ? GlassRepairEstimateContinuation.Launch
                : GlassRepairEstimateContinuation.None);
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
        RepairSpecificationPolicy.RequireStaffAuthor(request.Actor);
        var material = await store.GetAsync(request.SessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"There is no Glass's session {request.SessionId}.");
        var session = material.Session;
        RequireOwner(request.Actor, session);
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

        var provider = Unprotect(material.ProtectedProviderState);
        var credential = await RequireLaunchCredentialAsync(request.Actor, session, cancellationToken);
        if (IsUncertain(session, provider))
        {
            return await WriteAsync(session, GlassRepairEstimateSessionState.Unknown,
                GlassFailure.TransportUnknown, provider, material.CallbackDigest, results, cancellationToken);
        }

        return provider.EreId is null && provider.PegasusCallback is not null
            ? await LaunchStagesAsync(session, provider, credential, material.CallbackDigest, cancellationToken)
            : await ReopenAsync(session, provider, credential, material.CallbackDigest, results, cancellationToken);
    }

    private async Task<GlassRepairEstimateSession> LaunchStagesAsync(
        GlassRepairEstimateSession session, ProviderState provider,
        PerUserExternalCredentialMaterial credential, string digest, CancellationToken cancellationToken)
    {
        // Claim this version before any external work, including a resumed
        // Prepared session. A concurrent Resume cannot also create a vehicle.
        session = await WriteAsync(session, session.State, null, provider, digest, null, cancellationToken);
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
                provider.NatCode = await StageAsync(session, "RequirePlaceholder", () => client.RequirePlaceholderAsync(
                    provider.MvaVehicleId, provider.NatCode, estimateStarted: false, cancellationToken));
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
            return await WriteAsync(session with { ProviderEstimateId = launch.EreId },
                GlassRepairEstimateSessionState.Active, null, provider, digest, null, CancellationToken.None);
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
    /// Reopens a live estimate at the provider: fresh cookies, the grid
    /// selection re-asserted because it is server-side session state, and the
    /// calculation restarted as the portal restarts one — <c>ere_id</c> 0 on
    /// the vehicle — under the callback this session already minted. Only an
    /// answer naming one of the session's own estimates is opened.
    /// </summary>
    private async Task<GlassRepairEstimateSession> ReopenAsync(
        GlassRepairEstimateSession session, ProviderState provider,
        PerUserExternalCredentialMaterial credential, string digest, Results results,
        CancellationToken cancellationToken)
    {
        var (vehicleId, ereId, estimatorUrl) = RequireReopenable(provider);
        // The callback this session accepts never changes — the store refuses a
        // write that carries a different one — so the resumed launch reuses the
        // address the first one minted rather than trying to mint a second.
        var callback = PegasusCallbackOf(estimatorUrl);
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
            await WriteAsync(session, GlassRepairEstimateSessionState.Unknown,
                GlassFailure.Interrupted, provider, digest, results, CancellationToken.None);
            throw;
        }
        catch (Exception failure)
            when (failure is GlassMvaStageException || IsTransportFailure(failure, cancellationToken))
        {
            return await SettleUnknownAsync(
                session, AsFailure(failure), provider, digest, results, CancellationToken.None);
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
        RepairSpecificationPolicy.RequireStaffAuthor(actor);
        var material = await store.GetAsync(sessionId, cancellationToken);
        if (material is null)
        {
            return null;
        }

        RequireOwner(actor, material.Session);
        var provider = Unprotect(material.ProtectedProviderState);
        return material.Session.State == GlassRepairEstimateSessionState.Active
            && material.Session.ExpiresAtUtc > timeProvider.GetUtcNow()
            && provider.EstimatorUrl is { } url
                ? new Uri(url, UriKind.Absolute)
                : null;
    }

    /// <summary>
    /// A resume's prepare half. Current Case authority, unchanged vehicle
    /// facts and the launching credential are proved before anything is
    /// written, and the proved authority replaces the protected import
    /// authority. A session waiting to be imported is claimed for the import;
    /// a live one is recorded for its launch to continue; one whose outcome
    /// cannot be known, or whose lifetime has passed, is settled here and owes
    /// nothing more.
    /// </summary>
    public async Task<GlassRepairEstimateStep> PrepareResumeAsync(
        GlassRepairEstimateResumeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RepairSpecificationPolicy.RequireStaffAuthor(request.Actor);
        var material = await RequireSessionAsync(request.SessionId, request.ExpectedVersion, cancellationToken);
        var session = material.Session;
        RequireOwner(request.Actor, session);
        var provider = Unprotect(material.ProtectedProviderState);
        var results = Deserialize(material.ResultArtifactsJson);

        // A session Failed because the reader refused the export is taken up
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
        GlassRepairEstimateSessionPolicy.RequireUnchangedVehicle(
            provider.Registration, provider.MileageMiles, facts.Registration, facts.MileageMiles);
        await RequireLaunchCredentialAsync(request.Actor, session, cancellationToken);
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
        // was recorded. Neither a retry nor local expiry can prove it closed.
        if (IsUncertain(session, provider))
        {
            return new(
                await WriteAsync(session, GlassRepairEstimateSessionState.Unknown,
                    GlassFailure.TransportUnknown, provider, material.CallbackDigest, results, cancellationToken),
                GlassRepairEstimateContinuation.None);
        }
        if (session.ExpiresAtUtc <= timeProvider.GetUtcNow())
        {
            // The provider's side of an open calculation has lapsed; it is
            // settled here rather than re-opened for work the callback would
            // refuse. A claimed result above is read, not re-opened, so it is
            // not subject to this.
            return new(
                await ExpireAsync(session, provider, material.CallbackDigest, results, cancellationToken),
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
    /// the delivery. Everything after the claim that needs no provider —
    /// expiry, a replaced credential, a calculation that was not saved — is
    /// settled here; a saved one owes its import.
    /// </summary>
    public async Task<GlassRepairEstimateStep> AcceptCallbackAsync(
        GlassRepairEstimateCallback callback, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(callback.RawQuery);
        RepairSpecificationPolicy.RequireStaffAuthor(callback.Actor);
        var material = await RequireCorrelatedAsync(callback, cancellationToken);
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
        RequireOwner(callback.Actor, session);
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

        if (session.ExpiresAtUtc <= timeProvider.GetUtcNow())
        {
            return new(
                await ExpireAsync(session, provider, material.CallbackDigest, results, cancellationToken),
                GlassRepairEstimateContinuation.None);
        }

        var credential = await credentials.GetEnabledAsync(
            callback.Actor, ExternalCredentialProvider.GlassRepairEstimate, cancellationToken);
        if (credential is null || credential.Reference.CredentialGeneration != session.CredentialGeneration)
        {
            // The credential that launched this has been replaced or turned
            // off; the session it opened is no longer this staff member's to finish.
            return new(
                await ExpireAsync(session, provider, material.CallbackDigest, results, cancellationToken),
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
        RepairSpecificationPolicy.RequireStaffAuthor(request.Actor);
        var material = await store.GetAsync(request.SessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"There is no Glass's session {request.SessionId}.");
        var session = material.Session;
        RequireOwner(request.Actor, session);
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

        var credential = await RequireLaunchCredentialAsync(actor, session, cancellationToken);
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
        RepairSpecificationPolicy.RequireStaffAuthor(actor);
        var material = await store.GetAsync(sessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"There is no Glass's session {sessionId}.");
        var session = material.Session;
        RequireOwner(actor, session);
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
    /// A session past Prepared whose provider identities were not all recorded:
    /// the provider may have acted, and neither a retry nor local expiry can
    /// prove it did not.
    /// </summary>
    private static bool IsUncertain(GlassRepairEstimateSession session, ProviderState provider) =>
        session.State != GlassRepairEstimateSessionState.Prepared
        && (provider.MvaVehicleId is null
            || (provider.EstimateStartAttempted && provider.EreId is null));

    private static (string VehicleId, string EreId, string EstimatorUrl) RequireReopenable(ProviderState provider) =>
        provider is { MvaVehicleId: { } vehicleId, EreId: { } ereId, EstimatorUrl: { } estimatorUrl }
            ? (vehicleId, ereId, estimatorUrl)
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
                    Name: string.Empty),
                cancellationToken));
            results.ImportedEstimateId = imported.EstimateId;
        }
        catch (Exception stale)
            when (stale is CaseVersionConflictException
                or CaseEditLeaseExpiredException
                or CaseEditLeaseConflictException)
        {
            // The Case moved on while the operator was in Glass's. Everything
            // the provider produced is already retained; the estimate lands
            // when the staff member takes the Case back.
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
                stream),
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

    private async Task<PerUserExternalCredentialMaterial> RequireCredentialAsync(
        ActionActor actor, CancellationToken cancellationToken) =>
        await credentials.GetEnabledAsync(
            actor, ExternalCredentialProvider.GlassRepairEstimate, cancellationToken)
        ?? throw new GlassRepairEstimateRefusalException(
            "The signed-in staff member has no enabled Glass's account, so no estimate can be started.");

    /// <summary>The staff member's enabled credential, and only the generation the session was launched with.</summary>
    private async Task<PerUserExternalCredentialMaterial> RequireLaunchCredentialAsync(
        ActionActor actor, GlassRepairEstimateSession session, CancellationToken cancellationToken)
    {
        var credential = await RequireCredentialAsync(actor, cancellationToken);
        return credential.Reference.CredentialGeneration == session.CredentialGeneration
            ? credential
            : throw new GlassRepairEstimateRefusalException(
                "This Glass's session requires the account credentials it was launched with.");
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
    private async Task<GlassRepairEstimateSessionMaterial> RequireCorrelatedAsync(
        GlassRepairEstimateCallback callback, CancellationToken cancellationToken)
    {
        var material = await store.GetAsync(callback.SessionId, cancellationToken);
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

    private static void RequireOwner(ActionActor actor, GlassRepairEstimateSession session)
    {
        if (actor.Kind != ActorKind.Staff
            || !Guid.TryParse(actor.SubjectId, out var staffId)
            || staffId != session.PegasusUserId)
        {
            throw Conflict(
                GlassRepairEstimateSessionConflict.Callback,
                session.Id,
                "This Glass's session belongs to another staff member.");
        }
    }

    private Task<GlassRepairEstimateSession> ExpireAsync(
        GlassRepairEstimateSession session,
        ProviderState provider,
        string callbackDigest,
        Results results,
        CancellationToken cancellationToken) =>
        session.State == GlassRepairEstimateSessionState.Unknown ? Task.FromResult(session) : WriteAsync(
            session,
            GlassRepairEstimateSessionState.Expired,
            GlassFailure.CallbackExpired,
            provider,
            callbackDigest,
            results,
            cancellationToken);

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

    private async Task StageAsync(GlassRepairEstimateSession session, string stage, Func<Task> action)
    {
        await StageAsync(session, stage, async () => { await action(); return true; });
    }

    private async Task<T> StageAsync<T>(GlassRepairEstimateSession session, string stage, Func<Task<T>> action)
    {
        var started = Stopwatch.GetTimestamp();
        var outcome = "Succeeded";
        try { return await action(); }
        catch (Exception failure)
        {
            outcome = failure is GlassMvaStageException providerFailure
                ? providerFailure.FailureCode : failure is OperationCanceledException ? "Cancelled" : "Failed";
            throw;
        }
        finally
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                LogStage(logger, session.Id, session.CaseId, session.Version, stage, outcome, elapsedMs);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Glass's attempt {SessionId}/{Version} case {CaseId} stage {Stage} outcome {Outcome} in {ElapsedMs} ms")]
    private static partial void LogStage(ILogger logger, Guid sessionId, Guid caseId, long version,
        string stage, string outcome, double elapsedMs);

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
        Message = "Glass's session {SessionId} for case {CaseId} settled {State} at {FailureCode} {Detail}")]
    private static partial void LogSettled(
        ILogger logger, Guid sessionId, Guid caseId, GlassRepairEstimateSessionState state, string failureCode, string detail);

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
        !cancellationToken.IsCancellationRequested
        && exception is HttpRequestException or TaskCanceledException or TimeoutException;

    /// <summary>
    /// A stage's own refusal as it was thrown; a transport failure after the
    /// provider may have acted becomes the outcome-unknown transport code.
    /// </summary>
    private static GlassMvaStageException AsFailure(Exception exception) =>
        exception as GlassMvaStageException
            ?? new(GlassFailure.TransportUnknown, outcomeUnknown: true);

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

        public string? BoxFileId { get; set; }

        public string? BoxVersionId { get; set; }

        public string? FailureCode { get; set; }

        public string? PendingContentStorageKey { get; set; }

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
                BoxFileId = result.BoxFileId,
                BoxVersionId = result.BoxVersionId,
                FailureCode = result.FailureCode,
                PendingContentStorageKey = result.PendingContentStorageKey,
            };
    }
}
