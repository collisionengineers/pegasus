using System.Collections.Frozen;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using EditorLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.Editors;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// What every page that mutates a case shares: the staff actor, the two command
/// wrappers that turn a use-case call into a post-redirect-get with a status or
/// error (one naming the case as the reason for a refusal, one naming the item
/// it carries), and the CASE-27 edit-mode state that travels through TempData —
/// the lease token, the operation keys, and the refused editor's proposed values.
/// The workspace page (<see cref="DetailsModel"/>) reads that state back;
/// the capability pages only write it.
/// </summary>
public abstract partial class CaseMutationPageModel(ILogger logger) : StaffPageModel
{
    private const string LeaseTokenKey = "CaseLeaseToken";
    protected const string LeaseCaseIdKey = "CaseLeaseCaseId";
    protected const string ReleaseLeaseOperationKeyName = "CaseReleaseLeaseOperationKey";
    protected const string ProposedValuesKey = "CaseProposedValues";
    protected const string ProposedValuesCaseIdKey = "CaseProposedValuesCaseId";
    protected const string ProposedValuesDroppedKey = "CaseProposedValuesDropped";
    protected const string ProposedValuesShortenedKey = "CaseProposedValuesShortened";

    /// <summary>
    /// The retained payload is bounded so one refusal cannot grow the response without limit.
    /// Cookie TempData chunks across cookies, so the ceiling is a deliberate budget rather than a
    /// hard 4 KB wall: the per-value cap matches the longest field an edit form accepts, and the
    /// total holds an ordinary case-data save with circumstances, address, and reason together.
    /// Nothing is trimmed or discarded quietly — both outcomes are stated in the panel.
    /// </summary>
    private const int MaximumRetainedProposedCharacters = 8000;
    private const int MaximumRetainedProposedValueCharacters = 2000;

