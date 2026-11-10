using Pegasus.Core.Identity;

namespace Pegasus.Core.PhotoCapture;

/// <summary>The refusals the capture surface and the staff pages name; the Web maps each to one bounded answer.</summary>
public sealed class PhotoCaptureRefusalException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public static class PhotoCaptureRefusals
{
    public const string Unavailable = "unavailable";
    public const string DetailsRequired = "details_required";
    public const string RegistrationRequired = "registration_required";
    public const string RegistrationLocked = "registration_locked";
    public const string UnsupportedType = "unsupported_type";
    public const string TooLarge = "too_large";
    public const string CapReached = "cap_reached";
    public const string ItemConflict = "item_conflict";
    public const string NotReady = "not_ready";
    public const string MintCapReached = "mint_cap_reached";
    public const string CaseUnavailable = "case_unavailable";
    public const string InvalidRequest = "invalid_request";
}

/// <summary>
/// What a staff member asks for when they issue a photo link from a Case.
/// A quick link names who it is for (<see cref="QuickLinkFor"/>, R808); a
/// guided link names nobody.
/// </summary>
public sealed record IssuePhotoLinkRequest(
    Guid CaseId,
    ActionActor Actor,
    string? CustomerName,
    string? Mobile,
    string? Email,
    DateOnly? IncidentDate,
    PhotoCaptureMode Mode,
    PhotoCaptureQuickLinkFor? QuickLinkFor,
    bool LiveCameraOnly,
    bool OriginalsInBackground,
    int LifetimeDays,
    PhotoLinkTokenPurpose TokenPurpose);

public sealed record IssuedPhotoLink(PhotoCapture Capture, CaptureLinkSecret Token);

/// <summary>
/// HQ issues a link for a Case it already has (handover §6 "Send a photo
/// link"): one capture per issue, with its first token. Casework is the
/// right to do it; an archived Case takes no more evidence.
/// </summary>
public sealed class IssuePhotoLink(
    IPhotoCaptureStore captures,
    IPhotoCaptureCaseReader cases,
    TimeProvider timeProvider)
{
    public async Task<IssuedPhotoLink> ExecuteAsync(IssuePhotoLinkRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        if (request.Mode == PhotoCaptureMode.Quick && request.QuickLinkFor is null)
        {
            throw new ArgumentException("A quick link names who it is for.", nameof(request));
        }

        if (!CaptureLimits.IsLifetime(request.LifetimeDays))
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.InvalidRequest, "The link lifetime is not one of the offered ones.");
        }

        var facts = await cases.GetAsync(request.CaseId, cancellationToken);
        if (facts is null || facts.IsArchived)
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.CaseUnavailable, "The Case cannot take a photo link.");
        }

        var now = timeProvider.GetUtcNow();
        var token = PhotoLinkToken.Create();
        var capture = await captures.CreateAsync(
            new NewPhotoCapture(
                Guid.NewGuid(),
                PhotoCaptureOrigin.CaseLink,
                StandingLinkId: null,
                OrganizationId: null,
                request.CaseId,
                PhotoCaptureDestination.Case,
                facts.NormalizedRegistration,
                request.IncidentDate ?? facts.IncidentDate,
                PhotoCaptureText.Name(request.CustomerName) ?? facts.ClaimantName,
                PhotoCaptureText.Mobile(request.Mobile) ?? facts.ClaimantContactNumber,
                PhotoCaptureText.Email(request.Email),
                request.Mode,
                request.Mode == PhotoCaptureMode.Quick ? request.QuickLinkFor : null,
                request.LiveCameraOnly,
                request.OriginalsInBackground,
                Guid.TryParse(request.Actor.SubjectId, out var staffId) ? staffId : null,
                now,
                now.AddDays(request.LifetimeDays),
                OpenedAtUtc: null),
            PhotoLinkToken.Digest(token),
            request.TokenPurpose,
            sessionTokenDigest: null,
            cancellationToken);
        return new(capture, token);
    }
}

