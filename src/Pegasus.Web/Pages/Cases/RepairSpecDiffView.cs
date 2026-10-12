using Pegasus.Core.Assessment;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// What the side-by-side Compare table draws (v38 P2): the two specifications'
/// differences and the name each side prints under — the spec's working draft,
/// another spec's name, or one of its frozen versions.
/// </summary>
public sealed record RepairSpecDiffView(
    RepairSpecificationComparison.Diff Diff,
    string FromName,
    string ToName);
