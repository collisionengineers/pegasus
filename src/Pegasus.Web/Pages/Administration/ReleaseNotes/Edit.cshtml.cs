using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.ReleaseNotes;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Administration.ReleaseNotes;

/// <summary>
/// Writing a release note (FRD-17): a draft is saved and rewritten as often as
/// wanted, then published. Publish stamps the running build and freezes the
/// note; a published note opens read-only here. Only an Administrator's press
/// publishes; Core refuses every other actor.
/// </summary>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class EditModel(ReleaseNoteAdministration administration) : AdministrationPageModel
{
    [BindProperty(SupportsGet = true)] public Guid? Id { get; set; }
    [BindProperty] public long ExpectedRowVersion { get; set; }
    [BindProperty] public string OperationKey { get; set; } = NewOperationKey();
    [BindProperty] public string? Title { get; set; }
    [BindProperty] public string? Body { get; set; }

    public ReleaseNote? Note { get; private set; }

    public bool IsPublished => Note?.IsPublished == true;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (Id is { } id && id != Guid.Empty)
        {
            Note = await administration.GetAsync(actor, id, cancellationToken);
            if (Note is null)
            {
                return NotFound();
            }

            Title = Note.Title;
            Body = Note.Body;
            ExpectedRowVersion = Note.RowVersion;
        }

        return Page();
    }

    public Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken) =>
        SubmitAsync(publish: false, cancellationToken);

    public Task<IActionResult> OnPostPublishAsync(CancellationToken cancellationToken) =>
        SubmitAsync(publish: true, cancellationToken);

    private async Task<IActionResult> SubmitAsync(bool publish, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (!IsOperationKeyValid(OperationKey))
        {
            ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
            return await ReloadAsync(actor, cancellationToken);
        }

        try
        {
            var saved = await administration.SaveDraftAsync(
                actor,
                Id,
                Id is null ? null : ExpectedRowVersion,
                Title,
                Body,
                cancellationToken);
            if (!publish)
            {
                TempData["Confirmation"] = OperatorLabels.ReleaseNotes.Saved;
                return RedirectToPage("/Administration/ReleaseNotes/Edit", new { id = saved.Id });
            }

            await administration.PublishAsync(actor, saved.Id, saved.RowVersion, cancellationToken);
            TempData["Confirmation"] = OperatorLabels.ReleaseNotes.Published;
            return RedirectToPage("/Administration/ReleaseNotes/Index");
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (ReleaseNoteConflictException)
        {
            ModelState.AddModelError(string.Empty, OperatorLabels.ReleaseNotes.Conflict);
            return await ReloadAsync(actor, cancellationToken, preserveExpectedRowVersion: true);
        }

        return await ReloadAsync(actor, cancellationToken);
    }

    private async Task<IActionResult> ReloadAsync(
        ActionActor actor,
        CancellationToken cancellationToken,
        bool preserveExpectedRowVersion = false)
    {
        if (Id is { } id && id != Guid.Empty)
        {
            Note = await administration.GetAsync(actor, id, cancellationToken);
            if (Note is not null && !preserveExpectedRowVersion)
            {
                ExpectedRowVersion = Note.RowVersion;
            }
        }

        OperationKey = NewOperationKey();
        return Page();
    }
}
