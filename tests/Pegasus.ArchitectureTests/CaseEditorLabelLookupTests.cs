using Pegasus.Web.Presentation;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// The editor label lookup is one dictionary probe built from the six editor
/// dictionaries; it must answer exactly what scanning them in order answered.
/// </summary>
public sealed class CaseEditorLabelLookupTests
{
    [Fact]
    public void EveryEditorFieldHasTheLabelTheOrderedScanGave()
    {
        var editors = CaseWorkspaceLabels.Editors.Settlement
            .Concat(CaseWorkspaceLabels.Editors.Valuation)
            .Concat(CaseWorkspaceLabels.Editors.OriginalReport)
            .Concat(CaseWorkspaceLabels.Editors.Report)
            .Concat(CaseWorkspaceLabels.Editors.Damage)
            .Concat(CaseWorkspaceLabels.Editors.Vehicle)
            .ToArray();

        Assert.NotEmpty(editors);
        foreach (var entry in editors)
        {
            var field = CaseWorkspaceLabels.Editors.FormName(entry.Key);
            var scanned = editors.First(candidate => field == CaseWorkspaceLabels.Editors.FormName(candidate.Key)).Value;
            Assert.Equal(scanned, CaseWorkspaceLabels.Editors.Label(field));
        }
    }

    [Fact]
    public void AnUnknownFieldHasNoLabel()
    {
        Assert.Null(CaseWorkspaceLabels.Editors.Label(CaseWorkspaceLabels.Editors.FormName("not.an.assessment.path")));
    }
}