/// <summary>The short form a standing link's opener fills before photos or a hand-off.</summary>
public sealed record StandingCaptureForm(
    string? Registration,
    DateOnly? IncidentDate,
    string? CustomerName,
    string? Mobile,
    string? Email,
    PhotoCaptureDetails Details);

/// <summary>
/// A capture a standing link started: its token and the hand-off message,
/// and for a hand-off the <see cref="SessionToken"/> the staff phone carries
/// on, so what it still sends never marks the customer's link Opened.
/// </summary>
public sealed record MintedCapture(PhotoCapture Capture, CaptureLinkSecret Token, string Message, CaptureLinkSecret? SessionToken);

/// <summary>
/// A standing link starts captures and nothing else. <see cref="StartAsync"/>
/// is whoever holds the phone taking the photos themselves (quick mode for a
/// contact's staff, guided for the website); <see cref="HandoffAsync"/> is
/// their "Send customer a link", which mints a guided link the phone's own
/// SMS or WhatsApp carries (no server send; operator, 9 November 2026). Both
/// count against the link's daily cap. A capture started to take the photos
/// now is Opened as it is made, under a Session token; a hand-off is not
/// Opened until the customer opens it, and the staff phone gets a Session
/// token of its own for the form's paperwork.
/// </summary>
public sealed class MintFromStandingLink(
    IPhotoCaptureStore captures,
    IStandingPhotoLinkStore standingLinks,
    RecordPhotoCaptureDetails recordDetails,
    TimeProvider timeProvider)
{
    public Task<MintedCapture> StartAsync(
        StandingPhotoLink link,
        StandingCaptureForm form,
        CancellationToken cancellationToken) =>
        MintAsync(
            link,
            form,
            link.Kind == StandingPhotoLinkKind.Website ? PhotoCaptureOrigin.Website : PhotoCaptureOrigin.StandingStaff,
            link.Kind == StandingPhotoLinkKind.Website ? PhotoCaptureMode.Guided : PhotoCaptureMode.Quick,
            PhotoLinkTokenPurpose.Session,
            cancellationToken);

    public Task<MintedCapture> HandoffAsync(
        StandingPhotoLink link,
        StandingCaptureForm form,
        CancellationToken cancellationToken) =>
        MintAsync(
            link,
            form,
            PhotoCaptureOrigin.StandingForwarded,
            link.ForwardedMode,
            PhotoLinkTokenPurpose.Handoff,
            cancellationToken);

    private async Task<MintedCapture> MintAsync(
        StandingPhotoLink link,
        StandingCaptureForm form,
        PhotoCaptureOrigin origin,
        PhotoCaptureMode mode,
        PhotoLinkTokenPurpose purpose,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(form);
        if (!link.StartsCaptures)
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.Unavailable, "The link is not active.");
        }

        var registration = PhotoCaptureText.Registration(form.Registration)
            ?? throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.RegistrationRequired, "A vehicle registration is required.");
        var now = timeProvider.GetUtcNow();
        var minted = await standingLinks.CountMintedSinceAsync(link.Id, now.AddDays(-1), cancellationToken);
        var cap = link.Kind == StandingPhotoLinkKind.Website ? CaptureLimits.WebsiteMintsPerDay : CaptureLimits.ContactMintsPerDay;
        if (minted >= cap)
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.MintCapReached, "This link has started as many captures as it may today.");
        }

        var token = PhotoLinkToken.Create();
        var handoff = purpose == PhotoLinkTokenPurpose.Handoff;
        CaptureLinkSecret? session = handoff ? PhotoLinkToken.Create() : null;
        var capture = await captures.CreateAsync(
            new NewPhotoCapture(
                Guid.NewGuid(),
                origin,
                link.Id,
                link.OrganizationId,
                CaseId: null,
                PhotoCaptureDestination.Undecided,
                registration,
                form.IncidentDate,
                PhotoCaptureText.Name(form.CustomerName),
                PhotoCaptureText.Mobile(form.Mobile),
                PhotoCaptureText.Email(form.Email),
                mode,
                QuickLinkFor: null,
                link.LiveCameraOnly,
                link.OriginalsInBackground,
                IssuedByStaffId: null,
                now,
                now.AddDays(link.LinkLifetimeDays),
                OpenedAtUtc: handoff ? null : now),
            PhotoLinkToken.Digest(token),
            purpose,
            session is { } sessionToken ? PhotoLinkToken.Digest(sessionToken) : null,
            cancellationToken);
        // The form's registration routes the capture at once, so the first
        // photo already knows where it files.
        capture = await recordDetails.ExecuteAsync(capture, link, form, cancellationToken);
        return new(capture, token, PhotoCaptureMessages.Handoff(capture.CustomerName, registration), session);
    }
}

