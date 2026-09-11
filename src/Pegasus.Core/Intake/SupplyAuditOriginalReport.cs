using System.Security.Cryptography;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Core.Intake;

public sealed record SupplyAuditOriginalReportRequest(
    Guid UnidentifiedItemId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string FileName,
    string MediaType,
    ReadOnlyMemory<byte> Content);

/// <summary>
/// The staff-supplied report did not state exactly one unnegated outcome
/// (unreadable, scanned without text, neither literal, or both literals), so
/// Pegasus cannot derive the Audit reference from it and nothing was retained.
/// </summary>
public sealed class SuppliedOriginalReportRefusalException(string message)
    : InvalidOperationException(message);

public interface ISupplyAuditOriginalReport
{
    Task<IntakeReceipt> ExecuteAsync(
        SupplyAuditOriginalReportRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Supplies the original engineer's report for an Audit instruction that
/// arrived without one, so the waiting Unidentified item can progress
/// (operator decision, 2026-09-11): the instruction waits in Unidentified —
/// the Case/PO is not allocated — until an unambiguous report states Repairable
/// or Total loss and the <c>a.</c>/<c>ap.</c> reference becomes derivable.
///
/// The command retains the upload, attaches it to the receipt, and queues the
/// receipt's re-evaluation in one transaction; the queued pass re-classifies
/// with the supplied report's literal outcome, records the Audit evidence,
/// allocates the Case, and resolves the Unidentified item in the ordinary way.
///
/// The report's content is pre-checked before anything is retained: a report
/// that does not state exactly one unnegated outcome is refused outright, and
/// nothing about the refused upload is kept. Only the outcome is later taken
/// from the retained bytes — its text never joins the e-mail's own read
/// result, so it cannot pollute extraction, case matching or search.
/// </summary>
public sealed class SupplyAuditOriginalReport(
    IUnidentifiedStore unidentifiedStore,
    IIntakeReceiptQueries receiptQueries,
    IStandaloneAuditEvidenceQueries auditEvidenceQueries,
    IIntakeSourceReader sourceReader,
    IIntakeArtifactStore artifactStore,
    IIntakeMutationStore mutationStore,
    TimeProvider timeProvider) : ISupplyAuditOriginalReport
{
    public const string RefusalMessage =
        "This report does not state Repairable or Total loss clearly, so "
        + "Pegasus cannot derive the Audit reference from it.";

    public async Task<IntakeReceipt> ExecuteAsync(
        SupplyAuditOriginalReportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.UnidentifiedItemId == Guid.Empty)
        {
            throw new ArgumentException(
                "An Unidentified item identifier is required.", nameof(request));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(request.ExpectedVersion);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        var operationKey = RequireOperationKey(request.OperationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
        var fileName = Path.GetFileName(request.FileName.Trim());
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MediaType);
        if (request.Content.IsEmpty)
        {
            throw new ArgumentException(
                "The supplied report is empty.", nameof(request));
        }

        var item = await unidentifiedStore.GetAsync(request.UnidentifiedItemId, cancellationToken)
            ?? throw new KeyNotFoundException("The Unidentified item does not exist.");
        if (item.State != UnidentifiedState.Open
            || item.Origin.Kind != UnidentifiedOriginKind.Receipt
            || item.ReasonCode != UnidentifiedReasonCode.AuditOriginalReportMissing)
        {
            throw new InvalidOperationException(
                "This item is not an open Unidentified Audit that is missing its original report.");
        }

        var receipt = await receiptQueries.GetAsync(item.Origin.Id, cancellationToken)
            ?? throw new KeyNotFoundException("The Unidentified item's retained receipt does not exist.");
        await EnsureReceiptCanReceiveOriginalReportAsync(receipt, cancellationToken);

        // Pre-check before retention: a report that does not read as exactly
        // one unnegated outcome is refused and nothing is kept.
        var standaloneRead = await sourceReader.ReadAsync(
            new(
                fileName,
                request.MediaType,
                request.Content,
                receipt.ReceivedAtUtc,
                request.Actor.SubjectId,
                new(IntakeSourceChannel.ManualUpload, $"supplied-original-report:{receipt.Id:N}")),
            cancellationToken);
        if (QdosMailClassificationPolicy.EvaluateSuppliedOriginalReport(
                standaloneRead,
                SourceLabel(fileName)) is null)
        {
            throw new SuppliedOriginalReportRefusalException(RefusalMessage);
        }

        var contentHash = Convert.ToHexString(SHA256.HashData(request.Content.Span));
        var storageKey = await artifactStore.StoreAsync(
            contentHash,
            request.Content,
            cancellationToken);
        var updated = await mutationStore.AttachSuppliedOriginalReportAsync(
            new(
                receipt.Id,
                receipt.Version,
                request.Actor,
                operationKey,
                fileName,
                request.MediaType,
                SourceLabel(fileName),
                contentHash,
                storageKey,
                request.Content.Length),
            timeProvider.GetUtcNow(),
            cancellationToken);

        // The item's own history names what the operator did, so the page's
        // History shows the report's arrival whether or not the queued pass
        // has resolved the item yet. Keyed off the supply's operation key, so
        // a replayed command notes nothing twice.
        await unidentifiedStore.AppendNoteAsync(
            item.Id,
            $"Original report added: {fileName}",
            request.Actor,
            $"{operationKey}:note",
            timeProvider.GetUtcNow(),
            cancellationToken);
        return updated;
    }

    private static string SourceLabel(string fileName) => $"supplied original report: {fileName}";

    private async Task EnsureReceiptCanReceiveOriginalReportAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        if (receipt.MailClassificationDecision is not
            { CaseType: CaseType.Audit, StandaloneAuditReport: null })
        {
            throw new InvalidOperationException(
                "The retained instruction is not an Audit that is missing its original report.");
        }
        if (receipt.AcceptedCaseId is not null || receipt.CurrentCaseId is not null)
        {
            throw new InvalidOperationException(
                "The retained instruction is already associated with a case.");
        }
        if (await auditEvidenceQueries.GetForReceiptAsync(receipt.Id, cancellationToken) is not null)
        {
            throw new InvalidOperationException(
                "The retained instruction already has recorded original-report evidence.");
        }
    }

    private static string RequireOperationKey(string operationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        var trimmed = operationKey.Trim();
        // The intake mutation history holds the key in a 100-character column;
        // the note's own Unidentified-history column is wider, so this is the
        // binding limit for the shared key.
        if (trimmed.Length > 100)
        {
            throw new ArgumentException(
                "The operation key must be 100 characters or fewer.",
                nameof(operationKey));
        }
        return trimmed;
    }
}
