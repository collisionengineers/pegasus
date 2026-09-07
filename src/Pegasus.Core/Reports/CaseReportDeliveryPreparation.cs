using Pegasus.Core.Identity;
using Pegasus.Core.Operations;

namespace Pegasus.Core.Reports;

public sealed record ReportSendReadinessRequest(
    ActionActor Actor, Guid CaseId, long ExpectedCaseVersion, Guid GenerationId,
    long ExpectedGenerationVersion, Guid PreparationId, long ExpectedPreparationVersion,
    IReadOnlyList<StaffMailAttachment> Artifacts);
public interface IReportSendReadiness
{
    Task RequireReadyAsync(ReportSendReadinessRequest request, CancellationToken cancellationToken);
}
public sealed record CaseReportDeliveryPreparation(
    Guid Id, Guid CaseId, Guid GenerationId, long GenerationVersion, long Version,
    IReadOnlyList<StaffMailAttachment> Artifacts, ActionActor PreparedBy,
    DateTimeOffset PreparedAtUtc);
public sealed record PrepareCaseReportDeliveryRequest(
    ActionActor Actor, Guid CaseId, long ExpectedCaseVersion, string LeaseToken,
    Guid GenerationId, long ExpectedGenerationVersion, string OperationKey);
public interface IPrepareCaseReportDelivery
{
    Task<CaseReportDeliveryPreparation> ExecuteAsync(
        PrepareCaseReportDeliveryRequest request, CancellationToken cancellationToken);
}
