using System.Security.Cryptography;
using Pegasus.Core.Cases;
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
    OperationConflict
}

public sealed class ProviderSubmissionException(ProviderSubmissionError error)
    : Exception("The provider submission could not be completed.")
{
    public ProviderSubmissionError Error { get; } = error;
}

public sealed record ProviderSubmissionFile(
    int Ordinal,
    string FileName,
    string MediaType,
    ReadOnlyMemory<byte> Content);

public sealed record ProviderSubmissionRequest(
    PrincipalCredentialAuthentication Credential,
    string IdempotencyKey,
    string? ProviderReference,
    IReadOnlyList<ProviderSubmissionFile> Files,
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
    DateTimeOffset ReceivedAtUtc);

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

public sealed record ProviderSubmissionFileResult(
    int Ordinal,
    string FileName,
    QueuedIntakeStatusKind Status,
    IntakeDecision? Decision,
    IntakeAllocationFailureKind? AllocationFailure,
    string? FailureCode,
    string? CaseReference);

/// <summary>
/// The submission's own result: the Case/PO reference once one file's
/// processing allocated it, otherwise the precise per-file decision and
/// failure in the intake pipeline's own vocabulary (no provider-only list).
/// </summary>
public sealed record ProviderSubmissionResult(
    Guid SubmissionId,
    DateTimeOffset ReceivedAtUtc,
    string? ProviderReference,
    QueuedIntakeStatusKind Status,
    string? CaseReference,
    IReadOnlyList<ProviderSubmissionFileResult> Files);

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
}

