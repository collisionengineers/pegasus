using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;

namespace Pegasus.Web.Pages.ImageIntake;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class IndexModel(IImageIntakeQueries imageIntakeQueries) : PageModel
{
    private readonly IImageIntakeQueries _imageIntakeQueries =
        imageIntakeQueries ?? throw new ArgumentNullException(nameof(imageIntakeQueries));

    [BindProperty(SupportsGet = true, Name = "associated")]
    public string? AssociatedFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "query")]
    public string? Query { get; set; }

    public IReadOnlyList<ImageIntakeSummary> Results { get; private set; } = [];

    public bool? Associated { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Associated = AssociatedFilter switch
        {
            null or "" => null,
            "yes" => true,
            "no" => false,
            _ => null
        };
        if (AssociatedFilter is not (null or "" or "yes" or "no"))
        {
            return NotFound();
        }

        var query = Query?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(query))
        {
            Results = await _imageIntakeQueries.ListAsync(Associated, cancellationToken);
            return Page();
        }

        // An exact Image Intake Reference wins; otherwise the input is a
        // registration search listing every intake for that VRM.
        var byReference = await _imageIntakeQueries.GetByReferenceAsync(query, cancellationToken);
        if (byReference is not null)
        {
            Results =
            [
                new ImageIntakeSummary(
                    byReference.Record.Id,
                    byReference.Record.Origin.ReceiptId,
                    byReference.Record.ImageIntakeReference,
                    byReference.Record.NormalizedVehicleRegistration,
                    byReference.AssociatedCaseId,
                    byReference.AssociatedCaseReference,
                    byReference.RegisteredAtUtc)
            ];
            return Page();
        }

        var compact = new string(query
            .Where(character => char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character))
            .ToArray());
        Results = await _imageIntakeQueries.SearchByRegistrationAsync(compact, cancellationToken);
        return Page();
    }

    public static string OutcomeLabel(ImageIntakeSummary summary) =>
        summary.AssociatedCaseId is null ? "Image intake registered" : "Associated with Case";
}