/// <summary>
/// "Send customer a link" on a quick link sent from a Case (R798): the firm's
/// staff hand the same capture on to their customer, so the Case keeps one
/// link and one row on Links we sent. What they typed is recorded, then the
/// capture gains a Handoff token, the guided link the staff member's own
/// phone carries to the customer, and a Session token the staff phone sends
/// the form's paperwork under. Nothing stored is rewritten: the firm's
/// Opened stands, and the customer's guided view and "Sent on to their
/// customer" are read from the Handoff token.
/// </summary>
public sealed class HandOnCaseLink(
    IPhotoCaptureStore captures,
    RecordPhotoCaptureDetails recordDetails,
    TimeProvider timeProvider)
{
    public async Task<MintedCapture> ExecuteAsync(
        PhotoCapture capture,
        StandingCaptureForm form,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(form);
        var now = timeProvider.GetUtcNow();
        if (!PhotoCaptureAccess.IsLive(capture, now) || !IsQuickCaseLink(capture))
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.Unavailable, "The link is no longer active.");
        }

        var registration = capture.NormalizedRegistration
            ?? throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.RegistrationRequired, "A vehicle registration is required.");
        var recorded = await recordDetails.ExecuteAsync(capture, standingLink: null, form, cancellationToken);
        var token = PhotoLinkToken.Create();
        var session = PhotoLinkToken.Create();
        // The Session token first: a Handoff token is what marks the link
        // sent on, so it is written only once the staff phone has its own.
        await captures.AddTokenAsync(capture.Id, PhotoLinkToken.Digest(session), PhotoLinkTokenPurpose.Session, issuedByStaffId: null, now, cancellationToken);
        await captures.AddTokenAsync(capture.Id, PhotoLinkToken.Digest(token), PhotoLinkTokenPurpose.Handoff, issuedByStaffId: null, now, cancellationToken);
        return new(recorded, token, PhotoCaptureMessages.Handoff(recorded.CustomerName, registration), session);
    }

    /// <summary>A quick link sent from a Case that names who it is for: the staff version (R798, R808).</summary>
    public static bool IsQuickCaseLink(PhotoCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        return capture.Origin == PhotoCaptureOrigin.CaseLink
            && capture.Mode == PhotoCaptureMode.Quick
            && capture.QuickLinkFor is not null;
    }
}