/// <summary>
/// Read by <c>ProcessIntake</c> for a <see cref="IntakeSourceChannel.ProviderApi"/>
/// source: the Principal code the submission was bound to, or null when the
/// source belongs to no retained submission.
/// </summary>
public interface IProviderSubmissionBindings
{
    Task<string?> FindPrincipalCodeAsync(
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
    /// The envelope bound is the staff Upload bound (<see cref="IntakeEnvelopeLimits"/>):
    /// the same file count and per-file size, so a provider is capped
    /// exactly where a staff member reproducing the job manually is.
    /// </summary>
    public static IReadOnlyList<ProviderSubmissionFile> RequireEnvelope(
        IReadOnlyList<ProviderSubmissionFile>? files)
    {
        if (files is null || files.Count == 0)
        {
            throw new ArgumentException("At least one file is required.", nameof(files));
        }
        if (files.Count > IntakeEnvelopeLimits.MaximumBatchFileCount
            || files.Any(file => file.Content.Length > IntakeEnvelopeLimits.MaximumContentLength))
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

    public static string SubmissionToken(Guid submissionId) => submissionId.ToString("N");

    public static string Sha256(ReadOnlyMemory<byte> content) =>
        Convert.ToHexString(SHA256.HashData(content.Span));
}

public sealed class SubmitProviderInstruction(
    IProviderSubmissionStore store,
    IGroupedIntakeSubmission groupedSubmission,
    IIntakeSubmissionGroupStore groupStore,
    IActionHistoryWriter actionHistory,
    TimeProvider timeProvider) : ISubmitProviderInstruction
{
    public async Task<ProviderSubmissionReceipt> ExecuteAsync(
        ProviderSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Credential);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CorrelationId);
        var actor = ProviderSubmissionPolicy.Actor(request.Credential);
        StaffAuthorization.Require(actor, StaffAccessRight.SubmitProviderInstruction);
        if (!request.Credential.MaySubmit)
        {
            throw new ProviderSubmissionException(ProviderSubmissionError.CredentialPaused);
        }

        var idempotencyKey = ProviderSubmissionPolicy.NormalizeIdempotencyKey(request.IdempotencyKey);
        var providerReference = ProviderSubmissionPolicy.NormalizeProviderReference(request.ProviderReference);
        var files = ProviderSubmissionPolicy.RequireEnvelope(request.Files);
        var principalId = request.Credential.PrincipalId;

        var existing = await store.FindByIdempotencyKeyAsync(principalId, idempotencyKey, cancellationToken);
        if (existing is null)
        {
            var record = new ProviderSubmissionRecord(
                Guid.NewGuid(),
                principalId,
                request.Credential.KeyId,
                idempotencyKey,
                providerReference,
                timeProvider.GetUtcNow());
            try
            {
                await store.CreateAsync(record, cancellationToken);
                existing = record;
            }
            catch (ProviderSubmissionException conflict)
                when (conflict.Error == ProviderSubmissionError.OperationConflict)
            {
                // A concurrent request with the same key won the insert; it
                // is now a replay of that request, resolved below.
                existing = await store.FindByIdempotencyKeyAsync(principalId, idempotencyKey, cancellationToken)
                    ?? throw new ProviderSubmissionException(ProviderSubmissionError.OperationConflict);
            }
        }

        var submissionToken = ProviderSubmissionPolicy.SubmissionToken(existing.Id);
        var replayed = await groupStore.FindAsync(
            IntakeSourceChannel.ProviderApi,
            submissionToken,
            cancellationToken) is not null;

        GroupedIntakeSubmissionResult grouped;
        try
        {
            // The grouped owner dedups each member by content under the
            // submission's own token, so a replay carrying identical files
            // returns the same receipt and a different envelope under the
            // same key fails closed.
            grouped = await groupedSubmission.ExecuteAsync(
                new(
                    submissionToken,
                    MailClassificationActor.Format(actor),
                    existing.ReceivedAtUtc,
                    files.Select(file => new GroupedIntakeFile(
                            file.Ordinal,
                            new IntakeSource(
                                file.FileName,
                                file.MediaType,
                                file.Content,
                                existing.ReceivedAtUtc,
                                MailClassificationActor.Format(actor),
                                new(IntakeSourceChannel.ProviderApi, submissionToken))))
                        .ToArray(),
                    IntakeSourceChannel.ProviderApi),
                cancellationToken);
        }
        catch (IntakeSourceIdentityConflictException)
        {
            await AppendHistoryAsync(actor, existing.Id, "Refused", request.CorrelationId,
                "The idempotency key was reused with different content.", cancellationToken);
            throw new ProviderSubmissionException(ProviderSubmissionError.IdempotencyKeyConflict);
        }
        if (replayed && grouped.Group.ExpectedMemberCount != files.Count)
        {
            await AppendHistoryAsync(actor, existing.Id, "Refused", request.CorrelationId,
                "The idempotency key was reused with a different file count.", cancellationToken);
            throw new ProviderSubmissionException(ProviderSubmissionError.IdempotencyKeyConflict);
        }

        await AppendHistoryAsync(
            actor,
            existing.Id,
            replayed ? "Replayed" : "Accepted",
            request.CorrelationId,
            null,
            cancellationToken);
        return new(
            existing.Id,
            existing.ReceivedAtUtc,
            existing.ProviderReference,
            grouped.Members
                .OrderBy(member => member.Ordinal)
                .Select(member => new ProviderSubmissionAcceptedFile(
                    member.Ordinal,
                    member.SourceFileName,
                    member.SourceHash,
                    member.IsDuplicate))
                .ToArray(),
            replayed);
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
    IIntakeSubmissionGroupStore groupStore,
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

        var group = await groupStore.FindAsync(
            IntakeSourceChannel.ProviderApi,
            ProviderSubmissionPolicy.SubmissionToken(record.Id),
            cancellationToken);
        var files = new List<ProviderSubmissionFileResult>();
        foreach (var member in (group?.Members ?? []).OrderBy(member => member.Ordinal))
        {
            var memberStatus = await statusQueries.GetAsync(member.StagedReceiptId, cancellationToken);
            var receipt = memberStatus?.ProcessedReceiptId is { } processedId
                ? await receiptQueries.GetAsync(processedId, cancellationToken)
                : null;
            files.Add(new(
                member.Ordinal,
                member.SourceFileName,
                memberStatus?.Status ?? QueuedIntakeStatusKind.Received,
                receipt?.Decision,
                receipt?.AllocationState?.FailureKind,
                receipt?.FailureCode ?? memberStatus?.FailureCode,
                receipt?.CurrentCaseReference));
        }

        var caseReference = files.Select(file => file.CaseReference).FirstOrDefault(reference => reference is not null);
        var status = files.Count == 0
            ? QueuedIntakeStatusKind.Received
            : files.Any(file => file.Status == QueuedIntakeStatusKind.Failed)
                ? QueuedIntakeStatusKind.Failed
                : files.All(file => file.Status == QueuedIntakeStatusKind.Complete)
                    ? QueuedIntakeStatusKind.Complete
                    : files.Any(file => file.Status == QueuedIntakeStatusKind.Processing)
                        ? QueuedIntakeStatusKind.Processing
                        : QueuedIntakeStatusKind.Received;
        return new(record.Id, record.ReceivedAtUtc, record.ProviderReference, status, caseReference, files);
    }
}
