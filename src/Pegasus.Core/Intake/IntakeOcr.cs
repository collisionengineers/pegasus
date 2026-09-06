using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

/// <summary>
/// The life of one page-restricted OCR operation.
///
/// <see cref="Unknown"/> is the state that matters. A request that timed out, or
/// whose host restarted between sending and recording, may or may not have
/// reached the provider; saying "failed" would be a guess and re-sending would
/// risk a second charged side effect against the same pages. It stays Unknown
/// until the RECORDED operation is looked up at the provider and the answer is
/// known, and it is never blindly resubmitted.
/// </summary>
public enum IntakeOcrState
{
    Pending,
    Processing,
    Completed,
    RetryScheduled,
    Failed,
    Unknown
}

/// <summary>
/// One page-restricted OCR request, named by the exact immutable source it
/// reads. Either a logical document version or a retained intake asset — never
/// both, and never a storage key.
/// </summary>
/// <param name="QualifiedPages">
/// The pages the reader proved need OCR, in ascending order. Never "the whole
/// document": a page whose embedded text was read is not sent, is not charged
/// for and cannot be overwritten by a provider's reading of it.
/// </param>
/// <param name="OperationKey">
/// The caller's idempotency key for this request. One durable operation per
/// key: a replay finds the recorded operation rather than starting a second.
/// </param>
public sealed record IntakeOcrRequest(
    Guid IntakeReceiptId,
    Guid? DocumentVersionId,
    Guid? IntakeAssetId,
    string SourceSha256,
    long SourceContentLength,
    IReadOnlyList<int> QualifiedPages,
    string OperationKey)
{
    public static void Validate(IntakeOcrRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.IntakeReceiptId == Guid.Empty)
        {
            throw new ArgumentException("An intake receipt identifier is required.", nameof(request));
        }

        // The same exclusive-or the OCR operation table's own check constraint
        // enforces: a pre-case asset has no document version, and a case
        // document is not addressed as an asset.
        if (request.DocumentVersionId is null == request.IntakeAssetId is null)
        {
            throw new ArgumentException(
                "An OCR request names exactly one of a document version or a retained intake asset.",
                nameof(request));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceSha256);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.SourceContentLength);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        ArgumentNullException.ThrowIfNull(request.QualifiedPages);
        if (request.QualifiedPages.Count == 0)
        {
            throw new ArgumentException(
                "An OCR request must name at least one qualified page.",
                nameof(request));
        }

        if (request.QualifiedPages.Any(page => page < 1))
        {
            throw new ArgumentException("Page numbers are one-based.", nameof(request));
        }

        if (request.QualifiedPages.Distinct().Count() != request.QualifiedPages.Count)
        {
            throw new ArgumentException("A page is qualified once.", nameof(request));
        }
    }
}

/// <summary>
/// A bounded region of a page in the provider's own coordinate space, named so
/// that a later reader cannot mistake inches for points.
/// </summary>
public sealed record IntakeOcrBounds(
    double Left,
    double Top,
    double Right,
    double Bottom,
    string Unit);

public sealed record IntakeOcrWord(string Text, double? Confidence, IntakeOcrBounds? Bounds);

public sealed record IntakeOcrLine(
    string Text,
    IntakeOcrBounds? Bounds,
    IReadOnlyList<IntakeOcrWord> Words);

public sealed record IntakeOcrCell(
    int Row,
    int Column,
    string Text,
    IntakeOcrBounds? Bounds);

public sealed record IntakeOcrTable(
    int Ordinal,
    int RowCount,
    int ColumnCount,
    IReadOnlyList<IntakeOcrCell> Cells);

/// <summary>
/// One page as the provider read it. The page number is the number in the
/// SOURCE document, not the index within the submitted subset — a page sent
/// alone still comes back as page 7 of its document.
/// </summary>
public sealed record IntakeOcrPage(
    int Number,
    string Text,
    IReadOnlyList<IntakeOcrLine> Lines,
    IReadOnlyList<IntakeOcrTable> Tables);

/// <param name="Retryable">
/// Whether the same request may safely be sent again. A timeout, a throttle and
/// an outage are retryable; a malformed, inconsistent or unusably poor response
/// is not — it fails closed to staff review rather than being asked again in
/// the hope of a different answer.
/// </param>
public sealed record IntakeOcrFailure(
    string Code,
    string Reason,
    bool Retryable,
    TimeSpan? RetryAfter = null);

