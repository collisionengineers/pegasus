using System.Diagnostics;
using System.Security.Cryptography;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.ThirdPartyReports;

namespace Pegasus.Core.Assessment;

/// <summary>
/// Reads one filed original report for the Original report cells
/// (<see cref="OriginalReportPrefillPolicy"/>). Both callers read the report
/// file itself — never the whole e-mail it arrived in, whose instruction
/// letter prints dates and references of its own — and a report that cannot be
/// read yields no reading: the Case is still created and the document still
/// marked, and the cells stay hand-entered.
/// </summary>
public interface IReadOriginalReport
{
    /// <summary>
    /// The standalone Audit's original report as retained at intake, before
    /// the Case files it; null when the receipt holds no such evidence or the
    /// report cannot be read.
    /// </summary>
    Task<OriginalReportReading?> ForIntakeAsync(
        Guid receiptId,
        Guid standaloneAuditEvidenceId,
        CancellationToken cancellationToken);

    /// <summary>
    /// A document already filed on the Case, read as the acting staff member;
    /// null when it cannot be read.
    /// </summary>
    Task<OriginalReportReading?> ForDocumentAsync(
        ActionActor actor,
        Guid caseId,
        Guid occurrenceId,
        Guid versionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// A file retained at intake and filed on a Case, read to learn whether it
    /// is a report Pegasus recognises: exactly one catalogued report signature
    /// matches it. A file whose retained bytes are missing or differ is
    /// <see cref="OriginalReportRecognitionOutcome.Unavailable"/>, so a caller
    /// never mistakes an unread file for one that is not a report.
    /// </summary>
    Task<OriginalReportRecognition> RecogniseFiledAssetAsync(
        Guid receiptId,
        IntakeAssetRecord asset,
        CancellationToken cancellationToken);
}

public enum OriginalReportRecognitionOutcome
{
    Recognised,
    NotRecognised,
    Unavailable
}

/// <param name="Reading">The report's reading when it is recognised; otherwise null.</param>
public sealed record OriginalReportRecognition(
    OriginalReportRecognitionOutcome Outcome,
    OriginalReportReading? Reading = null);

public sealed class ReadOriginalReport(
    IIntakeSourceReader sourceReader,
    IStandaloneAuditEvidenceQueries evidence,
    IIntakeReceiptQueries receipts,
    IIntakeArtifactStore artifacts,
    IGetCaseDocumentMetadata metadata,
    IReadLogicalDocumentVersion documents,
    TimeProvider timeProvider) : IReadOriginalReport
{
    /// <summary>A report beyond this size is not read; the estimate import's cap.</summary>
    public const int MaximumDocumentBytes = ImportRawEstimate.MaximumDocumentBytes;

    private const string OutcomeTag = "original_report.prefill";

    private const string RecognitionTag = "original_report.recognition";

    public async Task<OriginalReportReading?> ForIntakeAsync(
        Guid receiptId,
        Guid standaloneAuditEvidenceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var recorded = await evidence.GetForReceiptAsync(receiptId, cancellationToken);
            if (recorded is null || recorded.Id != standaloneAuditEvidenceId)
            {
                return Outcome(null, "no_evidence");
            }

            var asset = await receipts.GetAssetAsync(receiptId, recorded.OriginalReportAssetId, cancellationToken);
            if (asset is null || asset.ContentLength is <= 0 or > MaximumDocumentBytes)
            {
                return Outcome(null, "unavailable");
            }

            // The retained intake bytes, not the Case document: at acceptance
            // the Case's custody of the report has not happened yet.
            var content = await artifacts.ReadAsync(asset.StorageKey, cancellationToken);
            return content is { } bytes
                ? await ReadAsync(bytes, asset.FileName, asset.MediaType, asset.ContentHash, receiptId, cancellationToken)
                : Outcome(null, "unavailable");
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            return Failed(exception);
        }
    }

    public async Task<OriginalReportReading?> ForDocumentAsync(
        ActionActor actor,
        Guid caseId,
        Guid occurrenceId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var retained = await metadata.ExecuteAsync(new(caseId, occurrenceId, versionId, actor), cancellationToken);
            if (retained is null || retained.VersionId != versionId
                || retained.ContentLength is <= 0 or > MaximumDocumentBytes)
            {
                return Outcome(null, "unavailable");
            }

            await using var document = await documents.OpenAsync(
                new(actor, retained.DocumentId, retained.VersionId, IntakeAssetId: null,
                    caseId, IntakeReceiptId: null, retained.Sha256, retained.ContentLength),
                cancellationToken);
            using var buffer = new MemoryStream();
            await document.Content.CopyToAsync(buffer, cancellationToken);
            return await ReadAsync(
                buffer.ToArray(), retained.FileName, retained.MediaType, retained.Sha256,
                Guid.Empty, cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            return Failed(exception);
        }
    }