/// <summary>
/// Records what the sender typed and, on a standing or website capture,
/// routes it (<see cref="RoutePhotoCapture"/>) again each time, until its
/// first photo, so a registration or accident date changed before then is
/// routed on, not just stored. A link sent from a Case keeps its Case's
/// registration and ignores a typed one. The registration is locked once a
/// photo has filed against it: a different one would be a different vehicle.
/// </summary>
public sealed class RecordPhotoCaptureDetails(
    IPhotoCaptureStore captures,
    RoutePhotoCapture route)
{
    public async Task<PhotoCapture> ExecuteAsync(
        PhotoCapture capture,
        StandingPhotoLink? standingLink,
        StandingCaptureForm form,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(form);
        var caseLink = capture.Origin == PhotoCaptureOrigin.CaseLink;
        var registration = caseLink
            ? capture.NormalizedRegistration
            : PhotoCaptureText.Registration(form.Registration) ?? capture.NormalizedRegistration;
        if (capture.AcceptedItemCount > 0
            && registration is not null
            && !string.Equals(registration, capture.NormalizedRegistration, StringComparison.Ordinal))
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.RegistrationLocked, "The registration cannot change once photos have been sent.");
        }

        var incidentDate = form.IncidentDate ?? capture.IncidentDate;
        var current = capture;
        if (!caseLink && capture.AcceptedItemCount == 0 && registration is not null)
        {
            // Routed before the details are written, so a routing the store
            // refuses (an item row arrived) leaves the typed details unwritten
            // too, rather than a registration the routing never saw.
            var decision = await route.ExecuteAsync(registration, incidentDate, standingLink?.PrincipalId, cancellationToken);
            current = await captures.RecordRoutingAsync(
                current.Id,
                current.Version,
                decision.Destination,
                decision.CaseId,
                decision.Reason,
                cancellationToken);
        }

        return await captures.RecordDetailsAsync(
            current.Id,
            current.Version,
            registration,
            incidentDate,
            PhotoCaptureText.Name(form.CustomerName) ?? capture.CustomerName,
            PhotoCaptureText.Mobile(form.Mobile) ?? capture.Mobile,
            PhotoCaptureText.Email(form.Email) ?? capture.Email,
            PhotoCaptureText.Details(form.Details),
            cancellationToken);
    }
}

public sealed record PhotoCaptureRoutingDecision(PhotoCaptureDestination Destination, Guid? CaseId, string Reason);

/// <summary>
/// Where a standing capture's photos go, decided from its typed registration
/// the way FRD-19 decides a mailed photograph's (operator, 9 November 2026):
/// exactly one eligible formal Case with that registration, within a known
/// Principal's own Cases, whose incident date does not contradict the typed
/// one (FRD-22: the date excludes and never matches), files there; none, or
/// several, becomes a Vehicle images record for staff and pairing.
/// </summary>
public sealed class RoutePhotoCapture(ImageIntake.IImageIntakeCaseCandidates caseCandidates)
{
    public const string NoEligibleCase = "photo_capture_no_eligible_case";
    public const string CasesAmbiguous = "photo_capture_cases_ambiguous";
    public const string SingleEligibleCase = "photo_capture_single_eligible_case";

    public async Task<PhotoCaptureRoutingDecision> ExecuteAsync(
        string normalizedRegistration,
        DateOnly? incidentDate,
        Guid? principalId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedRegistration);
        var candidates = await caseCandidates.FindEligibleByRegistrationAsync(normalizedRegistration, cancellationToken);
        return Decide(candidates, normalizedRegistration, incidentDate, principalId);
    }

    public static PhotoCaptureRoutingDecision Decide(
        IReadOnlyList<ImageIntake.ImageIntakeCaseCandidate> candidates,
        string normalizedRegistration,
        DateOnly? incidentDate,
        Guid? principalId)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var scoped = ImageIntake.ImageIntakeCasePairing.ScopeToPrincipal(candidates, principalId);
        var exact = scoped
            .Where(candidate => !candidate.IsTriage
                && string.Equals(candidate.ConfirmedRegistration, normalizedRegistration, StringComparison.Ordinal)
                && ImageIntake.IncidentDateExclusion.Admits(candidate, incidentDate))
            .ToArray();
        return exact.Length switch
        {
            1 => new(PhotoCaptureDestination.Case, exact[0].CaseId, SingleEligibleCase),
            0 => new(PhotoCaptureDestination.VehicleImages, null, NoEligibleCase),
            _ => new(PhotoCaptureDestination.VehicleImages, null, CasesAmbiguous)
        };
    }
}

/// <summary>One upload from the phone: a photo, a document or a video, or the full-size original of an earlier photo.</summary>
public sealed record ReceivePhotoCaptureItemRequest(
    Guid ClientItemId,
    PhotoCaptureItemKind Kind,
    string? Area,
    PhotoCaptureStage? Stage,
    int Position,
    DateTimeOffset? TakenAtUtc,
    bool LiveCamera,
    bool IsOriginal,
    string? FileName,
    string MediaType,
    long ContentLength,
    Func<CancellationToken, ValueTask<Stream>> OpenContentAsync);

