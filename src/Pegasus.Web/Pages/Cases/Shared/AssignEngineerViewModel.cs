using Pegasus.Core.Identity;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// What the Assign Engineer dialog offers and posts back: the enabled staff,
/// the signed-in person first (#1213, v41 K9), and that person's staff id,
/// which Take posts.
/// </summary>
public sealed record AssignEngineerViewModel(IReadOnlyList<CaseEngineerChoice> Engineers, Guid? ViewerStaffId)
{
    /// <summary>An option's words: the signed-in person reads "Name (you)".</summary>
    public string Label(CaseEngineerChoice engineer)
    {
        ArgumentNullException.ThrowIfNull(engineer);
        return engineer.StaffId == ViewerStaffId ? OperatorLabels.Triage.You(engineer.DisplayName) : engineer.DisplayName;
    }
}
