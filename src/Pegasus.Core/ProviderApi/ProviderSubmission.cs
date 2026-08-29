using System.Security.Cryptography;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.ProviderApi;

/// <summary>
/// API-01 (FRD-09 § Provider API principal and contract boundary; ADR-0004):
/// an authenticated Principal submits one instruction envelope — one or more
/// files — that enters the ordinary grouped durable intake path bound to
/// that Principal, and reads back only its own submission's receipt and
/// result. The Provider actor is the Principal; the credential only proves
/// who is calling.
/// </summary>
public enum ProviderSubmissionError
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

public sealed class ProviderSubmissionException(ProviderSubmissionError error)
    : Exception("The provider submission could not be completed.")
{
    public ProviderSubmissionError Error { get; } = error;
}

/// <summary>
/// One submitted file. <paramref name="Role"/> is what the provider says the
/// file is; it is optional, and when absent nothing is inferred — the file is
/// retained as an ordinary attachment (operator decision, 2026-08-28).
/// </summary>
public sealed record ProviderSubmissionFile(
    int Ordinal,
    string FileName,
    string MediaType,
    ReadOnlyMemory<byte> Content,
    DocumentSemanticRole? Role = null);

/// <summary>
/// One submission. <paramref name="Instruction"/> is what the provider
/// declared; <paramref name="RawBody"/> is the request exactly as it arrived and
/// is what Pegasus retains as the source, so the case's origin is the
/// provider's own words rather than a rendering of them.
/// </summary>
public sealed record ProviderSubmissionRequest(
    PrincipalCredentialAuthentication Credential,
    string IdempotencyKey,
    ProviderInstruction Instruction,
    IReadOnlyList<ProviderSubmissionFile> Files,
    ReadOnlyMemory<byte> RawBody,
    string CorrelationId);

/// <summary>
/// The durable submission row. It is the Principal binding processing reads
/// (<see cref="IProviderSubmissionBindings"/>) and the idempotency record a
/// replay resolves to; the files themselves are the intake submission group
/// whose token is <see cref="Id"/>.
/// </summary>
public sealed record ProviderSubmissionRecord(
    Guid Id,
    Guid PrincipalId,
    string KeyId,
    string IdempotencyKey,
    string? ProviderReference,
    DateTimeOffset ReceivedAtUtc,
    ProviderInstruction? Instruction = null,
    Guid? StagedReceiptId = null);

public sealed record ProviderSubmissionAcceptCandidate(
    Guid SubmissionId,
    Guid PrincipalId,
    DateTimeOffset ReceivedAtUtc,
    Guid? StagedReceiptId,
    bool HasAcceptedHistory);

/// <summary>
/// What intake needs to know about the submission a source belongs to: which
/// Principal it was bound to and what that Principal declared. Both come from
/// the retained submission row, never from the submitted content.
/// </summary>
public sealed record ProviderSubmissionBinding(
    Guid SubmissionId,
    Guid PrincipalId,
    string PrincipalCode,
    ProviderInstruction Instruction);

public sealed record ProviderSubmissionAcceptedFile(
    int Ordinal,
    string FileName,
    string Sha256,
    bool IsDuplicate);

/// <summary>
/// Returned the moment the envelope is durably received. It says nothing
/// about processing (operator decision, TICK-059 retired): the result is
/// read separately.
/// </summary>
public sealed record ProviderSubmissionReceipt(
    Guid SubmissionId,
    DateTimeOffset ReceivedAtUtc,
    string? ProviderReference,
    IReadOnlyList<ProviderSubmissionAcceptedFile> Files,
    bool Replayed);

/// <summary>
/// The submission's own result: the Case/PO once processing allocated one,
/// otherwise the intake pipeline's own decision and failure vocabulary (never a
/// provider-only list). One submission is one receipt, so there is one outcome
/// rather than a per-file table.
/// </summary>
public sealed record ProviderSubmissionResult(
    Guid SubmissionId,
    DateTimeOffset ReceivedAtUtc,
    string? ProviderReference,
    QueuedIntakeStatusKind Status,
    IntakeDecision? Decision,
    IntakeAllocationFailureKind? AllocationFailure,
    string? FailureCode,
    string? CaseReference);

