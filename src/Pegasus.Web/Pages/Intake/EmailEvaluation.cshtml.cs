using Pegasus.Core.Intake;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Pegasus.Web.Pages.Intake;

public sealed class EmailEvaluationModel(
    IIntakeSourceReader sourceReader,
    IInstructionExtractionPolicy extractionPolicy,
    TimeProvider timeProvider) : PageModel
{
    private const long MaximumFileLength = 10 * 1024 * 1024;

    [BindProperty]
    public IFormFile? Upload { get; set; }

    public IntakeSourceReadResult? ReadResult { get; private set; }

    public InstructionExtractionResult? ExtractionResult { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var fileName = Upload is null ? string.Empty : Path.GetFileName(Upload.FileName);
        if (Upload is null)
        {
            ModelState.AddModelError(nameof(Upload), "Choose an .eml email to evaluate.");
            return Page();
        }

        if (!fileName.EndsWith(".eml", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(Upload), "The selected file must be an .eml email.");
        }
        else if (Upload.Length == 0)
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
        var source = new IntakeSource(
            fileName,
            "message/rfc822",
            memory.ToArray(),
            timeProvider.GetUtcNow(),
            "Local email evaluation",
            new(IntakeSourceChannel.ManualUpload, Guid.NewGuid().ToString("N")));

        try
        {
            ReadResult = await sourceReader.ReadAsync(source, cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            ModelState.AddModelError(string.Empty, "The email could not be evaluated because of a technical failure.");
            return Page();
        }

        if (ReadResult.Status == IntakeSourceReadStatus.Readable && !ReadResult.IsIncomplete)
        {
            ExtractionResult = extractionPolicy.Extract(ReadResult, timeProvider.GetUtcNow());
        }

        return Page();
    }
}
