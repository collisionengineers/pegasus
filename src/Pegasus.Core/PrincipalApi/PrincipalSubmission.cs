using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.PrincipalApi;

/// <summary>
/// API-01 (FRD-09 § Principal API principal and contract boundary; ADR-0004):
/// an authenticated Principal submits one instruction envelope — zero or more
/// files — that enters the ordinary grouped durable intake path bound to
/// that Principal, and reads back only its own submission's receipt and
/// result. The Principal actor is the Principal; the credential only proves
/// who is calling.
/// </summary>
public enum PrincipalSubmissionError
{
    CredentialPaused,
    EnvelopeExceeded,
    IdempotencyKeyConflict,
    OperationConflict,

    /// <summary>
    /// The body named a Principal other than the one the credential
    /// authenticated. Refused, never redirected (FRD-09).
    /// </summary>
    PrincipalMismatch
}

public sealed class PrincipalSubmissionException(PrincipalSubmissionError error)
    : Exception("The Principal submission could not be completed.")
{
    public PrincipalSubmissionError Error { get; } = error;
}

/// <summary>
/// One submitted file. <paramref name="Role"/> is what the Principal says the
/// file is; it is optional, and when absent nothing is inferred — the file is
/// retained as an ordinary attachment (operator decision, 2026-08-28).
/// </summary>
public sealed record PrincipalSubmissionFile(
    int Ordinal,
    string FileName,
    string MediaType,
    ReadOnlyMemory<byte> Content,
    DocumentSemanticRole? Role = null);

/// <summary>
/// One submission. <paramref name="Instruction"/> is what the Principal
/// declared; <paramref name="RawBody"/> is the request exactly as it arrived and
/// is what Pegasus retains as the source, so the case's origin is the
/// Principal's own words rather than a rendering of them.
/// </summary>
public sealed record PrincipalSubmissionRequest(
    PrincipalCredentialAuthentication Credential,
    string IdempotencyKey,
    PrincipalInstruction Instruction,
    IReadOnlyList<PrincipalSubmissionFile> Files,
    ReadOnlyMemory<byte> RawBody,
    string CorrelationId);

/// <summary>
/// The durable submission row. It is the Principal binding processing reads
/// (<see cref="IPrincipalSubmissionBindings"/>) and the idempotency record a
/// replay resolves to; the files themselves are the intake submission group
/// whose token is <see cref="Id"/>.
/// </summary>
public sealed record PrincipalSubmissionRecord(
    Guid Id,
    Guid PrincipalId,
    string KeyId,
    string IdempotencyKey,
    string? PrincipalReference,
    DateTimeOffset ReceivedAtUtc,
    string BodySha256,
    PrincipalInstruction? Instruction = null,
    Guid? StagedReceiptId = null);

/// <summary>
/// A submission whose accept writes stopped part-way and can still be
/// completed. Its source is durably retained, so
/// <paramref name="RetainedStagedReceiptId"/> is the receipt the interrupted
/// accept would have written back; a bare reservation, whose retention never
/// happened, is not a candidate at all.
/// </summary>
public sealed record PrincipalSubmissionAcceptCandidate(
    Guid SubmissionId,
    Guid PrincipalId,
    DateTimeOffset ReceivedAtUtc,
    Guid? StagedReceiptId,
    Guid RetainedStagedReceiptId,
    bool HasAcceptedHistory);

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

public sealed record PrincipalSubmissionAcceptedFile(
    int Ordinal,
    string FileName,
    string Sha256,
    bool IsDuplicate);

