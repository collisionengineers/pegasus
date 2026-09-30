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
    protected const string RenewLeaseOperationKeyName = "CaseRenewLeaseOperationKey";
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
        "vehicleMileageSource",
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
        "evidenceReference",
        "artifactIdentity",
        "replacementPrincipalCode",
        "semanticRole",
        "expiresAtUtc",
        "instructionComplete",
        "imagesComplete",
        "instructionsComplete"
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// The retainable fields posted by a checkbox. Each carries a trailing hidden false, so an
    /// unchecked box still submits and a proposed "no" survives the refusal instead of vanishing.
    /// </summary>
    protected static readonly FrozenSet<string> BooleanFormFields = new[]
    {
        "instructionComplete",
        "imagesComplete",
        "instructionsComplete"
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The lease this browser holds on the case being rendered, if it holds one.</summary>
    public string? LeaseToken { get; private set; }

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
        IResumeCaseEditLease resumeLease,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(resumeLease);

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
                resumed = await resumeLease.ExecuteAsync(new(caseId, actor), cancellationToken);
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
        IAcquireCaseEditLease acquireLease,
        IResumeCaseEditLease resumeLease,
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
            var lease = await resumeLease.ExecuteAsync(new(id, actor), cancellationToken)
                ?? await acquireLease.ExecuteAsync(
                    new ClaimCaseEditLeaseRequest(id, expectedVersion, actor, normalizedOperationKey)
                    {
                        TakeOver = takeOver
                    },
                    cancellationToken);
            StoreLeaseAuthority(id, lease.Token);
            TempData.Remove(RenewLeaseOperationKeyName);
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
        IReleaseCaseEditLease releaseLease,
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
            await releaseLease.ExecuteAsync(
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
    /// mode. The command consumes the lease it runs under, so a lease claimed
    /// here is released only when the command refused. A claim a colleague's
    /// live lease refuses states the claim's own wording, and the claimed
    /// token never reaches the browser: the page reads on afterwards.
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
                keepEditing: false);
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
            var message = await execute(actor, lease!.Token);
            if (message is not null)
            {
                TempData[StatusTempDataKey] = message;
            }
        }
        catch (StaffAuthorizationException)
        {
            await ReleaseCommandLeaseQuietlyAsync(id, actor, lease!);
            RetainProposedValues(id);
            return Forbid();
        }
        catch (OperationCanceledException)
        {
            await ReleaseCommandLeaseQuietlyAsync(id, actor, lease!);
            throw;
        }
        catch (Exception exception)
        {
            LogCaseCommandFailed(logger, id, commandName, exception);
            await ReleaseCommandLeaseQuietlyAsync(id, actor, lease!);
            RetainProposedValues(id);
            TempData[ErrorTempDataKey] = failureMessage(exception);
        }

        return redirect(id);
    }

    /// <summary>
    /// Claims the Case's lease for one command, or answers with the refusal
    /// the page should return: a claim a colleague's lease, an expired claim
    /// or a changed Case refuses is stated in the claim's own words. The
    /// token is never stored for the browser.
    /// </summary>
    protected async Task<(CaseEditLease? Lease, IActionResult? Refusal)> ClaimLeaseForCommandAsync(
        Guid id,
        long expectedVersion,
        ActionActor actor,
        Func<Guid, RedirectToPageResult> redirect)
    {
        var acquire = HttpContext.RequestServices.GetRequiredService<IAcquireCaseEditLease>();
        try
        {
            var lease = await acquire.ExecuteAsync(
                new ClaimCaseEditLeaseRequest(id, expectedVersion, actor, NewOperationKey()),
                HttpContext.RequestAborted);
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
    /// Frees a lease claimed for a command the Case refused; a command that
    /// ran consumed it, so there is nothing to release then.
    /// </summary>
    protected Task ReleaseCommandLeaseQuietlyAsync(Guid id, ActionActor actor, CaseEditLease lease) =>
        Pegasus.Web.Presentation.CaseEditLeaseRelease.ReleaseQuietlyAsync(
            HttpContext.RequestServices.GetRequiredService<IReleaseCaseEditLease>(),
            logger,
            id,
            actor,
            lease);

    /// <summary>
    /// The lease-reading pair an immediate post needs to keep the operator's
    /// edit session open after the store consumed the lease it carried (v25
    /// decision F). A page that offers such posts supplies them; a page that
    /// does not leaves them null and its commands end the session as before.
    /// </summary>
    protected virtual (IGetCaseEditBasis Cases, IAcquireCaseEditLease Leases)? LeaseReclaim => null;

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
            var current = await reclaim.Cases.ExecuteAsync(new(id, actor), cancellationToken);
            if (current is null || current.Workflow.Archive is not null
                || Pegasus.Core.Lifecycle.CaseLifecycleRules.IsTerminal(current.Workflow.State))
            {
                return;
            }
            var operationKey = NewOperationKey();
            var lease = await reclaim.Leases.ExecuteAsync(
                new(id, current.Workflow.Version, actor, operationKey),
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
                TempData[StatusTempDataKey] = message;
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
            TempData[ErrorTempDataKey] = failureMessage(exception);
        }

        return redirect(id);
    }

    protected RedirectToPageResult RedirectToDetails(Guid id) =>
        RedirectToPage("/Cases/Details", new { id });

    /// <summary>
    /// Redirects back into the Files section's Images tab: the tag, untag, and create-tag
    /// actions live there, so the operator returns to the tile they just acted on instead of
    /// landing back on Overview with the tab strip forgotten.
    /// </summary>
    protected RedirectToPageResult RedirectToDetailsFilesImages(Guid id) =>
        RedirectToPage(
            "/Cases/Details",
            pageHandler: null,
            routeValues: new { id, section = "files" },
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
    /// browser only needs to know whether to keep beating.
    /// </summary>
    /// <remarks>
    /// It reads and writes no TempData on any path — not even to forget a lost lease. TempData
    /// here is cookie-backed, so re-issuing that cookie from a request the operator did not make
    /// can race a form post they did and lose them the token mid-edit. A refusal needs no state
    /// anyway: the page it lands on already renders the case's real edit state.
    /// </remarks>
    protected async Task<IActionResult> HeartbeatLeaseAsync(
        IHeartbeatCaseEditLease heartbeat,
        Guid id,
        string editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            await heartbeat.ExecuteAsync(new(id, actor, editLeaseToken), cancellationToken);
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

        return new StatusCodeResult(StatusCodes.Status204NoContent);
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
        if (!Request.HasFormContentType)
        {
            return;
        }

        var wasShortened = false;
        var submitted = Request.Form
            .Where(field => RetainableFormFields.Contains(field.Key) || EditorLabels.Label(field.Key) is not null)
            .Select(field => new
            {
                field.Key,
                // A checked box posts "true" followed by its hidden "false"; the model binder reads
                // the first entry, so retention reads the first entry too rather than joining both.
                Value = BooleanFormFields.Contains(field.Key)
                    ? field.Value.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty
                    : string.Join(", ", field.Value.Where(value => !string.IsNullOrWhiteSpace(value)))
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
        TempData.Remove(RenewLeaseOperationKeyName);
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

    protected static CaseReadinessEvidence Readiness(
        bool instructionsComplete,
        bool imagesComplete,
        string evidenceReference) =>
        new(
            instructionsComplete,
            imagesComplete,
            evidenceReference);

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
