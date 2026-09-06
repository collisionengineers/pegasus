using System.Collections.Frozen;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

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
    protected const string ClaimLeaseOperationKeyName = "CaseClaimLeaseOperationKey";
    protected const string ClaimLeaseCaseIdKey = "CaseClaimLeaseCaseId";
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
        "claimNumber",
        "vehicleRegistration",
        "vehicleMake",
        "vehicleModel",
        "vehicleMileage",
        "vehicleMileageUnit",
        "accidentCircumstances",
        "incidentDate",
        "contactName",
        "contactEmailAddress",
        "contactPhoneNumber",
        "instructionDate",
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

    public string ClaimLeaseOperationKey { get; private set; } = NewOperationKey();

    public string ReleaseLeaseOperationKey { get; private set; } = NewOperationKey();

    /// <summary>
    /// The case is held by this viewer, but this browser no longer carries the token — the holder
    /// re-enters edit mode deliberately rather than having it silently restored.
    /// </summary>
    public bool CanRecoverLease { get; private set; }

    /// <summary>
    /// Reconciles what this browser remembers against what the server says the case's edit
    /// authority actually is. Every page that renders edit mode asks it, so the workspace and the
    /// assessment agree about one lease without keeping two rules.
    /// </summary>
    protected void RestoreLeaseState(
        Guid caseId,
        ActionActor actor,
        CaseEditLeaseSnapshot? activeLease)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // An expired lease is already absent from the projection, so no page keeps a second rule.
        if (activeLease is null)
        {
            if (!string.IsNullOrWhiteSpace(PeekLeaseToken())
                || PeekGuid(LeaseCaseIdKey) is not null)
            {
                ClearLeaseState();
            }

            ClaimLeaseOperationKey = GetOrCreateClaimLeaseOperation(caseId);
            return;
        }

        if (!CaseEditAuthority.IsHolder(activeLease.HolderKind, activeLease.Holder, actor))
        {
            ClearLeaseState();
            return;
        }

        if (!Guid.TryParseExact(activeLease.OperationKey, "N", out var claimOperationId))
        {
            ClearLeaseState();
            return;
        }

        ClaimLeaseOperationKey = claimOperationId.ToString("N");
        StoreClaimLeaseOperation(caseId, ClaimLeaseOperationKey);
        var storedToken = PeekLeaseToken();
        if (PeekGuid(LeaseCaseIdKey) == caseId && !string.IsNullOrWhiteSpace(storedToken))
        {
            LeaseToken = storedToken;
            ReleaseLeaseOperationKey = GetOrCreateOperationKey(ReleaseLeaseOperationKeyName);
            return;
        }

        ClearLeaseAuthority();
        CanRecoverLease = true;
    }

    /// <summary>
    /// Where this page puts what it wants to say next. The workspace and its capability pages
    /// share one pair; a page whose messages are read somewhere else overrides them.
    /// </summary>
    protected virtual string StatusTempDataKey => "CaseStatus";

    protected virtual string ErrorTempDataKey => "CaseError";

    /// <summary>
    /// Enters edit mode. Every page that offers it enters it the same way, including what happens
    /// to the claim key when the claim is refused: a lost lease clears this page's state, and any
    /// other refusal keeps the same key, because the claim is idempotent by that key and a retry
    /// must replay rather than claim twice.
    /// </summary>
    protected async Task<IActionResult> ClaimLeaseAsync(
        IAcquireCaseEditLease acquireLease,
        Guid id,
        long expectedVersion,
        string operationKey,
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
            var lease = await acquireLease.ExecuteAsync(
                new(id, expectedVersion, actor, normalizedOperationKey),
                cancellationToken);
            StoreClaimLeaseOperation(id, normalizedOperationKey);
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
            else if (Guid.TryParseExact(operationKey, "N", out var operationId))
            {
                StoreClaimLeaseOperation(id, operationId.ToString("N"));
            }
            TempData[ErrorTempDataKey] =
                "Edit mode could not be entered because the case changed or is being edited by another member of staff.";
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

    protected string GetOrCreateClaimLeaseOperation(Guid caseId)
    {
        var storedOperationId = PeekGuid(ClaimLeaseOperationKeyName);
        if (PeekGuid(ClaimLeaseCaseIdKey) == caseId
            && storedOperationId is { } operationId
            && operationId != Guid.Empty)
        {
            return operationId.ToString("N");
        }

        ClearLeaseState();
        var operationKey = NewOperationKey();
        StoreClaimLeaseOperation(caseId, operationKey);
        return operationKey;
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

    protected void StoreClaimLeaseOperation(Guid caseId, string operationKey)
    {
        TempData[ClaimLeaseCaseIdKey] = caseId;
        TempData[ClaimLeaseOperationKeyName] = Guid.ParseExact(operationKey, "N");
    }

    protected static string RequireOperationKey(string value) =>
        Guid.TryParseExact(value, "N", out var operationId)
            ? operationId.ToString("N")
            : throw new ArgumentException("The operation key is invalid.", nameof(value));

    /// <summary>A command on the case itself; a refusal names the case as the reason.</summary>
    protected Task<IActionResult> ExecuteCaseCommandAsync(
        Guid id,
        string editLeaseToken,
        string commandName,
        Func<ActionActor, Task> execute,
        string successMessage) =>
        ExecuteCommandAsync(
            id,
            editLeaseToken,
            commandName,
            execute,
            successMessage,
            "The case action was not applied because the case changed, edit mode was lost, or the action is not permitted.");

    /// <summary>
    /// A command on one item the case carries (a document, an upload request); a refusal names
    /// the item as the reason.
    /// </summary>
    protected Task<IActionResult> ExecuteTransportCommandAsync(
        Guid id,
        string editLeaseToken,
        string commandName,
        Func<ActionActor, Task> execute,
        string successMessage) =>
        ExecuteCommandAsync(
            id,
            editLeaseToken,
            commandName,
            execute,
            successMessage,
            "The case action was not applied because the item is unavailable, changed, or not part of this case.");

    private async Task<IActionResult> ExecuteCommandAsync(
        Guid id,
        string editLeaseToken,
        string commandName,
        Func<ActionActor, Task> execute,
        string successMessage,
        string failureMessage)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            await execute(actor);
            ClearLeaseState();
            TempData[StatusTempDataKey] = successMessage;
        }
        catch (StaffAuthorizationException)
        {
            ClearLeaseState();
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, commandName, exception);
            HandleLeaseFailure(id, editLeaseToken, exception);
            RetainProposedValues(id);
            TempData[ErrorTempDataKey] = failureMessage;
        }

        return RedirectToDetails(id);
    }

    protected RedirectToPageResult RedirectToDetails(Guid id) =>
        RedirectToPage("/Cases/Details", new { id });

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
            .Where(field => RetainableFormFields.Contains(field.Key))
            .Select(field => new
            {
                field.Key,
                // A checked box posts "true" followed by its hidden "false"; the model binder reads
                // the first entry, so retention reads the first entry too rather than joining both.
                Value = BooleanFormFields.Contains(field.Key)
                    ? field.Value.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty
                    : string.Join(", ", field.Value.Where(value => !string.IsNullOrWhiteSpace(value)))
            })
            .Where(field => !string.IsNullOrWhiteSpace(field.Value)
                && !Guid.TryParse(field.Value, out _))
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
    protected void ClearLeaseAuthority()
    {
        TempData.Remove(LeaseTokenKey);
        TempData.Remove(LeaseCaseIdKey);
        TempData.Remove(RenewLeaseOperationKeyName);
        TempData.Remove(ReleaseLeaseOperationKeyName);
    }

    protected void ClearLeaseState()
    {
        ClearLeaseAuthority();
        TempData.Remove(ClaimLeaseOperationKeyName);
        TempData.Remove(ClaimLeaseCaseIdKey);
    }

    /// <summary>The lease itself is gone: it expired, or another actor holds it.</summary>
    protected static bool IsLeaseLoss(Exception exception) =>
        exception is CaseEditLeaseExpiredException or CaseEditLeaseConflictException;

    /// <summary>
    /// The refused mutations after which the editor must reacquire rather than resubmit. A lost
    /// lease is one; so is a stale version, because the requirement makes the rejected editor
    /// "reload and reacquire rather than merge or force the save". Clearing this page's lease state
    /// does not release the server-owned authority, so a holder who did nothing wrong keeps it and
    /// simply re-enters edit mode deliberately rather than saving over newer work.
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

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Case command {CommandName} failed for case {CaseId}.")]
    protected static partial void LogCaseCommandFailed(
        ILogger logger,
        Guid caseId,
        string commandName,
        Exception exception);

    protected sealed record RetainedProposedValue(string Field, string Value);
}