/// <summary>
/// Returned the moment the envelope is durably received. It says nothing
/// about processing (operator decision): the result is
/// read separately.
/// </summary>
public sealed record PrincipalSubmissionReceipt(
    Guid SubmissionId,
    DateTimeOffset ReceivedAtUtc,
    string? PrincipalReference,
    IReadOnlyList<PrincipalSubmissionAcceptedFile> Files,
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
    /// Inserts the submission row. A second row for the same Principal and
    /// idempotency key is refused with
    /// <see cref="PrincipalSubmissionError.OperationConflict"/>; the caller
    /// re-reads and treats it as a replay.
    /// </summary>
    Task CreateAsync(PrincipalSubmissionRecord record, CancellationToken cancellationToken);

    Task<PrincipalSubmissionRecord?> FindByIdempotencyKeyAsync(
        Guid principalId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<PrincipalSubmissionRecord?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The authenticated Principal's code, or null when it no longer exists or
    /// is not active. The credential proves which Principal is calling; this is
    /// how the submission learns what that Principal is called, so the declared
    /// value can be checked against it.
    /// </summary>
    Task<string?> FindPrincipalCodeAsync(Guid principalId, CancellationToken cancellationToken);

    /// <summary>
    /// Records which staged receipt the submission was retained as, so reading
    /// its result is one indexed read of our own row rather than a search of the
    /// intake work queue by source identity. Setting it twice is a no-op.
    /// </summary>
    Task RecordStagedReceiptAsync(
        Guid submissionId,
        Guid stagedReceiptId,
        CancellationToken cancellationToken);

    /// <summary>
    /// The oldest submissions whose accept writes are incomplete and whose
    /// intake retention already exists, so every row returned can be
    /// completed. A bare reservation is excluded by the query itself rather
    /// than skipped by the sweep: nothing ever removes one, so a bounded
    /// oldest-first window that admitted them would fill with rows it can
    /// never repair and starve the ones it can.
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
    public const int MaximumPrincipalReferenceLength = 200;
    public const int MaximumFileNameLength = 260;
    public const int MaximumMediaTypeLength = 200;
    public const string ActionHistoryAggregateType = "PrincipalSubmission";

    public static ActionActor Actor(PrincipalCredentialAuthentication credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        return ActionActor.Principal(credential.PrincipalId);
    }

    public static void RequireMaySubmit(PrincipalCredentialAuthentication credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        if (!credential.MaySubmit)
        {
            throw new PrincipalSubmissionException(PrincipalSubmissionError.CredentialPaused);
        }
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
    public static IReadOnlyList<PrincipalSubmissionFile> RequireEnvelope(
        IReadOnlyList<PrincipalSubmissionFile>? files)
    {
        if (files is null)
        {
            return [];
        }
        if (files.Count > IntakeEnvelopeLimits.MaximumBatchFileCount
            || files.Any(file => file.Content.Length > IntakeEnvelopeLimits.MaximumPrincipalApiFileLength)
            || files.Sum(file => (long)file.Content.Length)
                > IntakeEnvelopeLimits.MaximumPrincipalApiEnvelopeLength)
        {
            throw new PrincipalSubmissionException(PrincipalSubmissionError.EnvelopeExceeded);
        }

        var ordered = files.OrderBy(file => file.Ordinal).ToArray();
        for (var index = 0; index < ordered.Length; index++)
        {
            var file = ordered[index];
            if (file.Ordinal != index)
            {
                throw new ArgumentException("File ordinals must be contiguous from zero.", nameof(files));
            }

            var safeFileName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(safeFileName)
                || safeFileName.Length > MaximumFileNameLength
                || !string.Equals(safeFileName, file.FileName, StringComparison.Ordinal))
            {
                throw new ArgumentException("A leaf file name is required.", nameof(files));
            }
            if (string.IsNullOrWhiteSpace(file.MediaType) || file.MediaType.Length > MaximumMediaTypeLength)
            {
                throw new ArgumentException("A media type is required.", nameof(files));
            }
            if (file.Content.IsEmpty)
            {
                throw new ArgumentException("An empty file cannot be submitted.", nameof(files));
            }
        }

        return ordered;
    }

    /// <summary>
    /// The request body Pegasus retains as the source. It is bounded on its own
    /// because it carries the files inline and is held whole to be decoded.
    /// </summary>
    public static void RequireRetainableBody(ReadOnlyMemory<byte> body)
    {
        if (body.IsEmpty)
        {
            throw new ArgumentException("The submitted request body is required.", nameof(body));
        }
        if (body.Length > IntakeEnvelopeLimits.MaximumPrincipalApiRequestLength)
        {
            throw new PrincipalSubmissionException(PrincipalSubmissionError.EnvelopeExceeded);
        }
    }

    /// <summary>
    /// An original report is optional, even for an Audit (operator,
    /// 2026-09-28): an Audit sent without one waits on Original report
    /// missing until the report arrives. At most one file may claim the role.
    /// Two would both take the fixed <c>principal-original-report</c> label,
    /// and the single-match lookup downstream would then fail the whole
    /// accepted intake instead of telling the Principal which field was wrong.
    /// </summary>
    public static void RequireAtMostOneOriginalReport(IReadOnlyList<PrincipalSubmissionFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (files.Count(file => file.Role == DocumentSemanticRole.AuditReport) > 1)
        {
            throw new PrincipalInstructionValidationException(
                "files",
                "A submission may attach at most one original report, with its role stated as "
                + $"'{PrincipalFileRoles.OriginalReport}'.");
        }
    }

    /// <summary>
    /// The accepted files as the receipt reports them. A file repeated inside
    /// one envelope is named as the duplicate it is rather than silently
    /// counted twice.
    /// </summary>
    public static IReadOnlyList<PrincipalSubmissionAcceptedFile> AcceptedFiles(
        IReadOnlyList<PrincipalSubmissionFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return files
            .OrderBy(file => file.Ordinal)
            .Select(file =>
            {
                var hash = Sha256(file.Content);
                return new PrincipalSubmissionAcceptedFile(
                    file.Ordinal,
                    file.FileName,
                    hash,
                    !seen.Add(hash));
            })
            .ToArray();
    }

    public static string SubmissionToken(Guid submissionId) => submissionId.ToString("N");

    public static string OperationKey(Guid submissionId) => $"principal-submission:{submissionId:N}";

    /// <summary>
    /// The identity of a submission's one <c>Accepted</c> history row, derived
    /// from its operation key rather than minted per write. The inline request
    /// and the recovery sweep both record that acceptance under this id, so
    /// whichever writes second is refused instead of both landing in permanent
    /// history. Every other history row is its own event and keeps its own id.
    /// </summary>
    public static Guid AcceptedHistoryId(Guid submissionId)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(OperationKey(submissionId)), hash);
        return new Guid(hash[..16]);
    }

    public static string Sha256(ReadOnlyMemory<byte> content) =>
        Convert.ToHexString(SHA256.HashData(content.Span));
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
        ArgumentNullException.ThrowIfNull(request.Instruction);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CorrelationId);
        var actor = PrincipalSubmissionPolicy.Actor(request.Credential);
        StaffAuthorization.Require(actor, StaffAccessRight.SubmitPrincipalInstruction);
        PrincipalSubmissionPolicy.RequireMaySubmit(request.Credential);

        var idempotencyKey = PrincipalSubmissionPolicy.NormalizeIdempotencyKey(request.IdempotencyKey);
        var instruction = PrincipalInstructionPolicy.Normalize(request.Instruction);
        var files = PrincipalSubmissionPolicy.RequireEnvelope(request.Files);
        PrincipalSubmissionPolicy.RequireRetainableBody(request.RawBody);
        PrincipalSubmissionPolicy.RequireAtMostOneOriginalReport(files);
        var bodySha256 = PrincipalSubmissionPolicy.Sha256(request.RawBody);
        var principalId = request.Credential.PrincipalId;

        // The credential establishes the Principal. A body that names a
        // different one is refused rather than honoured: FRD-09 is explicit that
        // content never selects a Principal, so the field can only ever catch a
        // Principal posting to the wrong account.
        var principalCode = await store.FindPrincipalCodeAsync(principalId, cancellationToken)
            ?? throw new PrincipalSubmissionException(PrincipalSubmissionError.CredentialPaused);
        if (!PrincipalInstructionPolicy.DeclaredPrincipalMatches(instruction, principalCode))
        {
            throw new PrincipalSubmissionException(PrincipalSubmissionError.PrincipalMismatch);
        }
        await attachmentAdmission.RequireSupportedAsync(files, cancellationToken);

        var existing = await store.FindByIdempotencyKeyAsync(principalId, idempotencyKey, cancellationToken);
        if (existing is null)
        {
            var record = new PrincipalSubmissionRecord(
                Guid.NewGuid(),
                principalId,
                request.Credential.KeyId,
                idempotencyKey,
                instruction.ClaimNumber,
                timeProvider.GetUtcNow(),
                bodySha256,
                instruction);
            try
            {
                await store.CreateAsync(record, cancellationToken);
                existing = record;
            }
            catch (PrincipalSubmissionException conflict)
                when (conflict.Error == PrincipalSubmissionError.OperationConflict)
            {
                // A concurrent request with the same key won the insert; it is
                // now a replay of that request, resolved below.
                existing = await store.FindByIdempotencyKeyAsync(principalId, idempotencyKey, cancellationToken)
                    ?? throw new PrincipalSubmissionException(PrincipalSubmissionError.OperationConflict);
            }
        }

        if (!string.Equals(existing.BodySha256, bodySha256, StringComparison.Ordinal))
        {
            await actionHistory.AppendAsync(
                SubmissionHistory(Guid.NewGuid(), actor, existing.Id, "Refused", request.CorrelationId,
                    "The idempotency key was reused with a different submission."),
                cancellationToken);
            throw new PrincipalSubmissionException(PrincipalSubmissionError.IdempotencyKeyConflict);
        }

        // One submission is one receipt, and the retained source is the request
        // as it arrived — the Principal's own instruction, carrying its files
        // exactly as an e-mail carries its attachments. Retaining each file as
        // its own receipt instead would scatter one instruction across many, and
        // an Audit could not then find its original report on its own receipt.
        ReceivedIntake received;
        try
        {
            received = await intakeSubmission.ExecuteAsync(
                new(
                    PrincipalInstructionPolicy.SourceFileName,
                    PrincipalInstructionPolicy.SourceMediaType,
                    request.RawBody,
                    existing.ReceivedAtUtc,
                    MailClassificationActor.Format(actor),
                    new(
                        IntakeSourceChannel.PrincipalApi,
                        PrincipalSubmissionPolicy.SubmissionToken(existing.Id))),
                PrincipalSubmissionPolicy.OperationKey(existing.Id),
                cancellationToken);
        }
        catch (IntakeSourceIdentityConflictException)
        {
            await actionHistory.AppendAsync(
                SubmissionHistory(Guid.NewGuid(), actor, existing.Id, "Refused", request.CorrelationId,
                    "The idempotency key was reused with a different submission."),
                cancellationToken);
            throw new PrincipalSubmissionException(PrincipalSubmissionError.IdempotencyKeyConflict);
        }

        await store.RecordStagedReceiptAsync(existing.Id, received.StagedReceiptId, cancellationToken);
        if (received.IsDuplicate)
        {
            // Each replay is an event in its own right, so it keeps its own id
            // and its own request correlation.
            await actionHistory.AppendAsync(
                SubmissionHistory(Guid.NewGuid(), actor, existing.Id, "Replayed", request.CorrelationId, null),
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
                    PrincipalSubmissionPolicy.AcceptedHistoryId(existing.Id),
                    actor,
                    existing.Id,
                    "Accepted",
                    request.CorrelationId,
                    null),
                cancellationToken);
        }
        return new(
            existing.Id,
            existing.ReceivedAtUtc,
            existing.PrincipalReference,
            PrincipalSubmissionPolicy.AcceptedFiles(files),
            received.IsDuplicate);
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
        if (submissionId == Guid.Empty)
        {
            return null;
        }

        var record = await store.GetAsync(submissionId, cancellationToken);
        if (record is null || record.PrincipalId != credential.PrincipalId)
        {
            return null;
        }

        var status = record.StagedReceiptId is { } stagedReceiptId
            ? await statusQueries.GetAsync(stagedReceiptId, cancellationToken)
            : null;
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
            record.PrincipalReference,
            status?.Status ?? QueuedIntakeStatusKind.Received,
            receipt?.Decision,
            receipt?.AllocationState?.FailureKind,
            receipt?.FailureCode ?? status?.FailureCode,
            caseReference);
    }
}
