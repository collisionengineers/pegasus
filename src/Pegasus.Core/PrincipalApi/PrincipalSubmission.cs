using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.PrincipalApi;

/// <summary>
/// API-01 (FRD-09 § Principal API principal and contract boundary; ADR-0004):
/// an authenticated Principal submits one instruction, with zero or more
/// files inline, that is retained as one durable intake source bound to that
/// Principal, and reads back only its own submission's receipt and result.
/// The Principal actor is the Principal; the credential only proves who is
/// calling.
/// </summary>
public enum PrincipalSubmissionError
{
    EnvelopeExceeded,
    IdempotencyKeyConflict
}

public sealed class PrincipalSubmissionException(PrincipalSubmissionError error)
    : Exception("The Principal submission could not be completed.")
{
    public PrincipalSubmissionError Error { get; } = error;
}

/// <summary>
/// One submitted file, typed by its extension. <paramref name="Field"/> is its
/// path in the request, for a refusal to name; <paramref name="SourceLabel"/>
/// is the label its attachment is retained under.
/// </summary>
public sealed record PrincipalSubmissionFile(
    string Field,
    string SourceLabel,
    string FileName,
    string MediaType,
    ReadOnlyMemory<byte> Content);

/// <summary>
/// One submission. <paramref name="RawBody"/> is the request exactly as it
/// arrived: it is parsed here and is what Pegasus retains as the source, so
/// the case's origin is the Principal's own words rather than a rendering of
/// them.
/// </summary>
public sealed record PrincipalSubmissionRequest(
    PrincipalCredentialAuthentication Credential,
    string IdempotencyKey,
    ReadOnlyMemory<byte> RawBody,
    string CorrelationId);

/// <summary>
/// The durable submission row: the Principal binding processing reads
/// (<see cref="IPrincipalSubmissionBindings"/>) and what that Principal
/// declared. Its id is derived from the Principal and the idempotency key, and
/// its intake source carries that id as its token, so the intake source
/// identity is what makes a retry a replay. The files are the attachments of
/// that one source.
/// </summary>
public sealed record PrincipalSubmissionRecord(
    Guid Id,
    Guid PrincipalId,
    DateTimeOffset ReceivedAtUtc,
    PrincipalInstruction Instruction);

/// <summary>
/// A retained submission whose first <c>Accepted</c> history row was never
/// written. A bare reservation, whose retention never happened, is not a
/// candidate at all.
/// </summary>
public sealed record PrincipalSubmissionAcceptCandidate(
    Guid SubmissionId,
    Guid PrincipalId,
    DateTimeOffset ReceivedAtUtc);

/// <summary>
/// What intake needs to know about the submission a source belongs to: which
/// Principal it was bound to and what that Principal declared. Both come from
/// the retained submission row, never from the submitted content.
/// </summary>
public sealed record PrincipalSubmissionBinding(
    Guid SubmissionId,
    Guid PrincipalId,
    string PrincipalCode,
    PrincipalInstruction Instruction);

/// <summary>
/// Returned the moment the submission is durably received. It says nothing
/// about processing (operator decision): the result is read separately.
/// </summary>
public sealed record PrincipalSubmissionReceipt(
    Guid SubmissionId,
    DateTimeOffset ReceivedAtUtc,
    string? PrincipalReference,
    bool Replayed);

/// <summary>
/// The submission's own result: the Case/PO once processing allocated one,
/// otherwise the intake pipeline's own decision and failure vocabulary (never a
/// Principal-only list). One submission is one receipt, so there is one outcome
/// rather than a per-file table.
/// </summary>
public sealed record PrincipalSubmissionResult(
    Guid SubmissionId,
    DateTimeOffset ReceivedAtUtc,
    string? PrincipalReference,
    QueuedIntakeStatusKind Status,
    IntakeDecision? Decision,
    IntakeAllocationFailureKind? AllocationFailure,
    string? FailureCode,
    string? CaseReference);