    /// <summary>
    /// The values an operator types or chooses as case content. Identifiers, versions, keys,
    /// tokens, and the fields that only route a command are never retained, so the comparison
    /// shows editorial work and never an identifier.
    /// </summary>
    private static readonly FrozenSet<string> RetainableFormFields = new[]
    {
        "claimantName",
        "claimantContactNumber",
        "claimantAddress",
        "claimNumber",
        "vehicleRegistration",
        "vehicleMake",
        "vehicleModel",
        "vehicleYear",
        "vehicleMileage",
        "vehicleMileageUnit",
        "accidentCircumstances",
        "incidentDate",
        "contactName",
        "contactEmailAddress",
        "contactPhoneNumber",
        "vatStatus",
        "inspectionDate",
        "inspectionDeadline",
        "inspectionAddress",
        "inspectionMode",
        "storageLocation",
        "registration",
        "make",
        "model",
        "mileage",
        "mileageUnit",
        "reason",
        "note",
        "description",
        "channel",
        "recipient",
        "content",
        "outcome",
        "assessment",
        "artifactIdentity",
        "replacementPrincipalCode",
        "semanticRole",
        "expiresAtUtc"
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// The request is a commit made by the page script, and the page answers it in place (FRD-16)
    /// rather than by redirect. What the command says is then held on the page model instead of
    /// queued in TempData for a page load that never follows, so a notice cannot appear twice.
    /// The lease the browser holds stays in TempData: the next page load reads it from there.
    /// </summary>
    protected bool AnswersInPlace { get; private set; }

    private readonly Dictionary<string, string?> heldNotices = [];

    protected void AnswerInPlace() => AnswersInPlace = true;

    /// <summary>What the command said under <paramref name="key"/>, when the page answers in place.</summary>
    protected string? HeldNotice(string key) => heldNotices.GetValueOrDefault(key);

    /// <summary>Says <paramref name="message"/> (or nothing) under <paramref name="key"/>, wherever this request keeps its notices.</summary>
    protected void Say(string key, string? message)
    {
        if (AnswersInPlace)
        {
            heldNotices[key] = message;
        }
        else if (message is null)
        {
            TempData.Remove(key);
        }
        else
        {
            TempData[key] = message;
        }
    }

    /// <summary>
    /// The page cannot answer in place after all, so what the command said goes to the redirect
    /// that follows, as it does for every other request.
    /// </summary>
    protected void QueueHeldNotices()
    {
        AnswersInPlace = false;
        foreach (var (key, message) in heldNotices)
        {
            Say(key, message);
        }
        heldNotices.Clear();
    }

    /// <summary>The lease this browser holds on the case being rendered, if it holds one.</summary>
    public string? LeaseToken { get; private set; }

    /// <summary>
    /// Draws this request as the holder of a lease the browser presented to an
    /// asynchronous read, once Core has validated it. Nothing is stored: that
    /// read must not touch the cookie-backed state a redirect depends on.
    /// </summary>
    protected void AdoptRenderLease(string token) => LeaseToken = token;

    /// <summary>
    /// A claim key belongs to one claim, so every render offers a new one: with no live lease the
    /// last key this browser used has either ended with its lease or never claimed anything, and
    /// replaying a finished claim would only refuse the operator.
    /// </summary>
    public string ClaimLeaseOperationKey { get; } = NewOperationKey();

    public string ReleaseLeaseOperationKey { get; private set; } = NewOperationKey();

    /// <summary>
    /// Reconciles what this browser remembers against what the server says the case's edit
    /// authority actually is. Every page that renders edit mode asks it, so the workspace and the
    /// assessment agree about one lease without keeping two rules.
    /// </summary>
    /// <remarks>
    /// The holder is the staff member, not the window (FRD-14). This browser keeps one lease token
    /// at a time, so a visit to another case, or a second tab, can leave it without the token for
    /// the case the viewer is editing. The viewer is then put straight back into edit mode by
    /// resuming the lease they hold, never offered a takeover of themselves.
    /// </remarks>
    protected async Task RestoreLeaseStateAsync(
        Guid caseId,
        ActionActor actor,
        CaseEditLeaseSnapshot? activeLease,
        CaseEditLeases leases,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(leases);

        // An expired lease is already absent from the projection, so no page keeps a second rule.
        if (activeLease is null
            || !CaseEditAuthority.IsHolder(activeLease.HolderKind, activeLease.Holder, actor))
        {
            ClearLeaseState();
            return;
        }

        var storedToken = PeekLeaseToken();
        var token = PeekGuid(LeaseCaseIdKey) == caseId && !string.IsNullOrWhiteSpace(storedToken)
            ? storedToken
            : null;
        if (token is null)
        {
            CaseEditLease? resumed;
            try
            {
                resumed = await leases.ResumeAsync(new(caseId, actor), cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A resume that cannot be answered leaves the page reading, never unavailable.
                LogCaseCommandFailed(logger, caseId, "resume_lease", exception);
                resumed = null;
            }
            // A lease that lapsed between the read and the resume leaves Edit to claim afresh.
            ClearLeaseState();
            if (resumed is null)
            {
                return;
            }

            StoreLeaseAuthority(caseId, resumed.Token);
            token = resumed.Token;
        }

        LeaseToken = token;
        ReleaseLeaseOperationKey = GetOrCreateOperationKey(ReleaseLeaseOperationKeyName);
    }

    /// <summary>
    /// Where this page puts what it wants to say next. The workspace and its capability pages
    /// share one pair; a page whose messages are read somewhere else overrides them.
    /// </summary>
    protected virtual string StatusTempDataKey => "CaseStatus";

    protected virtual string ErrorTempDataKey => "CaseError";

    /// <summary>
    /// Enters edit mode. Every page that offers it enters it the same way. A staff member who
    /// already holds the lease, from another window or a page that went stale, resumes it rather
    /// than claiming. A refused claim leaves nothing to retry by key: if it landed after all, the
    /// page it returns to resumes the lease, and otherwise that page offers a new claim.
    /// </summary>
    protected async Task<IActionResult> ClaimLeaseAsync(
        CaseEditLeases leases,
        Guid id,
        long expectedVersion,
        string operationKey,
        bool takeOver,
        Func<IActionResult> redirect,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            ClearLeaseState();
            return Forbid();
        }

        try
        {
            var normalizedOperationKey = RequireOperationKey(operationKey);
            var lease = await leases.ResumeAsync(new(id, actor), cancellationToken)
                ?? await leases.ClaimAsync(
                    new ClaimCaseEditLeaseRequest(id, expectedVersion, actor, normalizedOperationKey)
                    {
                        TakeOver = takeOver
                    },
                    cancellationToken);
            StoreLeaseAuthority(id, lease.Token);
            TempData.Remove(ReleaseLeaseOperationKeyName);
            TempData[StatusTempDataKey] = "Edit mode is active.";
        }
        catch (StaffAuthorizationException)
        {
            ClearLeaseState();
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, "claim_lease", exception);
            if (IsLeaseLoss(exception))
            {
                ClearLeaseState();
            }
            TempData[ErrorTempDataKey] = ClaimLeaseFailureMessage(exception);
        }

        return redirect();
    }