    public async Task<OriginalReportRecognition> RecogniseFiledAssetAsync(
        Guid receiptId,
        IntakeAssetRecord asset,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (asset.ContentLength > MaximumDocumentBytes)
        {
            return Recognition(OriginalReportRecognitionOutcome.NotRecognised, null, "too_large");
        }

        // A fault reading the retained bytes is the caller's to retry: it is
        // not taken here for a file that is not a report.
        var content = await artifacts.ReadAsync(asset.StorageKey, cancellationToken);
        if (content is not { } bytes)
        {
            return Recognition(OriginalReportRecognitionOutcome.Unavailable, null, "unavailable");
        }

        ReadOriginalReportExtraction extracted;
        try
        {
            extracted = await ExtractAsync(
                bytes, asset.FileName, asset.MediaType, asset.ContentHash, receiptId, cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            Activity.Current?.SetTag(RecognitionTag + ".failure_type", exception.GetType().Name);
            return Recognition(OriginalReportRecognitionOutcome.NotRecognised, null, "failed");
        }

        return extracted switch
        {
            { Status: StatusHashMismatch } =>
                Recognition(OriginalReportRecognitionOutcome.Unavailable, null, extracted.Status),
            { Candidate: null } =>
                Recognition(OriginalReportRecognitionOutcome.NotRecognised, null, extracted.Status),
            _ => Recognition(
                OriginalReportRecognitionOutcome.Recognised,
                OriginalReportPrefillPolicy.Read(extracted.Candidate, asset.ContentHash.ToLowerInvariant()),
                extracted.Status)
        };
    }

    private async Task<OriginalReportReading?> ReadAsync(
        ReadOnlyMemory<byte> content,
        string fileName,
        string mediaType,
        string sha256,
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        var extracted = await ExtractAsync(content, fileName, mediaType, sha256, receiptId, cancellationToken);
        return Outcome(
            extracted.Status is StatusRead or StatusNoSignature
                ? OriginalReportPrefillPolicy.Read(extracted.Candidate, sha256)
                : null,
            extracted.Status);
    }

    private const string StatusHashMismatch = "hash_mismatch";
    private const string StatusNotReadable = "not_readable";
    private const string StatusNoSignature = "no_signature";
    private const string StatusRead = "read";

    /// <summary>
    /// The report file alone, read and matched against the report catalogue:
    /// its candidate when exactly one report signature matches, and how the
    /// reading ended.
    /// </summary>
    private async Task<ReadOriginalReportExtraction> ExtractAsync(
        ReadOnlyMemory<byte> content,
        string fileName,
        string mediaType,
        string sha256,
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        if (content.Length > MaximumDocumentBytes
            || !string.Equals(
                Convert.ToHexStringLower(SHA256.HashData(content.Span)), sha256, StringComparison.OrdinalIgnoreCase))
        {
            return new(null, StatusHashMismatch);
        }

        // Read as automation, never as the Principal API channel: that reader
        // takes its source for a Principal's JSON request, not a report file.
        var read = await sourceReader.ReadAsync(
            new(fileName, mediaType, content, timeProvider.GetUtcNow(), OriginalReportPrefillPolicy.RecorderId,
                new(IntakeSourceChannel.Automation, $"{OriginalReportPrefillPolicy.RecorderId}:{sha256}")),
            cancellationToken);
        if (read.Status != IntakeSourceReadStatus.Readable)
        {
            return new(null, StatusNotReadable);
        }

        var extraction = ThirdPartyReportExtraction.Extract(
            read,
            new(receiptId, sha256, Occurrence: 0, ReaderVersion: read.ReaderVersion, SourceLabel: fileName));
        return new(extraction.Candidate, extraction.Candidate is null ? StatusNoSignature : StatusRead);
    }

    private static OriginalReportReading? Outcome(OriginalReportReading? reading, string outcome)
    {
        Activity.Current?.SetTag(OutcomeTag, outcome);
        return reading;
    }

    private static OriginalReportRecognition Recognition(
        OriginalReportRecognitionOutcome outcome,
        OriginalReportReading? reading,
        string status)
    {
        Activity.Current?.SetTag(RecognitionTag, status);
        return new(outcome, reading);
    }

    private sealed record ReadOriginalReportExtraction(ThirdPartyReportCandidate? Candidate, string Status);

    private static OriginalReportReading? Failed(Exception exception)
    {
        Activity.Current?.SetTag(OutcomeTag, "failed");
        Activity.Current?.SetTag(OutcomeTag + ".failure_type", exception.GetType().Name);
        return null;
    }
}