public interface IProviderSubmissionStore
{
    /// <summary>
    /// Inserts the submission row. A second row for the same Principal and
    /// idempotency key is refused with
    /// <see cref="ProviderSubmissionError.OperationConflict"/>; the caller
    /// re-reads and treats it as a replay.
    /// </summary>
    Task CreateAsync(ProviderSubmissionRecord record, CancellationToken cancellationToken);

    Task<ProviderSubmissionRecord?> FindByIdempotencyKeyAsync(
        Guid principalId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<ProviderSubmissionRecord?> GetAsync(Guid id, CancellationToken cancellationToken);

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

    Task<IReadOnlyList<ProviderSubmissionAcceptCandidate>> ListAcceptRecoveryCandidatesAsync(
        int maximumItems,
        CancellationToken cancellationToken);

    Task<ProviderSubmissionAcceptCandidate?> GetAcceptRecoveryCandidateAsync(
        Guid submissionId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Read by <c>ProcessIntake</c> for a <see cref="IntakeSourceChannel.ProviderApi"/>
/// source: the Principal code the submission was bound to, or null when the
/// source belongs to no retained submission.
/// </summary>
public interface IProviderSubmissionBindings
{
    Task<ProviderSubmissionBinding?> FindAsync(
        IntakeSourceIdentity sourceIdentity,
        CancellationToken cancellationToken);
}

public interface ISubmitProviderInstruction
{
    Task<ProviderSubmissionReceipt> ExecuteAsync(
        ProviderSubmissionRequest request,
        CancellationToken cancellationToken);
}

public interface IGetProviderSubmissionResult
{
    /// <summary>
    /// Null when the submission does not exist or belongs to another
    /// Principal — the two are indistinguishable to the caller (FRD-09:
    /// cross-principal disclosure fails closed).
    /// </summary>
    Task<ProviderSubmissionResult?> ExecuteAsync(
        PrincipalCredentialAuthentication credential,
        Guid submissionId,
        CancellationToken cancellationToken);
}

public static class ProviderSubmissionPolicy
{
    public const int MaximumIdempotencyKeyLength = 200;
    public const int MaximumProviderReferenceLength = 200;
    public const int MaximumFileNameLength = 260;
    public const int MaximumMediaTypeLength = 200;
    public const string ActionHistoryAggregateType = "ProviderSubmission";

    public static ActionActor Actor(PrincipalCredentialAuthentication credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        return ActionActor.Provider(credential.PrincipalId);
    }

    public static void RequireMaySubmit(PrincipalCredentialAuthentication credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        if (!credential.MaySubmit)
        {
            throw new ProviderSubmissionException(ProviderSubmissionError.CredentialPaused);
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

    public static string? NormalizeProviderReference(string? providerReference)
    {
        var normalized = providerReference?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }
        if (normalized.Length > MaximumProviderReferenceLength)
        {
            throw new ArgumentException(
                $"A provider reference is at most {MaximumProviderReferenceLength} characters.",
                nameof(providerReference));
        }

        return normalized;
    }

    /// <summary>
    /// The envelope bound is the Provider API's own
    /// (<see cref="IntakeEnvelopeLimits.MaximumProviderApiEnvelopeLength"/>):
    /// every file arrives inline as base64 in one request body, so the whole
    /// submission is bounded together rather than only file by file.
    /// </summary>
    public static IReadOnlyList<ProviderSubmissionFile> RequireEnvelope(
        IReadOnlyList<ProviderSubmissionFile>? files)
    {
        if (files is null || files.Count == 0)
        {
            throw new ArgumentException("At least one file is required.", nameof(files));
        }
        if (files.Count > IntakeEnvelopeLimits.MaximumBatchFileCount
            || files.Any(file => file.Content.Length > IntakeEnvelopeLimits.MaximumContentLength)
            || files.Sum(file => (long)file.Content.Length)
                > IntakeEnvelopeLimits.MaximumProviderApiEnvelopeLength)
        {
            throw new ProviderSubmissionException(ProviderSubmissionError.EnvelopeExceeded);
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
        if (body.Length > IntakeEnvelopeLimits.MaximumProviderApiRequestLength)
        {
            throw new ProviderSubmissionException(ProviderSubmissionError.EnvelopeExceeded);
        }
    }

    /// <summary>
    /// An Audit needs its original report attached whoever states its outcome.
    /// The operator ruled on 2026-08-28 that the declared verdict decides the
    /// reference prefix, which settles who decides — not whether the Engineer
    /// receives the report they are auditing.
    /// </summary>
    public static void RequireOriginalReport(
        ProviderInstructionKind kind,
        IReadOnlyList<ProviderSubmissionFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (!ProviderInstructionKinds.RequiresOriginalReport(kind))
        {
            return;
        }
        // Exactly one, not at least one. Two files claiming the role both take
        // the fixed `provider-original-report` label, and the single-match
        // lookup downstream then fails the whole accepted intake instead of
        // telling the provider which field was wrong.
        if (files.Count(file => file.Role == DocumentSemanticRole.AuditReport) != 1)
        {
            throw new ProviderInstructionValidationException(
                "files",
                "An Audit submission must attach exactly one original report, with its role stated as "
                + $"'{ProviderFileRoles.OriginalReport}'.");
        }
    }

    /// <summary>
    /// The accepted files as the receipt reports them. A file repeated inside
    /// one envelope is named as the duplicate it is rather than silently
    /// counted twice.
    /// </summary>
    public static IReadOnlyList<ProviderSubmissionAcceptedFile> AcceptedFiles(
        IReadOnlyList<ProviderSubmissionFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return files
            .OrderBy(file => file.Ordinal)
            .Select(file =>
            {
                var hash = Sha256(file.Content);
                return new ProviderSubmissionAcceptedFile(
                    file.Ordinal,
                    file.FileName,
                    hash,
                    !seen.Add(hash));
            })
            .ToArray();
    }

    public static string SubmissionToken(Guid submissionId) => submissionId.ToString("N");

    public static string OperationKey(Guid submissionId) => $"provider-submission:{submissionId:N}";

    public static string Sha256(ReadOnlyMemory<byte> content) =>
        Convert.ToHexString(SHA256.HashData(content.Span));
}

public sealed class SubmitProviderInstruction(
    IProviderSubmissionStore store,
    IIntakeSubmission intakeSubmission,
    IActionHistoryWriter actionHistory,
    TimeProvider timeProvider) : ISubmitProviderInstruction
{
    public async Task<ProviderSubmissionReceipt> ExecuteAsync(
        ProviderSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Credential);
        ArgumentNullException.ThrowIfNull(request.Instruction);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CorrelationId);
        var actor = ProviderSubmissionPolicy.Actor(request.Credential);
        StaffAuthorization.Require(actor, StaffAccessRight.SubmitProviderInstruction);
        ProviderSubmissionPolicy.RequireMaySubmit(request.Credential);

        var idempotencyKey = ProviderSubmissionPolicy.NormalizeIdempotencyKey(request.IdempotencyKey);
        var instruction = ProviderInstructionPolicy.Normalize(request.Instruction);
        var files = ProviderSubmissionPolicy.RequireEnvelope(request.Files);
        ProviderSubmissionPolicy.RequireRetainableBody(request.RawBody);
        ProviderSubmissionPolicy.RequireOriginalReport(instruction.Kind, files);
        var principalId = request.Credential.PrincipalId;

        // The credential establishes the Principal. A body that names a
        // different one is refused rather than honoured: FRD-09 is explicit that
        // content never selects a Principal, so the field can only ever catch a
        // provider posting to the wrong account.
        var principalCode = await store.FindPrincipalCodeAsync(principalId, cancellationToken)
            ?? throw new ProviderSubmissionException(ProviderSubmissionError.CredentialPaused);
        if (!ProviderInstructionPolicy.DeclaredPrincipalMatches(instruction, principalCode))
        {
            throw new ProviderSubmissionException(ProviderSubmissionError.PrincipalMismatch);
        }

        var existing = await store.FindByIdempotencyKeyAsync(principalId, idempotencyKey, cancellationToken);
        if (existing is null)
        {
            var record = new ProviderSubmissionRecord(
                Guid.NewGuid(),
                principalId,
                request.Credential.KeyId,
                idempotencyKey,
                instruction.ClaimNumber,
                timeProvider.GetUtcNow(),
                instruction);
            try
            {
                await store.CreateAsync(record, cancellationToken);
                existing = record;
            }
            catch (ProviderSubmissionException conflict)
                when (conflict.Error == ProviderSubmissionError.OperationConflict)
            {
                // A concurrent request with the same key won the insert; it is
                // now a replay of that request, resolved below.
                existing = await store.FindByIdempotencyKeyAsync(principalId, idempotencyKey, cancellationToken)
                    ?? throw new ProviderSubmissionException(ProviderSubmissionError.OperationConflict);
            }
        }

        // One submission is one receipt, and the retained source is the request
        // as it arrived — the provider's own instruction, carrying its files
        // exactly as an e-mail carries its attachments. Retaining each file as
        // its own receipt instead would scatter one instruction across many, and
        // an Audit could not then find its original report on its own receipt.
        ReceivedIntake received;
        try
        {
            received = await intakeSubmission.ExecuteAsync(
                new(
                    ProviderInstructionPolicy.SourceFileName,
                    ProviderInstructionPolicy.SourceMediaType,
                    request.RawBody,
                    existing.ReceivedAtUtc,
                    MailClassificationActor.Format(actor),
                    new(
                        IntakeSourceChannel.ProviderApi,
                        ProviderSubmissionPolicy.SubmissionToken(existing.Id))),
                ProviderSubmissionPolicy.OperationKey(existing.Id),
                cancellationToken);
        }
        catch (IntakeSourceIdentityConflictException)
        {
            await AppendHistoryAsync(actor, existing.Id, "Refused", request.CorrelationId,
                "The idempotency key was reused with a different submission.", cancellationToken);
            throw new ProviderSubmissionException(ProviderSubmissionError.IdempotencyKeyConflict);
        }

        await store.RecordStagedReceiptAsync(existing.Id, received.StagedReceiptId, cancellationToken);
        await AppendHistoryAsync(
            actor,
            existing.Id,
            received.IsDuplicate ? "Replayed" : "Accepted",
            request.CorrelationId,
            null,
            cancellationToken);
        return new(
            existing.Id,
            existing.ReceivedAtUtc,
            existing.ProviderReference,
            ProviderSubmissionPolicy.AcceptedFiles(files),
            received.IsDuplicate);
    }

    private Task AppendHistoryAsync(
        ActionActor actor,
        Guid submissionId,
        string outcome,
        string correlationId,
        string? reason,
        CancellationToken cancellationToken) =>
        actionHistory.AppendAsync(
            new ActionHistoryEntry(
                Guid.NewGuid(),
                ProviderSubmissionPolicy.ActionHistoryAggregateType,
                submissionId.ToString("D"),
                "Submitted",
                actor,
                timeProvider.GetUtcNow(),
                outcome,
                correlationId,
                reason),
            cancellationToken);
}

public sealed class GetProviderSubmissionResult(
    IProviderSubmissionStore store,
    IQueuedIntakeStatusQueries statusQueries,
    IIntakeReceiptQueries receiptQueries) : IGetProviderSubmissionResult
{
    public async Task<ProviderSubmissionResult?> ExecuteAsync(
        PrincipalCredentialAuthentication credential,
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        var actor = ProviderSubmissionPolicy.Actor(credential);
        // A paused credential still reads its own receipts and results
        // (operator decision, TICK-061); only MaySubmit is withheld.
        StaffAuthorization.Require(actor, StaffAccessRight.SubmitProviderInstruction);
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
        return new(
            record.Id,
            record.ReceivedAtUtc,
            record.ProviderReference,
            status?.Status ?? QueuedIntakeStatusKind.Received,
            receipt?.Decision,
            receipt?.AllocationState?.FailureKind,
            receipt?.FailureCode ?? status?.FailureCode,
            receipt?.CurrentCaseReference);
    }
}
