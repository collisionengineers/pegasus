using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;

namespace Pegasus.Web.Pages.ImageIntake;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class DetailsModel(
    IVrmSuggestionStore vrmSuggestionStore,
    IImageIntakeCaseCandidates imageIntakeCaseCandidates,
    IImageIntakeStore imageIntakeStore) : StaffPageModel
{
    public ImageIntakeDetail Detail { get; private set; } = null!;

    public IReadOnlyList<ImageIntakeLifecycleEvent> History { get; private set; } = [];

    public IReadOnlyList<ImageIntakeImage> Images { get; private set; } = [];

    public IReadOnlyList<ImageVrmSuggestion> Suggestions { get; private set; } = [];

    public IReadOnlyList<ImageIntakeCaseCandidate> AssociationCandidates { get; private set; } = [];

    public IReadOnlyList<Principal> PrincipalOptions { get; private set; } = [];

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
        return Page();
    }

    public async Task<IActionResult> OnPostPrincipalAsync(
        Guid id,
        Guid? principalId,
        long expectedVersion,
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
                new(id, principalId, actor, expectedVersion),
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
                    expectedVersion),
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
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await OnGetAsync(id, cancellationToken);
        }
    }
}