public interface IPrincipalSubmissionStore
{
    /// <summary>
    /// Inserts the submission row, or returns the row already stored under its
    /// id when an earlier request with the same idempotency key wrote it.
    /// </summary>
    Task<PrincipalSubmissionRecord> GetOrCreateAsync(
        PrincipalSubmissionRecord record,
        CancellationToken cancellationToken);

    Task<PrincipalSubmissionRecord?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The oldest retained submissions with no <c>Accepted</c> history row.
    /// A bare reservation is excluded by the query itself rather than skipped
    /// by the sweep: nothing ever removes one, so a bounded oldest-first window
    /// that admitted them would fill with rows it can never repair and starve
    /// the ones it can.
    /// </summary>
    Task<IReadOnlyList<PrincipalSubmissionAcceptCandidate>> ListAcceptRecoveryCandidatesAsync(
        int maximumItems,
        CancellationToken cancellationToken);
}

/// <summary>
/// Read by <c>ProcessIntake</c> for a <see cref="IntakeSourceChannel.PrincipalApi"/>
/// source: the Principal code the submission was bound to, or null when the
/// source belongs to no retained submission.
/// </summary>
public interface IPrincipalSubmissionBindings
{
    Task<PrincipalSubmissionBinding?> FindAsync(
        IntakeSourceIdentity sourceIdentity,
        CancellationToken cancellationToken);
}

public interface ISubmitPrincipalInstruction
{
    Task<PrincipalSubmissionReceipt> ExecuteAsync(
        PrincipalSubmissionRequest request,
        CancellationToken cancellationToken);
}

public interface IPrincipalAttachmentAdmission
{
    Task RequireSupportedAsync(
        IReadOnlyList<PrincipalSubmissionFile> files,
        CancellationToken cancellationToken);
}

public interface IGetPrincipalSubmissionResult
{
    /// <summary>
    /// Null when the submission does not exist or belongs to another
    /// Principal — the two are indistinguishable to the caller (FRD-09:
    /// cross-principal disclosure fails closed).
    /// </summary>
    Task<PrincipalSubmissionResult?> ExecuteAsync(
        PrincipalCredentialAuthentication credential,
        Guid submissionId,
        CancellationToken cancellationToken);
}

public static class PrincipalSubmissionPolicy
{
    public const int MaximumIdempotencyKeyLength = 200;
    public const int MaximumFileNameLength = 260;
    public const string ActionHistoryAggregateType = "PrincipalSubmission";

    public static ActionActor Actor(PrincipalCredentialAuthentication credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        return ActionActor.Principal(credential.PrincipalId);
    }

    public static string NormalizeIdempotencyKey(string? idempotencyKey)
    {
        var normalized = idempotencyKey?.Trim();
        if (string.IsNullOrEmpty(normalized) || normalized.Length > MaximumIdempotencyKeyLength)
        {
            throw new ArgumentException(
                $"An idempotency key of at most {MaximumIdempotencyKeyLength} characters is required.",
                nameof(idempotencyKey));
        }

        return normalized;
    }

    /// <summary>
    /// The envelope bound is the Principal API's own
    /// (<see cref="IntakeEnvelopeLimits.MaximumPrincipalApiEnvelopeLength"/>):
    /// every file arrives inline as base64 in one request body, so the whole
    /// submission is bounded together rather than only file by file.
    ///
    /// The per-file bound is the channel's own
    /// (<see cref="IntakeEnvelopeLimits.MaximumPrincipalApiFileLength"/>) and
    /// not the manual channel's larger cap: one Principal API file may never be
    /// allowed past the envelope that carries it (C07 item 5).
    ///
    /// No file is required: an instruction may be declared on its own.
    /// </summary>
    public static void RequireEnvelope(IReadOnlyList<PrincipalSubmissionFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (files.Count > IntakeEnvelopeLimits.MaximumBatchFileCount
            || files.Any(file => file.Content.Length > IntakeEnvelopeLimits.MaximumPrincipalApiFileLength)
            || files.Sum(file => (long)file.Content.Length)
                > IntakeEnvelopeLimits.MaximumPrincipalApiEnvelopeLength)
        {
            throw new PrincipalSubmissionException(PrincipalSubmissionError.EnvelopeExceeded);
        }
    }

