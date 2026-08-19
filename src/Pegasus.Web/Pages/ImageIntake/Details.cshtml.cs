using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;

namespace Pegasus.Web.Pages.ImageIntake;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class DetailsModel(
    IImageIntakeQueries imageIntakeQueries,
    IVrmSuggestionStore vrmSuggestionStore,
    IImageIntakeCaseCandidates imageIntakeCaseCandidates,
    IImageIntakeStore imageIntakeStore) : PageModel
{
    public ImageIntakeDetail Detail { get; private set; } = null!;

    public IReadOnlyList<ImageVrmSuggestion> Suggestions { get; private set; } = [];

    public IReadOnlyList<ImageIntakeCaseCandidate> AssociationCandidates { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var detail = await imageIntakeQueries.GetAsync(id, cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        Detail = detail;
        Suggestions = await vrmSuggestionStore.ListForReceiptAsync(
            detail.Record.Origin.ReceiptId,
            cancellationToken);
        AssociationCandidates = detail.AssociatedCaseId is null
            ? await imageIntakeCaseCandidates.FindEligibleByRegistrationAsync(
                detail.Record.NormalizedVehicleRegistration,
                cancellationToken)
            : [];
        return Page();
    }

    public async Task<IActionResult> OnPostCloseAsync(
        Guid id,
        long expectedVersion,
        string reason,
        CancellationToken cancellationToken)
    {
        if (!StaffActorFactory.TryCreate(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var actor))
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
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await OnGetAsync(id, cancellationToken);
        }
    }
}
