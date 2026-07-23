using CollisionSpike.Core.Intake.Qdos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CollisionSpike.Web.Pages.Intake;

public sealed class QdosModel(ProcessQdosIntake processQdosIntake, TimeProvider timeProvider) : PageModel
{
    private const long MaximumFileLength = 10 * 1024 * 1024;

    [BindProperty]
    public IFormFile? Upload { get; set; }

    [BindProperty]
    public bool CaseCreationAuthorized { get; set; }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!CaseCreationAuthorized)
        {
            ModelState.AddModelError(
                nameof(CaseCreationAuthorized),
                "Confirm that this is a new QDOS instruction and that you are authorised to create its case and reference.");
        }

        if (Upload is null)
        {
            ModelState.AddModelError(nameof(Upload), "Choose an email or PDF to upload.");
            return Page();
        }

        var extension = Path.GetExtension(Upload.FileName);
        if (!extension.Equals(".eml", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(Upload), "Choose an .eml or .pdf file.");
        }

        if (Upload.Length == 0)
        {
            ModelState.AddModelError(nameof(Upload), "The selected file is empty.");
        }
        else if (Upload.Length > MaximumFileLength)
        {
            ModelState.AddModelError(nameof(Upload), "The selected file must be 10 MB or smaller.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        await using var memory = new MemoryStream((int)Upload.Length);
        await Upload.CopyToAsync(memory, cancellationToken);

        try
        {
            var result = await processQdosIntake.ExecuteAsync(
                new(
                    Path.GetFileName(Upload.FileName),
                    string.IsNullOrWhiteSpace(Upload.ContentType) ? "application/octet-stream" : Upload.ContentType,
                    memory.ToArray(),
                    timeProvider.GetUtcNow(),
                    "Web manual upload",
                    CaseCreationAuthorized),
                cancellationToken);

            return RedirectToPage("/Intake/Review", new { id = result.Id, duplicate = result.IsDuplicate });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ModelState.AddModelError(string.Empty, "The intake receipt could not be stored because of a technical failure.");
            return Page();
        }
    }
}