/// <summary>
/// What the provider returned, with everything needed to say later exactly what
/// was read and by what: the provider's own operation identity, its model and
/// REST API version, the SHA-256 of the response as received, and the page
/// output itself.
/// </summary>
public sealed record IntakeOcrResult(
    IntakeOcrState State,
    string Provider,
    string ModelId,
    string ApiVersion,
    string? ProviderOperationId = null,
    string? ResponseSha256 = null,
    IReadOnlyList<IntakeOcrPage>? Pages = null,
    IntakeOcrFailure? Failure = null)
{
    public IReadOnlyList<IntakeOcrPage> PageResults => Pages ?? [];
}

/// <summary>
/// The provider-neutral OCR boundary. One implementation exists — Azure
/// Document Intelligence <c>prebuilt-layout</c> — and the stream is forbidden a
/// second vendor or runtime; the interface exists so that Core owns what an OCR
/// result MEANS while Infrastructure owns how one is fetched.
/// </summary>
public interface IIntakeOcrProvider
{
    /// <summary>
    /// Submits the qualified pages and waits for the operation within the
    /// caller's bounded attempt. The returned result is Completed, Failed, or
    /// Unknown when the operation was accepted but its outcome could not be
    /// established inside the attempt.
    /// </summary>
    Task<IntakeOcrResult> AnalyzeAsync(
        IntakeOcrRequest request,
        Stream content,
        CancellationToken cancellationToken);

    /// <summary>
    /// Looks up an operation already recorded as sent, by the provider's own
    /// identity for it. This is the only way out of <see cref="IntakeOcrState.Unknown"/>:
    /// the operation is asked about, never re-sent.
    /// </summary>
    Task<IntakeOcrResult> ReconcileAsync(
        IntakeOcrRequest request,
        string providerOperationId,
        CancellationToken cancellationToken);
}

/// <summary>
/// The durable record of one OCR operation: its identity, its state and, once
/// it completes, the response hash and page output.
/// </summary>
public sealed record IntakeOcrOperation(
    Guid Id,
    Guid IntakeReceiptId,
    Guid? DocumentVersionId,
    Guid? IntakeAssetId,
    string SourceSha256,
    IReadOnlyList<int> QualifiedPages,
    string OperationKey,
    IntakeOcrState State,
    long Version,
    string? ProviderOperationId = null,
    string? ResponseSha256 = null,
    string? LastError = null,
    DateTimeOffset? RetryAtUtc = null,
    int AttemptCount = 0,
    IReadOnlyList<IntakeOcrPage>? Pages = null)
{
    public IReadOnlyList<IntakeOcrPage> PageResults => Pages ?? [];
}

/// <summary>
/// Persistence for OCR operations, on the storage the foundation froze for
/// C-F02. Identity is written BEFORE any HTTP call, so a host that dies mid-send
/// leaves a recorded operation to reconcile rather than an invisible one to
/// repeat.
/// </summary>
public interface IIntakeOcrOperationStore
{
    /// <summary>
    /// The operation for one durable work item, or null when the row does not
    /// exist. A missing row is a fail-closed condition, never an invitation to
    /// invent a request.
    /// </summary>
    Task<IntakeOcrOperation?> FindAsync(Guid operationId, CancellationToken cancellationToken);

