using Pegasus.Core.Identity;

namespace Pegasus.Core.Reports;

public sealed record CaseReportArtifact(
    Guid DocumentId, Guid VersionId, string Sha256, long ContentLength,
    string FileName, string MediaType, string ArtifactKind);
public sealed record CaseReportGeneration(
    Guid Id, Guid CaseId, long CaseVersion, long Version, string InputFingerprint,
    string TemplateVersion, string CalculationPolicyVersion, ActionActor GeneratedBy,
    DateTimeOffset GeneratedAtUtc, IReadOnlyList<CaseReportArtifact> Artifacts);
public sealed record GenerateCaseReportRequest(
    ActionActor Actor, Guid CaseId, long ExpectedCaseVersion, string LeaseToken,
    string OperationKey);
public interface IGenerateCaseReport
{
    Task<CaseReportGeneration> ExecuteAsync(
        GenerateCaseReportRequest request, CancellationToken cancellationToken);
}
public interface ICaseReportGenerationQueries
{
    Task<CaseReportGeneration?> GetAsync(
        ActionActor actor, Guid caseId, Guid generationId, CancellationToken cancellationToken);
}
