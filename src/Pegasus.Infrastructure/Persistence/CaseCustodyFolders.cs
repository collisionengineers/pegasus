using Pegasus.Core.Custody;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The persisted vocabulary of <see cref="CaseCustodyFolder"/> and the one
/// place a document's Box root is chosen: the Case folder, or the <c>a.</c>
/// folder of an Inspection + Audit Case's Audit. Every managed-content
/// address takes its root through here so a read, a write and the Box parent
/// check agree on the folder.
/// </summary>
internal static class CaseCustodyFolders
{
    public const string Case = "case";
    public const string Audit = "audit";

    public static string ToCode(CaseCustodyFolder folder) => folder switch
    {
        CaseCustodyFolder.Case => Case,
        CaseCustodyFolder.Audit => Audit,
        _ => throw new ArgumentOutOfRangeException(nameof(folder), folder, "Unknown custody folder.")
    };

    public static string? RootOf(CaseEntity caseEntity, string folder)
    {
        ArgumentNullException.ThrowIfNull(caseEntity);
        return RootOf(folder, caseEntity.CustodyRootRemoteId, caseEntity.AuditCustodyRemoteId);
    }

    public static string? RootOf(string folder, string? caseRootRemoteId, string? auditRootRemoteId) =>
        string.Equals(folder, Audit, StringComparison.Ordinal) ? auditRootRemoteId : caseRootRemoteId;
}
