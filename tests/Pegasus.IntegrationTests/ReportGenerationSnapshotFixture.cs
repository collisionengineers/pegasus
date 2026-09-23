using System.Text.Json;
using Pegasus.Core.Reports;
using Pegasus.IntegrationTests.Reports;

namespace Pegasus.IntegrationTests;

internal static class ReportGenerationSnapshotFixture
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Json(Guid caseId, string operationKey)
    {
        var input = AssessmentReportDraftWebTests.ReadyInput(caseId);
        var report = AssessmentReportProjection.Project(input).Snapshot!;
        var estimate = input.CurrentEstimate!;
        var snapshot = new CaseReportGenerationSnapshot(
            caseId, 0, "CE-100", operationKey, CaseReportActor.None,
            new DateTimeOffset(2031, 5, 6, 10, 0, 0, TimeSpan.Zero),
            Guid.Empty, new string('0', 64), "image/png",
            estimate.SpecificationId, estimate.Version, report.Costs, report.EngineerValue,
            Guid.Empty, report.Content, report.Guides, report.ReportDate,
            report.ReportDateOverridden, report.AgreedFee, report.FeeDescriptionLines,
            [], [], report.PayloadVersion, "renderer/test", report)
        {
            CurrentEstimate = estimate
        };

        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }
}
