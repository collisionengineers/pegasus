using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.ReleaseNotes;

namespace Pegasus.Web.Pages;

/// <summary>
/// Release notes for everyone (FRD-12): the published notes, newest first, and
/// the What's new dialog's acknowledgement. Got it records that this person
/// has seen the note; the dialog then stops opening for them.
/// </summary>
[Authorize]
public sealed class ReleaseNotesModel(IMyReleaseNotes releaseNotes) : StaffPageModel
{
    public IReadOnlyList<ReleaseNote> Notes { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        Notes = await releaseNotes.ListPublishedAsync(actor, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAcknowledgeAsync(Guid id, string? returnUrl, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (id == Guid.Empty)
        {
            return NotFound();
        }

        await releaseNotes.AcknowledgeAsync(actor, id, cancellationToken);
        return LocalRedirect(!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
    }
}