public sealed record ReceivedPhotoCaptureItem(PhotoCaptureItemWrite Write, PhotoCaptureItem Item, int ItemCount);

/// <summary>
/// Takes one item into the capture and into intake (FRD-28, FRD-19). Each
/// item is its own intake receipt: a submission group's membership is fixed
/// when it is made, and a capture keeps receiving with no known total. A
/// Case-destination item declares its Case and files at once, like an upload
/// to a declared Case (FRD-18; R727); a Vehicle-images-destination item waits
/// as a group member until the capture settles. The same client id with the
/// same bytes is a replay and does nothing twice.
/// </summary>
public sealed class ReceivePhotoCaptureItem(
    IPhotoCaptureStore captures,
    Intake.IIntakeSubmission intake,
    IFilePhotoCaptureOriginal originals,
    TimeProvider timeProvider)
{
    public async Task<ReceivedPhotoCaptureItem> ExecuteAsync(
        PhotoCapture capture,
        ReceivePhotoCaptureItemRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(request);
        var now = timeProvider.GetUtcNow();
        if (!PhotoCaptureAccess.IsLive(capture, now))
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.Unavailable, "The link is no longer active.");
        }

        if (!PhotoCaptureStory.AcceptsMediaType(request.Kind, request.MediaType))
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.UnsupportedType, "That kind of file is not accepted here.");
        }

        if (request.ContentLength <= 0 || request.ContentLength > CaptureLimits.MaximumBytesFor(request.Kind, request.IsOriginal))
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.TooLarge, "The file is larger than this link accepts.");
        }

        if (request.IsOriginal)
        {
            return await ReceiveOriginalAsync(capture, request, cancellationToken);
        }

        if (capture.Origin != PhotoCaptureOrigin.CaseLink && capture.Destination == PhotoCaptureDestination.Undecided)
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.DetailsRequired, "The registration is needed before photos.");
        }

        // A damage photo taken outside any marked area ("Add more", the
        // phone's own camera) is kept as a loose damage photo, not refused
        // (operator, 9 November 2026; R802); its stage stays as sent.
        var area = PhotoCaptureStory.ItemArea(request.Kind, request.Area);
        var hash = await Intake.ReceiveIntake.StreamHashAsync(
            new Intake.StreamedIntakeSource(
                "item", request.MediaType, request.ContentLength, request.OpenContentAsync, now,
                Intake.IntakeActorIdentity.PhotoCapture(capture.Id),
                new(Intake.IntakeSourceChannel.PhotoCapture, "hash")),
            cancellationToken);
        var group = PhotoCaptureStory.Group(request.Kind, area, capture.Details.MarkedAreas);
        var sequence = PhotoCaptureStory.Sequence(request.Kind, request.Stage, request.Position);
        var fileName = PhotoCaptureStory.FileName(
            request.Kind, area, request.Stage, request.Position, group, sequence, request.MediaType, request.FileName);
        var write = await captures.AddItemAsync(
            new NewPhotoCaptureItem(
                Guid.NewGuid(),
                capture.Id,
                request.ClientItemId,
                request.Kind,
                area,
                request.Stage,
                request.Position,
                group,
                sequence,
                fileName,
                request.MediaType,
                request.TakenAtUtc,
                request.LiveCamera,
                PhotoCaptureStory.StartsInReport(request.Kind),
                hash,
                request.ContentLength,
                now),
            cancellationToken);
        switch (write.Write)
        {
            case PhotoCaptureItemWrite.Conflict:
                throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.ItemConflict, "This photo was already sent with different content.");
            case PhotoCaptureItemWrite.CapReached:
                throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.CapReached, "This link has received as much as it can take.");
        }

        var item = write.Item!;
        var received = write.Write == PhotoCaptureItemWrite.Created || item.StagedReceiptId is null;
        if (received)
        {
            // The item's batch and declared Case are the store's, taken under
            // the capture's lock, not the capture as this request read it, so
            // a retry declares what its first attempt did. A document type is
            // declared only with the Case it files on: intake refuses one
            // without the other, and a capture with no Case has none to
            // declare (FRD-28).
            var declaredCaseId = item.DeclaredCaseId;
            var receivedIntake = await intake.ReceiveStreamedAsync(
                new Intake.StreamedIntakeSource(
                    item.FileName,
                    request.MediaType,
                    request.ContentLength,
                    request.OpenContentAsync,
                    now,
                    Intake.IntakeActorIdentity.PhotoCapture(capture.Id),
                    new(Intake.IntakeSourceChannel.PhotoCapture, PhotoCaptureIntakeRules.MemberToken(capture.Id, item.Batch, item.Ordinal)),
                    declaredCaseId,
                    declaredCaseId is null ? null : PhotoCaptureStory.DeclaredDocumentType(request.Kind, request.MediaType)),
                $"photo-capture-item:{item.Id:N}",
                cancellationToken);
            await captures.RecordItemReceiptAsync(item.Id, receivedIntake.StagedReceiptId, cancellationToken);
            await intake.PublishCommittedAsync(receivedIntake, cancellationToken);
            item = item with { StagedReceiptId = receivedIntake.StagedReceiptId };
        }

        var count = capture.AcceptedItemCount + (received ? 1 : 0);
        return new(write.Write, item, count);
    }

    private async Task<ReceivedPhotoCaptureItem> ReceiveOriginalAsync(
        PhotoCapture capture,
        ReceivePhotoCaptureItemRequest request,
        CancellationToken cancellationToken)
    {
        // Only a capture filing on its Case as each photo arrives takes
        // originals; for any other the refusal is final, so the phone sends
        // and retries nothing (operator, 9 November 2026; R801).
        if (!capture.TakesOriginals || !PhotoCaptureStory.IsPhotographKind(request.Kind))
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.UnsupportedType, "This link does not take full-size originals.");
        }

        var item = await captures.FindItemAsync(capture.Id, request.ClientItemId, cancellationToken)
            ?? throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.NotReady, "Send the photo before its original.");
        if (item.OriginalReceived)
        {
            return new(PhotoCaptureItemWrite.Replayed, item, capture.AcceptedItemCount);
        }

        if (item.IntakeAssetId is null)
        {
            // The compressed photo has not filed yet; the phone retries later.
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.NotReady, "The photo is still being filed.");
        }

        // The original counts against the link's originals and bytes before
        // it is filed, and is given back if it does not file.
        if (!await captures.ReserveOriginalAsync(item.Id, request.ContentLength, cancellationToken))
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.CapReached, "This link has received as much as it can take.");
        }

        bool filed;
        try
        {
            var hash = await Intake.ReceiveIntake.StreamHashAsync(
                new Intake.StreamedIntakeSource(
                    "original", request.MediaType, request.ContentLength, request.OpenContentAsync, timeProvider.GetUtcNow(),
                    Intake.IntakeActorIdentity.PhotoCapture(capture.Id),
                    new(Intake.IntakeSourceChannel.PhotoCapture, "hash")),
                cancellationToken);
            filed = await originals.FileAsync(
                item,
                item.FileName,
                request.MediaType,
                request.ContentLength,
                hash,
                request.OpenContentAsync,
                cancellationToken);
        }
        catch
        {
            await captures.ReleaseOriginalAsync(item.Id, request.ContentLength, CancellationToken.None);
            throw;
        }

        if (!filed)
        {
            await captures.ReleaseOriginalAsync(item.Id, request.ContentLength, cancellationToken);
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.NotReady, "The photo is still being filed.");
        }

        await captures.RecordItemOriginalAsync(item.Id, cancellationToken);
        return new(PhotoCaptureItemWrite.Created, item with { OriginalReceived = true }, capture.AcceptedItemCount);
    }
}

