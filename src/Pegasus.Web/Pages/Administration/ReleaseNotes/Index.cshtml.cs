using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.ReleaseNotes;
using Pegasus.Web.Presentation;

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
    public IReadOnlyList<ReleaseNote> Notes { get; private set; } = [];
    private IReadOnlyDictionary<Guid, string> _staffNames = new Dictionary<Guid, string>();

    public string PublishedBy(ReleaseNote note) =>
        note.PublishedByStaffId is { } id
            ? ActorDisplayNames.Resolve(ActorKind.Staff, id.ToString("D"), _staffNames)
            : OperatorLabels.CaseWorkspace.AbsentValue;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        Notes = await administration.ListAsync(actor, cancellationToken);
        _staffNames = await ActorDisplayNames.ResolveStaffNamesAsync(
            staffAccounts,
            Notes.Where(note => note.PublishedByStaffId is not null).Select(note => note.PublishedByStaffId!.Value),
            cancellationToken);
        return Page();
    }
}
