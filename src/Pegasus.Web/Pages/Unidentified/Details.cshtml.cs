using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Unidentified;

[Authorize(Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class DetailsModel(
    IUnidentifiedStore store,
    IResolveUnidentified resolve) : PageModel
{
    public UnidentifiedItem Item { get; private set; } = null!;

    public IReadOnlyList<UnidentifiedHistoryEntry> History { get; private set; } = [];

    [BindProperty]
    public long ExpectedVersion { get; set; }

    [BindProperty]
    public string ResolutionReason { get; set; } = string.Empty;

    [BindProperty]
    public UnidentifiedResolutionTargetKind TargetKind { get; set; } = UnidentifiedResolutionTargetKind.InstructionCase;

    [BindProperty]
    public string TargetId { get; set; } = string.Empty;

    [BindProperty]
    public string? TargetReference { get; set; }

    [BindProperty]
    public string OperationKey { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        return await LoadAsync(id, cancellationToken);
    }

    public async Task<IActionResult> OnPostResolveAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await store.GetAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        if (!StaffActorFactory.TryCreate(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var actor))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(OperationKey))
        {
            OperationKey = $"web-unidentified-resolve:{id:N}:{Guid.NewGuid():N}";
        }

        try
        {
            await resolve.ExecuteAsync(
                new(
                    id,
                    ExpectedVersion,
                    actor,
                    OperationKey,
                    ResolutionReason,
                    TargetKind,
                    TargetId,
                    TargetReference,
                    DateTimeOffset.UtcNow),
                cancellationToken);
        }
        catch (UnidentifiedVersionConflictException)
        {
            ModelState.AddModelError(string.Empty, "This item changed in another session. Reload it before resolving.");
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }

        return await LoadAsync(id, cancellationToken);
    }

    private async Task<IActionResult> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await store.GetAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        Item = item;
        ExpectedVersion = item.Version;
        History = await store.HistoryAsync(id, cancellationToken);
        return Page();
    }

    public string ReasonLabel => OperatorLabels.UnidentifiedReason(Item.ReasonCode);
}