public sealed record CompletedPhotoCapture(PhotoCapture Capture, IReadOnlyList<MissingCaptureItem> Missing);

/// <summary>
/// The sender pressed Send: the note is kept as last sent (an emptied one
/// empty, none at all the earlier one), the time recorded, and what is
/// still missing is told back so the app can offer "Could you add a few
/// more?". The link stays open to additions until it expires.
/// </summary>
public sealed class CompletePhotoCapture(
    IPhotoCaptureStore captures,
    IStandingPhotoLinkStore standingLinks,
    TimeProvider timeProvider)
{
    public async Task<CompletedPhotoCapture> ExecuteAsync(
        PhotoCapture capture,
        string? note,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capture);
        var now = timeProvider.GetUtcNow();
        if (!PhotoCaptureAccess.IsLive(capture, now))
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.Unavailable, "The link is no longer active.");
        }

        // A note already written to the Case stays there whatever is kept here.
        var trimmed = PhotoCaptureText.NoteOnSend(note, capture.Note);
        var updated = await captures.RecordSentAsync(capture.Id, trimmed, now, cancellationToken);
        var items = (await captures.ListItemsAsync(capture.Id, cancellationToken))
            .Where(item => item.StagedReceiptId is not null)
            .ToArray();
        var missing = PhotoCaptureRequiredItems.Evaluate(
            updated,
            items,
            PhotoCaptureRequiredItems.VinRequired(updated, await IsRepairerLinkAsync(updated, cancellationToken)));
        return new(updated, missing);
    }

    private async Task<bool> IsRepairerLinkAsync(PhotoCapture capture, CancellationToken cancellationToken) =>
        capture.StandingLinkId is { } linkId
        && await standingLinks.GetAsync(linkId, cancellationToken) is { IsRepairer: true };
}