    Task<IntakeOcrOperation?> FindByOperationKeyAsync(
        string operationKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the operation's identity and its Pending state. Idempotent on the
    /// operation key: a replay returns the recorded operation and starts no
    /// second one.
    /// </summary>
    Task<IntakeOcrOperation> BeginAsync(
        Guid operationId,
        IntakeOcrRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records that the provider accepted the operation, under the provider's
    /// own identity for it, and moves it to Processing. Written before the
    /// result is awaited, so an interrupted wait leaves something to look up.
    /// </summary>
    Task<IntakeOcrOperation> RecordSubmittedAsync(
        Guid operationId,
        long expectedVersion,
        string providerOperationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stores the response hash, the provider identity and the page output and
    /// makes the operation Completed IN ONE TRANSACTION, so re-analysis never
    /// sees a completed operation whose output is not there yet.
    /// </summary>
    Task<IntakeOcrOperation> CompleteAsync(
        Guid operationId,
        long expectedVersion,
        IntakeOcrResult result,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a non-completing outcome: RetryScheduled with a due time,
    /// Failed, or Unknown.
    /// </summary>
    Task<IntakeOcrOperation> RecordOutcomeAsync(
        Guid operationId,
        long expectedVersion,
        IntakeOcrState state,
        IntakeOcrFailure failure,
        DateTimeOffset? retryAtUtc,
        CancellationToken cancellationToken);
}

public sealed class IntakeOcrOperationConflictException()
    : Exception("The OCR operation changed after it was read.");

/// <summary>
/// The one owner of what an OCR outcome is worth: how long to wait before a
/// safe retry, how many attempts are allowed, and which provider answers are
/// good enough to accept.
/// </summary>
public static class IntakeOcrPolicy
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2)
    ];

    public static int MaximumAttempts => RetryDelays.Length + 1;

    public static TimeSpan? NextAttemptDelay(int attemptCount, IntakeOcrFailure? failure)
    {
        if (attemptCount < 1 || attemptCount >= MaximumAttempts || failure?.Retryable != true)
        {
            return null;
        }

        var delay = RetryDelays[attemptCount - 1];
        return failure.RetryAfter is { } after && after > delay ? after : delay;
    }

    /// <summary>
    /// Whether a completed provider response may be accepted at all.
    ///
    /// A response is refused when it names a page nobody asked for, omits a page
    /// that was asked for, or carries no response hash — each of those means the
    /// output cannot be attributed to the pages it claims to describe. Nothing
    /// here judges a value BY ITS CONFIDENCE: a field is never accepted because
    /// a number was high, and a page is never discarded because a number was
    /// low. Poor text becomes staff review, which is a person's judgement.
    /// </summary>
    public static IntakeOcrFailure? Validate(IntakeOcrRequest request, IntakeOcrResult result)
    {
        IntakeOcrRequest.Validate(request);
        ArgumentNullException.ThrowIfNull(result);
        if (string.IsNullOrWhiteSpace(result.ResponseSha256))
        {
            return new(
                "ocr_response_unattributable",
                "The provider response carries no content hash, so what was read cannot be evidenced.",
                Retryable: false);
        }

        if (!string.Equals(result.ApiVersion, IntakeOcrProviderIdentity.ApiVersion, StringComparison.Ordinal))
        {
            return new(
                "ocr_api_version_unexpected",
                $"The response was produced by API version '{result.ApiVersion}', not {IntakeOcrProviderIdentity.ApiVersion}.",
                Retryable: false);
        }

        var returned = result.PageResults.Select(page => page.Number).ToArray();
        if (returned.Length != returned.Distinct().Count())
        {
            return new(
                "ocr_pages_inconsistent",
                "The provider returned the same page more than once.",
                Retryable: false);
        }

        var missing = request.QualifiedPages.Except(returned).ToArray();
        if (missing.Length > 0)
        {
            return new(
                "ocr_pages_missing",
                $"The provider returned no output for page(s) {string.Join(", ", missing)}.",
                Retryable: false);
        }

        var unexpected = returned.Except(request.QualifiedPages).ToArray();
        return unexpected.Length > 0
            ? new(
                "ocr_pages_unexpected",
                $"The provider returned page(s) {string.Join(", ", unexpected)} that were not submitted.",
                Retryable: false)
            : null;
    }
}

/// <summary>
/// The one approved OCR boundary, named once. The REST API version is pinned:
/// a response produced by any other version is refused rather than mapped on
/// the assumption that the shape did not change.
/// </summary>
public static class IntakeOcrProviderIdentity
{
    public const string Provider = "azure-document-intelligence";
    public const string ModelId = "prebuilt-layout";
    public const string ApiVersion = "2024-11-30";
}

/// <summary>
/// The typed handler the durable external-work router invokes for an intake OCR
/// work item.
/// </summary>
public interface IProcessIntakeOcr
{
    Task ExecuteAsync(Guid workItemId, CancellationToken cancellationToken);
}

/// <summary>
/// One durable attempt at one OCR operation, and the single owner of the order
/// those steps happen in.
///
/// Identity is recorded before the HTTP call; the provider's own operation id is
/// recorded as soon as the provider accepts the work and before its result is
/// awaited; the response hash and the page output are stored atomically with the
/// completion; and only then is the instruction re-analysed, exactly once. A
/// timeout, a throttle or an outage schedules a safe retry. Anything malformed,
/// inconsistent or unattributable fails closed to staff review and creates no
/// candidate. An operation whose side effect is uncertain becomes Unknown and is
/// reconciled by asking the provider about the recorded operation — never by
/// sending the pages again.
/// </summary>
public sealed class ProcessIntakeOcr(
    IIntakeOcrOperationStore store,
    IIntakeOcrProvider provider,
    IReadLogicalDocumentVersion documentReader,
    AnalyzeRetainedInstruction analyzeRetainedInstruction,
    IIntakeReceiptQueries receiptQueries,
    TimeProvider timeProvider) : IProcessIntakeOcr
{
    /// <summary>
    /// The automation identity this work runs as. The same one intake
    /// reconciliation uses, read from its owner rather than repeated.
    /// </summary>
    private static readonly ActionActor OcrActor =
        ActionActor.Automation(ReconcileUnidentifiedDestinations.AutomationActorId);

    public async Task ExecuteAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException("An OCR work item identifier is required.", nameof(workItemId));
        }

