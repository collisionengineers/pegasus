using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.ReleaseNotes;

namespace Pegasus.Web.Pages.Administration.ReleaseNotes;

/// <summary>
/// The Administrator's release notes (FRD-17): every note, newest change
/// first, with its status and who published it. Writing happens on
/// <see cref="EditModel"/>; nothing here changes a note.
/// </summary>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel(
    ReleaseNoteAdministration administration,
    IStaffAccountQueries staffAccounts) : AdministrationPageModel
{
    public IReadOnlyList<ReleaseNoteListItem> Notes { get; private set; } = [];
    public bool HasMore { get; private set; }
    public int CurrentPage { get; private set; } = 1;
    private IReadOnlyDictionary<Guid, string> _staffNames = new Dictionary<Guid, string>();

    public string PublishedBy(ReleaseNoteListItem note) =>
        note.PublishedByStaffId is { } id
            ? ActorDisplayNames.Resolve(ActorKind.Staff, id.ToString("D"), _staffNames)
            : "—";

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "pageNumber")] int pageNumber,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        // One page of 50 with a pager (#1542, R418); a page Core refuses is not found, as AI jobs.
        CurrentPage = pageNumber == 0 ? 1 : pageNumber;
        ReleaseNoteListPage page;
        try { page = await administration.ListAsync(actor, CurrentPage, cancellationToken); }
        catch (ArgumentOutOfRangeException) { return NotFound(); }
        Notes = page.Notes;
        HasMore = page.HasMore;
        _staffNames = await ActorDisplayNames.ResolveStaffNamesAsync(
            staffAccounts,
            Notes.Where(note => note.PublishedByStaffId is not null).Select(note => note.PublishedByStaffId!.Value),
            cancellationToken);
        return Page();
    }
}
