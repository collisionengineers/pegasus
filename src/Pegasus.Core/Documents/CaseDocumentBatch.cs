using System.Globalization;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Documents;

/// <summary>One exact Case document version a batch download names.</summary>
public sealed record CaseDocumentBatchItem(Guid OccurrenceId, Guid VersionId);

/// <summary>
/// The versions one batch download names, in the caller's order, and the
/// operation key its <c>document_downloaded</c> rows are written under.
/// </summary>
public sealed record OpenCaseDocumentBatchQuery(
    Guid CaseId,
    IReadOnlyList<CaseDocumentBatchItem> Items,
    ActionActor Actor,
    string OperationKey)
{
    /// <summary>The key of the <c>document_downloaded</c> row of the item at <paramref name="index"/>.</summary>
    public static string ItemOperationKey(string operationKey, int index) =>
        string.Create(CultureInfo.InvariantCulture, $"{operationKey}:{index}");
}

/// <summary>
/// Opens the versions of one Automation batch download with one read for the
/// whole set. Answers <c>null</c> unless every item is a confirmed,
/// not-removed, current version of that Case, so one bad item fails the call
/// before any content is read.
/// </summary>
public interface IOpenCaseDocumentBatch
{
    Task<ICaseDocumentBatch?> ExecuteAsync(
        OpenCaseDocumentBatchQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// An opened batch. <see cref="Metadata"/> and <see cref="ItemOperationKeys"/>
/// are in request order; <see cref="ReadContentAsync"/> reads one item's
/// bytes, verified against its custody hash and length, with no database
/// read; <see cref="RecordDownloadedAsync"/> writes the
/// <c>document_downloaded</c> row of each item handed back, in one save, and
/// nothing for the others.
/// </summary>
public interface ICaseDocumentBatch
{
    IReadOnlyList<CaseDocumentMetadata> Metadata { get; }

    /// <summary>
    /// Each item's <see cref="OpenCaseDocumentBatchQuery.ItemOperationKey"/>,
    /// formatted once for its row and the caller's result.
    /// </summary>
    IReadOnlyList<string> ItemOperationKeys { get; }

    ValueTask<ReadOnlyMemory<byte>> ReadContentAsync(
        int index,
        CancellationToken cancellationToken = default);

    Task RecordDownloadedAsync(
        IReadOnlyCollection<int> indexes,
        CancellationToken cancellationToken = default);
}