        var operation = await store.FindAsync(workItemId, cancellationToken)
            ?? throw new InvalidOperationException("The intake OCR operation is unavailable.");

        // A terminal operation is done. A replay of the queue message finds it
        // terminal and stops, so one durable operation has one side effect
        // however many times the message is delivered.
        if (operation.State is IntakeOcrState.Completed or IntakeOcrState.Failed)
        {
            return;
        }

        var receipt = await receiptQueries.GetAsync(operation.IntakeReceiptId, cancellationToken);
        var asset = receipt is null || operation.IntakeAssetId is not { } assetId
            ? null
            : receipt.AssetRecords.SingleOrDefault(record => record.Id == assetId);

        // The length A04 verifies is the length the receipt recorded for the
        // asset. Nothing here invents one, and a source whose recorded hash no
        // longer matches the operation's is refused rather than read: an OCR
        // result must be attributable to exactly the bytes it was asked about.
        if (asset is null
            || !string.Equals(asset.ContentHash, operation.SourceSha256, StringComparison.OrdinalIgnoreCase))
        {
            await store.RecordOutcomeAsync(
                operation.Id,
                operation.Version,
                IntakeOcrState.Failed,
                new(
                    "ocr_source_unavailable",
                    "The retained source named by the operation is not available under the hash it was recorded with.",
                    Retryable: false),
                retryAtUtc: null,
                cancellationToken);
            return;
        }

        var request = new IntakeOcrRequest(
            operation.IntakeReceiptId,
            operation.DocumentVersionId,
            operation.IntakeAssetId,
            operation.SourceSha256,
            asset.ContentLength,
            operation.QualifiedPages,
            operation.OperationKey);

        // An operation already sent is never sent again. It is asked about.
        if (operation.State is IntakeOcrState.Unknown or IntakeOcrState.Processing
            && operation.ProviderOperationId is { } recorded)
        {
            await ApplyAsync(
                operation,
                receipt!,
                request,
                await ReconcileAsync(request, recorded, cancellationToken),
                cancellationToken);
            return;
        }

        // An operation recorded as sent WITHOUT the provider's identity for it
        // cannot be asked about and must not be repeated: the pages may already
        // have been read and charged for. It stays Unknown for an operator.
        if (operation.State is IntakeOcrState.Unknown or IntakeOcrState.Processing)
        {
            await store.RecordOutcomeAsync(
                operation.Id,
                operation.Version,
                IntakeOcrState.Unknown,
                new(
                    "ocr_operation_unidentified",
                    "The operation was recorded as sent but the provider returned no identity for it, so it can be neither looked up nor safely repeated.",
                    Retryable: false),
                retryAtUtc: null,
                cancellationToken);
            return;
        }

