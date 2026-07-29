namespace Pegasus.Core.Custody;

public enum CustodyWorkKind
{
    CreateCaseRoot,
    RetainAcceptedIntakeSource,
    CreateAuditReferenceFolder
}

public sealed record CustodyWork(
    Guid Id,
    CustodyWorkKind Kind,
    Guid CaseId,
    string OperationKey);

public sealed record CaseCustodyRoot(
    Guid CaseId,
    string RemoteId,
    string Reference);

public sealed record IntakeSourceCustodyReference(
    Guid IntakeReceiptId,
    string SourceFileName,
    string MediaType,
    string SourceHash,
    string StagedObjectKey);

public sealed record CustodyDocumentVersion(
    Guid CaseId,
    string RemoteId,
    string ContentHash,
    string ETag);

/// <summary>
/// A case-scoped port. Implementations must guard the configured custody root and never accept an
/// arbitrary remote identifier from a caller.
/// </summary>
public interface ICaseCustody
{
    Task<CaseCustodyRoot> CreateCaseRootAsync(
        Guid caseId,
        string caseReference,
        string operationKey,
        CancellationToken cancellationToken);

    Task<CustodyDocumentVersion> RetainAcceptedIntakeSourceAsync(
        CaseCustodyRoot root,
        IntakeSourceCustodyReference source,
        string operationKey,
        CancellationToken cancellationToken);

    Task<string> CreateAuditReferenceFolderAsync(
        CaseCustodyRoot root,
        string auditReference,
        string operationKey,
        CancellationToken cancellationToken);
}

public interface IProcessQueuedCustody
{
    Task ExecuteAsync(Guid workId, CancellationToken cancellationToken);
}