    /// <summary>Leaves edit mode, releasing the server-owned authority rather than forgetting it.</summary>
    protected async Task<IActionResult> ReleaseLeaseAsync(
        CaseEditLeases leases,
        Guid id,
        string operationKey,
        string editLeaseToken,
        Func<IActionResult> redirect,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            ClearLeaseState();
            return Forbid();
        }

        try
        {
            await leases.ReleaseAsync(
                new(id, actor, RequireOperationKey(operationKey), editLeaseToken),
                cancellationToken);
            ClearLeaseState();
            TempData[StatusTempDataKey] = "Edit mode was left safely.";
        }
        catch (StaffAuthorizationException)
        {
            ClearLeaseState();
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, "release_lease", exception);
            if (IsLeaseLoss(exception))
            {
                ClearLeaseState();
            }
            else
            {
                StoreLeaseAuthority(id, editLeaseToken);
                TempData[ReleaseLeaseOperationKeyName] = operationKey;
            }
            TempData[ErrorTempDataKey] =
                "Edit mode could not be released. Reload the case to confirm its current state.";
        }

        return redirect();
    }

    protected string GetOrCreateOperationKey(string key)
    {
        if (PeekGuid(key) is { } operationId && operationId != Guid.Empty)
        {
            return operationId.ToString("N");
        }

        var operationKey = NewOperationKey();
        TempData[key] = operationKey;
        return operationKey;
    }

    protected static string RequireOperationKey(string value) =>
        Guid.TryParseExact(value, "N", out var operationId)
            ? operationId.ToString("N")
            : throw new ArgumentException("The operation key is invalid.", nameof(value));

    /// <summary>The refusal a command on the case itself states when Core gives no reason of its own.</summary>
    protected const string CaseCommandRefused =
        "The case action was not applied because the case changed, edit mode was lost, or the action is not permitted.";

    /// <summary>A command on the case itself; a refusal names the case as the reason.</summary>
    protected Task<IActionResult> ExecuteCaseCommandAsync(
        Guid id,
        string editLeaseToken,
        string commandName,
        Func<ActionActor, Task> execute,
        string successMessage,
        Func<Guid, RedirectToPageResult>? redirect = null,
        bool keepEditing = false) =>
        ExecuteCommandAsync(
            id,
            editLeaseToken,
            commandName,
            execute,
            successMessage,
            CaseCommandRefused,
            redirect,
            keepEditing);

    /// <summary>
    /// A command on one item the case carries (a document, an upload request); a refusal names
    /// the item as the reason.
    /// </summary>
    protected Task<IActionResult> ExecuteTransportCommandAsync(
        Guid id,
        string editLeaseToken,
        string commandName,
        Func<ActionActor, Task> execute,
        string successMessage,
        Func<Guid, RedirectToPageResult>? redirect = null,
        bool keepEditing = false) =>
        ExecuteCommandAsync(
            id,
            editLeaseToken,
            commandName,
            execute,
            successMessage,
            "The case action was not applied because the item is unavailable, changed, or not part of this case.",
            redirect,
            keepEditing);

    /// <summary>
    /// A command on the case that runs under a lease (operator, 29 September
    /// 2026): the session's when the page holds one, otherwise one claimed for
    /// this command alone, the way Generate report claims its own outside edit
    /// mode. A lease claimed here is released after the command whatever it
    /// answered (R523); one the command consumed has nothing left to release. A claim a colleague's
    /// live lease refuses states the claim's own wording, and the claimed
    /// token never reaches the browser: the page reads on afterwards. A
    /// command taken inside the session keeps it: the page reclaims the next
    /// lease, as an immediate post does, so only Done or a link away from the
    /// Case ends edit mode (operator, 9 October 2026).
    /// </summary>
    protected Task<IActionResult> ExecuteCaseCommandUnderLeaseAsync(
        Guid id,
        long expectedVersion,
        string? editLeaseToken,
        string commandName,
        Func<ActionActor, string, Task> execute,
        string successMessage,
        Func<Guid, RedirectToPageResult>? redirect = null) =>
        ExecuteCaseCommandUnderLeaseAsync(
            id,
            expectedVersion,
            editLeaseToken,
            commandName,
            async (actor, lease) =>
            {
                await execute(actor, lease);
                return successMessage;
            },
            _ => CaseCommandRefused,
            redirect);

    /// <summary>
    /// The same, for a command whose notice comes from its result (null for
    /// none) and whose refusal is worded from the exception. A request the
    /// browser abandons mid-command releases a lease claimed for it, so the
    /// Case is not held until the lease lapses.
    /// </summary>
    protected async Task<IActionResult> ExecuteCaseCommandUnderLeaseAsync(
        Guid id,
        long expectedVersion,
        string? editLeaseToken,
        string commandName,
        Func<ActionActor, string, Task<string?>> execute,
        Func<Exception, string> failureMessage,
        Func<Guid, RedirectToPageResult>? redirect = null)
    {
        if (!string.IsNullOrWhiteSpace(editLeaseToken))
        {
            return await ExecuteCommandAsync(
                id,
                editLeaseToken,
                commandName,
                actor => execute(actor, editLeaseToken),
                failureMessage,
                redirect,
                keepEditing: true);
        }

        redirect ??= RedirectToDetails;
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        var (lease, refusal) = await ClaimLeaseForCommandAsync(id, expectedVersion, actor, redirect);
        if (refusal is not null)
        {
            return refusal;
        }

        try
        {
            var message = await OneCommandEdit.RunClaimedAsync(lease!, actor, () => execute(actor, lease!.Token));
            if (message is not null)
            {
                TempData[StatusTempDataKey] = message;
            }
        }
        catch (StaffAuthorizationException)
        {
            RetainProposedValues(id);
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, commandName, exception);
            RetainProposedValues(id);
            TempData[ErrorTempDataKey] = failureMessage(exception);
        }

        return redirect(id);
    }

    /// <summary>
    /// The one helper a command that holds the Case's edit authority for itself
    /// claims, runs and releases through (R523).
    /// </summary>
    protected Pegasus.Web.Presentation.OneCommandEdit OneCommandEdit =>
        HttpContext.RequestServices.GetRequiredService<Pegasus.Web.Presentation.OneCommandEdit>();

    /// <summary>
    /// Claims the Case's lease for one command, or answers with the refusal
    /// the page should return: a claim a colleague's lease, an expired claim
    /// or a changed Case refuses is stated in the claim's own words. The
    /// token is never stored for the browser.
    /// </summary>
    protected async Task<(OneCommandEditClaim? Lease, IActionResult? Refusal)> ClaimLeaseForCommandAsync(
        Guid id,
        long expectedVersion,
        ActionActor actor,
        Func<Guid, RedirectToPageResult> redirect)
    {
        try
        {
            var lease = await OneCommandEdit.ClaimAsync(
                id, isTriageCase: false, expectedVersion, actor, NewOperationKey(), HttpContext.RequestAborted);
            return (lease, null);
        }
        catch (StaffAuthorizationException)
        {
            return (null, Forbid());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, "claim_lease", exception);
            TempData[ErrorTempDataKey] = ClaimLeaseFailureMessage(exception);
            return (null, redirect(id));
        }
    }

    /// <summary>
    /// The lease-reading pair an immediate post needs to keep the operator's
    /// edit session open after the store consumed the lease it carried (v25
    /// decision F). A page that offers such posts supplies them; a page that
    /// does not leaves them null and its commands end the session as before.
    /// The reclaim needs only the workflow's version, state and archive, so
    /// it reads the workflow row alone; the claim authorizes the actor.
    /// </summary>
    protected virtual (ICaseWorkflowQueries Workflows, CaseEditLeases Leases)? LeaseReclaim => null;

    /// <summary>
    /// Claims a fresh lease on the Case's new version and stores it, so the
    /// redirected page is still in the edit session. A refused reclaim is not
    /// an error: the page simply reads. Archived and terminal Cases are never
    /// reclaimed.
    /// </summary>
    protected async Task ReclaimLeaseAsync(Guid id, CancellationToken cancellationToken)
    {
        if (LeaseReclaim is not { } reclaim || !TryGetActor(out var actor))
        {
            return;
        }

        try
        {
            var current = await reclaim.Workflows.GetAsync(id, cancellationToken);
            if (current is null || current.Archive is not null
                || Pegasus.Core.Lifecycle.CaseLifecycleRules.IsTerminal(current.State))
            {
                return;
            }
            var operationKey = NewOperationKey();
            var lease = await reclaim.Leases.ClaimAsync(
                new(id, current.Version, actor, operationKey),
                cancellationToken);
            StoreLeaseAuthority(id, lease.Token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, "reclaim_lease", exception);
            ClearLeaseState();
        }
    }

    private Task<IActionResult> ExecuteCommandAsync(
        Guid id,
        string editLeaseToken,
        string commandName,
        Func<ActionActor, Task> execute,
        string successMessage,
        string failureMessage,
        Func<Guid, RedirectToPageResult>? redirect = null,
        bool keepEditing = false) =>
        ExecuteCommandAsync(
            id,
            editLeaseToken,
            commandName,
            async actor =>
            {
                await execute(actor);
                return successMessage;
            },
            _ => failureMessage,
            redirect,
            keepEditing);

    private async Task<IActionResult> ExecuteCommandAsync(
        Guid id,
        string editLeaseToken,
        string commandName,
        Func<ActionActor, Task<string?>> execute,
        Func<Exception, string> failureMessage,
        Func<Guid, RedirectToPageResult>? redirect,
        bool keepEditing)
    {
        redirect ??= RedirectToDetails;
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var message = await execute(actor);
            ClearLeaseState();
            if (keepEditing)
            {
                await ReclaimLeaseAsync(id, CancellationToken.None);
            }
            if (message is not null)
            {
                Say(StatusTempDataKey, message);
            }
        }
        catch (StaffAuthorizationException)
        {
            ClearLeaseState();
            RetainProposedValues(id);
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, commandName, exception);
            HandleLeaseFailure(id, editLeaseToken, exception);
            RetainProposedValues(id);
            Say(ErrorTempDataKey, failureMessage(exception));
        }

        return redirect(id);
    }

    protected RedirectToPageResult RedirectToDetails(Guid id) =>
        RedirectToPage("/Cases/Details", new { id, view = ReturnView, section = ReturnSection });

    /// <summary>
    /// The section a request returns to: the one the form posted from
    /// (its <c>section</c> field, as the Case's own forms carry), else none,
    /// the top (v36 item S, 9 October 2026: a Workflow or Closure action no
    /// longer lands the reader at the top of the record).
    /// </summary>
    protected string? ReturnSection
    {
        get
        {
            var posted = Request.HasFormContentType ? Request.Form["section"].ToString() : string.Empty;
            return string.IsNullOrWhiteSpace(posted) || string.Equals(posted, "overview", StringComparison.OrdinalIgnoreCase) ? null : posted;
        }
    }

    /// <summary>
    /// The view of the Case record a request returns to: the Inspection view
    /// when the form posted from it carries its <c>view</c> field (or a GET
    /// names it), else none, the default view (operator, 2 October 2026).
    /// </summary>
    protected string? ReturnView
    {
        get
        {
            var posted = Request.HasFormContentType ? Request.Form["view"].ToString() : string.Empty;
            return DetailsModel.ViewRouteOf(string.IsNullOrEmpty(posted) ? Request.Query["view"].ToString() : posted);
        }
    }

    /// <summary>
    /// Redirects back into the Files section's Images tab: the tag, untag, and create-tag
    /// actions live there, so the operator returns to the tile they just acted on instead of
    /// landing back on Overview with the tab strip forgotten.
    /// </summary>
    protected RedirectToPageResult RedirectToDetailsFilesImages(Guid id) =>
        RedirectToPage(
            "/Cases/Details",
            pageHandler: null,
            routeValues: new { id, section = "files", view = ReturnView },
            fragment: "case-files-images");

    /// <summary>
    /// A fragment's 404. Script asks for a fragment and reads only its status,
    /// so the status pages middleware is switched off for this response and the
    /// refusal has an empty body instead of the full status page and its reads.
    /// A full-page 404 keeps the designed page.
    /// </summary>
    protected NotFoundResult FragmentNotFound()
    {
        var statusPages = HttpContext.Features.Get<IStatusCodePagesFeature>();
        if (statusPages is not null)
        {
            statusPages.Enabled = false;
        }
        return NotFound();
    }

    /// <summary>
    /// Tells the server the editor is still here, so an open page is never timed out mid-edit.
    /// Every page that carries edit mode answers it the same way, and none of them redirect: the
    /// browser needs to know whether to keep beating, and the Case version, which system work
    /// moves under the lease, so the page can catch up with it.
    /// </summary>
    /// <remarks>
    /// It reads and writes no TempData on any path — not even to forget a lost lease. TempData
    /// here is cookie-backed, so re-issuing that cookie from a request the operator did not make
    /// can race a form post they did and lose them the token mid-edit. A refusal needs no state
    /// anyway: the page it lands on already renders the case's real edit state.
    /// </remarks>
    protected async Task<IActionResult> HeartbeatLeaseAsync(
        CaseEditLeases leases,
        Guid id,
        string editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        CaseEditLease lease;
        try
        {
            lease = await leases.HeartbeatAsync(new(id, actor, editLeaseToken), cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (IsLeaseLoss(exception))
        {
            return new StatusCodeResult(StatusCodes.Status409Conflict);
        }
        catch (KeyNotFoundException)
        {
            // The Case no longer exists (a stale tab after a wipe). The browser stops
            // beating on a 404, as it does for an expired lease.
            return FragmentNotFound();
        }

        return new JsonResult(new { version = lease.Version });
    }

    /// <summary>
    /// Asks the member of staff holding the lease to hand it over (FRD-14 §Take over, R550). The
    /// page the requester lands on, or redraws, reads the request's state from the Case frame, so
    /// nothing is kept in TempData. An accepted request sent again hands the page its lease; a
    /// free Case, and the caller's own lease, are claimed or resumed by the page as usual.
    /// </summary>
    protected async Task<IActionResult> RequestEditAsync(
        CaseEditLeases leases,
        Guid id,
        long expectedVersion,
        string operationKey,
        Func<IActionResult> redirect,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        string state;
        try
        {
            var raised = await leases.RaiseRequestAsync(
                new(id, expectedVersion, actor, RequireOperationKey(operationKey)),
                cancellationToken);
            state = raised.Outcome switch
            {
                CaseEditRequestOutcome.Pending => "pending",
                CaseEditRequestOutcome.Free => "free",
                CaseEditRequestOutcome.Held => "held",
                CaseEditRequestOutcome.AutomationHeld => "automation-held",
                CaseEditRequestOutcome.Accepted => "accepted",
                _ => throw new InvalidDataException($"An unknown request outcome '{raised.Outcome}' was answered.")
            };
            if (raised.Lease is { } lease)
            {
                StoreLeaseAuthority(id, lease.Token);
                TempData.Remove(ReleaseLeaseOperationKeyName);
            }
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (CaseEditRequestRefusedException exception)
        {
            state = exception.Refusal == CaseEditRequestRefusal.AlreadyAsked ? "already-asked" : "declined";
        }
        catch (KeyNotFoundException)
        {
            return FragmentNotFound();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, "request_edit", exception);
            state = "refused";
        }

        return IsScriptRequest ? new JsonResult(new { state }) : redirect();
    }

    /// <summary>
    /// The holder's Accept or Deny, or the requester's withdrawal (FRD-14 §Take over, R553).
    /// Accept hands the lease over, so the holder's page forgets the authority it held and
    /// redraws read-only; nothing is said in TempData, because the ribbon then names who is
    /// editing. A withdrawal is beacon-safe: it answers 204 whatever it found.
    /// </summary>
    protected async Task<IActionResult> AnswerEditRequestAsync(
        CaseEditLeases leases,
        Guid id,
        Guid requestId,
        string? answer,
        string? editLeaseToken,
        Func<IActionResult> redirect,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        var parsed = answer?.Trim().ToLowerInvariant() switch
        {
            "accept" => CaseEditRequestAnswer.Accepted,
            "decline" => CaseEditRequestAnswer.Declined,
            "withdraw" => CaseEditRequestAnswer.Withdrawn,
            _ => (CaseEditRequestAnswer?)null
        };
        if (parsed is not { } parsedAnswer || id == Guid.Empty || requestId == Guid.Empty)
        {
            return BadRequest();
        }

        try
        {
            await leases.AnswerRequestAsync(
                new(id, actor, requestId, parsedAnswer, editLeaseToken),
                cancellationToken);
            if (parsedAnswer == CaseEditRequestAnswer.Accepted)
            {
                ClearLeaseState();
            }
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return FragmentNotFound();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (parsedAnswer == CaseEditRequestAnswer.Withdrawn)
            {
                // The request had already been answered or ended; there is nothing to withdraw.
                return new NoContentResult();
            }
            LogCaseCommandFailed(logger, id, "answer_edit_request", exception);
            if (IsLeaseLoss(exception))
            {
                ClearLeaseState();
            }
            return IsScriptRequest
                ? new StatusCodeResult(StatusCodes.Status409Conflict)
                : redirect();
        }

        return IsScriptRequest ? new NoContentResult() : redirect();
    }

    /// <summary>
    /// The long poll (R551): answers at once when the Case's lease or request differs from the
    /// stamp the page last saw, otherwise holds on the Case's signal for up to
    /// <see cref="CaseEditRequestWatch.MaximumHold"/> and answers the Case as it stands. A page
    /// that has gone (the request aborted) gets nothing; a host that is stopping answers at once
    /// so the restart is not held up. No TempData on any path, as for the heartbeat.
    /// </summary>
    protected async Task<IActionResult> WatchEditRequestAsync(
        CaseEditRequestWatch watch,
        IDescribeCaseEditAuthorityHolder describeHolder,
        IHostApplicationLifetime hostLifetime,
        Guid id,
        string? stamp,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (id == Guid.Empty)
        {
            return BadRequest();
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, hostLifetime.ApplicationStopping);
        CaseEditRequestView view;
        try
        {
            view = await watch.WaitAsync(
                id,
                actor,
                string.IsNullOrWhiteSpace(stamp) ? null : stamp,
                CaseEditRequestWatch.MaximumHold,
                linked.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (OperationCanceledException)
        {
            // The host is stopping: the page asks again as soon as it is back.
            view = await watch.WaitAsync(id, actor, null, TimeSpan.Zero, CancellationToken.None);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return FragmentNotFound();
        }

        object? holder = null;
        if (view.HolderIsLive && view.Holder is { } holderSubject)
        {
            var isViewer = CaseEditAuthority.IsHolder(view.HolderKind, holderSubject, actor);
            var named = isViewer
                ? null
                : await describeHolder.ExecuteAsync(view.HolderKind, holderSubject, actor, cancellationToken);
            holder = new
            {
                name = named is null ? null : EditModeDisplay.HolderName(named),
                isAi = view.HolderKind == ActorKind.Automation,
                isViewer
            };
        }

        object? request = null;
        if (view.Request is { } standing)
        {
            var isViewer = CaseEditAuthority.IsRequester(standing, actor);
            var named = isViewer
                ? null
                : await describeHolder.ExecuteAsync(standing.RequesterKind, standing.Requester, actor, cancellationToken);
            request = new
            {
                id = standing.Id,
                requester = named is null ? null : EditModeDisplay.HolderName(named),
                isAi = standing.RequesterKind == ActorKind.Automation,
                isViewer,
                state = standing.Answer switch
                {
                    CaseEditRequestAnswer.Accepted => "accepted",
                    CaseEditRequestAnswer.Declined => "declined",
                    CaseEditRequestAnswer.Withdrawn => "withdrawn",
                    _ => view.RequestIsPending ? "pending" : "unanswered"
                },
                answerBy = standing.RequestedAtUtc + CaseEditAuthority.RequestAnswerWindow
            };
        }

        return new JsonResult(new
        {
            stamp = view.Stamp,
            version = view.Version,
            holder,
            request
        });
    }

    protected void StoreLeaseAuthority(Guid caseId, string leaseToken)
    {
        if (string.IsNullOrWhiteSpace(leaseToken))
        {
            return;
        }

        TempData[LeaseCaseIdKey] = caseId;
        TempData[LeaseTokenKey] = new[] { leaseToken };
    }

    /// <summary>
    /// Carries the refused form's own submitted values through the post-redirect-get so the editor
    /// can compare them with the reloaded case. No lease token, version, or case identifier beyond
    /// the route value is retained, and an oversized payload is reported rather than discarded.
    /// </summary>
    protected void RetainProposedValues(Guid caseId)
    {
        // An answer drawn in place leaves the operator's typed values in the page itself.
        if (AnswersInPlace || !Request.HasFormContentType)
        {
            return;
        }

        var wasShortened = false;
        var submitted = Request.Form
            .Where(field => RetainableFormFields.Contains(field.Key) || EditorLabels.Label(field.Key) is not null)
            .Select(field => new
            {
                field.Key,
                Value = string.Join(", ", field.Value.Where(value => !string.IsNullOrWhiteSpace(value)))
            })
            .Where(field => field.Key == "signOffEngineerId" || !Guid.TryParse(field.Value, out _))
            .Select(field =>
            {
                if (field.Value.Length <= MaximumRetainedProposedValueCharacters)
                {
                    return new RetainedProposedValue(field.Key, field.Value);
                }

                wasShortened = true;
                return new RetainedProposedValue(
                    field.Key,
                    field.Value[..MaximumRetainedProposedValueCharacters]);
            })
            .ToArray();
        if (submitted.Length == 0)
        {
            return;
        }

        TempData[ProposedValuesCaseIdKey] = caseId;
        var payload = JsonSerializer.Serialize(submitted);
        if (payload.Length > MaximumRetainedProposedCharacters)
        {
            TempData.Remove(ProposedValuesKey);
            TempData.Remove(ProposedValuesShortenedKey);
            TempData[ProposedValuesDroppedKey] = true;
            return;
        }

        TempData.Remove(ProposedValuesDroppedKey);
        TempData[ProposedValuesShortenedKey] = wasShortened;
        TempData[ProposedValuesKey] = payload;
    }

    protected void HandleLeaseFailure(Guid caseId, string? editLeaseToken, Exception exception)
    {
        if (RequiresReacquisition(exception))
        {
            ClearLeaseState();
        }
        else
        {
            PreserveLeaseState(caseId, editLeaseToken);
        }
    }

    protected void PreserveLeaseState(Guid caseId, string? editLeaseToken)
    {
        if (!string.IsNullOrWhiteSpace(editLeaseToken))
        {
            StoreLeaseAuthority(caseId, editLeaseToken);
        }
    }

    // TempData materializes Guid-shaped strings as Guid values; the token array keeps opaque tokens textual.
    protected string? PeekLeaseToken() =>
        TempData.Peek(LeaseTokenKey) switch
        {
            string token => token,
            string[] { Length: 1 } tokens => tokens[0],
            _ => null
        };

    protected Guid? PeekGuid(string key) =>
        TempData.Peek(key) switch
        {
            Guid value => value,
            string text when Guid.TryParse(text, out var value) => value,
            _ => null
        };

    /// <summary>Forgets the lease authority this browser carries.</summary>
    protected void ClearLeaseState()
    {
        TempData.Remove(LeaseTokenKey);
        TempData.Remove(LeaseCaseIdKey);
        TempData.Remove(ReleaseLeaseOperationKeyName);
    }

    /// <summary>The lease itself is gone: it expired, or another actor holds it.</summary>
    protected static bool IsLeaseLoss(Exception exception) =>
        exception is CaseEditLeaseExpiredException or CaseEditLeaseConflictException;

    /// <summary>
    /// The claim store raises a lease conflict only after it has found a live edit lease for the
    /// Case. Other failures must not be presented as another editor: a stale version and a
    /// transient fault have different recovery paths and the latter supplies no evidence of a
    /// competing staff member.
    /// </summary>
    protected static string ClaimLeaseFailureMessage(Exception exception) => exception switch
    {
        CaseEditLeaseConflictException =>
            "Someone else is editing this case. Reload to see who is editing it.",
        CaseEditLeaseExpiredException =>
            "Edit mode could not be entered because the previous edit attempt expired. Reload the case and try again.",
        CaseVersionConflictException =>
            "Edit mode could not be entered because the case changed. Reload the case and try again.",
        _ =>
            "Edit mode could not be entered. Retry the same request, and if it continues, reload the case or contact support."
    };

    /// <summary>
    /// The refused mutations after which the editor must reacquire rather than resubmit. A lost
    /// lease is one; so is a stale version, because the requirement makes the rejected editor
    /// "reload and reacquire rather than merge or force the save". Clearing this page's lease state
    /// does not release the server-owned authority, so a holder who did nothing wrong keeps it: the
    /// reloaded page resumes it on the case as it now stands, and nothing is saved over newer work.
    /// </summary>
    private static bool RequiresReacquisition(Exception exception) =>
        IsLeaseLoss(exception) || exception is CaseVersionConflictException;

    /// <summary>
    /// Logs a failed case command. A designed refusal is a Warning naming the
    /// refusal type and its message only: an exception object would reach the
    /// exception index and page the on-call alert for an outcome the operator was
    /// meant to see. Anything else keeps its full payload.
    /// </summary>
    protected static void LogCaseCommandFailed(
        ILogger logger,
        Guid caseId,
        string commandName,
        Exception exception)
    {
        if (DesignedCaseRefusal.Is(exception))
        {
            LogCaseCommandRefused(logger, caseId, commandName, exception.GetType().Name, exception.Message);
            return;
        }

        LogCaseCommandUnexpectedFailure(logger, caseId, commandName, exception);
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Case command {CommandName} refused for case {CaseId}: {RefusalType}: {RefusalMessage}")]
    private static partial void LogCaseCommandRefused(
        ILogger logger,
        Guid caseId,
        string commandName,
        string refusalType,
        string refusalMessage);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Case command {CommandName} failed for case {CaseId}.")]
    private static partial void LogCaseCommandUnexpectedFailure(
        ILogger logger,
        Guid caseId,
        string commandName,
        Exception exception);

    protected sealed record RetainedProposedValue(string Field, string Value);
}
