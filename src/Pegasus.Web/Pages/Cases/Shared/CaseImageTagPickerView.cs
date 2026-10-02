using Pegasus.Core.Documents;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// One image tile's tag picker: the whole vocabulary, which of it this
/// occurrence already wears, and the envelope every tag post needs,
/// the view of the Case record it was drawn in among it.
/// </summary>
/// <remarks>
/// Each row is a real form posting to the Case's Custody handlers, so the
/// picker works with script off exactly as it does with script on; the
/// disclosure that hides it is the browser's own.
/// </remarks>
public sealed record CaseImageTagPickerView(
    Guid CaseId,
    long CaseVersion,
    Guid OccurrenceId,
    string? EditLeaseToken,
    IReadOnlyList<ImageTag> Vocabulary,
    IReadOnlyList<ImageTagAssignment> Applied,
    string? View = null)
{
    public bool IsApplied(Guid tagId) => Applied.Any(tag => tag.TagId == tagId);
}
