using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// Runs one Glass's Repair Estimate session for a Case: launch, resume, and the
/// operator's Save &amp; Exit through to a source-labelled Draft (CASE-047 B04).
///
/// <para>
/// <b>Identities are persisted before side effects.</b> The session exists,
/// with its account and its one-use callback fingerprint, before a single
/// request reaches Glass's; it moves to
/// <see cref="GlassRepairEstimateSessionState.Launching"/> before the stage that
/// first creates state inside the provider's account, and records the vehicle
/// and estimate it created as soon as they are known. Nothing here can leave an
/// estimate open at Glass's that Pegasus has no record of.
/// </para>
///
/// <para>
/// <b>A provider failure is data, not an exception.</b> Every outcome the
/// provider can produce lands on the session as a state and a failure code, so
/// the operator sees where it stopped. An outcome Pegasus cannot determine —
/// a lost answer to vehicle creation or to starting the estimate — is
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
/// stands, a different one is refused. What is lost after the claim stays
/// <see cref="GlassRepairEstimateSessionState.Unknown"/> on the record, and a
/// later resume looks the export up again rather than relaying again.
/// </para>
///
/// <para>
/// <b>What is protected.</b> The session's cookie jar, the prepared estimate
/// (<c>MvaVehicleId</c>, <c>NatCode</c>, <c>EreId</c>, the provider's own
/// callback — which carries its <c>ere_session</c> — and the rewritten
/// estimator URL) and the launch's own Case
/// authority are one protected blob at rest. The CSRF token is not: it is
/// single-use and belongs to one login. The <c>ere_session</c> never appears in
/// a log, an exception, a failure code or the session read model.
/// </para>
///
/// <para>
/// <b>The launch's Case authority is carried, not re-asked.</b> The Engineer
/// proved version and lease when they launched; the callback arrives on an
/// anonymous page minutes later and has no lease of its own. Replaying the
/// launch's version and lease into the import is what makes the completion the
/// same authorised act — and when the Case has moved on since, the import
/// refuses, the artifacts stay retained, and the session waits in
/// <see cref="GlassRepairEstimateSessionState.AwaitingImport"/> for the Engineer
/// to regain edit authority.
/// </para>
/// </summary>
public sealed class GlassRepairEstimateGateway(
    IGlassRepairEstimateSessionStore store,
    IGlassRepairEstimateCaseAuthority caseAuthority,
    IPerUserExternalCredentialReader credentials,
    ICaseArtifactCustody custody,
    ICaseArtifactCustodyStatus custodyStatus,
    IImportRawEstimate import,
    IHttpClientFactory httpClientFactory,
    IDataProtectionProvider dataProtection,
    GlassRepairEstimateOptions options,
    TimeProvider timeProvider) : IGlassRepairEstimateGateway
{
    /// <summary>
    /// Versioned on purpose: changing it makes every session in flight
    /// unreadable rather than silently mis-read, which is the right failure for
    /// protected provider state.
    /// </summary>
    public const string ProtectionPurpose = "Pegasus.Glass.Session.v1";

    /// <summary>
    /// The fingerprint a launch records for the callback it will accept. The
    /// correlation token itself is never stored, so a caller holding one — the
    /// callback page, reading it out of its own route — finds the session it
    /// names through this and nothing else.
    /// </summary>
    public static string CallbackDigestOf(string correlation) => Sha256Hex(correlation);

    /// <summary>The XML export's custody occurrence on the Case.</summary>
    public static string XmlOccurrenceIdentity(Guid sessionId) => $"glass-estimate:{sessionId:D}:xml";

    /// <summary>The embedded calculation sheet's custody occurrence on the Case.</summary>
    public static string PdfOccurrenceIdentity(Guid sessionId) => $"glass-estimate:{sessionId:D}:pdf";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<GlassRepairEstimateSession> LaunchAsync(
        GlassRepairEstimateLaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        RepairSpecificationPolicy.RequireEngineer(request.Actor);

        var facts = await caseAuthority.RequireEditAuthorityAsync(
            request.Actor, request.CaseId, request.ExpectedCaseVersion, request.LeaseToken, cancellationToken);
        var credential = await RequireCredentialAsync(request.Actor, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var correlation = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var digest = Sha256Hex(correlation);
        var provider = new ProviderState
        {
            Registration = facts.Registration,
            MileageMiles = facts.MileageMiles,
            CaseVersion = request.ExpectedCaseVersion,
            LeaseToken = request.LeaseToken,
        };
        var prepared = new GlassRepairEstimateSession(
            Guid.NewGuid(),
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

        var created = await store.CreateAsync(
            new(prepared, Protect(provider), digest, null), cancellationToken);
        if (created.Session.Id != prepared.Id)
        {
            // The same operation key: this launch already happened and the
            // session it created is the answer. Running the provider stages
            // again would start a second estimate for one operator action.
            return created.Session;
        }

        var session = created.Session;
        var client = NewClient(provider.Cookies);
        var startedProviderState = false;
        try
        {
            await client.SignInAsync(credential.Username, credential.Password, cancellationToken);
            var lookup = await client.LookupAsync(facts.Registration, facts.MileageMiles, cancellationToken);
            provider.NatCode = lookup.NatCode;
            session = await WriteAsync(
                session, GlassRepairEstimateSessionState.Launching, null, provider, digest, null, cancellationToken);

            startedProviderState = true;
            var vehicleId = await client.CreateVehicleAsync(
                facts.Registration, facts.MileageMiles, cancellationToken);
            provider.MvaVehicleId = vehicleId;
            await client.RequireVehicleAsync(vehicleId, lookup.NatCode, cancellationToken);
            await client.SelectOnlyAsync(vehicleId, cancellationToken);
            var launch = await client.StartEstimateAsync(
                "0", options.CallbackFor(correlation), cancellationToken);
            Record(provider, launch);

            return await WriteAsync(
                session with { ProviderVehicleId = vehicleId, ProviderEstimateId = launch.EreId },
                GlassRepairEstimateSessionState.Active,
                null,
                provider,
                digest,
                null,
                cancellationToken);
        }
        catch (GlassMvaStageException failure)
        {
            return await SettleAsync(session, failure, provider, digest, null, cancellationToken);
        }
        catch (Exception transport) when (IsTransportFailure(transport, cancellationToken))
        {
            return await SettleAsync(
                session,
                new GlassMvaStageException(
                    startedProviderState ? GlassFailure.TransportUnknown : GlassFailure.TransportFailed,
                    startedProviderState),
                provider,
                digest,
                null,
                cancellationToken);
        }
    }

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
    /// Engineer who launched it.
    /// </remarks>
    public async Task<Uri?> GetEstimatorUrlAsync(
        ActionActor actor, Guid sessionId, CancellationToken cancellationToken)
    {
        RepairSpecificationPolicy.RequireEngineer(actor);
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
    /// Picks a session back up. A live session is re-opened at the provider —
    /// fresh cookies, the grid selection re-asserted because it is server-side
    /// session state, and the calculation restarted against its existing
    /// estimate id under the callback this session already minted. A session
    /// waiting to be imported does only the import, and only for a caller that
    /// carries the Case authority it needs.
    /// </summary>
    public async Task<GlassRepairEstimateSession> ResumeAsync(
        GlassRepairEstimateResumeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RepairSpecificationPolicy.RequireEngineer(request.Actor);
        var material = await RequireSessionAsync(request.SessionId, request.ExpectedVersion, cancellationToken);
        var session = material.Session;
        RequireOwner(request.Actor, session);
        var provider = Unprotect(material.ProtectedProviderState);
        var results = Deserialize(material.ResultArtifactsJson);

        if (session.State == GlassRepairEstimateSessionState.AwaitingImport)
        {
            if (request.ExpectedCaseVersion is not { } caseVersion
                || string.IsNullOrWhiteSpace(request.LeaseToken))
            {
                throw new InvalidOperationException(
                    "Importing a waiting Glass's session needs the Case version and edit lease the "
                    + "Engineer has regained; resume it through the request that carries them.");
            }

            provider.CaseVersion = caseVersion;
            provider.LeaseToken = request.LeaseToken;
            // A retention whose answer was lost already has its identities, so
            // this asks custody what became of it instead of offering the same
            // bytes a second time.
            results.Xml = await ResolveAsync(request.Actor, session.CaseId, results.Xml, cancellationToken);
            results.Pdf = await ResolveAsync(request.Actor, session.CaseId, results.Pdf, cancellationToken);
            return await FinishAsync(
                request.Actor, session, provider, material.CallbackDigest, results, cancellationToken);
        }

        if (session.State is GlassRepairEstimateSessionState.Importing or GlassRepairEstimateSessionState.Unknown
            && results.CallbackQueryDigest is not null)
        {
            // The operator's Save & Exit was claimed and its answer was lost: a
            // transport failure after the relay, or a host that stopped between
            // the claim and the record. The relay is never repeated; the export
            // it produced is looked up again, which is the one safe retry.
            if (provider.MvaVehicleId is not { } lookupVehicle || provider.EreId is not { } lookupEre)
            {
                throw new InvalidOperationException(
                    "The Glass's session has no vehicle or estimate to look up, so its outcome stays for reconciliation.");
            }

            var heldCredential = await RequireCredentialAsync(request.Actor, cancellationToken);
            if (heldCredential.Reference.CredentialGeneration != session.CredentialGeneration)
            {
                return await ExpireAsync(session, provider, material.CallbackDigest, results, cancellationToken);
            }
            if (request.ExpectedCaseVersion is { } regainedVersion && !string.IsNullOrWhiteSpace(request.LeaseToken))
            {
                provider.CaseVersion = regainedVersion;
                provider.LeaseToken = request.LeaseToken;
            }

            provider.Cookies.Clear();
            var lookup = NewClient(provider.Cookies);
            try
            {
                await lookup.SignInAsync(heldCredential.Username, heldCredential.Password, cancellationToken);
                await lookup.SelectOnlyAsync(lookupVehicle, cancellationToken);
            }
            catch (Exception failure)
                when (failure is GlassMvaStageException || IsTransportFailure(failure, cancellationToken))
            {
                return await SettleAsync(
                    session, AsFailure(failure), provider, material.CallbackDigest, results, cancellationToken);
            }

            return await ExportAsync(
                request.Actor, session, provider, material.CallbackDigest, results, lookup, lookupEre, cancellationToken);
        }

        if (session.State is not (GlassRepairEstimateSessionState.Active
            or GlassRepairEstimateSessionState.Unknown))
        {
            throw new InvalidOperationException(
                $"A Glass's session in {session.State} cannot be resumed.");
        }
        if (session.ExpiresAtUtc <= timeProvider.GetUtcNow())
        {
            // The provider's side of an open calculation has lapsed; it is
            // settled here rather than re-opened for work the callback would
            // refuse. A claimed result above is read, not re-opened, so it is
            // not subject to this.
            return await ExpireAsync(session, provider, material.CallbackDigest, results, cancellationToken);
        }

        var credential = await RequireCredentialAsync(request.Actor, cancellationToken);
        if (credential.Reference.CredentialGeneration != session.CredentialGeneration)
        {
            return await ExpireAsync(session, provider, material.CallbackDigest, results, cancellationToken);
        }
        if (provider.MvaVehicleId is not { } vehicleId
            || provider.EreId is not { } ereId
            || provider.EstimatorUrl is not { } estimatorUrl)
        {
            throw new InvalidOperationException(
                "The Glass's session has no vehicle or estimate to resume, so its outcome stays for reconciliation.");
        }

        // The callback this session accepts never changes — the store refuses a
        // write that carries a different one — so the resumed launch reuses the
        // address the first one minted rather than trying to mint a second.
        var callback = PegasusCallbackOf(estimatorUrl);
        provider.Cookies.Clear();
        var client = NewClient(provider.Cookies);
        try
        {
            await client.SignInAsync(credential.Username, credential.Password, cancellationToken);
            await client.SelectOnlyAsync(vehicleId, cancellationToken);
            var launch = await client.StartEstimateAsync(ereId, callback, cancellationToken);
            Record(provider, launch);
            return await WriteAsync(
                session with { ProviderEstimateId = launch.EreId },
                GlassRepairEstimateSessionState.Active,
                null,
                provider,
                material.CallbackDigest,
                results,
                cancellationToken);
        }
        catch (Exception failure)
            when (failure is GlassMvaStageException || IsTransportFailure(failure, cancellationToken))
        {
            return await SettleAsync(
                session, AsFailure(failure), provider, material.CallbackDigest, results, cancellationToken);
        }
    }

    /// <summary>
    /// The operator's Save &amp; Exit, end to end: prove the correlation, relay
    /// the provider's message back to it, export and download the calculation,
    /// read it, reconcile it against the vehicle this session launched for,
    /// retain both artifacts, and land the estimate as a Draft.
    /// </summary>
    public async Task<GlassRepairEstimateSession> CompleteAsync(
        GlassRepairEstimateCallback callback, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(callback.RawQuery);
        RepairSpecificationPolicy.RequireEngineer(callback.Actor);
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
                ? session
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
        if (provider.EreId is not { } ereId || provider.OriginalCallback is not { } originalCallback)
        {
            throw new InvalidOperationException(
                "An active Glass's session must carry the estimate it started.");
        }

        // The claim. This delivery is the one acted on: its fingerprint and the
        // move to Importing go through the store's version check before the
        // provider hears anything, so two deliveries racing for the same
        // version meet here — one is recorded, the other reads the record.
        results.CallbackQueryDigest = queryDigest;
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
            return await ReadClaimAsync(callback, queryDigest, lost, cancellationToken);
        }

        if (session.ExpiresAtUtc <= timeProvider.GetUtcNow())
        {
            return await ExpireAsync(session, provider, material.CallbackDigest, results, cancellationToken);
        }

        var credential = await credentials.GetEnabledAsync(
            callback.Actor, ExternalCredentialProvider.GlassRepairEstimate, cancellationToken);
        if (credential is null || credential.Reference.CredentialGeneration != session.CredentialGeneration)
        {
            // The credential that launched this has been replaced or turned
            // off; the session it opened is no longer this Engineer's to finish.
            return await ExpireAsync(session, provider, material.CallbackDigest, results, cancellationToken);
        }
        if (Query(callback.RawQuery, "DoSave") != "1")
        {
            return await SettleAsync(
                session,
                new GlassMvaStageException(GlassFailure.CallbackNotSaved),
                provider,
                material.CallbackDigest,
                results,
                cancellationToken);
        }

        var client = NewClient(provider.Cookies);
        try
        {
            await client.RelayCallbackAsync(
                new Uri(originalCallback, UriKind.Absolute), ereId, callback.RawQuery, cancellationToken);
        }
        catch (Exception failure)
            when (failure is GlassMvaStageException || IsTransportFailure(failure, cancellationToken))
        {
            return await SettleAsync(
                session, AsFailure(failure), provider, material.CallbackDigest, results, cancellationToken);
        }

        return await ExportAsync(
            callback.Actor, session, provider, material.CallbackDigest, results, client, ereId, cancellationToken);
    }

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
            var link = await client.WaitForExportAsync(cancellationToken);
            exported = await client.DownloadExportAsync(link, cancellationToken);
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
        }
        catch (EstimateParseRejectedException)
        {
            return await SettleAsync(
                session,
                new GlassMvaStageException(GlassFailure.ExportUnreadable),
                provider,
                callbackDigest,
                results,
                cancellationToken);
        }
        catch (GlassMvaStageException failure)
        {
            return await SettleAsync(session, failure, provider, callbackDigest, results, cancellationToken);
        }

        results.Xml = await RetainAsync(
            actor,
            session,
            XmlOccurrenceIdentity(session.Id),
            $"{session.OperationKey}:xml",
            $"glass-estimate-{ereId}.xml",
            "application/xml",
            exported,
            cancellationToken);
        if (export.CalculationSheet is { } sheet)
        {
            results.Pdf = await RetainAsync(
                actor,
                session,
                PdfOccurrenceIdentity(session.Id),
                $"{session.OperationKey}:pdf",
                sheet.FileName,
                "application/pdf",
                sheet.Content.ToArray(),
                cancellationToken);
        }

        return await FinishAsync(actor, session, provider, callbackDigest, results, cancellationToken);
    }

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
            results.ImportedEstimateId = await import.ExecuteAsync(
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
                    RepairSpecificationSourceRoute.Glasses,
                    $"{session.OperationKey}:import",
                    Name: string.Empty),
                cancellationToken);
        }
        catch (Exception stale)
            when (stale is CaseVersionConflictException
                or CaseEditLeaseExpiredException
                or CaseEditLeaseConflictException)
        {
            // The Case moved on while the operator was in Glass's. Everything
            // the provider produced is already retained; the estimate lands
            // when the Engineer takes the Case back.
            return await WriteAsync(
                session,
                GlassRepairEstimateSessionState.AwaitingImport,
                null,
                provider,
                callbackDigest,
                results,
                cancellationToken);
        }
        catch (EstimateParseRejectedException)
        {
            return await SettleAsync(
                session,
                new GlassMvaStageException(GlassFailure.ExportUnreadable),
                provider,
                callbackDigest,
                results,
                cancellationToken);
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
    private static void RequireSameVehicle(GlassEstimateExport export, ProviderState provider)
    {
        if (!GlassMvaClient.SameRegistration(export.Identity.RegistrationPlate, provider.Registration))
        {
            throw new GlassMvaStageException(GlassFailure.IdentityRegistration);
        }
        if (export.Identity.Mileage != provider.MileageMiles)
        {
            throw new GlassMvaStageException(GlassFailure.IdentityMileage);
        }
        if (!string.Equals(export.Identity.TypeNumber, provider.NatCode, StringComparison.Ordinal))
        {
            throw new GlassMvaStageException(GlassFailure.IdentityNatCode);
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
        ?? throw new InvalidOperationException(
            "The signed-in Engineer has no enabled Glass's account, so no estimate can be started.");

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
                "This Glass's session belongs to another Engineer.");
        }
    }

    private Task<GlassRepairEstimateSession> ExpireAsync(
        GlassRepairEstimateSession session,
        ProviderState provider,
        string callbackDigest,
        Results results,
        CancellationToken cancellationToken) =>
        WriteAsync(
            session,
            GlassRepairEstimateSessionState.Expired,
            GlassFailure.CallbackExpired,
            provider,
            callbackDigest,
            results,
            cancellationToken);

    private Task<GlassRepairEstimateSession> SettleAsync(
        GlassRepairEstimateSession session,
        GlassMvaStageException failure,
        ProviderState provider,
        string callbackDigest,
        Results? results,
        CancellationToken cancellationToken) =>
        WriteAsync(
            session,
            failure.OutcomeUnknown
                ? GlassRepairEstimateSessionState.Unknown
                : GlassRepairEstimateSessionState.Failed,
            failure.FailureCode,
            provider,
            callbackDigest,
            results,
            cancellationToken);

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
        return next with { Version = session.Version + 1 };
    }

    private static void Record(ProviderState provider, GlassEstimateLaunch launch)
    {
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

        public string? MvaVehicleId { get; set; }

        public string? EreId { get; set; }

        /// <summary>
        /// The provider's own callback, whole. It names the estimate and the
        /// provider session the relay is made under, so keeping it is what lets
        /// a completion relay to exactly the address Glass's issued instead of
        /// rebuilding one from its parts.
        /// </summary>
        public string? OriginalCallback { get; set; }

        public string? EstimatorUrl { get; set; }
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

        public Artifact? Xml { get; set; }

        public Artifact? Pdf { get; set; }

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
