using Pegasus.Core.Documents;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class CaseDocumentEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public int Ordinal { get; set; }
    public string SourceOccurrenceIdentity { get; set; } = string.Empty;
}

internal sealed class DocumentVersionEntity
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int Version { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public long ContentLength { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string? BoxFileId { get; set; }
    public string? BoxVersionId { get; set; }
    public string? PendingContentStorageKey { get; set; }
    public DocumentCustodyStatus CustodyStatus { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public bool IsLogicallyRemoved { get; set; }
    public string? RemovalReason { get; set; }
    public string? RemovalOperationKey { get; set; }
}

internal sealed class DocumentOccurrenceEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid VersionId { get; set; }
    public int Ordinal { get; set; }
    public DocumentSemanticRole SemanticRole { get; set; }
    public DocumentSource Source { get; set; }
    public string SourceOccurrenceIdentity { get; set; } = string.Empty;
    public DateTimeOffset RecordedAtUtc { get; set; }
    public string OperationKey { get; set; } = string.Empty;
    public DateTimeOffset? ThirdPartyVehicleConfirmedAtUtc { get; set; }
    public string? ThirdPartyVehicleConfirmationReason { get; set; }
    public string? ThirdPartyVehicleConfirmationOperationKey { get; set; }
    public string? PreparationRole { get; set; }
    public int? SupportingOrder { get; set; }
    public short RotationDegrees { get; set; }
    public decimal? CropLeft { get; set; }
    public decimal? CropTop { get; set; }
    public decimal? CropWidth { get; set; }
    public decimal? CropHeight { get; set; }
    public long PreparationVersion { get; set; }
    public string? PreparedBy { get; set; }
    public DateTimeOffset? PreparedAtUtc { get; set; }
}

internal sealed class RequestUploadLinkEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public string TokenDigest { get; set; } = string.Empty;
    public RequestUploadStatus Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public int AcceptedFileCount { get; set; }
    public long AcceptedByteCount { get; set; }
    public string LimitsVersion { get; set; } = string.Empty;
    public string? Recipient { get; set; }
    public string? Reason { get; set; }
    public long Version { get; set; }
    public string CreateOperationKey { get; set; } = string.Empty;
    public string? RevokeOperationKey { get; set; }
}

internal sealed class RequestUploadReceiptEntity
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public Guid OccurrenceId { get; set; }
    public Guid VersionId { get; set; }
    public string OperationKey { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAtUtc { get; set; }
}
