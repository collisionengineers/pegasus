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
    public string ExternalReceiptToken { get; set; } = string.Empty;

    public void OnGet()
    {
        ExternalReceiptToken = CreateExternalReceiptToken();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ExternalReceiptToken))
        {
            ExternalReceiptToken = CreateExternalReceiptToken();
        }
        else if (!Guid.TryParseExact(ExternalReceiptToken, "N", out _))
        {
            ModelState.AddModelError(string.Empty, "The upload receipt is invalid. Refresh the page and try again.");
        }

        if (Upload is null)
        {
            ModelState.AddModelError(nameof(Upload), "Choose an email, document, PDF or image to upload.");
            return Page();
        }

        var extension = Path.GetExtension(Upload.FileName);
        if (!SupportedExtensions.Contains(extension))
        {
            ModelState.AddModelError(
                nameof(Upload),
                "Choose an .eml, .pdf, .docx, .doc, .msg, .jpg, .jpeg or .png file.");
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
                    new(IntakeSourceChannel.ManualUpload, ExternalReceiptToken)),
                cancellationToken);

            return RedirectToPage("/Intake/Review", new { id = result.Id, duplicate = result.IsDuplicate });
        }
        catch (IntakeSourceIdentityConflictException)
        {
            ModelState.AddModelError(
                string.Empty,
                "This upload receipt was already used for different content. Refresh the page and try again.");
            return Page();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ModelState.AddModelError(string.Empty, "The intake receipt could not be stored because of a technical failure.");
            return Page();
        }
    }

    private static string CreateExternalReceiptToken() => Guid.NewGuid().ToString("N");

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".eml",
        ".pdf",
        ".docx",
        ".doc",
        ".msg",
        ".jpg",
        ".jpeg",
        ".png"
    };
}
