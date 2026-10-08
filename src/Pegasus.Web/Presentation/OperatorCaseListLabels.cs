using Pegasus.Core.Cases;
using Pegasus.Core.Reports;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Presentation;

/// <summary>The Case list's cells in the operator's words: the same names the Case page and queues show.</summary>
public sealed class OperatorCaseListLabels : ICaseListLabels
{
    public static OperatorCaseListLabels Instance { get; } = new();

    public string CaseType(CaseType type) => OperatorLabels.CaseTypeName(type);

    public string Stage(CaseLifecycleState state) => OperatorLabels.CaseStage(state);

    public string TriageStage(TriageState state) => OperatorLabels.TriageState(state);

    public string SalvageCategory(string code) => OperatorLabels.PrincipalAdministration.SalvageCategory(code);
}