/// <summary>
/// The staff acts on a link: cancel, extend, remind, log a call; each is
/// casework, and each records who did it under the form's operation key, so
/// the link's history names them and a double click acts once (R805). A link
/// whose Case is archived is dead (R793): it is neither extended nor sent
/// again.
/// </summary>
public sealed class PhotoLinkActions(
    IPhotoCaptureStore captures,
    IPhotoCaptureCaseReader cases,
    TimeProvider timeProvider)
{
    public async Task<PhotoCapture> RevokeAsync(
        ActionActor actor,
        Guid captureId,
        string operationKey,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        return await captures.RevokeAsync(captureId, actor, operationKey, timeProvider.GetUtcNow(), cancellationToken);
    }

    public async Task<PhotoCapture> ExtendAsync(
        ActionActor actor,
        Guid captureId,
        int days,
        string operationKey,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (!CaptureLimits.IsLifetime(days))
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.InvalidRequest, "The extension is not one of the offered lifetimes.");
        }

        var capture = await captures.GetAsync(captureId, cancellationToken)
            ?? throw new KeyNotFoundException("The photo link was not found.");
        await RequireCaseNotArchivedAsync(capture, cancellationToken);
        var from = capture.ExpiresAtUtc > timeProvider.GetUtcNow() ? capture.ExpiresAtUtc : timeProvider.GetUtcNow();
        return await captures.ExtendAsync(captureId, actor, operationKey, from.AddDays(days), cancellationToken);
    }

    /// <summary>
    /// A fresh token for the same capture, for a resend or a reminder: the
    /// earlier message keeps working, and the new one is counted as a
    /// reminder when <paramref name="isReminder"/>.
    /// </summary>
    public async Task<IssuedPhotoLink> ReissueAsync(
        ActionActor actor,
        Guid captureId,
        PhotoLinkTokenPurpose purpose,
        bool isReminder,
        string operationKey,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        var capture = await captures.GetAsync(captureId, cancellationToken)
            ?? throw new KeyNotFoundException("The photo link was not found.");
        await RequireCaseNotArchivedAsync(capture, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (!PhotoCaptureAccess.IsLive(capture, now))
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.Unavailable, "The link has expired or was cancelled; extend it first.");
        }

        var token = PhotoLinkToken.Create();
        await captures.AddTokenAsync(
            captureId,
            PhotoLinkToken.Digest(token),
            purpose,
            Guid.TryParse(actor.SubjectId, out var staffId) ? staffId : null,
            now,
            cancellationToken);
        if (isReminder)
        {
            capture = await captures.RecordReminderAsync(captureId, actor, operationKey, now, cancellationToken);
        }

        return new(capture, token);
    }

    public async Task<PhotoCapture> LogCallAsync(
        ActionActor actor,
        Guid captureId,
        string operationKey,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        return await captures.RecordCallAsync(captureId, actor, operationKey, timeProvider.GetUtcNow(), cancellationToken);
    }

    private async Task RequireCaseNotArchivedAsync(PhotoCapture capture, CancellationToken cancellationToken)
    {
        if (capture.CaseId is { } caseId && await cases.GetAsync(caseId, cancellationToken) is { IsArchived: true })
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.CaseUnavailable, "The Case cannot take a photo link.");
        }
    }
}

