using System.Security.Cryptography;

using Pegasus.Core.Intake;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Pegasus.Web.Pages.Intake;

public sealed class EmailEvaluationModel(
    IIntakeSourceReader sourceReader,
    IInstructionExtractionPolicy extractionPolicy,
    IMailRoutePolicy mailRoutePolicy,
    TimeProvider timeProvider) : PageModel
{
    private const long MaximumFileLength = 10 * 1024 * 1024;

    [BindProperty]
    public IFormFile? Upload { get; set; }

    public IntakeSourceReadResult? ReadResult { get; private set; }
    public MailRouteEvaluationResult? RouteResult { get; private set; }


    public InstructionExtractionResult? ExtractionResult { get; private set; }
    public string? ReplayIdentity { get; private set; }

    public bool ActivationBlocked => ReadResult is not null;


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
        var content = memory.ToArray();
        ReplayIdentity = $"local-email-evaluation:{Convert.ToHexString(SHA256.HashData(content))}";
        var source = new IntakeSource(
            fileName,
            "message/rfc822",
            content,
            timeProvider.GetUtcNow(),
            "Local email evaluation",
            new(IntakeSourceChannel.ManualUpload, ReplayIdentity));

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
            RouteResult = mailRoutePolicy.Evaluate(ReadResult);
            if (RouteResult is
                {
                    Disposition: MailRouteDisposition.Accepted,
                    SelectedRoute: not null
                })
            {
                ExtractionResult = extractionPolicy.Extract(ReadResult, timeProvider.GetUtcNow());
            }
            else
            {
                ModelState.AddModelError(string.Empty, RouteResult.Reason);
            }
        }

        return Page();
    }
}