        await ApplyAsync(
            operation,
            receipt!,
            request,
            await SubmitAsync(operation, request, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Opens the exact immutable source through A04 and submits only the
    /// qualified pages. The provider's identity for the operation is recorded as
    /// soon as it is known, so an attempt interrupted after this point is
    /// reconcilable rather than lost.
    /// </summary>
    private async Task<IntakeOcrResult> SubmitAsync(
        IntakeOcrOperation operation,
        IntakeOcrRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var content = await documentReader.OpenAsync(
                new(
                    OcrActor,
                    DocumentId: null,
                    request.DocumentVersionId,
                    request.IntakeAssetId,
                    CaseId: null,
                    request.IntakeReceiptId,
                    request.SourceSha256,
                    request.SourceContentLength),
                cancellationToken);
            return await provider.AnalyzeAsync(request, content.Content, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            // The source could not be opened, or the send did not complete. The
            // send is the uncertain half: nothing here knows whether the
            // provider saw it, and the recorded operation carries no provider
            // identity, so the honest state is Unknown.
            return new(
                operation.ProviderOperationId is null ? IntakeOcrState.Unknown : IntakeOcrState.Processing,
                IntakeOcrProviderIdentity.Provider,
                IntakeOcrProviderIdentity.ModelId,
                IntakeOcrProviderIdentity.ApiVersion,
                operation.ProviderOperationId,
                Failure: new(
                    "ocr_dependency_failure",
                    "The OCR request did not complete: " + exception.GetType().Name,
                    Retryable: true));
        }
    }

    private async Task<IntakeOcrResult> ReconcileAsync(
        IntakeOcrRequest request,
        string providerOperationId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await provider.ReconcileAsync(request, providerOperationId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            return new(
                IntakeOcrState.Unknown,
                IntakeOcrProviderIdentity.Provider,
                IntakeOcrProviderIdentity.ModelId,
                IntakeOcrProviderIdentity.ApiVersion,
                providerOperationId,
                Failure: new(
                    "ocr_reconciliation_failure",
                    "The recorded operation could not be looked up: " + exception.GetType().Name,
                    Retryable: true));
        }
    }

    private async Task ApplyAsync(
        IntakeOcrOperation operation,
        IntakeReceipt receipt,
        IntakeOcrRequest request,
        IntakeOcrResult result,
        CancellationToken cancellationToken)
    {
        var current = operation;

        // Whatever else happens, the provider's identity for the operation is
        // recorded the moment it is known.
        if (result.ProviderOperationId is { } providerOperationId
            && !string.Equals(current.ProviderOperationId, providerOperationId, StringComparison.Ordinal))
        {
            current = await store.RecordSubmittedAsync(
                current.Id,
                current.Version,
                providerOperationId,
                cancellationToken);
        }

        var nowUtc = timeProvider.GetUtcNow();
        if (result.State == IntakeOcrState.Completed)
        {
            var refusal = IntakeOcrPolicy.Validate(request, result);
            if (refusal is not null)
            {
                // Fail closed. A response that cannot be attributed to the pages
                // it claims is not partially accepted: no page output is stored,
                // no candidate is created and staff see the refusal.
                await store.RecordOutcomeAsync(
                    current.Id,
                    current.Version,
                    IntakeOcrState.Failed,
                    refusal,
                    retryAtUtc: null,
                    cancellationToken);
                return;
            }

            await store.CompleteAsync(current.Id, current.Version, result, cancellationToken);
            await ReanalyzeAsync(current, receipt, cancellationToken);
            return;
        }

        var failure = result.Failure ?? new(
            "ocr_unspecified_failure",
            "The provider returned no usable result and no reason.",
            Retryable: false);
        var delay = IntakeOcrPolicy.NextAttemptDelay(current.AttemptCount + 1, failure);
        if (delay is { } retryIn)
        {
            await store.RecordOutcomeAsync(
                current.Id,
                current.Version,
                IntakeOcrState.RetryScheduled,
                failure,
                nowUtc.Add(retryIn),
                cancellationToken);
            return;
        }

        // Out of attempts, or not retryable. An uncertain side effect stays
        // Unknown — a person decides what happened to it — while a definite
        // refusal is Failed.
        await store.RecordOutcomeAsync(
            current.Id,
            current.Version,
            result.State == IntakeOcrState.Unknown ? IntakeOcrState.Unknown : IntakeOcrState.Failed,
            failure,
            retryAtUtc: null,
            cancellationToken);
    }

    /// <summary>
    /// Re-enters instruction analysis exactly once for the completed operation,
    /// under an operation key derived from the OCR operation's own key. A replay
    /// of the OCR work therefore replays the analysis rather than recording a
    /// second set of candidates for the same reading.
    /// </summary>
    private async Task ReanalyzeAsync(
        IntakeOcrOperation operation,
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        var key = $"ocr:{operation.OperationKey}";
        await analyzeRetainedInstruction.ExecuteAsync(
            new(
                OcrActor,
                receipt.Id,
                receipt.Version,
                key.Length > 100 ? key[..100] : key,
                operation.IntakeAssetId),
            cancellationToken);
    }
}