/// <summary>The messages the link travels in: the prototype's own wording (handover, 5 November 2026), confirmed in the FRD-28 review.</summary>
public static class PhotoCaptureMessages
{
    public static string Handoff(string? customerName, string registration)
    {
        var first = (customerName ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        var greeting = first is null ? "Hi," : $"Hi {first},";
        return $"{greeting} Collision Engineers need a few photos of {PhotoCaptureText.DisplayRegistration(registration)} to assess the damage. "
            + "Please open this link on your phone – it takes about 3 minutes and there's nothing to download: ";
    }
}

/// <summary>Trimming and bounding of what a sender types; a value past its bound is cut, not refused, because the sender cannot fix it later.</summary>
public static class PhotoCaptureText
{
    public static string? Registration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = new string(value.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        return normalized.Length is >= 2 and <= 10 ? normalized : null;
    }

    /// <summary>A registration as a plate shows it: the UK current format splits after the fourth character.</summary>
    public static string DisplayRegistration(string normalized) =>
        normalized.Length == 7 && char.IsAsciiLetter(normalized[0]) && char.IsAsciiDigit(normalized[2])
            ? normalized[..4] + " " + normalized[4..]
            : normalized;

    public static string? Name(string? value) => Bound(value, CaptureLimits.NameLength);

    public static string? Mobile(string? value) => Bound(value, CaptureLimits.MobileLength);

    public static string? Email(string? value)
    {
        var bounded = Bound(value, CaptureLimits.EmailLength);
        return bounded is not null && bounded.Contains('@') ? bounded : null;
    }

    public static string? Note(string? value) => Bound(value, CaptureLimits.NoteLength);

    /// <summary>
    /// The note a Send keeps: none sent keeps the earlier one, and a note the
    /// sender emptied (sent blank) is stored empty rather than leaving the
    /// earlier one in place.
    /// </summary>
    public static string? NoteOnSend(string? sent, string? earlier) => sent is null ? earlier : Note(sent);

    public static PhotoCaptureDetails Details(PhotoCaptureDetails? details)
    {
        if (details is null)
        {
            return PhotoCaptureDetails.Empty;
        }

        return new(
            Bound(details.Handler, CaptureLimits.FreeTextLength),
            Bound(details.InspectionLocation, CaptureLimits.FreeTextLength),
            Bound(details.InspectionLocationChoice, CaptureLimits.FreeTextLength),
            Bound(details.Roadworthy, 20),
            Bound(details.Outcome, 20),
            Bound(details.InHire, 20),
            Bound(details.RepairView, 20),
            Bound(details.VehicleRole, 40),
            details.Mileage is > 0 and < 10_000_000 ? details.Mileage : null,
            Bound(details.Airbags, 20),
            Bound(details.Steering, 20),
            details.MarkedAreas.Where(PhotoCaptureStory.IsZone).Distinct(StringComparer.Ordinal).Take(PhotoCaptureStory.MaximumAreas).ToArray(),
            details.NoKeys);
    }

    private static string? Bound(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = new string(value.Trim().Where(character => !char.IsControl(character) || character == '\n').ToArray());
        return trimmed.Length <= maximumLength ? trimmed : trimmed[..maximumLength];
    }
}
