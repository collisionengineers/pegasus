using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.ImageIntake;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class DetailsModel(
    IVrmSuggestionStore vrmSuggestionStore,
    IImageIntakeCaseCandidates imageIntakeCaseCandidates,
    IImageIntakeStore imageIntakeStore,
    ITriageQueries triageQueries,
    IEditScopeLeases editScopes,
    IDescribeCaseEditAuthorityHolder describeEditAuthorityHolder) : StaffPageModel
{
    public ImageIntakeDetail Detail { get; private set; } = null!;

    public IReadOnlyList<ImageIntakeLifecycleEvent> History { get; private set; } = [];

    public IReadOnlyList<ImageIntakeImage> Images { get; private set; } = [];

    public IReadOnlyList<ImageVrmSuggestion> Suggestions { get; private set; } = [];

    public IReadOnlyList<ImageIntakeCaseCandidate> AssociationCandidates { get; private set; } = [];

    public IReadOnlyList<Principal> PrincipalOptions { get; private set; } = [];

    /// <summary>
    /// The Triage opened from this record's origin receipt, if any — the same
    /// receipt-keyed lookup <c>Pegasus.Web.Pages.Intake.DetailsModel</c> uses
    /// (<see cref="ITriageQueries.GetByOriginReceiptAsync"/>), so this page can
    /// link straight to the disposition/assignment controls that already live
    /// on the Triage page.
    /// </summary>
    public TriageSummary? Triage { get; private set; }

    public EditScopeLease? EditLease { get; private set; }

    public bool IsEditing => EditLease is not null;

    /// <summary>
    /// Set when this operator is already editing this record in another window,
    /// so the page offers the take-over that ends the other window's claim
    /// instead of an Edit that would be refused again.
    /// </summary>
    public bool CanTakeOverEdit { get; private set; }

    /// <summary>The record as the operator reading an ownership sentence names it.</summary>
    private const string RecordName = "Image Intake record";

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var detail = await imageIntakeStore.GetAsync(id, cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        Detail = detail;
        Images = await imageIntakeStore.ListImagesAsync(id, cancellationToken);
        History = await imageIntakeStore.ListHistoryAsync(id, cancellationToken);
        Suggestions = await vrmSuggestionStore.ListForReceiptAsync(
            detail.Record.Origin.ReceiptId,
            cancellationToken);
        AssociationCandidates = detail.AssociatedCaseId is null
            ? await imageIntakeCaseCandidates.FindEligibleByRegistrationAsync(
                detail.Record.NormalizedVehicleRegistration,
                cancellationToken)
            : [];
        PrincipalOptions = await imageIntakeStore.ListActivePrincipalsAsync(cancellationToken);
        Triage = await triageQueries.GetByOriginReceiptAsync(
            detail.Record.Origin.ReceiptId,
            cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostPrincipalAsync(
        Guid id,
        Guid? principalId,
        long expectedVersion,
        string editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return await OnGetAsync(id, cancellationToken);
        }

        try
        {
            await imageIntakeStore.SetPrincipalAsync(
                new(id, principalId, actor, expectedVersion)
                {
                    EditLeaseToken = editLeaseToken
                },
                cancellationToken);
            return RedirectToPage(new { id });
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError("principalId", exception.Message);
            return await OnGetAsync(id, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(
                string.Empty,
                "This Image Intake changed while you were working. Reload and try again.");
            return await OnGetAsync(id, cancellationToken);
        }
        catch (EditScopeConflictException)
        {
            ModelState.AddModelError(
                string.Empty,
                await EditConflictMessageAsync(id, actor, cancellationToken));
            return await OnGetAsync(id, cancellationToken);
        }
        catch (EditScopeExpiredException)
        {
            ModelState.AddModelError(
                string.Empty,
                "Editing expired before this change was saved. Reload and try again.");
            return await OnGetAsync(id, cancellationToken);
        }
        catch (EditScopeVersionConflictException)
        {
            ModelState.AddModelError(
                string.Empty,
                "This Image Intake changed while you were working. Reload and try again.");
            return await OnGetAsync(id, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await OnGetAsync(id, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostCloseAsync(
        Guid id,
        long expectedVersion,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            await imageIntakeStore.CloseAsync(
                new(
                    id,
                    actor,
                    $"image-intake-staff-close:{id:N}:{expectedVersion}",
                    reason,
                    expectedVersion)
                {
                    EditLeaseToken = editLeaseToken
                },
                cancellationToken);
            return RedirectToPage(new { id });
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError("reason", exception.Message);
            return await OnGetAsync(id, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(
                string.Empty,
                "This Image Intake changed while you were working. Reload and try again.");
            return await OnGetAsync(id, cancellationToken);
        }
        catch (EditScopeConflictException)
        {
            ModelState.AddModelError(
                string.Empty,
                await EditConflictMessageAsync(id, actor, cancellationToken));
            return await OnGetAsync(id, cancellationToken);
        }
        catch (EditScopeExpiredException)
        {
            ModelState.AddModelError(
                string.Empty,
                "Editing expired before this change was saved. Reload and try again.");
            return await OnGetAsync(id, cancellationToken);
        }
        catch (EditScopeVersionConflictException)
        {
            ModelState.AddModelError(
                string.Empty,
                "This Image Intake changed while you were working. Reload and try again.");
            return await OnGetAsync(id, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await OnGetAsync(id, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostEditAsync(
        Guid id,
        long expectedVersion,
        bool takeOver,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            EditLease = await editScopes.ClaimAsync(
                new(
                    EditScopeKind.ImageIntake,
                    id,
                    expectedVersion,
                    actor,
                    $"image-intake-edit:{Guid.NewGuid():N}")
                {
                    TakeOver = takeOver
                },
                cancellationToken);
            await OnGetAsync(id, cancellationToken);
            return Page();
        }
        catch (EditScopeHeldElsewhereException)
        {
            CanTakeOverEdit = true;
            ModelState.AddModelError(string.Empty, EditModeDisplay.HeldElsewhere(RecordName));
        }
        catch (EditScopeConflictException)
        {
            ModelState.AddModelError(
                string.Empty,
                await EditConflictMessageAsync(id, actor, cancellationToken));
        }
        catch (EditScopeVersionConflictException)
        {
            ModelState.AddModelError(
                string.Empty,
                "This Image Intake changed while you were working. Reload and try again.");
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }

        return await OnGetAsync(id, cancellationToken);
    }

    public async Task<IActionResult> OnPostCancelEditAsync(
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
            await editScopes.ReleaseAsync(
                new(
                    EditScopeKind.ImageIntake,
                    id,
                    actor,
                    $"image-intake-edit-release:{Guid.NewGuid():N}",
                    editLeaseToken),
                cancellationToken);
        }
        catch (EditScopeExpiredException)
        {
            // The scope is already unusable. A read-only reload is the correct
            // cancellation result and commits no record mutation.
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostHeartbeatEditAsync(
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
            await editScopes.HeartbeatAsync(
                new(EditScopeKind.ImageIntake, id, actor, editLeaseToken),
                cancellationToken);
            return new OkResult();
        }
        catch (EditScopeExpiredException)
        {
            return new ConflictObjectResult("Editing this Image Intake record has ended. Reload it before making further changes.");
        }
    }

    private async Task<string> EditConflictMessageAsync(
        Guid imageIntakeId,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        var active = await editScopes.GetActiveAsync(
            EditScopeKind.ImageIntake, imageIntakeId, actor, cancellationToken);
        if (active is null)
        {
            return "Another member of staff is editing this Image Intake. Reload to try again.";
        }

        var isSelf = EditScopeAuthority.IsHolder(active.HolderKind, active.Holder, actor);
        var holder = isSelf
            ? CaseEditAuthorityHolder.Unnamed
            : await describeEditAuthorityHolder.ExecuteAsync(
                active.HolderKind,
                active.Holder,
                actor,
                cancellationToken);
        return EditModeDisplay.HeldBy(RecordName, holder, isSelf);
    }

    /// <summary>
    /// The release a leaving page beacons. It is not an operator action: it
    /// answers 204 whether or not a scope was still there to release, so a
    /// duplicate beacon and a beacon that lost a race with Cancel are both
    /// ordinary outcomes. Antiforgery is validated as it is for every post.
    /// </summary>
    public async Task<IActionResult> OnPostReleaseScopeBeaconAsync(
        Guid id,
        string? editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(editLeaseToken))
        {
            return new NoContentResult();
        }

        try
        {
            await editScopes.ReleaseAsync(
                new(
                    EditScopeKind.ImageIntake,
                    id,
                    actor,
                    $"image-intake-edit-beacon:{Guid.NewGuid():N}",
                    editLeaseToken),
                cancellationToken);
        }
        catch (Exception exception)
            when (exception is EditScopeExpiredException or EditScopeConflictException)
        {
            // The scope has already gone or has already been re-claimed by a
            // newer window of this operator's own session.
        }
        return new NoContentResult();
    }
}
