using System.Linq.Expressions;
using Pegasus.Core.Custody;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The persisted vocabulary of <see cref="CaseCustodyFolder"/> and the one
/// place a document's Box folder is chosen: the Case folder, the <c>a.</c>
/// folder of an Inspection + Audit Case's Audit, or the Case folder's one
/// subfolder, <c>market-research</c> (R1238). Every managed-content address
/// takes its root through here so a read, a write and the Box parent check
/// agree on the folder; nothing else compares a folder code to choose one.
/// </summary>
internal static class CaseCustodyFolders
{
    public const string Case = "case";
    public const string Audit = "audit";

    /// <summary>
    /// The subfolder of a Case's folder that holds what Pegasus Desktop
    /// submits beside its report: the Advert Evidence Pack and the advert
    /// screenshots (R1238, R1313). Its name in Box and its persisted code.
    /// </summary>
    public const string MarketResearch = "market-research";

    public static string ToCode(CaseCustodyFolder folder) => folder switch
    {
        CaseCustodyFolder.Case => Case,
        CaseCustodyFolder.Audit => Audit,
        _ => throw new ArgumentOutOfRangeException(nameof(folder), folder, "Unknown custody folder.")
    };

    /// <summary>Whether the document is filed in the Audit's <c>a.</c> folder.</summary>
    public static bool IsAudit(CaseDocumentEntity document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return string.Equals(document.CustodyFolder, Audit, StringComparison.Ordinal);
    }

    public static string? RootOf(CaseEntity caseEntity, CaseDocumentEntity document)
    {
        ArgumentNullException.ThrowIfNull(caseEntity);
        ArgumentNullException.ThrowIfNull(document);
        return RootOf(
            document.CustodyFolder,
            document.CustodySubfolder,
            caseEntity.CustodyRootRemoteId,
            caseEntity.AuditCustodyRemoteId,
            caseEntity.MarketResearchCustodyRemoteId);
    }

    /// <summary>
    /// The Box folder a document is filed in, from its folder and subfolder
    /// codes and its Case's three folders. Null while that folder does not
    /// exist yet.
    /// </summary>
    public static string? RootOf(
        string folder,
        string? subfolder,
        string? caseRootRemoteId,
        string? auditRootRemoteId,
        string? marketResearchRootRemoteId)
    {
        if (string.Equals(subfolder, MarketResearch, StringComparison.Ordinal))
        {
            return marketResearchRootRemoteId;
        }
        if (subfolder is not null)
        {
            throw new InvalidDataException("Unknown custody subfolder.");
        }

        return string.Equals(folder, Audit, StringComparison.Ordinal)
            ? auditRootRemoteId
            : caseRootRemoteId;
    }

    /// <summary>
    /// Whether the Case still keeps the document's folder at
    /// <paramref name="rootRemoteId"/>: <see cref="RootOf(CaseEntity, CaseDocumentEntity)"/>
    /// as a filter a query can run.
    /// </summary>
    public static Expression<Func<CaseEntity, bool>> RootIs(CaseDocumentEntity document, string rootRemoteId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootRemoteId);
        if (string.Equals(document.CustodySubfolder, MarketResearch, StringComparison.Ordinal))
        {
            return caseEntity => caseEntity.MarketResearchCustodyRemoteId == rootRemoteId;
        }
        if (document.CustodySubfolder is not null)
        {
            throw new InvalidDataException("Unknown custody subfolder.");
        }
        if (IsAudit(document))
        {
            return caseEntity => caseEntity.AuditCustodyRemoteId == rootRemoteId;
        }

        return caseEntity => caseEntity.CustodyRootRemoteId == rootRemoteId;
    }

    /// <summary>
    /// The occurrences that are the Case's own documents: every one but those
    /// kept in a subfolder, which belong to a space of their own and are
    /// listed only there (R1313).
    /// </summary>
    public static IQueryable<DocumentOccurrenceEntity> OfCaseDocuments(
        this IQueryable<DocumentOccurrenceEntity> occurrences,
        PegasusDbContext context)
    {
        ArgumentNullException.ThrowIfNull(occurrences);
        ArgumentNullException.ThrowIfNull(context);
        return occurrences.Where(occurrence => !context.Set<CaseDocumentEntity>().Any(document =>
            document.Id == occurrence.DocumentId && document.CustodySubfolder != null));
    }
}
