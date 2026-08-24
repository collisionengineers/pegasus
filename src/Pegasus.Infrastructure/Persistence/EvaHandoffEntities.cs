namespace Pegasus.Infrastructure.Persistence;

internal sealed class EvaHandoffRevisionEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public int Revision { get; set; }
    public long AcceptedCaseVersion { get; set; }
    public string SchemaVersion { get; set; } = string.Empty;
    public string InputFingerprint { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public byte[] BundleContent { get; set; } = [];
    public string BundleSha256 { get; set; } = string.Empty;
    public byte[] JsonContent { get; set; } = [];
    public string JsonSha256 { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public string GeneratedBy { get; set; } = string.Empty;
}

internal sealed class EvaHandoffOperationEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public string OperationKey { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public Guid RevisionId { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public string ActorSubjectId { get; set; } = string.Empty;
}

internal sealed class EvaFirstHandoffProxyEntity
{
    public Guid CaseId { get; set; }
    public Guid RevisionId { get; set; }
    public string AdapterKey { get; set; } = string.Empty;
    public string AdapterVersion { get; set; } = string.Empty;
    public DateTimeOffset RecordedAtUtc { get; set; }
    public string ActorSubjectId { get; set; } = string.Empty;
    public string OperationKey { get; set; } = string.Empty;
    public bool ClaimsExternalDelivery { get; set; }
    public bool ClaimsEngineerAssignment { get; set; }
}

internal sealed class EvaHandoffDownloadOperationEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public Guid RevisionId { get; set; }
    public string OperationKey { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ActorKind { get; set; } = string.Empty;
    public string ActorSubjectId { get; set; } = string.Empty;
    public string ActorRolesJson { get; set; } = string.Empty;
    public DateTimeOffset PreparedAtUtc { get; set; }
}
