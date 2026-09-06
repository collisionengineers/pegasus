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
    /// <param name="onAccepted">
    /// Invoked with the provider's own identity for the operation the moment
    /// the submission is accepted and BEFORE its result is awaited. This is
    /// what makes an interrupted wait reconcilable: the pages have reached the
    /// provider, and the caller has committed the name to ask about them by.
    /// An implementation that has accepted work must not begin polling without
    /// reporting the identity first.
    /// </param>
    Task<IntakeOcrResult> AnalyzeAsync(
        IntakeOcrRequest request,
        Stream content,
        Func<string, Task> onAccepted,
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
    IReadOnlyList<IntakeOcrPage>? Pages = null,
    DateTimeOffset? SubmitAttemptedAtUtc = null,
    DateTimeOffset? SubmittedAtUtc = null)
{
    public IReadOnlyList<IntakeOcrPage> PageResults => Pages ?? [];

    /// <summary>
    /// Whether a submission was begun for this operation and no outcome for it
    /// was ever recorded. The pages may already have been read and charged for,
    /// which is why such an operation is looked up, or left to a person, and is
    /// never sent again.
    /// </summary>
    public bool SubmissionUnaccountedFor =>
        SubmitAttemptedAtUtc is not null && SubmittedAtUtc is null;
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
    /// Records that a submission is about to be made, and moves the operation
    /// to Processing. Committed BEFORE the request leaves the host and never on
    /// the caller's cancellable token, because its whole purpose is to outlive
    /// the attempt: a host that dies between here and
    /// <see cref="RecordSubmittedAsync"/> leaves a row saying "a submission was
    /// begun and its outcome is unaccounted for", which is what stops the next
    /// delivery sending the same pages again.
    /// </summary>
    Task<IntakeOcrOperation> RecordSubmitAttemptAsync(
        Guid operationId,
        long expectedVersion,
        DateTimeOffset attemptedAtUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the provider's own identity for the accepted operation, and when
    /// it accepted it. Its own committed write, made the moment the provider
    /// answers the submission and before the result is polled, so an
    /// interrupted wait leaves something to look the operation up by.
    /// </summary>
    Task<IntakeOcrOperation> RecordSubmittedAsync(
        Guid operationId,
        long expectedVersion,
        string providerOperationId,
        DateTimeOffset submittedAtUtc,
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
    ///
    /// A RetryScheduled outcome also clears the unaccounted-for submission,
    /// because Core only ever schedules a safe retry where the pages were NOT
    /// read - the provider refused the submission, the source could not be
    /// opened, or it is the LOOKUP of a named operation being retried. An
    /// uncertain send with nothing to look up becomes Unknown and keeps the
    /// mark.
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
    IAnalyzeRetainedInstruction analyzeRetainedInstruction,
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
        // Which STATE it is in does not change that: a retry scheduled after a
        // failed wait is still a retry of the LOOKUP, because the pages have
        // already reached the provider and reading them again would be a second
        // charged side effect against the same source.
        if (operation.ProviderOperationId is { } recorded)
        {
            await ApplyAsync(
                operation,
                receipt!,
                request,
                await ReconcileAsync(request, recorded, cancellationToken),
                cancellationToken);
            return;
        }

        // A submission was begun for this operation and no outcome for it was
        // ever recorded, and the provider named nothing to ask about. The pages
        // may already have been read and charged for, so it is neither looked
        // up nor repeated: it waits for a person. The mark is committed before
        // the request leaves the host, which is what makes a host that DIED
        // mid-send indistinguishable from one that recorded this itself.
        if (operation.SubmissionUnaccountedFor
            || operation.State is IntakeOcrState.Unknown or IntakeOcrState.Processing)
        {
            await store.RecordOutcomeAsync(
                operation.Id,
                operation.Version,
                IntakeOcrState.Unknown,
                new(
                    "ocr_operation_unidentified",
                    "A submission was begun for this operation and the provider returned no identity for it, so it can be neither looked up nor safely repeated.",
                    Retryable: false),
                retryAtUtc: null,
                CancellationToken.None);
            return;
        }

        // The submission advances the recorded operation as it goes - the
        // attempt, then the provider's identity for it - and what it advanced
        // to is what the outcome is written against.
        var attempt = new Attempt(operation);
        var result = await SubmitAsync(attempt, request, cancellationToken);
        await ApplyAsync(attempt.Current, receipt!, request, result, cancellationToken);
    }

    /// <summary>
    /// One submission in flight, and the recorded operation as it stands after
    /// the writes that submission has already committed. The provider reports
    /// the accepted identity from inside its own call, so the version the next
    /// write must be optimistic on changes underneath the caller; this is where
    /// that is held rather than guessed.
    /// </summary>
    private sealed class Attempt(IntakeOcrOperation operation)
    {
        public IntakeOcrOperation Current { get; set; } = operation;
    }

    /// <summary>
    /// Opens the exact immutable source through A04 and submits only the
    /// qualified pages. The provider's identity for the operation is recorded as
    /// soon as it is known, so an attempt interrupted after this point is
    /// reconcilable rather than lost.
    /// </summary>
    private async Task<IntakeOcrResult> SubmitAsync(
        Attempt attempt,
        IntakeOcrRequest request,
        CancellationToken cancellationToken)
    {
        LogicalDocumentContent content;
        try
        {
            content = await documentReader.OpenAsync(
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
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            // Nothing was sent, so nothing is uncertain: the source could not be
            // opened this time and the same request may safely be made again.
            return Failure(
                "ocr_source_unreadable",
                "The retained source could not be opened: " + exception.GetType().Name,
                retryable: true);
        }

        // Nothing has been sent yet, and this is the last moment at which that
        // is still true. The attempt is committed, uncancellably, before the
        // request goes out, so the row can always answer the only question that
        // matters after a crash: might these pages already have been read?
        attempt.Current = await store.RecordSubmitAttemptAsync(
            attempt.Current.Id,
            attempt.Current.Version,
            timeProvider.GetUtcNow(),
            CancellationToken.None);

        // The provider's own name for the operation, the moment it accepts the
        // work. Recorded here even if the write that follows fails, so a
        // failure to persist it does not also lose it.
        string? accepted = null;
        async Task OnAcceptedAsync(string providerOperationId)
        {
            accepted = providerOperationId;
            attempt.Current = await store.RecordSubmittedAsync(
                attempt.Current.Id,
                attempt.Current.Version,
                providerOperationId,
                timeProvider.GetUtcNow(),
                CancellationToken.None);
        }

        await using (content)
        {
            try
            {
                return await provider.AnalyzeAsync(
                    request,
                    content.Content,
                    OnAcceptedAsync,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                // The send did not complete, and nothing here knows whether the
                // provider saw it. That is the definition of Unknown, and it is
                // why the request is not simply made again.
                return new(
                    IntakeOcrState.Unknown,
                    IntakeOcrProviderIdentity.Provider,
                    IntakeOcrProviderIdentity.ModelId,
                    IntakeOcrProviderIdentity.ApiVersion,
                    accepted ?? attempt.Current.ProviderOperationId,
                    Failure: new(
                        "ocr_dependency_failure",
                        "The OCR request did not complete: " + exception.GetType().Name,
                        Retryable: true));
            }
        }
    }

    private static IntakeOcrResult Failure(string code, string reason, bool retryable) =>
        new(
            IntakeOcrState.Failed,
            IntakeOcrProviderIdentity.Provider,
            IntakeOcrProviderIdentity.ModelId,
            IntakeOcrProviderIdentity.ApiVersion,
            Failure: new(code, reason, retryable));

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
        // recorded the moment it is known. On the submission path the provider
        // reported it from inside its own call and this has already happened; a
        // reconciliation, and a submission whose identity write did not land,
        // arrive here. Every write below is made on an uncancellable token: an
        // outcome that HAS happened is recorded even as the host shuts down,
        // because the alternative is a row that says nothing about pages that
        // were read.
        if (result.ProviderOperationId is { } providerOperationId
            && !string.Equals(current.ProviderOperationId, providerOperationId, StringComparison.Ordinal))
        {
            current = await store.RecordSubmittedAsync(
                current.Id,
                current.Version,
                providerOperationId,
                timeProvider.GetUtcNow(),
                CancellationToken.None);
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
                    CancellationToken.None);
                return;
            }

            await store.CompleteAsync(current.Id, current.Version, result, CancellationToken.None);
            await ReanalyzeAsync(current, receipt, request, result, cancellationToken);
            return;
        }

        var failure = result.Failure ?? new(
            "ocr_unspecified_failure",
            "The provider returned no usable result and no reason.",
            Retryable: false);
        // An uncertain side effect with no provider identity to look up is the
        // one thing that is never scheduled for another attempt: repeating it
        // would risk reading — and being charged for — the same pages twice,
        // and nothing here can tell whether that has already happened. It waits
        // for a person.
        if (result.State == IntakeOcrState.Unknown && current.ProviderOperationId is null)
        {
            await store.RecordOutcomeAsync(
                current.Id,
                current.Version,
                IntakeOcrState.Unknown,
                failure,
                retryAtUtc: null,
                CancellationToken.None);
            return;
        }

        // A safe retry is only ever scheduled where the pages were NOT read:
        // the provider refused the submission outright, the source could not be
        // opened, or the operation is named and it is the LOOKUP being retried.
        // Recording it is what clears the unaccounted-for submission, and so
        // what allows the next delivery to send.
        var delay = IntakeOcrPolicy.NextAttemptDelay(current.AttemptCount + 1, failure);
        if (delay is { } retryIn)
        {
            await store.RecordOutcomeAsync(
                current.Id,
                current.Version,
                IntakeOcrState.RetryScheduled,
                failure,
                nowUtc.Add(retryIn),
                CancellationToken.None);
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
            CancellationToken.None);
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
        IntakeOcrRequest ocrRequest,
        IntakeOcrResult ocrResult,
        CancellationToken cancellationToken)
    {
        var key = $"ocr:{operation.OperationKey}";
        await analyzeRetainedInstruction.ExecuteAsync(
            new(
                OcrActor,
                receipt.Id,
                receipt.Version,
                key.Length > 100 ? key[..100] : key,
                operation.IntakeAssetId,
                new(operation.SourceSha256, ocrRequest.QualifiedPages, ocrResult)),
            cancellationToken);
    }
}
