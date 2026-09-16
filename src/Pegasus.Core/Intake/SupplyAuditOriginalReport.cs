using System.Security.Cryptography;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Core.Intake;

public sealed record SupplyAuditOriginalReportRequest(
    Guid UnidentifiedItemId,
    long ExpectedItemVersion,
    long ExpectedReceiptVersion,
    ActionActor Actor,
    string OperationKey,
    string FileName,
    string MediaType,
    ReadOnlyMemory<byte> Content);

public sealed record SupplyAuditOriginalReportResult(Guid ReceiptId, Guid ReportAssetId, bool IsDuplicate);

public sealed class SuppliedOriginalReportRefusalException(string message) : InvalidOperationException(message);

public interface ISupplyAuditOriginalReport
{
    Task<SupplyAuditOriginalReportResult> ExecuteAsync(
        SupplyAuditOriginalReportRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Supplies the report which determines whether a waiting Audit is repairable or total loss.</summary>
public sealed class SupplyAuditOriginalReport(
    IUnidentifiedStore unidentifiedStore,
    IIntakeReceiptQueries receiptQueries,
    IIntakeSourceReader sourceReader,
    IIntakeMutationStore mutationStore,
    TimeProvider timeProvider) : ISupplyAuditOriginalReport
{
    public const string RefusalMessage =
        "This report does not state Repairable or Total loss clearly, so Pegasus cannot derive the Audit reference from it.";

    public async Task<SupplyAuditOriginalReportResult> ExecuteAsync(
        SupplyAuditOriginalReportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.UnidentifiedItemId == Guid.Empty)
        {
            throw new ArgumentException("An Unidentified item identifier is required.", nameof(request));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(request.ExpectedItemVersion);
        ArgumentOutOfRangeException.ThrowIfNegative(request.ExpectedReceiptVersion);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        var operationKey = RequireOperationKey(request.OperationKey);
        var fileName = Path.GetFileName(request.FileName?.Trim() ?? string.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MediaType);
        if (fileName.Length > 260 || request.MediaType.Length > 200)
        {
            throw new ArgumentException("The supplied report's filename or media type is too long.", nameof(request));
        }
        if (request.Content.IsEmpty)
        {
            throw new ArgumentException("The supplied report is empty.", nameof(request));
        }
        if (request.Content.Length > IntakeEnvelopeLimits.MaximumContentLength)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The supplied report exceeds the maximum file size.");
        }

        var hash = Convert.ToHexString(SHA256.HashData(request.Content.Span));
        var replay = await mutationStore.ProbeSuppliedOriginalReportReplayAsync(new(
            request.UnidentifiedItemId, request.ExpectedItemVersion, request.ExpectedReceiptVersion,
            request.Actor, operationKey, fileName, request.MediaType, hash, request.Content.Length), cancellationToken);
        if (replay is not null)
        {
            return new(replay.ReceiptId, replay.ReportAssetId, true);
        }

        // Current-state checks deliberately precede the source read and artifact
        // write. The transaction repeats them after its replay probe.
        var item = await unidentifiedStore.GetAsync(request.UnidentifiedItemId, cancellationToken)
            ?? throw new KeyNotFoundException("The Unidentified item does not exist.");
        if (item.Version != request.ExpectedItemVersion
            || item.State != UnidentifiedState.Open
            || item.Origin.Kind != UnidentifiedOriginKind.Receipt
            || item.ReasonCode != UnidentifiedReasonCode.AuditOriginalReportMissing)
        {
            throw new IntakeVersionConflictException();
        }
        var receipt = await receiptQueries.GetAsync(item.Origin.Id, cancellationToken)
            ?? throw new KeyNotFoundException("The Unidentified item's retained receipt does not exist.");
        if (receipt.Version != request.ExpectedReceiptVersion
            || receipt.MailClassificationDecision is not { CaseType: Pegasus.Core.Cases.CaseType.Audit, StandaloneAuditReport: null }
            || receipt.CurrentCaseId is not null)
        {
            throw new IntakeVersionConflictException();
        }
        var sourceLabel = $"supplied original report: {fileName}";
        var read = await sourceReader.ReadAsync(new(
            fileName, request.MediaType, request.Content, receipt.ReceivedAtUtc, request.Actor.SubjectId,
            new(IntakeSourceChannel.ManualUpload, $"supplied-original-report:{receipt.Id:N}")), cancellationToken);
        if (AuditOriginalReportOutcomePolicy.Evaluate(read, sourceLabel) is null)
        {
            throw new SuppliedOriginalReportRefusalException(RefusalMessage);
        }

        var result = await mutationStore.AttachSuppliedOriginalReportAsync(new(
            item.Id, request.ExpectedItemVersion, receipt.Id, request.ExpectedReceiptVersion,
            request.Actor, operationKey, fileName, request.MediaType, sourceLabel, hash,
            request.Content.Length, request.Content), timeProvider.GetUtcNow(), cancellationToken);
        return new(result.ReceiptId, result.ReportAssetId, result.IsDuplicate);
    }

    private static string RequireOperationKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var trimmed = value.Trim();
        if (trimmed.Length > 100)
        {
            throw new ArgumentException("The operation key must be 100 characters or fewer.", nameof(value));
        }
        return trimmed;
    }
}
