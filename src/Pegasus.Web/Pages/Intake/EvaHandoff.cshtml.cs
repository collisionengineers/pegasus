using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Eva;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Web.Pages.Intake;

public sealed class EvaHandoffModel(
    IDbContextFactory<PegasusDbContext> contextFactory) : PageModel
{
    private readonly EvaHandoffStore store = new(contextFactory);

    public EvaHandoffPreparation Preparation { get; private set; } = null!;

    [BindProperty]
    public List<Guid> SelectedImageIds { get; set; } = [];

    [BindProperty]
    public long ExpectedCaseVersion { get; set; }

    [BindProperty]
    public string OperationKey { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var preparation = await store.GetPreparationAsync(id, cancellationToken);
        if (preparation is null)
        {
            return NotFound();
        }

        Preparation = preparation;
        ExpectedCaseVersion = preparation.CaseVersion;
        OperationKey = $"eva-handoff:{id:N}:{Guid.NewGuid():N}";
        return Page();
    }

    public async Task<IActionResult> OnPostGenerateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var actor = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(actor))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(OperationKey))
        {
            ModelState.AddModelError(string.Empty, "The generation request is incomplete. Reload before retrying.");
            return await ReloadAsync(id, StatusCodes.Status400BadRequest, cancellationToken);
        }

        var result = await store.GenerateAsync(
            new(
                id,
                ExpectedCaseVersion,
                SelectedImageIds,
                actor,
                OperationKey),
            cancellationToken);

        if (result.Outcome == GenerateEvaHandoffOutcome.NotFound)
        {
            return NotFound();
        }

        if (result.Outcome == GenerateEvaHandoffOutcome.Generated && result.Bundle is not null)
        {
            Response.Headers.CacheControl = "private, no-store";
            Response.Headers.XContentTypeOptions = "nosniff";
            return File(result.Bundle.Content, "application/zip", result.Bundle.FileName);
        }

        foreach (var reason in result.Reasons)
        {
            ModelState.AddModelError(string.Empty, reason);
        }

        var statusCode = result.Outcome == GenerateEvaHandoffOutcome.Conflict
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status422UnprocessableEntity;
        return await ReloadAsync(id, statusCode, cancellationToken);
    }

    private async Task<IActionResult> ReloadAsync(
        Guid id,
        int statusCode,
        CancellationToken cancellationToken)
    {
        var preparation = await store.GetPreparationAsync(id, cancellationToken);
        if (preparation is null)
        {
            return NotFound();
        }

        Preparation = preparation;
        ExpectedCaseVersion = preparation.CaseVersion;
        Response.StatusCode = statusCode;
        return Page();
    }
}