    /// <summary>
    /// The submission a Principal's idempotency key names. Derived rather than
    /// minted, so every retry of one key reaches the same row and the same
    /// intake source, and two Principals using the same key never meet.
    /// </summary>
    public static Guid SubmissionId(Guid principalId, string idempotencyKey) =>
        DerivedId($"principal-submission:{principalId:N}:{idempotencyKey}");

    public static string SubmissionToken(Guid submissionId) => submissionId.ToString("N");

    public static IntakeSourceIdentity SourceIdentity(Guid submissionId) =>
        new(IntakeSourceChannel.PrincipalApi, SubmissionToken(submissionId));

    public static string OperationKey(Guid submissionId) => $"principal-submission:{submissionId:N}";

    /// <summary>
    /// The identity of a submission's one <c>Accepted</c> history row, derived
    /// from its operation key rather than minted per write. The inline request
    /// and the recovery sweep both record that acceptance under this id, so
    /// whichever writes second is refused instead of both landing in permanent
    /// history. Every other history row is its own event and keeps its own id.
    /// </summary>
    public static Guid AcceptedHistoryId(Guid submissionId) => DerivedId(OperationKey(submissionId));

    private static Guid DerivedId(string material)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(material), hash);
        return new Guid(hash[..16]);
    }
}

public sealed class SubmitPrincipalInstruction(
    IPrincipalSubmissionStore store,
    IIntakeSubmission intakeSubmission,
    IPrincipalAttachmentAdmission attachmentAdmission,
    IActionHistoryWriter actionHistory,
    TimeProvider timeProvider) : ISubmitPrincipalInstruction
{
    public async Task<PrincipalSubmissionReceipt> ExecuteAsync(
        PrincipalSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Credential);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CorrelationId);
        var actor = PrincipalSubmissionPolicy.Actor(request.Credential);
        StaffAuthorization.Require(actor, StaffAccessRight.SubmitPrincipalInstruction);

        var idempotencyKey = PrincipalSubmissionPolicy.NormalizeIdempotencyKey(request.IdempotencyKey);
        var (instruction, files) = PrincipalInstructionJson.Parse(request.RawBody);
        PrincipalSubmissionPolicy.RequireEnvelope(files);
        await attachmentAdmission.RequireSupportedAsync(files, cancellationToken);

        var submissionId = PrincipalSubmissionPolicy.SubmissionId(request.Credential.PrincipalId, idempotencyKey);
        var submission = await store.GetOrCreateAsync(
            new(submissionId, request.Credential.PrincipalId, timeProvider.GetUtcNow(), instruction),
            cancellationToken);

        // A reused key with a different body is refused. Once the source is
        // retained, intake compares the bytes; before then -- a retry after a
        // retention that failed -- only the stored declaration can say what the
        // key was first used for, and a different one must not be retained
        // under it.
        if (submission.Instruction != instruction)
        {
            await RefuseReusedKeyAsync(actor, submissionId, request.CorrelationId, cancellationToken);
        }

        // One submission is one receipt, and the retained source is the request
        // as it arrived — the Principal's own instruction, carrying its files
        // exactly as an e-mail carries its attachments.
        ReceivedIntake received;
        try
        {
            received = await intakeSubmission.ExecuteAsync(
                new(
                    PrincipalInstructionPolicy.SourceFileName,
                    PrincipalInstructionPolicy.SourceMediaType,
                    request.RawBody,
                    submission.ReceivedAtUtc,
                    MailClassificationActor.Format(actor),
                    PrincipalSubmissionPolicy.SourceIdentity(submissionId)),
                PrincipalSubmissionPolicy.OperationKey(submissionId),
                cancellationToken);
        }
        catch (IntakeSourceIdentityConflictException)
        {
            await RefuseReusedKeyAsync(actor, submissionId, request.CorrelationId, cancellationToken);
            throw;
        }

        if (received.IsDuplicate)
        {
            // Each replay is an event in its own right, so it keeps its own id
            // and its own request correlation.
            await actionHistory.AppendAsync(
                SubmissionHistory(Guid.NewGuid(), actor, submissionId, "Replayed", request.CorrelationId, null),
                cancellationToken);
        }
        else
        {
            // One submission is accepted once. The row carries the derived
            // identity, so when the recovery sweep has already recorded this
            // acceptance -- it can, for a submission retried long after its own
            // grace window -- that row stands and this write is refused rather
            // than a second Accepted landing in permanent history. Either way
            // the acceptance is recorded, which is all the receipt claims.
            _ = await actionHistory.TryAppendAsync(
                SubmissionHistory(
                    PrincipalSubmissionPolicy.AcceptedHistoryId(submissionId),
                    actor,
                    submissionId,
                    "Accepted",
                    request.CorrelationId,
                    null),
                cancellationToken);
        }
        return new(
            submissionId,
            submission.ReceivedAtUtc,
            submission.Instruction.Draft.ClaimNumber,
            received.IsDuplicate);
    }

    private async Task RefuseReusedKeyAsync(
        ActionActor actor,
        Guid submissionId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await actionHistory.AppendAsync(
            SubmissionHistory(Guid.NewGuid(), actor, submissionId, "Refused", correlationId,
                "The idempotency key was reused with a different submission."),
            cancellationToken);
        throw new PrincipalSubmissionException(PrincipalSubmissionError.IdempotencyKeyConflict);
    }

    private ActionHistoryEntry SubmissionHistory(
        Guid id,
        ActionActor actor,
        Guid submissionId,
        string outcome,
        string correlationId,
        string? reason) =>
        new(
            id,
            PrincipalSubmissionPolicy.ActionHistoryAggregateType,
            submissionId.ToString("D"),
            "Submitted",
            actor,
            timeProvider.GetUtcNow(),
            outcome,
            correlationId,
            reason);
}

