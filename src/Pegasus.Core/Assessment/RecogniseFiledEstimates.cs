using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Assessment;

/// <summary>
/// One confirmed, current version that has not yet been read for whether it
/// is an estimate.
/// </summary>
public sealed record EstimateRecognitionCandidate(
    Guid CaseId,
    Guid DocumentId,
    Guid VersionId,
    string FileName,
    string MediaType,
    long ContentLength,
    string Sha256,
    bool IsGenerated);

/// <summary>
/// The versions the Worker's estimate recognition reads, and where it
/// records what it found.
/// </summary>
public interface IEstimateRecognitionCandidates
{
    /// <summary>
    /// Confirmed, current versions with no recorded answer, newest first, at
    /// most <paramref name="maximumItems"/>.
    /// </summary>
    Task<IReadOnlyList<EstimateRecognitionCandidate>> ListAsync(
        int maximumItems,
        CancellationToken cancellationToken);

    /// <summary>Records the answer for a version that has none yet.</summary>
    Task RecordAsync(Guid versionId, bool isEstimate, CancellationToken cancellationToken);

    /// <summary>
    /// A version that could not be read this time. It is not listed again for
    /// a while, so it cannot take a place in every run ahead of newer files.
    /// </summary>
    void Defer(Guid versionId);
}

public sealed record RecogniseFiledEstimatesResult(
    int Candidates,
    int Estimates,
    int NotEstimates,
    int Failures,
    string? FirstFailure);

/// <summary>
/// Reads each newly filed Case file once and records whether it is an
/// estimate, so the Files row offers Import as repair spec only for a file
/// the import itself would read (<see cref="EstimateFormats"/>).
/// </summary>
/// <remarks>
/// It runs as the system worker. A file Pegasus generated, one beyond the
/// import's size bound, or one no single format names is recorded as not an
/// estimate without being read. A read that fails for now is deferred and
/// read again later; nothing is recorded for it.
/// </remarks>
public sealed class RecogniseFiledEstimates(
    IEstimateRecognitionCandidates candidates,
    IReadLogicalDocumentVersion documents,
    IEnumerable<IEstimateDocumentParser> parsers)
{
    public const string ActorId = "estimate-recognition";

    private readonly IEstimateRecognitionCandidates candidates =
        candidates ?? throw new ArgumentNullException(nameof(candidates));
    private readonly IReadLogicalDocumentVersion documents =
        documents ?? throw new ArgumentNullException(nameof(documents));
    private readonly IEstimateDocumentParser[] parsers =
        [.. parsers ?? throw new ArgumentNullException(nameof(parsers))];

    public async Task<RecogniseFiledEstimatesResult> ExecuteAsync(
        int maximumItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var actor = ActionActor.SystemWorker(ActorId);
        var listed = await candidates.ListAsync(maximumItems, cancellationToken);
        var estimates = 0;
        var notEstimates = 0;
        var failures = 0;
        string? firstFailure = null;
        foreach (var candidate in listed)
        {
            if (candidate.IsGenerated
                || candidate.ContentLength is <= 0 or > ImportRawEstimate.MaximumDocumentBytes
                || EstimateFormats.NamedBy(parsers, candidate.FileName, candidate.MediaType) is null)
            {
                await candidates.RecordAsync(candidate.VersionId, isEstimate: false, cancellationToken);
                notEstimates++;
                continue;
            }

            ReadOnlyMemory<byte> content;
            try
            {
                await using var document = await documents.OpenAsync(
                    new(actor, candidate.DocumentId, candidate.VersionId, IntakeAssetId: null,
                        candidate.CaseId, IntakeReceiptId: null, candidate.Sha256, candidate.ContentLength),
                    cancellationToken);
                content = await EstimateFormats.ReadVerifiedAsync(
                    document, candidate.ContentLength, candidate.Sha256, cancellationToken);
            }
            catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
            {
                // A read that failed is not an answer: the version is read
                // again later and the sweep carries on.
                failures++;
                firstFailure ??= exception.GetType().Name;
                candidates.Defer(candidate.VersionId);
                continue;
            }

            bool isEstimate;
            try
            {
                EstimateFormats.Parse(parsers, candidate.FileName, candidate.MediaType, content);
                isEstimate = true;
            }
            catch (EstimateParseRejectedException)
            {
                isEstimate = false;
            }
            catch (Exception exception) when (IsRecoverable(exception, cancellationToken))
            {
                // A parser fault is not a refusal either: the import would
                // fail the same way, so the version waits rather than being
                // recorded as no estimate.
                failures++;
                firstFailure ??= exception.GetType().Name;
                candidates.Defer(candidate.VersionId);
                continue;
            }
            await candidates.RecordAsync(candidate.VersionId, isEstimate, cancellationToken);
            if (isEstimate) estimates++; else notEstimates++;
        }
        return new(listed.Count, estimates, notEstimates, failures, firstFailure);
    }

    private static bool IsRecoverable(Exception exception, CancellationToken cancellationToken) =>
        IntakeExceptionPolicy.IsRecoverable(exception)
        || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);
}
