using Pegasus.Core.Reports;

namespace Pegasus.Web.Presentation;

/// <summary>The Management Reports exports in the operator's words: a turnaround reads as the page writes it.</summary>
public sealed class OperatorAdministrationReportLabels : IAdministrationReportLabels
{
    public static OperatorAdministrationReportLabels Instance { get; } = new();

    public string Turnaround(TimeSpan value) => OperatorLabels.ReportTurnaround(value, string.Empty);
}