public sealed class GetPrincipalSubmissionResult(
    IPrincipalSubmissionStore store,
    IQueuedIntakeStatusQueries statusQueries,
    IIntakeReceiptQueries receiptQueries,
    Pegasus.Core.Triage.ITriageQueries triageQueries) : IGetPrincipalSubmissionResult
{
    public async Task<PrincipalSubmissionResult?> ExecuteAsync(
        PrincipalCredentialAuthentication credential,
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        var actor = PrincipalSubmissionPolicy.Actor(credential);
        // A paused credential still reads its own receipts and results
        // (operator decision); only MaySubmit is withheld.
        StaffAuthorization.Require(actor, StaffAccessRight.SubmitPrincipalInstruction);
        var record = await store.GetAsync(submissionId, cancellationToken);
        if (record is null || record.PrincipalId != credential.PrincipalId)
        {
            return null;
        }

        var status = await statusQueries.FindBySourceIdentityAsync(
            PrincipalSubmissionPolicy.SourceIdentity(record.Id),
            cancellationToken);
        var receipt = status?.ProcessedReceiptId is { } processedId
            ? await receiptQueries.GetAsync(processedId, cancellationToken)
            : null;
        // A Triage request becomes a Triage Case rather than an accepted
        // instruction, so its receipt names no current Case: the result
        // returns the Triage Case's t. reference instead.
        var caseReference = receipt?.CurrentCaseReference
            ?? (receipt is null
                ? null
                : (await triageQueries.GetByOriginReceiptAsync(receipt.Id, cancellationToken))?.Reference);
        return new(
            record.Id,
            record.ReceivedAtUtc,
            record.Instruction.Draft.ClaimNumber,
            status?.Status ?? QueuedIntakeStatusKind.Received,
            receipt?.Decision,
            receipt?.AllocationState?.FailureKind,
            receipt?.FailureCode ?? status?.FailureCode,
            caseReference);
    }
}
